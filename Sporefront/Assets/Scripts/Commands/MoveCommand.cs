using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class MoveCommand : BaseEngineCommand
    {
        public Guid entityID;
        public HexCoordinate destination;
        public bool isArmy;

        public MoveCommand(Guid playerID, Guid entityID, HexCoordinate destination, bool isArmy)
            : base(playerID)
        {
            this.entityID = entityID;
            this.destination = destination;
            this.isArmy = isArmy;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check destination is a valid coordinate
            if (!state.mapData.IsValidCoordinate(destination))
                return EngineCommandResult.Failure("Destination is not a valid coordinate");

            if (isArmy)
            {
                var army = state.GetArmy(entityID);
                if (army == null)
                    return EngineCommandResult.Failure("Army not found");

                if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                    return EngineCommandResult.Failure("Army is not owned by this player");

                if (army.isInCombat)
                    return EngineCommandResult.Failure("Army is currently in combat");

                // Check stacking limit at destination
                var armiesAtDestination = state.GetArmies(destination);
                if (armiesAtDestination.Count >= GameConfig.Stacking.MaxEntitiesPerTile)
                    return EngineCommandResult.Failure("Destination tile has reached the stacking limit");
            }
            else
            {
                var group = state.GetVillagerGroup(entityID);
                if (group == null)
                    return EngineCommandResult.Failure("Villager group not found");

                if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                    return EngineCommandResult.Failure("Villager group is not owned by this player");
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            if (isArmy)
            {
                var army = state.GetArmy(entityID);
                if (army == null)
                    return EngineCommandResult.Failure("Army not found");

                HexCoordinate fromCoordinate = army.coordinate;

                // Cancel entrenchment if entrenching or entrenched
                if (army.isEntrenching || army.isEntrenched)
                {
                    army.ClearEntrenchment();
                    changeBuilder.Add(new ArmyEntrenchmentCancelledChange
                    {
                        armyID = army.id,
                        coordinate = fromCoordinate
                    });
                    DebugLog.Log(string.Format("MoveCommand: Cancelled entrenchment for army {0}", army.name));
                }

                // Find path from army's current position to destination
                var path = state.mapData.FindPath(army.coordinate, destination, PlayerID, state);
                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure("No valid path found to destination");

                // Set movement path on the army
                army.currentPath = path;
                army.pathIndex = 0;
                army.movementProgress = 0.0;

                // Emit movement state change
                changeBuilder.Add(new ArmyMovedChange
                {
                    armyID = army.id,
                    from = fromCoordinate,
                    to = destination,
                    path = path
                });

                DebugLog.Log(string.Format("MoveCommand: Army {0} moving from {1} to {2} ({3} steps)",
                    army.name, fromCoordinate, destination, path.Count));
            }
            else
            {
                var group = state.GetVillagerGroup(entityID);
                if (group == null)
                    return EngineCommandResult.Failure("Villager group not found");

                HexCoordinate fromCoordinate = group.coordinate;

                // Find path from group's current position to destination
                var path = state.mapData.FindPath(group.coordinate, destination, PlayerID, state);
                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure("No valid path found to destination");

                // Set path and update task to moving
                group.SetPath(path);
                group.currentTask = new MovingTask(destination);

                // Emit movement state change
                changeBuilder.Add(new VillagerGroupMovedChange
                {
                    groupID = group.id,
                    from = fromCoordinate,
                    to = destination,
                    path = path
                });

                DebugLog.Log(string.Format("MoveCommand: Villager group {0} moving from {1} to {2} ({3} steps)",
                    group.name, fromCoordinate, destination, path.Count));
            }

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
