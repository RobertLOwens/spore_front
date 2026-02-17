// ============================================================================
// FILE: Data/ArenaScenarioConfig.cs
// PURPOSE: Arena scenario configuration, army setup, and preset definitions
//          C# port of GameSetupViewController.swift (lines 10-126)
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    // ================================================================
    // Arena Army Configuration
    // ================================================================

    [Serializable]
    public class ArenaArmyConfiguration
    {
        public Dictionary<MilitaryUnitType, int> playerArmy = new Dictionary<MilitaryUnitType, int>();
        public Dictionary<MilitaryUnitType, int> enemyArmy = new Dictionary<MilitaryUnitType, int>();

        public static ArenaArmyConfiguration Default
        {
            get
            {
                var config = new ArenaArmyConfiguration();
                config.playerArmy[MilitaryUnitType.Swordsman] = 5;
                config.playerArmy[MilitaryUnitType.Archer] = 4;
                config.enemyArmy[MilitaryUnitType.Swordsman] = 5;
                config.enemyArmy[MilitaryUnitType.Archer] = 2;
                return config;
            }
        }

        public static readonly MilitaryUnitType[] AllUnitTypes = (MilitaryUnitType[])Enum.GetValues(typeof(MilitaryUnitType));
        public const int MaxUnitsPerType = 20;
    }

    // ================================================================
    // Arena Scenario Configuration
    // ================================================================

    [Serializable]
    public class ArenaScenarioConfig
    {
        public TerrainType enemyTerrain = TerrainType.Plains;
        public BuildingType? enemyBuilding = null;
        public bool enemyEntrenched = false;
        public int enemyArmyCount = 1;          // 1=single, 2-5=stacked same tile, -2 to -5=adjacent tiles
        public int playerArmyCount = 1;         // 1=single, 2-5=stacked same tile, -2 to -5=adjacent tiles
        public CommanderSpecialty playerCommanderSpecialty = CommanderSpecialty.InfantryAggressive;
        public CommanderSpecialty enemyCommanderSpecialty = CommanderSpecialty.InfantryAggressive;
        public Dictionary<MilitaryUnitType, int> playerUnitTiers = new Dictionary<MilitaryUnitType, int>();  // per-unit: 0=base, 1, 2
        public Dictionary<MilitaryUnitType, int> enemyUnitTiers = new Dictionary<MilitaryUnitType, int>();   // per-unit: 0=base, 1, 2
        public int playerCommanderLevel = 1;     // 1, 5, 10, 15, 20, 25
        public int enemyCommanderLevel = 1;      // 1, 5, 10, 15, 20, 25
        public int garrisonArchers = 0;          // 0-20
        public bool enemyAIEnabled = true;

        public static ArenaScenarioConfig Default => new ArenaScenarioConfig();
    }

    // ================================================================
    // Arena Preset
    // ================================================================

    public enum ArenaPreset
    {
        Custom,
        Plains,
        Hill,
        Mountain,
        Entrenched,
        EntrenchedHill,
        Level2Units,
        InfantryCommander,
        CavalryCommander,
        Tower,
        Fort,
        Castle,
        EntrenchedTower,
        EntrenchedFort,
        EntrenchedCastle,
        Stacked,
        StackedEntrenched,
        OverlapEntrench
    }

    public static class ArenaPresetExtensions
    {
        public static readonly ArenaPreset[] AllValues = (ArenaPreset[])Enum.GetValues(typeof(ArenaPreset));

        public static string DisplayName(this ArenaPreset preset)
        {
            switch (preset)
            {
                case ArenaPreset.Custom: return "Custom";
                case ArenaPreset.Plains: return "Plains";
                case ArenaPreset.Hill: return "Hill";
                case ArenaPreset.Mountain: return "Mountain";
                case ArenaPreset.Entrenched: return "Entrenched";
                case ArenaPreset.EntrenchedHill: return "Entrenched Hill";
                case ArenaPreset.Level2Units: return "Level 2 Units";
                case ArenaPreset.InfantryCommander: return "Infantry Cmdr";
                case ArenaPreset.CavalryCommander: return "Cavalry Cmdr";
                case ArenaPreset.Tower: return "Tower";
                case ArenaPreset.Fort: return "Fort";
                case ArenaPreset.Castle: return "Castle";
                case ArenaPreset.EntrenchedTower: return "Entrenched Tower";
                case ArenaPreset.EntrenchedFort: return "Entrenched Fort";
                case ArenaPreset.EntrenchedCastle: return "Entrenched Castle";
                case ArenaPreset.Stacked: return "Stacked";
                case ArenaPreset.StackedEntrenched: return "Stacked Entrenched";
                case ArenaPreset.OverlapEntrench: return "Overlap Entrench";
                default: return preset.ToString();
            }
        }

        public static ArenaScenarioConfig ToConfig(this ArenaPreset preset)
        {
            var c = new ArenaScenarioConfig();

            switch (preset)
            {
                case ArenaPreset.Custom:
                case ArenaPreset.Plains:
                    break; // all defaults

                case ArenaPreset.Hill:
                    c.enemyTerrain = TerrainType.Hill;
                    break;

                case ArenaPreset.Mountain:
                    c.enemyTerrain = TerrainType.Mountain;
                    break;

                case ArenaPreset.Entrenched:
                    c.enemyEntrenched = true;
                    break;

                case ArenaPreset.EntrenchedHill:
                    c.enemyTerrain = TerrainType.Hill;
                    c.enemyEntrenched = true;
                    break;

                case ArenaPreset.Level2Units:
                    foreach (var unitType in ArenaArmyConfiguration.AllUnitTypes)
                        c.enemyUnitTiers[unitType] = 2;
                    break;

                case ArenaPreset.InfantryCommander:
                    c.enemyCommanderSpecialty = CommanderSpecialty.InfantryAggressive;
                    break;

                case ArenaPreset.CavalryCommander:
                    c.enemyCommanderSpecialty = CommanderSpecialty.CavalryAggressive;
                    break;

                case ArenaPreset.Tower:
                    c.enemyBuilding = BuildingType.Tower;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.Fort:
                    c.enemyBuilding = BuildingType.WoodenFort;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.Castle:
                    c.enemyBuilding = BuildingType.Castle;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.EntrenchedTower:
                    c.enemyBuilding = BuildingType.Tower;
                    c.enemyEntrenched = true;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.EntrenchedFort:
                    c.enemyBuilding = BuildingType.WoodenFort;
                    c.enemyEntrenched = true;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.EntrenchedCastle:
                    c.enemyBuilding = BuildingType.Castle;
                    c.enemyEntrenched = true;
                    c.garrisonArchers = 5;
                    break;

                case ArenaPreset.Stacked:
                    c.enemyArmyCount = 2;
                    c.playerArmyCount = 2;
                    break;

                case ArenaPreset.StackedEntrenched:
                    c.enemyArmyCount = 2;
                    c.playerArmyCount = 2;
                    c.enemyEntrenched = true;
                    break;

                case ArenaPreset.OverlapEntrench:
                    c.enemyArmyCount = -2;
                    c.playerArmyCount = -2;
                    c.enemyEntrenched = true;
                    break;
            }

            return c;
        }
    }
}
