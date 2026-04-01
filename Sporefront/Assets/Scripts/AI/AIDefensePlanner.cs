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

            // Comeback mechanic: CriticallyBehind lowers all thresholds and forces tower building
            bool isCriticallyBehind = aiState.gamePosition == GamePosition.CriticallyBehind;
            bool isBehind = aiState.gamePosition == GamePosition.Behind;

            bool shouldBuildDefense;
            if (isCriticallyBehind)
            {
                // Desperate defense: build towers with minimal resources in ANY state
                shouldBuildDefense = player.GetResource(ResourceType.Wood) > 150 &&
                                     player.GetResource(ResourceType.Stone) > 100;
                if (shouldBuildDefense)
                    DebugLog.Log("AI critically behind — prioritizing defensive tower construction");
            }
            else if (aiState.currentState == AIState.Peace)
            {
                if (isBehind)
                {
                    // Behind: lower Peace-state thresholds to get towers up faster
                    shouldBuildDefense = player.GetResource(ResourceType.Wood) > 250 &&
                                         player.GetResource(ResourceType.Stone) > 200;
                }
                else if (threatIsRising)
                {
                    // Reduced thresholds when threat is rising
                    shouldBuildDefense = player.GetResource(ResourceType.Wood) > 300 &&
                                         player.GetResource(ResourceType.Stone) > 250;
                }
                else if (gameState.currentTime > 300.0 && towerCount == 0)
                {
                    // Build at least one tower after 5 minutes
                    shouldBuildDefense = true;
                }
                else if (hasBarracks && towerCount == 0)
                {
                    // Build a tower once we have military infrastructure
                    shouldBuildDefense = true;
                }
                else
                {
                    // Original high-resource threshold
                    shouldBuildDefense = player.GetResource(ResourceType.Wood) > 500 &&
                                         player.GetResource(ResourceType.Stone) > 400;
                }
            }
            else
            {
                shouldBuildDefense = threatLevel >= minThreatForDefenseBuilding ||
                                     aiState.currentState == AIState.Defense ||
                                     threatIsRising ||
                                     isBehind; // Behind: always consider defense building
            }

            if (!shouldBuildDefense) return commands;

            int fortCount = gameState.GetBuildingCount(BuildingType.WoodenFort, playerID);

            // CriticallyBehind: allow extra towers for desperate defense
            int effectiveMaxTowers = isCriticallyBehind ? maxTowersPerAI + 2 : maxTowersPerAI;

            if (towerCount < effectiveMaxTowers)
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
                (aiState.currentState == AIState.Defense || aiState.currentState == AIState.Alert || isCriticallyBehind))
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

            if (!player.CanAfford(buildingType.BuildCost())) return null;

            int maxDistance = buildingType == BuildingType.Tower ? 4 : 5;
            var location = FindDefenseBuildLocation(cityCenter.coordinate, maxDistance, gameState, playerID, buildingType, aiState.cachedChokepoints);
            if (!location.HasValue) return null;

            DebugLog.Log($"AI building {buildingType.DisplayName()} at ({location.Value.q}, {location.Value.r}) for defense");
            return new AIBuildCommand(playerID, buildingType, location.Value, 0);
        }

        private HexCoordinate? FindDefenseBuildLocation(HexCoordinate center, int maxDistance, GameState gameState, Guid playerID, BuildingType buildingType, List<ChokepointData> chokepoints = null)
        {
            var rng = new System.Random();
            var faction = gameState.GetPlayer(playerID)?.faction ?? FactionType.None;
            var defConfig = FactionAIConfig.Get(faction);

            for (int distance = 2; distance <= maxDistance; distance++)
            {
                var ring = center.CoordinatesInRing(distance);
                var valid = new List<(HexCoordinate coord, double score)>();

                foreach (var coord in ring)
                {
                    int hexSize = buildingType.HexSize();
                    bool canBuild;
                    if (hexSize == 1)
                    {
                        canBuild = gameState.CanBuildAt(coord, playerID);
                    }
                    else
                    {
                        canBuild = gameState.CanBuildAt(coord, playerID);
                        if (canBuild)
                        {
                            var neighbors = coord.Neighbors().Take(hexSize - 1);
                            canBuild = neighbors.All(n => gameState.CanBuildAt(n, playerID));
                        }
                    }
                    if (!canBuild) continue;

                    double score = rng.NextDouble(); // Base randomness

                    // Chokepoint bonus: defensive buildings near chokepoints are much more effective
                    score += AIStrategicAnalysis.GetChokepointBonus(coord, chokepoints);

                    // Faction terrain preferences for defensive buildings (data-driven)
                    var terrain = gameState.mapData.GetTerrain(coord);
                    double terrainBonus;
                    if (terrain.HasValue && defConfig.TowerTerrainBonus.TryGetValue(terrain.Value, out terrainBonus))
                        score += terrainBonus;
                    if (defConfig.TowerForestBonus)
                    {
                        var rp = gameState.GetResourcePoint(coord);
                        if (rp != null && rp.resourceType == ResourcePointType.Trees)
                            score += 1.5;
                    }

                    valid.Add((coord, score));
                }

                if (valid.Count > 0)
                {
                    valid.Sort((a, b) => b.score.CompareTo(a.score));
                    return valid[0].coord;
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

            if (!AIHelper.ShouldExecute(ref aiState.lastGarrisonCheckTime, currentTime, garrisonCheckInterval)) return commands;

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
                .Where(a => !a.isInCombat && a.currentPath == null && HasGarrisonableUnits(a));

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
                    DebugLog.Log($"AI moving army to garrison {targetBuilding.buildingType.DisplayName()}");
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

            if (!AIHelper.ShouldExecute(ref aiState.lastEntrenchCheckTime, currentTime, GameConfig.AI.Intervals.EntrenchCheck))
                return new List<IEngineCommand>();

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
            // Faction-aware: prefer terrain that synergizes with faction bonuses
            var commands = new List<IEngineCommand>();
            var entrenchConfig = FactionAIConfig.Get(player.faction);
            var candidates = armies
                .Where(a => !a.isInCombat && a.currentPath == null && !a.isRetreating &&
                            !a.isEntrenched && !a.isEntrenching)
                .Select(a => {
                    double score = -a.coordinate.Distance(cityCenter.coordinate); // Closer = higher
                    // Faction terrain preferences for entrenchment (data-driven)
                    double eTerrain;
                    var eTile = gameState.mapData.GetTerrain(a.coordinate);
                    if (eTile.HasValue && entrenchConfig.EntrenchTerrainBonus.TryGetValue(eTile.Value, out eTerrain))
                        score += eTerrain;
                    if (entrenchConfig.EntrenchForestBonus > 0)
                    {
                        var rp = gameState.GetResourcePoint(a.coordinate);
                        if (rp != null && rp.resourceType == ResourcePointType.Trees)
                            score += entrenchConfig.EntrenchForestBonus;
                    }
                    return (army: a, score: score);
                })
                .OrderByDescending(x => x.score)
                .Select(x => x.army);

            foreach (var army in candidates)
            {
                int distance = army.coordinate.Distance(cityCenter.coordinate);
                if (distance > 8) continue;

                // Check that this army's tile isn't already entrenched
                var armiesHere = gameState.GetArmies(army.coordinate);
                bool alreadyEntrenched = armiesHere.Any(a => a.isEntrenched && a.id != army.id);
                if (alreadyEntrenched) continue;

                commands.Add(new AIEntrenchCommand(playerID, army.id));
                DebugLog.Log($"AI entrenching army {army.name} near city center (threat: {(int)threatLevel})");
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
