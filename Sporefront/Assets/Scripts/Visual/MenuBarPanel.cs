// ============================================================================
// FILE: Visual/MenuBarPanel.cs
// PURPOSE: [DEPRECATED] Replaced by TendrilWheelHUD.cs
//          Previously: Bottom navigation bar with 7 tabs for overview panels.
//          Kept for reference only — no longer instantiated by UIManager.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    [System.Obsolete("Replaced by TendrilWheelHUD. Kept for reference.")]
    public class MenuBarPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnEntitiesClicked;
        public event Action OnBuildingsClicked;
        public event Action OnCommandersClicked;
        public event Action OnMilitaryClicked;
        public event Action OnResourcesClicked;
        public event Action OnResearchClicked;
        public event Action OnTrainingClicked;
        public event Action OnCombatClicked;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private HUDTendrilAnimator hudAnimator;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full width, 48px, anchored bottom
            panel = UIHelper.CreatePanel(canvasTransform, "BottomNavBar", UIHelper.HudBg, 0);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(0, 72);

            var tendrilGO = new GameObject("HUDTendrilLayer", typeof(RectTransform), typeof(CanvasRenderer));
            tendrilGO.transform.SetParent(panel.transform, false);
            UIHelper.StretchFull(tendrilGO.GetComponent<RectTransform>());
            tendrilGO.transform.SetAsFirstSibling();
            var hudRenderer = tendrilGO.AddComponent<UITendrilRenderer>();
            hudRenderer.raycastTarget = false;
            hudAnimator = tendrilGO.AddComponent<HUDTendrilAnimator>();
            UIHelper.AddParchmentOverlay(panel.transform, 0.20f).transform.SetSiblingIndex(1);
            hudAnimator.barHeight = 72f;
            hudAnimator.trunkYFrac         = -0.5f;  // trunk at bottom edge
            hudAnimator.longLimbRightFrac  = 0.15f;  // rightmost 15%: longer limbs
            hudAnimator.longLimbMultiplier = 4.2f;   // right-zone limbs 4.2x base length
            hudAnimator.limbSpacing        = 40f;
            hudAnimator.limbZoneHalfFrac   = 0.48f;
            hudAnimator.subBranchesPerLimb    = 4;
            hudAnimator.limbLengthMultiplier   = 1.5f;
            hudAnimator.subBranchStrandCount  = 8;
            hudAnimator.subBranchLengthFrac   = 0.35f;
            hudAnimator.cornerArmCount     = 0;
            // Light warm colors for visibility on dark HUD background
            hudAnimator.trunkColor     = SporefrontColors.ParchmentShadow;
            hudAnimator.limbColor      = SporefrontColors.ParchmentMid;
            hudAnimator.subBranchColor = SporefrontColors.ParchmentShadow;
            hudAnimator.trunkAlpha     = 0.90f;
            hudAnimator.limbAlpha      = 0.80f;
            hudAnimator.subBranchAlpha = 0.65f;
            hudAnimator.longLimbSubBranchCount = 15;
            hudAnimator.Initialize(hudRenderer, rt);

            // Scrim: gradient from opaque at bottom edge to transparent at top
            var scrimGO = new GameObject("Scrim", typeof(RectTransform));
            scrimGO.transform.SetParent(panel.transform, false);
            UIHelper.StretchFull(scrimGO.GetComponent<RectTransform>());
            var scrimGrad = scrimGO.AddComponent<GradientImage>();
            var scrimBase = SporefrontColors.BgDeep;
            scrimGrad.topColor    = new Color(scrimBase.r, scrimBase.g, scrimBase.b, 0f);
            scrimGrad.bottomColor = new Color(scrimBase.r, scrimBase.g, scrimBase.b, 0.80f);
            scrimGrad.raycastTarget = false;

            // Horizontal layout
            var row = UIHelper.CreateHorizontalRow(panel.transform, 72f, 8f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(12, 12, 6, 0);
            row.childAlignment = TextAnchor.UpperCenter;

            // Navigation buttons (equal width via flexibleWidth)
            var entitiesBtn = CreateNavButton(row.transform, "Entities", () => OnEntitiesClicked?.Invoke());
            UIHelper.AddTooltip(entitiesBtn.gameObject, "View all armies and villager groups");
            var buildingsBtn = CreateNavButton(row.transform, "Buildings", () => OnBuildingsClicked?.Invoke());
            UIHelper.AddTooltip(buildingsBtn.gameObject, "View all buildings");
            var commandersBtn = CreateNavButton(row.transform, "Commanders", () => OnCommandersClicked?.Invoke());
            UIHelper.AddTooltip(commandersBtn.gameObject, "Manage commanders");
            var militaryBtn = CreateNavButton(row.transform, "Military", () => OnMilitaryClicked?.Invoke());
            UIHelper.AddTooltip(militaryBtn.gameObject, "Military overview and upgrades");
            var resourcesBtn = CreateNavButton(row.transform, "Resources", () => OnResourcesClicked?.Invoke());
            UIHelper.AddTooltip(resourcesBtn.gameObject, "Resource income and expenditure");
            var researchBtn = CreateNavButton(row.transform, "Research", () => OnResearchClicked?.Invoke());
            UIHelper.AddTooltip(researchBtn.gameObject, "Research tree and progress");
            var trainingBtn = CreateNavButton(row.transform, "Training", () => OnTrainingClicked?.Invoke());
            UIHelper.AddTooltip(trainingBtn.gameObject, "Active training queues");
            var combatBtn = CreateNavButton(row.transform, "Combat", () => OnCombatClicked?.Invoke());
            UIHelper.AddTooltip(combatBtn.gameObject, "Combat log and history");

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            panel.SetActive(true);
            if (hudAnimator != null)
            {
                if (hudAnimator.IsGrown) hudAnimator.SnapToGrown();
                else hudAnimator.StartAnimation();
            }
        }
        public void Hide() => panel.SetActive(false);
        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateNavButton(Transform parent, string text, Action onClick)
        {
            var bgDark = SporefrontColors.BgDeep;
            var btnBg = new Color(bgDark.r, bgDark.g, bgDark.b, 0.35f);
            var btn = UIHelper.CreateButton(parent, text,
                btnBg, SporefrontColors.ParchmentLight,
                UIConstants.FontSubheader, onClick);

            // Subtle dark pill background; warm amber glow on hover/press
            var a = SporefrontColors.SporeAmber;
            btn.colors = new ColorBlock
            {
                normalColor      = btnBg,
                highlightedColor = new Color(a.r, a.g, a.b, 0.20f),
                pressedColor     = new Color(a.r, a.g, a.b, 0.38f),
                selectedColor    = new Color(a.r, a.g, a.b, 0.14f),
                disabledColor    = Color.clear,
                colorMultiplier  = 1f,
                fadeDuration     = 0.12f
            };

            var btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                var glow = btnText.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                glow.effectDistance = new Vector2(4f, -4f);
            }

            btn.gameObject.AddComponent<HoverTextGlow>();

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 44;
            return btn;
        }
    }
}
