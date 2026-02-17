// ============================================================================
// FILE: Data/GenomeLibrary.cs
// PURPOSE: Persistent library for storing and managing multiple AI genomes
//          C# port of GenomeLibrary.swift (139 lines)
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sporefront.Engine;

namespace Sporefront.Data
{
    /// <summary>
    /// File-based persistent library for AI genomes.
    /// Stores genomes as individual JSON files in a "genomes" subdirectory
    /// of the application's persistent data path.
    /// </summary>
    public class GenomeLibrary
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static GenomeLibrary _instance;
        public static GenomeLibrary Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GenomeLibrary();
                return _instance;
            }
        }

        // ================================================================
        // Properties
        // ================================================================

        private string genomesDirectoryPath
        {
            get { return Path.Combine(Application.persistentDataPath, "genomes"); }
        }

        // ================================================================
        // Init
        // ================================================================

        private GenomeLibrary()
        {
            EnsureDirectoryExists();
            MigrateLegacyGenomes();
        }

        // ================================================================
        // Directory Management
        // ================================================================

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(genomesDirectoryPath))
            {
                Directory.CreateDirectory(genomesDirectoryPath);
            }
        }

        // ================================================================
        // CRUD Operations
        // ================================================================

        /// <summary>
        /// Save a genome to the library. Auto-names if name is empty.
        /// </summary>
        public void Save(AIGenome genome)
        {
            EnsureDirectoryExists();

            if (string.IsNullOrEmpty(genome.name))
            {
                genome.name = AutoName(genome);
            }
            genome.savedDate = DateTime.UtcNow.ToString("o");

            string filePath = Path.Combine(genomesDirectoryPath, $"{genome.id}.json");

            try
            {
                string json = JsonUtility.ToJson(genome, true);
                File.WriteAllText(filePath, json);
                DebugLog.Log($"GenomeLibrary: Saved genome '{genome.name}' (gen {genome.generation})");
            }
            catch (Exception e)
            {
                DebugLog.Log($"GenomeLibrary: Failed to save genome: {e.Message}");
            }
        }

        /// <summary>
        /// Load a single genome by its ID string.
        /// </summary>
        public AIGenome Load(string id)
        {
            string filePath = Path.Combine(genomesDirectoryPath, $"{id}.json");
            if (!File.Exists(filePath)) return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<AIGenome>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// List all genomes sorted by savedDate descending.
        /// </summary>
        public List<AIGenome> ListAll()
        {
            var genomes = new List<AIGenome>();
            if (!Directory.Exists(genomesDirectoryPath)) return genomes;

            string[] files;
            try
            {
                files = Directory.GetFiles(genomesDirectoryPath, "*.json");
            }
            catch (Exception)
            {
                return genomes;
            }

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var genome = JsonUtility.FromJson<AIGenome>(json);
                    if (genome != null)
                        genomes.Add(genome);
                }
                catch (Exception)
                {
                    // Skip corrupt files
                }
            }

            // Sort by savedDate descending (ISO 8601 strings sort lexicographically)
            genomes.Sort((a, b) => string.Compare(b.savedDate, a.savedDate, StringComparison.Ordinal));
            return genomes;
        }

        /// <summary>
        /// Delete a genome by its ID string.
        /// </summary>
        public void Delete(string id)
        {
            string filePath = Path.Combine(genomesDirectoryPath, $"{id}.json");
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    DebugLog.Log($"GenomeLibrary: Deleted genome {id}");
                }
            }
            catch (Exception e)
            {
                DebugLog.Log($"GenomeLibrary: Failed to delete genome: {e.Message}");
            }
        }

        /// <summary>
        /// Rename a genome in-place.
        /// </summary>
        public void Rename(string id, string newName)
        {
            var genome = Load(id);
            if (genome == null) return;

            genome.name = newName;

            string filePath = Path.Combine(genomesDirectoryPath, $"{id}.json");
            try
            {
                string json = JsonUtility.ToJson(genome, true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                DebugLog.Log($"GenomeLibrary: Failed to rename genome: {e.Message}");
            }
        }

        /// <summary>
        /// Generate an automatic name for a genome.
        /// </summary>
        public string AutoName(AIGenome genome)
        {
            string mapName = char.ToUpper(genome.mapType[0]) + genome.mapType.Substring(1);
            return $"{mapName} Gen {genome.generation} - {genome.WinRatePercent}% WR";
        }

        // ================================================================
        // Legacy Migration
        // ================================================================

        /// <summary>
        /// Copy existing best_genome_*.json files into the library on first run.
        /// </summary>
        private void MigrateLegacyGenomes()
        {
            string migrationFlagPath = Path.Combine(genomesDirectoryPath, ".migrated");
            if (File.Exists(migrationFlagPath)) return;

            try
            {
                string documentsPath = Application.persistentDataPath;
                string[] files = Directory.GetFiles(documentsPath, "best_genome_*.json");

                foreach (string file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var genome = JsonUtility.FromJson<AIGenome>(json);
                        if (genome != null)
                        {
                            if (string.IsNullOrEmpty(genome.name))
                            {
                                genome.name = AutoName(genome);
                            }
                            Save(genome);
                            DebugLog.Log($"GenomeLibrary: Migrated legacy genome from {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception)
                    {
                        // Skip corrupt legacy files
                    }
                }

                // Mark migration complete
                File.WriteAllText(migrationFlagPath, "");
            }
            catch (Exception e)
            {
                DebugLog.Log($"GenomeLibrary: Migration failed: {e.Message}");
            }
        }
    }
}
