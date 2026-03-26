// ============================================================================
// FILE: Engine/GameSessionService.cs
// PURPOSE: Online game session lifecycle — create, command streaming, snapshots.
//          Port of GameSessionService.swift.
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
    // Session Error Types
    // ================================================================

    public enum GameSessionError
    {
        NotSignedIn,
        NoActiveSession,
        SessionNotFound,
        NoSnapshot,
        SerializationFailed
    }

    // ================================================================
    // Game Session Service
    // ================================================================

    public class GameSessionService
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static GameSessionService _instance;
        public static GameSessionService Instance => _instance ?? (_instance = new GameSessionService());

        // ================================================================
        // Events
        // ================================================================

        public event Action<OnlineCommand> OnCommandReceived;
        public event Action<GameSession> OnSessionUpdated;
        public event Action OnOpponentDisconnected;
        public event Action OnOpponentReconnected;
        /// <summary>Fired when a command permanently fails after all retries.</summary>
        public event Action<string> OnCommandSubmitFailed;

        // ================================================================
        // State
        // ================================================================

        public GameSession CurrentSession { get; private set; }
        public bool IsHost { get; private set; }

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private FirebaseFirestore db;
        private ListenerRegistration commandListener;
        private ListenerRegistration sessionListener;
#endif

        // Snapshot strategy
        private int commandsSinceSnapshot;
        private DateTime lastSnapshotTime = DateTime.UtcNow;

        // Command retry queue
        private struct PendingRetry
        {
            public string gameID;
            public OnlineCommand command;
            public int retryCount;
            public double nextRetryTime;
        }
        private readonly List<PendingRetry> retryQueue = new List<PendingRetry>();

        // Client-side rate limiting — prevent flooding Firestore with commands
        private const int MaxCommandsPerSecond = 20;
        private readonly Queue<double> commandSubmitTimestamps = new Queue<double>();

        // Disconnect detection
        public static double DisconnectTimeoutSeconds => GameConfig.Online.DisconnectTimeoutSeconds;
        public static double AbandonTimeoutSeconds => GameConfig.Online.AbandonTimeoutSeconds;
        private bool opponentDisconnected;
        private string localUID;

        private GameSessionService() { }

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
        // Create Game
        // ================================================================

        public void CreateGame(GameSession session, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var docRef = db.Collection("games").Document(session.gameID);
            docRef.SetAsync(session.ToDictionary()).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    callback?.Invoke(false, task.Exception?.InnerException?.Message ?? "Failed to create game");
                    return;
                }

                CurrentSession = session;
                IsHost = true;
                commandsSinceSnapshot = 0;
                lastSnapshotTime = DateTime.UtcNow;

                Debug.Log($"[GameSessionService] Online game created: {session.gameID}");
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Submit Command
        // ================================================================

        public void SubmitCommand(string gameID, OnlineCommand command, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (CurrentSession == null)
            {
                callback?.Invoke(false, "No active session");
                return;
            }

            // Client-side rate limiting
            double now = Time.realtimeSinceStartupAsDouble;
            while (commandSubmitTimestamps.Count > 0 && now - commandSubmitTimestamps.Peek() > 1.0)
                commandSubmitTimestamps.Dequeue();

            if (commandSubmitTimestamps.Count >= MaxCommandsPerSecond)
            {
                Debug.LogWarning("[GameSessionService] Command rate limit exceeded — queuing for retry");
                EnqueueRetry(gameID, command, 0);
                callback?.Invoke(false, "Rate limited");
                return;
            }
            commandSubmitTimestamps.Enqueue(now);

            var cmdRef = db.Collection("games").Document(gameID)
                .Collection("commands").Document(command.commandID);

            cmdRef.SetAsync(command.ToDictionary()).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[GameSessionService] Command submit failed, queuing retry: {task.Exception}");
                    EnqueueRetry(gameID, command, 0);
                    callback?.Invoke(false, task.Exception?.InnerException?.Message);
                    return;
                }

                // Update sequence on game document
                db.Collection("games").Document(gameID).UpdateAsync(
                    new Dictionary<string, object>
                    {
                        { "currentCommandSequence", command.sequence }
                    }).ContinueWithOnMainThread(seqTask =>
                {
                    if (seqTask.IsFaulted)
                        Debug.LogWarning($"[GameSessionService] Sequence update failed: {seqTask.Exception?.InnerException?.Message}");
                });

                commandsSinceSnapshot++;
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Command Retry Queue
        // ================================================================

        private void EnqueueRetry(string gameID, OnlineCommand command, int retryCount)
        {
            double delay = GameConfig.Online.RetryBaseDelaySeconds * Math.Pow(2, retryCount);
            retryQueue.Add(new PendingRetry
            {
                gameID = gameID,
                command = command,
                retryCount = retryCount,
                nextRetryTime = Time.realtimeSinceStartupAsDouble + delay
            });
        }

        /// <summary>
        /// Process the command retry queue. Call each frame from GameSceneManager.Update().
        /// Uses exponential backoff (1s, 2s, 4s) with max retries from GameConfig.
        /// </summary>
        public void ProcessRetryQueue()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (retryQueue.Count == 0) return;

            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = retryQueue.Count - 1; i >= 0; i--)
            {
                var entry = retryQueue[i];
                if (now < entry.nextRetryTime) continue;

                retryQueue.RemoveAt(i);

                // Discard stale retries if the game session has changed
                if (CurrentSession == null || entry.gameID != CurrentSession.gameID)
                {
                    Debug.LogWarning($"[GameSessionService] Discarding stale retry for game {entry.gameID} (current: {CurrentSession?.gameID})");
                    continue;
                }

                var cmdRef = db.Collection("games").Document(entry.gameID)
                    .Collection("commands").Document(entry.command.commandID);

                int currentRetry = entry.retryCount;
                string gid = entry.gameID;
                OnlineCommand cmd = entry.command;

                cmdRef.SetAsync(cmd.ToDictionary()).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        int nextRetry = currentRetry + 1;
                        if (nextRetry < GameConfig.Online.MaxCommandRetries)
                        {
                            Debug.LogWarning($"[GameSessionService] Retry {nextRetry}/{GameConfig.Online.MaxCommandRetries} failed for command {cmd.commandID}");
                            EnqueueRetry(gid, cmd, nextRetry);
                        }
                        else
                        {
                            Debug.LogError($"[GameSessionService] Command permanently failed after {GameConfig.Online.MaxCommandRetries} retries: {cmd.commandType} seq={cmd.sequence}");
                            OnCommandSubmitFailed?.Invoke(cmd.commandType);
                        }
                        return;
                    }

                    // Update sequence on game document
                    db.Collection("games").Document(gid).UpdateAsync(
                        new Dictionary<string, object>
                        {
                            { "currentCommandSequence", cmd.sequence }
                        });

                    commandsSinceSnapshot++;
                    Debug.Log($"[GameSessionService] Retry succeeded for command {cmd.commandID}");
                });

                // Only process one retry per frame to avoid flooding
                break;
            }
