// ============================================================================
// FILE: Visual/TooltipManager.cs
// PURPOSE: Lightweight shared tooltip — single GO, pointer-based show/hide
//          with configurable delay
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class TooltipManager : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        public static TooltipManager Instance { get; private set; }

        // ================================================================
        // State
        // ================================================================

        private GameObject tooltipGO;
        private Text tooltipText;
        private RectTransform tooltipRT;
        private Canvas parentCanvas;

        private string pendingText;
        private Vector2 pendingPosition;
        private float delayTimer;
        private bool isWaiting;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Canvas canvas)
        {
            Instance = this;
            parentCanvas = canvas;

            // Tooltip container
            tooltipGO = UIHelper.CreatePanel(canvas.transform, "Tooltip",
                SporefrontColors.InkDark);
            tooltipRT = tooltipGO.GetComponent<RectTransform>();
            tooltipRT.pivot = new Vector2(0f, 1f);
            tooltipRT.sizeDelta = new Vector2(200, 30);

            // Disable raycasts so tooltip doesn't block clicks
            var cg = tooltipGO.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Auto-size
            var csf = tooltipGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hlg = tooltipGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Label
            tooltipText = UIHelper.CreateLabel(tooltipGO.transform, "",
                UIConstants.FontSmall, SporefrontColors.ParchmentLight);
            var le = tooltipText.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 18;

            tooltipGO.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(string text, Vector2 screenPosition)
        {
            pendingText = text;
            pendingPosition = screenPosition;
            delayTimer = 0f;
            isWaiting = true;
            tooltipGO.SetActive(false);
        }

        public void Hide()
        {
            isWaiting = false;
            tooltipGO.SetActive(false);
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (!isWaiting) return;

            delayTimer += Time.unscaledDeltaTime;
            if (delayTimer < UIConstants.TooltipDelay) return;

            // Show tooltip
            isWaiting = false;
            tooltipText.text = pendingText;
            tooltipGO.SetActive(true);

            // Position near pointer
            var mouse = Mouse.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : pendingPosition;
            PositionTooltip(pos);
        }

        private void PositionTooltip(Vector2 screenPos)
        {
            if (parentCanvas == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.GetComponent<RectTransform>(), screenPos,
                parentCanvas.worldCamera, out localPoint);

            // Offset slightly below and right of cursor
            localPoint += new Vector2(12f, -12f);
            tooltipRT.localPosition = localPoint;
        }
    }
}
