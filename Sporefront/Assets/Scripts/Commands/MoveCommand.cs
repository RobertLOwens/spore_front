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

        // Reconstruction constructor for online deserialization
        public MoveCommand(Guid id, Guid playerID, double timestamp, Guid entityID, HexCoordinate destination, bool isArmy)
            : base(id, playerID, timestamp)
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
                var fail = ValidateOwnedArmy(state, entityID, out var army);
                if (fail != null) return fail;

                if (army.isInCombat)
                    return EngineCommandResult.Failure("Army is currently in combat");

                if (army.isStranded)
                    return EngineCommandResult.Failure("Army is stranded — build a home base first");

                // Check army has a commander
                if (!army.commanderID.HasValue)
                    return EngineCommandResult.Failure("Army requires a commander to move");

                // Check army has troops
                if (army.IsEmpty())
                    return EngineCommandResult.Failure("Army has no troops — assign units before moving");

                // Check commander stamina
                var commander = state.GetCommander(army.commanderID.Value);
                if (commander == null)
                    return EngineCommandResult.Failure("Commander not found");
                if (!commander.HasEnoughStamina())
                    return EngineCommandResult.Failure("Commander lacks stamina to issue this order");

                // Check stacking limit at destination
                var armiesAtDestination = state.GetArmies(destination);
                if (armiesAtDestination.Count >= GameConfig.Stacking.MaxEntitiesPerTile)
                    return EngineCommandResult.Failure("Destination tile has reached the stacking limit");
            }
            else
            {
                var fail = ValidateVillagerGroup(state, entityID, out var group);
                if (fail != null) return fail;
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            if (isArmy)
            {
                var fail = ValidateArmy(state, entityID, out var army);
                if (fail != null) return fail;

                // Consume commander stamina
                if (army.commanderID.HasValue)
                {
                    var commander = state.GetCommander(army.commanderID.Value);
                    if (commander != null)
                        commander.ConsumeStamina();
                }

                HexCoordinate fromCoordinate = army.coordinate;

                // Snap-to-nearest-tile: if army is mid-move and past halfway, advance to next tile
                if (army.currentPath != null && army.pathIndex < army.currentPath.Count
                    && army.movementProgress >= 0.5)
                {
                    HexCoordinate snapTarget = army.currentPath[army.pathIndex];
                    army.coordinate = snapTarget;
                    state.mapData.UpdateArmyPosition(army.id, snapTarget);
                    changeBuilder.Add(new ArmyMovedChange
                    {
                        armyID = army.id,
                        from = fromCoordinate,
                        to = snapTarget,
                        path = new List<HexCoordinate>()
                    });
                    fromCoordinate = snapTarget;
                }
                army.movementProgress = 0.0;

                // If army snapped to the destination itself, short-circuit
                if (army.coordinate.Equals(destination))
                {
                    army.currentPath = null;
                    army.pathIndex = 0;
                    army.movementSpeed = 0.0;
                    army.pendingAttackTarget = null;
                    return EngineCommandResult.Success(changeBuilder.Build().changes);
                }

                // Cancel entrenchment if entrenching or entrenched
                if (army.isEntrenching || army.isEntrenched)
                {
                    army.ClearEntrenchment();
                    changeBuilder.Add(new ArmyEntrenchmentCancelledChange
                    {
                        armyID = army.id,
                        coordinate = fromCoordinate
                    });
                    DebugLog.Log($"MoveCommand: Cancelled entrenchment for army {army.name}");
                }

                // Find path from army's current position to destination
                var path = state.mapData.FindPath(army.coordinate, destination, PlayerID, state);
                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure("No valid path found to destination");

                // Set movement path on the army
                army.currentPath = path;
                army.pathIndex = 0;
                army.movementProgress = 0.0;
                army.pendingAttackTarget = null;

                // Emit movement state change
                changeBuilder.Add(new ArmyMovedChange
                {
                    armyID = army.id,
                    from = fromCoordinate,
                    to = destination,
                    path = path
                });

                DebugLog.Log($"MoveCommand: Army {army.name} moving from {fromCoordinate} to {destination} ({path.Count} steps)");
            }
            else
            {
                var fail = ValidateVillagerGroup(state, entityID, out var group);
                if (fail != null) return fail;

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

                DebugLog.Log($"MoveCommand: Villager group {group.name} moving from {fromCoordinate} to {destination} ({path.Count} steps)");
            }

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
