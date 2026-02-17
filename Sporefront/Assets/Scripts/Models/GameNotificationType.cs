namespace Sporefront.Models
{
    // Abstract class hierarchy replaces Swift enum with associated values
    [System.Serializable]
    public abstract class GameNotificationType
    {
        public abstract string Icon { get; }
        public abstract string Message { get; }
        public abstract string NotificationTitle { get; }
        public abstract HexCoordinate? Coordinate { get; }
        public abstract string DeduplicationKey { get; }
        public abstract int Priority { get; }
    }

    public class GatheringCompletedNotification : GameNotificationType
    {
        public ResourceType ResourceType;
        public int Amount;
        public HexCoordinate Location;

        public GatheringCompletedNotification(ResourceType resourceType, int amount, HexCoordinate location)
        {
            ResourceType = resourceType;
            Amount = amount;
            Location = location;
        }

        public override string Icon => ResourceType.Icon();
        public override string Message => $"Gathered {Amount} {ResourceType.DisplayName()}";
        public override string NotificationTitle => "Resource Alert";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"gathering_{ResourceType}";
        public override int Priority => 20;
    }

    public class BuildingCompletedNotification : GameNotificationType
    {
        public BuildingType BuildingType;
        public HexCoordinate Location;

        public BuildingCompletedNotification(BuildingType buildingType, HexCoordinate location)
        {
            BuildingType = buildingType;
            Location = location;
        }

        public override string Icon => "building_completed";
        public override string Message => $"{BuildingType.DisplayName()} completed";
        public override string NotificationTitle => "Building Update";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"building_{BuildingType}";
        public override int Priority => 50;
    }

    public class UpgradeCompletedNotification : GameNotificationType
    {
        public BuildingType BuildingType;
        public int NewLevel;
        public HexCoordinate Location;

        public UpgradeCompletedNotification(BuildingType buildingType, int newLevel, HexCoordinate location)
        {
            BuildingType = buildingType;
            NewLevel = newLevel;
            Location = location;
        }

        public override string Icon => "upgrade";
        public override string Message => $"{BuildingType.DisplayName()} upgraded to level {NewLevel}";
        public override string NotificationTitle => "Building Update";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"upgrade_{BuildingType}";
        public override int Priority => 50;
    }

    public class ArmyAttackedNotification : GameNotificationType
    {
        public string ArmyName;
        public HexCoordinate Location;

        public ArmyAttackedNotification(string armyName, HexCoordinate location)
        {
            ArmyName = armyName;
            Location = location;
        }

        public override string Icon => "combat";
        public override string Message => $"{ArmyName} is under attack!";
        public override string NotificationTitle => "Combat Alert";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"armyAttacked_{Location.q}_{Location.r}";
        public override int Priority => 100;
    }

    public class ArmySightedNotification : GameNotificationType
    {
        public HexCoordinate Location;

        public ArmySightedNotification(HexCoordinate location) { Location = location; }

        public override string Icon => "sighted";
        public override string Message => "Enemy army spotted!";
        public override string NotificationTitle => "Enemy Sighted";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"armySighted_{Location.q}_{Location.r}";
        public override int Priority => 80;
    }

    public class VillagerAttackedNotification : GameNotificationType
    {
        public HexCoordinate Location;

        public VillagerAttackedNotification(HexCoordinate location) { Location = location; }

        public override string Icon => "alert";
        public override string Message => "Villagers are under attack!";
        public override string NotificationTitle => "Combat Alert";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"villagerAttacked_{Location.q}_{Location.r}";
        public override int Priority => 90;
    }

    public class ResourcesMaxedNotification : GameNotificationType
    {
        public ResourceType ResourceType;

        public ResourcesMaxedNotification(ResourceType resourceType) { ResourceType = resourceType; }

        public override string Icon => ResourceType.Icon();
        public override string Message => $"{ResourceType.DisplayName()} storage is full!";
        public override string NotificationTitle => "Resource Alert";
        public override HexCoordinate? Coordinate => null;
        public override string DeduplicationKey => $"resourcesMaxed_{ResourceType}";
        public override int Priority => 70;
    }

    public class ResearchCompletedNotification : GameNotificationType
    {
        public string ResearchName;

        public ResearchCompletedNotification(string researchName) { ResearchName = researchName; }

        public override string Icon => "research";
        public override string Message => $"{ResearchName} research completed";
        public override string NotificationTitle => "Research Complete";
        public override HexCoordinate? Coordinate => null;
        public override string DeduplicationKey => $"research_{ResearchName}";
        public override int Priority => 60;
    }

    public class ResourcePointDepletedNotification : GameNotificationType
    {
        public string ResourceTypeName;
        public HexCoordinate Location;

        public ResourcePointDepletedNotification(string resourceTypeName, HexCoordinate location)
        {
            ResourceTypeName = resourceTypeName;
            Location = location;
        }

        public override string Icon => "warning";
        public override string Message => $"{ResourceTypeName} deposit depleted";
        public override string NotificationTitle => "Resource Alert";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"depleted_{ResourceTypeName}_{Location.q}_{Location.r}";
        public override int Priority => 30;
    }

    public class TrainingCompletedNotification : GameNotificationType
    {
        public string UnitTypeName;
        public int Quantity;
        public HexCoordinate Location;

        public TrainingCompletedNotification(string unitTypeName, int quantity, HexCoordinate location)
        {
            UnitTypeName = unitTypeName;
            Quantity = quantity;
            Location = location;
        }

        public override string Icon => "training";
        public override string Message => $"Training complete: {Quantity}x {UnitTypeName}";
        public override string NotificationTitle => "Training Complete";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"training_{UnitTypeName}";
        public override int Priority => 40;
    }

    public class EntrenchmentCompletedNotification : GameNotificationType
    {
        public string ArmyName;
        public HexCoordinate Location;

        public EntrenchmentCompletedNotification(string armyName, HexCoordinate location)
        {
            ArmyName = armyName;
            Location = location;
        }

        public override string Icon => "entrenchment";
        public override string Message => $"{ArmyName} has finished entrenching";
        public override string NotificationTitle => "Army Update";
        public override HexCoordinate? Coordinate => Location;
        public override string DeduplicationKey => $"entrenchment_{ArmyName}";
        public override int Priority => 45;
    }
}
