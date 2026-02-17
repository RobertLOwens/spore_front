using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class UpgradeCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public UpgradeCommand(Guid playerID, Guid buildingID)
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
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (building.state != BuildingState.Completed)
                return EngineCommandResult.Failure("Building is not completed");

            if (building.state == BuildingState.Upgrading)
                return EngineCommandResult.Failure("Building is already upgrading");

            if (building.level >= building.MaxLevel)
                return EngineCommandResult.Failure("Building is already at max level");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var cost = building.GetUpgradeCost();
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Cannot afford upgrade cost");

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

            // Deduct resources
            var cost = building.GetUpgradeCost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            int toLevel = building.level + 1;

            // Start upgrade
            building.StartUpgrade();

            // Emit state change
            changeBuilder.Add(new BuildingUpgradeStartedChange
            {
                buildingID = buildingID,
                toLevel = toLevel
            });

            DebugLog.Log(string.Format("UpgradeCommand: Building {0} ({1}) upgrading to level {2}",
                buildingID, building.buildingType, toLevel));

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

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (building.state != BuildingState.Upgrading)
                return EngineCommandResult.Failure("Building is not upgrading");

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

            DebugLog.Log(string.Format("CancelUpgradeCommand: Cancelled upgrade for building {0} ({1})",
                buildingID, building.buildingType));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
