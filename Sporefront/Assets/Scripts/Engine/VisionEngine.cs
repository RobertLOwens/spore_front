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

        // Reusable collections to reduce GC pressure
        private HashSet<HexCoordinate> reusableVisibleCoords = new HashSet<HexCoordinate>();
        private HashSet<HexCoordinate> reusablePreviousVisible = new HashSet<HexCoordinate>();
        private HashSet<HexCoordinate> reusableNewlyVisible = new HashSet<HexCoordinate>();
        private HashSet<HexCoordinate> reusableNewlyHidden = new HashSet<HexCoordinate>();
        private List<HexCoordinate> reusableRing = new List<HexCoordinate>();
        private List<HexCoordinate> reusablePath = new List<HexCoordinate>();

        // Setup

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        // Update Loop

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null) return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            // Update vision for all players
            foreach (var player in gameState.players.Values)
            {
                reusableVisibleCoords.Clear();
                CalculateVisibleCoordinates(player.id, gameState, reusableVisibleCoords);

                reusablePreviousVisible.Clear();
                foreach (var coord in player.visibleCoordinates)
                    reusablePreviousVisible.Add(coord);

                player.SetVisibleCoordinates(reusableVisibleCoords);

                // Generate changes for newly visible/hidden coordinates
                reusableNewlyVisible.Clear();
                foreach (var coord in reusableVisibleCoords)
                {
                    if (!reusablePreviousVisible.Contains(coord))
                        reusableNewlyVisible.Add(coord);
                }

                reusableNewlyHidden.Clear();
                foreach (var coord in reusablePreviousVisible)
                {
                    if (!reusableVisibleCoords.Contains(coord))
                        reusableNewlyHidden.Add(coord);
                }

                foreach (var coord in reusableNewlyVisible)
                {
                    changes.Add(new FogOfWarUpdatedChange
                    {
                        playerID = player.id,
                        coordinate = coord,
                        visibility = "visible"
                    });
                }

                foreach (var coord in reusableNewlyHidden)
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

            // Emit camouflage changes for each player
            foreach (var player in gameState.players.Values)
            {
                var camouflagedIDs = new List<Guid>();
                foreach (var army in gameState.armies.Values)
                {
                    if (!army.ownerID.HasValue || army.ownerID.Value == player.id) continue;
                    if (player.IsVisible(army.coordinate) && IsArmyCamouflaged(army, player.id))
                    {
                        camouflagedIDs.Add(army.id);
                    }
                }
                if (camouflagedIDs.Count > 0)
                {
                    changes.Add(new CamouflagedArmiesChange
                    {
                        observingPlayerID = player.id,
                        camouflagedArmyIDs = camouflagedIDs
                    });
                }
            }

            return changes;
        }

        // Vision Calculation

        private void CalculateVisibleCoordinates(Guid playerID, GameState state, HashSet<HexCoordinate> visibleCoords)
        {
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
                    GetCoordinatesInRange(occupiedCoord, range, state, visibleCoords);
                }
            }

            // Vision from armies
            var player = state.GetPlayer(playerID);
            int factionVisionBonus = player != null ? player.faction.ArmyVisionBonus() : 0;
            foreach (var army in state.GetArmiesForPlayer(playerID))
            {
                int range = baseUnitVisionRange + factionVisionBonus;
                GetCoordinatesInRange(army.coordinate, range, state, visibleCoords);
            }

            // Vision from villager groups
            foreach (var group in state.GetVillagerGroupsForPlayer(playerID))
            {
                int range = baseVillagerVisionRange;
                GetCoordinatesInRange(group.coordinate, range, state, visibleCoords);
            }

            // Vision from reinforcements (just their tile)
            HashSet<HexCoordinate> reinforcementCoords;
            if (state.activeReinforcementPositions != null &&
                state.activeReinforcementPositions.TryGetValue(playerID, out reinforcementCoords))
            {
                visibleCoords.UnionWith(reinforcementCoords);
            }
        }

        private void GetCoordinatesInRange(HexCoordinate center, int range, GameState state, HashSet<HexCoordinate> coords)
        {
            // Add center
            coords.Add(center);

            // Add all hexes in range
            for (int r = 1; r <= range; r++)
            {
                GetRing(center, r, reusableRing);
                foreach (var coord in reusableRing)
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
        }

        private void GetRing(HexCoordinate center, int radius, List<HexCoordinate> results)
        {
            results.Clear();

            if (radius == 0)
            {
                results.Add(center);
                return;
            }

            // Walk West from center to starting position
            var hex = center;
            for (int i = 0; i < radius; i++)
                hex = hex.Neighbor(3); // West

            // Walk around ring: after starting West (dir 3), first edge is (3+2)%6 = 5
            for (int side = 0; side < 6; side++)
            {
                int direction = (5 + side) % 6;
                for (int i = 0; i < radius; i++)
                {
                    results.Add(hex);
                    hex = hex.Neighbor(direction);
                }
            }
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
            GetLinePath(start, end, reusablePath);

            for (int i = 1; i < reusablePath.Count - 1; i++)
            {
                var coord = reusablePath[i];
                TerrainType? terrain = state.mapData.GetTerrain(coord);
                if (terrain.HasValue && terrain.Value == TerrainType.Mountain)
                {
                    // Mountain blocks vision to hexes beyond it
                    return false;
                }
            }

            return true;
        }

        private void GetLinePath(HexCoordinate start, HexCoordinate end, List<HexCoordinate> path)
        {
            path.Clear();

            int distance = start.Distance(end);
            if (distance <= 0)
            {
                path.Add(start);
                return;
            }

            // Pre-compute cube coordinates for start and end
            double startX, startY, startZ;
            HexToCube(start, out startX, out startY, out startZ);

            double endX, endY, endZ;
            HexToCube(end, out endX, out endY, out endZ);

            for (int i = 0; i <= distance; i++)
            {
                double t = (double)i / (double)distance;

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
        }

        private void HexToCube(HexCoordinate hex, out double x, out double y, out double z)
        {
            x = hex.q - (hex.r - (hex.r & 1)) / 2.0;
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

            // Convert cube to odd-r offset
            int finalR = (int)rz;
            int finalQ = (int)rx + (finalR - (finalR & 1)) / 2;
            return new HexCoordinate(finalQ, finalR);
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
                    // Check woodland camouflage
                    if (IsArmyCamouflaged(army, playerID))
                        continue;

                    visibleEnemies.Add(army);
                }
            }

            return visibleEnemies;
        }

        private bool IsArmyCamouflaged(ArmyData army, Guid observingPlayerID)
        {
            if (gameState == null || !army.ownerID.HasValue) return false;

            var owner = gameState.GetPlayer(army.ownerID.Value);
            if (owner == null || !owner.faction.HasWoodlandCamouflage()) return false;

            // Check if army is on a Trees resource tile
            var resourcePoint = gameState.GetResourcePoint(army.coordinate);
            if (resourcePoint == null || resourcePoint.resourceType != ResourcePointType.Trees)
                return false;

            // Check if observing player has any unit within 1 tile
            foreach (var observerArmy in gameState.GetArmiesForPlayer(observingPlayerID))
            {
                if (observerArmy.coordinate.Distance(army.coordinate) <= 1)
                    return false;
            }
            foreach (var observerVillager in gameState.GetVillagerGroupsForPlayer(observingPlayerID))
            {
                if (observerVillager.coordinate.Distance(army.coordinate) <= 1)
                    return false;
            }

            return true;
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