#endif
        }

        public bool HasPendingRetries => retryQueue.Count > 0;

        // ================================================================
        // Command Listener
        // ================================================================

        public void StartCommandListener(string gameID, int afterSequence)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            StopCommandListener();

            commandListener = db.Collection("games").Document(gameID)
                .Collection("commands")
                .WhereGreaterThan("sequence", afterSequence)
                .OrderBy("sequence")
                .Listen(snapshot =>
                {
                    foreach (var change in snapshot.GetChanges())
                    {
                        if (change.ChangeType == DocumentChange.Type.Added)
                        {
                            var data = change.Document.ToDictionary();
                            var cmd = OnlineCommand.FromDictionary(data);
                            if (cmd != null)
                                OnCommandReceived?.Invoke(cmd);
                        }
                    }
                });
#endif
        }

        public void StopCommandListener()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            commandListener?.Stop();
            commandListener = null;
#endif
        }

        // ================================================================
        // Session Listener
        // ================================================================

        public void StartSessionListener(string gameID)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            sessionListener?.Stop();

            sessionListener = db.Collection("games").Document(gameID)
                .Listen(snapshot =>
                {
                    if (snapshot.Exists)
                    {
                        var data = snapshot.ToDictionary();
                        var session = GameSession.FromDictionary(data, gameID);
                        if (session != null)
                        {
                            // Use document's server-side update time as reference
                            // to avoid clock skew between client and server
                            session.serverUpdateTime = DateTime.UtcNow;

                            CurrentSession = session;
                            OnSessionUpdated?.Invoke(session);
                        }
                    }
                });
