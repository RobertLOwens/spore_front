using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class BuildCommand : BaseEngineCommand
    {
        public BuildingType buildingType;
        public HexCoordinate coordinate;
        public int rotation;

        public BuildCommand(Guid playerID, BuildingType buildingType, HexCoordinate coordinate, int rotation = 0)
            : base(playerID)
        {
            this.buildingType = buildingType;
            this.coordinate = coordinate;
            this.rotation = rotation;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check player exists
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found.");

            // Get all coordinates this building will occupy
            var occupiedCoordinates = buildingType.GetOccupiedCoordinates(coordinate, rotation);

            // Validate each occupied coordinate
            foreach (var coord in occupiedCoordinates)
            {
                if (!state.mapData.IsValidCoordinate(coord))
                    return EngineCommandResult.Failure($"Invalid coordinate: ({coord.q}, {coord.r}).");

                if (!state.mapData.IsWalkable(coord))
                    return EngineCommandResult.Failure($"Coordinate ({coord.q}, {coord.r}) is not buildable.");

                if (state.GetBuilding(coord) != null)
                    return EngineCommandResult.Failure($"A building already exists at ({coord.q}, {coord.r}).");
            }

            // Check City Center level requirement
            int requiredCCLevel = buildingType.RequiredCityCenterLevel();
            int currentCCLevel = state.GetCityCenterLevel(PlayerID);
            if (currentCCLevel < requiredCCLevel)
                return EngineCommandResult.Failure($"Requires City Center level {requiredCCLevel} (current: {currentCCLevel}).");

            // Check resources
            var buildCost = buildingType.BuildCost();
            if (!player.CanAfford(buildCost))
                return EngineCommandResult.Failure("Insufficient resources.");

            // Check that at least one idle villager group exists
            var villagers = state.GetVillagerGroupsForPlayer(PlayerID);
            bool hasIdleVillager = villagers != null &&
                villagers.Any(g => g.currentTask.IsIdle && g.currentPath == null);
            if (!hasIdleVillager)
                return EngineCommandResult.Failure("No idle villagers available.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var player = state.GetPlayer(PlayerID);

            // Deduct resources
            var buildCost = buildingType.BuildCost();
            foreach (var kvp in buildCost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Create the building
            var building = new BuildingData(buildingType, coordinate, PlayerID, rotation);

            // Add building to game state
            state.AddBuilding(building);

            changeBuilder.Add(new BuildingPlacedChange
            {
                buildingID = building.id,
                buildingType = buildingType.ToString(),
                coordinate = coordinate,
                ownerID = PlayerID,
                rotation = rotation
            });

            // Find nearest idle villager to dispatch as builder
            var idleVillagers = state.GetVillagerGroupsForPlayer(PlayerID)
                .Where(g => g.currentTask.IsIdle && g.currentPath == null)
                .OrderBy(g => g.coordinate.Distance(coordinate))
                .ToList();

            if (idleVillagers.Count > 0)
            {
                var builder = idleVillagers[0];
                builder.AssignTask(new BuildingTask(building.id), coordinate, building.id);

                changeBuilder.Add(new VillagerGroupTaskChangedChange
                {
                    groupID = builder.id,
                    task = "Building",
                    targetCoordinate = coordinate
                });

                if (builder.coordinate.Equals(coordinate))
                {
                    // Already on-site: start construction immediately
                    building.StartConstruction(builder.villagerCount);
                    changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
                }
                else
                {
                    // Dispatch villager — building stays in Planning until arrival
                    var path = state.mapData.FindPath(builder.coordinate, coordinate, PlayerID, state);
                    if (path != null)
                    {
                        builder.SetPath(path);
                        changeBuilder.Add(new VillagerGroupMovedChange
                        {
                            groupID = builder.id,
                            from = builder.coordinate,
                            to = coordinate,
                            path = path
                        });
                    }
                    else
                    {
                        // No path found — start construction as fallback
                        building.StartConstruction(1);
                        builder.ClearTask();
                        changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
                    }
                }
            }
            else
            {
                // No idle villager — start construction without one (graceful fallback)
                building.StartConstruction(1);
                changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
            }

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
