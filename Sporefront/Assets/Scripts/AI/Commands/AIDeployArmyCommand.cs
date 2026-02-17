using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AIDeployArmyCommand from AIController.swift.
    /// Deploy army from garrison. Validate garrison has enough units.
    /// Remove from garrison, find spawn coord via FindNearestWalkable, create ArmyData,
    /// assign home base respecting capacity, add military units to army.
    /// Auto-create CommanderData with RandomName + SpecialtyForComposition.
    /// Emit ArmyCreatedChange.
    /// </summary>
    public class AIDeployArmyCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Dictionary<MilitaryUnitType, int> composition;

        public AIDeployArmyCommand(Guid playerID, Guid buildingID, Dictionary<MilitaryUnitType, int> composition)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.composition = composition;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your building");

            foreach (var kvp in composition)
            {
                int available = building.garrison.ContainsKey(kvp.Key) ? building.garrison[kvp.Key] : 0;
                if (available < kvp.Value)
                    return EngineCommandResult.Failure(string.Format("Not enough {0}", kvp.Key.DisplayName()));
            }

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            // Remove units from garrison
            foreach (var kvp in composition)
            {
                building.RemoveFromGarrison(kvp.Key, kvp.Value);
            }

            // Find spawn position near building
            HexCoordinate spawnCoord = state.mapData.FindNearestWalkable(
                building.coordinate, 3, PlayerID, state
            ) ?? building.coordinate;

            // Create army
            var army = new ArmyData("AI Army", spawnCoord, PlayerID);

            // Assign home base respecting capacity limits
            if (state.HasHomeBaseCapacity(buildingID))
            {
                army.homeBaseID = buildingID;
            }
            else
            {
                var fallback = state.FindHomeBaseWithCapacity(PlayerID, spawnCoord, null);
                if (fallback != null)
                    army.homeBaseID = fallback.id;
                else
                    army.homeBaseID = buildingID;
            }

            // Add military units to army
            foreach (var kvp in composition)
            {
                army.AddMilitaryUnits(kvp.Key, kvp.Value);
            }

            // Auto-create a commander for this army
            var specialty = CommanderData.SpecialtyForComposition(army.militaryComposition);
            var commander = new CommanderData(
                CommanderData.RandomName(),
                specialty,
                PlayerID
            );
            commander.assignedArmyID = army.id;
            army.commanderID = commander.id;
            state.AddCommander(commander);

            // Add army to state
            state.AddArmy(army);

            // Build composition dict with string keys for the change event
            var compositionStrings = new Dictionary<string, int>();
            foreach (var kvp in army.militaryComposition)
            {
                compositionStrings[kvp.Key.ToString()] = kvp.Value;
            }

            changeBuilder.Add(new ArmyCreatedChange
            {
                armyID = army.id,
                ownerID = PlayerID,
                coordinate = spawnCoord,
                composition = compositionStrings
            });

            DebugLog.Log(string.Format("AI deployed army with {0} units, commander: {1} ({2})",
                army.GetTotalUnits(), commander.name, specialty));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
