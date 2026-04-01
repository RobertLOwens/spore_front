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

        // Reconstruction constructor for online deserialization
        public JoinVillagerGroupCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, Guid targetVillagerGroupID, int count)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.targetVillagerGroupID = targetVillagerGroupID;
            this.count = count;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Building exists, is owned by player, and is operational
            var fail = ValidateOperationalBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            // Count must be > 0
            if (count <= 0)
                return EngineCommandResult.Failure("Must send at least one villager");

            // Building has enough villagers
            if (building.villagerGarrison < count)
                return EngineCommandResult.Failure(
                    $"Building does not have enough villagers in garrison (have {building.villagerGarrison}, need {count})");

            // Target villager group exists and is owned by player
            fail = ValidateVillagerGroup(state, targetVillagerGroupID, out var targetGroup);
            if (fail != null) return fail;

            // Path exists from building to group
            var path = state.mapData.FindPath(building.coordinate, targetGroup.coordinate, PlayerID, state);
            if (path == null)
                return EngineCommandResult.Failure("No valid path from building to villager group");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            fail = ValidateVillagerGroup(state, targetVillagerGroupID, out var targetGroup);
            if (fail != null) return fail;

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
                $"Marching Villagers ({removed})",
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

            DebugLog.Log($"JoinVillagerGroupCommand: Dispatched {removed} villagers from {building.coordinate} to group {targetGroup.name} ({path.Count} steps)");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
