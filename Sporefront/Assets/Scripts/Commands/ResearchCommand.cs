using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class ResearchCommand : BaseEngineCommand
    {
        public string researchTypeRawValue;
        public Guid buildingID;

        public ResearchCommand(Guid playerID, string researchTypeRawValue, Guid buildingID)
            : base(playerID)
        {
            this.researchTypeRawValue = researchTypeRawValue;
            this.buildingID = buildingID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Parse research type
            ResearchType researchType;
            if (!Enum.TryParse<ResearchType>(researchTypeRawValue, out researchType))
                return EngineCommandResult.Failure("Invalid research type.");

            // Check player exists
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found.");

            // Check player has no active research
            if (player.activeResearchType != null)
                return EngineCommandResult.Failure("Research already in progress.");

            // Check research not already completed
            if (player.HasCompletedResearch(researchTypeRawValue))
                return EngineCommandResult.Failure("Research already completed.");

            // Check prerequisites are met
            var prerequisites = researchType.Prerequisites();
            foreach (var prereq in prerequisites)
            {
                if (!player.HasCompletedResearch(prereq.ToString()))
                    return EngineCommandResult.Failure($"Missing prerequisite: {prereq.DisplayName()}.");
            }

            // Check building requirement
            var buildingReq = researchType.BuildingRequirement();
            if (buildingReq.HasValue)
            {
                int count = state.GetBuildingCount(buildingReq.Value.buildingType, PlayerID);
                if (count < 1)
                    return EngineCommandResult.Failure($"Requires {buildingReq.Value.buildingType}.");
            }

            // Check player can afford the research cost
            var cost = researchType.Cost();
            if (!player.CanAfford(cost))
                return EngineCommandResult.Failure("Insufficient resources.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            ResearchType researchType;
            Enum.TryParse<ResearchType>(researchTypeRawValue, out researchType);

            var player = state.GetPlayer(PlayerID);

            // Deduct resources
            var cost = researchType.Cost();
            foreach (var kvp in cost)
            {
                int oldAmount = player.GetResource(kvp.Key);
                player.RemoveResource(kvp.Key, kvp.Value);
                int newAmount = player.GetResource(kvp.Key);

                changeBuilder.Add(new ResourcesChangedChange
                {
                    playerID = PlayerID,
                    resourceType = kvp.Key.ToString(),
                    oldAmount = oldAmount,
                    newAmount = newAmount
                });
            }

            // Start research on player
            player.StartResearch(researchTypeRawValue, state.currentTime);

            // Emit research started change
            changeBuilder.Add(new ResearchStartedChange
            {
                playerID = PlayerID,
                researchType = researchTypeRawValue,
                startTime = state.currentTime
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
