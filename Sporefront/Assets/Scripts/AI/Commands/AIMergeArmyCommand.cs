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
            var source = state.GetArmy(sourceArmyID);
            var target = state.GetArmy(targetArmyID);

            if (source == null || target == null)
                return EngineCommandResult.Failure("Army not found");

            if (!source.ownerID.HasValue || source.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Source army not owned by player");

            if (!target.ownerID.HasValue || target.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Target army not owned by player");

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
            var source = state.GetArmy(sourceArmyID);
            var target = state.GetArmy(targetArmyID);

            if (source == null || target == null)
                return EngineCommandResult.Failure("Army not found");

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

            DebugLog.Log(string.Format("AI merged army ({0} units) into army ({1} units) at ({2},{3})",
                source.GetTotalUnits(), target.GetTotalUnits(),
                target.coordinate.q, target.coordinate.r));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
