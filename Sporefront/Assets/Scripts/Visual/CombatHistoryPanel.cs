// ============================================================================
// FILE: Visual/CombatHistoryPanel.cs
// PURPOSE: Modal panel showing active battles and combat history log
//          Ported from CombatHistoryViewController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Models.Combat;

namespace Sporefront.Visual
{
    public class CombatHistoryPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnViewLiveCombat;
        public event Action<Guid> OnViewCombatDetail;
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "CombatHistoryBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 500x500
            panel = UIHelper.CreatePanel(backdrop.transform, "CombatHistoryPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 500, 500);

            // Title
            var titleLabel = UIHelper.CreateLabel(panel.transform, "Combat Log",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleRT = titleLabel.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.offsetMin = new Vector2(8, -32);
            titleRT.offsetMax = new Vector2(-8, -4);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "HistoryScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 40);
            scrollRT.offsetMax = new Vector2(0, -36);

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 36);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            var combatEngine = GameEngine.Instance.combatEngine;

            // Section 1: Active Battles
            BuildActiveBattlesSection(combatEngine, gameState);

            UIHelper.CreateDivider(contentRT, SporefrontColors.InkMid, 2f);

            // Section 2: Combat History
            BuildHistorySection(combatEngine, gameState);
        }

        // ================================================================
        // Active Battles Section
        // ================================================================

        private void BuildActiveBattlesSection(CombatEngine combatEngine, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Active Battles",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var activeCombats = combatEngine.activeCombats.Values
                .Where(c => c.phase != CombatPhase.Ended)
                .OrderByDescending(c => c.elapsedTime)
                .ToList();

            // Also include stack combats
            var stackCombats = combatEngine.stackCombats.Values
                .Where(sc => !sc.isComplete)
                .ToList();

            if (activeCombats.Count == 0 && stackCombats.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No active battles.",
                    12, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
                return;
            }

            // Render active combats
            foreach (var combat in activeCombats)
            {
                BuildActiveCombatRow(combat, gameState);
            }

            // Render stack combats
            foreach (var sc in stackCombats)
            {
                BuildActiveStackCombatRow(sc, combatEngine, gameState);
            }
        }

        // ================================================================
        // Active Combat Row
        // ================================================================

        private void BuildActiveCombatRow(ActiveCombat combat, GameState gameState)
        {
            var rowPanel = UIHelper.CreatePanel(contentRT, "ActiveRow",
                new Color(SporefrontColors.SporeRed.r, SporefrontColors.SporeRed.g,
                    SporefrontColors.SporeRed.b, 0.1f));
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 54;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top row: pulsing indicator + participants + duration
            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 20f, 4f);

            // Pulsing dot indicator (red circle)
            var pulseLabel = UIHelper.CreateLabel(topRow.transform, "*",
                14, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var pulseLE = pulseLabel.gameObject.AddComponent<LayoutElement>();
            pulseLE.preferredWidth = 16;

            // Participant names
            string atkName = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyName : "Attacker";
            string defName = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0].armyName : "Defender";
            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{atkName} vs {defName}", 12, UIHelper.BodyTextColor);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Duration
            int elapsed = (int)combat.elapsedTime;
            string timeStr = elapsed >= 60 ? $"{elapsed / 60}:{(elapsed % 60):D2}" : $"0:{elapsed:D2}";
            var timeLabel = UIHelper.CreateLabel(topRow.transform, timeStr,
                11, SporefrontColors.InkLight, TextAnchor.MiddleRight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 50;

            // Bottom row: phase + location
            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 16f, 4f);

            var phaseLabel = UIHelper.CreateLabel(bottomRow.transform,
                combat.phase.DisplayName(), 10, SporefrontColors.SporeAmber);
            var phaseLE = phaseLabel.gameObject.AddComponent<LayoutElement>();
            phaseLE.preferredWidth = 120;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"at ({combat.location.q},{combat.location.r})", 10, SporefrontColors.InkLight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.flexibleWidth = 1;

