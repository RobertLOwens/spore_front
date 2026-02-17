namespace Sporefront.Models
{
    public enum DiplomacyStatus
    {
        Me,
        Guild,
        Ally,
        Neutral,
        Enemy
    }

    public static class DiplomacyStatusExtensions
    {
        public static string DisplayName(this DiplomacyStatus status)
        {
            switch (status)
            {
                case DiplomacyStatus.Me: return "You";
                case DiplomacyStatus.Guild: return "Guild Member";
                case DiplomacyStatus.Ally: return "Ally";
                case DiplomacyStatus.Neutral: return "Neutral";
                case DiplomacyStatus.Enemy: return "Enemy";
                default: return "";
            }
        }

        public static string StrokeColorHex(this DiplomacyStatus status)
        {
            switch (status)
            {
                case DiplomacyStatus.Me: return "#0000FF";
                case DiplomacyStatus.Guild: return "#800080";
                case DiplomacyStatus.Ally: return "#00FF00";
                case DiplomacyStatus.Enemy: return "#FF0000";
                case DiplomacyStatus.Neutral: return "#FFA500";
                default: return "#FFFFFF";
            }
        }

        public static bool CanAttack(this DiplomacyStatus status)
        {
            return status == DiplomacyStatus.Enemy;
        }

        public static bool CanMove(this DiplomacyStatus status)
        {
            return status == DiplomacyStatus.Me ||
                   status == DiplomacyStatus.Guild ||
                   status == DiplomacyStatus.Ally;
        }
    }
}
