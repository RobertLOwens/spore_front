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
        // Events
        // ================================================================

        public event Action<Guid, BuildingType, HexCoordinate, int> OnUpgradeRequested;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private GameObject backdrop;
        private RectTransform contentRT;
        private Guid? currentBuildingID;
        private Guid localPlayerID;

        // Cached references for incremental refresh
        private Image hpFill;
        private Text hpLabel;
        private Image constructionProgressFill;
        private Image upgradeProgressFill;

        // Extracted section state
        private BuildingTrainingSection.State trainingState;
        private BuildingMarketSection.State marketState;

        // Fade animation
        private CanvasGroup backdropCG;
        private Coroutine fadeCoroutine;

        // Structural fingerprint — only full-rebuild when these change
        private BuildingState cachedState;
        private int cachedLevel;
        private int cachedTrainingQueueCount;
        private int cachedVillagerQueueCount;
        private int cachedGarrisonTotal;
        private bool cachedCanUpgrade;
        private bool hasCachedFingerprint;

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
            backdropCG = backdrop.AddComponent<CanvasGroup>();
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Main panel — centered 400x500
            panel = UIHelper.CreatePanel(backdrop.transform, "BuildingDetailPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalSmallW, UIConstants.ModalMediumH);

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

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid buildingID, GameState gameState)
        {
            currentBuildingID = buildingID;
            hasCachedFingerprint = false;
            Rebuild(gameState);
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            backdrop.SetActive(true);
            fadeCoroutine = StartCoroutine(UIHelper.FadeIn(backdropCG));
        }

        public void Close()
        {
            currentBuildingID = null;
            hasCachedFingerprint = false;
            constructionProgressFill = null;
            upgradeProgressFill = null;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(UIHelper.FadeOut(backdropCG));
        }

        public void Refresh(GameState gameState)
        {
            if (!currentBuildingID.HasValue || !backdrop.activeSelf) return;

            var building = gameState.GetBuilding(currentBuildingID.Value);
            if (building == null) { Close(); return; }

            // Check if structural fingerprint matches — if so, only update dynamic values
            if (hasCachedFingerprint && FingerprintMatches(building))
            {
                IncrementalUpdate(building);
                return;
            }

            Rebuild(gameState);
        }

        // ================================================================
        // Fingerprint & Incremental Update
        // ================================================================

        private bool FingerprintMatches(BuildingData building)
        {
            int trainingCount = building.trainingQueue != null ? building.trainingQueue.Count : 0;
            int villagerCount = building.villagerTrainingQueue != null ? building.villagerTrainingQueue.Count : 0;
            int garrisonTotal = building.GetTotalGarrisonCount();

            return building.state == cachedState
                && building.level == cachedLevel
                && trainingCount == cachedTrainingQueueCount
                && villagerCount == cachedVillagerQueueCount
                && garrisonTotal == cachedGarrisonTotal
                && building.CanUpgrade == cachedCanUpgrade;
        }

        private void CacheFingerprint(BuildingData building)
        {
            cachedState = building.state;
            cachedLevel = building.level;
            cachedTrainingQueueCount = building.trainingQueue != null ? building.trainingQueue.Count : 0;
            cachedVillagerQueueCount = building.villagerTrainingQueue != null ? building.villagerTrainingQueue.Count : 0;
            cachedGarrisonTotal = building.GetTotalGarrisonCount();
            cachedCanUpgrade = building.CanUpgrade;
            hasCachedFingerprint = true;
        }

        private void IncrementalUpdate(BuildingData building)
        {
            // Update HP bar
            if (hpFill != null && building.maxHealth > 0)
            {
                float pct = (float)(building.health / building.maxHealth);
                var fillRT = hpFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
            }
            if (hpLabel != null && building.maxHealth > 0)
            {
                hpLabel.text = $"HP: {(int)building.health}/{(int)building.maxHealth}";
            }

            // Update construction progress bar
            if (constructionProgressFill != null && building.state == BuildingState.Constructing)
            {
                var fillRT = constructionProgressFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)building.constructionProgress), 1);
            }

            // Update upgrade progress bar
            if (upgradeProgressFill != null && building.state == BuildingState.Upgrading)
            {
                var fillRT = upgradeProgressFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)building.upgradeProgress), 1);
            }

            // Update queue labels
            if (trainingState.queueLabel != null && building.trainingQueue != null)
            {
                trainingState.queueLabel.text = $"Queue: {building.trainingQueue.Count} item(s)";
            }
            if (trainingState.villagerQueueLabel != null && building.villagerTrainingQueue != null)
            {
                trainingState.villagerQueueLabel.text = $"Villager queue: {building.villagerTrainingQueue.Count} item(s)";
            }
        }

        /// <summary>
        /// Returns cached progress fill Image references for per-frame interpolation.
        /// </summary>
        public (Image constructionFill, Image upgradeFill) GetProgressFillRefs()
        {
            return (constructionProgressFill, upgradeProgressFill);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;
        public Guid? CurrentBuildingID => currentBuildingID;

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
            constructionProgressFill = null;
            upgradeProgressFill = null;

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
                    double progress = building.state == BuildingState.Constructing
                        ? building.constructionProgress
                        : building.upgradeProgress;
                    int pctInt = Mathf.Clamp((int)(progress * 100), 0, 100);

                    var (bg, fill, pctLabel) = UIHelper.CreateProgressBarWithLabel(contentRT, 16f,
                        SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                    pctLabel.text = $"{pctInt}%";
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)progress), 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.preferredHeight = 16;

                    // Cache fill reference for incremental updates
                    if (building.state == BuildingState.Constructing)
                        constructionProgressFill = fill;
                    else
                        upgradeProgressFill = fill;

                    // Time estimate
                    double currentTime = gameState.currentTime;
                    double? remaining = building.state == BuildingState.Constructing
                        ? building.GetRemainingConstructionTime(currentTime)
                        : building.GetRemainingUpgradeTime(currentTime);
                    if (remaining.HasValue)
                    {
                        var etaLabel = UIHelper.CreateLabel(contentRT,
                            $"~{UIHelper.FormatTime(remaining.Value)} remaining",
                            UIConstants.FontSmall, SporefrontColors.InkLight);
                        var etaLE = etaLabel.gameObject.AddComponent<LayoutElement>();
                        etaLE.preferredHeight = 18;
                    }
                }
            }

            UIHelper.CreateDivider(contentRT);

            // Training section (military buildings + CC)
            if (building.IsOperational)
            {
                trainingState = BuildingTrainingSection.BuildTraining(
                    contentRT, building, gameState, player, localPlayerID);
                UIHelper.CreateDivider(contentRT);

                // Market section
                if (building.buildingType == BuildingType.Market)
                {
                    marketState = BuildingMarketSection.Build(
                        contentRT, building, gameState, player, localPlayerID,
                        currentBuildingID, () => Rebuild(GameEngine.Instance.gameState));
                    UIHelper.CreateDivider(contentRT);
                }

                // Unit upgrades section (military production buildings)
                var availableUpgrades = UnitUpgradeTypeExtensions.UpgradesForBuilding(building.buildingType);
                if (availableUpgrades.Count > 0)
                {
                    BuildingUnitUpgradesSection.Build(
                        contentRT, building, gameState, player, localPlayerID,
                        currentBuildingID, availableUpgrades);
                    UIHelper.CreateDivider(contentRT);
                }

                // Garrison section
                BuildingTrainingSection.BuildGarrison(contentRT, building);
                UIHelper.CreateDivider(contentRT);

                // Deploy section
                BuildingTrainingSection.BuildDeploy(contentRT, building, gameState, localPlayerID);
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

            // Cache fingerprint for incremental refresh
            CacheFingerprint(building);
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
        // Upgrade Section
        // ================================================================

        private void BuildUpgradeSection(BuildingData building, PlayerState player)
        {
            if (!building.CanUpgrade) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Upgrade",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var cost = building.GetUpgradeCost();
            bool canAfford = player != null && player.CanAfford(cost);
            int nextLevel = building.level + 1;

            var infoLabel = UIHelper.CreateLabel(contentRT,
                $"Lv.{building.level} -> Lv.{nextLevel}  Cost: {UIHelper.FormatCost(cost)}", 12,
                canAfford ? UIHelper.BodyTextColor : SporefrontColors.SporeRed);
            infoLabel.supportRichText = true;
            var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 22;

            var capturedID = building.id;
            var capturedType = building.buildingType;
            var capturedCoord = building.coordinate;
            var capturedLevel = building.level;
            var upgradeBtn = UIHelper.CreateButton(contentRT, "Upgrade",
                canAfford ? SporefrontColors.SporeAmber : SporefrontColors.InkFaded,
                canAfford ? UIHelper.ButtonText : SporefrontColors.InkLight, 12, () =>
                {
                    OnUpgradeRequested?.Invoke(capturedID, capturedType, capturedCoord, capturedLevel);
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
                upgradeProgressFill = fill;
            }
        }


        // ================================================================
        // Home Base Section
        // ================================================================

        private void BuildHomeBaseSection(BuildingData building, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Home Base",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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

    }
}
