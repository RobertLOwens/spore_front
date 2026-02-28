// ============================================================================
// FILE: Visual/ReinforcePanel.cs
// PURPOSE: Centered modal panel for sending garrison reinforcements to
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

        private GameObject backdrop;
        private GameObject modalPanel;
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

            // Full-screen backdrop with click-to-dismiss
            backdrop = UIHelper.CreatePanel(canvasTransform, "ReinforceBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Centered modal panel (ModalMedium — needs more space for expandable rows)
            modalPanel = UIHelper.CreatePanel(backdrop.transform, "ReinforceModal", UIHelper.PanelBg);
            var rt = modalPanel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalMediumW, UIConstants.ModalMediumH);

            // Header "Reinforce Army" — fixed at top
            var header = UIHelper.CreateLabel(modalPanel.transform, "Reinforce Army",
                UIConstants.FontTitle, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.offsetMin = new Vector2(12, -40);
            headerRT.offsetMax = new Vector2(-12, -6);

            // Scroll area for army info + buildings list
            var scroll = UIHelper.CreateScrollView(modalPanel.transform, "ReinforceScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 52); // Space for close button
            scrollRT.offsetMax = new Vector2(0, -42); // Space for header

            // Close button at bottom
            var closeBtn = UIHelper.CreateButton(modalPanel.transform, "Close",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontBody,
                Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 48);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
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
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            currentArmyID = null;
            selections.Clear();
            expandedBuildings.Clear();
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentArmyID.HasValue || !backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentArmyID.HasValue) return;
            var army = gameState.GetArmy(currentArmyID.Value);
            if (army == null) { Hide(); return; }

            // Clear scroll content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var nameLabel = UIHelper.CreateLabel(contentRT,
                $"  {army.name} ({army.GetTotalUnits()} units)", UIConstants.FontBody);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 26;

            string status = army.isEntrenched ? "Entrenched" :
                            army.isInCombat ? "In Combat" :
                            army.isRetreating ? "Retreating" : "Idle";
            var statusLabel = UIHelper.CreateLabel(contentRT,
                $"  Status: {status}", UIConstants.FontSmall, SporefrontColors.InkLight);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 24;

            var coordLabel = UIHelper.CreateLabel(contentRT,
                $"  Location: ({army.coordinate.q},{army.coordinate.r})", UIConstants.FontSmall, SporefrontColors.InkLight);
            var coordLE = coordLabel.gameObject.AddComponent<LayoutElement>();
            coordLE.preferredHeight = 24;
        }

        // ================================================================
        // Buildings List
        // ================================================================

        private void BuildBuildingsList(GameState gameState, ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Garrisons",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

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
                buildingRowLE.preferredHeight = 44;

                var buildingHLG = buildingRow.AddComponent<HorizontalLayoutGroup>();
                buildingHLG.spacing = 4f;
                buildingHLG.padding = new RectOffset(10, 10, 6, 6);
                buildingHLG.childForceExpandWidth = false;
                buildingHLG.childForceExpandHeight = true;
                buildingHLG.childControlWidth = false;
                buildingHLG.childControlHeight = true;

                // Expand/collapse toggle
                var capturedBuildingID = building.id;
                var toggleBtn = UIHelper.CreateButton(buildingRow.transform,
                    isExpanded ? "-" : "+",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontSmall, null);
                var toggleBtnLE = toggleBtn.gameObject.AddComponent<LayoutElement>();
                toggleBtnLE.preferredWidth = 32;

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
                    $"{building.buildingType.DisplayName()} ({totalGarrisoned})", UIConstants.FontBody);
                var bNameLE = bNameLabel.gameObject.AddComponent<LayoutElement>();
                bNameLE.flexibleWidth = 1;

                // Travel distance
                var travelLabel = UIHelper.CreateLabel(buildingRow.transform,
                    $"{distance} tiles", UIConstants.FontSmall, SporefrontColors.InkLight);
                var travelLE = travelLabel.gameObject.AddComponent<LayoutElement>();
                travelLE.preferredWidth = 70;

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
                            SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontBody, () =>
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
                        confirmLE.preferredHeight = 44;
                    }

                    UIHelper.CreateDivider(contentRT);
                }
            }

            if (!anyGarrisoned)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "  No buildings with garrisoned units", UIConstants.FontSmall, SporefrontColors.InkFaded);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 24;
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
            rowLE.preferredHeight = 52;

            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(16, 10, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            int currentVal = sel.ContainsKey(unitType) ? sel[unitType] : 0;

            // Label row
            var labelRow = UIHelper.CreateHorizontalRow(row.transform, 22f, 4f);
            var unitLabel = UIHelper.CreateLabel(labelRow.transform,
                unitType.DisplayName(), UIConstants.FontSmall);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.flexibleWidth = 1;

            var countLabel = UIHelper.CreateLabel(labelRow.transform,
                $"{currentVal}/{maxCount}", UIConstants.FontSmall, SporefrontColors.InkLight,
                TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 60;

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
            sliderLE.preferredHeight = 24;
        }
    }
}
