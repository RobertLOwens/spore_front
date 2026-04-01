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
        // Data-Driven State × Category Bonuses
        // ================================================================

        private static readonly Dictionary<(AIState, ResearchCategory), double> StateCategoryBonuses =
            new Dictionary<(AIState, ResearchCategory), double>
            {
                { (AIState.Peace, ResearchCategory.Economic), 30.0 },
                { (AIState.Alert, ResearchCategory.Military), 25.0 },
                { (AIState.Defense, ResearchCategory.Military), 30.0 },
                { (AIState.Attack, ResearchCategory.Military), 30.0 },
            };

        // Alert non-military fallback handled separately (all non-Military categories get +15)

        // ================================================================
        // Data-Driven State × ResearchType Bonuses
        // ================================================================

        private static readonly Dictionary<(AIState, ResearchType), double> StateResearchBonuses =
            new Dictionary<(AIState, ResearchType), double>
            {
                // Peace: prioritize economy
                { (AIState.Peace, ResearchType.FarmGatheringI), 15.0 },
                { (AIState.Peace, ResearchType.FarmGatheringII), 15.0 },
                { (AIState.Peace, ResearchType.FarmGatheringIII), 15.0 },
                { (AIState.Peace, ResearchType.LumberCampGatheringI), 12.0 },
                { (AIState.Peace, ResearchType.LumberCampGatheringII), 12.0 },
                { (AIState.Peace, ResearchType.LumberCampGatheringIII), 12.0 },
                { (AIState.Peace, ResearchType.MiningCampGatheringI), 10.0 },
                { (AIState.Peace, ResearchType.MiningCampGatheringII), 10.0 },
                { (AIState.Peace, ResearchType.MiningCampGatheringIII), 10.0 },
                { (AIState.Peace, ResearchType.PopulationCapacityI), 8.0 },
                { (AIState.Peace, ResearchType.PopulationCapacityII), 8.0 },
                { (AIState.Peace, ResearchType.PopulationCapacityIII), 8.0 },
                { (AIState.Peace, ResearchType.BuildingSpeedI), 5.0 },
                { (AIState.Peace, ResearchType.BuildingSpeedII), 5.0 },
                { (AIState.Peace, ResearchType.BuildingSpeedIII), 5.0 },
                // Alert: armor + training speed
                { (AIState.Alert, ResearchType.InfantryMeleeArmorI), 10.0 },
                { (AIState.Alert, ResearchType.InfantryMeleeArmorII), 10.0 },
                { (AIState.Alert, ResearchType.InfantryMeleeArmorIII), 10.0 },
                { (AIState.Alert, ResearchType.InfantryPierceArmorI), 10.0 },
                { (AIState.Alert, ResearchType.InfantryPierceArmorII), 10.0 },
                { (AIState.Alert, ResearchType.InfantryPierceArmorIII), 10.0 },
                { (AIState.Alert, ResearchType.MilitaryTrainingSpeedI), 15.0 },
                { (AIState.Alert, ResearchType.MilitaryTrainingSpeedII), 15.0 },
                { (AIState.Alert, ResearchType.MilitaryTrainingSpeedIII), 15.0 },
                // Defense: fortification + armor + retreat
                { (AIState.Defense, ResearchType.FortifiedBuildingsI), 20.0 },
                { (AIState.Defense, ResearchType.FortifiedBuildingsII), 20.0 },
                { (AIState.Defense, ResearchType.FortifiedBuildingsIII), 20.0 },
                { (AIState.Defense, ResearchType.BuildingBludgeonArmorI), 18.0 },
                { (AIState.Defense, ResearchType.BuildingBludgeonArmorII), 18.0 },
                { (AIState.Defense, ResearchType.BuildingBludgeonArmorIII), 18.0 },
                { (AIState.Defense, ResearchType.InfantryMeleeArmorI), 12.0 },
                { (AIState.Defense, ResearchType.InfantryMeleeArmorII), 12.0 },
                { (AIState.Defense, ResearchType.InfantryMeleeArmorIII), 12.0 },
                { (AIState.Defense, ResearchType.CavalryMeleeArmorI), 12.0 },
                { (AIState.Defense, ResearchType.CavalryMeleeArmorII), 12.0 },
                { (AIState.Defense, ResearchType.CavalryMeleeArmorIII), 12.0 },
                { (AIState.Defense, ResearchType.RetreatSpeedI), 8.0 },
                { (AIState.Defense, ResearchType.RetreatSpeedII), 8.0 },
                { (AIState.Defense, ResearchType.RetreatSpeedIII), 8.0 },
                // Attack: offense + siege + march speed
                { (AIState.Attack, ResearchType.InfantryMeleeAttackI), 15.0 },
                { (AIState.Attack, ResearchType.InfantryMeleeAttackII), 15.0 },
                { (AIState.Attack, ResearchType.InfantryMeleeAttackIII), 15.0 },
                { (AIState.Attack, ResearchType.CavalryMeleeAttackI), 15.0 },
                { (AIState.Attack, ResearchType.CavalryMeleeAttackII), 15.0 },
                { (AIState.Attack, ResearchType.CavalryMeleeAttackIII), 15.0 },
                { (AIState.Attack, ResearchType.PiercingDamageI), 12.0 },
                { (AIState.Attack, ResearchType.PiercingDamageII), 12.0 },
                { (AIState.Attack, ResearchType.PiercingDamageIII), 12.0 },
                { (AIState.Attack, ResearchType.MarchSpeedI), 10.0 },
                { (AIState.Attack, ResearchType.MarchSpeedII), 10.0 },
                { (AIState.Attack, ResearchType.MarchSpeedIII), 10.0 },
                { (AIState.Attack, ResearchType.SiegeBludgeonDamageI), 15.0 },
                { (AIState.Attack, ResearchType.SiegeBludgeonDamageII), 15.0 },
                { (AIState.Attack, ResearchType.SiegeBludgeonDamageIII), 15.0 },
                // Retreat: escape + survivability
                { (AIState.Retreat, ResearchType.RetreatSpeedI), 25.0 },
                { (AIState.Retreat, ResearchType.RetreatSpeedII), 25.0 },
                { (AIState.Retreat, ResearchType.RetreatSpeedIII), 25.0 },
                { (AIState.Retreat, ResearchType.InfantryMeleeArmorI), 15.0 },
                { (AIState.Retreat, ResearchType.InfantryMeleeArmorII), 15.0 },
                { (AIState.Retreat, ResearchType.InfantryMeleeArmorIII), 15.0 },
                { (AIState.Retreat, ResearchType.CavalryMeleeArmorI), 15.0 },
                { (AIState.Retreat, ResearchType.CavalryMeleeArmorII), 15.0 },
                { (AIState.Retreat, ResearchType.CavalryMeleeArmorIII), 15.0 },
            };

        // ================================================================
        // Research Commands
        // ================================================================

        public List<IEngineCommand> GenerateResearchCommands(AIPlayerState aiState, GameState gameState, double currentTime)
        {
            var commands = new List<IEngineCommand>();
            var playerID = aiState.playerID;

            if (!AIHelper.ShouldExecute(ref aiState.lastResearchCheckTime, currentTime, researchCheckInterval)) return commands;

            var player = gameState.GetPlayer(playerID);
            if (player == null) return commands;

            if (player.IsResearchActive()) return commands;

            var bestResearch = SelectBestResearch(aiState, gameState);
            if (bestResearch.HasValue)
            {
                if (CanAffordResearch(bestResearch.Value, playerID, gameState))
                {
                    commands.Add(new AIStartResearchCommand(playerID, bestResearch.Value));
                    DebugLog.Log($"AI starting research: {bestResearch.Value.DisplayName()}");
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

            // State-based priorities (data-driven)
            var state = aiState.currentState;
            var category = research.Category();

            double categoryBonus;
            if (StateCategoryBonuses.TryGetValue((state, category), out categoryBonus))
                score += categoryBonus;
            else if (state == AIState.Alert && category != ResearchCategory.Military)
                score += 15.0; // Alert: non-military fallback

            double researchBonus;
            if (StateResearchBonuses.TryGetValue((state, research), out researchBonus))
                score += researchBonus;

            // Penalize Tier I research in gated branches when gate building isn't built yet
            if (research.Tier() == 1)
            {
                var gateBuildingType = research.Branch().GateBuildingType();
                if (gateBuildingType.HasValue)
                {
                    bool hasGateBuilding = gameState.HasBuilding(aiState.playerID, gateBuildingType.Value, operationalOnly: true);
                    if (!hasGateBuilding)
                    {
                        score -= 5.0;
                    }
                }
            }

            var playerID = aiState.playerID;
            var player = gameState.GetPlayer(playerID);

            // Synergy bonus: continuing a research line we already started
            if (research.Tier() > 1)
            {
                var prereqs = research.Prerequisites();
                bool allPrereqsComplete = prereqs.All(p => player != null && player.HasCompletedResearch(p.ToString()));
                if (allPrereqsComplete)
                    score += 10.0;
            }

            // Counter-research: boost research that counters inferred enemy capabilities
            if (aiState.lastEnemyAnalysis.HasValue)
            {
                var enemy = aiState.lastEnemyAnalysis.Value;
                var branch = research.Branch();

                // If enemy has strong melee (infantry dominant), boost armor research
                if (enemy.infantryRatio > 0.35)
                {
                    if (branch == ResearchBranch.MeleeEquipment && research.ToString().Contains("Armor"))
                        score += 12.0;
                }
                // If enemy has strong ranged, boost pierce armor
                if (enemy.rangedRatio > 0.35)
                {
                    if (research.ToString().Contains("PierceArmor"))
                        score += 12.0;
                }
                // If enemy has strong cavalry, boost infantry melee attack to counter
                if (enemy.cavalryRatio > 0.35)
                {
                    if (research.ToString().Contains("InfantryMeleeAttack"))
                        score += 12.0;
                }
            }

            // Composition-aware: match research to own army composition
            if (player != null)
            {
                var armies = gameState.GetArmiesForPlayer(playerID);
                int totalInfantry = 0, totalRanged = 0, totalCavalry = 0, totalSiege = 0, totalUnits = 0;
                foreach (var army in armies)
                {
                    var ratios = army.GetCategoryRatios();
                    int count = army.GetTotalUnits();
                    totalInfantry += (int)(ratios.infantry * count);
                    totalRanged += (int)(ratios.ranged * count);
                    totalCavalry += (int)(ratios.cavalry * count);
                    totalSiege += (int)(ratios.siege * count);
                    totalUnits += count;
                }

                if (totalUnits > 5)
                {
                    var branch = research.Branch();
                    double infantryRatio = (double)totalInfantry / totalUnits;
                    double rangedRatio = (double)totalRanged / totalUnits;
                    double cavalryRatio = (double)totalCavalry / totalUnits;

                    // Boost research matching our dominant army type
                    if (infantryRatio > 0.4 && branch == ResearchBranch.MeleeEquipment)
                        score += 8.0;
                    if (rangedRatio > 0.4 && branch == ResearchBranch.RangedEquipment)
                        score += 8.0;
                    if (cavalryRatio > 0.3 && branch == ResearchBranch.MeleeEquipment &&
                        (research.ToString().Contains("Cavalry")))
                        score += 8.0;
                }
            }

            // Breadth bonus: reward starting new branches, penalize deep specialization
            if (player != null)
            {
                var branch = research.Branch();
                int completedInBranch = 0;
                foreach (ResearchType r in Enum.GetValues(typeof(ResearchType)))
                {
                    if (r.Branch() == branch && player.HasCompletedResearch(r.ToString()))
                        completedInBranch++;
                }

                if (completedInBranch == 0)
                    score += 5.0; // Bonus for starting a new branch
                else if (completedInBranch >= 6)
                    score -= 5.0; // Penalty for over-specializing
            }

            // Faction synergy bonuses (data-driven via FactionAIConfig)
            var factionConfig = FactionAIConfig.Get(player?.faction ?? FactionType.None);

            double factionBonus;
            if (factionConfig.ResearchBonuses.TryGetValue(research, out factionBonus))
                score += factionBonus;

            if (factionConfig.ResearchNamePenalties.Count > 0)
            {
                var researchName = research.ToString();
                foreach (var penalty in factionConfig.ResearchNamePenalties)
                {
                    if (researchName.Contains(penalty.pattern))
                        score += penalty.penalty;
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

                // Skip faction-blocked and faction-exclusive research
                if (player.faction.BlockedResearch().Contains(research))
                    continue;
                var exclusiveFaction = research.ExclusiveFaction();
                if (exclusiveFaction != FactionType.None && exclusiveFaction != player.faction)
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
            return player.CanAfford(research.Cost());
        }
    }
}
