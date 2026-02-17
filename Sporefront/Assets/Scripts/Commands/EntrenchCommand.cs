using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class EntrenchCommand : BaseEngineCommand
    {
        public Guid armyID;

        public EntrenchCommand(Guid playerID, Guid armyID)
            : base(playerID)
        {
            this.armyID = armyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check player exists
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found.");

            // Check army exists
            var army = state.GetArmy(armyID);
            if (army == null)
                return EngineCommandResult.Failure("Army not found.");

            // Check army is owned by player
            if (army.ownerID != PlayerID)
                return EngineCommandResult.Failure("Army does not belong to this player.");

            // Check army is not in combat
            if (army.isInCombat)
                return EngineCommandResult.Failure("Cannot entrench while in combat.");

            // Check army is not already entrenched
            if (army.isEntrenched)
                return EngineCommandResult.Failure("Army is already entrenched.");

            // Check army is not already entrenching
            if (army.isEntrenching)
                return EngineCommandResult.Failure("Army is already entrenching.");

            // Check player has enough wood
            if (player.GetResource(ResourceType.Wood) < GameConfig.Entrenchment.WoodCost)
                return EngineCommandResult.Failure("Insufficient wood for entrenchment.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var player = state.GetPlayer(PlayerID);
            var army = state.GetArmy(armyID);

            // Deduct wood cost
            int oldWood = player.GetResource(ResourceType.Wood);
            player.RemoveResource(ResourceType.Wood, GameConfig.Entrenchment.WoodCost);
            int newWood = player.GetResource(ResourceType.Wood);

            // Mark army as entrenching
            army.isEntrenching = true;
            army.entrenchmentStartTime = state.currentTime;

            // Emit entrenchment started change
            changeBuilder.Add(new ArmyEntrenchmentStartedChange
            {
                armyID = armyID,
                coordinate = army.coordinate
            });

            // Emit resource change for wood deduction
            changeBuilder.Add(new ResourcesChangedChange
            {
                playerID = PlayerID,
                resourceType = ResourceType.Wood.ToString(),
                oldAmount = oldWood,
                newAmount = newWood
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
