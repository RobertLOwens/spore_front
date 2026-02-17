using System;
using System.Collections.Generic;
using Sporefront.Engine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Commands
{
    public class MarketTradeCommand : BaseEngineCommand
    {
        public Guid buildingID;
        public Dictionary<ResourceType, int> inputResources;
        public ResourceType outputType;

        private const double BaseTradeRate = 0.80;

        public MarketTradeCommand(Guid playerID, Guid buildingID,
            Dictionary<ResourceType, int> inputResources, ResourceType outputType)
            : base(playerID)
        {
            this.buildingID = buildingID;
            this.inputResources = inputResources;
            this.outputType = outputType;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            var building = state.GetBuilding(buildingID);
            if (building == null)
                return EngineCommandResult.Failure("Building not found");

            if (building.buildingType != BuildingType.Market)
                return EngineCommandResult.Failure("Building is not a market");

            if (!building.IsOperational)
                return EngineCommandResult.Failure("Market is not operational");

            if (!building.ownerID.HasValue || building.ownerID.Value != PlayerID)
                return EngineCommandResult.Failure("Market is not owned by this player");

            // Must have at least some input
            int totalInput = 0;
            foreach (var kvp in inputResources)
            {
                if (kvp.Value < 0)
                    return EngineCommandResult.Failure("Negative input amount");

                if (kvp.Key == outputType && kvp.Value > 0)
                    return EngineCommandResult.Failure("Cannot trade a resource for itself");

                if (kvp.Value > 0 && !player.HasResource(kvp.Key, kvp.Value))
                    return EngineCommandResult.Failure(
                        string.Format("Insufficient {0}: need {1}", kvp.Key, kvp.Value));

                totalInput += kvp.Value;
            }

            if (totalInput <= 0)
                return EngineCommandResult.Failure("No resources to trade");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var player = state.GetPlayer(PlayerID);
            if (player == null)
                return EngineCommandResult.Failure("Player not found");

            // Deduct input resources
            int totalInput = 0;
            foreach (var kvp in inputResources)
            {
                if (kvp.Value > 0)
                {
                    player.RemoveResource(kvp.Key, kvp.Value);
                    totalInput += kvp.Value;
                }
            }

            // Calculate output
            int outputAmount = (int)(totalInput * BaseTradeRate);
            if (outputAmount < 1) outputAmount = 1;

            // Add output resource
            int storageCapacity = state.GetStorageCapacity(PlayerID, outputType);
            player.AddResource(outputType, outputAmount, storageCapacity);

            changeBuilder.Add(new ResourcesChangedChange { playerID = PlayerID });

            DebugLog.Log(string.Format("MarketTradeCommand: Player {0} traded {1} resources for {2} {3}",
                PlayerID, totalInput, outputAmount, outputType));

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }

        public static int CalculateOutput(int totalInput)
        {
            int result = (int)(totalInput * BaseTradeRate);
            return result < 1 && totalInput > 0 ? 1 : result;
        }
    }
}
