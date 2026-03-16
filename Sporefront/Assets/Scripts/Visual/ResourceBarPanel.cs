// ============================================================================
// FILE: Visual/ResourceBarPanel.cs
// PURPOSE: Parchment-strip resource bar — Wood, Food, Stone, Ore, Population,
//          game speed, notification bell, and ellipsis dropdown.
//          Styled as ink-on-parchment to match the Sporefront visual theme.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class ResourceBarPanel : MonoBehaviour
    {
        // ================================================================
        // Cached color hex strings
        // ================================================================

        private static string cachedGreenHex;
        private static string cachedRedHex;
        private static string cachedFadedHex;

        private static string GreenHex => cachedGreenHex ??
            (cachedGreenHex = ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeGreen));
        private static string RedHex => cachedRedHex ??
            (cachedRedHex = ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeRed));
        private static string FadedHex => cachedFadedHex ??
            (cachedFadedHex = ColorUtility.ToHtmlStringRGB(SporefrontColors.InkFaded));

        // ================================================================
        // Events (public API consumed by UIManager)
        // ================================================================

        public event Action OnNotificationClicked;
        public event Action OnCombatLogClicked;
        public event Action OnSettingsClicked;
        public event Action OnMainMenuClicked;

        // ================================================================
        // Per-resource UI struct
        // ================================================================

        private struct ResourceEntry
        {
            public GameObject root;
            public Image icon;
            public Image hoverBg;
            public Text valueLabel;
            public Text rateLabel;
            public bool isWarning;
        }

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

        // Resources
        private ResourceEntry woodEntry;
        private ResourceEntry foodEntry;
        private ResourceEntry stoneEntry;
        private ResourceEntry oreEntry;

        // Population
        private Image popIcon;
        private Text popCurrentLabel;
        private Text popSlashLabel;
        private Text popMaxLabel;

        // Speed
        private Text speedLabel;
        private GameObject speedContainer;

        // Notification
        private Button notificationButton;
        private Text badgeText;
        private GameObject badgeGO;

        // Ellipsis
        private Button ellipsisButton;
        private GameObject ellipsisDropdown;
        private Transform canvasRoot;

        // Warning pulse animation
        private readonly List<Image> warningIcons = new List<Image>();
        private float warningTimer;

        // Hover color constants
        private static readonly Color HoverBg = new Color(
            SporefrontColors.ParchmentDeep.r,
            SporefrontColors.ParchmentDeep.g,
            SporefrontColors.ParchmentDeep.b, 0.25f);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            canvasRoot = canvasTransform;

            // Root panel — transparent container spanning the top of the screen
            panel = new GameObject("ResourceBar", typeof(RectTransform));
            panel.transform.SetParent(canvasTransform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0, 1);
            panelRT.anchorMax = new Vector2(1, 1);
            panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.offsetMin = new Vector2(0, -85);
            panelRT.offsetMax = Vector2.zero;

            // Top tendril border — thin decorative ink lines across full width
            CreateTopTendrilBorder(panel.transform);

            // Parchment strip — centered, auto-sized to content
            var strip = CreateParchmentStrip(panel.transform);

            // ---- Resource entries with ink dividers ----
            woodEntry  = CreateResourceEntry(strip.transform, "Wood",  "wood");
            CreateInkDivider(strip.transform, 0.8f);
            foodEntry  = CreateResourceEntry(strip.transform, "Food",  "food");
            CreateInkDivider(strip.transform, -0.5f);
            stoneEntry = CreateResourceEntry(strip.transform, "Stone", "stone");
            CreateInkDivider(strip.transform, 1.2f);
            oreEntry   = CreateResourceEntry(strip.transform, "Ore",   "ore");

            // Flexible spacer — pushes population and controls to the right
            var spacer = new GameObject("FlexSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(strip.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Section divider before Population
            CreateSectionDivider(strip.transform);

            // Population
            CreatePopulationEntry(strip.transform);

            // Section divider before right-side controls
            CreateSectionDivider(strip.transform);

            // Game speed indicator (hidden at 1x)
            CreateSpeedIndicator(strip.transform);

            // Notification bell
            CreateNotificationButton(strip.transform);

            // Ellipsis menu
            CreateEllipsisButton(strip.transform);

            // Dropdown (parented to canvas for z-ordering)
            CreateEllipsisDropdown();

            gameObject.transform.SetParent(panel.transform, false);
            panel.SetActive(false);
        }

        // ================================================================
        // Show / Hide
        // ================================================================

        public void Show() => panel.SetActive(true);
        public void Hide() => panel.SetActive(false);
        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Refresh
        // ================================================================

        public void Refresh(GameState gameState, Guid localPlayerID)
        {
            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            var foodInfo = gameState.GetFoodConsumptionRate(localPlayerID);

            warningIcons.Clear();
            UpdateResourceEntry(ref woodEntry,  player, ResourceType.Wood);
            UpdateResourceEntry(ref foodEntry,  player, ResourceType.Food, foodInfo);
            UpdateResourceEntry(ref stoneEntry, player, ResourceType.Stone);
            UpdateResourceEntry(ref oreEntry,   player, ResourceType.Ore);

            // Population
            int current, capacity;
            gameState.GetPopulationStats(localPlayerID, out current, out capacity);
            popCurrentLabel.text = current.ToString();
            popMaxLabel.text = capacity.ToString();

            if (capacity > 0 && current >= capacity)
            {
                popCurrentLabel.color = SporefrontColors.SporeRed;
            }
            else if (capacity > 0 && (float)current / capacity >= 0.9f)
            {
                popCurrentLabel.color = SporefrontColors.SporeAmber;
            }
            else
            {
                popCurrentLabel.color = SporefrontColors.InkDark;
            }

            // Game speed
            bool showSpeed = gameState.gameSpeed != 1.0;
            speedContainer.SetActive(showSpeed);
            if (showSpeed)
                speedLabel.text = $"{gameState.gameSpeed:0.#}x";
        }

        // ================================================================
        // Notification Badge
        // ================================================================

        public void UpdateNotificationBadge(int unreadCount)
        {
            if (badgeGO == null) return;
            if (unreadCount <= 0)
            {
                badgeGO.SetActive(false);
            }
            else
            {
                badgeGO.SetActive(true);
                badgeText.text = unreadCount > 99 ? "99+" : unreadCount.ToString();
            }
        }

        // ================================================================
        // Update — warning pulse + dropdown dismiss
        // ================================================================

        private void Update()
        {
            // Warning icon pulse (0.85 → 0.5 over 2s, ease-in-out)
            if (warningIcons.Count > 0)
            {
                warningTimer += Time.unscaledDeltaTime;
                float t = (Mathf.Sin(warningTimer * Mathf.PI) + 1f) * 0.5f; // 0..1 over 2s
                float alpha = Mathf.Lerp(0.5f, 0.85f, t);
                for (int i = 0; i < warningIcons.Count; i++)
                {
                    if (warningIcons[i] != null)
                    {
                        var c = warningIcons[i].color;
                        warningIcons[i].color = new Color(c.r, c.g, c.b, alpha);
                    }
                }
            }

            // Dismiss ellipsis dropdown on outside click
            if (ellipsisDropdown != null && ellipsisDropdown.activeSelf
                && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var dropdownRT = ellipsisDropdown.GetComponent<RectTransform>();
                var ellipsisBtnRT = ellipsisButton.GetComponent<RectTransform>();
                Vector2 mousePos = Mouse.current.position.ReadValue();

                if (!RectTransformUtility.RectangleContainsScreenPoint(dropdownRT, mousePos)
                    && !RectTransformUtility.RectangleContainsScreenPoint(ellipsisBtnRT, mousePos))
                {
                    ellipsisDropdown.SetActive(false);
                }
            }
        }

        // ================================================================
        // Update Helpers
        // ================================================================

        private void UpdateResourceEntry(ref ResourceEntry entry, PlayerState player,
            ResourceType type, GameState.FoodConsumptionInfo? foodInfo = null)
        {
            int amount = player.GetResource(type);
            double rate = player.GetCollectionRate(type);

            if (type == ResourceType.Food && foodInfo.HasValue)
                rate -= foodInfo.Value.adjustedRate;

            // Value
            entry.valueLabel.text = amount.ToString();

            // Rate
            string rateText;
            Color rateColor;
            if (rate > 0.01)
            {
                rateText = $"+{rate:F1} /t";
                rateColor = SporefrontColors.SporeGreen;
            }
            else if (rate < -0.01)
            {
                rateText = $"{rate:F1} /t";
                rateColor = SporefrontColors.SporeRed;
            }
            else
            {
                rateText = "+0.0 /t";
                rateColor = SporefrontColors.InkFaded;
            }
            entry.rateLabel.text = rateText;
            entry.rateLabel.color = rateColor;

            // Warning state: negative rate
            bool warning = rate < -0.01;
            entry.isWarning = warning;
            entry.valueLabel.color = warning ? SporefrontColors.SporeRed : SporefrontColors.InkDark;

            if (warning)
                warningIcons.Add(entry.icon);
            else
                entry.icon.color = new Color(
                    SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                    SporefrontColors.InkDark.b, 0.85f);
        }

        // ================================================================
        // Parchment Strip
        // ================================================================

        private GameObject CreateParchmentStrip(Transform parent)
        {
            var go = new GameObject("ParchmentStrip", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            // Full-width edge-to-edge, pinned to top
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1);
            rt.anchorMax = new Vector2(1f, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -16);
            rt.offsetMin = new Vector2(0, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0, 0);

            // 10% whitespace padding inside each side
            int padH = Mathf.RoundToInt(Screen.width * 0.10f);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(padH, padH, 20, 11);
            hlg.spacing = 0;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Solid parchment background (ignores layout)
            var bgGO = new GameObject("ParchmentBg", typeof(RectTransform),
                typeof(Image), typeof(LayoutElement));
            bgGO.transform.SetParent(go.transform, false);
            bgGO.GetComponent<LayoutElement>().ignoreLayout = true;
            UIHelper.StretchFull(bgGO.GetComponent<RectTransform>());
            var bgImg = bgGO.GetComponent<Image>();
            var parch = SporefrontColors.ParchmentMid;
            bgImg.color = new Color(parch.r, parch.g, parch.b, 0.92f);
            bgImg.raycastTarget = true; // captures pointer for strip area
            bgGO.transform.SetAsFirstSibling();

            // Paper-grain noise overlay (ignores layout)
            var noiseSprite = ResourceIconGenerator.GetNoiseSprite();
            if (noiseSprite != null)
            {
                var noiseGO = new GameObject("Noise", typeof(RectTransform),
                    typeof(Image), typeof(LayoutElement));
                noiseGO.transform.SetParent(go.transform, false);
                noiseGO.GetComponent<LayoutElement>().ignoreLayout = true;
                UIHelper.StretchFull(noiseGO.GetComponent<RectTransform>());
                var noiseImg = noiseGO.GetComponent<Image>();
                noiseImg.sprite = noiseSprite;
                noiseImg.color = new Color(1f, 1f, 1f, 0.04f);
                noiseImg.type = Image.Type.Tiled;
                noiseImg.raycastTarget = false;
                noiseGO.transform.SetSiblingIndex(1);
            }

            // Bottom ink border line (ignores layout)
            var borderGO = new GameObject("BottomInkBorder", typeof(RectTransform),
                typeof(Image), typeof(LayoutElement));
            borderGO.transform.SetParent(go.transform, false);
            borderGO.GetComponent<LayoutElement>().ignoreLayout = true;
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(0, 0);
            borderRT.anchorMax = new Vector2(1, 0);
            borderRT.pivot = new Vector2(0.5f, 0f);
            borderRT.offsetMin = new Vector2(8, 2);
            borderRT.offsetMax = new Vector2(-8, 4);
            var borderImg = borderGO.GetComponent<Image>();
            borderImg.color = new Color(
                SporefrontColors.InkMid.r, SporefrontColors.InkMid.g,
                SporefrontColors.InkMid.b, 0.3f);
            borderImg.raycastTarget = false;

            return go;
        }

        // ================================================================
        // Top Tendril Border
        // ================================================================

        private void CreateTopTendrilBorder(Transform parent)
        {
            var go = new GameObject("TopTendrilBorder", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -18);
            rt.offsetMax = Vector2.zero;

            // Thick ink line
            CreateTendrilLine(go.transform, -2f, 2.5f, SporefrontColors.InkDark, 0.4f);
            // Thin ink line
            CreateTendrilLine(go.transform, -5f, 1.2f, SporefrontColors.InkMid, 0.4f);
            // Top edge line
            CreateTendrilLine(go.transform, 0f, 1.5f, SporefrontColors.InkBlack, 0.15f);
        }

        private void CreateTendrilLine(Transform parent, float yOffset, float thickness, Color color, float opacity)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, yOffset - thickness);
            rt.offsetMax = new Vector2(0, yOffset);
            var img = go.GetComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, opacity);
            img.raycastTarget = false;
        }

        // ================================================================
        // Resource Entry
        // ================================================================

        private ResourceEntry CreateResourceEntry(Transform parent, string name, string iconName)
        {
            // Root — has Image for hover background
            var root = new GameObject($"Res_{name}", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var hoverBg = root.GetComponent<Image>();
            hoverBg.color = Color.clear;
            hoverBg.raycastTarget = true;
            hoverBg.sprite = UIHelper.GetRoundedRectSprite(UIHelper.SmallCornerRadius);
            hoverBg.type = Image.Type.Sliced;

            // Flexible width so entries spread across the bar
            root.GetComponent<LayoutElement>().flexibleWidth = 1;

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 5, 5);
            hlg.spacing = 9;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // Icon
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGO.transform.SetParent(root.transform, false);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = ResourceIconGenerator.GetIcon(iconName);
            iconImg.color = new Color(
                SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                SporefrontColors.InkDark.b, 0.85f);
            iconImg.raycastTarget = false;
            var iconLE = iconGO.GetComponent<LayoutElement>();
            iconLE.preferredWidth = 25;
            iconLE.preferredHeight = 25;

            // Info column
            var info = new GameObject("Info", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(root.transform, false);
            var vlg = info.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 1;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Top row: name + value
            var topRow = new GameObject("TopRow", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            topRow.transform.SetParent(info.transform, false);
            var topHlg = topRow.GetComponent<HorizontalLayoutGroup>();
            topHlg.spacing = 5;
            topHlg.childAlignment = TextAnchor.LowerLeft;
            topHlg.childForceExpandWidth = false;
            topHlg.childForceExpandHeight = false;
            topHlg.childControlWidth = true;
            topHlg.childControlHeight = true;
            var topLE = topRow.GetComponent<LayoutElement>();
            topLE.preferredHeight = 25;

            // Name label (14px, IM Fell English, ink-light)
            var nameLabel = CreateThemedLabel(topRow.transform, name, 14,
                SporefrontColors.InkLight, false);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 18;

            // Value label (21px, MedievalSharp, ink-dark)
            var valueLabel = CreateThemedLabel(topRow.transform, "---", 21,
                SporefrontColors.InkDark, true);
            var valLE = valueLabel.gameObject.AddComponent<LayoutElement>();
            valLE.preferredHeight = 25;

            // Rate label (13px, IM Fell English italic, colored by sign)
            var rateLabel = CreateThemedLabel(info.transform, "+0.0 /t", 13,
                SporefrontColors.InkFaded, false);
            rateLabel.fontStyle = FontStyle.Italic;
            var rateLE = rateLabel.gameObject.AddComponent<LayoutElement>();
            rateLE.preferredHeight = 16;

            // Hover behavior
            AddHoverEffect(root, hoverBg, valueLabel);

            return new ResourceEntry
            {
                root = root,
                icon = iconImg,
                hoverBg = hoverBg,
                valueLabel = valueLabel,
                rateLabel = rateLabel,
                isWarning = false
            };
        }

        // ================================================================
        // Ink Dividers
        // ================================================================

        private void CreateInkDivider(Transform parent, float rotationDeg)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(
                SporefrontColors.InkLight.r, SporefrontColors.InkLight.g,
                SporefrontColors.InkLight.b, 0.35f);
            img.raycastTarget = false;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 1.5f;
            le.preferredHeight = 35;
            go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, rotationDeg);
        }

        private void CreateSectionDivider(Transform parent)
        {
            var go = new GameObject("SectionDivider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(
                SporefrontColors.InkMid.r, SporefrontColors.InkMid.g,
                SporefrontColors.InkMid.b, 0.3f);
            img.raycastTarget = false;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 1.5f;
            le.preferredHeight = 35;
            le.minWidth = 1.5f;
            var rt = go.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.Euler(0, 0, -0.5f);

            // Add horizontal margin via padding spacers
            var spacerL = new GameObject("SpL", typeof(RectTransform), typeof(LayoutElement));
            spacerL.transform.SetParent(parent, false);
            spacerL.transform.SetSiblingIndex(go.transform.GetSiblingIndex());
            spacerL.GetComponent<LayoutElement>().preferredWidth = 8;
            spacerL.GetComponent<LayoutElement>().preferredHeight = 1;

            var spacerR = new GameObject("SpR", typeof(RectTransform), typeof(LayoutElement));
            spacerR.transform.SetParent(parent, false);
            spacerR.GetComponent<LayoutElement>().preferredWidth = 8;
            spacerR.GetComponent<LayoutElement>().preferredHeight = 1;
        }

        // ================================================================
        // Population Entry
        // ================================================================

        private void CreatePopulationEntry(Transform parent)
        {
            var root = new GameObject("Population", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var bg = root.GetComponent<Image>();
            bg.color = Color.clear;
            bg.sprite = UIHelper.GetRoundedRectSprite(UIHelper.SmallCornerRadius);
            bg.type = Image.Type.Sliced;

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.spacing = 9;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // Pop icon (28px)
            var iconGO = new GameObject("PopIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGO.transform.SetParent(root.transform, false);
            popIcon = iconGO.GetComponent<Image>();
            popIcon.sprite = ResourceIconGenerator.GetIcon("population");
            popIcon.color = new Color(
                SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                SporefrontColors.InkDark.b, 0.85f);
            popIcon.raycastTarget = false;
            var iconLE = iconGO.GetComponent<LayoutElement>();
            iconLE.preferredWidth = 28;
            iconLE.preferredHeight = 28;

            // Info column
            var info = new GameObject("Info", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            info.transform.SetParent(root.transform, false);
            var vlg = info.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // "Pop" label
            var popNameLabel = CreateThemedLabel(info.transform, "Pop", 14,
                SporefrontColors.InkLight, false);
            var popNameLE = popNameLabel.gameObject.AddComponent<LayoutElement>();
            popNameLE.preferredHeight = 18;

            // Fraction row: current / max
            var fracRow = new GameObject("FracRow", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            fracRow.transform.SetParent(info.transform, false);
            var fracHlg = fracRow.GetComponent<HorizontalLayoutGroup>();
            fracHlg.spacing = 2;
            fracHlg.childAlignment = TextAnchor.LowerLeft;
            fracHlg.childForceExpandWidth = false;
            fracHlg.childForceExpandHeight = false;
            fracHlg.childControlWidth = true;
            fracHlg.childControlHeight = true;
            var fracLE = fracRow.GetComponent<LayoutElement>();
            fracLE.preferredHeight = 25;

            // Current
            popCurrentLabel = CreateThemedLabel(fracRow.transform, "0", 21,
                SporefrontColors.InkDark, true);
            var curLE = popCurrentLabel.gameObject.AddComponent<LayoutElement>();
            curLE.preferredHeight = 25;

            // Slash
            popSlashLabel = CreateThemedLabel(fracRow.transform, "/", 16,
                SporefrontColors.InkFaded, false);
            var slashLE = popSlashLabel.gameObject.AddComponent<LayoutElement>();
            slashLE.preferredHeight = 20;

            // Max
            popMaxLabel = CreateThemedLabel(fracRow.transform, "0", 18,
                SporefrontColors.InkLight, true);
            popMaxLabel.color = new Color(
                SporefrontColors.InkLight.r, SporefrontColors.InkLight.g,
                SporefrontColors.InkLight.b, 0.7f);
            var maxLE = popMaxLabel.gameObject.AddComponent<LayoutElement>();
            maxLE.preferredHeight = 23;

            // Hover on population
            AddSimpleHover(root, bg);
        }

        // ================================================================
        // Speed Indicator
        // ================================================================

        private void CreateSpeedIndicator(Transform parent)
        {
            speedContainer = new GameObject("SpeedContainer", typeof(RectTransform),
                typeof(LayoutElement));
            speedContainer.transform.SetParent(parent, false);
            var le = speedContainer.GetComponent<LayoutElement>();
            le.preferredWidth = 40;
            le.preferredHeight = 30;

            speedLabel = CreateThemedLabel(speedContainer.transform, "", 19,
                SporefrontColors.SporeAmber, true);
            speedLabel.fontStyle = FontStyle.Bold;
            speedLabel.alignment = TextAnchor.MiddleCenter;
            var labelRT = speedLabel.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            speedContainer.SetActive(false);
        }

        // ================================================================
        // Notification Button
        // ================================================================

        private void CreateNotificationButton(Transform parent)
        {
            var btnGO = new GameObject("NotifBtn", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);

            var img = btnGO.GetComponent<Image>();
            img.color = Color.clear;
            img.sprite = UIHelper.GetRoundedRectSprite(UIHelper.SmallCornerRadius);
            img.type = Image.Type.Sliced;

            notificationButton = btnGO.GetComponent<Button>();
            notificationButton.colors = HoverButtonColors();
            notificationButton.onClick.AddListener(() => OnNotificationClicked?.Invoke());

            var le = btnGO.GetComponent<LayoutElement>();
            le.preferredWidth = 36;
            le.preferredHeight = 36;

            // Bell label
            var bellLabel = CreateThemedLabel(btnGO.transform, "M", 14,
                SporefrontColors.InkMid, false);
            bellLabel.alignment = TextAnchor.MiddleCenter;
            var bellRT = bellLabel.GetComponent<RectTransform>();
            bellRT.anchorMin = Vector2.zero;
            bellRT.anchorMax = Vector2.one;
            bellRT.offsetMin = Vector2.zero;
            bellRT.offsetMax = Vector2.zero;
            bellLabel.raycastTarget = false;

            // Badge (red circle with count)
            badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(btnGO.transform, false);
            var badgeImg = badgeGO.GetComponent<Image>();
            badgeImg.color = SporefrontColors.SporeRed;
            badgeImg.sprite = UIHelper.GetRoundedRectSprite(10);
            badgeImg.type = Image.Type.Sliced;

            var badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(1, 1);
            badgeRT.anchorMax = new Vector2(1, 1);
            badgeRT.pivot = new Vector2(1, 1);
            badgeRT.anchoredPosition = new Vector2(4, 4);
            badgeRT.sizeDelta = new Vector2(18, 18);

            var badgeTextGO = new GameObject("BadgeText", typeof(RectTransform), typeof(Text));
            badgeTextGO.transform.SetParent(badgeGO.transform, false);
            badgeText = badgeTextGO.GetComponent<Text>();
            badgeText.text = "0";
            badgeText.fontSize = 9;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = Color.white;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.font = UIHelper.BodyFont;
            UIHelper.StretchFull(badgeTextGO.GetComponent<RectTransform>());

            badgeGO.SetActive(false);
        }

        // ================================================================
        // Ellipsis Button + Dropdown
        // ================================================================

        private void CreateEllipsisButton(Transform parent)
        {
            var btnGO = new GameObject("EllipsisBtn", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);

            var img = btnGO.GetComponent<Image>();
            img.color = Color.clear;
            img.sprite = UIHelper.GetRoundedRectSprite(UIHelper.SmallCornerRadius);
            img.type = Image.Type.Sliced;

            ellipsisButton = btnGO.GetComponent<Button>();
            ellipsisButton.colors = HoverButtonColors();
            ellipsisButton.onClick.AddListener(() =>
            {
                if (ellipsisDropdown != null)
                    ellipsisDropdown.SetActive(!ellipsisDropdown.activeSelf);
            });

            var le = btnGO.GetComponent<LayoutElement>();
            le.preferredWidth = 36;
            le.preferredHeight = 36;

            var dotsLabel = CreateThemedLabel(btnGO.transform, "...", 16,
                SporefrontColors.InkMid, false);
            dotsLabel.alignment = TextAnchor.MiddleCenter;
            var dotsRT = dotsLabel.GetComponent<RectTransform>();
            dotsRT.anchorMin = Vector2.zero;
            dotsRT.anchorMax = Vector2.one;
            dotsRT.offsetMin = Vector2.zero;
            dotsRT.offsetMax = Vector2.zero;
            dotsLabel.raycastTarget = false;
        }

        private void CreateEllipsisDropdown()
        {
            ellipsisDropdown = UIHelper.CreatePanel(canvasRoot, "EllipsisDropdown", UIHelper.PanelBg);
            var rt = ellipsisDropdown.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-8, -84);
            rt.sizeDelta = new Vector2(180, 0);

            var vlg = ellipsisDropdown.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = ellipsisDropdown.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Combat Log
            var combatLogBtn = UIHelper.CreateButton(ellipsisDropdown.transform, "Combat Log",
                SporefrontColors.BgSurface, SporefrontColors.ParchmentMid, 14, () =>
                {
                    ellipsisDropdown.SetActive(false);
                    OnCombatLogClicked?.Invoke();
                });
            var clLE = combatLogBtn.gameObject.AddComponent<LayoutElement>();
            clLE.preferredHeight = 38;

            // Settings
            var settingsBtn = UIHelper.CreateButton(ellipsisDropdown.transform, "Settings",
                SporefrontColors.BgSurface, SporefrontColors.ParchmentMid, 14, () =>
                {
                    ellipsisDropdown.SetActive(false);
                    OnSettingsClicked?.Invoke();
                });
            var sLE = settingsBtn.gameObject.AddComponent<LayoutElement>();
            sLE.preferredHeight = 38;

            // Main Menu
            var menuBtn = UIHelper.CreateButton(ellipsisDropdown.transform, "Main Menu",
                SporefrontColors.BgSurface, SporefrontColors.ParchmentMid, 14, () =>
                {
                    ellipsisDropdown.SetActive(false);
                    OnMainMenuClicked?.Invoke();
                });
            var mLE = menuBtn.gameObject.AddComponent<LayoutElement>();
            mLE.preferredHeight = 38;

            ellipsisDropdown.SetActive(false);
        }

        // ================================================================
        // Label Helper
        // ================================================================

        private static Text CreateThemedLabel(Transform parent, string text, int fontSize,
            Color color, bool isHeader)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = isHeader ? UIHelper.HeaderFont : UIHelper.BodyFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ================================================================
        // Hover Helpers
        // ================================================================

        private static void AddHoverEffect(GameObject target, Image hoverBg, Text valueLabel)
        {
            var trigger = target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                hoverBg.color = HoverBg;
                valueLabel.color = SporefrontColors.InkBlack;
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                hoverBg.color = Color.clear;
                // Value color restored on next Refresh()
            });
            trigger.triggers.Add(exit);
        }

        private static void AddSimpleHover(GameObject target, Image hoverBg)
        {
            var trigger = target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => hoverBg.color = HoverBg);
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => hoverBg.color = Color.clear);
            trigger.triggers.Add(exit);
        }

        private static ColorBlock HoverButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.clear;
            colors.highlightedColor = HoverBg;
            colors.pressedColor = new Color(HoverBg.r, HoverBg.g, HoverBg.b, 0.4f);
            colors.disabledColor = Color.clear;
            return colors;
        }
    }
}
