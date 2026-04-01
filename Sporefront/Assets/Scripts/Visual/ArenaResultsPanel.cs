// ============================================================================
// FILE: Visual/ArenaResultsPanel.cs
// PURPOSE: Full-screen results display for arena combat (single-run detail or
//          batch aggregate). Port of ArenaResultsViewController.swift
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
    public class ArenaResultsPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnBack;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

        // Winner colors
        private static readonly Color AttackerWinColor = new Color(0.2f, 0.8f, 0.3f, 1.0f);
        private static readonly Color DefenderWinColor = new Color(0.9f, 0.3f, 0.3f, 1.0f);
        private static readonly Color DrawColor = new Color(0.9f, 0.7f, 0.2f, 1.0f);

        // Cached scenario config for modifiers display
        private ArenaScenarioConfig scenarioConfig;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "ArenaResultsPanel", UIHelper.PanelParchmentBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);
            PopupTendrilDecorator.Attach(panelRT);

            // ScrollView fills panel above back button
            var scroll = UIHelper.CreateScrollView(panel.transform, "ResultsScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 56);  // space for back button
            scrollRT.offsetMax = Vector2.zero;

            // Back button at bottom
            var backBtn = UIHelper.CreateButton(panel.transform, "Back to Setup",
                SporefrontColors.SporeTeal, UIHelper.HudTextColor, 16, () => OnBack?.Invoke());
            var backBtnRT = backBtn.GetComponent<RectTransform>();
            backBtnRT.anchorMin = new Vector2(0, 0);
            backBtnRT.anchorMax = new Vector2(1, 0);
            backBtnRT.pivot = new Vector2(0.5f, 0);
            backBtnRT.offsetMin = new Vector2(40, 8);
            backBtnRT.offsetMax = new Vector2(-40, 52);

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void ShowBatch(List<SimulationResult> results, ArenaScenarioConfig config = null)
        {
            scenarioConfig = config;
            ClearContent();

            if (results == null || results.Count == 0)
            {
                BuildNoResultsView();
            }
            else if (results.Count == 1)
            {
                BuildSingleSimView(results[0]);
            }
            else
            {
                BuildBatchView(results);
            }

            panel.SetActive(true);
        }

        public void ShowSingle(SimulationResult result, ArenaScenarioConfig config = null)
        {
            scenarioConfig = config;
            ClearContent();

            if (result == null)
                BuildNoResultsView();
            else
                BuildSingleSimView(result);

            panel.SetActive(true);
        }

        public override void Hide()
        {
            panel.SetActive(false);
        }

        public new bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Clear Content
        // ================================================================

        private new void ClearContent()
        {
            if (contentRT == null) return;
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);
        }

        // ================================================================
        // No Results
        // ================================================================

        private void BuildNoResultsView()
        {
            AddTitle("NO RESULTS");
        }

        // ================================================================
        // Single Simulation View
        // ================================================================

        private void BuildSingleSimView(SimulationResult result)
        {
            AddTitle("COMBAT RESULTS");

            // Winner banner
            string winnerText;
            Color winnerColor;
            GetWinnerDisplay(result.winner, out winnerText, out winnerColor);

            AddCenteredLabel(winnerText, 22, winnerColor, true);
            AddCenteredLabel($"Duration: {result.combatDuration:F1}s",
                14, UIHelper.InkMutedText, false);
            AddSpacer(10);

            // Attacker section
            AddSectionHeader("ATTACKER", AttackerWinColor);
            BuildCompositionSection(result.attackerInitial, result.attackerRemaining);
            AddSpacer(10);

            // Defender section
            AddSectionHeader("DEFENDER", DefenderWinColor);
            BuildCompositionSection(result.defenderInitial, result.defenderRemaining);
            AddSpacer(10);

            // Modifiers section
            BuildModifiersSection();
            AddSpacer(20);
        }

        // ================================================================
        // Batch Results View
        // ================================================================

        private void BuildBatchView(List<SimulationResult> results)
        {
            int total = results.Count;
            AddTitle($"SIMULATION RESULTS ({total} runs)");

            int attackerWins = results.Count(r => r.winner == SimWinner.Attacker);
            int defenderWins = results.Count(r => r.winner == SimWinner.Defender);
            int draws = results.Count(r => r.winner == SimWinner.Draw);

            // Win Rate section
            AddSectionHeader("WIN RATE", UIHelper.InkHeaderText);
            BuildWinRateBar("Attacker", attackerWins, total, AttackerWinColor);
            BuildWinRateBar("Defender", defenderWins, total, DefenderWinColor);
            BuildWinRateBar("Draw", draws, total, UIHelper.InkMutedText);
            AddSpacer(10);

            // Averages section
            AddSectionHeader("AVERAGES", UIHelper.InkHeaderText);

            double avgDuration = results.Sum(r => r.combatDuration) / total;
            AddInfoLabel($"Avg Duration: {avgDuration:F1}s");

            int avgAttackerCas = results.Sum(r => r.attackerCasualties.Values.Sum()) / total;
            int avgDefenderCas = results.Sum(r => r.defenderCasualties.Values.Sum()) / total;
            AddInfoLabel($"Avg Attacker Casualties: {avgAttackerCas}");
            AddInfoLabel($"Avg Defender Casualties: {avgDefenderCas}");
            AddSpacer(10);

            // Modifiers
            BuildModifiersSection();
            AddSpacer(10);

            // Individual runs list
            AddSectionHeader("INDIVIDUAL RUNS", UIHelper.InkHeaderText);

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                string winnerStr;
                Color color;
                switch (result.winner)
                {
                    case SimWinner.Attacker:
                        winnerStr = "Attacker Win";
                        color = AttackerWinColor;
                        break;
                    case SimWinner.Defender:
                        winnerStr = "Defender Win";
                        color = DefenderWinColor;
                        break;
                    default:
                        winnerStr = "Draw";
                        color = DrawColor;
                        break;
                }

                string text = $"Run {i + 1}: {winnerStr} - {result.combatDuration:F1}s";
                var label = UIHelper.CreateLabel(contentRT, "  " + text, UIConstants.FontCaption, color, TextAnchor.MiddleLeft);
                var le = label.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 22;
            }

            AddSpacer(20);
        }

        // ================================================================
        // Composition Section
        // ================================================================

        private void BuildCompositionSection(Dictionary<string, int> initial, Dictionary<string, int> remaining)
        {
            if (initial == null) return;

            foreach (var kvp in initial)
            {
                int finalCount = 0;
                if (remaining != null)
                    remaining.TryGetValue(kvp.Key, out finalCount);
                int killed = kvp.Value - finalCount;

                string text = $"  {kvp.Key}:  {kvp.Value} -> {finalCount}  ({killed} killed)";
                var label = UIHelper.CreateLabel(contentRT, text, 13, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                var le = label.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 22;
            }

            int totalInitial = initial.Values.Sum();
            int totalFinal = remaining != null ? remaining.Values.Sum() : 0;
            var totalLabel = UIHelper.CreateLabel(contentRT,
                $"  Total: {totalInitial} -> {totalFinal}",
                13, UIHelper.InkHeaderText, TextAnchor.MiddleLeft);
            totalLabel.fontStyle = FontStyle.Bold;
            var totalLE = totalLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.preferredHeight = 22;
        }

        // ================================================================
        // Win Rate Bar
        // ================================================================

        private void BuildWinRateBar(string label, int count, int total, Color barColor)
        {
            double pct = total > 0 ? (double)count / total : 0;

            var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

            // Name label
            var nameLabel = UIHelper.CreateLabel(row.transform, "  " + label, 13, UIHelper.InkBodyText);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 80;

            // Bar background
            var barBg = UIHelper.CreatePanel(row.transform, "BarBg", SporefrontColors.ParchmentDeep);
            var barBgLE = barBg.AddComponent<LayoutElement>();
            barBgLE.flexibleWidth = 1;
            barBgLE.preferredHeight = 20;

            // Bar fill
            var barFill = UIHelper.CreatePanel(barBg.transform, "BarFill", barColor);
            var barFillRT = barFill.GetComponent<RectTransform>();
            barFillRT.anchorMin = Vector2.zero;
            barFillRT.anchorMax = new Vector2(Mathf.Max(0.01f, (float)pct), 1);
            barFillRT.offsetMin = Vector2.zero;
            barFillRT.offsetMax = Vector2.zero;

            // Count label
            string countText = $"{count}/{total} ({(int)(pct * 100)}%)";
            var countLabel = UIHelper.CreateLabel(row.transform, countText,
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 90;
        }

        // ================================================================
        // Modifiers Section
        // ================================================================

        private void BuildModifiersSection()
        {
            if (scenarioConfig == null) return;

            AddSectionHeader("MODIFIERS", UIHelper.InkMutedText);

            // Terrain
            string terrainName = scenarioConfig.enemyTerrain.ToString();
            AddInfoLabel($"Terrain: {terrainName}");

            // Entrenchment
            if (scenarioConfig.enemyEntrenched)
                AddInfoLabel("Entrenchment: +10% def");

            // Player Commander
            string playerCmdr = $"Player Cmdr: {scenarioConfig.playerCommanderSpecialty.ToString()} Lv{scenarioConfig.playerCommanderLevel}";
            AddInfoLabel(playerCmdr);

            // Enemy Commander
            string enemyCmdr = $"Enemy Cmdr: {scenarioConfig.enemyCommanderSpecialty.ToString()} Lv{scenarioConfig.enemyCommanderLevel}";
            AddInfoLabel(enemyCmdr);

            // Enemy unit tiers
            if (scenarioConfig.enemyUnitTiers != null && scenarioConfig.enemyUnitTiers.Count > 0)
            {
                var tierParts = scenarioConfig.enemyUnitTiers
                    .Select(kvp => $"{kvp.Key}:T{kvp.Value}");
                AddInfoLabel("Enemy Tiers: " + string.Join(", ", tierParts));
            }

            // Player unit tiers
            if (scenarioConfig.playerUnitTiers != null && scenarioConfig.playerUnitTiers.Count > 0)
            {
                var tierParts = scenarioConfig.playerUnitTiers
                    .Select(kvp => $"{kvp.Key}:T{kvp.Value}");
                AddInfoLabel("Player Tiers: " + string.Join(", ", tierParts));
            }

            // Building
            if (scenarioConfig.enemyBuilding.HasValue)
                AddInfoLabel($"Building: {scenarioConfig.enemyBuilding.Value}");

            // Garrison
            if (scenarioConfig.garrisonArchers > 0)
                AddInfoLabel($"Garrison: {scenarioConfig.garrisonArchers} archers");

            // Stacking
            if (Math.Abs(scenarioConfig.enemyArmyCount) >= 2)
            {
                string stackType = scenarioConfig.enemyArmyCount > 0 ? "Same tile" : "Adjacent";
                AddInfoLabel($"Stacking: {stackType} ({Math.Abs(scenarioConfig.enemyArmyCount)} armies)");
            }
        }

        // ================================================================
        // UI Helpers
        // ================================================================

        private void AddTitle(string text)
        {
            var label = UIHelper.CreateLabel(contentRT, text,
                24, UIHelper.InkHeaderText, TextAnchor.MiddleCenter, true);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
        }

        private void AddSectionHeader(string text, Color color)
        {
            UIHelper.CreateDivider(contentRT);

            var label = UIHelper.CreateLabel(contentRT, text, 14, color, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 26;
        }

        private void AddCenteredLabel(string text, int fontSize, Color color, bool bold)
        {
            var label = UIHelper.CreateLabel(contentRT, text, fontSize, color, TextAnchor.MiddleCenter);
            if (bold) label.fontStyle = FontStyle.Bold;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 8;
        }

        private void AddInfoLabel(string text)
        {
            var label = UIHelper.CreateLabel(contentRT, "  " + text,
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 20;
        }

        private void AddSpacer(float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(contentRT, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private void GetWinnerDisplay(SimWinner winner, out string text, out Color color)
        {
            switch (winner)
            {
                case SimWinner.Attacker:
                    text = "ATTACKER VICTORY";
                    color = AttackerWinColor;
                    break;
                case SimWinner.Defender:
                    text = "DEFENDER VICTORY";
                    color = DefenderWinColor;
                    break;
                default:
                    text = "DRAW";
                    color = DrawColor;
                    break;
            }
        }
    }
}
