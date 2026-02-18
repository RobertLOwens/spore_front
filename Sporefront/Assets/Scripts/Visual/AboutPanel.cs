// ============================================================================
// FILE: Visual/AboutPanel.cs
// PURPOSE: Simple modal showing game information and version.
//          Port of AboutViewController.swift
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class AboutPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "AboutBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Center panel
            panel = UIHelper.CreatePanel(backdrop.transform, "AboutPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, UIConstants.ModalSmallW, UIConstants.ModalSmallH);

            BuildContent();
            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            // Title
            var title = UIHelper.CreateLabel(panel.transform, "About Sporefront",
                22, UIHelper.HeaderTextColor, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 40;

            UIHelper.CreateDivider(panel.transform, SporefrontColors.ParchmentShadow, 2f);

            // Spacer
            var spacer1 = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer1.transform.SetParent(panel.transform, false);
            spacer1.GetComponent<LayoutElement>().preferredHeight = 8;

            // Description
            string description =
                "Sporefront is a real-time strategy game where you build " +
                "a civilization, manage resources, train armies, and conquer " +
                "your enemies.\n\n" +
                "Features:\n" +
                "- Hex-based map with terrain and fog of war\n" +
                "- Resource gathering and building construction\n" +
                "- Military training and multi-army combat\n" +
                "- Commander stats and unit upgrades\n" +
                "- Entrenchment and garrison defense\n" +
                "- Research tree with 7 branches\n" +
                "- AI opponents with evolutionary tuning\n" +
                "- Arena mode with configurable scenarios";

            var descLabel = UIHelper.CreateLabel(panel.transform, description,
                13, UIHelper.BodyTextColor, TextAnchor.UpperLeft);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 220;

            // Flexible spacer
            var flexSpacer = new GameObject("FlexSpacer", typeof(RectTransform), typeof(LayoutElement));
            flexSpacer.transform.SetParent(panel.transform, false);
            flexSpacer.GetComponent<LayoutElement>().flexibleHeight = 1;

            // Version
            var versionLabel = UIHelper.CreateLabel(panel.transform, "v0.1.0",
                11, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var versionLE = versionLabel.gameObject.AddComponent<LayoutElement>();
            versionLE.preferredHeight = 20;

            // Spacer before button
            var spacer2 = new GameObject("Spacer2", typeof(RectTransform), typeof(LayoutElement));
            spacer2.transform.SetParent(panel.transform, false);
            spacer2.GetComponent<LayoutElement>().preferredHeight = 4;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14, Close);
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredHeight = 38;
        }

        // ================================================================
        // Close
        // ================================================================

        private void Close()
        {
            Hide();
            OnClose?.Invoke();
        }
    }
}
