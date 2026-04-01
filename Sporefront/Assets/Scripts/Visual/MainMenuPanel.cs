// ============================================================================
// FILE: Visual/MainMenuPanel.cs
// PURPOSE: Full-screen main menu panel with game title and navigation buttons.
//          Port of MainMenuViewController.swift
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class MainMenuPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnNewGame;
        public event Action OnResumeGame;
        public event Action OnRejoinGame;
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
        private GameObject rejoinButton;
        private Text statusLabel;

        // Intro animation
        private RectTransform titleRT;
        private CanvasGroup titleGroup;
        private CanvasGroup columnGroup;

        private const float ButtonWidth          = 320f;
        private const float ButtonSpacing        = 10f;
        private const float TitleTopY            = -40f;
        private const float TitleFadeDuration    = 2.5f;
        private const float MaxTendrilWait       = 4.0f;
        private const float TitleRiseDuration    = 0.75f;
        private const float ButtonsSlideDuration = 0.55f;
        private const float ButtonsSlideOffset   = 110f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen dark background
            panel = UIHelper.CreatePanel(canvasTransform, "MainMenuPanel", SporefrontColors.ParchmentMid);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public override void Show()
        {
            RefreshUsername();
            ShowMainPage();

            if (tendrilAnimator != null && !tendrilAnimator.IsGrown)
            {
                // First show: run cinematic intro sequence
                if (titleGroup  != null) titleGroup.alpha  = 0f;
                if (columnGroup != null)
                {
                    columnGroup.alpha           = 0f;
                    columnGroup.interactable    = false;
                    columnGroup.blocksRaycasts  = false;
                }
                panel.SetActive(true);
                tendrilAnimator.StartAnimation();
                StartCoroutine(RunIntroSequence());
            }
            else
            {
                // Returning from game / already grown: snap to final state
                if (tendrilAnimator != null) tendrilAnimator.SnapToGrown();
                panel.SetActive(true);
                SnapToFinalState();
            }
        }

        public override void Hide()
        {
            panel.SetActive(false);
        }

        public new bool IsVisible => panel != null && panel.activeSelf;

        public void SetRejoinVisible(bool visible)
        {
            if (rejoinButton != null)
                rejoinButton.SetActive(visible);
        }

        /// <summary>
        /// Show a status message at the bottom of the menu (e.g., "Joining game..." or error text).
        /// Pass null or empty to clear.
        /// </summary>
        public void SetStatusText(string text, Color? color = null)
        {
            if (statusLabel == null && panel != null)
            {
                statusLabel = UIHelper.CreateLabel(panel.transform, "",
                    UIConstants.FontCaption, SporefrontColors.ParchmentLight,
                    TextAnchor.MiddleCenter);
                var rt = statusLabel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.1f, 0.02f);
                rt.anchorMax = new Vector2(0.9f, 0.08f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (statusLabel != null)
            {
                statusLabel.text = text ?? "";
                statusLabel.color = color ?? SporefrontColors.ParchmentLight;
                statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        public RectTransform PanelRT => panel?.GetComponent<RectTransform>();

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

            // Parchment overlay — simulates paper fiber partially covering ink
            UIHelper.AddParchmentOverlay(panel.transform, 0.25f);

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
            columnGroup = centerColumn.AddComponent<CanvasGroup>();

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
            vlg.padding = new RectOffset(0, 0, 0, 0);

            // Title — positioned near top of screen, outside the VLG
            // Pre-request glyphs at target size to force Unity to allocate
            // a large enough dynamic font atlas before the Text mesh is built.
            var headerFont = UIHelper.HeaderFont;
            headerFont.RequestCharactersInTexture("SPOREFRONT", 96);
            var title = UIHelper.CreateLabel(panel.transform, "SPOREFRONT",
                96, SporefrontColors.SporeRed, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, TitleTopY);
            titleRT.sizeDelta = new Vector2(700f, 115f);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();

            // Username welcome label — just below title
            usernameLabel = UIHelper.CreateLabel(panel.transform, "",
                14, SporefrontColors.InkMid, TextAnchor.MiddleCenter);
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

            // Parchment card behind menu items — looks like a piece of paper over the tendrils
            float backdropWidth = ButtonWidth + 60f;
            var backdropGO = UIHelper.CreatePanel(centerColumn.transform, "ButtonBackdrop",
                new Color(SporefrontColors.ParchmentCream.r, SporefrontColors.ParchmentCream.g, SporefrontColors.ParchmentCream.b, 0.92f), cornerRadius: 6);
            // Style the outline as a subtle parchment edge
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null)
            {
                backdropOutline.effectColor = new Color(UIHelper.InkMutedText.r, UIHelper.InkMutedText.g, UIHelper.InkMutedText.b, 0.6f);
                backdropOutline.effectDistance = new Vector2(1f, -1f);
            }
            // Drop shadow behind the parchment card
            var shadowGO = new GameObject("ParchmentShadow", typeof(RectTransform), typeof(Image));
            shadowGO.transform.SetParent(backdropGO.transform, false);
            shadowGO.transform.SetAsFirstSibling();
            var shadowRT = shadowGO.GetComponent<RectTransform>();
            shadowRT.anchorMin = Vector2.zero;
            shadowRT.anchorMax = Vector2.one;
            shadowRT.offsetMin = new Vector2(-4f, -6f);
            shadowRT.offsetMax = new Vector2(4f, 2f);
            var shadowImg = shadowGO.GetComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
            shadowImg.raycastTarget = false;
            var shadowIgnore = shadowGO.AddComponent<LayoutElement>();
            shadowIgnore.ignoreLayout = true;
            var backdropLE = backdropGO.AddComponent<LayoutElement>();
            backdropLE.preferredWidth = backdropWidth;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = ButtonSpacing;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = true;
            backdropVLG.padding = new RectOffset(20, 20, 16, 16);

            var backdropCSF = backdropGO.AddComponent<ContentSizeFitter>();
            backdropCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Build shared hover gradient texture (1x64)
            hoverGradientTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            hoverGradientTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int y = 0; y < 64; y++)
            {
                float alpha;
                if (y < 4) // bottom ~6%: ramp 0 → 0.12
                    alpha = Mathf.Lerp(0f, 0.12f, y / 3f);
                else if (y < 60) // middle ~88%: constant 0.12
                    alpha = 0.12f;
                else // top ~6%: ramp 0.12 → 0
                    alpha = Mathf.Lerp(0.12f, 0f, (y - 60) / 3f);
                pixels[y] = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g, SporefrontColors.InkDark.b, alpha);
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
                SporefrontColors.InkDark, () => OnNewGame?.Invoke());

            CreateTextMenuItem(mainPage.transform, "Resume Game",
                SporefrontColors.InkDark, () => OnResumeGame?.Invoke());

            var rejoinBtn = CreateTextMenuItem(mainPage.transform, "Rejoin Online Game",
                SporefrontColors.SporeGreen, () => OnRejoinGame?.Invoke());
            rejoinButton = rejoinBtn.gameObject;
            rejoinButton.SetActive(false); // Hidden by default, shown when active game found

            CreateTextMenuItem(mainPage.transform, "Load Game",
                SporefrontColors.InkDark, () => OnLoadGame?.Invoke());

            CreateTextMenuItem(mainPage.transform, "Settings",
                SporefrontColors.InkDark, () => OnSettings?.Invoke());

            CreateTextMenuItem(mainPage.transform, "More",
                SporefrontColors.InkLight, () => ShowMorePage());

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
                SporefrontColors.InkMid, () => OnEvolveAI?.Invoke());

            CreateTextMenuItem(morePage.transform, "Spectate AI",
                SporefrontColors.InkMid, () => OnSpectateAI?.Invoke());

            CreateTextMenuItem(morePage.transform, "About",
                SporefrontColors.InkLight, () => OnAbout?.Invoke());

            CreateTextMenuItem(morePage.transform, "Account",
                SporefrontColors.InkLight, () => OnAccount?.Invoke());

            CreateTextMenuItem(morePage.transform, "Back",
                SporefrontColors.InkDark, () => ShowMainPage());

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
            UIHelper.CreateDivider(container.transform, SporefrontColors.InkFaded, 1f);

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

        private void SnapToFinalState()
        {
            StopAllCoroutines();
            if (titleGroup  != null) titleGroup.alpha = 1f;
            if (titleRT     != null) titleRT.anchoredPosition = new Vector2(0f, TitleTopY);
            if (columnGroup != null) { columnGroup.alpha = 1f; columnGroup.interactable = true; columnGroup.blocksRaycasts = true; }
            if (centerColumnRT != null) centerColumnRT.anchoredPosition = Vector2.zero;
            RefreshUsername();
        }

        private IEnumerator RunIntroSequence()
        {
            // Wait one frame for layout to resolve
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();

            // Compute title center-screen Y (anchor/pivot at top of panel)
            float panelH = panel.GetComponent<RectTransform>().rect.height;
            if (panelH <= 0f) panelH = 1080f;
            float titleH  = titleRT.sizeDelta.y;
            float centerY = (titleH * 0.5f) - (panelH * 0.5f);

            titleRT.anchoredPosition = new Vector2(0f, centerY);
            if (usernameLabel != null) usernameLabel.gameObject.SetActive(false);

            // ── Phase 1: title fades in at center while tendrils grow ──────
            float timer = 0f;
            while (timer < MaxTendrilWait)
            {
                timer += Time.deltaTime;
                if (titleGroup != null)
                    titleGroup.alpha = Mathf.Clamp01(timer / TitleFadeDuration);
                if (tendrilAnimator != null && tendrilAnimator.IsGrown) break;
                yield return null;
            }
            if (titleGroup != null) titleGroup.alpha = 1f;

            yield return new WaitForSeconds(0.2f);

            // ── Phase 2: title rises to top ──────────────────────────────────
            float riseTimer = 0f;
            float startY    = titleRT.anchoredPosition.y;
            while (riseTimer < TitleRiseDuration)
            {
                riseTimer += Time.deltaTime;
                float t     = Mathf.Clamp01(riseTimer / TitleRiseDuration);
                float eased = t * t * (3f - 2f * t); // smoothstep
                titleRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, TitleTopY, eased));
                yield return null;
            }
            titleRT.anchoredPosition = new Vector2(0f, TitleTopY);

            // ── Phase 3: buttons slide up ─────────────────────────────────────
            RefreshUsername();
            if (usernameLabel != null) usernameLabel.gameObject.SetActive(true);
            if (columnGroup != null)
            {
                columnGroup.alpha          = 0f;
                columnGroup.interactable   = false;
                columnGroup.blocksRaycasts = false;
            }
            if (centerColumnRT != null)
                centerColumnRT.anchoredPosition = new Vector2(0f, -ButtonsSlideOffset);

            float slideTimer = 0f;
            while (slideTimer < ButtonsSlideDuration)
            {
                slideTimer += Time.deltaTime;
                float t     = Mathf.Clamp01(slideTimer / ButtonsSlideDuration);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad
                if (columnGroup    != null) columnGroup.alpha = eased;
                if (centerColumnRT != null) centerColumnRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(-ButtonsSlideOffset, 0f, eased));
                yield return null;
            }

            if (columnGroup    != null) { columnGroup.alpha = 1f; columnGroup.interactable = true; columnGroup.blocksRaycasts = true; }
            if (centerColumnRT != null) centerColumnRT.anchoredPosition = Vector2.zero;
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
