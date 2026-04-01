// ============================================================================
// FILE: Engine/MatchmakingService.cs
// PURPOSE: Queue-based matchmaking — enter queue, find opponent via Firestore
//          transactions, ready-up, and launch game.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

namespace Sporefront.Engine
{
    // ================================================================
    // Match Result
    // ================================================================

    public struct MatchResult
    {
        public string gameID;
        public bool isHost;
        public string opponentUID;
        public string opponentDisplayName;
        public string opponentFaction;
        public string opponentPlayerID;   // Guid string
        public string localFaction;
        public string localPlayerID;      // Guid string
    }

    // ================================================================
    // Matchmaking Service
    // ================================================================

    public class MatchmakingService
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static MatchmakingService _instance;
        public static MatchmakingService Instance => _instance ?? (_instance = new MatchmakingService());

        // ================================================================
        // Events
        // ================================================================

        public event Action<MatchResult> OnMatchFound;
        public event Action OnMatchCancelled;
        public event Action<List<string>> OnReadyPlayersChanged;

        // ================================================================
        // State
        // ================================================================

        public bool IsInQueue { get; private set; }
        public bool IsMatched { get; private set; }
        public MatchResult? CurrentMatch { get; private set; }

        private string queuedFaction;
        private string queuedPlayerID;
        private float pollTimer;
        private float queueTimer;
        private float PollInterval => GameConfig.Online.PollIntervalSeconds;
        private float QueueTimeout => GameConfig.Online.QueueTimeoutSeconds;
        private float ReadyTimeout => GameConfig.Online.ReadyTimeoutSeconds;
        private float StaleEntryAge => GameConfig.Online.StaleEntryAgeSeconds;

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private FirebaseFirestore db;
        private ListenerRegistration matchListener;
        private ListenerRegistration readyListener;
#endif

        private MatchmakingService() { }

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
        // Enter Queue
        // ================================================================

        public void EnterQueue(string faction, Guid playerID, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(false, "Not signed in");
                return;
            }

            var displayName = AuthService.Instance.CurrentDisplayName ?? "Unknown";

            queuedFaction = faction;
            queuedPlayerID = playerID.ToString();
            pollTimer = 0f;
            queueTimer = 0f;

            // Clean stale entries first (best-effort)
            CleanupStaleEntries();

            var data = new Dictionary<string, object>
            {
                { "uid", uid },
                { "displayName", displayName },
                { "faction", faction },
                { "playerID", playerID.ToString() },
                { "enqueuedAt", FieldValue.ServerTimestamp },
                { "status", "waiting" },
                { "matchedGameID", "" },
                { "matchedOpponentUID", "" }
            };

            db.Collection("matchmaking").Document(uid).SetAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    callback?.Invoke(false, task.Exception?.InnerException?.Message ?? "Failed to enter queue");
                    return;
                }

                IsInQueue = true;
                IsMatched = false;
                CurrentMatch = null;

                // Start listening for match (in case another client matches us)
                StartListeningForMatch();

                Debug.Log("[MatchmakingService] Entered queue");
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Leave Queue
        // ================================================================

        public void LeaveQueue(Action<bool> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            StopListeningForMatch();
            StopListeningForReady();

            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                IsInQueue = false;
                IsMatched = false;
                CurrentMatch = null;
                callback?.Invoke(true);
                return;
            }

            db.Collection("matchmaking").Document(uid).DeleteAsync().ContinueWithOnMainThread(task =>
            {
                IsInQueue = false;
                IsMatched = false;
                CurrentMatch = null;
                callback?.Invoke(!task.IsFaulted);
            });
#else
            IsInQueue = false;
            IsMatched = false;
            CurrentMatch = null;
            callback?.Invoke(true);
