// ============================================================================
// FILE: Visual/GameSetupPanel.cs
// PURPOSE: Two-screen game setup: Mode Select → Details (1v1 or Arena).
//          Mode select shows two large cards; 1v1 details uses compact dropdown
//          map selection in a narrow centered column.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;
using TendrilBranch = Sporefront.Visual.UITendrilRenderer.TendrilBranch;

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
        public FactionType playerFaction;
        public FactionType aiFaction;
        public bool isOnlineMode;

        // Matchmaking fields (empty for offline games)
        public string matchGameID;
        public bool matchIsHost;
        public string matchOpponentUID;
        public string matchLocalPlayerID;
        public string matchOpponentPlayerID;
        public string matchOpponentDisplayName;
        public FactionType matchOpponentFaction;

        public static GameSetupConfig Default => new GameSetupConfig
        {
            mapType = MapType.Arabia,
            mapSize = MapSize.Medium,
            resourceDensity = ResourceDensity.Normal,
            startingResources = StartingResources.Medium,
            visibilityMode = VisibilityMode.Normal,
            playerFaction = FactionType.Morel,
            aiFaction = FactionType.Muscaria
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
        public event Action OnStartMatchmaking;

        // ================================================================
        // Screen State
        // ================================================================

        private enum SetupScreen { ModeSelect, Details }
        private SetupScreen currentScreen = SetupScreen.ModeSelect;
        private bool isArenaMode = false;
        private bool isOnlineMode = false;

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
        private FactionType selectedFaction = FactionType.Morel;
        private FactionType selectedAIFaction = FactionType.Muscaria;

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
        private Dictionary<string, List<Image>> segmentBorders = new Dictionary<string, List<Image>>();
        private Dictionary<string, int> segmentSelections = new Dictionary<string, int>();

        // Arena unit slider labels
        private Dictionary<MilitaryUnitType, Text> playerUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Dictionary<MilitaryUnitType, Text> enemyUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Text simRunLabel;

        // ================================================================
        // Menu Styling
        // ================================================================

        private Texture2D hoverGradientTexture;

        // ================================================================
        // Arrival Tendrils
        // ================================================================

        private UITendrilRenderer arrivalTendrilRenderer;
        private readonly List<TendrilBranch> arrivalBranches = new List<TendrilBranch>();
        private readonly List<int> arrivalBranchDepths = new List<int>();
        private const float ArrivalStagger = 0.08f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "GameSetupPanel", SporefrontColors.ParchmentMid, cornerRadius: 0);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Arrival tendril layer (behind all content, first child)
            var arrivalGO = new GameObject("ArrivalTendrilLayer", typeof(RectTransform), typeof(CanvasRenderer));
            arrivalGO.transform.SetParent(panel.transform, false);
            var arrivalRT = arrivalGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(arrivalRT);
            arrivalGO.transform.SetAsFirstSibling();

            arrivalTendrilRenderer = arrivalGO.AddComponent<UITendrilRenderer>();
            arrivalTendrilRenderer.raycastTarget = false;

            // Parchment overlay — simulates paper fiber partially covering ink
            UIHelper.AddParchmentOverlay(panel.transform, 0.25f);

            // Build shared hover gradient texture (1x64) — matches MainMenuPanel style
            hoverGradientTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            hoverGradientTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int y = 0; y < 64; y++)
            {
                float alpha;
                if (y < 4)
                    alpha = Mathf.Lerp(0f, 0.12f, y / 3f);
                else if (y < 60)
                    alpha = 0.12f;
                else
                    alpha = Mathf.Lerp(0.12f, 0f, (y - 60) / 3f);
                pixels[y] = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g, SporefrontColors.InkDark.b, alpha);
            }
            hoverGradientTexture.SetPixels(pixels);
            hoverGradientTexture.Apply();

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
            // Transparent header bar — just layout, no background panel
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(panel.transform, false);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 70);

            var headerRow = UIHelper.CreateHorizontalRow(header.transform, 70f, 8f);
            var headerRowRT = headerRow.GetComponent<RectTransform>();
            UIHelper.StretchFull(headerRowRT);
            headerRow.padding = new RectOffset(16, 16, 0, 0);

            // Back text button — container with Image for raycast, label inside
            var backBtnGO = new GameObject("BackButton", typeof(RectTransform), typeof(Image));
            backBtnGO.transform.SetParent(headerRow.transform, false);
            var backImg = backBtnGO.GetComponent<Image>();
            backImg.color = Color.clear;
            var backBtn = backBtnGO.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() => HandleBack());
            var backBtnLE = backBtnGO.AddComponent<LayoutElement>();
            backBtnLE.preferredWidth = 80;

            var backLabel = UIHelper.CreateLabel(backBtnGO.transform, "< Back",
                UIConstants.FontHeader, SporefrontColors.InkMid,
                TextAnchor.MiddleLeft);
            var backLabelRT = backLabel.GetComponent<RectTransform>();
            backLabelRT.anchorMin = Vector2.zero;
            backLabelRT.anchorMax = Vector2.one;
            backLabelRT.offsetMin = Vector2.zero;
            backLabelRT.offsetMax = Vector2.zero;

            // Add hover color change
            var backTrigger = backBtnGO.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry
                { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => backLabel.color = SporefrontColors.InkDark);
            backTrigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry
                { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => backLabel.color = SporefrontColors.InkMid);
            backTrigger.triggers.Add(exitEntry);

            headerTitle = UIHelper.CreateLabel(headerRow.transform, "N E W   G A M E",
                48, SporefrontColors.InkDark,
                TextAnchor.MiddleCenter, true);
            headerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleLE = headerTitle.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            // Invisible balance spacer
            var spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(headerRow.transform, false);
            spacerGO.GetComponent<LayoutElement>().preferredWidth = 80;
        }

        // ================================================================
        // Mode Select Screen
        // ================================================================

        private void BuildModeSelectScreen()
        {
            modeSelectContainer = new GameObject("ModeSelectContainer", typeof(RectTransform));
            modeSelectContainer.transform.SetParent(panel.transform, false);
            var containerRT = modeSelectContainer.GetComponent<RectTransform>();
            // Center column, same width as main menu
            containerRT.anchorMin = new Vector2(0.5f, 0f);
            containerRT.anchorMax = new Vector2(0.5f, 1f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 380f;
            containerRT.sizeDelta = new Vector2(columnWidth, 0f);
            containerRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            containerRT.offsetMax = new Vector2(columnWidth / 2f, -70f); // Below header

            var vlg = modeSelectContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            // Top flex spacer
            var topSpacer = new GameObject("TopSpacer", typeof(RectTransform), typeof(LayoutElement));
            topSpacer.transform.SetParent(modeSelectContainer.transform, false);
            topSpacer.GetComponent<LayoutElement>().flexibleHeight = 1;

            // Title
            var title = UIHelper.CreateLabel(modeSelectContainer.transform, "C H O O S E   G A M E   M O D E",
                42, SporefrontColors.InkDark,
                TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 50;

            // Spacer
            var midSpacer = new GameObject("MidSpacer", typeof(RectTransform), typeof(LayoutElement));
            midSpacer.transform.SetParent(modeSelectContainer.transform, false);
            midSpacer.GetComponent<LayoutElement>().preferredHeight = 16;

            // Semi-transparent backdrop behind menu items
            float backdropWidth = columnWidth * 0.5f;
            var backdropGO = UIHelper.CreatePanel(modeSelectContainer.transform, "ModeBackdrop",
                new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g, SporefrontColors.ParchmentDark.b, 0.50f), cornerRadius: 0);
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null) UnityEngine.Object.Destroy(backdropOutline);
            var backdropLE = backdropGO.AddComponent<LayoutElement>();
            backdropLE.preferredWidth = backdropWidth;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = 10f;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = false;
            backdropVLG.padding = new RectOffset(15, 15, 10, 10);

            // 1v1 Battle
            CreateSetupMenuItem(backdropGO.transform, "1v1 Battle", SporefrontColors.InkDark, () =>
            {
                isArenaMode = false;
                isOnlineMode = false;
                selectedMapType = selectedOneVsOneMap;
                TransitionToDetails();
            });

            // Online Game (queue-based matchmaking)
            CreateSetupMenuItem(backdropGO.transform, "Online Game", SporefrontColors.InkDark, () =>
            {
                OnStartMatchmaking?.Invoke();
            });

            // Arena
            CreateSetupMenuItem(backdropGO.transform, "Arena", SporefrontColors.InkDark, () =>
            {
                isArenaMode = true;
                selectedMapType = MapType.Arena;
                TransitionToDetails();
            });

            // Bottom flex spacer
            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(modeSelectContainer.transform, false);
            bottomSpacer.GetComponent<LayoutElement>().flexibleHeight = 1;
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
            containerRT.offsetMax = new Vector2(0, -70); // Below header

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

        public RectTransform PanelRT => panel?.GetComponent<RectTransform>();

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
            headerTitle.text = "N E W   G A M E";
        }

        private void TransitionToDetails()
        {
            currentScreen = SetupScreen.Details;
            modeSelectContainer.SetActive(false);
            detailsContainer.SetActive(true);
            headerTitle.text = "G A M E   S E T U P";
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
            segmentBorders.Clear();
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
            innerLE.flexibleWidth = 0;
            innerLE.preferredWidth = 420f;

            var innerVLG = innerColumn.GetComponent<VerticalLayoutGroup>();
            innerVLG.spacing = 4f;
            innerVLG.childForceExpandWidth = true;
            innerVLG.childForceExpandHeight = false;
            innerVLG.childControlWidth = true;
            innerVLG.childControlHeight = false;
            innerVLG.padding = new RectOffset(0, 0, 8, 8);

            var innerCSF = innerColumn.GetComponent<ContentSizeFitter>();
            innerCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Parchment card behind settings
            var settingsCard = UIHelper.CreatePanel(innerColumn.transform, "SettingsCard",
                new Color(SporefrontColors.ParchmentCream.r, SporefrontColors.ParchmentCream.g,
                          SporefrontColors.ParchmentCream.b, 0.92f), cornerRadius: 6);
            var cardOutline = settingsCard.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(UIHelper.InkMutedText.r,
                    UIHelper.InkMutedText.g, UIHelper.InkMutedText.b, 0.5f);
                cardOutline.effectDistance = new Vector2(1f, -1f);
            }
            // Drop shadow behind parchment card
            var cardShadowGO = new GameObject("SettingsShadow", typeof(RectTransform), typeof(Image));
            cardShadowGO.transform.SetParent(settingsCard.transform, false);
            cardShadowGO.transform.SetAsFirstSibling();
            var cardShadowRT = cardShadowGO.GetComponent<RectTransform>();
            cardShadowRT.anchorMin = Vector2.zero;
            cardShadowRT.anchorMax = Vector2.one;
            cardShadowRT.offsetMin = new Vector2(-4f, -6f);
            cardShadowRT.offsetMax = new Vector2(4f, 2f);
            cardShadowGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.10f);
            cardShadowGO.GetComponent<Image>().raycastTarget = false;
            cardShadowGO.AddComponent<LayoutElement>().ignoreLayout = true;

            var cardVLG = settingsCard.AddComponent<VerticalLayoutGroup>();
            cardVLG.spacing = 4f;
            cardVLG.childForceExpandWidth = true;
            cardVLG.childForceExpandHeight = false;
            cardVLG.childControlWidth = true;
            cardVLG.childControlHeight = true;
            cardVLG.padding = new RectOffset(20, 20, 16, 16);

            var cardCSF = settingsCard.AddComponent<ContentSizeFitter>();
            cardCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // All settings children go inside the parchment card
            var innerParent = settingsCard.transform;

            var softDivider = new Color(SporefrontColors.InkFaded.r, SporefrontColors.InkFaded.g, SporefrontColors.InkFaded.b, 0.3f);

            // Map dropdown (full width)
            BuildMapDropdownSection(innerParent);
            UIHelper.CreateDivider(innerParent, softDivider);

            // Faction selection (full width)
            BuildFactionSection(innerParent);
            UIHelper.CreateDivider(innerParent, softDivider);

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
            UIHelper.CreateDivider(leftCol.transform, softDivider);
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
            UIHelper.CreateDivider(rightCol.transform, softDivider);
            BuildVisibilitySection(rightCol.transform);

            UIHelper.CreateDivider(innerParent, softDivider);

            // Start Game button
            standardStartButton = BuildStartGameButton(innerParent);

            // Bottom spacer for breathing room below Start Game
            var bottomButtonSpacer = new GameObject("BottomButtonSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomButtonSpacer.transform.SetParent(innerParent, false);
            bottomButtonSpacer.GetComponent<LayoutElement>().preferredHeight = 30f;

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
                UIConstants.FontHeader, SporefrontColors.InkDark,
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
            dropdownImg.color = new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g, SporefrontColors.ParchmentDark.b, 0.50f);
            dropdownImg.sprite = UIHelper.GetRoundedRectSprite(UIHelper.ButtonCornerRadius);
            dropdownImg.type = Image.Type.Sliced;

            // Subtle border outline
            var dropdownOutline = dropdownGO.AddComponent<Outline>();
            dropdownOutline.effectColor = new Color(SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g, SporefrontColors.InkBorder.b, 0.4f);
            dropdownOutline.effectDistance = new Vector2(1f, 1f);

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
            captionText.fontSize = UIConstants.FontSubheader;
            captionText.color = SporefrontColors.InkDark;
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
            arrowText.color = SporefrontColors.InkLight;
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
            templateImg.color = new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g, SporefrontColors.ParchmentDark.b, 0.80f);
            var templateOutline = templateGO.AddComponent<Outline>();
            templateOutline.effectColor = new Color(SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g, SporefrontColors.InkBorder.b, 0.4f);
            templateOutline.effectDistance = new Vector2(1f, 1f);
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
            itemLabelText.color = SporefrontColors.InkDark;
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
                UIConstants.FontBody, SporefrontColors.InkLight,
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
        // Faction
        // ================================================================

        private Text factionBonusLabel;
        private Text factionDescriptionLabel;
        private Text aiFactionLabel;

        private void BuildFactionSection(Transform parent)
        {
            // ── Your Faction ─────────────────────────────────────────
            var sectionLabel = UIHelper.CreateLabel(parent, "Your Faction",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var row = UIHelper.CreateHorizontalRow(parent, 36f, UIConstants.SpaceXS);
            var buttons = new List<Button>();

            // Build buttons for each faction (skip None)
            var factions = new FactionType[] { FactionType.Morel, FactionType.Muscaria };
            for (int i = 0; i < factions.Length; i++)
            {
                int idx = i;
                var faction = factions[i];
                var btn = UIHelper.CreateButton(row.transform, faction.DisplayName(),
                    null, null, UIConstants.FontCaption, () =>
                {
                    selectedFaction = factions[idx];
                    UpdateSegmentSelection("faction", idx);
                    UpdateFactionInfo();
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 150;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["faction"] = buttons;
            segmentSelections["faction"] = 0;
            UpdateSegmentColors("faction");

            // Faction info card below the buttons
            var infoCard = UIHelper.CreateLedgerCard(parent, "FactionInfoCard");
            var infoCardLE = infoCard.gameObject.AddComponent<LayoutElement>();
            infoCardLE.minHeight = 100;

            // Bonus header
            var bonusHeader = UIHelper.CreateLabel(infoCard.transform, "Faction Bonuses",
                UIConstants.FontSmall, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            bonusHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            // Bonus list
            factionBonusLabel = UIHelper.CreateLabel(infoCard.transform, "",
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.UpperLeft);
            var bonusLE = factionBonusLabel.gameObject.AddComponent<LayoutElement>();
            bonusLE.preferredHeight = 72;

            // Divider
            UIHelper.CreateDivider(infoCard.transform, SporefrontColors.InkFaded);

            // Description
            factionDescriptionLabel = UIHelper.CreateLabel(infoCard.transform, "",
                UIConstants.FontCaption, UIHelper.InkSubText, TextAnchor.UpperLeft);
            var descLE = factionDescriptionLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 48;

            // ── AI Faction ───────────────────────────────────────────
            var aiLabel = UIHelper.CreateLabel(parent, "AI Faction",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var aiLabelLE = aiLabel.gameObject.AddComponent<LayoutElement>();
            aiLabelLE.preferredHeight = 28;

            var aiRow = UIHelper.CreateHorizontalRow(parent, 36f, UIConstants.SpaceXS);
            var aiButtons = new List<Button>();

            for (int i = 0; i < factions.Length; i++)
            {
                int idx = i;
                var faction = factions[i];
                var btn = UIHelper.CreateButton(aiRow.transform, faction.DisplayName(),
                    null, null, UIConstants.FontCaption, () =>
                {
                    selectedAIFaction = factions[idx];
                    UpdateSegmentSelection("aiFaction", idx);
                    UpdateFactionInfo();
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 150;
                btnLE.preferredHeight = 36;
                aiButtons.Add(btn);
            }

            segmentGroups["aiFaction"] = aiButtons;
            segmentSelections["aiFaction"] = 1; // default Muscaria (index 1)
            UpdateSegmentColors("aiFaction");

            // AI faction summary label
            aiFactionLabel = UIHelper.CreateLabel(parent, "",
                UIConstants.FontCaption, UIHelper.InkSubText, TextAnchor.UpperLeft);
            var aiFactionLE = aiFactionLabel.gameObject.AddComponent<LayoutElement>();
            aiFactionLE.preferredHeight = 20;

            // Populate initial faction info
            UpdateFactionInfo();
        }

        private void UpdateFactionInfo()
        {
            if (factionBonusLabel != null)
                factionBonusLabel.text = FormatBonusBullets(selectedFaction.StartingBonusDescription());
            if (factionDescriptionLabel != null)
                factionDescriptionLabel.text = selectedFaction.Description();
            if (aiFactionLabel != null)
                aiFactionLabel.text = selectedAIFaction.DisplayName() + " — " +
                    selectedAIFaction.StartingBonusDescription();
        }

        private string FormatBonusBullets(string bonusDescription)
        {
            var parts = bonusDescription.Split(',');
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("  \u2022 " + part.Trim());
            }
            return sb.ToString();
        }

        // ================================================================
        // Map Size
        // ================================================================

        private void BuildMapSizeSection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Map Size",
                UIConstants.FontHeader, SporefrontColors.InkDark,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "Controls map dimensions and distance between starting positions.",
                UIConstants.FontBody, SporefrontColors.InkLight,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 40f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Small", "Medium", "Large", "Huge" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, UIConstants.FontSubheader, () =>
                {
                    selectedMapSize = (MapSize)idx;
                    UpdateSegmentSelection("mapSize", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 40;
                buttons.Add(btn);
            }

            segmentGroups["mapSize"] = buttons;
            segmentBorders["mapSize"] = AddSegmentBottomBorders(buttons);
            segmentSelections["mapSize"] = (int)selectedMapSize;
            UpdateSegmentColors("mapSize");
        }

        // ================================================================
        // Resource Density
        // ================================================================

        private void BuildResourceDensitySection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Resource Density",
                UIConstants.FontHeader, SporefrontColors.InkDark,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "How many resource nodes are placed across the map.",
                UIConstants.FontBody, SporefrontColors.InkLight,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 40f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Sparse", "Normal", "Abundant" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, UIConstants.FontSubheader, () =>
                {
                    selectedDensity = (ResourceDensity)idx;
                    UpdateSegmentSelection("density", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 120;
                btnLE.preferredHeight = 40;
                buttons.Add(btn);
            }

            segmentGroups["density"] = buttons;
            segmentBorders["density"] = AddSegmentBottomBorders(buttons);
            segmentSelections["density"] = (int)selectedDensity;
            UpdateSegmentColors("density");
        }

        // ================================================================
        // Starting Resources
        // ================================================================

        private void BuildStartingResourcesSection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Starting Resources",
                UIConstants.FontHeader, SporefrontColors.InkDark,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "The amount of food, wood, and stone each player begins with.",
                UIConstants.FontBody, SporefrontColors.InkLight,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 40f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Small", "Medium", "Large" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, UIConstants.FontSubheader, () =>
                {
                    selectedStartingResources = (StartingResources)idx;
                    UpdateSegmentSelection("startingResources", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 120;
                btnLE.preferredHeight = 40;
                buttons.Add(btn);
            }

            segmentGroups["startingResources"] = buttons;
            segmentBorders["startingResources"] = AddSegmentBottomBorders(buttons);
            segmentSelections["startingResources"] = (int)selectedStartingResources;
            UpdateSegmentColors("startingResources");
        }

        // ================================================================
        // Visibility Mode
        // ================================================================

        private void BuildVisibilitySection(Transform parent)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, "Visibility",
                UIConstants.FontHeader, SporefrontColors.InkDark,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var descLabel = UIHelper.CreateLabel(parent,
                "Normal uses fog of war. Full reveals the entire map.",
                UIConstants.FontBody, SporefrontColors.InkLight,
                TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 32;

            var row = UIHelper.CreateHorizontalRow(parent, 40f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Normal", "Full" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, UIConstants.FontSubheader, () =>
                {
                    selectedVisibility = (VisibilityMode)idx;
                    UpdateSegmentSelection("visibility", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 120;
                btnLE.preferredHeight = 40;
                buttons.Add(btn);
            }

            segmentGroups["visibility"] = buttons;
            segmentBorders["visibility"] = AddSegmentBottomBorders(buttons);
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
            spacer.GetComponent<LayoutElement>().preferredHeight = 20;

            // Use text menu item style matching the main menu
            var container = new GameObject("StartContainer", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var containerLE = container.AddComponent<LayoutElement>();
            containerLE.preferredHeight = 50;
            var containerVLG = container.AddComponent<VerticalLayoutGroup>();
            containerVLG.childForceExpandWidth = true;
            containerVLG.childForceExpandHeight = true;
            containerVLG.childControlWidth = true;
            containerVLG.childControlHeight = true;

            CreateSetupMenuItem(container.transform, "Start Game", SporefrontColors.InkDark, () =>
            {
                var config = new GameSetupConfig
                {
                    mapType = selectedMapType,
                    mapSize = selectedMapSize,
                    resourceDensity = selectedDensity,
                    startingResources = selectedStartingResources,
                    visibilityMode = selectedVisibility,
                    playerFaction = selectedFaction,
                    aiFaction = selectedAIFaction,
                    isOnlineMode = isOnlineMode
                };
                OnStartGame?.Invoke(config);
            });

            return container;
        }

        // Arena section methods are in GameSetupArenaSection.cs (partial class)

        // ================================================================
        // Text Menu Item (matches MainMenuPanel style)
        // ================================================================

        private void CreateSetupMenuItem(Transform parent, string text, Color textColor, Action onClick)
        {
            var container = new GameObject("MenuItem_" + text, typeof(RectTransform), typeof(Image));
            container.transform.SetParent(parent, false);
            var containerImg = container.GetComponent<Image>();
            containerImg.color = Color.clear;

            var containerLE = container.AddComponent<LayoutElement>();
            containerLE.preferredHeight = 46f;

            var containerVLG = container.AddComponent<VerticalLayoutGroup>();
            containerVLG.spacing = 0f;
            containerVLG.childAlignment = TextAnchor.MiddleCenter;
            containerVLG.childForceExpandWidth = true;
            containerVLG.childForceExpandHeight = false;
            containerVLG.childControlWidth = true;
            containerVLG.childControlHeight = false;

            // Hover gradient overlay
            var hoverGO = new GameObject("HoverOverlay", typeof(RectTransform), typeof(RawImage));
            hoverGO.transform.SetParent(container.transform, false);
            var hoverRT = hoverGO.GetComponent<RectTransform>();
            hoverRT.anchorMin = Vector2.zero;
            hoverRT.anchorMax = Vector2.one;
            hoverRT.offsetMin = Vector2.zero;
            hoverRT.offsetMax = Vector2.zero;
            var hoverRaw = hoverGO.GetComponent<RawImage>();
            hoverRaw.texture = hoverGradientTexture;
            hoverRaw.enabled = false;
            hoverRaw.raycastTarget = false;
            var hoverIgnore = hoverGO.AddComponent<LayoutElement>();
            hoverIgnore.ignoreLayout = true;

            // Text label
            var label = UIHelper.CreateLabel(container.transform, text,
                (int)(UIConstants.FontSubheader * 1.7f), textColor, TextAnchor.MiddleCenter);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 41f;

            // Thin underline divider
            UIHelper.CreateDivider(container.transform, new Color(SporefrontColors.InkFaded.r, SporefrontColors.InkFaded.g, SporefrontColors.InkFaded.b, 0.3f), 1f);

            // Button
            var btn = container.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = containerImg;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Hover events
            var trigger = container.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => hoverRaw.enabled = true);
            trigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => hoverRaw.enabled = false);
            trigger.triggers.Add(exitEntry);
        }

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
            var borders = segmentBorders.ContainsKey(group) ? segmentBorders[group] : null;

            for (int i = 0; i < buttons.Count; i++)
            {
                bool isSel = i == selected;
                var img = buttons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = Color.clear;
                    img.sprite = null;
                }

                var label = buttons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    var inkDark = SporefrontColors.InkDark;
                    var inkMid = SporefrontColors.InkMid;
                    label.color = isSel ? inkDark : new Color(inkMid.r, inkMid.g, inkMid.b, 0.5f);
                }

                buttons[i].colors = UIHelper.CardButtonColors(Color.clear);

                // Show border on all buttons; selected gets SporeRed + thicker
                if (borders != null && i < borders.Count)
                {
                    borders[i].enabled = true;
                    borders[i].color = isSel ? SporefrontColors.SporeRed : SporefrontColors.InkFaded;
                    var borderRT = borders[i].GetComponent<RectTransform>();
                    borderRT.sizeDelta = new Vector2(0f, isSel ? 2f : 1f);
                }
            }
        }

        private List<Image> AddSegmentBottomBorders(List<Button> buttons)
        {
            var borders = new List<Image>();
            foreach (var btn in buttons)
            {
                var borderGO = new GameObject("BottomBorder", typeof(RectTransform), typeof(Image));
                borderGO.transform.SetParent(btn.transform, false);
                var borderRT = borderGO.GetComponent<RectTransform>();
                borderRT.anchorMin = new Vector2(0f, 0f);
                borderRT.anchorMax = new Vector2(1f, 0f);
                borderRT.pivot = new Vector2(0.5f, 0f);
                borderRT.sizeDelta = new Vector2(0f, 1f);
                borderRT.anchoredPosition = Vector2.zero;
                var borderImg = borderGO.GetComponent<Image>();
                borderImg.color = SporefrontColors.InkFaded;
                borderImg.raycastTarget = false;
                borders.Add(borderImg);
            }
            return borders;
        }

        // ================================================================
        // Arrival Tendrils
        // ================================================================

        /// <summary>
        /// Grows one main trunk limb continuing the bridge direction, then fans 9 sub-branches
        /// off it, with tertiary branches on every other sub-branch. Color matches the source limb.
        /// </summary>
        public void StartArrivalTendrils(Vector2 rootPoint, Vector2 arrivalDirection, Color tendrilColor)
        {
            ClearArrivalTendrils();

            if (arrivalTendrilRenderer == null) return;

            var panelRT = panel.GetComponent<RectTransform>();
            float pw = panelRT.rect.width  > 0f ? panelRT.rect.width  : 1920f;
            float ph = panelRT.rect.height > 0f ? panelRT.rect.height : 1080f;

            var rng = new System.Random();
            float halfW = pw * 0.5f;
            float halfH = ph * 0.5f;

            Vector2 root = rootPoint;

            // ============================================================
            // TRUNK: from arrival point, snakes down through the panel
            // ============================================================
            int trunkSegments = 8;
            var trunkPts = new List<Vector2>(trunkSegments + 1);
            float topY = root.y;
            float bottomY = -halfH * 0.90f;

            for (int i = 0; i <= trunkSegments; i++)
            {
                float t = (float)i / trunkSegments;
                float y = Mathf.Lerp(topY, bottomY, t);
                // Anchor trunk just left of the options container's inner column
                float trunkBaseX = -pw * 0.32f;
                float snakeX = trunkBaseX + Mathf.Sin(t * Mathf.PI * 2.5f + 0.3f) * pw * 0.015f;
                snakeX += (float)(rng.NextDouble() * 6.0 - 3.0);
                trunkPts.Add(new Vector2(snakeX, y));
            }

            // Red trunk
            var redTrunk = arrivalTendrilRenderer.AddBranch(trunkPts, ArrivalTrunkStrands(0), 8f, 0.10f);
            redTrunk.branchColor = SporefrontColors.InkRed;
            redTrunk.growthProgress = 0f;
            arrivalBranches.Add(redTrunk);
            arrivalBranchDepths.Add(0);

            // Teal trunk (intertwined)
            var tealTrunk = arrivalTendrilRenderer.AddBranch(trunkPts, ArrivalTrunkStrands(1), 8f, 0.10f);
            tealTrunk.branchColor = SporefrontColors.InkGreen;
            tealTrunk.growthProgress = 0f;
            arrivalBranches.Add(tealTrunk);
            arrivalBranchDepths.Add(0);

            // ============================================================
            // LIMBS: 4 main limbs biased toward screen edges
            // ============================================================
            float[] limbPositions = { 0.08f, 0.30f, 0.70f, 0.92f };
            for (int i = 0; i < 4; i++)
            {
                float t = limbPositions[i];
                // Interpolate spawn point along trunk
                int seg = Mathf.Min((int)(t * (trunkPts.Count - 1)), trunkPts.Count - 2);
                float segFrac = t * (trunkPts.Count - 1) - seg;
                Vector2 spawnPt = Vector2.Lerp(trunkPts[seg], trunkPts[seg + 1], segFrac);

                // Bias limbs strongly toward edges: upper limbs angle up, lower angle down
                float baseAngle;
                if (i < 2)
                {
                    // Upper limbs: angle up-right or up-left toward top edge
                    baseAngle = (i % 2 == 0) ? 35f : 145f;
                }
                else
                {
                    // Lower limbs: angle down-right or down-left toward bottom edge
                    baseAngle = (i % 2 == 0) ? -35f : -145f;
                }
                float verticalBias = (float)(rng.NextDouble() * 20.0 - 10.0);
                float angle = baseAngle + verticalBias;

                Color limbColor = (i % 2 == 0) ? SporefrontColors.InkRed : SporefrontColors.InkGreen;
                ArrivalGenerateBranch(spawnPt, angle, 1, rng, limbColor, pw, ph);
            }
        }

        private void ArrivalGenerateBranch(Vector2 start, float angleDeg, int depth,
                                           System.Random rng, Color branchColor, float pw, float ph)
        {
            const int MaxDepth = 4;
            if (depth > MaxDepth) return;

            float halfW = pw * 0.48f;
            float halfH = ph * 0.48f;

            // Branch length decreases with depth
            float baseLength;
            switch (depth)
            {
                case 1: baseLength = pw * 0.45f; break;
                case 2: baseLength = ph * 0.12f; break;
                case 3: baseLength = ph * 0.06f; break;
                default: baseLength = ph * 0.04f; break;
            }
            float length = baseLength * (0.5f + (float)rng.NextDouble() * 1.0f);

            // Build control points with organic curvature
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            int pointCount = depth == 1 ? 7 : (depth == 2 ? 5 : 3);
            var pts = new List<Vector2>(pointCount);
            pts.Add(start);

            for (int i = 1; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                Vector2 basePoint = start + dir * (length * t);
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float curvatureScale = depth == 1 ? 0.3f : 0.15f;
                float curvature = (float)(rng.NextDouble() * 2.0 - 1.0) * length * curvatureScale;
                Vector2 point = basePoint + perp * curvature;

                // Push limb control points out of center exclusion zone (middle 60% of screen)
                if (depth <= 2)
                {
                    float exW = pw * 0.30f;
                    float exH = ph * 0.30f;
                    if (Mathf.Abs(point.x) < exW && Mathf.Abs(point.y) < exH)
                    {
                        // Push toward nearest edge
                        float pushX = (point.x >= 0f ? exW : -exW);
                        float pushY = (point.y >= 0f ? exH : -exH);
                        // Use whichever axis is closer to the exclusion boundary
                        if (Mathf.Abs(point.x / exW) > Mathf.Abs(point.y / exH))
                            point.x = pushX;
                        else
                            point.y = pushY;
                    }
                }

                // Clamp to screen edges
                if (Mathf.Abs(point.x) >= halfW || Mathf.Abs(point.y) >= halfH)
                {
                    point.x = Mathf.Clamp(point.x, -halfW, halfW);
                    point.y = Mathf.Clamp(point.y, -halfH, halfH);
                    pts.Add(point);
                    break;
                }
                pts.Add(point);
            }

            // Strand style by depth
            List<UITendrilRenderer.StrandParams> strands;
            float tipWidth;
            float looseWidth;
            switch (depth)
            {
                case 1:
                    strands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams { width = 5.0f, alpha = 0.90f, waveFrequency = 1.2f, wavePhase = 0.5f },
                        new UITendrilRenderer.StrandParams { width = 3.5f, alpha = 0.90f, waveFrequency = 1.5f, wavePhase = 2.0f }
                    };
                    tipWidth = 10f; looseWidth = 0.15f;
                    break;
                case 2:
                    strands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams { width = 2.4f, alpha = 0.90f, waveFrequency = 1.0f, wavePhase = 0.8f }
                    };
                    tipWidth = 5f; looseWidth = 0.20f;
                    break;
                default:
                    strands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams { width = 2.0f, alpha = 0.90f, waveFrequency = 0.8f, wavePhase = 1.0f }
                    };
                    tipWidth = 3f; looseWidth = 0.25f;
                    break;
            }

            var branch = arrivalTendrilRenderer.AddBranch(pts, strands, tipWidth, looseWidth);
            branch.branchColor = branchColor;
            branch.growthProgress = 0f;
            arrivalBranches.Add(branch);
            arrivalBranchDepths.Add(depth);

            if (depth >= MaxDepth) return;

            // Spawn sub-branches along this branch
            int childCount = depth <= 1 ? 10 : 5;
            float childLimW = pw * 0.44f;
            float childLimH = ph * 0.44f;
            for (int f = 0; f < childCount; f++)
            {
                float spawnFrac = 0.2f + (float)f / (childCount - 1 + 0.001f) * 0.75f;
                int spawnSeg = Mathf.Min((int)(spawnFrac * (pts.Count - 1)), pts.Count - 2);
                float segFrac = spawnFrac * (pts.Count - 1) - spawnSeg;
                Vector2 spawnPt = Vector2.Lerp(pts[spawnSeg], pts[spawnSeg + 1], segFrac);

                // Skip if near screen edge
                if (Mathf.Abs(spawnPt.x) >= childLimW || Mathf.Abs(spawnPt.y) >= childLimH)
                    continue;

                // Skip if in center exclusion zone (middle 60% of screen)
                float exclusionW = pw * 0.30f;
                float exclusionH = ph * 0.30f;
                if (Mathf.Abs(spawnPt.x) < exclusionW && Mathf.Abs(spawnPt.y) < exclusionH)
                    continue;

                float side = (f % 2 == 0) ? 1f : -1f;
                float diverge = 30f + (float)(rng.NextDouble() * 40.0);
                float childAngle = angleDeg + side * diverge;

                ArrivalGenerateBranch(spawnPt, childAngle, depth + 1, rng, branchColor, pw, ph);
            }
        }

        private static List<UITendrilRenderer.StrandParams> ArrivalTrunkStrands(int half)
        {
            var all = new[]
            {
                new UITendrilRenderer.StrandParams { width = 4.5f, alpha = 0.90f, waveFrequency = 3.5f, wavePhase = 0.0f },
                new UITendrilRenderer.StrandParams { width = 3.8f, alpha = 0.90f, waveFrequency = 4.0f, wavePhase = 1.2f },
                new UITendrilRenderer.StrandParams { width = 4.0f, alpha = 0.90f, waveFrequency = 3.0f, wavePhase = 2.5f }
            };
            var result = new List<UITendrilRenderer.StrandParams>();
            for (int i = 0; i < all.Length; i++)
            {
                if (i % 2 == half) result.Add(all[i]);
            }
            return result;
        }

        /// <summary>
        /// Updates the growth progress of all arrival branches (0..1).
        /// Depth-based stagger: trunk at 0%, limbs from 15%, sub-branches from 35%,
        /// tendrils from 55%, quaternary from 70%.
        /// </summary>
        public void UpdateArrivalTendrilGrowth(float overallProgress)
        {
            // Count branches per depth for staggering within each tier
            var depthCounters = new int[5]; // depths 0-4

            for (int i = 0; i < arrivalBranches.Count; i++)
            {
                int depth = (i < arrivalBranchDepths.Count) ? arrivalBranchDepths[i] : 0;
                int indexInDepth = depthCounters[Mathf.Min(depth, 4)]++;

                float startAt;
                switch (depth)
                {
                    case 0: startAt = 0f; break;
                    case 1: startAt = 0.15f + indexInDepth * 0.012f; break;
                    case 2: startAt = 0.35f + indexInDepth * 0.005f; break;
                    case 3: startAt = 0.55f + indexInDepth * 0.003f; break;
                    default: startAt = 0.70f + indexInDepth * 0.002f; break;
                }
                startAt = Mathf.Min(startAt, 0.90f);

                float available = 1f - startAt;
                float branchT   = available > 0f
                    ? Mathf.Clamp01((overallProgress - startAt) / available)
                    : 1f;
                float eased = 1f - (1f - branchT) * (1f - branchT);
                arrivalBranches[i].growthProgress  = Mathf.Clamp01(eased);
                arrivalBranches[i].idlePulsePhase  = Time.time * 2.0f + i * 0.5f;
            }

            if (arrivalTendrilRenderer != null)
                arrivalTendrilRenderer.MarkDirty();
        }

        /// <summary>
        /// Clears all arrival tendril branches.
        /// </summary>
        public void ClearArrivalTendrils()
        {
            arrivalBranches.Clear();
            arrivalBranchDepths.Clear();
            if (arrivalTendrilRenderer != null)
                arrivalTendrilRenderer.Clear();
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
