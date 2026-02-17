using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class UpgradeUnitCommand : BaseEngineCommand
    {
        public string upgradeTypeRawValue;
        public Guid buildingID;

        public UpgradeUnitCommand(Guid playerID, string upgradeTypeRawValue, Guid buildingID)
            : base(playerID)
        {
            this.upgradeTypeRawValue = upgradeTypeRawValue;
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Parse upgrade type
            UnitUpgradeType upgradeType;
            if (!Enum.TryParse<UnitUpgradeType>(upgradeTypeRawValue, out upgradeType))
                return EngineCommandResult.Failure("Invalid upgrade type");

            // Player exists
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            // Building exists and owned by player
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            // Building is completed
            if (building.state != BuildingState.Completed)
                return EngineCommandResult.Failure("Building is not completed");

            // Building type matches required building type
            BuildingType requiredBuildingType = upgradeType.RequiredBuildingType();
            if (building.buildingType != requiredBuildingType)
                return EngineCommandResult.Failure(string.Format("Building type {0} does not match required type {1}",
                    building.buildingType, requiredBuildingType));

            // Building level sufficient
            int requiredLevel = upgradeType.RequiredBuildingLevel();
            if (building.level < requiredLevel)
                return EngineCommandResult.Failure(string.Format("Building level {0} is below required level {1}",
                    building.level, requiredLevel));

            // Prerequisite completed
            UnitUpgradeType? prerequisite = upgradeType.Prerequisite();
            if (prerequisite.HasValue)
            {
                if (!player.HasCompletedUnitUpgrade(prerequisite.Value.ToString()))
                    return EngineCommandResult.Failure(string.Format("Prerequisite upgrade {0} not completed",
                        prerequisite.Value));
            }

            // Not already completed
            if (player.HasCompletedUnitUpgrade(upgradeTypeRawValue))
                return EngineCommandResult.Failure("Upgrade already completed");

            // No active unit upgrade
            if (player.activeUnitUpgrade != null)
                return EngineCommandResult.Failure("Another unit upgrade is already in progress");

            // Can afford
            var cost = upgradeType.Cost();
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Cannot afford upgrade cost");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Parse upgrade type
            UnitUpgradeType upgradeType;
            if (!Enum.TryParse<UnitUpgradeType>(upgradeTypeRawValue, out upgradeType))
                return EngineCommandResult.Failure("Invalid upgrade type");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            // Deduct resources
            var cost = upgradeType.Cost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Start unit upgrade
            player.StartUnitUpgrade(upgradeTypeRawValue, buildingID, state.currentTime);

            // Emit state change
            changeBuilder.Add(new UnitUpgradeStartedChange
            {
                playerID = PlayerID,
                unitType = upgradeType.GetUnitType().ToString(),
                tier = upgradeType.Tier(),
                buildingID = buildingID,
                startTime = state.currentTime
            });

            DebugLog.Log(string.Format("UpgradeUnitCommand: Player {0} started {1} (tier {2}) at building {3}",
                PlayerID, upgradeTypeRawValue, upgradeType.Tier(), buildingID));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