#endif
        }

        // ================================================================
        // Update (call from MonoBehaviour Update)
        // ================================================================

        public void Update(float deltaTime)
        {
            if (!IsInQueue || IsMatched) return;

            queueTimer += deltaTime;
            if (queueTimer >= QueueTimeout)
            {
                Debug.Log("[MatchmakingService] Queue timeout");
                LeaveQueue(null);
                OnMatchCancelled?.Invoke();
                return;
            }

            pollTimer += deltaTime;
            if (pollTimer >= PollInterval)
            {
                pollTimer = 0f;
                PollForOpponents();
            }
        }

        public float QueueElapsedTime => queueTimer;

        // ================================================================
        // Poll for Opponents
        // ================================================================

        private void PollForOpponents()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (!IsInQueue || IsMatched) return;

            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid)) return;

            db.Collection("matchmaking")
                .WhereEqualTo("status", "waiting")
                .OrderBy("enqueuedAt")
                .Limit(10)
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !IsInQueue || IsMatched) return;

                foreach (var doc in task.Result.Documents)
                {
                    if (doc.Id == uid) continue; // Skip self

                    var data = doc.ToDictionary();
                    string status = data.ContainsKey("status") ? data["status"] as string : null;
                    if (status != "waiting") continue;

                    // Found an opponent — attempt atomic match
                    AttemptMatch(doc.Id, data);
                    return; // Only try one at a time
                }
            });
#endif
        }

        // ================================================================
        // Attempt Match (Firestore Transaction)
        // ================================================================

        private void AttemptMatch(string opponentUID, Dictionary<string, object> opponentData)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid)) return;

            var myRef = db.Collection("matchmaking").Document(uid);
            var oppRef = db.Collection("matchmaking").Document(opponentUID);

            db.RunTransactionAsync(transaction =>
            {
                return transaction.GetSnapshotAsync(myRef).ContinueWithOnMainThread(myTask =>
                {
                    if (myTask.IsFaulted || !myTask.Result.Exists)
                        throw new InvalidOperationException("Own queue entry missing");

                    var myData = myTask.Result.ToDictionary();
                    string myStatus = myData.ContainsKey("status") ? myData["status"] as string : null;
                    if (myStatus != "waiting")
                        throw new InvalidOperationException("Already matched");

                    return transaction.GetSnapshotAsync(oppRef).ContinueWithOnMainThread(oppTask =>
                    {
                        if (oppTask.IsFaulted || !oppTask.Result.Exists)
                            throw new InvalidOperationException("Opponent queue entry missing");

                        var oppData = oppTask.Result.ToDictionary();
                        string oppStatus = oppData.ContainsKey("status") ? oppData["status"] as string : null;
                        if (oppStatus != "waiting")
                            throw new InvalidOperationException("Opponent already matched");

                        // Both still waiting — match them
                        string gameID = Guid.NewGuid().ToString();

                        transaction.Update(myRef, new Dictionary<string, object>
                        {
                            { "status", "matched" },
                            { "matchedGameID", gameID },
                            { "matchedOpponentUID", opponentUID }
                        });

                        transaction.Update(oppRef, new Dictionary<string, object>
                        {
                            { "status", "matched" },
                            { "matchedGameID", gameID },
                            { "matchedOpponentUID", uid }
                        });

                        // Create skeleton game doc with readyPlayers array
                        var gameRef = db.Collection("games").Document(gameID);
                        transaction.Set(gameRef, new Dictionary<string, object>
                        {
                            { "hostUID", uid },
                            { "status", "lobby" },
                            { "readyPlayers", new List<string>() },
                            { "createdAt", FieldValue.ServerTimestamp }
                        });
                    });
                });
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.Log($"[MatchmakingService] Match transaction failed (race condition expected): {task.Exception?.InnerException?.Message}");
                    // Will retry on next poll
                }
                // Match result comes via the listener, not from here
            });
