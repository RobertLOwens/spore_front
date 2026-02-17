// ============================================================================
// FILE: AI/Commands/AIMoveCommand.cs
// PURPOSE: AI command to move an army or villager group to a destination
//          C# port of AIMoveCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIMoveCommand : BaseEngineCommand
    {
        public Guid entityID;
        public HexCoordinate destination;
        public bool isArmy;

        public AIMoveCommand(Guid playerID, Guid entityID, HexCoordinate destination, bool isArmy)
            : base(playerID)
        {
            this.entityID = entityID;
            this.destination = destination;
            this.isArmy = isArmy;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (isArmy)
            {
                var army = state.GetArmy(entityID);
                if (army == null)
                    return EngineCommandResult.Failure("Army not found");

                if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                    return EngineCommandResult.Failure("Not your army");

                if (army.isInCombat)
                    return EngineCommandResult.Failure("Cannot move during combat");
            }
            else
            {
                var group = state.GetVillagerGroup(entityID);
                if (group == null)
                    return EngineCommandResult.Failure("Villager group not found");

                if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                    return EngineCommandResult.Failure("Not your villagers");
            }

            if (!state.mapData.IsValidCoordinate(destination))
                return EngineCommandResult.Failure("Invalid destination");

            // Check stacking limit
            if (state.mapData.GetEntityCount(destination) >= GameConfig.Stacking.MaxEntitiesPerTile)
                return EngineCommandResult.Failure("Tile is full");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            if (isArmy)
            {
                var army = state.GetArmy(entityID);
                if (army == null)
                    return EngineCommandResult.Failure("Army not found");

                var path = state.mapData.FindPath(army.coordinate, destination, PlayerID, state);
                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure("No valid path");

                army.currentPath = path;
                army.pathIndex = 0;
                army.movementProgress = 0.0;

                changeBuilder.Add(new ArmyMovedChange
                {
                    armyID = entityID,
                    from = army.coordinate,
                    to = destination,
                    path = path
                });

                DebugLog.Log(string.Format("AIMoveCommand: Army moving to ({0}, {1})",
                    destination.q, destination.r));
            }
            else
            {
                var group = state.GetVillagerGroup(entityID);
                if (group == null)
                    return EngineCommandResult.Failure("Villager group not found");

                var path = state.mapData.FindPath(group.coordinate, destination, PlayerID, state);
                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure("No valid path");

                group.SetPath(path);
                group.currentTask = new MovingTask(destination);

                changeBuilder.Add(new VillagerGroupMovedChange
                {
                    groupID = entityID,
                    from = group.coordinate,
                    to = destination,
                    path = path
                });
            }

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
