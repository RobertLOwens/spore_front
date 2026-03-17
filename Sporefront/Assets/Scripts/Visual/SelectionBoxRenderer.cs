// ============================================================================
// FILE: Visual/SelectionBoxRenderer.cs
// PURPOSE: Screen-space UI overlay for drag-select marquee rectangle
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class SelectionBoxRenderer : MonoBehaviour
    {
        private RectTransform boxRect;
        private Image boxImage;
        private Canvas canvas;
        private Vector2 startPos;
        private bool isActive;

        public void Initialize(Canvas parentCanvas)
        {
            canvas = parentCanvas;

            // Create selection box container
            var boxGO = new GameObject("SelectionBox");
            boxGO.transform.SetParent(canvas.transform, false);

            boxRect = boxGO.AddComponent<RectTransform>();
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = Vector2.zero;
            boxRect.pivot = Vector2.zero;

            // Nearly transparent dark fill
            boxImage = boxGO.AddComponent<Image>();
            boxImage.color = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g, SporefrontColors.InkDark.b, 0.08f);
            boxImage.raycastTarget = false;

            // Border using Outline component
            var outline = boxGO.AddComponent<Outline>();
            outline.effectColor = new Color(SporefrontColors.InkLight.r, SporefrontColors.InkLight.g, SporefrontColors.InkLight.b, 0.5f);
            outline.effectDistance = new Vector2(2f, 2f);

            boxGO.SetActive(false);
        }

        public void BeginSelection(Vector2 screenPos)
        {
            startPos = screenPos;
            isActive = true;
            boxRect.gameObject.SetActive(true);
            UpdateRect(screenPos);
        }

        public void UpdateSelection(Vector2 screenPos)
        {
            if (!isActive) return;
            UpdateRect(screenPos);
        }

        public Rect EndSelection()
        {
            if (!isActive) return Rect.zero;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 endPos = mouse != null ? (Vector2)mouse.position.ReadValue() : startPos;
            var rect = GetScreenRect(startPos, endPos);

            isActive = false;
            boxRect.gameObject.SetActive(false);
            return rect;
        }

        public void CancelSelection()
        {
            isActive = false;
            boxRect.gameObject.SetActive(false);
        }

        public bool IsActive => isActive;

        /// <summary>
        /// Returns the current drag rect in screen-space without ending the selection.
        /// Used for per-frame drag preview queries.
        /// </summary>
        public Rect GetCurrentScreenRect()
        {
            if (!isActive) return Rect.zero;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 currentPos = mouse != null ? (Vector2)mouse.position.ReadValue() : startPos;
            return GetScreenRect(startPos, currentPos);
        }

        private void UpdateRect(Vector2 currentPos)
        {
            var rect = GetScreenRect(startPos, currentPos);

            // Convert screen rect to canvas-local position
            Vector2 localStart;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, new Vector2(rect.x, rect.y),
                null, out localStart);

            Vector2 localEnd;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, new Vector2(rect.xMax, rect.yMax),
                null, out localEnd);

            boxRect.localPosition = localStart;
            boxRect.sizeDelta = localEnd - localStart;
        }

        private Rect GetScreenRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.x, b.x);
            float y = Mathf.Min(a.y, b.y);
            float w = Mathf.Abs(a.x - b.x);
            float h = Mathf.Abs(a.y - b.y);
            return new Rect(x, y, w, h);
        }
    }
}
