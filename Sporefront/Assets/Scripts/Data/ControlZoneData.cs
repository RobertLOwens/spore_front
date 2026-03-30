using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public class ControlZoneData
    {
        public string label;
        public HexCoordinate center;
        public List<HexCoordinate> tiles;
        public Guid? controllingPlayerID;
        public Dictionary<Guid, int> presenceCount;
        public double pointsMultiplier = 1.0;

        public ControlZoneData(string label, HexCoordinate center, List<HexCoordinate> tiles, double pointsMultiplier = 1.0)
        {
            this.label = label;
            this.center = center;
            this.tiles = tiles;
            this.controllingPlayerID = null;
            this.presenceCount = new Dictionary<Guid, int>();
            this.pointsMultiplier = pointsMultiplier;
        }

        // Default constructor for deserialization
        public ControlZoneData()
        {
            this.tiles = new List<HexCoordinate>();
            this.presenceCount = new Dictionary<Guid, int>();
            this.pointsMultiplier = 1.0;
        }
    }
}
