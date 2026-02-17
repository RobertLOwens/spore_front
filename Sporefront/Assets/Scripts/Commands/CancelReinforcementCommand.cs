using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class CancelReinforcementCommand : BaseEngineCommand
    {
        public Guid reinforcementID;

        public CancelReinforcementCommand(Guid playerID, Guid reinforcementID)
            : base(playerID)
        {
            this.reinforcementID = reinforcementID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Find the reinforcement across all armies
            ArmyData owningArmy = null;
            PendingReinforcement? foundReinforcement = null;

            foreach (var army in state.armies.Values)
            {
                foreach (var reinforcement in army.pendingReinforcements)
                {
                    if (reinforcement.reinforcementID == reinforcementID)
                    {
                        owningArmy = army;
                        foundReinforcement = reinforcement;
                        break;
                    }
                }
                if (owningArmy != null) break;
            }

            if (owningArmy == null || !foundReinforcement.HasValue)
                return EngineCommandResult.Failure("Reinforcement not found");

            // Owned by player (check the army's owner)
            if (!owningArmy.ownerID.HasValue || owningArmy.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Reinforcement is not owned by this player");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Find the reinforcement and its owning army
            ArmyData owningArmy = null;
            PendingReinforcement? foundReinforcement = null;

            foreach (var army in state.armies.Values)
            {
                foreach (var reinforcement in army.pendingReinforcements)
                {
                    if (reinforcement.reinforcementID == reinforcementID)
                    {
                        owningArmy = army;
                        foundReinforcement = reinforcement;
                        break;
                    }
                }
                if (owningArmy != null) break;
            }

            if (owningArmy == null || !foundReinforcement.HasValue)
                return EngineCommandResult.Failure("Reinforcement not found");

            var reinforcement = foundReinforcement.Value;

            // Remove the reinforcement from the army
            owningArmy.RemovePendingReinforcement(reinforcementID);

            // Return units to the source building's garrison
            var sourceBuilding = state.GetBuilding(reinforcement.sourceCoordinate);
            if (sourceBuilding == null)
            {
                // Try to find a building at the source coordinate via map data
                var buildingAtSource = state.GetBuildingAt(reinforcement.sourceCoordinate);
                if (buildingAtSource != null)
                    sourceBuilding = buildingAtSource;
            }

            if (sourceBuilding != null && sourceBuilding.IsOperational)
            {
                // Return units to source building garrison
                foreach (var kvp in reinforcement.unitComposition)
                {
                    if (kvp.Value <= 0) continue;
                    sourceBuilding.AddToGarrison(kvp.Key, kvp.Value);

                    changeBuilder.Add(new UnitsGarrisonedChange
                    {
                        buildingID = sourceBuilding.id,
                        unitType = kvp.Key.ToString(),
                        quantity = kvp.Value
                    });
                }

                DebugLog.Log(string.Format("CancelReinforcementCommand: Returned {0} units to building at {1}",
                    reinforcement.GetTotalUnits(), sourceBuilding.coordinate));
            }
            else
            {
                // Source building no longer exists or is not operational
                // Find nearest operational building owned by player to return units
                var fallbackBuilding = state.FindNearestHomeBase(PlayerID, reinforcement.currentCoordinate);
                if (fallbackBuilding != null)
                {
                    foreach (var kvp in reinforcement.unitComposition)
                    {
                        if (kvp.Value <= 0) continue;
                        fallbackBuilding.AddToGarrison(kvp.Key, kvp.Value);

                        changeBuilder.Add(new UnitsGarrisonedChange
                        {
                            buildingID = fallbackBuilding.id,
                            unitType = kvp.Key.ToString(),
                            quantity = kvp.Value
                        });
                    }

                    DebugLog.Log(string.Format(
                        "CancelReinforcementCommand: Source building unavailable, returned {0} units to fallback building at {1}",
                        reinforcement.GetTotalUnits(), fallbackBuilding.coordinate));
                }
                else
                {
                    DebugLog.Log(string.Format(
                        "CancelReinforcementCommand: No building available to return {0} units, units lost",
                        reinforcement.GetTotalUnits()));
                }
            }

            // Emit army composition changed to reflect reinforcement cancellation
            var compositionDict = new Dictionary<string, int>();
            foreach (var kvp in owningArmy.militaryComposition)
                compositionDict[kvp.Key.ToString()] = kvp.Value;

            changeBuilder.Add(new ArmyCompositionChangedChange
            {
                armyID = owningArmy.id,
                newComposition = compositionDict
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
