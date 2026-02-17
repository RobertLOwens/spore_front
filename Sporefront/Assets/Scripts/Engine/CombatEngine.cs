// ============================================================================
// FILE: Engine/CombatEngine.cs
// PURPOSE: Handles all combat logic using the 3-phase combat system
// Ported from Swift CombatEngine.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Models.Combat;

namespace Sporefront.Engine
{
    // MARK: - Combat State Data (Legacy - kept for UI/History compatibility)

    /// <summary>
    /// Lightweight combat data for building combat tracking and UI queries.
    /// </summary>
    public class ActiveCombatData
    {
        public Guid id;
        public Guid attackerArmyID;
        public Guid? defenderArmyID;
        public Guid? defenderBuildingID;
        public HexCoordinate coordinate;
        public int currentPhase;
        public double startTime;
        public double lastPhaseTime;
        public bool isComplete;
        public CombatResultData? result;

        // Building damage tracking
        public string buildingType;
        public double buildingHealthBefore;
        public double totalBuildingDamage;

        public ActiveCombatData(
            Guid id,
            Guid attackerArmyID,
            Guid? defenderArmyID,
            Guid? defenderBuildingID,
            HexCoordinate coordinate,
            double startTime,
            double lastPhaseTime,
            int currentPhase = 0,
            bool isComplete = false,
            CombatResultData? result = null)
        {
            this.id = id;
            this.attackerArmyID = attackerArmyID;
            this.defenderArmyID = defenderArmyID;
            this.defenderBuildingID = defenderBuildingID;
            this.coordinate = coordinate;
            this.currentPhase = currentPhase;
            this.startTime = startTime;
            this.lastPhaseTime = lastPhaseTime;
            this.isComplete = isComplete;
            this.result = result;

            this.buildingType = null;
            this.buildingHealthBefore = 0;
            this.totalBuildingDamage = 0;
        }
    }

    /// <summary>
    /// Data for army vs villager combat (quick massacre - villagers fight back weakly).
    /// </summary>
    public class VillagerCombatData
    {
        public Guid id;
        public Guid attackerArmyID;
        public Guid defenderVillagerGroupID;
        public Guid? defenderOwnerID;
        public HexCoordinate coordinate;
        public double startTime;
        public double lastTickTime;
        public bool isComplete;
        public int villagersKilled;
        public int initialVillagerCount;
        public double accumulatedDamage;
        public int initialAttackerUnitCount;

        public VillagerCombatData(
            Guid id,
            Guid attackerArmyID,
            Guid defenderVillagerGroupID,
            Guid? defenderOwnerID,
            HexCoordinate coordinate,
            double startTime,
            double lastTickTime,
            int initialVillagerCount,
            int initialAttackerUnitCount)
        {
            this.id = id;
            this.attackerArmyID = attackerArmyID;
            this.defenderVillagerGroupID = defenderVillagerGroupID;
            this.defenderOwnerID = defenderOwnerID;
            this.coordinate = coordinate;
            this.startTime = startTime;
            this.lastTickTime = lastTickTime;
            this.isComplete = false;
            this.villagersKilled = 0;
            this.initialVillagerCount = initialVillagerCount;
            this.accumulatedDamage = 0.0;
            this.initialAttackerUnitCount = initialAttackerUnitCount;
        }
    }

    // MARK: - Combat Engine

    /// <summary>
    /// Handles all combat calculations and state using the 3-phase combat system:
    /// - Ranged Exchange (0-3s): Only ranged/siege units deal damage
    /// - Melee Engagement (3s+): All units fight
    /// - Cleanup: One side's melee units are gone
    /// </summary>
    public class CombatEngine
    {
        // MARK: - State
        private GameState gameState;

        // MARK: - Active Combats (using proper 3-phase system)
        public Dictionary<Guid, ActiveCombat> activeCombats { get; private set; } = new Dictionary<Guid, ActiveCombat>();

        // MARK: - Building Combats (separate tracking for army vs building)
        public Dictionary<Guid, ActiveCombatData> buildingCombats { get; private set; } = new Dictionary<Guid, ActiveCombatData>();

        // MARK: - Villager Combats (army vs villager group)
        public Dictionary<Guid, VillagerCombatData> villagerCombats { get; private set; } = new Dictionary<Guid, VillagerCombatData>();

        // MARK: - Stack Combats (multi-army engagement)
        public Dictionary<Guid, StackCombat> stackCombats { get; private set; } = new Dictionary<Guid, StackCombat>();

        // MARK: - Garrison Defense
        public GarrisonDefenseEngine garrisonDefenseEngine = new GarrisonDefenseEngine();

        // MARK: - Combat History
        public List<CombatRecord> combatHistory { get; private set; } = new List<CombatRecord>();
        public List<DetailedCombatRecord> detailedCombatHistory { get; private set; } = new List<DetailedCombatRecord>();

        // MARK: - Combat Constants
        private readonly double buildingPhaseInterval = GameConfig.Combat.BuildingPhaseInterval;
        private readonly double siegeBuildingBonusMultiplier = GameConfig.Combat.SiegeBuildingBonusMultiplier;

        // MARK: - Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
            activeCombats.Clear();
            buildingCombats.Clear();
            villagerCombats.Clear();
            stackCombats.Clear();

            garrisonDefenseEngine.Setup(gameState, (armyID) =>
                buildingCombats.Values.Any(bc => bc.attackerArmyID == armyID));
            garrisonDefenseEngine.OnCombatRecord = (record) =>
            {
                AddCombatRecord(record);
            };
        }

        // MARK: - Update Loop

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Process army vs army combats (3-phase system)
            var armyChanges = ProcessArmyCombats(currentTime, gameState);
            changes.AddRange(armyChanges);

            // Process army vs building combats (simpler phase system)
            var buildingChanges = ProcessBuildingCombats(currentTime, gameState);
            changes.AddRange(buildingChanges);

            // Process army vs villager combats (quick massacre)
            var villagerChanges = ProcessVillagerCombats(currentTime, gameState);
            changes.AddRange(villagerChanges);

            // Process stack combats (multi-army engagement)
            var stackChanges = ProcessStackCombats(currentTime, gameState);
            changes.AddRange(stackChanges);

            // Check for garrison defense attacks
            var garrisonChanges = garrisonDefenseEngine.ProcessGarrisonDefense(currentTime, gameState, 1.0);
            changes.AddRange(garrisonChanges);

