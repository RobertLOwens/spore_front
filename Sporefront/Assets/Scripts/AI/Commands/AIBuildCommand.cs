using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AIBuildCommand from AIController.swift.
    /// Build a structure: validate resources + location, deduct cost, place building as Planning,
    /// dispatch nearest idle villager to build site. If villager is on-site, start construction
    /// immediately. If no path found or no idle villager, start construction without (graceful fallback).
    /// The terrain cost multiplier applies if any occupied coordinate is Mountain.
    /// </summary>
    public class AIBuildCommand : BaseEngineCommand
    {
        public BuildingType buildingType;
        public HexCoordinate coordinate;
        public int rotation;

        public AIBuildCommand(Guid playerID, BuildingType buildingType, HexCoordinate coordinate, int rotation = 0)
            : base(playerID)
        {
            this.buildingType = buildingType;
            this.coordinate = coordinate;
            this.rotation = rotation;
        }

        private double GetTerrainCostMultiplier(GameState state)
        {
            var occupiedCoords = buildingType.GetOccupiedCoordinates(coordinate, rotation);
            bool hasMountain = occupiedCoords.Any(c => state.mapData.GetTerrain(c) == TerrainType.Mountain);
            return hasMountain ? GameConfig.Terrain.MountainBuildingCostMultiplier : 1.0;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            double costMultiplier = GetTerrainCostMultiplier(state);
            var buildCost = buildingType.BuildCost();
            foreach (var kvp in buildCost)
            {
                int adjustedAmount = (int)Math.Ceiling(kvp.Value * costMultiplier);
                if (!player.HasResource(kvp.Key, adjustedAmount))
                    return EngineCommandResult.Failure("Insufficient resources");
            }

            if (!state.CanBuildAt(coordinate, PlayerID))
                return EngineCommandResult.Failure("Cannot build at this location");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            // Deduct resources with terrain multiplier
            double costMultiplier = GetTerrainCostMultiplier(state);
            var buildCost = buildingType.BuildCost();
            foreach (var kvp in buildCost)
            {
                int adjustedAmount = (int)Math.Ceiling(kvp.Value * costMultiplier);
                player.RemoveResource(kvp.Key, adjustedAmount);
            }

            // Create and add building
            var building = new BuildingData(buildingType, coordinate, PlayerID, rotation);
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

                if (builder.coordinate.Equals(coordinate))
                {
                    // Already on-site: start construction immediately
                    building.StartConstruction(state.currentTime, builder.villagerCount);
                    changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
                }
                else
                {
                    // Dispatch villager -- building stays in Planning until arrival
                    var path = state.mapData.FindPath(builder.coordinate, coordinate, PlayerID, state);
                    if (path != null)
                    {
                        builder.SetPath(path);
                    }
                    else
                    {
                        // No path found -- start anyway as fallback
                        building.StartConstruction(state.currentTime, 1);
                        builder.ClearTask();
                        changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
                    }
                }
            }
            else
            {
                // No idle villager -- start construction without one (graceful fallback)
                building.StartConstruction(state.currentTime, 1);
                changeBuilder.Add(new BuildingConstructionStartedChange { buildingID = building.id });
            }

            DebugLog.Log(string.Format("AI built {0} at ({1}, {2})", buildingType, coordinate.q, coordinate.r));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
