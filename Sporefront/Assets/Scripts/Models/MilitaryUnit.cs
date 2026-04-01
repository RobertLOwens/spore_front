using System.Collections.Generic;

namespace Sporefront.Models
{
    public enum UnitCategory
    {
        Infantry,
        Ranged,
        Cavalry,
        Siege
    }

    public enum DamageType
    {
        Melee,
        Pierce,
        Bludgeon
    }

    public enum MilitaryUnitType
    {
        Swordsman,
        Archer,
        Crossbow,
        Pikeman,
        Scout,
        Knight,
        HeavyCavalry,
        Mangonel,
        Trebuchet
    }

    [System.Serializable]
    public struct UnitCombatStats
    {
        public double meleeDamage;
        public double pierceDamage;
        public double bludgeonDamage;

        public double meleeArmor;
        public double pierceArmor;
        public double bludgeonArmor;

        public double bonusVsInfantry;
        public double bonusVsCavalry;
        public double bonusVsRanged;
        public double bonusVsSiege;
        public double bonusVsBuildings;

        public UnitCombatStats(
            double meleeDamage = 0, double pierceDamage = 0, double bludgeonDamage = 0,
            double meleeArmor = 0, double pierceArmor = 0, double bludgeonArmor = 0,
            double bonusVsInfantry = 0, double bonusVsCavalry = 0,
            double bonusVsRanged = 0, double bonusVsSiege = 0, double bonusVsBuildings = 0)
        {
            this.meleeDamage = meleeDamage;
            this.pierceDamage = pierceDamage;
            this.bludgeonDamage = bludgeonDamage;
            this.meleeArmor = meleeArmor;
            this.pierceArmor = pierceArmor;
            this.bludgeonArmor = bludgeonArmor;
            this.bonusVsInfantry = bonusVsInfantry;
            this.bonusVsCavalry = bonusVsCavalry;
            this.bonusVsRanged = bonusVsRanged;
            this.bonusVsSiege = bonusVsSiege;
            this.bonusVsBuildings = bonusVsBuildings;
        }

        public double TotalDamage => meleeDamage + pierceDamage + bludgeonDamage;
        public double AverageArmor => (meleeArmor + pierceArmor + bludgeonArmor) / 3.0;

        public double CalculateEffectiveDamage(UnitCombatStats targetArmor, UnitCategory? targetCategory)
        {
            double effectiveMelee = System.Math.Max(0, meleeDamage - targetArmor.meleeArmor);
            double effectivePierce = System.Math.Max(0, pierceDamage - targetArmor.pierceArmor);
            double effectiveBludgeon = System.Math.Max(0, bludgeonDamage - targetArmor.bludgeonArmor);

            double total = effectiveMelee + effectivePierce + effectiveBludgeon;

            if (targetCategory.HasValue)
            {
                switch (targetCategory.Value)
                {
                    case UnitCategory.Cavalry: total += bonusVsCavalry; break;
                    case UnitCategory.Infantry: total += bonusVsInfantry; break;
                    case UnitCategory.Ranged: total += bonusVsRanged; break;
                    case UnitCategory.Siege: total += bonusVsSiege; break;
                }
            }

            return total;
        }

        public static UnitCombatStats Aggregate(IList<UnitCombatStats> stats)
        {
            var result = new UnitCombatStats();
            if (stats.Count == 0) return result;

            foreach (var stat in stats)
            {
                result.meleeDamage += stat.meleeDamage;
                result.pierceDamage += stat.pierceDamage;
                result.bludgeonDamage += stat.bludgeonDamage;
                result.meleeArmor += stat.meleeArmor;
                result.pierceArmor += stat.pierceArmor;
                result.bludgeonArmor += stat.bludgeonArmor;
            }

            double count = stats.Count;
            double sumBonusInf = 0, sumBonusCav = 0, sumBonusRng = 0, sumBonusSie = 0, sumBonusBld = 0;
            foreach (var stat in stats)
            {
                sumBonusInf += stat.bonusVsInfantry;
                sumBonusCav += stat.bonusVsCavalry;
                sumBonusRng += stat.bonusVsRanged;
                sumBonusSie += stat.bonusVsSiege;
                sumBonusBld += stat.bonusVsBuildings;
            }
            result.bonusVsInfantry = sumBonusInf / count;
            result.bonusVsCavalry = sumBonusCav / count;
            result.bonusVsRanged = sumBonusRng / count;
            result.bonusVsSiege = sumBonusSie / count;
            result.bonusVsBuildings = sumBonusBld / count;

            return result;
        }
    }

