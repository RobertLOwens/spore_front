using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class DemolishCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public DemolishCommand(Guid playerID, Guid buildingID)
            : base(playerID)
        {
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check building exists
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found.");

            // Check owned by player
            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player.");

            // Check building is completed (not under construction, upgrading, etc.)
            if (building.state != BuildingState.Completed)
                return EngineCommandResult.Failure("Building must be completed before it can be demolished.");

            // Check building is not City Center (can't demolish)
            if (building.buildingType == BuildingType.CityCenter)
                return EngineCommandResult.Failure("Cannot demolish the City Center.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var building = state.GetBuilding(buildingID);

            // Start demolition
            building.StartDemolition(1);

            // Emit state change
            changeBuilder.Add(new BuildingDemolitionStartedChange
            {
                buildingID = buildingID
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    public class CancelDemolishCommand : BaseEngineCommand
    {
        public Guid buildingID;

        public CancelDemolishCommand(Guid playerID, Guid buildingID)
            : base(playerID)
        {
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check building exists
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found.");

            // Check owned by player
            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player.");

            // Check building is being demolished
            if (building.state != BuildingState.Demolishing)
                return EngineCommandResult.Failure("Building is not being demolished.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var building = state.GetBuilding(buildingID);

            // Cancel demolition (restores building to Completed state)
            building.CancelDemolition();

            // Emit state change
            changeBuilder.Add(new BuildingDemolitionCancelledChange
            {
                buildingID = buildingID
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
