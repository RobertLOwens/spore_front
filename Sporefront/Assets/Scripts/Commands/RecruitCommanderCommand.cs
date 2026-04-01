using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class RecruitCommanderCommand : BaseEngineCommand
    {
        public CommanderSpecialty specialty;

        // Recruitment cost: 100 Food, 50 Ore
        public static readonly Dictionary<ResourceType, int> RecruitCost = new Dictionary<ResourceType, int>
        {
            { ResourceType.Food, 100 },
            { ResourceType.Ore, 50 }
        };

        public RecruitCommanderCommand(Guid playerID, CommanderSpecialty specialty)
            : base(playerID)
        {
            this.specialty = specialty;
        }

        // Reconstruction constructor for online deserialization
        public RecruitCommanderCommand(Guid id, Guid playerID, double timestamp, CommanderSpecialty specialty)
            : base(id, playerID, timestamp)
        {
            this.specialty = specialty;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            // Check player can afford
            if (!player.CanAfford(RecruitCost))
                return EngineCommandResult.Failure("Cannot afford commander recruitment (100 Food, 50 Ore)");

            // Require a Barracks or Castle
            bool hasRecruitBuilding = false;
            foreach (var building in state.GetBuildingsForPlayer(PlayerID))
            {
                if (building.IsOperational &&
                    (building.buildingType == BuildingType.Barracks ||
                     building.buildingType == BuildingType.Castle))
                {
                    hasRecruitBuilding = true;
                    break;
                }
            }
            if (!hasRecruitBuilding)
                return EngineCommandResult.Failure("Requires a Barracks or Castle to recruit commanders");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var player = state.GetPlayer(PlayerID);

            // Find the recruiting building (Barracks or Castle) for spawn location
            BuildingData recruitBuilding = null;
            foreach (var building in state.GetBuildingsForPlayer(PlayerID))
            {
                if (building.IsOperational &&
                    (building.buildingType == BuildingType.Barracks ||
                     building.buildingType == BuildingType.Castle))
                {
                    recruitBuilding = building;
                    break;
                }
            }

            // Deduct resources and emit state changes
            DeductResourcesWithChanges(player, RecruitCost, changeBuilder);

            // Create commander
            var commander = new CommanderData(
                CommanderData.RandomName(),
                specialty,
                PlayerID
            );
            state.AddCommander(commander);

            changeBuilder.Add(new CommanderCreatedChange
            {
                commanderID = commander.id,
                ownerID = PlayerID,
                name = commander.name,
                specialty = specialty.ToString()
            });

            // Deploy commander as a standalone army at the recruiting building
            HexCoordinate baseCoord = recruitBuilding != null
                ? recruitBuilding.coordinate
                : state.GetCityCenter(PlayerID)?.coordinate ?? new HexCoordinate(0, 0);

            var spawnResult = FindSpawnPosition(state, baseCoord, c => state.GetArmies(c).Count);
            HexCoordinate spawnCoord = spawnResult ?? baseCoord;

            // Create army with commander assigned
            var army = new ArmyData(commander.name + "'s Company", spawnCoord, PlayerID);
            army.commanderID = commander.id;
            army.homeBaseID = recruitBuilding?.id ?? state.GetCityCenter(PlayerID)?.id ?? Guid.Empty;
            commander.assignedArmyID = army.id;

            state.AddArmy(army);

            changeBuilder.Add(new ArmyCreatedChange
            {
                armyID = army.id,
                ownerID = PlayerID,
                coordinate = spawnCoord,
                composition = new Dictionary<string, int>()
            });

            DebugLog.Log($"RecruitCommanderCommand: Player recruited {commander.name} ({specialty}) deployed at {spawnCoord}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
