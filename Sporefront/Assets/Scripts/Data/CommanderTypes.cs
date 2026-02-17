using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public struct CommanderStatProfile
    {
        public int baseLeadership;
        public int leadershipPerLevel;
        public int leadershipPerRank;
        public int baseTactics;
        public int tacticsPerLevel;
        public int tacticsPerRank;
        public int baseLogistics;
        public int logisticsPerLevel;
        public int logisticsPerRank;
        public int baseRationing;
        public int rationingPerLevel;
        public int rationingPerRank;
        public int baseEndurance;
        public int endurancePerLevel;
        public int endurancePerRank;

        public CommanderStatProfile(
            int baseLeadership, int leadershipPerLevel, int leadershipPerRank,
            int baseTactics, int tacticsPerLevel, int tacticsPerRank,
            int baseLogistics, int logisticsPerLevel, int logisticsPerRank,
            int baseRationing, int rationingPerLevel, int rationingPerRank,
            int baseEndurance, int endurancePerLevel, int endurancePerRank)
        {
            this.baseLeadership = baseLeadership;
            this.leadershipPerLevel = leadershipPerLevel;
            this.leadershipPerRank = leadershipPerRank;
            this.baseTactics = baseTactics;
            this.tacticsPerLevel = tacticsPerLevel;
            this.tacticsPerRank = tacticsPerRank;
            this.baseLogistics = baseLogistics;
            this.logisticsPerLevel = logisticsPerLevel;
            this.logisticsPerRank = logisticsPerRank;
            this.baseRationing = baseRationing;
            this.rationingPerLevel = rationingPerLevel;
            this.rationingPerRank = rationingPerRank;
            this.baseEndurance = baseEndurance;
            this.endurancePerLevel = endurancePerLevel;
            this.endurancePerRank = endurancePerRank;
        }
    }

    public enum CommanderSpecialty
    {
        InfantryAggressive,
        InfantryDefensive,
        CavalryAggressive,
        CavalryDefensive,
        RangedAggressive,
        RangedDefensive,
        SiegeAggressive,
        SiegeDefensive,
        Defensive,
        Logistics
    }

    public static class CommanderSpecialtyExtensions
    {
        public static string DisplayName(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive: return "Infantry (Aggressive)";
                case CommanderSpecialty.InfantryDefensive: return "Infantry (Defensive)";
                case CommanderSpecialty.CavalryAggressive: return "Cavalry (Aggressive)";
                case CommanderSpecialty.CavalryDefensive: return "Cavalry (Defensive)";
                case CommanderSpecialty.RangedAggressive: return "Ranged (Aggressive)";
                case CommanderSpecialty.RangedDefensive: return "Ranged (Defensive)";
                case CommanderSpecialty.SiegeAggressive: return "Siege (Aggressive)";
                case CommanderSpecialty.SiegeDefensive: return "Siege (Defensive)";
                case CommanderSpecialty.Defensive: return "Defensive";
                case CommanderSpecialty.Logistics: return "Logistics";
                default: return spec.ToString();
            }
        }

        public static string Icon(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive: return "sword";
                case CommanderSpecialty.InfantryDefensive: return "shield";
                case CommanderSpecialty.CavalryAggressive: return "horse";
                case CommanderSpecialty.CavalryDefensive: return "horse";
                case CommanderSpecialty.RangedAggressive: return "bow";
                case CommanderSpecialty.RangedDefensive: return "bow";
                case CommanderSpecialty.SiegeAggressive: return "siege";
                case CommanderSpecialty.SiegeDefensive: return "siege";
                case CommanderSpecialty.Defensive: return "shield";
                case CommanderSpecialty.Logistics: return "logistics";
                default: return "";
            }
        }

        public static string Description(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive: return "+1 infantry attack, boosted endurance";
                case CommanderSpecialty.InfantryDefensive: return "+1 armor, boosted leadership";
                case CommanderSpecialty.CavalryAggressive: return "+1 cavalry attack, boosted endurance";
                case CommanderSpecialty.CavalryDefensive: return "+1 armor, boosted logistics";
                case CommanderSpecialty.RangedAggressive: return "+1 ranged attack, boosted endurance";
                case CommanderSpecialty.RangedDefensive: return "+1 armor, boosted tactics";
                case CommanderSpecialty.SiegeAggressive: return "+1 siege attack, boosted endurance";
                case CommanderSpecialty.SiegeDefensive: return "+1 armor, boosted rationing";
                case CommanderSpecialty.Defensive: return "Strong tactics and rationing, better leadership";
                case CommanderSpecialty.Logistics: return "Strong leadership and logistics";
                default: return "";
            }
        }

        public static UnitCategory? GetUnitCategory(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive:
                case CommanderSpecialty.InfantryDefensive:
                    return UnitCategory.Infantry;
                case CommanderSpecialty.CavalryAggressive:
                case CommanderSpecialty.CavalryDefensive:
                    return UnitCategory.Cavalry;
                case CommanderSpecialty.RangedAggressive:
                case CommanderSpecialty.RangedDefensive:
                    return UnitCategory.Ranged;
                case CommanderSpecialty.SiegeAggressive:
                case CommanderSpecialty.SiegeDefensive:
                    return UnitCategory.Siege;
                default:
                    return null;
            }
        }

        public static bool IsAggressive(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive:
                case CommanderSpecialty.CavalryAggressive:
                case CommanderSpecialty.RangedAggressive:
                case CommanderSpecialty.SiegeAggressive:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefensiveVariant(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryDefensive:
                case CommanderSpecialty.CavalryDefensive:
                case CommanderSpecialty.RangedDefensive:
                case CommanderSpecialty.SiegeDefensive:
                    return true;
                default:
                    return false;
            }
        }

        public static int AttackBonus(this CommanderSpecialty spec, UnitCategory category)
        {
            if (!spec.IsAggressive()) return 0;
            var specCategory = spec.GetUnitCategory();
            if (!specCategory.HasValue || specCategory.Value != category) return 0;
            return 1;
        }

        public static int ArmorBonus(this CommanderSpecialty spec)
        {
            return (spec.IsDefensiveVariant() || spec == CommanderSpecialty.Defensive) ? 1 : 0;
        }

        public static CommanderStatProfile StatProfile(this CommanderSpecialty spec)
        {
            switch (spec)
            {
                case CommanderSpecialty.InfantryAggressive:
                    return new CommanderStatProfile(10, 2, 4, 8, 1, 3, 8, 1, 3, 8, 1, 3, 10, 2, 4);
                case CommanderSpecialty.InfantryDefensive:
                    return new CommanderStatProfile(10, 2, 4, 8, 1, 3, 8, 1, 3, 8, 1, 3, 8, 1, 3);
                case CommanderSpecialty.CavalryAggressive:
                    return new CommanderStatProfile(8, 1, 3, 8, 1, 3, 10, 2, 4, 8, 1, 3, 10, 2, 4);
                case CommanderSpecialty.CavalryDefensive:
                    return new CommanderStatProfile(8, 1, 3, 8, 1, 3, 10, 2, 4, 8, 1, 3, 8, 1, 3);
                case CommanderSpecialty.RangedAggressive:
                    return new CommanderStatProfile(8, 1, 3, 10, 2, 4, 8, 1, 3, 8, 1, 3, 10, 2, 4);
                case CommanderSpecialty.RangedDefensive:
                    return new CommanderStatProfile(8, 1, 3, 10, 2, 4, 8, 1, 3, 8, 1, 3, 8, 1, 3);
                case CommanderSpecialty.SiegeAggressive:
                    return new CommanderStatProfile(8, 1, 3, 8, 1, 3, 8, 1, 3, 10, 2, 4, 10, 2, 4);
                case CommanderSpecialty.SiegeDefensive:
                    return new CommanderStatProfile(8, 1, 3, 8, 1, 3, 8, 1, 3, 10, 2, 4, 8, 1, 3);
                case CommanderSpecialty.Defensive:
                    return new CommanderStatProfile(10, 2, 4, 12, 3, 5, 6, 1, 2, 10, 2, 4, 8, 1, 3);
                case CommanderSpecialty.Logistics:
                    return new CommanderStatProfile(10, 2, 4, 6, 1, 2, 12, 3, 5, 8, 1, 3, 8, 1, 3);
                default:
                    return new CommanderStatProfile(8, 1, 3, 8, 1, 3, 8, 1, 3, 8, 1, 3, 8, 1, 3);
            }
        }
    }

    public enum CommanderRank
    {
        Recruit,
        Sergeant,
        Captain,
        Major,
        Colonel,
        General
    }

    public static class CommanderRankExtensions
    {
        public static string DisplayName(this CommanderRank rank)
        {
            return rank.ToString();
        }

        public static string Icon(this CommanderRank rank)
        {
            switch (rank)
            {
                case CommanderRank.Recruit: return "star1";
                case CommanderRank.Sergeant: return "star2";
                case CommanderRank.Captain: return "star3";
                case CommanderRank.Major: return "medal1";
                case CommanderRank.Colonel: return "medal2";
                case CommanderRank.General: return "crown";
                default: return "";
            }
        }

        public static int Index(this CommanderRank rank)
        {
            switch (rank)
            {
                case CommanderRank.Recruit: return 0;
                case CommanderRank.Sergeant: return 1;
                case CommanderRank.Captain: return 2;
                case CommanderRank.Major: return 3;
                case CommanderRank.Colonel: return 4;
                case CommanderRank.General: return 5;
                default: return 0;
            }
        }

        public static CommanderRank RankForLevel(int level)
        {
            if (level >= 25) return CommanderRank.General;
            if (level >= 20) return CommanderRank.Colonel;
            if (level >= 15) return CommanderRank.Major;
            if (level >= 10) return CommanderRank.Captain;
            if (level >= 5) return CommanderRank.Sergeant;
            return CommanderRank.Recruit;
        }
    }
}
