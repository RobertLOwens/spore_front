using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class UpgradeCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Guid? assignedVillagerGroupID;

        public UpgradeCommand(Guid playerID, Guid buildingID, Guid? villagerGroupID = null)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.assignedVillagerGroupID = villagerGroupID;
        }

        // Reconstruction constructor for online deserialization
        public UpgradeCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, Guid? assignedVillagerGroupID)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.assignedVillagerGroupID = assignedVillagerGroupID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (building.state != BuildingState.Completed)
                return EngineCommandResult.Failure("Building is not completed");

            if (building.state == BuildingState.Upgrading)
                return EngineCommandResult.Failure("Building is already upgrading");

            if (building.level >= building.MaxLevel)
                return EngineCommandResult.Failure("Building is already at max level");

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            var cost = building.GetUpgradeCost();
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Cannot afford upgrade cost");

            // Validate assigned villager if specified
            if (assignedVillagerGroupID.HasValue)
            {
                fail = ValidateVillagerGroup(state, assignedVillagerGroupID.Value, out _);
                if (fail != null) return fail;
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct resources
            var cost = building.GetUpgradeCost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            int toLevel = building.level + 1;

            if (assignedVillagerGroupID.HasValue)
            {
                // Dispatch villager to building for upgrade
                var upgrader = state.GetVillagerGroup(assignedVillagerGroupID.Value);
                if (upgrader != null)
                {
                    // Cancel current task if busy
                    if (!upgrader.currentTask.IsIdle)
                    {
                        if (upgrader.IsGathering())
                            GameEngine.Instance.resourceEngine.StopGathering(upgrader.id);
                        upgrader.ClearTask();
                        upgrader.ClearPath();
                    }

                    if (upgrader.coordinate.Equals(building.coordinate))
                    {
                        // Already on-site: start upgrade immediately
                        building.StartUpgrade(state.currentTime);
                        changeBuilder.Add(new BuildingUpgradeStartedChange
                        {
                            buildingID = buildingID,
                            toLevel = toLevel
                        });
                    }
                    else
                    {
                        // Dispatch villager — building waits in pendingUpgrade until arrival
                        upgrader.AssignTask(new UpgradingTask(buildingID), building.coordinate, buildingID);
                        var path = state.mapData.FindPath(upgrader.coordinate, building.coordinate, PlayerID, state);
                        if (path != null && path.Count > 0)
                        {
                            upgrader.SetPath(path);
                            building.pendingUpgrade = true;

                            changeBuilder.Add(new VillagerGroupTaskChangedChange
                            {
                                groupID = upgrader.id,
                                task = "Upgrading",
                                targetCoordinate = building.coordinate
                            });
                            changeBuilder.Add(new VillagerGroupMovedChange
                            {
                                groupID = upgrader.id,
                                from = upgrader.coordinate,
                                to = building.coordinate,
                                path = path
                            });
                        }
                        else
                        {
                            // No path found — start upgrade as fallback
                            upgrader.ClearTask();
                            building.StartUpgrade(state.currentTime);
                            changeBuilder.Add(new BuildingUpgradeStartedChange
                            {
                                buildingID = buildingID,
                                toLevel = toLevel
                            });
                        }
                    }
                }
                else
                {
                    // Villager not found — start upgrade without one (fallback)
                    building.StartUpgrade(state.currentTime);
                    changeBuilder.Add(new BuildingUpgradeStartedChange
                    {
                        buildingID = buildingID,
                        toLevel = toLevel
                    });
                }
            }
            else
            {
                // No villager assigned — start upgrade immediately (legacy behavior)
                building.StartUpgrade(state.currentTime);
                changeBuilder.Add(new BuildingUpgradeStartedChange
                {
                    buildingID = buildingID,
                    toLevel = toLevel
                });
            }

            DebugLog.Log($"UpgradeCommand: Building {buildingID} ({building.buildingType}) upgrading to level {toLevel}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    public class CancelUpgradeCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public CancelUpgradeCommand(Guid playerID, Guid buildingID)
            : base(playerID)
        {
            this.buildingID = buildingID;
        }

        // Reconstruction constructor for online deserialization
        public CancelUpgradeCommand(Guid id, Guid playerID, double timestamp, Guid buildingID)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (building.state != BuildingState.Upgrading)
                return EngineCommandResult.Failure("Building is not upgrading");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Cancel upgrade and get full cost for refund calculation
            var fullCost = building.CancelUpgrade();

            // Refund 50% of resources
            if (fullCost != null)
            {
                int storageCapacity;
                foreach (var kvp in fullCost)
                {
                    int refundAmount = kvp.Value / 2;
                    if (refundAmount > 0)
                    {
                        storageCapacity = state.GetStorageCapacity(PlayerID, kvp.Key);
                        player.AddResource(kvp.Key, refundAmount, storageCapacity);
                    }
                }
            }

            DebugLog.Log($"CancelUpgradeCommand: Cancelled upgrade for building {buildingID} ({building.buildingType})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
