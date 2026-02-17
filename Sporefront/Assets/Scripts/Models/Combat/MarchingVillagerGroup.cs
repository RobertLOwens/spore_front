using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Data;

namespace Sporefront.Models.Combat
{
    public class MarchingVillagerGroup
    {
        public Guid id;
        public string name;
        public HexCoordinate sourceCoordinate;
        public HexCoordinate coordinate;
        public Guid targetVillagerGroupID;
        public Guid sourceBuildingID;
        public int villagerCount;
        public List<HexCoordinate> movementPath;
        public int pathIndex;
        public double startTime;
        public bool isCancelled;
        public double segmentProgress;
        public Guid? ownerID;

        public MarchingVillagerGroup(
            Guid id,
            string name,
            HexCoordinate sourceCoordinate,
            Guid targetVillagerGroupID,
            Guid sourceBuildingID,
            int villagerCount,
            double startTime,
            Guid? ownerID = null)
        {
            this.id = id;
            this.name = name;
            this.sourceCoordinate = sourceCoordinate;
            this.coordinate = sourceCoordinate;
            this.targetVillagerGroupID = targetVillagerGroupID;
            this.sourceBuildingID = sourceBuildingID;
            this.villagerCount = villagerCount;
            this.movementPath = new List<HexCoordinate>();
            this.pathIndex = 0;
            this.startTime = startTime;
            this.isCancelled = false;
            this.segmentProgress = 0.0;
            this.ownerID = ownerID;
        }

        public string GetDescription()
        {
            return $"{villagerCount} villagers";
        }

        public void UpdateCoordinate(HexCoordinate newCoord)
        {
            coordinate = newCoord;
        }

        public void ApplyLosses(int count)
        {
            villagerCount = Math.Max(0, villagerCount - count);
        }
    }
}
