// ============================================================================
// FILE: AI/AITypes.cs
// PURPOSE: AI state machine, difficulty, player state, and scoring types
//          C# port of types from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

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

        public AIPlayerState(Guid playerID, AIDifficulty difficulty = AIDifficulty.Medium)
        {
            this.playerID = playerID;
            this.difficulty = difficulty;
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
