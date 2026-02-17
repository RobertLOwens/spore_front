using Sporefront.Models;

namespace Sporefront.Data
{
    public enum ResourcePointType
    {
        Trees,
        Forage,
        OreMine,
        StoneQuarry,
        Deer,
        WildBoar,
        DeerCarcass,
        BoarCarcass,
        Farmland
    }

    public static class ResourcePointTypeExtensions
    {
        public static string DisplayName(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees: return "Trees";
                case ResourcePointType.Forage: return "Forage";
                case ResourcePointType.OreMine: return "Ore Mine";
                case ResourcePointType.StoneQuarry: return "Stone Quarry";
                case ResourcePointType.Deer: return "Deer";
                case ResourcePointType.WildBoar: return "Wild Boar";
                case ResourcePointType.DeerCarcass: return "Deer Carcass";
                case ResourcePointType.BoarCarcass: return "Boar Carcass";
                case ResourcePointType.Farmland: return "Farmland";
                default: return type.ToString();
            }
        }

        public static string Icon(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees: return "trees";
                case ResourcePointType.Forage: return "forage";
                case ResourcePointType.OreMine: return "ore_mine";
                case ResourcePointType.StoneQuarry: return "stone_quarry";
                case ResourcePointType.Deer: return "deer";
                case ResourcePointType.WildBoar: return "wild_boar";
                case ResourcePointType.DeerCarcass: return "deer_carcass";
                case ResourcePointType.BoarCarcass: return "boar_carcass";
                case ResourcePointType.Farmland: return "farmland";
                default: return "";
            }
        }

        public static ResourceType ResourceYield(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees: return ResourceType.Wood;
                case ResourcePointType.Forage: return ResourceType.Food;
                case ResourcePointType.OreMine: return ResourceType.Ore;
                case ResourcePointType.StoneQuarry: return ResourceType.Stone;
                case ResourcePointType.Deer:
                case ResourcePointType.WildBoar:
                case ResourcePointType.DeerCarcass:
                case ResourcePointType.BoarCarcass:
                case ResourcePointType.Farmland:
                    return ResourceType.Food;
                default: return ResourceType.Food;
            }
        }

        public static int InitialAmount(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees: return 5000;
                case ResourcePointType.Forage: return 3000;
                case ResourcePointType.OreMine: return 8000;
                case ResourcePointType.StoneQuarry: return 6000;
                case ResourcePointType.Deer: return 2000;
                case ResourcePointType.WildBoar: return 1500;
                case ResourcePointType.DeerCarcass: return 2000;
                case ResourcePointType.BoarCarcass: return 1500;
                case ResourcePointType.Farmland: return 999999;
                default: return 1000;
            }
        }

        public static double BaseGatherRate(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees: return 0.5;
                case ResourcePointType.Forage: return 0.5;
                case ResourcePointType.OreMine: return 0.5;
                case ResourcePointType.StoneQuarry: return 0.5;
                case ResourcePointType.Deer: return 0.0;
                case ResourcePointType.WildBoar: return 0.0;
                case ResourcePointType.DeerCarcass: return 0.5;
                case ResourcePointType.BoarCarcass: return 0.5;
                case ResourcePointType.Farmland: return 0.1;
                default: return 0.0;
            }
        }

        public static bool RequiresCamp(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees:
                case ResourcePointType.OreMine:
                case ResourcePointType.StoneQuarry:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsHuntable(this ResourcePointType type)
        {
            return type == ResourcePointType.Deer || type == ResourcePointType.WildBoar;
        }

        public static bool IsCarcass(this ResourcePointType type)
        {
            return type == ResourcePointType.DeerCarcass || type == ResourcePointType.BoarCarcass;
        }

        public static bool IsGatherable(this ResourcePointType type)
        {
            return type != ResourcePointType.Deer && type != ResourcePointType.WildBoar;
        }

        public static TerrainType? RequiredTerrain(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Forage: return TerrainType.Plains;
                case ResourcePointType.OreMine:
                case ResourcePointType.StoneQuarry:
                    return TerrainType.Mountain;
                default: return null;
            }
        }

        public static double AttackPower(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Deer: return 2;
                case ResourcePointType.WildBoar: return 8;
                default: return 0;
            }
        }

        public static double DefensePower(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Deer: return 3;
                case ResourcePointType.WildBoar: return 5;
                default: return 0;
            }
        }

        public static double MaxHealth(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Deer: return 30;
                case ResourcePointType.WildBoar: return 50;
                default: return 0;
            }
        }

        public static ResourcePointType? CarcassType(this ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Deer: return ResourcePointType.DeerCarcass;
                case ResourcePointType.WildBoar: return ResourcePointType.BoarCarcass;
                default: return null;
            }
        }
    }
}
