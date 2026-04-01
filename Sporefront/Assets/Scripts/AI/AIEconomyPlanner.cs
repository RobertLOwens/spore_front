// ============================================================================
// FILE: AI/AIEconomyPlanner.cs
// PURPOSE: AI economy and expansion planning - resource gathering, building,
//          villager management, resource camps, and scouting
//          C# port of AIEconomyPlanner.swift
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
    public class AIEconomyPlanner
    {
        // ================================================================
        // Configuration
        // ================================================================

        private readonly double buildInterval = GameConfig.AI.Intervals.EconomicBuild;
        private readonly double trainInterval = GameConfig.AI.Intervals.MilitaryTrain;
        private readonly double scoutInterval = GameConfig.AI.Intervals.Scout;
        private readonly double campBuildInterval = GameConfig.AI.Intervals.CampBuild;
        private readonly int maxCampsPerType = GameConfig.AI.Limits.MaxCampsPerType;
        private readonly int scoutRange = GameConfig.AI.Limits.ScoutRange;

        // ================================================================
        // Economy Commands
        // ================================================================

        public List<IEngineCommand> GenerateEconomyCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            var idleVillagerCount = gameState.GetVillagerGroupsForPlayer(playerID)
                .Count(g => g.currentTask is IdleTask && g.currentPath == null);
            DebugLog.Log($"AI Economy: idleVillagers={idleVillagerCount}, wood={player.GetResource(ResourceType.Wood)}");

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

            // Build lumber camp ASAP if missing (always highest priority)
            if (currentTime - aiState.lastEconomicBuildTime >= buildInterval)
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

            // Hunt nearby animals for food
            commands.AddRange(TryHuntAnimal(playerID, gameState));

            // Assign idle villagers to gather resources
            commands.AddRange(TryAssignVillagersToGather(playerID, gameState));

            // Rebalance villagers
            commands.AddRange(TryRebalanceVillagers(playerID, gameState));

            // Weighted candidate system for building construction (informed by build order)
            if (currentTime - aiState.lastEconomicBuildTime >= buildInterval)
            {
                var urgency = AnalyzeResourceNeeds(playerID, gameState);
                double foodUrgency;
                urgency.TryGetValue(ResourceType.Food, out foodUrgency);
                var foodRate = player.GetCollectionRate(ResourceType.Food);

                bool shouldBuildStorage = urgency.Values.Any(u => u < 0.2);

                var buildCandidates = new List<(Func<IEngineCommand> tryBuild, double weight, string name)>();

                // Build order hint: boost the next step in the build order
                double buildOrderBonus = 0.0;
                BuildingType? buildOrderTarget = null;
                if (aiState.currentBuildOrder != null)
                {
                    var step = aiState.currentBuildOrder.CurrentStep;
                    if (step != null)
                    {
                        buildOrderTarget = step.buildingType;
                        buildOrderBonus = step.priorityBonus * 0.01; // Normalize to weight scale
                    }
                }

                // Farm: high weight if food urgency is high
                double farmWeight = 0.0;
                if (foodUrgency > 0.5 || foodRate < 2.0)
                    farmWeight = 0.8;
                if (buildOrderTarget == BuildingType.Farm)
                    farmWeight = Math.Max(farmWeight, 0.6) + buildOrderBonus;
                // Feature 4: Comeback — boost economy when behind
                if (aiState.gamePosition == GamePosition.Behind || aiState.gamePosition == GamePosition.CriticallyBehind)
                    farmWeight = Math.Max(farmWeight, 0.5) + 0.3;
                if (farmWeight > 0)
                    buildCandidates.Add((() => TryBuildFarm(playerID, gameState), farmWeight, "Farm"));

                // Military building
                double milWeight = 0.5;
                // Feature 4: Reduce military building priority when critically behind (focus on economy)
                if (aiState.gamePosition == GamePosition.CriticallyBehind)
                    milWeight = 0.2;
                if (buildOrderTarget == BuildingType.Barracks || buildOrderTarget == BuildingType.ArcheryRange ||
                    buildOrderTarget == BuildingType.Stable || buildOrderTarget == BuildingType.SiegeWorkshop)
                    milWeight += buildOrderBonus;
                // Feature 6: Siege intelligence — boost siege workshop when needed
                if (aiState.siegeRequired && !aiState.siegeReady)
                {
                    if (buildOrderTarget == BuildingType.SiegeWorkshop ||
                        !gameState.HasBuilding(playerID, BuildingType.SiegeWorkshop, operationalOnly: true))
                        milWeight = Math.Max(milWeight, GameConfig.AI.Siege.WorkshopBuildPriority);
                }
                buildCandidates.Add((() => TryBuildMilitaryBuilding(playerID, gameState), milWeight, "Military"));

                // Storage
                if (shouldBuildStorage)
                    buildCandidates.Add((() => TryBuildStorage(playerID, gameState), 0.3, "Storage"));

                // Library
                double libWeight = 0.2;
                if (buildOrderTarget == BuildingType.Library)
                    libWeight += buildOrderBonus;
                buildCandidates.Add((() => TryBuildLibrary(playerID, gameState), libWeight, "Library"));

                // Mining camp from build order
                if (buildOrderTarget == BuildingType.MiningCamp)
                    buildCandidates.Add((() => TryBuildResourceCamp(aiState, gameState), 0.6 + buildOrderBonus, "MiningCamp"));

                // Blacksmith from build order
                if (buildOrderTarget == BuildingType.Blacksmith)
                    buildCandidates.Add((() => TryBuildBlacksmith(playerID, gameState), 0.5 + buildOrderBonus, "Blacksmith"));

                // Feature 4: Emergency build order — override weights
                if (aiState.buildOrderEmergency && aiState.emergencyBuildTarget.HasValue)
                {
                    var emergencyType = aiState.emergencyBuildTarget.Value;
                    Func<IEngineCommand> emergencyBuilder = null;

                    switch (emergencyType)
                    {
                        case BuildingType.Barracks:
                        case BuildingType.ArcheryRange:
                        case BuildingType.Stable:
                        case BuildingType.SiegeWorkshop:
                            emergencyBuilder = () => TryBuildMilitaryBuilding(playerID, gameState);
                            break;
                        case BuildingType.Farm:
                            emergencyBuilder = () => TryBuildFarm(playerID, gameState);
                            break;
                        case BuildingType.LumberCamp:
                        case BuildingType.MiningCamp:
                            emergencyBuilder = () => TryBuildResourceCamp(aiState, gameState);
                            break;
                    }

                    if (emergencyBuilder != null)
                        buildCandidates.Add((emergencyBuilder, GameConfig.AI.BuildOrderTiming.EmergencyPriorityBonus, "Emergency-" + emergencyType.DisplayName()));
                }

                var buildCmd = PickWeightedCandidate(buildCandidates, aiState.economyRng);
                if (buildCmd != null)
                {
                    commands.Add(buildCmd);
                    aiState.lastEconomicBuildTime = currentTime;

                    // Advance build order if the built building matches the current step
                    if (aiState.currentBuildOrder != null && buildOrderTarget.HasValue)
                        aiState.currentBuildOrder.Advance();

                    if (aiState.buildOrderEmergency && aiState.emergencyBuildTarget.HasValue)
                    {
                        // Check if we now have the emergency building
                        bool hasBuildingNow = gameState.GetBuildingsForPlayer(playerID)
                            .Any(b => b.buildingType == aiState.emergencyBuildTarget.Value);
                        if (hasBuildingNow)
                        {
                            aiState.buildOrderEmergency = false;
                            aiState.emergencyBuildTarget = null;
                        }
                    }
                }
            }

            // Build houses proactively — maintain population headroom
            int popHeadroom = popCapacity - popCurrent;
            bool shouldBuildHouse = popHeadroom <= 3 ||  // Critical: almost capped
                (popHeadroom <= 5 && player.GetResource(ResourceType.Wood) > 150) ||  // Proactive: headroom tight
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

            if (currentTime - aiState.lastCampBuildTime >= campBuildInterval)
            {
                var cmd = TryBuildResourceCamp(aiState, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastCampBuildTime = currentTime;
                }
            }

            if (currentTime - aiState.lastScoutTime >= scoutInterval)
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
        // Feature 7: Expansion Timing — Second City Center
        // ================================================================

        /// <summary>
        /// Evaluates whether the AI should build a second City Center for expansion.
        /// Requires economic surplus, population pressure, and map knowledge.
        /// </summary>
        public List<IEngineCommand> GenerateExpansionTimingCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (aiState.expansionCount >= GameConfig.AI.Expansion.MaxExpansions) return commands;
            if (!AIHelper.ShouldExecute(ref aiState.lastExpansionCheckTime, currentTime, GameConfig.AI.Expansion.CheckInterval)) return commands;

            if (gameState.currentTime < GameConfig.AI.Expansion.MinGameTime) return commands;
            if (aiState.gamePosition == GamePosition.CriticallyBehind) return commands;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            // Check economic surplus
            if (player.GetResource(ResourceType.Food) < GameConfig.AI.Expansion.MinSurplusFood) return commands;
            if (player.GetResource(ResourceType.Wood) < GameConfig.AI.Expansion.MinSurplusWood) return commands;

            // Check population pressure — should be near cap
            int popCurrent, popCapacity;
            gameState.GetPopulationStats(playerID, out popCurrent, out popCapacity);
            if (popCurrent < popCapacity - 10) return commands; // Still have room, no need to expand yet

            // Check map knowledge
            int campCount = gameState.GetBuildingsForPlayer(playerID)
                .Count(b => (b.buildingType == BuildingType.LumberCamp || b.buildingType == BuildingType.MiningCamp) && b.IsOperational);
            if (aiState.mapExplorationPercent < 0.4 && campCount < 2) return commands;

            // Can we afford a CC?
            if (!player.CanAfford(BuildingType.CityCenter.BuildCost())) return commands;

            // Find or use cached expansion site
            if (!aiState.plannedExpansionSite.HasValue || !gameState.mapData.IsWalkable(aiState.plannedExpansionSite.Value))
            {
                aiState.plannedExpansionSite = FindBestExpansionSite(aiState, gameState);
            }

            if (!aiState.plannedExpansionSite.HasValue) return commands;

            var site = aiState.plannedExpansionSite.Value;

            // Verify we can still build there
            if (!gameState.CanBuildAt(site, playerID))
            {
                aiState.plannedExpansionSite = null; // Invalidate and retry next cycle
                return commands;
            }

            commands.Add(new AIBuildCommand(playerID, BuildingType.CityCenter, site, 0));
            aiState.expansionCount++;
            aiState.expansionPlanned = false;
            aiState.plannedExpansionSite = null;

            DebugLog.Log($"AI {playerID}: Expanding — building second City Center at ({site.q},{site.r})");

            return commands;
        }

        private HexCoordinate? FindBestExpansionSite(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;

            var ccCoord = cityCenter.coordinate;
            HexCoordinate? bestSite = null;
            double bestScore = double.MinValue;

            int minDist = GameConfig.AI.Expansion.MinDistFromCC;
            int maxDist = GameConfig.AI.Expansion.MaxDistFromCC;

            // Scan rings from minDist to maxDist
            for (int dist = minDist; dist <= maxDist; dist++)
            {
                var ring = ccCoord.CoordinatesInRing(dist);
                foreach (var coord in ring)
                {
                    if (!gameState.mapData.IsValidCoordinate(coord)) continue;
                    if (!gameState.mapData.IsWalkable(coord)) continue;
                    if (!gameState.CanBuildAt(coord, playerID)) continue;

                    // Must be explored
                    var player = gameState.GetPlayer(playerID);
                    if (player != null && !player.IsExplored(coord)) continue;

                    double score = AIStrategicAnalysis.ScoreExpansionSite(
                        coord, gameState, playerID, ccCoord,
                        aiState.knownEnemyBases, aiState.cachedChokepoints);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSite = coord;
                    }
                }
            }

            return bestSite;
        }

        // ================================================================
        // Feature 3: Resource Denial / Map Control
        // ================================================================

        /// <summary>
        /// Builds resource camps on contested midmap resource nodes to deny them to the enemy.
        /// Also sends a guard army to protect newly placed camps.
        /// </summary>
        public List<IEngineCommand> GenerateMapControlCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!aiState.enemyBaseFound) return commands;
            if (!AIHelper.ShouldExecute(ref aiState.lastMapControlCheckTime, currentTime, GameConfig.AI.MapControl.CheckInterval)) return commands;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            // Count existing contested camps (camps farther than 6 tiles from CC)
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;
            var ccCoord = cityCenter.coordinate;

            int contestedCampCount = gameState.GetBuildingsForPlayer(playerID)
                .Count(b => (b.buildingType == BuildingType.LumberCamp || b.buildingType == BuildingType.MiningCamp)
                    && b.IsOperational && b.coordinate.Distance(ccCoord) > 6);

            if (contestedCampCount >= GameConfig.AI.MapControl.MaxContestedCamps) return commands;

            // Find contested resources
            var contested = AIStrategicAnalysis.IdentifyContestedResources(gameState, playerID, aiState.knownEnemyBases);
            if (contested.Count == 0) return commands;

            // Try to build a camp at the best contested node
            var bestNode = contested[0];
            var resourceAtNode = gameState.GetResourcePoint(bestNode.coord);
            if (resourceAtNode == null) return commands;

            // Determine camp type based on resource
            BuildingType campType;
            var resType = resourceAtNode.resourceType;
            if (resType == ResourcePointType.Trees)
                campType = BuildingType.LumberCamp;
            else if (resType == ResourcePointType.StoneQuarry || resType == ResourcePointType.OreMine)
                campType = BuildingType.MiningCamp;
            else
                return commands; // Not a camp-able resource

            // Check affordability
            if (!player.CanAfford(campType.BuildCost())) return commands;

            // Find build location near the resource
            HexCoordinate? buildSite = null;
            if (gameState.CanBuildAt(bestNode.coord, playerID))
                buildSite = bestNode.coord;
            else
            {
                foreach (var neighbor in bestNode.coord.Neighbors())
                {
                    if (gameState.CanBuildAt(neighbor, playerID))
                    {
                        buildSite = neighbor;
                        break;
                    }
                }
            }

            if (!buildSite.HasValue) return commands;

            commands.Add(new AIBuildCommand(playerID, campType, buildSite.Value, 0));
            DebugLog.Log($"AI {playerID}: Map control — building {campType.DisplayName()} at contested node ({buildSite.Value.q},{buildSite.Value.r})");

            return commands;
        }

        // ================================================================
        // Villager Training & Deployment
        // ================================================================

        private IEngineCommand TryTrainVillagers(Guid playerID, GameState gameState, double currentTime, AIPlayerState aiState)
        {
            if (currentTime - aiState.lastVillagerTrainTime < trainInterval) return null;

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
                .Where(b => b.IsOperational && b.villagerGarrison >= 3)
                .ToList();

            if (buildings.Count == 0) return null;
            var building = buildings[0];

            int villagersToSpawn = building.villagerGarrison;
            DebugLog.Log($"AI deploying {villagersToSpawn} villagers from {building.buildingType.DisplayName()}");
            return new AIDeployVillagersCommand(playerID, building.id, villagersToSpawn);
        }

        // ================================================================
        // Resource Gathering
        // ================================================================

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
                    if (existingGatherers >= 2) continue;

                    double resourceUrgency;
                    urgency.TryGetValue(resource.resourceType.ResourceYield(), out resourceUrgency);
                    if (resourceUrgency < 0.15) continue;

                    commands.Add(new AIGatherCommand(playerID, villagerGroup.id, resource.id));
                    assignedResources.Add(resource.id);
                    break;
                }
            }

            return commands;
        }

        // ================================================================
        // Hunting
        // ================================================================

        private List<IEngineCommand> TryHuntAnimal(Guid playerID, GameState gameState)
        {
            var commands = new List<IEngineCommand>();
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var huntableAnimals = gameState.GetExploredResourcePoints(playerID)
                .Where(r => r.resourceType.IsHuntable() && r.currentHealth > 0 &&
                            r.coordinate.Distance(cityCenter.coordinate) <= 8)
                .ToList();

            var idleVillagers = gameState.GetVillagerGroupsForPlayer(playerID)
                .Where(g => g.currentTask is IdleTask && g.currentPath == null);

            var usedVillagers = new HashSet<Guid>();
            foreach (var animal in huntableAnimals)
            {
                var villager = idleVillagers.FirstOrDefault(v => !usedVillagers.Contains(v.id));
                if (villager == null) break;

                commands.Add(new AIHuntCommand(playerID, villager.id, animal.id));
                usedVillagers.Add(villager.id);
                if (commands.Count >= 3) break;
            }

            return commands;
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

                if (resourceType == ResourceType.Food) score *= 1.2;

                if (resourceType == ResourceType.Wood)
                {
                    int buildingCount = gameState.GetBuildingsForPlayer(playerID).Count;
                    if (buildingCount < 10) score *= 1.15;
                }

                if (rate < 0.1 && score > 0.2) score += 0.1;

                urgency[resourceType] = Math.Max(0.0, Math.Min(2.0, score));
            }

            // Faction resource urgency bias (data-driven via FactionAIConfig)
            var factionConfig = FactionAIConfig.Get(player.faction);
            foreach (var kvp in factionConfig.ResourceUrgencyMultiplier)
            {
                if (urgency.ContainsKey(kvp.Key))
                    urgency[kvp.Key] = Math.Min(2.0, urgency[kvp.Key] * kvp.Value);
            }

            return urgency;
        }

        private List<IEngineCommand> TryRebalanceVillagers(Guid playerID, GameState gameState)
        {
            var commands = new List<IEngineCommand>();

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var urgency = AnalyzeResourceNeeds(playerID, gameState);

            // Emergency rebalancing: if urgency gap > 0.8, aggressively reassign
            double maxUrgency = 0, minUrgency = double.MaxValue;
            ResourceType maxUrgencyType = ResourceType.Food, minUrgencyType = ResourceType.Food;
            foreach (var kvp in urgency)
            {
                if (kvp.Value > maxUrgency) { maxUrgency = kvp.Value; maxUrgencyType = kvp.Key; }
                if (kvp.Value < minUrgency) { minUrgency = kvp.Value; minUrgencyType = kvp.Key; }
            }
            bool emergencyRebalance = (maxUrgency - minUrgency) > 0.8;

            var overStaffedGroups = new List<(VillagerGroupData group, ResourcePointData resource)>();
            var underStaffedResources = new List<ResourcePointData>();

            foreach (var group in gameState.GetVillagerGroupsForPlayer(playerID))
            {
                var gatherTask = group.currentTask as GatheringResourceTask;
                if (gatherTask == null) continue;

                var resource = gameState.GetResourcePoint(gatherTask.ResourcePointID);
                if (resource == null) continue;

                // Pull villagers off nearly-depleted resources
                if (resource.remainingAmount < 50)
                {
                    overStaffedGroups.Add((group, resource));
                    continue;
                }

                var resourceType = resource.resourceType.ResourceYield();
                double resourceUrgency;
                urgency.TryGetValue(resourceType, out resourceUrgency);

                int assignedCount = resource.assignedVillagerGroupIDs != null ? resource.assignedVillagerGroupIDs.Count : 0;

                // Emergency: pull from lowest-urgency resource type
                if (emergencyRebalance && resourceType == minUrgencyType && assignedCount >= 1)
                {
                    overStaffedGroups.Add((group, resource));
                    continue;
                }

                if (resourceUrgency < 0.2 && assignedCount >= 2)
                {
                    overStaffedGroups.Add((group, resource));
                }
            }

            // In emergency mode, limit reassignments to 2 per cycle to avoid disruption
            if (emergencyRebalance && overStaffedGroups.Count > 2)
                overStaffedGroups = overStaffedGroups.GetRange(0, 2);

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
                if (resourceUrgency > 0.6 && assignedCount < 2)
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
                    return assignedCount < 2;
                });
                if (targetResource == null) break;

                commands.Add(new AIGatherCommand(playerID, group.id, targetResource.id));
            }

            return commands;
        }

        // ================================================================
        // Faction-Aware Build Location
        // ================================================================

        private HexCoordinate? FindFactionPreferredBuildLocation(HexCoordinate center, int maxDistance, GameState gameState, Guid playerID)
        {
            var factionBuildConfig = FactionAIConfig.Get(gameState.GetPlayer(playerID)?.faction ?? FactionType.None);
            if (!factionBuildConfig.PreferHighlandBuilding)
                return gameState.FindBuildLocation(center, maxDistance, playerID);

            // Muscaria: prefer mountain/hill tiles for -15% build cost reduction
            var rng = new System.Random();
            HexCoordinate? fallback = null;
            for (int distance = 1; distance <= maxDistance; distance++)
            {
                var ring = center.CoordinatesInRing(distance);
                var highland = new List<HexCoordinate>();
                var other = new List<HexCoordinate>();
                foreach (var coord in ring)
                {
                    if (!gameState.CanBuildAt(coord, playerID)) continue;
                    var terrain = gameState.mapData.GetTerrain(coord);
                    if (terrain == TerrainType.Mountain || terrain == TerrainType.Hill)
                        highland.Add(coord);
                    else
                        other.Add(coord);
                }
                if (highland.Count > 0) return highland[rng.Next(highland.Count)];
                if (fallback == null && other.Count > 0) fallback = other[rng.Next(other.Count)];
            }
            return fallback ?? gameState.FindBuildLocation(center, maxDistance, playerID);
        }

        // ================================================================
        // Building Construction
        // ================================================================

        /// <summary>
        /// Generic building construction helper. Validates player/CC, checks optional preconditions
        /// (CC level, singleton, max count), affordability, and finds a build location.
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

            var location = FindFactionPreferredBuildLocation(cityCenter.coordinate, searchRadius, gameState, playerID);
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

        private IEngineCommand TryBuildBlacksmith(Guid playerID, GameState gameState)
            => TryBuildStructure(playerID, gameState, BuildingType.Blacksmith, 4, requiresCCLevel: true, singleton: true);

        // ================================================================
        // Building Upgrades
        // ================================================================

        public List<IEngineCommand> GenerateUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastUpgradeCheckTime, currentTime, GameConfig.AI.Intervals.UpgradeCheck)) return new List<IEngineCommand>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();

            var buildings = gameState.GetBuildingsForPlayer(playerID);

            int currentlyUpgrading = buildings.Count(b => b.state == BuildingState.Upgrading);

            // Don't upgrade CC until we have a lumber camp or have wood income
            bool hasLumber = gameState.HasBuilding(playerID, BuildingType.LumberCamp, operationalOnly: true);
            bool hasWoodIncome = player.GetCollectionRate(ResourceType.Wood) > 0;
            if (!hasLumber && !hasWoodIncome) return new List<IEngineCommand>();

            // Minimum wood threshold: 150 for standard upgrades
            int woodReserve = player.GetResource(ResourceType.Wood);
            if (woodReserve < 150) return new List<IEngineCommand>();

            // Allow parallel upgrades only when economically strong
            bool allowParallel = woodReserve > 500 && player.GetResource(ResourceType.Stone) > 300;
            int maxConcurrent = allowParallel ? 2 : 1;
            if (currentlyUpgrading >= maxConcurrent) return new List<IEngineCommand>();

            // Don't stack on top of a CC upgrade (too resource-intensive)
            bool ccUpgrading = buildings.Any(b => b.buildingType == BuildingType.CityCenter && b.state == BuildingState.Upgrading);
            if (currentlyUpgrading > 0 && ccUpgrading) return new List<IEngineCommand>();

            // Score and pick the best building to upgrade
            var candidates = new List<(BuildingData building, double score)>();

            foreach (var building in buildings)
            {
                if (!building.CanUpgrade) continue;
                var cost = building.GetUpgradeCost();
                if (cost == null) continue;

                if (!player.CanAfford(cost)) continue;

                // Use genome upgrade priorities when available
                var genome = AIController.Instance?.genome;
                double score = 0.0;
                switch (building.buildingType)
                {
                    case BuildingType.CityCenter: score = genome != null ? genome.upgradePriorityCityCenter : 100.0; break;
                    case BuildingType.Barracks:
                    case BuildingType.ArcheryRange:
                    case BuildingType.Stable:
                    case BuildingType.SiegeWorkshop: score = genome != null ? genome.upgradePriorityMilitary : 60.0; break;
                    case BuildingType.Farm: score = genome != null ? genome.upgradePriorityFarm : 40.0; break;
                    case BuildingType.Warehouse: score = genome != null ? genome.upgradePriorityWarehouse : 30.0; break;
                    case BuildingType.Blacksmith: score = genome != null ? genome.upgradePriorityBlacksmith : 35.0; break;
                    case BuildingType.Library: score = genome != null ? genome.upgradePriorityLibrary : 25.0; break;
                    default: score = 10.0; break;
                }

                score += (6 - building.level) * 5.0;

                // State-aware score multipliers
                bool isMilitary = building.buildingType == BuildingType.Barracks
                    || building.buildingType == BuildingType.ArcheryRange
                    || building.buildingType == BuildingType.Stable
                    || building.buildingType == BuildingType.SiegeWorkshop;
                bool isEconomic = building.buildingType == BuildingType.Farm
                    || building.buildingType == BuildingType.Warehouse;
                bool isDefensive = building.buildingType == BuildingType.Tower
                    || building.buildingType == BuildingType.WoodenFort
                    || building.buildingType == BuildingType.Castle;
                bool isResourceCamp = building.buildingType == BuildingType.LumberCamp
                    || building.buildingType == BuildingType.MiningCamp;

                if (aiState.currentState == AIState.Attack && isMilitary)
                    score *= 1.5;
                else if (aiState.currentState == AIState.Peace && isEconomic)
                    score *= 1.3;

                // Feature 3: Strategic upgrade timing

                // Upgrade military buildings before a push (Attack or about to attack)
                if (isMilitary && (aiState.currentState == AIState.Alert || aiState.currentState == AIState.Attack))
                    score += 20.0;

                // Upgrade CC when approaching pop cap (unlocks higher tier buildings and pop)
                int popCurrent, popCapacity;
                gameState.GetPopulationStats(playerID, out popCurrent, out popCapacity);
                if (building.buildingType == BuildingType.CityCenter && popCapacity - popCurrent < 5)
                    score += 35.0;

                // Upgrade resource camps when nearby resource nodes are getting depleted
                if (isResourceCamp)
                {
                    int lowResourceCount = 0;
                    var nearbyResources = gameState.GetExploredResourcePoints(playerID);
                    foreach (var rp in nearbyResources)
                    {
                        if (rp.coordinate.Distance(building.coordinate) <= 3 && rp.remainingAmount < 200 && rp.remainingAmount > 0)
                            lowResourceCount++;
                    }
                    if (lowResourceCount > 0)
                        score += 15.0 * lowResourceCount; // Better gather rates from depleting nodes
                }

                // Upgrade defensive buildings when threat is rising
                if (isDefensive && (aiState.threatRisingCount >= 2 || aiState.currentState == AIState.Defense))
                    score += 25.0;

                // Upgrade farms when food is the bottleneck
                if (building.buildingType == BuildingType.Farm)
                {
                    double foodRate = player.GetCollectionRate(ResourceType.Food);
                    if (foodRate < 3.0) score += 20.0; // Low food income — upgrade for better yield
                }

                // CC fast-track: always top priority when affordable
                if (building.buildingType == BuildingType.CityCenter)
                    score += 50.0;

                candidates.Add((building, score));
            }

            if (candidates.Count == 0) return new List<IEngineCommand>();

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var best = candidates[0];

            DebugLog.Log($"AI upgrading {best.building.buildingType.DisplayName()} (level {best.building.level} -> {best.building.level + 1})");
            return new List<IEngineCommand> { new AIUpgradeBuildingCommand(playerID, best.building.id) };
        }

        // ================================================================
        // Military Building Construction
        // ================================================================

        private IEngineCommand TryBuildMilitaryBuilding(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            int ccLevel = cityCenter.level;
            var buildings = gameState.GetBuildingsForPlayer(playerID);

            // Faction-specific military building priorities
            // Faction-specific military building priorities (data-driven via FactionAIConfig)
            var priorities = FactionAIConfig.Get(player.faction).MilitaryBuildOrder;

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

                DebugLog.Log($"AI building {priority.type.DisplayName()} at ({location.Value.q}, {location.Value.r})");
                return new AIBuildCommand(playerID, priority.type, location.Value, 0);
            }

            return null;
        }

        // ================================================================
        // Lumber Camp Priority
        // ================================================================

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

        // ================================================================
        // Feature 3: Villager Flee on Attack
        // ================================================================

        /// <summary>
        /// Detects enemy armies near AI villagers and orders them to flee toward the CC.
        /// Villagers that are gathering or idle (not building) will retreat when enemies
        /// approach within 4 tiles. Once the threat clears, they're removed from the
        /// fleeing set and will be reassigned by the normal gather logic.
        /// </summary>
        public List<IEngineCommand> GenerateVillagerFleeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return commands;

            var ccCoord = cityCenter.coordinate;

            // Clean up stale fleeing references
            var toRemoveFromFleeing = new List<Guid>();
            foreach (var fleeingID in aiState.fleeingVillagers)
            {
                var group = gameState.GetVillagerGroup(fleeingID);
                if (group == null)
                {
                    toRemoveFromFleeing.Add(fleeingID);
                    continue;
                }

                // Check if threat has cleared (no enemies within 6 tiles)
                var nearbyEnemies = gameState.GetEnemyArmies(group.coordinate, 6, playerID);
                if (nearbyEnemies.Count == 0 && group.currentPath == null)
                {
                    // Threat cleared — stop fleeing, normal gather logic will reassign
                    toRemoveFromFleeing.Add(fleeingID);
                }
            }
            foreach (var id in toRemoveFromFleeing)
                aiState.fleeingVillagers.Remove(id);

            // Check each non-fleeing villager for nearby threats
            var villagerGroups = gameState.GetVillagerGroupsForPlayer(playerID);
            foreach (var group in villagerGroups)
            {
                if (aiState.fleeingVillagers.Contains(group.id)) continue;

                // Don't interrupt builders — they're assigned to specific buildings
                if (group.currentTask is BuildingTask) continue;
                if (group.currentTask is UpgradingTask) continue;

                // Check for enemies within 4 tiles
                var nearbyEnemies = gameState.GetEnemyArmies(group.coordinate, 4, playerID);
                if (nearbyEnemies.Count == 0) continue;

                // Count total enemy units nearby
                int totalEnemyUnits = 0;
                foreach (var enemy in nearbyEnemies)
                    totalEnemyUnits += enemy.GetTotalUnits();

                // Only flee if there's a real threat (not a lone scout)
                if (totalEnemyUnits < 3) continue;

                // Stop gathering if actively gathering
                if (group.currentTask is GatheringTask)
                {
                    GameEngine.Instance.resourceEngine.StopGathering(group.id);
                }

                // Clear current task
                group.ClearTask();

                // Find safest destination: CC is always a good fallback
                var destination = ccCoord;

                // Check for a closer defensive building
                var playerBuildings = gameState.GetBuildingsForPlayer(playerID);
                int bestDist = group.coordinate.Distance(ccCoord);
                foreach (var building in playerBuildings)
                {
                    if (!building.IsOperational) continue;
                    // Prefer CC, towers, forts — buildings that are safe
                    bool isSafe = building.buildingType == BuildingType.CityCenter
                        || building.buildingType == BuildingType.Tower
                        || building.buildingType == BuildingType.WoodenFort
                        || building.buildingType == BuildingType.Castle;
                    if (!isSafe) continue;

                    int dist = group.coordinate.Distance(building.coordinate);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        destination = building.coordinate;
                    }
                }

                // Issue move command (isArmy=false for villager groups)
                commands.Add(new AIMoveCommand(playerID, group.id, destination, false));
                aiState.fleeingVillagers.Add(group.id);

                DebugLog.Log($"AI {playerID}: Villager group fleeing from ({group.coordinate.q},{group.coordinate.r}) to ({destination.q},{destination.r}) — {totalEnemyUnits} enemy units nearby");
            }

            return commands;
        }

        // ================================================================
        // Resource Camp Building
        // ================================================================

        /// <summary>
        /// Checks if a resource point is within coverage range of an existing camp.
        /// Coverage radius is 3 hexes — one camp should serve a cluster of nearby resources.
        /// </summary>
        public bool HasResourceCampCoverage(ResourcePointData resource, GameState gameState, Guid playerID)
        {
            if (!resource.resourceType.RequiresCamp()) return true;

            var requiredCampType = GetRequiredCampType(resource.resourceType);
            if (!requiredCampType.HasValue) return true;

            // Check within 3-hex radius for an existing camp of the right type
            // This prevents building a new camp for every individual resource node
            const int campCoverageRadius = 3;
            var tilesToCheck = resource.coordinate.CoordinatesWithinRange(campCoverageRadius);

            foreach (var coord in tilesToCheck)
            {
                var building = gameState.GetBuilding(coord);
                if (building != null &&
                    building.buildingType == requiredCampType.Value &&
                    building.ownerID.HasValue && building.ownerID.Value == playerID &&
                    (building.IsOperational || building.state == BuildingState.Constructing || building.state == BuildingState.Planning))
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

        private (ResourcePointData resource, BuildingType campType)? FindResourceNeedingCamp(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            var urgency = AnalyzeResourceNeeds(playerID, gameState);

            int lumberCampCount = gameState.GetBuildingCount(BuildingType.LumberCamp, playerID);
            int miningCampCount = gameState.GetBuildingCount(BuildingType.MiningCamp, playerID);

            var exploredResources = gameState.GetExploredResourcePoints(playerID);

            // Dynamic search radius: expand further when nearby resources depleted
            var playerBuildings = gameState.GetBuildingsForPlayer(playerID);
            bool hasMilitaryBuilding = playerBuildings.Any(b =>
                (b.buildingType == BuildingType.Barracks || b.buildingType == BuildingType.ArcheryRange
                || b.buildingType == BuildingType.Stable || b.buildingType == BuildingType.SiegeWorkshop)
                && b.IsOperational);
            int searchRadius = 10;
            if (hasMilitaryBuilding)
            {
                // Check if nearby resources are depleted — expand search if so
                int nearbyWithRemaining = 0;
                foreach (var rp in exploredResources)
                {
                    if (rp.remainingAmount > 100 && rp.coordinate.Distance(cityCenter.coordinate) <= 10)
                        nearbyWithRemaining++;
                }
                if (nearbyWithRemaining < 3) searchRadius = 15;
                if (nearbyWithRemaining < 1) searchRadius = 18;
            }

            var candidates = new List<(ResourcePointData resource, BuildingType campType, double score)>();

            foreach (var resource in exploredResources)
            {
                if (resource.remainingAmount <= 0) continue;
                if (!resource.resourceType.RequiresCamp()) continue;
                if (HasResourceCampCoverage(resource, gameState, playerID)) continue;

                int distance = Math.Max(1, resource.coordinate.Distance(cityCenter.coordinate));
                if (distance > searchRadius) continue;

                double resourceUrgency;
                urgency.TryGetValue(resource.resourceType.ResourceYield(), out resourceUrgency);
                if (resourceUrgency == 0) resourceUrgency = 0.5;

                BuildingType campType;
                switch (resource.resourceType)
                {
                    case ResourcePointType.Trees:
                        if (lumberCampCount >= maxCampsPerType) continue;
                        campType = BuildingType.LumberCamp;
                        break;
                    case ResourcePointType.OreMine:
                    case ResourcePointType.StoneQuarry:
                        if (miningCampCount >= maxCampsPerType) continue;
                        campType = BuildingType.MiningCamp;
                        break;
                    default:
                        continue;
                }

                if (!player.CanAfford(campType.BuildCost())) continue;

                double score = resourceUrgency * resource.remainingAmount / (100.0 * distance);
                candidates.Add((resource, campType, score));
            }

            if (candidates.Count == 0) return null;

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var best = candidates[0];
            return (best.resource, best.campType);
        }

        private IEngineCommand TryBuildResourceCamp(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;

            var result = FindResourceNeedingCamp(aiState, gameState);
            if (!result.HasValue) return null;

            var (resource, campType) = result.Value;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            if (!player.CanAfford(campType.BuildCost())) return null;

            if (gameState.CanBuildAt(resource.coordinate, playerID))
            {
                return new AIBuildCommand(playerID, campType, resource.coordinate, 0);
            }

            foreach (var neighbor in resource.coordinate.Neighbors())
            {
                if (gameState.CanBuildAt(neighbor, playerID))
                {
                    return new AIBuildCommand(playerID, campType, neighbor, 0);
                }
            }

            return null;
        }

        // ================================================================
        // Weighted Build Candidate Selection
        // ================================================================

        private IEngineCommand PickWeightedCandidate(List<(Func<IEngineCommand> tryBuild, double weight, string name)> candidates, System.Random rng)
        {
            if (candidates.Count == 0) return null;

            // Sort by weight descending
            candidates.Sort((a, b) => b.weight.CompareTo(a.weight));

            // 80% chance: pick highest-weight candidate that succeeds
            // 20% chance: try a random candidate first
            double roll = rng.NextDouble();
            if (roll < 0.8)
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

        // ================================================================
        // Scouting
        // ================================================================

        private IEngineCommand TryScoutUnexploredArea(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;

            // Don't use generic scouting if we have dedicated Scout units
            // (dedicated scouting is handled by AIMilitaryPlanner.GenerateScoutingCommands)
            var armies = gameState.GetArmiesForPlayer(playerID);
            bool hasDedicatedScouts = armies.Any(a =>
                a.GetUnitCount(MilitaryUnitType.Scout) > 0 &&
                (double)a.GetUnitCount(MilitaryUnitType.Scout) / Math.Max(1, a.GetTotalUnits()) >= 0.5);
            if (hasDedicatedScouts) return null;

            var scoutTarget = gameState.FindNearestUnexploredCoordinate(cityCenter.coordinate, playerID, scoutRange);
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
        // Feature 4: Adaptive Build Order Timing
        // ================================================================

        /// <summary>
        /// Checks if the AI is behind on build order milestones and triggers emergency mode.
        /// </summary>
        public void CheckBuildOrderMilestones(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            if (!AIHelper.ShouldExecute(ref aiState.lastMilestoneCheckTime, currentTime, GameConfig.AI.BuildOrderTiming.MilestoneCheckInterval))
                return;

            // Initialize milestones if null
            if (aiState.milestones == null)
                aiState.milestones = BuildOrderMilestone.ForStrategy(aiState.mapStrategy);

            var playerID = aiState.playerID;
            bool anyBehind = false;

            foreach (var milestone in aiState.milestones)
            {
                if (gameState.currentTime > milestone.targetTime)
                {
                    bool hasBuilding = gameState.GetBuildingsForPlayer(playerID)
                        .Any(b => b.buildingType == milestone.requiredBuilding);
                    if (!hasBuilding)
                    {
                        aiState.buildOrderEmergency = true;
                        aiState.emergencyBuildTarget = milestone.requiredBuilding;
                        anyBehind = true;
                        DebugLog.Log($"AI {playerID}: Build order emergency — missing {milestone.requiredBuilding} past {milestone.targetTime}s deadline");
                        break; // Handle the most urgent milestone first
                    }
                }
            }

            // If all milestones met, clear emergency flags
            if (!anyBehind)
            {
                aiState.buildOrderEmergency = false;
                aiState.emergencyBuildTarget = null;
            }
        }
    }
}
