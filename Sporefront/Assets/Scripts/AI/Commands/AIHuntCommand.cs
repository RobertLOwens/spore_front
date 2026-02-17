// ============================================================================
// FILE: AI/Commands/AIHuntCommand.cs
// PURPOSE: AI command to assign a villager group to hunt a huntable animal
//          C# port of AIHuntCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIHuntCommand : BaseEngineCommand
    {
        public Guid villagerGroupID;
        public Guid resourcePointID;

        public AIHuntCommand(Guid playerID, Guid villagerGroupID, Guid resourcePointID)
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

            if (!(group.currentTask is IdleTask))
                return EngineCommandResult.Failure("Villager not available");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            if (!resource.resourceType.IsHuntable())
                return EngineCommandResult.Failure("Target not huntable");

            if (resource.currentHealth <= 0)
                return EngineCommandResult.Failure("Target not huntable");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var group = state.GetVillagerGroup(villagerGroupID);
            if (group == null)
                return EngineCommandResult.Failure("Villager group not found");

            var resource = state.GetResourcePoint(resourcePointID);
            if (resource == null)
                return EngineCommandResult.Failure("Resource not found");

            // Assign hunting task
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

            // Emit task changed state change
            changeBuilder.Add(new VillagerGroupTaskChangedChange
            {
                groupID = villagerGroupID,
                task = "hunting",
                targetCoordinate = resource.coordinate
            });

            DebugLog.Log(string.Format("AIHuntCommand: Villagers assigned to hunt at ({0}, {1})",
                resource.coordinate.q, resource.coordinate.r));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
