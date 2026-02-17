using System.Collections.Generic;

namespace Sporefront.Models
{
    public static class ResearchTypeExtensions
    {
        public static string DisplayName(this ResearchType type)
        {
            switch (type)
            {
                case ResearchType.FarmGatheringI: return "Farm Efficiency I";
                case ResearchType.FarmGatheringII: return "Farm Efficiency II";
                case ResearchType.FarmGatheringIII: return "Farm Efficiency III";
                case ResearchType.MiningCampGatheringI: return "Mining Efficiency I";
                case ResearchType.MiningCampGatheringII: return "Mining Efficiency II";
                case ResearchType.MiningCampGatheringIII: return "Mining Efficiency III";
                case ResearchType.LumberCampGatheringI: return "Lumber Efficiency I";
                case ResearchType.LumberCampGatheringII: return "Lumber Efficiency II";
                case ResearchType.LumberCampGatheringIII: return "Lumber Efficiency III";
                case ResearchType.BetterMarketRatesI: return "Better Market Rates I";
                case ResearchType.BetterMarketRatesII: return "Better Market Rates II";
                case ResearchType.BetterMarketRatesIII: return "Better Market Rates III";
                case ResearchType.VillagerSpeedI: return "Swift Villagers I";
                case ResearchType.VillagerSpeedII: return "Swift Villagers II";
                case ResearchType.VillagerSpeedIII: return "Swift Villagers III";
                case ResearchType.TradeSpeedI: return "Trade Routes I";
                case ResearchType.TradeSpeedII: return "Trade Routes II";
                case ResearchType.TradeSpeedIII: return "Trade Routes III";
                case ResearchType.ImprovedRoadsI: return "Improved Roads I";
                case ResearchType.ImprovedRoadsII: return "Improved Roads II";
                case ResearchType.ImprovedRoadsIII: return "Improved Roads III";
                case ResearchType.PopulationCapacityI: return "Urban Planning I";
                case ResearchType.PopulationCapacityII: return "Urban Planning II";
                case ResearchType.PopulationCapacityIII: return "Urban Planning III";
                case ResearchType.EfficientRationsI: return "Efficient Rations I";
                case ResearchType.EfficientRationsII: return "Efficient Rations II";
                case ResearchType.EfficientRationsIII: return "Efficient Rations III";
                case ResearchType.BuildingSpeedI: return "Construction I";
                case ResearchType.BuildingSpeedII: return "Construction II";
                case ResearchType.BuildingSpeedIII: return "Construction III";
                case ResearchType.MarchSpeedI: return "Forced March I";
                case ResearchType.MarchSpeedII: return "Forced March II";
                case ResearchType.MarchSpeedIII: return "Forced March III";
                case ResearchType.RetreatSpeedI: return "Tactical Retreat I";
                case ResearchType.RetreatSpeedII: return "Tactical Retreat II";
                case ResearchType.RetreatSpeedIII: return "Tactical Retreat III";
                case ResearchType.InfantryMeleeAttackI: return "Infantry Weapons I";
                case ResearchType.InfantryMeleeAttackII: return "Infantry Weapons II";
                case ResearchType.InfantryMeleeAttackIII: return "Infantry Weapons III";
                case ResearchType.CavalryMeleeAttackI: return "Cavalry Weapons I";
                case ResearchType.CavalryMeleeAttackII: return "Cavalry Weapons II";
                case ResearchType.CavalryMeleeAttackIII: return "Cavalry Weapons III";
                case ResearchType.InfantryMeleeArmorI: return "Infantry Shields I";
                case ResearchType.InfantryMeleeArmorII: return "Infantry Shields II";
                case ResearchType.InfantryMeleeArmorIII: return "Infantry Shields III";
                case ResearchType.CavalryMeleeArmorI: return "Cavalry Barding I";
                case ResearchType.CavalryMeleeArmorII: return "Cavalry Barding II";
                case ResearchType.CavalryMeleeArmorIII: return "Cavalry Barding III";
                case ResearchType.ArcherMeleeArmorI: return "Archer Padding I";
                case ResearchType.ArcherMeleeArmorII: return "Archer Padding II";
                case ResearchType.ArcherMeleeArmorIII: return "Archer Padding III";
                case ResearchType.PiercingDamageI: return "Bodkin Points I";
                case ResearchType.PiercingDamageII: return "Bodkin Points II";
                case ResearchType.PiercingDamageIII: return "Bodkin Points III";
                case ResearchType.InfantryPierceArmorI: return "Infantry Mail I";
                case ResearchType.InfantryPierceArmorII: return "Infantry Mail II";
                case ResearchType.InfantryPierceArmorIII: return "Infantry Mail III";
                case ResearchType.CavalryPierceArmorI: return "Cavalry Mail I";
                case ResearchType.CavalryPierceArmorII: return "Cavalry Mail II";
                case ResearchType.CavalryPierceArmorIII: return "Cavalry Mail III";
                case ResearchType.ArcherPierceArmorI: return "Archer Mail I";
                case ResearchType.ArcherPierceArmorII: return "Archer Mail II";
                case ResearchType.ArcherPierceArmorIII: return "Archer Mail III";
                case ResearchType.SiegeBludgeonDamageI: return "Siege Ammunition I";
                case ResearchType.SiegeBludgeonDamageII: return "Siege Ammunition II";
                case ResearchType.SiegeBludgeonDamageIII: return "Siege Ammunition III";
                case ResearchType.BuildingBludgeonArmorI: return "Reinforced Walls I";
                case ResearchType.BuildingBludgeonArmorII: return "Reinforced Walls II";
                case ResearchType.BuildingBludgeonArmorIII: return "Reinforced Walls III";
                case ResearchType.MilitaryTrainingSpeedI: return "Military Drills I";
                case ResearchType.MilitaryTrainingSpeedII: return "Military Drills II";
                case ResearchType.MilitaryTrainingSpeedIII: return "Military Drills III";
                case ResearchType.MilitaryRationsI: return "Field Rations I";
                case ResearchType.MilitaryRationsII: return "Field Rations II";
                case ResearchType.MilitaryRationsIII: return "Field Rations III";
                case ResearchType.FortifiedBuildingsI: return "Fortifications I";
                case ResearchType.FortifiedBuildingsII: return "Fortifications II";
                case ResearchType.FortifiedBuildingsIII: return "Fortifications III";
                default: return type.ToString();
            }
        }

