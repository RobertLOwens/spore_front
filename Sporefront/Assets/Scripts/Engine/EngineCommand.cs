using System;
using System.Collections.Generic;
using Sporefront.Data;

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

        public virtual EngineCommandResult Validate(GameState state)
        {
            return EngineCommandResult.Success(new List<StateChange>());
        }

        public virtual EngineCommandResult Execute(GameState state, StateChangeBuilder changeBuilder)
        {
            return EngineCommandResult.Success(new List<StateChange>());
        }
    }
}
