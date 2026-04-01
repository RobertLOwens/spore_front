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
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (!building.CanUpgrade)
                return EngineCommandResult.Failure("Building cannot be upgraded");

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            var cost = building.GetUpgradeCost();
            if (cost == null)
                return EngineCommandResult.Failure("No upgrade available");

            var fail2 = ValidateCanAfford(player, cost);
            if (fail2 != null) return fail2;

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

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
                    building.StartUpgrade(state.currentTime);
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
                        building.StartUpgrade(state.currentTime);
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
                building.StartUpgrade(state.currentTime);
                changeBuilder.Add(new BuildingUpgradeStartedChange
                {
                    buildingID = buildingID,
                    toLevel = newLevel
                });
            }

            DebugLog.Log($"AIUpgradeBuildingCommand: AI upgrading {building.buildingType} to level {newLevel}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