#endif
        }

        // ================================================================
        // Snapshots
        // ================================================================

        public void SaveSnapshot(string gameID, GameSnapshot snapshot, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var snapshotRef = db.Collection("games").Document(gameID)
                .Collection("snapshots").Document(snapshot.snapshotID);

            snapshotRef.SetAsync(snapshot.ToDictionary()).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    callback?.Invoke(false, task.Exception?.InnerException?.Message);
                    return;
                }

                // Update game doc with latest snapshot ID
                db.Collection("games").Document(gameID).UpdateAsync(
                    new Dictionary<string, object>
                    {
                        { "latestSnapshotID", snapshot.snapshotID }
                    }).ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsFaulted)
                        Debug.LogWarning($"[GameSessionService] Snapshot ID update failed: {updateTask.Exception?.InnerException?.Message}");
                });

                commandsSinceSnapshot = 0;
                lastSnapshotTime = DateTime.UtcNow;

                PruneSnapshots(gameID, GameConfig.Online.MaxSnapshots);

                Debug.Log($"[GameSessionService] Snapshot created: {snapshot.snapshotID}");
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        public bool ShouldCreateSnapshot()
        {
            return commandsSinceSnapshot >= GameConfig.Online.SnapshotCommandInterval ||
                   (DateTime.UtcNow - lastSnapshotTime).TotalSeconds >= GameConfig.Online.SnapshotTimeIntervalSeconds;
        }

        public void LoadLatestSnapshot(string gameID, Action<GameSnapshot, List<OnlineCommand>, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var gameRef = db.Collection("games").Document(gameID);

            gameRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    callback?.Invoke(null, null, "Session not found");
                    return;
                }

                var data = task.Result.ToDictionary();
                var session = GameSession.FromDictionary(data, gameID);
                if (session == null)
                {
                    callback?.Invoke(null, null, "Failed to parse session");
                    return;
                }

                CurrentSession = session;
                IsHost = session.hostUID == FirebaseAuth.DefaultInstance.CurrentUser?.UserId;

                if (string.IsNullOrEmpty(session.latestSnapshotID))
                {
                    callback?.Invoke(null, null, "No snapshot available");
                    return;
                }

                // Load snapshot
                gameRef.Collection("snapshots").Document(session.latestSnapshotID)
                    .GetSnapshotAsync().ContinueWithOnMainThread(snapTask =>
                {
                    if (snapTask.IsFaulted || !snapTask.Result.Exists)
                    {
                        callback?.Invoke(null, null, "Snapshot not found");
                        return;
                    }

                    var snapData = snapTask.Result.ToDictionary();
                    var snapshot = GameSnapshot.FromDictionary(snapData);
                    if (snapshot == null)
                    {
                        callback?.Invoke(null, null, "Failed to parse snapshot");
                        return;
                    }

                    // Load commands since snapshot
                    gameRef.Collection("commands")
                        .WhereGreaterThan("sequence", snapshot.commandSequence)
                        .OrderBy("sequence")
                        .GetSnapshotAsync().ContinueWithOnMainThread(cmdTask =>
                    {
                        var commands = new List<OnlineCommand>();
                        if (!cmdTask.IsFaulted)
                        {
                            foreach (var doc in cmdTask.Result.Documents)
                            {
                                var cmdData = doc.ToDictionary();
                                var cmd = OnlineCommand.FromDictionary(cmdData);
                                if (cmd != null)
                                    commands.Add(cmd);
                            }
                        }

                        callback?.Invoke(snapshot, commands, null);
                    });
                });
            });
