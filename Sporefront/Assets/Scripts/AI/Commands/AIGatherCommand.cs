using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AIGatherCommand from AIController.swift.
    /// CRITICAL PATTERN: Call ResourceEngine.StartGathering() FIRST. If it fails, return failure.
    /// Then set path if villager not at resource location.
    /// Then call ResourceEngine.UpdateCollectionRates().
    /// Emit VillagerGroupTaskChangedChange.
    /// Access ResourceEngine via GameEngine.Instance.resourceEngine.
    /// </summary>
    public class AIGatherCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;

        public AIGatherCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID)
            : base(playerID)
        {
            this.villagerGroupID = villagerGroupID;
            this.resourcePointID = resourcePointID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your villagers");

            if (!group.currentTask.IsIdle)
                return EngineCommandResult.Failure("Villager is busy");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            if (resource.remainingAmount <= 0)
                return EngineCommandResult.Failure("Resource depleted");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            var resource = state.GetResourcePoint(resourcePointID);
            if (group == null || resource == null)
                return EngineCommandResult.Failure("Not found");

            // CRITICAL: Call StartGathering() FIRST -- it sets task, assignedResourcePointID,
            // and taskTargetCoordinate internally. If it fails (e.g. no camp coverage),
            // villager stays idle.
            bool registered = GameEngine.Instance.resourceEngine.StartGathering(
                villagerGroupID, resourcePointID
            );

            if (!registered)
                return EngineCommandResult.Failure("Could not start gathering");

            // Set path if villager is not already at the resource location
            if (!group.coordinate.Equals(resource.coordinate))
            {
                var path = state.mapData.FindPath(
                    group.coordinate, resource.coordinate, PlayerID, state
                );
                if (path != null)
                {
                    group.SetPath(path);
                }
            }

            // Update collection rates for this player
            GameEngine.Instance.resourceEngine.UpdateCollectionRates(PlayerID);

            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "gathering",
                targetCoordinate = resource.coordinate
            });

            DebugLog.Log(string.Format("AI villagers assigned to gather {0}", resource.resourceType));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
