// ============================================================================
// FILE: Commands/AttackCommand.cs
// PURPOSE: Command to initiate combat - ported from AttackCommand.swift (engine logic path)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Models.Combat;

namespace Sporefront.Commands
{
    public class AttackCommand : BaseEngineCommand
    {
        public Guid armyID;
        public HexCoordinate targetCoordinate;

        public AttackCommand(Guid playerID, Guid armyID, HexCoordinate targetCoordinate)
            : base(playerID)
        {
            this.armyID = armyID;
            this.targetCoordinate = targetCoordinate;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check army exists
            ArmyData army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Attacker not found");

            // Check army is owned by player
            if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("You don't own this army");

            // Check army is not in combat
            if (army.isInCombat || GameEngine.Instance.combatEngine.IsInCombat(armyID))
                return EngineCommandResult.Failure("Army is currently in combat");

            // Check army has a commander
            if (!army.commanderID.HasValue)
                return EngineCommandResult.Failure("Army requires a commander to attack");

            // Check commander stamina
            CommanderData commander = state.GetCommander(army.commanderID.Value);
            if (commander == null)
                return EngineCommandResult.Failure("Commander not found");
            if (!commander.HasEnoughStamina())
                return EngineCommandResult.Failure("Commander lacks stamina to issue this order");

            // Check target coordinate is valid
            if (!state.mapData.IsValidCoordinate(targetCoordinate))
                return EngineCommandResult.Failure("Invalid target coordinate");

            // Check for entrenched defenders on the target tile
            List<ArmyData> armiesAtTarget = state.GetArmies(targetCoordinate);
            bool hasEntrenchedDefender = false;
            foreach (ArmyData targetArmy in armiesAtTarget)
            {
                if (targetArmy.isEntrenched &&
                    targetArmy.ownerID.HasValue && targetArmy.ownerID.Value != PlayerID)
                {
                    hasEntrenchedDefender = true;
                    break;
                }
            }

            if (hasEntrenchedDefender)
                return EngineCommandResult.Failure("Target is entrenched - attack from an adjacent tile");

            // Check if target has an enemy building
            BuildingData targetBuilding = state.GetBuilding(targetCoordinate);
            if (targetBuilding != null)
            {
                // Must be an enemy building
                DiplomacyStatus diplomacy = DiplomacyStatus.Neutral;
                if (targetBuilding.ownerID.HasValue)
                    diplomacy = state.GetDiplomacyStatus(PlayerID, targetBuilding.ownerID.Value);

                if (diplomacy != DiplomacyStatus.Enemy)
                    return EngineCommandResult.Failure("Target building is not an enemy");

                // Check if building is protected by a defensive structure
                List<BuildingData> protectors = state.GetProtectingBuildings(targetBuilding.id);
                if (protectors.Count > 0)
                {
                    string protectorName = protectors[0].buildingType.ToString();
                    if (protectors.Count == 1)
                        return EngineCommandResult.Failure("Protected by " + protectorName + " - destroy it first");
                    else
                        return EngineCommandResult.Failure("Protected by " + protectorName + " and " +
                            (protectors.Count - 1) + " other(s) - destroy them first");
                }

                return EngineCommandResult.Success(new List<StateChange>());
            }

            // Check if target has enemy armies (not on a building)
            bool hasEnemyArmy = false;
            foreach (ArmyData targetArmy in armiesAtTarget)
            {
                if (targetArmy.ownerID.HasValue && targetArmy.ownerID.Value != PlayerID)
                {
                    DiplomacyStatus armyDiplomacy = state.GetDiplomacyStatus(PlayerID, targetArmy.ownerID.Value);
                    if (armyDiplomacy == DiplomacyStatus.Enemy)
                    {
                        hasEnemyArmy = true;
                        break;
                    }
                }
            }

            if (hasEnemyArmy)
                return EngineCommandResult.Success(new List<StateChange>());

            // Check if target has enemy villager groups
            List<VillagerGroupData> villagerGroups = state.GetVillagerGroups(targetCoordinate);
            bool hasEnemyVillagers = false;
            foreach (VillagerGroupData group in villagerGroups)
            {
                if (group.ownerID.HasValue && group.ownerID.Value != PlayerID)
                {
                    DiplomacyStatus villagerDiplomacy = state.GetDiplomacyStatus(PlayerID, group.ownerID.Value);
                    if (villagerDiplomacy == DiplomacyStatus.Enemy)
                    {
                        hasEnemyVillagers = true;
                        break;
                    }
                }
            }

            if (hasEnemyVillagers)
                return EngineCommandResult.Success(new List<StateChange>());

            // Check for cross-tile entrenched enemies covering this coordinate
            List<ArmyData> crossTileEntrenched = state.GetEntrenchedArmiesCovering(targetCoordinate);
            bool hasCrossTileEnemy = false;
            foreach (ArmyData entrenched in crossTileEntrenched)
            {
                if (entrenched.ownerID.HasValue && entrenched.ownerID.Value != PlayerID)
                {
                    hasCrossTileEnemy = true;
                    break;
                }
            }

            if (hasCrossTileEnemy)
                return EngineCommandResult.Success(new List<StateChange>());

            return EngineCommandResult.Failure("No target at this location");
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            ArmyData army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Attacker not found");

            // Consume commander stamina
            if (army.commanderID.HasValue)
            {
                CommanderData cmdr = state.GetCommander(army.commanderID.Value);
                if (cmdr != null)
                    cmdr.ConsumeStamina();
            }

            // Cancel entrenchment if army is entrenching or entrenched
            if (army.isEntrenching || army.isEntrenched)
            {
                HexCoordinate armyCoord = army.coordinate;
                army.isEntrenching = false;
                army.isEntrenched = false;
                army.entrenchmentStartTime = null;

                changeBuilder.Add(new ArmyEntrenchmentCancelledChange
                {
                    armyID = armyID,
                    coordinate = armyCoord
                });
            }

            double currentTime = state.currentTime;

            // Capture origin BEFORE moving — friendly armies at origin should join the attack
            HexCoordinate attackerOrigin = army.coordinate;

            // Build defensive stack at the target
            DefensiveStack stack = DefensiveStack.Build(targetCoordinate, state, PlayerID);

            // Check for building target first
            BuildingData targetBuilding = state.GetBuilding(targetCoordinate);

            if (targetBuilding != null)
            {
                // Move attacker to target tile first
                var moveResult = MoveAttackerToTarget(army, targetCoordinate, state, changeBuilder, "No path to target building");
                if (moveResult != null)
                    return moveResult;

                // Building combat scenario
                if (stack.ArmyEntries.Count > 0)
                {
                    // Defenders present - use stack combat
                    List<Guid> attackerArmyIDs = new List<Guid> { armyID };

                    // Gather friendly armies at the attacker's ORIGIN position that can join
                    List<ArmyData> friendlyArmies = state.GetArmies(attackerOrigin);
                    foreach (ArmyData ally in friendlyArmies)
                    {
                        if (ally.ownerID.HasValue && ally.ownerID.Value == PlayerID &&
                            !ally.isInCombat && ally.id != armyID)
                        {
                            attackerArmyIDs.Add(ally.id);
                        }
                    }

                    List<StateChange> stackChanges = GameEngine.Instance.combatEngine.StartStackCombat(
                        attackerArmyIDs, targetCoordinate, currentTime);

                    if (stackChanges != null)
                        changeBuilder.AddAll(stackChanges);
                }
                else if (stack.villagerGroupIDs.Count > 0)
                {
                    // Only villagers defending - start villager combat
                    Guid firstVillagerID = stack.villagerGroupIDs[0];
                    StateChange villagerChange = GameEngine.Instance.combatEngine.StartVillagerCombat(
                        armyID, firstVillagerID, currentTime);

                    if (villagerChange != null)
                        changeBuilder.Add(villagerChange);
                }
                else
                {
                    // No defenders - pure building combat
                    StateChange buildingChange = GameEngine.Instance.combatEngine.StartBuildingCombat(
                        armyID, targetBuilding.id, currentTime);

                    if (buildingChange != null)
                        changeBuilder.Add(buildingChange);
                }

                return EngineCommandResult.Success(changeBuilder.Build().changes);
            }

            // Army or villager target (not on a building)
            if (stack.ArmyEntries.Count > 0)
            {
                // Move attacker to target tile first
                var moveResult = MoveAttackerToTarget(army, targetCoordinate, state, changeBuilder, "No path to target");
                if (moveResult != null)
                    return moveResult;

                // Has army defenders
                if (stack.ArmyEntries.Count > 1 || stack.HasEntrenchedDefenders)
                {
                    // Multiple defenders or entrenched defenders - use stack combat
                    List<Guid> attackerArmyIDs = new List<Guid> { armyID };

                    // Gather friendly armies at the attacker's ORIGIN position
                    List<ArmyData> friendlyArmies = state.GetArmies(attackerOrigin);
                    foreach (ArmyData ally in friendlyArmies)
                    {
                        if (ally.ownerID.HasValue && ally.ownerID.Value == PlayerID &&
                            !ally.isInCombat && ally.id != armyID)
                        {
                            attackerArmyIDs.Add(ally.id);
                        }
                    }

                    List<StateChange> stackChanges = GameEngine.Instance.combatEngine.StartStackCombat(
                        attackerArmyIDs, targetCoordinate, currentTime);

                    if (stackChanges != null)
                        changeBuilder.AddAll(stackChanges);
                }
                else
                {
                    // Simple 1v1 combat against the single defender
                    Guid defenderArmyID = stack.ArmyEntries[0].armyID;
                    StateChange combatChange = GameEngine.Instance.combatEngine.StartCombat(
                        armyID, defenderArmyID, currentTime);

                    if (combatChange != null)
                        changeBuilder.Add(combatChange);
                }

                return EngineCommandResult.Success(changeBuilder.Build().changes);
            }

            if (stack.OnlyVillagers)
            {
                // Move attacker to target tile first
                var moveResult = MoveAttackerToTarget(army, targetCoordinate, state, changeBuilder, "No path to target villagers");
                if (moveResult != null)
                    return moveResult;

                // Only villagers at target - start villager combat
                Guid firstVillagerID = stack.villagerGroupIDs[0];
                StateChange villagerChange = GameEngine.Instance.combatEngine.StartVillagerCombat(
                    armyID, firstVillagerID, currentTime);

                if (villagerChange != null)
                    changeBuilder.Add(villagerChange);

                return EngineCommandResult.Success(changeBuilder.Build().changes);
            }

            // Check for cross-tile entrenched enemies covering this coordinate
            List<ArmyData> crossTileEntrenched = state.GetEntrenchedArmiesCovering(targetCoordinate);
            List<ArmyData> enemyCrossTile = new List<ArmyData>();
            foreach (ArmyData entrenched in crossTileEntrenched)
            {
                if (entrenched.ownerID.HasValue && entrenched.ownerID.Value != PlayerID)
                    enemyCrossTile.Add(entrenched);
            }

            if (enemyCrossTile.Count > 0)
            {
                // Move attacker to target tile first
                var moveResult = MoveAttackerToTarget(army, targetCoordinate, state, changeBuilder, "No path to entrenchment zone");
                if (moveResult != null)
                    return moveResult;

                // Build defensive stack which gathers cross-tile entrenched into Tier 1
                DefensiveStack crossStack = DefensiveStack.Build(targetCoordinate, state, PlayerID);

                if (crossStack.ArmyEntries.Count > 0)
                {
                    List<Guid> attackerArmyIDs = new List<Guid> { armyID };

                    // Gather friendly armies at the attacker's ORIGIN position
                    List<ArmyData> friendlyArmies = state.GetArmies(attackerOrigin);
                    foreach (ArmyData ally in friendlyArmies)
                    {
                        if (ally.ownerID.HasValue && ally.ownerID.Value == PlayerID &&
                            !ally.isInCombat && ally.id != armyID)
                        {
                            attackerArmyIDs.Add(ally.id);
                        }
                    }

                    List<StateChange> stackChanges = GameEngine.Instance.combatEngine.StartStackCombat(
                        attackerArmyIDs, targetCoordinate, currentTime);

                    if (stackChanges != null)
                        changeBuilder.AddAll(stackChanges);
                }

                return EngineCommandResult.Success(changeBuilder.Build().changes);
            }

            return EngineCommandResult.Failure("No valid target found");
        }

