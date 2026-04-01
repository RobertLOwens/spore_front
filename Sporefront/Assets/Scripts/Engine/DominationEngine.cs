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

        // Reusable collections to avoid per-tick allocations
        private Dictionary<HexCoordinate, List<(Guid ownerID, int unitCount)>> _armyPositions
            = new Dictionary<HexCoordinate, List<(Guid ownerID, int unitCount)>>();
        private Dictionary<Guid, int> _playerTroops = new Dictionary<Guid, int>();
        private Dictionary<Guid, double> _multiplierPerPlayer = new Dictionary<Guid, double>();
        private List<List<(Guid, int)>> _listPool = new List<List<(Guid, int)>>();

        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
        }

        public List<StateChange> Update(double currentTime)
        {
            if (gameState == null || gameState.controlZones == null || gameState.controlZones.Count == 0)
                return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            // Return pooled lists from last tick
            foreach (var list in _armyPositions.Values)
            {
                list.Clear();
                _listPool.Add(list);
            }
            _armyPositions.Clear();

            // Build a lookup of army coordinate → (ownerID, unit count) for fast zone checking
            foreach (var army in gameState.armies.Values)
            {
                if (!army.ownerID.HasValue) continue;
                List<(Guid, int)> list;
                if (!_armyPositions.TryGetValue(army.coordinate, out list))
                {
                    list = _listPool.Count > 0 ? _listPool[_listPool.Count - 1] : new List<(Guid, int)>();
                    if (_listPool.Count > 0) _listPool.RemoveAt(_listPool.Count - 1);
                    _armyPositions[army.coordinate] = list;
                }
                int totalUnits = 0;
                foreach (var count in army.militaryComposition.Values)
                    totalUnits += count;
                list.Add((army.ownerID.Value, totalUnits));
            }

            // Evaluate each control zone
            foreach (var zone in gameState.controlZones)
            {
                // Count troops per player in this zone
                _playerTroops.Clear();
                foreach (var tile in zone.tiles)
                {
                    List<(Guid ownerID, int unitCount)> armiesOnTile;
                    if (_armyPositions.TryGetValue(tile, out armiesOnTile))
                    {
                        foreach (var entry in armiesOnTile)
                        {
                            int existing;
                            _playerTroops.TryGetValue(entry.ownerID, out existing);
                            _playerTroops[entry.ownerID] = existing + entry.unitCount;
                        }
                    }
                }

                zone.presenceCount = new Dictionary<Guid, int>(_playerTroops);

                // Determine controller: player with strictly more troops
                Guid? newController = null;
                int maxTroops = 0;
                bool tied = false;
                foreach (var kvp in _playerTroops)
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
            _multiplierPerPlayer.Clear();
            foreach (var zone in gameState.controlZones)
            {
                if (zone.controllingPlayerID.HasValue)
                {
                    double current;
                    _multiplierPerPlayer.TryGetValue(zone.controllingPlayerID.Value, out current);
                    _multiplierPerPlayer[zone.controllingPlayerID.Value] = current + zone.pointsMultiplier;
                }
            }

            foreach (var kvp in _multiplierPerPlayer)
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
