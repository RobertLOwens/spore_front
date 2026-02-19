// ============================================================================
// FILE: Visual/BuildingTrainingSection.cs
// PURPOSE: Training, garrison, and deploy UI sections — extracted from
//          BuildingDetailPanel
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
    public static class BuildingTrainingSection
    {
        public struct State
        {
            public Text queueLabel;
            public Text villagerQueueLabel;
            public List<Image> trainingProgressFills;
            public List<Text> trainingProgressLabels;
            public List<Text> trainingTimeLabels;
            public List<Image> villagerProgressFills;
            public List<Text> villagerProgressLabels;
            public List<Text> villagerTimeLabels;
        }

        public static State BuildTraining(RectTransform contentRT, BuildingData building,
            GameState gameState, PlayerState player, Guid localPlayerID)
        {
            var state = new State();
            state.trainingProgressFills = new List<Image>();
            state.trainingProgressLabels = new List<Text>();
            state.trainingTimeLabels = new List<Text>();
            state.villagerProgressFills = new List<Image>();
            state.villagerProgressLabels = new List<Text>();
            state.villagerTimeLabels = new List<Text>();

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Training",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            // Population stats for max trainable calculations
            int currentPop = 0, popCapacity = 0;
            gameState.GetPopulationStats(localPlayerID, out currentPop, out popCapacity);

            // Villager training (City Center / Neighborhood)
            if (building.CanTrainVillagers())
            {
                BuildVillagerTrainingControl(contentRT, building, player, localPlayerID,
                    currentPop, popCapacity);
            }

            // Military unit training
            var unitTypes = (MilitaryUnitType[])Enum.GetValues(typeof(MilitaryUnitType));
            foreach (var ut in unitTypes)
            {
                if (!building.CanTrain(ut)) continue;

                BuildMilitaryTrainingControl(contentRT, building, player, localPlayerID,
                    ut, currentPop, popCapacity);
            }

            // Training queue progress bars
            if (building.trainingQueue != null && building.trainingQueue.Count > 0)
            {
                UIHelper.CreateDivider(contentRT, null, 1);
                state.queueLabel = UIHelper.CreateLabel(contentRT,
                    $"Queue: {building.trainingQueue.Count} item(s)", 11, SporefrontColors.InkLight);
                var qlLE = state.queueLabel.gameObject.AddComponent<LayoutElement>();
                qlLE.preferredHeight = 20;

                foreach (var entry in building.trainingQueue)
                {
                    BuildMilitaryQueueRow(contentRT, building, entry, gameState, state);
                }
            }

            if (building.villagerTrainingQueue != null && building.villagerTrainingQueue.Count > 0)
            {
                if (building.trainingQueue == null || building.trainingQueue.Count == 0)
                    UIHelper.CreateDivider(contentRT, null, 1);

                state.villagerQueueLabel = UIHelper.CreateLabel(contentRT,
                    $"Villager queue: {building.villagerTrainingQueue.Count} item(s)", 11,
                    SporefrontColors.InkLight);
                var vtLE = state.villagerQueueLabel.gameObject.AddComponent<LayoutElement>();
                vtLE.preferredHeight = 20;

                foreach (var entry in building.villagerTrainingQueue)
                {
                    BuildVillagerQueueRow(contentRT, building, entry, gameState, state);
                }
            }

            return state;
        }

        // ================================================================
        // Military Training Control (with slider)
        // ================================================================

        private static void BuildMilitaryTrainingControl(RectTransform contentRT,
            BuildingData building, PlayerState player, Guid localPlayerID,
            MilitaryUnitType ut, int currentPop, int popCapacity)
        {
            var cost = ut.TrainingCost();
            int popSpace = ut.PopSpace();

            // Calculate max trainable
            int popRemaining = popCapacity > currentPop ? (popCapacity - currentPop) / popSpace : 0;
            int resourceMax = int.MaxValue;
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0 && player != null)
                    resourceMax = Math.Min(resourceMax, player.GetResource(kvp.Key) / kvp.Value);
            }
            if (resourceMax == int.MaxValue) resourceMax = 0;
            int maxTrainable = Math.Max(0, Math.Min(popRemaining, resourceMax));

            // Unit name row
            var nameRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);
            var nameLabel = UIHelper.CreateLabel(nameRow.transform, ut.DisplayName(), 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            var costPerUnit = UIHelper.FormatCost(cost);
            var baseCostLabel = UIHelper.CreateLabel(nameRow.transform, costPerUnit, 10,
                maxTrainable > 0 ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
            baseCostLabel.supportRichText = true;
            var baseCostLE = baseCostLabel.gameObject.AddComponent<LayoutElement>();
            baseCostLE.preferredWidth = 100;

            if (maxTrainable <= 0)
            {
                // No slider — just a disabled Train button
                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);
                var trainBtn = UIHelper.CreateButton(row.transform, "Train",
                    SporefrontColors.InkFaded, UIHelper.ButtonText, 11, null);
                trainBtn.interactable = false;
                var btnLE = trainBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.flexibleWidth = 1;
                return;
            }

            // Slider row: [Slider] [Count] [Train]
            var sliderRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

            int selectedQty = 1;
            Text countLabel = null;
            Text totalCostLabel = null;
            Button trainButton = null;

            var slider = UIHelper.CreateSlider(sliderRow.transform, 1, maxTrainable, true, null);
            slider.value = 1;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;

            countLabel = UIHelper.CreateLabel(sliderRow.transform, "1", 12,
                UIHelper.BodyTextColor, TextAnchor.MiddleCenter);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 28;

            var capturedUT = ut;
            var capturedBuildingID = building.id;
            var capturedCost = cost;

            trainButton = UIHelper.CreateButton(sliderRow.transform, "Train",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11, () =>
                {
                    var cmd = new TrainMilitaryCommand(localPlayerID, capturedBuildingID,
                        capturedUT, selectedQty);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            var trainBtnLE = trainButton.gameObject.AddComponent<LayoutElement>();
            trainBtnLE.preferredWidth = 50;

            // Total cost row
            var totalRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 4f);
            totalCostLabel = UIHelper.CreateLabel(totalRow.transform,
                $"Total: {costPerUnit}", UIConstants.FontCaption, SporefrontColors.InkLight);
            var totalLE = totalCostLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.flexibleWidth = 1;

            // Wire slider onChange (captured references)
            var capturedCountLabel = countLabel;
            var capturedTotalCostLabel = totalCostLabel;
            slider.onValueChanged.AddListener((val) =>
            {
                selectedQty = (int)val;
                capturedCountLabel.text = selectedQty.ToString();

                // Update total cost display
                var totalCost = new Dictionary<ResourceType, int>();
                foreach (var kvp in capturedCost)
                    totalCost[kvp.Key] = kvp.Value * selectedQty;
                capturedTotalCostLabel.text = $"Total: {UIHelper.FormatCost(totalCost)}";
            });
        }

        // ================================================================
        // Villager Training Control (with slider)
        // ================================================================

        private static void BuildVillagerTrainingControl(RectTransform contentRT,
            BuildingData building, PlayerState player, Guid localPlayerID,
            int currentPop, int popCapacity)
        {
            int villagerCostFood = 50;

            // Calculate max trainable
            int popRemaining = popCapacity > currentPop ? popCapacity - currentPop : 0;
            int resourceMax = player != null ? player.GetResource(ResourceType.Food) / villagerCostFood : 0;
            int maxTrainable = Math.Max(0, Math.Min(popRemaining, resourceMax));

            // Unit name row
            var nameRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);
            var nameLabel = UIHelper.CreateLabel(nameRow.transform, "Villager", 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            var baseCostLabel = UIHelper.CreateLabel(nameRow.transform, $"F{villagerCostFood}", 10,
                maxTrainable > 0 ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
            var baseCostLE = baseCostLabel.gameObject.AddComponent<LayoutElement>();
            baseCostLE.preferredWidth = 100;

            if (maxTrainable <= 0)
            {
                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);
                var trainBtn = UIHelper.CreateButton(row.transform, "Train",
                    SporefrontColors.InkFaded, UIHelper.ButtonText, 11, null);
                trainBtn.interactable = false;
                var btnLE = trainBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.flexibleWidth = 1;
                return;
            }

            // Slider row: [Slider] [Count] [Train]
            var sliderRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

            int selectedQty = 1;
            Text countLabel = null;
            Text totalCostLabel = null;

            var slider = UIHelper.CreateSlider(sliderRow.transform, 1, maxTrainable, true, null);
            slider.value = 1;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;

            countLabel = UIHelper.CreateLabel(sliderRow.transform, "1", 12,
                UIHelper.BodyTextColor, TextAnchor.MiddleCenter);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 28;

            var capturedBuildingID = building.id;
            var trainButton = UIHelper.CreateButton(sliderRow.transform, "Train",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11, () =>
                {
                    var cmd = new TrainVillagerCommand(localPlayerID, capturedBuildingID, selectedQty);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            var trainBtnLE = trainButton.gameObject.AddComponent<LayoutElement>();
            trainBtnLE.preferredWidth = 50;

            // Total cost row
            var totalRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 4f);
            totalCostLabel = UIHelper.CreateLabel(totalRow.transform,
                $"Total: F{villagerCostFood}", UIConstants.FontCaption, SporefrontColors.InkLight);
            var totalLE = totalCostLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.flexibleWidth = 1;

            // Wire slider onChange
            var capturedCountLabel = countLabel;
            var capturedTotalCostLabel = totalCostLabel;
            var capturedCostPerUnit = villagerCostFood;
            slider.onValueChanged.AddListener((val) =>
            {
                selectedQty = (int)val;
                capturedCountLabel.text = selectedQty.ToString();
                capturedTotalCostLabel.text = $"Total: F{capturedCostPerUnit * selectedQty}";
            });
        }

        // ================================================================
        // Military Queue Progress Row
        // ================================================================

        private static void BuildMilitaryQueueRow(RectTransform contentRT,
            BuildingData building, TrainingQueueEntry entry, GameState gameState,
            State state)
        {
            double progress = entry.GetProgress(gameState.currentTime,
                building.GetTrainingSpeedMultiplier());
            float progressFloat = Mathf.Clamp01((float)progress);

            var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            var unitLabel = UIHelper.CreateLabel(row.transform,
                $"{entry.unitType.DisplayName()} x{entry.quantity}", 11, SporefrontColors.InkDark);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.preferredWidth = 100;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 12f,
                SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(progressFloat, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            state.trainingProgressFills.Add(fill);

            var pctLabel = UIHelper.CreateLabel(row.transform,
                $"{(int)(progressFloat * 100)}%", UIConstants.FontCaption, SporefrontColors.InkLight);
            var pctLE = pctLabel.gameObject.AddComponent<LayoutElement>();
            pctLE.preferredWidth = 30;

            state.trainingProgressLabels.Add(pctLabel);

            // Time remaining
            double baseTime = entry.unitType.TrainingTime() * entry.quantity;
            double speedMultiplier = building.GetTrainingSpeedMultiplier();
            double totalTime = baseTime / speedMultiplier;
            double elapsed = gameState.currentTime - entry.startTime;
            double remaining = Math.Max(0, totalTime - elapsed);

            var timeLabel = UIHelper.CreateLabel(row.transform,
                $"~{UIHelper.FormatTime(remaining)}",
                UIConstants.FontCaption, SporefrontColors.InkLight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 50;

            state.trainingTimeLabels.Add(timeLabel);
        }

        // ================================================================
        // Villager Queue Progress Row
        // ================================================================

        private static void BuildVillagerQueueRow(RectTransform contentRT,
            BuildingData building, VillagerTrainingEntry entry, GameState gameState,
            State state)
        {
            double progress = entry.GetProgress(gameState.currentTime);
            float progressFloat = Mathf.Clamp01((float)progress);

            var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            var unitLabel = UIHelper.CreateLabel(row.transform,
                $"Villager x{entry.quantity}", 11, SporefrontColors.InkDark);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.preferredWidth = 100;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 12f,
                SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(progressFloat, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            state.villagerProgressFills.Add(fill);

            var pctLabel = UIHelper.CreateLabel(row.transform,
                $"{(int)(progressFloat * 100)}%", UIConstants.FontCaption, SporefrontColors.InkLight);
            var pctLE = pctLabel.gameObject.AddComponent<LayoutElement>();
            pctLE.preferredWidth = 30;

            state.villagerProgressLabels.Add(pctLabel);

            // Time remaining
            double totalTime = VillagerTrainingEntry.TrainingTimePerVillager * entry.quantity;
            double elapsed = gameState.currentTime - entry.startTime;
            double remaining = Math.Max(0, totalTime - elapsed);

            var timeLabel = UIHelper.CreateLabel(row.transform,
                $"~{UIHelper.FormatTime(remaining)}",
                UIConstants.FontCaption, SporefrontColors.InkLight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 50;

            state.villagerTimeLabels.Add(timeLabel);
        }

        // ================================================================
        // Garrison Section
        // ================================================================

        public static void BuildGarrison(RectTransform contentRT, BuildingData building)
        {
            int total = building.GetTotalGarrisonCount();
            int capacity = building.GetGarrisonCapacity();
            if (capacity <= 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT,
                $"Garrison ({total}/{capacity})",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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

        public static void BuildDeploy(RectTransform contentRT, BuildingData building,
            GameState gameState, Guid localPlayerID)
        {
            int totalGarrison = building.GetTotalGarrisonCount();
            if (totalGarrison <= 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Deploy",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(contentRT, 32f, 8f);

            // Deploy Army button
            if (building.garrison != null && building.GetTotalGarrisonedUnits() > 0)
            {
                var capturedBuildingID = building.id;
                var capturedGarrison = new Dictionary<MilitaryUnitType, int>(building.garrison);
                var deployArmyBtn = UIHelper.CreateButton(row.transform, "Deploy Army",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                    {
                        var cmd = new DeployArmyCommand(localPlayerID, capturedBuildingID, capturedGarrison);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var armyBtnLE = deployArmyBtn.gameObject.AddComponent<LayoutElement>();
                armyBtnLE.preferredWidth = 110;
                armyBtnLE.preferredHeight = 32;
            }

            // Deploy Villagers button
            if (building.villagerGarrison > 0)
            {
                var capturedBuildingID = building.id;
                var capturedVillagerCount = building.villagerGarrison;
                var deployVilBtn = UIHelper.CreateButton(row.transform, "Deploy Villagers",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                    {
                        var cmd = new DeployVillagersCommand(localPlayerID, capturedBuildingID,
                            capturedVillagerCount);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var vilBtnLE = deployVilBtn.gameObject.AddComponent<LayoutElement>();
                vilBtnLE.preferredWidth = 130;
                vilBtnLE.preferredHeight = 32;
            }
        }
    }
}
