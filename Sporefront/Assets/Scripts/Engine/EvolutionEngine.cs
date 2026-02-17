// ============================================================================
// FILE: Engine/EvolutionEngine.cs
// PURPOSE: Evolutionary algorithm for discovering optimal AI parameters
//          C# port of EvolutionEngine.swift (472 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sporefront.Data;

namespace Sporefront.Engine
{
    // ================================================================
    // Evolution Configuration
    // ================================================================

    [Serializable]
    public class EvolutionConfig
    {
        public int populationSize = 32;
        public int gamesPerEvaluation = 6;
        public int maxGenerations = 100;
        public int eliteCount = 4;
        public double mutationRate = 0.15;
        public double mutationMagnitude = 0.2;
        public double crossoverRate = 0.7;
        public int parallelGames = 8;
        public string mapType = "arabia";
    }

    // ================================================================
    // Generation Stats
    // ================================================================

    public class GenerationStats
    {
        public int generation;
        public double bestFitness;
        public double averageFitness;
        public double bestWinRate;
        public string bestGenomeID;

        public GenerationStats(int generation, double bestFitness, double averageFitness,
            double bestWinRate, string bestGenomeID)
        {
            this.generation = generation;
            this.bestFitness = bestFitness;
            this.averageFitness = averageFitness;
            this.bestWinRate = bestWinRate;
            this.bestGenomeID = bestGenomeID;
        }
    }

    // ================================================================
    // Evolution Engine
    // ================================================================

    /// <summary>
    /// Full genetic algorithm with parallel evaluation for discovering optimal AI parameters.
    /// </summary>
    public class EvolutionEngine
    {
        // ================================================================
        // Properties
        // ================================================================

        public EvolutionConfig config { get; private set; }
        public int currentGeneration { get; private set; }
        public List<AIGenome> population { get; private set; } = new List<AIGenome>();
        public List<GenerationStats> generationHistory { get; private set; } = new List<GenerationStats>();
        public bool isRunning { get; private set; }

        private volatile bool shouldStop;
        private readonly object resultsLock = new object();
        private System.Random rng = new System.Random();

        // ================================================================
        // Callbacks
        // ================================================================

        public event Action<int, AIGenome, List<AIGenome>> OnGenerationComplete;
        public event Action<string> OnProgress;
        public event Action<AIGenome> OnComplete;

        // ================================================================
        // Init
        // ================================================================