    // TrainableUnitType: abstract class with subclasses (replaces Swift enum with associated values)
    [System.Serializable]
    public abstract class TrainableUnitType
    {
        public abstract string DisplayName { get; }
        public abstract string Icon { get; }
        public abstract Dictionary<ResourceType, int> TrainingCost { get; }
        public abstract double TrainingTime { get; }
        public abstract string Description { get; }
        public abstract int PopSpace { get; }
    }

    [System.Serializable]
    public class MilitaryTrainable : TrainableUnitType
    {
        public MilitaryUnitType UnitType;

        public MilitaryTrainable(MilitaryUnitType unitType)
        {
            UnitType = unitType;
        }

        public override string DisplayName => UnitType.DisplayName();
        public override string Icon => UnitType.Icon();
        public override Dictionary<ResourceType, int> TrainingCost => UnitType.TrainingCost();
        public override double TrainingTime => UnitType.TrainingTime();
        public override string Description => UnitType.Description();
        public override int PopSpace => UnitType.PopSpace();
    }

    [System.Serializable]
    public class VillagerTrainable : TrainableUnitType
    {
        private static readonly Dictionary<ResourceType, int> CachedCost =
            new Dictionary<ResourceType, int> { { ResourceType.Food, 50 } };

        public override string DisplayName => "Villager";
        public override string Icon => "villager";
        public override Dictionary<ResourceType, int> TrainingCost => CachedCost;
        public override double TrainingTime => 15.0;
        public override string Description => "Gathers resources and constructs buildings";
        public override int PopSpace => 1;
    }

    // ================================================================
    // Data record for MilitaryUnitType lookup table
    // ================================================================

    public class MilitaryUnitTypeData
    {
        public string DisplayName;
        public string Icon;
        public double MoveSpeed;
        public double AttackSpeed;
        public double HP;
        public double TrainingTime;
        public Dictionary<ResourceType, int> TrainingCost;
        public BuildingType TrainingBuilding;
        public UnitCategory Category;
        public int PopSpace;
        public string Description;
        public UnitCombatStats CombatStats;

        public MilitaryUnitTypeData(
            string displayName, string icon,
            double moveSpeed, double attackSpeed, double hp,
            double trainingTime, Dictionary<ResourceType, int> trainingCost,
            BuildingType trainingBuilding, UnitCategory category,
            string description, UnitCombatStats combatStats,
            int popSpace = 1)
        {
            DisplayName = displayName;
            Icon = icon;
            MoveSpeed = moveSpeed;
            AttackSpeed = attackSpeed;
            HP = hp;
            TrainingTime = trainingTime;
            TrainingCost = trainingCost;
            TrainingBuilding = trainingBuilding;
            Category = category;
            PopSpace = popSpace;
            Description = description;
            CombatStats = combatStats;
        }
    }

    // ================================================================
    // Extension methods — data-driven via static lookup table
    // ================================================================

    public static class MilitaryUnitTypeExtensions
    {
        private static readonly Dictionary<MilitaryUnitType, MilitaryUnitTypeData> Data =
            new Dictionary<MilitaryUnitType, MilitaryUnitTypeData>
        {
            { MilitaryUnitType.Swordsman, new MilitaryUnitTypeData(
                "Swordsman", "swordsman", 1.40, 1.0, 50, 15,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 50 }, { ResourceType.Ore, 25 } },
                BuildingType.Barracks, UnitCategory.Infantry,
                "Balanced melee infantry unit with good armor",
                new UnitCombatStats(meleeDamage: 2, meleeArmor: 2, pierceArmor: 1)) },

