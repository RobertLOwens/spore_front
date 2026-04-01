// ============================================================================
// FILE: Visual/ResourceOverviewPanel.cs
// PURPOSE: Modal overview with per-resource detail cards showing storage,
//          collection rates, gathering groups, and active bonuses.
//          Parchment/ink ledger style with watercolor progress bars.
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
    public class ResourceOverviewPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

        // Throttled rebuild
        private bool isDirty;
        private float lastRebuildTime;
        private const float RebuildInterval = 0.5f;
        private GameState cachedGameState;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "ResourceOverviewBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel -- parchment background
            panel = UIHelper.CreatePanel(backdrop.transform, "ResourceOverviewPanel",
                UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalMediumW, UIConstants.ModalLargeH);
            PopupTendrilDecorator.Attach(rt);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Resource Overview",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(8, -32);
            headerRT.offsetMax = new Vector2(-8, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "ResourceScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 40);
            scrollRT.offsetMax = new Vector2(0, -36);

            // Ink-styled close annotation
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 4);
            closeBtnRT.offsetMax = new Vector2(-8, 36);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            cachedGameState = gameState;
            lastRebuildTime = Time.unscaledTime;
            isDirty = false;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public override void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!IsVisible) return;
            cachedGameState = gameState;
            isDirty = true;
        }

        private void Update()
        {
            if (!isDirty || !IsVisible || cachedGameState == null) return;
            if (Time.unscaledTime - lastRebuildTime < RebuildInterval) return;
            isDirty = false;
            lastRebuildTime = Time.unscaledTime;
            Rebuild(cachedGameState);
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            // Clear content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            // Food consumption info
            var foodInfo = gameState.GetFoodConsumptionRate(localPlayerID);

            // Build card for each resource type
            ResourceType[] resourceOrder = { ResourceType.Wood, ResourceType.Food,
                                              ResourceType.Stone, ResourceType.Ore };

            foreach (var resType in resourceOrder)
            {
                BuildResourceCard(resType, gameState, player, foodInfo);
                UIHelper.CreateDivider(contentRT, UIHelper.InkDividerColor);
            }

            // Active bonuses section
            BuildBonusSection(player);
        }

        // ================================================================
        // Resource Card
        // ================================================================

        private void BuildResourceCard(ResourceType resType, GameState gameState,
            PlayerState player, GameState.FoodConsumptionInfo foodInfo)
        {
            var vlg = UIHelper.CreateLedgerCard(contentRT, $"{resType}Card");
            var card = vlg.transform;

            // Row 1: Key badge + Resource name + amount
            // Row 1: Key badge + Resource name + amount
            var topRow = UIHelper.CreateHorizontalRow(card, 36f, 6f);
            var topRowLE = topRow.gameObject.AddComponent<LayoutElement>();
            topRowLE.preferredHeight = 36;

            UIHelper.CreateKeyBadge(topRow.transform, UIHelper.ResourceIcon(resType));

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                resType.DisplayName(), UIConstants.FontSubheader,
                UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 36;

            int amount = player.GetResource(resType);
            int capacity = gameState.GetStorageCapacity(localPlayerID, resType);
            var amountLabel = UIHelper.CreateLabel(topRow.transform,
                $"{amount} / {capacity}", UIConstants.FontBody,
                amount >= capacity ? SporefrontColors.SporeRed : UIHelper.InkBodyText,
                TextAnchor.MiddleRight);
            var amountLE = amountLabel.gameObject.AddComponent<LayoutElement>();
            amountLE.preferredWidth = 120;
            amountLE.preferredHeight = 36;

            // Ink-outlined storage bar with resource-appropriate watercolor fill
            float storagePct = capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : 0f;
            Color barColor = storagePct > 0.9f ? SporefrontColors.SporeRed :
                storagePct > 0.7f ? SporefrontColors.SporeAmber :
                UIHelper.GetResourceBarColor(resType);
            var (bg, fill) = UIHelper.CreateInkProgressBar(card, 14f, barColor);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(storagePct, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = 14;

            // Row 2: Collection rate (net for food)
            var rateRow = UIHelper.CreateHorizontalRow(card, 26f, 4f);
            var rateRowLE = rateRow.gameObject.AddComponent<LayoutElement>();
            rateRowLE.preferredHeight = 26;

            double rate = player.GetCollectionRate(resType);
            if (resType == ResourceType.Food)
                rate -= foodInfo.adjustedRate;
            string rateSign = rate >= 0 ? "+" : "";
            Color rateColor = rate > 0.01 ? SporefrontColors.SporeGreen :
                rate < -0.01 ? SporefrontColors.SporeRed : UIHelper.InkMutedText;

            var rateLabel = UIHelper.CreateLabel(rateRow.transform,
                $"Rate: {rateSign}{rate:F2}/s", UIConstants.FontBody, rateColor);
            var rateLE = rateLabel.gameObject.AddComponent<LayoutElement>();
            rateLE.flexibleWidth = 1;
            rateLE.preferredHeight = 26;

            // Food-specific: consumption breakdown
            if (resType == ResourceType.Food)
            {
                var consumeLabel = UIHelper.CreateLabel(rateRow.transform,
                    $"Consumption: -{foodInfo.adjustedRate:F2}/s ({foodInfo.civilian}civ + {foodInfo.military}mil)",
                    UIConstants.FontSmall, SporefrontColors.SporeRed, TextAnchor.MiddleRight);
                var consumeLE = consumeLabel.gameObject.AddComponent<LayoutElement>();
                consumeLE.flexibleWidth = 1;
                consumeLE.preferredHeight = 26;
            }

            // Wood-specific: farm upkeep note
            if (resType == ResourceType.Wood)
            {
                int farmCount = gameState.GetBuildingCount(BuildingType.Farm, localPlayerID);
                if (farmCount > 0)
                {
                    var upkeepLabel = UIHelper.CreateLabel(card,
                        $"Farm wood upkeep: {farmCount} farm(s) active", UIConstants.FontSmall,
                        UIHelper.InkMutedText);
                    var upkeepLE = upkeepLabel.gameObject.AddComponent<LayoutElement>();
                    upkeepLE.preferredHeight = 24;
                }
            }

            // Row 3: Gathering groups — compact when empty
            var villagerGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            int gatheringGroups = 0;
            int gatheringVillagers = 0;

            foreach (var vg in villagerGroups)
            {
                if (IsGatheringResource(vg, resType, gameState))
                {
                    gatheringGroups++;
                    gatheringVillagers += vg.villagerCount;
                }
            }

            if (gatheringGroups > 0)
            {
                var gatherLabel = UIHelper.CreateLabel(card,
                    $"Gathering: {gatheringGroups} group(s), {gatheringVillagers} villager(s)", UIConstants.FontBody,
                    SporefrontColors.SporeGreen);
                var gatherLE = gatherLabel.gameObject.AddComponent<LayoutElement>();
                gatherLE.preferredHeight = 26;
            }
            else
            {
                // Faded null state — compact and italic
                var noGatherLabel = UIHelper.CreateLabel(card,
                    "No villagers assigned", UIConstants.FontSmall, UIHelper.InkMutedText);
                noGatherLabel.fontStyle = FontStyle.Italic;
                var noGatherLE = noGatherLabel.gameObject.AddComponent<LayoutElement>();
                noGatherLE.preferredHeight = 24;
            }

            // Resource-specific building bonuses
            BuildBuildingBonuses(card, resType, gameState);
        }

        // ================================================================
        // Building Bonuses
        // ================================================================

        private void BuildBuildingBonuses(Transform parent, ResourceType resType, GameState gameState)
        {
            var buildings = gameState.GetBuildingsForPlayer(localPlayerID);
            int campCount = 0;
            BuildingType? campType = null;

            switch (resType)
            {
                case ResourceType.Wood:
                    campType = BuildingType.LumberCamp;
                    break;
                case ResourceType.Ore:
                    campType = BuildingType.MiningCamp;
                    break;
                case ResourceType.Food:
                    campType = BuildingType.Farm;
                    break;
            }

            if (campType.HasValue)
            {
                foreach (var b in buildings)
                {
                    if (b.buildingType == campType.Value && b.IsOperational)
                        campCount++;
                }
            }

            if (campCount > 0)
            {
                var bonusLabel = UIHelper.CreateLabel(parent,
                    $"{campType.Value.DisplayName()}(s): {campCount} active", 10,
                    SporefrontColors.SporeTeal);
                var bonusLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
                bonusLE.preferredHeight = 16;
            }

            // Mill adjacency for food
            if (resType == ResourceType.Food)
            {
                int millCount = 0;
                foreach (var b in buildings)
                {
                    if (b.buildingType == BuildingType.Mill && b.IsOperational)
                        millCount++;
                }
                if (millCount > 0)
                {
                    var millLabel = UIHelper.CreateLabel(parent,
                        $"Mill(s): {millCount} (+25% adjacent farm rate each)", UIConstants.FontSmall,
                        SporefrontColors.SporeTeal);
                    var millLE = millLabel.gameObject.AddComponent<LayoutElement>();
                    millLE.preferredHeight = 24;
                }
            }
        }

        // ================================================================
        // Bonus Section
        // ================================================================

        private void BuildBonusSection(PlayerState player)
        {
            // Gather research bonuses related to resources
            var bonuses = new List<(string name, double value)>();

            CheckResearchBonus(player, "FarmGatheringRate", "Farm Gathering Rate", bonuses);
            CheckResearchBonus(player, "MiningCampGatheringRate", "Mining Gathering Rate", bonuses);
            CheckResearchBonus(player, "LumberCampGatheringRate", "Lumber Gathering Rate", bonuses);
            CheckResearchBonus(player, "FoodConsumption", "Food Consumption", bonuses);
            CheckResearchBonus(player, "VillagerMarchSpeed", "Villager Speed", bonuses);
            CheckResearchBonus(player, "BuildingSpeed", "Building Speed", bonuses);

            if (bonuses.Count == 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Active Bonuses",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 26;

            foreach (var (name, value) in bonuses)
            {
                string sign = value >= 0 ? "+" : "";
                string display = value > -1 && value < 1
                    ? $"{sign}{value * 100:F0}%" : $"{sign}{value:F0}";
                Color color = value >= 0 ? SporefrontColors.SporeGreen : SporefrontColors.SporeRed;

                var bonusLabel = UIHelper.CreateLabel(contentRT,
                    $"  {name}: {display}", UIConstants.FontBody, color);
                var bonusLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
                bonusLE.preferredHeight = 26;
            }
        }

        private void CheckResearchBonus(PlayerState player, string bonusKey, string displayName,
            List<(string, double)> bonuses)
        {
            double value = player.GetResearchBonus(bonusKey);
            if (Math.Abs(value) > 0.001)
                bonuses.Add((displayName, value));
        }

        // ================================================================
        // Helpers
        // ================================================================

        private bool IsGatheringResource(VillagerGroupData vg, ResourceType resType, GameState gameState)
        {
            if (vg.currentTask is GatheringTask gatherTask)
                return gatherTask.GatherResourceType == resType;

            if (vg.currentTask is GatheringResourceTask gatherResTask)
            {
                var rp = gameState.GetResourcePoint(gatherResTask.ResourcePointID);
                if (rp != null)
                    return ResourcePointToResourceType(rp) == resType;
            }

            return false;
        }

        private ResourceType? ResourcePointToResourceType(ResourcePointData rp)
        {
            return rp.resourceType.ResourceYield();
        }
    }
}
