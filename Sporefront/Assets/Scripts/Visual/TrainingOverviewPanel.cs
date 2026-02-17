// ============================================================================
// FILE: Visual/TrainingOverviewPanel.cs
// PURPOSE: Modal overview of all active training across player buildings.
//          Shows building name, unit being trained, progress bar, time remaining.
//          New panel (no Swift source).
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
    public class TrainingOverviewPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnBuildingSelected;
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
            backdrop = UIHelper.CreatePanel(canvasTransform, "TrainingOverviewBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel -- centered 420x480
            panel = UIHelper.CreatePanel(backdrop.transform, "TrainingOverviewPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 420, 480);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Active Training",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(8, -32);
            headerRT.offsetMax = new Vector2(-8, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "TrainingScroll", out contentRT);
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

            var buildings = gameState.GetBuildingsForPlayer(localPlayerID);
            int trainingCount = 0;

            // Military training section
            var militaryTrainingBuildings = new List<(BuildingData building, TrainingQueueEntry entry)>();
            foreach (var building in buildings)
            {
                if (building.trainingQueue == null || building.trainingQueue.Count == 0) continue;
                foreach (var entry in building.trainingQueue)
                    militaryTrainingBuildings.Add((building, entry));
            }

            if (militaryTrainingBuildings.Count > 0)
            {
                var sectionLabel = UIHelper.CreateLabel(contentRT,
                    $"Military Training ({militaryTrainingBuildings.Count})",
                    UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                    TextAnchor.MiddleLeft, true);
                var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
                sectionLE.preferredHeight = 26;

                foreach (var (building, entry) in militaryTrainingBuildings)
                {
                    BuildMilitaryTrainingRow(building, entry, gameState);
                    trainingCount++;
                }

                UIHelper.CreateDivider(contentRT);
            }

            // Villager training section
            var villagerTrainingBuildings = new List<(BuildingData building, VillagerTrainingEntry entry)>();
            foreach (var building in buildings)
            {
                if (building.villagerTrainingQueue == null || building.villagerTrainingQueue.Count == 0) continue;
                foreach (var entry in building.villagerTrainingQueue)
                    villagerTrainingBuildings.Add((building, entry));
            }

            if (villagerTrainingBuildings.Count > 0)
            {
                var sectionLabel = UIHelper.CreateLabel(contentRT,
                    $"Villager Training ({villagerTrainingBuildings.Count})",
                    UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                    TextAnchor.MiddleLeft, true);
                var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
                sectionLE.preferredHeight = 26;

                foreach (var (building, entry) in villagerTrainingBuildings)
                {
                    BuildVillagerTrainingRow(building, entry, gameState);
                    trainingCount++;
                }
            }

            // Empty state
            if (trainingCount == 0)
            {
                var emptyCard = UIHelper.CreatePanel(contentRT, "EmptyCard", SporefrontColors.ParchmentMid);
                var emptyCardLE = emptyCard.AddComponent<LayoutElement>();
                emptyCardLE.preferredHeight = 60;
                emptyCardLE.flexibleWidth = 1;

                var emptyLabel = UIHelper.CreateLabel(emptyCard.transform, "No active training",
                    UIHelper.DefaultBodyFontSize, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var emptyLabelRT = emptyLabel.GetComponent<RectTransform>();
                UIHelper.StretchFull(emptyLabelRT);

                var hintLabel = UIHelper.CreateLabel(contentRT,
                    "Train units from military buildings or villagers from the City Center.",
                    11, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                var hintLE = hintLabel.gameObject.AddComponent<LayoutElement>();
                hintLE.preferredHeight = 36;
            }
        }

        // ================================================================
        // Military Training Row
        // ================================================================

        private void BuildMilitaryTrainingRow(BuildingData building, TrainingQueueEntry entry,
            GameState gameState)
        {
            var card = UIHelper.CreatePanel(contentRT, "MilitaryTrainCard", SporefrontColors.ParchmentMid);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 68;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Building name + unit name
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

            var buildingLabel = UIHelper.CreateLabel(topRow.transform,
                building.buildingType.DisplayName(), 12, UIHelper.HeaderTextColor);
            var buildingLE = buildingLabel.gameObject.AddComponent<LayoutElement>();
            buildingLE.flexibleWidth = 1;
            buildingLE.preferredHeight = 20;

            var unitLabel = UIHelper.CreateLabel(topRow.transform,
                $"{entry.unitType.DisplayName()} x{entry.quantity}", 12,
                SporefrontColors.InkDark, TextAnchor.MiddleRight);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.preferredWidth = 120;
            unitLE.preferredHeight = 20;

            // Row 2: Progress bar
            double progress = entry.GetProgress(gameState.currentTime,
                building.GetTrainingSpeedMultiplier());
            float progressFloat = Mathf.Clamp01((float)progress);

            var progressRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);

            var progressLabel = UIHelper.CreateLabel(progressRow.transform,
                $"{(int)(progressFloat * 100)}%", 10, SporefrontColors.InkLight);
            var progressLabelLE = progressLabel.gameObject.AddComponent<LayoutElement>();
            progressLabelLE.preferredWidth = 35;
            progressLabelLE.preferredHeight = 16;

            var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(progressFloat, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            // Row 3: Time remaining
            double baseTime = entry.unitType.TrainingTime() * entry.quantity;
            double speedMultiplier = building.GetTrainingSpeedMultiplier();
            double totalTime = baseTime / speedMultiplier;
            double elapsed = gameState.currentTime - entry.startTime;
            double remaining = Math.Max(0, totalTime - elapsed);

            string timeStr = remaining > 60 ? $"{(int)(remaining / 60)}m {(int)(remaining % 60)}s"
                : $"{(int)remaining}s";

            var timeLabel = UIHelper.CreateLabel(card.transform,
                $"Time remaining: {timeStr}", 10, SporefrontColors.InkLight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredHeight = 16;

            // Make card tappable to open building detail
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.ColorTint;
            var colors = cardBtn.colors;
            colors.normalColor = SporefrontColors.ParchmentMid;
            colors.highlightedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.white, 0.1f);
            colors.pressedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.black, 0.1f);
            cardBtn.colors = colors;

            var capturedBuildingID = building.id;
            cardBtn.onClick.AddListener(() => OnBuildingSelected?.Invoke(capturedBuildingID));
        }

        // ================================================================
        // Villager Training Row
        // ================================================================

        private void BuildVillagerTrainingRow(BuildingData building, VillagerTrainingEntry entry,
            GameState gameState)
        {
            var card = UIHelper.CreatePanel(contentRT, "VillagerTrainCard", SporefrontColors.ParchmentMid);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 68;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Building name + unit name
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

            var buildingLabel = UIHelper.CreateLabel(topRow.transform,
                building.buildingType.DisplayName(), 12, UIHelper.HeaderTextColor);
            var buildingLE = buildingLabel.gameObject.AddComponent<LayoutElement>();
            buildingLE.flexibleWidth = 1;
            buildingLE.preferredHeight = 20;

            var unitLabel = UIHelper.CreateLabel(topRow.transform,
                $"Villager x{entry.quantity}", 12,
                SporefrontColors.InkDark, TextAnchor.MiddleRight);
            var unitLE = unitLabel.gameObject.AddComponent<LayoutElement>();
            unitLE.preferredWidth = 100;
            unitLE.preferredHeight = 20;

            // Row 2: Progress bar
            double progress = entry.GetProgress(gameState.currentTime);
            float progressFloat = Mathf.Clamp01((float)progress);

            var progressRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);

            var progressLabel = UIHelper.CreateLabel(progressRow.transform,
                $"{(int)(progressFloat * 100)}%", 10, SporefrontColors.InkLight);
            var progressLabelLE = progressLabel.gameObject.AddComponent<LayoutElement>();
            progressLabelLE.preferredWidth = 35;
            progressLabelLE.preferredHeight = 16;

            var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(progressFloat, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            // Row 3: Time remaining
            double totalTime = VillagerTrainingEntry.TrainingTimePerVillager * entry.quantity;
            double elapsed = gameState.currentTime - entry.startTime;
            double remaining = Math.Max(0, totalTime - elapsed);

            string timeStr = remaining > 60 ? $"{(int)(remaining / 60)}m {(int)(remaining % 60)}s"
                : $"{(int)remaining}s";

            var timeLabel = UIHelper.CreateLabel(card.transform,
                $"Time remaining: {timeStr}", 10, SporefrontColors.InkLight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredHeight = 16;

            // Make card tappable
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.ColorTint;
            var colors = cardBtn.colors;
            colors.normalColor = SporefrontColors.ParchmentMid;
            colors.highlightedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.white, 0.1f);
            colors.pressedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.black, 0.1f);
            cardBtn.colors = colors;

            var capturedBuildingID = building.id;
            cardBtn.onClick.AddListener(() => OnBuildingSelected?.Invoke(capturedBuildingID));
        }
    }
}
