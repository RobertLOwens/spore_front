using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    /// <summary>
    /// Wrapper for game saves with metadata.
    /// Contains a GameStateSnapshot plus metadata for the save/load UI.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string saveID;
        public string saveName;
        public string createdAt;   // ISO 8601
        public string modifiedAt;  // ISO 8601
        public int version = 1;    // Schema version for forward compat
        public GameStateSnapshot snapshot;

        // Map tile data (not included in GameStateSnapshot)
        public List<SerializedTileData> tiles;
        public List<SerializedResourcePoint> resourcePointPositions;

        // Game settings
        public double gameSpeed;
        public VisibilityMode visibilityMode;
    }

    /// <summary>
    /// Serializable tile data for save files.
    /// </summary>
    [Serializable]
    public struct SerializedTileData
    {
        public int q;
        public int r;
        public TerrainType terrain;
        public int elevation;

        public SerializedTileData(HexCoordinate coord, TileData tile)
        {
            q = coord.q;
            r = coord.r;
            terrain = tile.terrain;
            elevation = tile.elevation;
        }
    }

    /// <summary>
    /// Serializable resource point position data.
    /// </summary>
    [Serializable]
    public struct SerializedResourcePoint
    {
        public Guid id;
        public int q;
        public int r;

        public SerializedResourcePoint(Guid id, HexCoordinate coord)
        {
            this.id = id;
            q = coord.q;
            r = coord.r;
        }
    }

    /// <summary>
    /// Lightweight info for displaying save slots in the UI.
    /// </summary>
    public class SaveSlotInfo
    {
        public string saveID;
        public string saveName;
        public string modifiedAt;
        public int mapWidth;
        public int mapHeight;
    }
}
