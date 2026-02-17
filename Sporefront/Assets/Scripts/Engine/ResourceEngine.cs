// ============================================================================
// FILE: Engine/ResourceEngine.cs
// PURPOSE: Handles resource gathering logic - Unity C# port of ResourceEngine.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // MARK: - Gathering Assignment

    [System.Serializable]
    public struct GatheringAssignment
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;
        public double accumulator;
        public double woodConsumptionAccumulator;

        public GatheringAssignment(Guid villagerGroupID, Guid resourcePointID, double accumulator = 0.0)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
            this.accumulator = accumulator;
            this.woodConsumptionAccumulator = 0.0;
        }
    }

    // MARK: - Resource Engine

    /// <summary>
    /// Handles all resource gathering and production logic.
    /// </summary>
    public class ResourceEngine
    {
        // MARK: - State
        private GameState gameState;

        // MARK: - Gathering State
        private Dictionary<Guid, GatheringAssignment> gatheringAssignments =
            new Dictionary<Guid, GatheringAssignment>(); // VillagerGroupID -> Assignment

        // MARK: - Constants
        private readonly double baseGatherRatePerVillager = GameConfig.Resources.BaseGatherRatePerVillager;
        private readonly double adjacencyBonusPercent = GameConfig.Resources.AdjacencyBonusPercent;

        // MARK: - Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
            gatheringAssignments.Clear();

            // Restore gathering assignments from game state
            foreach (var group in gameState.villagerGroups.Values)
            {
                if (group.assignedResourcePointID.HasValue)
                {
                    gatheringAssignments[group.id] = new GatheringAssignment(
                        villagerGroupID: group.id,
                        resourcePointID: group.assignedResourcePointID.Value,
                        accumulator: group.gatheringAccumulator
                    );
                }
            }
        }

        // MARK: - Update Loop

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Update resource generation for all players
            foreach (var player in gameState.players.Values)
            {
                var playerChanges = UpdatePlayerResources(player, currentTime);
                changes.AddRange(playerChanges);
            }

            // Process all gathering assignments
            var gatheringChanges = ProcessGathering(currentTime);
            changes.AddRange(gatheringChanges);

            return changes;
        }

        // MARK: - Player Resource Updates

        private List<StateChange> UpdatePlayerResources(PlayerState player, double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Note: Resource addition from gathering is handled in ProcessGathering()
            // which directly adds resources and tracks resource point depletion.
            // We do NOT call player.UpdateResources() here to avoid double-counting.

            // Process food consumption for all players
            var consumptionInfo = gameState.GetFoodConsumptionRate(player.id);
            if (consumptionInfo.rate > 0)
            {
                double deltaTime = 0.5; // Match resource update interval

                // Apply rationing reduction from player's best commander
                var commanders = gameState.GetCommandersForPlayer(player.id);
                int bestRationing = 0;
                foreach (var commander in commanders)
                {
                    if (commander.Rationing > bestRationing)
                        bestRationing = commander.Rationing;
                }
                double rationingReduction = Math.Min(
                    GameConfig.Commander.RationingReductionCap,
                    bestRationing * GameConfig.Commander.RationingReductionScaling
                );
                double adjustedRate = consumptionInfo.rate * (1.0 - rationingReduction);

                int oldFood = player.GetResource(ResourceType.Food);
                int consumed = player.ConsumeFood(adjustedRate, deltaTime);

                if (consumed > 0)
                {
                    changes.Add(new ResourcesChangedChange
                    {
                        playerID = player.id,
                        resourceType = ResourceType.Food.ToString(),
                        oldAmount = oldFood,
                        newAmount = player.GetResource(ResourceType.Food)
                    });
                }
            }

            return changes;
        }

        // MARK: - Gathering Processing

        private List<StateChange> ProcessGathering(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();
            var completedAssignments = new List<Guid>();
            double deltaTime = 0.5; // Resource update interval

            // Iterate over a snapshot of keys to allow mutation during iteration
            var assignmentKeys = new List<Guid>(gatheringAssignments.Keys);
            foreach (var groupID in assignmentKeys)
            {
                var assignment = gatheringAssignments[groupID];

                var group = gameState.GetVillagerGroup(groupID);
                var resourcePoint = group != null
                    ? gameState.GetResourcePoint(assignment.resourcePointID)
                    : null;

                if (group == null || resourcePoint == null || resourcePoint.IsDepleted())
                {
                    completedAssignments.Add(groupID);
                    continue;
                }

                if (!group.ownerID.HasValue) continue;
                var player = gameState.GetPlayer(group.ownerID.Value);
                if (player == null) continue;

                // Farm wood consumption: farms require wood to operate
                if (resourcePoint.resourceType == ResourcePointType.Farmland)
                {
                    double woodRate = GameConfig.Resources.FarmWoodConsumptionRate;
                    assignment.woodConsumptionAccumulator += woodRate * deltaTime;

                    int woodToConsume = (int)assignment.woodConsumptionAccumulator;
                    if (woodToConsume > 0)
                    {
                        int availableWood = player.GetResource(ResourceType.Wood);
                        if (availableWood <= 0)
                        {
                            // No wood -- pause farming
                            gatheringAssignments[groupID] = assignment;
                            continue;
                        }
                        int consumed = Math.Min(woodToConsume, availableWood);
                        player.RemoveResource(ResourceType.Wood, consumed);
                        assignment.woodConsumptionAccumulator -= consumed;

                        changes.Add(new ResourcesChangedChange
                        {
                            playerID = player.id,
                            resourceType = ResourceType.Wood.ToString(),
                            oldAmount = availableWood,
                            newAmount = player.GetResource(ResourceType.Wood)
                        });
                    }
                }

                // Calculate gather rate
                double gatherRate = CalculateGatherRate(
                    group.villagerCount,
                    resourcePoint.resourceType,
                    resourcePoint.coordinate,
                    gameState
                );

                // Accumulate gathered resources
                assignment.accumulator += gatherRate * deltaTime;
                group.gatheringAccumulator = assignment.accumulator;

                // Convert to whole resources
                int wholeAmount = (int)assignment.accumulator;
                if (wholeAmount > 0)
                {
                    // Gather from resource point
                    int oldResourceAmount = resourcePoint.remainingAmount;
                    int actualGathered = resourcePoint.Gather(wholeAmount);

                    // Emit resource point amount change for visual sync
                    if (actualGathered > 0)
                    {
                        changes.Add(new ResourcePointAmountChangedChange
                        {
                            coordinate = resourcePoint.coordinate,
                            oldAmount = oldResourceAmount,
                            newAmount = resourcePoint.remainingAmount
                        });
                    }

                    // Add to player resources
                    ResourceType yieldType = resourcePoint.resourceType.ResourceYield();
                    int storageCapacity = gameState.GetStorageCapacity(player.id, yieldType);
                    int oldAmount = player.GetResource(yieldType);
                    int added = player.AddResource(yieldType, actualGathered, storageCapacity);

                    assignment.accumulator -= wholeAmount;
                    group.gatheringAccumulator = assignment.accumulator;

                    if (added > 0)
                    {
                        changes.Add(new ResourcesGatheredChange
                        {
                            playerID = player.id,
                            resourceType = yieldType.ToString(),
                            amount = added,
                            sourceCoordinate = resourcePoint.coordinate
                        });

                        changes.Add(new ResourcesChangedChange
                        {
                            playerID = player.id,
                            resourceType = yieldType.ToString(),
                            oldAmount = oldAmount,
                            newAmount = player.GetResource(yieldType)
                        });
                    }

                    // Check for depletion
                    if (resourcePoint.IsDepleted())
                    {
                        // Emit task change for the villager group going idle
                        // This must be emitted BEFORE resourcePointDepleted so the visual layer
                        // can update the villager before the resource is removed
                        changes.Add(new VillagerGroupTaskChangedChange
                        {
                            groupID = groupID,
                            task = "idle",
                            targetCoordinate = null
                        });

                        changes.Add(new ResourcePointDepletedChange
                        {
                            coordinate = resourcePoint.coordinate,
                            resourceType = resourcePoint.resourceType.ToString()
                        });

                        // Stop gathering assignment
                        completedAssignments.Add(groupID);
                    }
                }

                gatheringAssignments[groupID] = assignment;
            }

            // Clean up completed assignments
            foreach (var groupID in completedAssignments)
            {
                StopGathering(groupID);
            }

            return changes;
        }

        // MARK: - Gather Rate Calculation

        private double CalculateGatherRate(int villagerCount, ResourcePointType resourceType,
            HexCoordinate resourceCoordinate, GameState state)
        {
            // Base rate
            double rate = villagerCount * baseGatherRatePerVillager;

            // Apply adjacency bonuses
            double adjacencyMultiplier = CalculateAdjacencyBonus(resourceType, resourceCoordinate, state);
            rate *= adjacencyMultiplier;

            // Apply camp/farm level bonus
            double campLevelMultiplier = CalculateCampLevelBonus(resourceType, resourceCoordinate, state);
            rate *= campLevelMultiplier;

            return rate;
        }

        private double CalculateAdjacencyBonus(ResourcePointType resourceType,
            HexCoordinate coordinate, GameState state)
        {
            double multiplier = 1.0;

            // Check for relevant buildings nearby
            var neighbors = coordinate.Neighbors();

            foreach (var neighborCoord in neighbors)
            {
                var building = state.GetBuilding(neighborCoord);
                if (building != null && building.IsOperational)
                {
                    switch (resourceType)
                    {
                        case ResourcePointType.Farmland:
                            if (building.buildingType == BuildingType.Mill)
                            {
                                multiplier += adjacencyBonusPercent;
                            }
                            break;

                        case ResourcePointType.Trees:
                            if (building.buildingType == BuildingType.LumberCamp)
                            {
                                // Already covered by camp requirement
                            }
                            else if (building.buildingType == BuildingType.Warehouse)
                            {
                                multiplier += adjacencyBonusPercent;
                            }
                            break;

                        case ResourcePointType.OreMine:
                        case ResourcePointType.StoneQuarry:
                            if (building.buildingType == BuildingType.Warehouse)
                            {
                                multiplier += adjacencyBonusPercent;
                            }
                            break;

                        default:
                            break;
                    }
                }
            }

            return multiplier;
        }

        private double CalculateCampLevelBonus(ResourcePointType resourceType,
            HexCoordinate coordinate, GameState state)
        {
            // Determine which building type boosts this resource
            BuildingType matchingType;
            switch (resourceType)
            {
                case ResourcePointType.Farmland:
                    matchingType = BuildingType.Farm;
                    break;
                case ResourcePointType.Trees:
                    matchingType = BuildingType.LumberCamp;
                    break;
                case ResourcePointType.OreMine:
                case ResourcePointType.StoneQuarry:
                    matchingType = BuildingType.MiningCamp;
                    break;
                default:
                    return 1.0;
            }

            // Check the tile itself and all neighbors for the highest-level matching building
            var tilesToCheck = new List<HexCoordinate> { coordinate };
            tilesToCheck.AddRange(coordinate.Neighbors());
            int highestLevel = 0;

            foreach (var coord in tilesToCheck)
            {
                var building = state.GetBuilding(coord);
                if (building != null &&
                    building.buildingType == matchingType &&
                    building.IsOperational &&
                    building.level > highestLevel)
                {
                    highestLevel = building.level;
                }
            }

            if (highestLevel <= 1) return 1.0;
            return 1.0 + (highestLevel - 1) * GameConfig.Resources.CampLevelBonusPerLevel;
        }

        // MARK: - Gathering Assignment Management

        public bool StartGathering(Guid villagerGroupID, Guid resourcePointID)
        {
            if (gameState == null)
            {
                DebugLog.Log("StartGathering failed: No game state");
                return false;
            }

            var group = gameState.GetVillagerGroup(villagerGroupID);
            if (group == null)
            {
                DebugLog.Log($"StartGathering failed: VillagerGroup {villagerGroupID} not found in engine state");
                return false;
            }

            var resourcePoint = gameState.GetResourcePoint(resourcePointID);
            if (resourcePoint == null)
            {
                DebugLog.Log($"StartGathering failed: ResourcePoint {resourcePointID} not found in engine state");
                return false;
            }

            // Check if resource can accept more villagers
            if (!resourcePoint.CanAddVillagers(group.villagerCount))
            {
                return false;
            }

            // Check camp coverage for resources that require it
            if (resourcePoint.resourceType.RequiresCamp())
            {
                if (!HasCampCoverage(resourcePoint.coordinate, resourcePoint.resourceType, gameState))
                {
                    return false;
                }
            }

            // Register the assignment
            resourcePoint.AssignVillagerGroup(group.id, group.villagerCount);
            group.assignedResourcePointID = resourcePointID;
            group.currentTask = new GatheringResourceTask(resourcePointID);
            group.taskTargetCoordinate = resourcePoint.coordinate;
            group.taskTargetID = resourcePointID;

            gatheringAssignments[villagerGroupID] = new GatheringAssignment(
                villagerGroupID: villagerGroupID,
                resourcePointID: resourcePointID
            );

            return true;
        }

        public void StopGathering(Guid villagerGroupID)
        {
            if (gameState == null) return;

            var group = gameState.GetVillagerGroup(villagerGroupID);
            if (group == null) return;

            // Remove from resource point
            if (group.assignedResourcePointID.HasValue)
            {
                var resourcePoint = gameState.GetResourcePoint(group.assignedResourcePointID.Value);
                if (resourcePoint != null)
                {
                    resourcePoint.UnassignVillagerGroup(villagerGroupID, group.villagerCount);
                }
            }

            // Clear group state
            group.assignedResourcePointID = null;
            group.ClearTask();

            // Remove assignment
            gatheringAssignments.Remove(villagerGroupID);
        }

        // MARK: - Camp Coverage

        private bool HasCampCoverage(HexCoordinate coordinate, ResourcePointType resourceType, GameState state)
        {
            string requiredCampType = GetRequiredCampType(resourceType);
            if (requiredCampType == null)
            {
                return true; // No camp required
            }

            // Find all matching camps in the game state
            var matchingCamps = new List<BuildingData>();
            foreach (var building in state.buildings.Values)
            {
                if (building.buildingType.ToString() == requiredCampType && building.IsOperational)
                {
                    matchingCamps.Add(building);
                }
            }

            // Check if any camp can reach this coordinate via roads
            foreach (var camp in matchingCamps)
            {
                var reachable = GetExtendedCampReach(camp.coordinate, state);
                if (reachable.Contains(coordinate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the required camp type string for a resource point type, or null if no camp is required.
        /// </summary>
        private string GetRequiredCampType(ResourcePointType resourceType)
        {
            switch (resourceType)
            {
                case ResourcePointType.Trees:
                    return BuildingType.LumberCamp.ToString();
                case ResourcePointType.OreMine:
                case ResourcePointType.StoneQuarry:
                    return BuildingType.MiningCamp.ToString();
                default:
                    return null;
            }
        }

        /// <summary>
        /// BFS to find all coordinates reachable from a camp via connected buildings/roads.
        /// Mirrors HexMap.GetExtendedCampReach() but uses GameState data layer.
        /// </summary>
        private HashSet<HexCoordinate> GetExtendedCampReach(HexCoordinate campCoordinate, GameState state)
        {
            var reachable = new HashSet<HexCoordinate>();
            var visited = new HashSet<HexCoordinate>();
            var queue = new Queue<HexCoordinate>();
            queue.Enqueue(campCoordinate);

            // Camp tile + direct neighbors always reachable
            reachable.Add(campCoordinate);
            foreach (var neighbor in campCoordinate.Neighbors())
            {
                reachable.Add(neighbor);
            }

            // BFS through connected buildings (all operational buildings act as roads)
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                foreach (var neighbor in current.Neighbors())
                {
                    if (!state.mapData.IsValidCoordinate(neighbor)) continue;

                    var building = state.GetBuilding(neighbor);
                    if (building != null && building.IsOperational && !visited.Contains(neighbor))
                    {
                        // Add the building tile itself
                        reachable.Add(neighbor);
                        // Add all neighbors of the building tile (resource can be gathered)
                        foreach (var roadNeighbor in neighbor.Neighbors())
                        {
                            reachable.Add(roadNeighbor);
                        }
                        // Continue BFS through this building
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return reachable;
        }

        // MARK: - Collection Rate Management

        public void UpdateCollectionRates(Guid playerID)
        {
            if (gameState == null) return;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return;

            // Reset all rates
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                player.SetCollectionRate(resourceType, 0);
            }

            // Calculate rates from all gathering assignments
            foreach (var assignment in gatheringAssignments.Values)
            {
                var group = gameState.GetVillagerGroup(assignment.villagerGroupID);
                if (group == null || !group.ownerID.HasValue || group.ownerID.Value != playerID)
                    continue;

                var resourcePoint = gameState.GetResourcePoint(assignment.resourcePointID);
                if (resourcePoint == null) continue;

                double rate = CalculateGatherRate(
                    group.villagerCount,
                    resourcePoint.resourceType,
                    resourcePoint.coordinate,
                    gameState
                );

                ResourceType yieldType = resourcePoint.resourceType.ResourceYield();
                player.SetCollectionRate(yieldType, player.GetCollectionRate(yieldType) + rate);

                // Farm wood consumption shows as negative wood rate
                if (resourcePoint.resourceType == ResourcePointType.Farmland)
                {
                    double woodDrain = GameConfig.Resources.FarmWoodConsumptionRate;
                    double currentWoodRate = player.GetCollectionRate(ResourceType.Wood);
                    player.SetCollectionRate(ResourceType.Wood, currentWoodRate - woodDrain);
                }
            }
        }
    }
}
