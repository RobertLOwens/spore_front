using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.AI.Commands
{
    /// <summary>
    /// Port of AIDeployVillagersCommand from AIController.swift.
    /// Deploy villagers from building garrison. Validate enough villagers.
    /// Decrement villagerGarrison, find spawn coord, create VillagerGroupData,
    /// emit VillagerGroupCreatedChange + VillagersUngarrisonedChange.
    /// </summary>
    public class AIDeployVillagersCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public int quantity;

        public AIDeployVillagersCommand(Guid playerID, Guid buildingID, int quantity)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.quantity = quantity;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your building");

            if (building.villagerGarrison < quantity)
                return EngineCommandResult.Failure("Not enough villagers in garrison");

            return EngineCommandResult.Success(new List<StateChange>());
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            // Remove villagers from garrison
            building.villagerGarrison -= quantity;

            // Find spawn position near building
            HexCoordinate spawnCoord = state.mapData.FindNearestWalkable(
                building.coordinate, 3, PlayerID, state
            ) ?? building.coordinate;

            // Create villager group
            var group = new VillagerGroupData("AI Villagers", spawnCoord, quantity, PlayerID);
            state.AddVillagerGroup(group);

            changeBuilder.Add(new VillagerGroupCreatedChange
            {
                groupID = group.id,
                ownerID = PlayerID,
                coordinate = spawnCoord,
                count = quantity
            });

            changeBuilder.Add(new VillagersUngarrisonedChange
            {
                buildingID = buildingID,
                quantity = quantity
            });

            DebugLog.Log(string.Format("AI deployed {0} villagers at ({1}, {2})",
                quantity, spawnCoord.q, spawnCoord.r));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
