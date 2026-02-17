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

        public override EngineCommandResult Validate(GameState state)
        {
            // Villager group must exist
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            // Must be owned by this player
            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Villager group is not owned by this player");

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
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            var resourcePoint = state.GetResourcePoint(resourcePointID);
            if (resourcePoint == null)
                return EngineCommandResult.Failure("Resource point not found");

            // Start gathering via ResourceEngine
            bool success = GameEngine.Instance.resourceEngine.StartGathering(villagerGroupID, resourcePointID);
            if (!success)
            {
                DebugLog.Log(string.Format("GatherCommand: StartGathering failed for group {0} at resource {1}",
                    villagerGroupID, resourcePointID));
                return EngineCommandResult.Failure("Failed to start gathering");
            }

            // Emit task changed state change
            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "gathering",
                targetCoordinate = resourcePoint.coordinate
            });

            DebugLog.Log(string.Format("GatherCommand: Villager group {0} now gathering at {1}",
                group.name, resourcePoint.coordinate));

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

        public override EngineCommandResult Validate(GameState state)
        {
            // Villager group must exist
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            // Must be owned by this player
            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Villager group is not owned by this player");

            // Must be currently gathering
            if (!group.IsGathering())
                return EngineCommandResult.Failure("Villager group is not currently gathering");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            // Stop gathering via ResourceEngine
            GameEngine.Instance.resourceEngine.StopGathering(villagerGroupID);

            // Emit task changed state change
            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "idle",
                targetCoordinate = null
            });

            DebugLog.Log(string.Format("StopGatheringCommand: Villager group {0} stopped gathering, now idle",
                group.name));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
