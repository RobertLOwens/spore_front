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
        }

        public static State BuildTraining(RectTransform contentRT, BuildingData building,
            GameState gameState, PlayerState player, Guid localPlayerID)
        {
            var state = new State();

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Training",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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

                var capturedBuildingID = building.id;
                var trainBtn = UIHelper.CreateButton(row.transform, "Train", null, null, 11, () =>
                {
                    var cmd = new TrainVillagerCommand(localPlayerID, capturedBuildingID, 1);
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

                var costLabel = UIHelper.CreateLabel(row.transform, UIHelper.FormatCost(cost), 10,
                    canAfford ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
                costLabel.supportRichText = true;
                var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
                costLE.preferredWidth = 80;

                var capturedUT = ut;
                var capturedBuildingID = building.id;
                var trainBtn = UIHelper.CreateButton(row.transform, "Train",
                    canAfford ? SporefrontColors.ParchmentDark : SporefrontColors.InkFaded,
                    UIHelper.ButtonText, 11, () =>
                    {
                        var cmd = new TrainMilitaryCommand(localPlayerID, capturedBuildingID, capturedUT, 1);
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
                state.queueLabel = UIHelper.CreateLabel(contentRT,
                    $"Queue: {building.trainingQueue.Count} item(s)", 11, SporefrontColors.InkLight);
                var qlLE = state.queueLabel.gameObject.AddComponent<LayoutElement>();
                qlLE.preferredHeight = 20;
            }

            if (building.villagerTrainingQueue != null && building.villagerTrainingQueue.Count > 0)
            {
                state.villagerQueueLabel = UIHelper.CreateLabel(contentRT,
                    $"Villager queue: {building.villagerTrainingQueue.Count} item(s)", 11,
                    SporefrontColors.InkLight);
                var vtLE = state.villagerQueueLabel.gameObject.AddComponent<LayoutElement>();
                vtLE.preferredHeight = 20;
            }

            return state;
        }

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
