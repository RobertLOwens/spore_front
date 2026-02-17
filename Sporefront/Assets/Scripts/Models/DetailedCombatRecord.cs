using System;
using System.Collections.Generic;
using System.Linq;

namespace Sporefront.Models
{
    [System.Serializable]
    public class CombatPhaseRecord
    {
        public int Phase; // 0=Ranged, 1=Charge, 2=Melee
        public double Duration;
        public double AttackerDamageDealt;
        public double DefenderDamageDealt;
        public Dictionary<string, int> AttackerCasualtiesByType;
        public Dictionary<string, int> DefenderCasualtiesByType;

        public CombatPhaseRecord(
            int phase,
            double duration,
            double attackerDamageDealt,
            double defenderDamageDealt,
            Dictionary<MilitaryUnitType, int> attackerCasualties,
            Dictionary<MilitaryUnitType, int> defenderCasualties)
        {
            Phase = phase;
            Duration = duration;
            AttackerDamageDealt = attackerDamageDealt;
            DefenderDamageDealt = defenderDamageDealt;

            AttackerCasualtiesByType = new Dictionary<string, int>();
            foreach (var kvp in attackerCasualties)
                AttackerCasualtiesByType[kvp.Key.ToString()] = kvp.Value;

            DefenderCasualtiesByType = new Dictionary<string, int>();
            foreach (var kvp in defenderCasualties)
                DefenderCasualtiesByType[kvp.Key.ToString()] = kvp.Value;
        }
    }

    [System.Serializable]
    public class UnitCombatBreakdown
    {
        public string UnitTypeRaw;
        public int InitialCount;
        public int FinalCount;
        public int Casualties;
        public double DamageDealt;
        public double DamageReceived;

        public UnitCombatBreakdown(
            MilitaryUnitType unitType,
            int initialCount,
            int finalCount,
            int casualties,
            double damageDealt,
            double damageReceived)
        {
            UnitTypeRaw = unitType.ToString();
            InitialCount = initialCount;
            FinalCount = finalCount;
            Casualties = casualties;
            DamageDealt = damageDealt;
            DamageReceived = damageReceived;
        }
    }

    [System.Serializable]
    public class ArmyCombatBreakdown
    {
        public string ArmyID;
        public string ArmyName;
        public string OwnerName;
        public string CommanderName;
        public double JoinTime;
        public bool WasReinforcement;
        public Dictionary<string, int> InitialComposition;
        public Dictionary<string, int> FinalComposition;
        public Dictionary<string, int> CasualtiesByType;
        public Dictionary<string, double> DamageDealtByType;
        public double TotalDamageDealt;
        public int TotalCasualties;

        public ArmyCombatBreakdown(
            string armyID,
            string armyName,
            string ownerName,
            string commanderName,
            double joinTime,
            bool wasReinforcement,
            Dictionary<MilitaryUnitType, int> initialComposition,
            Dictionary<MilitaryUnitType, int> finalComposition,
            Dictionary<MilitaryUnitType, int> casualtiesByType,
            Dictionary<MilitaryUnitType, double> damageDealtByType)
        {
            ArmyID = armyID;
            ArmyName = armyName;
            OwnerName = ownerName;
            CommanderName = commanderName;
            JoinTime = joinTime;
            WasReinforcement = wasReinforcement;

            InitialComposition = new Dictionary<string, int>();
            foreach (var kvp in initialComposition) InitialComposition[kvp.Key.ToString()] = kvp.Value;

            FinalComposition = new Dictionary<string, int>();
            foreach (var kvp in finalComposition) FinalComposition[kvp.Key.ToString()] = kvp.Value;

            CasualtiesByType = new Dictionary<string, int>();
            foreach (var kvp in casualtiesByType) CasualtiesByType[kvp.Key.ToString()] = kvp.Value;

            DamageDealtByType = new Dictionary<string, double>();
            foreach (var kvp in damageDealtByType) DamageDealtByType[kvp.Key.ToString()] = kvp.Value;

            TotalDamageDealt = damageDealtByType.Values.Sum();
            TotalCasualties = casualtiesByType.Values.Sum();
        }
    }

    [System.Serializable]
    public class DetailedCombatRecord
    {
        public string Id;
        public double Timestamp;
        public HexCoordinate Location;
        public double TotalDuration;
        public CombatResult Winner;

        // Terrain
        public string TerrainType;
        public double TerrainDefenseBonus;
        public double TerrainAttackPenalty;
        public double EntrenchmentDefenseBonus;

        // Attacker
        public string AttackerName;
        public string AttackerOwner;
        public string AttackerCommander;
        public string AttackerCommanderSpecialty;
        public Dictionary<string, int> AttackerInitialComposition;
        public Dictionary<string, int> AttackerFinalComposition;

        // Defender
        public string DefenderName;
        public string DefenderOwner;
        public string DefenderCommander;
        public string DefenderCommanderSpecialty;
        public Dictionary<string, int> DefenderInitialComposition;
        public Dictionary<string, int> DefenderFinalComposition;

        // Breakdowns
        public List<CombatPhaseRecord> PhaseRecords;
        public List<UnitCombatBreakdown> AttackerUnitBreakdowns;
        public List<UnitCombatBreakdown> DefenderUnitBreakdowns;
        public List<ArmyCombatBreakdown> AttackerArmyBreakdowns;
        public List<ArmyCombatBreakdown> DefenderArmyBreakdowns;

        // Computed
        public bool IsMultiArmyBattle => (AttackerArmyBreakdowns?.Count ?? 0) > 1 || (DefenderArmyBreakdowns?.Count ?? 0) > 1;
        public int AttackerTotalCasualties => AttackerUnitBreakdowns?.Sum(b => b.Casualties) ?? 0;
        public int DefenderTotalCasualties => DefenderUnitBreakdowns?.Sum(b => b.Casualties) ?? 0;
        public double AttackerTotalDamageDealt => AttackerUnitBreakdowns?.Sum(b => b.DamageDealt) ?? 0;
        public double DefenderTotalDamageDealt => DefenderUnitBreakdowns?.Sum(b => b.DamageDealt) ?? 0;
        public int AttackerInitialStrength => AttackerInitialComposition?.Values.Sum() ?? 0;
        public int DefenderInitialStrength => DefenderInitialComposition?.Values.Sum() ?? 0;
        public int AttackerFinalStrength => AttackerFinalComposition?.Values.Sum() ?? 0;
        public int DefenderFinalStrength => DefenderFinalComposition?.Values.Sum() ?? 0;

        public bool HasTerrainModifiers => TerrainDefenseBonus != 0 || TerrainAttackPenalty != 0 || EntrenchmentDefenseBonus != 0;

        public string GetSummary()
        {
            string winnerName = Winner == CombatResult.AttackerVictory ? AttackerName : DefenderName;
            return $"{AttackerName} vs {DefenderName} - {winnerName} Victory";
        }

        public string GetFormattedDuration()
        {
            int minutes = (int)TotalDuration / 60;
            int seconds = (int)TotalDuration % 60;
            return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
        }
    }
}
