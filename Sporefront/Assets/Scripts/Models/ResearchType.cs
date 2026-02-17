using System;
using System.Collections.Generic;

namespace Sporefront.Models
{
    public enum ResearchCategory
    {
        Economic,
        Military
    }

    public enum ResearchBonusType
    {
        WoodGatheringRate,
        FoodGatheringRate,
        StoneGatheringRate,
        OreGatheringRate,
        BuildingSpeed,
        TrainingSpeed,
        UnitAttack,
        UnitDefense,
        PopulationCapacity,
        VillagerCarryCapacity,
        MarketRate,
        VillagerMarchSpeed,
        TradeSpeed,
        RoadSpeed,
        FoodConsumption,
        FarmGatheringRate,
        MiningCampGatheringRate,
        LumberCampGatheringRate,
        MilitaryMarchSpeed,
        MilitaryRetreatSpeed,
        InfantryMeleeAttack,
        CavalryMeleeAttack,
        InfantryMeleeArmor,
        CavalryMeleeArmor,
        ArcherMeleeArmor,
        PiercingDamage,
        InfantryPierceArmor,
        CavalryPierceArmor,
        ArcherPierceArmor,
        SiegeBludgeonDamage,
        BuildingBludgeonArmor,
        MilitaryTrainingSpeed,
        MilitaryFoodConsumption,
        BuildingHP
    }

    public static class ResearchBonusTypeExtensions
    {
        public static bool IsFlatBonus(this ResearchBonusType type)
        {
            switch (type)
            {
                case ResearchBonusType.PopulationCapacity:
                case ResearchBonusType.InfantryMeleeAttack:
                case ResearchBonusType.CavalryMeleeAttack:
                case ResearchBonusType.PiercingDamage:
                case ResearchBonusType.SiegeBludgeonDamage:
                case ResearchBonusType.InfantryMeleeArmor:
                case ResearchBonusType.CavalryMeleeArmor:
                case ResearchBonusType.ArcherMeleeArmor:
                case ResearchBonusType.InfantryPierceArmor:
                case ResearchBonusType.CavalryPierceArmor:
                case ResearchBonusType.ArcherPierceArmor:
                case ResearchBonusType.BuildingBludgeonArmor:
                    return true;
                default:
                    return false;
            }
        }

        public static string DisplayName(this ResearchBonusType type)
        {
            switch (type)
            {
                case ResearchBonusType.WoodGatheringRate: return "Wood Gathering";
                case ResearchBonusType.FoodGatheringRate: return "Food Gathering";
                case ResearchBonusType.StoneGatheringRate: return "Stone Gathering";
                case ResearchBonusType.OreGatheringRate: return "Ore Gathering";
                case ResearchBonusType.BuildingSpeed: return "Building Speed";
                case ResearchBonusType.TrainingSpeed: return "Training Speed";
                case ResearchBonusType.UnitAttack: return "Unit Attack";
                case ResearchBonusType.UnitDefense: return "Unit Defense";
                case ResearchBonusType.PopulationCapacity: return "Population Capacity";
                case ResearchBonusType.VillagerCarryCapacity: return "Villager Efficiency";
                case ResearchBonusType.MarketRate: return "Market Rates";
                case ResearchBonusType.VillagerMarchSpeed: return "Villager Speed";
                case ResearchBonusType.TradeSpeed: return "Trade Speed";
                case ResearchBonusType.RoadSpeed: return "Road Speed";
                case ResearchBonusType.FoodConsumption: return "Food Consumption";
                case ResearchBonusType.FarmGatheringRate: return "Farm Gathering";
                case ResearchBonusType.MiningCampGatheringRate: return "Mining Camp Gathering";
                case ResearchBonusType.LumberCampGatheringRate: return "Lumber Camp Gathering";
                case ResearchBonusType.MilitaryMarchSpeed: return "March Speed";
                case ResearchBonusType.MilitaryRetreatSpeed: return "Retreat Speed";
                case ResearchBonusType.InfantryMeleeAttack: return "Infantry Melee Attack";
                case ResearchBonusType.CavalryMeleeAttack: return "Cavalry Melee Attack";
                case ResearchBonusType.InfantryMeleeArmor: return "Infantry Melee Armor";
                case ResearchBonusType.CavalryMeleeArmor: return "Cavalry Melee Armor";
                case ResearchBonusType.ArcherMeleeArmor: return "Archer Melee Armor";
                case ResearchBonusType.PiercingDamage: return "Piercing Damage";
                case ResearchBonusType.InfantryPierceArmor: return "Infantry Pierce Armor";
                case ResearchBonusType.CavalryPierceArmor: return "Cavalry Pierce Armor";
                case ResearchBonusType.ArcherPierceArmor: return "Archer Pierce Armor";
                case ResearchBonusType.SiegeBludgeonDamage: return "Siege Bludgeon Damage";
                case ResearchBonusType.BuildingBludgeonArmor: return "Building Bludgeon Armor";
                case ResearchBonusType.MilitaryTrainingSpeed: return "Military Training Speed";
                case ResearchBonusType.MilitaryFoodConsumption: return "Military Food Consumption";
                case ResearchBonusType.BuildingHP: return "Building HP";
                default: return type.ToString();
            }
        }
    }

    [System.Serializable]
    public struct ResearchBonus
    {
        public ResearchBonusType Type;
        public double Value;

        public ResearchBonus(ResearchBonusType type, double value)
        {
            Type = type;
            Value = value;
        }

        public string DisplayString
        {
            get
            {
                if (Type.IsFlatBonus())
                {
                    int flatValue = (int)Value;
                    return flatValue >= 0 ? $"+{flatValue} {Type.DisplayName()}" : $"{flatValue} {Type.DisplayName()}";
                }
                int percentage = (int)(Value * 100);
                return percentage < 0 ? $"{percentage}% {Type.DisplayName()}" : $"+{percentage}% {Type.DisplayName()}";
            }
        }
    }

