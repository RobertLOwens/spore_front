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
                    .Where(b => militaryBuildingTypes.Contains(b.buildingType) && b.IsOperational && b.trainingQueue.Count == 0);

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

            // Get target composition based on map strategy, AI state, and faction counter
            // Round 4: Use dynamic counter composition when available
            TargetComposition targetComp = null;
            if (aiState.dynamicTargetComposition != null)
            {
                targetComp = aiState.dynamicTargetComposition;
            }
            else if (aiState.mapAnalyzed)
            {
                targetComp = TargetComposition.ForStrategy(aiState.mapStrategy, aiState.currentState);
                // Apply faction counter-strategy adjustments
                if (aiState.factionStrategy.HasValue)
                    targetComp = aiState.factionStrategy.Value.AdjustComposition(targetComp);
            }

            // Score each trainable unit and pick the best
            MilitaryUnitType? bestUnit = null;
            double bestScore = double.MinValue;

            foreach (var unitType in trainableUnits)
            {
                if (!player.CanAfford(unitType.TrainingCost())) continue;

                double score = ScoreUnitTraining(unitType, enemyAnalysis, ownComposition, player, gameState, playerID, gameTime, targetComp, aiState.gamePosition, aiState.siegeRequired && !aiState.siegeReady);

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
            Guid playerID, double gameTime, TargetComposition targetComp = null, GamePosition gamePosition = GamePosition.Even,
            bool siegeUrgent = false)
        {
            double score = 0;
            var category = unitType.Category();

            // Counter bonus: graduated scoring based on enemy composition ratios
            if (enemyAnalysis.HasValue)
            {
                var enemy = enemyAnalysis.Value;
                double strongThresh = GameConfig.AI.Composition.StrongCounterThreshold;
                double mildThresh = GameConfig.AI.Composition.MildCounterThreshold;
                double strongBonus = GameConfig.AI.Composition.StrongCounterBonus;
                double mildBonus = GameConfig.AI.Composition.MildCounterBonus;
                double penalty = GameConfig.AI.Composition.CounteredPenalty;

                // Graduated counter bonuses
                // Infantry counters Cavalry
                if (category == UnitCategory.Infantry)
                {
                    if (enemy.cavalryRatio > strongThresh) score += strongBonus;
                    else if (enemy.cavalryRatio > mildThresh) score += mildBonus;
                    // Pikeman specific bonus vs heavy cavalry
                    if (unitType == MilitaryUnitType.Pikeman && enemy.cavalryRatio > 0.25) score += 15.0;
                    // Penalized by ranged
                    if (enemy.rangedRatio > strongThresh) score -= penalty;
                }
                // Ranged counters Infantry
                if (category == UnitCategory.Ranged)
                {
                    if (enemy.infantryRatio > strongThresh) score += strongBonus;
                    else if (enemy.infantryRatio > mildThresh) score += mildBonus;
                    // Crossbow specific bonus vs heavy infantry
                    if (unitType == MilitaryUnitType.Crossbow && enemy.infantryRatio > 0.4) score += 10.0;
                    // Penalized by cavalry
                    if (enemy.cavalryRatio > strongThresh) score -= penalty;
                }
                // Cavalry counters Ranged
                if (category == UnitCategory.Cavalry)
                {
                    if (enemy.rangedRatio > strongThresh) score += strongBonus;
                    else if (enemy.rangedRatio > mildThresh) score += mildBonus;
                    // Penalized by infantry
                    if (enemy.infantryRatio > strongThresh) score -= penalty;
                }
                // Cavalry also counters Siege
                if (category == UnitCategory.Cavalry && enemy.siegeRatio > 0.2) score += 20.0;
            }

            // Composition-pull: score based on how underrepresented this category is vs target
            double ownRatio = 0;
            ownComposition.TryGetValue(category, out ownRatio);

            if (targetComp != null)
            {
                double targetRatio = targetComp.GetRatio(category);
                double deficit = targetRatio - ownRatio;
                // Positive deficit means we need more of this type
                // Scale: a 0.3 deficit gives +30 score, a -0.2 excess gives -20 score
                score += deficit * 100.0;
            }
            else
            {
                // Fallback diversity penalty/bonus when no target composition
                if (ownRatio > 0.6) score -= 15.0;
                else if (ownRatio < 0.15) score += 10.0;
            }

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

            // Faction unit preferences (data-driven via FactionAIConfig)
            var factionConfig = FactionAIConfig.Get(player.faction);
            double catBias;
            if (factionConfig.UnitCategoryBias.TryGetValue(category, out catBias))
            {
                // Scouts are exempt from negative cavalry bias
                if (unitType == MilitaryUnitType.Scout && catBias < 0)
                    catBias = 0;
                score += catBias;
            }
            double typeBias;
            if (factionConfig.UnitTypeBias.TryGetValue(unitType, out typeBias))
                score += typeBias;

            // Feature 4: Comeback — favor cheap units when behind
            if (gamePosition == GamePosition.Behind || gamePosition == GamePosition.CriticallyBehind)
            {
                // Cheap units are more cost-effective when rebuilding
                if (unitType == MilitaryUnitType.Swordsman || unitType == MilitaryUnitType.Pikeman
                    || unitType == MilitaryUnitType.Archer)
                    score += 15.0;

                // Expensive units are wasteful when behind
                if (unitType == MilitaryUnitType.Knight || unitType == MilitaryUnitType.HeavyCavalry
                    || unitType == MilitaryUnitType.Trebuchet)
                    score -= 15.0;

                // CriticallyBehind: only infantry (cheapest possible)
                if (gamePosition == GamePosition.CriticallyBehind && category != UnitCategory.Infantry)
                    score -= 20.0;
            }

            // Feature 6: Siege intelligence — prioritize siege when required but not ready
            if (siegeUrgent)
            {
                if (unitType == MilitaryUnitType.Mangonel || unitType == MilitaryUnitType.Trebuchet)
                    score += GameConfig.AI.Siege.SiegeTrainingBonus;
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
        // Idle Army Tasking — patrol and positioning
        // ================================================================

        public List<IEngineCommand> GenerateIdleArmyCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastPatrolTime, currentTime, 8.0)) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a))
                .ToList();

            if (idleArmies.Count == 0) return commands;

            // Keep at least one army near CC as garrison
            var ccCoord = cityCenter.coordinate;
            bool hasNearbyGuard = idleArmies.Any(a => a.coordinate.Distance(ccCoord) <= 3);

            if (aiState.currentState == AIState.Alert && aiState.knownEnemyBases.Count > 0)
            {
                // Alert: interpose armies between CC and known enemy direction
                var enemyBase = aiState.knownEnemyBases[0];
                double dirQ = enemyBase.q - ccCoord.q;
                double dirR = enemyBase.r - ccCoord.r;
                double len = Math.Max(1.0, Math.Sqrt(dirQ * dirQ + dirR * dirR));
                dirQ /= len;
                dirR /= len;

                foreach (var army in idleArmies)
                {
                    // Position 3-4 tiles out toward enemy
                    int targetQ = ccCoord.q + (int)Math.Round(dirQ * 3.5);
                    int targetR = ccCoord.r + (int)Math.Round(dirR * 3.5);
                    // Jitter to avoid stacking
                    int jitter = (army.id.GetHashCode() & 0x3) - 1;
                    var dest = new HexCoordinate(
                        Math.Max(0, Math.Min(gameState.mapData.width - 1, targetQ + jitter)),
                        Math.Max(0, Math.Min(gameState.mapData.height - 1, targetR)));

                    if (army.coordinate.Distance(dest) > 3)
                        commands.Add(new AIMoveCommand(playerID, army.id, dest, true));
                }
            }
            else if (aiState.currentState == AIState.Peace)
            {
                // Peace: patrol near resource camps to protect them
                var camps = gameState.GetBuildingsForPlayer(playerID)
                    .Where(b => (b.buildingType == BuildingType.LumberCamp || b.buildingType == BuildingType.MiningCamp)
                        && b.IsOperational && b.coordinate.Distance(ccCoord) <= 8)
                    .ToList();

                if (camps.Count == 0) return commands;

                // Leave one army near CC, send rest to patrol camps
                int startIdx = hasNearbyGuard ? 0 : 1;
                for (int i = startIdx; i < idleArmies.Count; i++)
                {
                    var army = idleArmies[i];
                    // Cycle through camps using army hash for deterministic but varied assignment
                    int campIdx = Math.Abs(army.id.GetHashCode() + (int)(currentTime / 30.0)) % camps.Count;
                    var campCoord = camps[campIdx].coordinate;

                    if (army.coordinate.Distance(campCoord) > 4)
                        commands.Add(new AIMoveCommand(playerID, army.id, campCoord, true));
                }
            }

            return commands;
        }

        // ================================================================
        // Zone Contest Commands — Domination / Crooked / Ring modes
        // ================================================================

        public List<IEngineCommand> GenerateZoneContestCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (gameState.controlZones == null || gameState.controlZones.Count == 0)
                return commands;

            if (!AIHelper.ShouldExecute(ref aiState.lastZoneContestTime, currentTime, 5.0)) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a))
                .ToList();

            if (idleArmies.Count == 0) return commands;

            // Keep at least one army near CC
            bool hasGuard = idleArmies.Any(a => a.coordinate.Distance(cityCenter.coordinate) <= 3);
            int availableCount = hasGuard ? idleArmies.Count : idleArmies.Count - 1;
            if (availableCount <= 0) return commands;

            // In Attack state, only dedicate half of idle armies to zones
            if (aiState.currentState == AIState.Attack)
                availableCount = Math.Max(1, availableCount / 2);

            // Score zones: enemy-controlled > neutral > own
            var zoneScores = new List<(ControlZoneData zone, double score)>();
            foreach (var zone in gameState.controlZones)
            {
                double score = 0.0;
                int distToCC = Math.Max(1, zone.center.Distance(cityCenter.coordinate));

                if (zone.controllingPlayerID.HasValue && zone.controllingPlayerID.Value != playerID)
                {
                    // Enemy-controlled: high priority
                    score = 40.0 + 20.0 / distToCC;
                }
                else if (!zone.controllingPlayerID.HasValue)
                {
                    // Neutral: medium priority
                    score = 25.0 + 15.0 / distToCC;
                }
                else
                {
                    // We control it — check if contested (enemies have presence)
                    if (zone.presenceCount != null)
                    {
                        bool contested = false;
                        foreach (var kvp in zone.presenceCount)
                        {
                            if (kvp.Key != playerID && kvp.Value > 0) { contested = true; break; }
                        }
                        if (contested)
                            score = 20.0 + 10.0 / distToCC;
                        else
                            continue; // Secure zone, skip
                    }
                    else
                        continue;
                }

                // Ring mode: inner zone (higher multiplier) is more valuable
                score *= zone.pointsMultiplier;

                zoneScores.Add((zone, score));
            }

            if (zoneScores.Count == 0) return commands;
            zoneScores.Sort((a, b) => b.score.CompareTo(a.score));

            // Assign idle armies to zones by priority
            int assigned = 0;
            foreach (var (zone, score) in zoneScores)
            {
                if (assigned >= availableCount) break;

                // Find closest idle army to this zone (skip the CC guard if needed)
                ArmyData bestArmy = null;
                int bestDist = int.MaxValue;
                foreach (var army in idleArmies)
                {
                    if (!hasGuard && army == idleArmies[0]) continue; // reserve first as guard
                    int dist = army.coordinate.Distance(zone.center);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestArmy = army;
                    }
                }

                if (bestArmy != null && bestDist > 1)
                {
                    commands.Add(new AIMoveCommand(playerID, bestArmy.id, zone.center, true));
                    idleArmies.Remove(bestArmy);
                    assigned++;
                }
            }

            return commands;
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
                var targets = ScoreAllTargets(playerID, gameState, cityCenter.coordinate, aiState.factionStrategy, aiState.enemyStrategy);
                if (targets.Count > 0)
                {
                    targetCoordinate = targets[0].coordinate;
                    aiState.persistentAttackTargetID = targets[0].targetID;
                }
            }

            if (!targetCoordinate.HasValue) return commands;
            var target = targetCoordinate.Value;

            // Feature 2: Turtle detection — only attack if we have siege units
            if (aiState.enemyStrategy == EnemyStrategyRead.Turtle)
            {
                bool hasSiege = idleArmies.Any(a => IsSiegeArmy(a) || a.GetUnitCount(MilitaryUnitType.Mangonel) > 0
                    || a.GetUnitCount(MilitaryUnitType.Trebuchet) > 0);
                if (!hasSiege) return commands; // Don't attack turtles without siege
            }

            // Feature 4: Comeback — when behind, only attack with merged strong army
            if (aiState.gamePosition == GamePosition.Behind || aiState.gamePosition == GamePosition.CriticallyBehind)
            {
                bool hasStrongArmy = idleArmies.Any(a => a.GetTotalUnits() >= 12);
                if (!hasStrongArmy) return commands; // Don't attack piecemeal when behind
            }

            // Flanking: attempt with 30% probability when 2+ armies available
            if (idleArmies.Count >= 2)
            {
                // Use a deterministic hash to get ~30% chance per decision cycle
                int flankHash = (int)(currentTime * 7) ^ playerID.GetHashCode();
                if ((flankHash % 10) < 3) // ~30%
                {
                    var flankCommands = TryFlankAttack(aiState, gameState, idleArmies, target);
                    if (flankCommands != null)
                    {
                        commands.AddRange(flankCommands);
                        aiState.lastAttackTarget = target;
                        return commands;
                    }
                }
            }

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
                // Siege timing: hold siege armies back if they lack escort
                if (IsSiegeArmy(army) && !ShouldDeploySiege(army, idleArmies, target, gameState, playerID))
                {
                    // Keep siege near CC until escort is available
                    var cc = gameState.GetCityCenter(playerID);
                    if (cc != null && army.coordinate.Distance(cc.coordinate) > 4)
                        commands.Add(new AIMoveCommand(playerID, army.id, cc.coordinate, true));
                    continue;
                }

                commands.Add(new AIMoveCommand(playerID, army.id, target, true));
            }

            aiState.lastAttackTarget = target;
            return commands;
        }

        // ================================================================
        // Target Scoring
        // ================================================================

        public List<TargetScore> ScoreAllTargets(Guid playerID, GameState gameState, HexCoordinate from, FactionStrategy? factionStrategy = null, EnemyStrategyRead enemyStrategy = EnemyStrategyRead.Unknown)
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

                // Faction adaptation: deprioritize targets on unfavorable terrain
                if (factionStrategy.HasValue && factionStrategy.Value.preferOpenTerrain)
                {
                    var terrain = gameState.mapData.GetTerrain(army.coordinate);
                    if (terrain == TerrainType.Mountain) score -= 15.0;
                    else if (terrain == TerrainType.Hill) score -= 8.0;
                }

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

                // Feature 2: Greed detection — adjust target priorities
                if (enemyStrategy == EnemyStrategyRead.Turtle)
                {
                    // Avoid targets near defensive structures
                    bool isDefensive = building.buildingType == BuildingType.Tower
                        || building.buildingType == BuildingType.WoodenFort
                        || building.buildingType == BuildingType.Castle;
                    if (isDefensive) score -= 25.0;

                    // Boost economic targets — outeconomy turtles
                    bool isEconomic = building.buildingType == BuildingType.Farm
                        || building.buildingType == BuildingType.LumberCamp
                        || building.buildingType == BuildingType.MiningCamp;
                    if (isEconomic) score += 15.0;
                }

                scores.Add(new TargetScore(building.id, building.coordinate, score, true));
            }

            // Faction aggression bonus for army targets (e.g. Muscaria poison DoT)
            var attackerConfig = FactionAIConfig.Get(gameState.GetPlayer(playerID)?.faction ?? FactionType.None);
            if (attackerConfig.ArmyTargetBonus > 0)
            {
                for (int i = 0; i < scores.Count; i++)
                {
                    if (!scores[i].isBuilding)
                    {
                        var s = scores[i];
                        s.score += attackerConfig.ArmyTargetBonus;
                        scores[i] = s;
                    }
                }
            }

            scores.Sort((a, b) => b.score.CompareTo(a.score));
            return scores;
        }

        // ================================================================
        // Siege Timing
        // ================================================================

        /// <summary>
        /// Determines if a siege army should be deployed to attack or held back.
        /// Siege units should only move when escorted by at least 2 non-siege armies nearby.
        /// </summary>
        private bool ShouldDeploySiege(ArmyData siegeArmy, List<ArmyData> allArmies, HexCoordinate target, GameState gameState, Guid playerID)
        {
            // Only deploy siege when in Attack state — handled by caller
            // Check CC level requirement
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null || cityCenter.level < 2) return false;

            // Check for escort: at least 2 non-siege armies within 3 tiles of siege army
            int escortCount = 0;
            foreach (var army in allArmies)
            {
                if (army.id == siegeArmy.id) continue;
                if (IsSiegeArmy(army)) continue;
                if (army.coordinate.Distance(siegeArmy.coordinate) <= 3)
                    escortCount++;
            }

            return escortCount >= 2;
        }

        private bool IsSiegeArmy(ArmyData army)
        {
            int totalUnits = army.GetTotalUnits();
            if (totalUnits == 0) return false;
            int siegeCount = army.GetUnitCount(MilitaryUnitType.Mangonel) + army.GetUnitCount(MilitaryUnitType.Trebuchet);
            return siegeCount > 0 && (double)siegeCount / totalUnits >= 0.5;
        }

        // ================================================================
        // Feature 6: Siege Timing Intelligence
        // ================================================================

        /// <summary>
        /// Evaluates whether the AI needs siege units to attack the current target.
        /// Checks visible enemy defensive buildings count.
        /// </summary>
        public void EvaluateSiegeRequirement(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var visibleEnemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);

            int towers = 0, forts = 0, castles = 0;
            foreach (var building in visibleEnemyBuildings)
            {
                switch (building.buildingType)
                {
                    case BuildingType.Tower: towers++; break;
                    case BuildingType.WoodenFort: forts++; break;
                    case BuildingType.Castle: castles++; break;
                }
            }

            int totalDefensive = towers + forts + castles;
            bool wasSiegeRequired = aiState.siegeRequired;

            if (totalDefensive >= GameConfig.AI.Siege.DefenseBuildingThreshold || aiState.enemyStrategy == EnemyStrategyRead.Turtle)
            {
                aiState.siegeRequired = true;
                aiState.requiredSiegeCount = towers * GameConfig.AI.Siege.SiegePerTower
                    + forts * GameConfig.AI.Siege.SiegePerFort
                    + castles * GameConfig.AI.Siege.SiegePerFort;
                aiState.requiredSiegeCount = Math.Max(1, aiState.requiredSiegeCount);

                if (!wasSiegeRequired)
                {
                    aiState.siegeRequirementDetectedTime = gameState.currentTime;
                    DebugLog.Log($"AI {playerID}: Siege required — {totalDefensive} defensive buildings detected, need {aiState.requiredSiegeCount} siege units");
                }
            }
            else
            {
                aiState.siegeRequired = false;
                aiState.siegeReady = false;
            }
        }

        /// <summary>
        /// Checks if the AI has enough siege units to meet the requirement.
        /// </summary>
        public void CheckSiegeReadiness(AIPlayerState aiState, GameState gameState)
        {
            if (!aiState.siegeRequired) { aiState.siegeReady = false; return; }

            var playerID = aiState.playerID;
            int siegeCount = 0;
            foreach (var army in gameState.GetArmiesForPlayer(playerID))
            {
                siegeCount += army.GetUnitCount(MilitaryUnitType.Mangonel)
                            + army.GetUnitCount(MilitaryUnitType.Trebuchet);
            }

            bool wasReady = aiState.siegeReady;
            aiState.siegeReady = siegeCount >= aiState.requiredSiegeCount;

            if (aiState.siegeReady && !wasReady)
                DebugLog.Log($"AI {playerID}: Siege ready — {siegeCount}/{aiState.requiredSiegeCount} siege units built");
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
        // Flanking
        // ================================================================

        /// <summary>
        /// Attempts a flanking attack with 2+ armies. The largest army attacks directly
        /// while the second army approaches from a perpendicular direction.
        /// Returns commands if flanking was attempted, null otherwise.
        /// </summary>
        private List<IEngineCommand> TryFlankAttack(AIPlayerState aiState, GameState gameState,
            List<ArmyData> availableArmies, HexCoordinate target)
        {
            if (availableArmies.Count < 2) return null;
            var playerID = aiState.playerID;

            // Sort by army size — largest attacks directly, second flanks
            var sorted = availableArmies.OrderByDescending(a => a.GetTotalUnits()).ToList();
            var mainArmy = sorted[0];
            var flankArmy = sorted[1];

            // Only attempt if both are reasonably close (≤15 tiles)
            if (mainArmy.coordinate.Distance(target) > 15) return null;
            if (flankArmy.coordinate.Distance(target) > 15) return null;

            // Find flanking position: a tile 2-3 hexes from target, perpendicular to the main approach
            var ring = target.CoordinatesInRing(2);
            HexCoordinate? bestFlankPos = null;
            int bestFlankDist = 0;

            // The main army's approach line
            var mainLine = mainArmy.coordinate.LineTo(target);
            var mainLineSet = new HashSet<HexCoordinate>(mainLine);

            foreach (var pos in ring)
            {
                if (!gameState.mapData.IsValidCoordinate(pos)) continue;
                if (!gameState.mapData.IsWalkable(pos)) continue;

                // Pick position farthest from main army's approach line
                int minDistToLine = int.MaxValue;
                foreach (var linePoint in mainLine)
                {
                    int d = pos.Distance(linePoint);
                    if (d < minDistToLine) minDistToLine = d;
                }

                if (minDistToLine > bestFlankDist)
                {
                    bestFlankDist = minDistToLine;
                    bestFlankPos = pos;
                }
            }

            if (!bestFlankPos.HasValue || bestFlankDist < 2) return null;

            var commands = new List<IEngineCommand>
            {
                new AIMoveCommand(playerID, mainArmy.id, target, true),
                new AIMoveCommand(playerID, flankArmy.id, bestFlankPos.Value, true)
            };

            DebugLog.Log($"AI {playerID}: Flanking attack — main to ({target.q},{target.r}), flank to ({bestFlankPos.Value.q},{bestFlankPos.Value.r})");

            return commands;
        }

        // ================================================================
        // Feature 2: Feint / Bluffing
        // ================================================================

        /// <summary>
        /// Sends a small army toward the enemy as a diversion, then attacks from the
        /// opposite direction with the main force when the enemy responds to the feint.
        /// </summary>
        public List<IEngineCommand> GenerateFeintCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            // Manage active feint
            if (aiState.feintInProgress)
            {
                return ManageActiveFeint(aiState, gameState, currentTime);
            }

            // Cooldown check
            if (currentTime - aiState.lastFeintAttemptTime < GameConfig.AI.Feint.FeintCooldown)
                return commands;

            // Need known enemy base
            if (aiState.knownEnemyBases.Count == 0) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a)
                    && !aiState.activeRaidArmies.Contains(a.id))
                .OrderBy(a => a.GetTotalUnits()) // Smallest first (feint candidate)
                .ToList();

            if (idleArmies.Count < GameConfig.AI.Feint.MinArmiesForFeint) return commands;

            var feintArmy = idleArmies[0]; // Smallest army = feint
            var enemyCC = aiState.knownEnemyBases[0];

            // Send feint toward enemy CC
            commands.Add(new AIMoveCommand(playerID, feintArmy.id, enemyCC, true));

            aiState.activeFeintArmyID = feintArmy.id;
            aiState.feintTarget = enemyCC;
            aiState.feintStartTime = currentTime;
            aiState.feintInProgress = true;
            aiState.lastFeintAttemptTime = currentTime;

            DebugLog.Log($"AI {playerID}: Feint initiated — sending {feintArmy.GetTotalUnits()}-unit army toward enemy CC at ({enemyCC.q},{enemyCC.r})");

            return commands;
        }

        private List<IEngineCommand> ManageActiveFeint(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var feintArmy = aiState.activeFeintArmyID.HasValue ? gameState.GetArmy(aiState.activeFeintArmyID.Value) : null;

            // Feint army destroyed or timeout
            if (feintArmy == null || currentTime - aiState.feintStartTime > GameConfig.AI.Feint.FeintDuration)
            {
                aiState.feintInProgress = false;
                aiState.activeFeintArmyID = null;
                return commands;
            }

            // Check if feint army needs to retreat (enemy within retreat radius)
            var nearbyEnemies = gameState.GetEnemyArmies(feintArmy.coordinate, GameConfig.AI.Feint.FeintRetreatRadius, playerID);
            bool enemyResponded = nearbyEnemies.Count > 0;

            if (enemyResponded)
            {
                // Enemy took the bait! Retreat feint army
                var cityCenter = gameState.GetCityCenter(playerID);
                if (cityCenter != null && !feintArmy.isInCombat)
                {
                    commands.Add(new AIMoveCommand(playerID, feintArmy.id, cityCenter.coordinate, true));
                }

                // Send main force from opposite direction
                if (aiState.knownEnemyBases.Count > 0)
                {
                    var enemyCC = aiState.knownEnemyBases[0];
                    var ccCoord = cityCenter?.coordinate ?? feintArmy.coordinate;
                    var oppositeApproach = CalculateOppositeApproach(enemyCC, feintArmy.coordinate, ccCoord, gameState);

                    var mainArmies = gameState.GetArmiesForPlayer(playerID)
                        .Where(a => !a.isInCombat && a.currentPath == null
                            && a.id != feintArmy.id && !IsScoutArmy(a)
                            && !aiState.activeRaidArmies.Contains(a.id)
                            && a.GetTotalUnits() >= 5)
                        .OrderByDescending(a => a.GetTotalUnits());

                    int mainArmyCount = 0;
                    foreach (var army in mainArmies)
                    {
                        // Send to opposite approach, then they'll path to enemy CC
                        commands.Add(new AIMoveCommand(playerID, army.id, oppositeApproach, true));
                        mainArmyCount++;
                    }

                    if (mainArmyCount > 0)
                    {
                        DebugLog.Log($"AI {playerID}: Feint successful! Enemy responded — sending {mainArmyCount} armies from opposite direction ({oppositeApproach.q},{oppositeApproach.r})");
                    }
                }

                aiState.feintInProgress = false;
                aiState.activeFeintArmyID = null;
                return commands;
            }

            return commands; // Feint still in progress, wait
        }

        /// <summary>
        /// Calculates an approach position roughly opposite to the feint direction.
        /// </summary>
        private HexCoordinate CalculateOppositeApproach(HexCoordinate enemyCC, HexCoordinate feintPos, HexCoordinate ourCC, GameState gameState)
        {
            // Feint direction vector (from enemy CC to feint position)
            int feintDirQ = feintPos.q - enemyCC.q;
            int feintDirR = feintPos.r - enemyCC.r;

            // Opposite direction: flip the vector
            int oppositeQ = enemyCC.q - feintDirQ;
            int oppositeR = enemyCC.r - feintDirR;

            // Place the approach point 3 tiles from enemy CC in the opposite direction
            double len = Math.Max(1.0, Math.Sqrt(feintDirQ * feintDirQ + feintDirR * feintDirR));
            int approachQ = enemyCC.q + (int)((-feintDirQ / len) * 3);
            int approachR = enemyCC.r + (int)((-feintDirR / len) * 3);

            // Clamp to map bounds
            approachQ = Math.Max(0, Math.Min(gameState.mapData.width - 1, approachQ));
            approachR = Math.Max(0, Math.Min(gameState.mapData.height - 1, approachR));

            return new HexCoordinate(approachQ, approachR);
        }

        // ================================================================
        // Retreat Commands
        // ================================================================

        /// <summary>
        /// Finds the best retreat destination for an army.
        /// Priority: nearest fort/tower → nearest chokepoint → city center.
        /// </summary>
        private HexCoordinate FindRetreatDestination(GameState gameState, ArmyData army, Guid playerID, List<ChokepointData> chokepoints)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            var ccCoord = cityCenter?.coordinate ?? army.coordinate;

            // Priority 1: Nearest friendly fort or tower within 8 tiles
            var defensiveBuildings = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => (b.buildingType == BuildingType.WoodenFort || b.buildingType == BuildingType.Tower ||
                             b.buildingType == BuildingType.Castle) && b.IsOperational);

            BuildingData bestDefense = null;
            int bestDefenseDist = int.MaxValue;
            foreach (var building in defensiveBuildings)
            {
                int dist = army.coordinate.Distance(building.coordinate);
                if (dist <= 8 && dist < bestDefenseDist)
                {
                    bestDefenseDist = dist;
                    bestDefense = building;
                }
            }

            if (bestDefense != null)
                return bestDefense.coordinate;

            // Priority 2: Nearest chokepoint between army and CC
            if (chokepoints != null && chokepoints.Count > 0)
            {
                ChokepointData? bestChoke = null;
                int bestChokeDist = int.MaxValue;
                foreach (var choke in chokepoints)
                {
                    // Only retreat to chokepoints that are between us and CC (closer to CC than we are)
                    int chokeToCC = choke.center.Distance(ccCoord);
                    int armyToCC = army.coordinate.Distance(ccCoord);
                    if (chokeToCC < armyToCC)
                    {
                        int dist = army.coordinate.Distance(choke.center);
                        if (dist < bestChokeDist)
                        {
                            bestChokeDist = dist;
                            bestChoke = choke;
                        }
                    }
                }

                if (bestChoke.HasValue)
                    return bestChoke.Value.center;
            }

            // Priority 3: City center (fallback)
            return ccCoord;
        }

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
                    var destination = FindRetreatDestination(gameState, army, playerID, aiState.cachedChokepoints);
                    commands.Add(new AIMoveCommand(playerID, army.id, destination, true));
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
                        DebugLog.Log($"AI {playerID}: Found enemy base at ({building.coordinate.q}, {building.coordinate.r})");
                    }
                }
            }

            // Feature 1: Early game deliberate scouting — send ANY fast army to explore
            if (!aiState.earlyScoutDispatched && gameState.currentTime < GameConfig.AI.Scouting.EarlyScoutWindow && gameState.currentTime >= GameConfig.AI.Scouting.EarlyScoutTime)
            {
                // Find the fastest idle army (prefer scouts, but use any army)
                ArmyData earlyScoutCandidate = null;
                int bestScore = -1;
                foreach (var a in gameState.GetArmiesForPlayer(playerID))
                {
                    if (a.isInCombat || a.currentPath != null || a.GetTotalUnits() > GameConfig.AI.Hunting.MaxScoutCandidateUnits) continue;
                    int score = (IsScoutArmy(a) ? 2 : 0) + (IsFastArmy(a) ? 1 : 0);
                    if (score > bestScore) { bestScore = score; earlyScoutCandidate = a; }
                }

                if (earlyScoutCandidate != null)
                {
                    // Send toward map center first, then spiral out
                    var mapCenter = new HexCoordinate(gameState.mapData.width / 2, gameState.mapData.height / 2);
                    var target = gameState.FindNearestUnexploredCoordinate(earlyScoutCandidate.coordinate, playerID, 15);
                    if (!target.HasValue)
                        target = mapCenter;

                    commands.Add(new AIMoveCommand(playerID, earlyScoutCandidate.id, target.Value, true));
                    aiState.earlyScoutDispatched = true;
                    DebugLog.Log($"AI {playerID}: Early scout dispatched to ({target.Value.q},{target.Value.r})");
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
                    target = gameState.FindNearestUnexploredCoordinate(scout.coordinate, playerID, GameConfig.AI.Limits.ScoutRange);
                }
                else if (aiState.mapExplorationPercent < 0.5 && aiState.knownEnemyBases.Count > 0)
                {
                    // Continue exploring uncharted areas even after finding enemy
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

        // ================================================================
        // Feature 1: Raiding Parties
        // ================================================================

        /// <summary>
        /// Generates raid commands using fast cavalry armies to harass enemy economy.
        /// Targets enemy resource camps and villager groups far from the enemy CC.
        /// </summary>
        public List<IEngineCommand> GenerateRaidCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastRaidTime < GameConfig.AI.Raiding.RaidInterval) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            // Clean up stale raid army references
            aiState.activeRaidArmies.RemoveWhere(id => gameState.GetArmy(id) == null);

            // Find eligible raid armies: fast cavalry-heavy units, not in combat, not already raiding
            var raidCandidates = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null
                    && !aiState.activeRaidArmies.Contains(a.id)
                    && !IsSiegeArmy(a) && !IsScoutArmy(a)
                    && IsFastArmy(a)
                    && a.GetTotalUnits() >= GameConfig.AI.Raiding.MinCavalryForRaid
                    && a.GetTotalUnits() <= 8) // Small armies only — don't waste main force
                .ToList();

            if (raidCandidates.Count == 0) return commands;

            // Find enemy economic targets: resource camps far from their CC
            var enemyTargets = new List<(HexCoordinate coord, double score)>();

            var visibleEnemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            HexCoordinate? enemyCCCoord = null;
            foreach (var building in visibleEnemyBuildings)
            {
                if (building.buildingType == BuildingType.CityCenter)
                {
                    enemyCCCoord = building.coordinate;
                    break;
                }
            }

            foreach (var building in visibleEnemyBuildings)
            {
                // Target resource camps and farms (economic targets)
                bool isEconomicTarget = building.buildingType == BuildingType.LumberCamp
                    || building.buildingType == BuildingType.MiningCamp
                    || building.buildingType == BuildingType.Farm;

                if (!isEconomicTarget) continue;

                int distFromUs = building.coordinate.Distance(cityCenter.coordinate);
                if (distFromUs > GameConfig.AI.Raiding.MaxRaidDistance) continue;

                double score = 30.0;
                // Prefer targets far from enemy CC (less defended)
                if (enemyCCCoord.HasValue)
                {
                    int distFromEnemyCC = building.coordinate.Distance(enemyCCCoord.Value);
                    score += distFromEnemyCC * 3.0;
                }
                // Avoid areas where we were recently defeated
                if (aiState.threatMemory.ContainsKey(building.coordinate))
                {
                    var memory = aiState.threatMemory[building.coordinate];
                    if (!memory.IsStale(currentTime) && memory.wasDefeatedHere)
                        score -= 40.0;
                    else if (!memory.IsStale(currentTime))
                        score -= memory.estimatedStrength * 2.0;
                }

                // Check for nearby enemy armies — skip heavily defended targets
                var nearbyEnemies = gameState.GetEnemyArmies(building.coordinate, 3, playerID);
                if (nearbyEnemies.Count > 0)
                {
                    int totalEnemyUnits = 0;
                    foreach (var enemy in nearbyEnemies)
                        totalEnemyUnits += enemy.GetTotalUnits();
                    score -= totalEnemyUnits * 3.0;
                }

                if (score > 0)
                    enemyTargets.Add((building.coordinate, score));
            }

            if (enemyTargets.Count == 0) return commands;
            enemyTargets.Sort((a, b) => b.score.CompareTo(a.score));

            // Send the best raid candidate to the best target
            var raider = raidCandidates[0];
            var raidTarget = enemyTargets[0].coord;

            commands.Add(new AIMoveCommand(playerID, raider.id, raidTarget, true));
            aiState.activeRaidArmies.Add(raider.id);
            aiState.lastRaidTime = currentTime;

            DebugLog.Log($"AI {playerID}: Sending raid party ({raider.GetTotalUnits()} units) to ({raidTarget.q},{raidTarget.r})");

            return commands;
        }

        /// <summary>
        /// Generates flee commands for active raid armies that are near enemy defenders.
        /// </summary>
        public List<IEngineCommand> GenerateRaidFleeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var toRemove = new List<Guid>();
            foreach (var raidArmyID in aiState.activeRaidArmies)
            {
                var army = gameState.GetArmy(raidArmyID);
                if (army == null) { toRemove.Add(raidArmyID); continue; }

                // Check for nearby enemy armies — flee if threatened
                var nearbyEnemies = gameState.GetEnemyArmies(army.coordinate, GameConfig.AI.Raiding.FleeRadius, playerID);
                bool shouldFlee = false;

                if (nearbyEnemies.Count > 0)
                {
                    int totalEnemyUnits = 0;
                    foreach (var enemy in nearbyEnemies)
                        totalEnemyUnits += enemy.GetTotalUnits();

                    // Flee if enemy force is significant
                    if (totalEnemyUnits > army.GetTotalUnits() * 0.7)
                        shouldFlee = true;
                }

                if (shouldFlee && !army.isInCombat)
                {
                    // Retreat toward our CC
                    commands.Add(new AIMoveCommand(playerID, army.id, cityCenter.coordinate, true));
                    toRemove.Add(raidArmyID);
                    DebugLog.Log($"AI {playerID}: Raid party fleeing from ({army.coordinate.q},{army.coordinate.r})");
                }
            }

            foreach (var id in toRemove)
                aiState.activeRaidArmies.Remove(id);

            return commands;
        }

        /// <summary>
        /// Updates the cached map exploration percentage for the AI player.
        /// </summary>
        public void UpdateExplorationPercent(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            if (currentTime - aiState.lastExplorationUpdate < GameConfig.AI.Scouting.ExplorationUpdateInterval)
                return;
            aiState.lastExplorationUpdate = currentTime;

            var player = gameState.GetPlayer(aiState.playerID);
            if (player == null) return;

            int exploredCount = player.exploredCoordinates != null ? player.exploredCoordinates.Count : 0;
            int totalTiles = gameState.mapData.width * gameState.mapData.height;
            if (totalTiles > 0)
                aiState.mapExplorationPercent = (double)exploredCount / totalTiles;
        }

        /// <summary>
        /// Checks if an army is cavalry-heavy or has fast units (good for raiding).
        /// </summary>
        private bool IsFastArmy(ArmyData army)
        {
            int totalUnits = army.GetTotalUnits();
            if (totalUnits == 0) return false;
            int fastUnits = army.GetUnitCount(MilitaryUnitType.Scout)
                + army.GetUnitCount(MilitaryUnitType.Knight)
                + army.GetUnitCount(MilitaryUnitType.HeavyCavalry);
            return (double)fastUnits / totalUnits >= 0.5;
        }

        // ================================================================
        // Feature 2: Threat Memory
        // ================================================================

        /// <summary>
        /// Updates threat memory with currently visible enemy armies and cleans up stale entries.
        /// Called each decision cycle to maintain an awareness of where enemies have been.
        /// </summary>
        public void UpdateThreatMemory(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return;

            // Record all visible enemy army positions
            foreach (var army in gameState.armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;
                var status = gameState.GetDiplomacyStatus(playerID, army.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                // Only record if in our vision
                if (player.IsVisible(army.coordinate))
                {
                    aiState.threatMemory[army.coordinate] = new ThreatMemoryEntry(
                        army.coordinate,
                        army.ownerID.Value,
                        army.GetTotalUnits(),
                        currentTime
                    );
                }
            }

            // Mark locations where our armies were destroyed as defeat sites
            foreach (var raidID in aiState.activeRaidArmies.ToList())
            {
                if (gameState.GetArmy(raidID) == null)
                {
                    // Army was destroyed — we don't know the exact coord but remove from active raids
                    aiState.activeRaidArmies.Remove(raidID);
                }
            }

            // Periodic cleanup of stale entries
            if (currentTime - aiState.lastThreatMemoryCleanup >= GameConfig.AI.ThreatMemory.CleanupInterval)
            {
                var staleKeys = new List<HexCoordinate>();
                foreach (var kvp in aiState.threatMemory)
                {
                    if (kvp.Value.IsStale(currentTime, GameConfig.AI.ThreatMemory.DecayTime))
                        staleKeys.Add(kvp.Key);
                }
                foreach (var key in staleKeys)
                    aiState.threatMemory.Remove(key);

                aiState.lastThreatMemoryCleanup = currentTime;
            }
        }

        /// <summary>
        /// Estimates current enemy military strength from threat memory.
        /// Returns a more informed estimate than just looking at currently visible enemies.
        /// </summary>
        public int EstimateEnemyStrengthFromMemory(AIPlayerState aiState, double currentTime)
        {
            int totalEstimated = 0;
            foreach (var entry in aiState.threatMemory.Values)
            {
                if (!entry.IsStale(currentTime, GameConfig.AI.ThreatMemory.DecayTime))
                {
                    // Recent memories are more reliable
                    double recency = 1.0 - ((currentTime - entry.lastSeenTime) / GameConfig.AI.ThreatMemory.DecayTime);
                    totalEstimated += (int)(entry.estimatedStrength * recency);
                }
            }
            return totalEstimated;
        }

        /// <summary>
        /// Checks if the enemy main army has recently left an area, creating an attack opportunity.
        /// Returns the coordinates of a recently vacated position if one exists.
        /// </summary>
        public HexCoordinate? FindOpportunityWindow(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            foreach (var entry in aiState.threatMemory.Values)
            {
                double timeSinceSeen = currentTime - entry.lastSeenTime;

                // The enemy was here recently but has moved away — it's an opportunity
                if (timeSinceSeen > 3.0 && timeSinceSeen < GameConfig.AI.ThreatMemory.OpportunityWindow
                    && entry.estimatedStrength >= 5)
                {
                    // Verify the enemy is no longer there
                    var currentEnemies = gameState.GetEnemyArmies(entry.coordinate, 2, playerID);
                    if (currentEnemies.Count == 0)
                        return entry.coordinate;
                }
            }

            return null;
        }

        // ================================================================
        // Feature 4: Multi-Objective Attacks
        // ================================================================

        /// <summary>
        /// When the AI has 3+ armies, assigns them to different strategic objectives simultaneously:
        /// main force hits primary target, secondary force attacks economy, third contests a zone.
        /// </summary>
        public List<IEngineCommand> GenerateMultiObjectiveAttack(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastMultiObjectiveTime < 10.0) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a)
                    && !aiState.activeRaidArmies.Contains(a.id))
                .OrderByDescending(a => a.GetTotalUnits())
                .ToList();

            // Need 3+ armies for multi-objective
            if (idleArmies.Count < 3) return commands;

            var targets = ScoreAllTargets(playerID, gameState, cityCenter.coordinate, aiState.factionStrategy, aiState.enemyStrategy);
            if (targets.Count == 0) return commands;

            // Objective 1: Primary target — strongest army
            var primaryTarget = targets[0];
            var primaryArmy = idleArmies[0];
            commands.Add(new AIMoveCommand(playerID, primaryArmy.id, primaryTarget.coordinate, true));
            aiState.persistentAttackTargetID = primaryTarget.targetID;

            // Objective 2: Economic target — second army hits a different location
            BuildingData econTarget = null;
            var visibleEnemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            foreach (var building in visibleEnemyBuildings)
            {
                bool isEconomic = building.buildingType == BuildingType.LumberCamp
                    || building.buildingType == BuildingType.MiningCamp
                    || building.buildingType == BuildingType.Farm;

                if (isEconomic && building.coordinate.Distance(primaryTarget.coordinate) > 5)
                {
                    econTarget = building;
                    break;
                }
            }

            if (econTarget != null)
            {
                commands.Add(new AIMoveCommand(playerID, idleArmies[1].id, econTarget.coordinate, true));
            }
            else if (targets.Count > 1)
            {
                // Fallback: second target from target scoring
                commands.Add(new AIMoveCommand(playerID, idleArmies[1].id, targets[1].coordinate, true));
            }

            // Objective 3: Zone contest or tertiary target — third army
            bool sentToZone = false;
            if (gameState.controlZones != null && gameState.controlZones.Count > 0)
            {
                // Find best zone to contest
                ControlZoneData bestZone = null;
                double bestZoneScore = 0;
                foreach (var zone in gameState.controlZones)
                {
                    if (zone.controllingPlayerID.HasValue && zone.controllingPlayerID.Value != playerID)
                    {
                        double score = 40.0 * zone.pointsMultiplier;
                        if (score > bestZoneScore)
                        {
                            bestZoneScore = score;
                            bestZone = zone;
                        }
                    }
                }

                if (bestZone != null)
                {
                    commands.Add(new AIMoveCommand(playerID, idleArmies[2].id, bestZone.center, true));
                    sentToZone = true;
                }
            }

            if (!sentToZone && targets.Count > 2)
            {
                commands.Add(new AIMoveCommand(playerID, idleArmies[2].id, targets[2].coordinate, true));
            }

            aiState.lastMultiObjectiveTime = currentTime;
            DebugLog.Log($"AI {playerID}: Multi-objective attack with {Math.Min(3, idleArmies.Count)} armies");

            return commands;
        }

        // ================================================================
        // Feature 5: Defensive Staging & Rally Points
        // ================================================================

        /// <summary>
        /// Computes rally points at strategic locations: chokepoints, midpoints between CC
        /// and known enemy positions, and near domination zones. Armies stage here during
        /// Alert state instead of sitting idle at CC.
        /// </summary>
        public void ComputeRallyPoints(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return;

            var ccCoord = cityCenter.coordinate;
            var points = new List<(HexCoordinate coord, double priority)>();

            // Rally near chokepoints
            if (aiState.cachedChokepoints != null)
            {
                foreach (var choke in aiState.cachedChokepoints)
                {
                    // Only use chokepoints within reasonable distance
                    int dist = choke.center.Distance(ccCoord);
                    if (dist <= 10 && dist >= 3)
                    {
                        points.Add((choke.center, 30.0 + (10.0 - dist)));
                    }
                }
            }

            // Rally toward known enemy bases (midpoint)
            foreach (var enemyBase in aiState.knownEnemyBases)
            {
                int dist = enemyBase.Distance(ccCoord);
                if (dist > 4)
                {
                    // Stage at ~40% of the way toward enemy base
                    int midQ = ccCoord.q + (int)((enemyBase.q - ccCoord.q) * 0.4);
                    int midR = ccCoord.r + (int)((enemyBase.r - ccCoord.r) * 0.4);
                    var mid = new HexCoordinate(
                        Math.Max(0, Math.Min(gameState.mapData.width - 1, midQ)),
                        Math.Max(0, Math.Min(gameState.mapData.height - 1, midR)));

                    if (gameState.mapData.IsWalkable(mid))
                        points.Add((mid, 25.0));
                }
            }

            // Rally near domination zones that are contested or neutral
            if (gameState.controlZones != null)
            {
                foreach (var zone in gameState.controlZones)
                {
                    if (!zone.controllingPlayerID.HasValue || zone.controllingPlayerID.Value != playerID)
                    {
                        int dist = zone.center.Distance(ccCoord);
                        if (dist <= 12)
                            points.Add((zone.center, 20.0 * zone.pointsMultiplier));
                    }
                }
            }

            // Pick the best rally points
            points.Sort((a, b) => b.priority.CompareTo(a.priority));
            aiState.rallyPoints.Clear();
            int maxPoints = GameConfig.AI.Staging.MaxRallyPoints;
            for (int i = 0; i < Math.Min(maxPoints, points.Count); i++)
            {
                aiState.rallyPoints.Add(points[i].coord);
            }

            // Fallback: if no strategic rally points found, stage a few tiles toward map center
            if (aiState.rallyPoints.Count == 0)
            {
                var mapCenter = new HexCoordinate(gameState.mapData.width / 2, gameState.mapData.height / 2);
                int stageDist = GameConfig.AI.Staging.StagingDistanceFromCC;
                int dirQ = mapCenter.q - ccCoord.q;
                int dirR = mapCenter.r - ccCoord.r;
                double len = Math.Max(1.0, Math.Sqrt(dirQ * dirQ + dirR * dirR));
                var stagePoint = new HexCoordinate(
                    Math.Max(0, Math.Min(gameState.mapData.width - 1, ccCoord.q + (int)(dirQ / len * stageDist))),
                    Math.Max(0, Math.Min(gameState.mapData.height - 1, ccCoord.r + (int)(dirR / len * stageDist))));

                if (gameState.mapData.IsWalkable(stagePoint))
                    aiState.rallyPoints.Add(stagePoint);
            }

            aiState.rallyPointsComputed = true;

            if (aiState.rallyPoints.Count > 0)
            {
                DebugLog.Log($"AI {playerID}: Computed {aiState.rallyPoints.Count} rally points");
                foreach (var rp in aiState.rallyPoints)
                    DebugLog.Log($"   Rally point at ({rp.q},{rp.r})");
            }
        }

        // ================================================================
        // Feature 1: Army Merging
        // ================================================================

        /// <summary>
        /// Finds small nearby armies and merges them into stronger forces.
        /// Armies within 2 tiles with fewer than 8 units each are candidates.
        /// Merged result capped at 20 units to avoid mega-stacks.
        /// </summary>
        public List<IEngineCommand> GenerateMergeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastMergeCheckTime, currentTime, 5.0)) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a)
                    && !aiState.activeRaidArmies.Contains(a.id))
                .ToList();

            if (idleArmies.Count < 2) return commands;

            var merged = new HashSet<Guid>(); // Track armies already targeted this cycle

            for (int i = 0; i < idleArmies.Count; i++)
            {
                var armyA = idleArmies[i];
                if (merged.Contains(armyA.id)) continue;
                if (armyA.GetTotalUnits() >= 12) continue; // Already strong enough

                for (int j = i + 1; j < idleArmies.Count; j++)
                {
                    var armyB = idleArmies[j];
                    if (merged.Contains(armyB.id)) continue;
                    if (armyB.GetTotalUnits() >= 12) continue;

                    int combinedUnits = armyA.GetTotalUnits() + armyB.GetTotalUnits();
                    if (combinedUnits > 20) continue; // Don't create mega-stacks

                    int distance = armyA.coordinate.Distance(armyB.coordinate);

                    if (distance <= 1)
                    {
                        // Adjacent or same tile — merge immediately (smaller into larger)
                        Guid sourceID, targetID;
                        if (armyA.GetTotalUnits() >= armyB.GetTotalUnits())
                        {
                            targetID = armyA.id;
                            sourceID = armyB.id;
                        }
                        else
                        {
                            targetID = armyB.id;
                            sourceID = armyA.id;
                        }

                        commands.Add(new AIMergeArmyCommand(playerID, sourceID, targetID));
                        merged.Add(sourceID);
                        merged.Add(targetID);

                        DebugLog.Log($"AI {playerID}: Merging armies ({armyA.GetTotalUnits()}+{armyB.GetTotalUnits()} units) at ({armyA.coordinate.q},{armyA.coordinate.r})");
                        break; // One merge per army per cycle
                    }
                    else if (distance <= 3)
                    {
                        // Close but not adjacent — move smaller army toward larger
                        ArmyData smaller, larger;
                        if (armyA.GetTotalUnits() <= armyB.GetTotalUnits())
                        { smaller = armyA; larger = armyB; }
                        else
                        { smaller = armyB; larger = armyA; }

                        commands.Add(new AIMoveCommand(playerID, smaller.id, larger.coordinate, true));
                        merged.Add(smaller.id);
                        break;
                    }
                }
            }

            return commands;
        }

        // ================================================================
        // Feature 8: Commander Utilization
        // ================================================================

        /// <summary>
        /// Evaluates commander-army matchups and reassigns commanders for optimal bonuses.
        /// Scores each (commander, army) pair based on specialty match, army composition,
        /// and current AI state.
        /// </summary>
        public List<IEngineCommand> GenerateCommanderCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastCommanderCheckTime, currentTime, GameConfig.AI.Commander.CheckInterval))
                return commands;

            var commanders = gameState.GetCommandersForPlayer(playerID);
            if (commanders.Count == 0) return commands;

            var armies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.GetTotalUnits() > 0)
                .ToList();

            if (armies.Count < GameConfig.AI.Commander.MinArmiesForReassignment) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            var ccCoord = cityCenter?.coordinate ?? new HexCoordinate(0, 0);

            // Score all (commander, army) pairs
            CommanderData bestCommander = null;
            ArmyData bestArmy = null;
            double bestImprovement = 0;

            foreach (var commander in commanders)
            {
                foreach (var army in armies)
                {
                    // Skip if already assigned to this army
                    if (commander.assignedArmyID.HasValue && commander.assignedArmyID.Value == army.id)
                        continue;

                    double newScore = ScoreCommanderArmyMatch(commander, army, aiState.currentState, ccCoord);

                    // Get current assignment score for comparison
                    double currentScore = 0;
                    if (commander.assignedArmyID.HasValue)
                    {
                        var currentArmy = gameState.GetArmy(commander.assignedArmyID.Value);
                        if (currentArmy != null)
                            currentScore = ScoreCommanderArmyMatch(commander, currentArmy, aiState.currentState, ccCoord)
                                + GameConfig.AI.Commander.StabilityBonus;
                    }

                    double improvement = newScore - currentScore;
                    if (improvement > bestImprovement && improvement > GameConfig.AI.Commander.ReassignmentThreshold)
                    {
                        bestImprovement = improvement;
                        bestCommander = commander;
                        bestArmy = army;
                    }
                }
            }

            if (bestCommander != null && bestArmy != null)
            {
                commands.Add(new AIAssignCommanderCommand(playerID, bestCommander.id, bestArmy.id));
                DebugLog.Log($"AI {playerID}: Reassigning commander {bestCommander.name} ({bestCommander.specialty.DisplayName()}) to army at ({bestArmy.coordinate.q},{bestArmy.coordinate.r}) — improvement: {bestImprovement:F0}");
            }

            return commands;
        }

        private double ScoreCommanderArmyMatch(CommanderData commander, ArmyData army, AIState aiState, HexCoordinate ccCoord)
        {
            double score = 0;
            var ratios = army.GetCategoryRatios();
            var specCategory = commander.specialty.GetUnitCategory();

            // Category match: specialty matches army's dominant units
            if (specCategory.HasValue)
            {
                double categoryRatio = 0;
                switch (specCategory.Value)
                {
                    case UnitCategory.Infantry: categoryRatio = ratios.infantry; break;
                    case UnitCategory.Ranged: categoryRatio = ratios.ranged; break;
                    case UnitCategory.Cavalry: categoryRatio = ratios.cavalry; break;
                    case UnitCategory.Siege: categoryRatio = ratios.siege; break;
                }

                if (categoryRatio > 0.5) score += 40.0;
                else if (categoryRatio > 0.25) score += 25.0;
                else if (categoryRatio == 0) score -= 30.0; // No matching units at all
            }

            // Aggressive commanders for attacking
            if (commander.specialty.IsAggressive() && aiState == AIState.Attack)
                score += 20.0;

            // Defensive commanders for base defense
            if ((commander.specialty.IsDefensiveVariant() || commander.specialty == CommanderSpecialty.Defensive)
                && army.coordinate.Distance(ccCoord) <= 4)
                score += 20.0;

            // Logistics commanders for large armies
            if (commander.specialty == CommanderSpecialty.Logistics && army.GetTotalUnits() >= 15)
                score += 15.0;

            return score;
        }

        // ================================================================
        // Staging Commands
        // ================================================================

        /// <summary>
        /// Sends idle armies to rally points during Alert/Peace states instead of sitting at CC.
        /// </summary>
        public List<IEngineCommand> GenerateStagingCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!aiState.rallyPointsComputed || aiState.rallyPoints.Count == 0) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && !IsScoutArmy(a)
                    && !aiState.activeRaidArmies.Contains(a.id))
                .ToList();

            if (idleArmies.Count == 0) return commands;

            // Keep at least one army near CC as garrison
            var ccCoord = cityCenter.coordinate;
            bool hasNearbyGuard = idleArmies.Any(a => a.coordinate.Distance(ccCoord) <= 3);
            int startIdx = hasNearbyGuard ? 0 : 1;

            for (int i = startIdx; i < idleArmies.Count; i++)
            {
                var army = idleArmies[i];

                // Assign to rally points in round-robin based on army hash
                int rpIdx = Math.Abs(army.id.GetHashCode()) % aiState.rallyPoints.Count;
                var rallyPoint = aiState.rallyPoints[rpIdx];

                // Only move if not already near the rally point
                if (army.coordinate.Distance(rallyPoint) > 3)
                {
                    commands.Add(new AIMoveCommand(playerID, army.id, rallyPoint, true));
                }
            }

            return commands;
        }
    }
}
