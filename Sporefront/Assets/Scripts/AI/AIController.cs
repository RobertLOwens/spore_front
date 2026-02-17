// ============================================================================
// FILE: AI/AIController.cs
// PURPOSE: Main AI controller - orchestrates planners via state machine
//          C# port of AIController.swift (controller portion only;
//          command classes are in AI/Commands/)
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
    public class AIController
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static AIController _instance;
        public static AIController Instance
        {
            get
            {
                if (_instance == null) _instance = new AIController();
                return _instance;
            }
        }

        // ================================================================
        // State
        // ================================================================

        public Dictionary<Guid, AIPlayerState> aiPlayers { get; private set; } = new Dictionary<Guid, AIPlayerState>();
        private GameState gameState;

        /// Optional evolved genome (for identification/logging)
        public AIGenome genome { get; private set; }

        // ================================================================
        // Planners
        // ================================================================

        private readonly AIEconomyPlanner economyPlanner = new AIEconomyPlanner();
        private readonly AIMilitaryPlanner militaryPlanner = new AIMilitaryPlanner();
        private readonly AIDefensePlanner defensePlanner = new AIDefensePlanner();
        private readonly AIResearchPlanner researchPlanner = new AIResearchPlanner();

        private AIController() { }

        /// Attempt to load an evolved genome for the given map type
        public static AIGenome LoadEvolvedGenome(string mapType = "arabia")
        {
            return AIGenome.LoadBest(mapType);
        }

        // ================================================================
        // Setup
        // ================================================================

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
            aiPlayers.Clear();

            // Attempt to load evolved genome
            var evolvedGenome = LoadEvolvedGenome();
            if (evolvedGenome != null)
            {
                genome = evolvedGenome;
                DebugLog.Log(string.Format("AI Controller loaded evolved genome (gen {0}, fitness {1:F2})",
                    evolvedGenome.generation, evolvedGenome.fitness));
            }
            else
            {
                genome = null;
            }

            DebugLog.Log("AI Controller setup - checking all players:");
            foreach (var player in gameState.players.Values)
            {
                DebugLog.Log(string.Format("   Player: {0} (ID: {1}) - isAI: {2}",
                    player.name, player.id, player.isAI));
            }

            foreach (var player in gameState.GetAIPlayers())
            {
                aiPlayers[player.id] = new AIPlayerState(player.id);

                var cityCenter = gameState.GetCityCenter(player.id);
                if (cityCenter != null)
                {
                    DebugLog.Log(string.Format("AI Controller initialized for player: {0}", player.name));
                    DebugLog.Log(string.Format("   City center at: ({0}, {1})", cityCenter.coordinate.q, cityCenter.coordinate.r));
                }
                else
                {
                    DebugLog.Log(string.Format("WARNING: AI player {0} has NO city center!", player.name));
                    var allBuildings = gameState.GetBuildingsForPlayer(player.id);
                    DebugLog.Log(string.Format("   Buildings owned: {0}", allBuildings.Count));
                }

                DebugLog.Log(string.Format("   Resources: food={0}, wood={1}, stone={2}, ore={3}",
                    player.GetResource(ResourceType.Food), player.GetResource(ResourceType.Wood),
                    player.GetResource(ResourceType.Stone), player.GetResource(ResourceType.Ore)));
            }

            DebugLog.Log(string.Format("Total AI players registered: {0}", aiPlayers.Count));
        }

        public void Reset()
        {
            aiPlayers.Clear();
            gameState = null;
        }

        public void RegisterAIPlayer(Guid playerID, AIDifficulty difficulty = AIDifficulty.Medium)
        {
            aiPlayers[playerID] = new AIPlayerState(playerID, difficulty);
        }

        public void UnregisterAIPlayer(Guid playerID)
        {
            aiPlayers.Remove(playerID);
        }

        // ================================================================
        // Hunt Arrival Processing
        // ================================================================

        private void ProcessHuntArrivals(GameState state)
        {
            // Collect groups to process (avoid modifying collection during iteration)
            var groups = state.villagerGroups.Values.ToList();

            foreach (var group in groups)
            {
                var huntTask = group.currentTask as HuntingTask;
                if (huntTask == null) continue;
                if (group.currentPath != null) continue; // Still en route

                var resourcePointID = huntTask.ResourcePointID;
                var resource = state.GetResourcePoint(resourcePointID);
                if (resource == null)
                {
                    group.ClearTask();
                    continue;
                }

                if (!group.coordinate.Equals(resource.coordinate)) continue;

                // At target — execute hunt combat
                double villagerAttack = group.villagerCount * 25.0;
                double damageToAnimal = Math.Max(1.0, villagerAttack - resource.resourceType.DefensePower());
                resource.TakeDamage(damageToAnimal);

                double animalAttack = resource.resourceType.AttackPower();
                double damageToVillagers = Math.Max(0.0, animalAttack - group.villagerCount * 0.5);
                int villagersLost = (int)(damageToVillagers / 5.0);
                if (villagersLost > 0) group.RemoveVillagers(villagersLost);

                if (resource.currentHealth <= 0)
                {
                    // Animal killed — create carcass and start gathering
                    var carcass = resource.CreateCarcassData();
                    if (carcass != null)
                    {
                        state.RemoveResourcePoint(resource.id);
                        state.AddResourcePoint(carcass);

                        bool registered = GameEngine.Instance.resourceEngine.StartGathering(
                            group.id, carcass.id);
                        if (registered)
                        {
                            GameEngine.Instance.resourceEngine.UpdateCollectionRates(
                                group.ownerID ?? Guid.Empty);
                        }
                        else
                        {
                            group.ClearTask();
                        }
                    }
                    else
                    {
                        group.ClearTask();
                    }
                }
                else
                {
                    // Animal survived — clear task, AI will retry next cycle
                    group.ClearTask();
                }

                if (group.villagerCount <= 0)
                {
                    GameEngine.Instance.resourceEngine.StopGathering(group.id);
                    state.RemoveVillagerGroup(group.id);
                }
            }
        }

        // ================================================================
        // Main Update
        // ================================================================

        public List<IEngineCommand> Update(double currentTime)
        {
            if (gameState == null) return new List<IEngineCommand>();

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
                if (timeSinceLastDecision < aiState.difficulty.DecisionInterval()) continue;

                var cityCenter = gameState.GetCityCenter(playerID);
                int villagerCount = gameState.GetVillagerCount(playerID);
                int buildingCount = gameState.GetBuildingsForPlayer(playerID).Count;
                DebugLog.Log(string.Format("AI Decision cycle: state={0}, cityCenter={1}, buildings={2}, villagers={3}, food={4}",
                    aiState.currentState, cityCenter != null, buildingCount, villagerCount,
                    player.GetResource(ResourceType.Food)));

                // Update enemy analysis cache
                militaryPlanner.UpdateEnemyAnalysis(aiState, gameState, currentTime);

                // Update AI state machine
                UpdateState(aiState, gameState, currentTime);

                // Generate commands based on current state
                var commands = GenerateCommands(aiState, gameState, currentTime);

                DebugLog.Log(string.Format("AI {0}: Generated {1} commands in state {2}",
                    player.name, commands.Count, aiState.currentState));

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

            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);
            int ourStrength = gameState.GetMilitaryStrength(playerID);
            double ourWeightedStrength = gameState.GetWeightedMilitaryStrength(playerID);
            var nearbyEnemies = gameState.GetEnemyArmies(cityCenter.coordinate, 5, playerID);

            var armies = gameState.GetArmiesForPlayer(playerID);
            bool armyNeedsRetreat = false;
            foreach (var army in armies)
            {
                int distanceFromBase = army.coordinate.Distance(cityCenter.coordinate);
                bool isLocallyOutnumbered = gameState.IsArmyLocallyOutnumbered(army, playerID);

                if (isLocallyOutnumbered && distanceFromBase > 3)
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
                        DebugLog.Log(string.Format("AI {0}: Peace -> Defense (enemies nearby)", playerID));
                    }
                    else if (threatLevel > aiState.difficulty.AlertThreshold())
                    {
                        aiState.currentState = AIState.Alert;
                        DebugLog.Log(string.Format("AI {0}: Peace -> Alert (threat detected)", playerID));
                    }
                    else if (ShouldAttack(aiState, gameState, ourStrength))
                    {
                        aiState.currentState = AIState.Attack;
                        DebugLog.Log(string.Format("AI {0}: Peace -> Attack (strong enough)", playerID));
                    }
                    break;

                case AIState.Alert:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                        DebugLog.Log(string.Format("AI {0}: Alert -> Defense (enemies nearby)", playerID));
                    }
                    else if (threatLevel < aiState.difficulty.AlertThreshold() * 0.5)
                    {
                        aiState.currentState = AIState.Peace;
                        DebugLog.Log(string.Format("AI {0}: Alert -> Peace (threat reduced)", playerID));
                    }
                    else if (ShouldAttack(aiState, gameState, ourStrength))
                    {
                        aiState.currentState = AIState.Attack;
                        DebugLog.Log(string.Format("AI {0}: Alert -> Attack (strong enough)", playerID));
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
                            DebugLog.Log(string.Format("AI {0}: Defense -> Attack (counter-attack)", playerID));
                        }
                        else
                        {
                            aiState.currentState = AIState.Alert;
                            DebugLog.Log(string.Format("AI {0}: Defense -> Alert (enemies gone)", playerID));
                        }
                    }
                    else if (ourWeightedStrength < 500 || armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                        DebugLog.Log(string.Format("AI {0}: Defense -> Retreat (too weak or outnumbered)", playerID));
                    }
                    break;

                case AIState.Attack:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                        DebugLog.Log(string.Format("AI {0}: Attack -> Defense (base under attack)", playerID));
                    }
                    else if (ourWeightedStrength < 1000)
                    {
                        aiState.currentState = AIState.Peace;
                        aiState.persistentAttackTargetID = null;
                        DebugLog.Log(string.Format("AI {0}: Attack -> Peace (army depleted)", playerID));
                    }
                    else if (armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                        DebugLog.Log(string.Format("AI {0}: Attack -> Retreat (army in danger)", playerID));
                    }
                    break;

                case AIState.Retreat:
                    if (nearbyEnemies.Count == 0 && ourWeightedStrength > 1500)
                    {
                        aiState.currentState = AIState.Alert;
                        aiState.pendingArmyConvergence = null;
                        DebugLog.Log(string.Format("AI {0}: Retreat -> Alert (regrouped)", playerID));
                    }
                    break;
            }
        }

        private bool ShouldAttack(AIPlayerState aiState, GameState gameState, int ourStrength)
        {
            var playerID = aiState.playerID;
            double ourWeightedStrength = gameState.GetWeightedMilitaryStrength(playerID);
            if (ourWeightedStrength < 2000) return false;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return false;

            var enemyAnalysis = gameState.AnalyzeEnemyComposition(playerID);

            var enemyArmy = gameState.GetNearestEnemyArmy(cityCenter.coordinate, playerID);
            if (enemyArmy == null)
            {
                var enemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
                return enemyBuildings.Count > 0;
            }

            double enemyWeightedStrength = enemyArmy.GetWeightedStrength();

            double compositionModifier = 1.0;
            if (enemyAnalysis != null)
            {
                var analysis = enemyAnalysis;

                var ourArmies = gameState.GetArmiesForPlayer(playerID);
                double ourCavalryRatio = 0.0;
                double ourRangedRatio = 0.0;
                double ourInfantryRatio = 0.0;
                int totalOurUnits = 0;

                foreach (var army in ourArmies)
                {
                    var ratios = army.GetCategoryRatios();
                    int count = army.GetTotalUnits();
                    ourCavalryRatio += ratios.cavalry * count;
                    ourRangedRatio += ratios.ranged * count;
                    ourInfantryRatio += ratios.infantry * count;
                    totalOurUnits += count;
                }

                if (totalOurUnits > 0)
                {
                    ourCavalryRatio /= totalOurUnits;
                    ourRangedRatio /= totalOurUnits;
                    ourInfantryRatio /= totalOurUnits;

                    if (analysis.cavalryRatio > 0.35 && ourInfantryRatio > 0.3)
                        compositionModifier += 0.2;
                    if (analysis.rangedRatio > 0.35 && ourCavalryRatio > 0.3)
                        compositionModifier += 0.2;
                    if (analysis.infantryRatio > 0.4 && ourRangedRatio > 0.3)
                        compositionModifier += 0.15;
                    if (ourCavalryRatio > 0.35 && analysis.infantryRatio > 0.3)
                        compositionModifier -= 0.2;
                    if (ourRangedRatio > 0.35 && analysis.cavalryRatio > 0.3)
                        compositionModifier -= 0.2;
                }
            }

            compositionModifier = Math.Max(0.5, Math.Min(1.5, compositionModifier));

            double effectiveStrength = ourWeightedStrength * compositionModifier;
            double ratio = effectiveStrength / Math.Max(1.0, enemyWeightedStrength);

            return ratio >= aiState.difficulty.AttackThreshold();
        }

        // ================================================================
        // Unit Upgrade Commands
        // ================================================================

        private List<IEngineCommand> GenerateUnitUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastUnitUpgradeCheckTime < GameConfig.AI.Intervals.UnitUpgradeCheck)
                return new List<IEngineCommand>();
            aiState.lastUnitUpgradeCheckTime = currentTime;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();

            if (player.IsUnitUpgradeActive()) return new List<IEngineCommand>();

            var available = GetAvailableUnitUpgrades(playerID, gameState);
            if (available.Count == 0) return new List<IEngineCommand>();

            // Score and pick the best
            UnitUpgradeType? best = null;
            double bestScore = double.MinValue;

            foreach (var upgrade in available)
            {
                double score = ScoreUnitUpgrade(upgrade, playerID, gameState);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = upgrade;
                }
            }

            if (!best.HasValue) return new List<IEngineCommand>();

            // Check affordability
            var cost = best.Value.Cost();
            if (!player.CanAfford(cost)) return new List<IEngineCommand>();

            // Find a building of the right type with sufficient level
            var buildings = gameState.GetBuildingsForPlayer(playerID);
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

            DebugLog.Log(string.Format("AI starting unit upgrade: {0}", best.Value.DisplayName()));
            return new List<IEngineCommand> { new AIUpgradeUnitCommand(playerID, best.Value, building.id) };
        }

        private List<UnitUpgradeType> GetAvailableUnitUpgrades(Guid playerID, GameState gameState)
        {
            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<UnitUpgradeType>();

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

            return available;
        }

        private double ScoreUnitUpgrade(UnitUpgradeType upgrade, Guid playerID, GameState gameState)
        {
            double score = (4 - upgrade.Tier()) * 20.0;

            var armies = gameState.GetArmiesForPlayer(playerID);
            bool hasUnit = armies.Any(a => a.GetUnitCount(upgrade.GetUnitType()) > 0);

            if (hasUnit)
                score += 30.0;
            else
                score += 5.0;

            return score;
        }

        // ================================================================
        // Command Generation (Delegates to Planners)
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
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Alert:
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Defense:
                    commands.AddRange(militaryPlanner.GenerateDefenseCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Attack:
                    commands.AddRange(militaryPlanner.GenerateAttackCommands(aiState, gameState, currentTime));
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
    }
}