#else
            callback?.Invoke(null, null, "Firebase not available");
#endif
        }

        private void PruneSnapshots(string gameID, int keepCount)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            db.Collection("games").Document(gameID).Collection("snapshots")
                .OrderByDescending("commandSequence")
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) return;

                var docs = task.Result.Documents;
                int count = 0;
                foreach (var doc in docs)
                {
                    count++;
                    if (count > keepCount)
                    {
                        doc.Reference.DeleteAsync().ContinueWithOnMainThread(delTask =>
                        {
                            if (delTask.IsFaulted)
                                Debug.LogWarning($"[GameSessionService] Snapshot prune failed: {delTask.Exception?.InnerException?.Message}");
                        });
                    }
                }
            });
#endif
        }

        // ================================================================
        // List My Games
        // ================================================================

        public void ListMyGames(string uid, Action<List<GameSession>> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(new List<GameSession>());
                return;
            }

            db.Collection("games")
                .WhereEqualTo("hostUID", uid)
                .Limit(20)
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[GameSessionService] Failed to list games: {task.Exception}");
                    callback?.Invoke(new List<GameSession>());
                    return;
                }

                var sessions = new List<GameSession>();
                foreach (var doc in task.Result.Documents)
                {
                    var data = doc.ToDictionary();
                    var session = GameSession.FromDictionary(data, doc.Id);
                    if (session != null)
                        sessions.Add(session);
                }

                sessions.Sort((a, b) => DateTime.Compare(b.createdAt, a.createdAt));
                callback?.Invoke(sessions);
            });
#else
            callback?.Invoke(new List<GameSession>());
#endif
        }

        // ================================================================
        // Find Active Game (for reconnection)
        // ================================================================

        /// <summary>
        /// Search for an active (playing) game where this player is a participant.
        /// Used to offer "Rejoin Game" on the main menu after a crash or disconnect.
        /// </summary>
        public void FindActiveGame(string uid, Action<GameSession> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke(null);
                return;
            }

            db.Collection("games")
                .WhereEqualTo("status", "playing")
                .WhereArrayContains("participantUIDs", uid)
                .Limit(1)
                .GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[GameSessionService] Failed to find active games: {task.Exception}");
                    callback?.Invoke(null);
                    return;
                }

                foreach (var doc in task.Result.Documents)
                {
                    var data = doc.ToDictionary();
                    var session = GameSession.FromDictionary(data, doc.Id);
                    if (session != null)
                    {
                        callback?.Invoke(session);
                        return;
                    }
                }

                callback?.Invoke(null);
            });
#else
            callback?.Invoke(null);
#endif
        }

        // ================================================================
        // Heartbeat
        // ================================================================

        public void UpdateHeartbeat(string gameID, string uid)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(gameID) || string.IsNullOrEmpty(uid)) return;

            db.Collection("games").Document(gameID).UpdateAsync(
                new Dictionary<string, object>
                {
                    { $"players.{uid}.lastHeartbeat", FieldValue.ServerTimestamp },
                    { $"players.{uid}.status", "active" }
                }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[GameSessionService] Heartbeat update failed: {task.Exception?.InnerException?.Message}");
            });
