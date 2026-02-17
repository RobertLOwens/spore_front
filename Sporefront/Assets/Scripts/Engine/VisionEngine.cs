// ============================================================================
// FILE: Engine/VisionEngine.cs
// PURPOSE: Handles fog of war and vision logic
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    [System.Serializable]
    public struct VisionRangeData
    {
        public int baseRange;
        public BuildingType? buildingType;
        public string entityType;

        public VisionRangeData(int baseRange, BuildingType? buildingType = null, string entityType = null)
        {
            this.baseRange = baseRange;
            this.buildingType = buildingType;
            this.entityType = entityType;
        }
    }

    public class VisionEngine
    {
        // State
        private GameState gameState;

        // Vision Ranges
        private int baseUnitVisionRange = GameConfig.Vision.BaseUnitRange;
        private int baseVillagerVisionRange = GameConfig.Vision.BaseVillagerRange;
        private Dictionary<BuildingType, int> buildingVisionRanges = GameConfig.Vision.BuildingRanges;

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

            // Update vision for all players
            foreach (var player in gameState.players.Values)
            {
                var visibleCoords = CalculateVisibleCoordinates(player.id, gameState);
                var previousVisible = new HashSet<HexCoordinate>(player.visibleCoordinates);

                player.SetVisibleCoordinates(visibleCoords);

                // Generate changes for newly visible/hidden coordinates
                var newlyVisible = new HashSet<HexCoordinate>(visibleCoords);
                newlyVisible.ExceptWith(previousVisible);

                var newlyHidden = new HashSet<HexCoordinate>(previousVisible);
                newlyHidden.ExceptWith(visibleCoords);

                foreach (var coord in newlyVisible)
                {
                    changes.Add(new FogOfWarUpdatedChange
                    {
                        playerID = player.id,
                        coordinate = coord,
                        visibility = "visible"
                    });
                }

                foreach (var coord in newlyHidden)
                {
                    // Check if it's now explored (was visible) or unexplored
                    string visibility = player.IsExplored(coord) ? "explored" : "unexplored";
                    changes.Add(new FogOfWarUpdatedChange
                    {
                        playerID = player.id,
                        coordinate = coord,
                        visibility = visibility
                    });
                }
            }

            return changes;
        }

        // Vision Calculation

        private HashSet<HexCoordinate> CalculateVisibleCoordinates(Guid playerID, GameState state)
        {
            var visibleCoords = new HashSet<HexCoordinate>();

            // Vision from buildings
            foreach (var building in state.GetBuildingsForPlayer(playerID))
            {
                if (!building.IsOperational) continue;

                int range;
                if (!buildingVisionRanges.TryGetValue(building.buildingType, out range))
                    range = 2;

                // Add vision from all coordinates the building occupies
                foreach (var occupiedCoord in building.OccupiedCoordinates)
                {
                    var coords = GetCoordinatesInRange(occupiedCoord, range, state);
                    visibleCoords.UnionWith(coords);
                }
            }

            // Vision from armies
            foreach (var army in state.GetArmiesForPlayer(playerID))
            {
                int range = baseUnitVisionRange;
                var coords = GetCoordinatesInRange(army.coordinate, range, state);
                visibleCoords.UnionWith(coords);
            }

            // Vision from villager groups
            foreach (var group in state.GetVillagerGroupsForPlayer(playerID))
            {
                int range = baseVillagerVisionRange;
                var coords = GetCoordinatesInRange(group.coordinate, range, state);
                visibleCoords.UnionWith(coords);
            }

            // Vision from reinforcements (just their tile)
            HashSet<HexCoordinate> reinforcementCoords;
            if (state.activeReinforcementPositions != null &&
                state.activeReinforcementPositions.TryGetValue(playerID, out reinforcementCoords))
            {
                visibleCoords.UnionWith(reinforcementCoords);
            }

            return visibleCoords;
        }

        private HashSet<HexCoordinate> GetCoordinatesInRange(HexCoordinate center, int range, GameState state)
        {
            var coords = new HashSet<HexCoordinate>();

            // Add center
            coords.Add(center);

            // Add all hexes in range
            for (int r = 1; r <= range; r++)
            {
                var ring = GetRing(center, r);
                foreach (var coord in ring)
                {
                    if (state.mapData.IsValidCoordinate(coord))
                    {
                        // Check for line of sight blocking (mountains reduce vision)
                        if (HasLineOfSight(center, coord, r, state))
                        {
                            coords.Add(coord);
                        }
                    }
                }
            }

            return coords;
        }

        private List<HexCoordinate> GetRing(HexCoordinate center, int radius)
        {
            if (radius == 0)
            {
                return new List<HexCoordinate> { center };
            }

            var results = new List<HexCoordinate>();
            var hex = new HexCoordinate(center.q - radius, center.r + radius);

            var directions = new HexCoordinate[]
            {
                new HexCoordinate(1, 0), new HexCoordinate(1, -1),
                new HexCoordinate(0, -1), new HexCoordinate(-1, 0),
                new HexCoordinate(-1, 1), new HexCoordinate(0, 1)
            };

            foreach (var direction in directions)
            {
                for (int i = 0; i < radius; i++)
                {
                    results.Add(hex);
                    hex = new HexCoordinate(hex.q + direction.q, hex.r + direction.r);
                }
            }

            return results;
        }

        private bool HasLineOfSight(HexCoordinate start, HexCoordinate end, int maxBlockedRange, GameState state)
        {
            // Simple line of sight check - mountains at close range block vision beyond them
            int distance = start.Distance(end);
            if (distance <= 1)
            {
                return true; // Adjacent hexes always visible
            }

            // Check intermediate hexes for mountains
            var path = GetLinePath(start, end);

            for (int i = 1; i < path.Count - 1; i++)
            {
                var coord = path[i];
                TerrainType? terrain = state.mapData.GetTerrain(coord);
                if (terrain.HasValue && terrain.Value == TerrainType.Mountain)
                {
                    // Mountain blocks vision to hexes beyond it
                    return false;
                }
            }

            return true;
        }

        private List<HexCoordinate> GetLinePath(HexCoordinate start, HexCoordinate end)
        {
            int distance = start.Distance(end);
            if (distance <= 0) return new List<HexCoordinate> { start };

            var path = new List<HexCoordinate>();

            for (int i = 0; i <= distance; i++)
            {
                double t = (double)i / (double)distance;

                // Linear interpolation in cube coordinates
                double startX, startY, startZ;
                HexToCube(start, out startX, out startY, out startZ);

                double endX, endY, endZ;
                HexToCube(end, out endX, out endY, out endZ);

                double x = startX + (endX - startX) * t;
                double y = startY + (endY - startY) * t;
                double z = startZ + (endZ - startZ) * t;

                // Round to nearest hex
                var coord = RoundCubeToHex(x, y, z);
                if (path.Count == 0 || path[path.Count - 1] != coord)
                {
                    path.Add(coord);
                }
            }

            return path;
        }

        private void HexToCube(HexCoordinate hex, out double x, out double y, out double z)
        {
            x = (double)hex.q;
            z = (double)hex.r;
            y = -x - z;
        }

        private HexCoordinate RoundCubeToHex(double x, double y, double z)
        {
            double rx = Math.Round(x);
            double ry = Math.Round(y);
            double rz = Math.Round(z);

            double xDiff = Math.Abs(rx - x);
            double yDiff = Math.Abs(ry - y);
            double zDiff = Math.Abs(rz - z);

            if (xDiff > yDiff && xDiff > zDiff)
            {
                rx = -ry - rz;
            }
            else if (yDiff > zDiff)
            {
                ry = -rx - rz;
            }
            else
            {
                rz = -rx - ry;
            }

            // Convert cube to axial
            return new HexCoordinate((int)rx, (int)rz);
        }

        // Query Methods

        public bool IsVisible(HexCoordinate coordinate, Guid playerID)
        {
            var player = gameState != null ? gameState.GetPlayer(playerID) : null;
            if (player == null) return false;
            return player.IsVisible(coordinate);
        }

        public bool IsExplored(HexCoordinate coordinate, Guid playerID)
        {
            var player = gameState != null ? gameState.GetPlayer(playerID) : null;
            if (player == null) return false;
            return player.IsExplored(coordinate);
        }

        public VisibilityLevel GetVisibilityLevel(HexCoordinate coordinate, Guid playerID)
        {
            var player = gameState != null ? gameState.GetPlayer(playerID) : null;
            if (player == null) return VisibilityLevel.Unexplored;
            return player.GetVisibilityLevel(coordinate);
        }

        public List<ArmyData> GetVisibleEnemies(Guid playerID)
        {
            if (gameState == null) return new List<ArmyData>();
            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<ArmyData>();

            var visibleEnemies = new List<ArmyData>();

            foreach (var army in gameState.armies.Values)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;

                if (player.IsVisible(army.coordinate))
                {
                    visibleEnemies.Add(army);
                }
            }

            return visibleEnemies;
        }

        public List<BuildingData> GetVisibleEnemyBuildings(Guid playerID)
        {
            if (gameState == null) return new List<BuildingData>();
            var player = gameState.GetPlayer(playerID);
            if (player == null) return new List<BuildingData>();

            var visibleBuildings = new List<BuildingData>();

            foreach (var building in gameState.buildings.Values)
            {
                if (!building.ownerID.HasValue || building.ownerID.Value == playerID) continue;

                // Check if any of the building's coordinates are visible
                foreach (var coord in building.OccupiedCoordinates)
                {
                    if (player.IsVisible(coord))
                    {
                        visibleBuildings.Add(building);
                        break;
                    }
                }
            }

            return visibleBuildings;
        }
    }
}
