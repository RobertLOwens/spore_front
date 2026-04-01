// ============================================================================
// FILE: AI/FactionAIConfig.cs
// PURPOSE: Centralized per-faction AI tuning data. Adding a new faction
//          requires one entry here instead of editing 4+ planner files.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.AI
{
    public class FactionAIConfig
    {
        // ================================================================
        // Research Scoring
        // ================================================================

        /// <summary>Per-research score bonuses (faction synergy).</summary>
        public Dictionary<ResearchType, double> ResearchBonuses = new Dictionary<ResearchType, double>();

        /// <summary>
        /// Substring penalties applied to research names (e.g. "Cavalry" → -5.0).
        /// Used to deprioritize T3-blocked categories.
        /// </summary>
        public List<(string pattern, double penalty)> ResearchNamePenalties = new List<(string, double)>();

        // ================================================================
        // Unit Composition
        // ================================================================

        /// <summary>Score adjustments per unit category for army composition decisions.</summary>
        public Dictionary<UnitCategory, double> UnitCategoryBias = new Dictionary<UnitCategory, double>();

        /// <summary>Score adjustment for specific unit types (e.g. Scout bonus for Morel).</summary>
        public Dictionary<MilitaryUnitType, double> UnitTypeBias = new Dictionary<MilitaryUnitType, double>();

        /// <summary>Bonus score when selecting army targets (e.g. Muscaria poison aggression).</summary>
        public double ArmyTargetBonus = 0.0;

        // ================================================================
        // Economy
        // ================================================================

        /// <summary>Multiplier applied to resource urgency (e.g. Morel 1.10 for Wood).</summary>
        public Dictionary<ResourceType, double> ResourceUrgencyMultiplier = new Dictionary<ResourceType, double>();

        /// <summary>Ordered military building priorities: (type, minExisting, maxAllowed).</summary>
        public (BuildingType type, int minCount, int maxCount)[] MilitaryBuildOrder;

        /// <summary>If true, prefer mountain/hill tiles when placing buildings (Muscaria).</summary>
        public bool PreferHighlandBuilding = false;

        // ================================================================
        // Defense & Terrain
        // ================================================================

        /// <summary>Score bonus for tower placement on specific terrain types.</summary>
        public Dictionary<TerrainType, double> TowerTerrainBonus = new Dictionary<TerrainType, double>();

        /// <summary>If true, towers near forest resource points get a bonus (Morel camouflage synergy).</summary>
        public bool TowerForestBonus = false;

        /// <summary>Score bonus for entrenchment on specific terrain types.</summary>
        public Dictionary<TerrainType, double> EntrenchTerrainBonus = new Dictionary<TerrainType, double>();

        /// <summary>If true, entrenchment near forest resource points gets a bonus.</summary>
        public double EntrenchForestBonus = 0.0;

        // ================================================================
        // Static Configs
        // ================================================================

        private static readonly Dictionary<FactionType, FactionAIConfig> Configs =
            new Dictionary<FactionType, FactionAIConfig>
            {
                { FactionType.Morel, CreateMorel() },
                { FactionType.Muscaria, CreateMuscaria() },
            };

        private static readonly FactionAIConfig DefaultConfig = new FactionAIConfig
        {
            MilitaryBuildOrder = new[]
            {
                (BuildingType.Barracks, 0, 1),
                (BuildingType.ArcheryRange, 0, 1),
                (BuildingType.Stable, 0, 1),
                (BuildingType.SiegeWorkshop, 0, 1),
                (BuildingType.Barracks, 1, 2),
            }
        };

        public static FactionAIConfig Get(FactionType faction)
        {
            FactionAIConfig config;
            return Configs.TryGetValue(faction, out config) ? config : DefaultConfig;
        }

        // ================================================================
        // Morel Configuration
        // ================================================================

        private static FactionAIConfig CreateMorel()
        {
            return new FactionAIConfig
            {
                // Research synergies
                ResearchBonuses = new Dictionary<ResearchType, double>
                {
                    { ResearchType.LumberCampGatheringI, 12.0 },
                    { ResearchType.LumberCampGatheringII, 10.0 },
                    { ResearchType.BurnAreas, 15.0 },
                    { ResearchType.ToxicSpores, 12.0 },
                    { ResearchType.LethalSpores, 10.0 },
                    { ResearchType.InfantryMeleeAttackI, 8.0 },
                    { ResearchType.InfantryMeleeAttackII, 8.0 },
                    { ResearchType.InfantryMeleeAttackIII, 8.0 },
                    { ResearchType.InfantryMeleeArmorI, 6.0 },
                    { ResearchType.InfantryMeleeArmorII, 6.0 },
                    { ResearchType.InfantryMeleeArmorIII, 6.0 },
                    { ResearchType.MarchSpeedI, 5.0 },
                    { ResearchType.MarchSpeedII, 5.0 },
                    { ResearchType.MarchSpeedIII, 5.0 },
                },
                ResearchNamePenalties = new List<(string, double)>
                {
                    ("Cavalry", -5.0),
                    ("Siege", -5.0),
                },

                // Infantry focus
                UnitCategoryBias = new Dictionary<UnitCategory, double>
                {
                    { UnitCategory.Infantry, 6.0 },
                    { UnitCategory.Cavalry, -5.0 },
                    { UnitCategory.Siege, -5.0 },
                },
                UnitTypeBias = new Dictionary<MilitaryUnitType, double>
                {
                    { MilitaryUnitType.Scout, 5.0 },
                },

                // Economy
                ResourceUrgencyMultiplier = new Dictionary<ResourceType, double>
                {
                    { ResourceType.Wood, 1.10 },
                },
                MilitaryBuildOrder = new[]
                {
                    (BuildingType.Barracks, 0, 1),
                    (BuildingType.ArcheryRange, 0, 1),
                    (BuildingType.Barracks, 1, 2),
                    (BuildingType.Stable, 0, 1),
                    (BuildingType.SiegeWorkshop, 0, 1),
                },

                // Defense: forest preference
                TowerTerrainBonus = new Dictionary<TerrainType, double>
                {
                    { TerrainType.Mountain, 2.0 },
                },
                TowerForestBonus = true,
                EntrenchForestBonus = 3.0,
            };
        }

        // ================================================================
        // Muscaria Configuration
        // ================================================================

        private static FactionAIConfig CreateMuscaria()
        {
            return new FactionAIConfig
            {
                // Research synergies
                ResearchBonuses = new Dictionary<ResearchType, double>
                {
                    { ResearchType.IncreasedPoisonDamage, 20.0 },
                    { ResearchType.ToxinAccumulation, 18.0 },
                    { ResearchType.SporeBurst, 15.0 },
                    { ResearchType.MiningCampGatheringI, 12.0 },
                    { ResearchType.MiningCampGatheringII, 10.0 },
                    { ResearchType.PiercingDamageI, 8.0 },
                    { ResearchType.PiercingDamageII, 8.0 },
                    { ResearchType.PiercingDamageIII, 8.0 },
                    { ResearchType.SiegeBludgeonDamageI, 8.0 },
                    { ResearchType.SiegeBludgeonDamageII, 8.0 },
                    { ResearchType.SiegeBludgeonDamageIII, 8.0 },
                },
                ResearchNamePenalties = new List<(string, double)>
                {
                    ("InfantryMelee", -5.0),
                    ("Cavalry", -5.0),
                },

                // Ranged/siege focus + poison aggression
                UnitCategoryBias = new Dictionary<UnitCategory, double>
                {
                    { UnitCategory.Ranged, 6.0 },
                    { UnitCategory.Siege, 6.0 },
                    { UnitCategory.Infantry, -4.0 },
                    { UnitCategory.Cavalry, -4.0 },
                },
                ArmyTargetBonus = 8.0,

                // Economy
                ResourceUrgencyMultiplier = new Dictionary<ResourceType, double>
                {
                    { ResourceType.Stone, 1.10 },
                    { ResourceType.Ore, 1.10 },
                },
                PreferHighlandBuilding = true,
                MilitaryBuildOrder = new[]
                {
                    (BuildingType.Barracks, 0, 1),
                    (BuildingType.ArcheryRange, 0, 1),
                    (BuildingType.SiegeWorkshop, 0, 1),
                    (BuildingType.Stable, 0, 1),
                    (BuildingType.Barracks, 1, 2),
                },

                // Defense: mountain/hill preference
                TowerTerrainBonus = new Dictionary<TerrainType, double>
                {
                    { TerrainType.Mountain, 2.0 },
                    { TerrainType.Hill, 1.5 },
                },
                EntrenchTerrainBonus = new Dictionary<TerrainType, double>
                {
                    { TerrainType.Mountain, 3.0 },
                    { TerrainType.Hill, 2.0 },
                },
            };
        }
    }
}