#endif
        }

        // ================================================================
        // Listen for Match (on own document)
        // ================================================================

        private void StartListeningForMatch()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            StopListeningForMatch();

            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid)) return;

            matchListener = db.Collection("matchmaking").Document(uid)
                .Listen(snapshot =>
                {
                    if (!snapshot.Exists || IsMatched) return;

                    var data = snapshot.ToDictionary();
                    string status = data.ContainsKey("status") ? data["status"] as string : null;
                    if (status != "matched") return;

                    string gameID = data.ContainsKey("matchedGameID") ? data["matchedGameID"] as string : null;
                    string oppUID = data.ContainsKey("matchedOpponentUID") ? data["matchedOpponentUID"] as string : null;
                    if (string.IsNullOrEmpty(gameID) || string.IsNullOrEmpty(oppUID)) return;

                    // Determine if we are host: check game doc's hostUID
                    db.Collection("games").Document(gameID).GetSnapshotAsync().ContinueWithOnMainThread(gameTask =>
                    {
                        if (gameTask.IsFaulted || !gameTask.Result.Exists) return;

                        var gameData = gameTask.Result.ToDictionary();
                        string hostUID = gameData.ContainsKey("hostUID") ? gameData["hostUID"] as string : null;
                        bool isHost = hostUID == uid;

                        // Read opponent data from their matchmaking doc
                        db.Collection("matchmaking").Document(oppUID).GetSnapshotAsync().ContinueWithOnMainThread(oppTask =>
                        {
                            string oppName = "Opponent";
                            string oppFaction = "Morel";
                            string oppPlayerID = Guid.NewGuid().ToString();

                            if (!oppTask.IsFaulted && oppTask.Result.Exists)
                            {
                                var oppData = oppTask.Result.ToDictionary();
                                if (oppData.ContainsKey("displayName")) oppName = oppData["displayName"] as string ?? oppName;
                                if (oppData.ContainsKey("faction")) oppFaction = oppData["faction"] as string ?? oppFaction;
                                if (oppData.ContainsKey("playerID")) oppPlayerID = oppData["playerID"] as string ?? oppPlayerID;
                            }

                            var result = new MatchResult
                            {
                                gameID = gameID,
                                isHost = isHost,
                                opponentUID = oppUID,
                                opponentDisplayName = oppName,
                                opponentFaction = oppFaction,
                                opponentPlayerID = oppPlayerID,
                                localFaction = queuedFaction,
                                localPlayerID = queuedPlayerID
                            };

                            IsMatched = true;
                            IsInQueue = false;
                            CurrentMatch = result;

                            Debug.Log($"[MatchmakingService] Match found! Game: {gameID}, Host: {isHost}, Opponent: {oppName}");

                            OnMatchFound?.Invoke(result);
                        });
                    });
                });
#endif
        }

        private void StopListeningForMatch()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            matchListener?.Stop();
            matchListener = null;
#endif
        }

        // ================================================================
        // Ready-Up
        // ================================================================

        public void ConfirmReady(string gameID, Action<bool> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(false);
                return;
            }

            db.Collection("games").Document(gameID).UpdateAsync(
                new Dictionary<string, object>
                {
                    { "readyPlayers", FieldValue.ArrayUnion(uid) }
                }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[MatchmakingService] Failed to confirm ready: {task.Exception?.InnerException?.Message}");
                }
                callback?.Invoke(!task.IsFaulted);
            });
#else
            callback?.Invoke(false);
#endif
        }

        public void ListenForReady(string gameID, Action<List<string>> onChanged)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            StopListeningForReady();

            readyListener = db.Collection("games").Document(gameID)
                .Listen(snapshot =>
                {
                    if (!snapshot.Exists) return;

                    var data = snapshot.ToDictionary();
                    var readyPlayers = new List<string>();

                    if (data.ContainsKey("readyPlayers") && data["readyPlayers"] is List<object> readyList)
                    {
                        foreach (var item in readyList)
                        {
                            if (item is string s)
                                readyPlayers.Add(s);
                        }
                    }

                    onChanged?.Invoke(readyPlayers);
                    OnReadyPlayersChanged?.Invoke(readyPlayers);
                });
#endif
        }

        public void StopListeningForReady()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            readyListener?.Stop();
            readyListener = null;
#endif
        }

        // ================================================================
        // Cleanup Stale Entries
        // ================================================================

        private void CleanupStaleEntries()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            // Delete queue entries older than StaleEntryAge seconds
            // Best-effort: we query waiting entries and check their enqueuedAt
            db.Collection("matchmaking")
                .WhereEqualTo("status", "waiting")
                .Limit(20)
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) return;

                foreach (var doc in task.Result.Documents)
                {
                    var data = doc.ToDictionary();
                    if (data.ContainsKey("enqueuedAt") && data["enqueuedAt"] is DateTime enqueuedAt)
                    {
                        if ((DateTime.UtcNow - enqueuedAt).TotalSeconds > StaleEntryAge)
                        {
                            doc.Reference.DeleteAsync();
                            Debug.Log($"[MatchmakingService] Cleaned stale entry: {doc.Id}");
                        }
                    }
                }
            });
#endif
        }

        // ================================================================
        // Full Cleanup (call on app quit / disconnect)
        // ================================================================

        public void Cleanup()
        {
            if (IsInQueue || IsMatched)
            {
                LeaveQueue(null);
            }
            StopListeningForMatch();
            StopListeningForReady();
        }
    }
}
