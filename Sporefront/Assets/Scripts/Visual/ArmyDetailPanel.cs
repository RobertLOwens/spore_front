// ============================================================================
// FILE: Visual/ArmyDetailPanel.cs
// PURPOSE: Center modal for army inspection — composition, commander, stamina
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class ArmyDetailPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnMoveRequested;
        public event Action<Guid> OnAttackRequested;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private GameObject backdrop;
        private RectTransform contentRT;
        private Guid? currentArmyID;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "ArmyDetailBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Main panel — centered 380x450
            panel = UIHelper.CreatePanel(backdrop.transform, "ArmyDetailPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 380, 450);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "ArmyScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 40);
            scrollRT.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Close);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 36);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid armyID, GameState gameState)
        {
            currentArmyID = armyID;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Close()
        {
            currentArmyID = null;
            backdrop.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentArmyID.HasValue || !backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentArmyID.HasValue) return;
            var army = gameState.GetArmy(currentArmyID.Value);
            if (army == null) { Close(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;

            // Header: Army name + status
            string status = army.isEntrenched ? "Entrenched" :
                            army.isEntrenching ? "Entrenching" :
                            army.isInCombat ? "In Combat" :
                            army.isRetreating ? "Retreating" :
                            army.currentPath != null && army.currentPath.Count > 0 ? "Moving" :
                            "Idle";

            var header = UIHelper.CreateLabel(contentRT, army.name,
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            var statusLabel = UIHelper.CreateLabel(contentRT, status, 13,
                SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 20;

            UIHelper.CreateDivider(contentRT);

            // Composition
            BuildCompositionSection(army);
            UIHelper.CreateDivider(contentRT);

            // Commander
            if (army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    BuildCommanderSection(commander);
                    UIHelper.CreateDivider(contentRT);
                }
            }

            // Stamina bar
            BuildStaminaBar(army);
            UIHelper.CreateDivider(contentRT);

            // Actions (owned armies only)
            if (isOwned)
                BuildActionsSection(army);
        }

        // ================================================================
        // Composition
        // ================================================================

        private void BuildCompositionSection(ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Composition",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            if (army.militaryComposition != null)
            {
                foreach (var kvp in army.militaryComposition)
                {
                    if (kvp.Value <= 0) continue;
                    var label = UIHelper.CreateLabel(contentRT,
                        $"  {kvp.Key.DisplayName()}: {kvp.Value}", 12);
                    var le = label.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 20;
                }
            }

            var totalLabel = UIHelper.CreateLabel(contentRT,
                $"  Total: {army.GetTotalUnits()} units", 12,
                SporefrontColors.InkLight);
            var totalLE = totalLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.preferredHeight = 20;
        }

        // ================================================================
        // Commander
        // ================================================================

        private void BuildCommanderSection(CommanderData commander)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Commander",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var nameLabel = UIHelper.CreateLabel(contentRT,
                $"  {commander.name} — {commander.rank}", 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 20;

            var specialtyLabel = UIHelper.CreateLabel(contentRT,
                $"  Specialty: {commander.specialty}", 12, SporefrontColors.InkLight);
            var specLE = specialtyLabel.gameObject.AddComponent<LayoutElement>();
            specLE.preferredHeight = 20;

            // Stats
            var statsRow = UIHelper.CreateHorizontalRow(contentRT, 20f, 6f);

            CreateStatLabel(statsRow.transform, "Ldr", commander.Leadership);
            CreateStatLabel(statsRow.transform, "Tac", commander.Tactics);
            CreateStatLabel(statsRow.transform, "Log", commander.Logistics);
            CreateStatLabel(statsRow.transform, "Rat", commander.Rationing);
            CreateStatLabel(statsRow.transform, "End", commander.Endurance);
        }

        private void CreateStatLabel(Transform parent, string abbrev, int value)
        {
            var label = UIHelper.CreateLabel(parent, $"{abbrev}:{value}", 11,
                SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 50;
        }

        // ================================================================
        // Stamina
        // ================================================================

        private void BuildStaminaBar(ArmyData army)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            var label = UIHelper.CreateLabel(row.transform,
                $"Stamina: {(int)army.currentStamina}/{(int)army.maxStamina}", 12);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 130;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 14f,
                SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
            float pct = army.maxStamina > 0 ? (float)(army.currentStamina / army.maxStamina) : 0f;
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 14;
        }

        // ================================================================
        // Actions
        // ================================================================

        private void BuildActionsSection(ArmyData army)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 32f, 8f);

            // Move
            if (!army.isInCombat)
            {
                var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, 12, () =>
                    {
                        Close();
                        OnMoveRequested?.Invoke(army.id);
                    });
                var moveLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                moveLE.preferredWidth = 80;
                moveLE.preferredHeight = 32;
            }

            // Entrench
            if (!army.isEntrenched && !army.isEntrenching && !army.isInCombat)
            {
                var entrenchBtn = UIHelper.CreateButton(row.transform, "Entrench",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, 12, () =>
                    {
                        var cmd = new EntrenchCommand(localPlayerID, army.id);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var eLE = entrenchBtn.gameObject.AddComponent<LayoutElement>();
                eLE.preferredWidth = 80;
                eLE.preferredHeight = 32;
            }

            // Retreat
            if (army.isInCombat)
            {
                var retreatBtn = UIHelper.CreateButton(row.transform, "Retreat",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, () =>
                    {
                        var cmd = new RetreatCommand(localPlayerID, army.id);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var rLE = retreatBtn.gameObject.AddComponent<LayoutElement>();
                rLE.preferredWidth = 80;
                rLE.preferredHeight = 32;
            }

            // Attack
            if (!army.isInCombat && !army.isRetreating)
            {
                var attackBtn = UIHelper.CreateButton(row.transform, "Attack",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, () =>
                    {
                        Close();
                        OnAttackRequested?.Invoke(army.id);
                    });
                var aLE = attackBtn.gameObject.AddComponent<LayoutElement>();
                aLE.preferredWidth = 80;
                aLE.preferredHeight = 32;
            }
        }
    }
}
