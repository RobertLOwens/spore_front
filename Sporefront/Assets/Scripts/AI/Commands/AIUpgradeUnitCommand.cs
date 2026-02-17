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
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (building.buildingType != upgradeType.RequiredBuildingType())
                return EngineCommandResult.Failure("Wrong building type");

            if (building.level < upgradeType.RequiredBuildingLevel())
                return EngineCommandResult.Failure("Building level too low");

            // Check affordability
            var cost = upgradeType.Cost();
            foreach (var kvp in cost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value))
                    return EngineCommandResult.Failure("Insufficient resources");
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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

            DebugLog.Log(string.Format("AIUpgradeUnitCommand: AI started unit upgrade: {0}",
                upgradeType.DisplayName()));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
