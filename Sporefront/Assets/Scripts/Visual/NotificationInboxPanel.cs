// ============================================================================
// FILE: Visual/NotificationInboxPanel.cs
// PURPOSE: Modal panel showing scrollable notification history with mark-read,
//          clear-all, tap-to-jump, and relative timestamps.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class NotificationInboxPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<HexCoordinate?> OnNotificationTapped;
        public event Action OnMarkAllRead;
        public event Action OnClearAll;
        public event Action OnClose;

        // ================================================================
        // Types
        // ================================================================

        [System.Serializable]
        public class InboxEntry
        {
            public string icon;
            public string message;
            public string title;
            public float timestamp;       // Time.time when added
            public bool isRead;
            public HexCoordinate? coordinate;
            public int priority;
        }

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

        private List<InboxEntry> entries = new List<InboxEntry>();

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "InboxBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 400x500
            panel = UIHelper.CreatePanel(backdrop.transform, "InboxPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 400, 500);

            // Header bar
            var headerBar = UIHelper.CreatePanel(panel.transform, "HeaderBar",
                SporefrontColors.ParchmentDark);
            var headerBarRT = headerBar.GetComponent<RectTransform>();
            headerBarRT.anchorMin = new Vector2(0, 1);
            headerBarRT.anchorMax = new Vector2(1, 1);
            headerBarRT.pivot = new Vector2(0.5f, 1);
            headerBarRT.offsetMin = new Vector2(0, -44);
            headerBarRT.offsetMax = Vector2.zero;

            var headerHLG = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 4f;
            headerHLG.padding = new RectOffset(8, 8, 4, 4);
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = true;

            var titleLabel = UIHelper.CreateLabel(headerBar.transform, "Notifications",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var markReadBtn = UIHelper.CreateButton(headerBar.transform, "Read All",
                SporefrontColors.SporeTeal, UIHelper.HudTextColor, 10, () =>
                {
                    MarkAllAsRead();
                    OnMarkAllRead?.Invoke();
                    Rebuild();
                });
            var markReadLE = markReadBtn.gameObject.AddComponent<LayoutElement>();
            markReadLE.preferredWidth = 60;

            var clearBtn = UIHelper.CreateButton(headerBar.transform, "Clear",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 10, () =>
                {
                    ClearAllEntries();
                    OnClearAll?.Invoke();
                    Rebuild();
                });
            var clearLE = clearBtn.gameObject.AddComponent<LayoutElement>();
            clearLE.preferredWidth = 48;

            var closeBtn = UIHelper.CreateButton(headerBar.transform, "X",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredWidth = 28;

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "InboxScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = new Vector2(0, -48);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            Rebuild();
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh()
        {
            if (!backdrop.activeSelf) return;
            Rebuild();
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Notification Management
        // ================================================================

        public void AddNotification(GameNotificationType notification)
        {
            var entry = new InboxEntry
            {
                icon = notification.Icon,
                title = notification.NotificationTitle,
                message = notification.Message,
                timestamp = Time.time,
                isRead = false,
                coordinate = notification.Coordinate,
                priority = notification.Priority
            };
            entries.Insert(0, entry); // Newest first

            // Cap at 100 entries
            if (entries.Count > 100)
                entries.RemoveRange(100, entries.Count - 100);

            if (backdrop.activeSelf) Rebuild();
        }

        public void AddNotification(string title, string message, string icon,
            HexCoordinate? coordinate = null, int priority = 50)
        {
            var entry = new InboxEntry
            {
                icon = icon,
                title = title,
                message = message,
                timestamp = Time.time,
                isRead = false,
                coordinate = coordinate,
                priority = priority
            };
            entries.Insert(0, entry);

            if (entries.Count > 100)
                entries.RemoveRange(100, entries.Count - 100);

            if (backdrop.activeSelf) Rebuild();
        }

        public int GetUnreadCount()
        {
            int count = 0;
            foreach (var entry in entries)
            {
                if (!entry.isRead) count++;
            }
            return count;
        }

        public void MarkAllAsRead()
        {
            foreach (var entry in entries)
                entry.isRead = true;
        }

        public void ClearAllEntries()
        {
            entries.Clear();
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild()
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            if (entries.Count == 0)
            {
                BuildEmptyState();
                return;
            }

            foreach (var entry in entries)
            {
                BuildNotificationRow(entry);
            }
        }

        // ================================================================
        // Empty State
        // ================================================================

        private void BuildEmptyState()
        {
            // Spacer to center the message
            var spacer = UIHelper.CreatePanel(contentRT, "Spacer", Color.clear);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 160;

            var emptyLabel = UIHelper.CreateLabel(contentRT,
                "No notifications yet", UIHelper.DefaultHeaderFontSize,
                SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
            emptyLE.preferredHeight = 40;
        }

        // ================================================================
        // Notification Row
        // ================================================================

        private void BuildNotificationRow(InboxEntry entry)
        {
            Color rowBg = entry.isRead
                ? Color.clear
                : new Color(SporefrontColors.ParchmentMid.r,
                    SporefrontColors.ParchmentMid.g,
                    SporefrontColors.ParchmentMid.b, 0.5f);

            var row = UIHelper.CreatePanel(contentRT, "NotifRow", rowBg);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 56;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Unread dot indicator
            if (!entry.isRead)
            {
                var dotGO = UIHelper.CreatePanel(row.transform, "UnreadDot",
                    SporefrontColors.SporeRed);
                var dotLE = dotGO.AddComponent<LayoutElement>();
                dotLE.preferredWidth = 8;
                dotLE.preferredHeight = 8;
            }
            else
            {
                // Invisible spacer to keep alignment
                var dotSpacer = UIHelper.CreatePanel(row.transform, "DotSpacer", Color.clear);
                var dotSpacerLE = dotSpacer.AddComponent<LayoutElement>();
                dotSpacerLE.preferredWidth = 8;
            }

            // Icon label
            var iconLabel = UIHelper.CreateLabel(row.transform,
                GetIconSymbol(entry.icon), 16, GetIconColor(entry.icon),
                TextAnchor.MiddleCenter);
            var iconLE = iconLabel.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 28;

            // Text column
            var textCol = UIHelper.CreatePanel(row.transform, "TextCol", Color.clear);
            var textColLE = textCol.AddComponent<LayoutElement>();
            textColLE.flexibleWidth = 1;

            var textVLG = textCol.AddComponent<VerticalLayoutGroup>();
            textVLG.spacing = 1;
            textVLG.childForceExpandWidth = true;
            textVLG.childForceExpandHeight = false;
            textVLG.childControlWidth = true;
            textVLG.childControlHeight = false;

            // Title
            var titleLabel = UIHelper.CreateLabel(textCol.transform, entry.title, 12,
                entry.isRead ? SporefrontColors.InkLight : UIHelper.BodyTextColor);
            titleLabel.fontStyle = entry.isRead ? FontStyle.Normal : FontStyle.Bold;
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 18;

            // Message
            var msgLabel = UIHelper.CreateLabel(textCol.transform, entry.message, 11,
                SporefrontColors.InkLight);
            var msgLE = msgLabel.gameObject.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 16;

            // Right column: time + location pin
            var rightCol = UIHelper.CreatePanel(row.transform, "RightCol", Color.clear);
            var rightColLE = rightCol.AddComponent<LayoutElement>();
            rightColLE.preferredWidth = 55;

            var rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVLG.spacing = 2;
            rightVLG.childForceExpandWidth = true;
            rightVLG.childForceExpandHeight = false;
            rightVLG.childControlWidth = true;
            rightVLG.childControlHeight = false;
            rightVLG.childAlignment = TextAnchor.MiddleRight;

            // Relative time
            string timeStr = FormatRelativeTime(entry.timestamp);
            var timeLabel = UIHelper.CreateLabel(rightCol.transform, timeStr, 10,
                SporefrontColors.InkFaded, TextAnchor.MiddleRight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredHeight = 16;

            // Location pin
            if (entry.coordinate.HasValue)
            {
                var pinLabel = UIHelper.CreateLabel(rightCol.transform, "[loc]", 10,
                    SporefrontColors.SporeTeal, TextAnchor.MiddleRight);
                var pinLE = pinLabel.gameObject.AddComponent<LayoutElement>();
                pinLE.preferredHeight = 16;
            }

            // Click handler: mark read + jump to location
            var capturedEntry = entry;
            var btn = row.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                capturedEntry.isRead = true;
                OnNotificationTapped?.Invoke(capturedEntry.coordinate);
                if (capturedEntry.coordinate.HasValue)
                    Hide();
                else
                    Rebuild();
            });

            UIHelper.CreateDivider(contentRT, null, 1);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private string FormatRelativeTime(float timestamp)
        {
            float elapsed = Time.time - timestamp;

            if (elapsed < 60f)
                return $"{(int)elapsed}s ago";
            if (elapsed < 3600f)
                return $"{(int)(elapsed / 60f)}m ago";
            if (elapsed < 86400f)
                return $"{(int)(elapsed / 3600f)}h ago";

            return $"{(int)(elapsed / 86400f)}d ago";
        }

        private string GetIconSymbol(string icon)
        {
            switch (icon)
            {
                case "combat": return "!";
                case "alert": return "!";
                case "sighted": return "?";
                case "building_completed": return "B";
                case "upgrade": return "U";
                case "training": return "T";
                case "research": return "R";
                case "warning": return "W";
                case "entrenchment": return "E";
                default:
                    if (icon == "W" || icon == "F" || icon == "S" || icon == "O")
                        return icon;
                    return "*";
            }
        }

        private Color GetIconColor(string icon)
        {
            switch (icon)
            {
                case "combat":
                case "alert":
                case "warning":
                    return SporefrontColors.SporeRed;
                case "sighted":
                    return SporefrontColors.SporeAmber;
                case "building_completed":
                case "upgrade":
                case "training":
                case "entrenchment":
                    return SporefrontColors.SporeAmber;
                case "research":
                    return SporefrontColors.SporeTeal;
                default:
                    return SporefrontColors.SporeGreen;
            }
        }
    }
}
