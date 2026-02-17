namespace Sporefront.Models
{
    public enum GameOverReason
    {
        Starvation,
        Resignation,
        Conquest,
        CityCenterDestroyed
    }

    public static class GameOverReasonExtensions
    {
        public static string DisplayMessage(this GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.Starvation:
                    return "Your people starved to death.\nYou had no food for too long.";
                case GameOverReason.Resignation:
                    return "You have resigned from the game.";
                case GameOverReason.Conquest:
                    return "You have conquered your enemies!\nAll opposing forces have been eliminated.";
                case GameOverReason.CityCenterDestroyed:
                    return "Your city center has been destroyed.\nYour civilization has fallen.";
                default:
                    return "";
            }
        }
    }
}
