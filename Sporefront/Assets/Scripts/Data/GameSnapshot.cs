// ============================================================================
// FILE: Data/GameSnapshot.cs
// PURPOSE: Periodic game state snapshots for online session recovery
//          C# port of GameSnapshot.swift (99 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Sporefront.Engine;

namespace Sporefront.Data
{
    // ================================================================
    // Game Snapshot
    // ================================================================

    [Serializable]
    public class GameSnapshot
    {
        public string snapshotID;
        public string createdAt; // ISO 8601 string
        public double gameTime;
        public int commandSequence;
        public string stateJSON; // base64-encoded GameState JSON
        public int sizeBytes;

        // ================================================================
        // Factory: Create from GameState
        // ================================================================

        public static GameSnapshot Create(GameState gameState, int sequence)
        {
            try
            {
                string json = JsonUtility.ToJson(gameState);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                return new GameSnapshot
                {
                    snapshotID = Guid.NewGuid().ToString(),
                    createdAt = DateTime.UtcNow.ToString("o"),
                    gameTime = gameState.currentTime,
                    commandSequence = sequence,
                    stateJSON = Convert.ToBase64String(jsonBytes),
                    sizeBytes = jsonBytes.Length
                };
            }
            catch (Exception e)
            {
                DebugLog.Log(string.Format("Failed to create game snapshot: {0}", e.Message));
                return null;
            }
        }

        // ================================================================
        // Restore GameState
        // ================================================================

        public GameState ToGameState()
        {
            if (string.IsNullOrEmpty(stateJSON))
            {
                throw new GameSnapshotException(GameSnapshotException.CorruptedData());
            }

            byte[] data;
            try
            {
                data = Convert.FromBase64String(stateJSON);
            }
            catch (FormatException)
            {
                throw new GameSnapshotException(GameSnapshotException.CorruptedData());
            }

            string jsonString = Encoding.UTF8.GetString(data);

            try
            {
                return JsonUtility.FromJson<GameState>(jsonString);
            }
            catch (Exception e)
            {
                throw new GameSnapshotException(
                    GameSnapshotException.DecodingFailed(e.Message));
            }
        }

        // ================================================================
        // Firestore Serialization
        // ================================================================

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "snapshotID", snapshotID },
                { "createdAt", createdAt },
                { "gameTime", gameTime },
                { "commandSequence", commandSequence },
                { "stateJSON", stateJSON },
                { "sizeBytes", sizeBytes }
            };
        }

        public static GameSnapshot FromDictionary(Dictionary<string, object> data)
        {
            object val;

            if (!data.TryGetValue("snapshotID", out val) || !(val is string))
                return null;
            string id = (string)val;

            if (!data.TryGetValue("gameTime", out val))
                return null;
            double time = Convert.ToDouble(val);

            if (!data.TryGetValue("commandSequence", out val))
                return null;
            int sequence = Convert.ToInt32(val);

            if (!data.TryGetValue("stateJSON", out val) || !(val is string))
                return null;
            string state = (string)val;

            if (!data.TryGetValue("sizeBytes", out val))
                return null;
            int size = Convert.ToInt32(val);

            string created = DateTime.UtcNow.ToString("o");
            if (data.TryGetValue("createdAt", out val) && val is string)
            {
                created = (string)val;
            }

            return new GameSnapshot
            {
                snapshotID = id,
                createdAt = created,
                gameTime = time,
                commandSequence = sequence,
                stateJSON = state,
                sizeBytes = size
            };
        }
    }

    // ================================================================
    // Game Snapshot Exception
    // ================================================================

    public class GameSnapshotException : Exception
    {
        public GameSnapshotException(string message) : base(message) { }

        public static string CorruptedData()
        {
            return "Snapshot data is corrupted or missing.";
        }

        public static string DecodingFailed(string detail)
        {
            return string.Format("Failed to decode snapshot: {0}", detail);
        }
    }
}