        /// <summary>
        /// Sets up army movement toward the target coordinate for walk-then-attack.
        /// Returns null if army is already at target (combat starts immediately).
        /// Returns a success result if army needs to walk first (caller should return it to skip immediate combat).
        /// Returns a failure result if no path exists.
        /// </summary>
        private EngineCommandResult MoveAttackerToTarget(ArmyData army, HexCoordinate target,
            GameState state, StateChangeBuilder changeBuilder, string failureMsg)
        {
            if (army.coordinate.Equals(target))
                return null;

            List<HexCoordinate> path;
            if (army.coordinate.Distance(target) == 1)
            {
                path = new List<HexCoordinate> { target };
            }
            else
            {
                path = state.mapData.FindPath(
                    army.coordinate, target, PlayerID, state,
                    allowImpassableDestination: true);

                if (path == null || path.Count == 0)
                    return EngineCommandResult.Failure(failureMsg);
            }

            // Set walking path instead of teleporting
            army.currentPath = path;
            army.pathIndex = 0;
            army.movementProgress = 0.0;
            army.pendingAttackTarget = target;

            changeBuilder.Add(new ArmyMovedChange
            {
                armyID = army.id,
                from = army.coordinate,
                to = target,
                path = path
            });

            // Return success to signal caller that army is walking (skip immediate combat start)
            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
