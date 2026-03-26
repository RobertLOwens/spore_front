namespace Sporefront.Models
{
    public enum GameOverReason
    {
        Starvation,
        Resignation,
        Conquest,
        CityCenterDestroyed,
        Disconnected
    }

    public static class GameOverReasonExtensions
    {
        /// <summary>
        /// Get context-aware display message based on whether the viewer won or lost.
        /// </summary>
        public static string DisplayMessage(this GameOverReason reason, bool isVictory)
        {
            switch (reason)
            {
                case GameOverReason.Starvation:
                    return isVictory
                        ? "Your opponent's people starved to death.\nThey had no food for too long."
                        : "Your people starved to death.\nYou had no food for too long.";
                case GameOverReason.Resignation:
                    return isVictory
                        ? "Your opponent has surrendered."
                        : "You have resigned from the game.";
                case GameOverReason.Conquest:
                    return isVictory
                        ? "You have conquered your enemies!\nAll opposing forces have been eliminated."
                        : "Your forces have been eliminated.\nThe enemy has conquered your lands.";
                case GameOverReason.CityCenterDestroyed:
                    return isVictory
                        ? "You have destroyed your opponent's city center!\nTheir civilization has fallen."
                        : "Your city center has been destroyed.\nYour civilization has fallen.";
                case GameOverReason.Disconnected:
                    return isVictory
                        ? "Your opponent has abandoned the game."
                        : "You have been disconnected from the game.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Legacy display message (loser-perspective). Used by engine commands
        /// that don't know the viewer's perspective.
        /// </summary>
        public static string DisplayMessage(this GameOverReason reason)
        {
            return reason.DisplayMessage(false);
        }
    }
}
