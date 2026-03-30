namespace Sporefront.Models
{
    public enum GameMode
    {
        Conquest,
        Domination,
        CrookedDomination,
        Ring
    }

    public static class GameModeExtensions
    {
        public static bool UsesControlZones(this GameMode mode)
        {
            return mode == GameMode.Domination
                || mode == GameMode.CrookedDomination
                || mode == GameMode.Ring;
        }
    }
}