        public EvolutionEngine(EvolutionConfig config = null)
        {
            this.config = config ?? new EvolutionConfig();
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Start()
        {
            if (isRunning) return;
            isRunning = true;
            shouldStop = false;
            currentGeneration = 0;
            generationHistory.Clear();

            // Initialize population
            population = CreateInitialPopulation();

            OnProgress?.Invoke($"Starting evolution with {config.populationSize} genomes...");

            Task.Run(() => RunEvolutionLoop());
        }

        public void Stop()
        {
            shouldStop = true;
        }

        public void UpdateConfig(EvolutionConfig newConfig)
        {
            if (isRunning) return;
            config = newConfig;
        }

        // ================================================================
        // Evolution Loop
        // ================================================================

        private void RunEvolutionLoop()
        {
            // Generate fixed map seeds for fair evaluation
            var mapSeeds = new ulong[config.gamesPerEvaluation];
            for (int i = 0; i < config.gamesPerEvaluation; i++)
            {
                lock (rng)
                {
                    var bytes = new byte[8];
                    rng.NextBytes(bytes);
                    mapSeeds[i] = BitConverter.ToUInt64(bytes, 0);
                    if (mapSeeds[i] == 0) mapSeeds[i] = 1;
                }
            }

            while (currentGeneration < config.maxGenerations && !shouldStop)
            {
                int gen = currentGeneration;

                OnProgress?.Invoke($"Generation {gen + 1}/{config.maxGenerations} — Evaluating {config.populationSize} genomes...");

                // Evaluate all genomes
                EvaluatePopulation(mapSeeds);

                // Sort by fitness (descending)
                population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

                // Record stats
                var bestGenome = population[0];
                double totalFitness = 0;
                foreach (var g in population)
                    totalFitness += g.fitness;
                double avgFitness = totalFitness / population.Count;
                double bestWinRate = bestGenome.gamesPlayed > 0
                    ? (double)bestGenome.wins / bestGenome.gamesPlayed
                    : 0;

                var stats = new GenerationStats(gen, bestGenome.fitness, avgFitness, bestWinRate, bestGenome.id);

                lock (resultsLock)
                {
                    generationHistory.Add(stats);
                }

                // Save best genome
                var bestToSave = CloneGenome(bestGenome);
                bestToSave.mapType = config.mapType;
                AIGenome.Save(bestToSave);

                OnGenerationComplete?.Invoke(gen, bestGenome, population);
                OnProgress?.Invoke($"Gen {gen + 1}: Best fitness {bestGenome.fitness:F2} ({bestWinRate * 100:F0}% win rate)");

                // Evolve next generation (unless last)
                if (gen + 1 < config.maxGenerations && !shouldStop)
                {
                    population = EvolveNextGeneration();
                }

                currentGeneration++;
            }

            isRunning = false;

            // Final best
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
            if (population.Count > 0)
            {
                var best = CloneGenome(population[0]);
                best.mapType = config.mapType;
                // Save to genome library
                GenomeLibrary.Instance.Save(best);

                OnComplete?.Invoke(best);
                OnProgress?.Invoke($"Evolution complete! Best fitness: {best.fitness:F2}");
            }
        }

        // ================================================================
        // Population Initialization
        // ================================================================

        private List<AIGenome> CreateInitialPopulation()
        {
            var pop = new List<AIGenome>();

            // First genome is always the default (current AI behavior)
            var defaultGenome = CloneGenome(AIGenome.Default);
            defaultGenome.generation = 0;
            defaultGenome.mapType = config.mapType;
            pop.Add(defaultGenome);

            // If a previously evolved genome exists, include it
            var evolved = AIGenome.LoadBest(config.mapType);
            if (evolved != null)
            {
                var evolvedCopy = CloneGenome(evolved);
                evolvedCopy.id = Guid.NewGuid().ToString();
                evolvedCopy.generation = 0;
                evolvedCopy.fitness = 0;
                evolvedCopy.wins = 0;
                evolvedCopy.gamesPlayed = 0;
                pop.Add(evolvedCopy);
            }

            // Fill rest with random mutations of default
            while (pop.Count < config.populationSize)
            {
                var mutant = Mutate(CloneGenome(AIGenome.Default), 0.5, 0.3);
                mutant.generation = 0;
                mutant.mapType = config.mapType;
                pop.Add(mutant);
            }

            return pop;
        }

        // ================================================================
        // Evaluation
        // ================================================================

        private void EvaluatePopulation(ulong[] mapSeeds)
        {
            // Reset fitness for this generation
            for (int i = 0; i < population.Count; i++)
            {
                population[i].fitness = 0;
                population[i].wins = 0;
                population[i].gamesPlayed = 0;
            }

            // Each genome plays gamesPerEvaluation games against random opponents
            var semaphore = new SemaphoreSlim(config.parallelGames, config.parallelGames);
            var tasks = new List<Task>();

            var results = new List<(int genomeIndex, int opponentIndex, int seedIndex, GameResult result)>();

            for (int i = 0; i < population.Count; i++)
            {
                for (int seedIdx = 0; seedIdx < config.gamesPerEvaluation; seedIdx++)
                {
                    if (shouldStop) break;

                    // Pick a random opponent (not self)
                    int opponentIdx;
                    lock (rng)
                    {
                        opponentIdx = rng.Next(0, population.Count);
                        while (opponentIdx == i)
                            opponentIdx = rng.Next(0, population.Count);
                    }

                    // Snapshot values for the closure
                    int capturedI = i;
                    int capturedOpp = opponentIdx;
                    int capturedSeed = seedIdx;
                    var genome1 = population[i];
                    var genome2 = population[opponentIdx];
                    ulong seed = mapSeeds[seedIdx];

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var result = GameSimulator.RunGame(genome1, genome2, seed);
                            DebugLog.Log($"Game: G{capturedI} vs G{capturedOpp} seed={seed} -> " +
                                $"{(result.winnerIndex.HasValue ? $"P{result.winnerIndex.Value + 1}" : "Draw")} " +
                                $"({result.reason}, {result.duration:F0}s)");

                            lock (resultsLock)
                            {
                                results.Add((capturedI, capturedOpp, capturedSeed, result));
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }
            }

            Task.WaitAll(tasks.ToArray());

            // Process results
            foreach (var entry in results)
            {
                int genomeIdx = entry.genomeIndex;
                var result = entry.result;

                population[genomeIdx].gamesPlayed++;

                if (result.winnerIndex.HasValue)
                {
                    if (result.winnerIndex.Value == 0)
                    {
                        // genome won (it was player 1)
                        population[genomeIdx].wins++;
                        population[genomeIdx].fitness += 3.0;
                    }
                    // else: genome lost, fitness += 0.0
                }
                else
                {
                    // draw
                    population[genomeIdx].fitness += 1.0;
                }
            }

            // Normalize fitness by games played
            for (int i = 0; i < population.Count; i++)
            {
                if (population[i].gamesPlayed > 0)
                {
                    population[i].fitness /= population[i].gamesPlayed;
                }
            }
        }

        // ================================================================
        // Selection & Evolution
        // ================================================================

        private List<AIGenome> EvolveNextGeneration()
        {
            var nextGen = new List<AIGenome>();

            // Elitism: keep top N unchanged
            int eliteCount = Math.Min(config.eliteCount, population.Count);
            for (int i = 0; i < eliteCount; i++)
            {
                var elite = CloneGenome(population[i]);
                elite.generation = currentGeneration + 1;
                elite.parentIDs = new string[] { population[i].id };
                elite.id = Guid.NewGuid().ToString();
                elite.fitness = 0;
                elite.wins = 0;
                elite.gamesPlayed = 0;
                nextGen.Add(elite);
            }

            // Fill remaining with offspring
            while (nextGen.Count < config.populationSize)
            {
                if (shouldStop) break;

                var parent1 = TournamentSelect();
                var parent2 = TournamentSelect();

                AIGenome child;
                double crossoverRoll;
                lock (rng) { crossoverRoll = rng.NextDouble(); }

                if (crossoverRoll < config.crossoverRate)
                {
                    child = Crossover(parent1, parent2);
                    child.parentIDs = new string[] { parent1.id, parent2.id };
                }
                else
                {
                    child = CloneGenome(parent1);
                    child.parentIDs = new string[] { parent1.id };
                }

                child = Mutate(child, config.mutationRate, config.mutationMagnitude);
                child.id = Guid.NewGuid().ToString();
                child.generation = currentGeneration + 1;
                child.fitness = 0;
                child.wins = 0;
                child.gamesPlayed = 0;
                child.mapType = config.mapType;
                nextGen.Add(child);
            }

            return nextGen;
        }

        /// <summary>
        /// Tournament selection: pick 4 random genomes, return the best.
        /// </summary>
        private AIGenome TournamentSelect()
        {
            int tournamentSize = Math.Min(4, population.Count);
            AIGenome best = null;

            for (int i = 0; i < tournamentSize; i++)
            {
                int idx;
                lock (rng) { idx = rng.Next(0, population.Count); }
                var candidate = population[idx];
                if (best == null || candidate.fitness > best.fitness)
                    best = candidate;
            }

            return best ?? population[0];
        }

        // ================================================================
        // Crossover
        // ================================================================

        private AIGenome Crossover(AIGenome a, AIGenome b)
        {
            var child = new AIGenome();

            // For each parameter, randomly pick from parent a or b
            child.economicBuildInterval = RandBool() ? a.economicBuildInterval : b.economicBuildInterval;
            child.militaryTrainInterval = RandBool() ? a.militaryTrainInterval : b.militaryTrainInterval;
            child.scoutInterval = RandBool() ? a.scoutInterval : b.scoutInterval;
            child.campBuildInterval = RandBool() ? a.campBuildInterval : b.campBuildInterval;
            child.defenseBuildInterval = RandBool() ? a.defenseBuildInterval : b.defenseBuildInterval;
            child.garrisonCheckInterval = RandBool() ? a.garrisonCheckInterval : b.garrisonCheckInterval;
            child.researchCheckInterval = RandBool() ? a.researchCheckInterval : b.researchCheckInterval;
            child.upgradeCheckInterval = RandBool() ? a.upgradeCheckInterval : b.upgradeCheckInterval;
            child.unitUpgradeCheckInterval = RandBool() ? a.unitUpgradeCheckInterval : b.unitUpgradeCheckInterval;
            child.entrenchCheckInterval = RandBool() ? a.entrenchCheckInterval : b.entrenchCheckInterval;

            child.decisionInterval = RandBool() ? a.decisionInterval : b.decisionInterval;
            child.attackThreshold = RandBool() ? a.attackThreshold : b.attackThreshold;
            child.alertThreshold = RandBool() ? a.alertThreshold : b.alertThreshold;
            child.retreatHealthThreshold = RandBool() ? a.retreatHealthThreshold : b.retreatHealthThreshold;
            child.minWeightedStrengthForAttack = RandBool() ? a.minWeightedStrengthForAttack : b.minWeightedStrengthForAttack;

            child.maxVillagers = RandBool() ? a.maxVillagers : b.maxVillagers;
            child.farmFoodUrgencyThreshold = RandBool() ? a.farmFoodUrgencyThreshold : b.farmFoodUrgencyThreshold;
            child.farmFoodRateThreshold = RandBool() ? a.farmFoodRateThreshold : b.farmFoodRateThreshold;
            child.villagerDeployThreshold = RandBool() ? a.villagerDeployThreshold : b.villagerDeployThreshold;
            child.foodUrgencyMultiplier = RandBool() ? a.foodUrgencyMultiplier : b.foodUrgencyMultiplier;
            child.woodUrgencyMultiplier = RandBool() ? a.woodUrgencyMultiplier : b.woodUrgencyMultiplier;
            child.maxGatherersPerResource = RandBool() ? a.maxGatherersPerResource : b.maxGatherersPerResource;
            child.armyDeployMinGarrison = RandBool() ? a.armyDeployMinGarrison : b.armyDeployMinGarrison;

            child.upgradePriorityCityCenter = RandBool() ? a.upgradePriorityCityCenter : b.upgradePriorityCityCenter;
            child.upgradePriorityMilitary = RandBool() ? a.upgradePriorityMilitary : b.upgradePriorityMilitary;
            child.upgradePriorityFarm = RandBool() ? a.upgradePriorityFarm : b.upgradePriorityFarm;
            child.upgradePriorityBlacksmith = RandBool() ? a.upgradePriorityBlacksmith : b.upgradePriorityBlacksmith;
            child.upgradePriorityWarehouse = RandBool() ? a.upgradePriorityWarehouse : b.upgradePriorityWarehouse;
            child.upgradePriorityLibrary = RandBool() ? a.upgradePriorityLibrary : b.upgradePriorityLibrary;

            child.counterCavalryThreshold = RandBool() ? a.counterCavalryThreshold : b.counterCavalryThreshold;
            child.counterRangedThreshold = RandBool() ? a.counterRangedThreshold : b.counterRangedThreshold;
            child.counterInfantryThreshold = RandBool() ? a.counterInfantryThreshold : b.counterInfantryThreshold;
            child.retreatUnitThreshold = RandBool() ? a.retreatUnitThreshold : b.retreatUnitThreshold;
            child.retreatDistanceFromBase = RandBool() ? a.retreatDistanceFromBase : b.retreatDistanceFromBase;

            child.maxTowers = RandBool() ? a.maxTowers : b.maxTowers;
            child.maxForts = RandBool() ? a.maxForts : b.maxForts;
            child.minThreatForDefense = RandBool() ? a.minThreatForDefense : b.minThreatForDefense;
            child.peacetimeDefenseWood = RandBool() ? a.peacetimeDefenseWood : b.peacetimeDefenseWood;
            child.peacetimeDefenseStone = RandBool() ? a.peacetimeDefenseStone : b.peacetimeDefenseStone;
            child.maxEntrenchedLow = RandBool() ? a.maxEntrenchedLow : b.maxEntrenchedLow;
            child.maxEntrenchedHigh = RandBool() ? a.maxEntrenchedHigh : b.maxEntrenchedHigh;

            child.baseArmyScore = RandBool() ? a.baseArmyScore : b.baseArmyScore;
            child.smallArmyBonus = RandBool() ? a.smallArmyBonus : b.smallArmyBonus;
            child.entrenchedPenalty = RandBool() ? a.entrenchedPenalty : b.entrenchedPenalty;
            child.cityCenterValue = RandBool() ? a.cityCenterValue : b.cityCenterValue;

            return child;
        }

        // ================================================================
        // Mutation
        // ================================================================

        private AIGenome Mutate(AIGenome genome, double rate, double magnitude)
        {
            var g = genome; // already a clone (reference to the object we're mutating)

            // Intervals: multiply by (1 +/- magnitude), clamp [0.5, 60.0]
            g.economicBuildInterval = MutateInterval(g.economicBuildInterval, rate, magnitude);
            g.militaryTrainInterval = MutateInterval(g.militaryTrainInterval, rate, magnitude);
            g.scoutInterval = MutateInterval(g.scoutInterval, rate, magnitude);
            g.campBuildInterval = MutateInterval(g.campBuildInterval, rate, magnitude);
            g.defenseBuildInterval = MutateInterval(g.defenseBuildInterval, rate, magnitude);
            g.garrisonCheckInterval = MutateInterval(g.garrisonCheckInterval, rate, magnitude);
            g.researchCheckInterval = MutateInterval(g.researchCheckInterval, rate, magnitude);
            g.upgradeCheckInterval = MutateInterval(g.upgradeCheckInterval, rate, magnitude);
            g.unitUpgradeCheckInterval = MutateInterval(g.unitUpgradeCheckInterval, rate, magnitude);
            g.entrenchCheckInterval = MutateInterval(g.entrenchCheckInterval, rate, magnitude);
            g.decisionInterval = MutateInterval(g.decisionInterval, rate, magnitude);

            // Thresholds
            g.attackThreshold = MutateThreshold(g.attackThreshold, rate, magnitude, 0.5, 3.0);
            g.alertThreshold = MutateScore(g.alertThreshold, rate, magnitude, 5.0, 50.0);
            g.retreatHealthThreshold = MutateThreshold(g.retreatHealthThreshold, rate, magnitude, 0.05, 0.95);
            g.minWeightedStrengthForAttack = MutateScore(g.minWeightedStrengthForAttack, rate, magnitude, 500, 5000);

            g.farmFoodUrgencyThreshold = MutateThreshold(g.farmFoodUrgencyThreshold, rate, magnitude, 0.1, 1.0);
            g.farmFoodRateThreshold = MutateThreshold(g.farmFoodRateThreshold, rate, magnitude, 0.5, 5.0);
            g.foodUrgencyMultiplier = MutateThreshold(g.foodUrgencyMultiplier, rate, magnitude, 0.5, 2.0);
            g.woodUrgencyMultiplier = MutateThreshold(g.woodUrgencyMultiplier, rate, magnitude, 0.5, 2.0);

            g.counterCavalryThreshold = MutateThreshold(g.counterCavalryThreshold, rate, magnitude, 0.1, 0.8);
            g.counterRangedThreshold = MutateThreshold(g.counterRangedThreshold, rate, magnitude, 0.1, 0.8);
            g.counterInfantryThreshold = MutateThreshold(g.counterInfantryThreshold, rate, magnitude, 0.1, 0.8);
            g.minThreatForDefense = MutateScore(g.minThreatForDefense, rate, magnitude, 5.0, 50.0);

            // Counts: add +/- 2, clamp to range
            g.maxVillagers = MutateCount(g.maxVillagers, rate, 5, 30);
            g.villagerDeployThreshold = MutateCount(g.villagerDeployThreshold, rate, 1, 10);
            g.maxGatherersPerResource = MutateCount(g.maxGatherersPerResource, rate, 1, 5);
            g.armyDeployMinGarrison = MutateCount(g.armyDeployMinGarrison, rate, 2, 15);
            g.retreatUnitThreshold = MutateCount(g.retreatUnitThreshold, rate, 1, 15);
            g.retreatDistanceFromBase = MutateCount(g.retreatDistanceFromBase, rate, 1, 8);
            g.maxTowers = MutateCount(g.maxTowers, rate, 0, 8);
            g.maxForts = MutateCount(g.maxForts, rate, 0, 6);
            g.maxEntrenchedLow = MutateCount(g.maxEntrenchedLow, rate, 0, 6);
            g.maxEntrenchedHigh = MutateCount(g.maxEntrenchedHigh, rate, 1, 8);
            g.peacetimeDefenseWood = MutateCount(g.peacetimeDefenseWood, rate, 100, 1500);
            g.peacetimeDefenseStone = MutateCount(g.peacetimeDefenseStone, rate, 100, 1500);

            // Scores: multiply by (1 +/- magnitude), clamp to range
            g.upgradePriorityCityCenter = MutateScore(g.upgradePriorityCityCenter, rate, magnitude, 0, 200);
            g.upgradePriorityMilitary = MutateScore(g.upgradePriorityMilitary, rate, magnitude, 0, 200);
            g.upgradePriorityFarm = MutateScore(g.upgradePriorityFarm, rate, magnitude, 0, 200);
            g.upgradePriorityBlacksmith = MutateScore(g.upgradePriorityBlacksmith, rate, magnitude, 0, 200);
            g.upgradePriorityWarehouse = MutateScore(g.upgradePriorityWarehouse, rate, magnitude, 0, 200);
            g.upgradePriorityLibrary = MutateScore(g.upgradePriorityLibrary, rate, magnitude, 0, 200);

            g.baseArmyScore = MutateScore(g.baseArmyScore, rate, magnitude, 10, 200);
            g.smallArmyBonus = MutateScore(g.smallArmyBonus, rate, magnitude, 0, 100);
            g.entrenchedPenalty = MutateScore(g.entrenchedPenalty, rate, magnitude, 0, 100);
            g.cityCenterValue = MutateScore(g.cityCenterValue, rate, magnitude, 20, 200);

            return g;
        }

        // ================================================================
        // Mutation Helpers
        // ================================================================

        private double MutateInterval(double value, double rate, double magnitude)
        {
            double roll;
            lock (rng) { roll = rng.NextDouble(); }
            if (roll >= rate) return value;
            double factor;
            lock (rng) { factor = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * magnitude; }
            return Math.Max(0.5, Math.Min(60.0, value * factor));
        }

        private double MutateThreshold(double value, double rate, double magnitude, double min, double max)
        {
            double roll;
            lock (rng) { roll = rng.NextDouble(); }
            if (roll >= rate) return value;
            double delta;
            lock (rng) { delta = (rng.NextDouble() * 2.0 - 1.0) * magnitude * 0.5; }
            return Math.Max(min, Math.Min(max, value + delta));
        }

        private double MutateScore(double value, double rate, double magnitude, double min, double max)
        {
            double roll;
            lock (rng) { roll = rng.NextDouble(); }
            if (roll >= rate) return value;
            double factor;
            lock (rng) { factor = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * magnitude; }
            return Math.Max(min, Math.Min(max, value * factor));
        }

        private int MutateCount(int value, double rate, int min, int max)
        {
            double roll;
            lock (rng) { roll = rng.NextDouble(); }
            if (roll >= rate) return value;
            int delta;
            lock (rng) { delta = rng.Next(-2, 3); } // -2 to 2 inclusive
            return Math.Max(min, Math.Min(max, value + delta));
        }

        // ================================================================
        // Helpers
        // ================================================================

        private bool RandBool()
        {
            lock (rng) { return rng.Next(2) == 1; }
        }

        /// <summary>
        /// Deep-clone an AIGenome (critical: AIGenome is a class/reference type in C#,
        /// while Swift's was a struct/value type, so we must explicitly copy all fields).
        /// </summary>
        public static AIGenome CloneGenome(AIGenome source)
        {
            var clone = new AIGenome();

            // Metadata
            clone.id = source.id;
            clone.name = source.name;
            clone.savedDate = source.savedDate;
            clone.generation = source.generation;
            clone.fitness = source.fitness;
            clone.wins = source.wins;
            clone.gamesPlayed = source.gamesPlayed;
            clone.mapType = source.mapType;
            if (source.parentIDs != null)
            {
                clone.parentIDs = new string[source.parentIDs.Length];
                Array.Copy(source.parentIDs, clone.parentIDs, source.parentIDs.Length);
            }

            // Intervals
            clone.economicBuildInterval = source.economicBuildInterval;
            clone.militaryTrainInterval = source.militaryTrainInterval;
            clone.scoutInterval = source.scoutInterval;
            clone.campBuildInterval = source.campBuildInterval;
            clone.defenseBuildInterval = source.defenseBuildInterval;
            clone.garrisonCheckInterval = source.garrisonCheckInterval;
            clone.researchCheckInterval = source.researchCheckInterval;
            clone.upgradeCheckInterval = source.upgradeCheckInterval;
            clone.unitUpgradeCheckInterval = source.unitUpgradeCheckInterval;
            clone.entrenchCheckInterval = source.entrenchCheckInterval;

            // Aggressiveness
            clone.decisionInterval = source.decisionInterval;
            clone.attackThreshold = source.attackThreshold;
            clone.alertThreshold = source.alertThreshold;
            clone.retreatHealthThreshold = source.retreatHealthThreshold;
            clone.minWeightedStrengthForAttack = source.minWeightedStrengthForAttack;

            // Economy
            clone.maxVillagers = source.maxVillagers;
            clone.farmFoodUrgencyThreshold = source.farmFoodUrgencyThreshold;
            clone.farmFoodRateThreshold = source.farmFoodRateThreshold;
            clone.villagerDeployThreshold = source.villagerDeployThreshold;
            clone.foodUrgencyMultiplier = source.foodUrgencyMultiplier;
            clone.woodUrgencyMultiplier = source.woodUrgencyMultiplier;
            clone.maxGatherersPerResource = source.maxGatherersPerResource;
            clone.armyDeployMinGarrison = source.armyDeployMinGarrison;

            // Upgrade priorities
            clone.upgradePriorityCityCenter = source.upgradePriorityCityCenter;
            clone.upgradePriorityMilitary = source.upgradePriorityMilitary;
            clone.upgradePriorityFarm = source.upgradePriorityFarm;
            clone.upgradePriorityBlacksmith = source.upgradePriorityBlacksmith;
            clone.upgradePriorityWarehouse = source.upgradePriorityWarehouse;
            clone.upgradePriorityLibrary = source.upgradePriorityLibrary;

            // Military
            clone.counterCavalryThreshold = source.counterCavalryThreshold;
            clone.counterRangedThreshold = source.counterRangedThreshold;
            clone.counterInfantryThreshold = source.counterInfantryThreshold;
            clone.retreatUnitThreshold = source.retreatUnitThreshold;
            clone.retreatDistanceFromBase = source.retreatDistanceFromBase;

            // Defense
            clone.maxTowers = source.maxTowers;
            clone.maxForts = source.maxForts;
            clone.minThreatForDefense = source.minThreatForDefense;
            clone.peacetimeDefenseWood = source.peacetimeDefenseWood;
            clone.peacetimeDefenseStone = source.peacetimeDefenseStone;
            clone.maxEntrenchedLow = source.maxEntrenchedLow;
            clone.maxEntrenchedHigh = source.maxEntrenchedHigh;

            // Target scoring
            clone.baseArmyScore = source.baseArmyScore;
            clone.smallArmyBonus = source.smallArmyBonus;
            clone.entrenchedPenalty = source.entrenchedPenalty;
            clone.cityCenterValue = source.cityCenterValue;

            return clone;
        }
    }
}