            return changes;
        }

        // MARK: - Army vs Army Combat Processing (3-Phase System)

        private List<StateChange> ProcessArmyCombats(double currentTime, GameState state)
        {
            var changes = new List<StateChange>();
            var completedCombats = new List<Guid>();

            foreach (var kvp in new Dictionary<Guid, ActiveCombat>(activeCombats))
            {
                Guid combatID = kvp.Key;
                ActiveCombat combat = kvp.Value;

                if (combat.phase == CombatPhase.Ended)
                {
                    // Stack pairings are cleaned up by ProcessStackCombats()
                    if (!combat.isStackPairing)
                    {
                        completedCombats.Add(combatID);
                    }
                    continue;
                }

                // Calculate elapsed time since combat started (using game time, not Unix time)
                double newElapsed = currentTime - combat.gameStartTime;
                double deltaTime = newElapsed - combat.elapsedTime;

                // Skip if no time has passed
                if (deltaTime <= 0) continue;

                combat.elapsedTime = newElapsed;

                // Check phase transition
                CombatPhase previousPhase = combat.phase;
                combat.UpdatePhase();

                if (combat.phase != previousPhase)
                {
                    DebugLog.Log(string.Format("Combat phase changed: {0} -> {1}",
                        previousPhase.DisplayName(), combat.phase.DisplayName()));
                    changes.Add(new CombatPhaseCompletedChange
                    {
                        attackerID = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyID : Guid.Empty,
                        defenderID = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0].armyID : Guid.Empty,
                        phase = PhaseToInt(combat.phase)
                    });
                }

                // Process damage based on current phase
                var phaseChanges = ProcessCombatDamage(combat, deltaTime, state);
                changes.AddRange(phaseChanges);

                // Check for combat end
                if (combat.ShouldEnd)
                {
                    combat.phase = CombatPhase.Ended;

                    // Stack pairings: just mark ended -- ProcessStackCombats() handles cleanup & chain
                    if (combat.isStackPairing)
                    {
                        continue;
                    }

                    completedCombats.Add(combatID);

                    CombatResultData result = DetermineCombatResult(combat);
                    changes.Add(new CombatEndedChange
                    {
                        attackerID = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyID : Guid.Empty,
                        defenderID = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0].armyID : Guid.Empty,
                        result = result
                    });

                    // Save combat record to history (both basic and detailed)
                    CombatRecord combatRecord = CreateCombatRecord(combat, state);
                    AddCombatRecord(combatRecord);

                    DetailedCombatRecord detailedRecord = CreateDetailedCombatRecord(combat, state);
                    AddDetailedCombatRecord(detailedRecord);

                    // Clean up combat flags on armies
                    CleanupCombatFlags(combat, state);

                    // Handle draw: both armies empty -- retreat or destroy both
                    if (!result.winnerID.HasValue && !result.loserID.HasValue)
                    {
                        var allArmyStates = new List<ArmyCombatState>();
                        allArmyStates.AddRange(combat.attackerArmies);
                        allArmyStates.AddRange(combat.defenderArmies);

                        foreach (var armyState in allArmyStates)
                        {
                            Guid armyID = armyState.armyID;
                            List<HexCoordinate> retreatPath = InitiateAutoRetreat(armyID, state);
                            if (retreatPath != null)
                            {
                                changes.Add(new ArmyAutoRetreatingChange { armyID = armyID, path = retreatPath });
                            }
                            ArmyData army = state.GetArmy(armyID);
                            if (army != null && army.IsEmpty() && !army.isRetreating)
                            {
                                changes.Add(new ArmyDestroyedChange { armyID = armyID, coordinate = army.coordinate });
                                state.RemoveArmy(armyID);
                            }
                        }
                    }

                    // Auto-retreat for the losing army
                    if (result.loserID.HasValue)
                    {
                        Guid loserID = result.loserID.Value;
                        List<HexCoordinate> retreatPath = InitiateAutoRetreat(loserID, state);
                        if (retreatPath != null)
                        {
                            changes.Add(new ArmyAutoRetreatingChange { armyID = loserID, path = retreatPath });
                        }
                        // If army is empty and couldn't retreat, destroy it
                        ArmyData loserArmy = state.GetArmy(loserID);
                        if (loserArmy != null && loserArmy.IsEmpty() && !loserArmy.isRetreating)
                        {
                            changes.Add(new ArmyDestroyedChange { armyID = loserID, coordinate = loserArmy.coordinate });
                            state.RemoveArmy(loserID);
                        }
                    }

                    // Auto-attack enemy building at combat location if attacker won
                    if (result.winnerID.HasValue)
                    {
                        Guid winnerID = result.winnerID.Value;
                        if (combat.attackerArmies.Any(a => a.armyID == winnerID))
                        {
                            // Winner was the attacker - check for enemy building at this location
                            AutoStartBuildingCombat(winnerID, combat.location, state, currentTime, changes);
                        }
                    }
                }
            }

            // Remove completed combats (non-stack only; stack pairings removed by ProcessStackCombats)
            foreach (Guid id in completedCombats)
            {
                activeCombats.Remove(id);
            }

            return changes;
        }

        private int PhaseToInt(CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.RangedExchange: return 1;
                case CombatPhase.MeleeEngagement: return 2;
                case CombatPhase.Cleanup: return 3;
                case CombatPhase.Ended: return 4;
                default: return 0;
            }
        }

        // MARK: - Combat Initiation

        /// <summary>
        /// Start combat between two armies using the 3-phase system.
        /// </summary>
        public StateChange StartCombat(Guid attackerArmyID, Guid defenderArmyID, double currentTime)
        {
            if (gameState == null)
            {
                DebugLog.Log("CombatEngine: gameState is null");
                return null;
            }
            ArmyData attacker = gameState.GetArmy(attackerArmyID);
            if (attacker == null)
            {
                DebugLog.Log(string.Format("CombatEngine: Attacker army not found in GameState (ID: {0})", attackerArmyID));
                return null;
            }
            ArmyData defender = gameState.GetArmy(defenderArmyID);
            if (defender == null)
            {
                DebugLog.Log(string.Format("CombatEngine: Defender army not found in GameState (ID: {0})", defenderArmyID));
                return null;
            }

            // Get terrain at combat location
            TerrainType? terrainNullable = gameState.mapData.GetTerrain(defender.coordinate);
            TerrainType terrain = terrainNullable.HasValue ? terrainNullable.Value : TerrainType.Plains;

            // Create the ActiveCombat using the 3-phase system
            var combat = new ActiveCombat(
                attacker,
                defender,
                defender.coordinate,
                terrain,
                currentTime
            );

            // Look up player states for research bonus application
            if (attacker.ownerID.HasValue)
            {
                combat.attackerPlayerState = gameState.GetPlayer(attacker.ownerID.Value);
            }
            if (defender.ownerID.HasValue)
            {
                combat.defenderPlayerState = gameState.GetPlayer(defender.ownerID.Value);
            }

            // Look up commander tactics bonuses for terrain scaling and store commander data
            if (attacker.commanderID.HasValue)
            {
                CommanderData attackerCommander = gameState.GetCommander(attacker.commanderID.Value);
                if (attackerCommander != null)
                {
                    combat.attackerTacticsBonus = (double)attackerCommander.Tactics * GameConfig.Commander.TacticsTerrainScaling;
                    combat.attackerCommanderData = attackerCommander;
                }
            }
            if (defender.commanderID.HasValue)
            {
                CommanderData defenderCommander = gameState.GetCommander(defender.commanderID.Value);
                if (defenderCommander != null)
                {
                    combat.defenderTacticsBonus = (double)defenderCommander.Tactics * GameConfig.Commander.TacticsTerrainScaling;
                    combat.defenderCommanderData = defenderCommander;
                }
            }

            // Apply entrenchment defense bonus to defender (tracked separately)
            if (defender.isEntrenched)
            {
                combat.entrenchmentDefenseBonus = GameConfig.Entrenchment.DefenseBonus;
                DebugLog.Log(string.Format("   Defender is entrenched: +{0}% defense bonus",
                    (int)(GameConfig.Entrenchment.DefenseBonus * 100)));
            }

            // Store with combat's own ID
            activeCombats[combat.id] = combat;

            // Mark armies as in combat
            attacker.isInCombat = true;
            attacker.combatTargetID = defenderArmyID;
            defender.isInCombat = true;
            defender.combatTargetID = attackerArmyID;

            // Debug logging for terrain bonuses
            DebugLog.Log(string.Format("Combat started: Phase {0} at {1}", combat.phase.DisplayName(), defender.coordinate));
            DebugLog.Log(string.Format("   Terrain: {0}", terrain.DisplayName()));
            if (combat.terrainDefenseBonus > 0)
            {
                DebugLog.Log(string.Format("   Defender defense bonus: +{0}%", (int)(combat.terrainDefenseBonus * 100)));
            }
            if (combat.terrainAttackPenalty > 0)
            {
                DebugLog.Log(string.Format("   Attacker attack penalty: -{0}%", (int)(combat.terrainAttackPenalty * 100)));
            }
            if (combat.terrainDefenseBonus == 0 && combat.terrainAttackPenalty == 0)
            {
                DebugLog.Log("   No terrain modifiers");
            }

            return new CombatStartedChange
            {
                attackerID = attackerArmyID,
                defenderID = defenderArmyID,
                coordinate = defender.coordinate
            };
        }

        /// <summary>
        /// Start combat against a building (uses simpler phase system).
        /// </summary>
        public StateChange StartBuildingCombat(Guid attackerArmyID, Guid buildingID, double currentTime)
        {
            if (gameState == null) return null;
            ArmyData attacker = gameState.GetArmy(attackerArmyID);
            if (attacker == null) return null;
            BuildingData building = gameState.GetBuilding(buildingID);
            if (building == null) return null;

            Guid combatID = Guid.NewGuid();
            var combat = new ActiveCombatData(
                id: combatID,
                attackerArmyID: attackerArmyID,
                defenderArmyID: null,
                defenderBuildingID: buildingID,
                coordinate: building.coordinate,
                startTime: currentTime,
                lastPhaseTime: currentTime
            );

            // Track building info for damage reporting
            combat.buildingType = building.buildingType.DisplayName();
            combat.buildingHealthBefore = building.health;

            buildingCombats[combatID] = combat;

            attacker.isInCombat = true;
            attacker.combatTargetID = buildingID;

            return new CombatStartedChange
            {
                attackerID = attackerArmyID,
                defenderID = buildingID,
                coordinate = building.coordinate
            };
        }

        /// <summary>
        /// Start combat against a villager group (army massacres defenseless civilians).
        /// </summary>
        public StateChange StartVillagerCombat(Guid attackerArmyID, Guid defenderVillagerGroupID, double currentTime)
        {
            if (gameState == null) return null;
            ArmyData attacker = gameState.GetArmy(attackerArmyID);
            if (attacker == null) return null;
            VillagerGroupData villagerGroup = gameState.GetVillagerGroup(defenderVillagerGroupID);
            if (villagerGroup == null) return null;

            Guid combatID = Guid.NewGuid();
            var combat = new VillagerCombatData(
                id: combatID,
                attackerArmyID: attackerArmyID,
                defenderVillagerGroupID: defenderVillagerGroupID,
                defenderOwnerID: villagerGroup.ownerID,
                coordinate: villagerGroup.coordinate,
                startTime: currentTime,
                lastTickTime: currentTime,
                initialVillagerCount: villagerGroup.villagerCount,
                initialAttackerUnitCount: attacker.GetTotalUnits()
            );

            villagerCombats[combatID] = combat;

            attacker.isInCombat = true;
            attacker.combatTargetID = defenderVillagerGroupID;

            DebugLog.Log(string.Format("Army vs Villagers combat started: {0} attacking {1} villagers",
                attacker.name, villagerGroup.villagerCount));

            return new CombatStartedChange
            {
                attackerID = attackerArmyID,
                defenderID = defenderVillagerGroupID,
                coordinate = villagerGroup.coordinate
            };
        }

        // MARK: - Phase-Specific Damage Processing

        private List<StateChange> ProcessCombatDamage(ActiveCombat combat, double deltaTime, GameState state)
        {
            var changes = new List<StateChange>();

            switch (combat.phase)
            {
                case CombatPhase.RangedExchange:
                    // Only ranged and siege units deal damage
                    ProcessRangedDamage(combat, deltaTime, changes, state);
                    break;

                case CombatPhase.MeleeEngagement:
                    // Ranged continue, melee units now engage
                    ProcessRangedDamage(combat, deltaTime, changes, state);
                    ProcessMeleeDamage(combat, deltaTime, changes, state);
                    break;

                case CombatPhase.Cleanup:
                    // All remaining units attack
                    ProcessAllDamage(combat, deltaTime, changes, state);
                    break;

                case CombatPhase.Ended:
                    break;
            }

            return changes;
        }

        private void ProcessRangedDamage(ActiveCombat combat, double deltaTime, List<StateChange> changes, GameState state)
        {
            // Calculate DPS from ranged/siege units only, including bonus damage vs enemy composition
            double attackerRangedDPS = DamageCalculator.CalculateRangedDPS(
                combat.attackerState, combat.defenderState,
                terrainPenalty: combat.terrainAttackPenalty,
                playerState: combat.attackerPlayerState,
                tacticsBonus: combat.attackerTacticsBonus,
                commanderData: combat.attackerCommanderData);
            double defenderRangedDPS = DamageCalculator.CalculateRangedDPS(
                combat.defenderState, combat.attackerState,
                terrainBonus: combat.terrainDefenseBonus + combat.entrenchmentDefenseBonus,
                playerState: combat.defenderPlayerState,
                tacticsBonus: combat.defenderTacticsBonus,
                commanderData: combat.defenderCommanderData);

            double attackerDamage = attackerRangedDPS * deltaTime;
            double defenderDamage = defenderRangedDPS * deltaTime;

            // Apply damage to defender from attacker's ranged units
            if (attackerDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.defenderState, attackerDamage, combat, true, state, "ranged");
                combat.TrackPhaseDamage(true, attackerDamage);
                // Track damage dealt by attacker's ranged/siege units
                TrackDamageDealtByCategory(combat.attackerState, attackerDamage, new[] { UnitCategory.Ranged, UnitCategory.Siege });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.attackerArmies[0].armyID,
                        targetID = combat.defenderArmies[0].armyID,
                        damage = attackerDamage,
                        damageType = "ranged"
                    });
                }
            }

            // Apply damage to attacker from defender's ranged units
            if (defenderDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.attackerState, defenderDamage, combat, false, state, "ranged");
                combat.TrackPhaseDamage(false, defenderDamage);
                // Track damage dealt by defender's ranged/siege units
                TrackDamageDealtByCategory(combat.defenderState, defenderDamage, new[] { UnitCategory.Ranged, UnitCategory.Siege });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.defenderArmies[0].armyID,
                        targetID = combat.attackerArmies[0].armyID,
                        damage = defenderDamage,
                        damageType = "ranged"
                    });
                }
            }
        }

        private void ProcessMeleeDamage(ActiveCombat combat, double deltaTime, List<StateChange> changes, GameState state)
        {
            // Calculate DPS from infantry/cavalry units, including bonus damage vs enemy composition
            bool isCharging = combat.elapsedTime < ActiveCombat.MeleeEngagementThreshold + 1.0; // 1 second charge window after melee starts
            double attackerMeleeDPS = DamageCalculator.CalculateMeleeDPS(
                combat.attackerState, combat.defenderState,
                isCharge: isCharging,
                terrainPenalty: combat.terrainAttackPenalty,
                playerState: combat.attackerPlayerState,
                tacticsBonus: combat.attackerTacticsBonus,
                commanderData: combat.attackerCommanderData);
            double defenderMeleeDPS = DamageCalculator.CalculateMeleeDPS(
                combat.defenderState, combat.attackerState,
                isCharge: false,
                terrainBonus: combat.terrainDefenseBonus + combat.entrenchmentDefenseBonus,
                playerState: combat.defenderPlayerState,
                tacticsBonus: combat.defenderTacticsBonus,
                commanderData: combat.defenderCommanderData);

            double attackerDamage = attackerMeleeDPS * deltaTime;
            double defenderDamage = defenderMeleeDPS * deltaTime;

            // Apply damage to defender from attacker's melee units
            if (attackerDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.defenderState, attackerDamage, combat, true, state, "melee");
                combat.TrackPhaseDamage(true, attackerDamage);
                // Track damage dealt by attacker's melee units
                TrackDamageDealtByCategory(combat.attackerState, attackerDamage, new[] { UnitCategory.Infantry, UnitCategory.Cavalry });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.attackerArmies[0].armyID,
                        targetID = combat.defenderArmies[0].armyID,
                        damage = attackerDamage,
                        damageType = "melee"
                    });
                }
            }

            // Apply damage to attacker from defender's melee units
            if (defenderDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.attackerState, defenderDamage, combat, false, state, "melee");
                combat.TrackPhaseDamage(false, defenderDamage);
                // Track damage dealt by defender's melee units
                TrackDamageDealtByCategory(combat.defenderState, defenderDamage, new[] { UnitCategory.Infantry, UnitCategory.Cavalry });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.defenderArmies[0].armyID,
                        targetID = combat.attackerArmies[0].armyID,
                        damage = defenderDamage,
                        damageType = "melee"
                    });
                }
            }
        }

        private void ProcessAllDamage(ActiveCombat combat, double deltaTime, List<StateChange> changes, GameState state)
        {
            // In cleanup phase, all units attack, including bonus damage vs enemy composition
            double attackerTotalDPS = DamageCalculator.CalculateTotalDPS(
                combat.attackerState, combat.defenderState,
                terrainPenalty: combat.terrainAttackPenalty,
                playerState: combat.attackerPlayerState,
                tacticsBonus: combat.attackerTacticsBonus,
                commanderData: combat.attackerCommanderData);
            double defenderTotalDPS = DamageCalculator.CalculateTotalDPS(
                combat.defenderState, combat.attackerState,
                terrainBonus: combat.terrainDefenseBonus + combat.entrenchmentDefenseBonus,
                playerState: combat.defenderPlayerState,
                tacticsBonus: combat.defenderTacticsBonus,
                commanderData: combat.defenderCommanderData);

            double attackerDamage = attackerTotalDPS * deltaTime;
            double defenderDamage = defenderTotalDPS * deltaTime;

            if (attackerDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.defenderState, attackerDamage, combat, true, state);
                combat.TrackPhaseDamage(true, attackerDamage);
                // Track damage dealt by all attacker units
                TrackDamageDealtByCategory(combat.attackerState, attackerDamage,
                    new[] { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Ranged, UnitCategory.Siege });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.attackerArmies[0].armyID,
                        targetID = combat.defenderArmies[0].armyID,
                        damage = attackerDamage,
                        damageType = "mixed"
                    });
                }
            }

            if (defenderDamage > 0)
            {
                DamageCalculator.ApplyDamageToSide(combat.attackerState, defenderDamage, combat, false, state);
                combat.TrackPhaseDamage(false, defenderDamage);
                // Track damage dealt by all defender units
                TrackDamageDealtByCategory(combat.defenderState, defenderDamage,
                    new[] { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Ranged, UnitCategory.Siege });

                if (combat.attackerArmies.Count > 0 && combat.defenderArmies.Count > 0)
                {
                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.defenderArmies[0].armyID,
                        targetID = combat.attackerArmies[0].armyID,
                        damage = defenderDamage,
                        damageType = "mixed"
                    });
                }
            }
        }

        // MARK: - Damage Dealt Tracking

        /// <summary>
        /// Tracks damage dealt by units in specified categories, distributing proportionally.
        /// </summary>
        private void TrackDamageDealtByCategory(SideCombatState sideState, double damage, UnitCategory[] categories)
        {
            // Get total DPS from units in these categories to distribute damage proportionally
            double totalDPS = 0;
            var unitDPSMap = new Dictionary<MilitaryUnitType, double>();

            foreach (var kvp in sideState.unitCounts)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;
                bool inCategory = false;
                foreach (var cat in categories)
                {
                    if (unitType.Category() == cat) { inCategory = true; break; }
                }
                if (!inCategory) continue;

                UnitCombatStats stats = unitType.CombatStats();
                double unitDPS = stats.TotalDamage / unitType.AttackSpeed() * count;
                unitDPSMap[unitType] = unitDPS;
                totalDPS += unitDPS;
            }

            if (totalDPS <= 0) return;

            // Distribute damage proportionally based on each unit type's contribution
            foreach (var kvp in unitDPSMap)
            {
                double proportion = kvp.Value / totalDPS;
                double damageContribution = damage * proportion;
                sideState.TrackDamageDealt(damageContribution, kvp.Key);
            }
        }

        // MARK: - Auto-Start Building Combat

        /// <summary>
        /// Automatically starts combat against an enemy building at the given location.
        /// Called after winning army vs army combat.
        /// </summary>
        private void AutoStartBuildingCombat(Guid armyID, HexCoordinate location, GameState state, double currentTime, List<StateChange> changes)
        {
            ArmyData army = state.GetArmy(armyID);
            if (army == null || !army.ownerID.HasValue) return;
            Guid ownerID = army.ownerID.Value;

            // Find enemy building at this location
            BuildingData building = state.GetBuilding(location);
            if (building == null) return;
            if (!building.ownerID.HasValue) return;
            if (building.ownerID.Value == ownerID) return;
            if (!building.IsOperational) return;

            // Start building combat
            DebugLog.Log(string.Format("Auto-starting building attack: {0} vs {1}",
                army.name, building.buildingType.DisplayName()));

            StateChange combatChange = StartBuildingCombat(armyID, building.id, currentTime);
            if (combatChange != null)
            {
                changes.Add(combatChange);
            }
        }

        // MARK: - Building Combat Processing

        private List<StateChange> ProcessBuildingCombats(double currentTime, GameState state)
        {
            var changes = new List<StateChange>();
            var completedCombats = new List<Guid>();

            foreach (var kvp in new Dictionary<Guid, ActiveCombatData>(buildingCombats))
            {
                Guid combatID = kvp.Key;
                ActiveCombatData combat = kvp.Value;

                if (combat.isComplete)
                {
                    completedCombats.Add(combatID);
                    continue;
                }

                // Check if it's time for the next phase
                if (currentTime - combat.lastPhaseTime >= buildingPhaseInterval)
                {
                    if (combat.defenderBuildingID.HasValue)
                    {
                        var phaseChanges = ProcessArmyVsBuildingPhase(combat, combat.defenderBuildingID.Value, state);
                        changes.AddRange(phaseChanges);
                    }

                    combat.currentPhase += 1;
                    combat.lastPhaseTime = currentTime;
                    buildingCombats[combatID] = combat;

                    if (combat.isComplete)
                    {
                        completedCombats.Add(combatID);

                        // Emit combatEnded with building damage result
                        if (combat.result.HasValue && combat.defenderBuildingID.HasValue)
                        {
                            changes.Add(new CombatEndedChange
                            {
                                attackerID = combat.attackerArmyID,
                                defenderID = combat.defenderBuildingID.Value,
                                result = combat.result.Value
                            });
                        }
                    }
                }
            }

            // Remove completed combats
            foreach (Guid combatID in completedCombats)
            {
                buildingCombats.Remove(combatID);
            }

            return changes;
        }

        private List<StateChange> ProcessArmyVsBuildingPhase(ActiveCombatData combat, Guid buildingID, GameState state)
        {
            ArmyData attacker = state.GetArmy(combat.attackerArmyID);
            BuildingData building = state.GetBuilding(buildingID);

            if (attacker == null || building == null)
            {
                combat.isComplete = true;
                return new List<StateChange>();
            }

            var changes = new List<StateChange>();

            // Calculate siege damage
            UnitCombatStats attackerStats = attacker.GetAggregatedCombatStats();
            double damage = attackerStats.TotalDamage;

            // Siege bonus vs buildings
            int siegeCount = attacker.GetUnitCountByCategory(UnitCategory.Siege);
            if (siegeCount > 0)
            {
                damage += attackerStats.bonusVsBuildings;
                damage *= siegeBuildingBonusMultiplier;
            }

            // Track damage for reporting
            combat.totalBuildingDamage += damage;

            // Apply damage to building
            building.TakeDamage(damage);

            changes.Add(new CombatDamageDealtChange
            {
                sourceID = combat.attackerArmyID,
                targetID = buildingID,
                damage = damage,
                damageType = "siege"
            });

            changes.Add(new BuildingDamagedChange
            {
                buildingID = buildingID,
                currentHealth = building.health,
                maxHealth = building.maxHealth
            });

            // Check for building destruction
            if (building.health <= 0)
            {
                combat.isComplete = true;

                // Create building damage record
                var buildingDamageRecord = new BuildingDamageRecord(
                    buildingID,
                    combat.buildingType ?? building.buildingType.DisplayName(),
                    combat.totalBuildingDamage,
                    combat.buildingHealthBefore,
                    0,
                    true
                );

                // Set combat result with building damage info
                combat.result = new CombatResultData(
                    winnerID: combat.attackerArmyID,
                    loserID: null,
                    attackerCasualties: new Dictionary<string, int>(),
                    defenderCasualties: new Dictionary<string, int>(),
                    combatDuration: 0,
                    buildingDamage: buildingDamageRecord
                );

                changes.Add(new BuildingDestroyedChange
                {
                    buildingID = buildingID,
                    coordinate = building.coordinate
                });

                // Clean up attacker's combat flags
                attacker.isInCombat = false;
                attacker.combatTargetID = null;

                // Initiate retreat for any defending armies that were stationed at this building
                InitiateRetreatForDefendersAtBuilding(building, state);

                // Remove building from game state
                state.RemoveBuilding(buildingID);
            }

            return changes;
        }

        /// <summary>
        /// Initiates retreat for armies that were defending a building when it was destroyed.
        /// </summary>
        private void InitiateRetreatForDefendersAtBuilding(BuildingData building, GameState state)
        {
            if (!building.ownerID.HasValue) return;
            Guid buildingOwnerID = building.ownerID.Value;

            // Find armies at any of the building's occupied coordinates
            var buildingCoords = building.OccupiedCoordinates;
            var defendingArmies = new List<ArmyData>();

            foreach (var army in state.armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value != buildingOwnerID) continue;
                if (buildingCoords.Contains(army.coordinate))
                {
                    defendingArmies.Add(army);
                }
            }

            if (defendingArmies.Count == 0) return;

            foreach (ArmyData army in defendingArmies)
            {
                // Find a home base with capacity for each army
                BuildingData newHomeBase = state.FindHomeBaseWithCapacity(buildingOwnerID, army.coordinate, building.id);
                if (newHomeBase == null)
                {
                    DebugLog.Log(string.Format("No home base available for retreat - {0} has nowhere to go", army.name));
                    continue;
                }

                // Update home base
                army.homeBaseID = newHomeBase.id;

                // Calculate retreat path
                List<HexCoordinate> path = state.mapData.FindPath(army.coordinate, newHomeBase.coordinate, buildingOwnerID, state);
                if (path == null || path.Count == 0)
                {
                    DebugLog.Log(string.Format("{0} cannot find retreat path from destroyed building", army.name));
                    continue;
                }

                // Set retreat state
                army.isRetreating = true;
                army.currentPath = path;
                army.pathIndex = 0;
                army.movementProgress = 0.0;

                DebugLog.Log(string.Format("{0} retreating from destroyed {1} to {2}",
                    army.name, building.buildingType.DisplayName(), newHomeBase.buildingType.DisplayName()));
            }
        }

        // MARK: - Villager Combat Processing

        private List<StateChange> ProcessVillagerCombats(double currentTime, GameState state)
        {
            var changes = new List<StateChange>();
            var completedCombats = new List<Guid>();

            foreach (var kvp in new Dictionary<Guid, VillagerCombatData>(villagerCombats))
            {
                Guid combatID = kvp.Key;
                VillagerCombatData combat = kvp.Value;

                if (combat.isComplete)
                {
                    completedCombats.Add(combatID);
                    continue;
                }

                // Get attacker and defender
                ArmyData attacker = state.GetArmy(combat.attackerArmyID);
                VillagerGroupData villagerGroup = state.GetVillagerGroup(combat.defenderVillagerGroupID);

                if (attacker == null || villagerGroup == null)
                {
                    // Combat target no longer exists
                    combat.isComplete = true;
                    villagerCombats[combatID] = combat;
                    completedCombats.Add(combatID);

                    // Clean up attacker
                    ArmyData attackerCleanup = state.GetArmy(combat.attackerArmyID);
                    if (attackerCleanup != null)
                    {
                        attackerCleanup.isInCombat = false;
                        attackerCleanup.combatTargetID = null;
                    }
                    continue;
                }

                // Calculate time delta
                double deltaTime = currentTime - combat.lastTickTime;
                if (deltaTime <= 0) continue;

                combat.lastTickTime = currentTime;

                // === Army attacks villagers ===
                UnitCombatStats attackerStats = attacker.GetAggregatedCombatStats();
                double armyDamagePerSecond = attackerStats.TotalDamage;
                // Reduce damage by villager armor (minimum 1 damage)
                double effectiveArmyDamage = Math.Max(1.0, armyDamagePerSecond - villagerGroup.TotalMeleeArmor) * deltaTime;

                // Accumulate damage and convert to villager kills
                combat.accumulatedDamage += effectiveArmyDamage;
                int villagersToKill = (int)(combat.accumulatedDamage / VillagerGroupData.HpPerVillager);

                if (villagersToKill > 0)
                {
                    int actualKills = Math.Min(villagersToKill, villagerGroup.villagerCount);
                    int removedCount = villagerGroup.RemoveVillagers(actualKills);
                    combat.villagersKilled += removedCount;
                    combat.accumulatedDamage -= villagersToKill * VillagerGroupData.HpPerVillager;

                    changes.Add(new VillagerCasualtiesChange
                    {
                        villagerGroupID = combat.defenderVillagerGroupID,
                        casualties = removedCount,
                        remaining = villagerGroup.villagerCount
                    });

                    changes.Add(new CombatDamageDealtChange
                    {
                        sourceID = combat.attackerArmyID,
                        targetID = combat.defenderVillagerGroupID,
                        damage = removedCount * VillagerGroupData.HpPerVillager,
                        damageType = "melee"
                    });
                }

                // === Villagers fight back (weakly) ===
                if (villagerGroup.villagerCount > 0)
                {
                    double villagerDamage = villagerGroup.TotalMeleeAttack * deltaTime;
                    // Apply damage to army (simplified - spread across units)
                    Dictionary<MilitaryUnitType, int> armyCasualties = DamageCalculator.ApplyDamageToArmy(attacker, villagerDamage);

                    if (armyCasualties.Count > 0)
                    {
                        double totalDamageDealt = 0;
                        foreach (var casualtyKvp in armyCasualties)
                        {
                            totalDamageDealt += (double)casualtyKvp.Value * casualtyKvp.Key.HP();
                        }
                        changes.Add(new CombatDamageDealtChange
                        {
                            sourceID = combat.defenderVillagerGroupID,
                            targetID = combat.attackerArmyID,
                            damage = totalDamageDealt,
                            damageType = "melee"
                        });
                    }

                    // Check if army was destroyed
                    if (attacker.IsEmpty())
                    {
                        combat.isComplete = true;
                        completedCombats.Add(combatID);

                        DebugLog.Log("Army destroyed by villagers!");

                        // Save combat record before removing army
                        CombatRecord record = CreateVillagerCombatRecord(combat, currentTime, state);
                        AddCombatRecord(record);

                        changes.Add(new ArmyDestroyedChange
                        {
                            armyID = combat.attackerArmyID,
                            coordinate = attacker.coordinate
                        });

                        state.RemoveArmy(combat.attackerArmyID);
                        villagerCombats[combatID] = combat;
                        continue;
                    }
                }

                // Check if all villagers are dead
                if (villagerGroup.IsEmpty())
                {
                    combat.isComplete = true;
                    completedCombats.Add(combatID);

                    DebugLog.Log(string.Format("Villager group destroyed: {0} villagers killed", combat.villagersKilled));

                    // Save combat record before removing villager group
                    CombatRecord record = CreateVillagerCombatRecord(combat, currentTime, state);
                    AddCombatRecord(record);

                    changes.Add(new VillagerGroupDestroyedChange
                    {
                        groupID = combat.defenderVillagerGroupID,
                        coordinate = combat.coordinate
                    });

                    // Clean up attacker's combat flags
                    attacker.isInCombat = false;
                    attacker.combatTargetID = null;

                    // Remove villager group from game state
                    state.RemoveVillagerGroup(combat.defenderVillagerGroupID);
                }

                villagerCombats[combatID] = combat;
            }

            // Remove completed combats
            foreach (Guid combatID in completedCombats)
            {
                villagerCombats.Remove(combatID);
            }

            return changes;
        }

        // MARK: - Combat Resolution

        private CombatResultData DetermineCombatResult(ActiveCombat combat)
        {
            Guid? winnerID = null;
            Guid? loserID = null;

            Guid? attackerID = combat.attackerArmies.Count > 0 ? (Guid?)combat.attackerArmies[0].armyID : null;
            Guid? defenderID = combat.defenderArmies.Count > 0 ? (Guid?)combat.defenderArmies[0].armyID : null;

            bool attackerDead = combat.attackerState.TotalUnits == 0;
            bool defenderDead = combat.defenderState.TotalUnits == 0;

            if (attackerDead && defenderDead)
            {
                // Draw - mutual destruction
            }
            else if (attackerDead)
            {
                winnerID = defenderID;
                loserID = attackerID;
            }
            else if (defenderDead)
            {
                winnerID = attackerID;
                loserID = defenderID;
            }
            else
            {
                // Combat ended without total destruction - compare remaining strength
                double attackerStrength = DamageCalculator.CalculateTotalDPS(
                    combat.attackerState, combat.defenderState,
                    playerState: combat.attackerPlayerState,
                    commanderData: combat.attackerCommanderData);
                double defenderStrength = DamageCalculator.CalculateTotalDPS(
                    combat.defenderState, combat.attackerState,
                    playerState: combat.defenderPlayerState,
                    commanderData: combat.defenderCommanderData);

                if (attackerStrength > defenderStrength)
                {
                    winnerID = attackerID;
                    loserID = defenderID;
                }
                else
                {
                    winnerID = defenderID;
                    loserID = attackerID;
                }
            }

            // Calculate casualties for the result
            var attackerCasualties = new Dictionary<string, int>();
            var defenderCasualties = new Dictionary<string, int>();

            foreach (var kvp in combat.attackerState.initialComposition)
            {
                int currentCount;
                combat.attackerState.unitCounts.TryGetValue(kvp.Key, out currentCount);
                int lost = kvp.Value - currentCount;
                if (lost > 0)
                {
                    attackerCasualties[kvp.Key.ToString()] = lost;
                }
            }

            foreach (var kvp in combat.defenderState.initialComposition)
            {
                int currentCount;
                combat.defenderState.unitCounts.TryGetValue(kvp.Key, out currentCount);
                int lost = kvp.Value - currentCount;
                if (lost > 0)
                {
                    defenderCasualties[kvp.Key.ToString()] = lost;
                }
            }

            return new CombatResultData(
                winnerID: winnerID,
                loserID: loserID,
                attackerCasualties: attackerCasualties,
                defenderCasualties: defenderCasualties,
                combatDuration: combat.elapsedTime
            );
        }

        private void CleanupCombatFlags(ActiveCombat combat, GameState state)
        {
            // Clean up attacker armies
            foreach (ArmyCombatState armyState in combat.attackerArmies)
            {
                ArmyData army = state.GetArmy(armyState.armyID);
                if (army != null)
                {
                    army.isInCombat = false;
                    army.combatTargetID = null;
                }
            }

            // Clean up defender armies
            foreach (ArmyCombatState armyState in combat.defenderArmies)
            {
                ArmyData army = state.GetArmy(armyState.armyID);
                if (army != null)
                {
                    army.isInCombat = false;
                    army.combatTargetID = null;
                }
            }
        }

        /// <summary>
        /// Creates a CombatRecord from an ActiveCombat for saving to history.
        /// </summary>
        private CombatRecord CreateCombatRecord(ActiveCombat combat, GameState state)
        {
            // Get attacker info
            ArmyCombatState attackerArmyState = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0] : null;
            ArmyData attackerArmy = attackerArmyState != null ? state.GetArmy(attackerArmyState.armyID) : null;
            PlayerState attackerOwner = null;
            if (attackerArmy != null && attackerArmy.ownerID.HasValue)
                attackerOwner = state.GetPlayer(attackerArmy.ownerID.Value);

            Color attackerColor = Color.gray;
            if (attackerOwner != null && !string.IsNullOrEmpty(attackerOwner.colorHex))
            {
                Color parsedColor;
                if (ColorUtility.TryParseHtmlString(attackerOwner.colorHex, out parsedColor))
                    attackerColor = parsedColor;
            }

            var attackerParticipant = new CombatParticipant(
                name: attackerArmyState != null ? attackerArmyState.armyName : "Unknown Attacker",
                type: CombatParticipantType.Army,
                ownerName: attackerOwner != null ? attackerOwner.name : "Unknown",
                ownerColor: attackerColor,
                commanderName: attackerArmyState != null ? attackerArmyState.commanderName : null
            );

            // Get defender info
            ArmyCombatState defenderArmyState = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0] : null;
            ArmyData defenderArmy = defenderArmyState != null ? state.GetArmy(defenderArmyState.armyID) : null;
            PlayerState defenderOwner = null;
            if (defenderArmy != null && defenderArmy.ownerID.HasValue)
                defenderOwner = state.GetPlayer(defenderArmy.ownerID.Value);

            Color defenderColor = Color.gray;
            if (defenderOwner != null && !string.IsNullOrEmpty(defenderOwner.colorHex))
            {
                Color parsedColor;
                if (ColorUtility.TryParseHtmlString(defenderOwner.colorHex, out parsedColor))
                    defenderColor = parsedColor;
            }

            var defenderParticipant = new CombatParticipant(
                name: defenderArmyState != null ? defenderArmyState.armyName : "Unknown Defender",
                type: CombatParticipantType.Army,
                ownerName: defenderOwner != null ? defenderOwner.name : "Unknown",
                ownerColor: defenderColor,
                commanderName: defenderArmyState != null ? defenderArmyState.commanderName : null
            );

            // Calculate casualties
            int attackerCasualties = combat.attackerState.initialUnitCount - combat.attackerState.TotalUnits;
            int defenderCasualties = combat.defenderState.initialUnitCount - combat.defenderState.TotalUnits;

            return new CombatRecord(
                attacker: attackerParticipant,
                defender: defenderParticipant,
                attackerInitialStrength: (double)combat.attackerState.initialUnitCount,
                defenderInitialStrength: (double)combat.defenderState.initialUnitCount,
                attackerFinalStrength: (double)combat.attackerState.TotalUnits,
                defenderFinalStrength: (double)combat.defenderState.TotalUnits,
                winner: combat.Winner,
                attackerCasualties: attackerCasualties,
                defenderCasualties: defenderCasualties,
                location: combat.location,
                duration: combat.elapsedTime
            );
        }

        /// <summary>
        /// Creates a CombatRecord from a completed VillagerCombatData for battle history.
        /// </summary>
        private CombatRecord CreateVillagerCombatRecord(VillagerCombatData combat, double currentTime, GameState state)
        {
            // Get attacker info
            ArmyData attackerArmy = state.GetArmy(combat.attackerArmyID);
            PlayerState attackerOwner = null;
            if (attackerArmy != null && attackerArmy.ownerID.HasValue)
                attackerOwner = state.GetPlayer(attackerArmy.ownerID.Value);

            string commanderName = null;
            if (attackerArmy != null && attackerArmy.commanderID.HasValue)
            {
                CommanderData commander = state.GetCommander(attackerArmy.commanderID.Value);
                if (commander != null)
                    commanderName = commander.name;
            }

            Color attackerColor = Color.gray;
            if (attackerOwner != null && !string.IsNullOrEmpty(attackerOwner.colorHex))
            {
                Color parsedColor;
                if (ColorUtility.TryParseHtmlString(attackerOwner.colorHex, out parsedColor))
                    attackerColor = parsedColor;
            }

            var attackerParticipant = new CombatParticipant(
                name: attackerArmy != null ? attackerArmy.name : "Unknown Army",
                type: CombatParticipantType.Army,
                ownerName: attackerOwner != null ? attackerOwner.name : "Unknown",
                ownerColor: attackerColor,
                commanderName: commanderName
            );

            // Get defender info
            PlayerState defenderOwner = null;
            if (combat.defenderOwnerID.HasValue)
                defenderOwner = state.GetPlayer(combat.defenderOwnerID.Value);
            VillagerGroupData villagerGroup = state.GetVillagerGroup(combat.defenderVillagerGroupID);

            Color defenderColor = Color.gray;
            if (defenderOwner != null && !string.IsNullOrEmpty(defenderOwner.colorHex))
            {
                Color parsedColor;
                if (ColorUtility.TryParseHtmlString(defenderOwner.colorHex, out parsedColor))
                    defenderColor = parsedColor;
            }

            var defenderParticipant = new CombatParticipant(
                name: villagerGroup != null ? villagerGroup.name : "Villagers",
                type: CombatParticipantType.VillagerGroup,
                ownerName: defenderOwner != null ? defenderOwner.name : "Unknown",
                ownerColor: defenderColor,
                commanderName: null
            );

            // Calculate casualties
            int attackerFinalUnits = attackerArmy != null ? attackerArmy.GetTotalUnits() : 0;
            int attackerCasualties = combat.initialAttackerUnitCount - attackerFinalUnits;
            int defenderFinalUnits = villagerGroup != null ? villagerGroup.villagerCount : 0;

            // Determine winner
            CombatResult winner;
            if (attackerFinalUnits == 0)
            {
                winner = CombatResult.DefenderVictory;
            }
            else if (defenderFinalUnits == 0)
            {
                winner = CombatResult.AttackerVictory;
            }
            else
            {
                winner = CombatResult.Draw;
            }

            return new CombatRecord(
                attacker: attackerParticipant,
                defender: defenderParticipant,
                attackerInitialStrength: (double)combat.initialAttackerUnitCount,
                defenderInitialStrength: (double)combat.initialVillagerCount,
                attackerFinalStrength: (double)attackerFinalUnits,
                defenderFinalStrength: (double)defenderFinalUnits,
                winner: winner,
                attackerCasualties: attackerCasualties,
                defenderCasualties: combat.villagersKilled,
                location: combat.coordinate,
                duration: currentTime - combat.startTime
            );
        }

        /// <summary>
        /// Creates a DetailedCombatRecord from an ActiveCombat for enhanced battle reports.
        /// </summary>
        private DetailedCombatRecord CreateDetailedCombatRecord(ActiveCombat combat, GameState state)
        {
            // Get attacker info
            ArmyCombatState attackerArmyState = combat.attackerArmies.Count > 0 ? combat.attackerArmies[0] : null;
            ArmyData attackerArmy = attackerArmyState != null ? state.GetArmy(attackerArmyState.armyID) : null;
            PlayerState attackerOwner = null;
            if (attackerArmy != null && attackerArmy.ownerID.HasValue)
                attackerOwner = state.GetPlayer(attackerArmy.ownerID.Value);

            // Get defender info
            ArmyCombatState defenderArmyState = combat.defenderArmies.Count > 0 ? combat.defenderArmies[0] : null;
            ArmyData defenderArmy = defenderArmyState != null ? state.GetArmy(defenderArmyState.armyID) : null;
            PlayerState defenderOwner = null;
            if (defenderArmy != null && defenderArmy.ownerID.HasValue)
                defenderOwner = state.GetPlayer(defenderArmy.ownerID.Value);

            // Build unit breakdowns for attacker
            var attackerUnitBreakdowns = new List<UnitCombatBreakdown>();
            foreach (var kvp in combat.attackerState.initialComposition)
            {
                MilitaryUnitType unitType = kvp.Key;
                int initialCount = kvp.Value;
                int finalCount;
                combat.attackerState.unitCounts.TryGetValue(unitType, out finalCount);
                int casualties = initialCount - finalCount;
                double damageDealt;
                combat.attackerState.damageDealtByType.TryGetValue(unitType, out damageDealt);
                double damageReceived;
                combat.attackerState.damageReceivedByType.TryGetValue(unitType, out damageReceived);

                attackerUnitBreakdowns.Add(new UnitCombatBreakdown(
                    unitType, initialCount, finalCount, casualties, damageDealt, damageReceived
                ));
            }

            // Build unit breakdowns for defender
            var defenderUnitBreakdowns = new List<UnitCombatBreakdown>();
            foreach (var kvp in combat.defenderState.initialComposition)
            {
                MilitaryUnitType unitType = kvp.Key;
                int initialCount = kvp.Value;
                int finalCount;
                combat.defenderState.unitCounts.TryGetValue(unitType, out finalCount);
                int casualties = initialCount - finalCount;
                double damageDealt;
                combat.defenderState.damageDealtByType.TryGetValue(unitType, out damageDealt);
                double damageReceived;
                combat.defenderState.damageReceivedByType.TryGetValue(unitType, out damageReceived);

                defenderUnitBreakdowns.Add(new UnitCombatBreakdown(
                    unitType, initialCount, finalCount, casualties, damageDealt, damageReceived
                ));
            }

            // Build army breakdowns for multi-army support
            var attackerArmyBreakdowns = new List<ArmyCombatBreakdown>();
            for (int index = 0; index < combat.attackerArmies.Count; index++)
            {
                ArmyCombatState armyState = combat.attackerArmies[index];
                attackerArmyBreakdowns.Add(new ArmyCombatBreakdown(
                    armyState.armyID.ToString(),
                    armyState.armyName,
                    armyState.ownerName,
                    armyState.commanderName,
                    armyState.joinTime,
                    index > 0,
                    armyState.initialComposition,
                    armyState.currentUnits,
                    armyState.casualtiesByType,
                    armyState.damageDealtByType
                ));
            }

            var defenderArmyBreakdowns = new List<ArmyCombatBreakdown>();
            for (int index = 0; index < combat.defenderArmies.Count; index++)
            {
                ArmyCombatState armyState = combat.defenderArmies[index];
                defenderArmyBreakdowns.Add(new ArmyCombatBreakdown(
                    armyState.armyID.ToString(),
                    armyState.armyName,
                    armyState.ownerName,
                    armyState.commanderName,
                    armyState.joinTime,
                    index > 0,
                    armyState.initialComposition,
                    armyState.currentUnits,
                    armyState.casualtiesByType,
                    armyState.damageDealtByType
                ));
            }

            // Build attacker composition dictionaries (string keys for serialization)
            var attackerInitialComp = new Dictionary<string, int>();
            foreach (var kvp in combat.attackerState.initialComposition)
                attackerInitialComp[kvp.Key.ToString()] = kvp.Value;

            var attackerFinalComp = new Dictionary<string, int>();
            foreach (var kvp in combat.attackerState.unitCounts)
                attackerFinalComp[kvp.Key.ToString()] = kvp.Value;

            var defenderInitialComp = new Dictionary<string, int>();
            foreach (var kvp in combat.defenderState.initialComposition)
                defenderInitialComp[kvp.Key.ToString()] = kvp.Value;

            var defenderFinalComp = new Dictionary<string, int>();
            foreach (var kvp in combat.defenderState.unitCounts)
                defenderFinalComp[kvp.Key.ToString()] = kvp.Value;

            return new DetailedCombatRecord
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                Location = combat.location,
                TotalDuration = combat.elapsedTime,
                Winner = combat.Winner,
                TerrainType = combat.terrainType.DisplayName(),
                TerrainDefenseBonus = combat.terrainDefenseBonus,
                TerrainAttackPenalty = combat.terrainAttackPenalty,
                EntrenchmentDefenseBonus = combat.entrenchmentDefenseBonus,
                AttackerName = attackerArmyState != null ? attackerArmyState.armyName : "Unknown Attacker",
                AttackerOwner = attackerOwner != null ? attackerOwner.name : "Unknown",
                AttackerCommander = attackerArmyState != null ? attackerArmyState.commanderName : null,
                AttackerCommanderSpecialty = combat.attackerCommanderData != null
                    ? combat.attackerCommanderData.specialty.ToString()
                    : (attackerArmyState != null ? attackerArmyState.commanderSpecialty : null),
                AttackerInitialComposition = attackerInitialComp,
                AttackerFinalComposition = attackerFinalComp,
                DefenderName = defenderArmyState != null ? defenderArmyState.armyName : "Unknown Defender",
                DefenderOwner = defenderOwner != null ? defenderOwner.name : "Unknown",
                DefenderCommander = defenderArmyState != null ? defenderArmyState.commanderName : null,
                DefenderCommanderSpecialty = combat.defenderCommanderData != null
                    ? combat.defenderCommanderData.specialty.ToString()
                    : (defenderArmyState != null ? defenderArmyState.commanderSpecialty : null),
                DefenderInitialComposition = defenderInitialComp,
                DefenderFinalComposition = defenderFinalComp,
                PhaseRecords = combat.phaseRecords,
                AttackerUnitBreakdowns = attackerUnitBreakdowns,
                DefenderUnitBreakdowns = defenderUnitBreakdowns,
                AttackerArmyBreakdowns = attackerArmyBreakdowns,
                DefenderArmyBreakdowns = defenderArmyBreakdowns
            };
        }

        // MARK: - Stack Combat Initiation

        /// <summary>
        /// Start a stack combat at the target coordinate with multiple attackers vs a defensive stack.
        /// </summary>
        public List<StateChange> StartStackCombat(List<Guid> attackerArmyIDs, HexCoordinate coordinate, double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            Guid attackerOwnerID = Guid.Empty;
            foreach (Guid id in attackerArmyIDs)
            {
                ArmyData army = gameState.GetArmy(id);
                if (army != null && army.ownerID.HasValue)
                {
                    attackerOwnerID = army.ownerID.Value;
                    break;
                }
            }
            if (attackerOwnerID == Guid.Empty) attackerOwnerID = Guid.NewGuid();

            // Build defensive stack
            DefensiveStack defensiveStack = DefensiveStack.Build(coordinate, gameState, attackerOwnerID);

            if (defensiveStack.IsEmpty)
            {
                DebugLog.Log(string.Format("Stack combat: No defenders at {0}", coordinate));
                return new List<StateChange>();
            }

            var stackCombat = new StackCombat(coordinate, currentTime, attackerOwnerID);
            stackCombat.crossTileDefenderIDs = defensiveStack.CrossTileDefenderIDs;
            stackCombat.villagerGroupIDs = new List<Guid>(defensiveStack.villagerGroupIDs);

            // Queue all defenders by tier order
            stackCombat.defenderQueue = defensiveStack.ArmyEntries;

            // Create N-to-N pairings
            var remainingAttackers = new List<Guid>(attackerArmyIDs);
            var changes = new List<StateChange>();

            // Pair attackers with defenders
            while (remainingAttackers.Count > 0 && stackCombat.defenderQueue.Count > 0)
            {
                Guid attackerID = remainingAttackers[0];
                remainingAttackers.RemoveAt(0);
                DefensiveStackEntry? defenderEntry = stackCombat.DequeueNextDefender();
                if (!defenderEntry.HasValue) break;

                StateChange combatChange = CreateStackPairing(stackCombat, attackerID, defenderEntry.Value, currentTime, gameState);
                if (combatChange != null)
                {
                    changes.Add(combatChange);
                }
            }

            // Queue unmatched attackers
            stackCombat.attackerQueue = remainingAttackers;

            // Unmatched attackers join existing pairings as reinforcements
            while (true)
            {
                Guid? nextAttacker = stackCombat.DequeueNextAttacker();
                if (!nextAttacker.HasValue) break;

                CombatPairing firstPairing = stackCombat.activePairings.FirstOrDefault(p => !p.isComplete);
                if (firstPairing != null)
                {
                    ActiveCombat pairingCombat;
                    if (activeCombats.TryGetValue(firstPairing.activeCombatID, out pairingCombat))
                    {
                        ArmyData attackerArmy = gameState.GetArmy(nextAttacker.Value);
                        if (attackerArmy != null)
                        {
                            pairingCombat.AddReinforcement(attackerArmy, true);
                            attackerArmy.isInCombat = true;
                            attackerArmy.combatTargetID = firstPairing.defenderArmyID;
                            DebugLog.Log(string.Format("Stack: Attacker {0} joining as reinforcement", attackerArmy.name));
                        }
                    }
                }
            }

            stackCombats[stackCombat.id] = stackCombat;

            var defenderArmyIDs = new List<Guid>();
            foreach (var entry in defensiveStack.ArmyEntries)
            {
                defenderArmyIDs.Add(entry.armyID);
            }
            changes.Insert(0, new StackCombatStartedChange
            {
                coordinate = coordinate,
                attackerArmyIDs = new List<Guid>(attackerArmyIDs),
                defenderArmyIDs = defenderArmyIDs
            });

            DebugLog.Log(string.Format("Stack combat started at {0}: {1} attackers vs {2} defenders ({3} villager groups)",
                coordinate, attackerArmyIDs.Count, defensiveStack.entries.Count, defensiveStack.villagerGroupIDs.Count));

            return changes;
        }

        /// <summary>
        /// Creates a single combat pairing within a stack combat.
        /// </summary>
        private StateChange CreateStackPairing(StackCombat stackCombat, Guid attackerID, DefensiveStackEntry defenderEntry, double currentTime, GameState state)
        {
            ArmyData attacker = state.GetArmy(attackerID);
            ArmyData defender = state.GetArmy(defenderEntry.armyID);
            if (attacker == null || defender == null) return null;

            // Get terrain at combat location
            TerrainType? terrainNullable = state.mapData.GetTerrain(stackCombat.coordinate);
            TerrainType terrain = terrainNullable.HasValue ? terrainNullable.Value : TerrainType.Plains;

            // Create the ActiveCombat
            var combat = new ActiveCombat(attacker, defender, stackCombat.coordinate, terrain, currentTime);

            // Set player states for research bonuses
            if (attacker.ownerID.HasValue)
            {
                combat.attackerPlayerState = state.GetPlayer(attacker.ownerID.Value);
            }
            if (defender.ownerID.HasValue)
            {
                combat.defenderPlayerState = state.GetPlayer(defender.ownerID.Value);
            }

            // Commander tactics bonuses and store commander data
            if (attacker.commanderID.HasValue)
            {
                CommanderData attackerCommander = state.GetCommander(attacker.commanderID.Value);
                if (attackerCommander != null)
                {
                    combat.attackerTacticsBonus = (double)attackerCommander.Tactics * GameConfig.Commander.TacticsTerrainScaling;
                    combat.attackerCommanderData = attackerCommander;
                }
            }
            if (defender.commanderID.HasValue)
            {
                CommanderData defenderCommander = state.GetCommander(defender.commanderID.Value);
                if (defenderCommander != null)
                {
                    combat.defenderTacticsBonus = (double)defenderCommander.Tactics * GameConfig.Commander.TacticsTerrainScaling;
                    combat.defenderCommanderData = defenderCommander;
                }
            }

            // Apply entrenchment defense bonus
            if (defenderEntry.entrenchmentBonus > 0)
            {
                combat.entrenchmentDefenseBonus = defenderEntry.entrenchmentBonus;
            }

            // Mark as stack pairing so ProcessArmyCombats() skips it
            combat.isStackPairing = true;

            // Store combat
            activeCombats[combat.id] = combat;

            // Mark armies as in combat
            attacker.isInCombat = true;
            attacker.combatTargetID = defenderEntry.armyID;
            defender.isInCombat = true;
            defender.combatTargetID = attackerID;

            // Track fronts for stretching
            stackCombat.AddFront(attackerID);
            stackCombat.AddFront(defenderEntry.armyID);

            // Create pairing record
            var pairing = new CombatPairing(attackerID, defenderEntry.armyID, combat.id);
            stackCombat.activePairings.Add(pairing);

            DebugLog.Log(string.Format("Stack pairing: {0} vs {1} (Tier: {2}, Cross-tile: {3})",
                attacker.name, defender.name, defenderEntry.tier.DisplayName(), defenderEntry.isCrossTile));

            return new CombatStartedChange
            {
                attackerID = attackerID,
                defenderID = defenderEntry.armyID,
                coordinate = stackCombat.coordinate
            };
        }

        // MARK: - Stack Combat Processing

        private List<StateChange> ProcessStackCombats(double currentTime, GameState state)
        {
            var changes = new List<StateChange>();
            var completedStacks = new List<Guid>();

            foreach (var stackKvp in new Dictionary<Guid, StackCombat>(stackCombats))
            {
                Guid stackID = stackKvp.Key;
                StackCombat stackCombat = stackKvp.Value;

                if (stackCombat.isComplete)
                {
                    completedStacks.Add(stackID);
                    continue;
                }

                // Check each pairing for completion
                for (int index = 0; index < stackCombat.activePairings.Count; index++)
                {
                    CombatPairing pairing = stackCombat.activePairings[index];
                    if (pairing.isComplete) continue;

                    // Check if the underlying ActiveCombat has ended
                    ActiveCombat combat;
                    if (!activeCombats.TryGetValue(pairing.activeCombatID, out combat))
                    {
                        // Combat was already cleaned up (retreat, etc.)
                        stackCombat.activePairings[index].isComplete = true;
                        continue;
                    }

                    if (combat.phase != CombatPhase.Ended) continue;

                    // Pairing completed
                    stackCombat.activePairings[index].isComplete = true;

                    CombatResultData result = DetermineCombatResult(combat);
                    stackCombat.activePairings[index].winnerArmyID = result.winnerID;
                    stackCombat.activePairings[index].loserArmyID = result.loserID;

                    // Handle draw: both armies empty -- retreat or destroy both
                    if (!result.winnerID.HasValue && !result.loserID.HasValue)
                    {
                        var bothArmyIDs = new List<Guid> { pairing.attackerArmyID, pairing.defenderArmyID };
                        foreach (Guid armyID in bothArmyIDs)
                        {
                            stackCombat.defeatedArmyIDs.Add(armyID);
                            stackCombat.RemoveFront(armyID);
                            List<HexCoordinate> retreatPath = InitiateAutoRetreat(armyID, state);
                            if (retreatPath != null)
                            {
                                changes.Add(new ArmyAutoRetreatingChange { armyID = armyID, path = retreatPath });
                            }
                            ArmyData army = state.GetArmy(armyID);
                            if (army != null && army.IsEmpty() && !army.isRetreating)
                            {
                                changes.Add(new ArmyDestroyedChange { armyID = armyID, coordinate = army.coordinate });
                                state.RemoveArmy(armyID);
                            }
                        }
                        DebugLog.Log("Stack pairing draw: both armies eliminated");
                    }

                    // Handle the loser
                    if (result.loserID.HasValue)
                    {
                        Guid loserID = result.loserID.Value;
                        stackCombat.defeatedArmyIDs.Add(loserID);
                        stackCombat.RemoveFront(loserID);

                        // Cross-tile entrenched losers get forced retreat instead of destruction
                        if (stackCombat.crossTileDefenderIDs.Contains(loserID))
                        {
                            ArmyData loserArmy = state.GetArmy(loserID);
                            if (loserArmy != null && !loserArmy.IsEmpty())
                            {
                                loserArmy.ClearEntrenchment();
                                HexCoordinate fromCoord = loserArmy.coordinate;
                                List<HexCoordinate> retreatPath = InitiateAutoRetreat(loserID, state);
                                if (retreatPath != null)
                                {
                                    changes.Add(new ArmyAutoRetreatingChange { armyID = loserID, path = retreatPath });
                                }
                                HexCoordinate toCoord = loserArmy.currentPath != null && loserArmy.currentPath.Count > 0
                                    ? loserArmy.currentPath[loserArmy.currentPath.Count - 1]
                                    : loserArmy.coordinate;
                                changes.Add(new ArmyForcedRetreatChange { armyID = loserID, from = fromCoord, to = toCoord });
                                DebugLog.Log(string.Format("Stack: Cross-tile defender {0} forced to retreat (not destroyed)", loserArmy.name));
                            }
                        }
                        else
                        {
                            // Same-tile losers retreat or are destroyed
                            List<HexCoordinate> retreatPath = InitiateAutoRetreat(loserID, state);
                            if (retreatPath != null)
                            {
                                changes.Add(new ArmyAutoRetreatingChange { armyID = loserID, path = retreatPath });
                            }
                            // If army is empty and couldn't retreat, destroy it
                            ArmyData loserArmy = state.GetArmy(loserID);
                            if (loserArmy != null && loserArmy.IsEmpty() && !loserArmy.isRetreating)
                            {
                                changes.Add(new ArmyDestroyedChange { armyID = loserID, coordinate = loserArmy.coordinate });
                                state.RemoveArmy(loserID);
                            }
                        }
                    }

                    changes.Add(new StackCombatPairingEndedChange
                    {
                        coordinate = stackCombat.coordinate,
                        winnerArmyID = result.winnerID,
                        loserArmyID = result.loserID
                    });

                    // Save records
                    CombatRecord combatRecord = CreateCombatRecord(combat, state);
                    AddCombatRecord(combatRecord);
                    DetailedCombatRecord detailedRecord = CreateDetailedCombatRecord(combat, state);
                    AddDetailedCombatRecord(detailedRecord);

                    // Clean up combat flags and remove from activeCombats
                    CleanupCombatFlags(combat, state);
                    activeCombats.Remove(pairing.activeCombatID);

                    DebugLog.Log(string.Format("Stack pairing ended: winner={0} loser={1} defenderQueue={2} attackerQueue={3}",
                        result.winnerID.HasValue ? result.winnerID.Value.ToString().Substring(0, 8) : "nil",
                        result.loserID.HasValue ? result.loserID.Value.ToString().Substring(0, 8) : "nil",
                        stackCombat.defenderQueue.Count,
                        stackCombat.attackerQueue.Count));

                    // Chain: Winner engages next fresh enemy or reinforces an ally
                    if (result.winnerID.HasValue)
                    {
                        Guid winnerID = result.winnerID.Value;
                        stackCombat.RemoveFront(winnerID);

                        bool winnerIsAttacker = pairing.attackerArmyID == winnerID;
                        ArmyData winnerArmy = state.GetArmy(winnerID);
                        DebugLog.Log(string.Format("Stack chain: winnerIsAttacker={0} winnerUnits={1} winnerExists={2}",
                            winnerIsAttacker,
                            winnerArmy != null ? winnerArmy.GetTotalUnits() : -1,
                            winnerArmy != null));

                        if (winnerIsAttacker)
                        {
                            // Attacker won -- engage next defender
                            DefensiveStackEntry? nextDefender = stackCombat.DequeueNextDefender();
                            if (nextDefender.HasValue)
                            {
                                DebugLog.Log(string.Format("Stack chain: dequeued next defender {0} exists={1}",
                                    nextDefender.Value.armyID.ToString().Substring(0, 8),
                                    state.GetArmy(nextDefender.Value.armyID) != null));
                                StateChange combatChange = CreateStackPairing(stackCombat, winnerID, nextDefender.Value, currentTime, state);
                                if (combatChange != null)
                                {
                                    changes.Add(combatChange);
                                    DebugLog.Log(string.Format("Stack chain: {0} engages next defender - NEW pairing created",
                                        state.GetArmy(winnerID)?.name ?? "Winner"));
                                }
                                else
                                {
                                    DebugLog.Log("Stack chain: CreateStackPairing returned null!");
                                }
                            }
                            else
                            {
                                CombatPairing allyPairing = stackCombat.activePairings.FirstOrDefault(p => !p.isComplete);
                                if (allyPairing != null)
                                {
                                    ActiveCombat allyCombat;
                                    if (activeCombats.TryGetValue(allyPairing.activeCombatID, out allyCombat) && winnerArmy != null)
                                    {
                                        // No more defenders in queue -- reinforce an ally's fight
                                        allyCombat.AddReinforcement(winnerArmy, true);
                                        winnerArmy.isInCombat = true;
                                        DebugLog.Log(string.Format("Stack chain: {0} reinforcing ally", winnerArmy.name));
                                    }
                                }
                                else
                                {
                                    DebugLog.Log("Stack chain: No next defender and no ally to reinforce");
                                }
                            }
                        }
                        else
                        {
                            // Defender won -- engage next attacker
                            Guid? nextAttacker = stackCombat.DequeueNextAttacker();
                            if (nextAttacker.HasValue)
                            {
                                ArmyData defWinnerArmy = state.GetArmy(winnerID);
                                var defenderEntry = new DefensiveStackEntry(
                                    winnerID,
                                    DefensiveTier.Regular,
                                    stackCombat.crossTileDefenderIDs.Contains(winnerID),
                                    defWinnerArmy != null ? defWinnerArmy.coordinate : stackCombat.coordinate,
                                    defWinnerArmy != null && defWinnerArmy.isEntrenched ? GameConfig.Entrenchment.DefenseBonus : 0
                                );
                                StateChange combatChange = CreateStackPairing(stackCombat, nextAttacker.Value, defenderEntry, currentTime, state);
                                if (combatChange != null)
                                {
                                    changes.Add(combatChange);
                                    DebugLog.Log("Stack chain: Defender engages next attacker");
                                }
                            }
                        }
                    }
                }

                // Check for tier advancement
                int activePairingsRemaining = stackCombat.activePairings.Count(p => !p.isComplete);
                DebugLog.Log(string.Format("Stack status: totalPairings={0} active={1} defenderQueue={2} attackerQueue={3}",
                    stackCombat.activePairings.Count, activePairingsRemaining,
                    stackCombat.defenderQueue.Count, stackCombat.attackerQueue.Count));

                if (activePairingsRemaining == 0 && stackCombat.defenderQueue.Count == 0)
                {
                    if (!stackCombat.villagerPhaseActive && stackCombat.villagerGroupIDs.Count > 0)
                    {
                        // All army defenders defeated -- advance to villager phase
                        stackCombat.villagerPhaseActive = true;
                        stackCombat.currentTier = DefensiveTier.Villager;
                        changes.Add(new StackCombatTierAdvancedChange
                        {
                            coordinate = stackCombat.coordinate,
                            newTier = (int)DefensiveTier.Villager
                        });

                        // Start villager combats for remaining attackers
                        var survivingAttackers = stackCombat.activePairings
                            .Where(p => p.winnerArmyID.HasValue)
                            .Select(p => p.winnerArmyID.Value)
                            .Where(id => !stackCombat.defeatedArmyIDs.Contains(id) && !stackCombat.retreatedArmyIDs.Contains(id))
                            .ToList();

                        foreach (Guid villagerGroupID in stackCombat.villagerGroupIDs)
                        {
                            if (survivingAttackers.Count > 0)
                            {
                                Guid attackerID = survivingAttackers[0];
                                StateChange villagerChange = StartVillagerCombat(attackerID, villagerGroupID, currentTime);
                                if (villagerChange != null)
                                {
                                    changes.Add(villagerChange);
                                }
                            }
                        }

                        DebugLog.Log("Stack: All army defenders defeated, advancing to villager phase");
                    }
                    else
                    {
                        // Stack combat is complete
                        stackCombat.isComplete = true;
                        completedStacks.Add(stackID);

                        // Auto-attack building at location if attackers won
                        var survivingAttackers = stackCombat.activePairings
                            .Where(p => p.winnerArmyID.HasValue)
                            .Select(p => p.winnerArmyID.Value)
                            .Where(id => !stackCombat.defeatedArmyIDs.Contains(id) && !stackCombat.retreatedArmyIDs.Contains(id))
                            .ToList();

                        if (survivingAttackers.Count > 0)
                        {
                            AutoStartBuildingCombat(survivingAttackers[0], stackCombat.coordinate, state, currentTime, changes);
                        }

                        var overallResult = new CombatResultData(
                            winnerID: survivingAttackers.Count > 0 ? (Guid?)survivingAttackers[0] : null,
                            loserID: null,
                            combatDuration: currentTime - stackCombat.startTime
                        );
                        changes.Add(new StackCombatEndedChange { coordinate = stackCombat.coordinate, result = overallResult });
                        DebugLog.Log(string.Format("Stack combat completed at {0}", stackCombat.coordinate));
                    }
                }
            }

            // Remove completed stacks
            foreach (Guid id in completedStacks)
            {
                stackCombats.Remove(id);
            }

            return changes;
        }

        // MARK: - Stack Combat Individual Retreat

        /// <summary>
        /// Handles an army retreating from a stack combat.
        /// </summary>
        public void HandleIndividualRetreat(Guid armyID)
        {
            foreach (var stackKvp in stackCombats)
            {
                StackCombat stackCombat = stackKvp.Value;
                if (!stackCombat.InvolvesArmy(armyID)) continue;

                // Remove from active pairing if in one
                for (int index = 0; index < stackCombat.activePairings.Count; index++)
                {
                    CombatPairing pairing = stackCombat.activePairings[index];
                    if (pairing.isComplete) continue;

                    if (pairing.attackerArmyID == armyID || pairing.defenderArmyID == armyID)
                    {
                        // End this pairing's underlying combat
                        ActiveCombat combat;
                        if (activeCombats.TryGetValue(pairing.activeCombatID, out combat))
                        {
                            combat.phase = CombatPhase.Ended;
                        }
                        activeCombats.Remove(pairing.activeCombatID);
                        stackCombat.activePairings[index].isComplete = true;

                        // Determine who the opponent was
                        Guid opponentID = pairing.attackerArmyID == armyID ? pairing.defenderArmyID : pairing.attackerArmyID;
                        stackCombat.activePairings[index].winnerArmyID = opponentID;
                        stackCombat.activePairings[index].loserArmyID = armyID;

                        stackCombat.RemoveFront(opponentID);

                        // Opponent can now engage next enemy
                        if (pairing.attackerArmyID == armyID)
                        {
                            // Attacker retreated, defender won -- engage next attacker
                            Guid? nextAttacker = stackCombat.DequeueNextAttacker();
                            if (nextAttacker.HasValue && gameState != null)
                            {
                                ArmyData opponentArmy = gameState.GetArmy(opponentID);
                                var defenderEntry = new DefensiveStackEntry(
                                    opponentID,
                                    DefensiveTier.Regular,
                                    stackCombat.crossTileDefenderIDs.Contains(opponentID),
                                    opponentArmy != null ? opponentArmy.coordinate : stackCombat.coordinate,
                                    opponentArmy != null && opponentArmy.isEntrenched ? GameConfig.Entrenchment.DefenseBonus : 0
                                );
                                double combatCurrentTime = gameState.currentTime;
                                CreateStackPairing(stackCombat, nextAttacker.Value, defenderEntry, combatCurrentTime, gameState);
                            }
                        }
                        else
                        {
                            // Defender retreated, attacker won -- engage next defender
                            DefensiveStackEntry? nextDefender = stackCombat.DequeueNextDefender();
                            if (nextDefender.HasValue && gameState != null)
                            {
                                double combatCurrentTime = gameState.currentTime;
                                CreateStackPairing(stackCombat, opponentID, nextDefender.Value, combatCurrentTime, gameState);
                            }
                        }

                        break;
                    }
                }

                // Remove from queues
                stackCombat.RemoveArmy(armyID);
                break;
            }
        }

        /// <summary>
        /// Get the stack combat involving a specific army.
        /// </summary>
        public StackCombat GetStackCombat(Guid armyID)
        {
            foreach (StackCombat sc in stackCombats.Values)
            {
                if (sc.InvolvesArmy(armyID)) return sc;
            }
            return null;
        }

        /// <summary>
        /// Get an active (non-complete) stack combat at a coordinate.
        /// </summary>
        public StackCombat GetStackCombatAt(HexCoordinate coordinate)
        {
            foreach (StackCombat sc in stackCombats.Values)
            {
                if (sc.coordinate.Equals(coordinate) && !sc.isComplete) return sc;
            }
            return null;
        }

        // MARK: - Defender Reinforcement

        /// <summary>
        /// Adds a newly arrived army as a defender reinforcement to an active stack combat.
        /// Returns state changes for any new pairings created.
        /// </summary>
        public List<StateChange> AddDefenderToStackCombat(Guid armyID, HexCoordinate coordinate, double currentTime)
        {
            if (gameState == null) return new List<StateChange>();
            ArmyData army = gameState.GetArmy(armyID);
            if (army == null || !army.ownerID.HasValue) return new List<StateChange>();
            Guid armyOwnerID = army.ownerID.Value;

            StackCombat stackCombat = GetStackCombatAt(coordinate);
            if (stackCombat == null) return new List<StateChange>();

            // Only join if this army is enemy of the attackers (i.e., friendly to defenders)
            if (armyOwnerID == stackCombat.attackerOwnerID) return new List<StateChange>();

            // Don't join if already involved
            if (stackCombat.InvolvesArmy(armyID)) return new List<StateChange>();

            // Don't join if already in combat
            if (army.isInCombat) return new List<StateChange>();

            var changes = new List<StateChange>();

            var defenderEntry = new DefensiveStackEntry(
                armyID,
                DefensiveTier.Regular,
                false,
                coordinate,
                0
            );

            // Priority 1: Match with a queued attacker
            Guid? nextAttacker = stackCombat.DequeueNextAttacker();
            if (nextAttacker.HasValue)
            {
                StateChange combatChange = CreateStackPairing(stackCombat, nextAttacker.Value, defenderEntry, currentTime, gameState);
                if (combatChange != null)
                {
                    changes.Add(combatChange);
                    DebugLog.Log(string.Format("Stack reinforcement: {0} paired with queued attacker", army.name));
                }
            }
            // Priority 2: Reinforce an outnumbered active fight
            else
            {
                CombatPairing outnumberedPairing = null;
                ActiveCombat outnumberedCombat = null;
                foreach (CombatPairing p in stackCombat.activePairings)
                {
                    if (p.isComplete) continue;
                    ActiveCombat c;
                    if (!activeCombats.TryGetValue(p.activeCombatID, out c)) continue;
                    if (c.attackerArmies.Count > c.defenderArmies.Count)
                    {
                        outnumberedPairing = p;
                        outnumberedCombat = c;
                        break;
                    }
                }

                if (outnumberedPairing != null && outnumberedCombat != null)
                {
                    outnumberedCombat.AddReinforcement(army, false);
                    army.isInCombat = true;
                    army.combatTargetID = outnumberedPairing.attackerArmyID;
                    stackCombat.AddFront(armyID);
                    DebugLog.Log(string.Format("Stack reinforcement: {0} reinforcing outnumbered defender", army.name));
                }
                // Priority 3: Queue for later chain combat pickup
                else
                {
                    stackCombat.AddDefender(defenderEntry);
                    army.isInCombat = true;
                    DebugLog.Log(string.Format("Stack reinforcement: {0} queued as reserve defender", army.name));
                }
            }

            return changes;
        }

        // MARK: - Query Methods

        public bool IsInCombat(Guid armyID)
        {
            // Check army combats
            foreach (ActiveCombat combat in activeCombats.Values)
            {
                if (combat.attackerArmies.Any(a => a.armyID == armyID) ||
                    combat.defenderArmies.Any(a => a.armyID == armyID))
                {
                    return true;
                }
            }

            // Check building combats
            if (buildingCombats.Values.Any(bc => bc.attackerArmyID == armyID))
            {
                return true;
            }

            // Check villager combats
            if (villagerCombats.Values.Any(vc => vc.attackerArmyID == armyID))
            {
                return true;
            }

            // Check stack combats
            if (stackCombats.Values.Any(sc => sc.InvolvesArmy(armyID)))
            {
                return true;
            }

            return false;
        }

        public ActiveCombatData GetCombat(Guid armyID)
        {
            // Check army combats and convert to ActiveCombatData for compatibility
            foreach (ActiveCombat combat in activeCombats.Values)
            {
                bool isAttacker = combat.attackerArmies.Any(a => a.armyID == armyID);
                bool isDefender = combat.defenderArmies.Any(a => a.armyID == armyID);

                if (isAttacker || isDefender)
                {
                    return new ActiveCombatData(
                        id: combat.id,
                        attackerArmyID: combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyID : Guid.Empty,
                        defenderArmyID: combat.defenderArmies.Count > 0 ? (Guid?)combat.defenderArmies[0].armyID : null,
                        defenderBuildingID: null,
                        coordinate: combat.location,
                        startTime: combat.gameStartTime,
                        lastPhaseTime: combat.gameStartTime + combat.elapsedTime,
                        currentPhase: PhaseToInt(combat.phase),
                        isComplete: combat.phase == CombatPhase.Ended,
                        result: null
                    );
                }
            }

            // Check building combats
            foreach (ActiveCombatData bc in buildingCombats.Values)
            {
                if (bc.attackerArmyID == armyID)
                    return bc;
            }

            return null;
        }

        /// <summary>
        /// Get active combat data for UI display.
        /// </summary>
        public List<ActiveCombatData> GetActiveCombatData()
        {
            var result = new List<ActiveCombatData>();

            // Convert ActiveCombat to ActiveCombatData
            foreach (ActiveCombat combat in activeCombats.Values)
            {
                result.Add(new ActiveCombatData(
                    id: combat.id,
                    attackerArmyID: combat.attackerArmies.Count > 0 ? combat.attackerArmies[0].armyID : Guid.Empty,
                    defenderArmyID: combat.defenderArmies.Count > 0 ? (Guid?)combat.defenderArmies[0].armyID : null,
                    defenderBuildingID: null,
                    coordinate: combat.location,
                    startTime: combat.gameStartTime,
                    lastPhaseTime: combat.gameStartTime + combat.elapsedTime,
                    currentPhase: PhaseToInt(combat.phase),
                    isComplete: combat.phase == CombatPhase.Ended,
                    result: null
                ));
            }

            // Add building combats
            result.AddRange(buildingCombats.Values);

            return result;
        }

        /// <summary>
        /// Get an ActiveCombat by its ID.
        /// </summary>
        public ActiveCombat GetActiveCombatByID(Guid id)
        {
            ActiveCombat combat;
            activeCombats.TryGetValue(id, out combat);
            return combat;
        }

        /// <summary>
        /// Get an ActiveCombat involving a specific army ID.
        /// </summary>
        public ActiveCombat GetActiveCombatInvolvingArmy(Guid armyID)
        {
            foreach (ActiveCombat combat in activeCombats.Values)
            {
                if (combat.attackerArmies.Any(a => a.armyID == armyID) ||
                    combat.defenderArmies.Any(a => a.armyID == armyID))
                {
                    return combat;
                }
            }
            return null;
        }

        // MARK: - Combat History

        public void AddCombatRecord(CombatRecord record)
        {
            combatHistory.Insert(0, record); // Most recent first
        }

        public void AddDetailedCombatRecord(DetailedCombatRecord record)
        {
            detailedCombatHistory.Insert(0, record); // Most recent first
        }

        public List<CombatRecord> GetCombatHistory()
        {
            return combatHistory;
        }

        public List<DetailedCombatRecord> GetDetailedCombatHistory()
        {
            return detailedCombatHistory;
        }

        /// <summary>
        /// Get detailed record by matching basic record ID (by timestamp proximity).
        /// </summary>
        public DetailedCombatRecord GetDetailedRecord(CombatRecord basicRecord)
        {
            foreach (DetailedCombatRecord detailedRecord in detailedCombatHistory)
            {
                if (Math.Abs(detailedRecord.Timestamp - basicRecord.Timestamp) < 1.0 &&
                    detailedRecord.Location.Equals(basicRecord.Location))
                {
                    return detailedRecord;
                }
            }
            return null;
        }

        public void ClearCombatHistory()
        {
            combatHistory.Clear();
            detailedCombatHistory.Clear();
            activeCombats.Clear();
            buildingCombats.Clear();
            villagerCombats.Clear();
            stackCombats.Clear();
            garrisonDefenseEngine.Reset();
            DebugLog.Log("Combat history cleared");
        }

        // MARK: - Retreat

        /// <summary>
        /// Initiates auto-retreat for a losing army after combat ends.
        /// Returns the retreat path if retreat was initiated, null otherwise.
        /// </summary>
        private List<HexCoordinate> InitiateAutoRetreat(Guid armyID, GameState state)
        {
            ArmyData army = state.GetArmy(armyID);
            if (army == null || !army.ownerID.HasValue)
            {
                DebugLog.Log(string.Format("DEBUG: InitiateAutoRetreat - Army {0} not found in GameState or has no owner", armyID));
                return null; // Army doesn't exist - nothing to retreat
            }
            Guid ownerID = army.ownerID.Value;
            DebugLog.Log(string.Format("DEBUG: InitiateAutoRetreat - Processing army {0} at {1}, homeBaseID: {2}",
                army.name, army.coordinate, army.homeBaseID.HasValue ? army.homeBaseID.Value.ToString() : "null"));

            // Clear entrenchment when retreating from combat loss
            if (army.isEntrenching || army.isEntrenched)
            {
                army.ClearEntrenchment();
                DebugLog.Log(string.Format("Army {0} entrenchment cancelled due to combat loss", army.name));
            }

            // Check if army is currently at their home base (lost a fight defending it)
            // If so, they stay to defend the building - only retreat when building is destroyed
            if (army.homeBaseID.HasValue)
            {
                BuildingData homeBase = state.GetBuilding(army.homeBaseID.Value);
                if (homeBase != null && homeBase.OccupiedCoordinates.Contains(army.coordinate))
                {
                    DebugLog.Log(string.Format("{0} staying to defend {1}", army.name, homeBase.buildingType.DisplayName()));
                    return null;
                }
            }

            // Try to find a retreat destination
            HexCoordinate? retreatDestination = null;
            BuildingData retreatBuilding = null;

            // 1. Try existing home base
            if (army.homeBaseID.HasValue)
            {
                BuildingData homeBase = state.GetBuilding(army.homeBaseID.Value);
                if (homeBase != null && homeBase.IsOperational && !army.coordinate.Equals(homeBase.coordinate))
                {
                    retreatBuilding = homeBase;
                    retreatDestination = homeBase.coordinate;
                }
            }

            // 2. Fallback: Find nearest valid home base with capacity
            if (!retreatDestination.HasValue)
            {
                BuildingData nearestBase = state.FindHomeBaseWithCapacity(ownerID, army.coordinate);
                if (nearestBase != null && !army.coordinate.Equals(nearestBase.coordinate))
                {
                    retreatBuilding = nearestBase;
                    retreatDestination = nearestBase.coordinate;
                    // Update army's home base reference
                    army.homeBaseID = nearestBase.id;
                    DebugLog.Log(string.Format("{0} home base reassigned to {1}",
                        army.name, nearestBase.buildingType.DisplayName()));
                }
            }

            // 3. Calculate path to destination
            if (!retreatDestination.HasValue)
            {
                DebugLog.Log(string.Format("{0} cannot find retreat path - staying in place", army.name));
                return null;
            }

            List<HexCoordinate> path = state.mapData.FindPath(army.coordinate, retreatDestination.Value, ownerID, state);
            if (path == null || path.Count == 0)
            {
                DebugLog.Log(string.Format("{0} cannot find retreat path - staying in place", army.name));
                return null;
            }

            // Set retreat state and path
            army.isRetreating = true;
            army.currentPath = path;
            army.pathIndex = 0;
            army.movementProgress = 0.0;

            string buildingName = retreatBuilding != null ? retreatBuilding.buildingType.DisplayName() : "unknown";
            DebugLog.Log(string.Format("{0} retreating to {1}", army.name, buildingName));

            return path;
        }

        public void RetreatFromCombat(Guid armyID)
        {
            // Check if army is in a stack combat first
            if (GetStackCombat(armyID) != null)
            {
                HandleIndividualRetreat(armyID);
            }

            // Find and remove the combat involving this army
            Guid? combatIDToRemove = null;
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.attackerArmies.Any(a => a.armyID == armyID) ||
                    kvp.Value.defenderArmies.Any(a => a.armyID == armyID))
                {
                    combatIDToRemove = kvp.Key;
                    break;
                }
            }
            if (combatIDToRemove.HasValue)
            {
                activeCombats.Remove(combatIDToRemove.Value);
            }

            // Check building combats too
            Guid? buildingCombatID = null;
            foreach (var kvp in buildingCombats)
            {
                if (kvp.Value.attackerArmyID == armyID)
                {
                    buildingCombatID = kvp.Key;
                    break;
                }
            }
            if (buildingCombatID.HasValue)
            {
                buildingCombats.Remove(buildingCombatID.Value);
            }

            // Check villager combats too
            Guid? villagerCombatID = null;
            foreach (var kvp in villagerCombats)
            {
                if (kvp.Value.attackerArmyID == armyID)
                {
                    villagerCombatID = kvp.Key;
                    break;
                }
            }
            if (villagerCombatID.HasValue)
            {
                villagerCombats.Remove(villagerCombatID.Value);
            }

            // Mark army as retreating
            if (gameState != null)
            {
                ArmyData army = gameState.GetArmy(armyID);
                if (army != null)
                {
                    army.isRetreating = true;
                    army.isInCombat = false;
                    army.combatTargetID = null;
                }
            }
        }
    }
}
