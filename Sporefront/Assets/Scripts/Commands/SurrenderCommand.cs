using System;
using System.Linq;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class SurrenderCommand : BaseEngineCommand
    {
        public SurrenderCommand(Guid playerID)
            : base(playerID)
        {
        }

        // Reconstruction constructor for online deserialization
        public SurrenderCommand(Guid id, Guid playerID, double timestamp)
            : base(id, playerID, timestamp)
        {
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (state.isGameOver)
                return EngineCommandResult.Failure("Game is already over");

            if (state.GetPlayer(PlayerID) == null)
                return EngineCommandResult.Failure("Player not found");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            state.isGameOver = true;

            // Find the opponent (first player that isn't the surrendering player)
            var opponent = state.players.Values.FirstOrDefault(p => p.id != PlayerID);

            changeBuilder.Add(new GameOverChange
            {
                reason = GameOverReason.Resignation.DisplayMessage(),
                winnerID = opponent?.id,
                reasonType = GameOverReason.Resignation
            });

            DebugLog.Log($"SurrenderCommand: Player {PlayerID} surrendered. Winner: {opponent?.id}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
