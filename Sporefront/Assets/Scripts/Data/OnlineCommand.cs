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
        public string senderUID; // Firebase Auth UID of the submitting client
        public long stateHash;   // Periodic state hash for desync detection (0 = not included)

        // ================================================================
        // Create from IEngineCommand (player commands)
        // ================================================================

        public static OnlineCommand CreateFromCommand(int sequence, IEngineCommand command, bool isAI = false)
        {
            try
            {
                // Serialize via PlayerCommandRegistry to handle Dictionary and
                // nullable Guid fields that JsonUtility silently drops
                string json = PlayerCommandRegistry.Serialize(command);
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
        // Decode to IEngineCommand
        // ================================================================

        /// <summary>
        /// Decode the payload back to an IEngineCommand.
        /// Routes AI commands through AICommandEnvelope and player commands
        /// through PlayerCommandRegistry for type-safe deserialization.
        /// </summary>
        public IEngineCommand ToEngineCommand()
        {
            try
            {
                if (isAICommand)
                {
                    var envelope = ToAICommandEnvelope();
                    return envelope?.ToEngineCommand();
                }

                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                return PlayerCommandRegistry.Deserialize(commandType, json, commandID, playerID, timestamp);
            }
            catch (Exception e)
            {
                DebugLog.Log(string.Format(
                    "OnlineCommand.ToEngineCommand() failed for type {0}: {1}",
                    commandType, e.Message));
                return null;
            }
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
            var dict = new Dictionary<string, object>
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
            if (!string.IsNullOrEmpty(senderUID))
                dict["senderUID"] = senderUID;
            if (stateHash != 0)
                dict["stateHash"] = stateHash;
            return dict;
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

            cmd.senderUID = data.ContainsKey("senderUID")
                ? data["senderUID"] as string : null;

            if (data.ContainsKey("stateHash"))
            {
                try { cmd.stateHash = Convert.ToInt64(data["stateHash"]); }
                catch { cmd.stateHash = 0; }
            }

            return cmd;
        }
    }
}
