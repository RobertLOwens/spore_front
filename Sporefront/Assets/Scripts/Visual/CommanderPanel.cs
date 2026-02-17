// ============================================================================
// FILE: Visual/CommanderPanel.cs
// PURPOSE: Modal panel for commander management — list, detail, stats, recruit
//          Ported from CommanderViewController.swift
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
    public class CommanderPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<CommanderSpecialty> OnRecruitCommander;
        public event Action OnClose;
        public event Action<Guid> OnCommanderSelected;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform listContentRT;
        private RectTransform detailContentRT;
        private Guid localPlayerID;
        private Guid? selectedCommanderID;
        private bool isRecruitFlowActive;

        // Cached references for live updates
        private Image xpFill;
        private Text xpLabel;
        private Image staminaFill;
        private Text staminaLabel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "CommanderBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 700x520
            panel = UIHelper.CreatePanel(backdrop.transform, "CommanderPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 700, 520);

            // Title
            var titleLabel = UIHelper.CreateLabel(panel.transform, "Commanders",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleRT = titleLabel.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.offsetMin = new Vector2(8, -36);
            titleRT.offsetMax = new Vector2(-8, -4);

            // Left side — commander list (width ~220)
            var listPanel = UIHelper.CreatePanel(panel.transform, "ListPanel",
                new Color(SporefrontColors.ParchmentMid.r, SporefrontColors.ParchmentMid.g,
                    SporefrontColors.ParchmentMid.b, 0.5f));
            var listPanelRT = listPanel.GetComponent<RectTransform>();
            listPanelRT.anchorMin = new Vector2(0, 0);
            listPanelRT.anchorMax = new Vector2(0.32f, 1);
            listPanelRT.offsetMin = new Vector2(6, 44);
            listPanelRT.offsetMax = new Vector2(0, -40);

            var listScroll = UIHelper.CreateScrollView(listPanel.transform, "CommanderList", out listContentRT);
            var listScrollRT = listScroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(listScrollRT);

            // Right side — detail view
            var detailPanel = UIHelper.CreatePanel(panel.transform, "DetailPanel", Color.clear);
            var detailPanelRT = detailPanel.GetComponent<RectTransform>();
            detailPanelRT.anchorMin = new Vector2(0.32f, 0);
            detailPanelRT.anchorMax = new Vector2(1, 1);
            detailPanelRT.offsetMin = new Vector2(4, 44);
            detailPanelRT.offsetMax = new Vector2(-6, -40);

            var detailScroll = UIHelper.CreateScrollView(detailPanel.transform, "DetailScroll", out detailContentRT);
            var detailScrollRT = detailScroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(detailScrollRT);

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 38);

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
            isRecruitFlowActive = false;
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
            RebuildList(gameState);
            RebuildDetail(gameState);
        }

        // ================================================================
        // Commander List (Left Side)
        // ================================================================

        private void RebuildList(GameState gameState)
        {
            // Clear
            for (int i = listContentRT.childCount - 1; i >= 0; i--)
                Destroy(listContentRT.GetChild(i).gameObject);

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            var commanders = gameState.GetCommandersForPlayer(localPlayerID);

            if (commanders.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(listContentRT, "No commanders.\nRecruit one below.",
                    12, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 60;
            }

            foreach (var commander in commanders)
            {
                bool isSelected = selectedCommanderID.HasValue && selectedCommanderID.Value == commander.id;
                Color rowBg = isSelected ? SporefrontColors.ParchmentDark : SporefrontColors.ParchmentLight;

                var rowPanel = UIHelper.CreatePanel(listContentRT, "CmdrRow", rowBg);
                var rowLE = rowPanel.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 52;

                var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(6, 6, 4, 4);
                vlg.spacing = 2;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;

                // Name + rank
                var nameLabel = UIHelper.CreateLabel(rowPanel.transform,
                    $"{commander.name} ({commander.rank.DisplayName()})",
                    13, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, false);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredHeight = 20;

                // Specialty
                var specLabel = UIHelper.CreateLabel(rowPanel.transform,
                    commander.specialty.DisplayName(), 11, SporefrontColors.InkLight);
                var specLE = specLabel.gameObject.AddComponent<LayoutElement>();
                specLE.preferredHeight = 16;

                // Click handler
                var capturedID = commander.id;
                var clickBtn = rowPanel.AddComponent<Button>();
                clickBtn.transition = Selectable.Transition.None;
                clickBtn.onClick.AddListener(() =>
                {
                    selectedCommanderID = capturedID;
                    OnCommanderSelected?.Invoke(capturedID);
                    // Lazy rebuild on next Refresh
                });
            }

            UIHelper.CreateDivider(listContentRT);

            // Recruit button
            var recruitBtn = UIHelper.CreateButton(listContentRT, "Recruit Commander",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, () =>
                {
                    isRecruitFlowActive = true;
                });
            var recruitLE = recruitBtn.gameObject.AddComponent<LayoutElement>();
            recruitLE.preferredHeight = 34;
        }

        // ================================================================
        // Commander Detail (Right Side)
        // ================================================================

        private void RebuildDetail(GameState gameState)
        {
            // Clear
            for (int i = detailContentRT.childCount - 1; i >= 0; i--)
                Destroy(detailContentRT.GetChild(i).gameObject);

            xpFill = null;
            xpLabel = null;
            staminaFill = null;
            staminaLabel = null;

            if (isRecruitFlowActive)
            {
                BuildRecruitFlow();
                return;
            }

            if (!selectedCommanderID.HasValue)
            {
                var placeholder = UIHelper.CreateLabel(detailContentRT,
                    "Select a commander to view details.",
                    13, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var plLE = placeholder.gameObject.AddComponent<LayoutElement>();
                plLE.preferredHeight = 60;
                return;
            }

            var commander = gameState.GetCommander(selectedCommanderID.Value);
            if (commander == null)
            {
                selectedCommanderID = null;
                return;
            }

            // Name + specialty header
            var header = UIHelper.CreateLabel(detailContentRT, commander.name,
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 30;

            // Specialty + icon
            var specRow = UIHelper.CreateHorizontalRow(detailContentRT, 22f, 6f);
            var iconLabel = UIHelper.CreateLabel(specRow.transform,
                $"[{commander.specialty.Icon()}]", 12, SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
            var iconLE = iconLabel.gameObject.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 60;

            var specName = UIHelper.CreateLabel(specRow.transform,
                commander.specialty.DisplayName(), 13, UIHelper.BodyTextColor);
            var specNameLE = specName.gameObject.AddComponent<LayoutElement>();
            specNameLE.flexibleWidth = 1;

            // Level + Rank
            var levelRow = UIHelper.CreateHorizontalRow(detailContentRT, 22f, 6f);
            var levelLabel = UIHelper.CreateLabel(levelRow.transform,
                $"Level {commander.level}", 13, UIHelper.BodyTextColor);
            var levelLE = levelLabel.gameObject.AddComponent<LayoutElement>();
            levelLE.preferredWidth = 80;

            var rankLabel = UIHelper.CreateLabel(levelRow.transform,
                $"Rank: {commander.rank.DisplayName()} [{commander.rank.Icon()}]",
                13, SporefrontColors.SporeAmber);
            var rankLE = rankLabel.gameObject.AddComponent<LayoutElement>();
            rankLE.flexibleWidth = 1;

            // XP Progress Bar
            BuildXPBar(commander);

            UIHelper.CreateDivider(detailContentRT);

            // Stamina Bar
            BuildStaminaBar(commander);

            UIHelper.CreateDivider(detailContentRT);

            // Stats Section
            BuildStatsSection(commander);

            UIHelper.CreateDivider(detailContentRT);

            // Stat Benefits
            BuildStatBenefits(commander);

            UIHelper.CreateDivider(detailContentRT);

            // Assignment info
            BuildAssignmentInfo(commander, gameState);
        }

        // ================================================================
        // XP Progress Bar
        // ================================================================

        private void BuildXPBar(CommanderData commander)
        {
            int requiredXP = commander.level * 100;
            float xpPct = requiredXP > 0 ? Mathf.Clamp01((float)commander.experience / requiredXP) : 0f;

            var row = UIHelper.CreateHorizontalRow(detailContentRT, 20f, 4f);

            xpLabel = UIHelper.CreateLabel(row.transform,
                $"XP: {commander.experience}/{requiredXP}", 11, SporefrontColors.InkLight);
            var labelLE = xpLabel.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 100;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 14f,
                SporefrontColors.InkFaded, SporefrontColors.SporePurple);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(xpPct, 1);
            xpFill = fill;
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 14;
        }

        // ================================================================
        // Stamina Bar
        // ================================================================

        private void BuildStaminaBar(CommanderData commander)
        {
            float staminaPct = Mathf.Clamp01((float)(commander.stamina / CommanderData.MaxStamina));

            var row = UIHelper.CreateHorizontalRow(detailContentRT, 20f, 4f);

            staminaLabel = UIHelper.CreateLabel(row.transform,
                $"Stamina: {(int)commander.stamina}/{(int)CommanderData.MaxStamina}", 12);
            var labelLE = staminaLabel.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 140;

            Color staminaColor = staminaPct > 0.5f ? SporefrontColors.SporeTeal :
                                 staminaPct > 0.2f ? SporefrontColors.SporeAmber :
                                 SporefrontColors.SporeRed;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 14f,
                SporefrontColors.InkFaded, staminaColor);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(staminaPct, 1);
            staminaFill = fill;
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 14;
        }

        // ================================================================
        // Stats Section — 5 stat bars
        // ================================================================

        private void BuildStatsSection(CommanderData commander)
        {
            var sectionLabel = UIHelper.CreateLabel(detailContentRT, "Commander Stats",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            BuildStatBar("Leadership", commander.Leadership, SporefrontColors.SporeAmber);
            BuildStatBar("Tactics", commander.Tactics, SporefrontColors.SporeRed);
            BuildStatBar("Logistics", commander.Logistics, SporefrontColors.SporeTeal);
            BuildStatBar("Rationing", commander.Rationing, SporefrontColors.SporeGreen);
            BuildStatBar("Endurance", commander.Endurance, SporefrontColors.SporePurple);
        }

        private void BuildStatBar(string statName, int value, Color barColor)
        {
            // Max stat for display scaling (base 12 + growth can get to ~50+ at high levels)
            const int displayMax = 50;
            float pct = Mathf.Clamp01((float)value / displayMax);

            var row = UIHelper.CreateHorizontalRow(detailContentRT, 20f, 4f);

            var nameLabel = UIHelper.CreateLabel(row.transform, statName, 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 90;

            var (bg, fill) = UIHelper.CreateProgressBar(row.transform, 12f,
                SporefrontColors.InkFaded, barColor);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(pct, 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.flexibleWidth = 1;
            barLE.preferredHeight = 12;

            var valLabel = UIHelper.CreateLabel(row.transform, value.ToString(), 12,
                UIHelper.BodyTextColor, TextAnchor.MiddleRight);
            var valLE = valLabel.gameObject.AddComponent<LayoutElement>();
            valLE.preferredWidth = 30;
        }

        // ================================================================
        // Stat Benefits
        // ================================================================

        private void BuildStatBenefits(CommanderData commander)
        {
            var sectionLabel = UIHelper.CreateLabel(detailContentRT, "Stat Effects",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            int maxArmySize = GameConfig.Commander.LeadershipToArmySizeBase +
                              commander.Leadership * GameConfig.Commander.LeadershipToArmySizePerPoint;
            CreateBenefitRow("Leadership", $"Max Army Size: {maxArmySize}");

            double atkBonus = commander.GetAttackBonus(UnitCategory.Infantry);
            double defBonus = commander.GetDefenseBonus();
            CreateBenefitRow("Tactics", $"Attack +{atkBonus:P0}, Defense +{defBonus:P0}");

            double speedBonus = commander.GetSpeedBonus();
            CreateBenefitRow("Logistics", $"Movement Speed x{speedBonus:F2}");

            CreateBenefitRow("Rationing", "Reduces army food consumption");

            double regenMult = 1.0 + commander.Endurance * GameConfig.Commander.EnduranceRegenScaling;
            CreateBenefitRow("Endurance", $"Stamina Regen x{regenMult:F2}");
        }

        private void CreateBenefitRow(string statName, string benefit)
        {
            var row = UIHelper.CreateHorizontalRow(detailContentRT, 18f, 4f);

            var nameLabel = UIHelper.CreateLabel(row.transform, $"{statName}:", 11,
                SporefrontColors.InkMid);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 80;

            var benefitLabel = UIHelper.CreateLabel(row.transform, benefit, 11,
                SporefrontColors.InkLight);
            var benefitLE = benefitLabel.gameObject.AddComponent<LayoutElement>();
            benefitLE.flexibleWidth = 1;
        }

        // ================================================================
        // Assignment Info
        // ================================================================

        private void BuildAssignmentInfo(CommanderData commander, GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(detailContentRT, "Assignment",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            if (commander.assignedArmyID.HasValue)
            {
                var army = gameState.GetArmy(commander.assignedArmyID.Value);
                if (army != null)
                {
                    var armyLabel = UIHelper.CreateLabel(detailContentRT,
                        $"Assigned to: {army.name}", 12);
                    var armyLE = armyLabel.gameObject.AddComponent<LayoutElement>();
                    armyLE.preferredHeight = 20;

                    var locLabel = UIHelper.CreateLabel(detailContentRT,
                        $"Location: ({army.coordinate.q}, {army.coordinate.r})", 11,
                        SporefrontColors.InkLight);
                    var locLE = locLabel.gameObject.AddComponent<LayoutElement>();
                    locLE.preferredHeight = 18;

                    var unitsLabel = UIHelper.CreateLabel(detailContentRT,
                        $"Army Strength: {army.GetTotalUnits()} units", 11,
                        SporefrontColors.InkLight);
                    var unitsLE = unitsLabel.gameObject.AddComponent<LayoutElement>();
                    unitsLE.preferredHeight = 18;
                }
                else
                {
                    var missingLabel = UIHelper.CreateLabel(detailContentRT,
                        "Assigned army not found", 12, SporefrontColors.SporeRed);
                    var missingLE = missingLabel.gameObject.AddComponent<LayoutElement>();
                    missingLE.preferredHeight = 20;
                }
            }
            else
            {
                var unassignedLabel = UIHelper.CreateLabel(detailContentRT,
                    "Unassigned (available for deployment)", 12, SporefrontColors.InkLight);
                var unassignedLE = unassignedLabel.gameObject.AddComponent<LayoutElement>();
                unassignedLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Recruit Flow — Specialty Selection
        // ================================================================

        private void BuildRecruitFlow()
        {
            var header = UIHelper.CreateLabel(detailContentRT, "Recruit Commander",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 30;

            var infoLabel = UIHelper.CreateLabel(detailContentRT,
                "Choose a specialty for the new commander:", 12,
                SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 24;

            UIHelper.CreateDivider(detailContentRT);

            var specialties = (CommanderSpecialty[])Enum.GetValues(typeof(CommanderSpecialty));
            foreach (var spec in specialties)
            {
                var capturedSpec = spec;

                var rowPanel = UIHelper.CreatePanel(detailContentRT, "SpecRow", SporefrontColors.ParchmentMid);
                var rowLE = rowPanel.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 56;

                var vlg = rowPanel.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 4, 4);
                vlg.spacing = 2;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;

                // Specialty name + icon
                var nameRow = UIHelper.CreateHorizontalRow(rowPanel.transform, 20f, 4f);

                var iconLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"[{spec.Icon()}]", 12, SporefrontColors.SporeAmber);
                var iconLE2 = iconLabel.gameObject.AddComponent<LayoutElement>();
                iconLE2.preferredWidth = 50;

                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    spec.DisplayName(), 13, UIHelper.HeaderTextColor);
                var nameLE2 = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE2.flexibleWidth = 1;

                // Description
                var descLabel = UIHelper.CreateLabel(rowPanel.transform,
                    spec.Description(), 10, SporefrontColors.InkLight);
                var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
                descLE.preferredHeight = 16;

                // Click to recruit
                var clickBtn = rowPanel.AddComponent<Button>();
                clickBtn.transition = Selectable.Transition.None;
                clickBtn.onClick.AddListener(() =>
                {
                    isRecruitFlowActive = false;
                    OnRecruitCommander?.Invoke(capturedSpec);
                });
            }

            UIHelper.CreateDivider(detailContentRT);

            // Cancel button
            var cancelBtn = UIHelper.CreateButton(detailContentRT, "Cancel",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 12, () =>
                {
                    isRecruitFlowActive = false;
                });
            var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            cancelLE.preferredHeight = 30;
        }
    }
}
