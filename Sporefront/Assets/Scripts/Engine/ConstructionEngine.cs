// ============================================================================
// FILE: Engine/ConstructionEngine.cs
// PURPOSE: Handles building construction, upgrades, and demolition
//          C# port of Swift ConstructionEngine.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    public class ConstructionEngine
    {
        // Constants
        private readonly double progressChangeThreshold = GameConfig.Construction.ProgressChangeThreshold;

        // State
        private GameState gameState;

        // Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        // ================================================================
        // Update Loop
        // ================================================================

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Snapshot building values to avoid collection-modified-during-enumeration
            var buildingList = gameState.buildings.Values.ToList();

            foreach (var building in buildingList)
            {
                // Check for builder arrival at planning-state buildings
                if (building.state == BuildingState.Planning)
                {
                    foreach (var group in gameState.villagerGroups.Values)
                    {
                        if (group.currentTask is BuildingTask bt && bt.BuildingID == building.id &&
                            group.coordinate == building.coordinate && group.currentPath == null)
                        {
                            building.StartConstruction(group.villagerCount);
                            changes.Add(new BuildingConstructionStartedChange { buildingID = building.id });
                            break;
                        }
                    }
                }

                // Check for upgrader arrival at pendingUpgrade buildings
                if (building.pendingUpgrade && building.state == BuildingState.Completed)
                {
                    foreach (var group in gameState.villagerGroups.Values)
                    {
                        if (group.currentTask is UpgradingTask ut && ut.BuildingID == building.id &&
                            group.coordinate == building.coordinate && group.currentPath == null)
                        {
                            building.pendingUpgrade = false;
                            building.StartUpgrade();
                            group.ClearTask();
                            changes.Add(new BuildingUpgradeStartedChange
                            {
                                buildingID = building.id,
                                toLevel = building.level + 1
                            });
                            changes.Add(new VillagerGroupTaskChangedChange
                            {
                                groupID = group.id,
                                task = "idle",
                                targetCoordinate = null
                            });
                            break;
                        }
                    }
                }

                // Construction updates
                if (building.state == BuildingState.Constructing)
                {
                    var constructionChanges = UpdateConstruction(building, currentTime);
                    changes.AddRange(constructionChanges);
                }

                // Upgrade updates
                if (building.state == BuildingState.Upgrading)
                {
                    var upgradeChanges = UpdateUpgrade(building, currentTime);
                    changes.AddRange(upgradeChanges);
                }

                // Demolition updates
                if (building.state == BuildingState.Demolishing)
                {
                    var demolitionChanges = UpdateDemolition(building, currentTime, gameState);
                    changes.AddRange(demolitionChanges);
                }
            }

            return changes;
        }

        // ================================================================
        // Construction
        // ================================================================

        private List<StateChange> UpdateConstruction(BuildingData building, double currentTime)
        {
            if (gameState == null) return new List<StateChange>();
            var changes = new List<StateChange>();

            double previousProgress = building.constructionProgress;
            bool completed = building.UpdateConstruction(currentTime);

            // Only emit progress change if it changed significantly
            if (Math.Abs(building.constructionProgress - previousProgress) > progressChangeThreshold)
            {
                changes.Add(new BuildingConstructionProgressChange
                {
                    buildingID = building.id,
                    progress = building.constructionProgress
                });
            }

            if (completed)
            {
                // Find and release any villagers assigned to build this building
                var builderChanges = ReleaseBuilders(building.id, gameState);
                changes.AddRange(builderChanges);

                changes.Add(new BuildingCompletedChange { buildingID = building.id });
            }

            return changes;
        }

        /// <summary>
        /// Finds villagers assigned to a building and clears their task, emitting state changes.
        /// </summary>
        private List<StateChange> ReleaseBuilders(Guid buildingID, GameState state)
        {
            var changes = new List<StateChange>();

            foreach (var group in state.villagerGroups.Values)
            {
                if (group.currentTask is BuildingTask bt && bt.BuildingID == buildingID)
                {
                    // Clear the villager's task
                    group.ClearTask();

                    // Emit state change for visual layer sync
                    changes.Add(new VillagerGroupTaskChangedChange
                    {
                        groupID = group.id,
                        task = "idle",
                        targetCoordinate = null
                    });
                }
            }

            return changes;
        }

        // ================================================================
        // Upgrades
        // ================================================================

        private List<StateChange> UpdateUpgrade(BuildingData building, double currentTime)
        {
            var changes = new List<StateChange>();

            double previousProgress = building.upgradeProgress;
            bool completed = building.UpdateUpgrade(currentTime);

            // Only emit progress change if it changed significantly
            if (Math.Abs(building.upgradeProgress - previousProgress) > progressChangeThreshold)
            {
                changes.Add(new BuildingUpgradeProgressChange
                {
                    buildingID = building.id,
                    progress = building.upgradeProgress
                });
            }

            if (completed)
            {
                changes.Add(new BuildingUpgradeCompletedChange
                {
                    buildingID = building.id,
                    newLevel = building.level
                });
            }

            return changes;
        }

        // ================================================================
        // Demolition
        // ================================================================

        private List<StateChange> UpdateDemolition(BuildingData building, double currentTime, GameState state)
        {
            var changes = new List<StateChange>();

            double previousProgress = building.demolitionProgress;
            bool completed = building.UpdateDemolition(currentTime);

            // Only emit progress change if it changed significantly
            if (Math.Abs(building.demolitionProgress - previousProgress) > progressChangeThreshold)
            {
                changes.Add(new BuildingDemolitionProgressChange
                {
                    buildingID = building.id,
                    progress = building.demolitionProgress
                });
            }

            if (completed)
            {
                HexCoordinate coordinate = building.coordinate;

                // Refund resources to owner
                if (building.ownerID.HasValue)
                {
                    var player = state.GetPlayer(building.ownerID.Value);
                    if (player != null)
                    {
                        var refund = building.GetDemolitionRefund();
                        foreach (var kvp in refund)
                        {
                            int capacity = state.GetStorageCapacity(building.ownerID.Value, kvp.Key);
                            player.AddResource(kvp.Key, kvp.Value, capacity);
                        }
                    }
                }

                // Remove building from state
                state.RemoveBuilding(building.id);

                changes.Add(new BuildingDemolishedChange
                {
                    buildingID = building.id,
                    coordinate = coordinate
                });
            }

            return changes;
        }

        // ================================================================
        // Building Placement
        // ================================================================

        public (bool valid, string reason) CanPlaceBuilding(BuildingType type, HexCoordinate coordinate,
            int rotation, Guid playerID)
        {
            if (gameState == null)
                return (false, "Invalid game state");

            var player = gameState.GetPlayer(playerID);
            if (player == null)
                return (false, "Invalid player");

            // Check city center level requirements
            int ccLevel = gameState.GetCityCenterLevel(playerID);
            if (type.RequiredCityCenterLevel() > ccLevel)
                return (false, $"Requires City Center Level {type.RequiredCityCenterLevel()}");

            // Check warehouse limits
            if (type == BuildingType.Warehouse)
            {
                int maxAllowed = BuildingTypeExtensions.MaxWarehousesAllowed(ccLevel);
                int currentCount = 0;
                foreach (var b in gameState.GetBuildingsForPlayer(playerID))
                {
                    if (b.buildingType == BuildingType.Warehouse) currentCount++;
                }
                if (currentCount >= maxAllowed)
                    return (false, "Maximum warehouses reached for City Center level");
            }

            // Check library limit (unique building - max 1)
            if (type == BuildingType.Library)
            {
                int currentCount = 0;
                foreach (var b in gameState.GetBuildingsForPlayer(playerID))
                {
                    if (b.buildingType == BuildingType.Library) currentCount++;
                }
                if (currentCount >= BuildingTypeExtensions.MaxLibrariesAllowed())
                    return (false, "Only one Library allowed per player");
            }

            // Check resources
            var buildCost = type.BuildCost();
            if (!player.CanAfford(buildCost))
                return (false, "Insufficient resources");

            // Get all coordinates this building would occupy
            var occupiedCoords = type.GetOccupiedCoordinates(coordinate, rotation);

            // Check all tiles
            foreach (var coord in occupiedCoords)
            {
                // Check map bounds
                if (!gameState.mapData.IsValidCoordinate(coord))
                    return (false, "Outside map bounds");

                // Check walkable terrain
                if (!gameState.mapData.IsWalkable(coord))
                    return (false, "Cannot build on this terrain");

                // Check for existing buildings
                if (gameState.mapData.GetBuildingID(coord).HasValue)
                    return (false, "Space already occupied");
            }

            // Special checks for camps
            if (type == BuildingType.MiningCamp)
            {
                var resource = gameState.GetResourcePoint(coordinate);
                if (resource != null)
                {
                    if (resource.resourceType != ResourcePointType.OreMine &&
                        resource.resourceType != ResourcePointType.StoneQuarry)
                        return (false, "Mining camp requires ore or stone resource");
                }
                else
                {
                    return (false, "Mining camp requires ore or stone resource");
                }
            }

            if (type == BuildingType.LumberCamp)
            {
                var resource = gameState.GetResourcePoint(coordinate);
                if (resource != null)
                {
                    if (resource.resourceType != ResourcePointType.Trees)
                        return (false, "Lumber camp requires trees");
                }
                else
                {
                    return (false, "Lumber camp requires trees");
                }
            }

            return (true, null);
        }

        public (BuildingData building, List<StateChange> changes) PlaceBuilding(BuildingType type,
            HexCoordinate coordinate, int rotation, Guid playerID)
        {
            if (gameState == null)
                return (null, new List<StateChange>());

            var player = gameState.GetPlayer(playerID);
            if (player == null)
                return (null, new List<StateChange>());

            // Validate placement
            var validation = CanPlaceBuilding(type, coordinate, rotation, playerID);
            if (!validation.valid)
                return (null, new List<StateChange>());

            var changes = new List<StateChange>();

            // Deduct resources
            var cost = type.BuildCost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Create building
            var building = new BuildingData(type, coordinate, playerID, rotation);

            // Add to game state
            gameState.AddBuilding(building);

            // Start construction
            building.StartConstruction(1);

            changes.Add(new BuildingPlacedChange
            {
                buildingID = building.id,
                buildingType = type.ToString(),
                coordinate = coordinate,
                ownerID = playerID,
                rotation = rotation
            });

            changes.Add(new BuildingConstructionStartedChange { buildingID = building.id });

            // Remove any resource point at this location (except for camps)
            if (type != BuildingType.MiningCamp && type != BuildingType.LumberCamp)
            {
                var occupiedCoords = type.GetOccupiedCoordinates(coordinate, rotation);
                foreach (var coord in occupiedCoords)
                {
                    Guid? resourceID = gameState.mapData.GetResourcePointID(coord);
                    if (resourceID.HasValue)
                    {
                        gameState.RemoveResourcePoint(resourceID.Value);
                    }
                }
            }

            return (building, changes);
        }

        // ================================================================
        // Upgrade Validation and Execution
        // ================================================================

        public (bool valid, string reason) CanStartUpgrade(Guid buildingID, Guid playerID)
        {
            if (gameState == null)
                return (false, "Invalid game state");

            var building = gameState.GetBuilding(buildingID);
            if (building == null)
                return (false, "Building not found");

            var player = gameState.GetPlayer(playerID);
            if (player == null)
                return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            if (!building.CanUpgrade)
                return (false, "Building cannot be upgraded");

            var upgradeCost = building.GetUpgradeCost();
            if (upgradeCost == null)
                return (false, "No upgrade available");

            if (!player.CanAfford(upgradeCost))
                return (false, "Insufficient resources");

            return (true, null);
        }

        public List<StateChange> StartUpgrade(Guid buildingID, Guid playerID)
        {
            if (gameState == null)
                return new List<StateChange>();

            var building = gameState.GetBuilding(buildingID);
            if (building == null)
                return new List<StateChange>();

            var player = gameState.GetPlayer(playerID);
            if (player == null)
                return new List<StateChange>();

            var validation = CanStartUpgrade(buildingID, playerID);
            if (!validation.valid)
                return new List<StateChange>();

            // Deduct resources
            var upgradeCost = building.GetUpgradeCost();
            if (upgradeCost != null)
            {
                foreach (var kvp in upgradeCost)
                {
                    player.RemoveResource(kvp.Key, kvp.Value);
                }
            }

            // Start upgrade
            int targetLevel = building.level + 1;
            building.StartUpgrade();

            return new List<StateChange>
            {
                new BuildingUpgradeStartedChange { buildingID = buildingID, toLevel = targetLevel }
            };
        }

        // ================================================================
        // Demolition Validation and Execution
        // ================================================================

        public (bool valid, string reason) CanStartDemolition(Guid buildingID, Guid playerID)
        {
            if (gameState == null)
                return (false, "Invalid game state");

            var building = gameState.GetBuilding(buildingID);
            if (building == null)
                return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            if (!building.CanDemolish)
                return (false, "Building cannot be demolished");

            return (true, null);
        }

        public List<StateChange> StartDemolition(Guid buildingID, Guid playerID, int demolishers = 1)
        {
            if (gameState == null)
                return new List<StateChange>();

            var building = gameState.GetBuilding(buildingID);
            if (building == null)
                return new List<StateChange>();

            var validation = CanStartDemolition(buildingID, playerID);
            if (!validation.valid)
                return new List<StateChange>();

            building.StartDemolition(demolishers);

            return new List<StateChange>
            {
                new BuildingDemolitionStartedChange { buildingID = buildingID }
            };
        }
    }
}
