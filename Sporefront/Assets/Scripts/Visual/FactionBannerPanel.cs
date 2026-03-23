// ============================================================================
// FILE: Visual/FactionBannerPanel.cs
// PURPOSE: Small clickable HUD banner showing the player's faction.
//          Positioned top-left below the resource bar. Clicking opens a
//          detail popup with full faction info, bonuses, and restrictions.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class FactionBannerPanel : MonoBehaviour
    {
        private GameObject panel;
        private Text bannerLabel;
        private GameObject detailBackdrop;
        private CanvasGroup backdropCG;
        private Coroutine fadeCoroutine;
        private FactionType currentFaction = FactionType.None;
        private RectTransform popupContentRT;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid localPlayerID, GameState state)
        {
            var player = state.GetPlayer(localPlayerID);
            if (player != null) currentFaction = player.faction;

            BuildBanner(canvasTransform);
            BuildDetailPopup(canvasTransform);

            // Hide until game actually starts (main menu has no player yet)
            if (panel != null) panel.SetActive(false);
        }

        public void ShowBanner()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void HideBanner()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void UpdateFaction(FactionType faction)
        {
            currentFaction = faction;
            if (bannerLabel != null)
                bannerLabel.text = currentFaction.DisplayName();

            // Rebuild popup content with actual faction data
            RebuildPopupContent();
        }

        // ================================================================
        // Banner (small top-left label)
        // ================================================================

        private void BuildBanner(Transform canvasTransform)
        {
            panel = UIHelper.CreatePanel(canvasTransform, "FactionBanner",
                SporefrontColors.ParchmentDark, UIHelper.SmallCornerRadius);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(UIConstants.SpaceSM, -84);
            rt.sizeDelta = new Vector2(180, 32);

            gameObject.transform.SetParent(panel.transform, false);

            // Faction name label
            bannerLabel = UIHelper.CreateLabel(panel.transform, currentFaction.DisplayName(),
                UIConstants.FontCaption, SporefrontColors.InkBlack, TextAnchor.MiddleCenter);
            var labelRT = bannerLabel.GetComponent<RectTransform>();
            UIHelper.StretchFull(labelRT);

            // Make the whole banner clickable
            var btn = panel.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UIHelper.CardButtonColors(SporefrontColors.ParchmentDark);
            btn.onClick.AddListener(() => Toggle());
        }

        // ================================================================
        // Detail Popup (centered parchment modal with faction info)
        // ================================================================

        private void BuildDetailPopup(Transform canvasTransform)
        {
            // 1. Semi-transparent backdrop (click to dismiss)
            detailBackdrop = UIHelper.CreatePanel(canvasTransform, "FactionDetailBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = detailBackdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            backdropCG = detailBackdrop.AddComponent<CanvasGroup>();
            var bdBtn = detailBackdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(() => Close());

            // 2. Centered parchment panel
            var modal = UIHelper.CreatePanel(detailBackdrop.transform, "FactionDetailModal",
                UIHelper.PanelParchmentBg);
            var modalRT = modal.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(modalRT, UIConstants.ModalSmallW, UIConstants.ModalSmallH);

            // 3. Tendril decoration
            PopupTendrilDecorator.Attach(modalRT);

            // 4. Click sink — prevents backdrop close when clicking panel
            var panelBtn = modal.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            // 5. Scroll content area (leaves 48px at bottom for close button)
            RectTransform contentRT;
            var scroll = UIHelper.CreateScrollView(modal.transform, "FactionScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 48);
            scrollRT.offsetMax = Vector2.zero;

            // Content padding
            var contentLayout = contentRT.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                int pad = (int)UIConstants.SpaceMD;
                contentLayout.padding = new RectOffset(pad, pad, pad, pad);
                contentLayout.spacing = UIConstants.SectionCardSpacing;
            }

            popupContentRT = contentRT;
            BuildPopupContent(popupContentRT);

            // 6. Close button pinned to bottom
            var closeBtn = UIHelper.CreateInkCloseButton(modal.transform, Close);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 46);

            // Start hidden
            detailBackdrop.SetActive(false);
        }

        private void RebuildPopupContent()
        {
            if (popupContentRT == null) return;

            // Clear existing content
            for (int i = popupContentRT.childCount - 1; i >= 0; i--)
                Destroy(popupContentRT.GetChild(i).gameObject);

            BuildPopupContent(popupContentRT);
        }

        private void BuildPopupContent(RectTransform contentRT)
        {
            var parent = contentRT;

            // Faction name header
            var header = UIHelper.CreateLabel(parent, currentFaction.DisplayName(),
                UIConstants.FontHeader, UIHelper.InkHeaderText, TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36;

            // Divider
            UIHelper.CreateDivider(parent, SporefrontColors.InkFaded);

            // Description
            UIHelper.CreateLabel(parent, currentFaction.Description(),
                UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.UpperLeft);

            // Divider
            UIHelper.CreateDivider(parent, SporefrontColors.InkFaded);

            // Faction Bonuses card
            var bonusCard = UIHelper.CreateLedgerCard(parent, "BonusCard");

            UIHelper.CreateLabel(bonusCard.transform, "Faction Bonuses",
                UIConstants.FontSmall, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);

            string bonusText = FormatBonusBullets(currentFaction.StartingBonusDescription());
            UIHelper.CreateLabel(bonusCard.transform, bonusText,
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.UpperLeft);

            // Research Restrictions card
            var restrictCard = UIHelper.CreateLedgerCard(parent, "RestrictCard");

            UIHelper.CreateLabel(restrictCard.transform, "Research Restrictions",
                UIConstants.FontSmall, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);

            UIHelper.CreateLabel(restrictCard.transform,
                currentFaction.ResearchRestrictionDescription(),
                UIConstants.FontCaption, UIHelper.InkSubText, TextAnchor.UpperLeft);
        }

        // ================================================================
        // Show / Close with Fade
        // ================================================================

        public void Show()
        {
            if (detailBackdrop == null) return;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            detailBackdrop.SetActive(true);
            fadeCoroutine = StartCoroutine(UIHelper.FadeIn(backdropCG));
        }

        public void Close()
        {
            if (detailBackdrop == null) return;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(UIHelper.FadeOut(backdropCG));
        }

        public void Hide()
        {
            Close();
        }

        public void Toggle()
        {
            if (detailBackdrop != null)
            {
                if (detailBackdrop.activeSelf) Close();
                else Show();
            }
        }

        public bool IsVisible => detailBackdrop != null && detailBackdrop.activeSelf;

        // ================================================================
        // Helpers
        // ================================================================

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
    }
}
