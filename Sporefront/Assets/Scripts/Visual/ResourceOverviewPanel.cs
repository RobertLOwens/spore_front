// ============================================================================
// FILE: Visual/ResourceOverviewPanel.cs
// PURPOSE: Modal overview with per-resource detail cards showing storage,
//          collection rates, gathering groups, and active bonuses.
//          Port from ResourceOverviewViewController.swift
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
    public class ResourceOverviewPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

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

            // Main panel -- centered 440x560
            panel = UIHelper.CreatePanel(backdrop.transform, "ResourceOverviewPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalMediumW, UIConstants.ModalLargeH);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Resource Overview",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
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

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 36);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!IsVisible) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

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
                UIHelper.CreateDivider(contentRT);
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
            var card = UIHelper.CreatePanel(contentRT, $"{resType}Card", SporefrontColors.ParchmentMid);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f;
            vlg.padding = new RectOffset(10, 10, 6, 6);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Resource name + icon + amount
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 24f, 4f);

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"[{UIHelper.ResourceIcon(resType)}] {resType.DisplayName()}", 14,
                UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 24;

            int amount = player.GetResource(resType);
            int capacity = gameState.GetStorageCapacity(localPlayerID, resType);
            var amountLabel = UIHelper.CreateLabel(topRow.transform,
                $"{amount} / {capacity}", 13,
                amount >= capacity ? SporefrontColors.SporeRed : UIHelper.BodyTextColor,
                TextAnchor.MiddleRight);
            var amountLE = amountLabel.gameObject.AddComponent<LayoutElement>();
            amountLE.preferredWidth = 100;
            amountLE.preferredHeight = 24;

            // Storage bar
            float storagePct = capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : 0f;
            Color barColor = storagePct > 0.9f ? SporefrontColors.SporeRed :
                storagePct > 0.7f ? SporefrontColors.SporeAmber : SporefrontColors.SporeGreen;
            var (bg, fill) = UIHelper.CreateProgressBar(card.transform, 10f,
                SporefrontColors.InkFaded, barColor);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(storagePct, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = 10;

            // Row 2: Collection rate
            var rateRow = UIHelper.CreateHorizontalRow(card.transform, 18f, 4f);

            double rate = player.GetCollectionRate(resType);
            string rateSign = rate >= 0 ? "+" : "";
            Color rateColor = rate > 0.01 ? SporefrontColors.SporeGreen :
                rate < -0.01 ? SporefrontColors.SporeRed : SporefrontColors.InkLight;

            var rateLabel = UIHelper.CreateLabel(rateRow.transform,
                $"Rate: {rateSign}{rate:F2}/s", 12, rateColor);
            var rateLE = rateLabel.gameObject.AddComponent<LayoutElement>();
            rateLE.flexibleWidth = 1;
            rateLE.preferredHeight = 18;

            // Food-specific: consumption info
            if (resType == ResourceType.Food)
            {
                var consumeLabel = UIHelper.CreateLabel(rateRow.transform,
                    $"Consumption: -{foodInfo.rate:F2}/s ({foodInfo.civilian}civ + {foodInfo.military}mil)",
                    10, SporefrontColors.SporeRed, TextAnchor.MiddleRight);
                var consumeLE = consumeLabel.gameObject.AddComponent<LayoutElement>();
                consumeLE.flexibleWidth = 1;
                consumeLE.preferredHeight = 18;
            }

            // Wood-specific: farm upkeep note
            if (resType == ResourceType.Wood)
            {
                int farmCount = gameState.GetBuildingCount(BuildingType.Farm, localPlayerID);
                if (farmCount > 0)
                {
                    var upkeepLabel = UIHelper.CreateLabel(card.transform,
                        $"Farm wood upkeep: {farmCount} farm(s) active", 10,
                        SporefrontColors.InkLight);
                    var upkeepLE = upkeepLabel.gameObject.AddComponent<LayoutElement>();
                    upkeepLE.preferredHeight = 16;
                }
            }

            // Row 3: Gathering groups
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
                var gatherLabel = UIHelper.CreateLabel(card.transform,
                    $"Gathering: {gatheringGroups} group(s), {gatheringVillagers} villager(s)", 11,
                    SporefrontColors.SporeGreen);
                var gatherLE = gatherLabel.gameObject.AddComponent<LayoutElement>();
                gatherLE.preferredHeight = 18;
            }
            else
            {
                var noGatherLabel = UIHelper.CreateLabel(card.transform,
                    "No villagers assigned", 11, SporefrontColors.InkFaded);
                var noGatherLE = noGatherLabel.gameObject.AddComponent<LayoutElement>();
                noGatherLE.preferredHeight = 18;
            }

            // Resource-specific building bonuses
            BuildBuildingBonuses(card.transform, resType, gameState);

            // Auto-size card
            int rowCount = 4; // base rows
            if (resType == ResourceType.Food) rowCount++;
            if (resType == ResourceType.Wood && gameState.GetBuildingCount(BuildingType.Farm, localPlayerID) > 0) rowCount++;
            cardLE.preferredHeight = 28 + rowCount * 20;
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
                        $"Mill(s): {millCount} (+25% adjacent farm rate each)", 10,
                        SporefrontColors.SporeTeal);
                    var millLE = millLabel.gameObject.AddComponent<LayoutElement>();
                    millLE.preferredHeight = 16;
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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
                    $"  {name}: {display}", 11, color);
                var bonusLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
                bonusLE.preferredHeight = 18;
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
