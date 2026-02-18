// ============================================================================
// FILE: Visual/MenuBarPanel.cs
// PURPOSE: Bottom navigation bar with 7 tabs for overview panels
//          Anchored at screen bottom, full width, 48px tall
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
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

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

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
            rt.offsetMax = new Vector2(0, 48);

            // Horizontal layout
            var row = UIHelper.CreateHorizontalRow(panel.transform, 48f, 4f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(8, 8, 4, 4);
            row.childAlignment = TextAnchor.MiddleCenter;

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

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show() => panel.SetActive(true);
        public void Hide() => panel.SetActive(false);
        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateNavButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                12, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 40;
            return btn;
        }
    }
}
