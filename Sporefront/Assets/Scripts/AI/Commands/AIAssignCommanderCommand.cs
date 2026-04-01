// ============================================================================
// FILE: AI/Commands/AIAssignCommanderCommand.cs
// PURPOSE: AI command to reassign a commander from their current army to a
//          different army. Updates both commander and army linkage fields.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Reassigns a commander from their current army to a different army.
    /// Updates both commander.assignedArmyID and army.commanderID fields.
    /// </summary>
    public class AIAssignCommanderCommand : BaseEngineCommand
    {
        public Guid commanderID;
        public Guid targetArmyID;

        public AIAssignCommanderCommand(Guid playerID, Guid commanderID, Guid targetArmyID)
            : base(playerID)
        {
            this.commanderID = commanderID;
            this.targetArmyID = targetArmyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var commander = state.GetCommander(commanderID);
            if (commander == null)
                return EngineCommandResult.Failure("Commander not found");

            if (!commander.ownerID.HasValue || commander.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Commander not owned by player");

            var fail = ValidateOwnedArmy(state, targetArmyID, out var targetArmy);
            if (fail != null) return fail;

            if (commander.assignedArmyID.HasValue && commander.assignedArmyID.Value == targetArmyID)
                return EngineCommandResult.Failure("Commander already assigned to target army");

            if (targetArmy.isInCombat)
                return EngineCommandResult.Failure("Cannot reassign commander to army in combat");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var commander = state.GetCommander(commanderID);
            if (commander == null)
                return EngineCommandResult.Failure("Commander not found");

            var fail = ValidateArmy(state, targetArmyID, out var targetArmy);
            if (fail != null) return fail;

            // Unlink commander from their current army (if any)
            if (commander.assignedArmyID.HasValue)
            {
                var oldArmy = state.GetArmy(commander.assignedArmyID.Value);
                if (oldArmy != null)
                    oldArmy.commanderID = null;
            }

            // Unlink any existing commander from the target army
            if (targetArmy.commanderID.HasValue && targetArmy.commanderID.Value != commanderID)
            {
                var oldCommander = state.GetCommander(targetArmy.commanderID.Value);
                if (oldCommander != null)
                    oldCommander.assignedArmyID = null;
            }

            // Link commander to target army
            commander.assignedArmyID = targetArmyID;
            targetArmy.commanderID = commanderID;

            DebugLog.Log($"AI reassigned commander {commander.name} to army at ({targetArmy.coordinate.q},{targetArmy.coordinate.r})");

            return EngineCommandResult.Success(new List<StateChange>());
        }
    }
}
