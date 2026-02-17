// ============================================================================
// FILE: Visual/SpectatorOverlay.cs
// PURPOSE: Non-modal transparent overlay for spectator mode showing player
//          stats, game time, speed controls, and exit button.
//          Port of SpectatorOverlayView.swift
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
    public class SpectatorOverlay : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<float> OnSpeedChanged;  // speed multiplier
        public event Action OnExit;

        // ================================================================
        // State
        // ================================================================

        private GameObject overlay;

        // Top bar references
        private Text player1NameLabel;
        private Text player1StatsLabel;
        private Text player2NameLabel;
        private Text player2StatsLabel;
        private Text gameTimeLabel;

        // Bottom bar references
        private List<Button> speedButtons = new List<Button>();
        private float currentSpeed = 1.0f;

        // Colors
        private static readonly Color Player1Color = new Color(0.4f, 0.6f, 1.0f, 1.0f);   // blue
        private static readonly Color Player2Color = new Color(1.0f, 0.4f, 0.4f, 1.0f);   // red
        private static readonly Color StatsTextColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        private static readonly Color TimeTextColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        private static readonly Color BarBgColor = new Color(0, 0, 0, 0.6f);
        private static readonly Color SpeedActiveColor = new Color(0.3f, 0.5f, 0.8f, 1.0f);
        private static readonly Color SpeedInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Transparent full-screen overlay (does not block clicks on areas without UI)
            overlay = new GameObject("SpectatorOverlay", typeof(RectTransform));
            overlay.transform.SetParent(canvasTransform, false);
            var overlayRT = overlay.GetComponent<RectTransform>();
            UIHelper.StretchFull(overlayRT);

            BuildTopBar();
            BuildBottomBar();

            overlay.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            currentSpeed = 1.0f;
            HighlightSpeedButton(1.0f);
            overlay.SetActive(true);
        }

        public void Hide()
        {
            overlay.SetActive(false);
        }

        public bool IsVisible => overlay != null && overlay.activeSelf;

        /// <summary>
        /// Update player stats and game time display.
        /// </summary>
        public void UpdateDisplay(
            string player1Name, int player1Food, int player1Wood, int player1Stone, int player1Armies, int player1Buildings,
            string player2Name, int player2Food, int player2Wood, int player2Stone, int player2Armies, int player2Buildings,
            float gameTime)
        {
            if (player1NameLabel != null)
                player1NameLabel.text = player1Name;
            if (player1StatsLabel != null)
                player1StatsLabel.text = string.Format("F:{0} W:{1} S:{2} A:{3} B:{4}",
                    player1Food, player1Wood, player1Stone, player1Armies, player1Buildings);

            if (player2NameLabel != null)
                player2NameLabel.text = player2Name;
            if (player2StatsLabel != null)
                player2StatsLabel.text = string.Format("F:{0} W:{1} S:{2} A:{3} B:{4}",
                    player2Food, player2Wood, player2Stone, player2Armies, player2Buildings);

            if (gameTimeLabel != null)
            {
                int minutes = (int)gameTime / 60;
                int seconds = (int)gameTime % 60;
                gameTimeLabel.text = string.Format("{0}:{1:D2}", minutes, seconds);
            }
        }

        // ================================================================
        // Top Bar
        // ================================================================

        private void BuildTopBar()
        {
            var topBar = UIHelper.CreatePanel(overlay.transform, "TopBar", BarBgColor);
            var topBarRT = topBar.GetComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0, 1);
            topBarRT.anchorMax = new Vector2(1, 1);
            topBarRT.pivot = new Vector2(0.5f, 1);
            topBarRT.offsetMin = new Vector2(8, -70);
            topBarRT.offsetMax = new Vector2(-8, 0);

            // Player 1 name (top-left)
            player1NameLabel = UIHelper.CreateLabel(topBar.transform, "Player 1",
                14, Player1Color, TextAnchor.MiddleLeft);
            player1NameLabel.fontStyle = FontStyle.Bold;
            var p1NameRT = player1NameLabel.GetComponent<RectTransform>();
            p1NameRT.anchorMin = new Vector2(0, 0.5f);
            p1NameRT.anchorMax = new Vector2(0.4f, 1);
            p1NameRT.offsetMin = new Vector2(12, 0);
            p1NameRT.offsetMax = new Vector2(0, -6);

            // Player 1 stats (below name)
            player1StatsLabel = UIHelper.CreateLabel(topBar.transform, "F:0 W:0 S:0 A:0 B:0",
                11, StatsTextColor, TextAnchor.UpperLeft);
            var p1StatsRT = player1StatsLabel.GetComponent<RectTransform>();
            p1StatsRT.anchorMin = new Vector2(0, 0);
            p1StatsRT.anchorMax = new Vector2(0.4f, 0.55f);
            p1StatsRT.offsetMin = new Vector2(12, 8);
            p1StatsRT.offsetMax = new Vector2(0, 0);

            // VS label (center)
            var vsLabel = UIHelper.CreateLabel(topBar.transform, "vs",
                12, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            vsLabel.fontStyle = FontStyle.Bold;
            var vsRT = vsLabel.GetComponent<RectTransform>();
            vsRT.anchorMin = new Vector2(0.45f, 0.4f);
            vsRT.anchorMax = new Vector2(0.55f, 0.9f);
            vsRT.offsetMin = Vector2.zero;
            vsRT.offsetMax = Vector2.zero;

            // Player 2 name (top-right)
            player2NameLabel = UIHelper.CreateLabel(topBar.transform, "Player 2",
                14, Player2Color, TextAnchor.MiddleRight);
            player2NameLabel.fontStyle = FontStyle.Bold;
            var p2NameRT = player2NameLabel.GetComponent<RectTransform>();
            p2NameRT.anchorMin = new Vector2(0.6f, 0.5f);
            p2NameRT.anchorMax = new Vector2(1, 1);
            p2NameRT.offsetMin = new Vector2(0, 0);
            p2NameRT.offsetMax = new Vector2(-12, -6);

            // Player 2 stats (below name)
            player2StatsLabel = UIHelper.CreateLabel(topBar.transform, "F:0 W:0 S:0 A:0 B:0",
                11, StatsTextColor, TextAnchor.UpperRight);
            var p2StatsRT = player2StatsLabel.GetComponent<RectTransform>();
            p2StatsRT.anchorMin = new Vector2(0.6f, 0);
            p2StatsRT.anchorMax = new Vector2(1, 0.55f);
            p2StatsRT.offsetMin = new Vector2(0, 8);
            p2StatsRT.offsetMax = new Vector2(-12, 0);

            // Game time (center bottom)
            gameTimeLabel = UIHelper.CreateLabel(topBar.transform, "0:00",
                12, TimeTextColor, TextAnchor.MiddleCenter);
            var gtRT = gameTimeLabel.GetComponent<RectTransform>();
            gtRT.anchorMin = new Vector2(0.35f, 0);
            gtRT.anchorMax = new Vector2(0.65f, 0.35f);
            gtRT.offsetMin = Vector2.zero;
            gtRT.offsetMax = Vector2.zero;
        }

        // ================================================================
        // Bottom Bar
        // ================================================================

        private void BuildBottomBar()
        {
            var bottomBar = UIHelper.CreatePanel(overlay.transform, "BottomBar", BarBgColor);
            var bottomBarRT = bottomBar.GetComponent<RectTransform>();
            bottomBarRT.anchorMin = new Vector2(0, 0);
            bottomBarRT.anchorMax = new Vector2(1, 0);
            bottomBarRT.pivot = new Vector2(0.5f, 0);
            bottomBarRT.offsetMin = new Vector2(8, 0);
            bottomBarRT.offsetMax = new Vector2(-8, 48);

            // Speed buttons (left side)
            var speedRow = UIHelper.CreateHorizontalRow(bottomBar.transform, 32f, 8f);
            var speedRowRT = speedRow.GetComponent<RectTransform>();
            speedRowRT.anchorMin = new Vector2(0, 0);
            speedRowRT.anchorMax = new Vector2(0.7f, 1);
            speedRowRT.offsetMin = new Vector2(12, 8);
            speedRowRT.offsetMax = new Vector2(0, -8);
            speedRow.childAlignment = TextAnchor.MiddleLeft;

            float[] speeds = { 1f, 2f, 5f, 10f };
            string[] speedLabels = { "1x", "2x", "5x", "10x" };

            speedButtons.Clear();
            for (int i = 0; i < speeds.Length; i++)
            {
                float speed = speeds[i];
                bool isDefault = (speed == 1f);
                Color btnColor = isDefault ? SpeedActiveColor : SpeedInactiveColor;

                var btn = UIHelper.CreateButton(speedRow.transform, speedLabels[i],
                    btnColor, UIHelper.HudTextColor, 13, () =>
                    {
                        currentSpeed = speed;
                        HighlightSpeedButton(speed);
                        OnSpeedChanged?.Invoke(speed);
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 44;
                btnLE.preferredHeight = 32;
                speedButtons.Add(btn);
            }

            // Exit button (right side)
            var exitBtn = UIHelper.CreateButton(bottomBar.transform, "Exit",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14, () => OnExit?.Invoke());
            exitBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
            exitBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            exitBtn.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f);
            exitBtn.GetComponent<RectTransform>().offsetMin = new Vector2(-72, 8);
            exitBtn.GetComponent<RectTransform>().offsetMax = new Vector2(-12, -8);
        }

        // ================================================================
        // Speed Highlight
        // ================================================================

        private void HighlightSpeedButton(float activeSpeed)
        {
            float[] speeds = { 1f, 2f, 5f, 10f };

            for (int i = 0; i < speedButtons.Count && i < speeds.Length; i++)
            {
                bool isActive = Mathf.Approximately(speeds[i], activeSpeed);
                var img = speedButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = isActive ? SpeedActiveColor : SpeedInactiveColor;

                var colors = speedButtons[i].colors;
                colors.normalColor = isActive ? SpeedActiveColor : SpeedInactiveColor;
                colors.highlightedColor = Color.Lerp(
                    isActive ? SpeedActiveColor : SpeedInactiveColor, Color.white, 0.15f);
                speedButtons[i].colors = colors;
            }
        }
    }
}
