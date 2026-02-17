// ============================================================================
// FILE: Visual/ReinforcePanel.cs
// PURPOSE: Left-side slide-out panel for sending garrison reinforcements to
//          an army. Lists buildings with garrisoned units and per-unit sliders.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class ReinforcePanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, Guid, Dictionary<MilitaryUnitType, int>> OnReinforceConfirmed;
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private Guid? currentArmyID;
        private Guid localPlayerID;

        // Per-building selection state: buildingID -> (unitType -> selected count)
        private Dictionary<Guid, Dictionary<MilitaryUnitType, int>> selections =
            new Dictionary<Guid, Dictionary<MilitaryUnitType, int>>();

        // Track expanded buildings
        private HashSet<Guid> expandedBuildings = new HashSet<Guid>();

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Left-anchored slide-out panel, 300px wide
            panel = UIHelper.CreatePanel(canvasTransform, "ReinforcePanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.1f);
            rt.anchorMax = new Vector2(0, 0.9f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(300, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "ReinforceScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Bottom close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = Vector2.zero;
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 40);

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState, Guid targetArmyID)
        {
            currentArmyID = targetArmyID;
            selections.Clear();
            expandedBuildings.Clear();
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentArmyID = null;
            selections.Clear();
            expandedBuildings.Clear();
            panel.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentArmyID.HasValue || !panel.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentArmyID.HasValue) return;
            var army = gameState.GetArmy(currentArmyID.Value);
            if (army == null) { Hide(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Header
            var header = UIHelper.CreateLabel(contentRT, "Reinforce Army",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            UIHelper.CreateDivider(contentRT);

            // Target army info
            BuildArmyInfoSection(army);
            UIHelper.CreateDivider(contentRT);

            // Buildings with garrison
            BuildBuildingsList(gameState, army);
        }

        // ================================================================
        // Army Info Section
        // ================================================================

        private void BuildArmyInfoSection(ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Target Army",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var nameLabel = UIHelper.CreateLabel(contentRT,
                $"  {army.name} ({army.GetTotalUnits()} units)", 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 20;

            string status = army.isEntrenched ? "Entrenched" :
                            army.isInCombat ? "In Combat" :
                            army.isRetreating ? "Retreating" : "Idle";
            var statusLabel = UIHelper.CreateLabel(contentRT,
                $"  Status: {status}", 12, SporefrontColors.InkLight);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 20;

            var coordLabel = UIHelper.CreateLabel(contentRT,
                $"  Location: ({army.coordinate.q},{army.coordinate.r})", 12, SporefrontColors.InkLight);
            var coordLE = coordLabel.gameObject.AddComponent<LayoutElement>();
            coordLE.preferredHeight = 20;
        }

        // ================================================================
        // Buildings List
        // ================================================================

        private void BuildBuildingsList(GameState gameState, ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Garrisons",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var buildings = gameState.GetBuildingsForPlayer(localPlayerID);
            bool anyGarrisoned = false;

            // Sort by distance to army
            buildings.Sort((a, b) =>
                a.coordinate.Distance(army.coordinate).CompareTo(
                    b.coordinate.Distance(army.coordinate)));

            foreach (var building in buildings)
            {
                if (!building.IsOperational) continue;
                if (building.garrison == null) continue;

                int totalGarrisoned = 0;
                foreach (var kvp in building.garrison)
                    totalGarrisoned += kvp.Value;

                if (totalGarrisoned <= 0) continue;
                anyGarrisoned = true;

                int distance = building.coordinate.Distance(army.coordinate);
                bool isExpanded = expandedBuildings.Contains(building.id);

                // Building header row
                var buildingRow = UIHelper.CreatePanel(contentRT, "BuildingRow",
                    SporefrontColors.ParchmentMid);
                var buildingRowLE = buildingRow.AddComponent<LayoutElement>();
                buildingRowLE.preferredHeight = 36;

                var buildingHLG = buildingRow.AddComponent<HorizontalLayoutGroup>();
                buildingHLG.spacing = 4f;
                buildingHLG.padding = new RectOffset(8, 8, 4, 4);
                buildingHLG.childForceExpandWidth = false;
                buildingHLG.childForceExpandHeight = true;
                buildingHLG.childControlWidth = false;
                buildingHLG.childControlHeight = true;

                // Expand/collapse toggle
                var capturedBuildingID = building.id;
                var toggleBtn = UIHelper.CreateButton(buildingRow.transform,
                    isExpanded ? "-" : "+",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, 12, null);
                var toggleBtnLE = toggleBtn.gameObject.AddComponent<LayoutElement>();
                toggleBtnLE.preferredWidth = 28;

                toggleBtn.onClick.AddListener(() =>
                {
                    if (expandedBuildings.Contains(capturedBuildingID))
                        expandedBuildings.Remove(capturedBuildingID);
                    else
                        expandedBuildings.Add(capturedBuildingID);
                    Rebuild(GameEngine.Instance.GetGameState());
                });

                // Building name + garrison count
                var bNameLabel = UIHelper.CreateLabel(buildingRow.transform,
                    $"{building.buildingType.DisplayName()} ({totalGarrisoned})", 12);
                var bNameLE = bNameLabel.gameObject.AddComponent<LayoutElement>();
                bNameLE.flexibleWidth = 1;

                // Travel distance
                var travelLabel = UIHelper.CreateLabel(buildingRow.transform,
                    $"{distance} tiles", 11, SporefrontColors.InkLight);
                var travelLE = travelLabel.gameObject.AddComponent<LayoutElement>();
                travelLE.preferredWidth = 55;

                // Expanded: unit type sliders
                if (isExpanded)
                {
                    if (!selections.ContainsKey(building.id))
                        selections[building.id] = new Dictionary<MilitaryUnitType, int>();

                    var sel = selections[building.id];

                    foreach (var kvp in building.garrison)
                    {
                        if (kvp.Value <= 0) continue;
                        var unitType = kvp.Key;
                        int maxCount = kvp.Value;

                        if (!sel.ContainsKey(unitType))
                            sel[unitType] = 0;

                        BuildUnitSliderRow(building.id, unitType, maxCount, sel, army);
                    }

                    // Confirm button for this building
                    int totalSelected = 0;
                    foreach (var s in sel.Values) totalSelected += s;

                    if (totalSelected > 0)
                    {
                        var sendBuildingID = building.id;
                        var sendArmyID = army.id;
                        var sendUnits = new Dictionary<MilitaryUnitType, int>(sel);

                        var confirmBtn = UIHelper.CreateButton(contentRT, $"Send {totalSelected} Units",
                            SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                            {
                                // Filter out zero entries
                                var filtered = new Dictionary<MilitaryUnitType, int>();
                                foreach (var entry in sendUnits)
                                {
                                    if (entry.Value > 0) filtered[entry.Key] = entry.Value;
                                }
                                if (filtered.Count > 0)
                                {
                                    OnReinforceConfirmed?.Invoke(sendBuildingID, sendArmyID, filtered);
                                    Hide();
                                }
                            });
                        var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                        confirmLE.preferredHeight = 32;
                    }

                    UIHelper.CreateDivider(contentRT);
                }
            }

            if (!anyGarrisoned)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "  No buildings with garrisoned units", 12, SporefrontColors.InkFaded);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Unit Slider Row
        // ================================================================

        private void BuildUnitSliderRow(Guid buildingID, MilitaryUnitType unitType,
            int maxCount, Dictionary<MilitaryUnitType, int> sel, ArmyData army)
        {
            var row = UIHelper.CreatePanel(contentRT, "UnitRow", Color.clear);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 44;

            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(16, 8, 2, 2);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            int currentVal = sel.ContainsKey(unitType) ? sel[unitType] : 0;

            // Label row
            var labelRow = UIHelper.CreateHorizontalRow(row.transform, 18f, 4f);
            var unitLabel = UIHelper.CreateLabel(labelRow.transform,
                unitType.DisplayName(), 12);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.flexibleWidth = 1;

            var countLabel = UIHelper.CreateLabel(labelRow.transform,
                $"{currentVal}/{maxCount}", 12, SporefrontColors.InkLight,
                TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 50;

            // Slider
            var capturedUnitType = unitType;
            var capturedBuildingID = buildingID;
            var slider = UIHelper.CreateSlider(row.transform, 0, maxCount, true, (val) =>
            {
                if (selections.ContainsKey(capturedBuildingID))
                {
                    selections[capturedBuildingID][capturedUnitType] = (int)val;
                    // Rebuild to update confirm button total
                    Rebuild(GameEngine.Instance.GetGameState());
                }
            });
            slider.value = currentVal;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.preferredHeight = 20;
        }
    }
}
