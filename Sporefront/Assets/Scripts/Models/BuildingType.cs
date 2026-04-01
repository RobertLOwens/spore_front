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
        Gate,

        // Faction-Exclusive Buildings
        FalseMorel
    }

    // ================================================================
    // Data record for BuildingType lookup table
    // ================================================================

    public class BuildingTypeData
    {
        public string DisplayName;
        public string Icon;
        public BuildingCategory Category;
        public int PopulationCapacity;
        public int PopulationCapacityPerLevel;
        public int RequiredCityCenterLevel;
        public Dictionary<ResourceType, int> BuildCost;
        public double BuildTime;
        public int HexSize;
        public string Description;
        public Dictionary<ResourceType, double> ResourceBonus;
        public int MaxLevel;
        public int BaseStorageCapacityPerResource;
        public int StorageCapacityPerLevelPerResource;

        public BuildingTypeData(
            string displayName, string icon, BuildingCategory category,
            Dictionary<ResourceType, int> buildCost, double buildTime,
            string description,
            int requiredCCLevel = 1, int hexSize = 1, int maxLevel = 5,
            int popCap = 0, int popCapPerLevel = 0,
            Dictionary<ResourceType, double> resourceBonus = null,
            int baseStorage = 0, int storagePerLevel = 0)
        {
            DisplayName = displayName;
            Icon = icon;
            Category = category;
            BuildCost = buildCost;
            BuildTime = buildTime;
            Description = description;
            RequiredCityCenterLevel = requiredCCLevel;
            HexSize = hexSize;
            MaxLevel = maxLevel;
            PopulationCapacity = popCap;
            PopulationCapacityPerLevel = popCapPerLevel;
            ResourceBonus = resourceBonus;
            BaseStorageCapacityPerResource = baseStorage;
            StorageCapacityPerLevelPerResource = storagePerLevel;
        }
    }

    // ================================================================
    // Extension methods — data-driven via static lookup table
    // ================================================================

    public static class BuildingTypeExtensions
    {
        private static readonly Dictionary<BuildingType, BuildingTypeData> Data =
            new Dictionary<BuildingType, BuildingTypeData>
        {
            // ---- Economic Buildings ----
            { BuildingType.CityCenter, new BuildingTypeData(
                "City Center", "city_center", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 200 }, { ResourceType.Stone, 150 }, { ResourceType.Ore, 50 } },
                60.0, "Main hub for economy and villagers",
                maxLevel: 10, popCap: 10, popCapPerLevel: 5, baseStorage: 1200, storagePerLevel: 100) },

            { BuildingType.Farm, new BuildingTypeData(
                "Farm", "farm", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 50 }, { ResourceType.Stone, 20 } },
                20.0, "Produces food resources",
                resourceBonus: new Dictionary<ResourceType, double> { { ResourceType.Food, 2.0 } }) },

            { BuildingType.Neighborhood, new BuildingTypeData(
                "Neighborhood", "neighborhood", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 80 } },
                35.0, "Houses population",
                popCap: 5, popCapPerLevel: 3) },

            { BuildingType.Blacksmith, new BuildingTypeData(
                "Blacksmith", "blacksmith", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 60 }, { ResourceType.Ore, 40 } },
                40.0, "Upgrades units and tools",
                requiredCCLevel: 3) },

            { BuildingType.Market, new BuildingTypeData(
                "Market", "market", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 50 } },
                30.0, "Trade resources",
                requiredCCLevel: 3) },

            { BuildingType.MiningCamp, new BuildingTypeData(
                "Mining Camp", "mining_camp", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Stone, 30 } },
                25.0, "Increases ore collection",
                resourceBonus: new Dictionary<ResourceType, double> { { ResourceType.Ore, 1.5 } }) },

            { BuildingType.LumberCamp, new BuildingTypeData(
                "Lumber Camp", "lumber_camp", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 20 } },
                25.0, "Increases wood collection",
                resourceBonus: new Dictionary<ResourceType, double> { { ResourceType.Wood, 1.5 } }) },

            { BuildingType.Warehouse, new BuildingTypeData(
                "Warehouse", "warehouse", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 80 } },
                30.0, "Stores extra resources",
                baseStorage: 150, storagePerLevel: 75) },

            { BuildingType.University, new BuildingTypeData(
                "University", "university", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 150 }, { ResourceType.Stone, 120 }, { ResourceType.Ore, 60 } },
                50.0, "Research technologies",
                requiredCCLevel: 3) },

            { BuildingType.Library, new BuildingTypeData(
                "Library", "library", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 100 }, { ResourceType.Ore, 40 } },
                45.0, "Boosts research speed and unlocks advanced research",
                requiredCCLevel: 3) },

            { BuildingType.Mill, new BuildingTypeData(
                "Mill", "mill", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 40 } },
                25.0, "Boosts adjacent farm gather rates by 25%",
                requiredCCLevel: 2) },

            // ---- Infrastructure ----
            { BuildingType.Road, new BuildingTypeData(
                "Road", "road", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Stone, 10 } },
                5.0, "Increases movement speed for units",
                maxLevel: 1) },

            // ---- Military Buildings ----
            { BuildingType.Castle, new BuildingTypeData(
                "Castle", "castle", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 300 }, { ResourceType.Stone, 400 }, { ResourceType.Ore, 150 } },
                90.0, "Defensive stronghold and military hub",
                hexSize: 3) },

            { BuildingType.Barracks, new BuildingTypeData(
                "Barracks", "barracks", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 150 }, { ResourceType.Stone, 100 } },
                4.0, "Trains infantry units") },

            { BuildingType.ArcheryRange, new BuildingTypeData(
                "Archery Range", "archery_range", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Stone, 80 } },
                35.0, "Trains ranged units",
                requiredCCLevel: 2) },

            { BuildingType.Stable, new BuildingTypeData(
                "Stable", "stable", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 140 }, { ResourceType.Stone, 90 } },
                35.0, "Trains cavalry units",
                requiredCCLevel: 2) },

            { BuildingType.SiegeWorkshop, new BuildingTypeData(
                "Siege Workshop", "siege_workshop", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 180 }, { ResourceType.Stone, 120 }, { ResourceType.Ore, 80 } },
                45.0, "Builds siege weapons",
                requiredCCLevel: 5) },

            { BuildingType.Tower, new BuildingTypeData(
                "Tower", "tower", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Stone, 120 } },
                30.0, "Defensive structure",
                requiredCCLevel: 3) },

            { BuildingType.WoodenFort, new BuildingTypeData(
                "Wooden Fort", "wooden_fort", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 200 }, { ResourceType.Stone, 100 } },
                50.0, "Basic defensive structure",
                hexSize: 3) },

            { BuildingType.Wall, new BuildingTypeData(
                "Wall", "wall", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 30 }, { ResourceType.Stone, 50 } },
                15.0, "Blocks all movement",
                requiredCCLevel: 2, maxLevel: 1) },

            { BuildingType.Gate, new BuildingTypeData(
                "Gate", "gate", BuildingCategory.Military,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 }, { ResourceType.Stone, 40 } },
                20.0, "Allows passage for owner and allies",
                requiredCCLevel: 2, maxLevel: 1) },

            // ---- Faction-Exclusive ----
            { BuildingType.FalseMorel, new BuildingTypeData(
                "False Morel", "false_morel", BuildingCategory.Economic,
                new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 }, { ResourceType.Stone, 20 } },
                20.0, "A decoy structure that appears as an army to enemies until they are adjacent",
                requiredCCLevel: 2, maxLevel: 1) },
        };

        // ================================================================
        // One-liner data lookups
        // ================================================================

        public static string DisplayName(this BuildingType type) => Data[type].DisplayName;
        public static string Icon(this BuildingType type) => Data[type].Icon;
        public static BuildingCategory Category(this BuildingType type) => Data[type].Category;
        public static int PopulationCapacity(this BuildingType type) => Data[type].PopulationCapacity;
        public static int PopulationCapacityPerLevel(this BuildingType type) => Data[type].PopulationCapacityPerLevel;
        public static int RequiredCityCenterLevel(this BuildingType type) => Data[type].RequiredCityCenterLevel;
        public static Dictionary<ResourceType, int> BuildCost(this BuildingType type) => Data[type].BuildCost;
        public static double BuildTime(this BuildingType type) => Data[type].BuildTime;
        public static int HexSize(this BuildingType type) => Data[type].HexSize;
        public static string Description(this BuildingType type) => Data[type].Description;
        public static Dictionary<ResourceType, double> ResourceBonus(this BuildingType type) => Data[type].ResourceBonus;
        public static int MaxLevel(this BuildingType type) => Data[type].MaxLevel;
        public static int BaseStorageCapacityPerResource(this BuildingType type) => Data[type].BaseStorageCapacityPerResource;
        public static int StorageCapacityPerLevelPerResource(this BuildingType type) => Data[type].StorageCapacityPerLevelPerResource;

        // ================================================================
        // Computed methods (derive from base data)
        // ================================================================

        public static int PopulationCapacityForLevel(this BuildingType type, int level)
        {
            return type.PopulationCapacity() + type.PopulationCapacityPerLevel() * (level - 1);
        }

        public static bool RequiresRotation(this BuildingType type) => type.HexSize() > 1;

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

        public static int StorageCapacityPerResource(this BuildingType type, int level)
        {
            return type.BaseStorageCapacityPerResource() + type.StorageCapacityPerLevelPerResource() * (level - 1);
        }

        // ================================================================
        // Static helpers (not extension methods)
        // ================================================================

        public static int MaxCastleLevel(int cityCenterLevel)
        {
            if (cityCenterLevel < 6) return 0;
            return System.Math.Min(cityCenterLevel - 5, 5);
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