        public static string Icon(this ResearchType type)
        {
            var branch = type.Branch();
            switch (branch)
            {
                case ResearchBranch.Gathering: return "research_gathering";
                case ResearchBranch.Commerce: return "research_commerce";
                case ResearchBranch.Infrastructure: return "research_infrastructure";
                case ResearchBranch.Logistics: return "research_logistics";
                case ResearchBranch.MeleeEquipment: return "research_melee";
                case ResearchBranch.RangedEquipment: return "research_ranged";
                case ResearchBranch.SiegeFortification: return "research_siege";
                default: return "research_default";
            }
        }

        public static string Description(this ResearchType type)
        {
            switch (type)
            {
                // Gathering
                case ResearchType.FarmGatheringI: return "Improves farm gathering efficiency by 10%.";
                case ResearchType.FarmGatheringII: return "Improves farm gathering efficiency by 15%.";
                case ResearchType.FarmGatheringIII: return "Improves farm gathering efficiency by 20%.";
                case ResearchType.MiningCampGatheringI: return "Improves mining camp gathering efficiency by 10%.";
                case ResearchType.MiningCampGatheringII: return "Improves mining camp gathering efficiency by 15%.";
                case ResearchType.MiningCampGatheringIII: return "Improves mining camp gathering efficiency by 20%.";
                case ResearchType.LumberCampGatheringI: return "Improves lumber camp gathering efficiency by 10%.";
                case ResearchType.LumberCampGatheringII: return "Improves lumber camp gathering efficiency by 15%.";
                case ResearchType.LumberCampGatheringIII: return "Improves lumber camp gathering efficiency by 20%.";
                // Commerce
                case ResearchType.BetterMarketRatesI: return "Reduces market transaction fees by 5%.";
                case ResearchType.BetterMarketRatesII: return "Reduces market transaction fees by 10%.";
                case ResearchType.BetterMarketRatesIII: return "Reduces market transaction fees by 15%.";
                case ResearchType.TradeSpeedI: return "Increases trade cart speed by 10%.";
                case ResearchType.TradeSpeedII: return "Increases trade cart speed by 15%.";
                case ResearchType.TradeSpeedIII: return "Increases trade cart speed by 20%.";
                case ResearchType.ImprovedRoadsI: return "Units move 10% faster on roads.";
                case ResearchType.ImprovedRoadsII: return "Units move 15% faster on roads.";
                case ResearchType.ImprovedRoadsIII: return "Units move 20% faster on roads.";
                // Infrastructure
                case ResearchType.VillagerSpeedI: return "Increases villager movement speed by 10%.";
                case ResearchType.VillagerSpeedII: return "Increases villager movement speed by 15%.";
                case ResearchType.VillagerSpeedIII: return "Increases villager movement speed by 20%.";
                case ResearchType.PopulationCapacityI: return "Increases maximum population by 5.";
                case ResearchType.PopulationCapacityII: return "Increases maximum population by 10.";
                case ResearchType.PopulationCapacityIII: return "Increases maximum population by 15.";
                case ResearchType.EfficientRationsI: return "Reduces civilian food consumption by 5%.";
                case ResearchType.EfficientRationsII: return "Reduces civilian food consumption by 10%.";
                case ResearchType.EfficientRationsIII: return "Reduces civilian food consumption by 15%.";
                case ResearchType.BuildingSpeedI: return "Increases building construction speed by 10%.";
                case ResearchType.BuildingSpeedII: return "Increases building construction speed by 15%.";
                case ResearchType.BuildingSpeedIII: return "Increases building construction speed by 20%.";
                // Logistics
                case ResearchType.MarchSpeedI: return "Increases military march speed by 5%.";
                case ResearchType.MarchSpeedII: return "Increases military march speed by 7%.";
                case ResearchType.MarchSpeedIII: return "Increases military march speed by 10%.";
                case ResearchType.RetreatSpeedI: return "Increases retreat speed by 5%.";
                case ResearchType.RetreatSpeedII: return "Increases retreat speed by 7%.";
                case ResearchType.RetreatSpeedIII: return "Increases retreat speed by 10%.";
                case ResearchType.MilitaryTrainingSpeedI: return "Increases military training speed by 10%.";
                case ResearchType.MilitaryTrainingSpeedII: return "Increases military training speed by 15%.";
                case ResearchType.MilitaryTrainingSpeedIII: return "Increases military training speed by 20%.";
                case ResearchType.MilitaryRationsI: return "Reduces military food consumption by 5%.";
                case ResearchType.MilitaryRationsII: return "Reduces military food consumption by 10%.";
                case ResearchType.MilitaryRationsIII: return "Reduces military food consumption by 15%.";
                // Melee Equipment
                case ResearchType.InfantryMeleeAttackI: return "Increases infantry melee attack by +1.";
                case ResearchType.InfantryMeleeAttackII: return "Increases infantry melee attack by +1.";
                case ResearchType.InfantryMeleeAttackIII: return "Increases infantry melee attack by +2.";
                case ResearchType.CavalryMeleeAttackI: return "Increases cavalry melee attack by +1.";
                case ResearchType.CavalryMeleeAttackII: return "Increases cavalry melee attack by +1.";
                case ResearchType.CavalryMeleeAttackIII: return "Increases cavalry melee attack by +2.";
                case ResearchType.InfantryMeleeArmorI: return "Increases infantry melee armor by +1.";
                case ResearchType.InfantryMeleeArmorII: return "Increases infantry melee armor by +1.";
                case ResearchType.InfantryMeleeArmorIII: return "Increases infantry melee armor by +2.";
                case ResearchType.CavalryMeleeArmorI: return "Increases cavalry melee armor by +1.";
                case ResearchType.CavalryMeleeArmorII: return "Increases cavalry melee armor by +1.";
                case ResearchType.CavalryMeleeArmorIII: return "Increases cavalry melee armor by +2.";
                // Ranged Equipment
                case ResearchType.ArcherMeleeArmorI: return "Increases archer melee armor by +1.";
                case ResearchType.ArcherMeleeArmorII: return "Increases archer melee armor by +1.";
                case ResearchType.ArcherMeleeArmorIII: return "Increases archer melee armor by +2.";
                case ResearchType.PiercingDamageI: return "Increases piercing damage by +1.";
                case ResearchType.PiercingDamageII: return "Increases piercing damage by +1.";
                case ResearchType.PiercingDamageIII: return "Increases piercing damage by +2.";
                case ResearchType.InfantryPierceArmorI: return "Increases infantry pierce armor by +1.";
                case ResearchType.InfantryPierceArmorII: return "Increases infantry pierce armor by +1.";
                case ResearchType.InfantryPierceArmorIII: return "Increases infantry pierce armor by +2.";
                case ResearchType.CavalryPierceArmorI: return "Increases cavalry pierce armor by +1.";
                case ResearchType.CavalryPierceArmorII: return "Increases cavalry pierce armor by +1.";
                case ResearchType.CavalryPierceArmorIII: return "Increases cavalry pierce armor by +2.";
                case ResearchType.ArcherPierceArmorI: return "Increases archer pierce armor by +1.";
                case ResearchType.ArcherPierceArmorII: return "Increases archer pierce armor by +1.";
                case ResearchType.ArcherPierceArmorIII: return "Increases archer pierce armor by +2.";
                // Siege & Fortification
                case ResearchType.SiegeBludgeonDamageI: return "Increases siege bludgeon damage by +1.";
                case ResearchType.SiegeBludgeonDamageII: return "Increases siege bludgeon damage by +1.";
                case ResearchType.SiegeBludgeonDamageIII: return "Increases siege bludgeon damage by +2.";
                case ResearchType.BuildingBludgeonArmorI: return "Increases building bludgeon armor by +1.";
                case ResearchType.BuildingBludgeonArmorII: return "Increases building bludgeon armor by +1.";
                case ResearchType.BuildingBludgeonArmorIII: return "Increases building bludgeon armor by +2.";
                case ResearchType.FortifiedBuildingsI: return "Increases building hit points by 10%.";
                case ResearchType.FortifiedBuildingsII: return "Increases building hit points by 15%.";
                case ResearchType.FortifiedBuildingsIII: return "Increases building hit points by 20%.";
                default: return "";
            }
        }

