// ============================================================================
// FILE: AI/AITypes.cs
// PURPOSE: AI state machine, difficulty, player state, and scoring types
//          C# port of types from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.AI
{
    // ================================================================
    // AI State
    // ================================================================

    public enum AIState
    {
        Peace,      // Build economy, expand
        Alert,      // Enemy detected, train military
        Defense,    // Engage attacking enemies
        Attack,     // Exploit detected weakness
        Retreat     // Regroup after losses
    }

    // ================================================================
    // Enemy Strategy Read (Feature 2: Greed Detection)
    // ================================================================

    public enum EnemyStrategyRead
    {
        Unknown,    // Not yet analyzed
        Greedy,     // Heavy economy, no military — rush them
        Turtle,     // Heavy defense, static play — siege + outeconomy
        Passive,    // Has military but staying home — expand aggressively
        Balanced    // Normal play — play normal
    }

    // ================================================================
    // Game Position (Feature 4: Comeback Mechanics)
    // ================================================================

    public enum GamePosition
    {
        Winning,
        Even,
        Behind,
        CriticallyBehind
    }

    // ================================================================
    // AI Difficulty
    // ================================================================

    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    public static class AIDifficultyExtensions
    {
        public static double DecisionInterval(this AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy: return 5.0;
                case AIDifficulty.Medium: return 3.0;
                case AIDifficulty.Hard: return 1.5;
                default: return 3.0;
            }
        }

        public static double AttackThreshold(this AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy: return 2.0;
                case AIDifficulty.Medium: return 1.5;
                case AIDifficulty.Hard: return 1.2;
                default: return 1.5;
            }
        }

        public static double AlertThreshold(this AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy: return 30.0;
                case AIDifficulty.Medium: return 20.0;
                case AIDifficulty.Hard: return 10.0;
                default: return 20.0;
            }
        }

        public static double RetreatHealthThreshold(this AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy: return 0.4;
                case AIDifficulty.Medium: return 0.3;
                case AIDifficulty.Hard: return 0.2;
                default: return 0.3;
            }
        }

        public static bool CoordinatesArmies(this AIDifficulty difficulty)
        {
            return difficulty == AIDifficulty.Hard;
        }
    }

    // ================================================================
    // AI Player State
    // ================================================================

    public class AIPlayerState
    {
        public Guid playerID;
        public AIState currentState = AIState.Peace;
        public AIDifficulty difficulty = AIDifficulty.Medium;

        // Timing
        public double lastDecisionTime;
        public double lastEconomicBuildTime;
        public double lastMilitaryBuildTime;
        public double lastVillagerTrainTime;
        public double lastMilitaryTrainTime;
        public double lastScoutTime;
        public double lastCampBuildTime;

        // Research timing
        public double lastResearchCheckTime;

        // Unit upgrade timing
        public double lastUnitUpgradeCheckTime;

        // Building upgrade timing
        public double lastUpgradeCheckTime;

        // Defensive building timing
        public double lastDefenseBuildTime;
        public double lastGarrisonCheckTime;
        public double lastEntrenchCheckTime;

        // Strategic memory
        public List<HexCoordinate> knownEnemyBases = new List<HexCoordinate>();
        public HexCoordinate? lastAttackTarget;
        public int consecutiveDefenses;

        // Persistent target tracking
        public Guid? persistentAttackTargetID;
        public EnemyCompositionAnalysis? lastEnemyAnalysis;
        public double lastEnemyAnalysisTime;
        public HexCoordinate? pendingArmyConvergence;

        // Attack timeout tracking (A)
        public double attackStateEnteredTime;
        public double lastAttackProgressTime;
        public double lastKnownEnemyStrength;

        // Economy randomization (B)
        public System.Random economyRng;

        // Proactive defense tracking (D)
        public double previousThreatLevel;
        public int threatRisingCount;

        // Scouting (E)
        public bool enemyBaseFound;

        // Research inference (F)
        public HashSet<string> inferredEnemyResearch = new HashSet<string>();
        public double lastInferredResearchTime;

        // Idle army patrol (G)
        public double lastPatrolTime;

        // Zone contesting (H)
        public double lastZoneContestTime;

        // Strategic analysis (I)
        public List<ChokepointData> cachedChokepoints;
        public MapStrategy mapStrategy = MapStrategy.Balanced;
        public bool mapAnalyzed;

        // Build order (J)
        public BuildOrder currentBuildOrder;

        // Faction adaptation (K)
        public FactionStrategy? factionStrategy;

        // Threat memory (L) — tracks last-known enemy positions and encounter results
        public Dictionary<HexCoordinate, ThreatMemoryEntry> threatMemory = new Dictionary<HexCoordinate, ThreatMemoryEntry>();
        public double lastThreatMemoryCleanup;

        // Raiding (M)
        public double lastRaidTime;
        public HashSet<Guid> activeRaidArmies = new HashSet<Guid>();

        // Building upgrade timing (N)
        public double lastStrategicUpgradeTime;

        // Multi-objective attack (O)
        public double lastMultiObjectiveTime;

        // Defensive staging (P)
        public List<HexCoordinate> rallyPoints = new List<HexCoordinate>();
        public bool rallyPointsComputed;

        // Army merging (Q)
        public double lastMergeCheckTime;

        // Enemy strategy reading (R)
        public EnemyStrategyRead enemyStrategy = EnemyStrategyRead.Unknown;
        public double lastEnemyStrategyTime;

        // Villager flee (S)
        public HashSet<Guid> fleeingVillagers = new HashSet<Guid>();
        public double lastVillagerFleeCheck;

        // Comeback mechanics (T)
        public GamePosition gamePosition = GamePosition.Even;
        public double lastPositionAssessTime;

        // Round 4: Deliberate Scouting (U)
        public double mapExplorationPercent;
        public int scoutingSpiralRing;
        public double lastExplorationUpdate;
        public bool earlyScoutDispatched;

        // Round 4: Feints (V)
        public Guid? activeFeintArmyID;
        public HexCoordinate? feintTarget;
        public double feintStartTime;
        public bool feintInProgress;
        public double lastFeintAttemptTime;

        // Round 4: Resource Denial / Map Control (W)
        public List<HexCoordinate> contestedResourceNodes = new List<HexCoordinate>();
        public double lastMapControlCheckTime;

        // Round 4: Adaptive Build Order Timing (X)
        public List<BuildOrderMilestone> milestones;
        public bool buildOrderEmergency;
        public BuildingType? emergencyBuildTarget;
        public double lastMilestoneCheckTime;

        // Round 4: Composition Counter-Play (Y)
        public TargetComposition dynamicTargetComposition;
        public double lastCompositionAdaptTime;

        // Round 4: Siege Intelligence (Z)
        public bool siegeRequired;
        public bool siegeReady;
        public double siegeRequirementDetectedTime;
        public int requiredSiegeCount;

        // Round 4: Expansion Timing (AA)
        public bool expansionPlanned;
        public HexCoordinate? plannedExpansionSite;
        public double lastExpansionCheckTime;
        public int expansionCount;

        // Round 4: Commander Utilization (AB)
        public double lastCommanderCheckTime;

        public AIPlayerState(Guid playerID, AIDifficulty difficulty = AIDifficulty.Medium)
        {
            this.playerID = playerID;
            this.difficulty = difficulty;
            this.economyRng = new System.Random(playerID.GetHashCode());
        }
    }

    // ================================================================
    // Threat Memory Entry
    // ================================================================

    public struct ThreatMemoryEntry
    {
        public HexCoordinate coordinate;
        public Guid enemyPlayerID;
        public int estimatedStrength;   // Total units seen
        public double lastSeenTime;     // Game time when last observed
        public bool wasDefeatedHere;    // AI lost a fight at this location

        public ThreatMemoryEntry(HexCoordinate coordinate, Guid enemyPlayerID, int estimatedStrength, double lastSeenTime, bool wasDefeatedHere = false)
        {
            this.coordinate = coordinate;
            this.enemyPlayerID = enemyPlayerID;
            this.estimatedStrength = estimatedStrength;
            this.lastSeenTime = lastSeenTime;
            this.wasDefeatedHere = wasDefeatedHere;
        }

        /// <summary>
        /// Returns true if the memory has decayed (older than the given threshold in seconds).
        /// </summary>
        public bool IsStale(double currentTime, double decayTime = 60.0)
        {
            return currentTime - lastSeenTime > decayTime;
        }
    }

    // ================================================================
    // Build Order
    // ================================================================

    public class BuildOrderStep
    {
        public BuildingType buildingType;
        public double priorityBonus;  // Added to the building's normal build weight

        public BuildOrderStep(BuildingType buildingType, double priorityBonus)
        {
            this.buildingType = buildingType;
            this.priorityBonus = priorityBonus;
        }
    }

    public class BuildOrder
    {
        public List<BuildOrderStep> steps;
        public int currentStep;

        public BuildOrder(List<BuildOrderStep> steps)
        {
            this.steps = steps;
            this.currentStep = 0;
        }

        /// <summary>
        /// Returns the current build order step, or null if all steps are completed.
        /// </summary>
        public BuildOrderStep CurrentStep => currentStep < steps.Count ? steps[currentStep] : null;

        /// <summary>
        /// Advances to the next step (call after a building of the current type is built or skipped).
        /// </summary>
        public void Advance() { if (currentStep < steps.Count) currentStep++; }

        /// <summary>
        /// Creates a build order based on map strategy.
        /// All strategies start with LumberCamp → Farm, then diverge.
        /// </summary>
        public static BuildOrder ForStrategy(MapStrategy strategy)
        {
            var steps = new List<BuildOrderStep>
            {
                // Universal opener
                new BuildOrderStep(BuildingType.LumberCamp, 50.0),
                new BuildOrderStep(BuildingType.Farm, 40.0),
            };

            switch (strategy)
            {
                case MapStrategy.Highland:
                    steps.Add(new BuildOrderStep(BuildingType.MiningCamp, 35.0));
                    steps.Add(new BuildOrderStep(BuildingType.Blacksmith, 30.0));
                    steps.Add(new BuildOrderStep(BuildingType.Tower, 25.0));
                    steps.Add(new BuildOrderStep(BuildingType.Barracks, 30.0));
                    steps.Add(new BuildOrderStep(BuildingType.ArcheryRange, 25.0));
                    break;
                case MapStrategy.Woodland:
                    steps.Add(new BuildOrderStep(BuildingType.LumberCamp, 35.0));
                    steps.Add(new BuildOrderStep(BuildingType.Barracks, 30.0));
                    steps.Add(new BuildOrderStep(BuildingType.Farm, 25.0));
                    steps.Add(new BuildOrderStep(BuildingType.Library, 20.0));
                    steps.Add(new BuildOrderStep(BuildingType.ArcheryRange, 20.0));
                    break;
                case MapStrategy.Open:
                    steps.Add(new BuildOrderStep(BuildingType.Farm, 35.0));
                    steps.Add(new BuildOrderStep(BuildingType.Stable, 30.0));
                    steps.Add(new BuildOrderStep(BuildingType.Barracks, 25.0));
                    steps.Add(new BuildOrderStep(BuildingType.Farm, 20.0));
                    steps.Add(new BuildOrderStep(BuildingType.ArcheryRange, 20.0));
                    break;
                default: // Balanced
                    steps.Add(new BuildOrderStep(BuildingType.Barracks, 30.0));
                    steps.Add(new BuildOrderStep(BuildingType.MiningCamp, 25.0));
                    steps.Add(new BuildOrderStep(BuildingType.Farm, 25.0));
                    steps.Add(new BuildOrderStep(BuildingType.Blacksmith, 20.0));
                    steps.Add(new BuildOrderStep(BuildingType.ArcheryRange, 20.0));
                    break;
            }

            return new BuildOrder(steps);
        }
    }

    // ================================================================
    // Build Order Milestone (Feature 4: Adaptive Build Timing)
    // ================================================================

    public class BuildOrderMilestone
    {
        public double targetTime;              // Game seconds by which building should exist
        public BuildingType requiredBuilding;
        public double emergencyPriorityBonus;  // Priority boost when behind schedule

        public BuildOrderMilestone(double targetTime, BuildingType requiredBuilding, double emergencyPriorityBonus = 1.5)
        {
            this.targetTime = targetTime;
            this.requiredBuilding = requiredBuilding;
            this.emergencyPriorityBonus = emergencyPriorityBonus;
        }

        /// <summary>
        /// Creates milestones appropriate for the given map strategy.
        /// </summary>
        public static List<BuildOrderMilestone> ForStrategy(MapStrategy strategy)
        {
            var milestones = new List<BuildOrderMilestone>
            {
                // Universal milestones
                new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.FarmDeadline, BuildingType.Farm, 1.2),
                new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.BarracksDeadline, BuildingType.Barracks, 1.5),
            };

            switch (strategy)
            {
                case MapStrategy.Highland:
                    milestones.Add(new BuildOrderMilestone(200.0, BuildingType.MiningCamp, 1.0));
                    milestones.Add(new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.RangeDeadline, BuildingType.ArcheryRange, 1.2));
                    break;
                case MapStrategy.Woodland:
                    milestones.Add(new BuildOrderMilestone(240.0, BuildingType.LumberCamp, 1.0));
                    milestones.Add(new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.RangeDeadline, BuildingType.ArcheryRange, 1.2));
                    break;
                case MapStrategy.Open:
                    milestones.Add(new BuildOrderMilestone(240.0, BuildingType.Stable, 1.3));
                    milestones.Add(new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.RangeDeadline, BuildingType.ArcheryRange, 1.0));
                    break;
                default: // Balanced
                    milestones.Add(new BuildOrderMilestone(240.0, BuildingType.MiningCamp, 0.8));
                    milestones.Add(new BuildOrderMilestone(GameConfig.AI.BuildOrderTiming.RangeDeadline, BuildingType.ArcheryRange, 1.0));
                    break;
            }

            return milestones;
        }
    }

    // ================================================================
    // Target Composition
    // ================================================================

    public class TargetComposition
    {
        public double infantry;
        public double ranged;
        public double cavalry;
        public double siege;

        public TargetComposition(double infantry, double ranged, double cavalry, double siege)
        {
            this.infantry = infantry;
            this.ranged = ranged;
            this.cavalry = cavalry;
            this.siege = siege;
        }

        /// <summary>
        /// Returns the target ratio for a given unit category.
        /// </summary>
        public double GetRatio(UnitCategory category)
        {
            switch (category)
            {
                case UnitCategory.Infantry: return infantry;
                case UnitCategory.Ranged: return ranged;
                case UnitCategory.Cavalry: return cavalry;
                case UnitCategory.Siege: return siege;
                default: return 0.25;
            }
        }

        /// <summary>
        /// Returns a target composition based on map strategy and AI state.
        /// Adjusts composition for current needs (more siege when attacking, more infantry when defending).
        /// </summary>
        public static TargetComposition ForStrategy(MapStrategy strategy, AIState state)
        {
            double inf, rng, cav, sig;

            switch (strategy)
            {
                case MapStrategy.Highland:
                    inf = 0.30; rng = 0.40; cav = 0.10; sig = 0.20;
                    break;
                case MapStrategy.Woodland:
                    inf = 0.50; rng = 0.30; cav = 0.10; sig = 0.10;
                    break;
                case MapStrategy.Open:
                    inf = 0.20; rng = 0.20; cav = 0.40; sig = 0.20;
                    break;
                default: // Balanced
                    inf = 0.35; rng = 0.25; cav = 0.25; sig = 0.15;
                    break;
            }

            // State-based adjustments
            switch (state)
            {
                case AIState.Attack:
                    // Boost siege when attacking
                    sig += 0.10;
                    // Reduce the highest non-siege category
                    if (inf >= rng && inf >= cav) inf -= 0.10;
                    else if (rng >= cav) rng -= 0.10;
                    else cav -= 0.10;
                    break;
                case AIState.Defense:
                    // Boost infantry when defending
                    inf += 0.10;
                    // Reduce siege
                    sig = System.Math.Max(0.0, sig - 0.10);
                    break;
            }

            return new TargetComposition(inf, rng, cav, sig);
        }
    }

    // ================================================================
    // Enemy Composition Analysis
    // ================================================================

    public struct EnemyCompositionAnalysis
    {
        public double cavalryRatio;
        public double rangedRatio;
        public double infantryRatio;
        public double siegeRatio;
        public int totalStrength;
        public double weightedStrength;

        public UnitCategory? DominantCategory
        {
            get
            {
                double maxRatio = 0;
                UnitCategory? dominant = null;

                if (cavalryRatio > maxRatio) { maxRatio = cavalryRatio; dominant = UnitCategory.Cavalry; }
                if (rangedRatio > maxRatio) { maxRatio = rangedRatio; dominant = UnitCategory.Ranged; }
                if (infantryRatio > maxRatio) { maxRatio = infantryRatio; dominant = UnitCategory.Infantry; }
                if (siegeRatio > maxRatio) { dominant = UnitCategory.Siege; }

                return dominant;
            }
        }
    }

    // ================================================================
    // Target Score
    // ================================================================

    public struct TargetScore
    {
        public Guid targetID;
        public HexCoordinate coordinate;
        public double score;
        public bool isBuilding;

        public TargetScore(Guid targetID, HexCoordinate coordinate, double score, bool isBuilding)
        {
            this.targetID = targetID;
            this.coordinate = coordinate;
            this.score = score;
            this.isBuilding = isBuilding;
        }
    }
}
