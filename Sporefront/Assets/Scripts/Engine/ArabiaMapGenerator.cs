// ============================================================================
// FILE: Engine/ArabiaMapGenerator.cs
// PURPOSE: Arabia-style map generator for 1v1 competitive play
//          C# port of ArabiaMapGenerator.swift (459 lines)
//          Includes SeededRandom (xorshift64) for deterministic generation
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // ================================================================
    // Seeded Random Number Generator (xorshift64)
    // ================================================================

    /// <summary>
    /// A simple seeded random number generator for reproducible map generation.
    /// Uses xorshift64 algorithm — MUST match Swift SeededRandomNumberGenerator exactly.
    /// </summary>
    public class SeededRandom
    {
        private ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        /// <summary>
        /// Generate next raw UInt64 value using xorshift64.
        /// </summary>
        public ulong NextULong()
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return state;
        }

        /// <summary>
        /// Generate random int in inclusive range [min, max].
        /// </summary>
        public int NextInt(int min, int max)
        {
            if (min >= max) return min;
            ulong range = (ulong)(max - min + 1);
            return min + (int)(NextULong() % range);
        }

        /// <summary>
        /// Generate random double in range [min, max].
        /// </summary>
        public double NextDouble(double min, double max)
        {
            double normalized = (double)NextULong() / (double)ulong.MaxValue;
            return min + normalized * (max - min);
        }

        /// <summary>
        /// Generate random bool.
        /// </summary>
        public bool NextBool()
        {
            return (NextULong() & 1) == 1;
        }

        /// <summary>
        /// Fisher-Yates shuffle in place.
        /// </summary>
        public void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }

    // ================================================================
    // Arabia Map Generator Configuration
    // ================================================================

    [Serializable]
    public class ArabiaMapConfig
    {
        public int treePocketCount = 25;
        public int treePocketSizeMin = 3;
        public int treePocketSizeMax = 8;
        public int mineralDepositCount = 12;
        public int mineralDepositSizeMin = 2;
        public int mineralDepositSizeMax = 4;
        public double hillClusterChance = 0.15;
        public int maxElevation = 3;
    }

    // ================================================================
    // Arabia Map Generator
    // ================================================================

    /// <summary>
    /// Arabia-style map generator for 1v1 competitive play.
    /// Creates a 35x35 hex grid with balanced starting positions and resources.
    /// </summary>
    public class ArabiaMapGenerator : MapGeneratorBase
    {
        // ================================================================
        // Properties
        // ================================================================

        public override int Width => 35;
        public override int Height => 35;

        private readonly int startPadding = 8;
        private readonly int startingResourceRadius = 5;
        private readonly int neutralResourceExclusionRadius = 10;

        private SeededRandom rng;
        public ArabiaMapConfig config;

        // ================================================================
        // Initialization
        // ================================================================

        public ArabiaMapGenerator(ulong? seed = null, ArabiaMapConfig config = null)
        {
            Seed = seed;
            this.config = config ?? new ArabiaMapConfig();
            rng = new SeededRandom(seed ?? (ulong)DateTime.UtcNow.Ticks);
        }

        // ================================================================
        // MapGeneratorBase Implementation
        // ================================================================

        public override Dictionary<HexCoordinate, TerrainGenerationData> GenerateTerrain()
        {
            var terrain = new Dictionary<HexCoordinate, TerrainGenerationData>();

            // Step 1: Fill map with base plains terrain
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    terrain[coord] = new TerrainGenerationData(TerrainType.Plains, 0);
                }
            }

            // Step 2: Generate hill clusters with elevation
            GenerateHillClusters(terrain);

            // Step 3: Flatten starting areas
            var startPositions = GetStartingPositions();
            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);

            EnsureStartingAreasFlat(terrain, startCoords, 5);

            return terrain;
        }

        public override List<PlayerStartPosition> GetStartingPositions()
        {
            var player1Pos = new HexCoordinate(startPadding, startPadding);
            var player2Pos = new HexCoordinate(Width - startPadding - 1, Height - startPadding - 1);

            return new List<PlayerStartPosition>
            {
                new PlayerStartPosition(player1Pos, 0),
                new PlayerStartPosition(player2Pos, 1)
            };
        }

        public override List<ResourcePlacement> GenerateStartingResources(HexCoordinate position)
        {
            var placements = new List<ResourcePlacement>();
            var usedCoordinates = new HashSet<HexCoordinate> { position };

            // Get all valid coordinates within starting radius (excluding center)
            var availableCoords = new List<HexCoordinate>();
            for (int q = -startingResourceRadius; q <= startingResourceRadius; q++)
            {
                for (int r = -startingResourceRadius; r <= startingResourceRadius; r++)
                {
                    var coord = new HexCoordinate(position.q + q, position.r + r);
                    if (coord.Distance(position) <= startingResourceRadius && !coord.Equals(position))
                    {
                        availableCoords.Add(coord);
                    }
                }
            }

            // Shuffle for randomness
            rng.Shuffle(availableCoords);

            // Place 2 wild boars
            for (int i = 0; i < 2; i++)
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.WildBoar));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // Place 1 deer
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.Deer));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // Place 2 forage bushes (immediate food, no camp required)
            for (int i = 0; i < 2; i++)
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.Forage));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // Place 4-tile ore cluster
            var oreCluster = PlaceResourceCluster(
                ResourcePointType.OreMine, 4, position, startingResourceRadius, usedCoordinates);
            foreach (var placement in oreCluster)
            {
                placements.Add(placement);
                usedCoordinates.Add(placement.coordinate);
            }

            // Place 3-tile stone cluster
            var stoneCluster = PlaceResourceCluster(
                ResourcePointType.StoneQuarry, 3, position, startingResourceRadius, usedCoordinates);
            foreach (var placement in stoneCluster)
            {
                placements.Add(placement);
                usedCoordinates.Add(placement.coordinate);
            }

            // Place 3-4 woodlines (tree clusters) within 5 tile radius
            int woodlineCount = rng.NextInt(3, 4);
            for (int i = 0; i < woodlineCount; i++)
            {
                int woodlineSize = rng.NextInt(3, 5);
                var woodlineCluster = PlaceResourceCluster(
                    ResourcePointType.Trees, woodlineSize, position, startingResourceRadius, usedCoordinates);
                foreach (var placement in woodlineCluster)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }
            }

            return placements;
        }

        public override List<ResourcePlacement> GenerateNeutralResources(
            int excludingRadius, List<HexCoordinate> aroundPositions)
        {
            var placements = new List<ResourcePlacement>();
            var usedCoordinates = new HashSet<HexCoordinate>();

            // Mark starting area coordinates as used
            foreach (var startPos in aroundPositions)
            {
                for (int q = -excludingRadius; q <= excludingRadius; q++)
                {
                    for (int r = -excludingRadius; r <= excludingRadius; r++)
                    {
                        var coord = new HexCoordinate(startPos.q + q, startPos.r + r);
                        if (coord.Distance(startPos) <= excludingRadius)
                        {
                            usedCoordinates.Add(coord);
                        }
                    }
                }
            }

            // Generate tree pockets
            int treeExclusionRadius = 6;
            for (int i = 0; i < config.treePocketCount; i++)
            {
                int pocketSize = rng.NextInt(config.treePocketSizeMin, config.treePocketSizeMax);
                int centerQ = rng.NextInt(3, Width - 4);
                int centerR = rng.NextInt(3, Height - 4);
                var center = new HexCoordinate(centerQ, centerR);

                // Skip if too close to starting positions
                bool tooClose = false;
                foreach (var startPos in aroundPositions)
                {
                    if (center.Distance(startPos) < treeExclusionRadius)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Generate cluster of trees
                var treePlacements = GenerateResourceCluster(
                    ResourcePointType.Trees, pocketSize, center, usedCoordinates);
                foreach (var placement in treePlacements)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }
            }

            // Generate mineral deposits (mix of ore and stone)
            for (int i = 0; i < config.mineralDepositCount; i++)
            {
                int depositSize = rng.NextInt(config.mineralDepositSizeMin, config.mineralDepositSizeMax);
                int centerQ = rng.NextInt(10, Width - 11);
                int centerR = rng.NextInt(10, Height - 11);
                var center = new HexCoordinate(centerQ, centerR);

                // Skip if too close to starting positions
                bool tooClose = false;
                foreach (var startPos in aroundPositions)
                {
                    if (center.Distance(startPos) < excludingRadius + 3)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Alternate between ore and stone
                var resourceType = (i % 2 == 0) ? ResourcePointType.OreMine : ResourcePointType.StoneQuarry;

                var mineralPlacements = GenerateResourceCluster(
                    resourceType, depositSize, center, usedCoordinates);
                foreach (var placement in mineralPlacements)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }
            }

            // Scatter some deer and boar across the map
            int animalCount = 15;
            for (int i = 0; i < animalCount; i++)
            {
                int q = rng.NextInt(5, Width - 6);
                int r = rng.NextInt(5, Height - 6);
                var coord = new HexCoordinate(q, r);

                // Skip if used or too close to starting positions
                if (usedCoordinates.Contains(coord)) continue;
                bool tooClose = false;
                foreach (var startPos in aroundPositions)
                {
                    if (coord.Distance(startPos) < excludingRadius)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                var animalType = rng.NextBool() ? ResourcePointType.Deer : ResourcePointType.WildBoar;
                placements.Add(new ResourcePlacement(coord, animalType));
                usedCoordinates.Add(coord);
            }

            return placements;
        }

        // ================================================================
        // Private Helper Methods
        // ================================================================

        private void GenerateHillClusters(Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            var hillSeeds = new List<HexCoordinate>();

            // Generate hill seed points
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    if (rng.NextDouble(0.0, 1.0) < config.hillClusterChance * 0.3)
                    {
                        hillSeeds.Add(new HexCoordinate(q, r));
                    }
                }
            }

            // For each seed, create a hill cluster
            foreach (var seed in hillSeeds)
            {
                int clusterSize = rng.NextInt(2, 5);
                int peakElevation = rng.NextInt(1, config.maxElevation);

                // Set the seed tile
                TerrainGenerationData data;
                if (terrain.TryGetValue(seed, out data))
                {
                    data.terrain = TerrainType.Hill;
                    data.elevation = peakElevation;
                    terrain[seed] = data;
                }

                // Expand outward with decreasing elevation
                var frontier = new List<HexCoordinate> { seed };
                var visited = new HashSet<HexCoordinate> { seed };
                int tilesPlaced = 1;

                while (tilesPlaced < clusterSize && frontier.Count > 0)
                {
                    var current = frontier[0];
                    frontier.RemoveAt(0);

                    TerrainGenerationData currentData;
                    int currentElevation = terrain.TryGetValue(current, out currentData) ? currentData.elevation : 0;

                    foreach (var neighbor in current.Neighbors())
                    {
                        if (tilesPlaced >= clusterSize) break;
                        if (visited.Contains(neighbor)) continue;
                        if (neighbor.q < 0 || neighbor.q >= Width || neighbor.r < 0 || neighbor.r >= Height) continue;

                        visited.Add(neighbor);

                        // Probability decreases with distance from seed
                        if (rng.NextDouble(0.0, 1.0) < 0.6)
                        {
                            int newElevation = Math.Max(1, currentElevation - rng.NextInt(0, 1));
                            TerrainGenerationData neighborData;
                            if (terrain.TryGetValue(neighbor, out neighborData))
                            {
                                neighborData.terrain = TerrainType.Hill;
                                neighborData.elevation = newElevation;
                                terrain[neighbor] = neighborData;
                            }
                            frontier.Add(neighbor);
                            tilesPlaced++;
                        }
                    }
                }
            }
        }

        private HexCoordinate? FindUnusedCoordinate(
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

        private List<ResourcePlacement> PlaceResourceCluster(
            ResourcePointType type, int size, HexCoordinate center,
            int radius, HashSet<HexCoordinate> used)
        {
            var placements = new List<ResourcePlacement>();
            var usedLocal = new HashSet<HexCoordinate>(used);

            // Find candidate starting points for the cluster
            var candidates = new List<HexCoordinate>();
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    var coord = new HexCoordinate(center.q + q, center.r + r);
                    if (coord.Distance(center) <= radius && !usedLocal.Contains(coord))
                    {
                        candidates.Add(coord);
                    }
                }
            }

            rng.Shuffle(candidates);

            if (candidates.Count == 0) return placements;
            var startCoord = candidates[0];

            // BFS to create adjacent cluster
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

                // Add neighbors to frontier (shuffled for organic shape)
                var neighbors = current.Neighbors();
                var neighborList = new List<HexCoordinate>(neighbors);
                rng.Shuffle(neighborList);
                foreach (var neighbor in neighborList)
                {
                    if (!usedLocal.Contains(neighbor) && neighbor.Distance(center) <= radius)
                    {
                        frontier.Add(neighbor);
                    }
                }
            }

            return placements;
        }

        private List<ResourcePlacement> GenerateResourceCluster(
            ResourcePointType type, int size, HexCoordinate center,
            HashSet<HexCoordinate> used)
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

                // Add neighbors to frontier (shuffled for organic shape)
                var neighbors = current.Neighbors();
                var neighborList = new List<HexCoordinate>(neighbors);
                rng.Shuffle(neighborList);
                foreach (var neighbor in neighborList)
                {
                    if (!usedLocal.Contains(neighbor))
                    {
                        frontier.Add(neighbor);
                    }
                }
            }

            return placements;
        }
    }
}
