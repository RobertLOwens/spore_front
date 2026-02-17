// ============================================================================
// FILE: Data/GameSession.cs
// PURPOSE: Online game session data types for multiplayer pipeline
//          C# port of GameSession.swift (273 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Engine;

namespace Sporefront.Data
{
    // ================================================================
    // Game Session Status
    // ================================================================

    public enum GameSessionStatus
    {
        Lobby,
        Playing,
        Paused,
        Finished
    }

    // ================================================================
    // Player Session Status
    // ================================================================

    public enum PlayerSessionStatus
    {
        Active,
        Disconnected,
        Defeated,
        Left
    }

    // ================================================================
    // Map Generation Config
    // ================================================================

    [Serializable]
    public class MapGenerationConfig
    {
        public string mapType;
        public ulong seed;
        public int width;
        public int height;

        // ArabiaMapConfig fields
        public int treePocketCount = 25;
        public int treePocketSizeMin = 3;
        public int treePocketSizeMax = 8;
        public int mineralDepositCount = 12;
        public int mineralDepositSizeMin = 2;
        public int mineralDepositSizeMax = 4;
        public double hillClusterChance = 0.15;
        public int maxElevation = 3;

        public MapGenerationConfig(string mapType, ulong seed, int width, int height)
        {
            this.mapType = mapType;
            this.seed = seed;
            this.width = width;
            this.height = height;
        }

        public ArabiaMapConfig ToArabiaConfig()
        {
            var config = new ArabiaMapConfig();
            config.treePocketCount = treePocketCount;
            config.treePocketSizeMin = treePocketSizeMin;
            config.treePocketSizeMax = treePocketSizeMax;
            config.mineralDepositCount = mineralDepositCount;
            config.mineralDepositSizeMin = mineralDepositSizeMin;
            config.mineralDepositSizeMax = mineralDepositSizeMax;
            config.hillClusterChance = hillClusterChance;
            config.maxElevation = maxElevation;
            return config;
        }

