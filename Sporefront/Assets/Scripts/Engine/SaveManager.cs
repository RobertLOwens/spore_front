using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Data.Serialization;
using Sporefront.Models;

namespace Sporefront.Engine
{
    /// <summary>
    /// Manages game save/load operations using Newtonsoft JSON.
    /// Saves are stored as JSON files in Application.persistentDataPath/saves/.
    /// </summary>
    public static class SaveManager
    {
        private const int CurrentVersion = 1;
        private const string SaveDirectory = "saves";
        private const string AutoSaveName = "autosave";

        private static JsonSerializerSettings serializerSettings;

        private static JsonSerializerSettings GetSettings()
        {
            if (serializerSettings == null)
            {
                serializerSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.None,
                    Converters = new List<JsonConverter>
                    {
                        new HexCoordinateConverter()
                    }
                };
            }
            return serializerSettings;
        }

        private static string GetSavesPath()
        {
            return Path.Combine(Application.persistentDataPath, SaveDirectory);
        }

        private static void EnsureSavesDirectory()
        {
            string path = GetSavesPath();
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // ================================================================
        // Save
        // ================================================================

        public static bool Save(GameState gameState, string saveName)
        {
            try
            {
                EnsureSavesDirectory();

                string saveID = Guid.NewGuid().ToString("N").Substring(0, 12);
                string now = DateTime.UtcNow.ToString("o");

                var saveData = new SaveData
                {
                    saveID = saveID,
                    saveName = saveName,
                    createdAt = now,
                    modifiedAt = now,
                    version = CurrentVersion,
                    snapshot = new GameStateSnapshot(gameState),
                    tiles = SerializeTiles(gameState),
                    resourcePointPositions = SerializeResourcePointPositions(gameState),
                    gameSpeed = gameState.gameSpeed,
                    visibilityMode = gameState.visibilityMode
                };

                string json = JsonConvert.SerializeObject(saveData, GetSettings());
                string filePath = Path.Combine(GetSavesPath(), $"{saveID}.json");
                File.WriteAllText(filePath, json);

                DebugLog.Log($"SaveManager: Saved game '{saveName}' to {filePath}");
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Log($"SaveManager: Failed to save game: {e.Message}");
                return false;
            }
        }

        public static bool AutoSave(GameState gameState)
        {
            try
            {
                EnsureSavesDirectory();

                // Overwrite existing autosave
                string now = DateTime.UtcNow.ToString("o");

                var saveData = new SaveData
                {
                    saveID = AutoSaveName,
                    saveName = "Auto Save",
                    createdAt = now,
                    modifiedAt = now,
                    version = CurrentVersion,
                    snapshot = new GameStateSnapshot(gameState),
                    tiles = SerializeTiles(gameState),
                    resourcePointPositions = SerializeResourcePointPositions(gameState),
                    gameSpeed = gameState.gameSpeed,
                    visibilityMode = gameState.visibilityMode
                };

                string json = JsonConvert.SerializeObject(saveData, GetSettings());
                string filePath = Path.Combine(GetSavesPath(), $"{AutoSaveName}.json");
                File.WriteAllText(filePath, json);

                DebugLog.Log("SaveManager: Auto-saved game");
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Log($"SaveManager: Failed to auto-save: {e.Message}");
                return false;
            }
        }

        // ================================================================
        // Load
        // ================================================================

        public static GameState Load(string saveID)
        {
            try
            {
                string filePath = Path.Combine(GetSavesPath(), $"{saveID}.json");
                if (!File.Exists(filePath))
                {
                    DebugLog.Log($"SaveManager: Save file not found: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                var saveData = JsonConvert.DeserializeObject<SaveData>(json, GetSettings());
                if (saveData == null || saveData.snapshot == null)
                {
                    DebugLog.Log("SaveManager: Corrupt save data");
                    return null;
                }

                // Restore entities from snapshot
                var gameState = saveData.snapshot.Restore();

                // Restore tile data (not included in snapshot)
                if (saveData.tiles != null)
                {
                    foreach (var tile in saveData.tiles)
                    {
                        var coord = new HexCoordinate(tile.q, tile.r);
                        gameState.mapData.SetTile(new TileData(coord, tile.terrain, tile.elevation));
                    }
                }

                // Restore resource point positions
                if (saveData.resourcePointPositions != null)
                {
                    foreach (var rpp in saveData.resourcePointPositions)
                    {
                        var coord = new HexCoordinate(rpp.q, rpp.r);
                        if (!gameState.mapData.resourcePointCoordinates.ContainsKey(rpp.id))
                            gameState.mapData.resourcePointCoordinates[rpp.id] = coord;
                    }
                }

                // Restore game settings
                gameState.gameSpeed = saveData.gameSpeed;
                gameState.visibilityMode = saveData.visibilityMode;

                DebugLog.Log($"SaveManager: Loaded game '{saveData.saveName}'");
                return gameState;
            }
            catch (Exception e)
            {
                DebugLog.Log($"SaveManager: Failed to load save: {e.Message}");
                return null;
            }
        }

        // ================================================================
        // List & Delete
        // ================================================================

        public static List<SaveSlotInfo> ListSaves()
        {
            var saves = new List<SaveSlotInfo>();
            string savesPath = GetSavesPath();

            if (!Directory.Exists(savesPath))
                return saves;

            try
            {
                string[] files = Directory.GetFiles(savesPath, "*.json");
                foreach (string file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var saveData = JsonConvert.DeserializeObject<SaveData>(json, GetSettings());
                        if (saveData != null)
                        {
                            saves.Add(new SaveSlotInfo
                            {
                                saveID = saveData.saveID,
                                saveName = saveData.saveName,
                                modifiedAt = saveData.modifiedAt,
                                mapWidth = saveData.snapshot?.mapWidth ?? 0,
                                mapHeight = saveData.snapshot?.mapHeight ?? 0
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        DebugLog.Log($"SaveManager: Skipping corrupt save file {file}: {e.Message}");
                    }
                }

                // Sort by modification date descending
                saves.Sort((a, b) => string.Compare(b.modifiedAt, a.modifiedAt, StringComparison.Ordinal));
            }
            catch (Exception e)
            {
                DebugLog.Log($"SaveManager: Failed to list saves: {e.Message}");
            }

            return saves;
        }

        public static bool Delete(string saveID)
        {
            try
            {
                string filePath = Path.Combine(GetSavesPath(), $"{saveID}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    DebugLog.Log($"SaveManager: Deleted save {saveID}");
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                DebugLog.Log($"SaveManager: Failed to delete save: {e.Message}");
                return false;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static List<SerializedTileData> SerializeTiles(GameState gameState)
        {
            var tiles = new List<SerializedTileData>();
            foreach (var kvp in gameState.mapData.tiles)
            {
                tiles.Add(new SerializedTileData(kvp.Key, kvp.Value));
            }
            return tiles;
        }

        private static List<SerializedResourcePoint> SerializeResourcePointPositions(GameState gameState)
        {
            var positions = new List<SerializedResourcePoint>();
            foreach (var kvp in gameState.mapData.resourcePointCoordinates)
            {
                positions.Add(new SerializedResourcePoint(kvp.Key, kvp.Value));
            }
            return positions;
        }
    }
}
