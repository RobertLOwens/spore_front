using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    public class EngineCommandResult
    {
        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        public List<StateChange> Changes { get; private set; }

        private EngineCommandResult() { }

        public static EngineCommandResult Success(List<StateChange> changes)
        {
            return new EngineCommandResult
            {
                Succeeded = true,
                FailureReason = null,
                Changes = changes ?? new List<StateChange>()
            };
        }

        public static EngineCommandResult Failure(string reason)
        {
            return new EngineCommandResult
            {
                Succeeded = false,
                FailureReason = reason,
                Changes = new List<StateChange>()
            };
        }
    }

    public interface IEngineCommand
    {
        Guid Id { get; }
        Guid PlayerID { get; }
        double Timestamp { get; }
        EngineCommandResult Validate(GameState state);
        EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder);
    }

    public abstract class BaseEngineCommand : IEngineCommand
    {
        public Guid Id { get; private set; }
        public Guid PlayerID { get; private set; }
        public double Timestamp { get; private set; }

        protected BaseEngineCommand(Guid playerID)
        {
            Id = Guid.NewGuid();
            PlayerID = playerID;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        /// <summary>
        /// Reconstruction constructor for deserializing commands from online sessions.
        /// Preserves the original command ID, player ID, and timestamp instead of
        /// generating new ones, so that remote commands can be deduplicated.
        /// </summary>
        protected BaseEngineCommand(Guid id, Guid playerID, double timestamp)
        {
            Id = id;
            PlayerID = playerID;
            Timestamp = timestamp;
        }

        public virtual EngineCommandResult Validate(GameState state)
        {
            return EngineCommandResult.Success(new List<StateChange>());
        }

        public virtual EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            return EngineCommandResult.Success(new List<StateChange>());
        }

        // ================================================================
        // Validation Helpers
        // ================================================================

        protected EngineCommandResult ValidatePlayer(GameState state, out PlayerState player)
        {
            player = state.GetPlayer(PlayerID);
            return player == null ? EngineCommandResult.Failure("Player not found") : null;
        }

        protected EngineCommandResult ValidateBuilding(GameState state, Guid buildingID, out BuildingData building)
        {
            building = state.GetBuilding(buildingID);
            return building == null ? EngineCommandResult.Failure("Building not found") : null;
        }

        protected EngineCommandResult ValidateOwnedBuilding(GameState state, Guid buildingID, out BuildingData building)
        {
            building = state.GetBuilding(buildingID);
            if (building == null) return EngineCommandResult.Failure("Building not found");
            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your building");
            return null;
        }

        protected EngineCommandResult ValidateArmy(GameState state, Guid armyID, out ArmyData army)
        {
            army = state.GetArmy(armyID);
            return army == null ? EngineCommandResult.Failure("Army not found") : null;
        }

        protected EngineCommandResult ValidateOwnedArmy(GameState state, Guid armyID, out ArmyData army)
        {
            army = state.GetArmy(armyID);
            if (army == null) return EngineCommandResult.Failure("Army not found");
            if (!army.ownerID.HasValue || army.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your army");
            return null;
        }

        protected EngineCommandResult ValidateOperationalBuilding(GameState state, Guid buildingID, out BuildingData building, BuildingType? requiredType = null)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out building);
            if (fail != null) return fail;
            if (!building.IsOperational)
                return EngineCommandResult.Failure("Building is not operational");
            if (requiredType.HasValue && building.buildingType != requiredType.Value)
                return EngineCommandResult.Failure($"Building is not a {requiredType.Value.DisplayName()}");
            return null;
        }

        protected EngineCommandResult ValidateVillagerGroup(GameState state, Guid groupID, out VillagerGroupData group)
        {
            group = state.GetVillagerGroup(groupID);
            if (group == null) return EngineCommandResult.Failure("Villager group not found");
            if (!group.ownerID.HasValue || group.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Not your villagers");
            return null;
        }

        protected EngineCommandResult ValidateCanAfford(PlayerState player, Dictionary<ResourceType, int> costs, int multiplier = 1)
        {
            if (multiplier == 1) return player.CanAfford(costs) ? null : EngineCommandResult.Failure("Insufficient resources");
            foreach (var kvp in costs)
            {
                if (!player.HasResource(kvp.Key, kvp.Value * multiplier))
                    return EngineCommandResult.Failure("Insufficient resources");
            }
            return null;
        }

        /// <summary>
        /// Deducts resources from a player and emits ResourcesChangedChange for each resource type.
        /// Returns false if the player cannot afford the cost (caller should validate first).
        /// </summary>
        protected void DeductResourcesWithChanges(PlayerState player, Dictionary<ResourceType, int> cost, StateChangeBuilder changeBuilder)
        {
            foreach (var kvp in cost)
            {
                int oldAmount = player.GetResource(kvp.Key);
                player.RemoveResource(kvp.Key, kvp.Value);
                int newAmount = player.GetResource(kvp.Key);

                changeBuilder.Add(new ResourcesChangedChange
                {
                    playerID = PlayerID,
                    resourceType = kvp.Key.ToString(),
                    oldAmount = oldAmount,
                    newAmount = newAmount
                });
            }
        }
        /// <summary>
        /// Finds a spawn position near a building, avoiding tiles at entity capacity.
        /// Returns the building coordinate if available, or a neighboring walkable tile.
        /// Returns null if no position is available.
        /// </summary>
        protected static HexCoordinate? FindSpawnPosition(GameState state, HexCoordinate buildingCoord, Func<HexCoordinate, int> getEntityCount)
        {
            if (getEntityCount(buildingCoord) < GameConfig.Stacking.MaxEntitiesPerTile)
                return buildingCoord;

            foreach (var neighbor in buildingCoord.Neighbors())
            {
                if (state.mapData.IsValidCoordinate(neighbor) && state.mapData.IsWalkable(neighbor))
                {
                    if (getEntityCount(neighbor) < GameConfig.Stacking.MaxEntitiesPerTile)
                        return neighbor;
                }
            }
            return null;
        }
    }
}
