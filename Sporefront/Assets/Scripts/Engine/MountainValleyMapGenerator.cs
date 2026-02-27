// ============================================================================
// FILE: Engine/MountainValleyMapGenerator.cs
// PURPOSE: Mountain Valley map generator — two ridges with a resource-rich
//          valley between them. Players spawn on opposite ridges and must
//          push downhill to contest resources.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    // ================================================================
    // Mountain Valley Map Configuration
    // ================================================================

    [Serializable]
    public class MountainValleyMapConfig
    {
        // Slope resources (per slope, so doubled for both sides)
        public int slopeTreePocketCount = 10;
        public int slopeTreePocketSizeMin = 5;
        public int slopeTreePocketSizeMax = 10;
        public int slopeMineralCount = 6;
        public int slopeMineralSizeMin = 2;
        public int slopeMineralSizeMax = 5;

        // Valley floor resources
        public int valleyTreePocketCount = 8;
        public int valleyTreePocketSizeMin = 6;
        public int valleyTreePocketSizeMax = 12;
        public int valleyAnimalCount = 10;

        // Ridge resources (per ridge)
        public int ridgeTreePocketCount = 2;
        public int ridgeTreePocketSizeMin = 2;
        public int ridgeTreePocketSizeMax = 4;
        public int ridgeAnimalCount = 3;

        // Terrain
        public int maxElevation = 3;
    }

    // ================================================================
    // Mountain Valley Map Generator
    // ================================================================

    public class MountainValleyMapGenerator : MapGeneratorBase
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
        public MountainValleyMapConfig config;

        // Zone boundaries: jittered per column for organic edges
        // Stored after GenerateTerrain so GetStartingPositions can use them
        private int[] ridgeTopEnd;      // top ridge ends at this row (exclusive)
        private int[] slopeTopEnd;      // upper slope ends at this row (exclusive)
        private int[] valleyEnd;        // valley floor ends at this row (exclusive)
        private int[] slopeBottomEnd;   // lower slope ends at this row (exclusive)

        // Cached starting positions — avoids RNG drift when called multiple times
        private List<PlayerStartPosition> cachedStartPositions;

        // ================================================================
        // Zone Enum
        // ================================================================

        private enum Zone
        {
            TopRidge,
            UpperSlope,
            ValleyFloor,
            LowerSlope,
            BottomRidge
        }

        // ================================================================
        // Initialization
        // ================================================================

        public MountainValleyMapGenerator(int width = 35, int height = 35, ulong? seed = null, MountainValleyMapConfig config = null)
        {
            _width = width;
            _height = height;
            startPadding = Math.Max(4, height / 8);
            Seed = seed;
            this.config = config ?? new MountainValleyMapConfig();
            rng = new SeededRandom(seed ?? (ulong)DateTime.UtcNow.Ticks);
        }

        // ================================================================
        // MapGeneratorBase Implementation
        // ================================================================

        public override Dictionary<HexCoordinate, TerrainGenerationData> GenerateTerrain()
        {
            var terrain = new Dictionary<HexCoordinate, TerrainGenerationData>();

            // Compute zone boundary rows (with ±2 jitter per column)
            ComputeZoneBoundaries();

            // Fill map based on zone membership
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    var zone = GetZone(q, r);

                    switch (zone)
                    {
                        case Zone.TopRidge:
                        case Zone.BottomRidge:
                            terrain[coord] = new TerrainGenerationData(
                                TerrainType.Hill,
                                rng.NextInt(2, config.maxElevation));
                            break;

                        case Zone.UpperSlope:
                        case Zone.LowerSlope:
                        {
                            // Elevation decreases toward the valley
                            int midRow = Height / 2;
                            int distFromValley = Math.Abs(r - midRow);
                            int maxDist = Height / 4;
                            int elevation = (distFromValley > maxDist / 2) ? 2 : 1;
                            terrain[coord] = new TerrainGenerationData(TerrainType.Hill, elevation);
                            break;
                        }

                        case Zone.ValleyFloor:
                            terrain[coord] = new TerrainGenerationData(TerrainType.Plains, 0);
                            break;
                    }
                }
            }

            // Flatten starting areas
            var startPositions = GetStartingPositions();
            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);

            EnsureStartingAreasFlat(terrain, startCoords, 5);

            return terrain;
        }

        public override List<PlayerStartPosition> GetStartingPositions()
        {
            if (cachedStartPositions != null) return cachedStartPositions;

            int centerQ = Width / 2;

            // Randomize left/right offset within ridge so spawns aren't always dead center
            int offsetRange = Math.Max(1, Width / 6);
            int offset1 = rng.NextInt(-offsetRange, offsetRange);
            int offset2 = rng.NextInt(-offsetRange, offsetRange);

            int pad = startPadding;
            int q1 = Math.Max(pad, Math.Min(Width - pad - 1, centerQ + offset1));
            int q2 = Math.Max(pad, Math.Min(Width - pad - 1, centerQ + offset2));

            // Place spawns within the actual ridge zones instead of at a fixed padding row
            int topRow, botRow;
            if (ridgeTopEnd != null)
            {
                // Zone boundaries are computed — use midpoint of each ridge zone
                int topRidgeMid = ridgeTopEnd[centerQ] / 2;
                topRow = Math.Max(2, topRidgeMid);

                int botRidgeMid = (slopeBottomEnd[centerQ] + Height - 1) / 2;
                botRow = Math.Min(Height - 3, botRidgeMid);
            }
            else
            {
                // Fallback: estimate from zone percentages (ridge = 0–12.5%)
                topRow = Math.Max(2, (int)(Height * 0.0625));
                botRow = Math.Min(Height - 3, (int)(Height * 0.9375));
            }

            cachedStartPositions = new List<PlayerStartPosition>
            {
                new PlayerStartPosition(new HexCoordinate(q1, topRow), 0),
                new PlayerStartPosition(new HexCoordinate(q2, botRow), 1)
            };
            return cachedStartPositions;
        }

        public override List<ResourcePlacement> GenerateStartingResources(HexCoordinate position)
        {
            var placements = new List<ResourcePlacement>();
            var usedCoordinates = new HashSet<HexCoordinate> { position };

            // Get all valid coordinates within starting radius, excluding a 3-tile gap around center
            int minResourceDistance = 3;
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

            // Place 2 forage bushes
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

            // Place 2-3 woodlines (ridge is sparse — reduced from Arabia's 4-6)
            int woodlineCount = rng.NextInt(2, 3);
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

            // Ensure zone boundaries are computed
            if (ridgeTopEnd == null) ComputeZoneBoundaries();

            // Collect tiles by zone for targeted placement
            var slopeTiles = new List<HexCoordinate>();
            var valleyTiles = new List<HexCoordinate>();
            var ridgeTiles = new List<HexCoordinate>();

            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    if (usedCoordinates.Contains(coord)) continue;

                    var zone = GetZone(q, r);
                    switch (zone)
                    {
                        case Zone.UpperSlope:
                        case Zone.LowerSlope:
                            slopeTiles.Add(coord);
                            break;
                        case Zone.ValleyFloor:
                            valleyTiles.Add(coord);
                            break;
                        case Zone.TopRidge:
                        case Zone.BottomRidge:
                            ridgeTiles.Add(coord);
                            break;
                    }
                }
            }

            rng.Shuffle(slopeTiles);
            rng.Shuffle(valleyTiles);
            rng.Shuffle(ridgeTiles);

            // --- SLOPE RESOURCES (both slopes combined) ---
            // Tree pockets
            int slopeTrees = config.slopeTreePocketCount * 2; // both slopes
            PlaceTreePockets(slopeTrees, config.slopeTreePocketSizeMin, config.slopeTreePocketSizeMax,
                slopeTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Mineral deposits on slopes
            int slopeMinerals = config.slopeMineralCount * 2; // both slopes
            PlaceMineralDeposits(slopeMinerals, config.slopeMineralSizeMin, config.slopeMineralSizeMax,
                slopeTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Forage on slopes
            int slopeForage = config.slopeMineralCount; // roughly same as mineral count
            PlaceScatteredResources(slopeForage, ResourcePointType.Forage,
                slopeTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Huntable animals on slopes
            int slopeAnimals = config.ridgeAnimalCount * 2;
            PlaceAnimals(slopeAnimals, slopeTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // --- VALLEY FLOOR RESOURCES (densest) ---
            // Tree pockets — largest clusters
            PlaceTreePockets(config.valleyTreePocketCount, config.valleyTreePocketSizeMin, config.valleyTreePocketSizeMax,
                valleyTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Huntable animals — most numerous in valley
            PlaceAnimals(config.valleyAnimalCount, valleyTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Some forage in valley
            PlaceScatteredResources(4, ResourcePointType.Forage,
                valleyTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // --- RIDGE RESOURCES (very sparse) ---
            // Small tree clusters on ridges
            int ridgeTrees = config.ridgeTreePocketCount * 2; // both ridges
            PlaceTreePockets(ridgeTrees, config.ridgeTreePocketSizeMin, config.ridgeTreePocketSizeMax,
                ridgeTiles, aroundPositions, excludingRadius, placements, usedCoordinates);

            // Scattered deer on ridges
            int ridgeAnimals = config.ridgeAnimalCount * 2; // both ridges
            for (int i = 0; i < ridgeAnimals; i++)
            {
                var coord = FindValidPlacement(ridgeTiles, aroundPositions, excludingRadius, usedCoordinates);
                if (coord.HasValue)
                {
                    placements.Add(new ResourcePlacement(coord.Value, ResourcePointType.Deer));
                    usedCoordinates.Add(coord.Value);
                }
            }

            return placements;
        }

        // ================================================================
        // Zone Computation
        // ================================================================

        private void ComputeZoneBoundaries()
        {
            ridgeTopEnd = new int[Width];
            slopeTopEnd = new int[Width];
            valleyEnd = new int[Width];
            slopeBottomEnd = new int[Width];

            // Base zone boundaries (row indices)
            int baseRidgeTop = (int)(Height * 0.125);        // ~12.5%
            int baseSlopeTop = (int)(Height * 0.375);         // 12.5% + 25%
            int baseValley = (int)(Height * 0.625);           // + 25%
            int baseSlopeBottom = (int)(Height * 0.875);      // + 25%

            for (int q = 0; q < Width; q++)
            {
                // ±2 row jitter per column for organic, non-straight edges
                ridgeTopEnd[q] = Clamp(baseRidgeTop + rng.NextInt(-2, 2), 2, Height - 2);
                slopeTopEnd[q] = Clamp(baseSlopeTop + rng.NextInt(-2, 2), ridgeTopEnd[q] + 1, Height - 2);
                valleyEnd[q] = Clamp(baseValley + rng.NextInt(-2, 2), slopeTopEnd[q] + 1, Height - 2);
                slopeBottomEnd[q] = Clamp(baseSlopeBottom + rng.NextInt(-2, 2), valleyEnd[q] + 1, Height - 1);
            }
        }

        private Zone GetZone(int q, int r)
        {
            int col = Math.Max(0, Math.Min(Width - 1, q));

            if (r < ridgeTopEnd[col]) return Zone.TopRidge;
            if (r < slopeTopEnd[col]) return Zone.UpperSlope;
            if (r < valleyEnd[col]) return Zone.ValleyFloor;
            if (r < slopeBottomEnd[col]) return Zone.LowerSlope;
            return Zone.BottomRidge;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ================================================================
        // Resource Placement Helpers
        // ================================================================

        private void PlaceTreePockets(int count, int sizeMin, int sizeMax,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            int treeExclusionRadius = 6;
            for (int i = 0; i < count; i++)
            {
                var center = FindValidPlacementWithExclusion(zoneTiles, startPositions, treeExclusionRadius, used);
                if (!center.HasValue) continue;

                int pocketSize = rng.NextInt(sizeMin, sizeMax);
                var treePlacements = GenerateResourceCluster(
                    ResourcePointType.Trees, pocketSize, center.Value, used);
                foreach (var placement in treePlacements)
                {
                    placements.Add(placement);
                    used.Add(placement.coordinate);
                }
            }
        }

        private void PlaceMineralDeposits(int count, int sizeMin, int sizeMax,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var center = FindValidPlacementWithExclusion(zoneTiles, startPositions, exclusionRadius + 3, used);
                if (!center.HasValue) continue;

                int depositSize = rng.NextInt(sizeMin, sizeMax);
                var resourceType = (i % 2 == 0) ? ResourcePointType.OreMine : ResourcePointType.StoneQuarry;

                var mineralPlacements = GenerateResourceCluster(
                    resourceType, depositSize, center.Value, used);
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
            for (int i = 0; i < count; i++)
            {
                var coord = FindValidPlacement(zoneTiles, startPositions, exclusionRadius, used);
                if (!coord.HasValue) continue;

                var animalType = rng.NextBool() ? ResourcePointType.Deer : ResourcePointType.WildBoar;
                placements.Add(new ResourcePlacement(coord.Value, animalType));
                used.Add(coord.Value);
            }
        }

        private void PlaceScatteredResources(int count, ResourcePointType type,
            List<HexCoordinate> zoneTiles, List<HexCoordinate> startPositions,
            int exclusionRadius, List<ResourcePlacement> placements, HashSet<HexCoordinate> used)
        {
            for (int i = 0; i < count; i++)
            {
                var coord = FindValidPlacement(zoneTiles, startPositions, exclusionRadius, used);
                if (!coord.HasValue) continue;

                placements.Add(new ResourcePlacement(coord.Value, type));
                used.Add(coord.Value);
            }
        }

        private HexCoordinate? FindValidPlacement(
            List<HexCoordinate> candidates, List<HexCoordinate> startPositions,
            int exclusionRadius, HashSet<HexCoordinate> used)
        {
            return FindValidPlacementWithExclusion(candidates, startPositions, exclusionRadius, used);
        }

        private HexCoordinate? FindValidPlacementWithExclusion(
            List<HexCoordinate> candidates, List<HexCoordinate> startPositions,
            int exclusionRadius, HashSet<HexCoordinate> used)
        {
            // Try up to 20 random candidates from the zone tile list
            for (int attempt = 0; attempt < 20 && candidates.Count > 0; attempt++)
            {
                int idx = rng.NextInt(0, candidates.Count - 1);
                var coord = candidates[idx];

                if (used.Contains(coord)) continue;

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

                return coord;
            }
            return null;
        }

        // ================================================================
        // BFS Cluster Generation (same pattern as Arabia)
        // ================================================================

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
