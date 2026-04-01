// ============================================================================
// FILE: AI/SimulationAIController.cs
// PURPOSE: Genome-driven AI controller for headless simulation (non-singleton)
//          C# port of SimulationAIController.swift
//          Includes SimulationContext, SimGatherCommand, SimHuntCommand,
//          GenomeAIEconomyPlanner, GenomeAIMilitaryPlanner,
//          GenomeAIDefensePlanner, GenomeAIResearchPlanner
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;
using Sporefront.AI.Commands;

namespace Sporefront.AI
{
    // ================================================================
    // Simulation Context (DI container)
    // ================================================================

    /// <summary>
    /// Injected dependencies for simulation AI (replaces GameEngine.Instance references).
    /// </summary>
    public class SimulationContext
    {
        public ResourceEngine resourceEngine { get; private set; }
        public AIGenome genome { get; private set; }

        public SimulationContext(ResourceEngine resourceEngine, AIGenome genome)
        {
            this.resourceEngine = resourceEngine;
            this.genome = genome;
        }
    }

    // ================================================================
    // Simulation AI Controller
    // ================================================================

    /// <summary>
    /// Non-singleton AI controller driven by an AIGenome instead of GameConfig constants.
    /// Used by GameSimulator for headless AI-vs-AI evolution runs.
    /// </summary>
    public class SimulationAIController
    {
        // ================================================================
        // State
        // ================================================================

        public Dictionary<Guid, AIPlayerState> aiPlayers { get; private set; } = new Dictionary<Guid, AIPlayerState>();
        private readonly SimulationContext context;
        private readonly AIGenome genome;

        // ================================================================
        // Planners
        // ================================================================

        private readonly GenomeAIEconomyPlanner economyPlanner;
        private readonly GenomeAIMilitaryPlanner militaryPlanner;
        private readonly GenomeAIDefensePlanner defensePlanner;
        private readonly GenomeAIResearchPlanner researchPlanner;

        // ================================================================
        // Init
        // ================================================================

        public SimulationAIController(SimulationContext context)
        {
            this.context = context;
            this.genome = context.genome;
            this.economyPlanner = new GenomeAIEconomyPlanner(context.genome, context.resourceEngine);
            this.militaryPlanner = new GenomeAIMilitaryPlanner(context.genome);
            this.defensePlanner = new GenomeAIDefensePlanner(context.genome);
            this.researchPlanner = new GenomeAIResearchPlanner(context.genome);
        }

        // ================================================================
        // Setup
        // ================================================================

        public void Setup(GameState gameState)
        {
            aiPlayers.Clear();
            foreach (var player in gameState.GetAIPlayers())
            {
                aiPlayers[player.id] = new AIPlayerState(player.id);
            }
        }

        public void RegisterAIPlayer(Guid playerID)
        {
            aiPlayers[playerID] = new AIPlayerState(playerID);
        }

        public void ClearAIPlayers()
        {
            aiPlayers.Clear();
        }

        // ================================================================
        // Hunt Arrival Processing
        // ================================================================

        private void ProcessHuntArrivals(GameState state)
        {
            AIHelper.ProcessHuntArrivals(state, context.resourceEngine);
        }

        // ================================================================
        // Main Update
        // ================================================================

        public List<IEngineCommand> Update(double currentTime, GameState gameState)
        {
            // Process hunt arrivals before generating new commands
            ProcessHuntArrivals(gameState);

            var allCommands = new List<IEngineCommand>();

            foreach (var kvp in aiPlayers)
            {
                var playerID = kvp.Key;
                var aiState = kvp.Value;

                var player = gameState.GetPlayer(playerID);
                if (player == null || !player.isAI) continue;

                double timeSinceLastDecision = currentTime - aiState.lastDecisionTime;
                if (timeSinceLastDecision < genome.decisionInterval) continue;

                // Update enemy analysis
                militaryPlanner.UpdateEnemyAnalysis(aiState, gameState, currentTime, aiPlayers);

                // Update state machine
                UpdateState(aiState, gameState, currentTime);

                // Generate commands
                var commands = GenerateCommands(aiState, gameState, currentTime);
                allCommands.AddRange(commands);

                aiState.lastDecisionTime = currentTime;
            }

            return allCommands;
        }

        // ================================================================
        // State Machine
        // ================================================================

        private void UpdateState(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null)
            {
                aiState.currentState = AIState.Retreat;
                return;
            }

            double ourWeightedStrength = gameState.GetWeightedMilitaryStrength(playerID);
            var nearbyEnemies = gameState.GetEnemyArmies(cityCenter.coordinate, 5, playerID);
            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);

            var armies = gameState.GetArmiesForPlayer(playerID);
            bool armyNeedsRetreat = false;
            foreach (var army in armies)
            {
                int distanceFromBase = army.coordinate.Distance(cityCenter.coordinate);
                bool isLocallyOutnumbered = gameState.IsArmyLocallyOutnumbered(army, playerID);
                if (isLocallyOutnumbered && distanceFromBase > genome.retreatDistanceFromBase)
                {
                    armyNeedsRetreat = true;
                    break;
                }
            }

            switch (aiState.currentState)
            {
                case AIState.Peace:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                    }
                    else if (threatLevel > genome.alertThreshold)
                    {
                        aiState.currentState = AIState.Alert;
                    }
                    else if (ShouldAttack(aiState, gameState))
                    {
                        aiState.currentState = AIState.Attack;
                        aiState.attackStateEnteredTime = currentTime;
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                    }
                    break;

