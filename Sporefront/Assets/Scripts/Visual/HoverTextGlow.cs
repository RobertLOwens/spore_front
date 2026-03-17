// ============================================================================
// FILE: Visual/HoverTextGlow.cs
// PURPOSE: Adds the same hover effect as the main menu — a soft white gradient
//          overlay that fades in behind the button's text on pointer enter.
//          Attach to any Button GameObject.
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class HoverTextGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RawImage overlay;

        // Shared 1×64 gradient texture across all instances (built once)
        private static Texture2D _gradientTex;
        private static Texture2D GradientTex
        {
            get
            {
                if (_gradientTex != null) return _gradientTex;

                _gradientTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
                _gradientTex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[64];
                for (int y = 0; y < 64; y++)
                {
                    float alpha;
                    if (y < 4)        // bottom ramp 0 → 0.28
                        alpha = Mathf.Lerp(0f, 0.28f, y / 3f);
                    else if (y < 60)  // body: constant 0.28
                        alpha = 0.28f;
                    else              // top ramp 0.28 → 0
                        alpha = Mathf.Lerp(0.28f, 0f, (y - 60) / 3f);
                    pixels[y] = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g, SporefrontColors.InkDark.b, alpha);
                }
                _gradientTex.SetPixels(pixels);
                _gradientTex.Apply();
                return _gradientTex;
            }
        }

        private void Awake()
        {
            // Add overlay as the first child so it renders behind the button's text
            var overlayGO = new GameObject("HoverOverlay",
                typeof(RectTransform), typeof(RawImage));
            overlayGO.transform.SetParent(transform, false);
            overlayGO.transform.SetAsFirstSibling();

            var rt = overlayGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            overlay = overlayGO.GetComponent<RawImage>();
            overlay.texture = GradientTex;
            overlay.enabled = false;
            overlay.raycastTarget = false;

            // Exclude from layout calculations
            overlayGO.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (overlay != null) overlay.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (overlay != null) overlay.enabled = false;
        }
    }
}
