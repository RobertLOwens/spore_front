using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Models;
using Sporefront.Data;

namespace Sporefront.Models.Combat
{
    public class ReinforcementGroup
    {
        public Guid id;
        public string name;
        public HexCoordinate sourceCoordinate;
        public HexCoordinate coordinate;
        public Guid targetArmyID;
        public Guid sourceBuildingID;
        public Dictionary<MilitaryUnitType, int> unitComposition;
        public List<HexCoordinate> movementPath;
        public int pathIndex;
        public double startTime;
        public bool isCancelled;
        public double segmentProgress;
        public Guid? ownerID;

        public ReinforcementGroup(
            Guid id,
            string name,
            HexCoordinate sourceCoordinate,
            Guid targetArmyID,
            Guid sourceBuildingID,
            Dictionary<MilitaryUnitType, int> units,
            double startTime,
            Guid? ownerID = null)
        {
            this.id = id;
            this.name = name;
            this.sourceCoordinate = sourceCoordinate;
            this.coordinate = sourceCoordinate;
            this.targetArmyID = targetArmyID;
            this.sourceBuildingID = sourceBuildingID;
            this.unitComposition = new Dictionary<MilitaryUnitType, int>(units);
            this.movementPath = new List<HexCoordinate>();
            this.pathIndex = 0;
            this.startTime = startTime;
            this.isCancelled = false;
            this.segmentProgress = 0.0;
            this.ownerID = ownerID;
        }

        public int GetTotalUnits()
        {
            int total = 0;
            foreach (var kvp in unitComposition)
                total += kvp.Value;
            return total;
        }

        public int GetUnitCount(MilitaryUnitType type)
        {
            return unitComposition.ContainsKey(type) ? unitComposition[type] : 0;
        }

        public string GetUnitsDescription()
        {
            var parts = new List<string>();
            foreach (var kvp in unitComposition)
            {
                if (kvp.Value > 0)
                    parts.Add($"{kvp.Value}x {kvp.Key.DisplayName()}");
            }
            return string.Join(", ", parts);
        }

        public void UpdateCoordinate(HexCoordinate newCoord)
        {
            coordinate = newCoord;
        }
    }
}
