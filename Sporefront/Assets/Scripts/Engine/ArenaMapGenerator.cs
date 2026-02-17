// ============================================================================
// FILE: Engine/ArenaMapGenerator.cs
// PURPOSE: Simple 7x7 arena map generator for combat testing scenarios
//          C# port of ArenaMapGenerator.swift (68 lines)
// ============================================================================

using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    /// <summary>
    /// A minimal 7x7 arena map generator used for combat testing.
    /// All tiles are plains except the enemy position, which can be configured
    /// with a different terrain type (e.g., Hill or Mountain).
    /// </summary>
    public class ArenaMapGenerator : MapGeneratorBase
    {
        // ================================================================
        // Properties
        // ================================================================

        public override int Width => 7;
        public override int Height => 7;

        public TerrainType enemyTerrain;

        // ================================================================
        // Initialization
        // ================================================================

        public ArenaMapGenerator(ulong? seed = null, TerrainType enemyTerrain = TerrainType.Plains)
        {
            Seed = seed;
            this.enemyTerrain = enemyTerrain;
        }

        // ================================================================
        // MapGeneratorBase Implementation
        // ================================================================

        public override Dictionary<HexCoordinate, TerrainGenerationData> GenerateTerrain()
        {
            var terrain = new Dictionary<HexCoordinate, TerrainGenerationData>();

            // Fill all tiles with plains at elevation 0
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    terrain[coord] = new TerrainGenerationData(TerrainType.Plains, 0);
                }
            }

            // Set enemy position terrain and elevation
            var player2Pos = new HexCoordinate(4, 3);
            int elevation = enemyTerrain == TerrainType.Hill ? 1
                          : enemyTerrain == TerrainType.Mountain ? 2
                          : 0;
            terrain[player2Pos] = new TerrainGenerationData(enemyTerrain, elevation);

            return terrain;
        }

        public override List<PlayerStartPosition> GetStartingPositions()
        {
            var player1Pos = new HexCoordinate(2, 3);
            var player2Pos = new HexCoordinate(4, 3);

            return new List<PlayerStartPosition>
            {
                new PlayerStartPosition(player1Pos, 0),
                new PlayerStartPosition(player2Pos, 1)
            };
        }

        public override List<ResourcePlacement> GenerateStartingResources(HexCoordinate position)
        {
            // No resources in arena
            return new List<ResourcePlacement>();
        }

        public override List<ResourcePlacement> GenerateNeutralResources(
            int excludingRadius, List<HexCoordinate> aroundPositions)
        {
            // No neutral resources in arena
            return new List<ResourcePlacement>();
        }
    }
}
