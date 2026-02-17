using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class JoinVillagerGroupCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Guid targetVillagerGroupID;
        public int count;

        public JoinVillagerGroupCommand(Guid playerID, Guid buildingID, Guid targetVillagerGroupID, int count)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.targetVillagerGroupID = targetVillagerGroupID;
            this.count = count;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Building exists and is owned by player
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");

            // Count must be > 0
            if (count <= 0)
                return EngineCommandResult.Failure("Must send at least one villager");

            // Building has enough villagers
            if (building.villagerGarrison < count)
                return EngineCommandResult.Failure(
                    string.Format("Building does not have enough villagers in garrison (have {0}, need {1})",
                        building.villagerGarrison, count));

            // Target villager group exists and is owned by player
            var targetGroup = state.GetVillagerGroup(targetVillagerGroupID);
            if (targetGroup == null)
                return EngineCommandResult.Failure("Target villager group not found");

            if (!targetGroup.ownerID.HasValue || targetGroup.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Target villager group is not owned by this player");

            // Path exists from building to group
            var path = state.mapData.FindPath(building.coordinate, targetGroup.coordinate, PlayerID, state);
            if (path == null)
                return EngineCommandResult.Failure("No valid path from building to villager group");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            var targetGroup = state.GetVillagerGroup(targetVillagerGroupID);
            if (targetGroup == null)
                return EngineCommandResult.Failure("Target villager group not found");

            // Find path from building to target villager group
            var path = state.mapData.FindPath(building.coordinate, targetGroup.coordinate, PlayerID, state);
            if (path == null || path.Count == 0)
                return EngineCommandResult.Failure("No valid path found from building to villager group");

            // Remove villagers from building garrison
            int removed = building.RemoveVillagersFromGarrison(count);

            // Emit villagers ungarrisoned change
            changeBuilder.Add(new VillagersUngarrisonedChange
            {
                buildingID = buildingID,
                quantity = removed
            });

            // Create a new villager group to march from building to target
            var marchingGroup = new VillagerGroupData(
                string.Format("Marching Villagers ({0})", removed),
                building.coordinate,
                removed,
                PlayerID
            );

            // Register the marching group in game state
            state.AddVillagerGroup(marchingGroup);

            // Set path and task to moving toward the target group
            marchingGroup.SetPath(path);
            var destination = path[path.Count - 1];
            marchingGroup.currentTask = new MovingTask(destination);

            // Emit villager group created change
            changeBuilder.Add(new VillagerGroupCreatedChange
            {
                groupID = marchingGroup.id,
                ownerID = PlayerID,
                coordinate = building.coordinate,
                count = removed
            });

            // Emit movement change
            changeBuilder.Add(new VillagerGroupMovedChange
            {
                groupID = marchingGroup.id,
                from = building.coordinate,
                to = destination,
                path = path
            });

            DebugLog.Log(string.Format("JoinVillagerGroupCommand: Dispatched {0} villagers from {1} to group {2} ({3} steps)",
                removed, building.coordinate, targetGroup.name, path.Count));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
