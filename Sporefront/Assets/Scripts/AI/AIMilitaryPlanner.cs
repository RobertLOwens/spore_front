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

            var trainableUnits = GetTrainableUnitsForBuilding(building.buildingType);
            if (trainableUnits.Count == 0) return null;

            var ownComposition = CalculateOwnComposition(playerID, gameState);
            double gameTime = gameState.currentTime;

            // Score each trainable unit and pick the best
            MilitaryUnitType? bestUnit = null;
            double bestScore = double.MinValue;

            foreach (var unitType in trainableUnits)
            {
                var trainingCost = unitType.TrainingCost();
                bool canAfford = true;
                foreach (var kvp in trainingCost)
                {
                    if (!player.HasResource(kvp.Key, kvp.Value)) { canAfford = false; break; }
                }
                if (!canAfford) continue;

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
                // Infantry counters Cavalry, Ranged counters Infantry, Cavalry counters Ranged
                if (category == UnitCategory.Infantry && enemy.cavalryRatio > 0.3) score += 20.0;
                if (category == UnitCategory.Ranged && enemy.infantryRatio > 0.3) score += 20.0;
                if (category == UnitCategory.Cavalry && enemy.rangedRatio > 0.3) score += 20.0;
                // Pikeman gets extra bonus vs cavalry
                if (unitType == MilitaryUnitType.Pikeman && enemy.cavalryRatio > 0.25) score += 10.0;
            }

            // Diversity penalty/bonus
            double ownRatio = 0;
            ownComposition.TryGetValue(category, out ownRatio);
            if (ownRatio > 0.6) score -= 15.0;
            else if (ownRatio < 0.15) score += 10.0;

            // Tech bonus: reward units we have research bonuses for
            double techBonus = DamageCalculator.GetResearchDamageBonus(unitType, player);
            score += Math.Min(10.0, techBonus * 5.0);

            // Timing bonus
            bool earlyGame = gameTime < 300.0;
            if (earlyGame)
            {
                // Cheap units early
                if (unitType == MilitaryUnitType.Swordsman || unitType == MilitaryUnitType.Archer || unitType == MilitaryUnitType.Pikeman)
                    score += 8.0;
            }
            else
            {
                // Elite units late
                if (unitType == MilitaryUnitType.Knight || unitType == MilitaryUnitType.HeavyCavalry ||
                    unitType == MilitaryUnitType.Crossbow || unitType == MilitaryUnitType.Trebuchet)
                    score += 8.0;
            }

            // Scout priority: encourage scouts early if under cap
            if (unitType == MilitaryUnitType.Scout)
            {
                int scoutCount = CountScoutArmies(playerID, gameState);
                if (scoutCount < GameConfig.AI.Scouting.MaxScouts && earlyGame)
                    score += 25.0;
                else if (scoutCount >= GameConfig.AI.Scouting.MaxScouts)
                    score -= 30.0;
            }

            // Faction unit preferences
            var faction = player.faction;
            if (faction == FactionType.Morel)
            {
                if (category == UnitCategory.Infantry) score += 6.0;
                if (unitType == MilitaryUnitType.Scout) score += 5.0; // +1 vision makes scouts more valuable
                if (category == UnitCategory.Cavalry && unitType != MilitaryUnitType.Scout) score -= 5.0;
                if (category == UnitCategory.Siege) score -= 5.0;
            }
            else if (faction == FactionType.Muscaria)
            {
                if (category == UnitCategory.Ranged) score += 6.0;
                if (category == UnitCategory.Siege) score += 6.0;
                if (category == UnitCategory.Infantry) score -= 4.0;
                if (category == UnitCategory.Cavalry && unitType != MilitaryUnitType.Scout) score -= 4.0;
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

            // Muscaria aggression: poison DoT makes attacking armies more valuable
            var attackerFaction = gameState.GetPlayer(playerID)?.faction ?? FactionType.None;
            if (attackerFaction == FactionType.Muscaria)
            {
                for (int i = 0; i < scores.Count; i++)
                {
                    if (!scores[i].isBuilding)
                    {
                        var s = scores[i];
                        s.score += 8.0;
                        scores[i] = s;
                    }
                }
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
                        DebugLog.Log(string.Format("AI {0}: Found enemy base at ({1}, {2})", playerID, building.coordinate.q, building.coordinate.r));
                    }
                }
            }

            // Find scout armies
            var scoutArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => IsScoutArmy(a) && !a.isInCombat && a.currentPath == null)
                .ToList();

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
                    target = gameState.FindNearestUnexploredCoordinate(scout.coordinate, playerID, GameConfig.AI.Limits.ScoutRange);
                }
                else if (aiState.knownEnemyBases.Count > 0)
                {
                    // Patrol around known enemy base
                    var enemyBase = aiState.knownEnemyBases[0];
                    var patrolCoords = enemyBase.CoordinatesInRing(GameConfig.AI.Scouting.PatrolRadius);
                    if (patrolCoords.Count > 0)
                    {
                        // Pick a random patrol point
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
