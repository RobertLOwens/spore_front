namespace Sporefront.Models
{
    public enum ResourceType
    {
        Wood,
        Food,
        Stone,
        Ore
    }

    public static class ResourceTypeExtensions
    {
        public static string DisplayName(this ResourceType type)
        {
            return type.ToString();
        }

        public static string Icon(this ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return "wood";
                case ResourceType.Food: return "food";
                case ResourceType.Stone: return "stone";
                case ResourceType.Ore: return "ore";
                default: return "";
            }
        }
    }
}
