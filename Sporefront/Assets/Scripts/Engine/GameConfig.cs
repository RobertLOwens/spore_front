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

        public static class Poison
        {
            public const double BasePoisonDamagePerTick = 2.0;
            public const double PoisonDuration = 5.0;
            public const double IncreasedPoisonMultiplier = 1.5;
            public const int MaxPoisonStacks = 3;
            public const int SporeBurstRadius = 1;
            public const double SporeBurstDamageMultiplier = 0.5;
        }

        public static class FalseMorel
        {
            public const double PoisonDamagePerTick = 3.0;              // Base 3 DPS
            public const double PoisonDuration = 10.0;                   // Base 10 seconds
            public const double ToxicSporesMultiplier = 1.5;             // Tier I: +50% DPS
            public const double LethalSporesDPSMultiplier = 2.0;         // Tier II: 2x DPS
            public const double LethalSporesDurationMultiplier = 1.5;    // Tier II: +50% duration
        }

        public static class Scout
        {
            public const double MovementSpeedMultiplier = 1.5;     // 50% faster than armies
            public const double MaxStamina = 100.0;
            public const double StaminaCostPerTile = 10.0;
            public const double StaminaRegenPerSecond = 5.0;
            public const int VisionRange = 3;
            public const double TrainingTime = 15.0;
            public const int FoodCost = 50;
            public const double MaxHp = 30.0;
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
            public const int BaseUnitRange = 2;
            public const int BaseVillagerRange = 2;

            public static readonly Dictionary<Models.BuildingType, int> BuildingRanges = new Dictionary<Models.BuildingType, int>
            {
                { Models.BuildingType.CityCenter, 1 },
                { Models.BuildingType.Tower, 1 },
                { Models.BuildingType.Castle, 1 },
                { Models.BuildingType.WoodenFort, 1 },
                { Models.BuildingType.Barracks, 1 },
                { Models.BuildingType.ArcheryRange, 1 },
                { Models.BuildingType.Stable, 1 },
                { Models.BuildingType.SiegeWorkshop, 1 },
                { Models.BuildingType.LumberCamp, 1 },
                { Models.BuildingType.MiningCamp, 1 },
                { Models.BuildingType.Farm, 1 },
                { Models.BuildingType.Mill, 1 },
                { Models.BuildingType.Warehouse, 1 },
                { Models.BuildingType.Blacksmith, 1 },
                { Models.BuildingType.Market, 1 },
                { Models.BuildingType.Neighborhood, 1 },
                { Models.BuildingType.University, 1 },
                { Models.BuildingType.Library, 1 },
                { Models.BuildingType.Wall, 1 },
                { Models.BuildingType.Gate, 1 },
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
            public const int MaxBuildingsPerTarget = 3;
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

            // XP rewards
            public const int XPPerKill = 1;
            public const int XPPerCombatParticipation = 5;
            public const int XPPerVictory = 10;
            public const int XPPerBuildingDestroyed = 25;
        }

        public static class Entrenchment
        {
            public const double BuildTime = 10.0;
            public const int WoodCost = 100;
            public const double DefenseBonus = 0.10;
            public const double CheckInterval = 0.5;
        }

        public static class Stamina
        {
            public const double MovementCostPerTile = 1.0;
            public const double CombatCostPerRound = 3.0;
            public const double IdleRegenPerSecond = 1.0 / 300.0;
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

        public static class BuildingHealth
        {
            public const double Wall = 600.0;
            public const double Gate = 400.0;
            public const double Military = 500.0;
            public const double Civilian = 200.0;
            public const double FalseMorel = 50.0;
        }

        public static class Demolition
        {
            public const double TimeMultiplier = 0.5;
            public const double DemolisherSpeedBonus = 0.5;
            public const double RefundMultiplier = 0.25;
            public const int BurnAreasFoodPerLevel = 25;
        }

        public static class Domination
        {
            public const int ZoneCount = 3;
            public const int ZoneRadius = 2;
            public const double PointsPerSecond = 1.0;
            public const double ScoreToWin = 300.0;
            public const double UpdateInterval = 1.0;

            // Crooked Domination — how far A/C zones shift toward each player
            public const double CrookedAxisOffset = 0.3;

            // Ring — concentric circles at map center
            public const int RingInnerRadius = 2;
            public const int RingOuterRadius = 4;
            public const double RingInnerMultiplier = 2.0;
            public const double RingOuterMultiplier = 1.0;
        }

        public static class MapDimensions
        {
            public const int Small = 25;
            public const int Medium = 35;
            public const int Large = 50;
            public const int Huge = 65;
            public const int Arena = 7;
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

            public static class Timeouts
            {
                public const double AttackTimeout = 60.0;
            }

            public static class Economy
            {
                public const double RandomizationWeight = 0.2;
            }

            public static class Hunting
            {
                public const double VillagerAttackPower = 25.0;      // Damage per villager per hunt tick
                public const double VillagerDefenseFactor = 0.5;     // Defense per villager against animal
                public const double VillagerDeathThreshold = 5.0;    // Damage per villager lost
                public const int MaxScoutCandidateUnits = 5;         // Max units in early scout army
            }

            public static class Scouting
            {
                public const int MaxScouts = 2;
                public const int PatrolRadius = 6;
                public const double EarlyGameThreshold = 300.0;
                public const double EarlyScoutTime = 30.0;             // Send first scout by 30s
                public const double EarlyScoutWindow = 120.0;           // Stop trying early scout after this time
                public const double ExplorationUpdateInterval = 10.0;   // How often to recalc exploration %
                public const double MinExplorationBeforeAttack = 0.3;   // 30% explored before attacking blind
            }

            public static class ThreatMemory
            {
                public const double DecayTime = 60.0;           // Seconds before a memory entry goes stale
                public const double CleanupInterval = 30.0;      // How often to purge stale entries
                public const double OpportunityWindow = 10.0;    // Seconds after enemy army leaves before AI tries to exploit
            }

            public static class Raiding
            {
                public const double RaidInterval = 20.0;         // Min seconds between raid attempts
                public const int MinCavalryForRaid = 2;          // Min units in a raid-eligible army
                public const int MaxRaidDistance = 15;            // Max distance from CC for raid targets
                public const int FleeRadius = 4;                 // Retreat when enemy army within this range
            }

            public static class Staging
            {
                public const int MaxRallyPoints = 3;             // Max rally points to compute
                public const int StagingDistanceFromCC = 5;      // How far out to stage armies
                public const double RestageInterval = 15.0;      // How often to recheck staging
            }

            // Round 4 constants

            public static class Feint
            {
                public const double FeintDuration = 15.0;        // Max seconds for feint maneuver
                public const double FeintCooldown = 60.0;         // Min seconds between feints
                public const int MinArmiesForFeint = 2;           // Minimum armies to attempt feint
                public const int FeintRetreatRadius = 3;          // Retreat feint when enemy this close
            }

            public static class MapControl
            {
                public const double CheckInterval = 20.0;         // How often to check for contested resources
                public const double ContestDistanceRatio = 0.7;   // Nodes within 70% equidistant = contested
                public const int MaxContestedCamps = 2;            // Max camps beyond normal limit for map control
            }

            public static class BuildOrderTiming
            {
                public const double MilestoneCheckInterval = 10.0;
                public const double BarracksDeadline = 180.0;      // Must have barracks by 3 minutes
                public const double FarmDeadline = 120.0;          // Must have farm by 2 minutes
                public const double RangeDeadline = 300.0;         // Should have range by 5 minutes
                public const double EmergencyPriorityBonus = 1.5;  // Weight override for emergency builds
            }

            public static class Composition
            {
                public const double AdaptInterval = 15.0;          // How often to recalculate counter composition
                public const double StrongCounterBonus = 40.0;     // Bonus when countering >50% enemy category
                public const double MildCounterBonus = 25.0;       // Bonus when countering >30% enemy category
                public const double CounteredPenalty = 15.0;        // Penalty for units countered by enemy
                public const double StrongCounterThreshold = 0.5;
                public const double MildCounterThreshold = 0.3;
            }

            public static class Siege
            {
                public const int DefenseBuildingThreshold = 2;     // This many defensive buildings = siege needed
                public const int SiegePerTower = 1;                // Siege units needed per tower
                public const int SiegePerFort = 2;                 // Siege units needed per fort/castle
                public const double SiegeTrainingBonus = 50.0;     // Score bonus for siege when needed
                public const double SiegeWaitTimeout = 90.0;       // Don't wait forever for siege
                public const double WorkshopBuildPriority = 0.8;   // Economy weight for siege workshop when needed
            }

            public static class Expansion
            {
                public const double MinGameTime = 300.0;           // 5 minutes before considering expansion
                public const double CheckInterval = 30.0;
                public const int MinSurplusFood = 200;
                public const int MinSurplusWood = 300;
                public const int MaxExpansions = 1;                // Only 1 extra CC
                public const int MinDistFromCC = 8;
                public const int MaxDistFromCC = 15;
            }

            public static class Commander
            {
                public const double CheckInterval = 15.0;
                public const int MinArmiesForReassignment = 2;
                public const double StabilityBonus = 10.0;          // Bonus for staying with current army
                public const double ReassignmentThreshold = 20.0;   // Min improvement to justify swap
            }
        }

        public static class Online
        {
            // Heartbeat
            public const float HeartbeatIntervalSeconds = 30f;

            // Disconnect detection
            public const double DisconnectTimeoutSeconds = 60.0;
            public const double AbandonTimeoutSeconds = 180.0;

            // Command submission
            public const int MaxCommandRetries = 3;
            public const double RetryBaseDelaySeconds = 1.0;

            // Snapshots
            public const int SnapshotCommandInterval = 100;
            public const double SnapshotTimeIntervalSeconds = 300.0;
            public const int MaxSnapshots = 3;

            // Matchmaking
            public const float QueueTimeoutSeconds = 120f;
            public const float ReadyTimeoutSeconds = 30f;
            public const float PollIntervalSeconds = 3f;
            public const float StaleEntryAgeSeconds = 120f;

            // Win conditions
            public const double WinConditionCheckInterval = 1.0;
            public const double StarvationThresholdSeconds = 60.0;
            public const double WinConditionGracePeriod = 30.0;

            // Reconnection
            public const int MaxJoinRetries = 5;
            public const float JoinRetryDelaySeconds = 2f;

            // Desync detection — include state hash every N commands
            public const int DesyncCheckInterval = 10;

            // Snapshot loading timeout (seconds)
            public const float SnapshotLoadTimeoutSeconds = 15f;
        }
    }
}
