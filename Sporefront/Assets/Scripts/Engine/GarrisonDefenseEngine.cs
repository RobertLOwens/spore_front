// ============================================================================
// FILE: Engine/GarrisonDefenseEngine.cs
// PURPOSE: Handles garrison defense logic - extracted from CombatEngine
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
    /// <summary>
    /// Processes garrison defense attacks where ranged/siege units in defensive
    /// buildings fire on nearby enemy armies.
    /// </summary>
    public class GarrisonDefenseEngine
    {
        // Garrison Damage Constants
        private readonly double archerGarrisonDamage = GameConfig.GarrisonDefense.ArcherDamage;
        private readonly double crossbowGarrisonDamage = GameConfig.GarrisonDefense.CrossbowDamage;
        private readonly double mangonelGarrisonDamage = GameConfig.GarrisonDefense.MangonelDamage;
        private readonly double trebuchetGarrisonDamage = GameConfig.GarrisonDefense.TrebuchetDamage;

        // State
        public HashSet<Guid> ActiveGarrisonEngagements { get; private set; } = new HashSet<Guid>();

        /// <summary>
        /// Function that checks whether a given army is currently attacking a defensive building.
        /// Provided by CombatEngine during setup.
        /// </summary>
        private Func<Guid, bool> isArmyInBuildingCombat;
        private GameState gameState;

        /// <summary>
        /// Callback to record combat history.
        /// </summary>
        public Action<CombatRecord> OnCombatRecord;

        // Setup

        public void Setup(GameState gameState, Func<Guid, bool> isArmyInBuildingCombat)
        {
            this.gameState = gameState;
            this.isArmyInBuildingCombat = isArmyInBuildingCombat;
        }

        public void Reset()
        {
            ActiveGarrisonEngagements.Clear();
        }

        // Process Garrison Defense

        public List<StateChange> ProcessGarrisonDefense(double currentTime, GameState state, double piercingDamageMultiplier = 1.0)
        {
            var changes = new List<StateChange>();
            var armiesUnderFireThisTick = new HashSet<Guid>();

            var aggregatedAttacks = new Dictionary<Guid, AggregatedAttack>();

            foreach (var building in state.buildings.Values)
            {
                if (!building.CanProvideGarrisonDefense) continue;
                if (!building.IsOperational) continue;
                if (!building.ownerID.HasValue) continue;
                Guid ownerID = building.ownerID.Value;

                ArmyData garrisonArmy = state.GetArmy(building.coordinate);
                if (garrisonArmy == null) continue;
                if (!garrisonArmy.ownerID.HasValue || garrisonArmy.ownerID.Value != ownerID) continue;

                int archerCount = garrisonArmy.GetUnitCount(MilitaryUnitType.Archer);
                int crossbowCount = garrisonArmy.GetUnitCount(MilitaryUnitType.Crossbow);
                int mangonelCount = garrisonArmy.GetUnitCount(MilitaryUnitType.Mangonel);
                int trebuchetCount = garrisonArmy.GetUnitCount(MilitaryUnitType.Trebuchet);
                int defensiveUnitCount = archerCount + crossbowCount + mangonelCount + trebuchetCount;

                if (defensiveUnitCount <= 0) continue;

                List<ArmyData> enemies = state.GetEnemyArmiesInRange(building.coordinate, building.GarrisonDefenseRange, ownerID);
                if (enemies.Count == 0) continue;

                ArmyData target = null;
                foreach (var enemy in enemies)
                {
                    if (!IsArmyAttackingDefensiveBuilding(enemy.id))
                    {
                        target = enemy;
                        break;
                    }
                }
                if (target == null) continue;

                double pierceDamage = 0;
                double bludgeonDamage = 0;

                pierceDamage += (double)archerCount * archerGarrisonDamage;
                pierceDamage += (double)crossbowCount * crossbowGarrisonDamage;
                bludgeonDamage += (double)mangonelCount * mangonelGarrisonDamage;
                bludgeonDamage += (double)trebuchetCount * trebuchetGarrisonDamage;

                pierceDamage *= piercingDamageMultiplier;

                if (pierceDamage > 0 || bludgeonDamage > 0)
                {
                    armiesUnderFireThisTick.Add(target.id);

                    if (aggregatedAttacks.ContainsKey(target.id))
                    {
                        var existing = aggregatedAttacks[target.id];
                        existing.pierceDamage += pierceDamage;
                        existing.bludgeonDamage += bludgeonDamage;
                        existing.buildings.Add(building.buildingType.DisplayName());
                        aggregatedAttacks[target.id] = existing;
                    }
                    else
                    {
                        aggregatedAttacks[target.id] = new AggregatedAttack
                        {
                            pierceDamage = pierceDamage,
                            bludgeonDamage = bludgeonDamage,
                            buildings = new List<string> { building.buildingType.DisplayName() },
                            ownerID = ownerID,
                            location = building.coordinate
                        };
                    }

                    changes.Add(new GarrisonDefenseAttackChange
                    {
                        buildingID = building.id,
                        targetArmyID = target.id,
                        damage = pierceDamage + bludgeonDamage
                    });
                }
            }

            // Apply damage with armor reduction
            foreach (var kvp in aggregatedAttacks)
            {
                Guid targetArmyID = kvp.Key;
                AggregatedAttack attackData = kvp.Value;

                ArmyData target = state.GetArmy(targetArmyID);
                if (target == null) continue;

                UnitCombatStats targetArmor = target.GetAggregatedCombatStats();

                double effectivePierceDamage = Math.Max(0, attackData.pierceDamage - targetArmor.pierceArmor);
                double effectiveBludgeonDamage = Math.Max(0, attackData.bludgeonDamage - targetArmor.bludgeonArmor);
                double totalEffectiveDamage = effectivePierceDamage + effectiveBludgeonDamage;

                if (totalEffectiveDamage <= 0) continue;

                int targetInitialUnits = target.GetTotalUnits();
                DamageCalculator.ApplyDamageToArmy(target, totalEffectiveDamage);
                int targetFinalUnits = target.GetTotalUnits();
                int totalCasualties = targetInitialUnits - targetFinalUnits;

                bool isNewEngagement = !ActiveGarrisonEngagements.Contains(targetArmyID);
                bool isDestroyed = target.IsEmpty();

                if (isNewEngagement || isDestroyed)
                {
                    PlayerState buildingOwner = state.GetPlayer(attackData.ownerID);
                    PlayerState targetOwner = target.ownerID.HasValue ? state.GetPlayer(target.ownerID.Value) : null;

                    string attackerName;
                    if (attackData.buildings.Count == 1)
                    {
                        attackerName = attackData.buildings[0] + " Garrison";
                    }
                    else
                    {
                        var uniqueBuildings = new List<string>(new HashSet<string>(attackData.buildings));
                        uniqueBuildings.Sort();
                        attackerName = string.Join(" & ", uniqueBuildings) + " Garrisons";
                    }

                    Color attackerColor = Color.gray;
                    if (buildingOwner != null && !string.IsNullOrEmpty(buildingOwner.colorHex))
                    {
                        Color parsedColor;
                        if (ColorUtility.TryParseHtmlString(buildingOwner.colorHex, out parsedColor))
                            attackerColor = parsedColor;
                    }

                    Color defenderColor = Color.gray;
                    if (targetOwner != null && !string.IsNullOrEmpty(targetOwner.colorHex))
                    {
                        Color parsedColor;
                        if (ColorUtility.TryParseHtmlString(targetOwner.colorHex, out parsedColor))
                            defenderColor = parsedColor;
                    }

                    string commanderName = null;
                    if (target.commanderID.HasValue)
                    {
                        CommanderData commander = state.GetCommander(target.commanderID.Value);
                        if (commander != null)
                            commanderName = commander.name;
                    }

                    var attackerParticipant = new CombatParticipant(
                        name: attackerName,
                        type: CombatParticipantType.Building,
                        ownerName: buildingOwner != null ? buildingOwner.name : "Unknown",
                        ownerColor: attackerColor,
                        commanderName: null
                    );

                    var defenderParticipant = new CombatParticipant(
                        name: target.name,
                        type: CombatParticipantType.Army,
                        ownerName: targetOwner != null ? targetOwner.name : "Unknown",
                        ownerColor: defenderColor,
                        commanderName: commanderName
                    );

                    CombatResult winner = target.IsEmpty() ? CombatResult.AttackerVictory : CombatResult.Draw;

                    var record = new CombatRecord(
                        attacker: attackerParticipant,
                        defender: defenderParticipant,
                        attackerInitialStrength: totalEffectiveDamage,
                        defenderInitialStrength: (double)targetInitialUnits,
                        attackerFinalStrength: totalEffectiveDamage,
                        defenderFinalStrength: (double)targetFinalUnits,
                        winner: winner,
                        attackerCasualties: 0,
                        defenderCasualties: totalCasualties,
                        location: attackData.location,
                        duration: 0.0
                    );

                    if (OnCombatRecord != null)
                        OnCombatRecord(record);

                    ActiveGarrisonEngagements.Add(targetArmyID);
                }

                if (isDestroyed)
                {
                    ActiveGarrisonEngagements.Remove(targetArmyID);

                    changes.Add(new ArmyDestroyedChange
                    {
                        armyID = target.id,
                        coordinate = target.coordinate
                    });
                    state.RemoveArmy(target.id);
                }
            }

            // Clean up engagements for armies that left range
            var armiesThatLeftRange = new HashSet<Guid>(ActiveGarrisonEngagements);
            armiesThatLeftRange.ExceptWith(armiesUnderFireThisTick);
            foreach (Guid armyID in armiesThatLeftRange)
            {
                ActiveGarrisonEngagements.Remove(armyID);
            }

            return changes;
        }

        // Helpers

        private bool IsArmyAttackingDefensiveBuilding(Guid armyID)
        {
            if (isArmyInBuildingCombat == null) return false;
            return isArmyInBuildingCombat(armyID);
        }

        // Internal aggregated attack data
        private struct AggregatedAttack
        {
            public double pierceDamage;
            public double bludgeonDamage;
            public List<string> buildings;
            public Guid ownerID;
            public HexCoordinate location;
        }
    }
}
