using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public struct TileData
    {
        public HexCoordinate coordinate;
        public TerrainType terrain;
        public int elevation;

        public TileData(HexCoordinate coordinate, TerrainType terrain, int elevation = 0)
        {
            this.coordinate = coordinate;
            this.terrain = terrain;
            this.elevation = elevation;
        }
    }

    [System.Serializable]
    public class MapData
    {
        public int width;
        public int height;
        public Dictionary<HexCoordinate, TileData> tiles = new Dictionary<HexCoordinate, TileData>();

        // Entity ID sets
        public HashSet<Guid> buildingIDs = new HashSet<Guid>();
        public HashSet<Guid> armyIDs = new HashSet<Guid>();
        public HashSet<Guid> villagerGroupIDs = new HashSet<Guid>();
        public HashSet<Guid> resourcePointIDs = new HashSet<Guid>();
        public HashSet<Guid> scoutIDs = new HashSet<Guid>();

        // Coordinate tracking
        public Dictionary<Guid, HexCoordinate> buildingCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> armyCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> villagerGroupCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> resourcePointCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> scoutCoordinates = new Dictionary<Guid, HexCoordinate>();

        // Multi-tile building support
        public Dictionary<HexCoordinate, Guid> occupiedCoordinates = new Dictionary<HexCoordinate, Guid>();

        public MapData(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        // Tile Management

        public void SetTile(TileData tile) { tiles[tile.coordinate] = tile; }

        public TileData? GetTile(HexCoordinate coordinate)
        {
            TileData tile;
            return tiles.TryGetValue(coordinate, out tile) ? (TileData?)tile : null;
        }

        public TerrainType? GetTerrain(HexCoordinate coordinate)
        {
            var tile = GetTile(coordinate);
            return tile.HasValue ? (TerrainType?)tile.Value.terrain : null;
        }

        public bool IsValidCoordinate(HexCoordinate coord)
        {
            return coord.q >= 0 && coord.q < width && coord.r >= 0 && coord.r < height;
        }

        public bool IsWalkable(HexCoordinate coord)
        {
            var tile = GetTile(coord);
            return tile.HasValue && tile.Value.terrain.IsWalkable();
        }

        // Building Management

        public void RegisterBuilding(Guid id, HexCoordinate coordinate, List<HexCoordinate> occupiedCoords)
        {
            buildingIDs.Add(id);
            buildingCoordinates[id] = coordinate;
            foreach (var coord in occupiedCoords)
                occupiedCoordinates[coord] = id;
        }

        public void UnregisterBuilding(Guid id)
        {
            buildingIDs.Remove(id);
            buildingCoordinates.Remove(id);
            var toRemove = new List<HexCoordinate>();
            foreach (var kvp in occupiedCoordinates)
            {
                if (kvp.Value == id) toRemove.Add(kvp.Key);
            }
            foreach (var coord in toRemove) occupiedCoordinates.Remove(coord);
        }

        public Guid? GetBuildingID(HexCoordinate coordinate)
        {
            Guid id;
            return occupiedCoordinates.TryGetValue(coordinate, out id) ? (Guid?)id : null;
        }

        public HexCoordinate? GetBuildingCoordinate(Guid id)
        {
            HexCoordinate coord;
            return buildingCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Army Management

        // Reverse lookup: coordinate → army IDs (O(1) instead of O(N) scan)
        private Dictionary<HexCoordinate, HashSet<Guid>> coordinateToArmies = new Dictionary<HexCoordinate, HashSet<Guid>>();
        private static readonly HashSet<Guid> EmptyGuidSet = new HashSet<Guid>();

        public void RegisterArmy(Guid id, HexCoordinate coordinate)
        {
            armyIDs.Add(id);
            armyCoordinates[id] = coordinate;
            AddToCoordinateSet(coordinateToArmies, coordinate, id);
        }

        public void UnregisterArmy(Guid id)
        {
            HexCoordinate coord;
            if (armyCoordinates.TryGetValue(id, out coord))
                RemoveFromCoordinateSet(coordinateToArmies, coord, id);
            armyIDs.Remove(id);
            armyCoordinates.Remove(id);
        }

        public void UpdateArmyPosition(Guid id, HexCoordinate coordinate)
        {
            HexCoordinate oldCoord;
            if (armyCoordinates.TryGetValue(id, out oldCoord))
                RemoveFromCoordinateSet(coordinateToArmies, oldCoord, id);
            armyCoordinates[id] = coordinate;
            AddToCoordinateSet(coordinateToArmies, coordinate, id);
        }

        public Guid? GetArmyID(HexCoordinate coordinate)
        {
            HashSet<Guid> set;
            if (coordinateToArmies.TryGetValue(coordinate, out set))
            {
                foreach (var id in set) return id;
            }
            return null;
        }

        public IReadOnlyCollection<Guid> GetArmyIDs(HexCoordinate coordinate)
        {
            HashSet<Guid> set;
            if (coordinateToArmies.TryGetValue(coordinate, out set))
                return set;
            return EmptyGuidSet;
        }

        public HexCoordinate? GetArmyCoordinate(Guid id)
        {
            HexCoordinate coord;
            return armyCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Villager Group Management

        // Reverse lookup: coordinate → villager group IDs (O(1) instead of O(N) scan)
        private Dictionary<HexCoordinate, HashSet<Guid>> coordinateToVillagerGroups = new Dictionary<HexCoordinate, HashSet<Guid>>();

        public void RegisterVillagerGroup(Guid id, HexCoordinate coordinate)
        {
            villagerGroupIDs.Add(id);
            villagerGroupCoordinates[id] = coordinate;
            AddToCoordinateSet(coordinateToVillagerGroups, coordinate, id);
        }

        public void UnregisterVillagerGroup(Guid id)
        {
            HexCoordinate coord;
            if (villagerGroupCoordinates.TryGetValue(id, out coord))
                RemoveFromCoordinateSet(coordinateToVillagerGroups, coord, id);
            villagerGroupIDs.Remove(id);
            villagerGroupCoordinates.Remove(id);
        }

        public void UpdateVillagerGroupPosition(Guid id, HexCoordinate coordinate)
        {
            HexCoordinate oldCoord;
            if (villagerGroupCoordinates.TryGetValue(id, out oldCoord))
                RemoveFromCoordinateSet(coordinateToVillagerGroups, oldCoord, id);
            villagerGroupCoordinates[id] = coordinate;
            AddToCoordinateSet(coordinateToVillagerGroups, coordinate, id);
        }

        public Guid? GetVillagerGroupID(HexCoordinate coordinate)
        {
            HashSet<Guid> set;
            if (coordinateToVillagerGroups.TryGetValue(coordinate, out set))
            {
                foreach (var id in set) return id;
            }
            return null;
        }

        public IReadOnlyCollection<Guid> GetVillagerGroupIDs(HexCoordinate coordinate)
        {
            HashSet<Guid> set;
            if (coordinateToVillagerGroups.TryGetValue(coordinate, out set))
                return set;
            return EmptyGuidSet;
        }

        public HexCoordinate? GetVillagerGroupCoordinate(Guid id)
        {
            HexCoordinate coord;
            return villagerGroupCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Scout Management

        // Reverse lookup: coordinate → scout ID (O(1) instead of O(N) scan)
        private Dictionary<HexCoordinate, Guid> coordinateToScout = new Dictionary<HexCoordinate, Guid>();

        public void RegisterScout(Guid id, HexCoordinate coordinate)
        {
            scoutIDs.Add(id);
            scoutCoordinates[id] = coordinate;
            coordinateToScout[coordinate] = id;
        }

        public void UnregisterScout(Guid id)
        {
            HexCoordinate coord;
            if (scoutCoordinates.TryGetValue(id, out coord))
                coordinateToScout.Remove(coord);
            scoutIDs.Remove(id);
            scoutCoordinates.Remove(id);
        }

        public void UpdateScoutPosition(Guid id, HexCoordinate coordinate)
        {
            HexCoordinate oldCoord;
            if (scoutCoordinates.TryGetValue(id, out oldCoord))
                coordinateToScout.Remove(oldCoord);
            scoutCoordinates[id] = coordinate;
            coordinateToScout[coordinate] = id;
        }

        public Guid? GetScoutID(HexCoordinate coordinate)
        {
            Guid id;
            if (coordinateToScout.TryGetValue(coordinate, out id))
                return id;
            return null;
        }

        // Entity Stacking

        public int GetEntityCount(HexCoordinate coordinate)
        {
            int count = 0;
            HashSet<Guid> set;
            if (coordinateToArmies.TryGetValue(coordinate, out set))
                count += set.Count;
            if (coordinateToVillagerGroups.TryGetValue(coordinate, out set))
                count += set.Count;
            return count;
        }

        // Resource Point Management

        public void RegisterResourcePoint(Guid id, HexCoordinate coordinate)
        {
            resourcePointIDs.Add(id);
            resourcePointCoordinates[id] = coordinate;
        }

        public void UnregisterResourcePoint(Guid id)
        {
            resourcePointIDs.Remove(id);
            resourcePointCoordinates.Remove(id);
        }

        public Guid? GetResourcePointID(HexCoordinate coordinate)
        {
            foreach (var kvp in resourcePointCoordinates)
            {
                if (kvp.Value == coordinate) return kvp.Key;
            }
            return null;
        }

        public HexCoordinate? GetResourcePointCoordinate(Guid id)
        {
            HexCoordinate coord;
            return resourcePointCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Passability

        public bool IsPassable(HexCoordinate coord, Guid? playerID, GameState gameState)
        {
            if (!IsWalkable(coord)) return false;

            var buildingID = GetBuildingID(coord);
            if (buildingID.HasValue)
            {
                var building = gameState.GetBuilding(buildingID.Value);
                if (building != null && building.state == BuildingState.Completed)
                {
                    switch (building.buildingType)
                    {
                        case BuildingType.Wall:
                            return false;
                        case BuildingType.Castle:
                        case BuildingType.WoodenFort:
                        case BuildingType.Gate:
                            if (!building.ownerID.HasValue || !playerID.HasValue) return false;
                            var status = gameState.GetDiplomacyStatus(playerID.Value, building.ownerID.Value);
                            return status.CanMove();
                    }
                }
            }

            if (playerID.HasValue)
            {
                var armiesAtCoord = gameState.GetArmies(coord);
                foreach (var army in armiesAtCoord)
                {
                    if (army.isEntrenched && army.ownerID.HasValue && army.ownerID.Value != playerID.Value)
                    {
                        var status = gameState.GetDiplomacyStatus(playerID.Value, army.ownerID.Value);
                        if (!status.CanMove()) return false;
                    }
                }
            }

            return true;
        }

        public int GetMovementCost(HexCoordinate coordinate)
        {
            if (GetBuildingID(coordinate).HasValue) return 1;
            var tile = GetTile(coordinate);
            return tile.HasValue ? tile.Value.terrain.MovementCost() : 3;
        }

        // Pathfinding (A*)

        // Reusable pathfinding collections (cleared per call, avoids allocation)
        private Dictionary<HexCoordinate, HexCoordinate> _pfCameFrom = new Dictionary<HexCoordinate, HexCoordinate>();
        private Dictionary<HexCoordinate, int> _pfGScore = new Dictionary<HexCoordinate, int>();
        private HashSet<HexCoordinate> _pfClosedSet = new HashSet<HexCoordinate>();
        private List<(HexCoordinate coord, int priority)> _pfHeap = new List<(HexCoordinate, int)>();

        public List<HexCoordinate> FindPath(HexCoordinate start, HexCoordinate goal, Guid? playerID,
            GameState gameState, bool allowImpassableDestination = false,
            HashSet<HexCoordinate> targetBuildingCoordinates = null)
        {
            if (!IsValidCoordinate(start) || !IsValidCoordinate(goal)) return null;

            bool destinationPassable = IsPassable(goal, playerID, gameState);
            if (!destinationPassable && !allowImpassableDestination) return null;

            if (start == goal) return new List<HexCoordinate>();

            _pfCameFrom.Clear();
            _pfGScore.Clear();
            _pfClosedSet.Clear();
            _pfHeap.Clear();

            _pfGScore[start] = 0;
            HeapPush(_pfHeap, start, start.Distance(goal));

            while (_pfHeap.Count > 0)
            {
                var (current, _) = HeapPop(_pfHeap);

                if (current == goal)
                {
                    var path = new List<HexCoordinate>();
                    var node = goal;
                    while (node != start)
                    {
                        path.Add(node);
                        if (!_pfCameFrom.ContainsKey(node)) break;
                        node = _pfCameFrom[node];
                    }
                    path.Reverse();
                    return path;
                }

                if (!_pfClosedSet.Add(current)) continue; // Skip if already processed

                int currentG;
                if (!_pfGScore.TryGetValue(current, out currentG)) continue;

                foreach (var neighbor in current.Neighbors())
                {
                    if (_pfClosedSet.Contains(neighbor)) continue;

                    bool neighborPassable = IsPassable(neighbor, playerID, gameState);
                    bool isGoalTile = neighbor == goal && allowImpassableDestination;
                    bool isTargetBuildingTile = targetBuildingCoordinates != null && targetBuildingCoordinates.Contains(neighbor);
                    if (!IsValidCoordinate(neighbor) || (!neighborPassable && !isGoalTile && !isTargetBuildingTile))
                        continue;

                    int moveCost = GetMovementCost(neighbor);
                    int tentativeG = currentG + moveCost;
                    int neighborG;
                    if (!_pfGScore.TryGetValue(neighbor, out neighborG))
                        neighborG = int.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        _pfCameFrom[neighbor] = current;
                        _pfGScore[neighbor] = tentativeG;
                        HeapPush(_pfHeap, neighbor, tentativeG + neighbor.Distance(goal));
                    }
                }
            }

            return null;
        }

        // Binary min-heap operations (inline to avoid class overhead)
        private static void HeapPush(List<(HexCoordinate coord, int priority)> heap, HexCoordinate coord, int priority)
        {
            heap.Add((coord, priority));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].priority <= heap[i].priority) break;
                var tmp = heap[parent];
                heap[parent] = heap[i];
                heap[i] = tmp;
                i = parent;
            }
        }

        private static (HexCoordinate coord, int priority) HeapPop(List<(HexCoordinate coord, int priority)> heap)
        {
            var min = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            last--;
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int smallest = i;
                if (left <= last && heap[left].priority < heap[smallest].priority) smallest = left;
                if (right <= last && heap[right].priority < heap[smallest].priority) smallest = right;
                if (smallest == i) break;
                var tmp = heap[smallest];
                heap[smallest] = heap[i];
                heap[i] = tmp;
                i = smallest;
            }
            return min;
        }

        public HexCoordinate? FindNearestWalkable(HexCoordinate target, int maxDistance, Guid? playerID, GameState gameState)
        {
            if (IsPassable(target, playerID, gameState) &&
                GetEntityCount(target) < GameConfig.Stacking.MaxEntitiesPerTile &&
                !GetBuildingID(target).HasValue)
            {
                return target;
            }

            // Use CoordinatesInRing to check only hexes at each distance,
            // instead of scanning the entire tiles dictionary per ring
            for (int distance = 1; distance <= maxDistance; distance++)
            {
                HexCoordinate? best = null;
                int bestDist = int.MaxValue;

                foreach (var coord in target.CoordinatesInRing(distance))
                {
                    if (!IsValidCoordinate(coord)) continue;
                    if (!IsPassable(coord, playerID, gameState)) continue;
                    if (GetEntityCount(coord) >= GameConfig.Stacking.MaxEntitiesPerTile) continue;
                    if (GetBuildingID(coord).HasValue) continue;

                    int d = coord.Distance(target);
                    if (d < bestDist) { best = coord; bestDist = d; }
                }

                if (best.HasValue) return best;
            }

            return null;
        }

        // Shared helpers for coordinate reverse lookups
        private static void AddToCoordinateSet(Dictionary<HexCoordinate, HashSet<Guid>> dict, HexCoordinate coord, Guid id)
        {
            HashSet<Guid> set;
            if (!dict.TryGetValue(coord, out set))
            {
                set = new HashSet<Guid>();
                dict[coord] = set;
            }
            set.Add(id);
        }

        private static void RemoveFromCoordinateSet(Dictionary<HexCoordinate, HashSet<Guid>> dict, HexCoordinate coord, Guid id)
        {
            HashSet<Guid> set;
            if (dict.TryGetValue(coord, out set))
            {
                set.Remove(id);
                if (set.Count == 0) dict.Remove(coord);
            }
        }
    }
}
