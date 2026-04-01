using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AITrainVillagerCommand from AIController.swift.
    /// Queue villager training at city center. Validate CanTrainVillagers, 50 food per villager.
    /// Deduct food, call building.StartVillagerTraining().
    /// </summary>
    public class AITrainVillagerCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public int quantity;

        public AITrainVillagerCommand(Guid playerID, Guid buildingID, int quantity)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.quantity = quantity;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (!building.CanTrainVillagers())
                return EngineCommandResult.Failure("Cannot train villagers here");

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            if (!player.HasResource(ResourceType.Food, 50 * quantity))
                return EngineCommandResult.Failure("Insufficient food");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct food
            player.RemoveResource(ResourceType.Food, 50 * quantity);

            // Start villager training
            building.StartVillagerTraining(quantity, state.currentTime);

            changeBuilder.Add(new VillagerTrainingStartedChange
            {
                buildingID = buildingID,
                quantity = quantity,
                startTime = state.currentTime
            });

            DebugLog.Log($"AI training {quantity} villagers");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
