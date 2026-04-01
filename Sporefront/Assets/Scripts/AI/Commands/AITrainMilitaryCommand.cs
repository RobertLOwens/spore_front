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
            var fail = ValidateOperationalBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            fail = ValidateCanAfford(player, unitType.TrainingCost(), quantity);
            if (fail != null) return fail;

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;
            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            player.DeductCost(unitType.TrainingCost(), quantity);

            // Start training
            building.StartTraining(unitType, quantity, state.currentTime);

            changeBuilder.Add(new TrainingStartedChange
            {
                buildingID = buildingID,
                unitType = unitType.ToString(),
                quantity = quantity,
                startTime = state.currentTime
            });

            DebugLog.Log($"AI training {quantity}x {unitType.DisplayName()}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
