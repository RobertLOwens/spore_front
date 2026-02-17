// ============================================================================
// FILE: Engine/ArenaSimulator.cs
// PURPOSE: Headless batch combat simulation for arena scenarios
//          C# port of ArenaSimulator.swift (312 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // ================================================================
    // Simulation Winner
    // ================================================================

    public enum SimWinner
    {
        Attacker,
        Defender,
        Draw
    }

    // ================================================================
    // Simulation Result
    // ================================================================

    public class SimulationResult
    {
        public SimWinner winner;
        public Dictionary<string, int> attackerCasualties;   // unitType name -> count killed
        public Dictionary<string, int> defenderCasualties;
        public Dictionary<string, int> attackerRemaining;
        public Dictionary<string, int> defenderRemaining;
        public double combatDuration;
        public Dictionary<string, int> attackerInitial;
        public Dictionary<string, int> defenderInitial;

        public SimulationResult(
            SimWinner winner,
            Dictionary<string, int> attackerCasualties,
            Dictionary<string, int> defenderCasualties,
            Dictionary<string, int> attackerRemaining,
            Dictionary<string, int> defenderRemaining,
            double combatDuration,
            Dictionary<string, int> attackerInitial,
            Dictionary<string, int> defenderInitial)
        {
            this.winner = winner;
            this.attackerCasualties = attackerCasualties;
            this.defenderCasualties = defenderCasualties;
            this.attackerRemaining = attackerRemaining;
            this.defenderRemaining = defenderRemaining;
            this.combatDuration = combatDuration;
            this.attackerInitial = attackerInitial;
            this.defenderInitial = defenderInitial;
        }
    }

    // ================================================================
    // Arena Simulator
    // ================================================================

    /// <summary>
    /// Headless batch combat arena simulator (7x7 minimal map).
    /// </summary>
    public static class ArenaSimulator
    {
        /// <summary>
        /// Run N headless combat simulations with the given configuration.
        /// Executes on a background thread and calls onComplete when done.
        /// </summary>
        public static void RunBatch(
            ArenaArmyConfiguration armyConfig,
            ArenaScenarioConfig scenarioConfig,
            int runs,
            Action<List<SimulationResult>> onComplete)
        {
            Task.Run(() =>
            {
                var results = new List<SimulationResult>();
                for (int i = 0; i < runs; i++)
                {
                    var result = RunSingleSimulation(armyConfig, scenarioConfig);
                    results.Add(result);
                }
                onComplete?.Invoke(results);
            });
        }

        // ================================================================
        // Single Headless Simulation
        // ================================================================

        private static SimulationResult RunSingleSimulation(
            ArenaArmyConfiguration armyConfig,
            ArenaScenarioConfig scenarioConfig)
        {
            // Create fresh game state for this simulation
            var simState = new GameState(7, 7);

            // Set up terrain in mapData
            for (int r = 0; r < 7; r++)
            {
                for (int q = 0; q < 7; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    simState.mapData.SetTile(new TileData(coord, TerrainType.Plains, 0));
                }
            }

            var enemyPos = new HexCoordinate(4, 3);
            var playerPos = new HexCoordinate(2, 3);
            int elevation = scenarioConfig.enemyTerrain == TerrainType.Hill ? 1 :
                           (scenarioConfig.enemyTerrain == TerrainType.Mountain ? 2 : 0);
            simState.mapData.SetTile(new TileData(enemyPos, scenarioConfig.enemyTerrain, elevation));

            // Create player states
            var attackerPS = new PlayerState("Attacker", "#0000FF", false);
            var defenderPS = new PlayerState("Defender", "#FF0000", true);
            var attackerPlayerID = attackerPS.id;
            var defenderPlayerID = defenderPS.id;
            simState.AddPlayer(attackerPS);
            simState.AddPlayer(defenderPS);

            // Set diplomacy
            attackerPS.SetDiplomacyStatus(defenderPlayerID, DiplomacyStatus.Enemy);
            defenderPS.SetDiplomacyStatus(attackerPlayerID, DiplomacyStatus.Enemy);

            // Create city centers as home bases for retreat support
            var attackerCity = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0), attackerPlayerID);
            attackerCity.state = BuildingState.Completed;
            simState.AddBuilding(attackerCity);

            var defenderCity = new BuildingData(BuildingType.CityCenter, new HexCoordinate(6, 6), defenderPlayerID);
            defenderCity.state = BuildingState.Completed;
            simState.AddBuilding(defenderCity);

            // Apply per-unit tier upgrades to attacker
            foreach (var kvp in scenarioConfig.playerUnitTiers)
            {
                if (kvp.Value <= 0) continue;
                var upgrades = UnitUpgradeTypeExtensions.UpgradesForUnit(kvp.Key);
                upgrades.Sort((a, b) => a.Tier().CompareTo(b.Tier()));
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.Tier() <= kvp.Value)
                        attackerPS.CompleteUnitUpgrade(upgrade.ToString());
                }
            }

            // Apply per-unit tier upgrades to defender
            foreach (var kvp in scenarioConfig.enemyUnitTiers)
            {
                if (kvp.Value <= 0) continue;
                var upgrades = UnitUpgradeTypeExtensions.UpgradesForUnit(kvp.Key);
                upgrades.Sort((a, b) => a.Tier().CompareTo(b.Tier()));
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.Tier() <= kvp.Value)
                        defenderPS.CompleteUnitUpgrade(upgrade.ToString());
                }
            }

            // Create attacker army
            var attackerArmy = new ArmyData("Attacker Army", playerPos, attackerPlayerID);
            var attackerCommander = new CommanderData("Attacker Cmdr", scenarioConfig.playerCommanderSpecialty, attackerPlayerID);
            attackerCommander.level = scenarioConfig.playerCommanderLevel;
            attackerCommander.rank = CommanderRankExtensions.RankForLevel(scenarioConfig.playerCommanderLevel);
            attackerArmy.commanderID = attackerCommander.id;
            simState.AddCommander(attackerCommander);
            foreach (var kvp in armyConfig.playerArmy)
            {
                if (kvp.Value > 0)
                    attackerArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
            }
            attackerArmy.homeBaseID = attackerCity.id;
            simState.AddArmy(attackerArmy);

            // Create defender army
            var defenderArmy = new ArmyData("Defender Army", enemyPos, defenderPlayerID);
            var defenderCommander = new CommanderData("Defender Cmdr", scenarioConfig.enemyCommanderSpecialty, defenderPlayerID);
            defenderCommander.level = scenarioConfig.enemyCommanderLevel;
            defenderCommander.rank = CommanderRankExtensions.RankForLevel(scenarioConfig.enemyCommanderLevel);
            defenderArmy.commanderID = defenderCommander.id;
            simState.AddCommander(defenderCommander);
            foreach (var kvp in armyConfig.enemyArmy)
            {
                if (kvp.Value > 0)
                    defenderArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
            }
            defenderArmy.homeBaseID = defenderCity.id;
            simState.AddArmy(defenderArmy);

            // Store initial compositions
            var attackerInitial = CompositionToDict(attackerArmy.militaryComposition);
            var totalDefenderInitial = CompositionToDict(defenderArmy.militaryComposition);

            // Create extra defender armies if stacking
            var extraDefenderArmies = new List<ArmyData>();
            int extraCount = Math.Abs(scenarioConfig.enemyArmyCount) - 1;
            bool isStacked = scenarioConfig.enemyArmyCount > 1;
            bool isAdjacent = scenarioConfig.enemyArmyCount < -1;

            if (isStacked || isAdjacent)
            {
                var adjacentHexes = enemyPos.Neighbors();
                for (int i = 0; i < extraCount; i++)
                {
                    var coord = isStacked ? enemyPos : (i < adjacentHexes.Count ? adjacentHexes[i] : enemyPos);
                    var army = new ArmyData($"Defender Army {i + 2}", coord, defenderPlayerID);
                    var cmd = new CommanderData($"Defender Cmdr {i + 2}", scenarioConfig.enemyCommanderSpecialty, defenderPlayerID);
                    cmd.level = scenarioConfig.enemyCommanderLevel;
                    cmd.rank = CommanderRankExtensions.RankForLevel(scenarioConfig.enemyCommanderLevel);
                    army.commanderID = cmd.id;
                    simState.AddCommander(cmd);
                    foreach (var kvp in armyConfig.enemyArmy)
                    {
                        if (kvp.Value > 0)
                            army.AddMilitaryUnits(kvp.Key, kvp.Value);
                    }
                    army.homeBaseID = defenderCity.id;
                    simState.AddArmy(army);
                    extraDefenderArmies.Add(army);
                    foreach (var kvp in armyConfig.enemyArmy)
                    {
                        if (kvp.Value > 0)
                        {
                            string key = kvp.Key.ToString();
                            if (totalDefenderInitial.ContainsKey(key))
                                totalDefenderInitial[key] += kvp.Value;
                            else
                                totalDefenderInitial[key] = kvp.Value;
                        }
                    }
                }
            }

            // Create extra attacker armies if stacking
            var extraAttackerArmies = new List<ArmyData>();
            int playerExtraCount = Math.Abs(scenarioConfig.playerArmyCount) - 1;
            bool playerIsStacked = scenarioConfig.playerArmyCount > 1;
            bool playerIsAdjacent = scenarioConfig.playerArmyCount < -1;

            if (playerIsStacked || playerIsAdjacent)
            {
                var adjacentHexes = playerPos.Neighbors();
                for (int i = 0; i < playerExtraCount; i++)
                {
                    var coord = playerIsStacked ? playerPos : (i < adjacentHexes.Count ? adjacentHexes[i] : playerPos);
                    var army = new ArmyData($"Attacker Army {i + 2}", coord, attackerPlayerID);
                    var cmd = new CommanderData($"Attacker Cmdr {i + 2}", scenarioConfig.playerCommanderSpecialty, attackerPlayerID);
                    cmd.level = scenarioConfig.playerCommanderLevel;
                    cmd.rank = CommanderRankExtensions.RankForLevel(scenarioConfig.playerCommanderLevel);
                    army.commanderID = cmd.id;
                    simState.AddCommander(cmd);
                    foreach (var kvp in armyConfig.playerArmy)
                    {
                        if (kvp.Value > 0)
                            army.AddMilitaryUnits(kvp.Key, kvp.Value);
                    }
                    army.homeBaseID = attackerCity.id;
                    simState.AddArmy(army);
                    extraAttackerArmies.Add(army);
                }
            }

            // Apply entrenchment with coverage computation (after all armies are in state)
            if (scenarioConfig.enemyEntrenched)
            {
                var allDefenders = new List<ArmyData> { defenderArmy };
                allDefenders.AddRange(extraDefenderArmies);
                foreach (var army in allDefenders)
                {
                    var coverage = simState.ComputeEntrenchmentCoverage(army);
                    army.isEntrenched = true;
                    army.entrenchedCoveredTiles = coverage;
                }
            }

            // Place building + garrison if configured
            if (scenarioConfig.enemyBuilding.HasValue)
            {
                var buildingData = new BuildingData(scenarioConfig.enemyBuilding.Value, enemyPos, defenderPlayerID);
                buildingData.state = BuildingState.Completed;
                if (scenarioConfig.garrisonArchers > 0)
                {
                    buildingData.AddToGarrison(MilitaryUnitType.Archer, scenarioConfig.garrisonArchers);
                }
                simState.AddBuilding(buildingData);
            }

            // Create a fresh CombatEngine and MovementEngine, wire to sim state
            var simCombatEngine = new CombatEngine();
            simCombatEngine.Setup(simState);
            var simMovementEngine = new MovementEngine();
            simMovementEngine.Setup(simState);

            // Start combat
            double startTime = 100.0;
            simState.currentTime = startTime;

            // Determine if we need stack combat
            var allAttackerIDs = new List<Guid> { attackerArmy.id };
            foreach (var extra in extraAttackerArmies)
                allAttackerIDs.Add(extra.id);

            bool needsStackCombat = extraDefenderArmies.Count > 0 ||
                                    extraAttackerArmies.Count > 0 ||
                                    scenarioConfig.enemyEntrenched;

            if (needsStackCombat)
            {
                simCombatEngine.StartStackCombat(allAttackerIDs, enemyPos, startTime);
            }
            else
            {
                simCombatEngine.StartCombat(attackerArmy.id, defenderArmy.id, startTime);
            }

            // Tick until combat ends (max 300s simulated time)
            double tickInterval = 0.5;
            double simTime = startTime;
            double maxSimTime = startTime + 300.0;

            while (simTime < maxSimTime)
            {
                simTime += tickInterval;
                simState.currentTime = simTime;
                simCombatEngine.Update(simTime);
                simMovementEngine.Update(simTime);

                // Check if all combats are done
                if (simCombatEngine.activeCombats.Count == 0 && simCombatEngine.stackCombats.Count == 0)
                    break;
            }

            // Collect results
            double duration = simTime - startTime;

            // Get final compositions
            var attackerFinal = CompositionToDict(attackerArmy.militaryComposition);
            var defenderFinalComp = new Dictionary<MilitaryUnitType, int>(defenderArmy.militaryComposition);
            foreach (var extraArmy in extraDefenderArmies)
            {
                foreach (var kvp in extraArmy.militaryComposition)
                {
                    if (defenderFinalComp.ContainsKey(kvp.Key))
                        defenderFinalComp[kvp.Key] += kvp.Value;
                    else
                        defenderFinalComp[kvp.Key] = kvp.Value;
                }
            }
            var defenderFinal = CompositionToDict(defenderFinalComp);

            // Calculate casualties
            var attackerCasualties = new Dictionary<string, int>();
            foreach (var kvp in attackerInitial)
            {
                int remaining = 0;
                attackerFinal.TryGetValue(kvp.Key, out remaining);
                attackerCasualties[kvp.Key] = Math.Max(0, kvp.Value - remaining);
            }

            var defenderCasualties = new Dictionary<string, int>();
            foreach (var kvp in totalDefenderInitial)
            {
                int remaining = 0;
                defenderFinal.TryGetValue(kvp.Key, out remaining);
                defenderCasualties[kvp.Key] = Math.Max(0, kvp.Value - remaining);
            }

            // Determine winner
            int attackerAlive = attackerFinal.Values.Sum();
            int defenderAlive = defenderFinal.Values.Sum();
            SimWinner winner;
            if (attackerAlive > 0 && defenderAlive == 0)
                winner = SimWinner.Attacker;
            else if (defenderAlive > 0 && attackerAlive == 0)
                winner = SimWinner.Defender;
            else
                winner = SimWinner.Draw;

            return new SimulationResult(
                winner,
                attackerCasualties,
                defenderCasualties,
                attackerFinal,
                defenderFinal,
                duration,
                attackerInitial,
                totalDefenderInitial
            );
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static Dictionary<string, int> CompositionToDict(Dictionary<MilitaryUnitType, int> composition)
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in composition)
            {
                result[kvp.Key.ToString()] = kvp.Value;
            }
            return result;
        }
    }
}
