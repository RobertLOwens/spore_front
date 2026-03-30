using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Engine
{
    public class DominationEngine
    {
        private GameState gameState;

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null || gameState.controlZones == null || gameState.controlZones.Count == 0)
                return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            // Build a lookup of army coordinate → (ownerID, unit count) for fast zone checking
            var armyPositions = new Dictionary<HexCoordinate, List<(Guid ownerID, int unitCount)>>();
            foreach (var army in gameState.armies.Values)
            {
                if (!army.ownerID.HasValue) continue;
                List<(Guid, int)> list;
                if (!armyPositions.TryGetValue(army.coordinate, out list))
                {
                    list = new List<(Guid, int)>();
                    armyPositions[army.coordinate] = list;
                }
                int totalUnits = 0;
                foreach (var count in army.composition.Values)
                    totalUnits += count;
                list.Add((army.ownerID.Value, totalUnits));
            }

            // Evaluate each control zone
            foreach (var zone in gameState.controlZones)
            {
                // Count troops per player in this zone
                var playerTroops = new Dictionary<Guid, int>();
                foreach (var tile in zone.tiles)
                {
                    List<(Guid ownerID, int unitCount)> armiesOnTile;
                    if (armyPositions.TryGetValue(tile, out armiesOnTile))
                    {
                        foreach (var entry in armiesOnTile)
                        {
                            int existing;
                            playerTroops.TryGetValue(entry.ownerID, out existing);
                            playerTroops[entry.ownerID] = existing + entry.unitCount;
                        }
                    }
                }

                zone.presenceCount = playerTroops;

                // Determine controller: player with strictly more troops
                Guid? newController = null;
                int maxTroops = 0;
                bool tied = false;
                foreach (var kvp in playerTroops)
                {
                    if (kvp.Value > maxTroops)
                    {
                        maxTroops = kvp.Value;
                        newController = kvp.Key;
                        tied = false;
                    }
                    else if (kvp.Value == maxTroops && kvp.Value > 0)
                    {
                        tied = true;
                    }
                }
                if (tied) newController = null;

                if (newController != zone.controllingPlayerID)
                {
                    changes.Add(new ZoneControlChange
                    {
                        zoneLabel = zone.label,
                        oldControllerID = zone.controllingPlayerID,
                        newControllerID = newController
                    });
                    zone.controllingPlayerID = newController;
                }
            }

            // Award points for controlled zones (using per-zone pointsMultiplier)
            var multiplierPerPlayer = new Dictionary<Guid, double>();
            foreach (var zone in gameState.controlZones)
            {
                if (zone.controllingPlayerID.HasValue)
                {
                    double current;
                    multiplierPerPlayer.TryGetValue(zone.controllingPlayerID.Value, out current);
                    multiplierPerPlayer[zone.controllingPlayerID.Value] = current + zone.pointsMultiplier;
                }
            }

            foreach (var kvp in multiplierPerPlayer)
            {
                double delta = GameConfig.Domination.PointsPerSecond * kvp.Value * GameConfig.Domination.UpdateInterval;
                double oldScore;
                gameState.dominationScores.TryGetValue(kvp.Key, out oldScore);
                double newScore = oldScore + delta;
                gameState.dominationScores[kvp.Key] = newScore;

                changes.Add(new DominationScoreChange
                {
                    playerID = kvp.Key,
                    newScore = newScore,
                    delta = delta
                });
            }

            // Check for victory
            foreach (var kvp in gameState.dominationScores)
            {
                if (kvp.Value >= GameConfig.Domination.ScoreToWin)
                {
                    gameState.isGameOver = true;
                    changes.Add(new DominationVictoryChange { winnerID = kvp.Key });
                    changes.Add(new GameOverChange
                    {
                        reason = GameOverReason.DominationVictory.DisplayMessage(),
                        winnerID = kvp.Key,
                        reasonType = GameOverReason.DominationVictory
                    });
                    break;
                }
            }

            return changes;
        }
    }
}
