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

        public override EngineCommandResult Validate(GameState state)
        {
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

            if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Army is not owned by this player");

            if (army.isInCombat)
                return EngineCommandResult.Failure("Army is currently in combat");

            if (army.currentPath == null || army.pathIndex >= army.currentPath.Count)
                return EngineCommandResult.Failure("Army is not moving");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

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

            DebugLog.Log(string.Format("StopMovementCommand: Army {0} stopped at {1}", army.name, army.coordinate));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
