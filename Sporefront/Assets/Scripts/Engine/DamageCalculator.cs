using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Models;
using Sporefront.Models.Combat;
using Sporefront.Data;

namespace Sporefront.Engine
{
    public static class DamageCalculator
    {
        // Charge Bonuses
        public static readonly double CavalryChargeBonus = GameConfig.Combat.CavalryChargeBonus;
        public static readonly double InfantryChargeBonus = GameConfig.Combat.InfantryChargeBonus;

        // Terrain Modifier

        public static double ApplyTerrainModifier(double dps, double terrainPenalty, double terrainBonus, double tacticsBonus = 0)
        {
            double scaledTerrainBonus = terrainBonus * (1.0 + tacticsBonus);
            double modifier = 1.0 - terrainPenalty + scaledTerrainBonus;
            return dps * modifier;
        }

        // Weighted Bonus

        public static double CalculateWeightedBonus(UnitCombatStats attackerStats, SideCombatState enemyState)
        {
            double totalEnemyUnits = enemyState.TotalUnits;
            if (totalEnemyUnits <= 0) return 0;

            double infantryRatio = enemyState.InfantryUnits / totalEnemyUnits;
            double cavalryRatio = enemyState.CavalryUnits / totalEnemyUnits;
            double rangedRatio = enemyState.RangedUnits / totalEnemyUnits;
            double siegeRatio = enemyState.SiegeUnits / totalEnemyUnits;

            return attackerStats.bonusVsInfantry * infantryRatio
                 + attackerStats.bonusVsCavalry * cavalryRatio
                 + attackerStats.bonusVsRanged * rangedRatio
                 + attackerStats.bonusVsSiege * siegeRatio;
        }

        // Research Bonus Lookup

        public static double GetResearchDamageBonus(MilitaryUnitType unitType, PlayerState playerState)
        {
            if (playerState == null) return 0;
            switch (unitType.Category())
            {
                case UnitCategory.Infantry:
                    return playerState.GetResearchBonus(ResearchBonusType.InfantryMeleeAttack.ToString());
                case UnitCategory.Cavalry:
                    return playerState.GetResearchBonus(ResearchBonusType.CavalryMeleeAttack.ToString());
                case UnitCategory.Ranged:
                    return playerState.GetResearchBonus(ResearchBonusType.PiercingDamage.ToString());
                case UnitCategory.Siege:
                    return playerState.GetResearchBonus(ResearchBonusType.SiegeBludgeonDamage.ToString());
                default:
                    return 0;
            }
        }

        public static double GetResearchMeleeArmorBonus(UnitCategory category, PlayerState playerState)
        {
            if (playerState == null) return 0;
            switch (category)
            {
                case UnitCategory.Infantry:
                    return playerState.GetResearchBonus(ResearchBonusType.InfantryMeleeArmor.ToString());
                case UnitCategory.Cavalry:
                    return playerState.GetResearchBonus(ResearchBonusType.CavalryMeleeArmor.ToString());
                case UnitCategory.Ranged:
                    return playerState.GetResearchBonus(ResearchBonusType.ArcherMeleeArmor.ToString());
                default:
                    return 0;
            }
        }

        public static double GetResearchPierceArmorBonus(UnitCategory category, PlayerState playerState)
        {
            if (playerState == null) return 0;
            switch (category)
            {
                case UnitCategory.Infantry:
                    return playerState.GetResearchBonus(ResearchBonusType.InfantryPierceArmor.ToString());
                case UnitCategory.Cavalry:
                    return playerState.GetResearchBonus(ResearchBonusType.CavalryPierceArmor.ToString());
                case UnitCategory.Ranged:
                    return playerState.GetResearchBonus(ResearchBonusType.ArcherPierceArmor.ToString());
                default:
                    return 0;
            }
        }

        // Unit Upgrade Bonus Lookup

        public static double GetUnitUpgradeDamageBonus(MilitaryUnitType unitType, PlayerState playerState)
        {
            if (playerState == null) return 0;
            return playerState.GetUnitUpgradeBonus(unitType).attackBonus;
        }

        public static double GetUnitUpgradeArmorBonus(MilitaryUnitType unitType, PlayerState playerState)
        {
            if (playerState == null) return 0;
            return playerState.GetUnitUpgradeBonus(unitType).armorBonus;
        }

        // DPS Calculations

