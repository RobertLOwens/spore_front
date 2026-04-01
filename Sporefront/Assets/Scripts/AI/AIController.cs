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
                DebugLog.Log($"AI Controller loaded evolved genome (gen {evolvedGenome.generation}, fitness {evolvedGenome.fitness:F2})");
            }
            else
            {
                genome = null;
            }

            DebugLog.Log("AI Controller setup - checking all players:");
            foreach (var player in gameState.players.Values)
            {
                DebugLog.Log($"   Player: {player.name} (ID: {player.id}) - isAI: {player.isAI}");
            }

            foreach (var player in gameState.GetAIPlayers())
            {
                aiPlayers[player.id] = new AIPlayerState(player.id);

                var cityCenter = gameState.GetCityCenter(player.id);
                if (cityCenter != null)
                {
                    DebugLog.Log($"AI Controller initialized for player: {player.name}");
                    DebugLog.Log($"   City center at: ({cityCenter.coordinate.q}, {cityCenter.coordinate.r})");
                }
                else
                {
                    DebugLog.Log($"WARNING: AI player {player.name} has NO city center!");
                    var allBuildings = gameState.GetBuildingsForPlayer(player.id);
                    DebugLog.Log($"   Buildings owned: {allBuildings.Count}");
                }

                DebugLog.Log($"   Resources: food={player.GetResource(ResourceType.Food)}, wood={player.GetResource(ResourceType.Wood)}, stone={player.GetResource(ResourceType.Stone)}, ore={player.GetResource(ResourceType.Ore)}");
            }

            DebugLog.Log($"Total AI players registered: {aiPlayers.Count}");
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

        public void ProcessHuntArrivals(GameState state)
        {
            AIHelper.ProcessHuntArrivals(state, GameEngine.Instance.resourceEngine);
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
                DebugLog.Log($"AI Decision cycle: state={aiState.currentState}, cityCenter={cityCenter != null}, buildings={buildingCount}, villagers={villagerCount}, food={player.GetResource(ResourceType.Food)}");

                // One-time strategic map analysis (chokepoints + map strategy)
                if (!aiState.mapAnalyzed)
                {
                    aiState.cachedChokepoints = AIStrategicAnalysis.AnalyzeChokepoints(gameState, playerID);
                    aiState.mapStrategy = AIStrategicAnalysis.DetermineMapStrategy(gameState, playerID);
                    aiState.currentBuildOrder = BuildOrder.ForStrategy(aiState.mapStrategy);

                    // Detect enemy faction and generate counter-strategy
                    foreach (var otherPlayer in gameState.players.Values)
                    {
                        if (otherPlayer.id == playerID) continue;
                        var dipStatus = gameState.GetDiplomacyStatus(playerID, otherPlayer.id);
                        if (dipStatus == DiplomacyStatus.Enemy)
                        {
                            aiState.factionStrategy = AIStrategicAnalysis.GetFactionCounterStrategy(otherPlayer.faction);
                            DebugLog.Log($"AI {player.name}: Faction counter-strategy — {aiState.factionStrategy.Value.description}");
                            break;
                        }
                    }

                    aiState.mapAnalyzed = true;
                    DebugLog.Log($"AI {player.name}: Map analysis complete — strategy={AIStrategicAnalysis.DescribeStrategy(aiState.mapStrategy)}, chokepoints={aiState.cachedChokepoints.Count}");
                    foreach (var cp in aiState.cachedChokepoints)
                    {
                        DebugLog.Log($"   Chokepoint at ({cp.center.q},{cp.center.r}), width={cp.width}");
                    }
                }

                // Compute rally points once (after map analysis and enemy base discovery)
                if (aiState.mapAnalyzed && !aiState.rallyPointsComputed)
                    militaryPlanner.ComputeRallyPoints(aiState, gameState);
                // Recompute rally points when we discover new enemy bases
                if (aiState.rallyPointsComputed && aiState.knownEnemyBases.Count > 0)
                {
                    int expectedRallyCount = aiState.rallyPoints.Count;
                    if (expectedRallyCount == 0 || (aiState.knownEnemyBases.Count > 0 && expectedRallyCount < 2))
                        militaryPlanner.ComputeRallyPoints(aiState, gameState);
                }

                // Update threat memory with visible enemy positions
                militaryPlanner.UpdateThreatMemory(aiState, gameState, currentTime);

                // Update enemy analysis cache
                militaryPlanner.UpdateEnemyAnalysis(aiState, gameState, currentTime);

                // Feature 2: Analyze enemy strategy (greed detection) every 15 seconds
                if (AIHelper.ShouldExecute(ref aiState.lastEnemyStrategyTime, currentTime, 15.0))
                {
                    var prevStrategy = aiState.enemyStrategy;
                    aiState.enemyStrategy = AIStrategicAnalysis.AnalyzeEnemyStrategy(gameState, playerID);
                    if (aiState.enemyStrategy != prevStrategy && aiState.enemyStrategy != EnemyStrategyRead.Unknown)
                        DebugLog.Log($"AI {player.name}: Enemy strategy detected — {aiState.enemyStrategy}");
                }

                // Feature 4: Assess game position (comeback mechanics) every 15 seconds
                if (AIHelper.ShouldExecute(ref aiState.lastPositionAssessTime, currentTime, 15.0))
                {
                    var prevPosition = aiState.gamePosition;
                    aiState.gamePosition = AIStrategicAnalysis.AssessGamePosition(gameState, playerID);
                    if (aiState.gamePosition != prevPosition)
                        DebugLog.Log($"AI {player.name}: Game position — {aiState.gamePosition}");
                }

                // Round 4: Update exploration percentage
                militaryPlanner.UpdateExplorationPercent(aiState, gameState, currentTime);

                // Round 4: Check build order milestones (adaptive timing)
                economyPlanner.CheckBuildOrderMilestones(aiState, gameState, currentTime);

                // Round 4: Evaluate siege requirement when enemy strategy is known
                if (aiState.enemyStrategy != EnemyStrategyRead.Unknown)
                {
                    militaryPlanner.EvaluateSiegeRequirement(aiState, gameState);
                    militaryPlanner.CheckSiegeReadiness(aiState, gameState);
                }

                // Round 4: Update dynamic counter composition every 15s
                if (currentTime - aiState.lastCompositionAdaptTime >= GameConfig.AI.Composition.AdaptInterval)
                {
                    if (aiState.lastEnemyAnalysis.HasValue)
                    {
                        aiState.dynamicTargetComposition = AIStrategicAnalysis.GenerateCounterComposition(
                            aiState.lastEnemyAnalysis.Value, aiState.mapStrategy, aiState.currentState);
                        aiState.lastCompositionAdaptTime = currentTime;
                    }
                }

                // Update AI state machine
                UpdateState(aiState, gameState, currentTime);

                // Generate commands based on current state
                var commands = GenerateCommands(aiState, gameState, currentTime);

                DebugLog.Log($"AI {player.name}: Generated {commands.Count} commands in state {aiState.currentState}");

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
            // Faction adaptation: widen alert radius if facing stealth-capable factions
            int alertRadius = 5;
            if (aiState.factionStrategy.HasValue)
                alertRadius += aiState.factionStrategy.Value.alertRadiusBonus;
            var nearbyEnemies = gameState.GetEnemyArmies(cityCenter.coordinate, alertRadius, playerID);

            var armies = gameState.GetArmiesForPlayer(playerID);
            bool armyNeedsRetreat = false;
            int retreatDist = genome != null ? genome.retreatDistanceFromBase : 3;
            foreach (var army in armies)
            {
                int distanceFromBase = army.coordinate.Distance(cityCenter.coordinate);
                bool isLocallyOutnumbered = gameState.IsArmyLocallyOutnumbered(army, playerID);

                if (isLocallyOutnumbered && distanceFromBase > retreatDist)
                {
                    armyNeedsRetreat = true;
                    break;
                }

                // Retreat if army is severely depleted (unit count below threshold)
                int unitThresh = genome != null ? genome.retreatUnitThreshold : 5;
                if (army.GetTotalUnits() > 0 && army.GetTotalUnits() < unitThresh && distanceFromBase > retreatDist)
                {
                    armyNeedsRetreat = true;
                    break;
                }
            }

            // Feature 2: Greedy punishment — rush when enemy has no military
            if (aiState.enemyStrategy == EnemyStrategyRead.Greedy
                && ourWeightedStrength >= 1000
                && aiState.currentState != AIState.Attack && aiState.currentState != AIState.Defense)
            {
                aiState.currentState = AIState.Attack;
                aiState.attackStateEnteredTime = currentTime;
                aiState.lastAttackProgressTime = currentTime;
                aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                DebugLog.Log($"AI {playerID}: -> Attack (PUNISHING GREEDY PLAY — enemy has no military)");
            }

            // Feature 4: CriticallyBehind — force defensive posture
            if (aiState.gamePosition == GamePosition.CriticallyBehind
                && aiState.currentState == AIState.Attack)
            {
                aiState.currentState = AIState.Retreat;
                aiState.persistentAttackTargetID = null;
                DebugLog.Log($"AI {playerID}: Attack -> Retreat (critically behind — conserving forces)");
            }

            switch (aiState.currentState)
            {
                case AIState.Peace:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                        DebugLog.Log($"AI {playerID}: Peace -> Defense (enemies nearby)");
                    }
                    else if (threatLevel > aiState.difficulty.AlertThreshold())
                    {
                        aiState.currentState = AIState.Alert;
                        DebugLog.Log($"AI {playerID}: Peace -> Alert (threat detected)");
                    }
                    else if (ShouldAttack(aiState, gameState, ourStrength))
                    {
                        aiState.currentState = AIState.Attack;
                        aiState.attackStateEnteredTime = currentTime;
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                        DebugLog.Log($"AI {playerID}: Peace -> Attack (strong enough)");
                    }
                    break;

                case AIState.Alert:
                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                        DebugLog.Log($"AI {playerID}: Alert -> Defense (enemies nearby)");
                    }
                    else if (threatLevel < aiState.difficulty.AlertThreshold() * 0.5)
                    {
                        aiState.currentState = AIState.Peace;
                        DebugLog.Log($"AI {playerID}: Alert -> Peace (threat reduced)");
                    }
                    else if (ShouldAttack(aiState, gameState, ourStrength))
                    {
                        aiState.currentState = AIState.Attack;
                        aiState.attackStateEnteredTime = currentTime;
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                        DebugLog.Log($"AI {playerID}: Alert -> Attack (strong enough)");
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
                            DebugLog.Log($"AI {playerID}: Defense -> Attack (counter-attack)");
                        }
                        else
                        {
                            aiState.currentState = AIState.Alert;
                            DebugLog.Log($"AI {playerID}: Defense -> Alert (enemies gone)");
                        }
                    }
                    else if (ourWeightedStrength < 500 || armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                        // Feature 2: Mark current battle location as defeat site in threat memory
                        if (nearbyEnemies.Count > 0)
                        {
                            var defeatCoord = nearbyEnemies[0].coordinate;
                            int enemyStrength = 0;
                            foreach (var e in nearbyEnemies) enemyStrength += e.GetTotalUnits();
                            aiState.threatMemory[defeatCoord] = new ThreatMemoryEntry(
                                defeatCoord, nearbyEnemies[0].ownerID ?? Guid.Empty,
                                enemyStrength, currentTime, true);
                        }
                        DebugLog.Log($"AI {playerID}: Defense -> Retreat (too weak or outnumbered)");
                    }
                    break;

                case AIState.Attack:
                    // Attack timeout: check if making progress
                    double currentEnemyStrength = gameState.AnalyzeEnemyComposition(playerID)?.weightedStrength ?? 0;
                    // Relative progress: 10% reduction counts as progress (scales with enemy strength)
                    double progressThreshold = Math.Max(50.0, aiState.lastKnownEnemyStrength * 0.1);
                    if (aiState.lastKnownEnemyStrength - currentEnemyStrength >= progressThreshold)
                    {
                        aiState.lastAttackProgressTime = currentTime;
                        aiState.lastKnownEnemyStrength = currentEnemyStrength;
                    }
                    if (currentTime - aiState.lastAttackProgressTime > GameConfig.AI.Timeouts.AttackTimeout)
                    {
                        aiState.currentState = AIState.Retreat;
                        aiState.persistentAttackTargetID = null;
                        DebugLog.Log($"AI {playerID}: Attack -> Retreat (attack timeout, no progress)");
                        break;
                    }

                    if (nearbyEnemies.Count > 0)
                    {
                        aiState.currentState = AIState.Defense;
                        DebugLog.Log($"AI {playerID}: Attack -> Defense (base under attack)");
                    }
                    else if (ourWeightedStrength < 1000)
                    {
                        aiState.currentState = AIState.Peace;
                        aiState.persistentAttackTargetID = null;
                        DebugLog.Log($"AI {playerID}: Attack -> Peace (army depleted)");
                    }
                    else if (armyNeedsRetreat)
                    {
                        aiState.currentState = AIState.Retreat;
                        // Feature 2: Mark last attack target as defeat site
                        if (aiState.lastAttackTarget.HasValue)
                        {
                            aiState.threatMemory[aiState.lastAttackTarget.Value] = new ThreatMemoryEntry(
                                aiState.lastAttackTarget.Value, Guid.Empty, 0, currentTime, true);
                        }
                        DebugLog.Log($"AI {playerID}: Attack -> Retreat (army in danger)");
                    }
                    break;

                case AIState.Retreat:
                    if (nearbyEnemies.Count == 0 && ourWeightedStrength > 1500)
                    {
                        aiState.currentState = AIState.Alert;
                        aiState.pendingArmyConvergence = null;
                        DebugLog.Log($"AI {playerID}: Retreat -> Alert (regrouped)");
                    }
                    break;
            }
        }

        private bool ShouldAttack(AIPlayerState aiState, GameState gameState, int ourStrength)
        {
            var playerID = aiState.playerID;
            double ourWeightedStrength = gameState.GetWeightedMilitaryStrength(playerID);
            if (ourWeightedStrength < 2000) return false;

            // Round 4: Don't attack blind — require minimum map knowledge
            if (!aiState.enemyBaseFound && aiState.mapExplorationPercent < GameConfig.AI.Scouting.MinExplorationBeforeAttack)
                return false;

            // Round 4: Siege intelligence — delay attack until siege is ready (with timeout)
            if (aiState.siegeRequired && !aiState.siegeReady)
            {
                double waitTime = gameState.currentTime - aiState.siegeRequirementDetectedTime;
                if (waitTime < GameConfig.AI.Siege.SiegeWaitTimeout)
                    return false; // Still waiting for siege
            }

            // Feature 2: Threat memory — check if enemy army has recently moved away,
            // creating a window of opportunity for attack
            var opportunity = militaryPlanner.FindOpportunityWindow(aiState, gameState, gameState.currentTime);
            if (opportunity.HasValue && ourWeightedStrength >= 1500)
            {
                DebugLog.Log($"AI {playerID}: Opportunity window detected at ({opportunity.Value.q},{opportunity.Value.r}) — enemy moved away");
                return true; // Attack even at lower threshold when enemy is out of position
            }

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
                var analysis = enemyAnalysis.Value;

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

                    // Use genome thresholds when available, fall back to defaults
                    double cavThresh = genome != null ? genome.counterCavalryThreshold : 0.35;
                    double rngThresh = genome != null ? genome.counterRangedThreshold : 0.4;
                    double infThresh = genome != null ? genome.counterInfantryThreshold : 0.4;

                    if (analysis.cavalryRatio > cavThresh && ourInfantryRatio > 0.3)
                        compositionModifier += 0.2;
                    if (analysis.rangedRatio > rngThresh && ourCavalryRatio > 0.3)
                        compositionModifier += 0.2;
                    if (analysis.infantryRatio > infThresh && ourRangedRatio > 0.3)
                        compositionModifier += 0.15;
                    if (ourCavalryRatio > cavThresh && analysis.infantryRatio > 0.3)
                        compositionModifier -= 0.2;
                    if (ourRangedRatio > rngThresh && analysis.cavalryRatio > 0.3)
                        compositionModifier -= 0.2;
                }
            }

            compositionModifier = Math.Max(0.5, Math.Min(1.5, compositionModifier));

            double effectiveStrength = ourWeightedStrength * compositionModifier;
            double ratio = effectiveStrength / Math.Max(1.0, enemyWeightedStrength);

            // Faction counter-strategy: adjust attack threshold
            double attackThreshold = aiState.difficulty.AttackThreshold();
            if (aiState.factionStrategy.HasValue)
            {
                // Negative aggressionModifier = more defensive = higher threshold needed
                // Positive aggressionModifier = more aggressive = lower threshold needed
                attackThreshold -= aiState.factionStrategy.Value.aggressionModifier;
            }

            // Feature 2: Turtle detection — require much higher strength vs turtles
            if (aiState.enemyStrategy == EnemyStrategyRead.Turtle)
                attackThreshold *= 1.5;

            // Feature 4: Comeback — don't attack when behind unless very strong
            if (aiState.gamePosition == GamePosition.Behind)
                attackThreshold += 0.5;
            else if (aiState.gamePosition == GamePosition.CriticallyBehind)
                return false; // Never initiate attacks when critically behind

            return ratio >= attackThreshold;
        }

        // ================================================================
        // Unit Upgrade Commands
        // ================================================================

        private List<IEngineCommand> GenerateUnitUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastUnitUpgradeCheckTime, currentTime, GameConfig.AI.Intervals.UnitUpgradeCheck))
                return new List<IEngineCommand>();

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

            DebugLog.Log($"AI starting unit upgrade: {best.Value.DisplayName()}");
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

            // Feature 3: Villager flee runs in ALL states — always protect economy
            commands.AddRange(economyPlanner.GenerateVillagerFleeCommands(aiState, gameState, currentTime));

            // Feature 1: Army merging runs in most states
            if (aiState.currentState != AIState.Defense) // Don't merge during active defense
                commands.AddRange(militaryPlanner.GenerateMergeCommands(aiState, gameState, currentTime));

            // Round 4: Commander utilization runs in most states
            if (aiState.currentState != AIState.Retreat)
                commands.AddRange(militaryPlanner.GenerateCommanderCommands(aiState, gameState, currentTime));

            switch (aiState.currentState)
            {
                case AIState.Peace:
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateExpansionCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateStagingCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateIdleArmyCommands(aiState, gameState, currentTime));
                    if (gameState.gameMode.UsesControlZones())
                        commands.AddRange(militaryPlanner.GenerateZoneContestCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidFleeCommands(aiState, gameState, currentTime));
                    // Round 4: Expansion timing — second CC
                    commands.AddRange(economyPlanner.GenerateExpansionTimingCommands(aiState, gameState, currentTime));
                    // Round 4: Resource denial / map control
                    commands.AddRange(economyPlanner.GenerateMapControlCommands(aiState, gameState, currentTime));
                    // Feature 2: Passive detection — expand more aggressively
                    if (aiState.enemyStrategy == EnemyStrategyRead.Passive)
                        commands.AddRange(economyPlanner.GenerateExpansionCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Alert:
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateStagingCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateIdleArmyCommands(aiState, gameState, currentTime));
                    if (gameState.gameMode.UsesControlZones())
                        commands.AddRange(militaryPlanner.GenerateZoneContestCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidFleeCommands(aiState, gameState, currentTime));
                    // Round 4: Resource denial in Alert too
                    commands.AddRange(economyPlanner.GenerateMapControlCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Defense:
                    commands.AddRange(militaryPlanner.GenerateDefenseCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateMilitaryCommands(aiState, gameState, currentTime));
                    if (gameState.gameMode.UsesControlZones())
                        commands.AddRange(militaryPlanner.GenerateZoneContestCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateDefensiveBuildingCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateEntrenchmentCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidFleeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Attack:
                    // Round 4: Feint — try diversion before committing main force
                    var feintCmds = militaryPlanner.GenerateFeintCommands(aiState, gameState, currentTime);
                    if (feintCmds.Count > 0)
                    {
                        commands.AddRange(feintCmds);
                    }
                    else
                    {
                        // Feature 4: Multi-objective attack when 3+ armies available
                        var multiObjCmds = militaryPlanner.GenerateMultiObjectiveAttack(aiState, gameState, currentTime);
                        if (multiObjCmds.Count > 0)
                            commands.AddRange(multiObjCmds);
                        else
                            commands.AddRange(militaryPlanner.GenerateAttackCommands(aiState, gameState, currentTime));
                    }
                    commands.AddRange(militaryPlanner.GenerateScoutingCommands(aiState, gameState, currentTime));
                    if (gameState.gameMode.UsesControlZones())
                        commands.AddRange(militaryPlanner.GenerateZoneContestCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateEconomyCommands(aiState, gameState, currentTime));
                    commands.AddRange(economyPlanner.GenerateUpgradeCommands(aiState, gameState, currentTime));
                    commands.AddRange(researchPlanner.GenerateResearchCommands(aiState, gameState, currentTime));
                    commands.AddRange(defensePlanner.GenerateGarrisonCommands(aiState, gameState, currentTime));
                    commands.AddRange(GenerateUnitUpgradeCommands(aiState, gameState, currentTime));
                    // Feature 1: Raids during attack (harass while main force pushes)
                    commands.AddRange(militaryPlanner.GenerateRaidCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidFleeCommands(aiState, gameState, currentTime));
                    break;

                case AIState.Retreat:
                    commands.AddRange(militaryPlanner.GenerateRetreatCommands(aiState, gameState, currentTime));
                    commands.AddRange(militaryPlanner.GenerateRaidFleeCommands(aiState, gameState, currentTime));
                    break;
            }

            return commands;
        }
    }
}
