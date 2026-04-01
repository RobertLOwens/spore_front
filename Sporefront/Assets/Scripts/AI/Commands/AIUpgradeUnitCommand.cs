// ============================================================================
// FILE: AI/Commands/AIUpgradeUnitCommand.cs
// PURPOSE: AI command to start a unit upgrade at a production building
//          C# port of AIUpgradeUnitCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIUpgradeUnitCommand : BaseEngineCommand
    {
        public UnitUpgradeType upgradeType;
        public Guid buildingID;

        public AIUpgradeUnitCommand(Guid playerID, UnitUpgradeType upgradeType, Guid buildingID)
            : base(playerID)
        {
            this.upgradeType = upgradeType;
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            if (player.IsUnitUpgradeActive())
                return EngineCommandResult.Failure("Unit upgrade already in progress");

            string rawValue = upgradeType.ToString();
            if (player.HasCompletedUnitUpgrade(rawValue))
                return EngineCommandResult.Failure("Unit upgrade already completed");

            // Check prerequisite
            UnitUpgradeType? prerequisite = upgradeType.Prerequisite();
            if (prerequisite.HasValue)
            {
                if (!player.HasCompletedUnitUpgrade(prerequisite.Value.ToString()))
                    return EngineCommandResult.Failure("Prerequisites not met");
            }

            // Check building exists and matches requirements
            fail = ValidateBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (building.buildingType != upgradeType.RequiredBuildingType())
                return EngineCommandResult.Failure("Wrong building type");

            if (building.level < upgradeType.RequiredBuildingLevel())
                return EngineCommandResult.Failure("Building level too low");

            // Check affordability
            var fail2 = ValidateCanAfford(player, upgradeType.Cost());
            if (fail2 != null) return fail2;

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            string rawValue = upgradeType.ToString();

            // Deduct resources
            var cost = upgradeType.Cost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Start unit upgrade
            player.StartUnitUpgrade(rawValue, buildingID, state.currentTime);

            // Emit state change
            changeBuilder.Add(new UnitUpgradeStartedChange
            {
                playerID = PlayerID,
                unitType = upgradeType.GetUnitType().ToString(),
                tier = upgradeType.Tier(),
                buildingID = buildingID,
                startTime = state.currentTime
            });

            DebugLog.Log($"AIUpgradeUnitCommand: AI started unit upgrade: {upgradeType.DisplayName()}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
