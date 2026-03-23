using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    // Flags for classifying state changes into categories for conditional UI refresh
    [Flags]
    public enum StateChangeFlags
    {
        None          = 0,
        Resources     = 1 << 0,
        Buildings     = 1 << 1,
        Armies        = 1 << 2,
        Villagers     = 1 << 3,
        Training      = 1 << 4,
        Combat        = 1 << 5,
        FogOfWar      = 1 << 6,
        Research      = 1 << 7,
        Garrison      = 1 << 8,
        Movement      = 1 << 9,
        Commander     = 1 << 10,
        Entrenchment  = 1 << 11,
        UnitUpgrade   = 1 << 12,
    }

    // Base class for all state changes
    [System.Serializable]
    public abstract class StateChange
    {
        public static readonly List<StateChange> EmptyChanges = new List<StateChange>();

        public static StateChangeFlags ClassifyChange(StateChange change)
        {
            switch (change)
            {
                // Building changes
                case BuildingPlacedChange _:
                case BuildingConstructionStartedChange _:
                case BuildingConstructionProgressChange _:
                case BuildingCompletedChange _:
                case BuildingUpgradeStartedChange _:
                case BuildingUpgradeProgressChange _:
                case BuildingUpgradeCompletedChange _:
                case BuildingDemolitionStartedChange _:
                case BuildingDemolitionProgressChange _:
                case BuildingDemolishedChange _:
                case BuildingDemolitionCancelledChange _:
                case BuildingDamagedChange _:
                case BuildingRepairedChange _:
                case BuildingDestroyedChange _:
                    return StateChangeFlags.Buildings;

                // Army movement
                case ArmyMovedChange _:
                    return StateChangeFlags.Armies | StateChangeFlags.Movement;
                case ArmyRetreatingChange _:
                case ArmyAutoRetreatingChange _:
                case ArmyForcedRetreatChange _:
                    return StateChangeFlags.Armies | StateChangeFlags.Movement | StateChangeFlags.Combat;

                // Army creation/destruction
                case ArmyCreatedChange _:
                case ArmyCompositionChangedChange _:
                case ArmyDestroyedChange _:
                case ArmyMergedChange _:
                case ArmyStrandedChange _:
                case ArmyRemobilizedChange _:
                case AttackCancelledChange _:
                    return StateChangeFlags.Armies;

                // Entrenchment
                case ArmyEntrenchmentStartedChange _:
                case ArmyEntrenchmentProgressChange _:
                case ArmyEntrenchedChange _:
                case ArmyEntrenchmentCancelledChange _:
                    return StateChangeFlags.Armies | StateChangeFlags.Entrenchment;

                // Villager changes
                case VillagerGroupCreatedChange _:
                case VillagerGroupCountChangedChange _:
                case VillagerGroupDestroyedChange _:
                case VillagerGroupTaskChangedChange _:
                    return StateChangeFlags.Villagers;
                case VillagerGroupMovedChange _:
                    return StateChangeFlags.Villagers | StateChangeFlags.Movement;

                // Training
                case TrainingStartedChange _:
                case TrainingProgressChange _:
                case TrainingCompletedChange _:
                case VillagerTrainingStartedChange _:
                case VillagerTrainingProgressChange _:
                case VillagerTrainingCompletedChange _:
                    return StateChangeFlags.Training;

                // Garrison
                case UnitsGarrisonedChange _:
                case UnitsUngarrisonedChange _:
                case VillagersGarrisonedChange _:
                case VillagersUngarrisonedChange _:
                    return StateChangeFlags.Garrison;

                // Combat
                case CombatStartedChange _:
                case CombatDamageDealtChange _:
                case CombatPhaseCompletedChange _:
                case CombatEndedChange _:
                case GarrisonDefenseAttackChange _:
                case VillagerCasualtiesChange _:
                case StackCombatStartedChange _:
                case StackCombatPairingEndedChange _:
                case StackCombatTierAdvancedChange _:
                case StackCombatEndedChange _:
                    return StateChangeFlags.Combat;

                // Resources
                case ResourcesChangedChange _:
                case ResourcesGatheredChange _:
                case ResourcePointAmountChangedChange _:
                case ResourcePointDepletedChange _:
                case ResourcePointCreatedChange _:
                case CollectionRateChangedChange _:
                case PlayerResourcesUpdatedChange _:
                    return StateChangeFlags.Resources;

                // Fog of war
                case PlayerVisionUpdatedChange _:
                case FogOfWarUpdatedChange _:
                    return StateChangeFlags.FogOfWar;

                // Research
                case ResearchStartedChange _:
                case ResearchProgressChange _:
                case ResearchCompletedChange _:
                case ResearchCancelledChange _:
                    return StateChangeFlags.Research;

                // Commander
                case CommanderCreatedChange _:
                case CommanderXPGainedChange _:
                    return StateChangeFlags.Commander;

                // Unit upgrades
                case UnitUpgradeStartedChange _:
                case UnitUpgradeProgressChange _:
                case UnitUpgradeCompletedChange _:
                    return StateChangeFlags.UnitUpgrade;

                // Diplomacy
                case DiplomacyChangedChange _:
                    return StateChangeFlags.None;

                // Game state
                case GameTickChange _:
                case GameOverChange _:
                    return StateChangeFlags.None;

                default:
                    return StateChangeFlags.None;
            }
        }
    }

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
    public class ArmyStrandedChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }
    public class ArmyRemobilizedChange : StateChange { public Guid armyID; public HexCoordinate destination; }
    public class AttackCancelledChange : StateChange { public Guid armyID; public HexCoordinate coordinate; }

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
    public class CamouflagedArmiesChange : StateChange { public Guid observingPlayerID; public List<Guid> camouflagedArmyIDs; }

    // Poison Changes
    public class PoisonAppliedChange : StateChange
    {
        public Guid armyID;
        public Guid sourcePlayerID;
        public double damagePerTick;
        public double duration;
        public int stacks;
    }
    public class PoisonDamageTickChange : StateChange
    {
        public Guid armyID;
        public double damage;
        public double remainingDuration;
    }
    public class PoisonExpiredChange : StateChange { public Guid armyID; }
    public class SporeBurstTriggeredChange : StateChange
    {
        public Guid sourceArmyID;
        public HexCoordinate coordinate;
        public List<Guid> affectedArmyIDs;
    }

    // Research Changes
    public class ResearchStartedChange : StateChange { public Guid playerID; public string researchType; public double startTime; }
    public class ResearchProgressChange : StateChange { public Guid playerID; public string researchType; public double progress; }
    public class ResearchCompletedChange : StateChange { public Guid playerID; public string researchType; }
    public class ResearchCancelledChange : StateChange { public Guid playerID; public string researchType; }

    // Commander Changes
    public class CommanderCreatedChange : StateChange
    {
        public Guid commanderID;
        public Guid ownerID;
        public string name;
        public string specialty;
    }

    public class CommanderXPGainedChange : StateChange
    {
        public Guid commanderID;
        public int xpGained;
        public int newXP;
        public int newLevel;
        public CommanderRank newRank;
        public bool didLevelUp;
        public bool didRankUp;
    }

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
