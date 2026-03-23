// ============================================================================
// FILE: Visual/InGameMenuPanel.cs
// PURPOSE: Centered in-game pause menu modal with resume, save, settings,
//          and quit-to-main-menu options. Matches Sporefront parchment UI.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class InGameMenuPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnResumeGame;
        public event Action OnSaveGame;
        public event Action OnLoadGame;
        public event Action OnSettings;
        public event Action OnQuitToMainMenu;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private CanvasGroup backdropCG;
        private GameObject panel;
        private Coroutine fadeCoroutine;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // 1. Semi-transparent backdrop (click-to-close = resume)
            backdrop = UIHelper.CreatePanel(canvasTransform, "InGameMenuBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);

            // Override sorting to guarantee menu renders above all HUD elements
            var overrideCanvas = backdrop.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 200;
            backdrop.AddComponent<GraphicRaycaster>();

            backdropCG = backdrop.AddComponent<CanvasGroup>();
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // 2. Centered parchment panel
            panel = UIHelper.CreatePanel(backdrop.transform, "InGameMenuPanel",
                UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 380, 420);

            // 3. Tendril decoration
            PopupTendrilDecorator.Attach(rt);

            // 4. Click sink — prevent backdrop close when clicking panel
            var panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            BuildContent();

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            backdrop.SetActive(true);
            backdrop.transform.SetAsLastSibling(); // render above minimap & all HUD
            fadeCoroutine = StartCoroutine(UIHelper.FadeIn(backdropCG));
        }

        public void Close()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(UIHelper.FadeOut(backdropCG));
            OnResumeGame?.Invoke();
        }

        public void Hide()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            backdrop.SetActive(false);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            // ── Title header ────────────────────────────────────────────
            var headerBar = UIHelper.CreatePanel(panel.transform, "HeaderBar",
                SporefrontColors.ParchmentDeep);
            var headerBarRT = headerBar.GetComponent<RectTransform>();
            headerBarRT.anchorMin = new Vector2(0, 1);
            headerBarRT.anchorMax = new Vector2(1, 1);
            headerBarRT.pivot = new Vector2(0.5f, 1);
            headerBarRT.offsetMin = new Vector2(0, -52);
            headerBarRT.offsetMax = Vector2.zero;

            var titleLabel = UIHelper.CreateLabel(headerBar.transform, "SPOREFRONT",
                UIConstants.FontHeader, SporefrontColors.SporeRed,
                TextAnchor.MiddleCenter, true);
            var titleRT = titleLabel.GetComponent<RectTransform>();
            UIHelper.StretchFull(titleRT);

            // ── Menu buttons area ───────────────────────────────────────
            var buttonsArea = UIHelper.CreatePanel(panel.transform, "ButtonsArea", Color.clear);
            var buttonsImg = buttonsArea.GetComponent<Image>();
            if (buttonsImg != null) buttonsImg.enabled = false;
            var buttonsRT = buttonsArea.GetComponent<RectTransform>();
            UIHelper.StretchFull(buttonsRT);
            buttonsRT.offsetMin = new Vector2(
                UIConstants.SpaceXL, UIConstants.SpaceLG);
            buttonsRT.offsetMax = new Vector2(
                -UIConstants.SpaceXL, -56);

            var vlg = buttonsArea.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UIConstants.SpaceSM;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(0, 0, (int)UIConstants.SpaceMD, 0);

            // Resume Game
            CreateMenuButton(buttonsArea.transform, "Resume Game",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor,
                () => Close());

            // Save Game
            CreateMenuButton(buttonsArea.transform, "Save Game",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText,
                () =>
                {
                    Hide();
                    OnSaveGame?.Invoke();
                });

            // Load Game
            CreateMenuButton(buttonsArea.transform, "Load Game",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText,
                () =>
                {
                    Hide();
                    OnLoadGame?.Invoke();
                });

            // Settings
            CreateMenuButton(buttonsArea.transform, "Settings",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText,
                () =>
                {
                    OnSettings?.Invoke();
                });

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(buttonsArea.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // Divider
            UIHelper.CreateDivider(buttonsArea.transform,
                new Color(SporefrontColors.InkFaded.r,
                           SporefrontColors.InkFaded.g,
                           SporefrontColors.InkFaded.b, 0.3f), 1f);

            // Quit to Main Menu
            CreateMenuButton(buttonsArea.transform, "Quit to Main Menu",
                SporefrontColors.SporeRed, SporefrontColors.ParchmentLight,
                () =>
                {
                    Hide();
                    OnQuitToMainMenu?.Invoke();
                });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void CreateMenuButton(Transform parent, string text,
            Color bgColor, Color textColor, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                bgColor, textColor, UIConstants.FontSubheader, () => onClick?.Invoke());
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 48;
        }
    }
}
