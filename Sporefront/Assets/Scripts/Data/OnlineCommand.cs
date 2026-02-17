// ============================================================================
// FILE: Data/OnlineCommand.cs
// PURPOSE: Serializable command wrapper for online command streaming
//          C# port of OnlineCommand.swift (162 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Sporefront.Engine;

namespace Sporefront.Data
{
    // ================================================================
    // Online Command Exception
    // ================================================================

    public class OnlineCommandException : Exception
    {
        public OnlineCommandException(string message) : base(message) { }

        public static OnlineCommandException SerializationFailed(string detail)
        {
            return new OnlineCommandException(
                string.Format("Failed to serialize command: {0}", detail));
        }

        public static OnlineCommandException DeserializationFailed(string detail)
        {
            return new OnlineCommandException(
                string.Format("Failed to deserialize command: {0}", detail));
        }

        public static OnlineCommandException UnknownCommandType(string type)
        {
            return new OnlineCommandException(
                string.Format("Unknown command type: {0}", type));
        }
    }

    // ================================================================
    // Online Command
    // ================================================================

    [Serializable]
    public class OnlineCommand
    {
        public int sequence;
        public string commandID;
        public string commandType;
        public string playerID;
        public double timestamp;
        public string payload; // base64-encoded JSON
        public string createdAt; // ISO 8601 string (JsonUtility cannot serialize DateTime)
        public bool isAICommand;

        // ================================================================
        // Create from IEngineCommand (player commands)
        // ================================================================

        public static OnlineCommand CreateFromCommand(int sequence, IEngineCommand command, bool isAI = false)
        {
            try
            {
                // Serialize the command via JsonUtility on a wrapper or directly
                // Note: JsonUtility requires [Serializable] on the command class
                string json = JsonUtility.ToJson(command);
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                return new OnlineCommand
                {
                    sequence = sequence,
                    commandID = command.Id.ToString(),
                    commandType = command.GetType().Name,
                    playerID = command.PlayerID.ToString(),
                    timestamp = command.Timestamp,
                    isAICommand = isAI,
                    createdAt = DateTime.UtcNow.ToString("o"),
                    payload = base64
                };
            }
            catch (Exception e)
            {
                throw OnlineCommandException.SerializationFailed(e.Message);
            }
        }

        // ================================================================
        // Create from AI Command Envelope
        // ================================================================

        public static OnlineCommand CreateFromAIEnvelope(int sequence, AICommandEnvelope envelope)
        {
            try
            {
                string json = JsonUtility.ToJson(envelope);
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                return new OnlineCommand
                {
                    sequence = sequence,
                    commandID = envelope.commandID,
                    commandType = string.Format("ai_{0}", (AICommandType)envelope.aiCommandType),
                    playerID = envelope.playerID,
                    timestamp = envelope.timestamp,
                    isAICommand = true,
                    createdAt = DateTime.UtcNow.ToString("o"),
                    payload = base64
                };
            }
            catch (Exception e)
            {
                throw OnlineCommandException.SerializationFailed(e.Message);
            }
        }

        // ================================================================
        // Decode to IEngineCommand (stub)
        // ================================================================

        /// <summary>
        /// Decode the payload back to an IEngineCommand.
        /// This is a stub -- full deserialization requires a command type registry
        /// since C# IEngineCommand implementations are not polymorphically
        /// serializable via JsonUtility. In practice, the online system uses
        /// AICommandEnvelope for AI commands and can be extended for player
        /// commands later with a registered type map.
        /// </summary>
        public IEngineCommand ToEngineCommand()
        {
            DebugLog.Log(string.Format(
                "OnlineCommand.ToEngineCommand() stub called for type: {0}",
                commandType));
            return null;
        }

        // ================================================================
        // Decode AI Command Envelope
        // ================================================================

        /// <summary>
        /// Decode the base64 payload back into an AICommandEnvelope.
        /// Returns null on failure.
        /// </summary>
        public AICommandEnvelope ToAICommandEnvelope()
        {
            if (!isAICommand)
            {
                DebugLog.Log("ToAICommandEnvelope called on non-AI command");
                return null;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                return JsonUtility.FromJson<AICommandEnvelope>(json);
            }
            catch (Exception e)
            {
                DebugLog.Log(string.Format(
                    "Failed to decode AICommandEnvelope: {0}", e.Message));
                return null;
            }
        }

        // ================================================================
        // Dictionary Serialization (Firestore)
        // ================================================================

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "sequence", sequence },
                { "commandID", commandID },
                { "commandType", commandType },
                { "playerID", playerID },
                { "timestamp", timestamp },
                { "payload", payload },
                { "createdAt", createdAt },
                { "isAICommand", isAICommand }
            };
        }

        public static OnlineCommand FromDictionary(Dictionary<string, object> data)
        {
            if (data == null) return null;

            // Required fields
            if (!data.ContainsKey("sequence") ||
                !data.ContainsKey("commandID") ||
                !data.ContainsKey("commandType") ||
                !data.ContainsKey("playerID") ||
                !data.ContainsKey("timestamp") ||
                !data.ContainsKey("payload"))
            {
                return null;
            }

            var cmd = new OnlineCommand();

            // sequence may come back as long from Firestore
            cmd.sequence = Convert.ToInt32(data["sequence"]);
            cmd.commandID = data["commandID"].ToString();
            cmd.commandType = data["commandType"].ToString();
            cmd.playerID = data["playerID"].ToString();
            cmd.timestamp = Convert.ToDouble(data["timestamp"]);
            cmd.payload = data["payload"].ToString();

            cmd.createdAt = data.ContainsKey("createdAt")
                ? data["createdAt"].ToString()
                : DateTime.UtcNow.ToString("o");

            cmd.isAICommand = data.ContainsKey("isAICommand")
                && data["isAICommand"] is bool b && b;

            return cmd;
        }
    }
}
