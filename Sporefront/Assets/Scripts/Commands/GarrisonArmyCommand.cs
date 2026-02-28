using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class GarrisonArmyCommand : BaseEngineCommand
    {
        public Guid armyID;
        public Guid buildingID;

        public GarrisonArmyCommand(Guid playerID, Guid armyID, Guid buildingID)
            : base(playerID)
        {
            this.armyID = armyID;
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

            if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Army is not owned by this player");

            if (army.isInCombat)
                return EngineCommandResult.Failure("Cannot garrison while in combat");

            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            // Army must be at the building's tile (or an occupied coordinate)
            bool atBuilding = building.Occupies(army.coordinate);
            if (!atBuilding)
                return EngineCommandResult.Failure("Army must be at the building to garrison");

            // Check garrison capacity
            int totalUnits = army.GetTotalUnits();
            if (!building.HasGarrisonSpace(totalUnits))
                return EngineCommandResult.Failure("Not enough garrison space in this building");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var army = state.GetArmy(armyID);
            var building = state.GetBuilding(buildingID);

            // Transfer all units to building garrison
            foreach (var kvp in army.militaryComposition)
            {
                if (kvp.Value <= 0) continue;
                building.AddToGarrison(kvp.Key, kvp.Value);

                changeBuilder.Add(new UnitsGarrisonedChange
                {
                    buildingID = buildingID,
                    unitType = kvp.Key.ToString(),
                    quantity = kvp.Value
                });
            }

            // Remove the army
            var coord = army.coordinate;
            state.RemoveArmy(armyID);

            changeBuilder.Add(new ArmyDestroyedChange
            {
                armyID = armyID,
                coordinate = coord
            });

            DebugLog.Log($"GarrisonArmyCommand: Army garrisoned into {building.buildingType} at ({coord.q},{coord.r})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
