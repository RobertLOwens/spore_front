// ============================================================================
// FILE: Visual/VillagerDeployPanel.cs
// PURPOSE: Left-side panel for deploying villagers from a building and merging
//          villager groups. Two tabs: "Deploy New" and "Join Existing".
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
    public class VillagerDeployPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, int> OnDeployNew;             // buildingID, count
        public event Action<Guid, Guid> OnJoinExisting;         // buildingID, targetGroupID
        public event Action<Guid, Guid, int, int> OnMerge;      // groupA, groupB, countA, countB
        public event Action OnClose;

        // ================================================================
        // Types
        // ================================================================

        private enum TabMode
        {
            DeployNew,
            JoinExisting
        }

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private Guid? currentBuildingID;
        private Guid localPlayerID;

        private TabMode currentTab = TabMode.DeployNew;
        private int deployCount = 1;
        private Guid? selectedGroupID;

        // Merge sub-panel state
        private bool showMergePanel;
        private Guid? mergeGroupA;
        private Guid? mergeGroupB;
        private int mergeCountA;
        private int mergeCountB;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Left-anchored slide-out panel, 300px wide
            panel = UIHelper.CreatePanel(canvasTransform, "VillagerDeployPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.05f);
            rt.anchorMax = new Vector2(0, 0.95f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(300, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "DeployScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Bottom close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
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

        public void Show(GameState gameState, Guid buildingID)
        {
            currentBuildingID = buildingID;
            currentTab = TabMode.DeployNew;
            deployCount = 1;
            selectedGroupID = null;
            showMergePanel = false;
            mergeGroupA = null;
            mergeGroupB = null;
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentBuildingID = null;
            showMergePanel = false;
            panel.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentBuildingID.HasValue || !panel.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentBuildingID.HasValue) return;
            var building = gameState.GetBuilding(currentBuildingID.Value);
            if (building == null) { Hide(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Header
            var header = UIHelper.CreateLabel(contentRT, "Deploy Villagers",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            // Available count
            int available = building.villagerGarrison;
            var availLabel = UIHelper.CreateLabel(contentRT,
                $"Available: {available} villagers", 12,
                SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var availLE = availLabel.gameObject.AddComponent<LayoutElement>();
            availLE.preferredHeight = 20;

            UIHelper.CreateDivider(contentRT);

            // Tab buttons
            BuildTabButtons();
            UIHelper.CreateDivider(contentRT);

            // Tab content
            if (showMergePanel)
            {
                BuildMergeSubPanel(gameState);
            }
            else if (currentTab == TabMode.DeployNew)
            {
                BuildDeployNewTab(gameState, building, available);
            }
            else
            {
                BuildJoinExistingTab(gameState, building);
            }
        }

        // ================================================================
        // Tab Buttons
        // ================================================================

        private void BuildTabButtons()
        {
            var tabRow = UIHelper.CreateHorizontalRow(contentRT, 32f, 4f);

            var deployTabBtn = UIHelper.CreateButton(tabRow.transform, "Deploy New",
                currentTab == TabMode.DeployNew ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                currentTab == TabMode.DeployNew ? UIHelper.HudTextColor : UIHelper.ButtonText, 12, () =>
                {
                    currentTab = TabMode.DeployNew;
                    showMergePanel = false;
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var deployTabLE = deployTabBtn.gameObject.AddComponent<LayoutElement>();
            deployTabLE.flexibleWidth = 1;
            deployTabLE.preferredHeight = 32;

            var joinTabBtn = UIHelper.CreateButton(tabRow.transform, "Join Existing",
                currentTab == TabMode.JoinExisting ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                currentTab == TabMode.JoinExisting ? UIHelper.HudTextColor : UIHelper.ButtonText, 12, () =>
                {
                    currentTab = TabMode.JoinExisting;
                    showMergePanel = false;
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var joinTabLE = joinTabBtn.gameObject.AddComponent<LayoutElement>();
            joinTabLE.flexibleWidth = 1;
            joinTabLE.preferredHeight = 32;
        }

        // ================================================================
        // Deploy New Tab
        // ================================================================

        private void BuildDeployNewTab(GameState gameState, BuildingData building, int available)
        {
            if (available <= 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "No villagers available to deploy", 12,
                    SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 40;
                return;
            }

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Deploy Count",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            // Clamp deploy count to available
            if (deployCount > available) deployCount = available;
            if (deployCount < 1) deployCount = 1;

            // Count label
            var countLabel = UIHelper.CreateLabel(contentRT,
                $"  Villagers to deploy: {deployCount}", 12);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredHeight = 20;

            // Slider
            var slider = UIHelper.CreateSlider(contentRT, 1, available, true, (val) =>
            {
                deployCount = (int)val;
                Rebuild(GameEngine.Instance.GetGameState());
            });
            slider.value = deployCount;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.preferredHeight = 24;

            // Spacer
            var spacer = UIHelper.CreatePanel(contentRT, "Spacer", Color.clear);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 8;

            // Deploy button
            var capturedBuildingID = building.id;
            var capturedCount = deployCount;
            var deployBtn = UIHelper.CreateButton(contentRT, $"Deploy {deployCount} Villagers",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 13, () =>
                {
                    OnDeployNew?.Invoke(capturedBuildingID, capturedCount);
                    Hide();
                });
            var deployBtnLE = deployBtn.gameObject.AddComponent<LayoutElement>();
            deployBtnLE.preferredHeight = 36;
        }

        // ================================================================
        // Join Existing Tab
        // ================================================================

        private void BuildJoinExistingTab(GameState gameState, BuildingData building)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Nearby Groups",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var groups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            if (groups == null || groups.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "  No villager groups available", 12, SporefrontColors.InkFaded);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 20;
                return;
            }

            // Sort by distance to building
            groups.Sort((a, b) =>
                a.coordinate.Distance(building.coordinate).CompareTo(
                    b.coordinate.Distance(building.coordinate)));

            foreach (var group in groups)
            {
                if (group.villagerCount <= 0) continue;

                int distance = group.coordinate.Distance(building.coordinate);
                string taskDesc = group.currentTask != null && !group.currentTask.IsIdle
                    ? group.currentTask.DisplayName : "Idle";

                var row = UIHelper.CreatePanel(contentRT, "GroupRow", Color.clear);
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 52;

                var vlg = row.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 2;
                vlg.padding = new RectOffset(8, 8, 2, 2);
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Name + count + distance
                var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{group.name} ({group.villagerCount})", 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                var distLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{distance} tiles", 11, SporefrontColors.InkLight);
                var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
                distLE.preferredWidth = 55;

                // Task + actions
                var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);
                var taskLabel = UIHelper.CreateLabel(actionRow.transform,
                    taskDesc, 11, SporefrontColors.InkLight);
                var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
                taskLE.flexibleWidth = 1;

                // Join button
                var capturedBuildingID = building.id;
                var capturedGroupID = group.id;
                var joinBtn = UIHelper.CreateButton(actionRow.transform, "Join",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 11, () =>
                    {
                        OnJoinExisting?.Invoke(capturedBuildingID, capturedGroupID);
                        Hide();
                    });
                var joinBtnLE = joinBtn.gameObject.AddComponent<LayoutElement>();
                joinBtnLE.preferredWidth = 50;
                joinBtnLE.preferredHeight = 24;

                // Merge button (opens merge sub-panel)
                var mergeBtn = UIHelper.CreateButton(actionRow.transform, "Merge",
                    SporefrontColors.SporeTeal, UIHelper.HudTextColor, 11, () =>
                    {
                        showMergePanel = true;
                        mergeGroupA = capturedGroupID;
                        mergeGroupB = null;
                        Rebuild(GameEngine.Instance.GetGameState());
                    });
                var mergeBtnLE = mergeBtn.gameObject.AddComponent<LayoutElement>();
                mergeBtnLE.preferredWidth = 50;
                mergeBtnLE.preferredHeight = 24;

                UIHelper.CreateDivider(contentRT, null, 1);
            }
        }

        // ================================================================
        // Merge Sub-Panel
        // ================================================================

        private void BuildMergeSubPanel(GameState gameState)
        {
            var header = UIHelper.CreateLabel(contentRT, "Merge Groups",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 24;

            // Back button
            var backBtn = UIHelper.CreateButton(contentRT, "Back",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11, () =>
                {
                    showMergePanel = false;
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var backBtnLE = backBtn.gameObject.AddComponent<LayoutElement>();
            backBtnLE.preferredHeight = 26;

            UIHelper.CreateDivider(contentRT);

            if (!mergeGroupA.HasValue)
            {
                var infoLabel = UIHelper.CreateLabel(contentRT,
                    "Select a group to merge", 12, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
                infoLE.preferredHeight = 30;
                return;
            }

            var groupA = gameState.GetVillagerGroup(mergeGroupA.Value);
            if (groupA == null) { showMergePanel = false; return; }

            // Group A info
            var groupALabel = UIHelper.CreateLabel(contentRT,
                $"Group A: {groupA.name} ({groupA.villagerCount} villagers)", 12);
            var groupALE = groupALabel.gameObject.AddComponent<LayoutElement>();
            groupALE.preferredHeight = 22;

            UIHelper.CreateDivider(contentRT);

            // Select Group B if not selected
            if (!mergeGroupB.HasValue)
            {
                var selectLabel = UIHelper.CreateLabel(contentRT,
                    "Select group to merge with:", 12, UIHelper.BodyTextColor);
                var selectLE = selectLabel.gameObject.AddComponent<LayoutElement>();
                selectLE.preferredHeight = 22;

                var groups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                foreach (var group in groups)
                {
                    if (group.id == mergeGroupA.Value) continue;
                    if (group.villagerCount <= 0) continue;

                    var capturedGroupBID = group.id;
                    var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

                    var gNameLabel = UIHelper.CreateLabel(row.transform,
                        $"{group.name} ({group.villagerCount})", 12);
                    var gNameLE = gNameLabel.gameObject.AddComponent<LayoutElement>();
                    gNameLE.flexibleWidth = 1;

                    var selectBtn = UIHelper.CreateButton(row.transform, "Select",
                        SporefrontColors.SporeTeal, UIHelper.HudTextColor, 11, () =>
                        {
                            mergeGroupB = capturedGroupBID;
                            var gB = gameState.GetVillagerGroup(capturedGroupBID);
                            int totalVillagers = groupA.villagerCount + (gB != null ? gB.villagerCount : 0);
                            mergeCountA = totalVillagers;
                            mergeCountB = 0;
                            Rebuild(GameEngine.Instance.GetGameState());
                        });
                    var selectBtnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    selectBtnLE.preferredWidth = 55;
                    selectBtnLE.preferredHeight = 28;
                }
                return;
            }

            // Group B selected - show split slider
            var groupB = gameState.GetVillagerGroup(mergeGroupB.Value);
            if (groupB == null) { mergeGroupB = null; return; }

            var groupBLabel = UIHelper.CreateLabel(contentRT,
                $"Group B: {groupB.name} ({groupB.villagerCount} villagers)", 12);
            var groupBLE = groupBLabel.gameObject.AddComponent<LayoutElement>();
            groupBLE.preferredHeight = 22;

            UIHelper.CreateDivider(contentRT);

            int totalCount = groupA.villagerCount + groupB.villagerCount;

            // Split label
            var splitHeader = UIHelper.CreateLabel(contentRT, "Split Distribution",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var splitHeaderLE = splitHeader.gameObject.AddComponent<LayoutElement>();
            splitHeaderLE.preferredHeight = 22;

            // Current split display
            var splitLabel = UIHelper.CreateLabel(contentRT,
                $"  {groupA.name}: {mergeCountA}  |  {groupB.name}: {mergeCountB}", 12);
            var splitLabelLE = splitLabel.gameObject.AddComponent<LayoutElement>();
            splitLabelLE.preferredHeight = 22;

            // Slider: how many go to group A
            var splitSlider = UIHelper.CreateSlider(contentRT, 0, totalCount, true, (val) =>
            {
                mergeCountA = (int)val;
                mergeCountB = totalCount - mergeCountA;
                Rebuild(GameEngine.Instance.GetGameState());
            });
            splitSlider.value = mergeCountA;
            var splitSliderLE = splitSlider.gameObject.AddComponent<LayoutElement>();
            splitSliderLE.preferredHeight = 24;

            // Spacer
            var spacer = UIHelper.CreatePanel(contentRT, "Spacer", Color.clear);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 8;

            // Quick Merge All button (all into Group A)
            var mergeAllBtn = UIHelper.CreateButton(contentRT, "Quick Merge All into " + groupA.name,
                SporefrontColors.SporeAmber, UIHelper.ButtonText, 12, () =>
                {
                    OnMerge?.Invoke(mergeGroupA.Value, mergeGroupB.Value, totalCount, 0);
                    showMergePanel = false;
                    Hide();
                });
            var mergeAllBtnLE = mergeAllBtn.gameObject.AddComponent<LayoutElement>();
            mergeAllBtnLE.preferredHeight = 32;

            // Split & Confirm button
            var capturedA = mergeGroupA.Value;
            var capturedB = mergeGroupB.Value;
            var capturedCountA = mergeCountA;
            var capturedCountB = mergeCountB;
            var splitConfirmBtn = UIHelper.CreateButton(contentRT, "Split & Confirm",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 13, () =>
                {
                    OnMerge?.Invoke(capturedA, capturedB, capturedCountA, capturedCountB);
                    showMergePanel = false;
                    Hide();
                });
            var splitConfirmLE = splitConfirmBtn.gameObject.AddComponent<LayoutElement>();
            splitConfirmLE.preferredHeight = 36;
        }
    }
}
