// ============================================================================
// FILE: Engine/MapGenerator.cs
// PURPOSE: Base map generator with resource placement and starting positions
//          C# port of MapGenerator.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
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

        // ================================================================
        // Domination Zone Placement
        // ================================================================

        public virtual List<ControlZoneData> GenerateControlZones(
            List<HexCoordinate> startPositions,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            if (startPositions.Count < 2 || Seed == null)
                return new List<ControlZoneData>();

            var rng = new SeededRandom(Seed.Value ^ 0xD0D0D0D0D0D0UL);
            var posA = startPositions[0];
            var posB = startPositions[1];

            // Compute midpoint between the two starting positions
            double midQ = (posA.q + posB.q) / 2.0;
            double midR = (posA.r + posB.r) / 2.0;

            // Axis from A to B
            double axisQ = posB.q - posA.q;
            double axisR = posB.r - posA.r;

            // Perpendicular direction (rotate 90°)
            double perpQ = -axisR;
            double perpR = axisQ;

            // Normalize perpendicular
            double perpLen = Math.Sqrt(perpQ * perpQ + perpR * perpR);
            if (perpLen > 0)
            {
                perpQ /= perpLen;
                perpR /= perpLen;
            }

            // Spacing: spread zones along perpendicular axis
            double spacing = Math.Min(Width, Height) / 4.0;

            string[] labels = { "A", "B", "C" };
            double[] offsets = { -spacing, 0.0, spacing };
            var zones = new List<ControlZoneData>();
            int zoneRadius = GameConfig.Domination.ZoneRadius;

            for (int i = 0; i < GameConfig.Domination.ZoneCount; i++)
            {
                // Base position along perpendicular
                double baseQ = midQ + perpQ * offsets[i];
                double baseR = midR + perpR * offsets[i];

                // Random jitter ±2 hexes
                int jitterQ = rng.NextInt(-2, 2);
                int jitterR = rng.NextInt(-2, 2);
                int centerQ = Math.Max(zoneRadius, Math.Min(Width - 1 - zoneRadius, (int)Math.Round(baseQ) + jitterQ));
                int centerR = Math.Max(zoneRadius, Math.Min(Height - 1 - zoneRadius, (int)Math.Round(baseR) + jitterR));

                var center = new HexCoordinate(centerQ, centerR);

                // Snap to nearest walkable tile if center is unwalkable
                TerrainGenerationData centerTerrain;
                if (terrain.TryGetValue(center, out centerTerrain) && !centerTerrain.terrain.IsWalkable())
                {
                    center = FindNearestWalkable(center, terrain, zoneRadius + 3) ?? center;
                }

                // Expand zone tiles within radius, filtering non-walkable
                var tiles = new List<HexCoordinate>();
                for (int q = -zoneRadius; q <= zoneRadius; q++)
                {
                    for (int r = -zoneRadius; r <= zoneRadius; r++)
                    {
                        var coord = new HexCoordinate(center.q + q, center.r + r);
                        if (coord.Distance(center) > zoneRadius) continue;
                        if (coord.q < 0 || coord.q >= Width || coord.r < 0 || coord.r >= Height) continue;
                        TerrainGenerationData td;
                        if (terrain.TryGetValue(coord, out td) && td.terrain.IsWalkable())
                            tiles.Add(coord);
                    }
                }

                zones.Add(new ControlZoneData(labels[i], center, tiles));
            }

            return zones;
        }

        protected HexCoordinate? FindNearestWalkable(
            HexCoordinate center,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain,
            int searchRadius)
        {
            for (int dist = 1; dist <= searchRadius; dist++)
            {
                for (int q = -dist; q <= dist; q++)
                {
                    for (int r = -dist; r <= dist; r++)
                    {
                        var coord = new HexCoordinate(center.q + q, center.r + r);
                        if (coord.Distance(center) != dist) continue;
                        TerrainGenerationData td;
                        if (terrain.TryGetValue(coord, out td) && td.terrain.IsWalkable())
                            return coord;
                    }
                }
            }
            return null;
        }

        // ================================================================
        // Zone Dispatch
        // ================================================================

        public virtual List<ControlZoneData> GenerateZonesForMode(
            GameMode mode,
            List<HexCoordinate> startPositions,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            switch (mode)
            {
                case GameMode.Domination:
                    return GenerateControlZones(startPositions, terrain);
                case GameMode.CrookedDomination:
                    return GenerateCrookedZones(startPositions, terrain);
                case GameMode.Ring:
                    return GenerateRingZones(startPositions, terrain);
                default:
                    return new List<ControlZoneData>();
            }
        }

        // ================================================================
        // Crooked Domination Zone Placement
        // ================================================================

        public virtual List<ControlZoneData> GenerateCrookedZones(
            List<HexCoordinate> startPositions,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            if (startPositions.Count < 2 || Seed == null)
                return new List<ControlZoneData>();

            var rng = new SeededRandom(Seed.Value ^ 0xC200CED000UL);
            var posA = startPositions[0];
            var posB = startPositions[1];

            double midQ = (posA.q + posB.q) / 2.0;
            double midR = (posA.r + posB.r) / 2.0;

            // Axis from A to B (normalized)
            double axisQ = posB.q - posA.q;
            double axisR = posB.r - posA.r;
            double axisLen = Math.Sqrt(axisQ * axisQ + axisR * axisR);
            if (axisLen > 0) { axisQ /= axisLen; axisR /= axisLen; }

            // Perpendicular (for jitter)
            double perpQ = -axisR;
            double perpR = axisQ;

            double offset = GameConfig.Domination.CrookedAxisOffset * axisLen;
            int zoneRadius = GameConfig.Domination.ZoneRadius;

            // Zone A: offset toward player A
            // Zone B: at midpoint
            // Zone C: offset toward player B
            var zoneDefs = new[]
            {
                (label: "A", qBase: midQ - axisQ * offset, rBase: midR - axisR * offset),
                (label: "B", qBase: midQ, rBase: midR),
                (label: "C", qBase: midQ + axisQ * offset, rBase: midR + axisR * offset),
            };

            var zones = new List<ControlZoneData>();
            foreach (var def in zoneDefs)
            {
                // Jitter on perpendicular axis only (the axis offset is intentional)
                int jitterQ = (int)Math.Round(perpQ * rng.NextInt(-2, 2));
                int jitterR = (int)Math.Round(perpR * rng.NextInt(-2, 2));
                int centerQ = Math.Max(zoneRadius, Math.Min(Width - 1 - zoneRadius, (int)Math.Round(def.qBase) + jitterQ));
                int centerR = Math.Max(zoneRadius, Math.Min(Height - 1 - zoneRadius, (int)Math.Round(def.rBase) + jitterR));

                var center = new HexCoordinate(centerQ, centerR);

                TerrainGenerationData centerTerrain;
                if (terrain.TryGetValue(center, out centerTerrain) && !centerTerrain.terrain.IsWalkable())
                    center = FindNearestWalkable(center, terrain, zoneRadius + 3) ?? center;

                var tiles = ExpandZoneTiles(center, zoneRadius, terrain);
                zones.Add(new ControlZoneData(def.label, center, tiles));
            }

            return zones;
        }

        // ================================================================
        // Ring Zone Placement
        // ================================================================

        public virtual List<ControlZoneData> GenerateRingZones(
            List<HexCoordinate> startPositions,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            if (Seed == null)
                return new List<ControlZoneData>();

            int centerQ = Width / 2;
            int centerR = Height / 2;
            var mapCenter = new HexCoordinate(centerQ, centerR);

            // Snap to walkable if needed
            TerrainGenerationData centerTerrain;
            if (terrain.TryGetValue(mapCenter, out centerTerrain) && !centerTerrain.terrain.IsWalkable())
                mapCenter = FindNearestWalkable(mapCenter, terrain, 5) ?? mapCenter;

            int innerRadius = GameConfig.Domination.RingInnerRadius;
            int outerRadius = GameConfig.Domination.RingOuterRadius;

            // Inner zone: all tiles within innerRadius
            var innerTiles = ExpandZoneTiles(mapCenter, innerRadius, terrain);

            // Outer zone: ring of tiles from innerRadius+1 to outerRadius
            var outerTiles = new List<HexCoordinate>();
            for (int q = -outerRadius; q <= outerRadius; q++)
            {
                for (int r = -outerRadius; r <= outerRadius; r++)
                {
                    var coord = new HexCoordinate(mapCenter.q + q, mapCenter.r + r);
                    int dist = coord.Distance(mapCenter);
                    if (dist <= innerRadius || dist > outerRadius) continue;
                    if (coord.q < 0 || coord.q >= Width || coord.r < 0 || coord.r >= Height) continue;
                    TerrainGenerationData td;
                    if (terrain.TryGetValue(coord, out td) && td.terrain.IsWalkable())
                        outerTiles.Add(coord);
                }
            }

            var zones = new List<ControlZoneData>();
            zones.Add(new ControlZoneData("Inner", mapCenter, innerTiles, GameConfig.Domination.RingInnerMultiplier));
            zones.Add(new ControlZoneData("Outer", mapCenter, outerTiles, GameConfig.Domination.RingOuterMultiplier));

            return zones;
        }

        // ================================================================
        // Shared Zone Tile Expansion
        // ================================================================

        protected List<HexCoordinate> ExpandZoneTiles(
            HexCoordinate center, int radius,
            Dictionary<HexCoordinate, TerrainGenerationData> terrain)
        {
            var tiles = new List<HexCoordinate>();
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    var coord = new HexCoordinate(center.q + q, center.r + r);
                    if (coord.Distance(center) > radius) continue;
                    if (coord.q < 0 || coord.q >= Width || coord.r < 0 || coord.r >= Height) continue;
                    TerrainGenerationData td;
                    if (terrain.TryGetValue(coord, out td) && td.terrain.IsWalkable())
                        tiles.Add(coord);
                }
            }
            return tiles;
        }
    }
}
