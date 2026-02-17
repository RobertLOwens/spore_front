// ============================================================================
// FILE: Engine/MapGenerator.cs
// PURPOSE: Base map generator with resource placement and starting positions
//          C# port of MapGenerator.swift
// ============================================================================

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
    }
}
