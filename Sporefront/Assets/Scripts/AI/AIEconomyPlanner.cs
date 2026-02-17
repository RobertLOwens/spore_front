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

            // Build lumber camp ASAP if missing
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

            // Build farms if food urgency high
            var urgency = AnalyzeResourceNeeds(playerID, gameState);
            double foodUrgency;
            urgency.TryGetValue(ResourceType.Food, out foodUrgency);
            var foodRate = player.GetCollectionRate(ResourceType.Food);

            if ((foodUrgency > 0.5 || foodRate < 2.0) && currentTime - aiState.lastEconomicBuildTime >= buildInterval)
            {
                var cmd = TryBuildFarm(playerID, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastEconomicBuildTime = currentTime;
                }
            }

            // Build storage if near capacity
            bool shouldBuildStorage = urgency.Values.Any(u => u < 0.2);
            if (shouldBuildStorage && currentTime - aiState.lastEconomicBuildTime >= buildInterval)
            {
                var cmd = TryBuildStorage(playerID, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastEconomicBuildTime = currentTime;
                }
            }

            // Build Library if CC level >= 3
            if (currentTime - aiState.lastEconomicBuildTime >= buildInterval)
            {
                var cmd = TryBuildLibrary(playerID, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastEconomicBuildTime = currentTime;
                }
            }

            // Build houses if near pop cap
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

            // Build military buildings
            if (currentTime - aiState.lastMilitaryBuildTime >= buildInterval)
            {
                var cmd = TryBuildMilitaryBuilding(playerID, gameState);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    aiState.lastMilitaryBuildTime = currentTime;
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
                .ThenBy(r => r.coordinate.Distance(cityCenter.coordinate))
                .ToList();

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
                .Where(g => g.currentTask is IdleTask && g.currentPath == null)
                .ToList();

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

            return urgency;
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
                if (resourceUrgency < 0.2 && assignedCount >= 2)
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
        // Building Construction
        // ================================================================

        private IEngineCommand TryBuildFarm(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            var farmCost = BuildingType.Farm.BuildCost();
            foreach (var kvp in farmCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            var location = gameState.FindBuildLocation(cityCenter.coordinate, 4, playerID);
            if (!location.HasValue) return null;

            return new AIBuildCommand(playerID, BuildingType.Farm, location.Value, 0);
        }

        private IEngineCommand TryBuildHouse(Guid playerID, GameState gameState, double currentTime, AIPlayerState aiState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            var houseCost = BuildingType.Neighborhood.BuildCost();
            foreach (var kvp in houseCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            var location = gameState.FindBuildLocation(cityCenter.coordinate, 5, playerID);
            if (!location.HasValue) return null;

            return new AIBuildCommand(playerID, BuildingType.Neighborhood, location.Value, 0);
        }

        private IEngineCommand TryBuildStorage(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            int ccLevel = cityCenter.level;
            int currentWarehouses = gameState.GetBuildingCount(BuildingType.Warehouse, playerID);
            int maxWarehouses = BuildingTypeExtensions.MaxWarehousesAllowed(ccLevel);

            if (currentWarehouses >= maxWarehouses) return null;

            var warehouseCost = BuildingType.Warehouse.BuildCost();
            foreach (var kvp in warehouseCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            var location = gameState.FindBuildLocation(cityCenter.coordinate, 5, playerID);
            if (!location.HasValue) return null;

            return new AIBuildCommand(playerID, BuildingType.Warehouse, location.Value, 0);
        }

        private IEngineCommand TryBuildLibrary(Guid playerID, GameState gameState)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;
            var player = gameState.GetPlayer(playerID);
            if (player == null) return null;

            if (cityCenter.level < BuildingType.Library.RequiredCityCenterLevel()) return null;

            bool existingLibrary = gameState.GetBuildingsForPlayer(playerID).Any(b => b.buildingType == BuildingType.Library);
            if (existingLibrary) return null;

            var libraryCost = BuildingType.Library.BuildCost();
            foreach (var kvp in libraryCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

            var location = gameState.FindBuildLocation(cityCenter.coordinate, 4, playerID);
            if (!location.HasValue) return null;

            return new AIBuildCommand(playerID, BuildingType.Library, location.Value, 0);
        }

        // ================================================================
        // Building Upgrades
        // ================================================================

        public List<IEngineCommand> GenerateUpgradeCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastUpgradeCheckTime < GameConfig.AI.Intervals.UpgradeCheck) return new List<IEngineCommand>();
            aiState.lastUpgradeCheckTime = currentTime;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<IEngineCommand>();

            var buildings = gameState.GetBuildingsForPlayer(playerID);

            // Don't upgrade if already upgrading something
            if (buildings.Any(b => b.state == BuildingState.Upgrading)) return new List<IEngineCommand>();

            // Don't upgrade if wood reserves are too low
            if (player.GetResource(ResourceType.Wood) < 300) return new List<IEngineCommand>();

            // Don't upgrade CC until we have a lumber camp
            bool hasLumber = buildings.Any(b => b.buildingType == BuildingType.LumberCamp && b.IsOperational);
            if (!hasLumber) return new List<IEngineCommand>();

            // Score and pick the best building to upgrade
            var candidates = new List<(BuildingData building, double score)>();

            foreach (var building in buildings)
            {
                if (!building.CanUpgrade) continue;
                var cost = building.GetUpgradeCost();
                if (cost == null) continue;

                bool canAfford = true;
                foreach (var kvp in cost)
                {
                    if (!player.HasResource(kvp.Key, kvp.Value))
                    {
                        canAfford = false;
                        break;
                    }
                }
                if (!canAfford) continue;

                double score = 0.0;
                switch (building.buildingType)
                {
                    case BuildingType.CityCenter: score = 100.0; break;
                    case BuildingType.Barracks:
                    case BuildingType.ArcheryRange:
                    case BuildingType.Stable:
                    case BuildingType.SiegeWorkshop: score = 60.0; break;
                    case BuildingType.Farm: score = 40.0; break;
                    case BuildingType.Warehouse: score = 30.0; break;
                    case BuildingType.Blacksmith: score = 35.0; break;
                    case BuildingType.Library: score = 25.0; break;
                    default: score = 10.0; break;
                }

                score += (6 - building.level) * 5.0;
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

                var cost = priority.type.BuildCost();
                bool canAfford = true;
                foreach (var kvp in cost)
                {
                    if (!player.HasResource(kvp.Key, kvp.Value))
                    {
                        canAfford = false;
                        break;
                    }
                }
                if (!canAfford) continue;

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

            var campCost = BuildingType.LumberCamp.BuildCost();
            foreach (var kvp in campCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

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
        // Resource Camp Building
        // ================================================================

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

            var candidates = new List<(ResourcePointData resource, BuildingType campType, double score)>();

            foreach (var resource in exploredResources)
            {
                if (resource.remainingAmount <= 0) continue;
                if (!resource.resourceType.RequiresCamp()) continue;
                if (HasResourceCampCoverage(resource, gameState, playerID)) continue;

                int distance = Math.Max(1, resource.coordinate.Distance(cityCenter.coordinate));
                if (distance > 10) continue;

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

                var campCost = campType.BuildCost();
                bool canAfford = true;
                foreach (var kvp in campCost)
                {
                    if (!player.HasResource(kvp.Key, kvp.Value))
                    {
                        canAfford = false;
                        break;
                    }
                }
                if (!canAfford) continue;

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

            var campCost = campType.BuildCost();
            foreach (var kvp in campCost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value)) return null;
            }

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
        // Scouting
        // ================================================================

        private IEngineCommand TryScoutUnexploredArea(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return null;

            var scoutTarget = gameState.FindNearestUnexploredCoordinate(cityCenter.coordinate, playerID, scoutRange);
            if (!scoutTarget.HasValue) return null;

            var idleArmies = gameState.GetArmiesForPlayer(playerID)
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
    }
}
