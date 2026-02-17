using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AITrainMilitaryCommand from AIController.swift.
    /// Queue military unit training. Validate building ownership, operational status, and resources.
    /// Deduct resources, call building.StartTraining().
    /// </summary>
    public class AITrainMilitaryCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public MilitaryUnitType unitType;
        public int quantity;

        public AITrainMilitaryCommand(Guid playerID, Guid buildingID, MilitaryUnitType unitType, int quantity)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.unitType = unitType;
            this.quantity = quantity;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your building");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building not operational");

            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var unitCost = unitType.TrainingCost();
            foreach (var kvp in unitCost)
            {
                int totalCost = kvp.Value * quantity;
                if (!player.HasResource(kvp.Key, totalCost))
                    return EngineCommandResult.Failure("Insufficient resources");
            }

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            var player = state.GetPlayer(PlayerID);
            if (building == null || player == null)
                return EngineCommandResult.Failure("Not found");

            // Deduct resources
            var unitCost = unitType.TrainingCost();
            foreach (var kvp in unitCost)
            {
                int totalCost = kvp.Value * quantity;
                player.RemoveResource(kvp.Key, totalCost);
            }

            // Start training
            building.StartTraining(unitType, quantity, state.currentTime);

            changeBuilder.Add(new TrainingStartedChange
            {
                buildingID = buildingID,
                unitType = unitType.ToString(),
                quantity = quantity,
                startTime = state.currentTime
            });

            DebugLog.Log(string.Format("AI training {0}x {1}", quantity, unitType.DisplayName()));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
