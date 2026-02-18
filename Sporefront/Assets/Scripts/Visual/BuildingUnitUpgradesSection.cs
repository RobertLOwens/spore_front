// ============================================================================
// FILE: Visual/BuildingUnitUpgradesSection.cs
// PURPOSE: Unit upgrades UI section — extracted from BuildingDetailPanel
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
    public static class BuildingUnitUpgradesSection
    {
        public static void Build(RectTransform contentRT, BuildingData building,
            GameState gameState, PlayerState player, Guid localPlayerID,
            Guid? currentBuildingID, List<UnitUpgradeType> upgrades)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Unit Upgrades",
                UIConstants.FontSubheader, SporefrontColors.SporeAmber,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            // Group by unit type
            MilitaryUnitType? currentUnit = null;
            foreach (var upgrade in upgrades)
            {
                var unitType = upgrade.GetUnitType();
                if (!currentUnit.HasValue || currentUnit.Value != unitType)
                {
                    currentUnit = unitType;
                    int tier = player != null ? player.GetUnitUpgradeTier(unitType) : 0;
                    var unitHeader = UIHelper.CreateLabel(contentRT,
                        $"{unitType.DisplayName()} (Tier {tier})", 12,
                        SporefrontColors.InkDark, TextAnchor.MiddleLeft, true);
                    var unitHeaderLE = unitHeader.gameObject.AddComponent<LayoutElement>();
                    unitHeaderLE.preferredHeight = 20;
                }

                bool completed = player != null && player.HasCompletedUnitUpgrade(upgrade.ToString());
                bool isActive = player != null && player.activeUnitUpgrade == upgrade.ToString();
                bool prereqMet = true;
                var prereq = upgrade.Prerequisite();
                if (prereq.HasValue && player != null)
                    prereqMet = player.HasCompletedUnitUpgrade(prereq.Value.ToString());
                bool levelMet = building.level >= upgrade.RequiredBuildingLevel();
                bool canStart = !completed && !isActive && prereqMet && levelMet
                    && player != null && !player.IsUnitUpgradeActive()
                    && player.CanAfford(upgrade.Cost());

                var row = UIHelper.CreateHorizontalRow(contentRT, 26f, 4f);

                // Status indicator
                string statusText;
                Color statusColor;
                if (completed)
                {
                    statusText = "Done";
                    statusColor = SporefrontColors.SporeGreen;
                }
                else if (isActive)
                {
                    statusText = "Active";
                    statusColor = SporefrontColors.SporeTeal;
                }
                else if (!prereqMet || !levelMet)
                {
                    statusText = "Locked";
                    statusColor = SporefrontColors.InkFaded;
                }
                else
                {
                    statusText = $"Tier {upgrade.Tier()}";
                    statusColor = SporefrontColors.InkMid;
                }

                var statusLabel = UIHelper.CreateLabel(row.transform, statusText, 11, statusColor);
                var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
                statusLE.preferredWidth = 45;

                // Cost
                if (!completed)
                {
                    var cost = upgrade.Cost();
                    var costLabel = UIHelper.CreateLabel(row.transform, UIHelper.FormatCost(cost), 10,
                        (canStart || isActive) ? SporefrontColors.InkLight : SporefrontColors.InkFaded);
                    costLabel.supportRichText = true;
                    var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
                    costLE.flexibleWidth = 1;
                }
                else
                {
                    var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                    spacer.transform.SetParent(row.transform, false);
                    spacer.GetComponent<LayoutElement>().flexibleWidth = 1;
                }

                // Action button
                if (!completed && !isActive)
                {
                    var capturedUpgrade = upgrade;
                    var capturedBuildingID2 = currentBuildingID;
                    var btn = UIHelper.CreateButton(row.transform, "Upgrade",
                        canStart ? SporefrontColors.SporeAmber : SporefrontColors.InkFaded,
                        canStart ? UIHelper.ButtonText : SporefrontColors.InkLight, 11, () =>
                        {
                            if (!capturedBuildingID2.HasValue) return;
                            var cmd = new UpgradeUnitCommand(localPlayerID,
                                capturedUpgrade.ToString(), capturedBuildingID2.Value);
                            GameEngine.Instance.ExecuteCommand(cmd);
                        });
                    btn.interactable = canStart;
                    var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 60;
                }

                // Active progress bar
                if (isActive && player.activeUnitUpgradeStartTime.HasValue)
                {
                    double elapsed = gameState.currentTime - player.activeUnitUpgradeStartTime.Value;
                    double total = upgrade.UpgradeTime();
                    double pct = Math.Min(1.0, elapsed / total);
                    double remaining = Math.Max(0, total - elapsed);

                    var progressRow = UIHelper.CreateHorizontalRow(contentRT, 16f, 4f);
                    var (bg, fill) = UIHelper.CreateProgressBar(progressRow.transform, 12f,
                        SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)pct), 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.flexibleWidth = 1;
                    barLE.preferredHeight = 12;

                    var timeLabel = UIHelper.CreateLabel(progressRow.transform,
                        $"~{UIHelper.FormatTime(remaining)}", UIConstants.FontCaption,
                        SporefrontColors.InkLight);
                    var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
                    timeLE.preferredWidth = 45;
                }
            }
        }
    }
}
