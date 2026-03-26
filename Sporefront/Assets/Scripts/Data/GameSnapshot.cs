// ============================================================================
// FILE: Data/GameSnapshot.cs
// PURPOSE: Periodic game state snapshots for online session recovery
//          C# port of GameSnapshot.swift (99 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Sporefront.Data.Serialization;
using Sporefront.Engine;
using Sporefront.Models;

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
        public string gameVersion; // Game version that created this snapshot

        // Shared serializer settings (matches SaveManager)
        private static JsonSerializerSettings _settings;
        private static JsonSerializerSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.None,
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.None,
                        Converters = new List<JsonConverter>
                        {
                            new HexCoordinateConverter()
                        }
                    };
                }
                return _settings;
            }
        }

        // ================================================================
        // Factory: Create from GameState
        // ================================================================

        public static GameSnapshot Create(GameState gameState, int sequence)
        {
            try
            {
                // Use FullSnapshotData + Newtonsoft to properly serialize
                // Dictionary<Guid,T> and map tile data that JsonUtility silently drops
                var fullData = new FullSnapshotData(gameState);
                string json = JsonConvert.SerializeObject(fullData, Settings);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                return new GameSnapshot
                {
                    snapshotID = Guid.NewGuid().ToString(),
                    createdAt = DateTime.UtcNow.ToString("o"),
                    gameTime = gameState.currentTime,
                    commandSequence = sequence,
                    stateJSON = Convert.ToBase64String(jsonBytes),
                    sizeBytes = jsonBytes.Length,
                    gameVersion = Application.version
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
                var fullData = JsonConvert.DeserializeObject<FullSnapshotData>(jsonString, Settings);
                if (fullData == null)
                    throw new Exception("Deserialized snapshot is null");
                return fullData.Restore();
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
            var dict = new Dictionary<string, object>
            {
                { "snapshotID", snapshotID },
                { "createdAt", createdAt },
                { "gameTime", gameTime },
                { "commandSequence", commandSequence },
                { "stateJSON", stateJSON },
                { "sizeBytes", sizeBytes }
            };
            if (!string.IsNullOrEmpty(gameVersion))
                dict["gameVersion"] = gameVersion;
            return dict;
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

            string version = null;
            if (data.TryGetValue("gameVersion", out val) && val is string v)
                version = v;

            return new GameSnapshot
            {
                snapshotID = id,
                createdAt = created,
                gameTime = time,
                commandSequence = sequence,
                stateJSON = state,
                sizeBytes = size,
                gameVersion = version
            };
        }
    }

    // ================================================================
    // Full Snapshot Data (entities + terrain + resource positions)
    // ================================================================

    /// <summary>
    /// Complete serializable game state including terrain tiles and resource
    /// point positions that GameStateSnapshot alone does not include.
    /// Used by GameSnapshot for online recovery and matchmaking state transfer.
    /// </summary>
    [Serializable]
    public class FullSnapshotData
    {
        public GameStateSnapshot snapshot;
        public List<SerializedTileData> tiles;
        public List<SerializedResourcePoint> resourcePointPositions;
        public double gameSpeed;
        public VisibilityMode visibilityMode;

        public FullSnapshotData() { }

        public FullSnapshotData(GameState gameState)
        {
            snapshot = new GameStateSnapshot(gameState);
            gameSpeed = gameState.gameSpeed;
            visibilityMode = gameState.visibilityMode;

            // Serialize terrain tiles
            tiles = new List<SerializedTileData>();
            foreach (var kvp in gameState.mapData.tiles)
            {
                tiles.Add(new SerializedTileData(kvp.Key, kvp.Value));
            }

            // Serialize resource point coordinates
            resourcePointPositions = new List<SerializedResourcePoint>();
            foreach (var kvp in gameState.mapData.resourcePointCoordinates)
            {
                resourcePointPositions.Add(new SerializedResourcePoint(kvp.Key, kvp.Value));
            }
        }

        public GameState Restore()
        {
            var gameState = snapshot.Restore();
            gameState.gameSpeed = gameSpeed;
            gameState.visibilityMode = visibilityMode;

            // Restore terrain tiles
            if (tiles != null)
            {
                foreach (var tile in tiles)
                {
                    var coord = new HexCoordinate(tile.q, tile.r);
                    gameState.mapData.SetTile(new TileData(coord, tile.terrain, tile.elevation));
                }
            }

            // Restore resource point coordinates
            if (resourcePointPositions != null)
            {
                foreach (var rpp in resourcePointPositions)
                {
                    var coord = new HexCoordinate(rpp.q, rpp.r);
                    if (!gameState.mapData.resourcePointCoordinates.ContainsKey(rpp.id))
                        gameState.mapData.resourcePointCoordinates[rpp.id] = coord;
                }
            }

            return gameState;
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
