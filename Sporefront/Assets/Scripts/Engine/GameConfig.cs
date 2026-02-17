using System.Collections.Generic;

namespace Sporefront.Engine
{
    public static class GameConfig
    {
        public static class EngineIntervals
        {
            public const double Tick = 0.1;
            public const double VisionUpdate = 0.25;
            public const double BuildingUpdate = 0.5;
            public const double TrainingUpdate = 1.0;
            public const double CombatUpdate = 1.0;
            public const double ResourceUpdate = 0.5;
            public const double MovementUpdate = 0.1;
            public const double AIUpdate = 0.5;
        }

        public static class Movement
        {
            public const double BaseSpeed = 0.75;
            public const double TerrainSpeedMultiplier = 0.33;
            public const double RetreatSpeedBonus = 1.1;
            public const double VillagerSpeedMultiplier = 0.8;
            public const double ReinforcementSpeedMultiplier = 0.7;
        }

        public static class Combat
        {
            public const double BuildingPhaseInterval = 1.0;
            public const double SiegeBuildingBonusMultiplier = 1.5;
            public const double CavalryChargeBonus = 0.2;
            public const double InfantryChargeBonus = 0.1;
        }

        public static class Resources
        {
            public const double BaseGatherRatePerVillager = 0.2;
            public const double AdjacencyBonusPercent = 0.25;
            public const double CampLevelBonusPerLevel = 0.10;
            public const double FarmWoodConsumptionRate = 0.1;
        }

        public static class Terrain
        {
            public const double MountainBuildingCostMultiplier = 1.25;
        }

        public static class Construction
        {
            public const double ProgressChangeThreshold = 0.01;
            public const double DiminishingFactor = 0.8;

            public static double EffectiveBuilders(int count)
            {
                if (count <= 0) return 0.0;
                return (1.0 - System.Math.Pow(DiminishingFactor, count)) / (1.0 - DiminishingFactor);
            }
        }

        public static class Training
        {
            public const double VillagerTrainingTime = 10.0;
            public const double BuildingLevelSpeedBonusPerLevel = 0.10;
        }

        public static class Vision
        {
            public const int BaseUnitRange = 3;
            public const int BaseVillagerRange = 2;

            public static readonly Dictionary<Models.BuildingType, int> BuildingRanges = new Dictionary<Models.BuildingType, int>
            {
                { Models.BuildingType.CityCenter, 5 },
                { Models.BuildingType.Tower, 6 },
                { Models.BuildingType.Castle, 5 },
                { Models.BuildingType.WoodenFort, 4 },
                { Models.BuildingType.Barracks, 3 },
                { Models.BuildingType.ArcheryRange, 3 },
                { Models.BuildingType.Stable, 3 },
                { Models.BuildingType.SiegeWorkshop, 3 },
                { Models.BuildingType.LumberCamp, 2 },
                { Models.BuildingType.MiningCamp, 2 },
                { Models.BuildingType.Farm, 1 },
                { Models.BuildingType.Mill, 2 },
                { Models.BuildingType.Warehouse, 2 },
                { Models.BuildingType.Blacksmith, 2 },
                { Models.BuildingType.Market, 2 },
                { Models.BuildingType.Neighborhood, 2 },
                { Models.BuildingType.University, 3 },
                { Models.BuildingType.Library, 3 },
                { Models.BuildingType.Wall, 1 },
                { Models.BuildingType.Gate, 2 },
                { Models.BuildingType.Road, 1 }
            };
        }

        public static class Library
        {
            public const double ResearchSpeedBonusPerLevel = 0.10;
        }

        public static class Defense
        {
            public const double HPBonusPerLevel = 0.20;
            public const int CastleBaseArmyCapacity = 3;
            public const int CastleArmyCapacityPerLevel = 1;
            public const int FortBaseArmyCapacity = 1;
            public const int FortArmyCapacityPerLevel = 1;
        }

        public static class GarrisonDefense
        {
            public const double ArcherDamage = 12.0;
            public const double CrossbowDamage = 14.0;
            public const double MangonelDamage = 18.0;
            public const double TrebuchetDamage = 25.0;
        }

        public static class Commander
        {
            public const int LeadershipToArmySizeBase = 20;
            public const int LeadershipToArmySizePerPoint = 2;
            public const double TacticsTerrainScaling = 0.01;
            public const double LogisticsSpeedScaling = 0.005;
            public const double RationingReductionScaling = 0.005;
            public const double RationingReductionCap = 0.5;
            public const double EnduranceRegenScaling = 0.02;
        }

        public static class Entrenchment
        {
            public const double BuildTime = 10.0;
            public const int WoodCost = 100;
            public const double DefenseBonus = 0.10;
            public const double CheckInterval = 0.5;
        }

        public static class Stacking
        {
            public const int MaxEntitiesPerTile = 5;
        }

        public static class StackCombat
        {
            public const double StretchingPenaltyPerFront = 0.15;
            public const double ChainCombatDelay = 0.5;
        }

        public static class UnitUpgrade
        {
            public const int Tier1BuildingLevel = 2;
            public const int Tier2BuildingLevel = 3;
            public const int Tier3BuildingLevel = 5;

            public const double Tier1Time = 20.0;
            public const double Tier2Time = 40.0;
            public const double Tier3Time = 80.0;

            public const double Tier1AttackBonus = 0.5;
            public const double Tier2AttackBonus = 1.0;
            public const double Tier3AttackBonus = 1.5;

            public const double Tier1ArmorBonus = 0.5;
            public const double Tier2ArmorBonus = 1.0;
            public const double Tier3ArmorBonus = 1.5;

            public const double Tier1HPBonus = 5.0;
            public const double Tier2HPBonus = 10.0;
            public const double Tier3HPBonus = 15.0;

            public const double Tier1CostMultiplier = 2.0;
            public const double Tier2CostMultiplier = 4.0;
            public const double Tier3CostMultiplier = 8.0;

            public const double CheckInterval = 1.0;
        }

        public static class AI
        {
            public static class Intervals
            {
                public const double EconomicBuild = 2.0;
                public const double MilitaryTrain = 3.0;
                public const double Scout = 15.0;
                public const double CampBuild = 5.0;
                public const double DefenseBuild = 10.0;
                public const double GarrisonCheck = 5.0;
                public const double ResearchCheck = 5.0;
                public const double EnemyAnalysis = 10.0;
                public const double UnitUpgradeCheck = 10.0;
                public const double EntrenchCheck = 8.0;
                public const double UpgradeCheck = 10.0;
            }

            public static class Limits
            {
                public const int MaxCampsPerType = 3;
                public const int MaxTowersPerAI = 4;
                public const int MaxFortsPerAI = 2;
                public const int ScoutRange = 12;
            }

            public static class Thresholds
            {
                public const double MinThreatForDefenseBuilding = 15.0;
            }
        }
    }
}
