// ============================================================================
// FILE: Commands/MoveScoutCommand.cs
// PURPOSE: Command to move a Mycelium Scout to a destination hex.
//          Validates ownership, stamina, and destination. Pathfinding is
//          handled by MovementEngine after the path is set.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class MoveScoutCommand : BaseEngineCommand
    {
        public Guid scoutID;
        public HexCoordinate destination;

        public MoveScoutCommand(Guid playerID, Guid scoutID, HexCoordinate destination)
            : base(playerID)
        {
            this.scoutID = scoutID;
            this.destination = destination;
        }

        // Reconstruction constructor for online deserialization
        public MoveScoutCommand(Guid id, Guid playerID, double timestamp, Guid scoutID, HexCoordinate destination)
            : base(id, playerID, timestamp)
        {
            this.scoutID = scoutID;
            this.destination = destination;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (!state.mapData.IsValidCoordinate(destination))
                return EngineCommandResult.Failure("Destination is not a valid coordinate");

            var scout = state.GetScout(scoutID);
            if (scout == null)
                return EngineCommandResult.Failure("Scout not found");

            if (!scout.ownerID.HasValue || scout.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Scout is not owned by this player");

            if (!scout.HasEnoughStamina())
                return EngineCommandResult.Failure("Scout lacks stamina to move");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder builder)
        {
            var scout = state.GetScout(scoutID);
            if (scout == null)
                return EngineCommandResult.Failure("Scout not found");

            // Pathfind to destination
            var path = state.mapData.FindPath(scout.coordinate, destination, scout.ownerID, state);
            if (path == null || path.Count == 0)
                return EngineCommandResult.Failure("No valid path to destination");

            scout.SetPath(path);

            builder.Add(new ScoutMovedChange
            {
                scoutID = scoutID,
                from = scout.coordinate,
                to = destination,
                path = path
            });

            return EngineCommandResult.Success(builder.Build().changes);
        }
    }
}
