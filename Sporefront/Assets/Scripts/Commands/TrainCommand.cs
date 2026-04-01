using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class TrainMilitaryCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public MilitaryUnitType unitType;
        public int quantity;

        public TrainMilitaryCommand(Guid playerID, Guid buildingID, MilitaryUnitType unitType, int quantity)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.unitType = unitType;
            this.quantity = quantity;
        }

        // Reconstruction constructor for online deserialization
        public TrainMilitaryCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, MilitaryUnitType unitType, int quantity)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.unitType = unitType;
            this.quantity = quantity;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (quantity <= 0)
                return EngineCommandResult.Failure("Quantity must be greater than zero");

            var fail = ValidateOperationalBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (!building.CanTrain(unitType))
                return EngineCommandResult.Failure($"Building cannot train {unitType.DisplayName()}");

            // Check player can afford the training cost
            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            var unitCost = unitType.TrainingCost();
            var totalCost = new Dictionary<ResourceType, int>();
            foreach (var kvp in unitCost)
                totalCost[kvp.Key] = kvp.Value * quantity;

            if (!player.CanAfford(totalCost))
                return EngineCommandResult.Failure("Not enough resources to train units");

            // Population check
            int currentPop, popCapacity;
            state.GetPopulationStats(PlayerID, out currentPop, out popCapacity);
            int popRequired = unitType.PopSpace() * quantity;

            if (currentPop + popRequired > popCapacity)
                return EngineCommandResult.Failure("Not enough population capacity");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct resources
            var unitCost = unitType.TrainingCost();
            foreach (var kvp in unitCost)
                player.RemoveResource(kvp.Key, kvp.Value * quantity);

            // Start training
            building.StartTraining(unitType, quantity, state.currentTime);

            // Emit state change
            changeBuilder.Add(new TrainingStartedChange
            {
                buildingID = buildingID,
                unitType = unitType.ToString(),
                quantity = quantity,
                startTime = state.currentTime
            });

            DebugLog.Log($"TrainMilitaryCommand: Started training {quantity}x {unitType.DisplayName()} at building {buildingID}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    public class TrainVillagerCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public int quantity;

        private static readonly Dictionary<ResourceType, int> VillagerCost =
            new Dictionary<ResourceType, int> { { ResourceType.Food, 50 } };

        public TrainVillagerCommand(Guid playerID, Guid buildingID, int quantity)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.quantity = quantity;
        }

        // Reconstruction constructor for online deserialization
        public TrainVillagerCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, int quantity)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.quantity = quantity;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (quantity <= 0)
                return EngineCommandResult.Failure("Quantity must be greater than zero");

            var fail = ValidateOperationalBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (!building.CanTrainVillagers())
                return EngineCommandResult.Failure("Building cannot train villagers (must be City Center or Neighborhood)");

            // Check player can afford
            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            var totalCost = new Dictionary<ResourceType, int>();
            foreach (var kvp in VillagerCost)
                totalCost[kvp.Key] = kvp.Value * quantity;

            if (!player.CanAfford(totalCost))
                return EngineCommandResult.Failure("Not enough resources to train villagers");

            // Population check
            int currentPop, popCapacity;
            state.GetPopulationStats(PlayerID, out currentPop, out popCapacity);

            if (currentPop + quantity > popCapacity)
                return EngineCommandResult.Failure("Not enough population capacity");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct resources
            foreach (var kvp in VillagerCost)
                player.RemoveResource(kvp.Key, kvp.Value * quantity);

            // Start villager training
            building.StartVillagerTraining(quantity, state.currentTime);

            // Emit state change
            changeBuilder.Add(new VillagerTrainingStartedChange
            {
                buildingID = buildingID,
                quantity = quantity,
                startTime = state.currentTime
            });

            DebugLog.Log($"TrainVillagerCommand: Started training {quantity} villager(s) at building {buildingID}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
