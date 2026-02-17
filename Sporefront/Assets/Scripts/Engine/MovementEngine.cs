// ============================================================================
// FILE: Engine/MovementEngine.cs
// PURPOSE: Handles all movement logic - pathfinding and movement for armies,
//          villager groups, and reinforcements
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    /// <summary>
    /// Handles pathfinding and movement for all movable entities.
    /// </summary>
    public class MovementEngine
    {
        // State
        private GameState gameState;

        // Movement Constants
        private readonly double baseMovementSpeed = GameConfig.Movement.BaseSpeed;
        private readonly double terrainSpeedMultiplier = GameConfig.Movement.TerrainSpeedMultiplier;
        private readonly double retreatSpeedBonus = GameConfig.Movement.RetreatSpeedBonus;

        // Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        // Update Loop

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Update army movements
            // Copy to list to avoid collection modification during iteration (RemoveArmy)
            foreach (var army in gameState.armies.Values.ToList())
            {
                var armyChanges = UpdateArmyMovement(army, currentTime);
                changes.AddRange(armyChanges);
            }

            // Update villager group movements
            foreach (var group in gameState.villagerGroups.Values.ToList())
            {
                var groupChanges = UpdateVillagerGroupMovement(group, currentTime);
                changes.AddRange(groupChanges);
            }

            // Update reinforcement movements
            foreach (var army in gameState.armies.Values.ToList())
            {
                var reinforcementChanges = UpdateReinforcementMovements(army, currentTime);
                changes.AddRange(reinforcementChanges);
            }

            return changes;
        }

        // Army Movement

        private List<StateChange> UpdateArmyMovement(ArmyData army, double currentTime)
        {
            if (army.currentPath == null || army.pathIndex >= army.currentPath.Count)
                return new List<StateChange>();

            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();
            var path = army.currentPath;

            // Clear entrenchment when army moves
            if (army.isEntrenching || army.isEntrenched)
            {
                var coord = army.coordinate;
                army.ClearEntrenchment();
                changes.Add(new ArmyEntrenchmentCancelledChange
                {
                    armyID = army.id,
                    coordinate = coord
                });
                DebugLog.Log($"Army {army.id} entrenchment cancelled due to movement");
            }

            var targetCoord = path[army.pathIndex];

            // Calculate movement speed based on slowest unit in army
            double slowestUnitSpeed = army.SlowestUnitMoveSpeed;
            // Normalize: default army speed (1.6) = base speed, slower units reduce speed proportionally
            double speedMultiplier = 1.6 / slowestUnitSpeed;

            double speed;
            if (gameState.mapData.GetBuildingID(targetCoord) != null)
            {
                // On road
                speed = baseMovementSpeed * speedMultiplier;
            }
            else
            {
                speed = baseMovementSpeed * speedMultiplier * terrainSpeedMultiplier;
            }

            if (army.isRetreating)
            {
                speed *= retreatSpeedBonus;
            }

            // Apply commander logistics bonus
            if (army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    double logisticsBonus = 1.0 + (double)commander.Logistics * GameConfig.Commander.LogisticsSpeedScaling;
                    speed *= logisticsBonus;
                }
            }

            // Update progress (0.1 second update interval)
            army.movementProgress += speed * 0.1;

            // Check if we've reached the next tile
            if (army.movementProgress >= 1.0)
            {
                var fromCoord = army.coordinate;

                // Move to next tile
                army.coordinate = targetCoord;
                gameState.mapData.UpdateArmyPosition(army.id, targetCoord);
                army.pathIndex += 1;
                army.movementProgress = 0.0;

                changes.Add(new ArmyMovedChange
                {
                    armyID = army.id,
                    from = fromCoord,
                    to = targetCoord,
                    path = path.Skip(army.pathIndex).ToList()
                });

                // Check if path is complete
                if (army.pathIndex >= path.Count)
                {
                    army.currentPath = null;
                    army.pathIndex = 0;
                    bool wasRetreating = army.isRetreating;
                    army.isRetreating = false;

                    // Clean up empty armies that finished retreating (commander returns home)
                    if (wasRetreating && army.IsEmpty())
                    {
                        changes.Add(new ArmyDestroyedChange
                        {
                            armyID = army.id,
                            coordinate = army.coordinate
                        });
                        gameState.RemoveArmy(army.id);
                    }
                }
            }

            return changes;
        }

        // Villager Group Movement

        private List<StateChange> UpdateVillagerGroupMovement(VillagerGroupData group, double currentTime)
        {
            if (group.currentPath == null || group.pathIndex >= group.currentPath.Count)
                return new List<StateChange>();

            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();
            var path = group.currentPath;
            var targetCoord = path[group.pathIndex];

            // Calculate movement speed (villagers are slower)
            double villagerMultiplier = GameConfig.Movement.VillagerSpeedMultiplier;
            double speed;
            if (gameState.mapData.GetBuildingID(targetCoord) != null)
            {
                speed = baseMovementSpeed * villagerMultiplier;
            }
            else
            {
                speed = baseMovementSpeed * villagerMultiplier * terrainSpeedMultiplier;
            }

            // Update progress
            group.movementProgress += speed * 0.1;

            // Check if we've reached the next tile
            if (group.movementProgress >= 1.0)
            {
                var fromCoord = group.coordinate;

                // Move to next tile
                group.coordinate = targetCoord;
                gameState.mapData.UpdateVillagerGroupPosition(group.id, targetCoord);
                group.pathIndex += 1;
                group.movementProgress = 0.0;

                changes.Add(new VillagerGroupMovedChange
                {
                    groupID = group.id,
                    from = fromCoord,
                    to = targetCoord,
                    path = path.Skip(group.pathIndex).ToList()
                });

                // Check if path is complete
                if (group.pathIndex >= path.Count)
                {
                    group.currentPath = null;
                    group.pathIndex = 0;

                    // Clear moving task if that was the current task
                    if (group.currentTask is MovingTask)
                    {
                        group.ClearTask();
                    }
                }
            }

            return changes;
        }

        // Reinforcement Movement

        private List<StateChange> UpdateReinforcementMovements(ArmyData army, double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();
            var arrivedReinforcements = new List<Guid>();

            for (int i = 0; i < army.pendingReinforcements.Count; i++)
            {
                var reinforcement = army.pendingReinforcements[i];

                if (reinforcement.pathIndex >= reinforcement.path.Count)
                {
                    // Reinforcement has arrived
                    arrivedReinforcements.Add(reinforcement.reinforcementID);
                    continue;
                }

                // Calculate movement speed for reinforcements
                var targetCoord = reinforcement.path[reinforcement.pathIndex];
                double reinforcementMultiplier = GameConfig.Movement.ReinforcementSpeedMultiplier;
                double speed;

                if (gameState.mapData.GetBuildingID(targetCoord) != null)
                {
                    speed = baseMovementSpeed * reinforcementMultiplier;
                }
                else
                {
                    speed = baseMovementSpeed * reinforcementMultiplier * terrainSpeedMultiplier;
                }

                // Apply commander logistics bonus to reinforcement speed
                if (army.commanderID.HasValue)
                {
                    var commander = gameState.GetCommander(army.commanderID.Value);
                    if (commander != null)
                    {
                        double logisticsBonus = 1.0 + (double)commander.Logistics * GameConfig.Commander.LogisticsSpeedScaling;
                        speed *= logisticsBonus;
                    }
                }

                // Simple progress update
                reinforcement.pathIndex += 1;
                reinforcement.currentCoordinate = targetCoord;

                // Update in army (PendingReinforcement is a struct, must assign back)
                army.pendingReinforcements[i] = reinforcement;

                // Check if arrived at target
                if (reinforcement.currentCoordinate == army.coordinate || reinforcement.pathIndex >= reinforcement.path.Count)
                {
                    arrivedReinforcements.Add(reinforcement.reinforcementID);
                }
            }

            // Process arrived reinforcements
            foreach (var reinforcementID in arrivedReinforcements)
            {
                var reinforcement = army.pendingReinforcements.FirstOrDefault(r => r.reinforcementID == reinforcementID);
                if (reinforcement.reinforcementID == reinforcementID)
                {
                    // Add units to army
                    foreach (var kvp in reinforcement.unitComposition)
                    {
                        army.AddMilitaryUnits(kvp.Key, kvp.Value);
                    }

                    // Remove from pending
                    army.RemovePendingReinforcement(reinforcementID);

                    // Update army composition change
                    var compositionDict = new Dictionary<string, int>();
                    foreach (var kvp in army.militaryComposition)
                    {
                        compositionDict[kvp.Key.ToString()] = kvp.Value;
                    }
                    changes.Add(new ArmyCompositionChangedChange
                    {
                        armyID = army.id,
                        newComposition = compositionDict
                    });
                }
            }

            return changes;
        }

        // Path Calculation

        /// <summary>
        /// Calculate a path for an army.
        /// </summary>
        public List<HexCoordinate> CalculatePath(Guid armyID, HexCoordinate target)
        {
            if (gameState == null) return null;

            var army = gameState.GetArmy(armyID);
            if (army == null) return null;

            return gameState.mapData.FindPath(
                army.coordinate,
                target,
                army.ownerID ?? Guid.Empty,
                gameState
            );
        }

        /// <summary>
        /// Calculate a path for a villager group.
        /// </summary>
        public List<HexCoordinate> CalculatePathForVillagerGroup(Guid groupID, HexCoordinate target)
        {
            if (gameState == null) return null;

            var group = gameState.GetVillagerGroup(groupID);
            if (group == null) return null;

            return gameState.mapData.FindPath(
                group.coordinate,
                target,
                group.ownerID ?? Guid.Empty,
                gameState
            );
        }

        /// <summary>
        /// Set a movement path for an army.
        /// </summary>
        public void SetArmyPath(Guid armyID, List<HexCoordinate> path)
        {
            if (gameState == null) return;

            var army = gameState.GetArmy(armyID);
            if (army == null) return;

            army.currentPath = path;
            army.pathIndex = 0;
            army.movementProgress = 0.0;
        }

        /// <summary>
        /// Set a movement path for a villager group.
        /// </summary>
        public void SetVillagerGroupPath(Guid groupID, List<HexCoordinate> path)
        {
            if (gameState == null) return;

            var group = gameState.GetVillagerGroup(groupID);
            if (group == null) return;

            group.currentPath = path;
            group.pathIndex = 0;
            group.movementProgress = 0.0;
            group.currentTask = new MovingTask(path.Count > 0 ? path[path.Count - 1] : group.coordinate);
        }

        /// <summary>
        /// Stop army movement.
        /// </summary>
        public void StopArmyMovement(Guid armyID)
        {
            if (gameState == null) return;

            var army = gameState.GetArmy(armyID);
            if (army == null) return;

            army.currentPath = null;
            army.pathIndex = 0;
            army.movementProgress = 0.0;
            army.isRetreating = false;
        }

        /// <summary>
        /// Stop villager group movement.
        /// </summary>
        public void StopVillagerGroupMovement(Guid groupID)
        {
            if (gameState == null) return;

            var group = gameState.GetVillagerGroup(groupID);
            if (group == null) return;

            group.currentPath = null;
            group.pathIndex = 0;
            group.movementProgress = 0.0;

            if (group.currentTask is MovingTask)
            {
                group.ClearTask();
            }
        }
    }
}
