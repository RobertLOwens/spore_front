// ============================================================================
// FILE: Visual/GameOverPanel.cs
// PURPOSE: Modal overlay showing victory/defeat results with statistics.
//          Port of GameOverViewController.swift
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    // ================================================================
    // Stats Data
    // ================================================================

    [Serializable]
    public struct GameOverStats
    {
        public float timePlayed;        // seconds
        public int battlesWon;
        public int battlesLost;
        public int unitsKilled;
        public int unitsLost;
        public int buildingsBuilt;
        public int resourcesGathered;

        public static GameOverStats Empty => new GameOverStats();
    }

    // ================================================================
    // Panel
    // ================================================================

    public class GameOverPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnReturnToMenu;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "GameOverBackdrop",
                new Color(0, 0, 0, 0.6f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);

            // Center panel
            panel = UIHelper.CreatePanel(backdrop.transform, "GameOverPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 420, 520);

            // ScrollView inside panel
            var scroll = UIHelper.CreateScrollView(panel.transform, "GameOverScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 50); // Space for return button
            scrollRT.offsetMax = Vector2.zero;

            // Return to menu button
            var returnBtn = UIHelper.CreateButton(panel.transform, "Return to Menu",
                SporefrontColors.SporeAmber, UIHelper.ButtonText, 14,
                () => OnReturnToMenu?.Invoke());
            var returnBtnRT = returnBtn.GetComponent<RectTransform>();
            returnBtnRT.anchorMin = new Vector2(0, 0);
            returnBtnRT.anchorMax = new Vector2(1, 0);
            returnBtnRT.pivot = new Vector2(0.5f, 0);
            returnBtnRT.offsetMin = new Vector2(16, 10);
            returnBtnRT.offsetMax = new Vector2(-16, 46);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(bool isVictory, string reason, GameOverStats stats)
        {
            Rebuild(isVictory, reason, stats);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild Content
        // ================================================================

        private void Rebuild(bool isVictory, string reason, GameOverStats stats)
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Victory/Defeat header
            Color headerColor = isVictory ? SporefrontColors.SporeGreen : SporefrontColors.SporeRed;
            string headerText = isVictory ? "VICTORY" : "DEFEAT";

            var header = UIHelper.CreateLabel(contentRT, headerText,
                28, headerColor, TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 50;

            UIHelper.CreateDivider(contentRT, headerColor, 2f);

            // Spacer
            AddSpacer(contentRT, 8f);

            // Reason text
            var reasonLabel = UIHelper.CreateLabel(contentRT, reason,
                UIHelper.DefaultBodyFontSize, UIHelper.BodyTextColor,
                TextAnchor.MiddleCenter);
            reasonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            reasonLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var reasonLE = reasonLabel.gameObject.AddComponent<LayoutElement>();
            reasonLE.preferredHeight = 50;

            AddSpacer(contentRT, 8f);
            UIHelper.CreateDivider(contentRT);
            AddSpacer(contentRT, 8f);

            // Statistics section
            var statsHeader = UIHelper.CreateLabel(contentRT, "Statistics",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var statsHeaderLE = statsHeader.gameObject.AddComponent<LayoutElement>();
            statsHeaderLE.preferredHeight = 30;

            AddSpacer(contentRT, 4f);

            // Time played
            BuildStatRow("Time Played", FormatTime(stats.timePlayed));

            UIHelper.CreateDivider(contentRT, null, 1f);

            // Combat stats
            BuildStatRow("Battles Won", stats.battlesWon.ToString());
            BuildStatRow("Battles Lost", stats.battlesLost.ToString());

            UIHelper.CreateDivider(contentRT, null, 1f);

            // Unit stats
            BuildStatRow("Units Killed", stats.unitsKilled.ToString());
            BuildStatRow("Units Lost", stats.unitsLost.ToString());

            UIHelper.CreateDivider(contentRT, null, 1f);

            // Economy stats
            BuildStatRow("Buildings Built", stats.buildingsBuilt.ToString());
            BuildStatRow("Resources Gathered", stats.resourcesGathered.ToString());

            AddSpacer(contentRT, 10f);
        }

        // ================================================================
        // Stat Row
        // ================================================================

        private void BuildStatRow(string label, string value)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);

            var nameLabel = UIHelper.CreateLabel(row.transform, label, 13,
                SporefrontColors.InkMid, TextAnchor.MiddleLeft);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            var valueLabel = UIHelper.CreateLabel(row.transform, value, 13,
                SporefrontColors.InkBlack, TextAnchor.MiddleRight);
            valueLabel.fontStyle = FontStyle.Bold;
            var valueLE = valueLabel.gameObject.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 120;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void AddSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private string FormatTime(float totalSeconds)
        {
            int hours = Mathf.FloorToInt(totalSeconds / 3600f);
            int minutes = Mathf.FloorToInt((totalSeconds % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(totalSeconds % 60f);

            if (hours > 0)
                return $"{hours}h {minutes:D2}m {seconds:D2}s";
            else
                return $"{minutes}m {seconds:D2}s";
        }
    }
}