    public enum ResearchBranch
    {
        Gathering,
        Commerce,
        Infrastructure,
        Logistics,
        MeleeEquipment,
        RangedEquipment,
        SiegeFortification
    }

    public static class ResearchBranchExtensions
    {
        public static string DisplayName(this ResearchBranch branch)
        {
            switch (branch)
            {
                case ResearchBranch.Gathering: return "Gathering";
                case ResearchBranch.Commerce: return "Commerce";
                case ResearchBranch.Infrastructure: return "Infrastructure";
                case ResearchBranch.Logistics: return "Logistics";
                case ResearchBranch.MeleeEquipment: return "Melee Equipment";
                case ResearchBranch.RangedEquipment: return "Ranged Equipment";
                case ResearchBranch.SiegeFortification: return "Siege & Fortification";
                default: return branch.ToString();
            }
        }

        public static ResearchCategory Category(this ResearchBranch branch)
        {
            switch (branch)
            {
                case ResearchBranch.Gathering:
                case ResearchBranch.Commerce:
                case ResearchBranch.Infrastructure:
                    return ResearchCategory.Economic;
                default:
                    return ResearchCategory.Military;
            }
        }

        public static BuildingType? GateBuildingType(this ResearchBranch branch)
        {
            switch (branch)
            {
                case ResearchBranch.Commerce: return BuildingType.Library;
                case ResearchBranch.Infrastructure: return BuildingType.University;
                case ResearchBranch.MeleeEquipment:
                case ResearchBranch.RangedEquipment:
                case ResearchBranch.SiegeFortification:
                    return BuildingType.Blacksmith;
                default: return null;
            }
        }
    }

    public enum ResearchType
    {
        // Gathering
        FarmGatheringI, FarmGatheringII, FarmGatheringIII,
        MiningCampGatheringI, MiningCampGatheringII, MiningCampGatheringIII,
        LumberCampGatheringI, LumberCampGatheringII, LumberCampGatheringIII,
        // Commerce
        BetterMarketRatesI, BetterMarketRatesII, BetterMarketRatesIII,
        ImprovedRoadsI, ImprovedRoadsII, ImprovedRoadsIII,
        TradeSpeedI, TradeSpeedII, TradeSpeedIII,
        // Infrastructure
        VillagerSpeedI, VillagerSpeedII, VillagerSpeedIII,
        PopulationCapacityI, PopulationCapacityII, PopulationCapacityIII,
        EfficientRationsI, EfficientRationsII, EfficientRationsIII,
        BuildingSpeedI, BuildingSpeedII, BuildingSpeedIII,
        // Logistics
        MarchSpeedI, MarchSpeedII, MarchSpeedIII,
        RetreatSpeedI, RetreatSpeedII, RetreatSpeedIII,
        MilitaryTrainingSpeedI, MilitaryTrainingSpeedII, MilitaryTrainingSpeedIII,
        MilitaryRationsI, MilitaryRationsII, MilitaryRationsIII,
        // Melee Equipment
        InfantryMeleeAttackI, InfantryMeleeAttackII, InfantryMeleeAttackIII,
        CavalryMeleeAttackI, CavalryMeleeAttackII, CavalryMeleeAttackIII,
        InfantryMeleeArmorI, InfantryMeleeArmorII, InfantryMeleeArmorIII,
        CavalryMeleeArmorI, CavalryMeleeArmorII, CavalryMeleeArmorIII,
        // Ranged Equipment
        PiercingDamageI, PiercingDamageII, PiercingDamageIII,
        ArcherMeleeArmorI, ArcherMeleeArmorII, ArcherMeleeArmorIII,
        InfantryPierceArmorI, InfantryPierceArmorII, InfantryPierceArmorIII,
        CavalryPierceArmorI, CavalryPierceArmorII, CavalryPierceArmorIII,
        ArcherPierceArmorI, ArcherPierceArmorII, ArcherPierceArmorIII,
        // Siege & Fortification
        SiegeBludgeonDamageI, SiegeBludgeonDamageII, SiegeBludgeonDamageIII,
        BuildingBludgeonArmorI, BuildingBludgeonArmorII, BuildingBludgeonArmorIII,
        FortifiedBuildingsI, FortifiedBuildingsII, FortifiedBuildingsIII
    }

    // This is split into a separate partial-like file due to massive size.
    // All extension methods are in ResearchTypeData.cs
    // For now, keeping core lookup methods here.

    [System.Serializable]
    public class ActiveResearch
    {
        public ResearchType ResearchType;
        public double StartTime;

        public ActiveResearch(ResearchType researchType, double startTime)
        {
            ResearchType = researchType;
            StartTime = startTime;
        }

        public double GetProgress(double currentTime, double speedMultiplier = 1.0)
        {
            double elapsed = currentTime - StartTime;
            double effectiveTime = ResearchType.ResearchTime() / Math.Max(speedMultiplier, 0.1);
            return Math.Min(1.0, Math.Max(0.0, elapsed / effectiveTime));
        }

        public double GetRemainingTime(double currentTime, double speedMultiplier = 1.0)
        {
            double elapsed = currentTime - StartTime;
            double effectiveTime = ResearchType.ResearchTime() / Math.Max(speedMultiplier, 0.1);
            return Math.Max(0, effectiveTime - elapsed);
        }

        public bool IsComplete(double currentTime, double speedMultiplier = 1.0)
        {
            return GetProgress(currentTime, speedMultiplier) >= 1.0;
        }
    }
}
