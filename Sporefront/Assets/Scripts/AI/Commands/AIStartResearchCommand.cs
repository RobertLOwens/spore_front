// ============================================================================
// FILE: AI/Commands/AIStartResearchCommand.cs
// PURPOSE: AI command to start a research for an AI player
//          C# port of AIStartResearchCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIStartResearchCommand : BaseEngineCommand
    {
        public ResearchType researchType;

        public AIStartResearchCommand(Guid playerID, ResearchType researchType)
            : base(playerID)
        {
            this.researchType = researchType;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            if (player.IsResearchActive())
                return EngineCommandResult.Failure("Research already in progress");

            string rawValue = researchType.ToString();
            if (player.HasCompletedResearch(rawValue))
                return EngineCommandResult.Failure("Research already completed");

            // Check prerequisites
            var prerequisites = researchType.Prerequisites();
            foreach (var prereq in prerequisites)
            {
                if (!player.HasCompletedResearch(prereq.ToString()))
                    return EngineCommandResult.Failure("Prerequisites not met");
            }

            // Check city center level requirement
            int ccLevel = state.GetCityCenterLevel(PlayerID);
            if (researchType.CityCenterLevelRequirement() > ccLevel)
                return EngineCommandResult.Failure("City Center level too low");

            // Check building requirement (e.g. Library for Commerce, Blacksmith for Equipment)
            var buildingReq = researchType.BuildingRequirement();
            if (buildingReq.HasValue)
            {
                var buildings = state.GetBuildingsForPlayer(PlayerID);
                bool hasBuilding = false;
                foreach (var b in buildings)
                {
                    if (b.buildingType == buildingReq.Value.buildingType &&
                        b.level >= buildingReq.Value.level &&
                        b.state == BuildingState.Completed)
                    {
                        hasBuilding = true;
                        break;
                    }
                }
                if (!hasBuilding)
                    return EngineCommandResult.Failure(string.Format("Requires {0}", buildingReq.Value.buildingType));
            }

            // Check affordability
            var cost = researchType.Cost();
            foreach (var kvp in cost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value))
                    return EngineCommandResult.Failure("Insufficient resources");
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            string rawValue = researchType.ToString();

            // Deduct resources
            var cost = researchType.Cost();
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Start research
            player.StartResearch(rawValue, state.currentTime);

            // Emit state change
            changeBuilder.Add(new ResearchStartedChange
            {
                playerID = PlayerID,
                researchType = rawValue,
                startTime = state.currentTime
            });

            DebugLog.Log(string.Format("AIStartResearchCommand: AI started research: {0}",
                researchType.DisplayName()));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