        public static double CalculateRangedDPS(
            SideCombatState sideState,
            SideCombatState enemyState,
            double terrainPenalty = 0,
            double terrainBonus = 0,
            PlayerState playerState = null,
            double tacticsBonus = 0,
            double stretchingMultiplier = 1.0,
            CommanderData commanderData = null)
        {
            double totalDPS = 0;

            foreach (var kvp in sideState.unitCounts)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                UnitCategory category = unitType.Category();
                if (category != UnitCategory.Ranged && category != UnitCategory.Siege) continue;

                UnitCombatStats stats = unitType.CombatStats();
                double researchBonus = GetResearchDamageBonus(unitType, playerState);
                double upgradeBonus = GetUnitUpgradeDamageBonus(unitType, playerState);
                double baseDamage = stats.TotalDamage + researchBonus + upgradeBonus;
                double bonusDamage = CalculateWeightedBonus(stats, enemyState);
                double attackSpeed = unitType.AttackSpeed();
                double unitDPS = Math.Max(1.0 / attackSpeed, (baseDamage + bonusDamage) / attackSpeed);

                if (commanderData != null)
                {
                    unitDPS *= (1.0 + commanderData.GetAttackBonus(category));
                }

                totalDPS += unitDPS * count;
            }

            return ApplyTerrainModifier(totalDPS * stretchingMultiplier, terrainPenalty, terrainBonus, tacticsBonus);
        }

        public static double CalculateMeleeDPS(
            SideCombatState sideState,
            SideCombatState enemyState,
            bool isCharge,
            double terrainPenalty = 0,
            double terrainBonus = 0,
            PlayerState playerState = null,
            double tacticsBonus = 0,
            double stretchingMultiplier = 1.0,
            CommanderData commanderData = null)
        {
            double totalDPS = 0;

            foreach (var kvp in sideState.unitCounts)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                UnitCategory category = unitType.Category();
                if (category != UnitCategory.Infantry && category != UnitCategory.Cavalry) continue;

                UnitCombatStats stats = unitType.CombatStats();
                double researchBonus = GetResearchDamageBonus(unitType, playerState);
                double upgradeBonus = GetUnitUpgradeDamageBonus(unitType, playerState);
                double baseDamage = stats.TotalDamage + researchBonus + upgradeBonus;
                double bonusDamage = CalculateWeightedBonus(stats, enemyState);
                double attackSpeed = unitType.AttackSpeed();
                double unitDPS = Math.Max(1.0 / attackSpeed, (baseDamage + bonusDamage) / attackSpeed);

                if (isCharge)
                {
                    if (category == UnitCategory.Cavalry)
                    {
                        unitDPS *= (1.0 + CavalryChargeBonus);
                    }
                    else if (category == UnitCategory.Infantry)
                    {
                        unitDPS *= (1.0 + InfantryChargeBonus);
                    }
                }

                if (commanderData != null)
                {
                    unitDPS *= (1.0 + commanderData.GetAttackBonus(category));
                }

                totalDPS += unitDPS * count;
            }

