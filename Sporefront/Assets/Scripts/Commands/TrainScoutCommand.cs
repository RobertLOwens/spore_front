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
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            if (building.buildingType != BuildingType.CityCenter)
                return EngineCommandResult.Failure("Scouts can only be trained at the City Center");

            // Check player can afford
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var cost = new Dictionary<ResourceType, int> { { ResourceType.Food, GameConfig.Scout.FoodCost } };
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Not enough food to train scout");

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
            player.RemoveResource(ResourceType.Food, GameConfig.Scout.FoodCost);

            // Start scout training
            building.StartScoutTraining(state.currentTime);

            // Emit state change
            changeBuilder.Add(new ScoutTrainingStartedChange
            {
                buildingID = buildingID,
                startTime = state.currentTime
            });

            DebugLog.Log(string.Format("TrainScoutCommand: Started training scout at building {0}", buildingID));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
