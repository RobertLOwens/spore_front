using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class HuntCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;

        public HuntCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        // Reconstruction constructor for online deserialization
        public HuntCommand(Guid id, Guid playerID, double timestamp, Guid villagerGroupID, Guid resourcePointID)
            : base(id, playerID, timestamp)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            if (!resource.resourceType.IsHuntable())
                return EngineCommandResult.Failure("Target not huntable");

            if (resource.currentHealth <= 0)
                return EngineCommandResult.Failure("Target is already dead");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            // Assign hunting task (not GatheringResourceTask)
            group.AssignTask(new HuntingTask(resourcePointID), resource.coordinate, resourcePointID);

            // Set path if not already at the resource location
            if (!group.coordinate.Equals(resource.coordinate))
            {
                var path = state.mapData.FindPath(group.coordinate, resource.coordinate, PlayerID, state);
                if (path != null)
                {
                    group.SetPath(path);
                }
            }

            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "hunting",
                targetCoordinate = resource.coordinate
            });

            DebugLog.Log($"HuntCommand: Villagers assigned to hunt at ({resource.coordinate.q}, {resource.coordinate.r})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
