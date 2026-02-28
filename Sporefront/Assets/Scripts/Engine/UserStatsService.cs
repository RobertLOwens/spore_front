// ============================================================================
// FILE: Engine/UserStatsService.cs
// PURPOSE: Firestore-backed lifetime user statistics tracking and game history.
//          Port of UserStatsService.swift.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
using Firebase.Firestore;
using Firebase.Extensions;
#endif

namespace Sporefront.Engine
{
    // ================================================================
    // Game End Stats — passed from Visual layer to avoid circular dep
    // ================================================================

    public struct GameEndStats
    {
        public float timePlayed;
        public int battlesWon;
        public int battlesLost;
        public int unitsKilled;
        public int unitsLost;
        public int buildingsBuilt;
        public int resourcesGathered;
    }

    // ================================================================
    // User Stats Service
    // ================================================================

    public class UserStatsService
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static UserStatsService _instance;
        public static UserStatsService Instance => _instance ?? (_instance = new UserStatsService());

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private FirebaseFirestore db;
#endif

        private UserStatsService() { }

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            db = FirebaseFirestore.DefaultInstance;
#endif
        }

        // ================================================================
        // Fetch Stats
        // ================================================================

        public void FetchStats(string uid, Action<UserStats> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new UserStats());
                return;
            }

            db.Collection("users").Document(uid).Collection("stats").Document("lifetime")
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    callback?.Invoke(new UserStats());
                    return;
                }

                var data = task.Result.ToDictionary();
                var stats = UserStats.FromDictionary(data);
                callback?.Invoke(stats);
            });
#else
            callback?.Invoke(new UserStats());
#endif
        }

        // ================================================================
        // Record Game End
        // ================================================================

        public void RecordGameEnd(string uid, bool isVictory, GameEndStats stats,
            string reason, Action<bool> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(false);
                return;
            }

            var docRef = db.Collection("users").Document(uid)
                .Collection("stats").Document("lifetime");

            db.RunTransactionAsync(transaction =>
            {
                return transaction.GetSnapshotAsync(docRef).ContinueWithOnMainThread(task =>
                {
                    int currentHighest = 0;
                    if (task.Result.Exists)
                    {
                        var data = task.Result.ToDictionary();
                        if (data.TryGetValue("highestPopulation", out var hp))
                            currentHighest = Convert.ToInt32(hp);
                    }

                    var updateData = new Dictionary<string, object>
                    {
                        { "gamesPlayed", FieldValue.Increment(1) },
                        { "totalPlayTime", FieldValue.Increment((double)stats.timePlayed) },
                        { "battlesWon", FieldValue.Increment(stats.battlesWon) },
                        { "battlesLost", FieldValue.Increment(stats.battlesLost) },
                        { "unitsKilled", FieldValue.Increment(stats.unitsKilled) },
                        { "unitsLost", FieldValue.Increment(stats.unitsLost) },
                        { "buildingsBuilt", FieldValue.Increment(stats.buildingsBuilt) },
                        { "totalResourcesGathered", FieldValue.Increment(stats.resourcesGathered) },
                        { "highestPopulation", Math.Max(currentHighest, 0) },
                        { "lastUpdated", FieldValue.ServerTimestamp }
                    };

                    if (isVictory)
                        updateData["gamesWon"] = FieldValue.Increment(1);
                    else
                        updateData["gamesLost"] = FieldValue.Increment(1);

                    transaction.Set(docRef, updateData, SetOptions.MergeAll);
                });
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[UserStatsService] Failed to record stats: {task.Exception}");
                    callback?.Invoke(false);
                    return;
                }

                Debug.Log("[UserStatsService] Game stats recorded successfully");
                callback?.Invoke(true);
            });

            // Also record individual game history entry
            RecordGameHistory(uid, isVictory, stats, reason);
#else
            callback?.Invoke(false);
#endif
        }

        // ================================================================
        // Game History
        // ================================================================

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private void RecordGameHistory(string uid, bool isVictory, GameEndStats stats, string reason)
        {
            var data = new Dictionary<string, object>
            {
                { "date", FieldValue.ServerTimestamp },
                { "isVictory", isVictory },
                { "reason", reason ?? "unknown" },
                { "duration", (double)stats.timePlayed },
                { "battlesWon", stats.battlesWon },
                { "battlesLost", stats.battlesLost },
                { "unitsKilled", stats.unitsKilled },
                { "unitsLost", stats.unitsLost },
                { "buildingsBuilt", stats.buildingsBuilt },
                { "resourcesGathered", stats.resourcesGathered },
                { "maxPopulation", 0 }
            };

            db.Collection("users").Document(uid).Collection("gameHistory")
                .AddAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[UserStatsService] Failed to record game history: {task.Exception}");
                else
                    Debug.Log("[UserStatsService] Game history entry recorded");
            });
        }
#endif

        public void FetchRecentGames(string uid, int limit, Action<List<GameHistoryEntry>> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new List<GameHistoryEntry>());
                return;
            }

            db.Collection("users").Document(uid).Collection("gameHistory")
                .OrderByDescending("date")
                .Limit(limit)
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[UserStatsService] Failed to fetch recent games: {task.Exception}");
                    callback?.Invoke(new List<GameHistoryEntry>());
                    return;
                }

                var entries = new List<GameHistoryEntry>();
                foreach (var doc in task.Result.Documents)
                {
                    var data = doc.ToDictionary();
                    entries.Add(GameHistoryEntry.FromDictionary(data));
                }
                callback?.Invoke(entries);
            });
#else
            callback?.Invoke(new List<GameHistoryEntry>());
#endif
        }
    }
}
