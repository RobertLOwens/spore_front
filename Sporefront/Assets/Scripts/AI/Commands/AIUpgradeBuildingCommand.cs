// ============================================================================
// FILE: AI/Commands/AIUpgradeBuildingCommand.cs
// PURPOSE: AI command to upgrade a building to its next level
//          C# port of AIUpgradeBuildingCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIUpgradeBuildingCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public AIUpgradeBuildingCommand(Guid playerID, Guid buildingID)
            : base(playerID)
        {
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your building");

            if (!building.CanUpgrade)
                return EngineCommandResult.Failure("Building cannot be upgraded");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var cost = building.GetUpgradeCost();
            if (cost == null)
                return EngineCommandResult.Failure("No upgrade available");

            foreach (var kvp in cost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value))
                    return EngineCommandResult.Failure("Insufficient resources");
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var cost = building.GetUpgradeCost();
            if (cost == null)
                return EngineCommandResult.Failure("No upgrade available");

            // Deduct resources
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            int newLevel = building.level + 1;

            // Find nearest idle villager to dispatch as upgrader
            var idleVillagers = state.GetVillagerGroupsForPlayer(PlayerID)
                .Where(g => g.currentTask is IdleTask && g.currentPath == null)
                .OrderBy(g => g.coordinate.Distance(building.coordinate))
                .ToList();

            if (idleVillagers.Count > 0)
            {
                var upgrader = idleVillagers[0];

                if (upgrader.coordinate.Equals(building.coordinate))
                {
                    // Already on-site: start upgrade immediately
                    building.StartUpgrade();
                    changeBuilder.Add(new BuildingUpgradeStartedChange
                    {
                        buildingID = buildingID,
                        toLevel = newLevel
                    });
                }
                else
                {
                    // Dispatch villager -- building waits in pendingUpgrade until arrival
                    upgrader.AssignTask(new UpgradingTask(buildingID), building.coordinate, buildingID);
                    var path = state.mapData.FindPath(upgrader.coordinate, building.coordinate, PlayerID, state);
                    if (path != null && path.Count > 0)
                    {
                        upgrader.SetPath(path);
                        building.pendingUpgrade = true;
                    }
                    else
                    {
                        // No path found -- start upgrade as fallback
                        upgrader.ClearTask();
                        building.StartUpgrade();
                        changeBuilder.Add(new BuildingUpgradeStartedChange
                        {
                            buildingID = buildingID,
                            toLevel = newLevel
                        });
                    }
                }
            }
            else
            {
                // No idle villager -- start upgrade without one (graceful fallback)
                building.StartUpgrade();
                changeBuilder.Add(new BuildingUpgradeStartedChange
                {
                    buildingID = buildingID,
                    toLevel = newLevel
                });
            }

            DebugLog.Log(string.Format("AIUpgradeBuildingCommand: AI upgrading {0} to level {1}",
                building.buildingType, newLevel));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
