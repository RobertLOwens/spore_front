// ============================================================================
// FILE: AI/AIMilitaryPlanner.cs
// PURPOSE: AI military planning - unit training, army deployment, attack
//          coordination, defense interception, retreat, and target scoring
//          C# port of AIMilitaryPlanner.swift
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
    public class AIMilitaryPlanner
    {
        // ================================================================
        // Configuration
        // ================================================================

        private readonly double trainInterval = GameConfig.AI.Intervals.MilitaryTrain;

        // ================================================================
        // Military Training Commands
        // ================================================================

        public List<IEngineCommand> GenerateMilitaryCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            // Training: gated by military train timer
            if (currentTime - aiState.lastMilitaryTrainTime >= trainInterval)
            {
                var militaryBuildingTypes = new HashSet<BuildingType>
                {
                    BuildingType.Barracks, BuildingType.ArcheryRange,
                    BuildingType.Stable, BuildingType.SiegeWorkshop
                };

                var militaryBuildings = gameState.GetBuildingsForPlayer(playerID)
                    .Where(b => militaryBuildingTypes.Contains(b.buildingType) && b.IsOperational && b.trainingQueue.Count == 0)
                    .ToList();

                bool trainedThisCycle = false;
                foreach (var building in militaryBuildings)
                {
                    var cmd = TryTrainMilitary(playerID, building.id, gameState, aiState);
                    if (cmd != null)
                    {
                        commands.Add(cmd);
                        trainedThisCycle = true;
                    }
                }

                if (trainedThisCycle)
                    aiState.lastMilitaryTrainTime = currentTime;
            }

            // Deployment: always checked
            var deployCmd = TryDeployArmy(playerID, gameState);
            if (deployCmd != null) commands.Add(deployCmd);

            return commands;
        }

        private IEngineCommand TryTrainMilitary(Guid playerID, Guid buildingID, GameState gameState, AIPlayerState aiState)
        {
            var building = gameState.GetBuilding(buildingID);
            if (building == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            var enemyAnalysis = aiState.lastEnemyAnalysis;
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

            MilitaryUnitType unitType;

            switch (building.buildingType)
            {
                case BuildingType.Barracks:
                    if (enemyAnalysis.HasValue && enemyAnalysis.Value.cavalryRatio > 0.35)
                        unitType = MilitaryUnitType.Pikeman;
                    else
                        unitType = MilitaryUnitType.Swordsman;
                    break;
                case BuildingType.ArcheryRange:
                    if (enemyAnalysis.HasValue && enemyAnalysis.Value.infantryRatio > 0.4)
                        unitType = MilitaryUnitType.Crossbow;
                    else
                        unitType = MilitaryUnitType.Archer;
                    break;
                case BuildingType.Stable:
                    if (enemyAnalysis.HasValue && enemyAnalysis.Value.rangedRatio > 0.4)
                        unitType = MilitaryUnitType.Knight;
                    else
                        unitType = MilitaryUnitType.Scout;
                    break;
                case BuildingType.SiegeWorkshop:
                    unitType = MilitaryUnitType.Mangonel;
                    break;
                default:
                    return null;
            }

            var trainingCost = unitType.TrainingCost();
            foreach (var kvp in trainingCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            return new AITrainMilitaryCommand(playerID, buildingID, unitType, 1);
        }

        private IEngineCommand TryDeployArmy(Guid playerID, GameState gameState)
        {
            var buildings = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => b.IsOperational && b.GetTotalGarrisonedUnits() >= 5)
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

            // Army coordination for hard difficulty
            if (aiState.difficulty.CoordinatesArmies() && idleArmies.Count > 1)
            {
                if (ShouldWaitForConvergence(idleArmies, target))
                {
                    var rallyPoint = CalculateRallyPoint(idleArmies, target);
                    aiState.pendingArmyConvergence = rallyPoint;

                    foreach (var army in idleArmies)
                    {
                        if (army.coordinate.Distance(rallyPoint) > 2)
                        {
                            commands.Add(new AIMoveCommand(playerID, army.id, rallyPoint, true));
                        }
                    }

                    bool allConverged = idleArmies.All(a => a.coordinate.Distance(rallyPoint) <= 2);
                    if (allConverged)
                    {
                        aiState.pendingArmyConvergence = null;
                        foreach (var army in idleArmies)
                        {
                            commands.Add(new AIMoveCommand(playerID, army.id, target, true));
                        }
                    }

                    return commands;
                }
            }

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

                double score = 50.0 - totalStrength + (20.0 / distance);

                if (totalStrength < 10) score += 15.0;
                if (army.isEntrenched) score -= 20.0;
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
                    case BuildingType.CityCenter: baseScore = 100.0; break;
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
        // Army Coordination
        // ================================================================

        private bool ShouldWaitForConvergence(List<ArmyData> armies, HexCoordinate target)
        {
            if (armies.Count < 2) return false;

            int maxDistance = 0;
            for (int i = 0; i < armies.Count; i++)
            {
                for (int j = i + 1; j < armies.Count; j++)
                {
                    int dist = armies[i].coordinate.Distance(armies[j].coordinate);
                    if (dist > maxDistance) maxDistance = dist;
                }
            }

            return maxDistance > 5;
        }

        private HexCoordinate CalculateRallyPoint(List<ArmyData> armies, HexCoordinate target)
        {
            ArmyData closest = armies[0];
            int closestDist = closest.coordinate.Distance(target);

            for (int i = 1; i < armies.Count; i++)
            {
                int d = armies[i].coordinate.Distance(target);
                if (d < closestDist)
                {
                    closest = armies[i];
                    closestDist = d;
                }
            }

            return closest.coordinate;
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

                if (isLocallyOutnumbered && distanceFromBase > 3)
                    shouldRetreat = true;

                if (army.GetTotalUnits() < 5 && distanceFromBase > 3)
                    shouldRetreat = true;

                if (aiState.currentState == AIState.Retreat && distanceFromBase > 3)
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

        public void UpdateEnemyAnalysis(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            if (currentTime - aiState.lastEnemyAnalysisTime < GameConfig.AI.Intervals.EnemyAnalysis) return;

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
    }
}
