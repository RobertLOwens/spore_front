// ============================================================================
// FILE: AI/AIResearchPlanner.cs
// PURPOSE: AI research planning - research selection, scoring, and execution
//          C# port of AIResearchPlanner.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;
using Sporefront.AI.Commands;

namespace Sporefront.AI
{
    public class AIResearchPlanner
    {
        // ================================================================
        // Configuration
        // ================================================================

        private readonly double researchCheckInterval = GameConfig.AI.Intervals.ResearchCheck;

        // ================================================================
        // Research Commands
        // ================================================================

        public List<IEngineCommand> GenerateResearchCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (currentTime - aiState.lastResearchCheckTime < researchCheckInterval) return commands;
            aiState.lastResearchCheckTime = currentTime;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            if (player.IsResearchActive()) return commands;

            var bestResearch = SelectBestResearch(aiState, gameState);
            if (bestResearch.HasValue)
            {
                if (CanAffordResearch(bestResearch.Value, playerID, gameState))
                {
                    commands.Add(new AIStartResearchCommand(playerID, bestResearch.Value));
                    DebugLog.Log(string.Format("AI starting research: {0}", bestResearch.Value.DisplayName()));
                }
            }

            return commands;
        }

        // ================================================================
        // Research Selection
        // ================================================================

        private ResearchType? SelectBestResearch(AIPlayerState aiState, GameState gameState)
        {
            var playerID = aiState.playerID;
            var availableResearch = GetAvailableResearch(playerID, gameState);

            if (availableResearch.Count == 0) return null;

            ResearchType? best = null;
            double bestScore = double.MinValue;

            foreach (var research in availableResearch)
            {
                double score = ScoreResearch(research, aiState, gameState);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = research;
                }
            }

