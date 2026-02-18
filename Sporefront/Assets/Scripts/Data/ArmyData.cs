using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public struct PendingReinforcement
    {
        public Guid reinforcementID;
        public Dictionary<MilitaryUnitType, int> unitComposition;
        public double estimatedArrivalTime;
        public HexCoordinate sourceCoordinate;
        public HexCoordinate currentCoordinate;
        public List<HexCoordinate> path;
        public int pathIndex;

        public PendingReinforcement(Guid reinforcementID, Dictionary<MilitaryUnitType, int> units,
            double estimatedArrival, HexCoordinate source, List<HexCoordinate> path)
        {
            this.reinforcementID = reinforcementID;
            this.unitComposition = units;
            this.estimatedArrivalTime = estimatedArrival;
            this.sourceCoordinate = source;
            this.currentCoordinate = source;
            this.path = path;
            this.pathIndex = 0;
        }

        public int GetTotalUnits()
        {
            int total = 0;
            foreach (var kvp in unitComposition) total += kvp.Value;
            return total;
        }
    }

    [System.Serializable]
    public class ArmyData
    {
        public Guid id;
        public string name;
        public Guid? ownerID;
        public HexCoordinate coordinate;

        // Composition
        public Dictionary<MilitaryUnitType, int> militaryComposition = new Dictionary<MilitaryUnitType, int>();

        // Commander reference
        public Guid? commanderID;

        // Home base reference
        public Guid? homeBaseID;

        // State
        public bool isRetreating;

        // Pending reinforcements
        public List<PendingReinforcement> pendingReinforcements = new List<PendingReinforcement>();

        // Movement
        public List<HexCoordinate> currentPath;
        public int pathIndex;
        public double movementProgress;

        // Combat state
        public bool isInCombat;
        public Guid? combatTargetID;

        // Entrenchment state (transient)
        [System.NonSerialized] public bool isEntrenching;
        [System.NonSerialized] public bool isEntrenched;
        [System.NonSerialized] public double? entrenchmentStartTime;
        [System.NonSerialized] public HashSet<HexCoordinate> entrenchedCoveredTiles;

        // Arrival time (transient)
        [System.NonSerialized] public double arrivalTime;

        // Movement speed (transient, for visual interpolation)
        [System.NonSerialized] public double movementSpeed;

        // Stamina
        public double currentStamina = 100.0;
        public double maxStamina = 100.0;

        public ArmyData(string name, HexCoordinate coordinate, Guid? ownerID = null)
        {
            this.id = Guid.NewGuid();
            this.name = name;
            this.coordinate = coordinate;
            this.ownerID = ownerID;
            this.entrenchedCoveredTiles = new HashSet<HexCoordinate>();
        }

        public void ClearEntrenchment()
        {
            isEntrenching = false;
            isEntrenched = false;
            entrenchmentStartTime = null;
            if (entrenchedCoveredTiles == null)
                entrenchedCoveredTiles = new HashSet<HexCoordinate>();
            else
                entrenchedCoveredTiles.Clear();
        }

        // Unit Management

        public void AddMilitaryUnits(MilitaryUnitType unitType, int count)
        {
            if (militaryComposition.ContainsKey(unitType))
                militaryComposition[unitType] += count;
            else
                militaryComposition[unitType] = count;
        }

        public int RemoveMilitaryUnits(MilitaryUnitType unitType, int count)
        {
            if (!militaryComposition.ContainsKey(unitType)) return 0;
            int current = militaryComposition[unitType];
            int toRemove = Math.Min(current, count);
            int remaining = current - toRemove;
            if (remaining > 0)
                militaryComposition[unitType] = remaining;
            else
                militaryComposition.Remove(unitType);
            return toRemove;
        }

        public void SetMilitaryComposition(Dictionary<MilitaryUnitType, int> composition)
        {
            militaryComposition = composition;
        }

        public int GetTotalUnits()
        {
            int total = 0;
            foreach (var kvp in militaryComposition) total += kvp.Value;
            return total;
        }

        public int GetPopulationUsed()
        {
            int pop = 0;
            foreach (var kvp in militaryComposition)
                pop += kvp.Key.PopSpace() * kvp.Value;
            return pop;
        }

        public int GetUnitCount(MilitaryUnitType type)
        {
            return militaryComposition.ContainsKey(type) ? militaryComposition[type] : 0;
        }

        public bool HasMilitaryUnits() => GetTotalUnits() > 0;
        public bool IsEmpty() => GetTotalUnits() == 0;

        public double SlowestUnitMoveSpeed
        {
            get
            {
                if (militaryComposition.Count == 0) return 1.6;
                double slowest = 0;
                foreach (var kvp in militaryComposition)
                {
                    double speed = kvp.Key.MoveSpeed();
                    if (speed > slowest) slowest = speed;
                }
                return slowest;
            }
        }

        // Combat Stats

        public UnitCombatStats GetAggregatedCombatStats()
        {
            var allStats = new List<UnitCombatStats>();
            foreach (var kvp in militaryComposition)
            {
                for (int i = 0; i < kvp.Value; i++)
                    allStats.Add(kvp.Key.CombatStats());
            }
            return UnitCombatStats.Aggregate(allStats);
        }

        public double GetTotalHP()
        {
            double total = 0;
            foreach (var kvp in militaryComposition)
                total += kvp.Key.HP() * kvp.Value;
            return total;
        }

        public int GetUnitCountByCategory(UnitCategory category)
        {
            int count = 0;
            foreach (var kvp in militaryComposition)
            {
                if (kvp.Key.Category() == category)
                    count += kvp.Value;
            }
            return count;
        }

        public double GetWeightedStrength()
        {
            double strength = 0;
            foreach (var kvp in militaryComposition)
            {
                double hp = kvp.Key.HP();
                double damage = kvp.Key.CombatStats().TotalDamage;
                strength += kvp.Value * (hp * (1.0 + damage * 0.1));
            }
            return strength;
        }

        public (double cavalry, double ranged, double infantry, double siege) GetCategoryRatios()
        {
            double total = GetTotalUnits();
            if (total <= 0) return (0, 0, 0, 0);
            return (
                GetUnitCountByCategory(UnitCategory.Cavalry) / total,
                GetUnitCountByCategory(UnitCategory.Ranged) / total,
                GetUnitCountByCategory(UnitCategory.Infantry) / total,
                GetUnitCountByCategory(UnitCategory.Siege) / total
            );
        }

        public UnitCategory? GetPrimaryCategory()
        {
            var categoryCounts = new Dictionary<UnitCategory, int>();
            foreach (var kvp in militaryComposition)
            {
                var cat = kvp.Key.Category();
                if (categoryCounts.ContainsKey(cat))
                    categoryCounts[cat] += kvp.Value;
                else
                    categoryCounts[cat] = kvp.Value;
            }
            if (categoryCounts.Count == 0) return null;
            UnitCategory best = UnitCategory.Infantry;
            int bestCount = 0;
            foreach (var kvp in categoryCounts)
            {
                if (kvp.Value > bestCount) { best = kvp.Key; bestCount = kvp.Value; }
            }
            return best;
        }

        // Reinforcements

        public void AddPendingReinforcement(PendingReinforcement reinforcement)
        {
            pendingReinforcements.Add(reinforcement);
        }

        public void RemovePendingReinforcement(Guid id)
        {
            pendingReinforcements.RemoveAll(r => r.reinforcementID == id);
        }

        public void ReceiveReinforcement(Dictionary<MilitaryUnitType, int> units)
        {
            foreach (var kvp in units)
                AddMilitaryUnits(kvp.Key, kvp.Value);
        }

        public bool IsAwaitingReinforcements => pendingReinforcements.Count > 0;

        public int GetTotalPendingUnits()
        {
            int total = 0;
            foreach (var r in pendingReinforcements) total += r.GetTotalUnits();
            return total;
        }

        // Merging

        public void Merge(ArmyData otherArmy)
        {
            foreach (var kvp in otherArmy.militaryComposition)
                AddMilitaryUnits(kvp.Key, kvp.Value);
        }

        // Capacity

        public int GetMaxArmySize(int? commanderLeadership)
        {
            if (!commanderLeadership.HasValue) return 40;
            return GameConfig.Commander.LeadershipToArmySizeBase +
                   commanderLeadership.Value * GameConfig.Commander.LeadershipToArmySizePerPoint;
        }
    }
}
