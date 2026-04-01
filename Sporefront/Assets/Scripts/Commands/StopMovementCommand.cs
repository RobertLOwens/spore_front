using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class StopMovementCommand : BaseEngineCommand
    {
        public Guid armyID;

        public StopMovementCommand(Guid playerID, Guid armyID)
            : base(playerID)
        {
            this.armyID = armyID;
        }

        // Reconstruction constructor for online deserialization
        public StopMovementCommand(Guid id, Guid playerID, double timestamp, Guid armyID)
            : base(id, playerID, timestamp)
        {
            this.armyID = armyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedArmy(state, armyID, out var army);
            if (fail != null) return fail;

            if (army.isInCombat)
                return EngineCommandResult.Failure("Army is currently in combat");

            if (army.currentPath == null || army.pathIndex >= army.currentPath.Count)
                return EngineCommandResult.Failure("Army is not moving");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateArmy(state, armyID, out var army);
            if (fail != null) return fail;

            HexCoordinate originalCoord = army.coordinate;

            // Snap-to-nearest-tile: if past halfway, advance to next tile
            if (army.currentPath != null && army.pathIndex < army.currentPath.Count
                && army.movementProgress >= 0.5)
            {
                HexCoordinate snapTarget = army.currentPath[army.pathIndex];
                army.coordinate = snapTarget;
                state.mapData.UpdateArmyPosition(army.id, snapTarget);
                changeBuilder.Add(new ArmyMovedChange
                {
                    armyID = army.id,
                    from = originalCoord,
                    to = snapTarget,
                    path = new List<HexCoordinate>()
                });
            }

            // Clear movement state
            army.currentPath = null;
            army.pathIndex = 0;
            army.movementProgress = 0.0;
            army.movementSpeed = 0.0;
            army.isRetreating = false;
            army.pendingAttackTarget = null;

            DebugLog.Log($"StopMovementCommand: Army {army.name} stopped at {army.coordinate}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
