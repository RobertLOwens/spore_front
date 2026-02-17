using System.Collections.Generic;

namespace Sporefront.Models
{
    public enum BuildingCategory
    {
        Economic,
        Military
    }

    public enum BuildingState
    {
        Planning,
        Constructing,
        Completed,
        Upgrading,
        Demolishing,
        Damaged,
        Destroyed
    }

    public enum BuildingType
    {
        // Economic Buildings
        CityCenter,
        Farm,
        Neighborhood,
        Blacksmith,
        Market,
        MiningCamp,
        LumberCamp,
        Warehouse,
        University,
        Library,
        Mill,

        // Infrastructure
        Road,

        // Military Buildings
        Castle,
        Barracks,
        ArcheryRange,
        Stable,
        SiegeWorkshop,
        Tower,
        WoodenFort,
        Wall,
        Gate
    }

    public static class BuildingTypeExtensions
    {
        public static string DisplayName(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return "City Center";
                case BuildingType.Farm: return "Farm";
                case BuildingType.Neighborhood: return "Neighborhood";
                case BuildingType.Blacksmith: return "Blacksmith";
                case BuildingType.Market: return "Market";
                case BuildingType.MiningCamp: return "Mining Camp";
                case BuildingType.LumberCamp: return "Lumber Camp";
                case BuildingType.Warehouse: return "Warehouse";
                case BuildingType.University: return "University";
                case BuildingType.Library: return "Library";
                case BuildingType.Mill: return "Mill";
                case BuildingType.Road: return "Road";
                case BuildingType.Castle: return "Castle";
                case BuildingType.Barracks: return "Barracks";
                case BuildingType.ArcheryRange: return "Archery Range";
                case BuildingType.Stable: return "Stable";
                case BuildingType.SiegeWorkshop: return "Siege Workshop";
                case BuildingType.Tower: return "Tower";
                case BuildingType.WoodenFort: return "Wooden Fort";
                case BuildingType.Wall: return "Wall";
                case BuildingType.Gate: return "Gate";
                default: return type.ToString();
            }
        }

