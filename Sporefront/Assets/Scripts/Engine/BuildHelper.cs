// ============================================================================
// FILE: Engine/BuildHelper.cs
// PURPOSE: Shared building logic used by both BuildCommand and AIBuildCommand.
//          Extracted to avoid duplicating terrain cost and villager lookup code.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    public static class BuildHelper
    {
        /// <summary>
        /// Calculates the terrain cost multiplier for a building placement,
        /// accounting for mountain surcharge and faction highland discount.
        /// Checks all coordinates the building will occupy.
        /// </summary>
        public static double GetTerrainCostMultiplier(
            GameState state, BuildingType buildingType, HexCoordinate coordinate, int rotation, Guid playerID)
        {
            var occupiedCoords = buildingType.GetOccupiedCoordinates(coordinate, rotation);
            bool hasMountain = occupiedCoords.Any(c => state.mapData.GetTerrain(c) == TerrainType.Mountain);
            bool hasHighland = hasMountain || occupiedCoords.Any(c =>
            {
                var t = state.mapData.GetTerrain(c);
                return t == TerrainType.Hill;
            });
            double multiplier = hasMountain ? GameConfig.Terrain.MountainBuildingCostMultiplier : 1.0;

            if (hasHighland)
            {
                var player = state.GetPlayer(playerID);
                if (player != null)
                {
                    double reduction = player.faction.MountainBuildCostReduction();
                    if (reduction > 0)
                        multiplier *= (1.0 - reduction);
                }
            }

            return multiplier;
        }

        /// <summary>
        /// Returns the effective build cost dictionary with terrain multiplier applied.
        /// </summary>
        public static Dictionary<ResourceType, int> GetEffectiveBuildCost(
            GameState state, BuildingType buildingType, HexCoordinate coordinate, int rotation, Guid playerID)
        {
            double multiplier = GetTerrainCostMultiplier(state, buildingType, coordinate, rotation, playerID);
            var baseCost = buildingType.BuildCost();
            if (Math.Abs(multiplier - 1.0) < 0.001)
                return baseCost;

            var adjusted = new Dictionary<ResourceType, int>();
            foreach (var kvp in baseCost)
                adjusted[kvp.Key] = Math.Max(1, (int)Math.Ceiling(kvp.Value * multiplier));
            return adjusted;
        }

        /// <summary>
        /// Finds the nearest idle villager group to a target coordinate.
        /// Returns null if no idle villagers are available.
        /// </summary>
        public static VillagerGroupData FindNearestIdleVillager(
            GameState state, Guid playerID, HexCoordinate target)
        {
            VillagerGroupData nearest = null;
            int bestDist = int.MaxValue;
            foreach (var group in state.GetVillagerGroupsForPlayer(playerID))
            {
                if (!group.currentTask.IsIdle || group.currentPath != null) continue;
                int dist = group.coordinate.Distance(target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = group;
                }
            }
            return nearest;
        }
    }
}
