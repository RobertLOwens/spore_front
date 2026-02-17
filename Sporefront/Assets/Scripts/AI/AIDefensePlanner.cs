// ============================================================================
// FILE: AI/AIDefensePlanner.cs
// PURPOSE: AI defensive planning - tower/fort construction, garrison management,
//          and entrenchment decisions
//          C# port of AIDefensePlanner.swift
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
    public class AIDefensePlanner
    {
        // ================================================================
        // Configuration
        // ================================================================

        private readonly double defenseBuildInterval = GameConfig.AI.Intervals.DefenseBuild;
        private readonly double garrisonCheckInterval = GameConfig.AI.Intervals.GarrisonCheck;
        private readonly int maxTowersPerAI = GameConfig.AI.Limits.MaxTowersPerAI;
        private readonly int maxFortsPerAI = GameConfig.AI.Limits.MaxFortsPerAI;
        private readonly double minThreatForDefenseBuilding = GameConfig.AI.Thresholds.MinThreatForDefenseBuilding;

        // ================================================================
        // Defensive Building Commands
        // ================================================================

        public List<IEngineCommand> GenerateDefensiveBuildingCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastDefenseBuildTime < defenseBuildInterval) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);

            bool shouldBuildDefense;
            if (aiState.currentState == AIState.Peace)
            {
                bool hasExcessResources = player.GetResource(ResourceType.Wood) > 500 &&
                                          player.GetResource(ResourceType.Stone) > 400;
                shouldBuildDefense = hasExcessResources;
            }
            else
            {
                shouldBuildDefense = threatLevel >= minThreatForDefenseBuilding ||
                                     aiState.currentState == AIState.Defense;
            }

            if (!shouldBuildDefense) return commands;

            int towerCount = gameState.GetBuildingCount(BuildingType.Tower, playerID);
            int fortCount = gameState.GetBuildingCount(BuildingType.WoodenFort, playerID);

            if (towerCount < maxTowersPerAI)
            {
                var cmd = TryBuildDefensiveStructure(BuildingType.Tower, aiState, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastDefenseBuildTime = currentTime;
                    return commands;
                }
            }

            if (fortCount < maxFortsPerAI &&
                (aiState.currentState == AIState.Defense || aiState.currentState == AIState.Alert))
            {
                var cmd = TryBuildDefensiveStructure(BuildingType.WoodenFort, aiState, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastDefenseBuildTime = currentTime;
                    return commands;
                }
            }

            return commands;
        }

        private IEngineCommand TryBuildDefensiveStructure(BuildingType buildingType, AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;

            int ccLevel = cityCenter.level;
            if (ccLevel < buildingType.RequiredCityCenterLevel()) return null;

            var cost = buildingType.BuildCost();
            foreach (var kvp in cost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            int maxDistance = buildingType == BuildingType.Tower ? 4 : 5;
            var location = FindDefenseBuildLocation(cityCenter.coordinate, maxDistance, gameState, playerID, buildingType);
            if (!location.HasValue) return null;

            DebugLog.Log(string.Format("AI building {0} at ({1}, {2}) for defense",
                buildingType.DisplayName(), location.Value.q, location.Value.r));
            return new AIBuildCommand(playerID, buildingType, location.Value, 0);
        }

        private HexCoordinate? FindDefenseBuildLocation(HexCoordinate center, int maxDistance, GameState gameState, Guid playerID, BuildingType buildingType)
        {
            var rng = new System.Random();
            for (int distance = 2; distance <= maxDistance; distance++)
            {
                var ring = center.CoordinatesInRing(distance);
                var shuffled = ring.OrderBy(_ => rng.Next()).ToList();

                foreach (var coord in shuffled)
                {
                    int hexSize = buildingType.HexSize();
                    if (hexSize == 1)
                    {
                        if (gameState.CanBuildAt(coord, playerID))
                            return coord;
                    }
                    else
                    {
                        if (gameState.CanBuildAt(coord, playerID))
                        {
                            var neighbors = coord.Neighbors().Take(hexSize - 1);
                            bool allBuildable = neighbors.All(n => gameState.CanBuildAt(n, playerID));
                            if (allBuildable) return coord;
                        }
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

            if (currentTime - aiState.lastGarrisonCheckTime < garrisonCheckInterval) return commands;
            aiState.lastGarrisonCheckTime = currentTime;

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

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
                .Where(a => !a.isInCombat && a.currentPath == null && HasGarrisonableUnits(a))
                .ToList();

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
                    DebugLog.Log(string.Format("AI moving army to garrison {0}", targetBuilding.buildingType.DisplayName()));
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

            if (currentTime - aiState.lastEntrenchCheckTime < GameConfig.AI.Intervals.EntrenchCheck)
                return new List<IEngineCommand>();
            aiState.lastEntrenchCheckTime = currentTime;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return new List<IEngineCommand>();

            // Only entrench if there's a threat nearby
            double threatLevel = gameState.GetThreatLevel(cityCenter.coordinate, playerID);
            if (threatLevel <= 0) return new List<IEngineCommand>();

            // Need wood buffer beyond entrenchment cost
            int woodBuffer = 200;
            if (player.GetResource(ResourceType.Wood) < GameConfig.Entrenchment.WoodCost + woodBuffer)
                return new List<IEngineCommand>();

            // Count currently entrenched/entrenching armies
            var armies = gameState.GetArmiesForPlayer(playerID);
            int entrenchedCount = armies.Count(a => a.isEntrenched || a.isEntrenching);
            int maxEntrenched = threatLevel > 30 ? 4 : 2;
            if (entrenchedCount >= maxEntrenched) return new List<IEngineCommand>();

            // Find idle armies near city center that could entrench
            var commands = new List<IEngineCommand>();
            var candidates = armies
                .Where(a => !a.isInCombat && a.currentPath == null && !a.isRetreating &&
                            !a.isEntrenched && !a.isEntrenching)
                .OrderBy(a => a.coordinate.Distance(cityCenter.coordinate))
                .ToList();

            foreach (var army in candidates)
            {
                int distance = army.coordinate.Distance(cityCenter.coordinate);
                if (distance > 8) continue;

                // Check that this army's tile isn't already entrenched
                var armiesHere = gameState.GetArmies(army.coordinate);
                bool alreadyEntrenched = armiesHere.Any(a => a.isEntrenched && a.id != army.id);
                if (alreadyEntrenched) continue;

                commands.Add(new AIEntrenchCommand(playerID, army.id));
                DebugLog.Log(string.Format("AI entrenching army {0} near city center (threat: {1})",
                    army.name, (int)threatLevel));
                break; // One entrenchment command per cycle
            }

            return commands;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private bool HasGarrisonableUnits(ArmyData army)
        {
            var garrisonableTypes = new HashSet<MilitaryUnitType>
            {
                MilitaryUnitType.Archer, MilitaryUnitType.Crossbow,
                MilitaryUnitType.Mangonel, MilitaryUnitType.Trebuchet
            };

            foreach (var kvp in army.militaryComposition)
            {
                if (garrisonableTypes.Contains(kvp.Key) && kvp.Value > 0)
                    return true;
            }
            return false;
        }
    }
}