        public static MapGenerationConfig FromArabia(ulong seed, ArabiaMapConfig config = null)
        {
            if (config == null)
                config = new ArabiaMapConfig();

            var mapConfig = new MapGenerationConfig("arabia", seed, 35, 35);
            mapConfig.treePocketCount = config.treePocketCount;
            mapConfig.treePocketSizeMin = config.treePocketSizeMin;
            mapConfig.treePocketSizeMax = config.treePocketSizeMax;
            mapConfig.mineralDepositCount = config.mineralDepositCount;
            mapConfig.mineralDepositSizeMin = config.mineralDepositSizeMin;
            mapConfig.mineralDepositSizeMax = config.mineralDepositSizeMax;
            mapConfig.hillClusterChance = config.hillClusterChance;
            mapConfig.maxElevation = config.maxElevation;
            return mapConfig;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "mapType", mapType },
                { "seed", (long)seed },  // Network layer may not support ulong directly
                { "width", width },
                { "height", height },
                { "treePocketCount", treePocketCount },
                { "treePocketSizeMin", treePocketSizeMin },
                { "treePocketSizeMax", treePocketSizeMax },
                { "mineralDepositCount", mineralDepositCount },
                { "mineralDepositSizeMin", mineralDepositSizeMin },
                { "mineralDepositSizeMax", mineralDepositSizeMax },
                { "hillClusterChance", hillClusterChance },
                { "maxElevation", maxElevation }
            };
        }

        public static MapGenerationConfig FromDictionary(Dictionary<string, object> data)
        {
            string mapType = data.ContainsKey("mapType") ? data["mapType"] as string ?? "arabia" : "arabia";
            long seedInt64 = data.ContainsKey("seed") ? Convert.ToInt64(data["seed"]) : 0;
            ulong seed = (ulong)seedInt64;
            int width = data.ContainsKey("width") ? Convert.ToInt32(data["width"]) : 35;
            int height = data.ContainsKey("height") ? Convert.ToInt32(data["height"]) : 35;

            var config = new MapGenerationConfig(mapType, seed, width, height);
            if (data.ContainsKey("treePocketCount")) config.treePocketCount = Convert.ToInt32(data["treePocketCount"]);
            if (data.ContainsKey("treePocketSizeMin")) config.treePocketSizeMin = Convert.ToInt32(data["treePocketSizeMin"]);
            if (data.ContainsKey("treePocketSizeMax")) config.treePocketSizeMax = Convert.ToInt32(data["treePocketSizeMax"]);
            if (data.ContainsKey("mineralDepositCount")) config.mineralDepositCount = Convert.ToInt32(data["mineralDepositCount"]);
            if (data.ContainsKey("mineralDepositSizeMin")) config.mineralDepositSizeMin = Convert.ToInt32(data["mineralDepositSizeMin"]);
            if (data.ContainsKey("mineralDepositSizeMax")) config.mineralDepositSizeMax = Convert.ToInt32(data["mineralDepositSizeMax"]);
            if (data.ContainsKey("hillClusterChance")) config.hillClusterChance = Convert.ToDouble(data["hillClusterChance"]);
            if (data.ContainsKey("maxElevation")) config.maxElevation = Convert.ToInt32(data["maxElevation"]);
            return config;
        }
    }

    // ================================================================
    // Game Session Player
    // ================================================================

    [Serializable]
    public class GameSessionPlayer
    {
        public string uid;
        public string displayName;
        public string playerID;    // UUID string
        public string colorHex;
        public bool isAI;
        public bool isHost;
        public PlayerSessionStatus status;
        public DateTime lastHeartbeat;

        public GameSessionPlayer(
            string uid,
            string displayName,
            string playerID,
            string colorHex,
            bool isAI,
            bool isHost,
            PlayerSessionStatus status,
            DateTime lastHeartbeat)
        {
            this.uid = uid;
            this.displayName = displayName;
            this.playerID = playerID;
            this.colorHex = colorHex;
            this.isAI = isAI;
            this.isHost = isHost;
            this.status = status;
            this.lastHeartbeat = lastHeartbeat;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "uid", uid },
                { "displayName", displayName },
                { "playerID", playerID },
                { "colorHex", colorHex },
                { "isAI", isAI },
                { "isHost", isHost },
                { "status", status.ToString().ToLower() },
                { "lastHeartbeat", lastHeartbeat }
            };
        }

        public static GameSessionPlayer FromDictionary(Dictionary<string, object> data, string uid)
        {
            string displayName = data.ContainsKey("displayName") ? data["displayName"] as string : null;
            string playerID = data.ContainsKey("playerID") ? data["playerID"] as string : null;
            string colorHex = data.ContainsKey("colorHex") ? data["colorHex"] as string : null;

            if (displayName == null || playerID == null || colorHex == null)
                return null;

            bool isAI = data.ContainsKey("isAI") && data["isAI"] is bool b1 ? b1 : false;
            bool isHost = data.ContainsKey("isHost") && data["isHost"] is bool b2 ? b2 : false;

            PlayerSessionStatus status = PlayerSessionStatus.Active;
            if (data.ContainsKey("status") && data["status"] is string statusStr)
            {
                if (Enum.TryParse<PlayerSessionStatus>(statusStr, true, out var parsed))
                    status = parsed;
            }

            DateTime lastHeartbeat = DateTime.UtcNow;
            if (data.ContainsKey("lastHeartbeat") && data["lastHeartbeat"] is DateTime dt)
                lastHeartbeat = dt;

            return new GameSessionPlayer(uid, displayName, playerID, colorHex, isAI, isHost, status, lastHeartbeat);
        }
    }

    // ================================================================
    // Game Session
    // ================================================================

    [Serializable]
    public class GameSession
    {
        public string gameID;
        public string hostUID;
        public MapGenerationConfig mapConfig;
        public Dictionary<string, GameSessionPlayer> players;  // keyed by uid
        public GameSessionStatus status;
        public int currentCommandSequence;
        public string latestSnapshotID;   // nullable (null when unset)
        public double currentGameTime;
        public double gameSpeed;
        public string gameVersion;
        public DateTime createdAt;

        public GameSession(
            string gameID,
            string hostUID,
            MapGenerationConfig mapConfig,
            Dictionary<string, GameSessionPlayer> players,
            GameSessionStatus status,
            int currentCommandSequence,
            string latestSnapshotID,
            double currentGameTime,
            double gameSpeed,
            string gameVersion,
            DateTime createdAt)
        {
            this.gameID = gameID;
            this.hostUID = hostUID;
            this.mapConfig = mapConfig;
            this.players = players;
            this.status = status;
            this.currentCommandSequence = currentCommandSequence;
            this.latestSnapshotID = latestSnapshotID;
            this.currentGameTime = currentGameTime;
            this.gameSpeed = gameSpeed;
            this.gameVersion = gameVersion;
            this.createdAt = createdAt;
        }

        public static GameSession Create(
            string hostUID,
            string hostDisplayName,
            Guid hostPlayerID,
            string hostColorHex,
            MapGenerationConfig mapConfig,
            List<(string displayName, Guid playerID, string colorHex)> aiPlayers)
        {
            string gameID = Guid.NewGuid().ToString();

            var players = new Dictionary<string, GameSessionPlayer>();

            // Host player
            players[hostUID] = new GameSessionPlayer(
                uid: hostUID,
                displayName: hostDisplayName,
                playerID: hostPlayerID.ToString(),
                colorHex: hostColorHex,
                isAI: false,
                isHost: true,
                status: PlayerSessionStatus.Active,
                lastHeartbeat: DateTime.UtcNow
            );

            // AI players (keyed by "ai_0", "ai_1", etc.)
            for (int i = 0; i < aiPlayers.Count; i++)
            {
                var ai = aiPlayers[i];
                string aiKey = $"ai_{i}";
                players[aiKey] = new GameSessionPlayer(
                    uid: aiKey,
                    displayName: ai.displayName,
                    playerID: ai.playerID.ToString(),
                    colorHex: ai.colorHex,
                    isAI: true,
                    isHost: false,
                    status: PlayerSessionStatus.Active,
                    lastHeartbeat: DateTime.UtcNow
                );
            }

            return new GameSession(
                gameID: gameID,
                hostUID: hostUID,
                mapConfig: mapConfig,
                players: players,
                status: GameSessionStatus.Lobby,
                currentCommandSequence: 0,
                latestSnapshotID: null,
                currentGameTime: 0.0,
                gameSpeed: 1.0,
                gameVersion: "1.0.0",
                createdAt: DateTime.UtcNow
            );
        }

        public Dictionary<string, object> ToDictionary()
        {
            var playersData = new Dictionary<string, object>();
            foreach (var kvp in players)
            {
                playersData[kvp.Key] = kvp.Value.ToDictionary();
            }

            return new Dictionary<string, object>
            {
                { "hostUID", hostUID },
                { "status", status.ToString().ToLower() },
                { "gameSpeed", gameSpeed },
                { "gameVersion", gameVersion },
                { "mapConfig", mapConfig.ToDictionary() },
                { "players", playersData },
                { "currentCommandSequence", currentCommandSequence },
                { "latestSnapshotID", latestSnapshotID },
                { "currentGameTime", currentGameTime },
                { "createdAt", createdAt }
            };
        }

        public static GameSession FromDictionary(Dictionary<string, object> data, string gameID)
        {
            // Required fields
            if (!data.ContainsKey("hostUID") || !(data["hostUID"] is string hostUID))
                return null;
            if (!data.ContainsKey("status") || !(data["status"] is string statusRaw))
                return null;
            if (!Enum.TryParse<GameSessionStatus>(statusRaw, true, out var status))
                return null;
            if (!data.ContainsKey("mapConfig") || !(data["mapConfig"] is Dictionary<string, object> mapConfigData))
                return null;

            // Parse map config
            MapGenerationConfig mapConfig = MapGenerationConfig.FromDictionary(mapConfigData);

            // Parse players
            var players = new Dictionary<string, GameSessionPlayer>();
            if (data.ContainsKey("players") && data["players"] is Dictionary<string, object> playersData)
            {
                foreach (var kvp in playersData)
                {
                    if (kvp.Value is Dictionary<string, object> playerData)
                    {
                        var player = GameSessionPlayer.FromDictionary(playerData, kvp.Key);
                        if (player != null)
                            players[kvp.Key] = player;
                    }
                }
            }

            // Optional fields with defaults
            int currentCommandSequence = data.ContainsKey("currentCommandSequence")
                ? Convert.ToInt32(data["currentCommandSequence"]) : 0;
            string latestSnapshotID = data.ContainsKey("latestSnapshotID")
                ? data["latestSnapshotID"] as string : null;
            double currentGameTime = data.ContainsKey("currentGameTime")
                ? Convert.ToDouble(data["currentGameTime"]) : 0.0;
            double gameSpeed = data.ContainsKey("gameSpeed")
                ? Convert.ToDouble(data["gameSpeed"]) : 1.0;
            string gameVersion = data.ContainsKey("gameVersion")
                ? data["gameVersion"] as string ?? "1.0.0" : "1.0.0";

            DateTime createdAt = DateTime.UtcNow;
            if (data.ContainsKey("createdAt") && data["createdAt"] is DateTime dt)
                createdAt = dt;

            return new GameSession(
                gameID: gameID,
                hostUID: hostUID,
                mapConfig: mapConfig,
                players: players,
                status: status,
                currentCommandSequence: currentCommandSequence,
                latestSnapshotID: latestSnapshotID,
                currentGameTime: currentGameTime,
                gameSpeed: gameSpeed,
                gameVersion: gameVersion,
                createdAt: createdAt
            );
        }
    }
}
