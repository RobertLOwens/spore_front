// ============================================================================
// FILE: Visual/MenuBarPanel.cs
// PURPOSE: Top menu bar with navigation buttons for overview panels
//          Positioned below ResourceBar (y=-40), 32px tall
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

        public event Action OnResearchClicked;
        public event Action OnMilitaryClicked;
        public event Action OnBuildingsClicked;
        public event Action OnEntitiesClicked;
        public event Action OnSettingsClicked;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full width, 32px, anchored top below ResourceBar (which is 40px)
            panel = UIHelper.CreatePanel(canvasTransform, "MenuBar", UIHelper.HudBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -72); // 40 (resource bar) + 32 (this bar)
            rt.offsetMax = new Vector2(0, -40); // starts at y=-40

            // Horizontal layout
            var row = UIHelper.CreateHorizontalRow(panel.transform, 32f, 4f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(8, 8, 2, 2);
            row.childAlignment = TextAnchor.MiddleLeft;

            // Navigation buttons
            CreateMenuBarButton(row.transform, "Research", () => OnResearchClicked?.Invoke());
            CreateMenuBarButton(row.transform, "Military", () => OnMilitaryClicked?.Invoke());
            CreateMenuBarButton(row.transform, "Buildings", () => OnBuildingsClicked?.Invoke());
            CreateMenuBarButton(row.transform, "Entities", () => OnEntitiesClicked?.Invoke());

            // Flexible spacer to push Settings to right
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(row.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            CreateMenuBarButton(row.transform, "Settings", () => OnSettingsClicked?.Invoke());

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

        private Button CreateMenuBarButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                12, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 80;
            le.preferredHeight = 28;
            return btn;
        }
    }
}
