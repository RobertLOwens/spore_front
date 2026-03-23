using System.Collections.Generic;

namespace Sporefront.Models
{
    public enum FactionType
    {
        None,
        Morel,
        Muscaria
    }

    public static class FactionTypeExtensions
    {
        public static string DisplayName(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel: return "The Morels";
                case FactionType.Muscaria: return "Amanita Muscaria";
                default: return "No Faction";
            }
        }

        public static string Description(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel:
                    return "An infantry and woodland stealth faction. The Morels thrive in forested terrain, " +
                           "using superior vision and camouflage to control the map and strike from ambush.";
                case FactionType.Muscaria:
                    return "An aggressive poison and mountain faction. The Amanita Muscaria thrive in highland terrain, " +
                           "using toxic strikes and mountain mastery to dominate through attrition.";
                default: return "";
            }
        }

        public static string StartingBonusDescription(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel:
                    return "Extended Vision (+1 army sight), Woodland Camouflage, " +
                           "+5% wood gathering, Extended Lumberyards (2-tile reach)";
                case FactionType.Muscaria:
                    return "Toxic Strikes (poison DoT after combat), Mountain Builders (-15% mountain/hill build cost), " +
                           "Highland Movement (+20% speed on mountain/hill), +5% stone & ore gathering";
                default: return "";
            }
        }

        public static int ArmyVisionBonus(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel: return 1;
                default: return 0;
            }
        }

        public static double WoodGatheringBonus(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel: return 0.05;
                default: return 0.0;
            }
        }

        public static int LumberCampReach(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel: return 2;
                default: return 1;
            }
        }

        public static bool HasWoodlandCamouflage(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel: return true;
                default: return false;
            }
        }

        public static double StoneGatheringBonus(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Muscaria: return 0.05;
                default: return 0.0;
            }
        }

        public static double OreGatheringBonus(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Muscaria: return 0.05;
                default: return 0.0;
            }
        }

        public static bool HasToxicStrikes(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Muscaria: return true;
                default: return false;
            }
        }

        public static double HighlandSpeedBonus(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Muscaria: return 0.20;
                default: return 0.0;
            }
        }

        public static double MountainBuildCostReduction(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Muscaria: return 0.15;
                default: return 0.0;
            }
        }

        public static List<ResearchType> BlockedResearch(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel:
                    // Morels blocked from Tier III: Ranged, Cavalry, Stone/Ore
                    return new List<ResearchType>
                    {
                        // Ranged
                        ResearchType.PiercingDamageIII,
                        ResearchType.ArcherMeleeArmorIII,
                        ResearchType.ArcherPierceArmorIII,
                        // Cavalry
                        ResearchType.CavalryMeleeAttackIII,
                        ResearchType.CavalryMeleeArmorIII,
                        ResearchType.CavalryPierceArmorIII,
                        // Stone
                        ResearchType.MiningCampGatheringIII,
                        // Siege (ore-heavy)
                        ResearchType.SiegeBludgeonDamageIII
                    };
                case FactionType.Muscaria:
                    // Muscaria blocked from Tier III: Infantry, Cavalry, Wood, Food
                    return new List<ResearchType>
                    {
                        // Infantry
                        ResearchType.InfantryMeleeAttackIII,
                        ResearchType.InfantryMeleeArmorIII,
                        ResearchType.InfantryPierceArmorIII,
                        // Cavalry
                        ResearchType.CavalryMeleeAttackIII,
                        ResearchType.CavalryMeleeArmorIII,
                        ResearchType.CavalryPierceArmorIII,
                        // Wood
                        ResearchType.LumberCampGatheringIII,
                        // Food
                        ResearchType.FarmGatheringIII
                    };
                default:
                    return new List<ResearchType>();
            }
        }

        public static string ResearchRestrictionDescription(this FactionType faction)
        {
            switch (faction)
            {
                case FactionType.Morel:
                    return "Blocked from Tier III: Ranged, Cavalry, Stone, Siege research.";
                case FactionType.Muscaria:
                    return "Blocked from Tier III: Infantry, Cavalry, Wood, Food research.";
                default: return "No restrictions.";
            }
        }

        public static FactionType ExclusiveFaction(this BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.FalseMorel: return FactionType.Morel;
                default: return FactionType.None;
            }
        }

        public static bool IsFactionExclusive(this BuildingType buildingType)
        {
            return buildingType.ExclusiveFaction() != FactionType.None;
        }
    }
}
