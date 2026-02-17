// ============================================================================
// FILE: Visual/BuildingDetailPanel.cs
// PURPOSE: Center modal for building management — training, garrison, deploy,
//          upgrades. Supports castle/fort buildings (#19)
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
    public class BuildingDetailPanel : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private GameObject backdrop;
        private RectTransform contentRT;
        private Guid? currentBuildingID;
        private Guid localPlayerID;

        // Cached references for refresh
        private Image hpFill;
        private Text hpLabel;
        private Text queueLabel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "BuildingDetailBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Main panel — centered 400x500
            panel = UIHelper.CreatePanel(backdrop.transform, "BuildingDetailPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 400, 500);

            // ScrollView inside panel
            var scroll = UIHelper.CreateScrollView(panel.transform, "DetailScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 40); // Space for close button
            scrollRT.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Close);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 36);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid buildingID, GameState gameState)
        {
            currentBuildingID = buildingID;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Close()
        {
            currentBuildingID = null;
            backdrop.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentBuildingID.HasValue || !backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild Content
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentBuildingID.HasValue) return;
            var building = gameState.GetBuilding(currentBuildingID.Value);
            if (building == null) { Close(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            hpFill = null;
            hpLabel = null;
            queueLabel = null;

            var player = gameState.GetPlayer(localPlayerID);

            // Header
            var header = UIHelper.CreateLabel(contentRT,
                $"{building.buildingType.DisplayName()} Lv.{building.level}",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 32;

            // HP bar
            BuildHPBar(building);

            // State info
            if (building.state != BuildingState.Completed)
            {
                var stateLabel = UIHelper.CreateLabel(contentRT, $"State: {building.state}");
                var stateLE = stateLabel.gameObject.AddComponent<LayoutElement>();
                stateLE.preferredHeight = 20;

                if (building.state == BuildingState.Constructing || building.state == BuildingState.Upgrading)
                {
                    var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 14f,
                        SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                    double progress = building.state == BuildingState.Constructing
                        ? building.constructionProgress
                        : building.upgradeProgress;
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)progress), 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.preferredHeight = 14;
                }
            }

            UIHelper.CreateDivider(contentRT);

            // Training section (military buildings + CC)
            if (building.IsOperational)
            {
                BuildTrainingSection(building, gameState, player);
                UIHelper.CreateDivider(contentRT);

                // Garrison section
                BuildGarrisonSection(building);
                UIHelper.CreateDivider(contentRT);

                // Deploy section
                BuildDeploySection(building, gameState);
                UIHelper.CreateDivider(contentRT);

                // Upgrade section
                BuildUpgradeSection(building, player);
            }
        }

        // ================================================================
        // HP Bar
        // ================================================================

        private void BuildHPBar(BuildingData building)
        {
            if (building.maxHealth <= 0) return;

            var row = UIHelper.CreateHorizontalRow(contentRT, 18f, 4f);

            hpLabel = UIHelper.CreateLabel(row.transform,
                $"HP: {(int)building.health}/{(int)building.maxHealth}", 11);
            var labelLE = hpLabel.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 100;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 14f,
                SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
            float pct = (float)(building.health / building.maxHealth);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
            hpFill = fill;
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 14;
        }

        // ================================================================
        // Training Section
        // ================================================================

        private void BuildTrainingSection(BuildingData building, GameState gameState, PlayerState player)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Training",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            // Villager training (City Center)
            if (building.CanTrainVillagers())
            {
                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);
                var nameLabel = UIHelper.CreateLabel(row.transform, "Villager", 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                var trainBtn = UIHelper.CreateButton(row.transform, "Train", null, null, 11, () =>
                {
                    var cmd = new TrainVillagerCommand(localPlayerID, building.id, 1);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
                var btnLE = trainBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 50;
            }

            // Military unit training
            var unitTypes = (MilitaryUnitType[])Enum.GetValues(typeof(MilitaryUnitType));
            foreach (var ut in unitTypes)
            {
                if (!building.CanTrain(ut)) continue;

                var cost = ut.TrainingCost();
                bool canAfford = player != null && player.CanAfford(cost);

                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform, ut.DisplayName(), 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                var costLabel = UIHelper.CreateLabel(row.transform, FormatCost(cost), 10,
                    canAfford ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
                costLabel.supportRichText = true;
                var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
                costLE.preferredWidth = 80;

                var capturedUT = ut;
                var trainBtn = UIHelper.CreateButton(row.transform, "Train",
                    canAfford ? SporefrontColors.ParchmentDark : SporefrontColors.InkFaded,
                    UIHelper.ButtonText, 11, () =>
                    {
                        var cmd = new TrainMilitaryCommand(localPlayerID, building.id, capturedUT, 1);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                trainBtn.interactable = canAfford;
                var btnLE = trainBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 50;
            }

            // Training queue display
            if (building.trainingQueue != null && building.trainingQueue.Count > 0)
            {
                UIHelper.CreateDivider(contentRT, null, 1);
                queueLabel = UIHelper.CreateLabel(contentRT,
                    $"Queue: {building.trainingQueue.Count} item(s)", 11, SporefrontColors.InkLight);
                var qlLE = queueLabel.gameObject.AddComponent<LayoutElement>();
                qlLE.preferredHeight = 20;
            }

            if (building.villagerTrainingQueue != null && building.villagerTrainingQueue.Count > 0)
            {
                var vtLabel = UIHelper.CreateLabel(contentRT,
                    $"Villager queue: {building.villagerTrainingQueue.Count} item(s)", 11,
                    SporefrontColors.InkLight);
                var vtLE = vtLabel.gameObject.AddComponent<LayoutElement>();
                vtLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Garrison Section
        // ================================================================

        private void BuildGarrisonSection(BuildingData building)
        {
            int total = building.GetTotalGarrisonCount();
            int capacity = building.GetGarrisonCapacity();
            if (capacity <= 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT,
                $"Garrison ({total}/{capacity})",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            // Military garrison
            if (building.garrison != null)
            {
                foreach (var kvp in building.garrison)
                {
                    if (kvp.Value <= 0) continue;
                    var label = UIHelper.CreateLabel(contentRT,
                        $"  {kvp.Key.DisplayName()}: {kvp.Value}", 12);
                    var le = label.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 20;
                }
            }

            // Villager garrison
            if (building.villagerGarrison > 0)
            {
                var vlLabel = UIHelper.CreateLabel(contentRT,
                    $"  Villagers: {building.villagerGarrison}", 12);
                var vlLE = vlLabel.gameObject.AddComponent<LayoutElement>();
                vlLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Deploy Section
        // ================================================================

        private void BuildDeploySection(BuildingData building, GameState gameState)
        {
            int totalGarrison = building.GetTotalGarrisonCount();
            if (totalGarrison <= 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Deploy",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(contentRT, 32f, 8f);

            // Deploy Army button
            if (building.garrison != null && building.GetTotalGarrisonedUnits() > 0)
            {
                var deployArmyBtn = UIHelper.CreateButton(row.transform, "Deploy Army",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                    {
                        // Deploy all garrisoned military units
                        var composition = new Dictionary<MilitaryUnitType, int>(building.garrison);
                        var cmd = new DeployArmyCommand(localPlayerID, building.id, composition);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var armyBtnLE = deployArmyBtn.gameObject.AddComponent<LayoutElement>();
                armyBtnLE.preferredWidth = 110;
                armyBtnLE.preferredHeight = 32;
            }

            // Deploy Villagers button
            if (building.villagerGarrison > 0)
            {
                var deployVilBtn = UIHelper.CreateButton(row.transform, "Deploy Villagers",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                    {
                        var cmd = new DeployVillagersCommand(localPlayerID, building.id,
                            building.villagerGarrison);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var vilBtnLE = deployVilBtn.gameObject.AddComponent<LayoutElement>();
                vilBtnLE.preferredWidth = 130;
                vilBtnLE.preferredHeight = 32;
            }
        }

        // ================================================================
        // Upgrade Section
        // ================================================================

        private void BuildUpgradeSection(BuildingData building, PlayerState player)
        {
            if (!building.CanUpgrade) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Upgrade",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var cost = building.GetUpgradeCost();
            bool canAfford = player != null && player.CanAfford(cost);
            int nextLevel = building.level + 1;

            var infoLabel = UIHelper.CreateLabel(contentRT,
                $"Lv.{building.level} -> Lv.{nextLevel}  Cost: {FormatCost(cost)}", 12,
                canAfford ? UIHelper.BodyTextColor : SporefrontColors.SporeRed);
            infoLabel.supportRichText = true;
            var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 22;

            var upgradeBtn = UIHelper.CreateButton(contentRT, "Upgrade",
                canAfford ? SporefrontColors.SporeAmber : SporefrontColors.InkFaded,
                canAfford ? UIHelper.ButtonText : SporefrontColors.InkLight, 12, () =>
                {
                    var cmd = new UpgradeCommand(localPlayerID, building.id);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            upgradeBtn.interactable = canAfford;
            var btnLE = upgradeBtn.gameObject.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 32;

            // Upgrade progress
            if (building.state == BuildingState.Upgrading)
            {
                var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 14f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)building.upgradeProgress), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.preferredHeight = 14;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private string FormatCost(Dictionary<ResourceType, int> cost)
        {
            var parts = new List<string>();
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0)
                    parts.Add($"{UIHelper.ResourceIcon(kvp.Key)}{kvp.Value}");
            }
            return string.Join(" ", parts);
        }
    }
}
