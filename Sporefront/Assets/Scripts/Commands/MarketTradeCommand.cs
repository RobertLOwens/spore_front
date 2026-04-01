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

        // Reconstruction constructor for online deserialization
        public MarketTradeCommand(Guid id, Guid playerID, double timestamp, Guid buildingID,
            Dictionary<ResourceType, int> inputResources, ResourceType outputType)
            : base(id, playerID, timestamp)
        {
            this.buildingID = buildingID;
            this.inputResources = inputResources;
            this.outputType = outputType;
        }

        public override EngineCommandResult Validate(GameState state)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

            fail = ValidateOperationalBuilding(state, buildingID, out var building, BuildingType.Market);
            if (fail != null) return fail;

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
                        $"Insufficient {kvp.Key}: need {kvp.Value}");

                totalInput += kvp.Value;
            }

            if (totalInput <= 0)
                return EngineCommandResult.Failure("No resources to trade");

            return EngineCommandResult.Success(null);
        }

        public override EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            var fail = ValidatePlayer(state, out var player);
            if (fail != null) return fail;

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

            // Calculate output with MarketRate research bonus
            double tradeRate = BaseTradeRate;
            double marketBonus = player.GetResearchBonus(
                ResearchBonusType.MarketRate.ToString());
            tradeRate = Math.Min(0.98, tradeRate + marketBonus);
            int outputAmount = (int)(totalInput * tradeRate);
            if (outputAmount < 1) outputAmount = 1;

            // Add output resource
            int storageCapacity = state.GetStorageCapacity(PlayerID, outputType);
            player.AddResource(outputType, outputAmount, storageCapacity);

            changeBuilder.Add(new ResourcesChangedChange { playerID = PlayerID });

            DebugLog.Log($"MarketTradeCommand: Player {PlayerID} traded {totalInput} resources for {outputAmount} {outputType}");

            return EngineCommandResult.Success(changeBuilder.Build().changes);
        }

        public static int CalculateOutput(int totalInput, double marketBonus = 0.0)
        {
            double rate = Math.Min(0.98, BaseTradeRate + marketBonus);
            int result = (int)(totalInput * rate);
            return result < 1 && totalInput > 0 ? 1 : result;
        }
    }
}
