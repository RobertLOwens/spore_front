using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class BuildCommand : BaseEngineCommand
    {
        public BuildingType buildingType;
        public HexCoordinate coordinate;
        public int rotation;

        public BuildCommand(Guid playerID, BuildingType buildingType, HexCoordinate coordinate, int rotation = 0)
            : base(playerID)
        {
            this.buildingType = buildingType;
            this.coordinate = coordinate;
            this.rotation = rotation;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            // Check player exists
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found.");

            // Get all coordinates this building will occupy
            var occupiedCoordinates = buildingType.GetOccupiedCoordinates(coordinate, rotation);

            // Validate each occupied coordinate
            foreach (var coord in occupiedCoordinates)
            {
                if (!state.mapData.IsValidCoordinate(coord))
                    return EngineCommandResult.Failure($"Invalid coordinate: ({coord.q}, {coord.r}).");

                if (!state.mapData.IsWalkable(coord))
                    return EngineCommandResult.Failure($"Coordinate ({coord.q}, {coord.r}) is not buildable.");

                if (state.GetBuilding(coord) != null)
                    return EngineCommandResult.Failure($"A building already exists at ({coord.q}, {coord.r}).");
            }

            // Check City Center level requirement
            int requiredCCLevel = buildingType.RequiredCityCenterLevel();
            int currentCCLevel = state.GetCityCenterLevel(PlayerID);
            if (currentCCLevel < requiredCCLevel)
                return EngineCommandResult.Failure($"Requires City Center level {requiredCCLevel} (current: {currentCCLevel}).");

            // Check resources
            var buildCost = buildingType.BuildCost();
            if (!player.CanAfford(buildCost))
                return EngineCommandResult.Failure("Insufficient resources.");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            // Re-validate before executing
            var validation = Validate(state);
            if (!validation.Succeeded)
                return validation;

            var player = state.GetPlayer(PlayerID);

            // Deduct resources
            var buildCost = buildingType.BuildCost();
            foreach (var kvp in buildCost)
            {
                player.RemoveResource(kvp.Key, kvp.Value);
            }

            // Create the building
            var building = new BuildingData(buildingType, coordinate, PlayerID, rotation);

            // Start construction with 1 builder
            building.StartConstruction(1);

            // Add building to game state
            state.AddBuilding(building);

            // Emit state changes
            changeBuilder.Add(new BuildingPlacedChange
            {
                buildingID = building.id,
                buildingType = buildingType.ToString(),
                coordinate = coordinate,
                ownerID = PlayerID,
                rotation = rotation
            });

            changeBuilder.Add(new BuildingConstructionStartedChange
            {
                buildingID = building.id
            });

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }
    }
}
