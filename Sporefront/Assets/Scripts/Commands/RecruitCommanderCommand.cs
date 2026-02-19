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

            // Deduct resources
            foreach (var kvp in RecruitCost)
                player.RemoveResource(kvp.Key, kvp.Value);

            // Create commander
            var commander = new CommanderData(
                CommanderData.RandomName(),
                specialty,
                PlayerID
            );
            state.AddCommander(commander);

            DebugLog.Log($"RecruitCommanderCommand: Player recruited {commander.name} ({specialty})");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
