using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Models;
using Sporefront.Data;
using Sporefront.Engine;

namespace Sporefront.Models.Combat
{
    // MARK: - Combat Phase

    /// Represents the current phase of a phased combat
    public enum CombatPhase
    {
        RangedExchange,   // 0-3s: Ranged/siege fire, melee units closing
        MeleeEngagement,  // 3s+: Full melee combat, ranged continues
        Cleanup,          // One side's melee gone, mop up remaining
        Ended             // Combat concluded
    }

    public static class CombatPhaseExtensions
    {
        public static string DisplayName(this CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.RangedExchange: return "Ranged Exchange";
                case CombatPhase.MeleeEngagement: return "Melee Engagement";
                case CombatPhase.Cleanup: return "Cleanup";
                case CombatPhase.Ended: return "Ended";
                default: return phase.ToString();
            }
        }

        public static int ToPhaseIndex(this CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.RangedExchange: return 0;
                case CombatPhase.MeleeEngagement: return 1;
                case CombatPhase.Cleanup: return 2;
                case CombatPhase.Ended: return 3;
                default: return 0;
            }
        }
    }

    // MARK: - Cavalry Stance

    /// Cavalry positioning in phased combat
    public enum CavalryStance
    {
        Frontline, // Fight alongside infantry
        Flank,     // +25% damage to ranged units
        Reserve    // Wait until cleanup phase
    }

    public static class CavalryStanceExtensions
    {
        public static string DisplayName(this CavalryStance stance)
        {
            switch (stance)
            {
                case CavalryStance.Frontline: return "Frontline";
                case CavalryStance.Flank: return "Flanking";
                case CavalryStance.Reserve: return "Reserve";
                default: return stance.ToString();
            }
        }

        /// Damage multiplier when attacking ranged units while flanking
        public static double FlankBonusVsRanged(this CavalryStance stance)
        {
            return stance == CavalryStance.Flank ? 1.25 : 1.0;
        }
    }

    // MARK: - Terrain Combat Modifier

    /// Holds terrain-based combat modifiers for a specific combat
    public struct TerrainCombatModifier
    {
        public TerrainType terrain;
        public double defenderDefenseBonus;
        public double attackerAttackPenalty;

        public TerrainCombatModifier(TerrainType terrain, double defenderDefenseBonus, double attackerAttackPenalty)
        {
            this.terrain = terrain;
            this.defenderDefenseBonus = defenderDefenseBonus;
            this.attackerAttackPenalty = attackerAttackPenalty;
        }
    }

    // MARK: - Side Combat State

    /// Tracks the combat state for one side of a phased combat
    [System.Serializable]
    public class SideCombatState
    {
        /// Unit counts by type
        public Dictionary<MilitaryUnitType, int> unitCounts;

        /// Accumulated damage per unit type (kills happen when >= unit's HP)
        public Dictionary<MilitaryUnitType, double> damageAccumulators;

        /// Initial unit count at combat start (for calculating casualties)
        public int initialUnitCount;

        /// Cavalry stance for this side
        public CavalryStance cavalryStance = CavalryStance.Frontline;

        // MARK: - Tracking for Detailed Combat Records

        /// Initial composition snapshot at combat start
        public Dictionary<MilitaryUnitType, int> initialComposition;

        /// Total damage dealt by each unit type throughout combat
        public Dictionary<MilitaryUnitType, double> damageDealtByType;

        /// Total damage received by each unit type throughout combat
        public Dictionary<MilitaryUnitType, double> damageReceivedByType;

        /// Initialize from ArmyData (engine use only, no visual layer reference)
        public SideCombatState(ArmyData armyData)
        {
            this.unitCounts = new Dictionary<MilitaryUnitType, int>(armyData.militaryComposition);
            this.damageAccumulators = new Dictionary<MilitaryUnitType, double>();
            this.initialUnitCount = armyData.GetTotalUnits();

            // Snapshot initial composition for battle reports
            this.initialComposition = new Dictionary<MilitaryUnitType, int>(armyData.militaryComposition);

            // Initialize tracking dictionaries for all unit types present
            this.damageDealtByType = new Dictionary<MilitaryUnitType, double>();
            this.damageReceivedByType = new Dictionary<MilitaryUnitType, double>();
            foreach (var unitType in unitCounts.Keys)
            {
                damageAccumulators[unitType] = 0.0;
                damageDealtByType[unitType] = 0.0;
                damageReceivedByType[unitType] = 0.0;
            }
        }

        // MARK: - Computed Properties

        /// Total units remaining
        public int TotalUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts) total += kvp.Value;
                return total;
            }
        }

        /// Total HP of all current units (unit count * unit HP per type)
        public double CurrentTotalHP
        {
            get
            {
                double total = 0.0;
                foreach (var kvp in unitCounts)
                    total += kvp.Value * kvp.Key.HP();
                return total;
            }
        }

        /// Total HP of all initial units at combat start
        public double InitialTotalHP
        {
            get
            {
                double total = 0.0;
                foreach (var kvp in initialComposition)
                    total += kvp.Value * kvp.Key.HP();
                return total;
            }
        }

        /// Total melee units (infantry + cavalry)
        public int MeleeUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts)
                {
                    var cat = kvp.Key.Category();
                    if (cat == UnitCategory.Infantry || cat == UnitCategory.Cavalry)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Total ranged units
        public int RangedUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts)
                {
                    if (kvp.Key.Category() == UnitCategory.Ranged)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Total siege units
        public int SiegeUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts)
                {
                    if (kvp.Key.Category() == UnitCategory.Siege)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Total infantry units
        public int InfantryUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts)
                {
                    if (kvp.Key.Category() == UnitCategory.Infantry)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Total cavalry units
        public int CavalryUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in unitCounts)
                {
                    if (kvp.Key.Category() == UnitCategory.Cavalry)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Units by category
        public int Units(UnitCategory category)
        {
            int total = 0;
            foreach (var kvp in unitCounts)
            {
                if (kvp.Key.Category() == category)
                    total += kvp.Value;
            }
            return total;
        }

        /// Check if side has any units of a category
        public bool HasUnits(UnitCategory category)
        {
            return Units(category) > 0;
        }

        // MARK: - Damage Application

        /// Apply damage to a specific unit type, returns number of units killed
        public int ApplyDamage(double damage, MilitaryUnitType unitType, double? effectiveHP = null)
        {
            int currentCount;
            if (!unitCounts.TryGetValue(unitType, out currentCount) || currentCount <= 0)
                return 0;

            // Track damage received for battle reports
            if (!damageReceivedByType.ContainsKey(unitType))
                damageReceivedByType[unitType] = 0.0;
            damageReceivedByType[unitType] += damage;

            double unitHP = effectiveHP ?? unitType.HP();
            double currentAccumulator;
            if (!damageAccumulators.TryGetValue(unitType, out currentAccumulator))
                currentAccumulator = 0.0;
            double newAccumulator = currentAccumulator + damage;

            // Calculate kills (damage >= unit HP kills one unit)
            int kills = (int)(newAccumulator / unitHP);
            int actualKills = Math.Min(kills, currentCount);

            // Update counts and accumulator
            unitCounts[unitType] = currentCount - actualKills;
            damageAccumulators[unitType] = newAccumulator % unitHP;

            // Clean up if no units left
            if (unitCounts[unitType] == 0)
            {
                unitCounts.Remove(unitType);
                damageAccumulators.Remove(unitType);
            }

            return actualKills;
        }

        /// Track damage dealt by a specific unit type (for battle reports)
        public void TrackDamageDealt(double damage, MilitaryUnitType unitType)
        {
            if (!damageDealtByType.ContainsKey(unitType))
                damageDealtByType[unitType] = 0.0;
            damageDealtByType[unitType] += damage;
        }

        /// Get units of a specific type
        public int GetUnits(MilitaryUnitType type)
        {
            int count;
            return unitCounts.TryGetValue(type, out count) ? count : 0;
        }
    }

    // MARK: - Army Combat State

    /// Tracks per-army combat statistics within a multi-army battle
    [System.Serializable]
    public class ArmyCombatState
    {
        public Guid armyID;
        public string armyName;
        public string ownerName;
        public string commanderName;
        public string commanderSpecialty; // CommanderSpecialty raw value
        public double joinTime;
        public double? chargePhaseEndTime;
        public double? rangedPhaseEndTime;
        public Dictionary<MilitaryUnitType, int> initialComposition;
        public Dictionary<MilitaryUnitType, int> currentUnits;
        public Dictionary<MilitaryUnitType, double> damageDealtByType;
        public Dictionary<MilitaryUnitType, int> casualtiesByType;

        /// Initialize from ArmyData (engine use only, no visual layer reference)
        public ArmyCombatState(ArmyData armyData, double joinTime, bool isReinforcement, CommanderData commanderData = null)
        {
            this.armyID = armyData.id;
            this.armyName = armyData.name;
            this.ownerName = "Unknown"; // ArmyData doesn't hold owner reference
            this.commanderName = commanderData != null ? commanderData.name : null;
            this.commanderSpecialty = commanderData != null ? commanderData.specialty.ToString() : null;
            this.joinTime = joinTime;
            this.initialComposition = new Dictionary<MilitaryUnitType, int>(armyData.militaryComposition);
            this.currentUnits = new Dictionary<MilitaryUnitType, int>(armyData.militaryComposition);

            // Initialize tracking dictionaries
            this.damageDealtByType = new Dictionary<MilitaryUnitType, double>();
            this.casualtiesByType = new Dictionary<MilitaryUnitType, int>();
            foreach (var unitType in armyData.militaryComposition.Keys)
            {
                damageDealtByType[unitType] = 0.0;
                casualtiesByType[unitType] = 0;
            }

            // Reinforcements get special bonus windows
            if (isReinforcement)
            {
                this.chargePhaseEndTime = joinTime + 3.0;
                this.rangedPhaseEndTime = joinTime + 3.0;
            }
            else
            {
                this.chargePhaseEndTime = null;
                this.rangedPhaseEndTime = null;
            }
        }

        // MARK: - Computed Properties

        /// Total units remaining in this army
        public int TotalUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in currentUnits) total += kvp.Value;
                return total;
            }
        }

        /// Check if this army still has units
        public bool IsActive => TotalUnits > 0;

        /// Get ranged units in this army
        public int RangedUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in currentUnits)
                {
                    if (kvp.Key.Category() == UnitCategory.Ranged)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Get melee units in this army
        public int MeleeUnits
        {
            get
            {
                int total = 0;
                foreach (var kvp in currentUnits)
                {
                    var cat = kvp.Key.Category();
                    if (cat == UnitCategory.Infantry || cat == UnitCategory.Cavalry)
                        total += kvp.Value;
                }
                return total;
            }
        }

        /// Check if this army is currently in its reinforcement charge window
        public bool IsInChargeWindow(double combatTime)
        {
            if (!chargePhaseEndTime.HasValue) return false;
            return combatTime < chargePhaseEndTime.Value;
        }

        /// Check if this army is currently in its reinforcement ranged window
        public bool IsInRangedWindow(double combatTime)
        {
            if (!rangedPhaseEndTime.HasValue) return false;
            return combatTime < rangedPhaseEndTime.Value;
        }

        /// Get units of a specific type
        public int GetUnits(MilitaryUnitType type)
        {
            int count;
            return currentUnits.TryGetValue(type, out count) ? count : 0;
        }

        // MARK: - Mutation Methods

        /// Apply casualties to this army state
        public void ApplyCasualties(MilitaryUnitType unitType, int count)
        {
            if (count <= 0) return;
            int current;
            if (!currentUnits.TryGetValue(unitType, out current)) return;
            int actualCasualties = Math.Min(count, current);
            currentUnits[unitType] = Math.Max(0, current - actualCasualties);

            if (!casualtiesByType.ContainsKey(unitType))
                casualtiesByType[unitType] = 0;
            casualtiesByType[unitType] += actualCasualties;

            // Clean up empty entries
            if (currentUnits[unitType] == 0)
            {
                currentUnits.Remove(unitType);
            }
        }

        /// Track damage dealt by this army
        public void TrackDamageDealt(double damage, MilitaryUnitType unitType)
        {
            if (!damageDealtByType.ContainsKey(unitType))
                damageDealtByType[unitType] = 0.0;
            damageDealtByType[unitType] += damage;
        }
    }

    // MARK: - Active Combat

    /// Represents an ongoing phased combat between two armies
    [System.Serializable]
    public class ActiveCombat
    {
        public Guid id;
        public SideCombatState attackerState;
        public SideCombatState defenderState;
        public CombatPhase phase;
        public double elapsedTime;
        public HexCoordinate location;
        public DateTime startTime;

        /// Game time when combat started (for elapsed time calculation)
        public double gameStartTime;

        /// Terrain at combat location
        public TerrainType terrainType;
        public double terrainDefenseBonus;
        public double terrainAttackPenalty;
        public double entrenchmentDefenseBonus;

        /// Phase transition threshold (seconds)
        public const double MeleeEngagementThreshold = 3.0;

        /// Player states for research bonus application (not serialized)
        [System.NonSerialized] public PlayerState attackerPlayerState;
        [System.NonSerialized] public PlayerState defenderPlayerState;

        /// Commander tactics bonuses for terrain scaling (not serialized)
        [System.NonSerialized] public double attackerTacticsBonus;
        [System.NonSerialized] public double defenderTacticsBonus;

        /// Commander data for attack/defense bonus application (not serialized)
        [System.NonSerialized] public CommanderData attackerCommanderData;
        [System.NonSerialized] public CommanderData defenderCommanderData;

        // MARK: - Phase Tracking for Detailed Combat Records

        /// Records for completed phases
        public List<CombatPhaseRecord> phaseRecords;

        /// When the current phase started
        public double phaseStartTime;

        /// Damage dealt by attacker in current phase
        public double phaseAttackerDamage;

        /// Damage dealt by defender in current phase
        public double phaseDefenderDamage;

        /// Casualties suffered by attacker in current phase
        public Dictionary<MilitaryUnitType, int> phaseAttackerCasualties;

        /// Casualties suffered by defender in current phase
        public Dictionary<MilitaryUnitType, int> phaseDefenderCasualties;

        // MARK: - Multi-Army Support

        /// Per-army tracking for attacker side
        public List<ArmyCombatState> attackerArmies;

        /// Per-army tracking for defender side
        public List<ArmyCombatState> defenderArmies;

        /// Whether this combat is a pairing within a StackCombat (skip in processArmyCombats)
        public bool isStackPairing;

        /// Initialize from ArmyData pair (engine use only, no visual layer references)
        public ActiveCombat(ArmyData attackerData, ArmyData defenderData, HexCoordinate location,
            TerrainType terrainType = TerrainType.Plains, double gameStartTime = 0)
        {
            this.id = Guid.NewGuid();
            this.attackerState = new SideCombatState(attackerData);
            this.defenderState = new SideCombatState(defenderData);
            this.phase = CombatPhase.RangedExchange;
            this.elapsedTime = 0;
            this.location = location;
            this.startTime = DateTime.UtcNow;
            this.gameStartTime = gameStartTime;
            this.terrainType = terrainType;
            this.terrainDefenseBonus = terrainType.DefenderDefenseBonus();
            this.terrainAttackPenalty = terrainType.AttackerAttackPenalty();
            this.entrenchmentDefenseBonus = 0;
            this.phaseStartTime = 0;
            this.phaseAttackerDamage = 0;
            this.phaseDefenderDamage = 0;
            this.phaseAttackerCasualties = new Dictionary<MilitaryUnitType, int>();
            this.phaseDefenderCasualties = new Dictionary<MilitaryUnitType, int>();
            this.phaseRecords = new List<CombatPhaseRecord>();
            this.isStackPairing = false;

            // Initialize army tracking arrays (first armies are not reinforcements)
            this.attackerArmies = new List<ArmyCombatState>
            {
                new ArmyCombatState(attackerData, 0, false)
            };
            this.defenderArmies = new List<ArmyCombatState>
            {
                new ArmyCombatState(defenderData, 0, false)
            };
        }

        // MARK: - Terrain Modifiers

        /// Computed terrain modifier based on stored values
        public TerrainCombatModifier TerrainModifier
        {
            get
            {
                return new TerrainCombatModifier(
                    terrainType,
                    terrainDefenseBonus,
                    terrainAttackPenalty
                );
            }
        }

        // MARK: - Combat State

        /// Check if combat should end
        public bool ShouldEnd => attackerState.TotalUnits == 0 || defenderState.TotalUnits == 0;

        /// Determine the winner (Draw if ongoing or both eliminated)
        public CombatResult Winner
        {
            get
            {
                if (attackerState.TotalUnits > 0 && defenderState.TotalUnits == 0)
                    return CombatResult.AttackerVictory;
                if (defenderState.TotalUnits > 0 && attackerState.TotalUnits == 0)
                    return CombatResult.DefenderVictory;
                return CombatResult.Draw;
            }
        }

        /// Check and update phase based on current state
        public void UpdatePhase()
        {
            if (phase == CombatPhase.Ended) return;

            CombatPhase previousPhase = phase;

            switch (phase)
            {
                case CombatPhase.RangedExchange:
                    // Transition to melee after threshold time
                    if (elapsedTime >= MeleeEngagementThreshold)
                    {
                        RecordPhaseCompletion(previousPhase);
                        phase = CombatPhase.MeleeEngagement;
                    }
                    break;

                case CombatPhase.MeleeEngagement:
                    // Transition to cleanup when one side has no melee units
                    if (attackerState.MeleeUnits == 0 || defenderState.MeleeUnits == 0)
                    {
                        RecordPhaseCompletion(previousPhase);
                        phase = CombatPhase.Cleanup;
                    }
                    break;

                case CombatPhase.Cleanup:
                    // End when one side has no units
                    if (ShouldEnd)
                    {
                        RecordPhaseCompletion(previousPhase);
                        phase = CombatPhase.Ended;
                    }
                    break;

                case CombatPhase.Ended:
                    break;
            }
        }

        // MARK: - Phase Tracking Methods

        /// Records the current phase statistics and resets accumulators
        public void RecordPhaseCompletion(CombatPhase completedPhase)
        {
            double phaseDuration = elapsedTime - phaseStartTime;

            var record = new CombatPhaseRecord(
                completedPhase.ToPhaseIndex(),
                phaseDuration,
                phaseAttackerDamage,
                phaseDefenderDamage,
                phaseAttackerCasualties,
                phaseDefenderCasualties
            );

            phaseRecords.Add(record);

            // Reset phase accumulators
            phaseStartTime = elapsedTime;
            phaseAttackerDamage = 0;
            phaseDefenderDamage = 0;
            phaseAttackerCasualties = new Dictionary<MilitaryUnitType, int>();
            phaseDefenderCasualties = new Dictionary<MilitaryUnitType, int>();
        }

        /// Track damage dealt during this phase
        public void TrackPhaseDamage(bool byAttacker, double amount)
        {
            if (byAttacker)
                phaseAttackerDamage += amount;
            else
                phaseDefenderDamage += amount;
        }

        /// Track casualties during this phase
        public void TrackPhaseCasualty(bool isAttacker, MilitaryUnitType unitType, int count)
        {
            var casualties = isAttacker ? phaseAttackerCasualties : phaseDefenderCasualties;
            if (!casualties.ContainsKey(unitType))
                casualties[unitType] = 0;
            casualties[unitType] += count;
        }

        // MARK: - Reinforcement Methods

        /// Adds a reinforcement army to the combat using ArmyData (engine use only)
        public void AddReinforcement(ArmyData armyData, bool isAttacker)
        {
            var armyState = new ArmyCombatState(armyData, elapsedTime, true);

            if (isAttacker)
            {
                attackerArmies.Add(armyState);
                MergeIntoSideState(attackerState, armyData);
            }
            else
            {
                defenderArmies.Add(armyState);
                MergeIntoSideState(defenderState, armyData);
            }

            DebugLog.Log(string.Format("Reinforcements arrived: {0} joined {1} side at time {2:F1}s",
                armyData.name, isAttacker ? "attacker" : "defender", elapsedTime));
        }

        /// Merges an ArmyData's units into an aggregated side state
        private void MergeIntoSideState(SideCombatState state, ArmyData armyData)
        {
            foreach (var kvp in armyData.militaryComposition)
            {
                MilitaryUnitType unitType = kvp.Key;
                int count = kvp.Value;

                if (!state.unitCounts.ContainsKey(unitType))
                    state.unitCounts[unitType] = 0;
                state.unitCounts[unitType] += count;

                // Initialize accumulators for new unit types
                if (!state.damageAccumulators.ContainsKey(unitType))
                    state.damageAccumulators[unitType] = 0.0;

                if (!state.damageDealtByType.ContainsKey(unitType))
                    state.damageDealtByType[unitType] = 0.0;

                if (!state.damageReceivedByType.ContainsKey(unitType))
                    state.damageReceivedByType[unitType] = 0.0;

                // Update initial composition to track total units that participated
                if (!state.initialComposition.ContainsKey(unitType))
                    state.initialComposition[unitType] = 0;
                state.initialComposition[unitType] += count;
            }
        }

        /// Gets all active army states for a side
        public List<ArmyCombatState> GetActiveArmies(bool isAttacker)
        {
            var armies = isAttacker ? attackerArmies : defenderArmies;
            return armies.Where(a => a.IsActive).ToList();
        }

        /// Gets army state by army ID
        public ArmyCombatState GetArmyState(Guid armyID)
        {
            var state = attackerArmies.FirstOrDefault(a => a.armyID == armyID);
            if (state != null) return state;
            return defenderArmies.FirstOrDefault(a => a.armyID == armyID);
        }

        /// Checks if any reinforcement on a side has an active ranged window
        public bool HasReinforcementRangedWindow(bool isAttacker)
        {
            var armies = isAttacker ? attackerArmies : defenderArmies;
            // Skip first army (original, not a reinforcement)
            if (armies.Count <= 1) return false;
            return armies.Skip(1).Any(a => a.IsInRangedWindow(elapsedTime));
        }

        /// Checks if any reinforcement on a side has an active charge window
        public bool HasReinforcementChargeWindow(bool isAttacker)
        {
            var armies = isAttacker ? attackerArmies : defenderArmies;
            // Skip first army (original, not a reinforcement)
            if (armies.Count <= 1) return false;
            return armies.Skip(1).Any(a => a.IsInChargeWindow(elapsedTime));
        }
    }

    // MARK: - Target Priority

    /// Determines which enemy units to prioritize based on attacker category
    public static class TargetPriority
    {
        /// Get prioritized target categories for an attacker type
        public static List<UnitCategory> GetTargetPriority(UnitCategory attackerCategory, CavalryStance stance = CavalryStance.Frontline)
        {
            switch (attackerCategory)
            {
                case UnitCategory.Ranged:
                    // Ranged prioritize: Siege > Cavalry > Infantry > Ranged
                    return new List<UnitCategory> { UnitCategory.Siege, UnitCategory.Cavalry, UnitCategory.Infantry, UnitCategory.Ranged };

                case UnitCategory.Siege:
                    // Siege prioritize: Siege > Ranged > Infantry > Cavalry
                    return new List<UnitCategory> { UnitCategory.Siege, UnitCategory.Ranged, UnitCategory.Infantry, UnitCategory.Cavalry };

                case UnitCategory.Infantry:
                    // Infantry prioritize: Infantry > Cavalry > Ranged > Siege
                    return new List<UnitCategory> { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Ranged, UnitCategory.Siege };

                case UnitCategory.Cavalry:
                    if (stance == CavalryStance.Flank)
                    {
                        // Flanking cavalry prioritize ranged: Ranged > Siege > Infantry > Cavalry
                        return new List<UnitCategory> { UnitCategory.Ranged, UnitCategory.Siege, UnitCategory.Infantry, UnitCategory.Cavalry };
                    }
                    else
                    {
                        // Frontline cavalry same as infantry: Infantry > Cavalry > Ranged > Siege
                        return new List<UnitCategory> { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Ranged, UnitCategory.Siege };
                    }

                default:
                    return new List<UnitCategory> { UnitCategory.Infantry, UnitCategory.Cavalry, UnitCategory.Ranged, UnitCategory.Siege };
            }
        }

        /// Find the best target unit type from enemy state
        public static MilitaryUnitType? FindTarget(UnitCategory attackerCategory, CavalryStance stance, SideCombatState enemyState)
        {
            var priorities = GetTargetPriority(attackerCategory, stance);

            foreach (var targetCategory in priorities)
            {
                // Find unit types in this category that the enemy has
                foreach (var kvp in enemyState.unitCounts)
                {
                    if (kvp.Key.Category() == targetCategory && kvp.Value > 0)
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }
    }
}
