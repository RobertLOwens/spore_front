// ============================================================================
// FILE: Engine/GameSimulator.cs
// PURPOSE: Headless AI-vs-AI game simulator for evolutionary optimization
//          C# port of GameSimulator.swift (373 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.AI;
using Sporefront.AI.Commands;

namespace Sporefront.Engine
{
    // ================================================================
    // Game End Reason
    // ================================================================

    public enum GameEndReason
    {
        Conquest,       // City center destroyed
        Starvation,     // 0 food for 60+ seconds
        Timeout         // Max game time reached
    }

    // ================================================================
    // Player Stats
    // ================================================================

    public class PlayerStats
    {
        public double peakMilitaryStrength;
        public int totalUnitsBuilt;
        public int totalBuildingsBuilt;
        public int totalResourcesGathered;
        public double finalMilitaryStrength;
    }

    // ================================================================
    // Game Result
    // ================================================================

    public class GameResult
    {
        public int? winnerIndex;       // 0, 1, or null for draw
        public GameEndReason reason;
        public double duration;
        public PlayerStats p1Stats;
        public PlayerStats p2Stats;

        public GameResult(int? winnerIndex, GameEndReason reason, double duration,
            PlayerStats p1Stats, PlayerStats p2Stats)
        {
            this.winnerIndex = winnerIndex;
            this.reason = reason;
            this.duration = duration;
            this.p1Stats = p1Stats;
            this.p2Stats = p2Stats;
        }
    }

    // ================================================================
    // Game Simulator
    // ================================================================

    /// <summary>
    /// Runs a complete headless AI-vs-AI game on a full 35x35 Arabia map.
    /// Each invocation is independent with its own engine subsystems.
    /// </summary>
    public static class GameSimulator
    {
        // Configuration
        public const double MaxGameTime = 1200.0;      // 20 minutes
        public const double TickInterval = 0.5;
        public const double StarvationThreshold = 60.0; // Seconds of zero food before loss

        /// <summary>
        /// Run a single headless AI-vs-AI game and return the result.
        /// </summary>
        public static GameResult RunGame(AIGenome genome1, AIGenome genome2, ulong mapSeed)
        {
            bool previousLogState = DebugLog.Enabled;
            DebugLog.Enabled = false;
            try
            {
                return RunGameInternal(genome1, genome2, mapSeed);
            }
            finally
            {
                DebugLog.Enabled = previousLogState;
            }
        }

