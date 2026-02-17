// ============================================================================
// FILE: Visual/NotificationPanel.cs
// PURPOSE: Toast notification banners for game events (war_ios#16)
//          Priority queue, deduplication, auto-dismiss, click-to-focus
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class NotificationPanel : MonoBehaviour
    {
        // ================================================================
        // Types
        // ================================================================

        private class NotificationEntry
        {
            public string key;
            public string title;
            public string message;
            public Color accentColor;
            public HexCoordinate? coordinate;
            public int priority;
            public float displayTime;
            public float elapsed;
            public GameObject bannerGO;
        }

        // ================================================================
        // State
        // ================================================================

        private Transform container;
        private List<NotificationEntry> activeBanners = new List<NotificationEntry>();
        private Dictionary<string, float> deduplicationCooldowns = new Dictionary<string, float>();

        private const float AutoDismissTime = 4f;
        private const float DeduplicationCooldown = 10f;
        private const int MaxVisibleBanners = 4;
        private const float BannerHeight = 50f;
        private const float BannerWidth = 300f;

        public event Action<HexCoordinate> OnNotificationClicked;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Container: top-right, below resource bar
            var containerGO = new GameObject("NotificationContainer", typeof(RectTransform));
            containerGO.transform.SetParent(canvasTransform, false);
            container = containerGO.transform;

            var rt = containerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-10, -50); // Below resource bar
            rt.sizeDelta = new Vector2(BannerWidth, 300);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void ShowNotification(string title, string message, Color accentColor,
            HexCoordinate? coordinate = null, int priority = 50, string deduplicationKey = null)
        {
            string key = deduplicationKey ?? $"{title}_{message}";

            // Deduplication check
            if (deduplicationCooldowns.ContainsKey(key)) return;
            deduplicationCooldowns[key] = DeduplicationCooldown;

            // Remove oldest if at max
            while (activeBanners.Count >= MaxVisibleBanners)
            {
                RemoveBanner(activeBanners[0]);
            }

            var entry = new NotificationEntry
            {
                key = key,
                title = title,
                message = message,
                accentColor = accentColor,
                coordinate = coordinate,
                priority = priority,
                displayTime = AutoDismissTime,
                elapsed = 0f,
                bannerGO = CreateBanner(title, message, accentColor, coordinate)
            };

            // Insert sorted by priority (highest first)
            int insertIdx = activeBanners.Count;
            for (int i = 0; i < activeBanners.Count; i++)
            {
                if (entry.priority > activeBanners[i].priority)
                {
                    insertIdx = i;
                    break;
                }
            }
            activeBanners.Insert(insertIdx, entry);

            LayoutBanners();
        }

        public void ShowNotification(GameNotificationType notificationType)
        {
            Color accent = GetAccentColor(notificationType);
            ShowNotification(
                notificationType.NotificationTitle,
                notificationType.Message,
                accent,
                notificationType.Coordinate,
                notificationType.Priority,
                notificationType.DeduplicationKey
            );
        }

        // ================================================================
        // Update
        // ================================================================

        public void UpdateNotifications()
        {
            float dt = Time.deltaTime;

            // Update deduplication cooldowns
            var expiredKeys = new List<string>();
            var keys = new List<string>(deduplicationCooldowns.Keys);
            foreach (var key in keys)
            {
                deduplicationCooldowns[key] -= dt;
                if (deduplicationCooldowns[key] <= 0)
                    expiredKeys.Add(key);
            }
            foreach (var key in expiredKeys)
                deduplicationCooldowns.Remove(key);

            // Update active banners
            var toRemove = new List<NotificationEntry>();
            foreach (var entry in activeBanners)
            {
                entry.elapsed += dt;
                if (entry.elapsed >= entry.displayTime)
                    toRemove.Add(entry);
            }
            foreach (var entry in toRemove)
                RemoveBanner(entry);

            if (toRemove.Count > 0) LayoutBanners();
        }

        // ================================================================
        // Banner Creation
        // ================================================================

        private GameObject CreateBanner(string title, string message, Color accent, HexCoordinate? coord)
        {
            var banner = UIHelper.CreatePanel(container, "Banner", UIHelper.HudBg);
            var bannerRT = banner.GetComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0, 1);
            bannerRT.anchorMax = new Vector2(1, 1);
            bannerRT.pivot = new Vector2(0.5f, 1f);
            bannerRT.sizeDelta = new Vector2(0, BannerHeight);

            // Left accent bar
            var accentBar = UIHelper.CreatePanel(banner.transform, "Accent", accent);
            var accentRT = accentBar.GetComponent<RectTransform>();
            accentRT.anchorMin = Vector2.zero;
            accentRT.anchorMax = new Vector2(0, 1);
            accentRT.offsetMin = Vector2.zero;
            accentRT.offsetMax = new Vector2(4, 0);

            // Title
            var titleLabel = UIHelper.CreateLabel(banner.transform, title, 12,
                UIHelper.HudTextColor, TextAnchor.UpperLeft);
            titleLabel.fontStyle = FontStyle.Bold;
            var titleRT = titleLabel.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(10, 0);
            titleRT.offsetMax = new Vector2(-8, -4);

            // Message
            var msgLabel = UIHelper.CreateLabel(banner.transform, message, 11,
                new Color(UIHelper.HudTextColor.r, UIHelper.HudTextColor.g,
                    UIHelper.HudTextColor.b, 0.8f), TextAnchor.LowerLeft);
            var msgRT = msgLabel.GetComponent<RectTransform>();
            msgRT.anchorMin = Vector2.zero;
            msgRT.anchorMax = new Vector2(1, 0.5f);
            msgRT.offsetMin = new Vector2(10, 4);
            msgRT.offsetMax = new Vector2(-8, 0);

            // Click handler
            if (coord.HasValue)
            {
                var btn = banner.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var capturedCoord = coord.Value;
                btn.onClick.AddListener(() => OnNotificationClicked?.Invoke(capturedCoord));
            }

            return banner;
        }

        // ================================================================
        // Layout
        // ================================================================

        private void LayoutBanners()
        {
            float y = 0f;
            foreach (var entry in activeBanners)
            {
                if (entry.bannerGO == null) continue;
                var rt = entry.bannerGO.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, -y);
                y += BannerHeight + 4f;
            }
        }

        private void RemoveBanner(NotificationEntry entry)
        {
            if (entry.bannerGO != null) Destroy(entry.bannerGO);
            activeBanners.Remove(entry);
        }

        // ================================================================
        // Accent Color Mapping
        // ================================================================

        private Color GetAccentColor(GameNotificationType type)
        {
            if (type is BuildingCompletedNotification || type is UpgradeCompletedNotification
                || type is TrainingCompletedNotification || type is EntrenchmentCompletedNotification)
                return SporefrontColors.SporeAmber;

            if (type is ArmyAttackedNotification || type is VillagerAttackedNotification)
                return SporefrontColors.SporeRed;

            if (type is ResourcePointDepletedNotification)
                return SporefrontColors.SporeRed;

            if (type is ResourcesMaxedNotification || type is GatheringCompletedNotification)
                return SporefrontColors.SporeGreen;

            if (type is ResearchCompletedNotification)
                return SporefrontColors.SporeTeal;

            if (type is ArmySightedNotification)
                return SporefrontColors.SporeAmber;

            return SporefrontColors.SporeAmber;
        }
    }
}
