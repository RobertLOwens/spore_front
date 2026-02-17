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
        public event Action<Guid, HexCoordinate> OnAttackRequested;
        public event Action<Guid, Guid> OnGatherRequested; // villagerGroupID, resourcePointID

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private HexCoordinate? currentCoord;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Right-anchored panel, 260px wide
            panel = UIHelper.CreatePanel(canvasTransform, "TileInfoPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-260, 50); // 50px bottom margin
            rt.offsetMax = new Vector2(0, -50);    // 50px top margin (below resource bar)

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
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentCoord = null;
            panel.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !panel.activeSelf) return;
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

            // Clear existing content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

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
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 22;

            foreach (var army in armies)
            {
                bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;
                string status = army.isEntrenched ? " [E]" : army.isInCombat ? " [C]" : "";
                int total = army.GetTotalUnits();

                var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform,
                    $"{army.name}{status} ({total})", 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;
                nameLE.preferredHeight = 26;

                if (isOwned)
                {
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
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
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
                        break; // One gather button is enough
                    }
                }
            }
        }
    }
}
