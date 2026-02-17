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

        // Coordinate tracking
        public Dictionary<Guid, HexCoordinate> buildingCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> armyCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> villagerGroupCoordinates = new Dictionary<Guid, HexCoordinate>();
        public Dictionary<Guid, HexCoordinate> resourcePointCoordinates = new Dictionary<Guid, HexCoordinate>();

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

        public void RegisterArmy(Guid id, HexCoordinate coordinate)
        {
            armyIDs.Add(id);
            armyCoordinates[id] = coordinate;
        }

        public void UnregisterArmy(Guid id)
        {
            armyIDs.Remove(id);
            armyCoordinates.Remove(id);
        }

        public void UpdateArmyPosition(Guid id, HexCoordinate coordinate)
        {
            armyCoordinates[id] = coordinate;
        }

        public Guid? GetArmyID(HexCoordinate coordinate)
        {
            foreach (var kvp in armyCoordinates)
            {
                if (kvp.Value == coordinate) return kvp.Key;
            }
            return null;
        }

        public List<Guid> GetArmyIDs(HexCoordinate coordinate)
        {
            var result = new List<Guid>();
            foreach (var kvp in armyCoordinates)
            {
                if (kvp.Value == coordinate) result.Add(kvp.Key);
            }
            return result;
        }

        public HexCoordinate? GetArmyCoordinate(Guid id)
        {
            HexCoordinate coord;
            return armyCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Villager Group Management

        public void RegisterVillagerGroup(Guid id, HexCoordinate coordinate)
        {
            villagerGroupIDs.Add(id);
            villagerGroupCoordinates[id] = coordinate;
        }

        public void UnregisterVillagerGroup(Guid id)
        {
            villagerGroupIDs.Remove(id);
            villagerGroupCoordinates.Remove(id);
        }

        public void UpdateVillagerGroupPosition(Guid id, HexCoordinate coordinate)
        {
            villagerGroupCoordinates[id] = coordinate;
        }

        public Guid? GetVillagerGroupID(HexCoordinate coordinate)
        {
            foreach (var kvp in villagerGroupCoordinates)
            {
                if (kvp.Value == coordinate) return kvp.Key;
            }
            return null;
        }

        public List<Guid> GetVillagerGroupIDs(HexCoordinate coordinate)
        {
            var result = new List<Guid>();
            foreach (var kvp in villagerGroupCoordinates)
            {
                if (kvp.Value == coordinate) result.Add(kvp.Key);
            }
            return result;
        }

        public HexCoordinate? GetVillagerGroupCoordinate(Guid id)
        {
            HexCoordinate coord;
            return villagerGroupCoordinates.TryGetValue(id, out coord) ? (HexCoordinate?)coord : null;
        }

        // Entity Stacking

        public int GetEntityCount(HexCoordinate coordinate)
        {
            int count = 0;
            foreach (var kvp in armyCoordinates)
            {
                if (kvp.Value == coordinate) count++;
            }
            foreach (var kvp in villagerGroupCoordinates)
            {
                if (kvp.Value == coordinate) count++;
            }
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

        public List<HexCoordinate> FindPath(HexCoordinate start, HexCoordinate goal, Guid? playerID,
            GameState gameState, bool allowImpassableDestination = false,
            HashSet<HexCoordinate> targetBuildingCoordinates = null)
        {
            if (!IsValidCoordinate(start) || !IsValidCoordinate(goal)) return null;

            bool destinationPassable = IsPassable(goal, playerID, gameState);
            if (!destinationPassable && !allowImpassableDestination) return null;

            if (start == goal) return new List<HexCoordinate>();

            var openSet = new HashSet<HexCoordinate> { start };
            var cameFrom = new Dictionary<HexCoordinate, HexCoordinate>();
            var gScore = new Dictionary<HexCoordinate, int> { { start, 0 } };
            var fScore = new Dictionary<HexCoordinate, int> { { start, start.Distance(goal) } };

            while (openSet.Count > 0)
            {
                HexCoordinate current = default;
                int bestF = int.MaxValue;
                foreach (var c in openSet)
                {
                    int f = fScore.ContainsKey(c) ? fScore[c] : int.MaxValue;
                    if (f < bestF) { bestF = f; current = c; }
                }

                if (current == goal)
                {
                    var path = new List<HexCoordinate>();
                    var node = goal;
                    while (node != start)
                    {
                        path.Add(node);
                        if (!cameFrom.ContainsKey(node)) break;
                        node = cameFrom[node];
                    }
                    path.Reverse();
                    return path;
                }

                openSet.Remove(current);

                foreach (var neighbor in current.Neighbors())
                {
                    bool neighborPassable = IsPassable(neighbor, playerID, gameState);
                    bool isGoalTile = neighbor == goal && allowImpassableDestination;
                    bool isTargetBuildingTile = targetBuildingCoordinates != null && targetBuildingCoordinates.Contains(neighbor);
                    if (!IsValidCoordinate(neighbor) || (!neighborPassable && !isGoalTile && !isTargetBuildingTile))
                        continue;

                    int moveCost = GetMovementCost(neighbor);
                    int currentG = gScore.ContainsKey(current) ? gScore[current] : int.MaxValue;
                    if (currentG == int.MaxValue) continue;
                    int tentativeG = currentG + moveCost;
                    int neighborG = gScore.ContainsKey(neighbor) ? gScore[neighbor] : int.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + neighbor.Distance(goal);
                        openSet.Add(neighbor);
                    }
                }
            }

            return null;
        }

        public HexCoordinate? FindNearestWalkable(HexCoordinate target, int maxDistance, Guid? playerID, GameState gameState)
        {
            if (IsPassable(target, playerID, gameState) &&
                GetEntityCount(target) < GameConfig.Stacking.MaxEntitiesPerTile &&
                !GetBuildingID(target).HasValue)
            {
                return target;
            }

            for (int distance = 1; distance <= maxDistance; distance++)
            {
                var candidates = new List<HexCoordinate>();
                foreach (var kvp in tiles)
                {
                    if (kvp.Key.Distance(target) == distance &&
                        IsPassable(kvp.Key, playerID, gameState) &&
                        GetEntityCount(kvp.Key) < GameConfig.Stacking.MaxEntitiesPerTile &&
                        !GetBuildingID(kvp.Key).HasValue)
                    {
                        candidates.Add(kvp.Key);
                    }
                }

                if (candidates.Count > 0)
                {
                    HexCoordinate best = candidates[0];
                    int bestDist = best.Distance(target);
                    for (int i = 1; i < candidates.Count; i++)
                    {
                        int d = candidates[i].Distance(target);
                        if (d < bestDist) { best = candidates[i]; bestDist = d; }
                    }
                    return best;
                }
            }

            return null;
        }
    }
}
