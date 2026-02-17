using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    // Base class for all state changes
    [System.Serializable]
    public abstract class StateChange { }

    // Building Changes
    public class BuildingPlacedChange : StateChange
    {
        public Guid buildingID; public string buildingType; public HexCoordinate coordinate; public Guid ownerID; public int rotation;
    }
    public class BuildingConstructionStartedChange : StateChange { public Guid buildingID; }
    public class BuildingConstructionProgressChange : StateChange { public Guid buildingID; public double progress; }
    public class BuildingCompletedChange : StateChange { public Guid buildingID; }
    public class BuildingUpgradeStartedChange : StateChange { public Guid buildingID; public int toLevel; }
    public class BuildingUpgradeProgressChange : StateChange { public Guid buildingID; public double progress; }
    public class BuildingUpgradeCompletedChange : StateChange { public Guid buildingID; public int newLevel; }
    public class BuildingDemolitionStartedChange : StateChange { public Guid buildingID; }
    public class BuildingDemolitionProgressChange : StateChange { public Guid buildingID; public double progress; }
    public class BuildingDemolishedChange : StateChange { public Guid buildingID; public HexCoordinate coordinate; }
    public class BuildingDemolitionCancelledChange : StateChange { public Guid buildingID; }
    public class BuildingDamagedChange : StateChange { public Guid buildingID; public double currentHealth; public double maxHealth; }
    public class BuildingRepairedChange : StateChange { public Guid buildingID; public double currentHealth; }
    public class BuildingDestroyedChange : StateChange { public Guid buildingID; public HexCoordinate coordinate; }

    // Army Changes
    public class ArmyCreatedChange : StateChange
    {
        public Guid armyID; public Guid ownerID; public HexCoordinate coordinate; public Dictionary<string, int> composition;
    }
    public class ArmyMovedChange : StateChange
    {
        public Guid armyID; public HexCoordinate from; public HexCoordinate to; public List<HexCoordinate> path;
    }
    public class ArmyCompositionChangedChange : StateChange { public Guid armyID; public Dictionary<string, int> newComposition; }
    public class ArmyDestroyedChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }
    public class ArmyMergedChange : StateChange { public Guid sourceArmyID; public Guid targetArmyID; }
    public class ArmyRetreatingChange : StateChange { public Guid armyID; public HexCoordinate to; }
    public class ArmyAutoRetreatingChange : StateChange { public Guid armyID; public List<HexCoordinate> path; }
    public class ArmyEntrenchmentStartedChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }
    public class ArmyEntrenchmentProgressChange : StateChange { public Guid armyID; public double progress; }
    public class ArmyEntrenchedChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }
    public class ArmyEntrenchmentCancelledChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }

    // Villager Group Changes
    public class VillagerGroupCreatedChange : StateChange
    {
        public Guid groupID; public Guid ownerID; public HexCoordinate coordinate; public int count;
    }
    public class VillagerGroupMovedChange : StateChange
    {
        public Guid groupID; public HexCoordinate from; public HexCoordinate to; public List<HexCoordinate> path;
    }
    public class VillagerGroupCountChangedChange : StateChange { public Guid groupID; public int newCount; }
    public class VillagerGroupDestroyedChange : StateChange { public Guid groupID; public HexCoordinate coordinate; }
    public class VillagerGroupTaskChangedChange : StateChange
    {
        public Guid groupID; public string task; public HexCoordinate? targetCoordinate;
    }

    // Training Changes
    public class TrainingStartedChange : StateChange
    {
        public Guid buildingID; public string unitType; public int quantity; public double startTime;
    }
    public class TrainingProgressChange : StateChange { public Guid buildingID; public int entryIndex; public double progress; }
    public class TrainingCompletedChange : StateChange { public Guid buildingID; public string unitType; public int quantity; }
    public class VillagerTrainingStartedChange : StateChange
    {
        public Guid buildingID; public int quantity; public double startTime;
    }
    public class VillagerTrainingProgressChange : StateChange { public Guid buildingID; public int entryIndex; public double progress; }
    public class VillagerTrainingCompletedChange : StateChange { public Guid buildingID; public int quantity; }

    // Garrison Changes
    public class UnitsGarrisonedChange : StateChange { public Guid buildingID; public string unitType; public int quantity; }
    public class UnitsUngarrisonedChange : StateChange { public Guid buildingID; public string unitType; public int quantity; }
    public class VillagersGarrisonedChange : StateChange { public Guid buildingID; public int quantity; }
    public class VillagersUngarrisonedChange : StateChange { public Guid buildingID; public int quantity; }

    // Combat Changes
    public class CombatStartedChange : StateChange { public Guid attackerID; public Guid defenderID; public HexCoordinate coordinate; }
    public class CombatDamageDealtChange : StateChange
    {
        public Guid sourceID; public Guid targetID; public double damage; public string damageType;
    }
    public class CombatPhaseCompletedChange : StateChange { public Guid attackerID; public Guid defenderID; public int phase; }
    public class CombatEndedChange : StateChange { public Guid attackerID; public Guid defenderID; public CombatResultData result; }
    public class GarrisonDefenseAttackChange : StateChange { public Guid buildingID; public Guid targetArmyID; public double damage; }
    public class VillagerCasualtiesChange : StateChange { public Guid villagerGroupID; public int casualties; public int remaining; }

    // Stack Combat Changes
    public class StackCombatStartedChange : StateChange
    {
        public HexCoordinate coordinate; public List<Guid> attackerArmyIDs; public List<Guid> defenderArmyIDs;
    }
    public class StackCombatPairingEndedChange : StateChange
    {
        public HexCoordinate coordinate; public Guid? winnerArmyID; public Guid? loserArmyID;
    }
    public class StackCombatTierAdvancedChange : StateChange { public HexCoordinate coordinate; public int newTier; }
    public class StackCombatEndedChange : StateChange { public HexCoordinate coordinate; public CombatResultData result; }
    public class ArmyForcedRetreatChange : StateChange { public Guid armyID; public HexCoordinate from; public HexCoordinate to; }

    // Resource Changes
    public class ResourcesChangedChange : StateChange
    {
        public Guid playerID; public string resourceType; public int oldAmount; public int newAmount;
    }
    public class ResourcesGatheredChange : StateChange
    {
        public Guid playerID; public string resourceType; public int amount; public HexCoordinate sourceCoordinate;
    }
    public class ResourcePointAmountChangedChange : StateChange
    {
        public HexCoordinate coordinate; public int oldAmount; public int newAmount;
    }
    public class ResourcePointDepletedChange : StateChange { public HexCoordinate coordinate; public string resourceType; }
    public class ResourcePointCreatedChange : StateChange
    {
        public HexCoordinate coordinate; public string resourceType; public int amount;
    }
    public class CollectionRateChangedChange : StateChange
    {
        public Guid playerID; public string resourceType; public double oldRate; public double newRate;
    }

    // Player Changes
    public class PlayerResourcesUpdatedChange : StateChange { public Guid playerID; public Dictionary<string, int> resources; }
    public class PlayerVisionUpdatedChange : StateChange
    {
        public Guid playerID; public List<HexCoordinate> visibleCoordinates; public List<HexCoordinate> exploredCoordinates;
    }
    public class DiplomacyChangedChange : StateChange { public Guid playerID; public Guid otherPlayerID; public string newStatus; }

    // Map Changes
    public class FogOfWarUpdatedChange : StateChange { public Guid playerID; public HexCoordinate coordinate; public string visibility; }

    // Research Changes
    public class ResearchStartedChange : StateChange { public Guid playerID; public string researchType; public double startTime; }
    public class ResearchProgressChange : StateChange { public Guid playerID; public string researchType; public double progress; }
    public class ResearchCompletedChange : StateChange { public Guid playerID; public string researchType; }

    // Unit Upgrade Changes
    public class UnitUpgradeStartedChange : StateChange
    {
        public Guid playerID; public string unitType; public int tier; public Guid buildingID; public double startTime;
    }
    public class UnitUpgradeProgressChange : StateChange { public Guid playerID; public string unitType; public double progress; }
    public class UnitUpgradeCompletedChange : StateChange { public Guid playerID; public string unitType; public int tier; }

    // Game State Changes
    public class GameTickChange : StateChange { public double currentTime; }
    public class GameOverChange : StateChange { public string reason; public Guid? winnerID; }

    // Supporting Types

    [System.Serializable]
    public struct BuildingDamageRecord
    {
        public Guid buildingID;
        public string buildingType;
        public double damageDealt;
        public double healthBefore;
        public double healthAfter;
        public bool wasDestroyed;

        public BuildingDamageRecord(Guid buildingID, string buildingType, double damageDealt,
            double healthBefore, double healthAfter, bool wasDestroyed)
        {
            this.buildingID = buildingID;
            this.buildingType = buildingType;
            this.damageDealt = damageDealt;
            this.healthBefore = healthBefore;
            this.healthAfter = healthAfter;
            this.wasDestroyed = wasDestroyed;
        }
    }

    [System.Serializable]
    public struct CombatResultData
    {
        public Guid? winnerID;
        public Guid? loserID;
        public Dictionary<string, int> attackerCasualties;
        public Dictionary<string, int> defenderCasualties;
        public double combatDuration;
        public BuildingDamageRecord? buildingDamage;

        public CombatResultData(Guid? winnerID = null, Guid? loserID = null,
            Dictionary<string, int> attackerCasualties = null,
            Dictionary<string, int> defenderCasualties = null,
            double combatDuration = 0,
            BuildingDamageRecord? buildingDamage = null)
        {
            this.winnerID = winnerID;
            this.loserID = loserID;
            this.attackerCasualties = attackerCasualties ?? new Dictionary<string, int>();
            this.defenderCasualties = defenderCasualties ?? new Dictionary<string, int>();
            this.combatDuration = combatDuration;
            this.buildingDamage = buildingDamage;
        }
    }

    [System.Serializable]
    public class StateChangeBatch
    {
        public double timestamp;
        public List<StateChange> changes;
        public Guid? sourceCommandID;

        public StateChangeBatch(double timestamp, List<StateChange> changes, Guid? sourceCommandID = null)
        {
            this.timestamp = timestamp;
            this.changes = changes;
            this.sourceCommandID = sourceCommandID;
        }
    }

    [System.Serializable]
    public class StateChangeBuilder
    {
        private List<StateChange> changes = new List<StateChange>();
        private double startTime;
        private Guid? sourceCommandID;

        public StateChangeBuilder(double currentTime, Guid? sourceCommandID = null)
        {
            this.startTime = currentTime;
            this.sourceCommandID = sourceCommandID;
        }

        public void Add(StateChange change) { changes.Add(change); }
        public void AddAll(List<StateChange> newChanges) { changes.AddRange(newChanges); }

        public StateChangeBatch Build()
        {
            return new StateChangeBatch(startTime, changes, sourceCommandID);
        }

        public bool IsEmpty => changes.Count == 0;
        public int Count => changes.Count;
    }
}