            return best;
        }

        // ================================================================
        // Research Scoring
        // ================================================================

        private double ScoreResearch(ResearchType research, AIPlayerState aiState, GameState gameState)
        {
            double score = 0.0;

            // Base score: prefer lower tier research (cheaper, faster)
            score += (4 - research.Tier()) * 10.0;

            // State-based priorities
            switch (aiState.currentState)
            {
                case AIState.Peace:
                    if (research.Category() == ResearchCategory.Economic)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.FarmGatheringI:
                            case ResearchType.FarmGatheringII:
                            case ResearchType.FarmGatheringIII:
                                score += 15.0; break;
                            case ResearchType.LumberCampGatheringI:
                            case ResearchType.LumberCampGatheringII:
                            case ResearchType.LumberCampGatheringIII:
                                score += 12.0; break;
                            case ResearchType.MiningCampGatheringI:
                            case ResearchType.MiningCampGatheringII:
                            case ResearchType.MiningCampGatheringIII:
                                score += 10.0; break;
                            case ResearchType.PopulationCapacityI:
                            case ResearchType.PopulationCapacityII:
                            case ResearchType.PopulationCapacityIII:
                                score += 8.0; break;
                            case ResearchType.BuildingSpeedI:
                            case ResearchType.BuildingSpeedII:
                            case ResearchType.BuildingSpeedIII:
                                score += 5.0; break;
                        }
                    }
                    break;

                case AIState.Alert:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 25.0;
                        switch (research)
                        {
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.InfantryPierceArmorI:
                            case ResearchType.InfantryPierceArmorII:
                            case ResearchType.InfantryPierceArmorIII:
                                score += 10.0; break;
                            case ResearchType.MilitaryTrainingSpeedI:
                            case ResearchType.MilitaryTrainingSpeedII:
                            case ResearchType.MilitaryTrainingSpeedIII:
                                score += 15.0; break;
                        }
                    }
                    else
                    {
                        score += 15.0;
                    }
                    break;

                case AIState.Defense:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.FortifiedBuildingsI:
                            case ResearchType.FortifiedBuildingsII:
                            case ResearchType.FortifiedBuildingsIII:
                                score += 20.0; break;
                            case ResearchType.BuildingBludgeonArmorI:
                            case ResearchType.BuildingBludgeonArmorII:
                            case ResearchType.BuildingBludgeonArmorIII:
                                score += 18.0; break;
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.CavalryMeleeArmorI:
                            case ResearchType.CavalryMeleeArmorII:
                            case ResearchType.CavalryMeleeArmorIII:
                                score += 12.0; break;
                            case ResearchType.RetreatSpeedI:
                            case ResearchType.RetreatSpeedII:
                            case ResearchType.RetreatSpeedIII:
                                score += 8.0; break;
                        }
                    }
                    break;

                case AIState.Attack:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        score += 30.0;
                        switch (research)
                        {
                            case ResearchType.InfantryMeleeAttackI:
                            case ResearchType.InfantryMeleeAttackII:
                            case ResearchType.InfantryMeleeAttackIII:
                            case ResearchType.CavalryMeleeAttackI:
                            case ResearchType.CavalryMeleeAttackII:
                            case ResearchType.CavalryMeleeAttackIII:
                                score += 15.0; break;
                            case ResearchType.PiercingDamageI:
                            case ResearchType.PiercingDamageII:
                            case ResearchType.PiercingDamageIII:
                                score += 12.0; break;
                            case ResearchType.MarchSpeedI:
                            case ResearchType.MarchSpeedII:
                            case ResearchType.MarchSpeedIII:
                                score += 10.0; break;
                            case ResearchType.SiegeBludgeonDamageI:
                            case ResearchType.SiegeBludgeonDamageII:
                            case ResearchType.SiegeBludgeonDamageIII:
                                score += 15.0; break;
                        }
                    }
                    break;

                case AIState.Retreat:
                    if (research.Category() == ResearchCategory.Military)
                    {
                        switch (research)
                        {
                            case ResearchType.RetreatSpeedI:
                            case ResearchType.RetreatSpeedII:
                            case ResearchType.RetreatSpeedIII:
                                score += 25.0; break;
                            case ResearchType.InfantryMeleeArmorI:
                            case ResearchType.InfantryMeleeArmorII:
                            case ResearchType.InfantryMeleeArmorIII:
                            case ResearchType.CavalryMeleeArmorI:
                            case ResearchType.CavalryMeleeArmorII:
                            case ResearchType.CavalryMeleeArmorIII:
                                score += 15.0; break;
                        }
                    }
                    break;
            }

            // Penalize Tier I research in gated branches when gate building isn't built yet
            if (research.Tier() == 1)
            {
                var gateBuildingType = research.Branch().GateBuildingType();
                if (gateBuildingType.HasValue)
                {
                    bool hasGateBuilding = gameState.GetBuildingsForPlayer(aiState.playerID)
                        .Any(b => b.buildingType == gateBuildingType.Value && b.IsOperational);
                    if (!hasGateBuilding)
                    {
                        score -= 5.0;
                    }
                }
            }

            return score;
        }

        // ================================================================
        // Research Availability
        // ================================================================

        private List<ResearchType> GetAvailableResearch(Guid playerID, GameState gameState)
        {
            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<ResearchType>();

            int ccLevel = gameState.GetCityCenter(playerID)?.level ?? 1;

            var available = new List<ResearchType>();
            foreach (ResearchType research in Enum.GetValues(typeof(ResearchType)))
            {
                if (player.HasCompletedResearch(research.ToString()))
                    continue;

                if (research.CityCenterLevelRequirement() > ccLevel)
                    continue;

                bool prereqsMet = true;
                foreach (var prereq in research.Prerequisites())
                {
                    if (!player.HasCompletedResearch(prereq.ToString()))
                    {
                        prereqsMet = false;
                        break;
                    }
                }

                // Check building requirement (e.g. Library for Commerce, Blacksmith for Equipment)
                var buildingReq = research.BuildingRequirement();
                if (buildingReq.HasValue)
                {
                    var reqType = buildingReq.Value.buildingType;
                    int reqLevel = buildingReq.Value.level;
                    bool hasBuilding = gameState.GetBuildingsForPlayer(playerID)
                        .Any(b => b.buildingType == reqType &&
                                  b.level >= reqLevel &&
                                  b.IsOperational);
                    if (!hasBuilding)
                    {
                        prereqsMet = false;
                    }
                }

                if (prereqsMet)
                {
                    available.Add(research);
                }
            }

            return available;
        }

        private bool CanAffordResearch(ResearchType research, Guid playerID, GameState gameState)
        {
            var player = gameState.GetPlayer(playerID);
            if (player == null) return false;

            var cost = research.Cost();
            foreach (var kvp in cost)
            {
                if (!player.HasResource(kvp.Key, kvp.Value))
                    return false;
            }
            return true;
        }
    }
}
