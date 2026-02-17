using System;
using UnityEngine;

namespace Sporefront.Models
{
    public enum CombatParticipantType
    {
        Army,
        Building,
        VillagerGroup
    }

    public enum CombatResult
    {
        AttackerVictory,
        DefenderVictory,
        Draw
    }

    public static class CombatParticipantTypeExtensions
    {
        public static string Icon(this CombatParticipantType type)
        {
            switch (type)
            {
                case CombatParticipantType.Army: return "army";
                case CombatParticipantType.Building: return "building";
                case CombatParticipantType.VillagerGroup: return "villager";
                default: return "";
            }
        }
    }

    public static class CombatResultExtensions
    {
        public static string DisplayName(this CombatResult result)
        {
            switch (result)
            {
                case CombatResult.AttackerVictory: return "Attacker Victory";
                case CombatResult.DefenderVictory: return "Defender Victory";
                case CombatResult.Draw: return "Draw";
                default: return "";
            }
        }
    }

    [System.Serializable]
    public struct CombatParticipant
    {
        public string Name;
        public CombatParticipantType Type;
        public string OwnerName;
        public Color OwnerColor;
        public string CommanderName;

        public CombatParticipant(string name, CombatParticipantType type, string ownerName, Color ownerColor, string commanderName = null)
        {
            Name = name;
            Type = type;
            OwnerName = ownerName;
            OwnerColor = ownerColor;
            CommanderName = commanderName;
        }
    }

    [System.Serializable]
    public class CombatRecord
    {
        public string Id;
        public double Timestamp;

        // Participants
        public CombatParticipant Attacker;
        public CombatParticipant Defender;

        // Combat stats
        public double AttackerInitialStrength;
        public double DefenderInitialStrength;
        public double AttackerFinalStrength;
        public double DefenderFinalStrength;

        // Results
        public CombatResult Winner;
        public int AttackerCasualties;
        public int DefenderCasualties;
        public double Duration;

        // Location
        public HexCoordinate Location;

        public CombatRecord(
            CombatParticipant attacker,
            CombatParticipant defender,
            double attackerInitialStrength,
            double defenderInitialStrength,
            double attackerFinalStrength,
            double defenderFinalStrength,
            CombatResult winner,
            int attackerCasualties,
            int defenderCasualties,
            HexCoordinate location,
            double duration = 5.0)
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            Attacker = attacker;
            Defender = defender;
            AttackerInitialStrength = attackerInitialStrength;
            DefenderInitialStrength = defenderInitialStrength;
            AttackerFinalStrength = attackerFinalStrength;
            DefenderFinalStrength = defenderFinalStrength;
            Winner = winner;
            AttackerCasualties = attackerCasualties;
            DefenderCasualties = defenderCasualties;
            Location = location;
            Duration = duration;
        }

        public string GetSummary()
        {
            string winnerName = Winner == CombatResult.AttackerVictory ? Attacker.Name : Defender.Name;
            return $"{Attacker.Name} vs {Defender.Name} - {winnerName} Victory";
        }
    }
}
