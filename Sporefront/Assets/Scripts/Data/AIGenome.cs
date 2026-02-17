// ============================================================================
// FILE: Data/AIGenome.cs
// PURPOSE: Tunable AI parameter genome for evolutionary optimization
//          C# port of AIGenome.swift
// ============================================================================

using System;
using System.IO;
using UnityEngine;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [Serializable]
    public class AIGenome
    {
        // ================================================================
        // Metadata
        // ================================================================

        public string id = Guid.NewGuid().ToString();
        public string name = "";
        public string savedDate = "";
        public int generation;
        public double fitness;
        public int wins;
        public int gamesPlayed;
        public string[] parentIDs = new string[0];
        public string mapType = "arabia";

        public int WinRatePercent
        {
            get
            {
                if (gamesPlayed <= 0) return 0;
                return (int)((double)wins / gamesPlayed * 100);
            }
        }

        // ================================================================
        // Interval Parameters (seconds between actions)
        // ================================================================

        public double economicBuildInterval = 2.0;
        public double militaryTrainInterval = 3.0;
        public double scoutInterval = 15.0;
        public double campBuildInterval = 5.0;
        public double defenseBuildInterval = 10.0;
        public double garrisonCheckInterval = 5.0;
        public double researchCheckInterval = 5.0;
        public double upgradeCheckInterval = 10.0;
        public double unitUpgradeCheckInterval = 10.0;
        public double entrenchCheckInterval = 8.0;

        // ================================================================
        // Aggressiveness Parameters
        // ================================================================

        public double decisionInterval = 3.0;
        public double attackThreshold = 1.5;
        public double alertThreshold = 20.0;
        public double retreatHealthThreshold = 0.3;
        public double minWeightedStrengthForAttack = 2000.0;

        // ================================================================
        // Economy Parameters
        // ================================================================

        public int maxVillagers = 20;
        public double farmFoodUrgencyThreshold = 0.5;
        public double farmFoodRateThreshold = 2.0;
        public int villagerDeployThreshold = 3;
        public double foodUrgencyMultiplier = 1.2;
        public double woodUrgencyMultiplier = 1.15;
        public int maxGatherersPerResource = 2;
        public int armyDeployMinGarrison = 5;

        // ================================================================
        // Building Upgrade Priority Scores
        // ================================================================

        public double upgradePriorityCityCenter = 100.0;
        public double upgradePriorityMilitary = 60.0;
        public double upgradePriorityFarm = 40.0;
        public double upgradePriorityBlacksmith = 35.0;
        public double upgradePriorityWarehouse = 30.0;
        public double upgradePriorityLibrary = 25.0;

        // ================================================================
        // Military Parameters
        // ================================================================

        public double counterCavalryThreshold = 0.35;
        public double counterRangedThreshold = 0.4;
        public double counterInfantryThreshold = 0.4;
        public int retreatUnitThreshold = 5;
        public int retreatDistanceFromBase = 3;

        // ================================================================
        // Defense Parameters
        // ================================================================

        public int maxTowers = 4;
        public int maxForts = 2;
        public double minThreatForDefense = 15.0;
        public int peacetimeDefenseWood = 500;
        public int peacetimeDefenseStone = 400;
        public int maxEntrenchedLow = 2;
        public int maxEntrenchedHigh = 4;

        // ================================================================
        // Target Scoring Parameters
        // ================================================================

        public double baseArmyScore = 50.0;
        public double smallArmyBonus = 15.0;
        public double entrenchedPenalty = 20.0;
        public double cityCenterValue = 100.0;

        // ================================================================
        // Default Genome
        // ================================================================

        public static AIGenome Default => new AIGenome();

        // ================================================================
        // Persistence
        // ================================================================

        public static AIGenome LoadBest(string mapType = "arabia")
        {
            string filename = $"best_genome_{mapType}.json";
            string path = Path.Combine(Application.persistentDataPath, filename);

            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<AIGenome>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(AIGenome genome)
        {
            string filename = $"best_genome_{genome.mapType}.json";
            string path = Path.Combine(Application.persistentDataPath, filename);

            try
            {
                string json = JsonUtility.ToJson(genome, true);
                File.WriteAllText(path, json);
                DebugLog.Log($"Saved best genome (gen {genome.generation}, fitness {genome.fitness:F2}) to {filename}");
            }
            catch (Exception e)
            {
                DebugLog.Log($"Failed to save genome: {e.Message}");
            }
        }
    }
}
