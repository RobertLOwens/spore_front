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

        // Reconstruction constructor for online deserialization
        public RetreatCommand(Guid id, Guid playerID, double timestamp, Guid armyID)
            : base(id, playerID, timestamp)
        {
            this.armyID = armyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedArmy(state, armyID, out var army);
            if (fail != null) return fail;

            if (army.isRetreating)
                return EngineCommandResult.Failure("Army is already retreating");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateArmy(state, armyID, out var army);
            if (fail != null) return fail;

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
                DebugLog.Log($"RetreatCommand: No home base available for army {army.name} to retreat to");
                return EngineCommandResult.Failure("No home base available for retreat");
            }

            // Find path to home base
            var path = state.mapData.FindPath(army.coordinate, homeBase.coordinate, PlayerID, state);
            if (path == null || path.Count == 0)
            {
                DebugLog.Log($"RetreatCommand: No valid path found for army {army.name} to retreat to {homeBase.coordinate}");
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
                DebugLog.Log($"RetreatCommand: Cancelled entrenchment for army {army.name}");
            }

            // Disengage from combat if in combat
            if (army.isInCombat)
            {
                GameEngine.Instance.combatEngine.RetreatFromCombat(armyID);
                DebugLog.Log($"RetreatCommand: Disengaged army {army.name} from combat");
            }

            // Clear any pending attack and set army path as retreating
            army.pendingAttackTarget = null;
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

            DebugLog.Log($"RetreatCommand: Army {army.name} retreating from {army.coordinate} to {homeBase.coordinate} ({homeBase.buildingType})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
