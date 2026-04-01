using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class DeployArmyCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Dictionary<MilitaryUnitType, int> composition;

        public DeployArmyCommand(Guid playerID, Guid buildingID, Dictionary<MilitaryUnitType, int> composition)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.composition = composition;
        }

        // Reconstruction constructor for online deserialization
        public DeployArmyCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, Dictionary<MilitaryUnitType, int> composition)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.composition = composition;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (composition == null || composition.Count == 0)
                return EngineCommandResult.Failure("Composition cannot be empty");

            // Check building has enough of each unit type in garrison
            foreach (var kvp in composition)
            {
                if (kvp.Value <= 0)
                    return EngineCommandResult.Failure($"Invalid quantity for {kvp.Key.DisplayName()}");

                int garrisoned = building.garrison.ContainsKey(kvp.Key) ? building.garrison[kvp.Key] : 0;
                if (garrisoned < kvp.Value)
                    return EngineCommandResult.Failure(
                        $"Not enough {kvp.Key.DisplayName()} in garrison (have {garrisoned}, need {kvp.Value})");
            }

            // Army count limit: current armies < max allowed (1 + ccLevel / 2)
            int ccLevel = state.GetCityCenterLevel(PlayerID);
            int maxArmies = 1 + ccLevel / 2;
            var currentArmies = state.GetArmiesForPlayer(PlayerID);
            if (currentArmies.Count >= maxArmies)
                return EngineCommandResult.Failure(
                    $"Army limit reached ({currentArmies.Count}/{maxArmies}). Upgrade your City Center to deploy more armies.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            // Remove units from garrison and emit ungarrisoned changes
            foreach (var kvp in composition)
            {
                building.RemoveFromGarrison(kvp.Key, kvp.Value);

                changeBuilder.Add(new UnitsUngarrisonedChange
                {
                    buildingID = buildingID,
                    unitType = kvp.Key.ToString(),
                    quantity = kvp.Value
                });
            }

            // Find spawn position near building
            var spawnResult = FindSpawnPosition(state, building.coordinate, c => state.GetArmies(c).Count);
            if (!spawnResult.HasValue)
                return EngineCommandResult.Failure("No available spawn position near building");
            HexCoordinate spawnCoord = spawnResult.Value;

            // Create army
            var army = new ArmyData("Army", spawnCoord, PlayerID);
            army.homeBaseID = buildingID;

            // Add units to army
            foreach (var kvp in composition)
                army.AddMilitaryUnits(kvp.Key, kvp.Value);

            // Add to state
            state.AddArmy(army);

            // Build composition dict for change (string keys)
            var compositionStrings = new Dictionary<string, int>();
            foreach (var kvp in composition)
                compositionStrings[kvp.Key.ToString()] = kvp.Value;

            // Emit army created change
            changeBuilder.Add(new ArmyCreatedChange
            {
                armyID = army.id,
                ownerID = PlayerID,
                coordinate = spawnCoord,
                composition = compositionStrings
            });

            DebugLog.Log($"DeployArmyCommand: Deployed army {army.id} at {spawnCoord} with {composition.Count} unit type(s)");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }

    public class DeployVillagersCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public int count;

        public DeployVillagersCommand(Guid playerID, Guid buildingID, int count)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.count = count;
        }

        // Reconstruction constructor for online deserialization
        public DeployVillagersCommand(Guid id, Guid playerID, double timestamp, Guid buildingID, int count)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.count = count;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            if (count <= 0)
                return EngineCommandResult.Failure("Count must be greater than zero");

            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            if (building.villagerGarrison < count)
                return EngineCommandResult.Failure(
                    $"Not enough villagers in garrison (have {building.villagerGarrison}, need {count})");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidateOwnedBuilding(state, buildingID, out var building);
            if (fail != null) return fail;

            // Remove villagers from garrison
            building.RemoveVillagersFromGarrison(count);

            // Emit ungarrisoned change
            changeBuilder.Add(new VillagersUngarrisonedChange
            {
                buildingID = buildingID,
                quantity = count
            });

            // Find spawn position near building
            var spawnResult = FindSpawnPosition(state, building.coordinate, c => state.GetVillagerGroups(c).Count);
            if (!spawnResult.HasValue)
                return EngineCommandResult.Failure("No available spawn position near building");
            HexCoordinate spawnCoord = spawnResult.Value;

            // Create villager group
            var group = new VillagerGroupData("Villagers", spawnCoord, count, PlayerID);

            // Add to state
            state.AddVillagerGroup(group);

            // Emit villager group created change
            changeBuilder.Add(new VillagerGroupCreatedChange
            {
                groupID = group.id,
                ownerID = PlayerID,
                coordinate = spawnCoord,
                count = count
            });

            DebugLog.Log($"DeployVillagersCommand: Deployed {count} villager(s) at {spawnCoord}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
