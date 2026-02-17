using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class ReinforceArmyCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Guid armyID;
        public Dictionary<MilitaryUnitType, int> units;

        public ReinforceArmyCommand(Guid playerID, Guid buildingID, Guid armyID, Dictionary<MilitaryUnitType, int> units)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.armyID = armyID;
            this.units = units;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Building exists and is owned by player
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            // Army exists and is owned by player
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

            if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Army is not owned by this player");

            // Total units must be > 0
            int totalUnits = 0;
            foreach (var kvp in units)
                totalUnits += kvp.Value;

            if (totalUnits <= 0)
                return EngineCommandResult.Failure("Must reinforce with at least one unit");

            // Building has enough of each unit type in garrison
            foreach (var kvp in units)
            {
                if (kvp.Value <= 0) continue;

                int garrisonCount = building.garrison.ContainsKey(kvp.Key) ? building.garrison[kvp.Key] : 0;
                if (garrisonCount < kvp.Value)
                    return EngineCommandResult.Failure(
                        string.Format("Building does not have enough {0} in garrison (have {1}, need {2})",
                            kvp.Key, garrisonCount, kvp.Value));
            }

            // Path exists from building to army
            var path = state.mapData.FindPath(building.coordinate, army.coordinate, PlayerID, state);
            if (path == null)
                return EngineCommandResult.Failure("No valid path from building to army");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found");

            // Find path from building to army
            var path = state.mapData.FindPath(building.coordinate, army.coordinate, PlayerID, state);
            if (path == null || path.Count == 0)
                return EngineCommandResult.Failure("No valid path found from building to army");

            // Remove units from garrison
            foreach (var kvp in units)
            {
                if (kvp.Value <= 0) continue;
                building.RemoveFromGarrison(kvp.Key, kvp.Value);

                // Emit ungarrisoned change for each unit type
                changeBuilder.Add(new UnitsUngarrisonedChange
                {
                    buildingID = buildingID,
                    unitType = kvp.Key.ToString(),
                    quantity = kvp.Value
                });
            }

            // Create a PendingReinforcement and register it on the target army
            var reinforcementID = Guid.NewGuid();
            double estimatedArrival = state.currentTime + path.Count * (1.0 / GameConfig.Movement.BaseSpeed);

            var unitsCopy = new Dictionary<MilitaryUnitType, int>(units);
            var reinforcement = new PendingReinforcement(
                reinforcementID,
                unitsCopy,
                estimatedArrival,
                building.coordinate,
                path
            );

            army.AddPendingReinforcement(reinforcement);

            // Emit army composition changed change to signal reinforcement dispatched
            var compositionDict = new Dictionary<string, int>();
            foreach (var kvp in army.militaryComposition)
                compositionDict[kvp.Key.ToString()] = kvp.Value;

            changeBuilder.Add(new ArmyCompositionChangedChange
            {
                armyID = army.id,
                newComposition = compositionDict
            });

            DebugLog.Log(string.Format("ReinforceArmyCommand: Dispatched reinforcement from {0} to army {1} ({2} units, {3} steps)",
                building.coordinate, army.name, GetTotalUnits(), path.Count));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }

        private int GetTotalUnits()
        {
            int total = 0;
            foreach (var kvp in units)
                total += kvp.Value;
            return total;
        }
    }
}