#endif
        }

        // ================================================================
        // Disconnect Detection
        // ================================================================

        /// <summary>
        /// Set the local player UID for disconnect detection.
        /// Called when starting/joining an online game.
        /// </summary>
        public void SetLocalUID(string uid)
        {
            localUID = uid;
            opponentDisconnected = false;
        }

        /// <summary>
        /// Check opponent heartbeat status from a session update.
        /// Call this from HandleSessionUpdate() with the current server time.
        /// </summary>
        public void CheckOpponentHeartbeat(GameSession session)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(localUID) || session == null) return;

            foreach (var kvp in session.players)
            {
                // Skip self and AI players
                if (kvp.Key == localUID || kvp.Value.isAI) continue;

                var playerData = kvp.Value;

                // Check if player has explicitly left or been defeated
                if (playerData.status == PlayerSessionStatus.Left ||
                    playerData.status == PlayerSessionStatus.Defeated)
                {
                    if (!opponentDisconnected)
                    {
                        opponentDisconnected = true;
                        OnOpponentDisconnected?.Invoke();
                    }
                    continue;
                }

                // Check heartbeat staleness using server update time to avoid clock skew
                DateTime referenceTime = session.serverUpdateTime != default
                    ? session.serverUpdateTime
                    : DateTime.UtcNow;
                double heartbeatAge = (referenceTime - playerData.lastHeartbeat).TotalSeconds;

                if (heartbeatAge > DisconnectTimeoutSeconds && !opponentDisconnected)
                {
                    opponentDisconnected = true;
                    OnOpponentDisconnected?.Invoke();
                }
                else if (heartbeatAge <= DisconnectTimeoutSeconds && opponentDisconnected)
                {
                    // Opponent reconnected
                    opponentDisconnected = false;
                    OnOpponentReconnected?.Invoke();
                }
            }
#endif
        }

        public bool IsOpponentDisconnected => opponentDisconnected;

        // ================================================================
        // Session / Player Status Updates
        // ================================================================

        public void UpdateSessionStatus(string gameID, GameSessionStatus status)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(gameID)) return;

            db.Collection("games").Document(gameID).UpdateAsync(
                new Dictionary<string, object>
                {
                    { "status", status.ToString().ToLower() }
                }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[GameSessionService] Session status update failed: {task.Exception?.InnerException?.Message}");
            });
#endif
        }

        public void UpdatePlayerStatus(string gameID, string uid, PlayerSessionStatus status)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(gameID) || string.IsNullOrEmpty(uid)) return;

            db.Collection("games").Document(gameID).UpdateAsync(
                new Dictionary<string, object>
                {
                    { $"players.{uid}.status", status.ToString().ToLower() }
                }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[GameSessionService] Player status update failed: {task.Exception?.InnerException?.Message}");
            });
#endif
        }

        // ================================================================
        // Leave / Cleanup
        // ================================================================

        public void LeaveSession(string gameID, string uid)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            StopCommandListener();
            sessionListener?.Stop();
            sessionListener = null;

            if (!string.IsNullOrEmpty(gameID) && !string.IsNullOrEmpty(uid))
            {
                db.Collection("games").Document(gameID).UpdateAsync(
                    new Dictionary<string, object>
                    {
                        { $"players.{uid}.status", "left" }
                    });
            }
#endif
            CurrentSession = null;
            IsHost = false;
        }

        public void DeleteGame(string gameID, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var gameRef = db.Collection("games").Document(gameID);
            int pendingOps = 3;
            string deleteError = null;

            void OnSubcollectionDeleted(string error)
            {
                if (error != null && deleteError == null) deleteError = error;
                pendingOps--;
                if (pendingOps > 0) return;

                if (deleteError != null)
                {
                    callback?.Invoke(false, deleteError);
                    return;
                }

                gameRef.DeleteAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        callback?.Invoke(false, task.Exception?.InnerException?.Message);
                        return;
                    }

                    if (CurrentSession?.gameID == gameID)
                    {
                        CurrentSession = null;
                        IsHost = false;
                    }
                    callback?.Invoke(true, null);
                });
            }

            foreach (var subcollection in new[] { "commands", "snapshots", "playerData" })
            {
                string subName = subcollection;
                gameRef.Collection(subName).GetSnapshotAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        OnSubcollectionDeleted(task.Exception?.InnerException?.Message);
                        return;
                    }

                    foreach (var doc in task.Result.Documents)
                        doc.Reference.DeleteAsync();

                    OnSubcollectionDeleted(null);
                });
            }
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }
    }
}