        public static int Tier(this ResearchType type)
        {
            string name = type.ToString();
            if (name.EndsWith("III")) return 3;
            if (name.EndsWith("II")) return 2;
            return 1;
        }

        public static double ResearchTime(this ResearchType type)
        {
            switch (type.Tier())
            {
                case 1: return 30.0;
                case 2: return 60.0;
                case 3: return 120.0;
                default: return 30.0;
            }
        }

        public static int CityCenterLevelRequirement(this ResearchType type)
        {
            return type.Tier(); // Tier 1=CC1, Tier 2=CC2, Tier 3=CC3
        }

        public static ResearchCategory Category(this ResearchType type)
        {
            var branch = type.Branch();
            return branch.Category();
        }

        public static ResearchBranch Branch(this ResearchType type)
        {
            switch (type)
            {
                case ResearchType.FarmGatheringI: case ResearchType.FarmGatheringII: case ResearchType.FarmGatheringIII:
                case ResearchType.MiningCampGatheringI: case ResearchType.MiningCampGatheringII: case ResearchType.MiningCampGatheringIII:
                case ResearchType.LumberCampGatheringI: case ResearchType.LumberCampGatheringII: case ResearchType.LumberCampGatheringIII:
                    return ResearchBranch.Gathering;

                case ResearchType.BetterMarketRatesI: case ResearchType.BetterMarketRatesII: case ResearchType.BetterMarketRatesIII:
                case ResearchType.ImprovedRoadsI: case ResearchType.ImprovedRoadsII: case ResearchType.ImprovedRoadsIII:
                case ResearchType.TradeSpeedI: case ResearchType.TradeSpeedII: case ResearchType.TradeSpeedIII:
                    return ResearchBranch.Commerce;

                case ResearchType.VillagerSpeedI: case ResearchType.VillagerSpeedII: case ResearchType.VillagerSpeedIII:
                case ResearchType.PopulationCapacityI: case ResearchType.PopulationCapacityII: case ResearchType.PopulationCapacityIII:
                case ResearchType.EfficientRationsI: case ResearchType.EfficientRationsII: case ResearchType.EfficientRationsIII:
                case ResearchType.BuildingSpeedI: case ResearchType.BuildingSpeedII: case ResearchType.BuildingSpeedIII:
                    return ResearchBranch.Infrastructure;

                case ResearchType.MarchSpeedI: case ResearchType.MarchSpeedII: case ResearchType.MarchSpeedIII:
                case ResearchType.RetreatSpeedI: case ResearchType.RetreatSpeedII: case ResearchType.RetreatSpeedIII:
                case ResearchType.MilitaryTrainingSpeedI: case ResearchType.MilitaryTrainingSpeedII: case ResearchType.MilitaryTrainingSpeedIII:
                case ResearchType.MilitaryRationsI: case ResearchType.MilitaryRationsII: case ResearchType.MilitaryRationsIII:
                    return ResearchBranch.Logistics;

                case ResearchType.InfantryMeleeAttackI: case ResearchType.InfantryMeleeAttackII: case ResearchType.InfantryMeleeAttackIII:
                case ResearchType.CavalryMeleeAttackI: case ResearchType.CavalryMeleeAttackII: case ResearchType.CavalryMeleeAttackIII:
                case ResearchType.InfantryMeleeArmorI: case ResearchType.InfantryMeleeArmorII: case ResearchType.InfantryMeleeArmorIII:
                case ResearchType.CavalryMeleeArmorI: case ResearchType.CavalryMeleeArmorII: case ResearchType.CavalryMeleeArmorIII:
                    return ResearchBranch.MeleeEquipment;

                case ResearchType.PiercingDamageI: case ResearchType.PiercingDamageII: case ResearchType.PiercingDamageIII:
                case ResearchType.ArcherMeleeArmorI: case ResearchType.ArcherMeleeArmorII: case ResearchType.ArcherMeleeArmorIII:
                case ResearchType.InfantryPierceArmorI: case ResearchType.InfantryPierceArmorII: case ResearchType.InfantryPierceArmorIII:
                case ResearchType.CavalryPierceArmorI: case ResearchType.CavalryPierceArmorII: case ResearchType.CavalryPierceArmorIII:
                case ResearchType.ArcherPierceArmorI: case ResearchType.ArcherPierceArmorII: case ResearchType.ArcherPierceArmorIII:
                    return ResearchBranch.RangedEquipment;

                case ResearchType.SiegeBludgeonDamageI: case ResearchType.SiegeBludgeonDamageII: case ResearchType.SiegeBludgeonDamageIII:
                case ResearchType.BuildingBludgeonArmorI: case ResearchType.BuildingBludgeonArmorII: case ResearchType.BuildingBludgeonArmorIII:
                case ResearchType.FortifiedBuildingsI: case ResearchType.FortifiedBuildingsII: case ResearchType.FortifiedBuildingsIII:
                    return ResearchBranch.SiegeFortification;

                default: return ResearchBranch.Gathering;
            }
        }

