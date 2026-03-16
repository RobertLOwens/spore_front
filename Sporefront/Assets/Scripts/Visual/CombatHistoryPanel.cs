// ============================================================================
// FILE: Visual/CombatHistoryPanel.cs
// PURPOSE: Modal panel showing active battles and combat history log.
//          Parchment/ink ledger style.
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

            // Main panel — parchment background
            panel = UIHelper.CreatePanel(backdrop.transform, "CombatHistoryPanel",
                UIHelper.PanelParchmentBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 938, 938);
            PopupTendrilDecorator.Attach(panelRT);

            // Title
            var titleLabel = UIHelper.CreateLabel(panel.transform, "Combat Log",
                UIHelper.DefaultHeaderFontSize + 4, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var titleRT = titleLabel.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.offsetMin = new Vector2(12, -48);
            titleRT.offsetMax = new Vector2(-12, -6);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "HistoryScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = new Vector2(0, -52);

            // Ink-styled close annotation
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 40);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
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

            UIHelper.CreateDivider(contentRT, UIHelper.InkDividerColor, 2f);

            // Section 2: Combat History
            BuildHistorySection(combatEngine, gameState);
        }

        // ================================================================
        // Active Battles Section
        // ================================================================

        private void BuildActiveBattlesSection(CombatEngine combatEngine, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Active Battles",
                UIHelper.DefaultHeaderFontSize + 4, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 42;

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
                    18, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
                emptyLabel.fontStyle = FontStyle.Italic;
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 45;
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
            var rowOutline = rowPanel.GetComponent<Outline>();
            rowOutline.effectColor = new Color(
                SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.25f);
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 81;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 6, 6);
            vlg.spacing = 3;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top row: pulsing indicator + participants + duration
            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 30f, 6f);

            // Pulsing dot indicator
            var pulseLabel = UIHelper.CreateLabel(topRow.transform, "*",
                21, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var pulseLE = pulseLabel.gameObject.AddComponent<LayoutElement>();
            pulseLE.preferredWidth = 24;

            // Participant names
            string atkName = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyName : "Attacker";
            string defName = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0].armyName : "Defender";
            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{atkName} vs {defName}", 18, UIHelper.InkBodyText);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Duration
            int elapsed = (int)combat.elapsedTime;
            string timeStr = elapsed >= 60 ? $"{elapsed / 60}:{(elapsed % 60):D2}" : $"0:{elapsed:D2}";
            var timeLabel = UIHelper.CreateLabel(topRow.transform, timeStr,
                17, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 75;

            // Bottom row: phase + location
            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 24f, 6f);

            var phaseLabel = UIHelper.CreateLabel(bottomRow.transform,
                combat.phase.DisplayName(), 15, SporefrontColors.SporeAmber);
            var phaseLE = phaseLabel.gameObject.AddComponent<LayoutElement>();
            phaseLE.preferredWidth = 180;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"at ({combat.location.q},{combat.location.r})", 15, UIHelper.InkMutedText);
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
            var rowOutline = rowPanel.GetComponent<Outline>();
            rowOutline.effectColor = new Color(
                SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.25f);
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 81;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 6, 6);
            vlg.spacing = 3;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 30f, 6f);

            var pulseLabel = UIHelper.CreateLabel(topRow.transform, "**",
                21, SporefrontColors.SporePurple, TextAnchor.MiddleCenter);
            var pulseLE = pulseLabel.gameObject.AddComponent<LayoutElement>();
            pulseLE.preferredWidth = 30;

            var titleLabel = UIHelper.CreateLabel(topRow.transform,
                $"Stack Combat ({stackCombat.activePairings.Count} pairings)",
                18, UIHelper.InkBodyText);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 24f, 6f);

            var tierLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"Tier: {stackCombat.currentTier}", 15, SporefrontColors.SporeAmber);
            var tierLE = tierLabel.gameObject.AddComponent<LayoutElement>();
            tierLE.preferredWidth = 120;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"at ({stackCombat.coordinate.q},{stackCombat.coordinate.r})", 15,
                UIHelper.InkMutedText);
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
                UIHelper.DefaultHeaderFontSize + 4, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 42;

            var history = combatEngine.GetCombatHistory();

            if (history.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No battles recorded yet.",
                    18, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
                emptyLabel.fontStyle = FontStyle.Italic;
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 45;
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
                        SporefrontColors.SporeRed.b, 0.08f);
                    resultIcon = "Victory";
                    break;
                case CombatResult.DefenderVictory:
                    rowBg = new Color(SporefrontColors.SporeTeal.r, SporefrontColors.SporeTeal.g,
                        SporefrontColors.SporeTeal.b, 0.08f);
                    resultIcon = "Victory";
                    break;
                default:
                    rowBg = new Color(SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                        SporefrontColors.InkBorder.b, 0.06f);
                    resultIcon = "Draw";
                    break;
            }

            var rowPanel = UIHelper.CreatePanel(contentRT, "HistoryRow", rowBg);
            var rowOutline = rowPanel.GetComponent<Outline>();
            rowOutline.effectColor = new Color(
                SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.2f);
            var rowLE = rowPanel.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 75;

            var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 6, 6);
            vlg.spacing = 3;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top row: result text + participants
            var topRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 30f, 6f);

            Color iconColor;
            switch (record.Winner)
            {
                case CombatResult.AttackerVictory: iconColor = SporefrontColors.SporeRed; break;
                case CombatResult.DefenderVictory: iconColor = SporefrontColors.SporeTeal; break;
                default: iconColor = UIHelper.InkMutedText; break;
            }
            var iconLabel = UIHelper.CreateLabel(topRow.transform, resultIcon, 13, iconColor);
            iconLabel.fontStyle = FontStyle.Italic;
            var iconLE = iconLabel.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 50;

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                $"{record.Attacker.Name} vs {record.Defender.Name}", 18, UIHelper.InkBodyText);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Time ago
            double secondsAgo = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) - record.Timestamp;
            string timeAgo = FormatTimeAgo(secondsAgo);
            var timeLabel = UIHelper.CreateLabel(topRow.transform, timeAgo,
                15, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 90;

            // Bottom row: casualties + location
            var bottomRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 24f, 6f);

            var casualtyLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"Casualties: {record.AttackerCasualties} / {record.DefenderCasualties}",
                15, UIHelper.InkSubText);
            var casualtyLE = casualtyLabel.gameObject.AddComponent<LayoutElement>();
            casualtyLE.flexibleWidth = 1;

            var locLabel = UIHelper.CreateLabel(bottomRow.transform,
                $"({record.Location.q},{record.Location.r})",
                15, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 90;

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
