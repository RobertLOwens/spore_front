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
        // Ridgelines
        public int ridgeCount = 4;
        public int ridgeLengthMin = 6;
        public int ridgeLengthMax = 12;
        public double ridgeFoothillChance = 0.4;

        // Neutral resources
        public int treePocketCount = 15;
        public int treePocketSizeMin = 5;
        public int treePocketSizeMax = 10;
        public int mineralDepositCount = 12;
        public int mineralDepositSizeMin = 2;
        public int mineralDepositSizeMax = 4;
        public int animalCount = 15;
    }

    // ================================================================
    // Arabia Map Generator
    // ================================================================

    /// <summary>
    /// Arabia-style map generator for 1v1 competitive play.
    /// Creates a hex grid with balanced starting positions and resources.
    /// </summary>
    public class ArabiaMapGenerator : MapGeneratorBase
    {
        // ================================================================
        // Properties
        // ================================================================

        private readonly int _width;
        private readonly int _height;

        public override int Width => _width;
        public override int Height => _height;

        private readonly int startPadding;
        private readonly int startingResourceRadius = 7;

        private SeededRandom rng;
        public ArabiaMapConfig config;

        // Cached starting positions — avoids RNG drift when called multiple times
        private List<PlayerStartPosition> cachedStartPositions;

        // Cached terrain — used by GenerateStartingResources for mining hill placement
        private Dictionary<HexCoordinate, TerrainGenerationData> cachedTerrain;

        // Mining hill tiles per player index — ore/stone placed here
        private Dictionary<int, List<HexCoordinate>> miningHillTiles = new Dictionary<int, List<HexCoordinate>>();

        // All ridgeline tile coordinates — used for neutral mineral placement
        private HashSet<HexCoordinate> ridgeTiles = new HashSet<HexCoordinate>();

        // ================================================================
        // Initialization
        // ================================================================

        public ArabiaMapGenerator(int width = 35, int height = 35, ulong? seed = null, ArabiaMapConfig config = null)
        {
            _width = width;
            _height = height;
            startPadding = Math.Max(4, width / 5);
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

            // Step 2: Get starting positions for ridge exclusion
            var startPositions = GetStartingPositions();
            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);

            // Step 3: Generate connected ridgelines
            GenerateRidgelines(terrain, startCoords);

            // Step 4: Flatten starting areas
            EnsureStartingAreasFlat(terrain, startCoords, 5);

            // Step 5: Place mining hills at edge of each starting area
            for (int i = 0; i < startPositions.Count; i++)
            {
                var hillTiles = PlaceStartingMiningHill(terrain, startPositions[i].coordinate);
                miningHillTiles[i] = hillTiles;
            }

            // Cache terrain for use in GenerateStartingResources
            cachedTerrain = terrain;

            return terrain;
        }

        public override List<PlayerStartPosition> GetStartingPositions()
        {
            if (cachedStartPositions != null) return cachedStartPositions;

            int pad = startPadding;
            int w = Width;
            int h = Height;

            // 4 possible diagonal spawn pair configurations
            var spawnPairs = new (HexCoordinate p1, HexCoordinate p2)[]
            {
                (new HexCoordinate(pad, pad), new HexCoordinate(w - pad - 1, h - pad - 1)),         // top-left vs bottom-right
                (new HexCoordinate(w - pad - 1, pad), new HexCoordinate(pad, h - pad - 1)),         // top-right vs bottom-left
                (new HexCoordinate(pad, h - pad - 1), new HexCoordinate(w - pad - 1, pad)),         // bottom-left vs top-right
                (new HexCoordinate(w - pad - 1, h - pad - 1), new HexCoordinate(pad, pad)),         // bottom-right vs top-left
            };

            int choice = rng.NextInt(0, 3);
            var chosen = spawnPairs[choice];

            cachedStartPositions = new List<PlayerStartPosition>
            {
                new PlayerStartPosition(chosen.p1, 0),
                new PlayerStartPosition(chosen.p2, 1)
            };
            return cachedStartPositions;
        }

        public override List<ResourcePlacement> GenerateStartingResources(HexCoordinate position)
        {
            var placements = new List<ResourcePlacement>();
            var usedCoordinates = new HashSet<HexCoordinate> { position };

            // Block tiles immediately around city center to prevent cluttered spawns
            foreach (var neighbor in position.Neighbors())
                usedCoordinates.Add(neighbor);

            // Get all valid coordinates within starting radius, excluding a 4-tile gap around center
            int minResourceDistance = 4;
            var availableCoords = new List<HexCoordinate>();
            for (int q = -startingResourceRadius; q <= startingResourceRadius; q++)
            {
                for (int r = -startingResourceRadius; r <= startingResourceRadius; r++)
                {
                    var coord = new HexCoordinate(position.q + q, position.r + r);
                    int dist = coord.Distance(position);
                    if (dist >= minResourceDistance && dist <= startingResourceRadius)
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

            // Place ore and stone on mining hill tiles if available
            int playerIndex = -1;
            var startPositions = GetStartingPositions();
            for (int i = 0; i < startPositions.Count; i++)
            {
                if (startPositions[i].coordinate.Equals(position))
                {
                    playerIndex = i;
                    break;
                }
            }

            List<HexCoordinate> hillTiles = null;
            if (playerIndex >= 0 && miningHillTiles.ContainsKey(playerIndex))
            {
                hillTiles = new List<HexCoordinate>(miningHillTiles[playerIndex]);
                rng.Shuffle(hillTiles);
            }

            if (hillTiles != null && hillTiles.Count >= 7)
            {
                // Place ore cluster on mining hill tiles
                int orePlaced = 0;
                for (int i = 0; i < hillTiles.Count && orePlaced < 4; i++)
                {
                    if (!usedCoordinates.Contains(hillTiles[i]))
                    {
                        placements.Add(new ResourcePlacement(hillTiles[i], ResourcePointType.OreMine));
                        usedCoordinates.Add(hillTiles[i]);
                        orePlaced++;
                    }
                }

                // Place stone cluster on remaining mining hill tiles
                int stonePlaced = 0;
                for (int i = 0; i < hillTiles.Count && stonePlaced < 3; i++)
                {
                    if (!usedCoordinates.Contains(hillTiles[i]))
                    {
                        placements.Add(new ResourcePlacement(hillTiles[i], ResourcePointType.StoneQuarry));
                        usedCoordinates.Add(hillTiles[i]);
                        stonePlaced++;
                    }
                }

                // Fall back to BFS cluster for any remaining
                if (orePlaced < 4)
                {
                    var oreCluster = PlaceResourceCluster(
                        ResourcePointType.OreMine, 4 - orePlaced, position, startingResourceRadius, usedCoordinates);
                    foreach (var placement in oreCluster)
                    {
                        placements.Add(placement);
                        usedCoordinates.Add(placement.coordinate);
                    }
                }
                if (stonePlaced < 3)
                {
                    var stoneCluster = PlaceResourceCluster(
                        ResourcePointType.StoneQuarry, 3 - stonePlaced, position, startingResourceRadius, usedCoordinates);
                    foreach (var placement in stoneCluster)
                    {
                        placements.Add(placement);
                        usedCoordinates.Add(placement.coordinate);
                    }
                }
            }
            else
            {
                // Fallback: place ore/stone via BFS cluster (no mining hill available)
                var oreCluster = PlaceResourceCluster(
                    ResourcePointType.OreMine, 4, position, startingResourceRadius, usedCoordinates);
                foreach (var placement in oreCluster)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }

                var stoneCluster = PlaceResourceCluster(
                    ResourcePointType.StoneQuarry, 3, position, startingResourceRadius, usedCoordinates);
                foreach (var placement in stoneCluster)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }
            }

            // Place 4-6 woodlines (tree clusters) within starting radius
            int woodlineCount = rng.NextInt(4, 6);
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

            // Collect tiles by terrain type for targeted placement
            var plainsTiles = new List<HexCoordinate>();
            var hillTiles = new List<HexCoordinate>();

            if (cachedTerrain != null)
            {
                for (int r = 0; r < Height; r++)
                {
                    for (int q = 0; q < Width; q++)
                    {
                        var coord = new HexCoordinate(q, r);
                        if (usedCoordinates.Contains(coord)) continue;

                        TerrainGenerationData data;
                        if (cachedTerrain.TryGetValue(coord, out data))
                        {
                            if (data.terrain == TerrainType.Plains)
                                plainsTiles.Add(coord);
                            else if (data.terrain == TerrainType.Hill)
                                hillTiles.Add(coord);
                        }
                    }
                }
            }
            else
            {
                // Fallback: treat all non-excluded tiles as plains
                for (int r = 0; r < Height; r++)
                {
                    for (int q = 0; q < Width; q++)
                    {
                        var coord = new HexCoordinate(q, r);
                        if (!usedCoordinates.Contains(coord))
                            plainsTiles.Add(coord);
                    }
                }
            }

            rng.Shuffle(plainsTiles);
            rng.Shuffle(hillTiles);

            // Track tree coordinates for animal placement near woodlines
            var treeCoordinates = new HashSet<HexCoordinate>();

            // Generate tree pockets on plains tiles
            int treeExclusionRadius = 6;
            for (int i = 0; i < config.treePocketCount; i++)
            {
                var center = FindValidPlacementFromList(
                    plainsTiles, aroundPositions, treeExclusionRadius, usedCoordinates);
                if (!center.HasValue) continue;

                int pocketSize = rng.NextInt(config.treePocketSizeMin, config.treePocketSizeMax);
                var treePlacements = GenerateResourceCluster(
                    ResourcePointType.Trees, pocketSize, center.Value, usedCoordinates);
                foreach (var placement in treePlacements)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                    treeCoordinates.Add(placement.coordinate);
                }
            }

            // Generate mineral deposits on hill tiles (terrain-correlated)
            for (int i = 0; i < config.mineralDepositCount; i++)
            {
                // Try hill tiles first, fall back to any tile
                var candidates = hillTiles.Count > 0 ? hillTiles : plainsTiles;
                var center = FindValidPlacementFromList(
                    candidates, aroundPositions, excludingRadius + 3, usedCoordinates);
                if (!center.HasValue) continue;

                int depositSize = rng.NextInt(config.mineralDepositSizeMin, config.mineralDepositSizeMax);
                var resourceType = (i % 2 == 0) ? ResourcePointType.OreMine : ResourcePointType.StoneQuarry;

                var mineralPlacements = GenerateResourceCluster(
                    resourceType, depositSize, center.Value, usedCoordinates);
                foreach (var placement in mineralPlacements)
                {
                    placements.Add(placement);
                    usedCoordinates.Add(placement.coordinate);
                }
            }

            // Scatter animals, preferring tiles near woodlines
            var nearTreeTiles = new List<HexCoordinate>();
            foreach (var treeCoord in treeCoordinates)
            {
                foreach (var neighbor in treeCoord.Neighbors())
                {
                    if (!usedCoordinates.Contains(neighbor) && !treeCoordinates.Contains(neighbor))
                    {
                        if (neighbor.q >= 0 && neighbor.q < Width && neighbor.r >= 0 && neighbor.r < Height)
                        {
                            nearTreeTiles.Add(neighbor);
                        }
                    }
                }
            }
            // Also add tiles at distance 2 from trees
            var nearTreeSet = new HashSet<HexCoordinate>(nearTreeTiles);
            foreach (var tile in new List<HexCoordinate>(nearTreeTiles))
            {
                foreach (var neighbor in tile.Neighbors())
                {
                    if (!usedCoordinates.Contains(neighbor) && !treeCoordinates.Contains(neighbor)
                        && !nearTreeSet.Contains(neighbor))
                    {
                        if (neighbor.q >= 0 && neighbor.q < Width && neighbor.r >= 0 && neighbor.r < Height)
                        {
                            nearTreeTiles.Add(neighbor);
                            nearTreeSet.Add(neighbor);
                        }
                    }
                }
            }
            rng.Shuffle(nearTreeTiles);

            for (int i = 0; i < config.animalCount; i++)
            {
                // Try near-tree tiles first, fall back to plains
                var candidates = nearTreeTiles.Count > 0 ? nearTreeTiles : plainsTiles;
                var coord = FindValidPlacementFromList(
                    candidates, aroundPositions, excludingRadius, usedCoordinates);
                if (!coord.HasValue) continue;

                var animalType = rng.NextBool() ? ResourcePointType.Deer : ResourcePointType.WildBoar;
                placements.Add(new ResourcePlacement(coord.Value, animalType));
                usedCoordinates.Add(coord.Value);
            }

            return placements;
        }

        /// <summary>
        /// Find a valid placement from a pre-collected tile list, respecting exclusion radius from start positions.
        /// Tries up to 20 random candidates.
        /// </summary>
        private HexCoordinate? FindValidPlacementFromList(
            List<HexCoordinate> candidates,
            List<HexCoordinate> startPositions,
            int exclusionRadius,
            HashSet<HexCoordinate> used)
        {
            return FindValidPlacement(rng, candidates, startPositions, exclusionRadius, used);
        }

        // ================================================================
        // Private Helper Methods
        // ================================================================

        /// <summary>
        /// Generate connected ridgelines via random walks with directional bias.
        /// Each ridge is 6-12 tiles long with foothills expanding outward.
        /// </summary>
        private void GenerateRidgelines(
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            List<HexCoordinate> startCoords)
        {
            int ridgeExclusionRadius = 8;

            for (int i = 0; i < config.ridgeCount; i++)
            {
                // Pick a seed point biased toward mid-map, away from edges and spawns
                HexCoordinate? seed = null;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    int q = rng.NextInt(6, Width - 7);
                    int r = rng.NextInt(6, Height - 7);
                    var candidate = new HexCoordinate(q, r);

                    // Skip if too close to any starting position
                    bool tooClose = false;
                    foreach (var startPos in startCoords)
                    {
                        if (candidate.Distance(startPos) < ridgeExclusionRadius)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    seed = candidate;
                    break;
                }

                if (!seed.HasValue) continue;

                // Random walk to form the ridge spine
                int ridgeLength = rng.NextInt(config.ridgeLengthMin, config.ridgeLengthMax);
                var spineTiles = new List<HexCoordinate> { seed.Value };
                var current = seed.Value;

                // Pick an initial direction (0-5 for hex neighbors)
                int direction = rng.NextInt(0, 5);

                for (int step = 1; step < ridgeLength; step++)
                {
                    // 70% continue same direction, 30% turn ±1
                    if (rng.NextDouble(0.0, 1.0) < 0.3)
                    {
                        direction = (direction + (rng.NextBool() ? 1 : 5)) % 6;
                    }

                    var neighbors = current.Neighbors();
                    // Neighbors() returns list of 6 neighbors in a fixed order
                    var next = neighbors[direction % neighbors.Count];

                    // Stay in bounds
                    if (next.q < 2 || next.q >= Width - 2 || next.r < 2 || next.r >= Height - 2)
                    {
                        // Try turning instead of going out of bounds
                        direction = (direction + (rng.NextBool() ? 2 : 4)) % 6;
                        next = neighbors[direction % neighbors.Count];
                        if (next.q < 2 || next.q >= Width - 2 || next.r < 2 || next.r >= Height - 2)
                            break;
                    }

                    // Skip if too close to a spawn
                    bool nearSpawn = false;
                    foreach (var startPos in startCoords)
                    {
                        if (next.Distance(startPos) < ridgeExclusionRadius)
                        {
                            nearSpawn = true;
                            break;
                        }
                    }
                    if (nearSpawn) break;

                    spineTiles.Add(next);
                    current = next;
                }

                // Set spine tiles as Hill with elevation 2
                foreach (var tile in spineTiles)
                {
                    TerrainGenerationData data;
                    if (terrain.TryGetValue(tile, out data))
                    {
                        data.terrain = TerrainType.Hill;
                        data.elevation = 2;
                        terrain[tile] = data;
                        ridgeTiles.Add(tile);
                    }
                }

                // Expand foothills (elevation 1) from spine tiles
                foreach (var tile in spineTiles)
                {
                    foreach (var neighbor in tile.Neighbors())
                    {
                        if (ridgeTiles.Contains(neighbor)) continue;
                        if (neighbor.q < 0 || neighbor.q >= Width || neighbor.r < 0 || neighbor.r >= Height) continue;

                        if (rng.NextDouble(0.0, 1.0) < config.ridgeFoothillChance)
                        {
                            TerrainGenerationData neighborData;
                            if (terrain.TryGetValue(neighbor, out neighborData))
                            {
                                if (neighborData.terrain == TerrainType.Plains)
                                {
                                    neighborData.terrain = TerrainType.Hill;
                                    neighborData.elevation = 1;
                                    terrain[neighbor] = neighborData;
                                    ridgeTiles.Add(neighbor);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Place a small mining hill (4-5 tiles) at distance 5-6 from spawn for starting ore/stone.
        /// Returns the hill tile coordinates.
        /// </summary>
        // Delegates to base class methods

        private List<HexCoordinate> PlaceStartingMiningHill(
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            HexCoordinate spawnPosition)
        {
            return base.PlaceStartingMiningHill(rng, terrain, spawnPosition, 4, 5);
        }

        private List<ResourcePlacement> PlaceResourceCluster(
            ResourcePointType type, int size, HexCoordinate center,
            int radius, HashSet<HexCoordinate> used)
        {
            return base.PlaceResourceCluster(rng, type, size, center, radius, used);
        }

        private List<ResourcePlacement> GenerateResourceCluster(
            ResourcePointType type, int size, HexCoordinate center,
            HashSet<HexCoordinate> used)
        {
            return base.GenerateResourceCluster(rng, type, size, center, used);
        }
    }
}
