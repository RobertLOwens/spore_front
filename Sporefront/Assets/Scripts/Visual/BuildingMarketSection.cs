// ============================================================================
// FILE: Visual/BuildingMarketSection.cs
// PURPOSE: Market trade UI section — extracted from BuildingDetailPanel
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
    public static class BuildingMarketSection
    {
        public struct State
        {
            public Dictionary<ResourceType, int> tradeInputAmounts;
            public ResourceType tradeOutputType;
            public Text tradePreviewLabel;
        }

        public static State Build(RectTransform contentRT, BuildingData building,
            GameState gameState, PlayerState player, Guid localPlayerID,
            Guid? currentBuildingID, Action rebuildPanel)
        {
            var state = new State
            {
                tradeInputAmounts = new Dictionary<ResourceType, int>(),
                tradeOutputType = ResourceType.Food
            };

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Trade Resources",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var rateLabel = UIHelper.CreateLabel(contentRT, "Exchange Rate: 80%", 11,
                SporefrontColors.InkLight);
            var rateLE = rateLabel.gameObject.AddComponent<LayoutElement>();
            rateLE.preferredHeight = 18;

            // Input sliders for each resource
            var resourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
            foreach (var rt in resourceTypes)
            {
                int available = player != null ? player.GetResource(rt) : 0;
                state.tradeInputAmounts[rt] = 0;

                var row = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform,
                    $"{UIHelper.ResourceIcon(rt)} {rt}", 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 60;

                var capturedRT = rt;
                var capturedState = state;
                var amountLabel = UIHelper.CreateLabel(row.transform, "0", 12,
                    SporefrontColors.InkMid, TextAnchor.MiddleCenter);
                var amountLE = amountLabel.gameObject.AddComponent<LayoutElement>();
                amountLE.preferredWidth = 30;

                if (available > 0)
                {
                    var slider = UIHelper.CreateSlider(row.transform, 0, available, true, (val) =>
                    {
                        capturedState.tradeInputAmounts[capturedRT] = (int)val;
                        amountLabel.text = ((int)val).ToString();
                        UpdatePreview(capturedState);
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
                var capturedOutputRT = rt;
                bool isSelected = (rt == state.tradeOutputType);
                var btn = UIHelper.CreateButton(outputRow.transform,
                    UIHelper.ResourceIcon(rt),
                    isSelected ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText, 12, () =>
                    {
                        state.tradeOutputType = capturedOutputRT;
                        rebuildPanel?.Invoke();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 40;
                btnLE.preferredHeight = 28;
            }

            // Preview
            state.tradePreviewLabel = UIHelper.CreateLabel(contentRT, "Select resources to trade", 12,
                SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var previewLE = state.tradePreviewLabel.gameObject.AddComponent<LayoutElement>();
            previewLE.preferredHeight = 22;
            UpdatePreview(state);

            // Execute button
            var capturedBuildingID = currentBuildingID;
            var capturedLocalPlayerID = localPlayerID;
            var capturedState2 = state;
            var tradeBtn = UIHelper.CreateButton(contentRT, "Execute Trade",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                {
                    if (!capturedBuildingID.HasValue) return;
                    var inputs = new Dictionary<ResourceType, int>();
                    foreach (var kvp in capturedState2.tradeInputAmounts)
                    {
                        if (kvp.Value > 0 && kvp.Key != capturedState2.tradeOutputType)
                            inputs[kvp.Key] = kvp.Value;
                    }
                    if (inputs.Count == 0) return;
                    var cmd = new MarketTradeCommand(capturedLocalPlayerID, capturedBuildingID.Value,
                        inputs, capturedState2.tradeOutputType);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            var tradeBtnLE = tradeBtn.gameObject.AddComponent<LayoutElement>();
            tradeBtnLE.preferredHeight = 32;

            return state;
        }

        public static void UpdatePreview(State state)
        {
            if (state.tradePreviewLabel == null) return;
            int totalInput = 0;
            foreach (var kvp in state.tradeInputAmounts)
            {
                if (kvp.Key != state.tradeOutputType)
                    totalInput += kvp.Value;
            }
            if (totalInput <= 0)
            {
                state.tradePreviewLabel.text = "Select resources to trade";
                return;
            }
            int output = MarketTradeCommand.CalculateOutput(totalInput);
            state.tradePreviewLabel.text = $"{totalInput} input -> {output} {UIHelper.ResourceIcon(state.tradeOutputType)}";
        }
    }
}
