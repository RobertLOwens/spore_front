// ============================================================================
// FILE: Visual/ResourceBarPanel.cs
// PURPOSE: Top HUD bar showing local player's resources, rates, population,
//          notification icon with badge, and ellipsis dropdown menu
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class ResourceBarPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnNotificationClicked;
        public event Action OnSettingsClicked;
        public event Action OnMainMenuClicked;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private Text woodLabel;
        private Text foodLabel;
        private Text stoneLabel;
        private Text oreLabel;
        private Text popLabel;
        private Text starvationLabel;

        private Button notificationButton;
        private Text badgeText;
        private GameObject badgeGO;

        private Text speedLabel;

        private Button ellipsisButton;
        private GameObject ellipsisDropdown;
        private Transform canvasRoot;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            canvasRoot = canvasTransform;

            // Panel: full width, 60px, anchored top
            panel = UIHelper.CreatePanel(canvasTransform, "ResourceBar", UIHelper.HudBg, 0);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -60);
            rt.offsetMax = new Vector2(0, 0);

            gameObject.transform.SetParent(panel.transform, false);

            // Horizontal layout
            var row = UIHelper.CreateHorizontalRow(panel.transform, 60f, 16f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(16, 16, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;

            // Resource labels (full names, wider)
            woodLabel = CreateResourceLabel(row.transform, "Wood", 160);
            UIHelper.AddTooltip(woodLabel.gameObject, "Wood: gathered from trees");
            foodLabel = CreateResourceLabel(row.transform, "Food", 160);
            UIHelper.AddTooltip(foodLabel.gameObject, "Food: gathered from farms and bushes");
            stoneLabel = CreateResourceLabel(row.transform, "Stone", 160);
            UIHelper.AddTooltip(stoneLabel.gameObject, "Stone: mined from quarries");
            oreLabel = CreateResourceLabel(row.transform, "Ore", 160);
            UIHelper.AddTooltip(oreLabel.gameObject, "Ore: mined from deposits");

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(row.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Population
            popLabel = UIHelper.CreateLabel(row.transform, "Pop: -/-", 16,
                UIHelper.HudTextColor, TextAnchor.MiddleRight);
            popLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 60);
            var popLE = popLabel.gameObject.AddComponent<LayoutElement>();
            popLE.preferredWidth = 140;

            // Starvation warning (hidden by default)
            starvationLabel = UIHelper.CreateLabel(row.transform, "STARVING", 16,
                SporefrontColors.SporeRed, TextAnchor.MiddleRight);
            starvationLabel.fontStyle = FontStyle.Bold;
            starvationLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 60);
            var starvLE = starvationLabel.gameObject.AddComponent<LayoutElement>();
            starvLE.preferredWidth = 100;
            starvationLabel.gameObject.SetActive(false);

            // Game speed indicator (hidden at normal speed)
            speedLabel = UIHelper.CreateLabel(row.transform, "", UIConstants.FontBody,
                SporefrontColors.SporeAmber, TextAnchor.MiddleRight);
            speedLabel.fontStyle = FontStyle.Bold;
            speedLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);
            var speedLE = speedLabel.gameObject.AddComponent<LayoutElement>();
            speedLE.preferredWidth = 60;
            speedLabel.gameObject.SetActive(false);

            // Notification button
            CreateNotificationButton(row.transform);
            UIHelper.AddTooltip(notificationButton.gameObject, "Notifications");

            // Ellipsis button
            CreateEllipsisButton(row.transform);

            // Ellipsis dropdown (on canvas for z-ordering)
            CreateEllipsisDropdown();

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

            UpdateResourceLabel(woodLabel, player, ResourceType.Wood);
            UpdateResourceLabel(foodLabel, player, ResourceType.Food);
            UpdateResourceLabel(stoneLabel, player, ResourceType.Stone);
            UpdateResourceLabel(oreLabel, player, ResourceType.Ore);

            int current, capacity;
            gameState.GetPopulationStats(localPlayerID, out current, out capacity);

            // Population capacity warning
            if (capacity > 0)
            {
                float ratio = (float)current / capacity;
                if (ratio >= 1f)
                {
                    popLabel.text = $"Pop: {current}/{capacity} FULL";
                    popLabel.color = SporefrontColors.SporeRed;
                }
                else if (ratio >= 0.9f)
                {
                    popLabel.text = $"Pop: {current}/{capacity} !";
                    popLabel.color = SporefrontColors.SporeAmber;
                }
                else
                {
                    popLabel.text = $"Pop: {current}/{capacity}";
                    popLabel.color = UIHelper.HudTextColor;
                }
            }
            else
            {
                popLabel.text = $"Pop: {current}/{capacity}";
                popLabel.color = UIHelper.HudTextColor;
            }

            // Starvation warning
            bool starving = player.GetResource(ResourceType.Food) <= 0;
            starvationLabel.gameObject.SetActive(starving);

            // Game speed indicator
            bool showSpeed = gameState.gameSpeed != 1.0;
            speedLabel.gameObject.SetActive(showSpeed);
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
        // Update — dismiss ellipsis dropdown on outside click
        // ================================================================

        private void Update()
        {
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
        // Creation Helpers
        // ================================================================

        private void CreateNotificationButton(Transform parent)
        {
            notificationButton = UIHelper.CreateButton(parent, "[M]",
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                14, () => OnNotificationClicked?.Invoke());
            var btnRT = notificationButton.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(44, 44);
            var le = notificationButton.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 44;
            le.preferredHeight = 44;

            // Badge (red circle with count)
            badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(notificationButton.transform, false);
            var badgeImg = badgeGO.GetComponent<Image>();
            badgeImg.color = SporefrontColors.SporeRed;
            badgeImg.sprite = UIHelper.GetRoundedRectSprite(10);
            badgeImg.type = Image.Type.Sliced;

            var badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(1, 1);
            badgeRT.anchorMax = new Vector2(1, 1);
            badgeRT.pivot = new Vector2(1, 1);
            badgeRT.anchoredPosition = new Vector2(4, 4);
            badgeRT.sizeDelta = new Vector2(20, 20);

            // Badge text
            var badgeTextGO = new GameObject("BadgeText", typeof(RectTransform), typeof(Text));
            badgeTextGO.transform.SetParent(badgeGO.transform, false);
            badgeText = badgeTextGO.GetComponent<Text>();
            badgeText.text = "0";
            badgeText.fontSize = 10;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = Color.white;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.font = UIHelper.BodyFont;
            var badgeTextRT = badgeTextGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(badgeTextRT);

            badgeGO.SetActive(false);
        }

        private void CreateEllipsisButton(Transform parent)
        {
            ellipsisButton = UIHelper.CreateButton(parent, "...",
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                18, () =>
                {
                    if (ellipsisDropdown != null)
                        ellipsisDropdown.SetActive(!ellipsisDropdown.activeSelf);
                });
            var btnRT = ellipsisButton.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(44, 44);
            var le = ellipsisButton.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 44;
            le.preferredHeight = 44;
        }

        private void CreateEllipsisDropdown()
        {
            ellipsisDropdown = UIHelper.CreatePanel(canvasRoot, "EllipsisDropdown", UIHelper.PanelBg);
            var rt = ellipsisDropdown.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-8, -64);
            rt.sizeDelta = new Vector2(160, 0);

            // Vertical layout
            var vlg = ellipsisDropdown.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = ellipsisDropdown.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Settings button
            var settingsBtn = UIHelper.CreateButton(ellipsisDropdown.transform, "Settings",
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                14, () =>
                {
                    ellipsisDropdown.SetActive(false);
                    OnSettingsClicked?.Invoke();
                });
            var sLE = settingsBtn.gameObject.AddComponent<LayoutElement>();
            sLE.preferredHeight = 36;

            // Main Menu button
            var menuBtn = UIHelper.CreateButton(ellipsisDropdown.transform, "Main Menu",
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                14, () =>
                {
                    ellipsisDropdown.SetActive(false);
                    OnMainMenuClicked?.Invoke();
                });
            var mLE = menuBtn.gameObject.AddComponent<LayoutElement>();
            mLE.preferredHeight = 36;

            ellipsisDropdown.SetActive(false);
        }

        private Text CreateResourceLabel(Transform parent, string name, float width)
        {
            var label = UIHelper.CreateLabel(parent, $"{name} ---", 16,
                UIHelper.HudTextColor, TextAnchor.MiddleLeft);
            var labelRT = label.GetComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(width, 60);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return label;
        }

        private void UpdateResourceLabel(Text label, PlayerState player, ResourceType type)
        {
            int amount = player.GetResource(type);
            double rate = player.GetCollectionRate(type);
            string name = type.DisplayName();

            string rateStr = "";
            if (rate > 0.01)
                rateStr = $" <color=#{ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeAmber)}>+{rate:F1}</color>";
            else if (rate < -0.01)
                rateStr = $" <color=#{ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeRed)}>{rate:F1}</color>";

            label.text = $"{name} {amount}{rateStr}";
            label.supportRichText = true;
        }
    }
}
