// ============================================================================
// FILE: Visual/ResourceBarPanel.cs
// PURPOSE: Top HUD bar showing local player's resources, rates, and population
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class ResourceBarPanel : MonoBehaviour
    {
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

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Panel: full width, 40px, anchored top
            panel = UIHelper.CreatePanel(canvasTransform, "ResourceBar", UIHelper.HudBg);
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

            // Resource labels
            woodLabel = CreateResourceLabel(row.transform, "W", 120);
            foodLabel = CreateResourceLabel(row.transform, "F", 120);
            stoneLabel = CreateResourceLabel(row.transform, "S", 120);
            oreLabel = CreateResourceLabel(row.transform, "O", 120);

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
            popLabel.text = $"Pop: {current}/{capacity}";

            // Starvation warning
            bool starving = player.GetResource(ResourceType.Food) <= 0;
            starvationLabel.gameObject.SetActive(starving);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Text CreateResourceLabel(Transform parent, string icon, float width)
        {
            var label = UIHelper.CreateLabel(parent, $"{icon} ---", 16,
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
            string icon = UIHelper.ResourceIcon(type);

            string rateStr = "";
            if (rate > 0.01)
                rateStr = $" <color=#{ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeAmber)}>+{rate:F1}</color>";
            else if (rate < -0.01)
                rateStr = $" <color=#{ColorUtility.ToHtmlStringRGB(SporefrontColors.SporeRed)}>{rate:F1}</color>";

            label.text = $"{icon} {amount}{rateStr}";
            label.supportRichText = true;
        }
    }
}
