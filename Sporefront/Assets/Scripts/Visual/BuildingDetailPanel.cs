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
    public class BuildingDetailPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, BuildingType, HexCoordinate, int> OnUpgradeRequested;
        public event Action<Guid> OnCancelUpgradeRequested;
        public event Action<Guid> OnCancelDemolishRequested;
        public event Action<Guid> OnDemolishRequested;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private Guid? currentBuildingID;

        // Cached references for incremental refresh
        private Image hpFill;
        private Text hpLabel;
        private Image constructionProgressFill;
        private Image upgradeProgressFill;

        // Extracted section state
        private BuildingTrainingSection.State trainingState;
        private BuildingMarketSection.State marketState;

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

            // Main panel — centered 480x620
            panel = UIHelper.CreatePanel(backdrop.transform, "BuildingDetailPanel", UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalDetailW, UIConstants.ModalDetailH);
            PopupTendrilDecorator.Attach(rt);

            // Click sink — absorbs pointer clicks inside the panel so they don't
            // propagate up to the backdrop's close-on-click Button
            var panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            // ScrollView inside panel
            var scroll = UIHelper.CreateScrollView(panel.transform, "DetailScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 48); // Space for close button
            scrollRT.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Close);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 46);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid buildingID, GameState gameState)
        {
            currentBuildingID = buildingID;
            hasCachedFingerprint = false;
            Rebuild(gameState);
            backdrop.SetActive(true);
            FadeIn();
        }

        public void Close()
        {
            currentBuildingID = null;
            hasCachedFingerprint = false;
            constructionProgressFill = null;
            upgradeProgressFill = null;
            FadeOut();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentBuildingID.HasValue || !backdrop.activeSelf) return;

            var building = gameState.GetBuilding(currentBuildingID.Value);
            if (building == null) { Close(); return; }

            // Check if structural fingerprint matches — if so, only update dynamic values
            if (hasCachedFingerprint && FingerprintMatches(building))
            {
                IncrementalUpdate(building, gameState);
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

        private void IncrementalUpdate(BuildingData building, GameState gameState)
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

            // Update military training progress bars
            if (trainingState.trainingProgressFills != null && building.trainingQueue != null)
            {
                for (int i = 0; i < trainingState.trainingProgressFills.Count && i < building.trainingQueue.Count; i++)
                {
                    var entry = building.trainingQueue[i];
                    float pct = Mathf.Clamp01((float)entry.GetProgress(gameState.currentTime,
                        building.GetTrainingSpeedMultiplier()));
                    var fillRT = trainingState.trainingProgressFills[i].GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(pct, 1);

                    if (i < trainingState.trainingProgressLabels.Count)
                        trainingState.trainingProgressLabels[i].text = $"{(int)(pct * 100)}%";

                    if (i < trainingState.trainingTimeLabels.Count)
                    {
                        double baseTime = entry.unitType.TrainingTime() * entry.quantity;
                        double totalTime = baseTime / building.GetTrainingSpeedMultiplier();
                        double elapsed = gameState.currentTime - entry.startTime;
                        double remaining = System.Math.Max(0, totalTime - elapsed);
                        trainingState.trainingTimeLabels[i].text = $"~{UIHelper.FormatTime(remaining)}";
                    }
                }
            }

            // Update villager training progress bars
            if (trainingState.villagerProgressFills != null && building.villagerTrainingQueue != null)
            {
                for (int i = 0; i < trainingState.villagerProgressFills.Count && i < building.villagerTrainingQueue.Count; i++)
                {
                    var entry = building.villagerTrainingQueue[i];
                    float pct = Mathf.Clamp01((float)entry.GetProgress(gameState.currentTime));
                    var fillRT = trainingState.villagerProgressFills[i].GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(pct, 1);

                    if (i < trainingState.villagerProgressLabels.Count)
                        trainingState.villagerProgressLabels[i].text = $"{(int)(pct * 100)}%";

                    if (i < trainingState.villagerTimeLabels.Count)
                    {
                        double totalTime = VillagerTrainingEntry.TrainingTimePerVillager * entry.quantity;
                        double elapsed = gameState.currentTime - entry.startTime;
                        double remaining = System.Math.Max(0, totalTime - elapsed);
                        trainingState.villagerTimeLabels[i].text = $"~{UIHelper.FormatTime(remaining)}";
                    }
                }
            }
        }

        /// <summary>
        /// Returns cached progress fill Image references for per-frame interpolation.
        /// </summary>
        public (Image constructionFill, Image upgradeFill) GetProgressFillRefs()
        {
            return (constructionProgressFill, upgradeProgressFill);
        }

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

            // Adjust scroll content spacing
            var contentVLG = contentRT.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null)
            {
                contentVLG.spacing = UIConstants.SectionCardSpacing;
                contentVLG.padding = new RectOffset(12, 12, 12, 12);
            }

            var player = gameState.GetPlayer(localPlayerID);

            // Header
            var header = UIHelper.CreateLabel(contentRT,
                $"{building.buildingType.DisplayName()} Lv.{building.level}",
                UIConstants.FontTitle, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36;

            // Status / HP card
            var statusCard = UIHelper.CreateSectionCard(contentRT, "StatusCard", "Status");
            BuildHPBar(building, statusCard.transform);

            // State info
            if (building.state != BuildingState.Completed)
            {
                var stateLabel = UIHelper.CreateLabel(statusCard.transform, $"State: {building.state}",
                    UIConstants.FontCaption, UIHelper.InkBodyText);
                var stateLE = stateLabel.gameObject.AddComponent<LayoutElement>();
                stateLE.preferredHeight = 20;

                if (building.state == BuildingState.Constructing || building.state == BuildingState.Upgrading)
                {
                    double progress = building.state == BuildingState.Constructing
                        ? building.constructionProgress
                        : building.upgradeProgress;
                    int pctInt = Mathf.Clamp((int)(progress * 100), 0, 100);

                    var (bg, fill, pctLabel) = UIHelper.CreateInkProgressBarWithLabel(statusCard.transform, 16f,
                        UIHelper.InkMutedText, SporefrontColors.SporeAmber);
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
                        var etaLabel = UIHelper.CreateLabel(statusCard.transform,
                            $"~{UIHelper.FormatTime(remaining.Value)} remaining",
                            UIConstants.FontSmall, UIHelper.InkMutedText);
                        var etaLE = etaLabel.gameObject.AddComponent<LayoutElement>();
                        etaLE.preferredHeight = 18;
                    }

                    // Cancel button for upgrading buildings
                    if (building.state == BuildingState.Upgrading &&
                        building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                    {
                        var capturedID = building.id;
                        var cancelBtn = UIHelper.CreateButton(statusCard.transform, "Cancel Upgrade (50% refund)",
                            SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                            {
                                OnCancelUpgradeRequested?.Invoke(capturedID);
                            });
                        var cancelLabel = UIHelper.GetButtonLabel(cancelBtn);
                        UIHelper.EnableAutoFit(cancelLabel, 10, UIConstants.FontCaption);
                        var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
                        cancelLE.preferredHeight = 30;
                    }
                }

                // Cancel button for demolishing buildings
                if (building.state == BuildingState.Demolishing &&
                    building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var capturedID = building.id;
                    var cancelBtn = UIHelper.CreateButton(statusCard.transform, "Cancel Demolish",
                        SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                        {
                            OnCancelDemolishRequested?.Invoke(capturedID);
                        });
                    var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
                    cancelLE.preferredHeight = 30;
                }
            }

            // Training section (military buildings + CC)
            if (building.IsOperational)
            {
                var trainingCard = UIHelper.CreateSectionCard(contentRT, "TrainingCard");
                var trainingCardRT = (RectTransform)trainingCard.transform;
                trainingState = BuildingTrainingSection.BuildTraining(
                    trainingCardRT, building, gameState, player, localPlayerID);

                // Market section
                if (building.buildingType == BuildingType.Market)
                {
                    var marketCard = UIHelper.CreateSectionCard(contentRT, "MarketCard");
                    var marketCardRT = (RectTransform)marketCard.transform;
                    marketState = BuildingMarketSection.Build(
                        marketCardRT, building, gameState, player, localPlayerID,
                        currentBuildingID, () => Rebuild(GameEngine.Instance.gameState));
                }

                // Unit upgrades section (military production buildings)
                var availableUpgrades = UnitUpgradeTypeExtensions.UpgradesForBuilding(building.buildingType);
                if (availableUpgrades.Count > 0)
                {
                    var upgradesCard = UIHelper.CreateSectionCard(contentRT, "UnitUpgradesCard");
                    var upgradesCardRT = (RectTransform)upgradesCard.transform;
                    BuildingUnitUpgradesSection.Build(
                        upgradesCardRT, building, gameState, player, localPlayerID,
                        currentBuildingID, availableUpgrades);
                }

                // Garrison section
                var garrisonCard = UIHelper.CreateSectionCard(contentRT, "GarrisonCard");
                var garrisonCardRT = (RectTransform)garrisonCard.transform;
                BuildingTrainingSection.BuildGarrison(garrisonCardRT, building);

                // Deploy section
                var deployCard = UIHelper.CreateSectionCard(contentRT, "DeployCard");
                var deployCardRT = (RectTransform)deployCard.transform;
                BuildingTrainingSection.BuildDeploy(deployCardRT, building, gameState, localPlayerID);

                // Home base section
                int? homeBaseCapacity = building.GetArmyHomeBaseCapacity();
                if (homeBaseCapacity.HasValue || building.buildingType == BuildingType.CityCenter)
                {
                    BuildHomeBaseSection(building, gameState);
                }

                // Upgrade section
                BuildUpgradeSection(building, player);

                // Demolish button (completed, player-owned, non-CityCenter)
                if (building.CanDemolish &&
                    building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var demolishCard = UIHelper.CreateSectionCard(contentRT, "DemolishCard");
                    var capturedID = building.id;
                    var demolishBtn = UIHelper.CreateButton(demolishCard.transform, "Demolish",
                        SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                        {
                            OnDemolishRequested?.Invoke(capturedID);
                        });
                    var demolishLE = demolishBtn.gameObject.AddComponent<LayoutElement>();
                    demolishLE.preferredHeight = 32;
                }
            }

            // Cache fingerprint for incremental refresh
            CacheFingerprint(building);
        }

        // ================================================================
        // HP Bar
        // ================================================================

        private void BuildHPBar(BuildingData building, Transform parent)
        {
            if (building.maxHealth <= 0) return;

            var row = UIHelper.CreateHorizontalRow(parent, 18f, 4f);

            hpLabel = UIHelper.CreateLabel(row.transform,
                $"HP: {(int)building.health}/{(int)building.maxHealth}", UIConstants.FontCaption);
            var labelLE = hpLabel.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 100;

            var (bg, fill) = UIHelper.CreateInkProgressBar(row.transform, 14f,
                UIHelper.InkMutedText, SporefrontColors.SporeGreen);
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

            var card = UIHelper.CreateSectionCard(contentRT, "UpgradeCard", "Upgrade");

            var cost = building.GetUpgradeCost();
            bool canAfford = player != null && player.CanAfford(cost);
            int nextLevel = building.level + 1;

            var infoLabel = UIHelper.CreateLabel(card.transform,
                $"Lv.{building.level} -> Lv.{nextLevel}  Cost: {UIHelper.FormatCost(cost)}", UIConstants.FontCaption,
                canAfford ? UIHelper.InkBodyText : SporefrontColors.SporeRed);
            infoLabel.supportRichText = true;
            var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 22;

            var capturedID = building.id;
            var capturedType = building.buildingType;
            var capturedCoord = building.coordinate;
            var capturedLevel = building.level;
            var upgradeBtn = UIHelper.CreateButton(card.transform, "Upgrade",
                canAfford ? SporefrontColors.SporeAmber : UIHelper.InkMutedText,
                canAfford ? UIHelper.ButtonText : UIHelper.InkMutedText, UIConstants.FontCaption, () =>
                {
                    OnUpgradeRequested?.Invoke(capturedID, capturedType, capturedCoord, capturedLevel);
                });
            upgradeBtn.interactable = canAfford;
            var btnLE = upgradeBtn.gameObject.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 32;

            // Upgrade progress
            if (building.state == BuildingState.Upgrading)
            {
                var (bg, fill) = UIHelper.CreateInkProgressBar(card.transform, 14f,
                    UIHelper.InkMutedText, SporefrontColors.SporeAmber);
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
            var card = UIHelper.CreateSectionCard(contentRT, "HomeBaseCard", "Home Base");

            int? capacity = building.GetArmyHomeBaseCapacity();
            int count = gameState.GetArmyCountForHomeBase(building.id);

            string capacityText = capacity.HasValue
                ? $"Army Capacity: {count}/{capacity.Value}"
                : $"Army Capacity: {count} (Unlimited)";

            var capLabel = UIHelper.CreateLabel(card.transform, capacityText, UIConstants.FontCaption, UIHelper.InkMutedText);
            var capLE = capLabel.gameObject.AddComponent<LayoutElement>();
            capLE.preferredHeight = 20;

            var armies = gameState.GetArmiesForHomeBase(building.id);
            if (armies.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(card.transform, "No armies based here", UIConstants.FontCaption,
                    UIHelper.InkMutedText);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 18;
            }
            else
            {
                foreach (var army in armies)
                {
                    var row = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

                    var nameLabel = UIHelper.CreateLabel(row.transform,
                        army.name ?? "Army", UIConstants.FontCaption, UIHelper.InkMutedText);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;

                    int totalUnits = army.GetTotalUnits();
                    var unitsLabel = UIHelper.CreateLabel(row.transform,
                        $"{totalUnits} units", UIConstants.FontCaption, UIHelper.InkMutedText);
                    var unitsLE = unitsLabel.gameObject.AddComponent<LayoutElement>();
                    unitsLE.preferredWidth = 60;

                    var coordLabel = UIHelper.CreateLabel(row.transform,
                        $"({army.coordinate.q},{army.coordinate.r})", UIConstants.FontCaption,
                        UIHelper.InkMutedText);
                    var coordLE = coordLabel.gameObject.AddComponent<LayoutElement>();
                    coordLE.preferredWidth = 50;
                }
            }
        }

    }
}
