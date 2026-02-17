using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class RetreatCommand : BaseEngineCommand
    {
        public Guid armyID;

        public RetreatCommand(Guid playerID, Guid armyID)
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

            // Army must be in combat or have a valid reason to retreat
            if (!army.isInCombat && !army.isEntrenched && !army.isEntrenching)
                return EngineCommandResult.Failure("Army has no reason to retreat");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

            // Find nearest home base
            BuildingData homeBase = null;

            // Check army's assigned home base first
            if (army.homeBaseID.HasValue)
            {
                var assignedBase = state.GetBuilding(army.homeBaseID.Value);
                if (assignedBase != null && assignedBase.IsOperational)
                {
                    homeBase = assignedBase;
                }
            }

            // Fallback to nearest building owned by player
            if (homeBase == null)
            {
                homeBase = state.FindNearestHomeBase(PlayerID, army.coordinate);
            }

            if (homeBase == null)
            {
                DebugLog.Log(string.Format("RetreatCommand: No home base available for army {0} to retreat to", army.name));
                return EngineCommandResult.Failure("No home base available for retreat");
            }

            // Find path to home base
            var path = state.mapData.FindPath(army.coordinate, homeBase.coordinate, PlayerID, state);
            if (path == null || path.Count == 0)
            {
                DebugLog.Log(string.Format("RetreatCommand: No valid path found for army {0} to retreat to {1}",
                    army.name, homeBase.coordinate));
                return EngineCommandResult.Failure("No valid path found to home base");
            }

            // Cancel entrenchment if active
            if (army.isEntrenching || army.isEntrenched)
            {
                army.ClearEntrenchment();
                changeBuilder.Add(new ArmyEntrenchmentCancelledChange
                {
                    armyID = army.id,
                    coordinate = army.coordinate
                });
                DebugLog.Log(string.Format("RetreatCommand: Cancelled entrenchment for army {0}", army.name));
            }

            // Disengage from combat if in combat
            if (army.isInCombat)
            {
                GameEngine.Instance.combatEngine.RetreatFromCombat(armyID);
                DebugLog.Log(string.Format("RetreatCommand: Disengaged army {0} from combat", army.name));
            }

            // Set army path and mark as retreating
            army.currentPath = path;
            army.pathIndex = 0;
            army.movementProgress = 0.0;
            army.isRetreating = true;

            // Emit retreat state change
            changeBuilder.Add(new ArmyRetreatingChange
            {
                armyID = army.id,
                to = homeBase.coordinate
            });

            DebugLog.Log(string.Format("RetreatCommand: Army {0} retreating from {1} to {2} ({3})",
                army.name, army.coordinate, homeBase.coordinate, homeBase.buildingType));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
