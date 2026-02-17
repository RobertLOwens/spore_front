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

        public override EngineCommandResult Validate(GameState state)
        {
            if (quantity <= 0)
                return EngineCommandResult.Failure("Quantity must be greater than zero");

            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            if (!building.CanTrain(unitType))
                return EngineCommandResult.Failure(string.Format("Building cannot train {0}", unitType.DisplayName()));

            // Check player can afford the training cost
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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

            DebugLog.Log(string.Format("TrainMilitaryCommand: Started training {0}x {1} at building {2}",
                quantity, unitType.DisplayName(), buildingID));

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

        public override EngineCommandResult Validate(GameState state)
        {
            if (quantity <= 0)
                return EngineCommandResult.Failure("Quantity must be greater than zero");

            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            if (!building.CanTrainVillagers())
                return EngineCommandResult.Failure("Building cannot train villagers (must be City Center or Neighborhood)");

            // Check player can afford
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

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

            DebugLog.Log(string.Format("TrainVillagerCommand: Started training {0} villager(s) at building {1}",
                quantity, buildingID));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
