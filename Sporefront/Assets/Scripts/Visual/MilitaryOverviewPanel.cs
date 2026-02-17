// ============================================================================
// FILE: Visual/MilitaryOverviewPanel.cs
// PURPOSE: Modal overview of military units — summary totals by category and
//          per-unit-type detail cards with stats. Port from MilitaryOverviewViewController.swift
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
    public class MilitaryOverviewPanel : MonoBehaviour
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
            backdrop = UIHelper.CreatePanel(canvasTransform, "MilitaryOverviewBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel -- centered 440x560
            panel = UIHelper.CreatePanel(backdrop.transform, "MilitaryOverviewPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, 440, 560);

            // Header
            var headerLabel = UIHelper.CreateLabel(panel.transform, "Military Overview",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(8, -32);
            headerRT.offsetMax = new Vector2(-8, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "MilitaryScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
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
            if (!IsVisible) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            // Clear content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            // Aggregate unit counts across all armies and garrisons
            var unitCounts = new Dictionary<MilitaryUnitType, int>();
            foreach (MilitaryUnitType ut in Enum.GetValues(typeof(MilitaryUnitType)))
                unitCounts[ut] = 0;

            var armies = gameState.GetArmiesForPlayer(localPlayerID);
            foreach (var army in armies)
            {
                foreach (var kvp in army.militaryComposition)
                {
                    if (unitCounts.ContainsKey(kvp.Key))
                        unitCounts[kvp.Key] += kvp.Value;
                    else
                        unitCounts[kvp.Key] = kvp.Value;
                }
            }

            var buildings = gameState.GetBuildingsForPlayer(localPlayerID);
            foreach (var building in buildings)
            {
                foreach (var kvp in building.garrison)
                {
                    if (unitCounts.ContainsKey(kvp.Key))
                        unitCounts[kvp.Key] += kvp.Value;
                    else
                        unitCounts[kvp.Key] = kvp.Value;
                }
            }

            // Category totals
            var categoryTotals = new Dictionary<UnitCategory, int>
            {
                { UnitCategory.Infantry, 0 },
                { UnitCategory.Ranged, 0 },
                { UnitCategory.Cavalry, 0 },
                { UnitCategory.Siege, 0 }
            };

            foreach (var kvp in unitCounts)
                categoryTotals[kvp.Key.Category()] += kvp.Value;

            int totalUnits = 0;
            foreach (var kvp in categoryTotals)
                totalUnits += kvp.Value;

            // Summary card
            BuildSummaryCard(categoryTotals, totalUnits);

            UIHelper.CreateDivider(contentRT);

            // Per-category sections with unit type detail cards
            UnitCategory[] categoryOrder = { UnitCategory.Infantry, UnitCategory.Ranged,
                                              UnitCategory.Cavalry, UnitCategory.Siege };

            foreach (var category in categoryOrder)
            {
                var unitsInCategory = new List<MilitaryUnitType>();
                foreach (MilitaryUnitType ut in Enum.GetValues(typeof(MilitaryUnitType)))
                {
                    if (ut.Category() == category)
                        unitsInCategory.Add(ut);
                }

                if (unitsInCategory.Count == 0) continue;

                // Category header
                var catHeader = UIHelper.CreateLabel(contentRT,
                    $"{CategoryDisplayName(category)} ({categoryTotals[category]})",
                    UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                    TextAnchor.MiddleLeft, true);
                var catHeaderLE = catHeader.gameObject.AddComponent<LayoutElement>();
                catHeaderLE.preferredHeight = 26;

                foreach (var ut in unitsInCategory)
                {
                    BuildUnitTypeCard(ut, unitCounts[ut], player);
                }

                UIHelper.CreateDivider(contentRT);
            }
        }

        // ================================================================
        // Summary Card
        // ================================================================

        private void BuildSummaryCard(Dictionary<UnitCategory, int> categoryTotals, int totalUnits)
        {
            var card = UIHelper.CreatePanel(contentRT, "SummaryCard", SporefrontColors.ParchmentMid);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 80;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(10, 10, 6, 6);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var totalLabel = UIHelper.CreateLabel(card.transform,
                $"Total Military: {totalUnits} units",
                UIHelper.DefaultHeaderFontSize - 1, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var totalLE = totalLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.preferredHeight = 24;

            // Category breakdown row
            var row = UIHelper.CreateHorizontalRow(card.transform, 20f, 8f);

            CreateCategoryStat(row.transform, "Infantry", categoryTotals[UnitCategory.Infantry]);
            CreateCategoryStat(row.transform, "Ranged", categoryTotals[UnitCategory.Ranged]);
            CreateCategoryStat(row.transform, "Cavalry", categoryTotals[UnitCategory.Cavalry]);
            CreateCategoryStat(row.transform, "Siege", categoryTotals[UnitCategory.Siege]);
        }

        private void CreateCategoryStat(Transform parent, string label, int count)
        {
            var text = UIHelper.CreateLabel(parent, $"{label}: {count}", 11,
                count > 0 ? SporefrontColors.InkDark : SporefrontColors.InkFaded,
                TextAnchor.MiddleCenter);
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 20;
        }

        // ================================================================
        // Unit Type Detail Card
        // ================================================================

        private void BuildUnitTypeCard(MilitaryUnitType unitType, int count, PlayerState player)
        {
            var card = UIHelper.CreatePanel(contentRT, "UnitCard",
                count > 0 ? SporefrontColors.ParchmentMid : new Color(
                    SporefrontColors.ParchmentMid.r, SporefrontColors.ParchmentMid.g,
                    SporefrontColors.ParchmentMid.b, 0.5f));
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 96;
            cardLE.flexibleWidth = 1;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(10, 10, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Row 1: Name + count
            var topRow = UIHelper.CreateHorizontalRow(card.transform, 20f, 4f);

            var nameLabel = UIHelper.CreateLabel(topRow.transform,
                unitType.DisplayName(), 13, UIHelper.HeaderTextColor);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 20;

            var countLabel = UIHelper.CreateLabel(topRow.transform,
                $"x{count}", 13, count > 0 ? SporefrontColors.SporeGreen : SporefrontColors.InkFaded,
                TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 40;
            countLE.preferredHeight = 20;

            // Row 2: HP, Attack, Armor
            var statsRow = UIHelper.CreateHorizontalRow(card.transform, 18f, 4f);
            var combatStats = unitType.CombatStats();

            // Get research bonuses
            var upgradeBonuses = player.GetUnitUpgradeBonus(unitType);

            double baseHP = unitType.HP();
            double totalHP = baseHP + upgradeBonuses.hpBonus;

            double baseAttack = combatStats.TotalDamage;
            double attackBonus = upgradeBonuses.attackBonus;

            double baseArmor = combatStats.AverageArmor;
            double armorBonus = upgradeBonuses.armorBonus;

            string hpStr = $"HP: {totalHP:F0}";
            if (upgradeBonuses.hpBonus > 0) hpStr += $" (+{upgradeBonuses.hpBonus:F0})";

            string atkStr = $"ATK: {baseAttack:F1}";
            if (attackBonus > 0) atkStr += $" (+{attackBonus:F1})";

            string armStr = $"ARM: {baseArmor:F1}";
            if (armorBonus > 0) armStr += $" (+{armorBonus:F1})";

            CreateStatLabel(statsRow.transform, hpStr);
            CreateStatLabel(statsRow.transform, atkStr);
            CreateStatLabel(statsRow.transform, armStr);

            // Row 3: Speed, Training info
            var infoRow = UIHelper.CreateHorizontalRow(card.transform, 18f, 4f);

            double moveSpeed = unitType.MoveSpeed();
            CreateStatLabel(infoRow.transform, $"Speed: {moveSpeed:F2}s/hex");

            double trainingTime = unitType.TrainingTime();
            CreateStatLabel(infoRow.transform, $"Train: {trainingTime:F0}s");

            var trainingBuilding = unitType.TrainingBuilding();
            CreateStatLabel(infoRow.transform, trainingBuilding.DisplayName());

            // Row 4: Training cost
            var costRow = UIHelper.CreateHorizontalRow(card.transform, 16f, 4f);
            var cost = unitType.TrainingCost();
            var costLabel = UIHelper.CreateLabel(costRow.transform,
                $"Cost: {FormatCost(cost)}", 10, SporefrontColors.InkLight);
            costLabel.supportRichText = true;
            var costLabelLE = costLabel.gameObject.AddComponent<LayoutElement>();
            costLabelLE.flexibleWidth = 1;
            costLabelLE.preferredHeight = 16;

            int popSpace = unitType.PopSpace();
            if (popSpace > 1)
            {
                var popLabel = UIHelper.CreateLabel(costRow.transform,
                    $"Pop: {popSpace}", 10, SporefrontColors.InkLight, TextAnchor.MiddleRight);
                var popLE = popLabel.gameObject.AddComponent<LayoutElement>();
                popLE.preferredWidth = 40;
                popLE.preferredHeight = 16;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void CreateStatLabel(Transform parent, string text)
        {
            var label = UIHelper.CreateLabel(parent, text, 10, SporefrontColors.InkLight);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredHeight = 18;
        }

        private string CategoryDisplayName(UnitCategory category)
        {
            switch (category)
            {
                case UnitCategory.Infantry: return "Infantry";
                case UnitCategory.Ranged: return "Ranged";
                case UnitCategory.Cavalry: return "Cavalry";
                case UnitCategory.Siege: return "Siege";
                default: return category.ToString();
            }
        }

        private string FormatCost(Dictionary<ResourceType, int> cost)
        {
            var parts = new List<string>();
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0)
                    parts.Add($"{UIHelper.ResourceIcon(kvp.Key)}{kvp.Value}");
            }
            return string.Join(" ", parts);
        }
    }
}
