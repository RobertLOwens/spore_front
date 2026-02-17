// ============================================================================
// FILE: Engine/TrainingEngine.cs
// PURPOSE: Handles unit training logic - military units, villagers, and
//          army/villager deployment from garrisons
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    public class TrainingEngine
    {
        // State
        private GameState gameState;

        // Constants
        private double villagerTrainingTime = GameConfig.Training.VillagerTrainingTime;

        // Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        // ================================================================
        // Update Loop
        // ================================================================

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return new List<StateChange>();

            var changes = new List<StateChange>();

            // Process training for all buildings
            foreach (var building in gameState.buildings.Values)
            {
                if (!building.IsOperational) continue;

                // Military training
                if (building.trainingQueue.Count > 0)
                {
                    var militaryChanges = UpdateMilitaryTraining(building, currentTime);
                    changes.AddRange(militaryChanges);
                }

                // Villager training
                if (building.villagerTrainingQueue.Count > 0)
                {
                    var villagerChanges = UpdateVillagerTraining(building, currentTime, gameState);
                    changes.AddRange(villagerChanges);
                }
            }

            return changes;
        }

        // ================================================================
        // Military Training
        // ================================================================

        private List<StateChange> UpdateMilitaryTraining(BuildingData building, double currentTime)
        {
            var changes = new List<StateChange>();

            var completedEntries = building.UpdateTraining(currentTime);

            foreach (var entry in completedEntries)
            {
                changes.Add(new TrainingCompletedChange
                {
                    buildingID = building.id,
                    unitType = entry.unitType.ToString(),
                    quantity = entry.quantity
                });

                changes.Add(new UnitsGarrisonedChange
                {
                    buildingID = building.id,
                    unitType = entry.unitType.ToString(),
                    quantity = entry.quantity
                });
            }

            // Update progress for remaining entries
            for (int index = 0; index < building.trainingQueue.Count; index++)
            {
                var entry = building.trainingQueue[index];
                if (entry.progress > 0 && entry.progress < 1.0)
                {
                    changes.Add(new TrainingProgressChange
                    {
                        buildingID = building.id,
                        entryIndex = index,
                        progress = entry.progress
                    });
                }
            }

            return changes;
        }

        // ================================================================
        // Villager Training
        // ================================================================

        private List<StateChange> UpdateVillagerTraining(BuildingData building, double currentTime, GameState state)
        {
            var changes = new List<StateChange>();

            var completedEntries = building.UpdateVillagerTraining(currentTime);

            foreach (var entry in completedEntries)
            {
                changes.Add(new VillagerTrainingCompletedChange
                {
                    buildingID = building.id,
                    quantity = entry.quantity
                });

                changes.Add(new VillagersGarrisonedChange
                {
                    buildingID = building.id,
                    quantity = entry.quantity
                });
            }

            // Update progress for remaining entries
            for (int index = 0; index < building.villagerTrainingQueue.Count; index++)
            {
                var entry = building.villagerTrainingQueue[index];
                if (entry.progress > 0 && entry.progress < 1.0)
                {
                    changes.Add(new VillagerTrainingProgressChange
                    {
                        buildingID = building.id,
                        entryIndex = index,
                        progress = entry.progress
                    });
                }
            }

            return changes;
        }

        // ================================================================
        // Training Commands - Military
        // ================================================================

        public (bool valid, string reason) CanTrainMilitary(Guid buildingID, MilitaryUnitType unitType, int quantity, Guid playerID)
        {
            if (gameState == null) return (false, "Building not found");

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (false, "Building not found");

            var player = gameState.GetPlayer(playerID);
            if (player == null) return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            if (!building.IsOperational)
                return (false, "Building not operational");

            // Check if building can train this unit type
            if (unitType.TrainingBuilding() != building.buildingType)
                return (false, "This building cannot train " + unitType.DisplayName());

            // Check population capacity
            int currentPop, popCapacity;
            gameState.GetPopulationStats(playerID, out currentPop, out popCapacity);
            if (currentPop + (unitType.PopSpace() * quantity) > popCapacity)
                return (false, "Not enough population capacity");

            // Check resources
            var cost = ConvertTrainingCost(unitType.TrainingCost(), quantity);
            if (!player.CanAfford(cost))
                return (false, "Insufficient resources");

            return (true, null);
        }

        public List<StateChange> StartMilitaryTraining(Guid buildingID, MilitaryUnitType unitType, int quantity, Guid playerID)
        {
            if (gameState == null) return new List<StateChange>();

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return new List<StateChange>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<StateChange>();

            var validation = CanTrainMilitary(buildingID, unitType, quantity, playerID);
            if (!validation.valid) return new List<StateChange>();

            // Deduct resources
            var cost = ConvertTrainingCost(unitType.TrainingCost(), quantity);
            foreach (var kvp in cost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Start training
            building.StartTraining(unitType, quantity, gameState.currentTime);

            return new List<StateChange>
            {
                new TrainingStartedChange
                {
                    buildingID = buildingID,
                    unitType = unitType.ToString(),
                    quantity = quantity,
                    startTime = gameState.currentTime
                }
            };
        }

        // ================================================================
        // Training Commands - Villagers
        // ================================================================

        public (bool valid, string reason) CanTrainVillagers(Guid buildingID, int quantity, Guid playerID)
        {
            if (gameState == null) return (false, "Building not found");

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (false, "Building not found");

            var player = gameState.GetPlayer(playerID);
            if (player == null) return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            if (!building.IsOperational)
                return (false, "Building not operational");

            if (!building.CanTrainVillagers())
                return (false, "This building cannot train villagers");

            // Check population capacity
            int currentPop, popCapacity;
            gameState.GetPopulationStats(playerID, out currentPop, out popCapacity);
            if (currentPop + quantity > popCapacity)
                return (false, "Not enough population capacity");

            // Check resources (villagers cost 50 food each)
            var cost = new Dictionary<ResourceType, int> { { ResourceType.Food, 50 * quantity } };
            if (!player.CanAfford(cost))
                return (false, "Insufficient resources");

            return (true, null);
        }

        public List<StateChange> StartVillagerTraining(Guid buildingID, int quantity, Guid playerID)
        {
            if (gameState == null) return new List<StateChange>();

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return new List<StateChange>();

            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<StateChange>();

            var validation = CanTrainVillagers(buildingID, quantity, playerID);
            if (!validation.valid) return new List<StateChange>();

            // Deduct resources
            player.RemoveResource(ResourceType.Food, 50 * quantity);

            // Start training
            building.StartVillagerTraining(quantity, gameState.currentTime);

            return new List<StateChange>
            {
                new VillagerTrainingStartedChange
                {
                    buildingID = buildingID,
                    quantity = quantity,
                    startTime = gameState.currentTime
                }
            };
        }

        // ================================================================
        // Deployment - Army
        // ================================================================

        public (bool valid, string reason) CanDeployArmy(Guid buildingID, Dictionary<MilitaryUnitType, int> composition, Guid playerID)
        {
            if (gameState == null) return (false, "Building not found");

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            // Check garrison has enough units
            foreach (var kvp in composition)
            {
                int available = building.garrison.ContainsKey(kvp.Key) ? building.garrison[kvp.Key] : 0;
                if (available < kvp.Value)
                    return (false, "Not enough " + kvp.Key.DisplayName() + " in garrison");
            }

            // Check army limit
            int currentArmies = gameState.GetArmiesForPlayer(playerID).Count;
            int ccLevel = gameState.GetCityCenterLevel(playerID);
            int maxArmies = 1 + (ccLevel / 2);

            if (currentArmies >= maxArmies)
                return (false, "Maximum army limit reached");

            return (true, null);
        }

        public (ArmyData army, List<StateChange> changes) DeployArmy(Guid buildingID, Dictionary<MilitaryUnitType, int> composition, Guid playerID)
        {
            if (gameState == null) return (null, new List<StateChange>());

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (null, new List<StateChange>());

            var validation = CanDeployArmy(buildingID, composition, playerID);
            if (!validation.valid) return (null, new List<StateChange>());

            var changes = new List<StateChange>();

            // Remove units from garrison
            foreach (var kvp in composition)
            {
                building.RemoveFromGarrison(kvp.Key, kvp.Value);

                changes.Add(new UnitsUngarrisonedChange
                {
                    buildingID = buildingID,
                    unitType = kvp.Key.ToString(),
                    quantity = kvp.Value
                });
            }

            // Find spawn position
            HexCoordinate spawnCoord;
            HexCoordinate? walkable = gameState.mapData.FindNearestWalkable(building.coordinate, 3, playerID, gameState);
            spawnCoord = walkable.HasValue ? walkable.Value : building.coordinate;

            // Create army
            var army = new ArmyData("Army", spawnCoord, playerID);

            // Assign home base respecting capacity limits
            if (gameState.HasHomeBaseCapacity(buildingID))
            {
                army.homeBaseID = buildingID;
            }
            else
            {
                var fallback = gameState.FindHomeBaseWithCapacity(playerID, spawnCoord, null);
                if (fallback != null)
                {
                    army.homeBaseID = fallback.id;
                    DebugLog.Log("Army deployed from " + building.buildingType.ToString() +
                        " but assigned to " + fallback.buildingType.ToString() + " (capacity full)");
                }
                else
                {
                    army.homeBaseID = buildingID; // No capacity anywhere - assign anyway
                }
            }

            // Add units to army
            foreach (var kvp in composition)
            {
                army.AddMilitaryUnits(kvp.Key, kvp.Value);
            }

            // Add to game state
            gameState.AddArmy(army);

            // Build composition dict for change
            var compositionDict = new Dictionary<string, int>();
            foreach (var kvp in army.militaryComposition)
            {
                compositionDict[kvp.Key.ToString()] = kvp.Value;
            }

            changes.Add(new ArmyCreatedChange
            {
                armyID = army.id,
                ownerID = playerID,
                coordinate = spawnCoord,
                composition = compositionDict
            });

            return (army, changes);
        }

        // ================================================================
        // Deployment - Villagers
        // ================================================================

        public (bool valid, string reason) CanDeployVillagers(Guid buildingID, int count, Guid playerID)
        {
            if (gameState == null) return (false, "Building not found");

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (false, "Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != playerID)
                return (false, "Not your building");

            if (building.villagerGarrison < count)
                return (false, "Not enough villagers in garrison");

            // Check villager group limit
            int currentGroups = gameState.GetVillagerGroupsForPlayer(playerID).Count;
            int ccLevel = gameState.GetCityCenterLevel(playerID);
            int maxGroups = 2 + ccLevel;

            if (currentGroups >= maxGroups)
                return (false, "Maximum villager group limit reached");

            return (true, null);
        }

        public (VillagerGroupData group, List<StateChange> changes) DeployVillagers(Guid buildingID, int count, Guid playerID)
        {
            if (gameState == null) return (null, new List<StateChange>());

            var building = gameState.GetBuilding(buildingID);
            if (building == null) return (null, new List<StateChange>());

            var validation = CanDeployVillagers(buildingID, count, playerID);
            if (!validation.valid) return (null, new List<StateChange>());

            var changes = new List<StateChange>();

            // Remove villagers from garrison
            building.RemoveVillagersFromGarrison(count);

            changes.Add(new VillagersUngarrisonedChange
            {
                buildingID = buildingID,
                quantity = count
            });

            // Find spawn position
            HexCoordinate spawnCoord;
            HexCoordinate? walkable = gameState.mapData.FindNearestWalkable(building.coordinate, 3, playerID, gameState);
            spawnCoord = walkable.HasValue ? walkable.Value : building.coordinate;

            // Create villager group
            var group = new VillagerGroupData("Villagers", spawnCoord, count, playerID);

            // Add to game state
            gameState.AddVillagerGroup(group);

            changes.Add(new VillagerGroupCreatedChange
            {
                groupID = group.id,
                ownerID = playerID,
                coordinate = spawnCoord,
                count = count
            });

            return (group, changes);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Dictionary<ResourceType, int> ConvertTrainingCost(Dictionary<ResourceType, int> cost, int quantity)
        {
            var total = new Dictionary<ResourceType, int>();
            foreach (var kvp in cost)
            {
                total[kvp.Key] = kvp.Value * quantity;
            }
            return total;
        }
    }
}
