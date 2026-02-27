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
        private const int SnapshotCommandInterval = 100;
        private const double SnapshotTimeIntervalSeconds = 300.0;
        private const int MaxSnapshots = 3;

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

            var cmdRef = db.Collection("games").Document(gameID)
                .Collection("commands").Document(command.commandID);

            cmdRef.SetAsync(command.ToDictionary()).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[GameSessionService] Failed to write command: {task.Exception}");
                    callback?.Invoke(false, task.Exception?.InnerException?.Message);
                    return;
                }

                // Update sequence on game document
                db.Collection("games").Document(gameID).UpdateAsync(
                    new Dictionary<string, object>
                    {
                        { "currentCommandSequence", command.sequence }
                    });

                commandsSinceSnapshot++;
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

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
                    });

                commandsSinceSnapshot = 0;
                lastSnapshotTime = DateTime.UtcNow;

                PruneSnapshots(gameID, MaxSnapshots);

                Debug.Log($"[GameSessionService] Snapshot created: {snapshot.snapshotID}");
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        public bool ShouldCreateSnapshot()
        {
            return commandsSinceSnapshot >= SnapshotCommandInterval ||
                   (DateTime.UtcNow - lastSnapshotTime).TotalSeconds >= SnapshotTimeIntervalSeconds;
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
                        doc.Reference.DeleteAsync();
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

                sessions.Sort((a, b) => string.Compare(b.createdAt, a.createdAt, StringComparison.Ordinal));
                callback?.Invoke(sessions);
            });
#else
            callback?.Invoke(new List<GameSession>());
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
