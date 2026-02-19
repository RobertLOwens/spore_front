// ============================================================================
// FILE: Visual/TileInfoPanel.cs
// PURPOSE: Right-side tile details panel with contextual info
//          Shows terrain, buildings, armies, villagers, resources, actions
//          Merged: absorbs EntityListPanel's inline action buttons
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class TileInfoPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnBuildingDetailRequested;
        public event Action<Guid> OnArmyDetailRequested;
        public event Action<HexCoordinate> OnBuildRequested;
        public event Action<Guid, HexCoordinate> OnMoveRequested;
        public event Action<Guid> OnArmyMoveRequested;
        public event Action<Guid, HexCoordinate> OnAttackRequested;
        public event Action<Guid, Guid> OnGatherRequested; // villagerGroupID, resourcePointID
        public event Action<Guid> OnHuntRequested; // resourcePointID — opens GatherPanel in hunt mode
        public event Action<Guid, HexCoordinate, bool> OnMoveEntityToTile; // entityID, destination, isArmy
        public event Action<Guid, HexCoordinate> OnAttackEntityToTile; // armyID, target coordinate

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private HexCoordinate? currentCoord;
        private Guid localPlayerID;
        private bool showingMoveSelection;
        private bool showingAttackSelection;
        private GameState cachedGameState;

        // Cached references for incremental updates
        private Image buildingHPFill;

        // Structural fingerprint
        private bool hasCachedFingerprint;
        private int cachedBuildingCount; // 0 or 1
        private BuildingState cachedBuildingState;
        private int cachedBuildingLevel;
        private int cachedArmyCount;
        private int cachedVillagerCount;
        private bool cachedHasResource;
        private int cachedArmyStateHash;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Right-anchored panel
            panel = UIHelper.CreatePanel(canvasTransform, "TileInfoPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-UIConstants.SidePanelWidth, 50); // bottom margin
            rt.offsetMax = new Vector2(0, -70);    // 70px top margin (below resource bar)

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "TileScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(HexCoordinate coord, GameState gameState, Guid playerID)
        {
            currentCoord = coord;
            localPlayerID = playerID;
            showingMoveSelection = false;
            showingAttackSelection = false;
            hasCachedFingerprint = false;
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentCoord = null;
            showingMoveSelection = false;
            showingAttackSelection = false;
            panel.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !panel.activeSelf) return;

            // Skip full rebuild if structure hasn't changed
            if (hasCachedFingerprint && !showingMoveSelection && !showingAttackSelection && TileFingerprintMatches(gameState))
            {
                IncrementalTileUpdate(gameState);
                return;
            }

            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild Content
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentCoord.HasValue) return;
            var coord = currentCoord.Value;
            cachedGameState = gameState;

            // Clear existing content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            buildingHPFill = null;

            // Move/Attack selection views — show entity list instead of normal content
            var panelImg = panel.GetComponent<Image>();
            if (showingMoveSelection)
            {
                panelImg.color = Color.Lerp(UIHelper.PanelBg, SporefrontColors.SporeTeal, 0.06f);
                BuildMoveSelectionView(gameState, coord);
                return;
            }
            if (showingAttackSelection)
            {
                panelImg.color = Color.Lerp(UIHelper.PanelBg, SporefrontColors.SporeRed, 0.06f);
                BuildAttackSelectionView(gameState, coord);
                return;
            }
            panelImg.color = UIHelper.PanelBg;

            var tileNullable = gameState.mapData.GetTile(coord);
            if (!tileNullable.HasValue) return;
            var tile = tileNullable.Value;

            // 1. Header: terrain + coordinate
            var header = UIHelper.CreateLabel(contentRT, $"{tile.terrain} ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // 2. Building section
            var building = gameState.GetBuilding(coord);
            if (building != null)
            {
                BuildBuildingSection(building, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 3. Armies section
            var armies = gameState.GetArmies(coord);
            if (armies != null && armies.Count > 0)
            {
                BuildArmiesSection(armies, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 4. Villagers section
            var villagers = gameState.GetVillagerGroups(coord);
            if (villagers != null && villagers.Count > 0)
            {
                BuildVillagersSection(villagers, coord, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 5. Resource point section
            var rp = gameState.GetResourcePoint(coord);
            if (rp != null && !rp.IsDepleted())
            {
                BuildResourceSection(rp, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 6. Actions: "Build Here" on empty buildable tiles
            if (building == null && gameState.CanBuildAt(coord, localPlayerID))
            {
                var buildBtn = UIHelper.CreateButton(contentRT, "Build Here",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                    () => OnBuildRequested?.Invoke(coord));
                var btnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 38;
            }

            // 7. "Move Here" — destination-first move flow
            var moveHereBtn = UIHelper.CreateButton(contentRT, "Move Here",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                () => { showingMoveSelection = true; Rebuild(cachedGameState); });
            var moveHereBtnLE = moveHereBtn.gameObject.AddComponent<LayoutElement>();
            moveHereBtnLE.preferredHeight = 38;

            // 8. "Attack Here" — destination-first attack flow (when tile has enemy entities)
            bool hasEnemyEntities = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value != localPlayerID)
                    {
                        hasEnemyEntities = true;
                        break;
                    }
                }
            }
            if (hasEnemyEntities)
            {
                var attackHereBtn = UIHelper.CreateButton(contentRT, "Attack Here",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody,
                    () => { showingAttackSelection = true; Rebuild(cachedGameState); });
                var attackHereBtnLE = attackHereBtn.gameObject.AddComponent<LayoutElement>();
                attackHereBtnLE.preferredHeight = 38;
            }

            // Cache fingerprint for incremental refresh
            CacheTileFingerprint(gameState);
        }

        // ================================================================
        // Fingerprint & Incremental Update
        // ================================================================

        private int ComputeArmyStateHash(GameState gameState)
        {
            var coord = currentCoord.Value;
            var armies = gameState.GetArmies(coord);
            if (armies == null || armies.Count == 0) return 0;
            int hash = 17;
            foreach (var army in armies)
            {
                hash = hash * 31 + (army.isInCombat ? 1 : 0);
                hash = hash * 31 + (army.isEntrenched ? 2 : 0);
                hash = hash * 31 + (army.isEntrenching ? 4 : 0);
                hash = hash * 31 + (army.isRetreating ? 8 : 0);
                hash = hash * 31 + army.GetTotalUnits();
            }
            return hash;
        }

        private bool TileFingerprintMatches(GameState gameState)
        {
            var coord = currentCoord.Value;
            var building = gameState.GetBuilding(coord);
            int buildingCount = building != null ? 1 : 0;
            var armies = gameState.GetArmies(coord);
            int armyCount = armies != null ? armies.Count : 0;
            var villagers = gameState.GetVillagerGroups(coord);
            int villagerCount = villagers != null ? villagers.Count : 0;
            var rp = gameState.GetResourcePoint(coord);
            bool hasResource = rp != null && !rp.IsDepleted();

            if (buildingCount != cachedBuildingCount) return false;
            if (armyCount != cachedArmyCount) return false;
            if (villagerCount != cachedVillagerCount) return false;
            if (hasResource != cachedHasResource) return false;

            if (building != null)
            {
                if (building.state != cachedBuildingState) return false;
                if (building.level != cachedBuildingLevel) return false;
            }

            // Check army combat/entrench/retreat state changes
            if (ComputeArmyStateHash(gameState) != cachedArmyStateHash) return false;

            // Force rebuild when any army is entrenching (progress bar needs updates)
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.isEntrenching) return false;
                }
            }

            return true;
        }

        private void CacheTileFingerprint(GameState gameState)
        {
            var coord = currentCoord.Value;
            var building = gameState.GetBuilding(coord);
            cachedBuildingCount = building != null ? 1 : 0;
            cachedBuildingState = building != null ? building.state : BuildingState.Planning;
            cachedBuildingLevel = building != null ? building.level : 0;
            var armies = gameState.GetArmies(coord);
            cachedArmyCount = armies != null ? armies.Count : 0;
            var villagers = gameState.GetVillagerGroups(coord);
            cachedVillagerCount = villagers != null ? villagers.Count : 0;
            var rp = gameState.GetResourcePoint(coord);
            cachedHasResource = rp != null && !rp.IsDepleted();
            cachedArmyStateHash = ComputeArmyStateHash(gameState);
            hasCachedFingerprint = true;
        }

        private void IncrementalTileUpdate(GameState gameState)
        {
            var coord = currentCoord.Value;
            cachedGameState = gameState;

            // Only update building HP bar
            var building = gameState.GetBuilding(coord);
            if (building != null && building.maxHealth > 0 && buildingHPFill != null)
            {
                float hpPct = (float)(building.health / building.maxHealth);
                var fillRT = buildingHPFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
            }
        }

        // ================================================================
        // Section Builders
        // ================================================================

        private void BuildBuildingSection(BuildingData building, GameState gameState)
        {
            bool isOwned = building.ownerID.HasValue && building.ownerID.Value == localPlayerID;
            string displayName = building.buildingType.DisplayName();

            var label = UIHelper.CreateLabel(contentRT,
                $"{displayName} Lv.{building.level}", UIConstants.FontBody, null, TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 30;

            // State
            string stateStr = building.state.ToString();
            var stateLabel = UIHelper.CreateLabel(contentRT, stateStr, UIConstants.FontBody);
            var stateLE = stateLabel.gameObject.AddComponent<LayoutElement>();
            stateLE.preferredHeight = 24;

            // HP bar
            if (building.maxHealth > 0)
            {
                var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 14f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
                float hpPct = (float)(building.health / building.maxHealth);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.preferredHeight = 14;
                buildingHPFill = fill;
            }

            // Details button (owned buildings only)
            if (isOwned && building.IsOperational)
            {
                var detailBtn = UIHelper.CreateButton(contentRT, "Details",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                    () => OnBuildingDetailRequested?.Invoke(building.id));
                var btnLE = detailBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 34;
            }
        }

        private void BuildArmiesSection(List<ArmyData> armies, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Armies",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            foreach (var army in armies)
            {
                bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;
                string status = UIHelper.FormatArmyStatus(army);
                int total = army.GetTotalUnits();

                // Row 1: Name + status + unit count
                var nameRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{army.name}{status} ({total})", UIConstants.FontBody);
                nameLabel.supportRichText = true;
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;
                nameLE.preferredHeight = 30;

                // Entrenchment progress bar
                if (army.isEntrenching && army.entrenchmentStartTime.HasValue)
                {
                    double currentTime = gameState.currentTime;
                    double elapsed = currentTime - army.entrenchmentStartTime.Value;
                    double buildTime = GameConfig.Entrenchment.BuildTime;
                    float progress = Mathf.Clamp01((float)(elapsed / buildTime));
                    double remaining = buildTime - elapsed;

                    var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 14f,
                        SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(progress, 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.preferredHeight = 14;

                    var timeLabel = UIHelper.CreateLabel(contentRT,
                        $"Entrenching: {UIHelper.FormatTime(remaining)}", UIConstants.FontCaption,
                        SporefrontColors.SporeAmber);
                    var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
                    timeLE.preferredHeight = 18;
                }

                if (isOwned)
                {
                    // Row 2: Action buttons
                    var btnRow = UIHelper.CreateHorizontalRow(contentRT, 34f, 3f);

                    // Entrench
                    if (!army.isEntrenched && !army.isEntrenching && !army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Dig In", () =>
                        {
                            var cmd = new EntrenchCommand(localPlayerID, capturedArmyID);
                            UIManager.ExecutePlayerCommand(cmd);
                        });
                    }

                    // Retreat
                    if (army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Retreat", () =>
                        {
                            var cmd = new RetreatCommand(localPlayerID, capturedArmyID);
                            UIManager.ExecutePlayerCommand(cmd);
                        });
                    }

                    // Move
                    if (!army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Move", () =>
                            OnArmyMoveRequested?.Invoke(capturedArmyID));
                    }

                    // Garrison — at owned operational building with garrison capacity
                    if (!army.isInCombat)
                    {
                        var building = gameState.GetBuilding(army.coordinate);
                        if (building != null && building.ownerID.HasValue &&
                            building.ownerID.Value == localPlayerID && building.IsOperational &&
                            building.GetGarrisonCapacity() > 0)
                        {
                            var capturedBuildingID = building.id;
                            var capturedArmyID = army.id;
                            CreateActionButton(btnRow.transform, "Garrison", () =>
                            {
                                var cmd = new GarrisonArmyCommand(localPlayerID, capturedArmyID, capturedBuildingID);
                                UIManager.ExecutePlayerCommand(cmd);
                            });
                        }
                    }

                    // Attack
                    if (!army.isInCombat && !army.isRetreating)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Attack", () =>
                            OnAttackRequested?.Invoke(capturedArmyID, army.coordinate));
                    }

                    // Info
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Info", () =>
                            OnArmyDetailRequested?.Invoke(capturedArmyID));
                    }
                }
                else if (army.ownerID.HasValue && army.ownerID.Value != localPlayerID)
                {
                    // Enemy army — label tag (use "Attack Here" button for destination-first flow)
                    var enemyTag = UIHelper.CreateLabel(nameRow.transform, "Enemy",
                        UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
                    var tagLE = enemyTag.gameObject.AddComponent<LayoutElement>();
                    tagLE.preferredWidth = 48;
                    tagLE.preferredHeight = 30;
                }
            }
        }

        private void BuildVillagersSection(List<VillagerGroupData> groups, HexCoordinate coord, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Villagers",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            foreach (var group in groups)
            {
                bool isOwned = group.ownerID.HasValue && group.ownerID.Value == localPlayerID;
                string task = group.currentTask != null ? group.currentTask.DisplayName : "Idle";

                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

                var label = UIHelper.CreateLabel(row.transform,
                    $"{group.villagerCount}x — {task}", UIConstants.FontBody);
                var labelLE = label.gameObject.AddComponent<LayoutElement>();
                labelLE.flexibleWidth = 1;

                if (isOwned)
                {
                    var capturedGroupID = group.id;

                    var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                        () => OnMoveRequested?.Invoke(capturedGroupID, coord));
                    var moveBtnLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                    moveBtnLE.preferredWidth = 50;
                    moveBtnLE.preferredHeight = 28;

                    // Cancel task button
                    if (group.currentTask != null && !group.currentTask.IsIdle)
                    {
                        var cancelBtn = UIHelper.CreateButton(row.transform, "Cancel",
                            SporefrontColors.InkMid, UIHelper.HudTextColor, UIConstants.FontBody,
                            () =>
                            {
                                var cmd = new StopGatheringCommand(localPlayerID, capturedGroupID);
                                UIManager.ExecutePlayerCommand(cmd);
                            });
                        var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
                        cancelLE.preferredWidth = 56;
                        cancelLE.preferredHeight = 28;
                    }
                }
            }
        }

        private void BuildResourceSection(ResourcePointData rp, GameState gameState)
        {
            var label = UIHelper.CreateLabel(contentRT,
                $"Resource: {rp.resourceType} ({rp.remainingAmount})", UIConstants.FontBody, null,
                TextAnchor.MiddleLeft);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 26;

            bool isHuntable = rp.resourceType.IsHuntable() && rp.currentHealth > 0;

            if (isHuntable)
            {
                // Hunt button — opens GatherPanel in hunt mode with villager selection
                var capturedRPID = rp.id;
                var huntBtn = UIHelper.CreateButton(contentRT, "Hunt",
                    SporefrontColors.SporeRed,
                    UIHelper.HudTextColor, UIConstants.FontBody,
                    () => OnHuntRequested?.Invoke(capturedRPID));
                var btnLE = huntBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 34;
            }
            else if (rp.resourceType.RequiresCamp())
            {
                // Camp-required resources: check camp coverage via ResourceEngine
                bool hasCoverage = GameEngine.Instance.resourceEngine.HasCampCoverage(
                    rp.coordinate, rp.resourceType, gameState);

                if (hasCoverage)
                {
                    // Find nearest idle villager owned by this player (no distance restriction)
                    var villagers = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                    VillagerGroupData nearest = null;
                    int nearestDist = int.MaxValue;
                    if (villagers != null)
                    {
                        foreach (var vg in villagers)
                        {
                            if (vg.currentTask != null && !vg.currentTask.IsIdle) continue;
                            int dist = vg.coordinate.Distance(rp.coordinate);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearest = vg;
                            }
                        }
                    }

                    if (nearest != null)
                    {
                        var capturedVG = nearest;
                        var gatherBtn = UIHelper.CreateButton(contentRT, "Gather",
                            SporefrontColors.SporeGreen,
                            UIHelper.HudTextColor, UIConstants.FontBody,
                            () => OnGatherRequested?.Invoke(capturedVG.id, rp.id));
                        var btnLE = gatherBtn.gameObject.AddComponent<LayoutElement>();
                        btnLE.preferredHeight = 34;
                    }
                }
                else
                {
                    // Show "Needs [Camp Type]" label
                    string campName = rp.resourceType == ResourcePointType.Trees
                        ? "Lumber Camp"
                        : "Mining Camp";
                    var needsLabel = UIHelper.CreateLabel(contentRT,
                        $"Needs {campName}", UIConstants.FontBody, SporefrontColors.InkFaded,
                        TextAnchor.MiddleLeft);
                    var needsLE = needsLabel.gameObject.AddComponent<LayoutElement>();
                    needsLE.preferredHeight = 26;
                }
            }
            else
            {
                // Non-camp resources (Farmland, Forage, Carcasses): nearby idle villager check
                var villagers = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                if (villagers != null)
                {
                    foreach (var vg in villagers)
                    {
                        if (vg.coordinate.Distance(rp.coordinate) <= 2 &&
                            (vg.currentTask == null || vg.currentTask.IsIdle))
                        {
                            var gatherBtn = UIHelper.CreateButton(contentRT, "Gather",
                                SporefrontColors.SporeGreen,
                                UIHelper.HudTextColor, UIConstants.FontBody,
                                () => OnGatherRequested?.Invoke(vg.id, rp.id));
                            var btnLE = gatherBtn.gameObject.AddComponent<LayoutElement>();
                            btnLE.preferredHeight = 34;
                            break;
                        }
                    }
                }
            }
        }

        private void BuildMoveSelectionView(GameState gameState, HexCoordinate coord)
        {
            // Breadcrumb header with back arrow
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

            var backArrow = UIHelper.CreateButton(headerRow.transform, "<",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                () => { showingMoveSelection = false; Rebuild(cachedGameState); });
            var arrowLE = backArrow.gameObject.AddComponent<LayoutElement>();
            arrowLE.preferredWidth = 28;
            arrowLE.preferredHeight = 28;

            var header = UIHelper.CreateLabel(headerRow.transform, $"Move to ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.flexibleWidth = 1;
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // Armies section
            var armies = gameState.GetArmiesForPlayer(localPlayerID);
            bool hasArmies = false;
            if (armies != null && armies.Count > 0)
            {
                foreach (var army in armies)
                {
                    if (army.isInCombat) continue;
                    if (!hasArmies)
                    {
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Armies",
                            UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasArmies = true;
                    }

                    int total = army.GetTotalUnits();
                    var ac = army.coordinate;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        $"{army.name} ({total}) at ({ac.q},{ac.r})", UIConstants.FontBody);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;
                    nameLE.preferredHeight = 30;

                    var capturedID = army.id;
                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                        () =>
                        {
                            OnMoveEntityToTile?.Invoke(capturedID, coord, true);
                            showingMoveSelection = false;
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 56;
                    btnLE.preferredHeight = 30;
                }
            }

            // Villagers section
            var villagerGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            bool hasVillagers = false;
            if (villagerGroups != null && villagerGroups.Count > 0)
            {
                foreach (var group in villagerGroups)
                {
                    if (!hasVillagers)
                    {
                        if (hasArmies) UIHelper.CreateDivider(contentRT);
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Villagers",
                            UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasVillagers = true;
                    }

                    var gc = group.coordinate;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        $"Villagers ({group.villagerCount}) at ({gc.q},{gc.r})", UIConstants.FontBody);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;
                    nameLE.preferredHeight = 30;

                    var capturedID = group.id;
                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                        () =>
                        {
                            OnMoveEntityToTile?.Invoke(capturedID, coord, false);
                            showingMoveSelection = false;
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 56;
                    btnLE.preferredHeight = 30;
                }
            }

            if (!hasArmies && !hasVillagers)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No movable entities",
                    UIConstants.FontBody, null, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
            }
        }

        private void BuildAttackSelectionView(GameState gameState, HexCoordinate coord)
        {
            // Breadcrumb header with back arrow
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

            var backArrow = UIHelper.CreateButton(headerRow.transform, "<",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                () => { showingAttackSelection = false; Rebuild(cachedGameState); });
            var arrowLE = backArrow.gameObject.AddComponent<LayoutElement>();
            arrowLE.preferredWidth = 28;
            arrowLE.preferredHeight = 28;

            var header = UIHelper.CreateLabel(headerRow.transform, $"Attack ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.flexibleWidth = 1;
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // List player's armies that can attack
            var armies = gameState.GetArmiesForPlayer(localPlayerID);
            bool hasArmies = false;
            if (armies != null && armies.Count > 0)
            {
                foreach (var army in armies)
                {
                    if (army.isInCombat || army.isRetreating) continue;
                    if (!hasArmies)
                    {
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Your Armies",
                            UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasArmies = true;
                    }

                    int total = army.GetTotalUnits();
                    var ac = army.coordinate;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        $"{army.name} ({total}) at ({ac.q},{ac.r})", UIConstants.FontBody);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;
                    nameLE.preferredHeight = 30;

                    var capturedID = army.id;
                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody,
                        () =>
                        {
                            OnAttackEntityToTile?.Invoke(capturedID, coord);
                            showingAttackSelection = false;
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 56;
                    btnLE.preferredHeight = 30;
                }
            }

            if (!hasArmies)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No available armies",
                    UIConstants.FontBody, null, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateActionButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.InkMid, UIHelper.HudTextColor, UIConstants.FontBody, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 56;
            le.preferredHeight = 34;
            return btn;
        }
    }
}
