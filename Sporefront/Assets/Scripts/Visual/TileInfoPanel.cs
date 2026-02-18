// ============================================================================
// FILE: Visual/TileInfoPanel.cs
// PURPOSE: Right-side tile details panel with contextual info (#14)
//          Shows terrain, buildings, armies, villagers, resources, actions
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

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

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private HexCoordinate? currentCoord;
        private Guid localPlayerID;
        private bool showingMoveSelection;
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
            hasCachedFingerprint = false;
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentCoord = null;
            showingMoveSelection = false;
            panel.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !panel.activeSelf) return;

            // Skip full rebuild if structure hasn't changed
            if (hasCachedFingerprint && !showingMoveSelection && TileFingerprintMatches(gameState))
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

            // Move selection view — show entity list instead of normal content
            var panelImg = panel.GetComponent<Image>();
            if (showingMoveSelection)
            {
                // Subtle tint to distinguish sub-flow
                panelImg.color = Color.Lerp(UIHelper.PanelBg, SporefrontColors.SporeTeal, 0.06f);
                BuildMoveSelectionView(gameState, coord);
                return;
            }
            panelImg.color = UIHelper.PanelBg;

            var tileNullable = gameState.mapData.GetTile(coord);
            if (!tileNullable.HasValue) return;
            var tile = tileNullable.Value;

            // 1. Header: terrain + coordinate
            var header = UIHelper.CreateLabel(contentRT, $"{tile.terrain} ({coord.q},{coord.r})",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
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
                BuildVillagersSection(villagers, coord);
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
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, -1,
                    () => OnBuildRequested?.Invoke(coord));
                var btnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 32;
            }

            // 7. "Move Here" — destination-first move flow
            var moveHereBtn = UIHelper.CreateButton(contentRT, "Move Here",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, -1,
                () => { showingMoveSelection = true; Rebuild(cachedGameState); });
            var moveHereBtnLE = moveHereBtn.gameObject.AddComponent<LayoutElement>();
            moveHereBtnLE.preferredHeight = 32;

            // Cache fingerprint for incremental refresh
            CacheTileFingerprint(gameState);
        }

        // ================================================================
        // Fingerprint & Incremental Update
        // ================================================================

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
                $"{displayName} Lv.{building.level}", -1, null, TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            // State
            string stateStr = building.state.ToString();
            var stateLabel = UIHelper.CreateLabel(contentRT, stateStr);
            var stateLE = stateLabel.gameObject.AddComponent<LayoutElement>();
            stateLE.preferredHeight = 20;

            // HP bar
            if (building.maxHealth > 0)
            {
                var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 12f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
                float hpPct = (float)(building.health / building.maxHealth);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.preferredHeight = 12;
                buildingHPFill = fill;
            }

            // Details button (owned buildings only)
            if (isOwned && building.IsOperational)
            {
                var detailBtn = UIHelper.CreateButton(contentRT, "Details",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, 12,
                    () => OnBuildingDetailRequested?.Invoke(building.id));
                var btnLE = detailBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 28;
            }
        }

        private void BuildArmiesSection(List<ArmyData> armies, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Armies",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 22;

            foreach (var army in armies)
            {
                bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;
                string status = UIHelper.FormatArmyStatus(army);
                int total = army.GetTotalUnits();

                var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform,
                    $"{army.name}{status} ({total})", 12);
                nameLabel.supportRichText = true;
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;
                nameLE.preferredHeight = 26;

                if (isOwned)
                {
                    if (!army.isInCombat)
                    {
                        var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                            SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11,
                            () => OnArmyMoveRequested?.Invoke(army.id));
                        var moveBtnLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                        moveBtnLE.preferredWidth = 44;
                        moveBtnLE.preferredHeight = 26;
                    }

                    var detailBtn = UIHelper.CreateButton(row.transform, "Info",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11,
                        () => OnArmyDetailRequested?.Invoke(army.id));
                    var btnLE = detailBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 40;
                    btnLE.preferredHeight = 26;
                }
                else if (army.ownerID.HasValue && army.ownerID.Value != localPlayerID)
                {
                    // Enemy army — Attack button for local player's armies
                    var attackBtn = UIHelper.CreateButton(row.transform, "Atk",
                        SporefrontColors.SporeRed, UIHelper.HudTextColor, 11,
                        () => OnAttackRequested?.Invoke(army.id, army.coordinate));
                    var btnLE = attackBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 36;
                    btnLE.preferredHeight = 26;
                }
            }
        }

        private void BuildVillagersSection(List<VillagerGroupData> groups, HexCoordinate coord)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Villagers",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 22;

            foreach (var group in groups)
            {
                bool isOwned = group.ownerID.HasValue && group.ownerID.Value == localPlayerID;
                string task = group.currentTask != null ? group.currentTask.DisplayName : "Idle";

                var row = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);

                var label = UIHelper.CreateLabel(row.transform,
                    $"{group.villagerCount}x — {task}", 12);
                var labelLE = label.gameObject.AddComponent<LayoutElement>();
                labelLE.flexibleWidth = 1;

                if (isOwned)
                {
                    var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11,
                        () => OnMoveRequested?.Invoke(group.id, coord));
                    var btnLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 44;
                    btnLE.preferredHeight = 24;
                }
            }
        }

        private void BuildResourceSection(ResourcePointData rp, GameState gameState)
        {
            var label = UIHelper.CreateLabel(contentRT,
                $"Resource: {rp.resourceType} ({rp.remainingAmount})", -1, null,
                TextAnchor.MiddleLeft);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 22;

            bool isHuntable = rp.resourceType.IsHuntable() && rp.currentHealth > 0;

            if (isHuntable)
            {
                // Hunt button — opens GatherPanel in hunt mode with villager selection
                var capturedRPID = rp.id;
                var huntBtn = UIHelper.CreateButton(contentRT, "Hunt",
                    SporefrontColors.SporeRed,
                    UIHelper.HudTextColor, 12,
                    () => OnHuntRequested?.Invoke(capturedRPID));
                var btnLE = huntBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 28;
            }
            else
            {
                // Gather button if local player has villagers nearby
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
                                UIHelper.HudTextColor, 12,
                                () => OnGatherRequested?.Invoke(vg.id, rp.id));
                            var btnLE = gatherBtn.gameObject.AddComponent<LayoutElement>();
                            btnLE.preferredHeight = 28;
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
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
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
                        shLE.preferredHeight = 22;
                        hasArmies = true;
                    }

                    int total = army.GetTotalUnits();
                    var ac = army.coordinate;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        $"{army.name} ({total}) at ({ac.q},{ac.r})", 12);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;
                    nameLE.preferredHeight = 26;

                    var capturedID = army.id;
                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11,
                        () =>
                        {
                            OnMoveEntityToTile?.Invoke(capturedID, coord, true);
                            showingMoveSelection = false;
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 50;
                    btnLE.preferredHeight = 26;
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
                        shLE.preferredHeight = 22;
                        hasVillagers = true;
                    }

                    var gc = group.coordinate;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        $"Villagers ({group.villagerCount}) at ({gc.q},{gc.r})", 12);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;
                    nameLE.preferredHeight = 26;

                    var capturedID = group.id;
                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11,
                        () =>
                        {
                            OnMoveEntityToTile?.Invoke(capturedID, coord, false);
                            showingMoveSelection = false;
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 50;
                    btnLE.preferredHeight = 26;
                }
            }

            if (!hasArmies && !hasVillagers)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No movable entities", -1, null,
                    TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
            }

        }
    }
}
