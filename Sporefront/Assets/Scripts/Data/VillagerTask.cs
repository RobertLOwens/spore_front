using System;
using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public abstract class VillagerTask
    {
        public abstract string DisplayName { get; }
        public abstract bool IsIdle { get; }
    }

    [System.Serializable]
    public class IdleTask : VillagerTask
    {
        public override string DisplayName => "Idle";
        public override bool IsIdle => true;
    }

    [System.Serializable]
    public class BuildingTask : VillagerTask
    {
        public Guid BuildingID;
        public BuildingTask(Guid buildingID) { BuildingID = buildingID; }
        public override string DisplayName => "Building";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class GatheringTask : VillagerTask
    {
        public ResourceType GatherResourceType;
        public GatheringTask(ResourceType resourceType) { GatherResourceType = resourceType; }
        public override string DisplayName => $"Gathering {GatherResourceType.DisplayName()}";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class GatheringResourceTask : VillagerTask
    {
        public Guid ResourcePointID;
        public GatheringResourceTask(Guid resourcePointID) { ResourcePointID = resourcePointID; }
        public override string DisplayName => "Gathering";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class HuntingTask : VillagerTask
    {
        public Guid ResourcePointID;
        public HuntingTask(Guid resourcePointID) { ResourcePointID = resourcePointID; }
        public override string DisplayName => "Hunting";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class RepairingTask : VillagerTask
    {
        public Guid BuildingID;
        public RepairingTask(Guid buildingID) { BuildingID = buildingID; }
        public override string DisplayName => "Repairing";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class MovingTask : VillagerTask
    {
        public HexCoordinate TargetCoordinate;
        public MovingTask(HexCoordinate target) { TargetCoordinate = target; }
        public override string DisplayName => $"Moving to {TargetCoordinate}";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class UpgradingTask : VillagerTask
    {
        public Guid BuildingID;
        public UpgradingTask(Guid buildingID) { BuildingID = buildingID; }
        public override string DisplayName => "Upgrading";
        public override bool IsIdle => false;
    }

    [System.Serializable]
    public class DemolishingTask : VillagerTask
    {
        public Guid BuildingID;
        public DemolishingTask(Guid buildingID) { BuildingID = buildingID; }
        public override string DisplayName => "Demolishing";
        public override bool IsIdle => false;
    }
}
