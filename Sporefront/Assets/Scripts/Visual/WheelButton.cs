// ============================================================================
// FILE: Visual/WheelButton.cs
// PURPOSE: Individual circular icon button for the Tendril Wheel HUD.
//          Handles hover, active/selected, pulse glow, pop-in animation,
//          key badge visibility, and label display.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    // ================================================================
    // Config
    // ================================================================

    public struct WheelButtonConfig
    {
        public string name;             // Display name ("Build", "Resources", etc.)
        public string shortcutLabel;    // Single key letter ("B", "R", etc.)
        public string iconResourcePath; // "Icons/Action/build" (no extension)
        public bool isActionButton;     // true = right wheel (red active), false = left wheel (amber)
    }

    // ================================================================
    // WheelButton
    // ================================================================

    public class WheelButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClicked;

        // ================================================================
        // State
        // ================================================================

        private WheelButtonConfig config;
        private bool isActive;
        private bool isHovered;
        private bool keyBadgeVisible;

        // Visual refs
        private Image circleImage;
        private Outline circleOutline;
        private Shadow circleShadow;
        private Image iconImage;
        private Text labelText;
        private Image labelBgImage;
        private GameObject keyBadgeGO;
        private RectTransform rootRT;

        // Animation
        private float currentScale = 1f;
        private float targetScale = 1f;
        private float labelAlpha;
        private float targetLabelAlpha;
        private CanvasGroup labelCanvasGroup;
        private float pulseTime;

        // Colors
        private static readonly Color DefaultBg = SporefrontColors.ParchmentMid;
        private static readonly Color HoverBg = SporefrontColors.ParchmentLight;
        private static readonly Color DefaultBorder = SporefrontColors.InkMid;
        private static readonly Color HoverBorder = SporefrontColors.InkDark;
        private static readonly Color DefaultIconTint = SporefrontColors.InkDark;
        private static readonly Color ActiveActionColor = SporefrontColors.SporeRed;
        private static readonly Color ActiveInfoColor = SporefrontColors.SporeAmber;
        private static readonly Color ShadowColor = new Color(0.102f, 0.086f, 0.067f, 0.35f);
        private static readonly Color ShadowHover = new Color(0.102f, 0.086f, 0.067f, 0.50f);

        // ================================================================
        // Public API
        // ================================================================

        public bool IsActive => isActive;
        public bool IsActionButton => config.isActionButton;

        public void Initialize(WheelButtonConfig cfg, bool labelOnLeft)
        {
            config = cfg;
            BuildVisualHierarchy(labelOnLeft);
        }

        public void SetActive(bool active)
        {
            isActive = active;
            UpdateVisuals();
        }

        public void ShowKeyBadge(bool show)
        {
            keyBadgeVisible = show;
            if (keyBadgeGO != null) keyBadgeGO.SetActive(show);
        }

        public void PlayPopIn(float delay)
        {
            rootRT.localScale = Vector3.zero;
            StartCoroutine(PopInCoroutine(delay));
        }

        public void FireClickEvent()
        {
            OnClicked?.Invoke();
        }

        // ================================================================
        // Visual Hierarchy
        // ================================================================

        private void BuildVisualHierarchy(bool labelOnLeft)
        {
            rootRT = GetComponent<RectTransform>();
            float btnSize = UIConstants.WheelButtonSize;

            // -- Circle background --
            var circleGO = new GameObject("Circle", typeof(RectTransform), typeof(Image),
                typeof(Outline), typeof(Shadow));
            circleGO.transform.SetParent(transform, false);

            var circleRT = circleGO.GetComponent<RectTransform>();
            circleRT.sizeDelta = new Vector2(btnSize, btnSize);
            circleRT.anchoredPosition = Vector2.zero;

            circleImage = circleGO.GetComponent<Image>();
            // cornerRadius = half of size for a perfect circle
            circleImage.sprite = UIHelper.GetRoundedRectSprite((int)(btnSize * 0.5f));
            circleImage.type = Image.Type.Sliced;
            circleImage.color = DefaultBg;

            circleOutline = circleGO.GetComponent<Outline>();
            circleOutline.effectColor = DefaultBorder;
            circleOutline.effectDistance = new Vector2(UIConstants.WheelButtonBorder,
                -UIConstants.WheelButtonBorder);

            circleShadow = circleGO.GetComponent<Shadow>();
            circleShadow.effectColor = ShadowColor;
            circleShadow.effectDistance = new Vector2(0f, -2f);

            // -- Icon --
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(circleGO.transform, false);

            var iconRT = iconGO.GetComponent<RectTransform>();
            float iconSize = UIConstants.WheelButtonIconSize;
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            iconRT.anchoredPosition = Vector2.zero;

            iconImage = iconGO.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>(config.iconResourcePath);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.preserveAspect = true;
            }
            iconImage.color = DefaultIconTint;
            iconImage.raycastTarget = false;

            // -- Key Badge --
            float badgeSize = UIConstants.WheelKeyBadgeSize;
            keyBadgeGO = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image));
            keyBadgeGO.transform.SetParent(circleGO.transform, false);

            var badgeRT = keyBadgeGO.GetComponent<RectTransform>();
            badgeRT.sizeDelta = new Vector2(badgeSize, badgeSize);
            badgeRT.anchorMin = new Vector2(1f, 0f);
            badgeRT.anchorMax = new Vector2(1f, 0f);
            badgeRT.pivot = new Vector2(1f, 0f);
            badgeRT.anchoredPosition = new Vector2(1f, -1f);

            var badgeImg = keyBadgeGO.GetComponent<Image>();
            badgeImg.sprite = UIHelper.GetRoundedRectSprite(3);
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = SporefrontColors.InkDark;
            badgeImg.raycastTarget = false;

            var badgeTextGO = new GameObject("BadgeText", typeof(RectTransform), typeof(Text));
            badgeTextGO.transform.SetParent(keyBadgeGO.transform, false);
            UIHelper.StretchFull(badgeTextGO.GetComponent<RectTransform>());

            var badgeText = badgeTextGO.GetComponent<Text>();
            badgeText.text = config.shortcutLabel;
            badgeText.font = Font.CreateDynamicFontFromOSFont("Courier New", 8);
            badgeText.fontSize = 8;
            badgeText.color = SporefrontColors.ParchmentMid;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.raycastTarget = false;

            keyBadgeGO.SetActive(false);

            // -- Label — parchment card tooltip --
            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
            labelGO.transform.SetParent(transform, false);

            var labelRT = labelGO.GetComponent<RectTransform>();
            float labelOffset = btnSize * 0.5f + 3f;

            if (labelOnLeft)
            {
                // Label appears to the left of the button (right wheel)
                labelRT.anchorMin = new Vector2(0f, 0.5f);
                labelRT.anchorMax = new Vector2(0f, 0.5f);
                labelRT.pivot = new Vector2(1f, 0.5f);
                labelRT.anchoredPosition = new Vector2(-labelOffset, 0f);
            }
            else
            {
                // Label appears to the right of the button (left wheel)
                labelRT.anchorMin = new Vector2(1f, 0.5f);
                labelRT.anchorMax = new Vector2(1f, 0.5f);
                labelRT.pivot = new Vector2(0f, 0.5f);
                labelRT.anchoredPosition = new Vector2(labelOffset, 0f);
            }

            labelRT.sizeDelta = new Vector2(160f, 44f);

            labelCanvasGroup = labelGO.GetComponent<CanvasGroup>();
            labelCanvasGroup.alpha = 0f;
            labelCanvasGroup.blocksRaycasts = false;
            labelCanvasGroup.interactable = false;

            // Parchment card background
            labelBgImage = labelGO.AddComponent<Image>();
            labelBgImage.sprite = UIHelper.GetRoundedRectSprite(6);
            labelBgImage.type = Image.Type.Sliced;
            labelBgImage.color = SporefrontColors.ParchmentMid;
            labelBgImage.raycastTarget = false;

            // Subtle ink border
            var labelBorder = labelGO.AddComponent<Outline>();
            labelBorder.effectColor = new Color(
                SporefrontColors.InkLight.r,
                SporefrontColors.InkLight.g,
                SporefrontColors.InkLight.b, 0.5f);
            labelBorder.effectDistance = new Vector2(1f, -1f);

            // Drop shadow for depth
            var labelDropShadow = labelGO.AddComponent<Shadow>();
            labelDropShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            labelDropShadow.effectDistance = new Vector2(0f, -2f);

            // Text child with horizontal padding
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(labelGO.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10f, 4f);   // left, bottom padding
            textRT.offsetMax = new Vector2(-10f, -4f);  // right, top padding

            labelText = textGO.GetComponent<Text>();
            labelText.text = config.name;
            labelText.font = UIHelper.BodyFont;
            labelText.fontSize = UIConstants.WheelLabelFontSize;
            labelText.color = SporefrontColors.InkDark;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.raycastTarget = false;
        }

        // ================================================================
        // Pointer Events
        // ================================================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisuals();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisuals();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }

        // ================================================================
        // Visual State
        // ================================================================

        private void UpdateVisuals()
        {
            Color activeColor = config.isActionButton ? ActiveActionColor : ActiveInfoColor;

            if (isActive)
            {
                targetScale = UIConstants.WheelButtonHoverScale;
                targetLabelAlpha = 1f;
                circleImage.color = HoverBg;
                circleOutline.effectColor = activeColor;
                iconImage.color = activeColor;

                // Parchment label: active color text, subtle tinted background
                labelText.color = activeColor;
                if (labelBgImage != null)
                    labelBgImage.color = Color.Lerp(
                        SporefrontColors.ParchmentMid, activeColor, 0.08f);
            }
            else if (isHovered)
            {
                targetScale = UIConstants.WheelButtonHoverScale;
                targetLabelAlpha = 1f;
                circleImage.color = HoverBg;
                circleOutline.effectColor = HoverBorder;
                iconImage.color = DefaultIconTint;

                // Parchment label: dark ink text on neutral parchment
                labelText.color = SporefrontColors.InkDark;
                if (labelBgImage != null)
                    labelBgImage.color = SporefrontColors.ParchmentMid;
            }
            else
            {
                targetScale = 1f;
                targetLabelAlpha = 0f;
                circleImage.color = DefaultBg;
                circleOutline.effectColor = DefaultBorder;
                iconImage.color = DefaultIconTint;

                labelText.color = SporefrontColors.InkDark;
                if (labelBgImage != null)
                    labelBgImage.color = SporefrontColors.ParchmentMid;
            }
        }

        // ================================================================
        // Animation Update
        // ================================================================

        private void Update()
        {
            // Smooth scale lerp
            float lerpSpeed = 1f / UIConstants.WheelHoverDuration;
            currentScale = Mathf.MoveTowards(currentScale, targetScale, Time.deltaTime * lerpSpeed);
            rootRT.localScale = Vector3.one * currentScale;

            // Smooth label alpha
            labelAlpha = Mathf.MoveTowards(labelAlpha, targetLabelAlpha, Time.deltaTime * lerpSpeed);
            if (labelCanvasGroup != null)
                labelCanvasGroup.alpha = labelAlpha;

            // Active pulse glow
            if (isActive && circleShadow != null)
            {
                pulseTime += Time.deltaTime;
                float pulse = Mathf.Sin(pulseTime * (2f * Mathf.PI / UIConstants.WheelPulseDuration));
                float glowAlpha = Mathf.Lerp(0.25f, 0.50f, (pulse + 1f) * 0.5f);
                Color activeColor = config.isActionButton ? ActiveActionColor : ActiveInfoColor;
                circleShadow.effectColor = new Color(activeColor.r, activeColor.g, activeColor.b, glowAlpha);
                circleShadow.effectDistance = new Vector2(0f, Mathf.Lerp(-2f, -4f, (pulse + 1f) * 0.5f));
            }
            else if (!isActive && circleShadow != null)
            {
                pulseTime = 0f;
                circleShadow.effectColor = isHovered ? ShadowHover : ShadowColor;
                circleShadow.effectDistance = new Vector2(0f, -2f);
            }
        }

        // ================================================================
        // Pop-In Animation
        // ================================================================

        private IEnumerator PopInCoroutine(float delay)
        {
            rootRT.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            float duration = UIConstants.WheelPopDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Cubic-bezier(0.34, 1.56, 0.64, 1) approximation — spring overshoot
                float s;
                if (t < 0.5f)
                {
                    float t2 = t * 2f;
                    s = t2 * t2 * (2.5f * t2 - 1.5f);
                }
                else
                {
                    float t2 = (t - 0.5f) * 2f;
                    s = 0.5f + t2 * (1f - t2 * 0.12f) * 0.5f;
                }

                // Clamp to overshoot range
                s = Mathf.Clamp(s, 0f, 1.15f);
                rootRT.localScale = Vector3.one * s;

                yield return null;
            }

            rootRT.localScale = Vector3.one;
            currentScale = 1f;
            targetScale = 1f;
        }
    }
}