            // Click to view live
            var capturedID = combat.id;
            var clickBtn = rowPanel.AddComponent<Button>();
            clickBtn.transition = Selectable.Transition.None;
            clickBtn.onClick.AddListener(() =>
            {
                OnViewLiveCombat?.Invoke(capturedID);
            });
        }

        // ================================================================
        // Active Stack Combat Row
        // ================================================================

        private void BuildActiveStackCombatRow(StackCombat stackCombat, CombatEngine combatEngine, GameState gameState)
        {
            var rowPanel = UIHelper.CreatePanel(contentRT, "StackRow",
                new Color(SporefrontColors.SporePurple.r, SporefrontColors.SporePurple.g,
                    SporefrontColors.SporePurple.b, 0.1f));
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 54;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 20f, 4f);

            var pulseLabel = UIHelper.CreateLabel(topRow.transform, "**",
                14, SporefrontColors.SporePurple, TextAnchor.MiddleCenter);
            var pulseLE = pulseLabel.gameObject.AddComponent<LayoutElement>();
            pulseLE.preferredWidth = 20;

            var titleLabel = UIHelper.CreateLabel(topRow.transform,
                $"Stack Combat ({stackCombat.activePairings.Count} pairings)",
                12, UIHelper.BodyTextColor);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 16f, 4f);

            var tierLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"Tier: {stackCombat.currentTier}", 10, SporefrontColors.SporeAmber);
            var tierLE = tierLabel.gameObject.AddComponent<LayoutElement>();
            tierLE.preferredWidth = 80;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"at ({stackCombat.coordinate.q},{stackCombat.coordinate.r})", 10,
                SporefrontColors.InkLight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.flexibleWidth = 1;

            // Click to view the first active pairing
            if (stackCombat.activePairings.Count > 0)
            {
                var firstPairing = stackCombat.activePairings[0];
                var capturedID = firstPairing.activeCombatID;
                var clickBtn = rowPanel.AddComponent<Button>();
                clickBtn.transition = Selectable.Transition.None;
                clickBtn.onClick.AddListener(() =>
                {
                    OnViewLiveCombat?.Invoke(capturedID);
                });
            }
        }

        // ================================================================
        // Combat History Section
        // ================================================================

        private void BuildHistorySection(CombatEngine combatEngine, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Battle History",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var history = combatEngine.GetCombatHistory();

            if (history.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No battles recorded yet.",
                    12, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
                return;
            }

            // Show most recent first, limit to 50
            var recentHistory = history.OrderByDescending(r => r.Timestamp).Take(50).ToList();

            foreach (var record in recentHistory)
            {
                BuildHistoryRow(record, gameState);
            }
        }

        // ================================================================
        // History Row
        // ================================================================

        private void BuildHistoryRow(CombatRecord record, GameState gameState)
        {
            Color rowBg;
            string resultIcon;
            switch (record.Winner)
            {
                case CombatResult.AttackerVictory:
                    rowBg = new Color(SporefrontColors.SporeRed.r, SporefrontColors.SporeRed.g,
                        SporefrontColors.SporeRed.b, 0.05f);
                    resultIcon = "[V]";
                    break;
                case CombatResult.DefenderVictory:
                    rowBg = new Color(SporefrontColors.SporeTeal.r, SporefrontColors.SporeTeal.g,
                        SporefrontColors.SporeTeal.b, 0.05f);
                    resultIcon = "[V]";
                    break;
                default:
                    rowBg = new Color(SporefrontColors.InkFaded.r, SporefrontColors.InkFaded.g,
                        SporefrontColors.InkFaded.b, 0.05f);
                    resultIcon = "[D]";
                    break;
            }

            var rowPanel = UIHelper.CreatePanel(contentRT, "HistoryRow", rowBg);
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top row: result icon + participants
            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 20f, 4f);

            Color iconColor;
            switch (record.Winner)
            {
                case CombatResult.AttackerVictory: iconColor = SporefrontColors.SporeRed; break;
                case CombatResult.DefenderVictory: iconColor = SporefrontColors.SporeTeal; break;
                default: iconColor = SporefrontColors.InkFaded; break;
            }
            var iconLabel = UIHelper.CreateLabel(topRow.transform, resultIcon, 12, iconColor);
            var iconLE = iconLabel.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 24;

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{record.Attacker.Name} vs {record.Defender.Name}", 12, UIHelper.BodyTextColor);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Time ago
            double secondsAgo = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) - record.Timestamp;
            string timeAgo = FormatTimeAgo(secondsAgo);
            var timeLabel = UIHelper.CreateLabel(topRow.transform, timeAgo,
                10, SporefrontColors.InkFaded, TextAnchor.MiddleRight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 60;

            // Bottom row: casualties + location
            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 16f, 4f);

            var casualtyLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"Casualties: {record.AttackerCasualties} / {record.DefenderCasualties}",
                10, SporefrontColors.InkLight);
            var casualtyLE = casualtyLabel.gameObject.AddComponent<LayoutElement>();
            casualtyLE.flexibleWidth = 1;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"({record.Location.q},{record.Location.r})",
                10, SporefrontColors.InkFaded, TextAnchor.MiddleRight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 60;

            // Click to view detail
            var capturedID = record.Id;
            var clickBtn = rowPanel.AddComponent<Button>();
            clickBtn.transition = Selectable.Transition.None;
            clickBtn.onClick.AddListener(() =>
            {
                Guid recordGuid;
                if (Guid.TryParse(capturedID, out recordGuid))
                    OnViewCombatDetail?.Invoke(recordGuid);
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private string FormatTimeAgo(double seconds)
        {
            if (seconds < 60) return $"{(int)seconds}s ago";
            if (seconds < 3600) return $"{(int)(seconds / 60)}m ago";
            if (seconds < 86400) return $"{(int)(seconds / 3600)}h ago";
            return $"{(int)(seconds / 86400)}d ago";
        }
    }
}
