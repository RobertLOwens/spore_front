using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Models.Combat
{
    public enum DefensiveTier
    {
        Entrenched = 1,
        Regular = 2,
        Villager = 3
    }

    public static class DefensiveTierExtensions
    {
        public static string DisplayName(this DefensiveTier tier)
        {
            switch (tier)
            {
                case DefensiveTier.Entrenched: return "Entrenched";
                case DefensiveTier.Regular: return "Regular";
                case DefensiveTier.Villager: return "Villager";
                default: return tier.ToString();
            }
        }
    }

    [System.Serializable]
    public struct DefensiveStackEntry
    {
        public Guid armyID;
        public DefensiveTier tier;
        public bool isCrossTile;
        public HexCoordinate sourceCoordinate;
        public double entrenchmentBonus;

        public DefensiveStackEntry(Guid armyID, DefensiveTier tier, bool isCrossTile,
            HexCoordinate sourceCoordinate, double entrenchmentBonus)
        {
            this.armyID = armyID;
            this.tier = tier;
            this.isCrossTile = isCrossTile;
            this.sourceCoordinate = sourceCoordinate;
            this.entrenchmentBonus = entrenchmentBonus;
        }
    }

    public class DefensiveStack
    {
        public List<DefensiveStackEntry> entries;
        public List<Guid> villagerGroupIDs;
        public HexCoordinate coordinate;

        public DefensiveStack(HexCoordinate coordinate)
        {
            this.coordinate = coordinate;
            this.entries = new List<DefensiveStackEntry>();
            this.villagerGroupIDs = new List<Guid>();
        }

        public bool HasEntrenchedDefenders
        {
            get
            {
                foreach (var entry in entries)
                {
                    if (entry.tier == DefensiveTier.Entrenched) return true;
                }
                return false;
            }
        }

        public List<DefensiveStackEntry> ArmyEntries
        {
            get
            {
                var result = new List<DefensiveStackEntry>();
                foreach (var entry in entries)
                {
                    if (entry.tier == DefensiveTier.Entrenched || entry.tier == DefensiveTier.Regular)
                        result.Add(entry);
                }
                return result;
            }
        }

        public HashSet<Guid> CrossTileDefenderIDs
        {
            get
            {
                var result = new HashSet<Guid>();
                foreach (var entry in entries)
                {
                    if (entry.isCrossTile) result.Add(entry.armyID);
                }
                return result;
            }
        }

        public bool IsEmpty
        {
            get { return entries.Count == 0 && villagerGroupIDs.Count == 0; }
        }

        public bool OnlyVillagers
        {
            get { return entries.Count == 0 && villagerGroupIDs.Count > 0; }
        }

        public List<DefensiveStackEntry> GetEntries(DefensiveTier tier)
        {
            var result = new List<DefensiveStackEntry>();
            foreach (var entry in entries)
            {
                if (entry.tier == tier) result.Add(entry);
            }
            return result;
        }

        public static DefensiveStack Build(HexCoordinate coordinate, GameState gameState, Guid attackerOwnerID)
        {
            var stack = new DefensiveStack(coordinate);
            double defenseBonus = GameConfig.Entrenchment.DefenseBonus;

            // Tier 1: Entrenched armies on the tile owned by enemy
            var armiesAtCoordinate = gameState.GetArmies(coordinate);
            var entrenchedOnTile = new List<ArmyData>();
            var nonEntrenched = new List<ArmyData>();

            foreach (var army in armiesAtCoordinate)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == attackerOwnerID) continue;
                if (army.isInCombat) continue;

                if (army.isEntrenched)
                {
                    entrenchedOnTile.Add(army);
                }
                else
                {
                    nonEntrenched.Add(army);
                }
            }

            // Sort entrenched on-tile by entrenchmentStartTime descending (LIFO)
            entrenchedOnTile.Sort((a, b) =>
            {
                double timeA = a.entrenchmentStartTime ?? 0.0;
                double timeB = b.entrenchmentStartTime ?? 0.0;
                return timeB.CompareTo(timeA);
            });

            foreach (var army in entrenchedOnTile)
            {
                stack.entries.Add(new DefensiveStackEntry(
                    army.id,
                    DefensiveTier.Entrenched,
                    false,
                    army.coordinate,
                    defenseBonus
                ));
            }

            // Tier 1 (cross-tile): Entrenched armies covering this coordinate from adjacent tiles
            var crossTileEntrenched = gameState.GetEntrenchedArmiesCovering(coordinate);
            var crossTileFiltered = new List<ArmyData>();

            foreach (var army in crossTileEntrenched)
            {
                if (!army.ownerID.HasValue || army.ownerID.Value == attackerOwnerID) continue;
                if (army.isInCombat) continue;

                // Avoid duplicating armies already on the tile
                bool alreadyAdded = false;
                foreach (var entry in stack.entries)
                {
                    if (entry.armyID == army.id) { alreadyAdded = true; break; }
                }
                if (!alreadyAdded)
                {
                    crossTileFiltered.Add(army);
                }
            }

            // Sort cross-tile entrenched by entrenchmentStartTime descending (LIFO)
            crossTileFiltered.Sort((a, b) =>
            {
                double timeA = a.entrenchmentStartTime ?? 0.0;
                double timeB = b.entrenchmentStartTime ?? 0.0;
                return timeB.CompareTo(timeA);
            });

            foreach (var army in crossTileFiltered)
            {
                stack.entries.Add(new DefensiveStackEntry(
                    army.id,
                    DefensiveTier.Entrenched,
                    true,
                    army.coordinate,
                    defenseBonus
                ));
            }

            // Tier 2: Non-entrenched, non-in-combat armies on the tile, sorted by arrivalTime descending
            nonEntrenched.Sort((a, b) => b.arrivalTime.CompareTo(a.arrivalTime));

            foreach (var army in nonEntrenched)
            {
                stack.entries.Add(new DefensiveStackEntry(
                    army.id,
                    DefensiveTier.Regular,
                    false,
                    army.coordinate,
                    0.0
                ));
            }

            // Tier 3: Villager groups at the coordinate
            var villagerGroups = gameState.GetVillagerGroups(coordinate);
            foreach (var group in villagerGroups)
            {
                if (!group.ownerID.HasValue || group.ownerID.Value == attackerOwnerID) continue;
                stack.villagerGroupIDs.Add(group.id);
            }

            return stack;
        }
    }
}
