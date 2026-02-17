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

                // Market section
                if (building.buildingType == BuildingType.Market)
                {
                    BuildMarketSection(building, gameState, player);
                    UIHelper.CreateDivider(contentRT);
                }

                // Unit upgrades section (military production buildings)
                var availableUpgrades = UnitUpgradeTypeExtensions.UpgradesForBuilding(building.buildingType);
                if (availableUpgrades.Count > 0)
                {
                    BuildUnitUpgradesSection(building, gameState, player, availableUpgrades);
                    UIHelper.CreateDivider(contentRT);
                }

                // Garrison section
                BuildGarrisonSection(building);
                UIHelper.CreateDivider(contentRT);

                // Deploy section
                BuildDeploySection(building, gameState);
                UIHelper.CreateDivider(contentRT);

                // Home base section
                int? homeBaseCapacity = building.GetArmyHomeBaseCapacity();
                if (homeBaseCapacity.HasValue || building.buildingType == BuildingType.CityCenter)
                {
                    BuildHomeBaseSection(building, gameState);
                    UIHelper.CreateDivider(contentRT);
                }

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
        // Market Section
        // ================================================================

        private Dictionary<ResourceType, int> tradeInputAmounts = new Dictionary<ResourceType, int>();
        private ResourceType tradeOutputType = ResourceType.Food;
        private Text tradePreviewLabel;

        private void BuildMarketSection(BuildingData building, GameState gameState, PlayerState player)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Trade Resources",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var rateLabel = UIHelper.CreateLabel(contentRT, "Exchange Rate: 80%", 11,
                SporefrontColors.InkLight);
            var rateLE = rateLabel.gameObject.AddComponent<LayoutElement>();
            rateLE.preferredHeight = 18;

            // Input sliders for each resource
            tradeInputAmounts.Clear();
            var resourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
            foreach (var rt in resourceTypes)
            {
                int available = player != null ? player.GetResource(rt) : 0;
                tradeInputAmounts[rt] = 0;

                var row = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform,
                    $"{UIHelper.ResourceIcon(rt)} {rt}", 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 60;

                var capturedRT = rt;
                var amountLabel = UIHelper.CreateLabel(row.transform, "0", 12,
                    SporefrontColors.InkMid, TextAnchor.MiddleCenter);
                var amountLE = amountLabel.gameObject.AddComponent<LayoutElement>();
                amountLE.preferredWidth = 30;

                if (available > 0)
                {
                    var slider = UIHelper.CreateSlider(row.transform, 0, available, true, (val) =>
                    {
                        tradeInputAmounts[capturedRT] = (int)val;
                        amountLabel.text = ((int)val).ToString();
                        UpdateTradePreview();
                    });
                    var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
                    sliderLE.flexibleWidth = 1;
                    sliderLE.preferredHeight = 20;
                }
                else
                {
                    var emptyLabel = UIHelper.CreateLabel(row.transform, "(none)", 11,
                        SporefrontColors.InkFaded);
                    var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                    emptyLE.flexibleWidth = 1;
                }

                var maxLabel = UIHelper.CreateLabel(row.transform, $"/{available}", 10,
                    SporefrontColors.InkFaded);
                var maxLE = maxLabel.gameObject.AddComponent<LayoutElement>();
                maxLE.preferredWidth = 40;
            }

            // Output type selection
            var outputHeader = UIHelper.CreateLabel(contentRT, "Receive:", 12,
                UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var outputHeaderLE = outputHeader.gameObject.AddComponent<LayoutElement>();
            outputHeaderLE.preferredHeight = 22;

            var outputRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);
            foreach (var rt in resourceTypes)
            {
                var capturedRT = rt;
                bool isSelected = (rt == tradeOutputType);
                var btn = UIHelper.CreateButton(outputRow.transform,
                    UIHelper.ResourceIcon(rt),
                    isSelected ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText, 12, () =>
                    {
                        tradeOutputType = capturedRT;
                        if (currentBuildingID.HasValue)
                            Rebuild(GameEngine.Instance.gameState);
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 40;
                btnLE.preferredHeight = 28;
            }

            // Preview
            tradePreviewLabel = UIHelper.CreateLabel(contentRT, "Select resources to trade", 12,
                SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var previewLE = tradePreviewLabel.gameObject.AddComponent<LayoutElement>();
            previewLE.preferredHeight = 22;
            UpdateTradePreview();

            // Execute button
            var tradeBtn = UIHelper.CreateButton(contentRT, "Execute Trade",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                {
                    if (!currentBuildingID.HasValue) return;
                    var inputs = new Dictionary<ResourceType, int>();
                    foreach (var kvp in tradeInputAmounts)
                    {
                        if (kvp.Value > 0 && kvp.Key != tradeOutputType)
                            inputs[kvp.Key] = kvp.Value;
                    }
                    if (inputs.Count == 0) return;
                    var cmd = new MarketTradeCommand(localPlayerID, currentBuildingID.Value,
                        inputs, tradeOutputType);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            var tradeBtnLE = tradeBtn.gameObject.AddComponent<LayoutElement>();
            tradeBtnLE.preferredHeight = 32;
        }

        private void UpdateTradePreview()
        {
            if (tradePreviewLabel == null) return;
            int totalInput = 0;
            foreach (var kvp in tradeInputAmounts)
            {
                if (kvp.Key != tradeOutputType)
                    totalInput += kvp.Value;
            }
            if (totalInput <= 0)
            {
                tradePreviewLabel.text = "Select resources to trade";
                return;
            }
            int output = MarketTradeCommand.CalculateOutput(totalInput);
            tradePreviewLabel.text = $"{totalInput} input -> {output} {UIHelper.ResourceIcon(tradeOutputType)}";
        }

        // ================================================================
        // Unit Upgrades Section
        // ================================================================

        private void BuildUnitUpgradesSection(BuildingData building, GameState gameState,
            PlayerState player, List<UnitUpgradeType> upgrades)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Unit Upgrades",
                UIHelper.DefaultHeaderFontSize - 2, SporefrontColors.SporeAmber,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            // Group by unit type
            MilitaryUnitType? currentUnit = null;
            foreach (var upgrade in upgrades)
            {
                var unitType = upgrade.GetUnitType();
                if (!currentUnit.HasValue || currentUnit.Value != unitType)
                {
                    currentUnit = unitType;
                    int tier = player != null ? player.GetUnitUpgradeTier(unitType) : 0;
                    var unitHeader = UIHelper.CreateLabel(contentRT,
                        $"{unitType.DisplayName()} (Tier {tier})", 12,
                        SporefrontColors.InkDark, TextAnchor.MiddleLeft, true);
                    var unitHeaderLE = unitHeader.gameObject.AddComponent<LayoutElement>();
                    unitHeaderLE.preferredHeight = 20;
                }

                bool completed = player != null && player.HasCompletedUnitUpgrade(upgrade.ToString());
                bool isActive = player != null && player.activeUnitUpgrade == upgrade.ToString();
                bool prereqMet = true;
                var prereq = upgrade.Prerequisite();
                if (prereq.HasValue && player != null)
                    prereqMet = player.HasCompletedUnitUpgrade(prereq.Value.ToString());
                bool levelMet = building.level >= upgrade.RequiredBuildingLevel();
                bool canStart = !completed && !isActive && prereqMet && levelMet
                    && player != null && !player.IsUnitUpgradeActive()
                    && player.CanAfford(upgrade.Cost());

                var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                // Status indicator
                string statusText;
                Color statusColor;
                if (completed)
                {
                    statusText = "Done";
                    statusColor = SporefrontColors.SporeGreen;
                }
                else if (isActive)
                {
                    statusText = "Active";
                    statusColor = SporefrontColors.SporeTeal;
                }
                else if (!prereqMet || !levelMet)
                {
                    statusText = "Locked";
                    statusColor = SporefrontColors.InkFaded;
                }
                else
                {
                    statusText = $"Tier {upgrade.Tier()}";
                    statusColor = SporefrontColors.InkMid;
                }

                var statusLabel = UIHelper.CreateLabel(row.transform, statusText, 11, statusColor);
                var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
                statusLE.preferredWidth = 45;

                // Cost
                if (!completed)
                {
                    var cost = upgrade.Cost();
                    var costLabel = UIHelper.CreateLabel(row.transform, FormatCost(cost), 10,
                        (canStart || isActive) ? SporefrontColors.InkLight : SporefrontColors.InkFaded);
                    costLabel.supportRichText = true;
                    var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
                    costLE.flexibleWidth = 1;
                }
                else
                {
                    var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                    spacer.transform.SetParent(row.transform, false);
                    spacer.GetComponent<LayoutElement>().flexibleWidth = 1;
                }

                // Action button
                if (!completed && !isActive)
                {
                    var capturedUpgrade = upgrade;
                    var btn = UIHelper.CreateButton(row.transform, "Upgrade",
                        canStart ? SporefrontColors.SporeAmber : SporefrontColors.InkFaded,
                        canStart ? UIHelper.ButtonText : SporefrontColors.InkLight, 11, () =>
                        {
                            if (!currentBuildingID.HasValue) return;
                            var cmd = new UpgradeUnitCommand(localPlayerID,
                                capturedUpgrade.ToString(), currentBuildingID.Value);
                            GameEngine.Instance.ExecuteCommand(cmd);
                        });
                    btn.interactable = canStart;
                    var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 60;
                }

                // Active progress bar
                if (isActive && player.activeUnitUpgradeStartTime.HasValue)
                {
                    double elapsed = gameState.currentTime - player.activeUnitUpgradeStartTime.Value;
                    double total = upgrade.UpgradeTime();
                    double pct = Math.Min(1.0, elapsed / total);
                    double remaining = Math.Max(0, total - elapsed);

                    var progressRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 4f);
                    var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                        SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)pct), 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.flexibleWidth = 1;
                    barLE.preferredHeight = 12;

                    var timeLabel = UIHelper.CreateLabel(progressRow.transform,
                        $"{(int)remaining}s", 10, SporefrontColors.InkLight);
                    var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
                    timeLE.preferredWidth = 30;
                }
            }
        }

        // ================================================================
        // Home Base Section
        // ================================================================

        private void BuildHomeBaseSection(BuildingData building, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Home Base",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            int? capacity = building.GetArmyHomeBaseCapacity();
            int count = gameState.GetArmyCountForHomeBase(building.id);

            string capacityText = capacity.HasValue
                ? $"Army Capacity: {count}/{capacity.Value}"
                : $"Army Capacity: {count} (Unlimited)";

            var capLabel = UIHelper.CreateLabel(contentRT, capacityText, 12, SporefrontColors.InkMid);
            var capLE = capLabel.gameObject.AddComponent<LayoutElement>();
            capLE.preferredHeight = 20;

            var armies = gameState.GetArmiesForHomeBase(building.id);
            if (armies.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No armies based here", 11,
                    SporefrontColors.InkFaded);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 18;
            }
            else
            {
                foreach (var army in armies)
                {
                    var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        army.name ?? "Army", 12, SporefrontColors.InkDark);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;

                    int totalUnits = army.GetTotalUnits();
                    var unitsLabel = UIHelper.CreateLabel(row.transform,
                        $"{totalUnits} units", 11, SporefrontColors.InkLight);
                    var unitsLE = unitsLabel.gameObject.AddComponent<LayoutElement>();
                    unitsLE.preferredWidth = 60;

                    var coordLabel = UIHelper.CreateLabel(row.transform,
                        $"({army.coordinate.q},{army.coordinate.r})", 10,
                        SporefrontColors.InkFaded);
                    var coordLE = coordLabel.gameObject.AddComponent<LayoutElement>();
                    coordLE.preferredWidth = 50;
                }
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