            { MilitaryUnitType.Archer, new MilitaryUnitTypeData(
                "Archer", "archer", 1.40, 1.0, 30, 12,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 40 }, { ResourceType.Wood, 30 } },
                BuildingType.ArcheryRange, UnitCategory.Ranged,
                "Ranged unit with pierce damage",
                new UnitCombatStats(pierceDamage: 2, pierceArmor: 1)) },

            { MilitaryUnitType.Crossbow, new MilitaryUnitTypeData(
                "Crossbow", "crossbow", 1.52, 1.5, 40, 18,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 50 }, { ResourceType.Wood, 40 }, { ResourceType.Ore, 20 } },
                BuildingType.ArcheryRange, UnitCategory.Ranged,
                "Heavy ranged unit with high pierce damage and armor",
                new UnitCombatStats(pierceDamage: 2, meleeArmor: 1, pierceArmor: 2)) },

            { MilitaryUnitType.Pikeman, new MilitaryUnitTypeData(
                "Pikeman", "pikeman", 1.60, 1.2, 35, 14,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 45 }, { ResourceType.Wood, 20 }, { ResourceType.Ore, 15 } },
                BuildingType.Barracks, UnitCategory.Infantry,
                "Anti-cavalry infantry with bonus damage vs mounted units",
                new UnitCombatStats(meleeDamage: 1, meleeArmor: 1, pierceArmor: 1, bludgeonArmor: 3, bonusVsCavalry: 8)) },

            { MilitaryUnitType.Scout, new MilitaryUnitTypeData(
                "Scout", "scout", 0.88, 0.7, 30, 18,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 60 }, { ResourceType.Ore, 20 } },
                BuildingType.Stable, UnitCategory.Cavalry,
                "Fast light cavalry for reconnaissance",
                new UnitCombatStats(meleeDamage: 2, meleeArmor: 1, bonusVsRanged: 1)) },

            { MilitaryUnitType.Knight, new MilitaryUnitTypeData(
                "Knight", "knight", 1.00, 1.1, 60, 25,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 80 }, { ResourceType.Ore, 60 } },
                BuildingType.Stable, UnitCategory.Cavalry,
                "Powerful mounted unit with high melee damage",
                new UnitCombatStats(meleeDamage: 4, meleeArmor: 2, pierceArmor: 2, bludgeonArmor: 1, bonusVsRanged: 1)) },

            { MilitaryUnitType.HeavyCavalry, new MilitaryUnitTypeData(
                "Heavy Cavalry", "heavy_cavalry", 1.12, 1.2, 80, 35,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 100 }, { ResourceType.Ore, 80 } },
                BuildingType.Stable, UnitCategory.Cavalry,
                "Very heavy mounted unit with devastating charge",
                new UnitCombatStats(meleeDamage: 5, meleeArmor: 3, pierceArmor: 3, bludgeonArmor: 1, bonusVsRanged: 1)) },

            { MilitaryUnitType.Mangonel, new MilitaryUnitTypeData(
                "Mangonel", "mangonel", 2.00, 2.5, 70, 45,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 60 }, { ResourceType.Wood, 100 }, { ResourceType.Ore, 40 } },
                BuildingType.SiegeWorkshop, UnitCategory.Siege,
                "Siege weapon with bludgeon damage, effective vs buildings",
                new UnitCombatStats(bludgeonDamage: 8, meleeArmor: 2, pierceArmor: 10, bludgeonArmor: 3, bonusVsBuildings: 20),
                popSpace: 3) },

            { MilitaryUnitType.Trebuchet, new MilitaryUnitTypeData(
                "Trebuchet", "trebuchet", 2.40, 4.0, 120, 60,
                new Dictionary<ResourceType, int> { { ResourceType.Food, 80 }, { ResourceType.Wood, 150 }, { ResourceType.Ore, 60 } },
                BuildingType.SiegeWorkshop, UnitCategory.Siege,
                "Long-range siege weapon, devastating vs buildings",
                new UnitCombatStats(bludgeonDamage: 12, meleeArmor: 2, pierceArmor: 15, bludgeonArmor: 4, bonusVsBuildings: 30),
                popSpace: 5) },
        };

        // ================================================================
        // One-liner data lookups
        // ================================================================

        public static string DisplayName(this MilitaryUnitType type) => Data[type].DisplayName;
        public static string Icon(this MilitaryUnitType type) => Data[type].Icon;
        public static double MoveSpeed(this MilitaryUnitType type) => Data[type].MoveSpeed;
        public static double AttackSpeed(this MilitaryUnitType type) => Data[type].AttackSpeed;
        public static double HP(this MilitaryUnitType type) => Data[type].HP;
        public static double TrainingTime(this MilitaryUnitType type) => Data[type].TrainingTime;
        public static Dictionary<ResourceType, int> TrainingCost(this MilitaryUnitType type) => Data[type].TrainingCost;
        public static BuildingType TrainingBuilding(this MilitaryUnitType type) => Data[type].TrainingBuilding;
        public static UnitCategory Category(this MilitaryUnitType type) => Data[type].Category;
        public static int PopSpace(this MilitaryUnitType type) => Data[type].PopSpace;
        public static string Description(this MilitaryUnitType type) => Data[type].Description;
        public static UnitCombatStats CombatStats(this MilitaryUnitType type) => Data[type].CombatStats;
    }
}
