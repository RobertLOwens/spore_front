using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class GatherCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;

        public GatherCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        // Reconstruction constructor for online deserialization
        public GatherCommand(Guid id, Guid playerID, double timestamp, Guid villagerGroupID, Guid resourcePointID)
            : base(id, playerID, timestamp)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            // Resource point must exist
            var resourcePoint = state.GetResourcePoint(resourcePointID);
            if (resourcePoint == null)
                return EngineCommandResult.Failure("Resource point not found");

            // Resource point must not be depleted
            if (resourcePoint.IsDepleted())
                return EngineCommandResult.Failure("Resource point is depleted");

            // Villager group must be idle or not already gathering
            if (group.IsGathering())
                return EngineCommandResult.Failure("Villager group is already gathering");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            var resourcePoint = state.GetResourcePoint(resourcePointID);
            if (resourcePoint == null)
                return EngineCommandResult.Failure("Resource point not found");

            if (group.coordinate.Equals(resourcePoint.coordinate))
            {
                // Already at the resource — start gathering immediately
                bool success = GameEngine.Instance.resourceEngine.StartGathering(villagerGroupID, resourcePointID);
                if (!success)
                {
                    DebugLog.Log($"GatherCommand: StartGathering failed for group {villagerGroupID} at resource {resourcePointID}");
                    return EngineCommandResult.Failure("Failed to start gathering");
                }

                changeBuilder.Add(new VillagerGroupTaskChangedChange
                {
                    groupID = villagerGroupID,
                    task = "gathering",
                    targetCoordinate = resourcePoint.coordinate
                });

                DebugLog.Log($"GatherCommand: Villager group {group.name} now gathering at {resourcePoint.coordinate}");
            }
            else
            {
                // Not at resource — assign task and pathfind (StartGathering deferred to arrival)
                group.AssignTask(new GatheringResourceTask(resourcePointID), resourcePoint.coordinate, resourcePointID);

                var path = state.mapData.FindPath(group.coordinate, resourcePoint.coordinate, PlayerID, state);
                if (path != null)
                {
                    group.SetPath(path);
                }

                changeBuilder.Add(new VillagerGroupTaskChangedChange
                {
                    groupID = villagerGroupID,
                    task = "gathering",
                    targetCoordinate = resourcePoint.coordinate
                });

                DebugLog.Log($"GatherCommand: Villager group {group.name} moving to gather at {resourcePoint.coordinate}");
            }

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    public class StopGatheringCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;

        public StopGatheringCommand(Guid playerID, Guid villagerGroupID)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
        }

        // Reconstruction constructor for online deserialization
        public StopGatheringCommand(Guid id, Guid playerID, double timestamp, Guid villagerGroupID)
            : base(id, playerID, timestamp)
        {
            this.villagerGroupID = villagerGroupID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            // Must be currently gathering
            if (!group.IsGathering())
                return EngineCommandResult.Failure("Villager group is not currently gathering");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateVillagerGroup(state, villagerGroupID, out var group);
            if (fail != null) return fail;

            // Stop gathering via ResourceEngine
            GameEngine.Instance.resourceEngine.StopGathering(villagerGroupID);

            // Emit task changed state change
            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "idle",
                targetCoordinate = null
            });

            DebugLog.Log($"StopGatheringCommand: Villager group {group.name} stopped gathering, now idle");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
