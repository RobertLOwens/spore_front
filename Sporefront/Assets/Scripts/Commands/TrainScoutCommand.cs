// ============================================================================
// FILE: Commands/TrainScoutCommand.cs
// PURPOSE: Command to train a Mycelium Scout from the City Center.
//          Validates ownership, building type, and resources.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class TrainScoutCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public TrainScoutCommand(Guid playerID, Guid buildingID)
            : base(playerID)
        {
            this.buildingID = buildingID;
        }

        // Reconstruction constructor for online deserialization
        public TrainScoutCommand(Guid id, Guid playerID, double timestamp, Guid buildingID)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOperationalBuilding(state, buildingID, out var building, BuildingType.CityCenter);
            if (fail != null) return fail;

            // Check player can afford
            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            var cost = new Dictionary<ResourceType, int> { { ResourceType.Food, GameConfig.Scout.FoodCost } };
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Not enough food to train scout");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct resources
            player.RemoveResource(ResourceType.Food, GameConfig.Scout.FoodCost);

            // Start scout training
            building.StartScoutTraining(state.currentTime);

            // Emit state change
            changeBuilder.Add(new ScoutTrainingStartedChange
            {
                buildingID = buildingID,
                startTime = state.currentTime
            });

            DebugLog.Log($"TrainScoutCommand: Started training scout at building {buildingID}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