        public static string Icon(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return "city_center";
                case BuildingType.Farm: return "farm";
                case BuildingType.Neighborhood: return "neighborhood";
                case BuildingType.Blacksmith: return "blacksmith";
                case BuildingType.Market: return "market";
                case BuildingType.MiningCamp: return "mining_camp";
                case BuildingType.LumberCamp: return "lumber_camp";
                case BuildingType.Warehouse: return "warehouse";
                case BuildingType.University: return "university";
                case BuildingType.Library: return "library";
                case BuildingType.Mill: return "mill";
                case BuildingType.Road: return "road";
                case BuildingType.Castle: return "castle";
                case BuildingType.Barracks: return "barracks";
                case BuildingType.ArcheryRange: return "archery_range";
                case BuildingType.Stable: return "stable";
                case BuildingType.SiegeWorkshop: return "siege_workshop";
                case BuildingType.Tower: return "tower";
                case BuildingType.WoodenFort: return "wooden_fort";
                case BuildingType.Wall: return "wall";
                case BuildingType.Gate: return "gate";
                default: return "";
            }
        }

        public static BuildingCategory Category(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter:
                case BuildingType.Farm:
                case BuildingType.Neighborhood:
                case BuildingType.Blacksmith:
                case BuildingType.Market:
                case BuildingType.MiningCamp:
                case BuildingType.LumberCamp:
                case BuildingType.Warehouse:
                case BuildingType.University:
                case BuildingType.Library:
                case BuildingType.Road:
                case BuildingType.Mill:
                    return BuildingCategory.Economic;
                default:
                    return BuildingCategory.Military;
            }
        }

        public static int PopulationCapacity(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 10;
                case BuildingType.Neighborhood: return 5;
                default: return 0;
            }
        }

        public static int PopulationCapacityPerLevel(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 5;
                case BuildingType.Neighborhood: return 3;
                default: return 0;
            }
        }

        public static int PopulationCapacityForLevel(this BuildingType type, int level)
        {
            return type.PopulationCapacity() + type.PopulationCapacityPerLevel() * (level - 1);
        }

        public static int RequiredCityCenterLevel(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 1;
                case BuildingType.Neighborhood:
                case BuildingType.Warehouse:
                case BuildingType.Farm:
                case BuildingType.Barracks:
                case BuildingType.Road:
                case BuildingType.WoodenFort:
                case BuildingType.Castle:
                case BuildingType.MiningCamp:
                case BuildingType.LumberCamp:
                    return 1;
                case BuildingType.ArcheryRange:
                case BuildingType.Stable:
                case BuildingType.Mill:
                case BuildingType.Wall:
                case BuildingType.Gate:
                    return 2;
                case BuildingType.Market:
                case BuildingType.Blacksmith:
                case BuildingType.Tower:
                case BuildingType.University:
                case BuildingType.Library:
                    return 3;
                case BuildingType.SiegeWorkshop:
                    return 5;
                default: return 1;
            }
        }

        public static Dictionary<ResourceType, int> BuildCost(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 200 }, { ResourceType.Stone, 150 }, { ResourceType.Ore, 50 } };
                case BuildingType.Farm:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 50 }, { ResourceType.Stone, 20 } };
                case BuildingType.Neighborhood:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 80 } };
                case BuildingType.Blacksmith:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 60 }, { ResourceType.Ore, 40 } };
                case BuildingType.Market:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 50 } };
                case BuildingType.MiningCamp:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 30 } };
                case BuildingType.LumberCamp:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 20 } };
                case BuildingType.Warehouse:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 80 } };
                case BuildingType.University:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 150 }, { ResourceType.Stone, 120 }, { ResourceType.Ore, 60 } };
                case BuildingType.Library:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 100 }, { ResourceType.Ore, 40 } };
                case BuildingType.Road:
                    return new Dictionary<ResourceType, int> { { ResourceType.Stone, 10 } };
                case BuildingType.Castle:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 300 }, { ResourceType.Stone, 400 }, { ResourceType.Ore, 150 } };
                case BuildingType.Barracks:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 150 }, { ResourceType.Stone, 100 } };
                case BuildingType.ArcheryRange:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 80 } };
                case BuildingType.Stable:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 140 }, { ResourceType.Stone, 90 } };
                case BuildingType.SiegeWorkshop:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 180 }, { ResourceType.Stone, 120 }, { ResourceType.Ore, 80 } };
                case BuildingType.Tower:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 120 } };
                case BuildingType.WoodenFort:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 200 }, { ResourceType.Stone, 100 } };
                case BuildingType.Mill:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 40 } };
                case BuildingType.Wall:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 30 }, { ResourceType.Stone, 50 } };
                case BuildingType.Gate:
                    return new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 }, { ResourceType.Stone, 40 } };
                default:
                    return new Dictionary<ResourceType, int>();
            }
        }

        public static double BuildTime(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 60.0;
                case BuildingType.Farm: return 20.0;
                case BuildingType.Neighborhood: return 35.0;
                case BuildingType.Blacksmith: return 40.0;
                case BuildingType.Market: return 30.0;
                case BuildingType.MiningCamp: return 25.0;
                case BuildingType.LumberCamp: return 25.0;
                case BuildingType.Warehouse: return 30.0;
                case BuildingType.University: return 50.0;
                case BuildingType.Library: return 45.0;
                case BuildingType.Road: return 5.0;
                case BuildingType.Castle: return 90.0;
                case BuildingType.Barracks: return 4.0;
                case BuildingType.ArcheryRange: return 35.0;
                case BuildingType.Stable: return 35.0;
                case BuildingType.SiegeWorkshop: return 45.0;
                case BuildingType.Tower: return 30.0;
                case BuildingType.WoodenFort: return 50.0;
                case BuildingType.Mill: return 25.0;
                case BuildingType.Wall: return 15.0;
                case BuildingType.Gate: return 20.0;
                default: return 30.0;
            }
        }

        public static int HexSize(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Castle:
                case BuildingType.WoodenFort:
                    return 3;
                default: return 1;
            }
        }

        public static bool RequiresRotation(this BuildingType type)
        {
            return type.HexSize() > 1;
        }

        public static string Description(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return "Main hub for economy and villagers";
                case BuildingType.Farm: return "Produces food resources";
                case BuildingType.Neighborhood: return "Houses population";
                case BuildingType.Blacksmith: return "Upgrades units and tools";
                case BuildingType.Market: return "Trade resources";
                case BuildingType.MiningCamp: return "Increases ore collection";
                case BuildingType.LumberCamp: return "Increases wood collection";
                case BuildingType.Warehouse: return "Stores extra resources";
                case BuildingType.University: return "Research technologies";
                case BuildingType.Library: return "Boosts research speed and unlocks advanced research";
                case BuildingType.Road: return "Increases movement speed for units";
                case BuildingType.Castle: return "Defensive stronghold and military hub";
                case BuildingType.Barracks: return "Trains infantry units";
                case BuildingType.ArcheryRange: return "Trains ranged units";
                case BuildingType.Stable: return "Trains cavalry units";
                case BuildingType.SiegeWorkshop: return "Builds siege weapons";
                case BuildingType.Tower: return "Defensive structure";
                case BuildingType.WoodenFort: return "Basic defensive structure";
                case BuildingType.Mill: return "Boosts adjacent farm gather rates by 25%";
                case BuildingType.Wall: return "Blocks all movement";
                case BuildingType.Gate: return "Allows passage for owner and allies";
                default: return "";
            }
        }

        public static Dictionary<ResourceType, double> ResourceBonus(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Farm:
                    return new Dictionary<ResourceType, double> { { ResourceType.Food, 2.0 } };
                case BuildingType.MiningCamp:
                    return new Dictionary<ResourceType, double> { { ResourceType.Ore, 1.5 } };
                case BuildingType.LumberCamp:
                    return new Dictionary<ResourceType, double> { { ResourceType.Wood, 1.5 } };
                default: return null;
            }
        }

        public static int MaxLevel(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 10;
                case BuildingType.Road:
                case BuildingType.Wall:
                case BuildingType.Gate:
                    return 1;
                default: return 5;
            }
        }

        public static bool IsRoad(this BuildingType type) => type == BuildingType.Road;

        public static bool ProvidesRoadBonus(this BuildingType type)
        {
            return type != BuildingType.Wall && type != BuildingType.Gate;
        }

        public static bool BlocksMovement(this BuildingType type)
        {
            return type == BuildingType.Wall || type == BuildingType.Gate;
        }

        public static Dictionary<ResourceType, int> UpgradeCost(this BuildingType type, int level)
        {
            if (level >= type.MaxLevel()) return null;

            double multiplier = (level + 1);
            var baseCost = type.BuildCost();
            var cost = new Dictionary<ResourceType, int>();
            foreach (var kvp in baseCost)
            {
                cost[kvp.Key] = (int)(kvp.Value * multiplier * 0.75);
            }
            return cost;
        }

        public static double? UpgradeTime(this BuildingType type, int level)
        {
            if (level >= type.MaxLevel()) return null;
            double multiplier = (level + 1);
            return type.BuildTime() * multiplier * 0.8;
        }

        public static int MaxCastleLevel(int cityCenterLevel)
        {
            if (cityCenterLevel < 6) return 0;
            return System.Math.Min(cityCenterLevel - 5, 5);
        }

        public static int BaseStorageCapacityPerResource(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 1200;
                case BuildingType.Warehouse: return 150;
                default: return 0;
            }
        }

        public static int StorageCapacityPerLevelPerResource(this BuildingType type)
        {
            switch (type)
            {
                case BuildingType.CityCenter: return 100;
                case BuildingType.Warehouse: return 75;
                default: return 0;
            }
        }

        public static int StorageCapacityPerResource(this BuildingType type, int level)
        {
            return type.BaseStorageCapacityPerResource() + type.StorageCapacityPerLevelPerResource() * (level - 1);
        }

        public static int MaxWarehousesAllowed(int cityCenterLevel)
        {
            if (cityCenterLevel < 2) return 0;
            if (cityCenterLevel < 5) return 1;
            if (cityCenterLevel < 8) return 2;
            return 3;
        }

        public static int CityCenterLevelRequiredForWarehouse(int warehouseNumber)
        {
            switch (warehouseNumber)
            {
                case 1: return 2;
                case 2: return 5;
                case 3: return 8;
                default: return 99;
            }
        }

        public static int MaxLibrariesAllowed() => 1;

        public static List<HexCoordinate> GetOccupiedCoordinates(this BuildingType type, HexCoordinate anchor, int rotation)
        {
            if (type.HexSize() <= 1)
                return new List<HexCoordinate> { anchor };

            int normalizedRotation = ((rotation % 6) + 6) % 6;
            int dir1 = normalizedRotation;
            int dir2 = (normalizedRotation + 1) % 6;

            return new List<HexCoordinate>
            {
                anchor,
                anchor.Neighbor(dir1),
                anchor.Neighbor(dir2)
            };
        }
    }
}
