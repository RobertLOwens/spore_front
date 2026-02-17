// ============================================================================
// FILE: Visual/UIHelper.cs
// PURPOSE: Static UGUI creation utilities with Sporefront parchment/ink style
//          Font loading, element creators, slider snapping (#17)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public static class UIHelper
    {
        // ================================================================
        // Font Loading
        // ================================================================

        private static Font _bodyFont;
        private static Font _headerFont;

        public static Font BodyFont
        {
            get
            {
                if (_bodyFont == null)
                {
                    _bodyFont = Font.CreateDynamicFontFromOSFont("IM Fell English", DefaultBodyFontSize);
                    if (_bodyFont == null)
                        _bodyFont = Font.CreateDynamicFontFromOSFont("Arial", DefaultBodyFontSize);
                }
                return _bodyFont;
            }
        }

        public static Font HeaderFont
        {
            get
            {
                if (_headerFont == null)
                {
                    _headerFont = Font.CreateDynamicFontFromOSFont("MedievalSharp", DefaultHeaderFontSize);
                    if (_headerFont == null)
                        _headerFont = Font.CreateDynamicFontFromOSFont("Arial", DefaultHeaderFontSize);
                }
                return _headerFont;
            }
        }

        // ================================================================
        // Style Constants
        // ================================================================

        public const int DefaultBodyFontSize = 14;
        public const int DefaultHeaderFontSize = 18;

        public static readonly Color PanelBg = new Color(
            SporefrontColors.ParchmentMid.r,
            SporefrontColors.ParchmentMid.g,
            SporefrontColors.ParchmentMid.b, 0.95f);

        public static readonly Color HudBg = new Color(
            SporefrontColors.InkDark.r,
            SporefrontColors.InkDark.g,
            SporefrontColors.InkDark.b, 0.9f);

        public static readonly Color ButtonBg = SporefrontColors.ParchmentDark;
        public static readonly Color ButtonText = SporefrontColors.InkBlack;
        public static readonly Color BodyTextColor = SporefrontColors.InkDark;
        public static readonly Color HeaderTextColor = SporefrontColors.InkBlack;
        public static readonly Color HudTextColor = SporefrontColors.ParchmentLight;

        // ================================================================
        // Rounded Corner Sprites
        // ================================================================

        public const int PanelCornerRadius = 16;
        public const int ButtonCornerRadius = 12;
        public const int SmallCornerRadius = 6;

        private static readonly Dictionary<int, Sprite> _roundedRectCache = new Dictionary<int, Sprite>();

        public static Sprite GetRoundedRectSprite(int cornerRadius)
        {
            if (_roundedRectCache.TryGetValue(cornerRadius, out var cached))
                return cached;
            var sprite = CreateRoundedRectSprite(cornerRadius);
            _roundedRectCache[cornerRadius] = sprite;
            return sprite;
        }

        private static Sprite CreateRoundedRectSprite(int cornerRadius)
        {
            int size = cornerRadius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[size * size];
            float r = cornerRadius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    float dx = Mathf.Max(r - px, px - (size - r), 0f);
                    float dy = Mathf.Max(r - py, py - (size - r), 0f);

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            float border = cornerRadius;
            return Sprite.Create(tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        // ================================================================
        // Element Creators
        // ================================================================

        public static GameObject CreatePanel(Transform parent, string name, Color bgColor,
            int cornerRadius = -1)
        {
            int effectiveRadius = cornerRadius == -1 ? PanelCornerRadius : cornerRadius;

            // Auto-skip rounding for transparent overlays and near-black backdrops
            bool useRounding = effectiveRadius > 0
                && bgColor.a >= 0.01f
                && (bgColor.r + bgColor.g + bgColor.b) >= 0.05f;

            if (useRounding)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Shadow));
                go.transform.SetParent(parent, false);

                var img = go.GetComponent<Image>();
                img.color = bgColor;
                img.sprite = GetRoundedRectSprite(effectiveRadius);
                img.type = Image.Type.Sliced;

                var shadow = go.GetComponent<Shadow>();
                shadow.effectColor = new Color(
                    SporefrontColors.InkFaded.r,
                    SporefrontColors.InkFaded.g,
                    SporefrontColors.InkFaded.b, 0.25f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);

                return go;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
                go.transform.SetParent(parent, false);
                go.GetComponent<Image>().color = bgColor;

                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(
                    SporefrontColors.InkFaded.r,
                    SporefrontColors.InkFaded.g,
                    SporefrontColors.InkFaded.b, 0.3f);
                outline.effectDistance = new Vector2(2f, -2f);

                return go;
            }
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize = -1,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft, bool isHeader = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = isHeader ? HeaderFont : BodyFont;
            t.fontSize = fontSize > 0 ? fontSize : (isHeader ? DefaultHeaderFontSize : DefaultBodyFontSize);
            t.color = color ?? (isHeader ? HeaderTextColor : BodyTextColor);
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button CreateButton(Transform parent, string text,
            Color? bgColor = null, Color? textColor = null, int fontSize = -1,
            Action onClick = null)
        {
            var bg = bgColor ?? ButtonBg;
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bg;
            img.sprite = GetRoundedRectSprite(ButtonCornerRadius);
            img.type = Image.Type.Sliced;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bg;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(bg, Color.black, 0.15f);
            colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.5f);
            btn.colors = colors;

            // Button label
            var label = CreateLabel(go.transform, text,
                fontSize > 0 ? fontSize : DefaultBodyFontSize,
                textColor ?? ButtonText, TextAnchor.MiddleCenter);
            StretchFull(label.GetComponent<RectTransform>());

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            return btn;
        }

        public static HorizontalLayoutGroup CreateHorizontalRow(Transform parent, float height = 30f, float spacing = 4f)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            return hlg;
        }

        public static VerticalLayoutGroup CreateVerticalGroup(Transform parent, float spacing = 4f)
        {
            var go = new GameObject("VGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            return vlg;
        }

        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform contentRT)
        {
            // ScrollView root
            var scrollGO = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(parent, false);
            scrollGO.GetComponent<Image>().color = Color.clear;

            var scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport with rect-based clipping (RectMask2D works reliably with URP)
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = viewportGO.GetComponent<RectTransform>();
            StretchFull(vpRT);

            // Content container
            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewportGO.transform, false);

            contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0, 0);

            var csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            scrollRect.viewport = vpRT;
            scrollRect.content = contentRT;

            return scrollRect;
        }

        public static (Image bg, Image fill) CreateProgressBar(Transform parent, float height = 16f,
            Color? bgColor = null, Color? fillColor = null)
        {
            var bgGO = CreatePanel(parent, "ProgressBar", bgColor ?? SporefrontColors.InkFaded, SmallCornerRadius);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(0, height);
            var bgImg = bgGO.GetComponent<Image>();

            var fillGO = CreatePanel(bgGO.transform, "Fill", fillColor ?? SporefrontColors.SporeGreen, SmallCornerRadius);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0, 1);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.GetComponent<Image>();

            return (bgImg, fillImg);
        }

        /// <summary>
        /// Creates a slider with wholeNumbers=true by default for integer snapping (#17).
        /// </summary>
        public static Slider CreateSlider(Transform parent, float min, float max,
            bool wholeNumbers = true, Action<float> onChange = null)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 20);

            // Background
            var bgGO = CreatePanel(go.transform, "Background", SporefrontColors.InkFaded, SmallCornerRadius);
            var bgRT = bgGO.GetComponent<RectTransform>();
            StretchFull(bgRT);
            bgRT.offsetMin = new Vector2(0, 7);
            bgRT.offsetMax = new Vector2(0, -7);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
            StretchFull(fillAreaRT);
            fillAreaRT.offsetMin = new Vector2(5, 7);
            fillAreaRT.offsetMax = new Vector2(-5, -7);

            var fillGO = CreatePanel(fillAreaGO.transform, "Fill", SporefrontColors.SporeAmber, SmallCornerRadius);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0, 1);
            fillRT.sizeDelta = new Vector2(0, 0);

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGO.transform.SetParent(go.transform, false);
            var handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
            StretchFull(handleAreaRT);
            handleAreaRT.offsetMin = new Vector2(5, 0);
            handleAreaRT.offsetMax = new Vector2(-5, 0);

            var handleGO = CreatePanel(handleAreaGO.transform, "Handle", SporefrontColors.ParchmentDeep, SmallCornerRadius);
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(16, 0);

            // Wire slider
            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = min;

            if (onChange != null)
                slider.onValueChanged.AddListener((v) => onChange(v));

            return slider;
        }

        public static Image CreateDivider(Transform parent, Color? color = null, float height = 1f)
        {
            var go = CreatePanel(parent, "Divider",
                color ?? new Color(SporefrontColors.InkFaded.r, SporefrontColors.InkFaded.g,
                    SporefrontColors.InkFaded.b, 0.3f), 0);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, height);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;
            return go.GetComponent<Image>();
        }

        // ================================================================
        // Layout Helpers
        // ================================================================

        public static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void SetFixedSize(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
        }

        // ================================================================
        // Resource Helpers
        // ================================================================

        public static string ResourceIcon(Sporefront.Models.ResourceType type)
        {
            switch (type)
            {
                case Sporefront.Models.ResourceType.Wood: return "W";
                case Sporefront.Models.ResourceType.Food: return "F";
                case Sporefront.Models.ResourceType.Stone: return "S";
                case Sporefront.Models.ResourceType.Ore: return "O";
                default: return "?";
            }
        }
    }
}
