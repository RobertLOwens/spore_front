using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public struct UnitUpgradeBonusData
    {
        public double attackBonus;
        public double armorBonus;
        public double hpBonus;

        public UnitUpgradeBonusData(double attackBonus, double armorBonus, double hpBonus)
        {
            this.attackBonus = attackBonus;
            this.armorBonus = armorBonus;
            this.hpBonus = hpBonus;
        }
    }

    public enum UnitUpgradeType
    {
        SwordsmanTier1, SwordsmanTier2, SwordsmanTier3,
        ArcherTier1, ArcherTier2, ArcherTier3,
        CrossbowTier1, CrossbowTier2, CrossbowTier3,
        PikemanTier1, PikemanTier2, PikemanTier3,
        ScoutTier1, ScoutTier2, ScoutTier3,
        KnightTier1, KnightTier2, KnightTier3,
        HeavyCavalryTier1, HeavyCavalryTier2, HeavyCavalryTier3,
        MangonelTier1, MangonelTier2, MangonelTier3,
        TrebuchetTier1, TrebuchetTier2, TrebuchetTier3
    }

    public static class UnitUpgradeTypeExtensions
    {
        private static readonly UnitUpgradeType[] AllValues = (UnitUpgradeType[])Enum.GetValues(typeof(UnitUpgradeType));

        public static MilitaryUnitType GetUnitType(this UnitUpgradeType type)
        {
            switch (type)
            {
                case UnitUpgradeType.SwordsmanTier1:
                case UnitUpgradeType.SwordsmanTier2:
                case UnitUpgradeType.SwordsmanTier3:
                    return MilitaryUnitType.Swordsman;
                case UnitUpgradeType.ArcherTier1:
                case UnitUpgradeType.ArcherTier2:
                case UnitUpgradeType.ArcherTier3:
                    return MilitaryUnitType.Archer;
                case UnitUpgradeType.CrossbowTier1:
                case UnitUpgradeType.CrossbowTier2:
                case UnitUpgradeType.CrossbowTier3:
                    return MilitaryUnitType.Crossbow;
                case UnitUpgradeType.PikemanTier1:
                case UnitUpgradeType.PikemanTier2:
                case UnitUpgradeType.PikemanTier3:
                    return MilitaryUnitType.Pikeman;
                case UnitUpgradeType.ScoutTier1:
                case UnitUpgradeType.ScoutTier2:
                case UnitUpgradeType.ScoutTier3:
                    return MilitaryUnitType.Scout;
                case UnitUpgradeType.KnightTier1:
                case UnitUpgradeType.KnightTier2:
                case UnitUpgradeType.KnightTier3:
                    return MilitaryUnitType.Knight;
                case UnitUpgradeType.HeavyCavalryTier1:
                case UnitUpgradeType.HeavyCavalryTier2:
                case UnitUpgradeType.HeavyCavalryTier3:
                    return MilitaryUnitType.HeavyCavalry;
                case UnitUpgradeType.MangonelTier1:
                case UnitUpgradeType.MangonelTier2:
                case UnitUpgradeType.MangonelTier3:
                    return MilitaryUnitType.Mangonel;
                case UnitUpgradeType.TrebuchetTier1:
                case UnitUpgradeType.TrebuchetTier2:
                case UnitUpgradeType.TrebuchetTier3:
                    return MilitaryUnitType.Trebuchet;
                default:
                    return MilitaryUnitType.Swordsman;
            }
        }

        public static int Tier(this UnitUpgradeType type)
        {
            int index = (int)type;
            return (index % 3) + 1;
        }

        public static int RequiredBuildingLevel(this UnitUpgradeType type)
        {
            switch (type.Tier())
            {
                case 1: return GameConfig.UnitUpgrade.Tier1BuildingLevel;
                case 2: return GameConfig.UnitUpgrade.Tier2BuildingLevel;
                case 3: return GameConfig.UnitUpgrade.Tier3BuildingLevel;
                default: return 1;
            }
        }

        public static BuildingType RequiredBuildingType(this UnitUpgradeType type)
        {
            return type.GetUnitType().TrainingBuilding();
        }

        public static string DisplayName(this UnitUpgradeType type)
        {
            return $"{type.GetUnitType().DisplayName()} Upgrade {type.Tier()}";
        }

        public static double UpgradeTime(this UnitUpgradeType type)
        {
            switch (type.Tier())
            {
                case 1: return GameConfig.UnitUpgrade.Tier1Time;
                case 2: return GameConfig.UnitUpgrade.Tier2Time;
                case 3: return GameConfig.UnitUpgrade.Tier3Time;
                default: return 20.0;
            }
        }

        public static Dictionary<ResourceType, int> Cost(this UnitUpgradeType type)
        {
            var trainingCost = type.GetUnitType().TrainingCost();
            double multiplier;
            switch (type.Tier())
            {
                case 1: multiplier = GameConfig.UnitUpgrade.Tier1CostMultiplier; break;
                case 2: multiplier = GameConfig.UnitUpgrade.Tier2CostMultiplier; break;
                case 3: multiplier = GameConfig.UnitUpgrade.Tier3CostMultiplier; break;
                default: multiplier = 2.0; break;
            }
            var result = new Dictionary<ResourceType, int>();
            foreach (var kvp in trainingCost)
            {
                result[kvp.Key] = (int)(kvp.Value * multiplier);
            }
            return result;
        }

        public static UnitUpgradeBonusData Bonuses(this UnitUpgradeType type)
        {
            switch (type.Tier())
            {
                case 1: return new UnitUpgradeBonusData(
                    GameConfig.UnitUpgrade.Tier1AttackBonus,
                    GameConfig.UnitUpgrade.Tier1ArmorBonus,
                    GameConfig.UnitUpgrade.Tier1HPBonus);
                case 2: return new UnitUpgradeBonusData(
                    GameConfig.UnitUpgrade.Tier2AttackBonus,
                    GameConfig.UnitUpgrade.Tier2ArmorBonus,
                    GameConfig.UnitUpgrade.Tier2HPBonus);
                case 3: return new UnitUpgradeBonusData(
                    GameConfig.UnitUpgrade.Tier3AttackBonus,
                    GameConfig.UnitUpgrade.Tier3ArmorBonus,
                    GameConfig.UnitUpgrade.Tier3HPBonus);
                default: return new UnitUpgradeBonusData(0, 0, 0);
            }
        }

        public static UnitUpgradeType? Prerequisite(this UnitUpgradeType type)
        {
            int tier = type.Tier();
            if (tier <= 1) return null;
            // Previous tier is the enum value - 1
            return (UnitUpgradeType)((int)type - 1);
        }

        public static List<UnitUpgradeType> UpgradesForBuilding(BuildingType buildingType)
        {
            var result = new List<UnitUpgradeType>();
            foreach (var type in AllValues)
            {
                if (type.RequiredBuildingType() == buildingType)
                    result.Add(type);
            }
            return result;
        }

        public static List<UnitUpgradeType> UpgradesForUnit(MilitaryUnitType unitType)
        {
            var result = new List<UnitUpgradeType>();
            foreach (var type in AllValues)
            {
                if (type.GetUnitType() == unitType)
                    result.Add(type);
            }
            return result;
        }

        public static int CurrentTier(MilitaryUnitType unitType, HashSet<string> completedUpgrades)
        {
            var upgrades = UpgradesForUnit(unitType);
            upgrades.Sort((a, b) => a.Tier().CompareTo(b.Tier()));
            int highestTier = 0;
            foreach (var upgrade in upgrades)
            {
                if (completedUpgrades.Contains(upgrade.ToString()))
                    highestTier = upgrade.Tier();
                else
                    break;
            }
            return highestTier;
        }

        public static UnitUpgradeBonusData CumulativeBonuses(MilitaryUnitType unitType, HashSet<string> completedUpgrades)
        {
            double totalAttack = 0, totalArmor = 0, totalHP = 0;
            foreach (var upgrade in UpgradesForUnit(unitType))
            {
                if (completedUpgrades.Contains(upgrade.ToString()))
                {
                    var b = upgrade.Bonuses();
                    totalAttack += b.attackBonus;
                    totalArmor += b.armorBonus;
                    totalHP += b.hpBonus;
                }
            }
            return new UnitUpgradeBonusData(totalAttack, totalArmor, totalHP);
        }
    }
}