        public static (BuildingType buildingType, int level)? BuildingRequirement(this ResearchType type)
        {
            if (type.Tier() < 2) return null;
            var gateBuilding = type.Branch().GateBuildingType();
            if (!gateBuilding.HasValue) return null;
            return (gateBuilding.Value, 1);
        }

        public static ResearchBonus[] Bonuses(this ResearchType type)
        {
            switch (type)
            {
                // Farm Gathering
                case ResearchType.FarmGatheringI: return new[] { new ResearchBonus(ResearchBonusType.FarmGatheringRate, 0.10) };
                case ResearchType.FarmGatheringII: return new[] { new ResearchBonus(ResearchBonusType.FarmGatheringRate, 0.15) };
                case ResearchType.FarmGatheringIII: return new[] { new ResearchBonus(ResearchBonusType.FarmGatheringRate, 0.20) };
                // Mining Camp
                case ResearchType.MiningCampGatheringI: return new[] { new ResearchBonus(ResearchBonusType.MiningCampGatheringRate, 0.10) };
                case ResearchType.MiningCampGatheringII: return new[] { new ResearchBonus(ResearchBonusType.MiningCampGatheringRate, 0.15) };
                case ResearchType.MiningCampGatheringIII: return new[] { new ResearchBonus(ResearchBonusType.MiningCampGatheringRate, 0.20) };
                // Lumber Camp
                case ResearchType.LumberCampGatheringI: return new[] { new ResearchBonus(ResearchBonusType.LumberCampGatheringRate, 0.10) };
                case ResearchType.LumberCampGatheringII: return new[] { new ResearchBonus(ResearchBonusType.LumberCampGatheringRate, 0.15) };
                case ResearchType.LumberCampGatheringIII: return new[] { new ResearchBonus(ResearchBonusType.LumberCampGatheringRate, 0.20) };
                // Market
                case ResearchType.BetterMarketRatesI: return new[] { new ResearchBonus(ResearchBonusType.MarketRate, 0.05) };
                case ResearchType.BetterMarketRatesII: return new[] { new ResearchBonus(ResearchBonusType.MarketRate, 0.10) };
                case ResearchType.BetterMarketRatesIII: return new[] { new ResearchBonus(ResearchBonusType.MarketRate, 0.15) };
                // Villager Speed
                case ResearchType.VillagerSpeedI: return new[] { new ResearchBonus(ResearchBonusType.VillagerMarchSpeed, 0.10) };
                case ResearchType.VillagerSpeedII: return new[] { new ResearchBonus(ResearchBonusType.VillagerMarchSpeed, 0.15) };
                case ResearchType.VillagerSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.VillagerMarchSpeed, 0.20) };
                // Trade Speed
                case ResearchType.TradeSpeedI: return new[] { new ResearchBonus(ResearchBonusType.TradeSpeed, 0.10) };
                case ResearchType.TradeSpeedII: return new[] { new ResearchBonus(ResearchBonusType.TradeSpeed, 0.15) };
                case ResearchType.TradeSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.TradeSpeed, 0.20) };
                // Roads
                case ResearchType.ImprovedRoadsI: return new[] { new ResearchBonus(ResearchBonusType.RoadSpeed, 0.10) };
                case ResearchType.ImprovedRoadsII: return new[] { new ResearchBonus(ResearchBonusType.RoadSpeed, 0.15) };
                case ResearchType.ImprovedRoadsIII: return new[] { new ResearchBonus(ResearchBonusType.RoadSpeed, 0.20) };
                // Population
                case ResearchType.PopulationCapacityI: return new[] { new ResearchBonus(ResearchBonusType.PopulationCapacity, 5.0) };
                case ResearchType.PopulationCapacityII: return new[] { new ResearchBonus(ResearchBonusType.PopulationCapacity, 10.0) };
                case ResearchType.PopulationCapacityIII: return new[] { new ResearchBonus(ResearchBonusType.PopulationCapacity, 15.0) };
                // Food Consumption
                case ResearchType.EfficientRationsI: return new[] { new ResearchBonus(ResearchBonusType.FoodConsumption, -0.05) };
                case ResearchType.EfficientRationsII: return new[] { new ResearchBonus(ResearchBonusType.FoodConsumption, -0.10) };
                case ResearchType.EfficientRationsIII: return new[] { new ResearchBonus(ResearchBonusType.FoodConsumption, -0.15) };
                // Building Speed
                case ResearchType.BuildingSpeedI: return new[] { new ResearchBonus(ResearchBonusType.BuildingSpeed, 0.10) };
                case ResearchType.BuildingSpeedII: return new[] { new ResearchBonus(ResearchBonusType.BuildingSpeed, 0.15) };
                case ResearchType.BuildingSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.BuildingSpeed, 0.20) };
                // March Speed
                case ResearchType.MarchSpeedI: return new[] { new ResearchBonus(ResearchBonusType.MilitaryMarchSpeed, 0.05) };
                case ResearchType.MarchSpeedII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryMarchSpeed, 0.07) };
                case ResearchType.MarchSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryMarchSpeed, 0.10) };
                // Retreat Speed
                case ResearchType.RetreatSpeedI: return new[] { new ResearchBonus(ResearchBonusType.MilitaryRetreatSpeed, 0.05) };
                case ResearchType.RetreatSpeedII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryRetreatSpeed, 0.07) };
                case ResearchType.RetreatSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryRetreatSpeed, 0.10) };
                // Infantry Melee Attack (+1, +1, +2)
                case ResearchType.InfantryMeleeAttackI: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeAttack, 1.0) };
                case ResearchType.InfantryMeleeAttackII: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeAttack, 1.0) };
                case ResearchType.InfantryMeleeAttackIII: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeAttack, 2.0) };
                // Cavalry Melee Attack
                case ResearchType.CavalryMeleeAttackI: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeAttack, 1.0) };
                case ResearchType.CavalryMeleeAttackII: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeAttack, 1.0) };
                case ResearchType.CavalryMeleeAttackIII: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeAttack, 2.0) };
                // Infantry Melee Armor
                case ResearchType.InfantryMeleeArmorI: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeArmor, 1.0) };
                case ResearchType.InfantryMeleeArmorII: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeArmor, 1.0) };
                case ResearchType.InfantryMeleeArmorIII: return new[] { new ResearchBonus(ResearchBonusType.InfantryMeleeArmor, 2.0) };
                // Cavalry Melee Armor
                case ResearchType.CavalryMeleeArmorI: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeArmor, 1.0) };
                case ResearchType.CavalryMeleeArmorII: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeArmor, 1.0) };
                case ResearchType.CavalryMeleeArmorIII: return new[] { new ResearchBonus(ResearchBonusType.CavalryMeleeArmor, 2.0) };
                // Archer Melee Armor
                case ResearchType.ArcherMeleeArmorI: return new[] { new ResearchBonus(ResearchBonusType.ArcherMeleeArmor, 1.0) };
                case ResearchType.ArcherMeleeArmorII: return new[] { new ResearchBonus(ResearchBonusType.ArcherMeleeArmor, 1.0) };
                case ResearchType.ArcherMeleeArmorIII: return new[] { new ResearchBonus(ResearchBonusType.ArcherMeleeArmor, 2.0) };
                // Piercing Damage
                case ResearchType.PiercingDamageI: return new[] { new ResearchBonus(ResearchBonusType.PiercingDamage, 1.0) };
                case ResearchType.PiercingDamageII: return new[] { new ResearchBonus(ResearchBonusType.PiercingDamage, 1.0) };
                case ResearchType.PiercingDamageIII: return new[] { new ResearchBonus(ResearchBonusType.PiercingDamage, 2.0) };
                // Infantry Pierce Armor
                case ResearchType.InfantryPierceArmorI: return new[] { new ResearchBonus(ResearchBonusType.InfantryPierceArmor, 1.0) };
                case ResearchType.InfantryPierceArmorII: return new[] { new ResearchBonus(ResearchBonusType.InfantryPierceArmor, 1.0) };
                case ResearchType.InfantryPierceArmorIII: return new[] { new ResearchBonus(ResearchBonusType.InfantryPierceArmor, 2.0) };
                // Cavalry Pierce Armor
                case ResearchType.CavalryPierceArmorI: return new[] { new ResearchBonus(ResearchBonusType.CavalryPierceArmor, 1.0) };
                case ResearchType.CavalryPierceArmorII: return new[] { new ResearchBonus(ResearchBonusType.CavalryPierceArmor, 1.0) };
                case ResearchType.CavalryPierceArmorIII: return new[] { new ResearchBonus(ResearchBonusType.CavalryPierceArmor, 2.0) };
                // Archer Pierce Armor
                case ResearchType.ArcherPierceArmorI: return new[] { new ResearchBonus(ResearchBonusType.ArcherPierceArmor, 1.0) };
                case ResearchType.ArcherPierceArmorII: return new[] { new ResearchBonus(ResearchBonusType.ArcherPierceArmor, 1.0) };
                case ResearchType.ArcherPierceArmorIII: return new[] { new ResearchBonus(ResearchBonusType.ArcherPierceArmor, 2.0) };
                // Siege Bludgeon Damage
                case ResearchType.SiegeBludgeonDamageI: return new[] { new ResearchBonus(ResearchBonusType.SiegeBludgeonDamage, 1.0) };
                case ResearchType.SiegeBludgeonDamageII: return new[] { new ResearchBonus(ResearchBonusType.SiegeBludgeonDamage, 1.0) };
                case ResearchType.SiegeBludgeonDamageIII: return new[] { new ResearchBonus(ResearchBonusType.SiegeBludgeonDamage, 2.0) };
                // Building Bludgeon Armor
                case ResearchType.BuildingBludgeonArmorI: return new[] { new ResearchBonus(ResearchBonusType.BuildingBludgeonArmor, 1.0) };
                case ResearchType.BuildingBludgeonArmorII: return new[] { new ResearchBonus(ResearchBonusType.BuildingBludgeonArmor, 1.0) };
                case ResearchType.BuildingBludgeonArmorIII: return new[] { new ResearchBonus(ResearchBonusType.BuildingBludgeonArmor, 2.0) };
                // Military Training Speed
                case ResearchType.MilitaryTrainingSpeedI: return new[] { new ResearchBonus(ResearchBonusType.MilitaryTrainingSpeed, 0.10) };
                case ResearchType.MilitaryTrainingSpeedII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryTrainingSpeed, 0.15) };
                case ResearchType.MilitaryTrainingSpeedIII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryTrainingSpeed, 0.20) };
                // Military Rations
                case ResearchType.MilitaryRationsI: return new[] { new ResearchBonus(ResearchBonusType.MilitaryFoodConsumption, -0.05) };
                case ResearchType.MilitaryRationsII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryFoodConsumption, -0.10) };
                case ResearchType.MilitaryRationsIII: return new[] { new ResearchBonus(ResearchBonusType.MilitaryFoodConsumption, -0.15) };
                // Fortified Buildings
                case ResearchType.FortifiedBuildingsI: return new[] { new ResearchBonus(ResearchBonusType.BuildingHP, 0.10) };
                case ResearchType.FortifiedBuildingsII: return new[] { new ResearchBonus(ResearchBonusType.BuildingHP, 0.15) };
                case ResearchType.FortifiedBuildingsIII: return new[] { new ResearchBonus(ResearchBonusType.BuildingHP, 0.20) };
                default: return new ResearchBonus[0];
            }
        }

        public static ResearchType[] Prerequisites(this ResearchType type)
        {
            switch (type)
            {
                // Economic Tier I - no prereqs (except TradeSpeedI)
                case ResearchType.FarmGatheringI:
                case ResearchType.MiningCampGatheringI:
                case ResearchType.LumberCampGatheringI:
                case ResearchType.BetterMarketRatesI:
                case ResearchType.VillagerSpeedI:
                case ResearchType.ImprovedRoadsI:
                case ResearchType.PopulationCapacityI:
                case ResearchType.EfficientRationsI:
                case ResearchType.BuildingSpeedI:
                    return new ResearchType[0];

                case ResearchType.TradeSpeedI: return new[] { ResearchType.BetterMarketRatesI };

                // Economic Tier II
                case ResearchType.FarmGatheringII: return new[] { ResearchType.FarmGatheringI };
                case ResearchType.MiningCampGatheringII: return new[] { ResearchType.MiningCampGatheringI };
                case ResearchType.LumberCampGatheringII: return new[] { ResearchType.LumberCampGatheringI };
                case ResearchType.BetterMarketRatesII: return new[] { ResearchType.BetterMarketRatesI };
                case ResearchType.VillagerSpeedII: return new[] { ResearchType.VillagerSpeedI };
                case ResearchType.TradeSpeedII: return new[] { ResearchType.TradeSpeedI, ResearchType.ImprovedRoadsI };
                case ResearchType.ImprovedRoadsII: return new[] { ResearchType.ImprovedRoadsI };
                case ResearchType.PopulationCapacityII: return new[] { ResearchType.PopulationCapacityI };
                case ResearchType.EfficientRationsII: return new[] { ResearchType.EfficientRationsI };
                case ResearchType.BuildingSpeedII: return new[] { ResearchType.BuildingSpeedI, ResearchType.LumberCampGatheringI };

                // Economic Tier III
                case ResearchType.FarmGatheringIII: return new[] { ResearchType.FarmGatheringII };
                case ResearchType.MiningCampGatheringIII: return new[] { ResearchType.MiningCampGatheringII };
                case ResearchType.LumberCampGatheringIII: return new[] { ResearchType.LumberCampGatheringII };
                case ResearchType.BetterMarketRatesIII: return new[] { ResearchType.BetterMarketRatesII };
                case ResearchType.VillagerSpeedIII: return new[] { ResearchType.VillagerSpeedII, ResearchType.EfficientRationsII };
                case ResearchType.TradeSpeedIII: return new[] { ResearchType.TradeSpeedII, ResearchType.ImprovedRoadsII };
                case ResearchType.ImprovedRoadsIII: return new[] { ResearchType.ImprovedRoadsII };
                case ResearchType.PopulationCapacityIII: return new[] { ResearchType.PopulationCapacityII, ResearchType.EfficientRationsI };
                case ResearchType.EfficientRationsIII: return new[] { ResearchType.EfficientRationsII };
                case ResearchType.BuildingSpeedIII: return new[] { ResearchType.BuildingSpeedII };

                // Military Tier I - no prereqs
                case ResearchType.MarchSpeedI:
                case ResearchType.RetreatSpeedI:
                case ResearchType.InfantryMeleeAttackI:
                case ResearchType.CavalryMeleeAttackI:
                case ResearchType.InfantryMeleeArmorI:
                case ResearchType.CavalryMeleeArmorI:
                case ResearchType.ArcherMeleeArmorI:
                case ResearchType.PiercingDamageI:
                case ResearchType.InfantryPierceArmorI:
                case ResearchType.CavalryPierceArmorI:
                case ResearchType.ArcherPierceArmorI:
                case ResearchType.SiegeBludgeonDamageI:
                case ResearchType.BuildingBludgeonArmorI:
                case ResearchType.MilitaryTrainingSpeedI:
                case ResearchType.MilitaryRationsI:
                case ResearchType.FortifiedBuildingsI:
                    return new ResearchType[0];

                // Military Tier II
                case ResearchType.MarchSpeedII: return new[] { ResearchType.MarchSpeedI };
                case ResearchType.RetreatSpeedII: return new[] { ResearchType.RetreatSpeedI, ResearchType.MarchSpeedI };
                case ResearchType.InfantryMeleeAttackII: return new[] { ResearchType.InfantryMeleeAttackI };
                case ResearchType.CavalryMeleeAttackII: return new[] { ResearchType.CavalryMeleeAttackI };
                case ResearchType.InfantryMeleeArmorII: return new[] { ResearchType.InfantryMeleeArmorI, ResearchType.InfantryMeleeAttackI };
                case ResearchType.CavalryMeleeArmorII: return new[] { ResearchType.CavalryMeleeArmorI, ResearchType.CavalryMeleeAttackI };
                case ResearchType.ArcherMeleeArmorII: return new[] { ResearchType.ArcherMeleeArmorI };
                case ResearchType.PiercingDamageII: return new[] { ResearchType.PiercingDamageI };
                case ResearchType.InfantryPierceArmorII: return new[] { ResearchType.InfantryPierceArmorI, ResearchType.InfantryMeleeArmorI };
                case ResearchType.CavalryPierceArmorII: return new[] { ResearchType.CavalryPierceArmorI, ResearchType.CavalryMeleeArmorI };
                case ResearchType.ArcherPierceArmorII: return new[] { ResearchType.ArcherPierceArmorI, ResearchType.ArcherMeleeArmorI };
                case ResearchType.SiegeBludgeonDamageII: return new[] { ResearchType.SiegeBludgeonDamageI, ResearchType.FortifiedBuildingsI };
                case ResearchType.BuildingBludgeonArmorII: return new[] { ResearchType.BuildingBludgeonArmorI, ResearchType.FortifiedBuildingsI };
                case ResearchType.MilitaryTrainingSpeedII: return new[] { ResearchType.MilitaryTrainingSpeedI };
                case ResearchType.MilitaryRationsII: return new[] { ResearchType.MilitaryRationsI };
                case ResearchType.FortifiedBuildingsII: return new[] { ResearchType.FortifiedBuildingsI };

                // Military Tier III
                case ResearchType.MarchSpeedIII: return new[] { ResearchType.MarchSpeedII };
                case ResearchType.RetreatSpeedIII: return new[] { ResearchType.RetreatSpeedII };
                case ResearchType.InfantryMeleeAttackIII: return new[] { ResearchType.InfantryMeleeAttackII };
                case ResearchType.CavalryMeleeAttackIII: return new[] { ResearchType.CavalryMeleeAttackII };
                case ResearchType.InfantryMeleeArmorIII: return new[] { ResearchType.InfantryMeleeArmorII };
                case ResearchType.CavalryMeleeArmorIII: return new[] { ResearchType.CavalryMeleeArmorII };
                case ResearchType.ArcherMeleeArmorIII: return new[] { ResearchType.ArcherMeleeArmorII };
                case ResearchType.PiercingDamageIII: return new[] { ResearchType.PiercingDamageII };
                case ResearchType.InfantryPierceArmorIII: return new[] { ResearchType.InfantryPierceArmorII };
                case ResearchType.CavalryPierceArmorIII: return new[] { ResearchType.CavalryPierceArmorII };
                case ResearchType.ArcherPierceArmorIII: return new[] { ResearchType.ArcherPierceArmorII };
                case ResearchType.SiegeBludgeonDamageIII: return new[] { ResearchType.SiegeBludgeonDamageII };
                case ResearchType.BuildingBludgeonArmorIII: return new[] { ResearchType.BuildingBludgeonArmorII };
                case ResearchType.MilitaryTrainingSpeedIII: return new[] { ResearchType.MilitaryTrainingSpeedII };
                case ResearchType.MilitaryRationsIII: return new[] { ResearchType.MilitaryRationsII, ResearchType.MilitaryTrainingSpeedII };
                case ResearchType.FortifiedBuildingsIII: return new[] { ResearchType.FortifiedBuildingsII };

                default: return new ResearchType[0];
            }
        }

        public static Dictionary<ResourceType, int> Cost(this ResearchType type)
        {
            // Economic Tier I
            switch (type)
            {
                case ResearchType.FarmGatheringI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 50}, {ResourceType.Food, 30} };
                case ResearchType.MiningCampGatheringI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 50}, {ResourceType.Food, 30} };
                case ResearchType.LumberCampGatheringI: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 40}, {ResourceType.Food, 30} };
                case ResearchType.BetterMarketRatesI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 40}, {ResourceType.Stone, 40}, {ResourceType.Food, 20} };
                case ResearchType.VillagerSpeedI: return new Dictionary<ResourceType, int> { {ResourceType.Food, 60}, {ResourceType.Wood, 20} };
                case ResearchType.TradeSpeedI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 50}, {ResourceType.Stone, 30} };
                case ResearchType.ImprovedRoadsI: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 60}, {ResourceType.Wood, 30} };
                case ResearchType.PopulationCapacityI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 50}, {ResourceType.Stone, 30} };
                case ResearchType.EfficientRationsI: return new Dictionary<ResourceType, int> { {ResourceType.Food, 80}, {ResourceType.Wood, 20} };
                case ResearchType.BuildingSpeedI: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 40}, {ResourceType.Stone, 40} };

                // Economic Tier II
                case ResearchType.FarmGatheringII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 100}, {ResourceType.Food, 60}, {ResourceType.Stone, 30} };
                case ResearchType.MiningCampGatheringII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 100}, {ResourceType.Food, 60}, {ResourceType.Stone, 30} };
                case ResearchType.LumberCampGatheringII: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 80}, {ResourceType.Food, 60}, {ResourceType.Wood, 30} };
                case ResearchType.BetterMarketRatesII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 80}, {ResourceType.Stone, 80}, {ResourceType.Food, 40} };
                case ResearchType.VillagerSpeedII: return new Dictionary<ResourceType, int> { {ResourceType.Food, 120}, {ResourceType.Wood, 40}, {ResourceType.Stone, 20} };
                case ResearchType.TradeSpeedII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 100}, {ResourceType.Stone, 60}, {ResourceType.Food, 30} };
                case ResearchType.ImprovedRoadsII: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 120}, {ResourceType.Wood, 60}, {ResourceType.Food, 30} };
                case ResearchType.PopulationCapacityII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 100}, {ResourceType.Stone, 60}, {ResourceType.Food, 30} };
                case ResearchType.EfficientRationsII: return new Dictionary<ResourceType, int> { {ResourceType.Food, 160}, {ResourceType.Wood, 40}, {ResourceType.Stone, 20} };
                case ResearchType.BuildingSpeedII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 80}, {ResourceType.Stone, 80}, {ResourceType.Food, 30} };

                // Economic Tier III
                case ResearchType.FarmGatheringIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 200}, {ResourceType.Food, 120}, {ResourceType.Stone, 60}, {ResourceType.Ore, 30} };
                case ResearchType.MiningCampGatheringIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 200}, {ResourceType.Food, 120}, {ResourceType.Stone, 60}, {ResourceType.Ore, 30} };
                case ResearchType.LumberCampGatheringIII: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 160}, {ResourceType.Food, 120}, {ResourceType.Wood, 60}, {ResourceType.Ore, 30} };
                case ResearchType.BetterMarketRatesIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 160}, {ResourceType.Stone, 160}, {ResourceType.Food, 80}, {ResourceType.Ore, 40} };
                case ResearchType.VillagerSpeedIII: return new Dictionary<ResourceType, int> { {ResourceType.Food, 240}, {ResourceType.Wood, 80}, {ResourceType.Stone, 40}, {ResourceType.Ore, 20} };
                case ResearchType.TradeSpeedIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 200}, {ResourceType.Stone, 120}, {ResourceType.Food, 60}, {ResourceType.Ore, 30} };
                case ResearchType.ImprovedRoadsIII: return new Dictionary<ResourceType, int> { {ResourceType.Stone, 240}, {ResourceType.Wood, 120}, {ResourceType.Food, 60}, {ResourceType.Ore, 40} };
                case ResearchType.PopulationCapacityIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 200}, {ResourceType.Stone, 120}, {ResourceType.Food, 60}, {ResourceType.Ore, 30} };
                case ResearchType.EfficientRationsIII: return new Dictionary<ResourceType, int> { {ResourceType.Food, 320}, {ResourceType.Wood, 80}, {ResourceType.Stone, 40}, {ResourceType.Ore, 20} };
                case ResearchType.BuildingSpeedIII: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 160}, {ResourceType.Stone, 160}, {ResourceType.Food, 60}, {ResourceType.Ore, 40} };

                default: break;
            }

            // Military costs by tier
            int tier = type.Tier();
            switch (tier)
            {
                case 1: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 75}, {ResourceType.Food, 50}, {ResourceType.Stone, 25} };
                case 2: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 150}, {ResourceType.Food, 100}, {ResourceType.Stone, 50}, {ResourceType.Ore, 25} };
                case 3: return new Dictionary<ResourceType, int> { {ResourceType.Wood, 300}, {ResourceType.Food, 200}, {ResourceType.Stone, 100}, {ResourceType.Ore, 50} };
                default: return new Dictionary<ResourceType, int>();
            }
        }
    }
}