        private static GameResult RunGameInternal(AIGenome genome1, AIGenome genome2, ulong mapSeed)
        {
            // 1. Create game state
            var gameState = new GameState(35, 35);

            // 2. Generate terrain via ArabiaMapGenerator
            var generator = new ArabiaMapGenerator(mapSeed);
            var terrainData = generator.GenerateTerrain();

            foreach (var kvp in terrainData)
            {
                gameState.mapData.SetTile(new TileData(kvp.Key, kvp.Value.terrain, kvp.Value.elevation));
            }

            // 3. Create 2 AI player states
            var player1 = new PlayerState("AI-1", "#0000FF", true);
            var player2 = new PlayerState("AI-2", "#FF0000", true);
            var player1ID = player1.id;
            var player2ID = player2.id;
            gameState.AddPlayer(player1);
            gameState.AddPlayer(player2);

            // Set diplomacy
            player1.SetDiplomacyStatus(player2ID, DiplomacyStatus.Enemy);
            player2.SetDiplomacyStatus(player1ID, DiplomacyStatus.Enemy);

            // 4. Place city centers at starting positions
            var startPositions = generator.GetStartingPositions();
            var pos1 = startPositions[0].coordinate;
            var pos2 = startPositions[1].coordinate;

            var cc1 = new BuildingData(BuildingType.CityCenter, pos1, player1ID);
            cc1.state = BuildingState.Completed;
            gameState.AddBuilding(cc1);

            var cc2 = new BuildingData(BuildingType.CityCenter, pos2, player2ID);
            cc2.state = BuildingState.Completed;
            gameState.AddBuilding(cc2);

            // 5. Place starting villagers adjacent to each city center
            PlaceStartingVillagers(gameState, player1ID, pos1, "AI-1 Villagers");
            PlaceStartingVillagers(gameState, player2ID, pos2, "AI-2 Villagers");

            // 6. Place starting resources from map generator
            foreach (var startPos in startPositions)
            {
                var resources = generator.GenerateStartingResources(startPos.coordinate);
                foreach (var placement in resources)
                {
                    var resourceData = new ResourcePointData(placement.coordinate, placement.resourceType);
                    gameState.AddResourcePoint(resourceData);
                    gameState.mapData.RegisterResourcePoint(resourceData.id, placement.coordinate);
                }
            }

            // 7. Place neutral resources
            var startCoords = new List<HexCoordinate>();
            foreach (var sp in startPositions)
                startCoords.Add(sp.coordinate);

            var neutralResources = generator.GenerateNeutralResources(10, startCoords);
            foreach (var placement in neutralResources)
            {
                var resourceData = new ResourcePointData(placement.coordinate, placement.resourceType);
                gameState.AddResourcePoint(resourceData);
                gameState.mapData.RegisterResourcePoint(resourceData.id, placement.coordinate);
            }

            // 8. Create fresh subsystem engines
            var combatEngine = new CombatEngine();
            var movementEngine = new MovementEngine();
            var resourceEngine1 = new ResourceEngine();
            var resourceEngine2 = new ResourceEngine();
            var constructionEngine = new ConstructionEngine();
            var trainingEngine = new TrainingEngine();
            var visionEngine = new VisionEngine();
            var sharedResourceEngine = new ResourceEngine();

            combatEngine.Setup(gameState);
            movementEngine.Setup(gameState);
            sharedResourceEngine.Setup(gameState);
            resourceEngine1.Setup(gameState);
            resourceEngine2.Setup(gameState);
            constructionEngine.Setup(gameState);
            trainingEngine.Setup(gameState);
            visionEngine.Setup(gameState);

            // 9. Create simulation AI controllers with respective genomes
            var context1 = new SimulationContext(resourceEngine1, genome1);
            var context2 = new SimulationContext(resourceEngine2, genome2);

            var aiController1 = new SimulationAIController(context1);
            var aiController2 = new SimulationAIController(context2);

            aiController1.Setup(gameState);
            aiController1.ClearAIPlayers();
            aiController1.RegisterAIPlayer(player1ID);

            aiController2.Setup(gameState);
            aiController2.ClearAIPlayers();
            aiController2.RegisterAIPlayer(player2ID);

            // 10. Game loop
            double simTime = 0.0;
            var p1Stats = new PlayerStats();
            var p2Stats = new PlayerStats();
            double p1ZeroFoodTime = 0;
            double p2ZeroFoodTime = 0;

            // Timing trackers (mirroring GameEngine)
            double lastVisionUpdate = 0;
            double lastMovementUpdate = 0;
            double lastBuildingUpdate = 0;
            double lastTrainingUpdate = 0;
            double lastResourceUpdate = 0;
            double lastCombatUpdate = 0;
            double lastEntrenchmentUpdate = 0;

            while (simTime < MaxGameTime)
            {
                simTime += TickInterval;
                gameState.currentTime = simTime;

                // Vision (4x/sec -> every 0.25s)
                if (simTime - lastVisionUpdate >= 0.25)
                {
                    visionEngine.Update(simTime);
                    lastVisionUpdate = simTime;
                }

                // Movement (10x/sec -> every 0.1s, but our tick is 0.5s so every tick)
                if (simTime - lastMovementUpdate >= 0.1)
                {
                    var movementChanges = movementEngine.Update(simTime);

                    // Check for reinforcements joining stack combat
                    foreach (var change in movementChanges)
                    {
                        var moved = change as ArmyMovedChange;
                        if (moved != null && moved.path != null && moved.path.Count == 0)
                        {
                            combatEngine.AddDefenderToStackCombat(
                                moved.armyID, moved.to, simTime);
                        }
                    }

                    lastMovementUpdate = simTime;
                }

                // Construction (2x/sec -> every 0.5s)
                if (simTime - lastBuildingUpdate >= 0.5)
                {
                    var constructionChanges = constructionEngine.Update(simTime);

                    // Recalculate collection rates on building upgrade
                    foreach (var change in constructionChanges)
                    {
                        var upgradeComplete = change as BuildingUpgradeCompletedChange;
                        if (upgradeComplete != null)
                        {
                            var building = gameState.GetBuilding(upgradeComplete.buildingID);
                            if (building != null && building.ownerID.HasValue)
                            {
                                sharedResourceEngine.UpdateCollectionRates(building.ownerID.Value);
                            }
                        }
                    }

                    lastBuildingUpdate = simTime;
                }

                // Training (1x/sec)
                if (simTime - lastTrainingUpdate >= 1.0)
                {
                    trainingEngine.Update(simTime);
                    lastTrainingUpdate = simTime;
                }

                // Resources (2x/sec -> every 0.5s)
                if (simTime - lastResourceUpdate >= 0.5)
                {
                    sharedResourceEngine.Update(simTime);
                    lastResourceUpdate = simTime;
                }

                // Combat (1x/sec)
                if (simTime - lastCombatUpdate >= 1.0)
                {
                    combatEngine.Update(simTime);
                    lastCombatUpdate = simTime;
                }

                // AI updates (every tick = 0.5s)
                var ai1Commands = aiController1.Update(simTime, gameState);
                var ai2Commands = aiController2.Update(simTime, gameState);

                // Execute AI commands
                var allCommands = new List<IEngineCommand>();
                allCommands.AddRange(ai1Commands);
                allCommands.AddRange(ai2Commands);

                foreach (var command in allCommands)
                {
                    var changeBuilder = new StateChangeBuilder(simTime, command.Id);
                    var validationResult = command.Validate(gameState);
                    if (validationResult.Succeeded)
                    {
                        var result = command.Execute(gameState, changeBuilder);
                        if (result.Succeeded)
                        {
                            // Track building construction
                            if (command is AIBuildCommand)
                            {
                                if (command.PlayerID == player1ID) p1Stats.totalBuildingsBuilt++;
                                else p2Stats.totalBuildingsBuilt++;
                            }
                            if (command is AITrainMilitaryCommand || command is AITrainVillagerCommand)
                            {
                                if (command.PlayerID == player1ID) p1Stats.totalUnitsBuilt++;
                                else p2Stats.totalUnitsBuilt++;
                            }
                        }
                    }
                }

                // Entrenchment progress
                if (simTime - lastEntrenchmentUpdate >= 0.5)
                {
                    foreach (var army in gameState.armies.Values)
                    {
                        if (!army.isEntrenching || !army.entrenchmentStartTime.HasValue) continue;
                        double elapsed = simTime - army.entrenchmentStartTime.Value;
                        if (elapsed >= GameConfig.Entrenchment.BuildTime)
                        {
                            var coverage = gameState.ComputeEntrenchmentCoverage(army);
                            army.isEntrenching = false;
                            army.isEntrenched = true;
                            army.entrenchmentStartTime = null;
                            army.entrenchedCoveredTiles = coverage;
                        }
                    }
                    lastEntrenchmentUpdate = simTime;
                }

                // Research completion
                foreach (var player in gameState.players.Values)
                {
                    if (player.activeResearchType == null || !player.activeResearchStartTime.HasValue)
                        continue;

                    ResearchType researchType;
                    if (!Enum.TryParse(player.activeResearchType, out researchType))
                        continue;

                    if (simTime - player.activeResearchStartTime.Value >= researchType.ResearchTime())
                    {
                        player.CompleteResearch(player.activeResearchType);
                    }
                }

                // Unit upgrade completion
                foreach (var player in gameState.players.Values)
                {
                    if (player.activeUnitUpgrade == null || !player.activeUnitUpgradeStartTime.HasValue)
                        continue;

                    UnitUpgradeType upgradeType;
                    if (!Enum.TryParse(player.activeUnitUpgrade, out upgradeType))
                        continue;

                    if (simTime - player.activeUnitUpgradeStartTime.Value >= upgradeType.UpgradeTime())
                    {
                        player.CompleteUnitUpgrade(player.activeUnitUpgrade);
                    }
                }

                // Track peak military strength
                double p1Strength = gameState.GetWeightedMilitaryStrength(player1ID);
                double p2Strength = gameState.GetWeightedMilitaryStrength(player2ID);
                p1Stats.peakMilitaryStrength = Math.Max(p1Stats.peakMilitaryStrength, p1Strength);
                p2Stats.peakMilitaryStrength = Math.Max(p2Stats.peakMilitaryStrength, p2Strength);

                // Win condition: conquest (city center destroyed)
                bool cc1Exists = gameState.GetCityCenter(player1ID) != null;
                bool cc2Exists = gameState.GetCityCenter(player2ID) != null;

                if (!cc1Exists && cc2Exists)
                {
                    p1Stats.finalMilitaryStrength = p1Strength;
                    p2Stats.finalMilitaryStrength = p2Strength;
                    return new GameResult(1, GameEndReason.Conquest, simTime, p1Stats, p2Stats);
                }
                if (!cc2Exists && cc1Exists)
                {
                    p1Stats.finalMilitaryStrength = p1Strength;
                    p2Stats.finalMilitaryStrength = p2Strength;
                    return new GameResult(0, GameEndReason.Conquest, simTime, p1Stats, p2Stats);
                }
                if (!cc1Exists && !cc2Exists)
                {
                    p1Stats.finalMilitaryStrength = p1Strength;
                    p2Stats.finalMilitaryStrength = p2Strength;
                    return new GameResult(null, GameEndReason.Conquest, simTime, p1Stats, p2Stats);
                }

                // Win condition: starvation (0 food for 60s)
                if (player1.GetResource(ResourceType.Food) <= 0)
                    p1ZeroFoodTime += TickInterval;
                else
                    p1ZeroFoodTime = 0;

                if (player2.GetResource(ResourceType.Food) <= 0)
                    p2ZeroFoodTime += TickInterval;
                else
                    p2ZeroFoodTime = 0;

                if (p1ZeroFoodTime >= StarvationThreshold && p2ZeroFoodTime < StarvationThreshold)
                {
                    p1Stats.finalMilitaryStrength = p1Strength;
                    p2Stats.finalMilitaryStrength = p2Strength;
                    return new GameResult(1, GameEndReason.Starvation, simTime, p1Stats, p2Stats);
                }
                if (p2ZeroFoodTime >= StarvationThreshold && p1ZeroFoodTime < StarvationThreshold)
                {
                    p1Stats.finalMilitaryStrength = p1Strength;
                    p2Stats.finalMilitaryStrength = p2Strength;
                    return new GameResult(0, GameEndReason.Starvation, simTime, p1Stats, p2Stats);
                }
            }

            // Timeout: determine winner by military strength
            double finalP1Strength = gameState.GetWeightedMilitaryStrength(player1ID);
            double finalP2Strength = gameState.GetWeightedMilitaryStrength(player2ID);
            p1Stats.finalMilitaryStrength = finalP1Strength;
            p2Stats.finalMilitaryStrength = finalP2Strength;

            int? winnerIndex;
            if (finalP1Strength > finalP2Strength * 1.2)
                winnerIndex = 0;
            else if (finalP2Strength > finalP1Strength * 1.2)
                winnerIndex = 1;
            else
                winnerIndex = null;

            return new GameResult(winnerIndex, GameEndReason.Timeout, simTime, p1Stats, p2Stats);
        }

        private static void PlaceStartingVillagers(
            GameState gameState, Guid ownerID, HexCoordinate cityPos, string name)
        {
            var spawnCoord = gameState.mapData.FindNearestWalkable(cityPos, 3, ownerID, gameState);
            var coord = spawnCoord ?? cityPos;
            var group = new VillagerGroupData(name, coord, 5, ownerID);
            gameState.AddVillagerGroup(group);
        }
    }
}
