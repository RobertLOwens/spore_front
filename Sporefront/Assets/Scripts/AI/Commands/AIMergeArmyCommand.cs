// ============================================================================
// FILE: AI/Commands/AIMergeArmyCommand.cs
// PURPOSE: AI command to merge two nearby armies into one stronger force.
//          Uses existing ArmyData.Merge() infrastructure.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Merges the source army INTO the target army, then removes the source.
    /// Both armies must be owned by the same player, on the same tile or adjacent,
    /// and not in combat or pathing.
    /// </summary>
    public class AIMergeArmyCommand : BaseEngineCommand
    {
        public Guid sourceArmyID;
        public Guid targetArmyID;

        public AIMergeArmyCommand(Guid playerID, Guid sourceArmyID, Guid targetArmyID)
            : base(playerID)
        {
            this.sourceArmyID = sourceArmyID;
            this.targetArmyID = targetArmyID;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedArmy(state, sourceArmyID, out var source);
            if (fail != null) return fail;

            fail = ValidateOwnedArmy(state, targetArmyID, out var target);
            if (fail != null) return fail;

            if (source.isInCombat || target.isInCombat)
                return EngineCommandResult.Failure("Cannot merge armies in combat");

            if (source.currentPath != null || target.currentPath != null)
                return EngineCommandResult.Failure("Cannot merge armies that are moving");

            int distance = source.coordinate.Distance(target.coordinate);
            if (distance > 1)
                return EngineCommandResult.Failure("Armies must be on same tile or adjacent to merge");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateArmy(state, sourceArmyID, out var source);
            if (fail != null) return fail;

            fail = ValidateArmy(state, targetArmyID, out var target);
            if (fail != null) return fail;

            // Merge source units into target
            target.Merge(source);

            // Emit merge change
            changeBuilder.Add(new ArmyMergedChange
            {
                sourceArmyID = sourceArmyID,
                targetArmyID = targetArmyID
            });

            // Remove the source army from state
            state.RemoveArmy(sourceArmyID);

            DebugLog.Log($"AI merged army ({source.GetTotalUnits()} units) into army ({target.GetTotalUnits()} units) at ({target.coordinate.q},{target.coordinate.r})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
