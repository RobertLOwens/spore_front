// ============================================================================
// FILE: Visual/MainMenuPanel.cs
// PURPOSE: Full-screen main menu panel with game title and navigation buttons.
//          Port of MainMenuViewController.swift
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class MainMenuPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnNewGame;
        public event Action OnResumeGame;
        public event Action OnLoadGame;
        public event Action OnSettings;
        public event Action OnEvolveAI;
        public event Action OnSpectateAI;
        public event Action OnAbout;
        public event Action OnAccount;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private Text versionLabel;
        private Text usernameLabel;
        private MenuTendrilAnimator tendrilAnimator;
        private RectTransform centerColumnRT;
        private Texture2D hoverGradientTexture;
        private GameObject mainPage;
        private GameObject morePage;

        private const float ButtonWidth = 320f;
        private const float ButtonSpacing = 10f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen dark background
            panel = UIHelper.CreatePanel(canvasTransform, "MainMenuPanel", SporefrontColors.BgDeep);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            RefreshUsername();
            ShowMainPage();
            panel.SetActive(true);
            if (tendrilAnimator != null)
                tendrilAnimator.StartAnimation();
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            // Tendril layer behind all content
            var tendrilGO = new GameObject("TendrilLayer", typeof(RectTransform), typeof(CanvasRenderer));
            tendrilGO.transform.SetParent(panel.transform, false);
            var tendrilRT = tendrilGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(tendrilRT);
            tendrilGO.transform.SetAsFirstSibling();

            var tendrilRenderer = tendrilGO.AddComponent<UITendrilRenderer>();
            tendrilRenderer.raycastTarget = false;

            tendrilAnimator = tendrilGO.AddComponent<MenuTendrilAnimator>();

            // Center column container
            var centerColumn = UIHelper.CreatePanel(panel.transform, "CenterColumn", Color.clear);
            var columnRT = centerColumn.GetComponent<RectTransform>();
            columnRT.anchorMin = new Vector2(0.5f, 0f);
            columnRT.anchorMax = new Vector2(0.5f, 1f);
            columnRT.pivot = new Vector2(0.5f, 0.5f);
            columnRT.sizeDelta = new Vector2(ButtonWidth + 60f, 0f);
            columnRT.offsetMin = new Vector2(-(ButtonWidth + 60f) / 2f, 0f);
            columnRT.offsetMax = new Vector2((ButtonWidth + 60f) / 2f, 0f);
            centerColumnRT = columnRT;

            // Disable Image on center column so it's truly transparent
            var centerImg = centerColumn.GetComponent<Image>();
            if (centerImg != null) centerImg.enabled = false;

            // Initialize tendril animator with references
            var panelRT = panel.GetComponent<RectTransform>();
            tendrilAnimator.Initialize(tendrilRenderer, centerColumnRT, panelRT);

            var vlg = centerColumn.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = ButtonSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            // Title — positioned near top of screen, outside the VLG
            var title = UIHelper.CreateLabel(panel.transform, "SPOREFRONT",
                116, SporefrontColors.ParchmentLight, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -40f);
            titleRT.sizeDelta = new Vector2(600f, 130f);

            // Username welcome label — just below title
            usernameLabel = UIHelper.CreateLabel(panel.transform, "",
                14, SporefrontColors.SporeTeal, TextAnchor.MiddleCenter);
            var usernameRT = usernameLabel.GetComponent<RectTransform>();
            usernameRT.anchorMin = new Vector2(0.5f, 1f);
            usernameRT.anchorMax = new Vector2(0.5f, 1f);
            usernameRT.pivot = new Vector2(0.5f, 1f);
            usernameRT.anchoredPosition = new Vector2(0f, -170f);
            usernameRT.sizeDelta = new Vector2(400f, 24f);

            // Flexible spacer above backdrop to center it
            var topSpacer = new GameObject("TopSpacer", typeof(RectTransform), typeof(LayoutElement));
            topSpacer.transform.SetParent(centerColumn.transform, false);
            var topSpacerLE = topSpacer.GetComponent<LayoutElement>();
            topSpacerLE.flexibleHeight = 1f;

            // Semi-transparent backdrop behind menu items, half the column width
            float backdropWidth = (ButtonWidth + 60f) * 0.5f;
            var backdropGO = UIHelper.CreatePanel(centerColumn.transform, "ButtonBackdrop",
                new Color(0f, 0f, 0f, 0.80f), cornerRadius: 0);
            // Remove outline added by CreatePanel
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null) UnityEngine.Object.Destroy(backdropOutline);
            var backdropLE = backdropGO.AddComponent<LayoutElement>();
            backdropLE.preferredWidth = backdropWidth;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = ButtonSpacing;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = false;
            backdropVLG.padding = new RectOffset(15, 15, 10, 10);

            // Build shared hover gradient texture (1x64)
            hoverGradientTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            hoverGradientTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int y = 0; y < 64; y++)
            {
                float alpha;
                if (y < 4) // bottom ~6%: ramp 0 → 0.2
                    alpha = Mathf.Lerp(0f, 0.2f, y / 3f);
                else if (y < 60) // middle ~88%: constant 0.2
                    alpha = 0.2f;
                else // top ~6%: ramp 0.2 → 0
                    alpha = Mathf.Lerp(0.2f, 0f, (y - 60) / 3f);
                pixels[y] = new Color(1f, 1f, 1f, alpha);
            }
            hoverGradientTexture.SetPixels(pixels);
            hoverGradientTexture.Apply();

            // === Main page ===
            mainPage = new GameObject("MainPage", typeof(RectTransform));
            mainPage.transform.SetParent(backdropGO.transform, false);
            var mainPageVLG = mainPage.AddComponent<VerticalLayoutGroup>();
            mainPageVLG.spacing = ButtonSpacing;
            mainPageVLG.childAlignment = TextAnchor.UpperCenter;
            mainPageVLG.childForceExpandWidth = true;
            mainPageVLG.childForceExpandHeight = false;
            mainPageVLG.childControlWidth = true;
            mainPageVLG.childControlHeight = false;

            CreateTextMenuItem(mainPage.transform, "New Game",
                SporefrontColors.ParchmentLight, () => OnNewGame?.Invoke());

            CreateTextMenuItem(mainPage.transform, "Resume Game",
                SporefrontColors.ParchmentLight, () => OnResumeGame?.Invoke());

            CreateTextMenuItem(mainPage.transform, "Load Game",
                SporefrontColors.ParchmentLight, () => OnLoadGame?.Invoke());

            CreateTextMenuItem(mainPage.transform, "Settings",
                SporefrontColors.ParchmentLight, () => OnSettings?.Invoke());

            CreateTextMenuItem(mainPage.transform, "More",
                SporefrontColors.ParchmentShadow, () => ShowMorePage());

            // === More page ===
            morePage = new GameObject("MorePage", typeof(RectTransform));
            morePage.transform.SetParent(backdropGO.transform, false);
            var morePageVLG = morePage.AddComponent<VerticalLayoutGroup>();
            morePageVLG.spacing = ButtonSpacing;
            morePageVLG.childAlignment = TextAnchor.UpperCenter;
            morePageVLG.childForceExpandWidth = true;
            morePageVLG.childForceExpandHeight = false;
            morePageVLG.childControlWidth = true;
            morePageVLG.childControlHeight = false;

            CreateTextMenuItem(morePage.transform, "Evolve AI",
                SporefrontColors.SporeTeal, () => OnEvolveAI?.Invoke());

            CreateTextMenuItem(morePage.transform, "Spectate AI",
                SporefrontColors.SporeTeal, () => OnSpectateAI?.Invoke());

            CreateTextMenuItem(morePage.transform, "About",
                SporefrontColors.ParchmentShadow, () => OnAbout?.Invoke());

            CreateTextMenuItem(morePage.transform, "Account",
                SporefrontColors.ParchmentShadow, () => OnAccount?.Invoke());

            CreateTextMenuItem(morePage.transform, "Back",
                SporefrontColors.ParchmentLight, () => ShowMainPage());

            morePage.SetActive(false);

            // Flexible spacer at bottom
            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(centerColumn.transform, false);
            var bottomSpacerLE = bottomSpacer.GetComponent<LayoutElement>();
            bottomSpacerLE.flexibleHeight = 1f;

            // Version label — bottom-right corner of the screen
            versionLabel = UIHelper.CreateLabel(panel.transform, "v0.1.0",
                UIConstants.FontCaption, SporefrontColors.InkFaded, TextAnchor.LowerRight);
            var versionRT = versionLabel.GetComponent<RectTransform>();
            versionRT.anchorMin = new Vector2(1f, 0f);
            versionRT.anchorMax = new Vector2(1f, 0f);
            versionRT.pivot = new Vector2(1f, 0f);
            versionRT.anchoredPosition = new Vector2(-10f, 8f);
            versionRT.sizeDelta = new Vector2(120f, 24f);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateTextMenuItem(Transform parent, string text, Color textColor, Action onClick)
        {
            // Container — transparent Image needed so Button/EventTrigger raycast the full area
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

            // Hover gradient overlay (behind label, stretched to fill container)
            var hoverGO = new GameObject("HoverOverlay", typeof(RectTransform), typeof(RawImage));
            hoverGO.transform.SetParent(container.transform, false);
            var hoverRT = hoverGO.GetComponent<RectTransform>();
            hoverRT.anchorMin = Vector2.zero;
            hoverRT.anchorMax = Vector2.one;
            hoverRT.offsetMin = Vector2.zero;
            hoverRT.offsetMax = Vector2.zero;
            var hoverRaw = hoverGO.GetComponent<RawImage>();
            hoverRaw.texture = hoverGradientTexture;
            hoverRaw.enabled = false; // hidden at rest — enabled on hover
            hoverRaw.raycastTarget = false;
            // Ignore layout so it doesn't affect VLG sizing
            var hoverIgnore = hoverGO.AddComponent<LayoutElement>();
            hoverIgnore.ignoreLayout = true;

            // Text label — larger for menu prominence (85% of 2x)
            var label = UIHelper.CreateLabel(container.transform, text,
                (int)(UIConstants.FontSubheader * 1.7f), textColor, TextAnchor.MiddleCenter);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 41f;

            // Thin underline divider
            UIHelper.CreateDivider(container.transform, SporefrontColors.BorderAccent, 1f);

            // Button component on container — uses container Image as raycast target
            var btn = container.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = containerImg;

            // Disable navigation to avoid highlight artifacts
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            btn.onClick.AddListener(() => onClick?.Invoke());

            // Hover events to show/hide gradient overlay
            var trigger = container.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => hoverRaw.enabled = true);
            trigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => hoverRaw.enabled = false);
            trigger.triggers.Add(exitEntry);

            return btn;
        }

        private void ShowMainPage()
        {
            if (mainPage != null) mainPage.SetActive(true);
            if (morePage != null) morePage.SetActive(false);
        }

        private void ShowMorePage()
        {
            if (mainPage != null) mainPage.SetActive(false);
            if (morePage != null) morePage.SetActive(true);
        }

        /// <summary>
        /// Updates the version label text.
        /// </summary>
        public void SetVersion(string version)
        {
            if (versionLabel != null)
                versionLabel.text = version;
        }

        /// <summary>
        /// Refresh username display from AuthService.
        /// </summary>
        public void RefreshUsername()
        {
            if (usernameLabel == null) return;

            var auth = AuthService.Instance;
            if (auth.CurrentState == AuthState.SignedIn && !string.IsNullOrEmpty(auth.CurrentDisplayName))
            {
                usernameLabel.text = $"Welcome, {auth.CurrentDisplayName}";
                usernameLabel.gameObject.SetActive(true);
            }
            else
            {
                usernameLabel.text = "";
                usernameLabel.gameObject.SetActive(false);
            }
        }
    }
}
