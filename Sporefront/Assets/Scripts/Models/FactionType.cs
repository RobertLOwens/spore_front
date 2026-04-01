using System.Collections.Generic;

namespace Sporefront.Models
{
    public enum FactionType
    {
        None,
        Morel,
        Muscaria
    }

    /// <summary>
    /// Data-driven faction configuration. All faction properties are stored in a lookup table
    /// rather than individual switch statements, following the same pattern as BuildingType/MilitaryUnitType.
    /// </summary>
    public class FactionTypeData
    {
        public string DisplayName;
        public string Description;
        public string StartingBonusDescription;
        public string ResearchRestrictionDescription;
        public int ArmyVisionBonus;
        public double WoodGatheringBonus;
        public int LumberCampReach;
        public bool HasWoodlandCamouflage;
        public double StoneGatheringBonus;
        public double OreGatheringBonus;
        public bool HasToxicStrikes;
        public double HighlandSpeedBonus;
        public double MountainBuildCostReduction;
        public List<ResearchType> BlockedResearch;
    }

    public static class FactionTypeExtensions
    {
        private static readonly FactionTypeData NoneData = new FactionTypeData
        {
            DisplayName = "No Faction",
            Description = "",
            StartingBonusDescription = "",
            ResearchRestrictionDescription = "No restrictions.",
            ArmyVisionBonus = 0,
            WoodGatheringBonus = 0.0,
            LumberCampReach = 1,
            HasWoodlandCamouflage = false,
            StoneGatheringBonus = 0.0,
            OreGatheringBonus = 0.0,
            HasToxicStrikes = false,
            HighlandSpeedBonus = 0.0,
            MountainBuildCostReduction = 0.0,
            BlockedResearch = new List<ResearchType>()
        };

        private static readonly Dictionary<FactionType, FactionTypeData> Data = new Dictionary<FactionType, FactionTypeData>
        {
            {
                FactionType.Morel, new FactionTypeData
                {
                    DisplayName = "The Morels",
                    Description = "An infantry and woodland stealth faction. The Morels thrive in forested terrain, " +
                                  "using superior vision and camouflage to control the map and strike from ambush.",
                    StartingBonusDescription = "Extended Vision (+1 army sight), Woodland Camouflage, " +
                                               "+5% wood gathering, Extended Lumberyards (2-tile reach)",
                    ResearchRestrictionDescription = "Blocked from Tier III: Ranged, Cavalry, Stone, Siege research.",
                    ArmyVisionBonus = 1,
                    WoodGatheringBonus = 0.05,
                    LumberCampReach = 2,
                    HasWoodlandCamouflage = true,
                    StoneGatheringBonus = 0.0,
                    OreGatheringBonus = 0.0,
                    HasToxicStrikes = false,
                    HighlandSpeedBonus = 0.0,
                    MountainBuildCostReduction = 0.0,
                    BlockedResearch = new List<ResearchType>
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
                    }
                }
            },
            {
                FactionType.Muscaria, new FactionTypeData
                {
                    DisplayName = "Amanita Muscaria",
                    Description = "An aggressive poison and mountain faction. The Amanita Muscaria thrive in highland terrain, " +
                                  "using toxic strikes and mountain mastery to dominate through attrition.",
                    StartingBonusDescription = "Toxic Strikes (poison DoT after combat), Mountain Builders (-15% mountain/hill build cost), " +
                                               "Highland Movement (+20% speed on mountain/hill), +5% stone & ore gathering",
                    ResearchRestrictionDescription = "Blocked from Tier III: Infantry, Cavalry, Wood, Food research.",
                    ArmyVisionBonus = 0,
                    WoodGatheringBonus = 0.0,
                    LumberCampReach = 1,
                    HasWoodlandCamouflage = false,
                    StoneGatheringBonus = 0.05,
                    OreGatheringBonus = 0.05,
                    HasToxicStrikes = true,
                    HighlandSpeedBonus = 0.20,
                    MountainBuildCostReduction = 0.15,
                    BlockedResearch = new List<ResearchType>
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
                    }
                }
            }
        };

        private static FactionTypeData Get(FactionType faction)
        {
            return Data.TryGetValue(faction, out var data) ? data : NoneData;
        }

        public static string DisplayName(this FactionType faction) => Get(faction).DisplayName;
        public static string Description(this FactionType faction) => Get(faction).Description;
        public static string StartingBonusDescription(this FactionType faction) => Get(faction).StartingBonusDescription;
        public static string ResearchRestrictionDescription(this FactionType faction) => Get(faction).ResearchRestrictionDescription;
        public static int ArmyVisionBonus(this FactionType faction) => Get(faction).ArmyVisionBonus;
        public static double WoodGatheringBonus(this FactionType faction) => Get(faction).WoodGatheringBonus;
        public static int LumberCampReach(this FactionType faction) => Get(faction).LumberCampReach;
        public static bool HasWoodlandCamouflage(this FactionType faction) => Get(faction).HasWoodlandCamouflage;
        public static double StoneGatheringBonus(this FactionType faction) => Get(faction).StoneGatheringBonus;
        public static double OreGatheringBonus(this FactionType faction) => Get(faction).OreGatheringBonus;
        public static bool HasToxicStrikes(this FactionType faction) => Get(faction).HasToxicStrikes;
        public static double HighlandSpeedBonus(this FactionType faction) => Get(faction).HighlandSpeedBonus;
        public static double MountainBuildCostReduction(this FactionType faction) => Get(faction).MountainBuildCostReduction;
        public static List<ResearchType> BlockedResearch(this FactionType faction) => Get(faction).BlockedResearch;

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
