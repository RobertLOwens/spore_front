// ============================================================================
// FILE: Visual/CombatDetailPanel.cs
// PURPOSE: Modal scrollable report for a completed combat — summary, participants,
//          phase timeline, unit breakdowns
//          Ported from CombatDetailViewController.swift
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
    public class CombatDetailPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

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
            backdrop = UIHelper.CreatePanel(canvasTransform, "CombatDetailBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 520x560
            panel = UIHelper.CreatePanel(backdrop.transform, "CombatDetailPanel", UIHelper.PanelParchmentBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 520, 560);
            PopupTendrilDecorator.Attach(panelRT);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "DetailScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Close button at bottom
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 42);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(DetailedCombatRecord record)
        {
            Rebuild(record);
            backdrop.SetActive(true);
        }

        public void Show(CombatRecord basicRecord, GameState gameState)
        {
            // Try to find detailed record from combat engine
            var combatEngine = GameEngine.Instance.combatEngine;
            var detailedRecord = combatEngine.GetDetailedRecord(basicRecord);
            if (detailedRecord != null)
            {
                Show(detailedRecord);
                return;
            }

            // Fallback: show basic record info
            RebuildFromBasicRecord(basicRecord);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(DetailedCombatRecord record)
        {
            if (!backdrop.activeSelf) return;
            Rebuild(record);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild from DetailedCombatRecord
        // ================================================================

        private void Rebuild(DetailedCombatRecord record)
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Battle Summary
            BuildBattleSummary(record);
            UIHelper.CreateDivider(contentRT, UIHelper.InkMutedText, 2f);

            // Participant Cards
            BuildParticipantCards(record);
            UIHelper.CreateDivider(contentRT, UIHelper.InkMutedText, 2f);

            // Phase Timeline
            BuildPhaseTimeline(record);
            UIHelper.CreateDivider(contentRT, UIHelper.InkMutedText, 2f);

            // Unit Breakdown Tables
            BuildUnitBreakdownTables(record);
        }

        // ================================================================
        // Rebuild from basic CombatRecord (fallback)
        // ================================================================

        private void RebuildFromBasicRecord(CombatRecord record)
        {
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Summary
            var title = UIHelper.CreateLabel(contentRT, "Battle Report",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30;

            // Winner
            Color winnerColor = record.Winner == CombatResult.AttackerVictory
                ? SporefrontColors.SporeRed
                : record.Winner == CombatResult.DefenderVictory
                    ? SporefrontColors.SporeTeal
                    : UIHelper.InkMutedText;

            var winnerLabel = UIHelper.CreateLabel(contentRT, record.Winner.DisplayName(),
                16, winnerColor, TextAnchor.MiddleCenter, true);
            var winnerLE = winnerLabel.gameObject.AddComponent<LayoutElement>();
            winnerLE.preferredHeight = 28;

            UIHelper.CreateDivider(contentRT);

            // Participants
            var participantLabel = UIHelper.CreateLabel(contentRT,
                $"{record.Attacker.Name} vs {record.Defender.Name}",
                14, UIHelper.InkBodyText, TextAnchor.MiddleCenter);
            var partLE = participantLabel.gameObject.AddComponent<LayoutElement>();
            partLE.preferredHeight = 24;

            // Stats
            CreateInfoRow("Location", $"({record.Location.q}, {record.Location.r})");
            CreateInfoRow("Duration", $"{(int)record.Duration}s");
            CreateInfoRow("Attacker Casualties", record.AttackerCasualties.ToString());
            CreateInfoRow("Defender Casualties", record.DefenderCasualties.ToString());
            CreateInfoRow("Attacker Strength",
                $"{(int)record.AttackerInitialStrength} -> {(int)record.AttackerFinalStrength}");
            CreateInfoRow("Defender Strength",
                $"{(int)record.DefenderInitialStrength} -> {(int)record.DefenderFinalStrength}");
        }

        // ================================================================
        // Battle Summary
        // ================================================================

        private void BuildBattleSummary(DetailedCombatRecord record)
        {
            var title = UIHelper.CreateLabel(contentRT, "Battle Report",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30;

            // Winner badge
            Color winnerColor;
            string winnerName;
            switch (record.Winner)
            {
                case CombatResult.AttackerVictory:
                    winnerColor = SporefrontColors.SporeRed;
                    winnerName = record.AttackerName ?? "Attacker";
                    break;
                case CombatResult.DefenderVictory:
                    winnerColor = SporefrontColors.SporeTeal;
                    winnerName = record.DefenderName ?? "Defender";
                    break;
                default:
                    winnerColor = UIHelper.InkMutedText;
                    winnerName = "Draw";
                    break;
            }

            var winnerPanel = UIHelper.CreatePanel(contentRT, "WinnerBadge", winnerColor);
            var winnerPanelLE = winnerPanel.AddComponent<LayoutElement>();
            winnerPanelLE.preferredHeight = 28;
            var winnerLabel = UIHelper.CreateLabel(winnerPanel.transform,
                $"{winnerName} — {record.Winner.DisplayName()}",
                14, UIHelper.HudTextColor, TextAnchor.MiddleCenter, true);
            UIHelper.StretchFull(winnerLabel.GetComponent<RectTransform>());

            // Battle info rows
            CreateInfoRow("Location", $"({record.Location.q}, {record.Location.r})");
            CreateInfoRow("Duration", record.GetFormattedDuration());

            // Terrain info
            if (record.HasTerrainModifiers)
            {
                var terrainParts = new List<string>();
                terrainParts.Add($"Terrain: {record.TerrainType}");
                if (record.TerrainDefenseBonus > 0)
                    terrainParts.Add($"Def+{record.TerrainDefenseBonus:P0}");
                if (record.TerrainAttackPenalty > 0)
                    terrainParts.Add($"Atk-{record.TerrainAttackPenalty:P0}");
                if (record.EntrenchmentDefenseBonus > 0)
                    terrainParts.Add($"Entrench+{record.EntrenchmentDefenseBonus:P0}");
                CreateInfoRow("Terrain", string.Join(", ", terrainParts));
            }
        }

        // ================================================================
        // Participant Cards
        // ================================================================

        private void BuildParticipantCards(DetailedCombatRecord record)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Participants",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 26;

            // Attacker card
            BuildParticipantCard(
                "ATTACKER",
                record.AttackerName ?? "Unknown",
                record.AttackerCommander,
                record.AttackerCommanderSpecialty,
                record.AttackerInitialStrength,
                record.AttackerFinalStrength,
                record.AttackerTotalCasualties,
                record.Winner == CombatResult.AttackerVictory,
                SporefrontColors.SporeRed
            );

            // Defender card
            BuildParticipantCard(
                "DEFENDER",
                record.DefenderName ?? "Unknown",
                record.DefenderCommander,
                record.DefenderCommanderSpecialty,
                record.DefenderInitialStrength,
                record.DefenderFinalStrength,
                record.DefenderTotalCasualties,
                record.Winner == CombatResult.DefenderVictory,
                SporefrontColors.SporeTeal
            );
        }

        private void BuildParticipantCard(string role, string name, string commander,
            string commanderSpecialty, int initialStrength, int finalStrength,
            int casualties, bool isWinner, Color accentColor)
        {
            Color cardBg = new Color(accentColor.r, accentColor.g, accentColor.b, 0.08f);
            var card = UIHelper.CreatePanel(contentRT, $"{role}Card", cardBg);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = isWinner ? 90 : 80;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 6, 6);
            vlg.spacing = 3;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Role + Name
            var nameRow = UIHelper.CreateHorizontalRow(card.transform, 22f, 4f);
            var roleLabel = UIHelper.CreateLabel(nameRow.transform, role, UIConstants.FontCaption,
                accentColor, TextAnchor.MiddleLeft, true);
            var roleLE = roleLabel.gameObject.AddComponent<LayoutElement>();
            roleLE.preferredWidth = 70;

            var nameLabel = UIHelper.CreateLabel(nameRow.transform, name, 13,
                UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Winner badge
            if (isWinner)
            {
                var badgeLabel = UIHelper.CreateLabel(nameRow.transform, "[WINNER]", UIConstants.FontCaption,
                    accentColor, TextAnchor.MiddleRight, true);
                var badgeLE = badgeLabel.gameObject.AddComponent<LayoutElement>();
                badgeLE.preferredWidth = 60;
            }

            // Commander
            if (!string.IsNullOrEmpty(commander))
            {
                string cmdrText = commander;
                if (!string.IsNullOrEmpty(commanderSpecialty))
                    cmdrText += $" ({commanderSpecialty})";
                var cmdrLabel = UIHelper.CreateLabel(card.transform,
                    $"Commander: {cmdrText}", UIConstants.FontCaption, UIHelper.InkMutedText);
                var cmdrLE = cmdrLabel.gameObject.AddComponent<LayoutElement>();
                cmdrLE.preferredHeight = 16;
            }

            // Strength + Casualties
            var strengthRow = UIHelper.CreateHorizontalRow(card.transform, 18f, 4f);

            var strengthLabel = UIHelper.CreateLabel(strengthRow.transform,
                $"Strength: {initialStrength} -> {finalStrength}", UIConstants.FontCaption, UIHelper.InkBodyText);
            var strLE = strengthLabel.gameObject.AddComponent<LayoutElement>();
            strLE.flexibleWidth = 1;

            var casualtyLabel = UIHelper.CreateLabel(strengthRow.transform,
                $"Casualties: {casualties}", UIConstants.FontCaption,
                casualties > 0 ? SporefrontColors.SporeRed : UIHelper.InkMutedText,
                TextAnchor.MiddleRight);
            var casLE = casualtyLabel.gameObject.AddComponent<LayoutElement>();
            casLE.preferredWidth = 100;
        }

        // ================================================================
        // Phase Timeline
        // ================================================================

        private void BuildPhaseTimeline(DetailedCombatRecord record)
        {
            if (record.PhaseRecords == null || record.PhaseRecords.Count == 0) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Phase Timeline",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 26;

            // Table header
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 18f, 2f);
            CreateTableCell(headerRow.transform, "Phase", 100, UIHelper.InkMutedText, UIConstants.FontCaption, true);
            CreateTableCell(headerRow.transform, "Duration", 60, UIHelper.InkMutedText, UIConstants.FontCaption, true);
            CreateTableCell(headerRow.transform, "Atk Dmg", 60, SporefrontColors.SporeRed, UIConstants.FontCaption, true);
            CreateTableCell(headerRow.transform, "Def Dmg", 60, SporefrontColors.SporeTeal, UIConstants.FontCaption, true);
            CreateTableCell(headerRow.transform, "Losses", 70, UIHelper.InkMutedText, UIConstants.FontCaption, true);

            foreach (var phase in record.PhaseRecords)
            {
                string phaseName = GetPhaseName(phase.Phase);

                // Casualties summary
                int atkCas = phase.AttackerCasualtiesByType != null
                    ? phase.AttackerCasualtiesByType.Values.Sum() : 0;
                int defCas = phase.DefenderCasualtiesByType != null
                    ? phase.DefenderCasualtiesByType.Values.Sum() : 0;

                var row = UIHelper.CreateHorizontalRow(contentRT, 18f, 2f);
                CreateTableCell(row.transform, phaseName, 100, UIHelper.InkBodyText, UIConstants.FontCaption, false);
                CreateTableCell(row.transform, $"{phase.Duration:F1}s", 60, UIHelper.InkMutedText, UIConstants.FontCaption, false);
                CreateTableCell(row.transform, $"{(int)phase.AttackerDamageDealt}", 60,
                    SporefrontColors.SporeRed, UIConstants.FontCaption, false);
                CreateTableCell(row.transform, $"{(int)phase.DefenderDamageDealt}", 60,
                    SporefrontColors.SporeTeal, UIConstants.FontCaption, false);
                CreateTableCell(row.transform, $"{atkCas}/{defCas}", 70,
                    UIHelper.InkMutedText, UIConstants.FontCaption, false);
            }
        }

        // ================================================================
        // Unit Breakdown Tables
        // ================================================================

        private void BuildUnitBreakdownTables(DetailedCombatRecord record)
        {
            if (record.AttackerUnitBreakdowns == null && record.DefenderUnitBreakdowns == null) return;

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Unit Breakdown",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 26;

            // Attacker breakdown
            if (record.AttackerUnitBreakdowns != null && record.AttackerUnitBreakdowns.Count > 0)
            {
                BuildUnitTable("Attacker Units", record.AttackerUnitBreakdowns, SporefrontColors.SporeRed);
            }

            // Defender breakdown
            if (record.DefenderUnitBreakdowns != null && record.DefenderUnitBreakdowns.Count > 0)
            {
                BuildUnitTable("Defender Units", record.DefenderUnitBreakdowns, SporefrontColors.SporeTeal);
            }
        }

        private void BuildUnitTable(string title, List<UnitCombatBreakdown> breakdowns, Color accentColor)
        {
            var titleLabel = UIHelper.CreateLabel(contentRT, title, 13, accentColor,
                TextAnchor.MiddleLeft, true);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 22;

            // Table header
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 2f);
            CreateTableCell(headerRow.transform, "Unit", 80, UIHelper.InkMutedText, 9, true);
            CreateTableCell(headerRow.transform, "Start", 40, UIHelper.InkMutedText, 9, true);
            CreateTableCell(headerRow.transform, "End", 40, UIHelper.InkMutedText, 9, true);
            CreateTableCell(headerRow.transform, "Lost", 40, UIHelper.InkMutedText, 9, true);
            CreateTableCell(headerRow.transform, "Dmg", 50, UIHelper.InkMutedText, 9, true);

            foreach (var breakdown in breakdowns)
            {
                var row = UIHelper.CreateHorizontalRow(contentRT, 16f, 2f);

                string unitName = breakdown.UnitTypeRaw;
                // Try to parse and get display name
                MilitaryUnitType parsedType;
                if (Enum.TryParse(breakdown.UnitTypeRaw, out parsedType))
                    unitName = parsedType.DisplayName();

                CreateTableCell(row.transform, unitName, 80, UIHelper.InkBodyText, 9, false);
                CreateTableCell(row.transform, breakdown.InitialCount.ToString(), 40,
                    UIHelper.InkMutedText, 9, false);
                CreateTableCell(row.transform, breakdown.FinalCount.ToString(), 40,
                    breakdown.FinalCount < breakdown.InitialCount ? SporefrontColors.SporeRed
                        : UIHelper.InkMutedText, 9, false);
                CreateTableCell(row.transform, breakdown.Casualties.ToString(), 40,
                    breakdown.Casualties > 0 ? SporefrontColors.SporeRed
                        : UIHelper.InkMutedText, 9, false);
                CreateTableCell(row.transform, $"{(int)breakdown.DamageDealt}", 50,
                    SporefrontColors.SporeAmber, 9, false);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void CreateInfoRow(string label, string value)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            var labelText = UIHelper.CreateLabel(row.transform, $"{label}:", UIConstants.FontCaption,
                UIHelper.InkMutedText);
            var labelLE = labelText.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 120;

            var valueText = UIHelper.CreateLabel(row.transform, value, UIConstants.FontCaption, UIHelper.InkBodyText);
            var valueLE = valueText.gameObject.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1;
        }

        private void CreateTableCell(Transform parent, string text, float width, Color color,
            int fontSize, bool isBold)
        {
            var label = UIHelper.CreateLabel(parent, text, fontSize, color,
                TextAnchor.MiddleCenter, isBold);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
        }

        private string GetPhaseName(int phaseIndex)
        {
            switch (phaseIndex)
            {
                case 0: return "Ranged Exchange";
                case 1: return "Melee Engagement";
                case 2: return "Cleanup";
                case 3: return "Ended";
                default: return $"Phase {phaseIndex}";
            }
        }
    }
}
