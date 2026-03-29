// ============================================================================
// FILE: Engine/MapGenerator.cs
// PURPOSE: Base map generator with resource placement and starting positions
//          C# port of MapGenerator.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // ================================================================
    // Resource Placement
    // ================================================================

    public struct ResourcePlacement
    {
        public HexCoordinate coordinate;
        public ResourcePointType resourceType;

        public ResourcePlacement(HexCoordinate coordinate, ResourcePointType resourceType)
        {
            this.coordinate = coordinate;
            this.resourceType = resourceType;
        }
    }

    // ================================================================
    // Player Start Position
    // ================================================================

    public struct PlayerStartPosition
    {
        public HexCoordinate coordinate;
        public int playerIndex;

        public PlayerStartPosition(HexCoordinate coordinate, int playerIndex)
        {
            this.coordinate = coordinate;
            this.playerIndex = playerIndex;
        }
    }

    // ================================================================
    // Terrain Generation Data (replaces Swift tuple)
    // ================================================================

    public struct TerrainGenerationData
    {
        public TerrainType terrain;
        public int elevation;

        public TerrainGenerationData(TerrainType terrain, int elevation)
        {
            this.terrain = terrain;
            this.elevation = elevation;
        }
    }

    // ================================================================
    // Map Generator Base
    // ================================================================

    public abstract class MapGeneratorBase
    {
        public abstract int Width { get; }
        public abstract int Height { get; }

        public ulong? Seed { get; set; }

        /// <summary>
        /// Generate terrain with elevation data for all tiles.
        /// Returns dictionary mapping coordinates to terrain type and elevation.
        /// </summary>
        public abstract Dictionary<HexCoordinate, TerrainGenerationData> GenerateTerrain();

        /// <summary>
        /// Get starting positions for all players.
        /// </summary>
        public abstract List<PlayerStartPosition> GetStartingPositions();

        /// <summary>
        /// Generate guaranteed resources around a starting position.
        /// </summary>
        public abstract List<ResourcePlacement> GenerateStartingResources(HexCoordinate position);

        /// <summary>
        /// Generate neutral resources scattered across the map.
        /// Excludes areas near starting positions.
        /// </summary>
        public abstract List<ResourcePlacement> GenerateNeutralResources(int excludingRadius, List<HexCoordinate> aroundPositions);

        /// <summary>
        /// Ensure starting areas have flat terrain (elevation 0).
        /// Returns updated terrain data with flattened starting zones.
        /// </summary>
        public virtual void EnsureStartingAreasFlat(
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            List<HexCoordinate> startPositions,
            int radius)
        {
            foreach (var startPos in startPositions)
            {
                for (int q = -radius; q <= radius; q++)
                {
                    for (int r = -radius; r <= radius; r++)
                    {
                        var coord = new HexCoordinate(startPos.q + q, startPos.r + r);
                        if (coord.Distance(startPos) <= radius)
                        {
                            TerrainGenerationData tileData;
                            if (terrain.TryGetValue(coord, out tileData))
                            {
                                tileData.elevation = 0;
                                if (!tileData.terrain.IsWalkable())
                                {
                                    tileData.terrain = TerrainType.Plains;
                                }
                                terrain[coord] = tileData;
                            }
                        }
                    }
                }
            }
        }

        // ================================================================
        // Shared Resource Placement Utilities
        // ================================================================

        protected HexCoordinate? FindValidPlacement(
            SeededRandom rng, List<HexCoordinate> candidates,
            List<HexCoordinate> startPositions, int exclusionRadius,
            HashSet<HexCoordinate> used)
        {
            for (int attempt = 0; attempt < 20 && candidates.Count > 0; attempt++)
            {
                int idx = rng.NextInt(0, candidates.Count - 1);
                var coord = candidates[idx];

                if (used.Contains(coord)) continue;

                if (exclusionRadius > 0)
                {
                    bool tooClose = false;
                    foreach (var startPos in startPositions)
                    {
                        if (coord.Distance(startPos) < exclusionRadius)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;
                }

                return coord;
            }
            return null;
        }

        protected HexCoordinate? FindUnusedCoordinate(
            List<HexCoordinate> coords, HashSet<HexCoordinate> used)
        {
            while (coords.Count > 0)
            {
                var coord = coords[0];
                coords.RemoveAt(0);
                if (!used.Contains(coord))
                    return coord;
            }
            return null;
        }

        protected List<ResourcePlacement> GenerateResourceCluster(
            SeededRandom rng, ResourcePointType type, int size,
            HexCoordinate center, HashSet<HexCoordinate> used)
        {
            var placements = new List<ResourcePlacement>();
            var usedLocal = new HashSet<HexCoordinate>(used);
            var frontier = new List<HexCoordinate> { center };
            int placed = 0;

            while (placed < size && frontier.Count > 0)
            {
                var current = frontier[0];
                frontier.RemoveAt(0);

                if (usedLocal.Contains(current)) continue;
                if (current.q < 0 || current.q >= Width || current.r < 0 || current.r >= Height) continue;

                placements.Add(new ResourcePlacement(current, type));
                usedLocal.Add(current);
                placed++;

                var neighbors = current.Neighbors();
                var neighborList = new List<HexCoordinate>(neighbors);
                rng.Shuffle(neighborList);
                foreach (var neighbor in neighborList)
                {
                    if (!usedLocal.Contains(neighbor))
                        frontier.Add(neighbor);
                }
            }

            return placements;
        }

        protected List<ResourcePlacement> PlaceResourceCluster(
            SeededRandom rng, ResourcePointType type, int size,
            HexCoordinate center, int radius, HashSet<HexCoordinate> used)
        {
            var placements = new List<ResourcePlacement>();
            var usedLocal = new HashSet<HexCoordinate>(used);

            var candidates = new List<HexCoordinate>();
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    var coord = new HexCoordinate(center.q + q, center.r + r);
                    if (coord.Distance(center) <= radius && !usedLocal.Contains(coord))
                        candidates.Add(coord);
                }
            }

            rng.Shuffle(candidates);
            if (candidates.Count == 0) return placements;
            var startCoord = candidates[0];

            var frontier = new List<HexCoordinate> { startCoord };
            int placed = 0;

            while (placed < size && frontier.Count > 0)
            {
                var current = frontier[0];
                frontier.RemoveAt(0);

                if (usedLocal.Contains(current)) continue;
                if (current.Distance(center) > radius) continue;

                placements.Add(new ResourcePlacement(current, type));
                usedLocal.Add(current);
                placed++;

                var neighbors = current.Neighbors();
                var neighborList = new List<HexCoordinate>(neighbors);
                rng.Shuffle(neighborList);
                foreach (var neighbor in neighborList)
                {
                    if (!usedLocal.Contains(neighbor) && neighbor.Distance(center) <= radius)
                        frontier.Add(neighbor);
                }
            }

            return placements;
        }

        protected void PlaceAnimals(
            SeededRandom rng, int count,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements,
            HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var coord = FindValidPlacement(rng, zoneTiles, startPositions, exclusionRadius, used);
                if (!coord.HasValue) continue;

                var animalType = rng.NextBool() ? ResourcePointType.Deer : ResourcePointType.WildBoar;
                placements.Add(new ResourcePlacement(coord.Value, animalType));
                used.Add(coord.Value);
            }
        }

        protected void PlaceScatteredResources(
            SeededRandom rng, int count, ResourcePointType type,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements,
            HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var coord = FindValidPlacement(rng, zoneTiles, startPositions, exclusionRadius, used);
                if (!coord.HasValue) continue;

                placements.Add(new ResourcePlacement(coord.Value, type));
                used.Add(coord.Value);
            }
        }

        protected void PlaceTreePockets(
            SeededRandom rng, int count, int sizeMin, int sizeMax,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements,
            HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var center = FindValidPlacement(rng, zoneTiles, startPositions, exclusionRadius, used);
                if (!center.HasValue) continue;

                int pocketSize = rng.NextInt(sizeMin, sizeMax);
                var treePlacements = GenerateResourceCluster(
                    rng, ResourcePointType.Trees, pocketSize, center.Value, used);
                foreach (var placement in treePlacements)
                {
                    placements.Add(placement);
                    used.Add(placement.coordinate);
                }
            }
        }

        protected List<HexCoordinate> PlaceStartingMiningHill(
            SeededRandom rng,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            HexCoordinate spawnPosition, int hillSizeMin = 4, int hillSizeMax = 5)
        {
            var hillCoords = new List<HexCoordinate>();

            var candidates = new List<HexCoordinate>();
            for (int q = -7; q <= 7; q++)
            {
                for (int r = -7; r <= 7; r++)
                {
                    var coord = new HexCoordinate(spawnPosition.q + q, spawnPosition.r + r);
                    int dist = coord.Distance(spawnPosition);
                    if (dist >= 5 && dist <= 6)
                    {
                        if (coord.q >= 0 && coord.q < Width && coord.r >= 0 && coord.r < Height)
                            candidates.Add(coord);
                    }
                }
            }

            if (candidates.Count == 0) return hillCoords;

            rng.Shuffle(candidates);
            var center = candidates[0];

            int hillSize = rng.NextInt(hillSizeMin, hillSizeMax);
            var frontier = new List<HexCoordinate> { center };
            var visited = new HashSet<HexCoordinate>();
            int placed = 0;

            while (placed < hillSize && frontier.Count > 0)
            {
                var cur = frontier[0];
                frontier.RemoveAt(0);

                if (visited.Contains(cur)) continue;
                if (cur.q < 0 || cur.q >= Width || cur.r < 0 || cur.r >= Height) continue;
                visited.Add(cur);

                TerrainGenerationData data;
                if (terrain.TryGetValue(cur, out data))
                {
                    data.terrain = TerrainType.Hill;
                    data.elevation = 1;
                    terrain[cur] = data;
                    hillCoords.Add(cur);
                    placed++;

                    var neighbors = cur.Neighbors();
                    var neighborList = new List<HexCoordinate>(neighbors);
                    rng.Shuffle(neighborList);
                    foreach (var neighbor in neighborList)
                    {
                        if (!visited.Contains(neighbor))
                            frontier.Add(neighbor);
                    }
                }
            }

            return hillCoords;
        }
    }
}
