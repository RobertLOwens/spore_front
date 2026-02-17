using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;

namespace Sporefront.Models.Combat
{
    [System.Serializable]
    public class CombatPairing
    {
        public Guid attackerArmyID;
        public Guid defenderArmyID;
        public Guid activeCombatID;
        public bool isComplete;
        public Guid? winnerArmyID;
        public Guid? loserArmyID;

        public CombatPairing(Guid attackerArmyID, Guid defenderArmyID, Guid activeCombatID)
        {
            this.attackerArmyID = attackerArmyID;
            this.defenderArmyID = defenderArmyID;
            this.activeCombatID = activeCombatID;
            this.isComplete = false;
            this.winnerArmyID = null;
            this.loserArmyID = null;
        }
    }

    public class StackCombat
    {
        public Guid id;
        public HexCoordinate coordinate;
        public double startTime;
        public Guid attackerOwnerID;

        public List<CombatPairing> activePairings;
        public List<Guid> attackerQueue;
        public List<DefensiveStackEntry> defenderQueue;

        public HashSet<Guid> crossTileDefenderIDs;
        public HashSet<Guid> defeatedArmyIDs;
        public HashSet<Guid> retreatedArmyIDs;

        public bool villagerPhaseActive;
        public List<Guid> villagerGroupIDs;
        public DefensiveTier currentTier;
        public bool isComplete;

        public Dictionary<Guid, int> frontsPerArmy;

        public StackCombat(HexCoordinate coordinate, double startTime, Guid attackerOwnerID)
        {
            this.id = Guid.NewGuid();
            this.coordinate = coordinate;
            this.startTime = startTime;
            this.attackerOwnerID = attackerOwnerID;

            this.activePairings = new List<CombatPairing>();
            this.attackerQueue = new List<Guid>();
            this.defenderQueue = new List<DefensiveStackEntry>();

            this.crossTileDefenderIDs = new HashSet<Guid>();
            this.defeatedArmyIDs = new HashSet<Guid>();
            this.retreatedArmyIDs = new HashSet<Guid>();

            this.villagerPhaseActive = false;
            this.villagerGroupIDs = new List<Guid>();
            this.currentTier = DefensiveTier.Entrenched;
            this.isComplete = false;

            this.frontsPerArmy = new Dictionary<Guid, int>();
        }

        public double StretchingMultiplier(Guid armyID)
        {
            int fronts;
            if (!frontsPerArmy.TryGetValue(armyID, out fronts)) return 1.0;

            double penalty = GameConfig.StackCombat.StretchingPenaltyPerFront;
            double multiplier = 1.0 - penalty * fronts;
            return Math.Max(0.1, multiplier);
        }

        public void AddFront(Guid armyID)
        {
            if (frontsPerArmy.ContainsKey(armyID))
                frontsPerArmy[armyID]++;
            else
                frontsPerArmy[armyID] = 1;
        }

        public void RemoveFront(Guid armyID)
        {
            if (!frontsPerArmy.ContainsKey(armyID)) return;

            frontsPerArmy[armyID]--;
            if (frontsPerArmy[armyID] <= 0)
                frontsPerArmy.Remove(armyID);
        }

        public void AddDefender(DefensiveStackEntry entry)
        {
            defenderQueue.Add(entry);
        }

        public DefensiveStackEntry? DequeueNextDefender()
        {
            if (defenderQueue.Count == 0) return null;

            var next = defenderQueue[0];
            defenderQueue.RemoveAt(0);
            return next;
        }

        public Guid? DequeueNextAttacker()
        {
            if (attackerQueue.Count == 0) return null;

            var next = attackerQueue[0];
            attackerQueue.RemoveAt(0);
            return next;
        }

        public bool AllArmyDefendersEngaged
        {
            get { return defenderQueue.Count == 0; }
        }

        public bool HasWaitingAttackers
        {
            get { return attackerQueue.Count > 0; }
        }

        public bool HasWaitingDefenders
        {
            get { return defenderQueue.Count > 0; }
        }

        public bool InvolvesArmy(Guid armyID)
        {
            if (attackerQueue.Contains(armyID)) return true;

            foreach (var entry in defenderQueue)
            {
                if (entry.armyID == armyID) return true;
            }

            foreach (var pairing in activePairings)
            {
                if (pairing.attackerArmyID == armyID || pairing.defenderArmyID == armyID)
                    return true;
            }

            if (defeatedArmyIDs.Contains(armyID)) return true;
            if (retreatedArmyIDs.Contains(armyID)) return true;

            return false;
        }

        public void RemoveArmy(Guid armyID)
        {
            attackerQueue.Remove(armyID);

            defenderQueue.RemoveAll(entry => entry.armyID == armyID);

            for (int i = activePairings.Count - 1; i >= 0; i--)
            {
                var pairing = activePairings[i];
                if (pairing.attackerArmyID == armyID || pairing.defenderArmyID == armyID)
                {
                    pairing.isComplete = true;
                    if (pairing.attackerArmyID == armyID)
                    {
                        pairing.winnerArmyID = pairing.defenderArmyID;
                        pairing.loserArmyID = armyID;
                    }
                    else
                    {
                        pairing.winnerArmyID = pairing.attackerArmyID;
                        pairing.loserArmyID = armyID;
                    }
                }
            }

            if (frontsPerArmy.ContainsKey(armyID))
                frontsPerArmy.Remove(armyID);
        }
    }
}
