// ============================================================================
// FILE: Visual/EntitiesOverviewPanel.cs
// PURPOSE: Modal overview of all player entities (villagers + armies) with
//          filter tabs and capacity display. Parchment/ink ledger style.
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
    public class EntitiesOverviewPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, bool> OnEntitySelected; // (entityID, isArmy)
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

        // Filter
        private enum FilterMode { All, Villagers, Armies }
        private FilterMode currentFilter = FilterMode.All;
        private Button allFilterBtn;
        private Button villagersFilterBtn;
        private Button armiesFilterBtn;
        private Text capacityLabel;

        // Cached game state for filter changes
        private GameState cachedGameState;

        // Throttled rebuild
        private bool isDirty;
        private float lastRebuildTime;
        private const float RebuildInterval = 0.5f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "EntitiesOverviewBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel -- parchment background
            panel = UIHelper.CreatePanel(backdrop.transform, "EntitiesOverviewPanel",
                UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalMediumW, UIConstants.ModalMediumH);
            PopupTendrilDecorator.Attach(rt);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Entities",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(8, -32);
            headerRT.offsetMax = new Vector2(-8, 0);

            // Capacity label
            capacityLabel = UIHelper.CreateLabel(panel.transform, "Pop: -/-",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var capRT = capacityLabel.GetComponent<RectTransform>();
            capRT.anchorMin = new Vector2(0, 1);
            capRT.anchorMax = new Vector2(1, 1);
            capRT.pivot = new Vector2(0.5f, 1f);
            capRT.offsetMin = new Vector2(8, -50);
            capRT.offsetMax = new Vector2(-8, -32);

            // Filter tabs row
            var filterRow = UIHelper.CreateHorizontalRow(panel.transform, 30f, 4f);
            var filterRT = filterRow.GetComponent<RectTransform>();
            filterRT.anchorMin = new Vector2(0, 1);
            filterRT.anchorMax = new Vector2(1, 1);
            filterRT.pivot = new Vector2(0.5f, 1f);
            filterRT.offsetMin = new Vector2(8, -82);
            filterRT.offsetMax = new Vector2(-8, -52);

            allFilterBtn = CreateFilterButton(filterRow.transform, "All", () => SetFilter(FilterMode.All));
            villagersFilterBtn = CreateFilterButton(filterRow.transform, "Villagers", () => SetFilter(FilterMode.Villagers));
            armiesFilterBtn = CreateFilterButton(filterRow.transform, "Armies", () => SetFilter(FilterMode.Armies));

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "EntitiesScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 40);
            scrollRT.offsetMax = new Vector2(0, -86);

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

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
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

        public void Hide()
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

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        private void Update()
        {
            if (!isDirty || !IsVisible || cachedGameState == null) return;
            if (Time.unscaledTime - lastRebuildTime < RebuildInterval) return;
            isDirty = false;
            lastRebuildTime = Time.unscaledTime;
            Rebuild(cachedGameState);
        }

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
            SetFilterActive(villagersFilterBtn, currentFilter == FilterMode.Villagers);
            SetFilterActive(armiesFilterBtn, currentFilter == FilterMode.Armies);
        }

        private void SetFilterActive(Button btn, bool active)
        {
            var img = btn.GetComponent<Image>();
            img.color = active ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDeep;
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
                label.color = active ? SporefrontColors.ParchmentLight : UIHelper.InkBodyText;
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

            // Update capacity
            int currentPop, maxPop;
            gameState.GetPopulationStats(localPlayerID, out currentPop, out maxPop);
            capacityLabel.text = $"Population: {currentPop} / {maxPop}";

            UpdateFilterButtonColors();

            bool showVillagers = currentFilter == FilterMode.All || currentFilter == FilterMode.Villagers;
            bool showArmies = currentFilter == FilterMode.All || currentFilter == FilterMode.Armies;

            int entityCount = 0;

            // Villager groups
            if (showVillagers)
            {
                var villagerGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                if (villagerGroups.Count > 0)
                {
                    // Section header
                    var sectionLabel = UIHelper.CreateLabel(contentRT,
                        $"Villager Groups ({villagerGroups.Count})",
                        UIConstants.FontSubheader, UIHelper.InkHeaderText,
                        TextAnchor.MiddleLeft, true);
                    var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
                    sectionLE.preferredHeight = 26;

                    foreach (var vg in villagerGroups)
                    {
                        BuildVillagerRow(vg);
                        entityCount++;
                    }

                    UIHelper.CreateDivider(contentRT, UIHelper.InkDividerColor);
                }
            }

            // Armies
            if (showArmies)
            {
                var armies = gameState.GetArmiesForPlayer(localPlayerID);
                if (armies.Count > 0)
                {
                    // Section header
                    var sectionLabel = UIHelper.CreateLabel(contentRT,
                        $"Armies ({armies.Count})",
                        UIConstants.FontSubheader, UIHelper.InkHeaderText,
                        TextAnchor.MiddleLeft, true);
                    var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
                    sectionLE.preferredHeight = 26;

                    foreach (var army in armies)
                    {
                        BuildArmyRow(army, gameState);
                        entityCount++;
                    }
                }
            }

            if (entityCount == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No entities found.",
                    UIHelper.DefaultBodyFontSize, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 40;
            }
        }

        // ================================================================
        // Villager Row
        // ================================================================

        private void BuildVillagerRow(VillagerGroupData group)
        {
            var card = UIHelper.CreatePanel(contentRT, "VillagerCard", SporefrontColors.ParchmentDark);
            var cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(
                SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.35f);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 52;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Name + count + location
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{group.name} x{group.villagerCount}", UIConstants.FontCaption, UIHelper.InkHeaderText);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 20;

            var locLabel = UIHelper.CreateLabel(topRow.transform,
                $"({group.coordinate.q},{group.coordinate.r})", UIConstants.FontCaption,
                UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 60;
            locLE.preferredHeight = 20;

            // Row 2: Current task
            string taskName = group.currentTask != null ? group.currentTask.DisplayName : "Idle";
            Color taskColor = (group.currentTask != null && !group.currentTask.IsIdle)
                ? SporefrontColors.SporeGreen : UIHelper.InkMutedText;
            var taskLabel = UIHelper.CreateLabel(card.transform, $"Task: {taskName}", UIConstants.FontCaption, taskColor);
            var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
            taskLE.preferredHeight = 18;

            // Make tappable
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.ColorTint;
            cardBtn.colors = UIHelper.CardButtonColors(SporefrontColors.ParchmentDark);

            var capturedID = group.id;
            cardBtn.onClick.AddListener(() => OnEntitySelected?.Invoke(capturedID, false));
        }

        // ================================================================
        // Army Row
        // ================================================================

        private void BuildArmyRow(ArmyData army, GameState gameState)
        {
            var card = UIHelper.CreatePanel(contentRT, "ArmyCard", SporefrontColors.ParchmentDark);
            var cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(
                SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.35f);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 72;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Name + status + location
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

            string status = UIHelper.FormatArmyStatus(army);
            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{army.name}{status}", UIConstants.FontCaption, UIHelper.InkHeaderText);
            nameLabel.supportRichText = true;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 20;

            var locLabel = UIHelper.CreateLabel(topRow.transform,
                $"({army.coordinate.q},{army.coordinate.r})", UIConstants.FontCaption,
                UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 60;
            locLE.preferredHeight = 20;

            // Row 2: Unit count + commander info
            var midRow = UIHelper.CreateHorizontalRow(card.transform, 18f, 4f);

            var unitsLabel = UIHelper.CreateLabel(midRow.transform,
                $"{army.GetTotalUnits()} units", UIConstants.FontCaption, UIHelper.InkMutedText);
            var unitsLE = unitsLabel.gameObject.AddComponent<LayoutElement>();
            unitsLE.preferredWidth = 60;
            unitsLE.preferredHeight = 18;

            // Commander info
            string commanderText = "No Commander";
            if (army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                    commanderText = $"Cmdr: {commander.name} Lv.{commander.level}";
            }
            var cmdLabel = UIHelper.CreateLabel(midRow.transform, commanderText, UIConstants.FontCaption,
                UIHelper.InkMutedText);
            var cmdLE = cmdLabel.gameObject.AddComponent<LayoutElement>();
            cmdLE.flexibleWidth = 1;
            cmdLE.preferredHeight = 18;

            // Row 3: Stamina bar (commander stamina)
            var staminaRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);

            string staminaText = "\u2014";
            float staminaPct = 0f;
            if (army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    staminaText = $"Stamina: {(int)commander.stamina}/{(int)CommanderData.MaxStamina}";
                    staminaPct = Mathf.Clamp01((float)(commander.stamina / CommanderData.MaxStamina));
                }
            }

            var staminaLabel = UIHelper.CreateLabel(staminaRow.transform,
                staminaText, UIConstants.FontCaption, UIHelper.InkMutedText);
            var staminaTextLE = staminaLabel.gameObject.AddComponent<LayoutElement>();
            staminaTextLE.preferredWidth = 100;
            staminaTextLE.preferredHeight = 16;

            Color staminaColor = staminaPct > 0.5f ? SporefrontColors.SporeTeal :
                staminaPct > 0.25f ? SporefrontColors.SporeAmber : SporefrontColors.SporeRed;

            var (bg, fill) = UIHelper.CreateInkProgressBar(staminaRow.transform, 12f, staminaColor);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(staminaPct, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            // Make tappable
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.ColorTint;
            cardBtn.colors = UIHelper.CardButtonColors(SporefrontColors.ParchmentDark);

            var capturedID = army.id;
            cardBtn.onClick.AddListener(() => OnEntitySelected?.Invoke(capturedID, true));
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateFilterButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontCaption, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 70;
            le.preferredHeight = 28;
            return btn;
        }
    }
}