                case AIState.Alert:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                    }
                    else if (threatLevel < genome.alertThreshold * 0.5)
                    {
                        aiState.currentState = AIState.Peace;
                    }
                    else if (ShouldAttack(aiState, gameState))
                    {
                        aiState.currentState = AIState.Attack;
                        aiState.attackStateEnteredTime = currentTime;
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                    }
                    break;

                case AIState.Defense:
                    if (nearbyEnemies.Count == 0)
                    {
                        aiState.consecutiveDefenses += 1;
                        if (aiState.consecutiveDefenses > 3)
                        {
                            aiState.currentState = AIState.Attack;
                            aiState.consecutiveDefenses = 0;
                            aiState.attackStateEnteredTime = currentTime;
                            aiState.lastAttackProgressTime = currentTime;
                            aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                        }
                        else
                        {
                            aiState.currentState = AIState.Alert;
                        }
                    }
                    else if (ourWeightedStrength < 500 || armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                    }
                    break;

                case AIState.Attack:
                    // Attack timeout: check if making progress
                    double currentEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                    if (aiState.lastKnownEnemyStrength - currentEnemyStrength >= 100)
                    {
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = currentEnemyStrength;
                    }
                    if (currentTime - aiState.lastAttackProgressTime > genome.attackTimeoutDuration)
                    {
                        aiState.currentState = AIState.Retreat;
                        aiState.persistentAttackTargetID = null;
                        break;
                    }

                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                    }
                    else if (ourWeightedStrength < 1000)
                    {
                        aiState.currentState = AIState.Peace;
                        aiState.persistentAttackTargetID = null;
                    }
                    else if (armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                    }
                    break;

                case AIState.Retreat:
                    if (nearbyEnemies.Count == 0 && ourWeightedStrength > 1500)
                    {
                        aiState.currentState = AIState.Alert;
                        aiState.pendingArmyConvergence = null;
                    }
                    break;
            }
        }

        private bool ShouldAttack(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            double ourWeightedStrength = gameState.GetWeightedMilitaryStrength(playerID);
            if (ourWeightedStrength < genome.minWeightedStrengthForAttack) return false;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return false;

            var enemyArmy = gameState.GetNearestEnemyArmy(cityCenter.coordinate, playerID);
            if (enemyArmy == null)
            {
                var enemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
                return enemyBuildings.Count > 0;
            }

            double enemyWeightedStrength = enemyArmy.GetWeightedStrength();
            double ratio = ourWeightedStrength / Math.Max(1.0, enemyWeightedStrength);
            return ratio >= genome.attackThreshold;
        }

        // ================================================================
        // Command Generation
        // ================================================================

        private List<IEngineCommand> GenerateCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();

            switch (aiState.currentState)
            {
                case AIState.Peace:
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateExpansionCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime, aiPlayers));
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Alert:
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime, aiPlayers));
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Defense:
                    commands.AddRange(militaryPlanner.GenerateDefenseCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime, aiPlayers));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Attack:
                    commands.AddRange(militaryPlanner.GenerateAttackCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Retreat:
                    commands.AddRange(militaryPlanner.GenerateRetreatCommands(aiState, gameState, currentTime));
                    break;
            }

            return commands;
        }

        // ================================================================
        // Unit Upgrade Commands
        // ================================================================

        private List<IEngineCommand> GenerateUnitUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastUnitUpgradeCheckTime, currentTime, genome.unitUpgradeCheckInterval))
                return new List<IEngineCommand>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();

            if (player.IsUnitUpgradeActive()) return new List<IEngineCommand>();

            var buildings = gameState.GetBuildingsForPlayer(playerID);
            var available = new List<UnitUpgradeType>();

            foreach (UnitUpgradeType upgrade in Enum.GetValues(typeof(UnitUpgradeType)))
            {
                if (player.HasCompletedUnitUpgrade(upgrade.ToString())) continue;

                var prereq = upgrade.Prerequisite();
                if (prereq.HasValue)
                {
                    if (!player.HasCompletedUnitUpgrade(prereq.Value.ToString())) continue;
                }

                bool hasBuilding = buildings.Any(b =>
                    b.buildingType == upgrade.RequiredBuildingType() &&
                    b.state == BuildingState.Completed &&
                    b.level >= upgrade.RequiredBuildingLevel());
                if (!hasBuilding) continue;

                available.Add(upgrade);
            }

            if (available.Count == 0) return new List<IEngineCommand>();

            // Score upgrades
            UnitUpgradeType? best = null;
            double bestScore = double.MinValue;
            var armies = gameState.GetArmiesForPlayer(playerID);

            foreach (var upgrade in available)
            {
                double score = (4 - upgrade.Tier()) * 20.0;

                bool hasUnit = armies.Any(a => a.GetUnitCount(upgrade.GetUnitType()) > 0);
                if (hasUnit)
                    score += 30.0;
                else
                    score += 5.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = upgrade;
                }
            }

            if (!best.HasValue) return new List<IEngineCommand>();

            var cost = best.Value.Cost();
            if (!player.CanAfford(cost)) return new List<IEngineCommand>();

            BuildingData building = null;
            foreach (var b in buildings)
            {
                if (b.buildingType == best.Value.RequiredBuildingType() &&
                    b.state == BuildingState.Completed &&
                    b.level >= best.Value.RequiredBuildingLevel())
                {
                    building = b;
                    break;
                }
            }

            if (building == null) return new List<IEngineCommand>();

            return new List<IEngineCommand> { new AIUpgradeUnitCommand(playerID, best.Value, building.id) };
        }
    }

    // ================================================================
    // SimGatherCommand
    // ================================================================

    /// <summary>
    /// Gather command that uses injected ResourceEngine instead of GameEngine.Instance.
    /// CRITICAL PATTERN: Call ResourceEngine.StartGathering() FIRST. If it fails, return failure.
    /// </summary>
    public class SimGatherCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;
        private readonly ResourceEngine resourceEngine;

        public SimGatherCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID, ResourceEngine resourceEngine)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
            this.resourceEngine = resourceEngine;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your villagers");

            if (!(group.currentTask is IdleTask))
                return EngineCommandResult.Failure("Villager is busy");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            if (resource.remainingAmount <= 0)
                return EngineCommandResult.Failure("Resource depleted");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            var resource = state.GetResourcePoint(resourcePointID);
            if (group == null || resource == null)
                return EngineCommandResult.Failure("Not found");

            // CRITICAL: Call StartGathering() FIRST -- it sets task, assignedResourcePointID,
            // and taskTargetCoordinate internally. If it fails (e.g. no camp coverage),
            // villager stays idle.
            bool registered = resourceEngine.StartGathering(
                villagerGroupID, resourcePointID
            );

            if (!registered)
                return EngineCommandResult.Failure("Could not start gathering");

            // Set path if villager is not already at the resource location
            if (!group.coordinate.Equals(resource.coordinate))
            {
                var path = state.mapData.FindPath(
                    group.coordinate, resource.coordinate, PlayerID, state
                );
                if (path != null)
                {
                    group.SetPath(path);
                }
            }

            // Update collection rates for this player
            resourceEngine.UpdateCollectionRates(PlayerID);

            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "gathering",
                targetCoordinate = resource.coordinate
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    // ================================================================
    // SimHuntCommand
    // ================================================================

    /// <summary>
    /// Hunt command that uses injected ResourceEngine instead of GameEngine.Instance.
    /// </summary>
    public class SimHuntCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;

        public SimHuntCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager not available");

            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Villager not available");

            if (!(group.currentTask is IdleTask))
                return EngineCommandResult.Failure("Villager not available");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Target not huntable");

            if (!resource.resourceType.IsHuntable())
                return EngineCommandResult.Failure("Target not huntable");

            if (resource.currentHealth <= 0)
                return EngineCommandResult.Failure("Target not huntable");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Not found");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Not found");

            // Assign hunting task
            group.AssignTask(new HuntingTask(resourcePointID), resource.coordinate, resourcePointID);

            // Set path if not already at the resource location
            if (!group.coordinate.Equals(resource.coordinate))
            {
                var path = state.mapData.FindPath(group.coordinate, resource.coordinate, PlayerID, state);
                if (path != null)
                {
                    group.SetPath(path);
                }
            }

            // Emit task changed state change
            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "hunting",
                targetCoordinate = resource.coordinate
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    // ================================================================
    // GenomeAIEconomyPlanner
    // ================================================================

    /// <summary>
    /// Genome-aware economy planner. Reads all intervals and thresholds from the AIGenome
    /// instead of GameConfig constants.
    /// </summary>
    public class GenomeAIEconomyPlanner
    {
        private readonly AIGenome genome;
        private readonly ResourceEngine resourceEngine;

        public GenomeAIEconomyPlanner(AIGenome genome, ResourceEngine resourceEngine)
        {
            this.genome = genome;
            this.resourceEngine = resourceEngine;
        }

        // ================================================================
        // Economy Commands
        // ================================================================

        public List<IEngineCommand> GenerateEconomyCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            var exploredCount = gameState.GetExploredResourcePoints(playerID).Count;
            var idleVillagerCount = gameState.GetVillagerGroupsForPlayer(playerID)
                .Count(g => g.currentTask is IdleTask && g.currentPath == null);
            DebugLog.Log($"Sim Economy: exploredResources={exploredCount}, idleVillagers={idleVillagerCount}, wood={player.GetResource(ResourceType.Wood)}");

            var villagerCount = gameState.GetVillagerCount(playerID);
            int popCurrent, popCapacity;
            gameState.GetPopulationStats(playerID, out popCurrent, out popCapacity);

            // Train villagers if we have capacity
            if (popCurrent < popCapacity)
            {
                var cmd = TryTrainVillagers(playerID, gameState, currentTime, aiState);
                if (cmd != null) commands.Add(cmd);
            }

            // Deploy garrisoned villagers
            var deployCmd = TryDeployVillagers(playerID, gameState);
            if (deployCmd != null) commands.Add(deployCmd);

            // Build lumber camp ASAP if missing
            if (currentTime - aiState.lastEconomicBuildTime >= genome.economicBuildInterval)
            {
                if (!HasLumberCamp(playerID, gameState))
                {
                    var cmd = TryBuildLumberCamp(playerID, gameState);
                    if (cmd != null)
                    {
                        commands.Add(cmd);
                        aiState.lastEconomicBuildTime = currentTime;
                    }
                }
            }

            // Hunt nearby animals for food (before gathering -- hunting provides early food)
            commands.AddRange(TryHuntAnimal(playerID, gameState));

            // Assign idle villagers to gather resources
            commands.AddRange(TryAssignVillagersToGather(playerID, gameState));

            // Rebalance villagers
            commands.AddRange(TryRebalanceVillagers(playerID, gameState));

            // Weighted candidate system for building construction (randomized build order)
            if (currentTime - aiState.lastEconomicBuildTime >= genome.economicBuildInterval)
            {
                var urgency = AnalyzeResourceNeeds(playerID, gameState);
                double foodUrgency;
                urgency.TryGetValue(ResourceType.Food, out foodUrgency);
                var foodRate = player.GetCollectionRate(ResourceType.Food);

                bool shouldBuildStorage = urgency.Values.Any(u => u < 0.2);

                var buildCandidates = new List<(Func<IEngineCommand> tryBuild, double weight, string name)>();

                // Farm: high weight if food urgency is high
                if (foodUrgency > genome.farmFoodUrgencyThreshold || foodRate < genome.farmFoodRateThreshold)
                    buildCandidates.Add((() => TryBuildFarm(playerID, gameState), 0.8, "Farm"));

                // Military building
                buildCandidates.Add((() => TryBuildMilitaryBuilding(playerID, gameState), 0.5, "Military"));

                // Storage
                if (shouldBuildStorage)
                    buildCandidates.Add((() => TryBuildStorage(playerID, gameState), 0.3, "Storage"));

                // Library
                buildCandidates.Add((() => TryBuildLibrary(playerID, gameState), 0.2, "Library"));

                var buildCmd = PickWeightedCandidate(buildCandidates, aiState.economyRng, genome.economyRandomizationWeight);
                if (buildCmd != null)
                {
                    commands.Add(buildCmd);
                    aiState.lastEconomicBuildTime = currentTime;
                }
            }

            // Build houses if near pop cap (always checked, not randomized)
            bool shouldBuildHouse = popCurrent >= popCapacity - 5 ||
                (aiState.currentState == AIState.Peace &&
                 villagerCount >= 15 &&
                 popCurrent >= popCapacity - 10 &&
                 player.GetResource(ResourceType.Wood) > 200 &&
                 player.GetResource(ResourceType.Stone) > 150);

            if (shouldBuildHouse)
            {
                var cmd = TryBuildHouse(playerID, gameState, currentTime, aiState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastEconomicBuildTime = currentTime;
                }
            }

            return commands;
        }

        // ================================================================
        // Expansion Commands
        // ================================================================

        public List<IEngineCommand> GenerateExpansionCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();

            if (currentTime - aiState.lastCampBuildTime >= genome.campBuildInterval)
            {
                var cmd = TryBuildResourceCamp(aiState, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastCampBuildTime = currentTime;
                }
            }

            if (currentTime - aiState.lastScoutTime >= genome.scoutInterval)
            {
                var cmd = TryScoutUnexploredArea(aiState, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastScoutTime = currentTime;
                }
            }

            return commands;
        }

        // ================================================================
        // Building Upgrade Commands
        // ================================================================

        public List<IEngineCommand> GenerateUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastUpgradeCheckTime, currentTime, genome.upgradeCheckInterval))
                return new List<IEngineCommand>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();

            var buildings = gameState.GetBuildingsForPlayer(playerID);

            // Don't upgrade if already upgrading something
            if (buildings.Any(b => b.state == BuildingState.Upgrading)) return new List<IEngineCommand>();

            // Don't upgrade if wood reserves are too low
            if (player.GetResource(ResourceType.Wood) < 300) return new List<IEngineCommand>();

            // Don't upgrade CC until we have a lumber camp
            bool hasLumber = gameState.HasBuilding(playerID, BuildingType.LumberCamp, operationalOnly: true);
            if (!hasLumber) return new List<IEngineCommand>();

            // Score and pick the best building to upgrade
            var candidates = new List<(BuildingData building, double score)>();

            foreach (var building in buildings)
            {
                if (!building.CanUpgrade) continue;
                var cost = building.GetUpgradeCost();
                if (cost == null) continue;

                if (!player.CanAfford(cost)) continue;

                double score = 0.0;
                switch (building.buildingType)
                {
                    case BuildingType.CityCenter: score = genome.upgradePriorityCityCenter; break;
                    case BuildingType.Barracks:
                    case BuildingType.ArcheryRange:
                    case BuildingType.Stable:
                    case BuildingType.SiegeWorkshop: score = genome.upgradePriorityMilitary; break;
                    case BuildingType.Farm: score = genome.upgradePriorityFarm; break;
                    case BuildingType.Blacksmith: score = genome.upgradePriorityBlacksmith; break;
                    case BuildingType.Warehouse: score = genome.upgradePriorityWarehouse; break;
                    case BuildingType.Library: score = genome.upgradePriorityLibrary; break;
                    default: score = 10.0; break;
                }

                score += (6 - building.level) * 5.0;
                candidates.Add((building, score));
            }

            if (candidates.Count == 0) return new List<IEngineCommand>();

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var best = candidates[0];

            return new List<IEngineCommand> { new AIUpgradeBuildingCommand(playerID, best.building.id) };
        }

        // ================================================================
        // Resource Analysis
        // ================================================================

        public Dictionary<ResourceType, double> AnalyzeResourceNeeds(Guid playerID, GameState gameState)
        {
            var urgency = new Dictionary<ResourceType, double>();
            var player = gameState.GetPlayer(playerID);
            if (player == null) return urgency;

            var resourceTypes = new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

            foreach (var resourceType in resourceTypes)
            {
                double current = player.GetResource(resourceType);
                double rate = player.GetCollectionRate(resourceType);
                double capacity = gameState.GetStorageCapacity(playerID, resourceType);

                double score = 1.0 - (current / Math.Max(1.0, capacity));

                if (current < 100) score += 0.5;
                if (current >= capacity - 50) score = 0.1;

                if (resourceType == ResourceType.Food) score *= genome.foodUrgencyMultiplier;

                if (resourceType == ResourceType.Wood)
                {
                    int buildingCount = gameState.GetBuildingsForPlayer(playerID).Count;
                    if (buildingCount < 10) score *= genome.woodUrgencyMultiplier;
                }

                if (rate < 0.1 && score > 0.2) score += 0.1;

                urgency[resourceType] = Math.Max(0.0, Math.Min(2.0, score));
            }

            return urgency;
        }

        // ================================================================
        // Private Helpers
        // ================================================================

        private IEngineCommand TryTrainVillagers(Guid playerID, GameState gameState, double currentTime, AIPlayerState aiState)
        {
            if (currentTime - aiState.lastVillagerTrainTime < genome.militaryTrainInterval) return null;

            var cityCenters = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => b.buildingType == BuildingType.CityCenter && b.IsOperational && b.villagerTrainingQueue.Count == 0)
                .ToList();

            if (cityCenters.Count == 0) return null;
            var cityCenter = cityCenters[0];

            var player = gameState.GetPlayer(playerID);
            if (player == null || !player.HasResource(ResourceType.Food, 50)) return null;

            aiState.lastVillagerTrainTime = currentTime;
            return new AITrainVillagerCommand(playerID, cityCenter.id, 1);
        }

        private IEngineCommand TryDeployVillagers(Guid playerID, GameState gameState)
        {
            var buildings = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => b.IsOperational && b.villagerGarrison >= genome.villagerDeployThreshold)
                .ToList();

            if (buildings.Count == 0) return null;
            var building = buildings[0];

            return new AIDeployVillagersCommand(playerID, building.id, building.villagerGarrison);
        }

        private List<IEngineCommand> TryAssignVillagersToGather(Guid playerID, GameState gameState)
        {
            var commands = new List<IEngineCommand>();

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleVillagers = gameState.GetVillagerGroupsForPlayer(playerID)
                .Where(g => g.currentTask is IdleTask && g.currentPath == null)
                .ToList();

            if (idleVillagers.Count == 0) return commands;

            var urgency = AnalyzeResourceNeeds(playerID, gameState);

            var exploredResources = gameState.GetExploredResourcePoints(playerID);
            var nearbyResources = exploredResources
                .Where(r => r.coordinate.Distance(cityCenter.coordinate) <= 8 &&
                            r.remainingAmount > 0 &&
                            r.resourceType.IsGatherable() &&
                            HasResourceCampCoverage(r, gameState, playerID))
                .OrderByDescending(r =>
                {
                    double u;
                    urgency.TryGetValue(r.resourceType.ResourceYield(), out u);
                    return u;
                })
                .ThenBy(r => r.coordinate.Distance(cityCenter.coordinate));

            var assignedResources = new HashSet<Guid>();
            foreach (var villagerGroup in idleVillagers)
            {
                foreach (var resource in nearbyResources)
                {
                    if (assignedResources.Contains(resource.id)) continue;

                    int existingGatherers = resource.assignedVillagerGroupIDs != null ? resource.assignedVillagerGroupIDs.Count : 0;
                    if (existingGatherers >= genome.maxGatherersPerResource) continue;

                    double resourceUrgency;
                    urgency.TryGetValue(resource.resourceType.ResourceYield(), out resourceUrgency);
                    if (resourceUrgency < 0.15) continue;

                    commands.Add(new SimGatherCommand(playerID, villagerGroup.id, resource.id, resourceEngine));
                    assignedResources.Add(resource.id);
                    break;
                }
            }

            return commands;
        }

        private List<IEngineCommand> TryRebalanceVillagers(Guid playerID, GameState gameState)
        {
            var commands = new List<IEngineCommand>();

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var urgency = AnalyzeResourceNeeds(playerID, gameState);

            var overStaffedGroups = new List<(VillagerGroupData group, ResourcePointData resource)>();
            var underStaffedResources = new List<ResourcePointData>();

            foreach (var group in gameState.GetVillagerGroupsForPlayer(playerID))
            {
                var gatherTask = group.currentTask as GatheringResourceTask;
                if (gatherTask == null) continue;

                var resource = gameState.GetResourcePoint(gatherTask.ResourcePointID);
                if (resource == null) continue;

                var resourceType = resource.resourceType.ResourceYield();
                double resourceUrgency;
                urgency.TryGetValue(resourceType, out resourceUrgency);

                int assignedCount = resource.assignedVillagerGroupIDs != null ? resource.assignedVillagerGroupIDs.Count : 0;
                if (resourceUrgency < 0.2 && assignedCount >= genome.maxGatherersPerResource)
                {
                    overStaffedGroups.Add((group, resource));
                }
            }

            var exploredResources = gameState.GetExploredResourcePoints(playerID);
            foreach (var resource in exploredResources)
            {
                if (resource.coordinate.Distance(cityCenter.coordinate) > 8) continue;
                if (resource.remainingAmount <= 0 || !resource.resourceType.IsGatherable()) continue;
                if (!HasResourceCampCoverage(resource, gameState, playerID)) continue;

                var resourceType = resource.resourceType.ResourceYield();
                double resourceUrgency;
                urgency.TryGetValue(resourceType, out resourceUrgency);

                int assignedCount = resource.assignedVillagerGroupIDs != null ? resource.assignedVillagerGroupIDs.Count : 0;
                if (resourceUrgency > 0.6 && assignedCount < genome.maxGatherersPerResource)
                {
                    underStaffedResources.Add(resource);
                }
            }

            underStaffedResources.Sort((r1, r2) =>
            {
                double u1, u2;
                urgency.TryGetValue(r1.resourceType.ResourceYield(), out u1);
                urgency.TryGetValue(r2.resourceType.ResourceYield(), out u2);
                return u2.CompareTo(u1);
            });

            foreach (var (group, _) in overStaffedGroups)
            {
                var targetResource = underStaffedResources.FirstOrDefault(r =>
                {
                    int assignedCount = r.assignedVillagerGroupIDs != null ? r.assignedVillagerGroupIDs.Count : 0;
                    return assignedCount < genome.maxGatherersPerResource;
                });
                if (targetResource == null) break;

                commands.Add(new SimGatherCommand(playerID, group.id, targetResource.id, resourceEngine));
            }

            return commands;
        }

        private List<IEngineCommand> TryHuntAnimal(Guid playerID, GameState gameState)
        {
            var commands = new List<IEngineCommand>();
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var huntableAnimals = gameState.GetExploredResourcePoints(playerID)
                .Where(r => r.resourceType.IsHuntable() && r.currentHealth > 0 &&
                            r.coordinate.Distance(cityCenter.coordinate) <= 8)
                .ToList();

            if (huntableAnimals.Count == 0)
            {
                DebugLog.Log($"Sim Hunt: no huntable animals found (explored={gameState.GetExploredResourcePoints(playerID).Count})");
            }

            var idleVillagers = gameState.GetVillagerGroupsForPlayer(playerID)
                .Where(g => g.currentTask is IdleTask && g.currentPath == null);

            var usedVillagers = new HashSet<Guid>();
            foreach (var animal in huntableAnimals)
            {
                var villager = idleVillagers.FirstOrDefault(v => !usedVillagers.Contains(v.id));
                if (villager == null) break;

                commands.Add(new SimHuntCommand(playerID, villager.id, animal.id));
                usedVillagers.Add(villager.id);
                if (commands.Count >= 3) break; // Cap at 3 hunters per cycle
            }

            return commands;
        }

        private bool HasLumberCamp(Guid playerID, GameState gameState)
        {
            return gameState.GetBuildingsForPlayer(playerID).Any(b =>
                b.buildingType == BuildingType.LumberCamp &&
                (b.state == BuildingState.Completed || b.state == BuildingState.Constructing || b.state == BuildingState.Planning));
        }

        private IEngineCommand TryBuildLumberCamp(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            if (!player.CanAfford(BuildingType.LumberCamp.BuildCost())) return null;

            var trees = gameState.GetExploredResourcePoints(playerID)
                .Where(r => r.resourceType == ResourcePointType.Trees && r.remainingAmount > 0 &&
                            r.coordinate.Distance(cityCenter.coordinate) <= 8)
                .OrderBy(r => r.coordinate.Distance(cityCenter.coordinate))
                .ToList();

            if (trees.Count == 0)
            {
                DebugLog.Log("Sim LumberCamp: no explored trees found within 8 tiles");
            }

            if (trees.Count == 0) return null;
            var target = trees[0];

            if (gameState.CanBuildAt(target.coordinate, playerID))
            {
                return new AIBuildCommand(playerID, BuildingType.LumberCamp, target.coordinate, 0);
            }

            foreach (var neighbor in target.coordinate.Neighbors())
            {
                if (gameState.CanBuildAt(neighbor, playerID))
                {
                    return new AIBuildCommand(playerID, BuildingType.LumberCamp, neighbor, 0);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a resource has camp coverage (uses GetRequiredCampType helper approach).
        /// </summary>
        public bool HasResourceCampCoverage(ResourcePointData resource, GameState gameState, Guid playerID)
        {
            if (!resource.resourceType.RequiresCamp()) return true;

            var requiredCampType = GetRequiredCampType(resource.resourceType);
            if (!requiredCampType.HasValue) return true;

            var tilesToCheck = new List<HexCoordinate> { resource.coordinate };
            tilesToCheck.AddRange(resource.coordinate.Neighbors());

            foreach (var coord in tilesToCheck)
            {
                var building = gameState.GetBuilding(coord);
                if (building != null &&
                    building.buildingType == requiredCampType.Value &&
                    building.ownerID.HasValue && building.ownerID.Value == playerID &&
                    building.IsOperational)
                {
                    return true;
                }
            }

            return false;
        }

        private static BuildingType? GetRequiredCampType(ResourcePointType resourceType)
        {
            switch (resourceType)
            {
                case ResourcePointType.Trees: return BuildingType.LumberCamp;
                case ResourcePointType.OreMine:
                case ResourcePointType.StoneQuarry: return BuildingType.MiningCamp;
                default: return null;
            }
        }

        /// <summary>
        /// Generic building construction helper for simulation AI. Validates player/CC,
        /// checks optional preconditions, affordability, and finds a build location.
        /// </summary>
        private IEngineCommand TryBuildStructure(Guid playerID, GameState gameState, BuildingType type, int searchRadius,
            bool requiresCCLevel = false, bool singleton = false, int? maxAllowed = null)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            if (requiresCCLevel && cityCenter.level < type.RequiredCityCenterLevel()) return null;
            if (singleton && gameState.HasBuilding(playerID, type)) return null;
            if (maxAllowed.HasValue && gameState.GetBuildingCount(type, playerID) >= maxAllowed.Value) return null;

            if (!player.CanAfford(type.BuildCost())) return null;

            var location = gameState.FindBuildLocation(cityCenter.coordinate, searchRadius, playerID);
            if (!location.HasValue) return null;

            return new AIBuildCommand(playerID, type, location.Value, 0);
        }

        private IEngineCommand TryBuildFarm(Guid playerID, GameState gameState)
            => TryBuildStructure(playerID, gameState, BuildingType.Farm, 4);

        private IEngineCommand TryBuildHouse(Guid playerID, GameState gameState, double currentTime, AIPlayerState aiState)
            => TryBuildStructure(playerID, gameState, BuildingType.Neighborhood, 5);

        private IEngineCommand TryBuildStorage(Guid playerID, GameState gameState)
        {
            int ccLevel = gameState.GetCityCenter(playerID)?.level ?? 0;
            return TryBuildStructure(playerID, gameState, BuildingType.Warehouse, 5,
                maxAllowed: BuildingTypeExtensions.MaxWarehousesAllowed(ccLevel));
        }

        private IEngineCommand TryBuildLibrary(Guid playerID, GameState gameState)
            => TryBuildStructure(playerID, gameState, BuildingType.Library, 4, requiresCCLevel: true, singleton: true);

        private IEngineCommand TryBuildMilitaryBuilding(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            int ccLevel = cityCenter.level;
            var buildings = gameState.GetBuildingsForPlayer(playerID);

            var priorities = new[]
            {
                (type: BuildingType.Barracks, minCount: 0, maxCount: 1),
                (type: BuildingType.ArcheryRange, minCount: 0, maxCount: 1),
                (type: BuildingType.Stable, minCount: 0, maxCount: 1),
                (type: BuildingType.Barracks, minCount: 1, maxCount: 2),
                (type: BuildingType.SiegeWorkshop, minCount: 0, maxCount: 1),
            };

            foreach (var priority in priorities)
            {
                if (ccLevel < priority.type.RequiredCityCenterLevel()) continue;

                int existingCount = buildings.Count(b =>
                    b.buildingType == priority.type &&
                    (b.state == BuildingState.Completed || b.state == BuildingState.Constructing));

                if (existingCount < priority.minCount || existingCount >= priority.maxCount) continue;

                if (!player.CanAfford(priority.type.BuildCost())) continue;

                var location = gameState.FindBuildLocation(cityCenter.coordinate, 5, playerID);
                if (!location.HasValue) continue;

                return new AIBuildCommand(playerID, priority.type, location.Value, 0);
            }

            return null;
        }

        private IEngineCommand TryBuildResourceCamp(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            var urgency = AnalyzeResourceNeeds(playerID, gameState);
            var exploredResources = gameState.GetExploredResourcePoints(playerID);
            int lumberCampCount = gameState.GetBuildingCount(BuildingType.LumberCamp, playerID);
            int miningCampCount = gameState.GetBuildingCount(BuildingType.MiningCamp, playerID);

            var candidates = new List<(ResourcePointData resource, BuildingType campType, double score)>();

            foreach (var resource in exploredResources)
            {
                if (resource.remainingAmount <= 0) continue;
                if (!resource.resourceType.RequiresCamp()) continue;

                int distance = Math.Max(1, resource.coordinate.Distance(cityCenter.coordinate));
                if (distance > 10) continue;

                BuildingType campType;
                switch (resource.resourceType)
                {
                    case ResourcePointType.Trees:
                        if (lumberCampCount >= 3) continue;
                        campType = BuildingType.LumberCamp;
                        break;
                    case ResourcePointType.OreMine:
                    case ResourcePointType.StoneQuarry:
                        if (miningCampCount >= 3) continue;
                        campType = BuildingType.MiningCamp;
                        break;
                    default:
                        continue;
                }

                // Check camp coverage
                bool hasCoverage = false;
                var tilesToCheck = new List<HexCoordinate> { resource.coordinate };
                tilesToCheck.AddRange(resource.coordinate.Neighbors());
                foreach (var coord in tilesToCheck)
                {
                    var building = gameState.GetBuilding(coord);
                    if (building != null &&
                        building.buildingType == campType &&
                        building.ownerID.HasValue && building.ownerID.Value == playerID &&
                        building.IsOperational)
                    {
                        hasCoverage = true;
                        break;
                    }
                }
                if (hasCoverage) continue;

                if (!player.CanAfford(campType.BuildCost())) continue;

                double resourceUrgency;
                urgency.TryGetValue(resource.resourceType.ResourceYield(), out resourceUrgency);
                if (resourceUrgency == 0) resourceUrgency = 0.5;

                double score = resourceUrgency * resource.remainingAmount / (100.0 * distance);
                candidates.Add((resource, campType, score));
            }

            if (candidates.Count == 0) return null;

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var best = candidates[0];

            if (gameState.CanBuildAt(best.resource.coordinate, playerID))
            {
                return new AIBuildCommand(playerID, best.campType, best.resource.coordinate, 0);
            }

            foreach (var neighbor in best.resource.coordinate.Neighbors())
            {
                if (gameState.CanBuildAt(neighbor, playerID))
                {
                    return new AIBuildCommand(playerID, best.campType, neighbor, 0);
                }
            }

            return null;
        }

        private IEngineCommand TryScoutUnexploredArea(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;

            // Don't use generic scouting if we have dedicated Scout units
            // (dedicated scouting is handled by GenomeAIMilitaryPlanner.GenerateScoutingCommands)
            var armies = gameState.GetArmiesForPlayer(playerID);
            bool hasDedicatedScouts = armies.Any(a =>
                a.GetUnitCount(MilitaryUnitType.Scout) > 0 &&
                (double)a.GetUnitCount(MilitaryUnitType.Scout) / Math.Max(1, a.GetTotalUnits()) >= 0.5);
            if (hasDedicatedScouts) return null;

            var scoutTarget = gameState.FindNearestUnexploredCoordinate(cityCenter.coordinate, playerID, 12);
            if (!scoutTarget.HasValue) return null;

            var idleArmies = armies
                .Where(a => !a.isInCombat && a.currentPath == null)
                .ToList();

            if (idleArmies.Count > 0)
            {
                return new AIMoveCommand(playerID, idleArmies[0].id, scoutTarget.Value, true);
            }

            if (aiState.currentState == AIState.Peace)
            {
                var idleVillagers = gameState.GetVillagerGroupsForPlayer(playerID)
                    .Where(g => g.currentTask is IdleTask && g.currentPath == null)
                    .ToList();

                if (idleVillagers.Count > 0)
                {
                    return new AIMoveCommand(playerID, idleVillagers[0].id, scoutTarget.Value, false);
                }
            }

            return null;
        }

        // ================================================================
        // Weighted Build Candidate Selection
        // ================================================================

        private IEngineCommand PickWeightedCandidate(List<(Func<IEngineCommand> tryBuild, double weight, string name)> candidates, System.Random rng, double randomizationWeight)
        {
            if (candidates.Count == 0) return null;

            // Sort by weight descending
            candidates.Sort((a, b) => b.weight.CompareTo(a.weight));

            // (1 - randomizationWeight) chance: pick highest-weight candidate that succeeds
            // randomizationWeight chance: try a random candidate first
            double roll = rng.NextDouble();
            if (roll >= randomizationWeight)
            {
                // Try in weight order
                foreach (var candidate in candidates)
                {
                    var cmd = candidate.tryBuild();
                    if (cmd != null) return cmd;
                }
            }
            else
            {
                // Shuffle and try random order
                var shuffled = candidates.OrderBy(_ => rng.Next());
                foreach (var candidate in shuffled)
                {
                    var cmd = candidate.tryBuild();
                    if (cmd != null) return cmd;
                }
            }

            return null;
        }
    }

    // ================================================================
    // GenomeAIMilitaryPlanner
    // ================================================================

    /// <summary>
    /// Genome-aware military planner. Reads all thresholds and intervals from the AIGenome.
    /// </summary>
    public class GenomeAIMilitaryPlanner
    {
        private readonly AIGenome genome;

        public GenomeAIMilitaryPlanner(AIGenome genome)
        {
            this.genome = genome;
        }

        // ================================================================
        // Military Training Commands
        // ================================================================

        public List<IEngineCommand> GenerateMilitaryCommands(AIPlayerState aiState, GameState gameState, double currentTime, Dictionary<Guid, AIPlayerState> aiPlayers)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            // Training: gated by military train timer
            if (currentTime - aiState.lastMilitaryTrainTime >= genome.militaryTrainInterval)
            {
                var militaryBuildingTypes = new HashSet<BuildingType>
                {
                    BuildingType.Barracks, BuildingType.ArcheryRange,
                    BuildingType.Stable, BuildingType.SiegeWorkshop
                };

                var militaryBuildings = gameState.GetBuildingsForPlayer(playerID)
                    .Where(b => militaryBuildingTypes.Contains(b.buildingType) && b.IsOperational && b.trainingQueue.Count == 0);

                bool trainedThisCycle = false;
                foreach (var building in militaryBuildings)
                {
                    var cmd = TryTrainMilitary(playerID, building.id, gameState, aiPlayers);
                    if (cmd != null)
                    {
                        commands.Add(cmd);
                        trainedThisCycle = true;
                    }
                }

                if (trainedThisCycle)
                    aiState.lastMilitaryTrainTime = currentTime;
            }

            // Deployment: always checked, not gated by training timer
            var deployCmd = TryDeployArmy(playerID, gameState);
            if (deployCmd != null) commands.Add(deployCmd);

            return commands;
        }

        private IEngineCommand TryTrainMilitary(Guid playerID, Guid buildingID, GameState gameState, Dictionary<Guid, AIPlayerState> aiPlayers)
        {
            var building = gameState.GetBuilding(buildingID);
            if (building == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            AIPlayerState aiState;
            aiPlayers.TryGetValue(playerID, out aiState);

            var enemyAnalysis = aiState?.lastEnemyAnalysis;
            if (!enemyAnalysis.HasValue)
            {
                var analysis = gameState.AnalyzeEnemyComposition(playerID);
                if (analysis.HasValue)
                {
                    var a = analysis.Value;
                    enemyAnalysis = new EnemyCompositionAnalysis
                    {
                        cavalryRatio = a.cavalryRatio,
                        rangedRatio = a.rangedRatio,
                        infantryRatio = a.infantryRatio,
                        siegeRatio = a.siegeRatio,
                        totalStrength = a.totalStrength,
                        weightedStrength = a.weightedStrength
                    };
                }
            }

            var trainableUnits = GetTrainableUnitsForBuilding(building.buildingType);
            if (trainableUnits.Count == 0) return null;

            var ownComposition = CalculateOwnComposition(playerID, gameState);
            double gameTime = gameState.currentTime;

            // Score each trainable unit and pick the best
            MilitaryUnitType? bestUnit = null;
            double bestScore = double.MinValue;

            foreach (var unitType in trainableUnits)
            {
                if (!player.CanAfford(unitType.TrainingCost())) continue;

                double score = ScoreUnitTraining(unitType, enemyAnalysis, ownComposition, player, gameState, playerID, gameTime);

                // Small hash-based tiebreaker
                score += (unitType.GetHashCode() & 0xFF) * 0.01;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestUnit = unitType;
                }
            }

            if (!bestUnit.HasValue) return null;
            return new AITrainMilitaryCommand(playerID, buildingID, bestUnit.Value, 1);
        }

        private List<MilitaryUnitType> GetTrainableUnitsForBuilding(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Barracks:
                    return new List<MilitaryUnitType> { MilitaryUnitType.Swordsman, MilitaryUnitType.Pikeman };
                case BuildingType.ArcheryRange:
                    return new List<MilitaryUnitType> { MilitaryUnitType.Archer, MilitaryUnitType.Crossbow };
                case BuildingType.Stable:
                    return new List<MilitaryUnitType> { MilitaryUnitType.Scout, MilitaryUnitType.Knight, MilitaryUnitType.HeavyCavalry };
                case BuildingType.SiegeWorkshop:
                    return new List<MilitaryUnitType> { MilitaryUnitType.Mangonel, MilitaryUnitType.Trebuchet };
                default:
                    return new List<MilitaryUnitType>();
            }
        }

        private Dictionary<UnitCategory, double> CalculateOwnComposition(Guid playerID, GameState gameState)
        {
            var result = new Dictionary<UnitCategory, double>
            {
                { UnitCategory.Infantry, 0 }, { UnitCategory.Ranged, 0 },
                { UnitCategory.Cavalry, 0 }, { UnitCategory.Siege, 0 }
            };

            int totalUnits = 0;
            foreach (var army in gameState.GetArmiesForPlayer(playerID))
            {
                var ratios = army.GetCategoryRatios();
                int count = army.GetTotalUnits();
                result[UnitCategory.Infantry] += ratios.infantry * count;
                result[UnitCategory.Ranged] += ratios.ranged * count;
                result[UnitCategory.Cavalry] += ratios.cavalry * count;
                result[UnitCategory.Siege] += ratios.siege * count;
                totalUnits += count;
            }

            if (totalUnits > 0)
            {
                result[UnitCategory.Infantry] /= totalUnits;
                result[UnitCategory.Ranged] /= totalUnits;
                result[UnitCategory.Cavalry] /= totalUnits;
                result[UnitCategory.Siege] /= totalUnits;
            }

            return result;
        }

        private double ScoreUnitTraining(MilitaryUnitType unitType, EnemyCompositionAnalysis? enemyAnalysis,
            Dictionary<UnitCategory, double> ownComposition, PlayerState player, GameState gameState,
            Guid playerID, double gameTime)
        {
            double score = 0;
            var category = unitType.Category();

            // Counter bonus: does this unit's category counter the enemy's dominant type?
            if (enemyAnalysis.HasValue)
            {
                var enemy = enemyAnalysis.Value;
                if (category == UnitCategory.Infantry && enemy.cavalryRatio > 0.3) score += 20.0;
                if (category == UnitCategory.Ranged && enemy.infantryRatio > 0.3) score += 20.0;
                if (category == UnitCategory.Cavalry && enemy.rangedRatio > 0.3) score += 20.0;
                if (unitType == MilitaryUnitType.Pikeman && enemy.cavalryRatio > 0.25) score += 10.0;
            }

            // Diversity penalty/bonus (using genome params)
            double ownRatio = 0;
            ownComposition.TryGetValue(category, out ownRatio);
            if (ownRatio > genome.compositionDiversityThreshold) score -= genome.compositionDiversityBonus;
            else if (ownRatio < 0.15) score += genome.compositionDiversityBonus * 0.67;

            // Tech bonus: reward units we have research bonuses for
            double techBonus = DamageCalculator.GetResearchDamageBonus(unitType, player);
            score += Math.Min(genome.compositionTechBonus, techBonus * 5.0);

            // Timing bonus
            bool earlyGame = gameTime < genome.compositionTimingEarlyThreshold;
            if (earlyGame)
            {
                if (unitType == MilitaryUnitType.Swordsman || unitType == MilitaryUnitType.Archer || unitType == MilitaryUnitType.Pikeman)
                    score += 8.0;
            }
            else
            {
                if (unitType == MilitaryUnitType.Knight || unitType == MilitaryUnitType.HeavyCavalry ||
                    unitType == MilitaryUnitType.Crossbow || unitType == MilitaryUnitType.Trebuchet)
                    score += 8.0;
            }

            // Scout priority: encourage scouts early if under cap
            if (unitType == MilitaryUnitType.Scout)
            {
                int scoutCount = CountScoutArmies(playerID, gameState);
                if (scoutCount < genome.maxScouts && earlyGame)
                    score += 25.0;
                else if (scoutCount >= genome.maxScouts)
                    score -= 30.0;
            }

            return score;
        }

        private int CountScoutArmies(Guid playerID, GameState gameState)
        {
            int count = 0;
            foreach (var army in gameState.GetArmiesForPlayer(playerID))
            {
                if (IsScoutArmy(army)) count++;
            }
            return count;
        }

        private bool IsScoutArmy(ArmyData army)
        {
            int totalUnits = army.GetTotalUnits();
            if (totalUnits == 0) return false;
            int scoutCount = army.GetUnitCount(MilitaryUnitType.Scout);
            return scoutCount > 0 && (double)scoutCount / totalUnits >= 0.5;
        }

        private IEngineCommand TryDeployArmy(Guid playerID, GameState gameState)
        {
            var buildings = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => b.IsOperational && b.GetTotalGarrisonedUnits() >= genome.armyDeployMinGarrison)
                .ToList();

            if (buildings.Count == 0) return null;
            var building = buildings[0];

            var composition = new Dictionary<MilitaryUnitType, int>();
            foreach (var kvp in building.garrison)
            {
                if (kvp.Value > 0) composition[kvp.Key] = kvp.Value;
            }

            if (composition.Count == 0) return null;

            return new AIDeployArmyCommand(playerID, building.id, composition);
        }

        // ================================================================
        // Defense Commands
        // ================================================================

        public List<IEngineCommand> GenerateDefenseCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var nearbyEnemies = gameState.GetEnemyArmies(cityCenter.coordinate, 5, playerID);
            if (nearbyEnemies.Count == 0) return commands;
            var nearestEnemy = nearbyEnemies[0];

            foreach (var army in gameState.GetArmiesForPlayer(playerID))
            {
                if (army.isInCombat || army.currentPath != null) continue;
                commands.Add(new AIMoveCommand(playerID, army.id, nearestEnemy.coordinate, true));
            }

            return commands;
        }

        // ================================================================
        // Attack Commands
        // ================================================================

        public List<IEngineCommand> GenerateAttackCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null)
                .ToList();

            if (idleArmies.Count == 0) return commands;

            // Check if persistent target still exists
            HexCoordinate? targetCoordinate = null;

            if (aiState.persistentAttackTargetID.HasValue)
            {
                var persistentID = aiState.persistentAttackTargetID.Value;
                var targetArmy = gameState.GetArmy(persistentID);
                if (targetArmy != null && targetArmy.GetTotalUnits() > 0)
                {
                    targetCoordinate = targetArmy.coordinate;
                }
                else
                {
                    var targetBuilding = gameState.GetBuilding(persistentID);
                    if (targetBuilding != null && targetBuilding.state != BuildingState.Destroyed)
                    {
                        targetCoordinate = targetBuilding.coordinate;
                    }
                    else
                    {
                        aiState.persistentAttackTargetID = null;
                    }
                }
            }

            // If no persistent target, find a new one
            if (!targetCoordinate.HasValue)
            {
                var targets = ScoreAllTargets(playerID, gameState, cityCenter.coordinate);
                if (targets.Count > 0)
                {
                    targetCoordinate = targets[0].coordinate;
                    aiState.persistentAttackTargetID = targets[0].targetID;
                }
            }

            if (!targetCoordinate.HasValue) return commands;
            var target = targetCoordinate.Value;

            foreach (var army in idleArmies)
            {
                commands.Add(new AIMoveCommand(playerID, army.id, target, true));
            }

            aiState.lastAttackTarget = target;
            return commands;
        }

        // ================================================================
        // Target Scoring
        // ================================================================

        public List<TargetScore> ScoreAllTargets(Guid playerID, GameState gameState, HexCoordinate from)
        {
            var scores = new List<TargetScore>();

            foreach (var army in gameState.armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;

                var status = gameState.GetDiplomacyStatus(playerID, army.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                int distance = Math.Max(1, from.Distance(army.coordinate));

                // Score by full defensive stack strength
                var allDefenders = gameState.GetArmies(army.coordinate)
                    .Where(a => !a.ownerID.HasValue || a.ownerID.Value != playerID).ToList();
                var crossTileEntrenched = gameState.GetEntrenchedArmiesCovering(army.coordinate)
                    .Where(a => !a.ownerID.HasValue || a.ownerID.Value != playerID).ToList();

                int totalStrength = allDefenders.Sum(a => a.GetTotalUnits()) +
                                   crossTileEntrenched.Sum(a => a.GetTotalUnits());

                double score = genome.baseArmyScore - totalStrength + (20.0 / distance);

                if (totalStrength < 10) score += genome.smallArmyBonus;
                if (army.isEntrenched) score -= genome.entrenchedPenalty;
                if (allDefenders.Count > 1) score -= (allDefenders.Count - 1) * 10.0;

                scores.Add(new TargetScore(army.id, army.coordinate, score, false));
            }

            var enemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            foreach (var building in enemyBuildings)
            {
                int distance = Math.Max(1, from.Distance(building.coordinate));

                double baseScore;
                switch (building.buildingType)
                {
                    case BuildingType.CityCenter: baseScore = genome.cityCenterValue; break;
                    case BuildingType.Castle: baseScore = 80.0; break;
                    case BuildingType.Barracks:
                    case BuildingType.ArcheryRange:
                    case BuildingType.Stable: baseScore = 60.0; break;
                    case BuildingType.SiegeWorkshop: baseScore = 55.0; break;
                    case BuildingType.WoodenFort:
                    case BuildingType.Tower: baseScore = 40.0; break;
                    case BuildingType.Farm: baseScore = 20.0; break;
                    default: baseScore = 30.0; break;
                }

                int garrison = building.GetTotalGarrisonedUnits();
                if (garrison > 0) baseScore -= garrison * 2.0;

                double score = baseScore + (15.0 / distance);
                scores.Add(new TargetScore(building.id, building.coordinate, score, true));
            }

            scores.Sort((a, b) => b.score.CompareTo(a.score));
            return scores;
        }

        // ================================================================
        // Retreat Commands
        // ================================================================

        public List<IEngineCommand> GenerateRetreatCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            foreach (var army in gameState.GetArmiesForPlayer(playerID))
            {
                if (army.isInCombat) continue;

                int distanceFromBase = army.coordinate.Distance(cityCenter.coordinate);
                bool isLocallyOutnumbered = gameState.IsArmyLocallyOutnumbered(army, playerID);

                bool shouldRetreat = false;

                if (isLocallyOutnumbered && distanceFromBase > genome.retreatDistanceFromBase)
                    shouldRetreat = true;

                if (army.GetTotalUnits() < genome.retreatUnitThreshold && distanceFromBase > genome.retreatDistanceFromBase)
                    shouldRetreat = true;

                if (aiState.currentState == AIState.Retreat && distanceFromBase > genome.retreatDistanceFromBase)
                    shouldRetreat = true;

                if (shouldRetreat)
                {
                    aiState.persistentAttackTargetID = null;
                    commands.Add(new AIMoveCommand(playerID, army.id, cityCenter.coordinate, true));
                }
            }

            return commands;
        }

        // ================================================================
        // Enemy Analysis
        // ================================================================

        public void UpdateEnemyAnalysis(AIPlayerState aiState, GameState gameState, double currentTime, Dictionary<Guid, AIPlayerState> aiPlayers)
        {
            if (currentTime - aiState.lastEnemyAnalysisTime < 10.0) return;

            var analysis = gameState.AnalyzeEnemyComposition(aiState.playerID);
            if (analysis.HasValue)
            {
                var a = analysis.Value;
                aiState.lastEnemyAnalysis = new EnemyCompositionAnalysis
                {
                    cavalryRatio = a.cavalryRatio,
                    rangedRatio = a.rangedRatio,
                    infantryRatio = a.infantryRatio,
                    siegeRatio = a.siegeRatio,
                    totalStrength = a.totalStrength,
                    weightedStrength = a.weightedStrength
                };
                aiState.lastEnemyAnalysisTime = currentTime;
            }
        }

        // ================================================================
        // Scouting Commands
        // ================================================================

        public List<IEngineCommand> GenerateScoutingCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            // Update known enemy bases from visible buildings
            var visibleEnemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            foreach (var building in visibleEnemyBuildings)
            {
                if (building.buildingType == BuildingType.CityCenter)
                {
                    bool alreadyKnown = false;
                    foreach (var known in aiState.knownEnemyBases)
                    {
                        if (known.Distance(building.coordinate) <= 2) { alreadyKnown = true; break; }
                    }
                    if (!alreadyKnown)
                    {
                        aiState.knownEnemyBases.Add(building.coordinate);
                        aiState.enemyBaseFound = true;
                    }
                }
            }

            // Find scout armies
            var scoutArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => IsScoutArmy(a) && !a.isInCombat && a.currentPath == null);

            foreach (var scout in scoutArmies)
            {
                // Check for nearby enemy armies - retreat if threatened
                var nearbyEnemies = gameState.GetEnemyArmies(scout.coordinate, 3, playerID);
                if (nearbyEnemies.Count > 0)
                {
                    commands.Add(new AIMoveCommand(playerID, scout.id, cityCenter.coordinate, true));
                    continue;
                }

                // Choose scouting target
                HexCoordinate? target = null;

                if (!aiState.enemyBaseFound)
                {
                    // Explore map edges/unexplored areas
                    target = gameState.FindNearestUnexploredCoordinate(scout.coordinate, playerID, 15);
                }
                else if (aiState.knownEnemyBases.Count > 0)
                {
                    // Patrol around known enemy base
                    var enemyBase = aiState.knownEnemyBases[0];
                    var patrolCoords = enemyBase.CoordinatesInRing(genome.scoutPatrolRadius);
                    if (patrolCoords.Count > 0)
                    {
                        int idx = Math.Abs(scout.id.GetHashCode() + (int)(currentTime * 10)) % patrolCoords.Count;
                        target = patrolCoords[idx];
                    }
                }

                if (target.HasValue)
                {
                    commands.Add(new AIMoveCommand(playerID, scout.id, target.Value, true));
                }
            }

            return commands;
        }
    }

    // ================================================================
    // GenomeAIDefensePlanner
    // ================================================================

    /// <summary>
    /// Genome-aware defense planner. Reads all limits and intervals from the AIGenome.
    /// </summary>
    public class GenomeAIDefensePlanner
    {
        private readonly AIGenome genome;

        public GenomeAIDefensePlanner(AIGenome genome)
        {
            this.genome = genome;
        }

        // ================================================================
        // Defensive Building Commands
        // ================================================================

        public List<IEngineCommand> GenerateDefensiveBuildingCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastDefenseBuildTime < genome.defenseBuildInterval) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);

            // Track threat trend for proactive defense
            bool threatIsRising = false;
            if (threatLevel > aiState.previousThreatLevel)
            {
                aiState.threatRisingCount++;
                if (aiState.threatRisingCount >= 2) threatIsRising = true;
            }
            else
            {
                aiState.threatRisingCount = 0;
            }
            aiState.previousThreatLevel = threatLevel;

            int towerCount = gameState.GetBuildingCount(BuildingType.Tower, playerID);
            bool hasBarracks = gameState.HasBuilding(playerID, BuildingType.Barracks, operationalOnly: true);

            bool shouldBuildDefense;
            if (aiState.currentState == AIState.Peace)
            {
                if (threatIsRising)
                {
                    shouldBuildDefense = player.GetResource(ResourceType.Wood) > genome.proactiveDefenseWoodThreshold &&
                                         player.GetResource(ResourceType.Stone) > genome.proactiveDefenseStoneThreshold;
                }
                else if (gameState.currentTime > genome.earlyTowerGameTime && towerCount == 0)
                {
                    shouldBuildDefense = true;
                }
                else if (hasBarracks && towerCount == 0)
                {
                    shouldBuildDefense = true;
                }
                else
                {
                    shouldBuildDefense = player.GetResource(ResourceType.Wood) > genome.peacetimeDefenseWood &&
                                         player.GetResource(ResourceType.Stone) > genome.peacetimeDefenseStone;
                }
            }
            else
            {
                shouldBuildDefense = threatLevel >= genome.minThreatForDefense ||
                                     aiState.currentState == AIState.Defense ||
                                     threatIsRising;
            }

            if (!shouldBuildDefense) return commands;
            int fortCount = gameState.GetBuildingCount(BuildingType.WoodenFort, playerID);

            if (towerCount < genome.maxTowers)
            {
                var cmd = TryBuildDefensiveStructure(BuildingType.Tower, playerID, gameState, cityCenter);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastDefenseBuildTime = currentTime;
                    return commands;
                }
            }

            if (fortCount < genome.maxForts &&
                (aiState.currentState == AIState.Defense || aiState.currentState == AIState.Alert))
            {
                var cmd = TryBuildDefensiveStructure(BuildingType.WoodenFort, playerID, gameState, cityCenter);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastDefenseBuildTime = currentTime;
                    return commands;
                }
            }

            return commands;
        }

        private IEngineCommand TryBuildDefensiveStructure(BuildingType buildingType, Guid playerID, GameState gameState, BuildingData cityCenter)
        {
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            int ccLevel = cityCenter.level;
            if (ccLevel < buildingType.RequiredCityCenterLevel()) return null;

            if (!player.CanAfford(buildingType.BuildCost())) return null;

            int maxDistance = buildingType == BuildingType.Tower ? 4 : 5;
            var rng = new System.Random();
            for (int distance = 2; distance <= maxDistance; distance++)
            {
                var ring = cityCenter.coordinate.CoordinatesInRing(distance);
                var shuffled = ring.OrderBy(_ => rng.Next());

                foreach (var coord in shuffled)
                {
                    if (gameState.CanBuildAt(coord, playerID))
                    {
                        return new AIBuildCommand(playerID, buildingType, coord, 0);
                    }
                }
            }

            return null;
        }

        // ================================================================
        // Garrison Commands
        // ================================================================

        public List<IEngineCommand> GenerateGarrisonCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastGarrisonCheckTime, currentTime, genome.garrisonCheckInterval)) return commands;

            var defensiveTypes = new HashSet<BuildingType>
            {
                BuildingType.Tower, BuildingType.Castle, BuildingType.WoodenFort
            };

            var ungarrisonedDefenses = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => defensiveTypes.Contains(b.buildingType) &&
                            b.IsOperational &&
                            b.GetTotalGarrisonedUnits() == 0)
                .ToList();

            if (ungarrisonedDefenses.Count == 0) return commands;

            var garrisonableTypes = new HashSet<MilitaryUnitType>
            {
                MilitaryUnitType.Archer, MilitaryUnitType.Crossbow,
                MilitaryUnitType.Mangonel, MilitaryUnitType.Trebuchet
            };

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null &&
                            a.militaryComposition.Any(kvp => garrisonableTypes.Contains(kvp.Key) && kvp.Value > 0));

            var assignedBuildings = new HashSet<Guid>();
            foreach (var army in idleArmies)
            {
                var targetBuilding = ungarrisonedDefenses.FirstOrDefault(b => !assignedBuildings.Contains(b.id));
                if (targetBuilding == null) break;

                int distance = army.coordinate.Distance(targetBuilding.coordinate);
                if (distance <= 6)
                {
                    commands.Add(new AIMoveCommand(playerID, army.id, targetBuilding.coordinate, true));
                    assignedBuildings.Add(targetBuilding.id);
                }
            }

            return commands;
        }

        // ================================================================
        // Entrenchment Commands
        // ================================================================

        public List<IEngineCommand> GenerateEntrenchmentCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastEntrenchCheckTime, currentTime, genome.entrenchCheckInterval))
                return new List<IEngineCommand>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return new List<IEngineCommand>();

            // Only entrench if there's a threat nearby
            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);
            if (threatLevel <= 0) return new List<IEngineCommand>();

            // Need wood buffer beyond entrenchment cost
            if (player.GetResource(ResourceType.Wood) < GameConfig.Entrenchment.WoodCost + 200)
                return new List<IEngineCommand>();

            // Count currently entrenched/entrenching armies
            var armies = gameState.GetArmiesForPlayer(playerID);
            int entrenchedCount = armies.Count(a => a.isEntrenched || a.isEntrenching);
            int maxEntrenched = threatLevel > 30 ? genome.maxEntrenchedHigh : genome.maxEntrenchedLow;
            if (entrenchedCount >= maxEntrenched) return new List<IEngineCommand>();

            // Find idle armies near city center that could entrench
            var candidates = armies
                .Where(a => !a.isInCombat && a.currentPath == null && !a.isRetreating &&
                            !a.isEntrenched && !a.isEntrenching)
                .OrderBy(a => a.coordinate.Distance(cityCenter.coordinate));

            foreach (var army in candidates)
            {
                int distance = army.coordinate.Distance(cityCenter.coordinate);
                if (distance > 8) continue;

                // Check that this army's tile isn't already entrenched
                var armiesHere = gameState.GetArmies(army.coordinate);
                bool alreadyEntrenched = armiesHere.Any(a => a.isEntrenched && a.id != army.id);
                if (alreadyEntrenched) continue;

                return new List<IEngineCommand> { new AIEntrenchCommand(playerID, army.id) };
            }

            return new List<IEngineCommand>();
        }
    }

    // ================================================================
    // GenomeAIResearchPlanner
    // ================================================================

    /// <summary>
    /// Genome-aware research planner. Reads research check interval from the AIGenome.
    /// </summary>
    public class GenomeAIResearchPlanner
    {
        private readonly AIGenome genome;

        public GenomeAIResearchPlanner(AIGenome genome)
        {
            this.genome = genome;
        }

        // ================================================================
        // Research Commands
        // ================================================================

        public List<IEngineCommand> GenerateResearchCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastResearchCheckTime, currentTime, genome.researchCheckInterval)) return commands;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            if (player.IsResearchActive()) return commands;

            var bestResearch = SelectBestResearch(aiState, gameState);
            if (bestResearch.HasValue)
            {
                if (player.CanAfford(bestResearch.Value.Cost()))
                {
                    commands.Add(new AIStartResearchCommand(playerID, bestResearch.Value));
                }
            }

            return commands;
        }

        // ================================================================
        // Research Selection
        // ================================================================

        private ResearchType? SelectBestResearch(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            int ccLevel = gameState.GetCityCenter(playerID)?.level ?? 1;

            var available = new List<ResearchType>();
            foreach (ResearchType research in Enum.GetValues(typeof(ResearchType)))
            {
                if (player.HasCompletedResearch(research.ToString())) continue;

                // Skip faction-blocked and faction-exclusive research
                if (player.faction.BlockedResearch().Contains(research)) continue;
                var exclusiveFaction = research.ExclusiveFaction();
                if (exclusiveFaction != FactionType.None && exclusiveFaction != player.faction) continue;

                if (research.CityCenterLevelRequirement() > ccLevel) continue;

                bool prereqsMet = true;
                foreach (var prereq in research.Prerequisites())
                {
                    if (!player.HasCompletedResearch(prereq.ToString()))
                    {
                        prereqsMet = false;
                        break;
                    }
                }

                // Check building requirement
                var buildingReq = research.BuildingRequirement();
                if (buildingReq.HasValue)
                {
                    var reqType = buildingReq.Value.buildingType;
                    int reqLevel = buildingReq.Value.level;
                    bool hasBuilding = gameState.GetBuildingsForPlayer(playerID)
                        .Any(b => b.buildingType == reqType &&
                                  b.level >= reqLevel &&
                                  b.IsOperational);
                    if (!hasBuilding)
                    {
                        prereqsMet = false;
                    }
                }

                if (prereqsMet) available.Add(research);
            }

            if (available.Count == 0) return null;

            ResearchType? best = null;
            double bestScore = double.MinValue;

            foreach (var research in available)
            {
                double score = ScoreResearch(research, aiState, gameState);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = research;
                }
            }

            return best;
        }

        // ================================================================
        // Research Scoring
        // ================================================================

        private double ScoreResearch(ResearchType research, AIPlayerState aiState, GameState gameState)
        {
            double score = (4 - research.Tier()) * 10.0;

            switch (aiState.currentState)
            {
                case AIState.Peace:
                    if (research.Category() == ResearchCategory.Economic)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.FarmGatheringI:
                            case ResearchType.FarmGatheringII:
                            case ResearchType.FarmGatheringIII:
                                score += 15.0; break;
                            case ResearchType.LumberCampGatheringI:
                            case ResearchType.LumberCampGatheringII:
                            case ResearchType.LumberCampGatheringIII:
                                score += 12.0; break;
                            case ResearchType.MiningCampGatheringI:
                            case ResearchType.MiningCampGatheringII:
                            case ResearchType.MiningCampGatheringIII:
                                score += 10.0; break;
                            case ResearchType.PopulationCapacityI:
                            case ResearchType.PopulationCapacityII:
                            case ResearchType.PopulationCapacityIII:
                                score += 8.0; break;
                            case ResearchType.BuildingSpeedI:
                            case ResearchType.BuildingSpeedII:
                            case ResearchType.BuildingSpeedIII:
                                score += 5.0; break;
                        }
                    }
                    break;

                case AIState.Alert:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 25.0;
                        switch (research)
                        {
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.InfantryPierceArmorI:
                            case ResearchType.InfantryPierceArmorII:
                            case ResearchType.InfantryPierceArmorIII:
                                score += 10.0; break;
                            case ResearchType.MilitaryTrainingSpeedI:
                            case ResearchType.MilitaryTrainingSpeedII:
                            case ResearchType.MilitaryTrainingSpeedIII:
                                score += 15.0; break;
                        }
                    }
                    else
                    {
                        score += 15.0;
                    }
                    break;

                case AIState.Defense:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.FortifiedBuildingsI:
                            case ResearchType.FortifiedBuildingsII:
                            case ResearchType.FortifiedBuildingsIII:
                                score += 20.0; break;
                            case ResearchType.BuildingBludgeonArmorI:
                            case ResearchType.BuildingBludgeonArmorII:
                            case ResearchType.BuildingBludgeonArmorIII:
                                score += 18.0; break;
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.CavalryMeleeArmorI:
                            case ResearchType.CavalryMeleeArmorII:
                            case ResearchType.CavalryMeleeArmorIII:
                                score += 12.0; break;
                            case ResearchType.RetreatSpeedI:
                            case ResearchType.RetreatSpeedII:
                            case ResearchType.RetreatSpeedIII:
                                score += 8.0; break;
                        }
                    }
                    break;

                case AIState.Attack:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.InfantryMeleeAttackI:
                            case ResearchType.InfantryMeleeAttackII:
                            case ResearchType.InfantryMeleeAttackIII:
                            case ResearchType.CavalryMeleeAttackI:
                            case ResearchType.CavalryMeleeAttackII:
                            case ResearchType.CavalryMeleeAttackIII:
                                score += 15.0; break;
                            case ResearchType.PiercingDamageI:
                            case ResearchType.PiercingDamageII:
                            case ResearchType.PiercingDamageIII:
                                score += 12.0; break;
                            case ResearchType.MarchSpeedI:
                            case ResearchType.MarchSpeedII:
                            case ResearchType.MarchSpeedIII:
                                score += 10.0; break;
                            case ResearchType.SiegeBludgeonDamageI:
                            case ResearchType.SiegeBludgeonDamageII:
                            case ResearchType.SiegeBludgeonDamageIII:
                                score += 15.0; break;
                        }
                    }
                    break;

                case AIState.Retreat:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        switch (research)
                        {
                            case ResearchType.RetreatSpeedI:
                            case ResearchType.RetreatSpeedII:
                            case ResearchType.RetreatSpeedIII:
                                score += 25.0; break;
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.CavalryMeleeArmorI:
                            case ResearchType.CavalryMeleeArmorII:
                            case ResearchType.CavalryMeleeArmorIII:
                                score += 15.0; break;
                        }
                    }
                    break;
            }

            // Penalize Tier I research in gated branches when gate building isn't built yet
            if (research.Tier() == 1)
            {
                var gateBuildingType = research.Branch().GateBuildingType();
                if (gateBuildingType.HasValue)
                {
                    bool hasGateBuilding = gameState.GetBuildingsForPlayer(aiState.playerID)
                        .Any(b => b.buildingType == gateBuildingType.Value && b.IsOperational);
                    if (!hasGateBuilding)
                    {
                        score -= 5.0;
                    }
                }
            }

            var playerID = aiState.playerID;
            var player = gameState.GetPlayer(playerID);

            // Synergy bonus: continuing a research line we already started
            if (research.Tier() > 1)
            {
                var prereqs = research.Prerequisites();
                bool allPrereqsComplete = prereqs.All(p => player != null && player.HasCompletedResearch(p.ToString()));
                if (allPrereqsComplete)
                    score += genome.researchSynergyWeight;
            }

            // Counter-research: boost research that counters inferred enemy capabilities
            if (aiState.lastEnemyAnalysis.HasValue)
            {
                var enemy = aiState.lastEnemyAnalysis.Value;
                var branch = research.Branch();

                if (enemy.infantryRatio > 0.35)
                {
                    if (branch == ResearchBranch.MeleeEquipment && research.ToString().Contains("Armor"))
                        score += genome.researchCounterWeight;
                }
                if (enemy.rangedRatio > 0.35)
                {
                    if (research.ToString().Contains("PierceArmor"))
                        score += genome.researchCounterWeight;
                }
                if (enemy.cavalryRatio > 0.35)
                {
                    if (research.ToString().Contains("InfantryMeleeAttack"))
                        score += genome.researchCounterWeight;
                }
            }

            // Composition-aware: match research to own army composition
            if (player != null)
            {
                var armies = gameState.GetArmiesForPlayer(playerID);
                int totalInfantry = 0, totalRanged = 0, totalCavalry = 0, totalSiege = 0, totalUnits = 0;
                foreach (var army in armies)
                {
                    var ratios = army.GetCategoryRatios();
                    int count = army.GetTotalUnits();
                    totalInfantry += (int)(ratios.infantry * count);
                    totalRanged += (int)(ratios.ranged * count);
                    totalCavalry += (int)(ratios.cavalry * count);
                    totalSiege += (int)(ratios.siege * count);
                    totalUnits += count;
                }

                if (totalUnits > 5)
                {
                    var branch = research.Branch();
                    double infantryRatio = (double)totalInfantry / totalUnits;
                    double rangedRatio = (double)totalRanged / totalUnits;
                    double cavalryRatio = (double)totalCavalry / totalUnits;

                    if (infantryRatio > 0.4 && branch == ResearchBranch.MeleeEquipment)
                        score += genome.researchCompositionWeight;
                    if (rangedRatio > 0.4 && branch == ResearchBranch.RangedEquipment)
                        score += genome.researchCompositionWeight;
                    if (cavalryRatio > 0.3 && branch == ResearchBranch.MeleeEquipment &&
                        research.ToString().Contains("Cavalry"))
                        score += genome.researchCompositionWeight;
                }
            }

            // Breadth bonus: reward starting new branches, penalize deep specialization
            if (player != null)
            {
                var branch = research.Branch();
                int completedInBranch = 0;
                foreach (ResearchType r in Enum.GetValues(typeof(ResearchType)))
                {
                    if (r.Branch() == branch && player.HasCompletedResearch(r.ToString()))
                        completedInBranch++;
                }

                if (completedInBranch == 0)
                    score += genome.researchBreadthBonus;
                else if (completedInBranch >= 6)
                    score -= genome.researchBreadthBonus;
            }

            return score;
        }
    }
}
