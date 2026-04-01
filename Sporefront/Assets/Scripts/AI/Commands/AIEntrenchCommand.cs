// ============================================================================
// FILE: AI/Commands/AIEntrenchCommand.cs
// PURPOSE: AI command to entrench an army at its current position
//          C# port of AIEntrenchCommand from AIController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    public class AIEntrenchCommand : BaseEngineCommand
    {
        public Guid armyID;

        public AIEntrenchCommand(Guid playerID, Guid armyID)
            : base(playerID)
        {
            this.armyID = armyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedArmy(state, armyID, out var army);
            if (fail != null) return fail;

            if (army.isEntrenched)
                return EngineCommandResult.Failure("Already entrenched");

            if (army.isEntrenching)
                return EngineCommandResult.Failure("Already entrenching");

            if (army.currentPath != null)
                return EngineCommandResult.Failure("Cannot entrench while moving");

            if (army.isInCombat)
                return EngineCommandResult.Failure("Cannot entrench while in combat");

            if (army.isRetreating)
                return EngineCommandResult.Failure("Cannot entrench while retreating");

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            if (!player.HasResource(ResourceType.Wood, GameConfig.Entrenchment.WoodCost))
                return EngineCommandResult.Failure("Not enough wood");

            // Check commander stamina if commander exists
            if (army.commanderID.HasValue)
            {
                var commander = state.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    if (!commander.HasEnoughStamina(CommanderData.StaminaCostPerCommand))
                        return EngineCommandResult.Failure("Commander too exhausted");
                }
            }

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedArmy(state, armyID, out var army);
            if (fail != null) return fail;

            fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            // Deduct wood cost
            player.RemoveResource(ResourceType.Wood, GameConfig.Entrenchment.WoodCost);

            // Consume commander stamina if commander exists
            if (army.commanderID.HasValue)
            {
                var commander = state.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    commander.ConsumeStamina();
                }
            }

            // Mark army as entrenching
            army.isEntrenching = true;
            army.entrenchmentStartTime = state.currentTime;

            // Emit entrenchment started change
            changeBuilder.Add(new ArmyEntrenchmentStartedChange
            {
                armyID = armyID,
                coordinate = army.coordinate
            });

            DebugLog.Log($"AIEntrenchCommand: AI army started entrenching at ({army.coordinate.q}, {army.coordinate.r})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
