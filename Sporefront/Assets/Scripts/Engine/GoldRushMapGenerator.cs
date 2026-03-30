// ============================================================================
// FILE: Engine/GoldRushMapGenerator.cs
// PURPOSE: Gold Rush map generator — massive central resource cluster with
//          barren outskirts. Players start with minimal local resources and
//          race to claim the rich center. Desert terrain around the center
//          punishes passive play. Fast, aggressive games.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // ================================================================
    // Gold Rush Map Configuration
    // ================================================================

    [Serializable]
    public class GoldRushMapConfig
    {
        // Central cluster (radius as fraction of map half-size)
        public float centerRadiusFraction = 0.22f;
        public float desertRingFraction = 0.15f;

        // Central cluster resources (the "gold rush" bounty)
        public int centerOreCount = 12;
        public int centerStoneCount = 8;
        public int centerForageCount = 6;
        public int centerTreePocketCount = 6;
        public int centerTreePocketSizeMin = 4;
        public int centerTreePocketSizeMax = 8;
        public int centerAnimalCount = 8;

        // Outskirts resources (sparse — just enough scattered resources)
        public int outskirtTreePocketCount = 4;
        public int outskirtTreePocketSizeMin = 3;
        public int outskirtTreePocketSizeMax = 5;
        public int outskirtAnimalCount = 4;
        public int outskirtMineralCount = 2;

        // Terrain
        public int ridgeCount = 2;
        public int ridgeLengthMin = 4;
        public int ridgeLengthMax = 8;
    }

    // ================================================================
    // Gold Rush Map Generator
    // ================================================================

    public class GoldRushMapGenerator : MapGeneratorBase
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
        public GoldRushMapConfig config;

        private List<PlayerStartPosition> cachedStartPositions;
        private Dictionary<HexCoordinate, TerrainGenerationData> cachedTerrain;

        // Zone tracking
        private HexCoordinate mapCenter;
        private int centerRadius;
        private int desertOuterRadius;

        // ================================================================
        // Initialization
        // ================================================================

        public GoldRushMapGenerator(int width = 35, int height = 35, ulong? seed = null, GoldRushMapConfig config = null)
        {
            _width = width;
            _height = height;
            startPadding = Math.Max(4, width / 5);
            Seed = seed;
            this.config = config ?? new GoldRushMapConfig();
            rng = new SeededRandom(seed ?? (ulong)DateTime.UtcNow.Ticks);

            mapCenter = new HexCoordinate(width / 2, height / 2);
            int halfSize = Math.Min(width, height) / 2;
            centerRadius = Math.Max(3, (int)(halfSize * this.config.centerRadiusFraction));
            desertOuterRadius = centerRadius + Math.Max(2, (int)(halfSize * this.config.desertRingFraction));
        }

        // ================================================================
        // MapGeneratorBase Implementation
        // ================================================================

        public override Dictionary<HexCoordinate, TerrainGenerationData> GenerateTerrain()
        {
            var terrain = new Dictionary<HexCoordinate, TerrainGenerationData>();

            // Step 1: Fill map with base plains
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    terrain[coord] = new TerrainGenerationData(TerrainType.Plains, 0);
                }
            }

            // Step 2: Desert ring around center
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    int dist = coord.Distance(mapCenter);
                    if (dist > centerRadius && dist <= desertOuterRadius)
                    {
                        terrain[coord] = new TerrainGenerationData(TerrainType.Desert, 0);
                    }
                }
            }

            // Step 3: Scatter a few small hills in center for ore/stone placement
            int hillClusterCount = rng.NextInt(2, 4);
            for (int i = 0; i < hillClusterCount; i++)
            {
                PlaceCenterHillCluster(terrain);
            }

            // Step 4: Small ridgelines in outskirts for variety
            var startPositions = GetStartingPositions();
            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);

            GenerateOutskirtRidges(terrain, startCoords);

            // Step 5: Flatten starting areas
            EnsureStartingAreasFlat(terrain, startCoords, 5);

            // Step 6: Place a mining hill near each starting position
            for (int i = 0; i < startPositions.Count; i++)
            {
                PlaceStartingMiningHill(terrain, startPositions[i].coordinate);
            }

            cachedTerrain = terrain;
            return terrain;
        }

        public override List<PlayerStartPosition> GetStartingPositions()
        {
            if (cachedStartPositions != null) return cachedStartPositions;

            int pad = startPadding;
            int w = Width;
            int h = Height;

            // Diagonal spawn pairs — same pattern as Arabia
            var spawnPairs = new (HexCoordinate p1, HexCoordinate p2)[]
            {
                (new HexCoordinate(pad, pad), new HexCoordinate(w - pad - 1, h - pad - 1)),
                (new HexCoordinate(w - pad - 1, pad), new HexCoordinate(pad, h - pad - 1)),
                (new HexCoordinate(pad, h - pad - 1), new HexCoordinate(w - pad - 1, pad)),
                (new HexCoordinate(w - pad - 1, h - pad - 1), new HexCoordinate(pad, pad)),
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

            foreach (var neighbor in position.Neighbors())
                usedCoordinates.Add(neighbor);

            // Reduced starting resources — just enough to bootstrap
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

            rng.Shuffle(availableCoords);

            // 1 wild boar (reduced from Arabia's 2)
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.WildBoar));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // 1 deer
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.Deer));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // 1 forage bush (reduced from Arabia's 2)
            {
                var coord = FindUnusedCoordinate(availableCoords, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.Forage));
                    usedCoordinates.Add(coord.Value);
                }
            }

            // 2 ore (reduced from Arabia's 4)
            var oreCluster = PlaceResourceCluster(
                ResourcePointType.OreMine, 2, position, startingResourceRadius, usedCoordinates);
            foreach (var placement in oreCluster)
            {
                placements.Add(placement);
                usedCoordinates.Add(placement.coordinate);
            }

            // 2 stone (reduced from Arabia's 3)
            var stoneCluster = PlaceResourceCluster(
                ResourcePointType.StoneQuarry, 2, position, startingResourceRadius, usedCoordinates);
            foreach (var placement in stoneCluster)
            {
                placements.Add(placement);
                usedCoordinates.Add(placement.coordinate);
            }

            // 2-3 small woodlines (reduced from Arabia's 4-6)
            int woodlineCount = rng.NextInt(2, 3);
            for (int i = 0; i < woodlineCount; i++)
            {
                int woodlineSize = rng.NextInt(2, 4);
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

            // Mark starting areas as used
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

            // Collect tiles by zone
            var centerTiles = new List<HexCoordinate>();
            var outskirtTiles = new List<HexCoordinate>();

            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    if (usedCoordinates.Contains(coord)) continue;

                    int dist = coord.Distance(mapCenter);
                    if (dist <= centerRadius)
                        centerTiles.Add(coord);
                    else if (dist > desertOuterRadius)
                        outskirtTiles.Add(coord);
                    // Desert ring tiles are intentionally left empty
                }
            }

            rng.Shuffle(centerTiles);
            rng.Shuffle(outskirtTiles);

            // === CENTER CLUSTER (the prize) ===

            // Large ore deposits
            PlaceMineralClusters(config.centerOreCount, ResourcePointType.OreMine, 2, 4,
                centerTiles, aroundPositions, 0, placements, usedCoordinates);

            // Large stone deposits
            PlaceMineralClusters(config.centerStoneCount, ResourcePointType.StoneQuarry, 2, 3,
                centerTiles, aroundPositions, 0, placements, usedCoordinates);

            // Forage bushes
            PlaceScatteredResources(config.centerForageCount, ResourcePointType.Forage,
                centerTiles, aroundPositions, 0, placements, usedCoordinates);

            // Tree pockets
            PlaceTreePockets(config.centerTreePocketCount,
                config.centerTreePocketSizeMin, config.centerTreePocketSizeMax,
                centerTiles, aroundPositions, 0, placements, usedCoordinates);

            // Animals
            PlaceAnimals(config.centerAnimalCount,
                centerTiles, aroundPositions, 0, placements, usedCoordinates);

            // === OUTSKIRTS (sparse) ===

            // A few small tree pockets
            PlaceTreePockets(config.outskirtTreePocketCount,
                config.outskirtTreePocketSizeMin, config.outskirtTreePocketSizeMax,
                outskirtTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Scattered animals
            PlaceAnimals(config.outskirtAnimalCount,
                outskirtTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Very few minerals in outskirts
            PlaceMineralClusters(config.outskirtMineralCount, ResourcePointType.OreMine, 1, 2,
                outskirtTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            return placements;
        }

        // ================================================================
        // Terrain Helpers
        // ================================================================

        private void PlaceCenterHillCluster(Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            // Pick a random point within the center radius
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int q = mapCenter.q + rng.NextInt(-centerRadius, centerRadius);
                int r = mapCenter.r + rng.NextInt(-centerRadius, centerRadius);
                var coord = new HexCoordinate(q, r);

                if (coord.Distance(mapCenter) > centerRadius) continue;
                if (coord.q < 0 || coord.q >= Width || coord.r < 0 || coord.r >= Height) continue;

                // BFS expand 3-5 hill tiles
                int hillSize = rng.NextInt(3, 5);
                var frontier = new List<HexCoordinate> { coord };
                var visited = new HashSet<HexCoordinate>();
                int placed = 0;

                while (placed < hillSize && frontier.Count > 0)
                {
                    var current = frontier[0];
                    frontier.RemoveAt(0);

                    if (visited.Contains(current)) continue;
                    if (current.q < 0 || current.q >= Width || current.r < 0 || current.r >= Height) continue;
                    if (current.Distance(mapCenter) > centerRadius) continue;
                    visited.Add(current);

                    TerrainGenerationData data;
                    if (terrain.TryGetValue(current, out data))
                    {
                        data.terrain = TerrainType.Hill;
                        data.elevation = 1;
                        terrain[current] = data;
                        placed++;

                        var neighbors = current.Neighbors();
                        var neighborList = new List<HexCoordinate>(neighbors);
                        rng.Shuffle(neighborList);
                        foreach (var neighbor in neighborList)
                        {
                            if (!visited.Contains(neighbor))
                                frontier.Add(neighbor);
                        }
                    }
                }
                return;
            }
        }

        private void GenerateOutskirtRidges(
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            List<HexCoordinate> startCoords)
        {
            int ridgeExclusionRadius = 8;

            for (int i = 0; i < config.ridgeCount; i++)
            {
                HexCoordinate? seed = null;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    int q = rng.NextInt(4, Width - 5);
                    int r = rng.NextInt(4, Height - 5);
                    var candidate = new HexCoordinate(q, r);

                    // Must be in outskirts (outside desert ring)
                    if (candidate.Distance(mapCenter) <= desertOuterRadius) continue;

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

                int ridgeLength = rng.NextInt(config.ridgeLengthMin, config.ridgeLengthMax);
                var current = seed.Value;
                int direction = rng.NextInt(0, 5);

                for (int step = 0; step < ridgeLength; step++)
                {
                    if (current.q < 2 || current.q >= Width - 2 || current.r < 2 || current.r >= Height - 2) break;
                    if (current.Distance(mapCenter) <= desertOuterRadius) break;

                    TerrainGenerationData data;
                    if (terrain.TryGetValue(current, out data))
                    {
                        data.terrain = TerrainType.Hill;
                        data.elevation = 1;
                        terrain[current] = data;
                    }

                    // Walk
                    if (rng.NextDouble(0.0, 1.0) < 0.3)
                        direction = (direction + (rng.NextBool() ? 1 : 5)) % 6;

                    var neighbors = current.Neighbors();
                    current = neighbors[direction % neighbors.Count];
                }
            }
        }

        private void PlaceStartingMiningHill(
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            HexCoordinate spawnPosition)
        {
            base.PlaceStartingMiningHill(rng, terrain, spawnPosition, 3, 4);
        }

        // ================================================================
        // Resource Placement Helpers — delegates to MapGeneratorBase
        // ================================================================

        private void PlaceTreePockets(int count, int sizeMin, int sizeMax,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            base.PlaceTreePockets(rng, count, sizeMin, sizeMax, zoneTiles, startPositions,
                exclusionRadius, placements, used);
        }

        private void PlaceMineralClusters(int count, ResourcePointType type, int sizeMin, int sizeMax,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var center = FindValidPlacement(rng, zoneTiles, startPositions, exclusionRadius, used);
                if (!center.HasValue) continue;

                int depositSize = rng.NextInt(sizeMin, sizeMax);
                var mineralPlacements = GenerateResourceCluster(rng, type, depositSize, center.Value, used);
                foreach (var placement in mineralPlacements)
                {
                    placements.Add(placement);
                    used.Add(placement.coordinate);
                }
            }
        }

        private void PlaceAnimals(int count,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            base.PlaceAnimals(rng, count, zoneTiles, startPositions, exclusionRadius, placements, used);
        }

        private void PlaceScatteredResources(int count, ResourcePointType type,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            base.PlaceScatteredResources(rng, count, type, zoneTiles, startPositions,
                exclusionRadius, placements, used);
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
