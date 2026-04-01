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

        // Reconstruction constructor for online deserialization
        public UpgradeUnitCommand(Guid id, Guid playerID, double timestamp, string upgradeTypeRawValue, Guid buildingID)
            : base(id, playerID, timestamp)
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
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Building exists and owned by player
            fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            // Building is completed
            if (building.state != BuildingState.Completed)
                return EngineCommandResult.Failure("Building is not completed");

            // Building type matches required building type
            BuildingType requiredBuildingType = upgradeType.RequiredBuildingType();
            if (building.buildingType != requiredBuildingType)
                return EngineCommandResult.Failure($"Building type {building.buildingType} does not match required type {requiredBuildingType}");

            // Building level sufficient
            int requiredLevel = upgradeType.RequiredBuildingLevel();
            if (building.level < requiredLevel)
                return EngineCommandResult.Failure($"Building level {building.level} is below required level {requiredLevel}");

            // Prerequisite completed
            UnitUpgradeType? prerequisite = upgradeType.Prerequisite();
            if (prerequisite.HasValue)
            {
                if (!player.HasCompletedUnitUpgrade(prerequisite.Value.ToString()))
                    return EngineCommandResult.Failure($"Prerequisite upgrade {prerequisite.Value} not completed");
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

            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

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

            DebugLog.Log($"UpgradeUnitCommand: Player {PlayerID} started {upgradeTypeRawValue} (tier {upgradeType.Tier()}) at building {buildingID}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
