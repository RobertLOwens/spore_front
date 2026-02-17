// ============================================================================
// FILE: Visual/EntityRenderer.cs
// PURPOSE: Renders buildings, armies, and villager groups on the hex map
//          as colored programmatic shapes with type labels
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

        // Shared meshes (created once)
        private Mesh diamondMesh;   // buildings
        private Mesh circleMesh;    // armies (8 segments)
        private Mesh smallCircleMesh; // villagers (6 segments)
        private Mesh triangleMesh;  // resource nodes

        private Material sharedMaterial;

        private const float BuildingSize = 0.30f;  // ~30% of hex outer radius
        private const float ArmySize = 0.22f;      // ~22%
        private const float VillagerSize = 0.15f;   // ~15%
        private const float ResourceSize = 0.18f;   // ~18%
        private const float ZPosition = -0.05f;     // in front of tiles/borders/selection/paths

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
            // Clear all existing visuals
            foreach (var kvp in entityVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            entityVisuals.Clear();

            // Track entities per tile for offset logic
            var entitiesPerTile = new Dictionary<HexCoordinate, List<EntityPlacement>>();

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

            // Create visuals with offset logic
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
                    CreateEntityVisual(b, tileCenter, Vector2.zero);
                }

                // Resources at center when no building, offset lower-center when building present
                foreach (var r in resources)
                {
                    Vector2 offset = buildings.Count > 0
                        ? new Vector2(0f, -0.25f)
                        : Vector2.zero;
                    CreateEntityVisual(r, tileCenter, offset);
                }

                // Armies offset upper-left / upper-right
                for (int i = 0; i < armies.Count; i++)
                {
                    float xOff = (i % 2 == 0) ? -0.3f : 0.3f;
                    float yOff = 0.3f;
                    CreateEntityVisual(armies[i], tileCenter, new Vector2(xOff, yOff));
                }

                // Villagers offset lower-left / lower-right
                for (int i = 0; i < villagers.Count; i++)
                {
                    float xOff = (i % 2 == 0) ? -0.3f : 0.3f;
                    float yOff = -0.3f;
                    CreateEntityVisual(villagers[i], tileCenter, new Vector2(xOff, yOff));
                }
            }
        }

        // ================================================================
        // Visual Creation
        // ================================================================

        private void CreateEntityVisual(EntityPlacement placement, Vector3 tileCenter, Vector2 offset)
        {
            var go = new GameObject($"Entity_{placement.type}_{placement.id.ToString().Substring(0, 8)}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(
                tileCenter.x + offset.x,
                tileCenter.y + offset.y,
                ZPosition);

            // Mesh
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();

            switch (placement.type)
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
            mpb.SetColor("_Color", placement.color);
            meshRenderer.SetPropertyBlock(mpb);

            // Label for buildings
            if (placement.label != null)
            {
                CreateLabel(go.transform, placement.label, placement.color);
            }

            entityVisuals[placement.id] = go;
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
        // Shared Mesh Creation
        // ================================================================

        private void CreateSharedMeshes()
        {
            diamondMesh = CreateDiamondMesh(BuildingSize);
            circleMesh = CreateCircleMesh(ArmySize, 8);
            smallCircleMesh = CreateCircleMesh(VillagerSize, 6);
            triangleMesh = CreateTriangleMesh(ResourceSize);
        }

        private Mesh CreateDiamondMesh(float size)
        {
            var mesh = new Mesh();
            mesh.name = "Diamond";

            // Rotated square (diamond shape)
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, size, 0f),       // top
                new Vector3(size, 0f, 0f),        // right
                new Vector3(0f, -size, 0f),       // bottom
                new Vector3(-size, 0f, 0f)        // left
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
                    Mathf.Sin(angle) * radius,
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

            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, size, 0f),            // top
                new Vector3(size * 0.866f, -size * 0.5f, 0f),  // bottom-right
                new Vector3(-size * 0.866f, -size * 0.5f, 0f)  // bottom-left
            };

            mesh.triangles = new int[] { 0, 1, 2 };

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
    }
}
