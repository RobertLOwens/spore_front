// ============================================================================
// FILE: Visual/MainMenuPanel.cs
// PURPOSE: Full-screen main menu panel with game title and navigation buttons.
//          Port of MainMenuViewController.swift
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

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

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private Text versionLabel;

        private const float ButtonWidth = 260f;
        private const float ButtonHeight = 44f;
        private const float ButtonSpacing = 10f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen dark parchment background
            panel = UIHelper.CreatePanel(canvasTransform, "MainMenuPanel", SporefrontColors.ParchmentDeep);
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
            panel.SetActive(true);
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
            // Center column container
            var centerColumn = UIHelper.CreatePanel(panel.transform, "CenterColumn", Color.clear);
            var columnRT = centerColumn.GetComponent<RectTransform>();
            columnRT.anchorMin = new Vector2(0.5f, 0f);
            columnRT.anchorMax = new Vector2(0.5f, 1f);
            columnRT.pivot = new Vector2(0.5f, 0.5f);
            columnRT.sizeDelta = new Vector2(ButtonWidth + 40f, 0f);
            columnRT.offsetMin = new Vector2(-(ButtonWidth + 40f) / 2f, 0f);
            columnRT.offsetMax = new Vector2((ButtonWidth + 40f) / 2f, 0f);

            var vlg = centerColumn.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = ButtonSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(20, 20, 0, 0);

            // Add a flexible spacer at top to push content down
            var topSpacer = new GameObject("TopSpacer", typeof(RectTransform), typeof(LayoutElement));
            topSpacer.transform.SetParent(centerColumn.transform, false);
            var topSpacerLE = topSpacer.GetComponent<LayoutElement>();
            topSpacerLE.flexibleHeight = 1f;

            // Title
            var title = UIHelper.CreateLabel(centerColumn.transform, "SPOREFRONT",
                32, SporefrontColors.InkBlack, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 60f;

            // Subtitle
            var subtitle = UIHelper.CreateLabel(centerColumn.transform, "A Strategy Game",
                16, SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var subtitleLE = subtitle.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 28f;

            // Spacer between title and buttons
            var titleSpacer = new GameObject("TitleSpacer", typeof(RectTransform), typeof(LayoutElement));
            titleSpacer.transform.SetParent(centerColumn.transform, false);
            var titleSpacerLE = titleSpacer.GetComponent<LayoutElement>();
            titleSpacerLE.preferredHeight = 30f;

            UIHelper.CreateDivider(centerColumn.transform, SporefrontColors.ParchmentShadow, 2f);

            // Spacer after divider
            var divSpacer = new GameObject("DivSpacer", typeof(RectTransform), typeof(LayoutElement));
            divSpacer.transform.SetParent(centerColumn.transform, false);
            var divSpacerLE = divSpacer.GetComponent<LayoutElement>();
            divSpacerLE.preferredHeight = 10f;

            // Menu buttons
            CreateMenuButton(centerColumn.transform, "New Game", SporefrontColors.SporeGreen,
                UIHelper.HudTextColor, () => OnNewGame?.Invoke());

            CreateMenuButton(centerColumn.transform, "Resume Game", UIHelper.ButtonBg,
                UIHelper.ButtonText, () => OnResumeGame?.Invoke());

            CreateMenuButton(centerColumn.transform, "Load Game", UIHelper.ButtonBg,
                UIHelper.ButtonText, () => OnLoadGame?.Invoke());

            CreateMenuButton(centerColumn.transform, "Settings", UIHelper.ButtonBg,
                UIHelper.ButtonText, () => OnSettings?.Invoke());

            // Spacer
            var midSpacer = new GameObject("MidSpacer", typeof(RectTransform), typeof(LayoutElement));
            midSpacer.transform.SetParent(centerColumn.transform, false);
            var midSpacerLE = midSpacer.GetComponent<LayoutElement>();
            midSpacerLE.preferredHeight = 10f;

            UIHelper.CreateDivider(centerColumn.transform, SporefrontColors.ParchmentShadow, 1f);

            // Spacer
            var midSpacer2 = new GameObject("MidSpacer2", typeof(RectTransform), typeof(LayoutElement));
            midSpacer2.transform.SetParent(centerColumn.transform, false);
            var midSpacer2LE = midSpacer2.GetComponent<LayoutElement>();
            midSpacer2LE.preferredHeight = 10f;

            // AI buttons
            CreateMenuButton(centerColumn.transform, "Evolve AI", SporefrontColors.SporePurple,
                UIHelper.HudTextColor, () => OnEvolveAI?.Invoke());

            CreateMenuButton(centerColumn.transform, "Spectate AI", SporefrontColors.SporeTeal,
                UIHelper.HudTextColor, () => OnSpectateAI?.Invoke());

            // Spacer
            var midSpacer3 = new GameObject("MidSpacer3", typeof(RectTransform), typeof(LayoutElement));
            midSpacer3.transform.SetParent(centerColumn.transform, false);
            var midSpacer3LE = midSpacer3.GetComponent<LayoutElement>();
            midSpacer3LE.preferredHeight = 10f;

            // About button
            CreateMenuButton(centerColumn.transform, "About", SporefrontColors.ParchmentDark,
                SporefrontColors.InkDark, () => OnAbout?.Invoke());

            // Flexible spacer at bottom
            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(centerColumn.transform, false);
            var bottomSpacerLE = bottomSpacer.GetComponent<LayoutElement>();
            bottomSpacerLE.flexibleHeight = 1f;

            // Version label at bottom
            versionLabel = UIHelper.CreateLabel(centerColumn.transform, "v0.1.0",
                11, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var versionLE = versionLabel.gameObject.AddComponent<LayoutElement>();
            versionLE.preferredHeight = 24f;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateMenuButton(Transform parent, string text, Color bgColor,
            Color textColor, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text, bgColor, textColor,
                UIHelper.DefaultBodyFontSize + 2, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = ButtonHeight;
            le.preferredWidth = ButtonWidth;
            return btn;
        }

        /// <summary>
        /// Updates the version label text.
        /// </summary>
        public void SetVersion(string version)
        {
            if (versionLabel != null)
                versionLabel.text = version;
        }
    }
}
