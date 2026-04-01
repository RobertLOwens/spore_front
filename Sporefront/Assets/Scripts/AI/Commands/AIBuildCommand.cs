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
    /// Build a structure: validate resources + location + idle villager availability,
    /// deduct cost, place building as Planning, dispatch nearest idle villager to build site.
    /// If villager is on-site, start construction immediately. If no path found, cancel and refund.
    /// AI must have an idle villager to build — no fallback construction without a builder.
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
            return BuildHelper.GetTerrainCostMultiplier(state, buildingType, coordinate, rotation, PlayerID);
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Check faction-exclusive building restrictions
            if (buildingType.IsFactionExclusive() && buildingType.ExclusiveFaction() != player.faction)
                return EngineCommandResult.Failure("This building is exclusive to another faction.");

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

            // Require an idle villager — AI must assign a builder just like the player does
            if (BuildHelper.FindNearestIdleVillager(state, PlayerID, coordinate) == null)
                return EngineCommandResult.Failure("No idle villager available to build");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

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

            // Find nearest idle villager to dispatch as builder (validated to exist)
            var builder = BuildHelper.FindNearestIdleVillager(state, PlayerID, coordinate);

            if (builder != null)
            {
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
                        // No path found -- cancel the build, refund resources
                        builder.ClearTask();
                        state.RemoveBuilding(building.id);
                        var buildCostRefund = buildingType.BuildCost();
                        double refundMultiplier = GetTerrainCostMultiplier(state);
                        foreach (var kvp in buildCostRefund)
                        {
                            int adjustedAmount = (int)Math.Ceiling(kvp.Value * refundMultiplier);
                            player.AddResource(kvp.Key, adjustedAmount, int.MaxValue);
                        }
                        return EngineCommandResult.Failure("No path to build site");
                    }
                }
            }
            else
            {
                // Should not reach here — Validate checks for idle villagers
                state.RemoveBuilding(building.id);
                var buildCostRefund = buildingType.BuildCost();
                double refundMultiplier = GetTerrainCostMultiplier(state);
                foreach (var kvp in buildCostRefund)
                {
                    int adjustedAmount = (int)Math.Ceiling(kvp.Value * refundMultiplier);
                    player.AddResource(kvp.Key, adjustedAmount, int.MaxValue);
                }
                return EngineCommandResult.Failure("No idle villager available to build");
            }

            DebugLog.Log($"AI built {buildingType} at ({coordinate.q}, {coordinate.r})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
