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

        // Reconstruction constructor for online deserialization
        public ReinforceArmyCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, Guid armyID, Dictionary<MilitaryUnitType, int> units)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.armyID = armyID;
            this.units = units;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Building exists, is owned by player, and is operational
            var fail = ValidateOperationalBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            // Army exists and is owned by player
            fail = ValidateOwnedArmy(state, armyID, out var army);
            if (fail != null) return fail;

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
                        $"Building does not have enough {kvp.Key} in garrison (have {garrisonCount}, need {kvp.Value})");
            }

            // Path exists from building to army
            var path = state.mapData.FindPath(building.coordinate, army.coordinate, PlayerID, state);
            if (path == null)
                return EngineCommandResult.Failure("No valid path from building to army");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidateArmy(state, armyID, out var army);
            if (fail != null) return fail;

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

            DebugLog.Log($"ReinforceArmyCommand: Dispatched reinforcement from {building.coordinate} to army {army.name} ({GetTotalUnits()} units, {path.Count} steps)");

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
