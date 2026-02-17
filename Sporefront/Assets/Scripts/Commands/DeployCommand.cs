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

        public override EngineCommandResult Validate(GameState state)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (composition == null || composition.Count == 0)
                return EngineCommandResult.Failure("Composition cannot be empty");

            // Check building has enough of each unit type in garrison
            foreach (var kvp in composition)
            {
                if (kvp.Value <= 0)
                    return EngineCommandResult.Failure(string.Format("Invalid quantity for {0}", kvp.Key.DisplayName()));

                int garrisoned = building.garrison.ContainsKey(kvp.Key) ? building.garrison[kvp.Key] : 0;
                if (garrisoned < kvp.Value)
                    return EngineCommandResult.Failure(string.Format(
                        "Not enough {0} in garrison (have {1}, need {2})",
                        kvp.Key.DisplayName(), garrisoned, kvp.Value));
            }

            // Army count limit: current armies < max allowed (1 + ccLevel / 2)
            int ccLevel = state.GetCityCenterLevel(PlayerID);
            int maxArmies = 1 + ccLevel / 2;
            var currentArmies = state.GetArmiesForPlayer(PlayerID);
            if (currentArmies.Count >= maxArmies)
                return EngineCommandResult.Failure(string.Format(
                    "Army limit reached ({0}/{1}). Upgrade your City Center to deploy more armies.",
                    currentArmies.Count, maxArmies));

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

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
            HexCoordinate spawnCoord = building.coordinate;

            // Try to find a walkable tile near the building if the building tile is occupied
            var armiesAtBuilding = state.GetArmies(spawnCoord);
            if (armiesAtBuilding.Count >= GameConfig.Stacking.MaxEntitiesPerTile)
            {
                bool found = false;
                foreach (var neighbor in spawnCoord.Neighbors())
                {
                    if (state.mapData.IsValidCoordinate(neighbor) && state.mapData.IsWalkable(neighbor))
                    {
                        var armiesAtNeighbor = state.GetArmies(neighbor);
                        if (armiesAtNeighbor.Count < GameConfig.Stacking.MaxEntitiesPerTile)
                        {
                            spawnCoord = neighbor;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    return EngineCommandResult.Failure("No available spawn position near building");
            }

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

            DebugLog.Log(string.Format("DeployArmyCommand: Deployed army {0} at {1} with {2} unit type(s)",
                army.id, spawnCoord, composition.Count));

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

        public override EngineCommandResult Validate(GameState state)
        {
            if (count <= 0)
                return EngineCommandResult.Failure("Count must be greater than zero");

            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Building is not owned by this player");

            if (building.villagerGarrison < count)
                return EngineCommandResult.Failure(string.Format(
                    "Not enough villagers in garrison (have {0}, need {1})",
                    building.villagerGarrison, count));

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            // Remove villagers from garrison
            building.RemoveVillagersFromGarrison(count);

            // Emit ungarrisoned change
            changeBuilder.Add(new VillagersUngarrisonedChange
            {
                buildingID = buildingID,
                quantity = count
            });

            // Find spawn position near building
            HexCoordinate spawnCoord = building.coordinate;

            // Try to find a walkable tile near the building if needed
            var groupsAtBuilding = state.GetVillagerGroups(spawnCoord);
            if (groupsAtBuilding.Count >= GameConfig.Stacking.MaxEntitiesPerTile)
            {
                bool found = false;
                foreach (var neighbor in spawnCoord.Neighbors())
                {
                    if (state.mapData.IsValidCoordinate(neighbor) && state.mapData.IsWalkable(neighbor))
                    {
                        var groupsAtNeighbor = state.GetVillagerGroups(neighbor);
                        if (groupsAtNeighbor.Count < GameConfig.Stacking.MaxEntitiesPerTile)
                        {
                            spawnCoord = neighbor;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    return EngineCommandResult.Failure("No available spawn position near building");
            }

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

            DebugLog.Log(string.Format("DeployVillagersCommand: Deployed {0} villager(s) at {1}",
                count, spawnCoord));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
