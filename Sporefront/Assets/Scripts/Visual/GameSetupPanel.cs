// ============================================================================
// FILE: Visual/GameSetupPanel.cs
// PURPOSE: Two-screen game setup: Mode Select → Details (1v1 or Arena).
//          Mode select shows two large cards; 1v1 details uses compact dropdown
//          map selection in a narrow centered column.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    // ================================================================
    // Configuration Structs
    // ================================================================

    [Serializable]
    public struct GameSetupConfig
    {
        public MapType mapType;
        public MapSize mapSize;
        public ResourceDensity resourceDensity;
        public StartingResources startingResources;
        public VisibilityMode visibilityMode;

        public static GameSetupConfig Default => new GameSetupConfig
        {
            mapType = MapType.Arabia,
            mapSize = MapSize.Medium,
            resourceDensity = ResourceDensity.Normal,
            startingResources = StartingResources.Medium,
            visibilityMode = VisibilityMode.Normal
        };
    }

    public enum MapType { Arabia, MountainValley, Random, Arena }
    public enum MapSize { Small, Medium, Large, Huge }
    public enum ResourceDensity { Sparse, Normal, Abundant }
    public enum StartingResources { Small, Medium, Large }

    [Serializable]
    public struct ArenaConfig
    {
        public ArenaScenarioConfig scenarioConfig;
        public ArenaArmyConfiguration armyConfig;
        public ArenaPreset selectedPreset;

        public static ArenaConfig Default => new ArenaConfig
        {
            scenarioConfig = ArenaScenarioConfig.Default,
            armyConfig = ArenaArmyConfiguration.Default,
            selectedPreset = ArenaPreset.Plains
        };
    }

    // ================================================================
    // Panel
    // ================================================================

    public partial class GameSetupPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<GameSetupConfig> OnStartGame;
        public event Action<ArenaConfig> OnPlayArena;
        public event Action<ArenaConfig, int> OnAutoSim;
        public event Action OnBack;

        // ================================================================
        // Screen State
        // ================================================================

        private enum SetupScreen { ModeSelect, Details }
        private SetupScreen currentScreen = SetupScreen.ModeSelect;
        private bool isArenaMode = false;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;

        // Two-screen containers
        private GameObject modeSelectContainer;
        private GameObject detailsContainer;
        private GameObject oneVsOneSection;
        private Text headerTitle;

        // Map dropdown
        private Dropdown mapDropdown;
        private Text mapDescriptionLabel;

        // Current selections
        private MapType selectedMapType = MapType.Arabia;
        private MapSize selectedMapSize = MapSize.Medium;
        private ResourceDensity selectedDensity = ResourceDensity.Normal;
        private StartingResources selectedStartingResources = StartingResources.Medium;
        private VisibilityMode selectedVisibility = VisibilityMode.Normal;

        // Arena config
        private ArenaScenarioConfig arenaScenario = ArenaScenarioConfig.Default;
        private ArenaArmyConfiguration arenaArmy = ArenaArmyConfiguration.Default;
        private ArenaPreset selectedPreset = ArenaPreset.Plains;
        private int simRunCount = 10;

        // UI references for arena section toggle
        private GameObject arenaSection;
        private GameObject standardStartButton;
        private MapType selectedOneVsOneMap = MapType.Arabia;

        // Segmented button tracking
        private Dictionary<string, List<Button>> segmentGroups = new Dictionary<string, List<Button>>();
        private Dictionary<string, int> segmentSelections = new Dictionary<string, int>();

        // Arena unit slider labels
        private Dictionary<MilitaryUnitType, Text> playerUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Dictionary<MilitaryUnitType, Text> enemyUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Text simRunLabel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "GameSetupPanel", UIHelper.PanelBg, cornerRadius: 0);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Header bar
            BuildHeader();

            // Mode select container (direct child of panel, below header)
            BuildModeSelectScreen();

            // Details container with scroll view (direct child of panel, below header)
            BuildDetailsContainer();

            // Show mode select by default
            TransitionToModeSelect();

            panel.SetActive(false);
        }

        // ================================================================
        // Header
        // ================================================================

        private void BuildHeader()
        {
            var header = UIHelper.CreatePanel(panel.transform, "Header", SporefrontColors.BgSection, cornerRadius: 0);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 50);

            var headerRow = UIHelper.CreateHorizontalRow(header.transform, 50f, 8f);
            var headerRowRT = headerRow.GetComponent<RectTransform>();
            UIHelper.StretchFull(headerRowRT);
            headerRow.padding = new RectOffset(12, 12, 0, 0);

            var backBtn = UIHelper.CreateButton(headerRow.transform, "Back",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14,
                () => HandleBack());
            var backBtnLE = backBtn.gameObject.AddComponent<LayoutElement>();
            backBtnLE.preferredWidth = 70;

            headerTitle = UIHelper.CreateLabel(headerRow.transform, "New Game",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleLE = headerTitle.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            // Invisible balance spacer
            var spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(headerRow.transform, false);
            spacerGO.GetComponent<LayoutElement>().preferredWidth = 70;
        }

        // ================================================================
        // Mode Select Screen
        // ================================================================

        private void BuildModeSelectScreen()
        {
            modeSelectContainer = UIHelper.CreatePanel(panel.transform, "ModeSelectContainer", Color.clear);
            var containerRT = modeSelectContainer.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.15f, 0);
            containerRT.anchorMax = new Vector2(0.85f, 1);
            containerRT.offsetMin = new Vector2(0, 0);
            containerRT.offsetMax = new Vector2(0, -50); // Below header

            // VLG for vertical centering
            var vlg = modeSelectContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top flex spacer
            var topSpacer = new GameObject("TopSpacer", typeof(RectTransform), typeof(LayoutElement));
            topSpacer.transform.SetParent(modeSelectContainer.transform, false);
            topSpacer.GetComponent<LayoutElement>().flexibleHeight = 1;

            // Title
            var title = UIHelper.CreateLabel(modeSelectContainer.transform, "Choose Game Mode",
                UIConstants.FontTitle, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 40;

            // Spacer between title and cards
            var midSpacer = new GameObject("MidSpacer", typeof(RectTransform), typeof(LayoutElement));
            midSpacer.transform.SetParent(modeSelectContainer.transform, false);
            midSpacer.GetComponent<LayoutElement>().preferredHeight = 24;

            // Cards row
            var cardsRow = UIHelper.CreateHorizontalRow(modeSelectContainer.transform, 280f, 30f);
            cardsRow.childAlignment = TextAnchor.MiddleCenter;
            var cardsRowLE = cardsRow.gameObject.AddComponent<LayoutElement>();
            cardsRowLE.preferredHeight = 280;

            // 1v1 Card
            BuildModeCard(cardsRow.transform, "1v1 Battle",
                "Classic match against an AI opponent", false);

            // Arena Card
            BuildModeCard(cardsRow.transform, "Arena",
                "Configure custom combat scenarios", true);

            // Bottom flex spacer
            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(modeSelectContainer.transform, false);
            bottomSpacer.GetComponent<LayoutElement>().flexibleHeight = 1;
        }

        private void BuildModeCard(Transform parent, string title, string subtitle, bool arena)
        {
            var card = UIHelper.CreatePanel(parent, $"ModeCard_{title}", SporefrontColors.BgElevated);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.flexibleWidth = 1;
            cardLE.preferredHeight = 280;

            var cardVLG = card.AddComponent<VerticalLayoutGroup>();
            cardVLG.spacing = 14f;
            cardVLG.childAlignment = TextAnchor.MiddleCenter;
            cardVLG.childForceExpandWidth = true;
            cardVLG.childForceExpandHeight = false;
            cardVLG.childControlWidth = true;
            cardVLG.childControlHeight = false;
            cardVLG.padding = new RectOffset(28, 28, 36, 36);

            var titleLabel = UIHelper.CreateLabel(card.transform, title,
                38, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            titleLabel.fontStyle = FontStyle.Bold;
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 50;

            var subtitleLabel = UIHelper.CreateLabel(card.transform, subtitle,
                UIConstants.FontBody, SporefrontColors.ParchmentShadow,
                TextAnchor.MiddleCenter, false);
            subtitleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var subtitleLE = subtitleLabel.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 52;

            // Make card clickable
            var btn = card.AddComponent<Button>();
            btn.colors = UIHelper.CardButtonColors(SporefrontColors.BgElevated);

            btn.onClick.AddListener(() =>
            {
                isArenaMode = arena;
                if (arena)
                    selectedMapType = MapType.Arena;
                else
                    selectedMapType = selectedOneVsOneMap;
                TransitionToDetails();
            });
        }

        // ================================================================
        // Details Container
        // ================================================================

        private void BuildDetailsContainer()
        {
            detailsContainer = UIHelper.CreatePanel(panel.transform, "DetailsContainer", Color.clear);
            var containerRT = detailsContainer.GetComponent<RectTransform>();
            containerRT.anchorMin = Vector2.zero;
            containerRT.anchorMax = Vector2.one;
            containerRT.offsetMin = new Vector2(0, 0);
            containerRT.offsetMax = new Vector2(0, -50); // Below header

            // Scroll view inside details container
            var scroll = UIHelper.CreateScrollView(detailsContainer.transform, "DetailsScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);

            Rebuild();
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(bool returnToDetails = false)
        {
            panel.SetActive(true);
            if (returnToDetails)
                TransitionToDetails();
            else
                TransitionToModeSelect();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Navigation
        // ================================================================

        private void HandleBack()
        {
            if (currentScreen == SetupScreen.Details)
                TransitionToModeSelect();
            else
                OnBack?.Invoke();
        }

        private void TransitionToModeSelect()
        {
            currentScreen = SetupScreen.ModeSelect;
            modeSelectContainer.SetActive(true);
            detailsContainer.SetActive(false);
            headerTitle.text = "New Game";
        }

        private void TransitionToDetails()
        {
            currentScreen = SetupScreen.Details;
            modeSelectContainer.SetActive(false);
            detailsContainer.SetActive(true);
            headerTitle.text = "Game Setup";
            UpdateArenaSectionVisibility();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        // ================================================================
        // Rebuild Content (Details Screen)
        // ================================================================

        private void Rebuild()
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(contentRT.GetChild(i).gameObject);

            segmentGroups.Clear();
            segmentSelections.Clear();
            playerUnitLabels.Clear();
            enemyUnitLabels.Clear();

            // 1v1 section (narrow centered wrapper)
            BuildOneVsOneSection();

            // Arena config section
            BuildArenaSection();

            UpdateArenaSectionVisibility();
        }

        // ================================================================
        // 1v1 Section (narrow centered wrapper)
        // ================================================================

        private void BuildOneVsOneSection()
        {
            // Outer wrapper: HLG with flex spacers for horizontal centering
            oneVsOneSection = new GameObject("OneVsOneSection", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            oneVsOneSection.transform.SetParent(contentRT, false);

            var hlg = oneVsOneSection.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 0;
            hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var csf = oneVsOneSection.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Left flex spacer
            var leftSpacer = new GameObject("LeftSpacer", typeof(RectTransform), typeof(LayoutElement));
            leftSpacer.transform.SetParent(oneVsOneSection.transform, false);
            leftSpacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Inner column (500px wide)
            var innerColumn = new GameObject("InnerColumn", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            innerColumn.transform.SetParent(oneVsOneSection.transform, false);

            var innerLE = innerColumn.GetComponent<LayoutElement>();
            innerLE.flexibleWidth = 3;

            var innerVLG = innerColumn.GetComponent<VerticalLayoutGroup>();
            innerVLG.spacing = 4f;
            innerVLG.childForceExpandWidth = true;
            innerVLG.childForceExpandHeight = false;
            innerVLG.childControlWidth = true;
            innerVLG.childControlHeight = false;
            innerVLG.padding = new RectOffset(0, 0, 8, 8);

            var innerCSF = innerColumn.GetComponent<ContentSizeFitter>();
            innerCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var innerParent = innerColumn.transform;

            // Map dropdown (full width)
            BuildMapDropdownSection(innerParent);
            UIHelper.CreateDivider(innerParent);

            // Two-column settings grid
            var settingsRow = new GameObject("SettingsGrid", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            settingsRow.transform.SetParent(innerParent, false);

            var settingsHLG = settingsRow.GetComponent<HorizontalLayoutGroup>();
            settingsHLG.spacing = 24f;
            settingsHLG.childAlignment = TextAnchor.UpperCenter;
            settingsHLG.childForceExpandWidth = true;
            settingsHLG.childForceExpandHeight = true;
            settingsHLG.childControlWidth = true;
            settingsHLG.childControlHeight = true;

            var settingsCSF = settingsRow.GetComponent<ContentSizeFitter>();
            settingsCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Left column: Map Size, Starting Resources
            var leftCol = new GameObject("LeftColumn", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            leftCol.transform.SetParent(settingsRow.transform, false);

            leftCol.GetComponent<LayoutElement>().flexibleWidth = 1;

            var leftColVLG = leftCol.GetComponent<VerticalLayoutGroup>();
            leftColVLG.spacing = 4f;
            leftColVLG.childForceExpandWidth = true;
            leftColVLG.childForceExpandHeight = false;
            leftColVLG.childControlWidth = true;
            leftColVLG.childControlHeight = false;

            BuildMapSizeSection(leftCol.transform);
            UIHelper.CreateDivider(leftCol.transform);
            BuildStartingResourcesSection(leftCol.transform);

            // Right column: Resource Density, Visibility
            var rightCol = new GameObject("RightColumn", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rightCol.transform.SetParent(settingsRow.transform, false);

            rightCol.GetComponent<LayoutElement>().flexibleWidth = 1;

            var rightColVLG = rightCol.GetComponent<VerticalLayoutGroup>();
            rightColVLG.spacing = 4f;
            rightColVLG.childForceExpandWidth = true;
            rightColVLG.childForceExpandHeight = false;
            rightColVLG.childControlWidth = true;
            rightColVLG.childControlHeight = false;

            BuildResourceDensitySection(rightCol.transform);
            UIHelper.CreateDivider(rightCol.transform);
            BuildVisibilitySection(rightCol.transform);

            UIHelper.CreateDivider(innerParent);

            // Start Game button
            standardStartButton = BuildStartGameButton(innerParent);

            // Right flex spacer
            var rightSpacer = new GameObject("RightSpacer", typeof(RectTransform), typeof(LayoutElement));
            rightSpacer.transform.SetParent(oneVsOneSection.transform, false);
            rightSpacer.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        // ================================================================
        // Map Dropdown
        // ================================================================

        private void BuildMapDropdownSection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Select Map",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            // Dropdown container
            var dropdownGO = new GameObject("MapDropdown", typeof(RectTransform),
                typeof(Image), typeof(Dropdown));
            dropdownGO.transform.SetParent(parent, false);

            var dropdownRT = dropdownGO.GetComponent<RectTransform>();
            dropdownRT.sizeDelta = new Vector2(0, 36);

            var dropdownImg = dropdownGO.GetComponent<Image>();
            dropdownImg.color = SporefrontColors.BgSurface;
            dropdownImg.sprite = UIHelper.GetRoundedRectSprite(UIHelper.ButtonCornerRadius);
            dropdownImg.type = Image.Type.Sliced;

            var dropdownLE = dropdownGO.AddComponent<LayoutElement>();
            dropdownLE.preferredHeight = 36;

            mapDropdown = dropdownGO.GetComponent<Dropdown>();

            // Caption text (selected item display)
            var captionGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            captionGO.transform.SetParent(dropdownGO.transform, false);
            var captionRT = captionGO.GetComponent<RectTransform>();
            captionRT.anchorMin = Vector2.zero;
            captionRT.anchorMax = Vector2.one;
            captionRT.offsetMin = new Vector2(12, 2);
            captionRT.offsetMax = new Vector2(-30, -2);
            var captionText = captionGO.GetComponent<Text>();
            captionText.font = UIHelper.BodyFont;
            captionText.fontSize = UIConstants.FontBody;
            captionText.color = UIHelper.BodyTextColor;
            captionText.alignment = TextAnchor.MiddleLeft;
            mapDropdown.captionText = captionText;

            // Arrow indicator
            var arrowGO = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowGO.transform.SetParent(dropdownGO.transform, false);
            var arrowRT = arrowGO.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(1, 0);
            arrowRT.anchorMax = new Vector2(1, 1);
            arrowRT.pivot = new Vector2(1, 0.5f);
            arrowRT.sizeDelta = new Vector2(28, 0);
            arrowRT.anchoredPosition = new Vector2(-4, 0);
            var arrowText = arrowGO.GetComponent<Text>();
            arrowText.font = UIHelper.BodyFont;
            arrowText.fontSize = UIConstants.FontCaption;
            arrowText.color = SporefrontColors.ParchmentShadow;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.text = "v";
            arrowText.raycastTarget = false;

            // Template (dropdown list)
            var templateGO = new GameObject("Template", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect));
            templateGO.transform.SetParent(dropdownGO.transform, false);
            var templateRT = templateGO.GetComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1f);
            templateRT.sizeDelta = new Vector2(0, 120);
            var templateImg = templateGO.GetComponent<Image>();
            templateImg.color = SporefrontColors.BgCard;
            templateImg.sprite = UIHelper.GetRoundedRectSprite(UIHelper.SmallCornerRadius);
            templateImg.type = Image.Type.Sliced;

            // Viewport inside template
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(templateGO.transform, false);
            var vpRT = viewportGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(vpRT);

            // Content inside viewport
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentItemRT = contentGO.GetComponent<RectTransform>();
            contentItemRT.anchorMin = new Vector2(0, 1);
            contentItemRT.anchorMax = new Vector2(1, 1);
            contentItemRT.pivot = new Vector2(0.5f, 1f);
            contentItemRT.sizeDelta = new Vector2(0, 0);

            // Wire up scroll rect
            var scrollRect = templateGO.GetComponent<ScrollRect>();
            scrollRect.viewport = vpRT;
            scrollRect.content = contentItemRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Item template
            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGO.transform.SetParent(contentItemRT.transform, false);
            var itemRT = itemGO.GetComponent<RectTransform>();
            itemRT.sizeDelta = new Vector2(0, 36);
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);

            // Item background
            var itemBgGO = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            var itemBgRT = itemBgGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(itemBgRT);
            var itemBgImg = itemBgGO.GetComponent<Image>();
            itemBgImg.color = new Color(1, 1, 1, 0);

            // Item checkmark (hidden - we don't need it but Toggle requires a graphic)
            var checkGO = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            checkGO.transform.SetParent(itemGO.transform, false);
            var checkRT = checkGO.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0, 0.5f);
            checkRT.anchorMax = new Vector2(0, 0.5f);
            checkRT.sizeDelta = new Vector2(0, 0);
            checkGO.GetComponent<Image>().color = Color.clear;

            // Item label
            var itemLabelGO = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabelRT = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(12, 2);
            itemLabelRT.offsetMax = new Vector2(-12, -2);
            var itemLabelText = itemLabelGO.GetComponent<Text>();
            itemLabelText.font = UIHelper.BodyFont;
            itemLabelText.fontSize = UIConstants.FontBody;
            itemLabelText.color = UIHelper.BodyTextColor;
            itemLabelText.alignment = TextAnchor.MiddleLeft;

            // Wire toggle
            var toggle = itemGO.GetComponent<Toggle>();
            toggle.targetGraphic = itemBgImg;
            toggle.graphic = checkGO.GetComponent<Image>();
            toggle.isOn = false;

            // Wire dropdown
            mapDropdown.template = templateRT;
            mapDropdown.itemText = itemLabelText;
            templateGO.SetActive(false);

            // Populate options
            mapDropdown.ClearOptions();
            mapDropdown.AddOptions(new List<string> { "Arabia", "Mountain Valley", "Random" });

            // Set initial selection
            int initialIndex = 0;
            if (selectedOneVsOneMap == MapType.MountainValley) initialIndex = 1;
            else if (selectedOneVsOneMap == MapType.Random) initialIndex = 2;
            mapDropdown.value = initialIndex;

            // Description label below dropdown
            mapDescriptionLabel = UIHelper.CreateLabel(parent, GetMapDescription(selectedOneVsOneMap),
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                TextAnchor.UpperLeft, false);
            mapDescriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = mapDescriptionLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 48;

            // Listen for changes
            mapDropdown.onValueChanged.AddListener((index) =>
            {
                switch (index)
                {
                    case 0: selectedOneVsOneMap = MapType.Arabia; break;
                    case 1: selectedOneVsOneMap = MapType.MountainValley; break;
                    case 2: selectedOneVsOneMap = MapType.Random; break;
                }
                selectedMapType = selectedOneVsOneMap;
                mapDescriptionLabel.text = GetMapDescription(selectedOneVsOneMap);
            });
        }

        private string GetMapDescription(MapType mapType)
        {
            switch (mapType)
            {
                case MapType.Arabia:
                    return "Open terrain with scattered hills and forests. Balanced starting positions with resources spread evenly across the map.";
                case MapType.MountainValley:
                    return "Two ridges separated by a resource-rich valley. Sparse hilltop starts force players downhill to contest forests, minerals, and game.";
                case MapType.Random:
                    return "Randomly selects a map type for a unique experience every game.";
                default:
                    return "";
            }
        }

        // ================================================================
        // Map Size
        // ================================================================

        private void BuildMapSizeSection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Map Size",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "Controls map dimensions and distance between starting positions.",
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Small", "Medium", "Large", "Huge" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedMapSize = (MapSize)idx;
                    UpdateSegmentSelection("mapSize", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["mapSize"] = buttons;
            segmentSelections["mapSize"] = (int)selectedMapSize;
            UpdateSegmentColors("mapSize");
        }

        // ================================================================
        // Resource Density
        // ================================================================

        private void BuildResourceDensitySection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Resource Density",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "How many resource nodes are placed across the map.",
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Sparse", "Normal", "Abundant" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedDensity = (ResourceDensity)idx;
                    UpdateSegmentSelection("density", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["density"] = buttons;
            segmentSelections["density"] = (int)selectedDensity;
            UpdateSegmentColors("density");
        }

        // ================================================================
        // Starting Resources
        // ================================================================

        private void BuildStartingResourcesSection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Starting Resources",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "The amount of food, wood, and stone each player begins with.",
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Small", "Medium", "Large" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedStartingResources = (StartingResources)idx;
                    UpdateSegmentSelection("startingResources", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["startingResources"] = buttons;
            segmentSelections["startingResources"] = (int)selectedStartingResources;
            UpdateSegmentColors("startingResources");
        }

        // ================================================================
        // Visibility Mode
        // ================================================================

        private void BuildVisibilitySection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Visibility",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "Normal uses fog of war. Full reveals the entire map.",
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Normal", "Full" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedVisibility = (VisibilityMode)idx;
                    UpdateSegmentSelection("visibility", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["visibility"] = buttons;
            segmentSelections["visibility"] = (int)selectedVisibility;
            UpdateSegmentColors("visibility");
        }

        // ================================================================
        // Start Game Button (for non-Arena modes)
        // ================================================================

        private GameObject BuildStartGameButton(Transform parent)
        {
            var spacer = new GameObject("StartSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 10;

            var container = UIHelper.CreatePanel(parent, "StartContainer", Color.clear);
            var containerLE = container.AddComponent<LayoutElement>();
            containerLE.preferredHeight = 50;

            var btn = UIHelper.CreateButton(container.transform, "Start Game",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 16, () =>
                {
                    var config = new GameSetupConfig
                    {
                        mapType = selectedMapType,
                        mapSize = selectedMapSize,
                        resourceDensity = selectedDensity,
                        startingResources = selectedStartingResources,
                        visibilityMode = selectedVisibility
                    };
                    OnStartGame?.Invoke(config);
                });
            var btnRT = btn.GetComponent<RectTransform>();
            UIHelper.StretchFull(btnRT);
            btnRT.offsetMin = new Vector2(20, 4);
            btnRT.offsetMax = new Vector2(-20, -4);

            return container;
        }

        // Arena section methods are in GameSetupArenaSection.cs (partial class)

        // ================================================================
        // Segmented Button Helpers
        // ================================================================

        private void UpdateSegmentSelection(string group, int index)
        {
            segmentSelections[group] = index;
            UpdateSegmentColors(group);
        }

        private void UpdateSegmentColors(string group)
        {
            if (!segmentGroups.ContainsKey(group)) return;
            int selected = segmentSelections.ContainsKey(group) ? segmentSelections[group] : 0;
            var buttons = segmentGroups[group];

            for (int i = 0; i < buttons.Count; i++)
            {
                bool isSel = i == selected;
                var img = buttons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = isSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg;
                }

                var label = buttons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = isSel ? UIHelper.HudTextColor : UIHelper.ButtonText;
                }

                Color bg = isSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg;
                buttons[i].colors = UIHelper.CardButtonColors(bg);
            }
        }

        // ================================================================
        // Config Helpers
        // ================================================================

        private ArenaConfig GetCurrentArenaConfig()
        {
            return new ArenaConfig
            {
                scenarioConfig = arenaScenario,
                armyConfig = arenaArmy,
                selectedPreset = selectedPreset
            };
        }
    }
}
