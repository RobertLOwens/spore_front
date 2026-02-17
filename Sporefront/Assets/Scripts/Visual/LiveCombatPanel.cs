// ============================================================================
// FILE: Visual/LiveCombatPanel.cs
// PURPOSE: Floating panel showing live combat status — phase, HP, unit breakdown
//          Ported from LiveCombatViewController.swift
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
    public class LiveCombatPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;
        public event Action<HexCoordinate> OnFocusCombat;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;
        private Guid? trackedCombatID;

        // Cached UI references for rapid updates
        private Text timerLabel;
        private Text phaseLabel;
        private Text locationLabel;
        private Image attackerHPFill;
        private Image defenderHPFill;
        private Text attackerHPLabel;
        private Text defenderHPLabel;
        private Text attackerNameLabel;
        private Text defenderNameLabel;
        private RectTransform attackerUnitsRT;
        private RectTransform defenderUnitsRT;
        private Text terrainLabel;
        private Text stackInfoLabel;
        private GameObject winnerBadge;
        private Text winnerText;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Side panel — anchored to right edge, 360 wide
            panel = UIHelper.CreatePanel(canvasTransform, "LiveCombatPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1, 0.2f);
            panelRT.anchorMax = new Vector2(1, 0.85f);
            panelRT.pivot = new Vector2(1, 0.5f);
            panelRT.offsetMin = new Vector2(-360, 0);
            panelRT.offsetMax = new Vector2(-4, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "CombatScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 36);
            scrollRT.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 11, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(6, 4);
            closeBtnRT.offsetMax = new Vector2(-6, 32);

            panel.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid combatID, GameState gameState)
        {
            trackedCombatID = combatID;
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            trackedCombatID = null;
            panel.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!panel.activeSelf || !trackedCombatID.HasValue) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!trackedCombatID.HasValue) return;

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Find the combat
            var combatEngine = GameEngine.Instance.combatEngine;
            ActiveCombat combat = null;

            if (combatEngine.activeCombats.TryGetValue(trackedCombatID.Value, out combat))
            {
                BuildCombatView(combat, gameState);
                return;
            }

            // Try stack combats
            foreach (var sc in combatEngine.stackCombats.Values)
            {
                foreach (var pairing in sc.activePairings)
                {
                    if (pairing.activeCombatID == trackedCombatID.Value)
                    {
                        if (combatEngine.activeCombats.TryGetValue(pairing.activeCombatID, out combat))
                        {
                            BuildStackCombatView(combat, sc, gameState);
                            return;
                        }
                    }
                }
            }

            // Combat not found — may have ended
            var endedLabel = UIHelper.CreateLabel(contentRT, "Combat has ended.",
                14, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var endedLE = endedLabel.gameObject.AddComponent<LayoutElement>();
            endedLE.preferredHeight = 40;
        }

        // ================================================================
        // Standard Combat View
        // ================================================================

        private void BuildCombatView(ActiveCombat combat, GameState gameState)
        {
            // Header: Combat Timer + Phase
            BuildCombatHeader(combat);

            UIHelper.CreateDivider(contentRT);

            // Terrain modifiers
            BuildTerrainInfo(combat);

            UIHelper.CreateDivider(contentRT);

            // Two columns: Attacker vs Defender
            BuildSideByDisplay(combat, gameState);

            UIHelper.CreateDivider(contentRT);

            // Unit breakdowns
            BuildUnitBreakdowns(combat);

            // Winner badge if ended
            if (combat.phase == CombatPhase.Ended)
            {
                BuildWinnerBadge(combat);
            }

            // Focus button
            BuildFocusButton(combat.location);
        }

        // ================================================================
        // Stack Combat View
        // ================================================================

        private void BuildStackCombatView(ActiveCombat combat, StackCombat stackCombat, GameState gameState)
        {
            // Stack info header
            var stackHeader = UIHelper.CreateLabel(contentRT, "Stack Combat",
                UIHelper.DefaultHeaderFontSize, SporefrontColors.SporeRed,
                TextAnchor.MiddleCenter, true);
            var stackHeaderLE = stackHeader.gameObject.AddComponent<LayoutElement>();
            stackHeaderLE.preferredHeight = 26;

            var tierLabel = UIHelper.CreateLabel(contentRT,
                $"Current Tier: {stackCombat.currentTier}  |  Pairings: {stackCombat.activePairings.Count}",
                12, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var tierLE = tierLabel.gameObject.AddComponent<LayoutElement>();
            tierLE.preferredHeight = 20;

            UIHelper.CreateDivider(contentRT);

            // Show the active combat within the stack
            BuildCombatHeader(combat);
            UIHelper.CreateDivider(contentRT);
            BuildTerrainInfo(combat);
            UIHelper.CreateDivider(contentRT);
            BuildSideByDisplay(combat, gameState);
            UIHelper.CreateDivider(contentRT);
            BuildUnitBreakdowns(combat);

            if (combat.phase == CombatPhase.Ended)
            {
                BuildWinnerBadge(combat);
            }

            BuildFocusButton(stackCombat.coordinate);
        }

        // ================================================================
        // Combat Header — Timer + Phase
        // ================================================================

        private void BuildCombatHeader(ActiveCombat combat)
        {
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 26f, 6f);

            // Timer
            int elapsed = (int)combat.elapsedTime;
            string timeStr = elapsed >= 60 ? $"{elapsed / 60}:{(elapsed % 60):D2}" : $"0:{elapsed:D2}";
            timerLabel = UIHelper.CreateLabel(headerRow.transform, timeStr,
                14, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            var timerLE = timerLabel.gameObject.AddComponent<LayoutElement>();
            timerLE.preferredWidth = 60;

            // Phase
            phaseLabel = UIHelper.CreateLabel(headerRow.transform,
                combat.phase.DisplayName(), 13, GetPhaseColor(combat.phase),
                TextAnchor.MiddleCenter);
            var phaseLE = phaseLabel.gameObject.AddComponent<LayoutElement>();
            phaseLE.flexibleWidth = 1;

            // Location
            locationLabel = UIHelper.CreateLabel(headerRow.transform,
                $"({combat.location.q},{combat.location.r})", 11,
                SporefrontColors.InkLight, TextAnchor.MiddleRight);
            var locLE = locationLabel.gameObject.AddComponent<LayoutElement>();
            locLE.preferredWidth = 70;
        }

        // ================================================================
        // Terrain Info
        // ================================================================

        private void BuildTerrainInfo(ActiveCombat combat)
        {
            var terrainRow = UIHelper.CreateHorizontalRow(contentRT, 18f, 4f);

            var terrainNameLabel = UIHelper.CreateLabel(terrainRow.transform,
                $"Terrain: {combat.terrainType}", 11, SporefrontColors.InkLight);
            var tnLE = terrainNameLabel.gameObject.AddComponent<LayoutElement>();
            tnLE.flexibleWidth = 1;

            if (combat.terrainDefenseBonus > 0 || combat.terrainAttackPenalty > 0 ||
                combat.entrenchmentDefenseBonus > 0)
            {
                var modParts = new List<string>();
                if (combat.terrainDefenseBonus > 0)
                    modParts.Add($"Def+{combat.terrainDefenseBonus:P0}");
                if (combat.terrainAttackPenalty > 0)
                    modParts.Add($"Atk-{combat.terrainAttackPenalty:P0}");
                if (combat.entrenchmentDefenseBonus > 0)
                    modParts.Add($"Entrench+{combat.entrenchmentDefenseBonus:P0}");

                var modLabel = UIHelper.CreateLabel(terrainRow.transform,
                    string.Join(" ", modParts), 10, SporefrontColors.SporeAmber,
                    TextAnchor.MiddleRight);
                var modLE = modLabel.gameObject.AddComponent<LayoutElement>();
                modLE.preferredWidth = 180;
            }
        }

        // ================================================================
        // Side-by-Side Display: Attacker vs Defender
        // ================================================================

        private void BuildSideByDisplay(ActiveCombat combat, GameState gameState)
        {
            // Attacker/Defender labels
            var labelRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);

            var atkHeader = UIHelper.CreateLabel(labelRow.transform, "ATTACKER",
                12, SporefrontColors.SporeRed, TextAnchor.MiddleCenter, true);
            var atkHeaderLE = atkHeader.gameObject.AddComponent<LayoutElement>();
            atkHeaderLE.flexibleWidth = 1;

            var vsLabel = UIHelper.CreateLabel(labelRow.transform, "vs",
                11, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var vsLE = vsLabel.gameObject.AddComponent<LayoutElement>();
            vsLE.preferredWidth = 24;

            var defHeader = UIHelper.CreateLabel(labelRow.transform, "DEFENDER",
                12, SporefrontColors.SporeTeal, TextAnchor.MiddleCenter, true);
            var defHeaderLE = defHeader.gameObject.AddComponent<LayoutElement>();
            defHeaderLE.flexibleWidth = 1;

            // Army names
            var nameRow = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            string atkName = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyName : "Attacker";
            attackerNameLabel = UIHelper.CreateLabel(nameRow.transform, atkName,
                11, UIHelper.BodyTextColor, TextAnchor.MiddleCenter);
            var atkNameLE = attackerNameLabel.gameObject.AddComponent<LayoutElement>();
            atkNameLE.flexibleWidth = 1;

            var spacer = UIHelper.CreateLabel(nameRow.transform, "", 11);
            var spacerLE = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLE.preferredWidth = 24;

            string defName = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0].armyName : "Defender";
            defenderNameLabel = UIHelper.CreateLabel(nameRow.transform, defName,
                11, UIHelper.BodyTextColor, TextAnchor.MiddleCenter);
            var defNameLE = defenderNameLabel.gameObject.AddComponent<LayoutElement>();
            defNameLE.flexibleWidth = 1;

            // Commander info
            if (combat.attackerArmies.Count > 0 || combat.defenderArmies.Count > 0)
            {
                var cmdrRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 4f);

                string atkCmdr = combat.attackerArmies.Count > 0 && combat.attackerArmies[0].commanderName != null
                    ? combat.attackerArmies[0].commanderName : "--";
                var atkCmdrLabel = UIHelper.CreateLabel(cmdrRow.transform, atkCmdr,
                    10, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var atkCmdrLE = atkCmdrLabel.gameObject.AddComponent<LayoutElement>();
                atkCmdrLE.flexibleWidth = 1;

                var cmdrSpacer = UIHelper.CreateLabel(cmdrRow.transform, "", 10);
                var cmdrSpacerLE = cmdrSpacer.gameObject.AddComponent<LayoutElement>();
                cmdrSpacerLE.preferredWidth = 24;

                string defCmdr = combat.defenderArmies.Count > 0 && combat.defenderArmies[0].commanderName != null
                    ? combat.defenderArmies[0].commanderName : "--";
                var defCmdrLabel = UIHelper.CreateLabel(cmdrRow.transform, defCmdr,
                    10, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var defCmdrLE = defCmdrLabel.gameObject.AddComponent<LayoutElement>();
                defCmdrLE.flexibleWidth = 1;
            }

            // HP Bars (shared scale)
            BuildHPBars(combat);
        }

        // ================================================================
        // HP Bars
        // ================================================================

        private void BuildHPBars(ActiveCombat combat)
        {
            double maxHP = Math.Max(combat.attackerState.InitialTotalHP, combat.defenderState.InitialTotalHP);
            if (maxHP <= 0) maxHP = 1;

            // Attacker HP
            var atkRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);
            var atkUnitLabel = UIHelper.CreateLabel(atkRow.transform,
                $"{combat.attackerState.TotalUnits}/{combat.attackerState.initialUnitCount}", 10,
                SporefrontColors.InkLight);
            var atkUnitLE = atkUnitLabel.gameObject.AddComponent<LayoutElement>();
            atkUnitLE.preferredWidth = 50;

            float atkPct = Mathf.Clamp01((float)(combat.attackerState.CurrentTotalHP / maxHP));
            var (atkBg, atkFill) = UIHelper.CreateProgressBar(atkRow.transform, 16f,
                SporefrontColors.InkFaded, SporefrontColors.SporeRed);
            var atkFillRT = atkFill.GetComponent<RectTransform>();
            atkFillRT.anchorMax = new Vector2(atkPct, 1);
            attackerHPFill = atkFill;
            var atkBarLE = atkBg.gameObject.AddComponent<LayoutElement>();
            atkBarLE.flexibleWidth = 1;
            atkBarLE.preferredHeight = 16;

            // Defender HP
            var defRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);
            var defUnitLabel = UIHelper.CreateLabel(defRow.transform,
                $"{combat.defenderState.TotalUnits}/{combat.defenderState.initialUnitCount}", 10,
                SporefrontColors.InkLight);
            var defUnitLE = defUnitLabel.gameObject.AddComponent<LayoutElement>();
            defUnitLE.preferredWidth = 50;

            float defPct = Mathf.Clamp01((float)(combat.defenderState.CurrentTotalHP / maxHP));
            var (defBg, defFill) = UIHelper.CreateProgressBar(defRow.transform, 16f,
                SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
            var defFillRT = defFill.GetComponent<RectTransform>();
            defFillRT.anchorMax = new Vector2(defPct, 1);
            defenderHPFill = defFill;
            var defBarLE = defBg.gameObject.AddComponent<LayoutElement>();
            defBarLE.flexibleWidth = 1;
            defBarLE.preferredHeight = 16;
        }

        // ================================================================
        // Unit Breakdowns
        // ================================================================

        private void BuildUnitBreakdowns(ActiveCombat combat)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Unit Breakdown",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            // Table header
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 18f, 2f);
            CreateTableCell(headerRow.transform, "Unit Type", 90, SporefrontColors.InkMid, 10, true);
            CreateTableCell(headerRow.transform, "Atk", 40, SporefrontColors.SporeRed, 10, true);
            CreateTableCell(headerRow.transform, "Def", 40, SporefrontColors.SporeTeal, 10, true);

            // Collect all unit types from both sides
            var allTypes = new HashSet<MilitaryUnitType>();
            foreach (var kvp in combat.attackerState.initialComposition) allTypes.Add(kvp.Key);
            foreach (var kvp in combat.defenderState.initialComposition) allTypes.Add(kvp.Key);

            foreach (var unitType in allTypes.OrderBy(t => t.Category()))
            {
                int atkCount = combat.attackerState.GetUnits(unitType);
                int defCount = combat.defenderState.GetUnits(unitType);
                int atkInitial = 0;
                int defInitial = 0;
                combat.attackerState.initialComposition.TryGetValue(unitType, out atkInitial);
                combat.defenderState.initialComposition.TryGetValue(unitType, out defInitial);

                if (atkInitial == 0 && defInitial == 0) continue;

                var row = UIHelper.CreateHorizontalRow(contentRT, 16f, 2f);
                CreateTableCell(row.transform, unitType.DisplayName(), 90, UIHelper.BodyTextColor, 10, false);
                CreateTableCell(row.transform, $"{atkCount}/{atkInitial}", 40,
                    atkCount < atkInitial ? SporefrontColors.SporeRed : SporefrontColors.InkLight, 10, false);
                CreateTableCell(row.transform, $"{defCount}/{defInitial}", 40,
                    defCount < defInitial ? SporefrontColors.SporeRed : SporefrontColors.InkLight, 10, false);
            }
        }

        // ================================================================
        // Winner Badge
        // ================================================================

        private void BuildWinnerBadge(ActiveCombat combat)
        {
            UIHelper.CreateDivider(contentRT);

            Color badgeColor;
            string resultText;
            switch (combat.Winner)
            {
                case CombatResult.AttackerVictory:
                    badgeColor = SporefrontColors.SporeRed;
                    resultText = "ATTACKER VICTORY";
                    break;
                case CombatResult.DefenderVictory:
                    badgeColor = SporefrontColors.SporeTeal;
                    resultText = "DEFENDER VICTORY";
                    break;
                default:
                    badgeColor = SporefrontColors.InkMid;
                    resultText = "DRAW";
                    break;
            }

            var badgePanel = UIHelper.CreatePanel(contentRT, "WinnerBadge", badgeColor);
            var badgeLE = badgePanel.AddComponent<LayoutElement>();
            badgeLE.preferredHeight = 32;

            var badgeLabel = UIHelper.CreateLabel(badgePanel.transform, resultText,
                14, UIHelper.HudTextColor, TextAnchor.MiddleCenter, true);
            UIHelper.StretchFull(badgeLabel.GetComponent<RectTransform>());
        }

        // ================================================================
        // Focus Button
        // ================================================================

        private void BuildFocusButton(HexCoordinate location)
        {
            var focusBtn = UIHelper.CreateButton(contentRT, "Focus Camera",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 11, () =>
                {
                    OnFocusCombat?.Invoke(location);
                });
            var focusLE = focusBtn.gameObject.AddComponent<LayoutElement>();
            focusLE.preferredHeight = 26;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void CreateTableCell(Transform parent, string text, float width, Color color,
            int fontSize, bool isBold)
        {
            var label = UIHelper.CreateLabel(parent, text, fontSize, color,
                TextAnchor.MiddleCenter, isBold);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
        }

        private Color GetPhaseColor(CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.RangedExchange: return SporefrontColors.SporeAmber;
                case CombatPhase.MeleeEngagement: return SporefrontColors.SporeRed;
                case CombatPhase.Cleanup: return SporefrontColors.SporePurple;
                case CombatPhase.Ended: return SporefrontColors.InkFaded;
                default: return SporefrontColors.InkLight;
            }
        }
    }
}
