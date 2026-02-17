using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public class GameState
    {
        // Map Data
        public MapData mapData;

        // Players
        public Dictionary<Guid, PlayerState> players = new Dictionary<Guid, PlayerState>();
        public Guid? localPlayerID;

        // Buildings
        public Dictionary<Guid, BuildingData> buildings = new Dictionary<Guid, BuildingData>();

        // Armies
        public Dictionary<Guid, ArmyData> armies = new Dictionary<Guid, ArmyData>();

        // Villager Groups
        public Dictionary<Guid, VillagerGroupData> villagerGroups = new Dictionary<Guid, VillagerGroupData>();

        // Resource Points
        public Dictionary<Guid, ResourcePointData> resourcePoints = new Dictionary<Guid, ResourcePointData>();

        // Commanders
        public Dictionary<Guid, CommanderData> commanders = new Dictionary<Guid, CommanderData>();

        // Time
        public double currentTime;
        public double gameStartTime;

        // Game Settings
        public bool isPaused;
        public double gameSpeed = 1.0;

        // Transient State (not saved)
        [System.NonSerialized]
        public Dictionary<Guid, HashSet<HexCoordinate>> activeReinforcementPositions =
            new Dictionary<Guid, HashSet<HexCoordinate>>();

        public GameState(int mapWidth, int mapHeight)
        {
            this.mapData = new MapData(mapWidth, mapHeight);
        }

        // ================================================================
        // Player Management
        // ================================================================

        public void AddPlayer(PlayerState player) { players[player.id] = player; }

        public void RemovePlayer(Guid id) { players.Remove(id); }

        public PlayerState GetPlayer(Guid id)
        {
            PlayerState player;
            return players.TryGetValue(id, out player) ? player : null;
        }

        public PlayerState GetLocalPlayer()
        {
            return localPlayerID.HasValue ? GetPlayer(localPlayerID.Value) : null;
        }

        public List<PlayerState> GetAllPlayers()
        {
            return new List<PlayerState>(players.Values);
        }

        public DiplomacyStatus GetDiplomacyStatus(Guid playerID, Guid otherPlayerID)
        {
            var player = GetPlayer(playerID);
            if (player == null) return DiplomacyStatus.Neutral;
            return player.GetDiplomacyStatus(otherPlayerID);
        }

        // ================================================================
        // Building Management
        // ================================================================

        public void AddBuilding(BuildingData building)
        {
            buildings[building.id] = building;

            // Register with map data
            mapData.RegisterBuilding(building.id, building.coordinate, building.OccupiedCoordinates);

            // Update player ownership
            if (building.ownerID.HasValue)
            {
                var player = GetPlayer(building.ownerID.Value);
                if (player != null) player.AddOwnedBuilding(building.id);
            }
        }

        public void RemoveBuilding(Guid id)
        {
            BuildingData building;
            if (!buildings.TryGetValue(id, out building)) return;

            // Reassign home bases for armies that had this building as home
            ReassignHomeBasesForDestroyedBuilding(id, building.ownerID);

            // Update player ownership
            if (building.ownerID.HasValue)
            {
                var player = GetPlayer(building.ownerID.Value);
                if (player != null) player.RemoveOwnedBuilding(id);
            }

            // Unregister from map data
            mapData.UnregisterBuilding(id);

            buildings.Remove(id);
        }

        private void ReassignHomeBasesForDestroyedBuilding(Guid buildingID, Guid? ownerID)
        {
            if (!ownerID.HasValue) return;
            Guid ownerId = ownerID.Value;

            var evictedArmies = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (army.homeBaseID.HasValue && army.homeBaseID.Value == buildingID)
                    evictedArmies.Add(army);
            }
            if (evictedArmies.Count == 0) return;

            var validTypes = new HashSet<BuildingType> { BuildingType.WoodenFort, BuildingType.Castle };

            foreach (var army in evictedArmies)
            {
                var candidateBases = new List<BuildingData>();
                foreach (var b in buildings.Values)
                {
                    if (b.ownerID.HasValue && b.ownerID.Value == ownerId &&
                        validTypes.Contains(b.buildingType) &&
                        b.IsOperational && b.id != buildingID)
                    {
                        candidateBases.Add(b);
                    }
                }
                candidateBases.Sort((a, b) =>
                    army.coordinate.Distance(a.coordinate).CompareTo(army.coordinate.Distance(b.coordinate)));

                bool assigned = false;
                foreach (var homeBase in candidateBases)
                {
                    if (HasHomeBaseCapacity(homeBase.id))
                    {
                        army.homeBaseID = homeBase.id;
                        assigned = true;
                        break;
                    }
                }

                // Fallback: city center (unlimited capacity)
                if (!assigned)
                {
                    foreach (var b in buildings.Values)
                    {
                        if (b.ownerID.HasValue && b.ownerID.Value == ownerId &&
                            b.buildingType == BuildingType.CityCenter)
                        {
                            army.homeBaseID = b.id;
                            break;
                        }
                    }
                }
            }
        }

        // ================================================================
        // Home Base Capacity
        // ================================================================

        public int GetArmyCountForHomeBase(Guid buildingID)
        {
            int count = 0;
            foreach (var army in armies.Values)
            {
                if (army.homeBaseID.HasValue && army.homeBaseID.Value == buildingID) count++;
            }
            return count;
        }

        public List<ArmyData> GetArmiesForHomeBase(Guid buildingID)
        {
            var result = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (army.homeBaseID.HasValue && army.homeBaseID.Value == buildingID)
                    result.Add(army);
            }
            return result;
        }

        public bool HasHomeBaseCapacity(Guid buildingID)
        {
            BuildingData building;
            if (!buildings.TryGetValue(buildingID, out building)) return false;
            int? capacity = building.GetArmyHomeBaseCapacity();
            if (!capacity.HasValue) return true; // null = unlimited
            if (capacity.Value <= 0) return false; // 0 = not a home base
            return GetArmyCountForHomeBase(buildingID) < capacity.Value;
        }

        public BuildingData FindHomeBaseWithCapacity(Guid playerID, HexCoordinate fromCoordinate, Guid? excludingBuildingID = null)
        {
            var validTypes = new HashSet<BuildingType> { BuildingType.CityCenter, BuildingType.WoodenFort, BuildingType.Castle };
            var candidates = new List<BuildingData>();

            foreach (var b in buildings.Values)
            {
                if (b.ownerID.HasValue && b.ownerID.Value == playerID &&
                    validTypes.Contains(b.buildingType) &&
                    b.IsOperational &&
                    (!excludingBuildingID.HasValue || b.id != excludingBuildingID.Value))
                {
                    candidates.Add(b);
                }
            }
            candidates.Sort((a, b) =>
                fromCoordinate.Distance(a.coordinate).CompareTo(fromCoordinate.Distance(b.coordinate)));

            foreach (var homeBase in candidates)
            {
                if (HasHomeBaseCapacity(homeBase.id)) return homeBase;
            }
            return null;
        }

        public BuildingData FindNearestHomeBase(Guid playerID, HexCoordinate fromCoordinate, HexCoordinate? excludingCoordinate = null)
        {
            var validTypes = new HashSet<BuildingType> { BuildingType.CityCenter, BuildingType.WoodenFort, BuildingType.Castle };
            var candidates = new List<BuildingData>();

            foreach (var b in buildings.Values)
            {
                if (b.ownerID.HasValue && b.ownerID.Value == playerID &&
                    validTypes.Contains(b.buildingType) &&
                    b.IsOperational)
                {
                    if (excludingCoordinate.HasValue && b.OccupiedCoordinates.Contains(excludingCoordinate.Value))
                        continue;
                    candidates.Add(b);
                }
            }

            if (candidates.Count == 0) return null;

            BuildingData nearest = candidates[0];
            int nearestDist = fromCoordinate.Distance(nearest.coordinate);
            for (int i = 1; i < candidates.Count; i++)
            {
                int d = fromCoordinate.Distance(candidates[i].coordinate);
                if (d < nearestDist) { nearest = candidates[i]; nearestDist = d; }
            }
            return nearest;
        }

        public BuildingData GetBuilding(Guid id)
        {
            BuildingData building;
            return buildings.TryGetValue(id, out building) ? building : null;
        }

        public BuildingData GetBuilding(HexCoordinate coordinate)
        {
            Guid? buildingID = mapData.GetBuildingID(coordinate);
            if (!buildingID.HasValue) return null;
            return GetBuilding(buildingID.Value);
        }

        public List<BuildingData> GetBuildingsForPlayer(Guid playerID)
        {
            var result = new List<BuildingData>();
            foreach (var b in buildings.Values)
            {
                if (b.ownerID.HasValue && b.ownerID.Value == playerID) result.Add(b);
            }
            return result;
        }

        // ================================================================
        // Army Management
        // ================================================================

        public void AddArmy(ArmyData army)
        {
            armies[army.id] = army;
            mapData.RegisterArmy(army.id, army.coordinate);

            if (army.ownerID.HasValue)
            {
                var player = GetPlayer(army.ownerID.Value);
                if (player != null) player.AddOwnedArmy(army.id);
            }
        }

        public void RemoveArmy(Guid id)
        {
            ArmyData army;
            if (!armies.TryGetValue(id, out army)) return;

            if (army.ownerID.HasValue)
            {
                var player = GetPlayer(army.ownerID.Value);
                if (player != null) player.RemoveOwnedArmy(id);
            }

            mapData.UnregisterArmy(id);
            armies.Remove(id);
        }

        public ArmyData GetArmy(Guid id)
        {
            ArmyData army;
            return armies.TryGetValue(id, out army) ? army : null;
        }

        public ArmyData GetArmy(HexCoordinate coordinate)
        {
            Guid? armyID = mapData.GetArmyID(coordinate);
            if (!armyID.HasValue) return null;
            return GetArmy(armyID.Value);
        }

        public List<ArmyData> GetArmies(HexCoordinate coordinate)
        {
            var result = new List<ArmyData>();
            foreach (var id in mapData.GetArmyIDs(coordinate))
            {
                ArmyData army;
                if (armies.TryGetValue(id, out army)) result.Add(army);
            }
            return result;
        }

        public List<ArmyData> GetEntrenchedArmiesCovering(HexCoordinate coordinate)
        {
            var result = new List<ArmyData>();
            foreach (var neighbor in coordinate.Neighbors())
            {
                var armiesAtNeighbor = GetArmies(neighbor);
                foreach (var army in armiesAtNeighbor)
                {
                    if (army.isEntrenched && army.entrenchedCoveredTiles != null &&
                        army.entrenchedCoveredTiles.Contains(coordinate))
                    {
                        result.Add(army);
                    }
                }
            }
            return result;
        }

        public HashSet<HexCoordinate> ComputeEntrenchmentCoverage(ArmyData army)
        {
            var covered = new HashSet<HexCoordinate>();
            foreach (var neighbor in army.coordinate.Neighbors())
            {
                var enemyCoverage = GetEntrenchedArmiesCovering(neighbor);
                bool hasEnemy = false;
                foreach (var a in enemyCoverage)
                {
                    if (!a.ownerID.HasValue || !army.ownerID.HasValue ||
                        a.ownerID.Value != army.ownerID.Value)
                    {
                        hasEnemy = true;
                        break;
                    }
                }
                if (!hasEnemy) covered.Add(neighbor);
            }
            return covered;
        }

        public List<ArmyData> GetArmiesForPlayer(Guid playerID)
        {
            var result = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (army.ownerID.HasValue && army.ownerID.Value == playerID) result.Add(army);
            }
            return result;
        }

        public void UpdateArmyPosition(Guid armyID, HexCoordinate to)
        {
            ArmyData army;
            if (!armies.TryGetValue(armyID, out army)) return;
            army.coordinate = to;
            mapData.UpdateArmyPosition(armyID, to);
        }

        // ================================================================
        // Villager Group Management
        // ================================================================

        public void AddVillagerGroup(VillagerGroupData group)
        {
            villagerGroups[group.id] = group;
            mapData.RegisterVillagerGroup(group.id, group.coordinate);

            if (group.ownerID.HasValue)
            {
                var player = GetPlayer(group.ownerID.Value);
                if (player != null) player.AddOwnedVillagerGroup(group.id);
            }
        }

        public void RemoveVillagerGroup(Guid id)
        {
            VillagerGroupData group;
            if (!villagerGroups.TryGetValue(id, out group)) return;

            if (group.ownerID.HasValue)
            {
                var player = GetPlayer(group.ownerID.Value);
                if (player != null) player.RemoveOwnedVillagerGroup(id);
            }

            mapData.UnregisterVillagerGroup(id);
            villagerGroups.Remove(id);
        }

        public VillagerGroupData GetVillagerGroup(Guid id)
        {
            VillagerGroupData group;
            return villagerGroups.TryGetValue(id, out group) ? group : null;
        }

        public VillagerGroupData GetVillagerGroup(HexCoordinate coordinate)
        {
            Guid? groupID = mapData.GetVillagerGroupID(coordinate);
            if (!groupID.HasValue) return null;
            return GetVillagerGroup(groupID.Value);
        }

        public List<VillagerGroupData> GetVillagerGroups(HexCoordinate coordinate)
        {
            var result = new List<VillagerGroupData>();
            foreach (var id in mapData.GetVillagerGroupIDs(coordinate))
            {
                VillagerGroupData group;
                if (villagerGroups.TryGetValue(id, out group)) result.Add(group);
            }
            return result;
        }

        public List<VillagerGroupData> GetVillagerGroupsForPlayer(Guid playerID)
        {
            var result = new List<VillagerGroupData>();
            foreach (var group in villagerGroups.Values)
            {
                if (group.ownerID.HasValue && group.ownerID.Value == playerID) result.Add(group);
            }
            return result;
        }

        public void UpdateVillagerGroupPosition(Guid groupID, HexCoordinate to)
        {
            VillagerGroupData group;
            if (!villagerGroups.TryGetValue(groupID, out group)) return;
            group.coordinate = to;
            mapData.UpdateVillagerGroupPosition(groupID, to);
        }

        // ================================================================
        // Resource Point Management
        // ================================================================

        public void AddResourcePoint(ResourcePointData resourcePoint)
        {
            resourcePoints[resourcePoint.id] = resourcePoint;
            mapData.RegisterResourcePoint(resourcePoint.id, resourcePoint.coordinate);
        }

        public void RemoveResourcePoint(Guid id)
        {
            mapData.UnregisterResourcePoint(id);
            resourcePoints.Remove(id);
        }

        public ResourcePointData GetResourcePoint(Guid id)
        {
            ResourcePointData rp;
            return resourcePoints.TryGetValue(id, out rp) ? rp : null;
        }

        public ResourcePointData GetResourcePoint(HexCoordinate coordinate)
        {
            Guid? rpID = mapData.GetResourcePointID(coordinate);
            if (!rpID.HasValue) return null;
            return GetResourcePoint(rpID.Value);
        }

        public List<ResourcePointData> GetAllResourcePoints()
        {
            return new List<ResourcePointData>(resourcePoints.Values);
        }

        // ================================================================
        // Commander Management
        // ================================================================

        public void AddCommander(CommanderData commander)
        {
            commanders[commander.id] = commander;

            if (commander.ownerID.HasValue)
            {
                var player = GetPlayer(commander.ownerID.Value);
                if (player != null) player.AddOwnedCommander(commander.id);
            }
        }

        public void RemoveCommander(Guid id)
        {
            CommanderData commander;
            if (!commanders.TryGetValue(id, out commander)) return;

            if (commander.ownerID.HasValue)
            {
                var player = GetPlayer(commander.ownerID.Value);
                if (player != null) player.RemoveOwnedCommander(id);
            }

            commanders.Remove(id);
        }

        public CommanderData GetCommander(Guid id)
        {
            CommanderData commander;
            return commanders.TryGetValue(id, out commander) ? commander : null;
        }

        public List<CommanderData> GetCommandersForPlayer(Guid playerID)
        {
            var result = new List<CommanderData>();
            foreach (var commander in commanders.Values)
            {
                if (commander.ownerID.HasValue && commander.ownerID.Value == playerID)
                    result.Add(commander);
            }
            return result;
        }

        // ================================================================
        // Query Helpers
        // ================================================================

        public BuildingData GetBuildingAt(HexCoordinate coordinate) => GetBuilding(coordinate);
        public ArmyData GetArmyAt(HexCoordinate coordinate) => GetArmy(coordinate);
        public VillagerGroupData GetVillagerGroupAt(HexCoordinate coordinate) => GetVillagerGroup(coordinate);
        public ResourcePointData GetResourcePointAt(HexCoordinate coordinate) => GetResourcePoint(coordinate);

        public bool IsCoordinateEmpty(HexCoordinate coordinate)
        {
            return GetBuilding(coordinate) == null &&
                   GetArmy(coordinate) == null &&
                   GetVillagerGroup(coordinate) == null;
        }

        public List<ArmyData> GetEnemyArmiesInRange(HexCoordinate coordinate, int range, Guid playerID)
        {
            var result = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;
                if (army.coordinate.Distance(coordinate) <= range) result.Add(army);
            }
            return result;
        }

        public void GetPopulationStats(Guid playerID, out int current, out int capacity)
        {
            current = 0;
            capacity = 0;

            // Count villagers
            foreach (var group in GetVillagerGroupsForPlayer(playerID))
                current += group.villagerCount;

            // Count military units in armies (pop-space-aware)
            foreach (var army in GetArmiesForPlayer(playerID))
                current += army.GetPopulationUsed();

            // Count garrisoned units and calculate capacity
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.IsOperational)
                {
                    current += building.villagerGarrison;
                    current += building.GetGarrisonPopulation();

                    // Add training queue units (pop-space-aware)
                    foreach (var entry in building.trainingQueue)
                        current += entry.unitType.PopSpace() * entry.quantity;
                    foreach (var entry in building.villagerTrainingQueue)
                        current += entry.quantity;

                    // Add capacity from building type (scales with level)
                    capacity += building.buildingType.PopulationCapacityForLevel(building.level);
                }
            }
        }

        public int GetStorageCapacity(Guid playerID, ResourceType resourceType)
        {
            int capacity = 0;
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.IsOperational)
                {
                    int buildingCapacity = building.buildingType.StorageCapacityPerResource(building.level);
                    if (buildingCapacity > 0) capacity += buildingCapacity;
                }
            }
            return Math.Max(200, capacity);
        }

        public int GetCityCenterLevel(Guid playerID)
        {
            int maxLevel = 0;
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.buildingType == BuildingType.CityCenter &&
                    (building.state == BuildingState.Completed || building.state == BuildingState.Upgrading))
                {
                    if (building.level > maxLevel) maxLevel = building.level;
                }
            }
            return maxLevel;
        }

        // ================================================================
        // Building Protection
        // ================================================================

        public bool IsBuildingProtected(Guid buildingID)
        {
            return GetProtectingBuildings(buildingID).Count > 0;
        }

        public List<BuildingData> GetDefensiveBuildingsInRange(HexCoordinate coordinate, int range, Guid playerID)
        {
            var result = new List<BuildingData>();
            foreach (var building in buildings.Values)
            {
                if (!building.ownerID.HasValue || building.ownerID.Value != playerID) continue;
                if (!building.CanProvideGarrisonDefense) continue;
                if (!building.IsOperational) continue;

                int minDistance = int.MaxValue;
                foreach (var coord in building.OccupiedCoordinates)
                {
                    int d = coord.Distance(coordinate);
                    if (d < minDistance) minDistance = d;
                }
                if (minDistance <= range) result.Add(building);
            }
            return result;
        }

        public List<BuildingData> GetProtectingBuildings(Guid buildingID)
        {
            BuildingData targetBuilding;
            if (!buildings.TryGetValue(buildingID, out targetBuilding)) return new List<BuildingData>();
            if (!targetBuilding.ownerID.HasValue) return new List<BuildingData>();
            Guid ownerID = targetBuilding.ownerID.Value;

            var protectors = new List<BuildingData>();
            foreach (var building in buildings.Values)
            {
                if (building.id == buildingID) continue;
                if (!building.ownerID.HasValue || building.ownerID.Value != ownerID) continue;
                if (!building.CanProvideGarrisonDefense) continue;
                if (!building.IsOperational) continue;

                int defenseRange = building.GarrisonDefenseRange;
                bool isInRange = false;

                foreach (var defenderCoord in building.OccupiedCoordinates)
                {
                    foreach (var targetCoord in targetBuilding.OccupiedCoordinates)
                    {
                        if (defenderCoord.Distance(targetCoord) <= defenseRange)
                        {
                            isInRange = true;
                            break;
                        }
                    }
                    if (isInRange) break;
                }

                if (isInRange) protectors.Add(building);
            }
            return protectors;
        }

        // ================================================================
        // AI Query Helpers
        // ================================================================

        public List<PlayerState> GetAIPlayers()
        {
            var result = new List<PlayerState>();
            foreach (var player in players.Values)
            {
                if (player.isAI) result.Add(player);
            }
            return result;
        }

        public List<ResourcePointData> GetExploredResourcePoints(Guid playerID)
        {
            var player = GetPlayer(playerID);
            if (player == null) return new List<ResourcePointData>();

            var result = new List<ResourcePointData>();
            foreach (var rp in resourcePoints.Values)
            {
                if (player.IsExplored(rp.coordinate)) result.Add(rp);
            }
            return result;
        }

        public List<ResourcePointData> GetVisibleResourcePoints(Guid playerID)
        {
            var player = GetPlayer(playerID);
            if (player == null) return new List<ResourcePointData>();

            var result = new List<ResourcePointData>();
            foreach (var rp in resourcePoints.Values)
            {
                if (player.IsVisible(rp.coordinate)) result.Add(rp);
            }
            return result;
        }

        public HexCoordinate? FindNearestUnexploredCoordinate(HexCoordinate from, Guid playerID, int maxRange = 12)
        {
            var player = GetPlayer(playerID);
            if (player == null) return null;

            for (int distance = 1; distance <= maxRange; distance++)
            {
                var ring = from.CoordinatesInRing(distance);
                foreach (var coord in ring)
                {
                    if (!mapData.IsValidCoordinate(coord) || !mapData.IsWalkable(coord)) continue;
                    if (!player.IsExplored(coord)) return coord;
                }
            }
            return null;
        }

        public ArmyData GetNearestEnemyArmy(HexCoordinate from, Guid playerID)
        {
            ArmyData nearestArmy = null;
            int nearestDistance = int.MaxValue;

            foreach (var army in armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;

                var status = GetDiplomacyStatus(playerID, army.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                int distance = from.Distance(army.coordinate);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestArmy = army;
                }
            }
            return nearestArmy;
        }

        public List<ArmyData> GetEnemyArmies(HexCoordinate near, int range, Guid playerID)
        {
            var result = new List<ArmyData>();
            foreach (var army in armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;

                var status = GetDiplomacyStatus(playerID, army.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                if (army.coordinate.Distance(near) <= range) result.Add(army);
            }
            return result;
        }

        public List<BuildingData> GetUndefendedBuildings(Guid playerID)
        {
            var result = new List<BuildingData>();
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (!IsBuildingProtected(building.id) && building.IsOperational)
                    result.Add(building);
            }
            return result;
        }

        public double GetThreatLevel(HexCoordinate coordinate, Guid playerID, int sightRange = 10)
        {
            double threatLevel = 0.0;
            var nearbyEnemies = GetEnemyArmies(coordinate, sightRange, playerID);

            foreach (var enemy in nearbyEnemies)
            {
                int distance = Math.Max(1, coordinate.Distance(enemy.coordinate));
                double armyStrength = enemy.GetTotalUnits();
                threatLevel += armyStrength / distance;
            }
            return threatLevel;
        }

        public int GetMilitaryStrength(Guid playerID)
        {
            int strength = 0;
            foreach (var army in GetArmiesForPlayer(playerID))
                strength += army.GetTotalUnits();
            foreach (var building in GetBuildingsForPlayer(playerID))
                strength += building.GetTotalGarrisonedUnits();
            return strength;
        }

        public int GetVillagerCount(Guid playerID)
        {
            int count = 0;
            foreach (var group in GetVillagerGroupsForPlayer(playerID))
                count += group.villagerCount;
            foreach (var building in GetBuildingsForPlayer(playerID))
                count += building.villagerGarrison;
            return count;
        }

        public List<BuildingData> GetVisibleEnemyBuildings(Guid playerID)
        {
            var player = GetPlayer(playerID);
            if (player == null) return new List<BuildingData>();

            var result = new List<BuildingData>();
            foreach (var building in buildings.Values)
            {
                if (!building.ownerID.HasValue || building.ownerID.Value == playerID) continue;

                var status = GetDiplomacyStatus(playerID, building.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                bool isVisible = false;
                foreach (var coord in building.OccupiedCoordinates)
                {
                    if (player.IsVisible(coord)) { isVisible = true; break; }
                }
                if (isVisible) result.Add(building);
            }
            return result;
        }

        public BuildingData GetCityCenter(Guid playerID)
        {
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.buildingType == BuildingType.CityCenter && building.IsOperational)
                    return building;
            }
            return null;
        }

        public List<ResourcePointData> GetAvailableResourcePoints(HexCoordinate near, int range, Guid playerID)
        {
            var result = new List<ResourcePointData>();
            foreach (var rp in resourcePoints.Values)
            {
                if (rp.coordinate.Distance(near) > range) continue;
                if (rp.remainingAmount <= 0) continue;

                // Check if there's already a camp built here
                Guid? buildingID = mapData.GetBuildingID(rp.coordinate);
                if (buildingID.HasValue)
                {
                    var building = GetBuilding(buildingID.Value);
                    if (building != null && building.ownerID.HasValue && building.ownerID.Value == playerID)
                        continue;
                }
                result.Add(rp);
            }
            return result;
        }

        public bool HasBuilding(BuildingType type, HexCoordinate coordinate, Guid playerID)
        {
            Guid? buildingID = mapData.GetBuildingID(coordinate);
            if (!buildingID.HasValue) return false;
            var building = GetBuilding(buildingID.Value);
            if (building == null) return false;
            return building.buildingType == type && building.ownerID.HasValue && building.ownerID.Value == playerID;
        }

        public int GetBuildingCount(BuildingType type, Guid playerID)
        {
            int count = 0;
            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.buildingType == type && building.IsOperational) count++;
            }
            return count;
        }

        public HexCoordinate? FindBuildLocation(HexCoordinate target, int maxDistance, Guid playerID)
        {
            if (CanBuildAt(target, playerID)) return target;

            for (int distance = 1; distance <= maxDistance; distance++)
            {
                var candidates = target.CoordinatesInRing(distance);
                var valid = new List<HexCoordinate>();
                foreach (var coord in candidates)
                {
                    if (CanBuildAt(coord, playerID)) valid.Add(coord);
                }
                if (valid.Count > 0)
                {
                    // Shuffle to avoid always picking the same tile
                    var rng = new System.Random();
                    int idx = rng.Next(valid.Count);
                    return valid[idx];
                }
            }
            return null;
        }

        public bool CanBuildAt(HexCoordinate coordinate, Guid playerID)
        {
            if (!mapData.IsValidCoordinate(coordinate)) return false;
            if (!mapData.IsWalkable(coordinate)) return false;
            if (mapData.GetBuildingID(coordinate).HasValue) return false;
            if (mapData.GetArmyID(coordinate).HasValue) return false;
            if (mapData.GetVillagerGroupID(coordinate).HasValue) return false;
            return true;
        }

        // ================================================================
        // Composition Analysis
        // ================================================================

        public struct EnemyCompositionAnalysis
        {
            public double cavalryRatio;
            public double rangedRatio;
            public double infantryRatio;
            public double siegeRatio;
            public int totalStrength;
            public double weightedStrength;
        }

        public EnemyCompositionAnalysis? AnalyzeEnemyComposition(Guid playerID)
        {
            int totalCavalry = 0;
            int totalRanged = 0;
            int totalInfantry = 0;
            int totalSiege = 0;
            double totalWeightedStrength = 0.0;

            foreach (var army in armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;

                var status = GetDiplomacyStatus(playerID, army.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                totalCavalry += army.GetUnitCountByCategory(UnitCategory.Cavalry);
                totalRanged += army.GetUnitCountByCategory(UnitCategory.Ranged);
                totalInfantry += army.GetUnitCountByCategory(UnitCategory.Infantry);
                totalSiege += army.GetUnitCountByCategory(UnitCategory.Siege);
                totalWeightedStrength += army.GetWeightedStrength();
            }

            // Also count garrisoned units in enemy buildings
            foreach (var building in buildings.Values)
            {
                if (!building.ownerID.HasValue || building.ownerID.Value == playerID) continue;

                var status = GetDiplomacyStatus(playerID, building.ownerID.Value);
                if (status != DiplomacyStatus.Enemy) continue;

                foreach (var kvp in building.garrison)
                {
                    switch (kvp.Key.Category())
                    {
                        case UnitCategory.Cavalry: totalCavalry += kvp.Value; break;
                        case UnitCategory.Ranged: totalRanged += kvp.Value; break;
                        case UnitCategory.Infantry: totalInfantry += kvp.Value; break;
                        case UnitCategory.Siege: totalSiege += kvp.Value; break;
                    }
                }
            }

            int totalUnits = totalCavalry + totalRanged + totalInfantry + totalSiege;
            if (totalUnits == 0) return null;

            double total = totalUnits;
            return new EnemyCompositionAnalysis
            {
                cavalryRatio = totalCavalry / total,
                rangedRatio = totalRanged / total,
                infantryRatio = totalInfantry / total,
                siegeRatio = totalSiege / total,
                totalStrength = totalUnits,
                weightedStrength = totalWeightedStrength
            };
        }

        public double GetWeightedMilitaryStrength(Guid playerID)
        {
            double strength = 0.0;

            foreach (var army in GetArmiesForPlayer(playerID))
                strength += army.GetWeightedStrength();

            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                foreach (var kvp in building.garrison)
                {
                    double hp = kvp.Key.HP();
                    double damage = kvp.Key.CombatStats().TotalDamage;
                    strength += kvp.Value * (hp * (1.0 + damage * 0.1));
                }
            }
            return strength;
        }

        public bool IsArmyLocallyOutnumbered(ArmyData army, Guid playerID)
        {
            double armyStrength = army.GetWeightedStrength();
            var nearbyEnemies = GetEnemyArmies(army.coordinate, 3, playerID);

            double enemyStrength = 0.0;
            foreach (var enemy in nearbyEnemies)
                enemyStrength += enemy.GetWeightedStrength();

            return enemyStrength > armyStrength * 1.5;
        }

        // ================================================================
        // Food Consumption
        // ================================================================

        public struct FoodConsumptionInfo
        {
            public int civilian;
            public int military;
            public double rate;
        }

        public FoodConsumptionInfo GetFoodConsumptionRate(Guid playerID)
        {
            int civilianCount = 0;
            int militaryCount = 0;

            foreach (var group in GetVillagerGroupsForPlayer(playerID))
                civilianCount += group.villagerCount;

            foreach (var army in GetArmiesForPlayer(playerID))
                militaryCount += army.GetPopulationUsed();

            foreach (var building in GetBuildingsForPlayer(playerID))
            {
                if (building.IsOperational)
                {
                    civilianCount += building.villagerGarrison;
                    militaryCount += building.GetGarrisonPopulation();

                    foreach (var entry in building.trainingQueue)
                        militaryCount += entry.unitType.PopSpace() * entry.quantity;
                    foreach (var entry in building.villagerTrainingQueue)
                        civilianCount += entry.quantity;
                }
            }

            double baseRate = 0.1;
            double totalRate = (civilianCount + militaryCount) * baseRate;

            return new FoodConsumptionInfo
            {
                civilian = civilianCount,
                military = militaryCount,
                rate = totalRate
            };
        }
    }

    // ================================================================
    // Game State Snapshot
    // ================================================================

    [System.Serializable]
    public class GameStateSnapshot
    {
        public double timestamp;
        public int mapWidth;
        public int mapHeight;
        public List<PlayerState> players;
        public List<BuildingData> buildings;
        public List<ArmyData> armies;
        public List<VillagerGroupData> villagerGroups;
        public List<ResourcePointData> resourcePoints;
        public List<CommanderData> commanders;
        public Guid? localPlayerID;

        public GameStateSnapshot(GameState gameState)
        {
            this.timestamp = gameState.currentTime;
            this.mapWidth = gameState.mapData.width;
            this.mapHeight = gameState.mapData.height;
            this.players = new List<PlayerState>(gameState.players.Values);
            this.buildings = new List<BuildingData>(gameState.buildings.Values);
            this.armies = new List<ArmyData>(gameState.armies.Values);
            this.villagerGroups = new List<VillagerGroupData>(gameState.villagerGroups.Values);
            this.resourcePoints = new List<ResourcePointData>(gameState.resourcePoints.Values);
            this.commanders = new List<CommanderData>(gameState.commanders.Values);
            this.localPlayerID = gameState.localPlayerID;
        }

        public GameState Restore()
        {
            var gameState = new GameState(mapWidth, mapHeight);
            gameState.currentTime = timestamp;
            gameState.localPlayerID = localPlayerID;

            foreach (var player in players) gameState.AddPlayer(player);
            foreach (var rp in resourcePoints) gameState.AddResourcePoint(rp);
            foreach (var building in buildings) gameState.AddBuilding(building);
            foreach (var army in armies) gameState.AddArmy(army);
            foreach (var group in villagerGroups) gameState.AddVillagerGroup(group);
            foreach (var commander in commanders) gameState.AddCommander(commander);

            return gameState;
        }
    }
}