            return ApplyTerrainModifier(totalDPS * stretchingMultiplier, terrainPenalty, terrainBonus, tacticsBonus);
        }

        public static double CalculateTotalDPS(
            SideCombatState sideState,
            SideCombatState enemyState,
            double terrainPenalty = 0,
            double terrainBonus = 0,
            PlayerState playerState = null,
            double tacticsBonus = 0,
            double stretchingMultiplier = 1.0,
            CommanderData commanderData = null)
        {
            double totalDPS = 0;

            foreach (var kvp in sideState.unitCounts)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                UnitCombatStats stats = unitType.CombatStats();
                double researchBonus = GetResearchDamageBonus(unitType, playerState);
                double upgradeBonus = GetUnitUpgradeDamageBonus(unitType, playerState);
                double baseDamage = stats.TotalDamage + researchBonus + upgradeBonus;
                double bonusDamage = CalculateWeightedBonus(stats, enemyState);
                double attackSpeed = unitType.AttackSpeed();
                double unitDPS = Math.Max(1.0 / attackSpeed, (baseDamage + bonusDamage) / attackSpeed);

                if (commanderData != null)
                {
                    unitDPS *= (1.0 + commanderData.GetAttackBonus(unitType.Category()));
                }

                totalDPS += unitDPS * count;
            }

            return ApplyTerrainModifier(totalDPS * stretchingMultiplier, terrainPenalty, terrainBonus, tacticsBonus);
        }

        // Damage Application

        public static void ApplyDamageToSide(
            SideCombatState sideState,
            double damage,
            ActiveCombat combat,
            bool isDefender,
            GameState state,
            string damageType = "all")
        {
            PlayerState receiverPlayerState = isDefender ? combat.defenderPlayerState : combat.attackerPlayerState;

            CommanderData defenderCommander = isDefender ? combat.defenderCommanderData : combat.attackerCommanderData;
            double defenseBonus = defenderCommander != null ? defenderCommander.GetDefenseBonus() : 0;
            double remainingDamage = damage * (1.0 - defenseBonus);

            UnitCategory[] priorityOrder;
            switch (damageType)
            {
                case "ranged":
                    priorityOrder = new[] { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Siege, UnitCategory.Ranged };
                    break;
                case "melee":
                    priorityOrder = new[] { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Siege, UnitCategory.Ranged };
                    break;
                default:
                    priorityOrder = new[] { UnitCategory.Siege, UnitCategory.Ranged, UnitCategory.Infantry, UnitCategory.Cavalry };
                    break;
            }

            foreach (UnitCategory category in priorityOrder)
            {
                if (remainingDamage <= 0) break;

                var unitsInCategory = new List<KeyValuePair<MilitaryUnitType, int>>();
                foreach (var kvp in sideState.unitCounts)
                {
                    if (kvp.Key.Category() == category && kvp.Value > 0)
                        unitsInCategory.Add(kvp);
                }

                foreach (var kvp in unitsInCategory)
                {
                    if (remainingDamage <= 0) break;

                    MilitaryUnitType unitType = kvp.Key;
                    int count = kvp.Value;

                    UnitUpgradeBonusData upgradeBonus = receiverPlayerState != null
                        ? receiverPlayerState.GetUnitUpgradeBonus(unitType)
                        : new UnitUpgradeBonusData(0, 0, 0);
                    double effectiveHP = unitType.HP() + upgradeBonus.hpBonus;
                    double armorReduction = upgradeBonus.armorBonus * count;
                    double effectiveDamage = Math.Max(0, Math.Min(remainingDamage, count * effectiveHP) - armorReduction);
                    double damageToApply = effectiveDamage;
                    int kills = sideState.ApplyDamage(damageToApply, unitType, effectiveHP);

                    if (kills > 0)
                    {
                        combat.TrackPhaseCasualty(!isDefender, unitType, kills);

                        if (isDefender)
                        {
                            if (combat.defenderArmies.Count > 0)
                            {
                                Guid armyID = combat.defenderArmies[0].armyID;
                                ArmyData army = state.GetArmy(armyID);
                                if (army != null)
                                {
                                    army.RemoveMilitaryUnits(unitType, kills);
                                }
                            }
                        }
                        else
                        {
                            if (combat.attackerArmies.Count > 0)
                            {
                                Guid armyID = combat.attackerArmies[0].armyID;
                                ArmyData army = state.GetArmy(armyID);
                                if (army != null)
                                {
                                    army.RemoveMilitaryUnits(unitType, kills);
                                }
                            }
                        }
                    }

                    remainingDamage -= damageToApply;
                }
            }
        }

        public static Dictionary<MilitaryUnitType, int> ApplyDamageToArmy(ArmyData army, double damage, PlayerState playerState = null)
        {
            var casualties = new Dictionary<MilitaryUnitType, int>();
            double remainingDamage = damage;

            var entries = new List<KeyValuePair<MilitaryUnitType, int>>(army.militaryComposition);
            foreach (var kvp in entries)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;
                if (remainingDamage <= 0 || count <= 0) continue;

                UnitUpgradeBonusData upgradeBonus = playerState != null
                    ? playerState.GetUnitUpgradeBonus(unitType)
                    : new UnitUpgradeBonusData(0, 0, 0);
                double unitHealth = unitType.HP() + upgradeBonus.hpBonus;
                int unitsKilled = Math.Min(count, (int)(remainingDamage / unitHealth));

                if (unitsKilled > 0)
                {
                    army.RemoveMilitaryUnits(unitType, unitsKilled);
                    casualties[unitType] = unitsKilled;
                    remainingDamage -= unitsKilled * unitHealth;
                }
            }

            return casualties;
        }
    }
}
