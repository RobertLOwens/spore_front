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
        public override string DisplayName => "Villager";
        public override string Icon => "villager";
        public override Dictionary<ResourceType, int> TrainingCost => new Dictionary<ResourceType, int> { { ResourceType.Food, 50 } };
        public override double TrainingTime => 15.0;
        public override string Description => "Gathers resources and constructs buildings";
        public override int PopSpace => 1;
    }

    public static class MilitaryUnitTypeExtensions
    {
        public static string DisplayName(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return "Swordsman";
                case MilitaryUnitType.Archer: return "Archer";
                case MilitaryUnitType.Crossbow: return "Crossbow";
                case MilitaryUnitType.Pikeman: return "Pikeman";
                case MilitaryUnitType.Scout: return "Scout";
                case MilitaryUnitType.Knight: return "Knight";
                case MilitaryUnitType.HeavyCavalry: return "Heavy Cavalry";
                case MilitaryUnitType.Mangonel: return "Mangonel";
                case MilitaryUnitType.Trebuchet: return "Trebuchet";
                default: return type.ToString();
            }
        }

        public static string Icon(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return "swordsman";
                case MilitaryUnitType.Pikeman: return "pikeman";
                case MilitaryUnitType.Archer: return "archer";
                case MilitaryUnitType.Crossbow: return "crossbow";
                case MilitaryUnitType.Scout: return "scout";
                case MilitaryUnitType.Knight: return "knight";
                case MilitaryUnitType.HeavyCavalry: return "heavy_cavalry";
                case MilitaryUnitType.Mangonel: return "mangonel";
                case MilitaryUnitType.Trebuchet: return "trebuchet";
                default: return "";
            }
        }

        public static double MoveSpeed(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return 1.40;
                case MilitaryUnitType.Pikeman: return 1.60;
                case MilitaryUnitType.Archer: return 1.40;
                case MilitaryUnitType.Crossbow: return 1.52;
                case MilitaryUnitType.Scout: return 0.88;
                case MilitaryUnitType.Knight: return 1.00;
                case MilitaryUnitType.HeavyCavalry: return 1.12;
                case MilitaryUnitType.Mangonel: return 2.00;
                case MilitaryUnitType.Trebuchet: return 2.40;
                default: return 1.40;
            }
        }

        public static double AttackSpeed(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return 1.0;
                case MilitaryUnitType.Pikeman: return 1.2;
                case MilitaryUnitType.Archer: return 1.0;
                case MilitaryUnitType.Crossbow: return 1.5;
                case MilitaryUnitType.Scout: return 0.7;
                case MilitaryUnitType.Knight: return 1.1;
                case MilitaryUnitType.HeavyCavalry: return 1.2;
                case MilitaryUnitType.Mangonel: return 2.5;
                case MilitaryUnitType.Trebuchet: return 4.0;
                default: return 1.0;
            }
        }

        public static double HP(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return 50;
                case MilitaryUnitType.Archer: return 30;
                case MilitaryUnitType.Crossbow: return 40;
                case MilitaryUnitType.Pikeman: return 35;
                case MilitaryUnitType.Scout: return 30;
                case MilitaryUnitType.Knight: return 60;
                case MilitaryUnitType.HeavyCavalry: return 80;
                case MilitaryUnitType.Mangonel: return 70;
                case MilitaryUnitType.Trebuchet: return 120;
                default: return 50;
            }
        }

        public static double TrainingTime(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return 15;
                case MilitaryUnitType.Archer: return 12;
                case MilitaryUnitType.Crossbow: return 18;
                case MilitaryUnitType.Pikeman: return 14;
                case MilitaryUnitType.Scout: return 18;
                case MilitaryUnitType.Knight: return 25;
                case MilitaryUnitType.HeavyCavalry: return 35;
                case MilitaryUnitType.Mangonel: return 45;
                case MilitaryUnitType.Trebuchet: return 60;
                default: return 15;
            }
        }

        public static Dictionary<ResourceType, int> TrainingCost(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return new Dictionary<ResourceType, int> { { ResourceType.Food, 50 }, { ResourceType.Ore, 25 } };
                case MilitaryUnitType.Archer: return new Dictionary<ResourceType, int> { { ResourceType.Food, 40 }, { ResourceType.Wood, 30 } };
                case MilitaryUnitType.Crossbow: return new Dictionary<ResourceType, int> { { ResourceType.Food, 50 }, { ResourceType.Wood, 40 }, { ResourceType.Ore, 20 } };
                case MilitaryUnitType.Pikeman: return new Dictionary<ResourceType, int> { { ResourceType.Food, 45 }, { ResourceType.Wood, 20 }, { ResourceType.Ore, 15 } };
                case MilitaryUnitType.Scout: return new Dictionary<ResourceType, int> { { ResourceType.Food, 60 }, { ResourceType.Ore, 20 } };
                case MilitaryUnitType.Knight: return new Dictionary<ResourceType, int> { { ResourceType.Food, 80 }, { ResourceType.Ore, 60 } };
                case MilitaryUnitType.HeavyCavalry: return new Dictionary<ResourceType, int> { { ResourceType.Food, 100 }, { ResourceType.Ore, 80 } };
                case MilitaryUnitType.Mangonel: return new Dictionary<ResourceType, int> { { ResourceType.Food, 60 }, { ResourceType.Wood, 100 }, { ResourceType.Ore, 40 } };
                case MilitaryUnitType.Trebuchet: return new Dictionary<ResourceType, int> { { ResourceType.Food, 80 }, { ResourceType.Wood, 150 }, { ResourceType.Ore, 60 } };
                default: return new Dictionary<ResourceType, int>();
            }
        }

        public static BuildingType TrainingBuilding(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman:
                case MilitaryUnitType.Pikeman:
                    return BuildingType.Barracks;
                case MilitaryUnitType.Archer:
                case MilitaryUnitType.Crossbow:
                    return BuildingType.ArcheryRange;
                case MilitaryUnitType.Scout:
                case MilitaryUnitType.Knight:
                case MilitaryUnitType.HeavyCavalry:
                    return BuildingType.Stable;
                case MilitaryUnitType.Mangonel:
                case MilitaryUnitType.Trebuchet:
                    return BuildingType.SiegeWorkshop;
                default:
                    return BuildingType.Barracks;
            }
        }

        public static UnitCategory Category(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman:
                case MilitaryUnitType.Pikeman:
                    return UnitCategory.Infantry;
                case MilitaryUnitType.Archer:
                case MilitaryUnitType.Crossbow:
                    return UnitCategory.Ranged;
                case MilitaryUnitType.Scout:
                case MilitaryUnitType.Knight:
                case MilitaryUnitType.HeavyCavalry:
                    return UnitCategory.Cavalry;
                case MilitaryUnitType.Mangonel:
                case MilitaryUnitType.Trebuchet:
                    return UnitCategory.Siege;
                default:
                    return UnitCategory.Infantry;
            }
        }

        public static int PopSpace(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Mangonel: return 3;
                case MilitaryUnitType.Trebuchet: return 5;
                default: return 1;
            }
        }

        public static string Description(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman: return "Balanced melee infantry unit with good armor";
                case MilitaryUnitType.Pikeman: return "Anti-cavalry infantry with bonus damage vs mounted units";
                case MilitaryUnitType.Archer: return "Ranged unit with pierce damage";
                case MilitaryUnitType.Crossbow: return "Heavy ranged unit with high pierce damage and armor";
                case MilitaryUnitType.Scout: return "Fast light cavalry for reconnaissance";
                case MilitaryUnitType.Knight: return "Powerful mounted unit with high melee damage";
                case MilitaryUnitType.HeavyCavalry: return "Very heavy mounted unit with devastating charge";
                case MilitaryUnitType.Mangonel: return "Siege weapon with bludgeon damage, effective vs buildings";
                case MilitaryUnitType.Trebuchet: return "Long-range siege weapon, devastating vs buildings";
                default: return "";
            }
        }

        public static UnitCombatStats CombatStats(this MilitaryUnitType type)
        {
            switch (type)
            {
                case MilitaryUnitType.Swordsman:
                    return new UnitCombatStats(meleeDamage: 2, meleeArmor: 2, pierceArmor: 1);
                case MilitaryUnitType.Archer:
                    return new UnitCombatStats(pierceDamage: 2, pierceArmor: 1);
                case MilitaryUnitType.Crossbow:
                    return new UnitCombatStats(pierceDamage: 2, meleeArmor: 1, pierceArmor: 2);
                case MilitaryUnitType.Pikeman:
                    return new UnitCombatStats(meleeDamage: 1, meleeArmor: 1, pierceArmor: 1, bludgeonArmor: 3, bonusVsCavalry: 8);
                case MilitaryUnitType.Scout:
                    return new UnitCombatStats(meleeDamage: 2, meleeArmor: 1, bonusVsRanged: 1);
                case MilitaryUnitType.Knight:
                    return new UnitCombatStats(meleeDamage: 4, meleeArmor: 2, pierceArmor: 2, bludgeonArmor: 1, bonusVsRanged: 1);
                case MilitaryUnitType.HeavyCavalry:
                    return new UnitCombatStats(meleeDamage: 5, meleeArmor: 3, pierceArmor: 3, bludgeonArmor: 1, bonusVsRanged: 1);
                case MilitaryUnitType.Mangonel:
                    return new UnitCombatStats(bludgeonDamage: 8, meleeArmor: 2, pierceArmor: 10, bludgeonArmor: 3, bonusVsBuildings: 20);
                case MilitaryUnitType.Trebuchet:
                    return new UnitCombatStats(bludgeonDamage: 12, meleeArmor: 2, pierceArmor: 15, bludgeonArmor: 4, bonusVsBuildings: 30);
                default:
                    return new UnitCombatStats();
            }
        }
    }
}
