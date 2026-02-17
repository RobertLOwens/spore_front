// ============================================================================
// FILE: Visual/BuildingsOverviewPanel.cs
// PURPOSE: Modal overview of all player buildings with filter tabs, HP bars,
//          construction/upgrade progress. Port from BuildingsOverviewViewController.swift
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
    public class BuildingsOverviewPanel : MonoBehaviour
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

        // Filter
        private enum FilterMode { All, Economic, Military }
        private FilterMode currentFilter = FilterMode.All;
        private Button allFilterBtn;
        private Button economicFilterBtn;
        private Button militaryFilterBtn;
        private Text countLabel;

        // Cached game state for filter changes
        private GameState cachedGameState;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "BuildingsOverviewBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel -- centered 420x520
            panel = UIHelper.CreatePanel(backdrop.transform, "BuildingsOverviewPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 420, 520);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Buildings",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(8, -32);
            headerRT.offsetMax = new Vector2(-8, 0);

            // Filter tabs row
            var filterRow = UIHelper.CreateHorizontalRow(panel.transform, 30f, 4f);
            var filterRT = filterRow.GetComponent<RectTransform>();
            filterRT.anchorMin = new Vector2(0, 1);
            filterRT.anchorMax = new Vector2(1, 1);
            filterRT.pivot = new Vector2(0.5f, 1f);
            filterRT.offsetMin = new Vector2(8, -66);
            filterRT.offsetMax = new Vector2(-8, -36);

            allFilterBtn = CreateFilterButton(filterRow.transform, "All", () => SetFilter(FilterMode.All));
            economicFilterBtn = CreateFilterButton(filterRow.transform, "Economic", () => SetFilter(FilterMode.Economic));
            militaryFilterBtn = CreateFilterButton(filterRow.transform, "Military", () => SetFilter(FilterMode.Military));

            // Count label
            countLabel = UIHelper.CreateLabel(filterRow.transform, "", 11,
                SporefrontColors.InkLight, TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.flexibleWidth = 1;
            countLE.preferredHeight = 30;

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "BuildingsScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 40);
            scrollRT.offsetMax = new Vector2(0, -70);

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

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            cachedGameState = gameState;
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
            cachedGameState = gameState;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Filter
        // ================================================================

        private void SetFilter(FilterMode mode)
        {
            currentFilter = mode;
            UpdateFilterButtonColors();
            if (cachedGameState != null)
                Rebuild(cachedGameState);
        }

        private void UpdateFilterButtonColors()
        {
            SetFilterActive(allFilterBtn, currentFilter == FilterMode.All);
            SetFilterActive(economicFilterBtn, currentFilter == FilterMode.Economic);
            SetFilterActive(militaryFilterBtn, currentFilter == FilterMode.Military);
        }

        private void SetFilterActive(Button btn, bool active)
        {
            var img = btn.GetComponent<Image>();
            img.color = active ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark;
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

            var buildings = gameState.GetBuildingsForPlayer(localPlayerID);

            // Apply filter
            var filtered = new List<BuildingData>();
            foreach (var b in buildings)
            {
                switch (currentFilter)
                {
                    case FilterMode.All:
                        filtered.Add(b);
                        break;
                    case FilterMode.Economic:
                        if (b.buildingType.Category() == BuildingCategory.Economic)
                            filtered.Add(b);
                        break;
                    case FilterMode.Military:
                        if (b.buildingType.Category() == BuildingCategory.Military)
                            filtered.Add(b);
                        break;
                }
            }

            // Sort: operational first, then by type name
            filtered.Sort((a, b) =>
            {
                int stateCompare = a.IsOperational == b.IsOperational ? 0 :
                    a.IsOperational ? -1 : 1;
                if (stateCompare != 0) return stateCompare;
                return string.Compare(a.buildingType.DisplayName(), b.buildingType.DisplayName(),
                    StringComparison.Ordinal);
            });

            // Update count label
            countLabel.text = $"{filtered.Count} building{(filtered.Count != 1 ? "s" : "")}";
            UpdateFilterButtonColors();

            if (filtered.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No buildings found.",
                    UIHelper.DefaultBodyFontSize, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 40;
                return;
            }

            foreach (var building in filtered)
            {
                BuildBuildingRow(building, gameState);
                UIHelper.CreateDivider(contentRT);
            }
        }

        // ================================================================
        // Building Row
        // ================================================================

        private void BuildBuildingRow(BuildingData building, GameState gameState)
        {
            // Card background
            var card = UIHelper.CreatePanel(contentRT, "BuildingCard", SporefrontColors.ParchmentMid);
            var cardRT = card.GetComponent<RectTransform>();
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 74;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Name, category icon, level, location
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 22f, 4f);

            string catIcon = building.buildingType.Category() == BuildingCategory.Military ? "[M]" : "[E]";
            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{catIcon} {building.buildingType.DisplayName()} Lv.{building.level}", 13,
                UIHelper.HeaderTextColor);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 22;

            var locLabel = UIHelper.CreateLabel(topRow.transform,
                $"({building.coordinate.q},{building.coordinate.r})", 11,
                SporefrontColors.InkLight, TextAnchor.MiddleRight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 60;
            locLE.preferredHeight = 22;

            // Row 2: HP bar + state info
            var midRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);

            if (building.maxHealth > 0)
            {
                var hpText = UIHelper.CreateLabel(midRow.transform,
                    $"HP: {(int)building.health}/{(int)building.maxHealth}", 10,
                    SporefrontColors.InkLight);
                var hpTextLE = hpText.gameObject.AddComponent<LayoutElement>();
                hpTextLE.preferredWidth = 80;
                hpTextLE.preferredHeight = 16;

                float hpPct = Mathf.Clamp01((float)(building.health / building.maxHealth));
                Color hpColor = hpPct > 0.5f ? SporefrontColors.SporeGreen :
                    hpPct > 0.25f ? SporefrontColors.SporeAmber : SporefrontColors.SporeRed;

                var (bg, fill) = UIHelper.CreateProgressBar(midRow.transform, 12f,
                    SporefrontColors.InkFaded, hpColor);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(hpPct, 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.flexibleWidth = 1;
                barLE.preferredHeight = 12;
            }

            // Row 3: Construction/upgrade progress or status
            if (building.state == BuildingState.Constructing)
            {
                var progressRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);
                var stateLabel = UIHelper.CreateLabel(progressRow.transform, "Constructing...", 10,
                    SporefrontColors.SporeAmber);
                var stateLabelLE = stateLabel.gameObject.AddComponent<LayoutElement>();
                stateLabelLE.preferredWidth = 90;
                stateLabelLE.preferredHeight = 16;

                var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)building.constructionProgress), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.flexibleWidth = 1;
                barLE.preferredHeight = 12;

                cardLE.preferredHeight = 92;
            }
            else if (building.state == BuildingState.Upgrading)
            {
                var progressRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);
                var stateLabel = UIHelper.CreateLabel(progressRow.transform,
                    $"Upgrading to Lv.{building.level + 1}...", 10, SporefrontColors.SporeAmber);
                var stateLabelLE = stateLabel.gameObject.AddComponent<LayoutElement>();
                stateLabelLE.preferredWidth = 120;
                stateLabelLE.preferredHeight = 16;

                var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)building.upgradeProgress), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.flexibleWidth = 1;
                barLE.preferredHeight = 12;

                cardLE.preferredHeight = 92;
            }
            else if (building.state == BuildingState.Damaged)
            {
                var statusLabel = UIHelper.CreateLabel(card.transform, "Damaged", 10,
                    SporefrontColors.SporeRed);
                var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
                statusLE.preferredHeight = 16;
                cardLE.preferredHeight = 92;
            }

            // Make entire card tappable
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.ColorTint;
            var colors = cardBtn.colors;
            colors.normalColor = SporefrontColors.ParchmentMid;
            colors.highlightedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.white, 0.1f);
            colors.pressedColor = Color.Lerp(SporefrontColors.ParchmentMid, Color.black, 0.1f);
            cardBtn.colors = colors;

            var capturedID = building.id;
            cardBtn.onClick.AddListener(() => OnBuildingSelected?.Invoke(capturedID));
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateFilterButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 70;
            le.preferredHeight = 28;
            return btn;
        }

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
