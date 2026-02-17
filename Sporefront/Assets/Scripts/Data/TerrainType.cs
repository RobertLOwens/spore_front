namespace Sporefront.Data
{
    public enum TerrainType
    {
        Plains,
        Water,
        Mountain,
        Desert,
        Hill
    }

    public static class TerrainTypeExtensions
    {
        public static string DisplayName(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plains: return "Plains";
                case TerrainType.Water: return "Water";
                case TerrainType.Mountain: return "Mountain";
                case TerrainType.Desert: return "Desert";
                case TerrainType.Hill: return "Hill";
                default: return type.ToString();
            }
        }

        public static string ColorHex(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plains: return "#33B333";
                case TerrainType.Water: return "#3380E6";
                case TerrainType.Mountain: return "#808080";
                case TerrainType.Desert: return "#E6CC66";
                case TerrainType.Hill: return "#998066";
                default: return "#FFFFFF";
            }
        }

        public static bool IsWalkable(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plains:
                case TerrainType.Desert:
                case TerrainType.Hill:
                case TerrainType.Mountain:
                    return true;
                case TerrainType.Water:
                    return false;
                default:
                    return false;
            }
        }

        public static int MovementCost(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plains:
                case TerrainType.Desert:
                    return 3;
                case TerrainType.Hill:
                    return 4;
                case TerrainType.Mountain:
                    return 5;
                case TerrainType.Water:
                    return int.MaxValue;
                default:
                    return 3;
            }
        }

        public static double DefenderDefenseBonus(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Plains: return 0.0;
                case TerrainType.Hill: return 0.15;
                case TerrainType.Mountain: return 0.25;
                case TerrainType.Desert: return -0.05;
                case TerrainType.Water: return 0.0;
                default: return 0.0;
            }
        }

        public static double AttackerAttackPenalty(this TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Mountain: return 0.10;
                default: return 0.0;
            }
        }
    }
}
