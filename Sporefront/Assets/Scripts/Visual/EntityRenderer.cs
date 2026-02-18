// ============================================================================
// FILE: Visual/EntityRenderer.cs
// PURPOSE: Renders buildings, armies, and villager groups on the hex map
//          as colored programmatic shapes with type labels.
//          Uses differential updates to avoid destroy/recreate flicker.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class EntityRenderer : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private Dictionary<Guid, GameObject> entityVisuals = new Dictionary<Guid, GameObject>();
        private Dictionary<Guid, EntityRenderState> currentStates = new Dictionary<Guid, EntityRenderState>();

        // Shared meshes (created once)
        private Mesh diamondMesh;   // buildings
        private Mesh circleMesh;    // armies (8 segments)
        private Mesh smallCircleMesh; // villagers (6 segments)
        private Mesh triangleMesh;  // resource nodes
        private Mesh barMesh;       // progress/HP bars

        private Material sharedMaterial;

        private const float BuildingSize = 0.30f;  // ~30% of hex outer radius
        private const float ArmySize = 0.22f;      // ~22%
        private const float VillagerSize = 0.15f;   // ~15%
        private const float ResourceSize = 0.18f;   // ~18%
        private const float ZPosition = -0.05f;     // in front of tiles/borders/selection/paths

        // Building progress/HP bar constants (diagonal, aligned to hex bottom-right edge)
        private const float BarWidth = 0.45f;
        private const float BarHeight = 0.04f;
        private const float BarZ = -0.01f;
        // Hex bottom-right edge: corner[3] (0,-0.5) → corner[4] (0.866,-0.25)
        // Rotation angle ≈ 16.1° from horizontal
        private const float BarRotationAngle = 16.1f;
        // Container at edge midpoint offset back by BarWidth/2 along edge direction to center bar
        private static readonly Vector3 BarContainerPosition = new Vector3(0.217f, -0.437f, -0.01f);

        // Reusable collections to reduce GC
        private Dictionary<HexCoordinate, List<EntityPlacement>> entitiesPerTile =
            new Dictionary<HexCoordinate, List<EntityPlacement>>();
        private Dictionary<Guid, EntityRenderState> desiredStates = new Dictionary<Guid, EntityRenderState>();
        private List<Guid> toRemove = new List<Guid>();

        // Movement interpolation state
        private Dictionary<Guid, MovementInterpolationState> movementStates =
            new Dictionary<Guid, MovementInterpolationState>();
        private Dictionary<Guid, GameObject> timerLabels = new Dictionary<Guid, GameObject>();
        private HashSet<Guid> activelyMovingEntities = new HashSet<Guid>();

        // Building progress/HP bar state
        private Dictionary<Guid, BuildingBarVisuals> buildingBars = new Dictionary<Guid, BuildingBarVisuals>();

        // ================================================================
        // Initialization
        // ================================================================

        private void Awake()
        {
            CreateSharedMeshes();
            sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        // ================================================================
        // Public API
        // ================================================================

        public void UpdateEntities(GameState gameState)
        {
            // Compute what we want to show
            ComputeDesiredStates(gameState);

            // Remove entities that no longer exist
            toRemove.Clear();
            foreach (var kvp in currentStates)
            {
                if (!desiredStates.ContainsKey(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                if (entityVisuals.TryGetValue(id, out var go) && go != null)
                    Destroy(go);
                entityVisuals.Remove(id);
                currentStates.Remove(id);
                movementStates.Remove(id);
                timerLabels.Remove(id); // child GO destroyed with parent
                buildingBars.Remove(id); // child GOs destroyed with parent
            }

            // Update existing or create new
            foreach (var kvp in desiredStates)
            {
                var id = kvp.Key;
                var desired = kvp.Value;

                if (currentStates.TryGetValue(id, out var current))
                {
                    // Entity already exists — check if anything changed
                    if (!entityVisuals.TryGetValue(id, out var go) || go == null)
                    {
                        // GameObject was destroyed externally; recreate
                        entityVisuals.Remove(id);
                        currentStates.Remove(id);
                        CreateEntityVisual(desired);
                        continue;
                    }

                    bool changed = false;

                    // Update position if moved — skip snap for entities being interpolated
                    if (current.worldPosition != desired.worldPosition)
                    {
                        if (!movementStates.ContainsKey(id))
                            go.transform.position = desired.worldPosition;
                        changed = true;
                    }

                    // Update color if changed
                    if (current.color != desired.color)
                    {
                        var meshRenderer = go.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            var mpb = new MaterialPropertyBlock();
                            mpb.SetColor("_Color", desired.color);
                            meshRenderer.SetPropertyBlock(mpb);
                        }

                        // Update label color too
                        var labelTF = go.transform.Find("Label");
                        if (labelTF != null)
                        {
                            var textMesh = labelTF.GetComponent<TextMesh>();
                            if (textMesh != null)
                            {
                                float luminance = desired.color.r * 0.299f +
                                    desired.color.g * 0.587f + desired.color.b * 0.114f;
                                textMesh.color = luminance > 0.5f
                                    ? SporefrontColors.InkBlack : SporefrontColors.ParchmentLight;
                            }
                        }
                        changed = true;
                    }

                    if (changed)
                        currentStates[id] = desired;
                }
                else
                {
                    // New entity — create visual
                    CreateEntityVisual(desired);
                }
            }
        }

        // ================================================================
        // Desired State Computation
        // ================================================================

        private void ComputeDesiredStates(GameState gameState)
        {
            // Clear reusable collections
            entitiesPerTile.Clear();
            desiredStates.Clear();
            activelyMovingEntities.Clear();

            // Collect buildings
            foreach (var kvp in gameState.buildings)
            {
                var building = kvp.Value;
                if (building.state == BuildingState.Planning) continue;

                var coord = building.coordinate;
                if (!entitiesPerTile.ContainsKey(coord))
                    entitiesPerTile[coord] = new List<EntityPlacement>();

                Color color = GetOwnerColor(building.ownerID, gameState);
                string label = GetBuildingLabel(building.buildingType);

                entitiesPerTile[coord].Add(new EntityPlacement
                {
                    id = building.id,
                    type = EntityVisualType.Building,
                    color = color,
                    label = label
                });
            }

            // Collect armies
            foreach (var kvp in gameState.armies)
            {
                var army = kvp.Value;
                var coord = army.coordinate;
                if (!entitiesPerTile.ContainsKey(coord))
                    entitiesPerTile[coord] = new List<EntityPlacement>();

                Color color = GetOwnerColor(army.ownerID, gameState);

                entitiesPerTile[coord].Add(new EntityPlacement
                {
                    id = army.id,
                    type = EntityVisualType.Army,
                    color = color,
                    label = null
                });
            }

            // Collect villager groups
            foreach (var kvp in gameState.villagerGroups)
            {
                var group = kvp.Value;
                var coord = group.coordinate;
                if (!entitiesPerTile.ContainsKey(coord))
                    entitiesPerTile[coord] = new List<EntityPlacement>();

                Color color = GetOwnerColor(group.ownerID, gameState);

                entitiesPerTile[coord].Add(new EntityPlacement
                {
                    id = group.id,
                    type = EntityVisualType.Villager,
                    color = color,
                    label = null
                });
            }

            // Collect resource points
            foreach (var kvp in gameState.resourcePoints)
            {
                var resource = kvp.Value;

                // Skip depleted resources and carcasses
                if (resource.IsDepleted()) continue;
                if (resource.resourceType == ResourcePointType.DeerCarcass ||
                    resource.resourceType == ResourcePointType.BoarCarcass) continue;

                var coord = resource.coordinate;
                if (!entitiesPerTile.ContainsKey(coord))
                    entitiesPerTile[coord] = new List<EntityPlacement>();

                entitiesPerTile[coord].Add(new EntityPlacement
                {
                    id = resource.id,
                    type = EntityVisualType.Resource,
                    color = SporefrontColors.GetResourceColor(resource.resourceType),
                    label = GetResourceLabel(resource.resourceType)
                });
            }

            // Compute world positions with offset logic, populate desiredStates
            foreach (var kvp in entitiesPerTile)
            {
                var coord = kvp.Key;
                var placements = kvp.Value;
                Vector3 tileCenter = HexMetrics.HexToWorldPosition(coord);

                // Separate by type for offset calculation
                var buildings = new List<EntityPlacement>();
                var armies = new List<EntityPlacement>();
                var villagers = new List<EntityPlacement>();
                var resources = new List<EntityPlacement>();

                foreach (var p in placements)
                {
                    switch (p.type)
                    {
                        case EntityVisualType.Building: buildings.Add(p); break;
                        case EntityVisualType.Army: armies.Add(p); break;
                        case EntityVisualType.Villager: villagers.Add(p); break;
                        case EntityVisualType.Resource: resources.Add(p); break;
                    }
                }

                // Building at center
                foreach (var b in buildings)
                {
                    AddDesiredState(b, tileCenter, Vector2.zero);
                }

                // Resources at center when no building, offset lower-center when building present
                foreach (var r in resources)
                {
                    Vector2 offset = buildings.Count > 0
                        ? new Vector2(0f, -0.25f * HexMetrics.IsometricYScale)
                        : Vector2.zero;
                    AddDesiredState(r, tileCenter, offset);
                }

                // Armies offset upper-left / upper-right
                for (int i = 0; i < armies.Count; i++)
                {
                    float xOff = (i % 2 == 0) ? -0.3f : 0.3f;
                    float yOff = 0.3f * HexMetrics.IsometricYScale;
                    AddDesiredState(armies[i], tileCenter, new Vector2(xOff, yOff));
                }

                // Villagers offset lower-left / lower-right
                for (int i = 0; i < villagers.Count; i++)
                {
                    float xOff = (i % 2 == 0) ? -0.3f : 0.3f;
                    float yOff = -0.3f * HexMetrics.IsometricYScale;
                    AddDesiredState(villagers[i], tileCenter, new Vector2(xOff, yOff));
                }
            }

            // Second pass: override positions for moving entities with interpolated positions
            double now = Time.timeAsDouble;

            foreach (var kvp in gameState.armies)
            {
                var army = kvp.Value;
                if (army.currentPath != null && army.pathIndex < army.currentPath.Count &&
                    army.movementSpeed > 0 && desiredStates.ContainsKey(army.id))
                {
                    activelyMovingEntities.Add(army.id);
                    var desired = desiredStates[army.id];
                    var fromPos = desired.worldPosition; // tile-offset position at current coordinate
                    var nextTileCenter = HexMetrics.HexToWorldPosition(army.currentPath[army.pathIndex]);
                    // Apply same offset as armies get (upper-left for index 0)
                    var typeOffset = new Vector3(desired.worldPosition.x - HexMetrics.HexToWorldPosition(army.coordinate).x,
                                                 desired.worldPosition.y - HexMetrics.HexToWorldPosition(army.coordinate).y, 0f);
                    var toPos = new Vector3(nextTileCenter.x + typeOffset.x, nextTileCenter.y + typeOffset.y, ZPosition);
                    float t = Mathf.Clamp01((float)army.movementProgress);
                    var interpolatedPos = Vector3.Lerp(fromPos, toPos, t);

                    desired.worldPosition = interpolatedPos;
                    desiredStates[army.id] = desired;

                    int remainingTiles = army.currentPath.Count - army.pathIndex;
                    movementStates[army.id] = new MovementInterpolationState
                    {
                        fromPosition = fromPos,
                        toPosition = toPos,
                        lastEngineProgress = army.movementProgress,
                        lastEngineSpeed = army.movementSpeed,
                        lastUpdateTime = now,
                        remainingTiles = remainingTiles
                    };
                }
            }

            foreach (var kvp in gameState.villagerGroups)
            {
                var group = kvp.Value;
                if (group.currentPath != null && group.pathIndex < group.currentPath.Count &&
                    group.movementSpeed > 0 && desiredStates.ContainsKey(group.id))
                {
                    activelyMovingEntities.Add(group.id);
                    var desired = desiredStates[group.id];
                    var fromPos = desired.worldPosition;
                    var nextTileCenter = HexMetrics.HexToWorldPosition(group.currentPath[group.pathIndex]);
                    var typeOffset = new Vector3(desired.worldPosition.x - HexMetrics.HexToWorldPosition(group.coordinate).x,
                                                 desired.worldPosition.y - HexMetrics.HexToWorldPosition(group.coordinate).y, 0f);
                    var toPos = new Vector3(nextTileCenter.x + typeOffset.x, nextTileCenter.y + typeOffset.y, ZPosition);
                    float t = Mathf.Clamp01((float)group.movementProgress);
                    var interpolatedPos = Vector3.Lerp(fromPos, toPos, t);

                    desired.worldPosition = interpolatedPos;
                    desiredStates[group.id] = desired;

                    int remainingTiles = group.currentPath.Count - group.pathIndex;
                    movementStates[group.id] = new MovementInterpolationState
                    {
                        fromPosition = fromPos,
                        toPosition = toPos,
                        lastEngineProgress = group.movementProgress,
                        lastEngineSpeed = group.movementSpeed,
                        lastUpdateTime = now,
                        remainingTiles = remainingTiles
                    };
                }
            }

            // Clean up movement states and timer labels for entities that stopped moving
            toRemove.Clear();
            foreach (var id in movementStates.Keys)
            {
                if (!activelyMovingEntities.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                movementStates.Remove(id);
                if (timerLabels.TryGetValue(id, out var timerGO) && timerGO != null)
                    Destroy(timerGO);
                timerLabels.Remove(id);
            }
        }

        private void AddDesiredState(EntityPlacement placement, Vector3 tileCenter, Vector2 offset)
        {
            desiredStates[placement.id] = new EntityRenderState
            {
                id = placement.id,
                type = placement.type,
                color = placement.color,
                label = placement.label,
                worldPosition = new Vector3(
                    tileCenter.x + offset.x,
                    tileCenter.y + offset.y,
                    ZPosition)
            };
        }

        // ================================================================
        // Per-Frame Movement Interpolation
        // ================================================================

        public void InterpolateMovingEntities(GameState gameState)
        {
            if (gameState.isPaused || movementStates.Count == 0) return;

            double now = Time.timeAsDouble;
            float dt = Time.deltaTime;
            double gameSpeed = gameState.gameSpeed;

            foreach (var kvp in movementStates)
            {
                var id = kvp.Key;
                var state = kvp.Value;

                if (!entityVisuals.TryGetValue(id, out var go) || go == null) continue;

                // Predict current progress based on elapsed time since last engine update
                double elapsed = now - state.lastUpdateTime;
                double predictedProgress = state.lastEngineProgress + state.lastEngineSpeed * elapsed * gameSpeed;
                predictedProgress = System.Math.Min(predictedProgress, 1.0);

                // SmoothStep ease-in/ease-out on predicted progress
                float t = (float)predictedProgress;
                t = t * t * (3f - 2f * t);
                Vector3 targetPos = Vector3.Lerp(state.fromPosition, state.toPosition, t);

                // Frame-rate-independent exponential smoothing
                float smoothFactor = 1f - Mathf.Exp(-12f * dt);
                go.transform.position = Vector3.Lerp(go.transform.position, targetPos, smoothFactor);

                // Update/create timer label
                UpdateTimerLabel(id, go, state, predictedProgress);
            }
        }

        private void UpdateTimerLabel(Guid id, GameObject entityGO, MovementInterpolationState state, double currentProgress)
        {
            if (state.lastEngineSpeed <= 0)
            {
                // Remove timer if speed is zero
                if (timerLabels.TryGetValue(id, out var existing) && existing != null)
                    Destroy(existing);
                timerLabels.Remove(id);
                return;
            }

            // ETA = (remaining tiles - 1 + (1 - progress)) / speed
            double tilesLeft = (state.remainingTiles - 1) + (1.0 - currentProgress);
            double etaSeconds = tilesLeft / state.lastEngineSpeed;

            if (etaSeconds < 0.5)
            {
                // About to arrive — hide timer
                if (timerLabels.TryGetValue(id, out var existing) && existing != null)
                    existing.SetActive(false);
                return;
            }

            // Format ETA
            string etaText;
            int totalSeconds = Mathf.CeilToInt((float)etaSeconds);
            if (totalSeconds < 60)
                etaText = $"{totalSeconds}s";
            else
                etaText = $"{totalSeconds / 60}m{totalSeconds % 60}s";

            if (timerLabels.TryGetValue(id, out var timerGO) && timerGO != null)
            {
                timerGO.SetActive(true);
                var tm = timerGO.GetComponent<TextMesh>();
                if (tm != null) tm.text = etaText;
            }
            else
            {
                // Create timer label as child of entity GO
                timerGO = new GameObject("Timer");
                timerGO.transform.SetParent(entityGO.transform, false);
                timerGO.transform.localPosition = new Vector3(0f, -0.2f * HexMetrics.IsometricYScale, -0.01f);

                var textMesh = timerGO.AddComponent<TextMesh>();
                textMesh.text = etaText;
                textMesh.fontSize = 24;
                textMesh.characterSize = 0.06f;
                textMesh.anchor = TextAnchor.UpperCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = SporefrontColors.ParchmentLight;

                var meshRenderer = timerGO.GetComponent<MeshRenderer>();
                meshRenderer.sortingOrder = 8;

                timerLabels[id] = timerGO;
            }
        }

        // ================================================================
        // Visual Creation
        // ================================================================

        private void CreateEntityVisual(EntityRenderState state)
        {
            var go = new GameObject($"Entity_{state.type}_{state.id.ToString().Substring(0, 8)}");
            go.transform.SetParent(transform, false);
            go.transform.position = state.worldPosition;

            // Mesh
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();

            switch (state.type)
            {
                case EntityVisualType.Building:
                    meshFilter.sharedMesh = diamondMesh;
                    break;
                case EntityVisualType.Army:
                    meshFilter.sharedMesh = circleMesh;
                    break;
                case EntityVisualType.Villager:
                    meshFilter.sharedMesh = smallCircleMesh;
                    break;
                case EntityVisualType.Resource:
                    meshFilter.sharedMesh = triangleMesh;
                    break;
            }

            meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.sortingOrder = 6;

            // Per-instance color via MaterialPropertyBlock
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", state.color);
            meshRenderer.SetPropertyBlock(mpb);

            // Label for buildings and resources
            if (state.label != null)
            {
                CreateLabel(go.transform, state.label, state.color);
            }

            // Progress/HP bars for buildings
            if (state.type == EntityVisualType.Building)
            {
                CreateBuildingBars(state.id, go);
            }

            entityVisuals[state.id] = go;
            currentStates[state.id] = state;
        }

        private void CreateLabel(Transform parent, string text, Color entityColor)
        {
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(parent, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            var textMesh = labelGO.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.08f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            // Contrasting label color — use light text on dark entity colors
            float luminance = entityColor.r * 0.299f + entityColor.g * 0.587f + entityColor.b * 0.114f;
            textMesh.color = luminance > 0.5f ? SporefrontColors.InkBlack : SporefrontColors.ParchmentLight;

            var meshRenderer = labelGO.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 7;
        }

        // ================================================================
        // Building Progress/HP Bars
        // ================================================================

        private void CreateBuildingBars(Guid id, GameObject parent)
        {
            // Create rotated container aligned to hex bottom-right edge
            var container = new GameObject("BarContainer");
            container.transform.SetParent(parent.transform, false);
            container.transform.localPosition = BarContainerPosition;
            container.transform.localRotation = Quaternion.Euler(0f, 0f, BarRotationAngle);

            Color bgColor = new Color(SporefrontColors.InkBlack.r, SporefrontColors.InkBlack.g,
                SporefrontColors.InkBlack.b, 0.5f);

            var bg = CreateBarChild(container.transform, "BarBG", bgColor, 5, BarWidth);
            var hpFill = CreateBarChild(container.transform, "BarHP", SporefrontColors.SporeGreen, 6, 0f);
            var upgradeFill = CreateBarChild(container.transform, "BarUpgrade", SporefrontColors.SporeAmber, 7, 0f);
            upgradeFill.SetActive(false);

            buildingBars[id] = new BuildingBarVisuals
            {
                container = container,
                background = bg,
                hpFill = hpFill,
                upgradeFill = upgradeFill,
                currentHPFill = 0f,
                currentUpgradeFill = 0f
            };
        }

        private GameObject CreateBarChild(Transform parent, string name, Color color, int sortingOrder, float scaleX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = barMesh;
            go.transform.localScale = new Vector3(scaleX, 1f, 1f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sharedMaterial;
            mr.sortingOrder = sortingOrder;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", color);
            mr.SetPropertyBlock(mpb);
            return go;
        }

        public void UpdateBuildingBars(GameState gameState)
        {
            if (gameState == null || buildingBars.Count == 0) return;

            float dt = Time.deltaTime;

            // Iterate over a snapshot of keys to allow struct mutation
            var barKeys = new List<Guid>(buildingBars.Keys);
            foreach (var id in barKeys)
            {
                if (!buildingBars.TryGetValue(id, out var bars)) continue;
                if (bars.hpFill == null) continue;

                if (!gameState.buildings.TryGetValue(id, out var building)) continue;
                if (building.state == BuildingState.Planning) continue;

                // Compute target fills
                float hpTarget = 0f;
                float upgradeTarget = 0f;

                switch (building.state)
                {
                    case BuildingState.Constructing:
                        hpTarget = Mathf.Clamp01((float)building.constructionProgress);
                        break;
                    case BuildingState.Completed:
                        hpTarget = building.maxHealth > 0 ? Mathf.Clamp01((float)(building.health / building.maxHealth)) : 1f;
                        break;
                    case BuildingState.Upgrading:
                        hpTarget = building.maxHealth > 0 ? Mathf.Clamp01((float)(building.health / building.maxHealth)) : 1f;
                        upgradeTarget = Mathf.Clamp01((float)building.upgradeProgress);
                        break;
                    case BuildingState.Damaged:
                        hpTarget = building.maxHealth > 0 ? Mathf.Clamp01((float)(building.health / building.maxHealth)) : 0f;
                        break;
                    case BuildingState.Demolishing:
                        hpTarget = 1f - Mathf.Clamp01((float)building.demolitionProgress);
                        break;
                    case BuildingState.Destroyed:
                        hpTarget = 0f;
                        break;
                }

                // Smooth interpolation
                bars.currentHPFill = Mathf.Lerp(bars.currentHPFill, hpTarget, dt * 8f);
                bars.currentUpgradeFill = Mathf.Lerp(bars.currentUpgradeFill, upgradeTarget, dt * 8f);

                // Update HP fill scale and color
                bars.hpFill.transform.localScale = new Vector3(BarWidth * bars.currentHPFill, 1f, 1f);
                Color hpColor = Color.Lerp(SporefrontColors.SporeRed, SporefrontColors.SporeGreen, bars.currentHPFill);
                var hpMR = bars.hpFill.GetComponent<MeshRenderer>();
                if (hpMR != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_Color", hpColor);
                    hpMR.SetPropertyBlock(mpb);
                }

                // Update upgrade fill
                bool showUpgrade = upgradeTarget > 0.001f;
                if (bars.upgradeFill.activeSelf != showUpgrade)
                    bars.upgradeFill.SetActive(showUpgrade);
                if (showUpgrade)
                    bars.upgradeFill.transform.localScale = new Vector3(BarWidth * bars.currentUpgradeFill, 1f, 1f);

                buildingBars[id] = bars;
            }
        }

        // ================================================================
        // Entity Position API (for PathRenderer)
        // ================================================================

        /// <summary>
        /// Returns the world position at the bottom of the entity's visual shape.
        /// Used by PathRenderer to anchor path start points to entity bottoms.
        /// </summary>
        public Vector3 GetEntityBottomPosition(Guid id)
        {
            if (!entityVisuals.TryGetValue(id, out var go) || go == null)
                return Vector3.zero;

            float bottomOffset = 0f;
            if (currentStates.TryGetValue(id, out var state))
            {
                switch (state.type)
                {
                    case EntityVisualType.Army:
                        bottomOffset = ArmySize * HexMetrics.IsometricYScale;
                        break;
                    case EntityVisualType.Villager:
                        bottomOffset = VillagerSize * HexMetrics.IsometricYScale;
                        break;
                }
            }

            var pos = go.transform.position;
            return new Vector3(pos.x, pos.y - bottomOffset, pos.z);
        }

        // ================================================================
        // Shared Mesh Creation
        // ================================================================

        private void CreateSharedMeshes()
        {
            diamondMesh = CreateDiamondMesh(BuildingSize);
            circleMesh = CreateCircleMesh(ArmySize, 8);
            smallCircleMesh = CreateCircleMesh(VillagerSize, 6);
            triangleMesh = CreateTriangleMesh(ResourceSize);
            barMesh = CreateBarMesh(BarHeight);
        }

        private Mesh CreateDiamondMesh(float size)
        {
            var mesh = new Mesh();
            mesh.name = "Diamond";

            // Rotated square (diamond shape), Y scaled for isometric
            float ySize = size * HexMetrics.IsometricYScale;
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, ySize, 0f),       // top
                new Vector3(size, 0f, 0f),         // right
                new Vector3(0f, -ySize, 0f),       // bottom
                new Vector3(-size, 0f, 0f)         // left
            };

            mesh.triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3
            };

            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh CreateCircleMesh(float radius, int segments)
        {
            var mesh = new Mesh();
            mesh.name = $"Circle{segments}";

            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            // Center vertex
            vertices[0] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float angle = (360f / segments) * i * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * HexMetrics.IsometricYScale,
                    0f);

                int nextIdx = (i + 1) % segments + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = nextIdx;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh CreateTriangleMesh(float size)
        {
            var mesh = new Mesh();
            mesh.name = "Triangle";

            float yScale = HexMetrics.IsometricYScale;
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, size * yScale, 0f),                        // top
                new Vector3(size * 0.866f, -size * 0.5f * yScale, 0f),    // bottom-right
                new Vector3(-size * 0.866f, -size * 0.5f * yScale, 0f)    // bottom-left
            };

            mesh.triangles = new int[] { 0, 1, 2 };

            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh CreateBarMesh(float height)
        {
            var mesh = new Mesh { name = "Bar" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, height, 0),
                new Vector3(0, height, 0)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Color GetOwnerColor(Guid? ownerID, GameState gameState)
        {
            if (!ownerID.HasValue) return SporefrontColors.InkMid;

            var player = gameState.GetPlayer(ownerID.Value);
            if (player == null) return SporefrontColors.InkMid;

            return SporefrontColors.ParsePlayerColor(player.colorHex);
        }

        private string GetBuildingLabel(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter:    return "C";
                case BuildingType.Farm:          return "F";
                case BuildingType.Neighborhood:  return "N";
                case BuildingType.Blacksmith:    return "B";
                case BuildingType.Market:        return "M";
                case BuildingType.MiningCamp:    return "Mi";
                case BuildingType.LumberCamp:    return "L";
                case BuildingType.Warehouse:     return "W";
                case BuildingType.University:    return "U";
                case BuildingType.Library:       return "Li";
                case BuildingType.Mill:          return "Ml";
                case BuildingType.Road:          return "R";
                case BuildingType.Castle:        return "Ca";
                case BuildingType.Barracks:      return "Bk";
                case BuildingType.ArcheryRange:  return "A";
                case BuildingType.Stable:        return "S";
                case BuildingType.SiegeWorkshop: return "Sw";
                case BuildingType.Tower:         return "T";
                case BuildingType.WoodenFort:    return "Wf";
                case BuildingType.Wall:          return "Wa";
                case BuildingType.Gate:          return "G";
                default:                         return "?";
            }
        }

        private string GetResourceLabel(ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees:        return "Tr";
                case ResourcePointType.Forage:       return "Fo";
                case ResourcePointType.OreMine:      return "Or";
                case ResourcePointType.StoneQuarry:  return "Sq";
                case ResourcePointType.Deer:         return "De";
                case ResourcePointType.WildBoar:     return "Wb";
                case ResourcePointType.Farmland:     return "Fm";
                default:                             return "?";
            }
        }

        private void OnDestroy()
        {
            foreach (var kvp in entityVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            entityVisuals.Clear();
            currentStates.Clear();
            movementStates.Clear();
            timerLabels.Clear();
            buildingBars.Clear();
        }

        // ================================================================
        // Internal Types
        // ================================================================

        private enum EntityVisualType
        {
            Building,
            Army,
            Villager,
            Resource
        }

        private struct EntityPlacement
        {
            public Guid id;
            public EntityVisualType type;
            public Color color;
            public string label;
        }

        private struct EntityRenderState
        {
            public Guid id;
            public EntityVisualType type;
            public Color color;
            public string label;
            public Vector3 worldPosition;
        }

        private struct MovementInterpolationState
        {
            public Vector3 fromPosition;
            public Vector3 toPosition;
            public double lastEngineProgress;
            public double lastEngineSpeed;
            public double lastUpdateTime;
            public int remainingTiles;
        }

        private struct BuildingBarVisuals
        {
            public GameObject container;
            public GameObject background;
            public GameObject hpFill;
            public GameObject upgradeFill;
            public float currentHPFill;
            public float currentUpgradeFill;
        }
    }
}
