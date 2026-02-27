// ============================================================================
// FILE: Engine/AuthService.cs
// PURPOSE: Firebase Auth wrapper — email/password sign-in, username claiming,
//          account management. Port of AuthService.swift (email/password only).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

namespace Sporefront.Engine
{
    // ================================================================
    // Auth State
    // ================================================================

    public enum AuthState
    {
        Unknown,
        SignedOut,
        SignedIn,
        NeedsUsername
    }

    // ================================================================
    // Auth Service
    // ================================================================

    public class AuthService
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static AuthService _instance;
        public static AuthService Instance => _instance ?? (_instance = new AuthService());

        // ================================================================
        // Events
        // ================================================================

        public event Action<AuthState> OnAuthStateChanged;
        public event Action OnUsernameChanged;

        // ================================================================
        // State
        // ================================================================

        public AuthState CurrentState { get; private set; } = AuthState.Unknown;
        public string CurrentUID { get; private set; }
        public string CurrentEmail { get; private set; }
        public string CurrentDisplayName { get; private set; }

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private FirebaseAuth auth;
        private FirebaseFirestore db;
#endif

        // ================================================================
        // Initialization
        // ================================================================

        private AuthService() { }

        public void Initialize()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;

            auth.StateChanged += OnFirebaseAuthStateChanged;

            // Sync current user
            if (auth.CurrentUser != null)
            {
                CurrentUID = auth.CurrentUser.UserId;
                CurrentEmail = auth.CurrentUser.Email;
                CurrentDisplayName = auth.CurrentUser.DisplayName;
            }
            else
            {
                SetState(AuthState.SignedOut);
            }
#else
            Debug.LogWarning("[AuthService] Firebase not available — offline mode");
            SetState(AuthState.SignedOut);
#endif
        }

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private void OnFirebaseAuthStateChanged(object sender, EventArgs e)
        {
            var user = auth.CurrentUser;
            if (user != null)
            {
                CurrentUID = user.UserId;
                CurrentEmail = user.Email;
                CurrentDisplayName = user.DisplayName;
                // Check if username exists
                CheckHasUsername((hasUsername) =>
                {
                    SetState(hasUsername ? AuthState.SignedIn : AuthState.NeedsUsername);
                });
            }
            else
            {
                CurrentUID = null;
                CurrentEmail = null;
                CurrentDisplayName = null;
                SetState(AuthState.SignedOut);
            }
        }
#endif

        private void SetState(AuthState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnAuthStateChanged?.Invoke(newState);
        }

        // ================================================================
        // Sign Up
        // ================================================================

        public void SignUp(string email, string password, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    string error = GetFirebaseErrorMessage(task.Exception);
                    callback?.Invoke(false, error);
                    return;
                }
                var user = task.Result.User;
                CurrentUID = user.UserId;
                CurrentEmail = user.Email;
                SetState(AuthState.NeedsUsername);
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Sign In
        // ================================================================

        public void SignIn(string email, string password, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    string error = GetFirebaseErrorMessage(task.Exception);
                    callback?.Invoke(false, error);
                    return;
                }
                var user = task.Result.User;
                CurrentUID = user.UserId;
                CurrentEmail = user.Email;
                CurrentDisplayName = user.DisplayName;

                // Check if username exists
                CheckHasUsername((hasUsername) =>
                {
                    SetState(hasUsername ? AuthState.SignedIn : AuthState.NeedsUsername);
                    callback?.Invoke(true, null);
                });
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Sign Out
        // ================================================================

        public void SignOut()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            auth.SignOut();
#endif
            CurrentUID = null;
            CurrentEmail = null;
            CurrentDisplayName = null;
            SetState(AuthState.SignedOut);
        }

        // ================================================================
        // Password Reset
        // ================================================================

        public void SendPasswordReset(string email, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    string error = GetFirebaseErrorMessage(task.Exception);
                    callback?.Invoke(false, error);
                    return;
                }
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Change Password
        // ================================================================

        public void ChangePassword(string currentPassword, string newPassword, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var user = auth.CurrentUser;
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                callback?.Invoke(false, "Not signed in");
                return;
            }

            var credential = EmailAuthProvider.GetCredential(user.Email, currentPassword);
            user.ReauthenticateAsync(credential).ContinueWithOnMainThread(reauthTask =>
            {
                if (reauthTask.IsFaulted || reauthTask.IsCanceled)
                {
                    string error = GetFirebaseErrorMessage(reauthTask.Exception);
                    callback?.Invoke(false, error);
                    return;
                }

                user.UpdatePasswordAsync(newPassword).ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsFaulted || updateTask.IsCanceled)
                    {
                        string error = GetFirebaseErrorMessage(updateTask.Exception);
                        callback?.Invoke(false, error);
                        return;
                    }
                    callback?.Invoke(true, null);
                });
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Username System
        // ================================================================

        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,20}$");
        }

        public void CheckHasUsername(Action<bool> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(CurrentUID))
            {
                callback?.Invoke(false);
                return;
            }

            // Fast path: check PlayerPrefs cache
            string key = $"hasUsername_{CurrentUID}";
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                LoadUsername(null);
                callback?.Invoke(true);
                return;
            }

            // Fallback: check Firestore
            db.Collection("users").Document(CurrentUID).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    callback?.Invoke(false);
                    return;
                }

                var data = task.Result.ToDictionary();
                if (data.ContainsKey("username") && data["username"] is string username)
                {
                    CurrentDisplayName = username;
                    PlayerPrefs.SetInt(key, 1);
                    PlayerPrefs.Save();
                    callback?.Invoke(true);
                }
                else
                {
                    callback?.Invoke(false);
                }
            });
#else
            callback?.Invoke(false);
#endif
        }

        public void LoadUsername(Action<string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(CurrentUID))
            {
                callback?.Invoke(null);
                return;
            }

            db.Collection("users").Document(CurrentUID).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    callback?.Invoke(null);
                    return;
                }

                var data = task.Result.ToDictionary();
                if (data.ContainsKey("username") && data["username"] is string username)
                {
                    CurrentDisplayName = username;
                    callback?.Invoke(username);
                }
                else
                {
                    callback?.Invoke(null);
                }
            });
#else
            callback?.Invoke(null);
#endif
        }

        public void CheckUsernameAvailability(string username, Action<bool> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            string lowered = username.ToLowerInvariant();
            db.Collection("usernames").Document(lowered).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    callback?.Invoke(false);
                    return;
                }
                callback?.Invoke(!task.Result.Exists);
            });
#else
            callback?.Invoke(false);
#endif
        }

        public void ClaimUsername(string username, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(CurrentUID))
            {
                callback?.Invoke(false, "Not signed in");
                return;
            }

            string lowered = username.ToLowerInvariant();
            var usernameDocRef = db.Collection("usernames").Document(lowered);
            var userDocRef = db.Collection("users").Document(CurrentUID);

            db.RunTransactionAsync(transaction =>
            {
                return transaction.GetSnapshotAsync(usernameDocRef).ContinueWithOnMainThread(task =>
                {
                    if (task.Result.Exists)
                        throw new InvalidOperationException("Username is already taken.");

                    transaction.Set(usernameDocRef, new Dictionary<string, object>
                    {
                        { "uid", CurrentUID },
                        { "originalCase", username },
                        { "createdAt", FieldValue.ServerTimestamp }
                    });

                    transaction.Set(userDocRef, new Dictionary<string, object>
                    {
                        { "username", username },
                        { "usernameLower", lowered },
                        { "usernameSetAt", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
                });
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    string error = task.Exception?.InnerException?.Message ?? "Failed to claim username";
                    callback?.Invoke(false, error);
                    return;
                }

                CurrentDisplayName = username;
                PlayerPrefs.SetInt($"hasUsername_{CurrentUID}", 1);
                PlayerPrefs.Save();

                // Update Firebase Auth display name
                UpdateFirebaseDisplayName(username);

                SetState(AuthState.SignedIn);
                OnUsernameChanged?.Invoke();
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        public void ChangeUsername(string newUsername, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (string.IsNullOrEmpty(CurrentUID))
            {
                callback?.Invoke(false, "Not signed in");
                return;
            }

            string newLowered = newUsername.ToLowerInvariant();
            var newUsernameDocRef = db.Collection("usernames").Document(newLowered);
            var userDocRef = db.Collection("users").Document(CurrentUID);

            db.RunTransactionAsync(transaction =>
            {
                return transaction.GetSnapshotAsync(userDocRef).ContinueWithOnMainThread(userTask =>
                {
                    var userData = userTask.Result.ToDictionary();
                    string oldUsernameLower = userData.ContainsKey("usernameLower")
                        ? userData["usernameLower"] as string : null;

                    return transaction.GetSnapshotAsync(newUsernameDocRef).ContinueWithOnMainThread(nameTask =>
                    {
                        if (nameTask.Result.Exists)
                            throw new InvalidOperationException("Username is already taken.");

                        // Delete old username doc
                        if (!string.IsNullOrEmpty(oldUsernameLower))
                        {
                            var oldUsernameDocRef = db.Collection("usernames").Document(oldUsernameLower);
                            transaction.Delete(oldUsernameDocRef);
                        }

                        // Claim new username
                        transaction.Set(newUsernameDocRef, new Dictionary<string, object>
                        {
                            { "uid", CurrentUID },
                            { "originalCase", newUsername },
                            { "createdAt", FieldValue.ServerTimestamp }
                        });

                        // Update user profile
                        transaction.Set(userDocRef, new Dictionary<string, object>
                        {
                            { "username", newUsername },
                            { "usernameLower", newLowered },
                            { "usernameSetAt", FieldValue.ServerTimestamp }
                        }, SetOptions.MergeAll);
                    });
                });
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    string error = task.Exception?.InnerException?.Message ?? "Failed to change username";
                    callback?.Invoke(false, error);
                    return;
                }

                CurrentDisplayName = newUsername;
                UpdateFirebaseDisplayName(newUsername);
                OnUsernameChanged?.Invoke();
                callback?.Invoke(true, null);
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Delete Account
        // ================================================================

        public void DeleteAccount(string currentPassword, Action<bool, string> callback)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var user = auth.CurrentUser;
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                callback?.Invoke(false, "Not signed in");
                return;
            }

            string uid = user.UserId;
            var credential = EmailAuthProvider.GetCredential(user.Email, currentPassword);

            // Step 1: Re-authenticate
            user.ReauthenticateAsync(credential).ContinueWithOnMainThread(reauthTask =>
            {
                if (reauthTask.IsFaulted || reauthTask.IsCanceled)
                {
                    string error = GetFirebaseErrorMessage(reauthTask.Exception);
                    callback?.Invoke(false, error);
                    return;
                }

                // Step 2: Release username
                ReleaseUsername(uid, () =>
                {
                    // Step 3: Delete user data
                    DeleteAllUserData(uid, () =>
                    {
                        // Step 4: Delete Firebase Auth account
                        user.DeleteAsync().ContinueWithOnMainThread(deleteTask =>
                        {
                            if (deleteTask.IsFaulted || deleteTask.IsCanceled)
                            {
                                string error = GetFirebaseErrorMessage(deleteTask.Exception);
                                callback?.Invoke(false, error);
                                return;
                            }

                            // Step 5: Clear local state
                            CurrentUID = null;
                            CurrentEmail = null;
                            CurrentDisplayName = null;
                            PlayerPrefs.DeleteKey($"hasUsername_{uid}");
                            PlayerPrefs.Save();
                            SetState(AuthState.SignedOut);
                            callback?.Invoke(true, null);
                        });
                    });
                });
            });
#else
            callback?.Invoke(false, "Firebase not available");
#endif
        }

        // ================================================================
        // Private Helpers
        // ================================================================

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        private void UpdateFirebaseDisplayName(string displayName)
        {
            var user = auth.CurrentUser;
            if (user == null) return;

            var profile = user.UserProfile;
            profile.DisplayName = displayName;
            user.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning($"[AuthService] Failed to update display name: {task.Exception}");
            });
        }

        private void ReleaseUsername(string uid, Action onComplete)
        {
            db.Collection("users").Document(uid).GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists)
                {
                    onComplete?.Invoke();
                    return;
                }

                var data = task.Result.ToDictionary();
                if (!data.ContainsKey("usernameLower") || !(data["usernameLower"] is string usernameLower))
                {
                    onComplete?.Invoke();
                    return;
                }

                var batch = db.StartBatch();
                batch.Delete(db.Collection("usernames").Document(usernameLower));
                batch.Update(db.Collection("users").Document(uid), new Dictionary<string, object>
                {
                    { "username", FieldValue.Delete },
                    { "usernameLower", FieldValue.Delete },
                    { "usernameSetAt", FieldValue.Delete }
                });
                batch.CommitAsync().ContinueWithOnMainThread(commitTask =>
                {
                    if (commitTask.IsFaulted)
                        Debug.LogWarning($"[AuthService] Failed to release username: {commitTask.Exception}");
                    onComplete?.Invoke();
                });
            });
        }

        private void DeleteAllUserData(string uid, Action onComplete)
        {
            var userDocRef = db.Collection("users").Document(uid);
            int pendingOps = 3;

            void OnOpComplete()
            {
                pendingOps--;
                if (pendingOps <= 0)
                {
                    // Delete user document itself
                    userDocRef.DeleteAsync().ContinueWithOnMainThread(task =>
                    {
                        if (task.IsFaulted)
                            Debug.LogWarning($"[AuthService] Failed to delete user doc: {task.Exception}");
                        onComplete?.Invoke();
                    });
                }
            }

            DeleteSubcollection(userDocRef, "saves", OnOpComplete);
            DeleteSubcollection(userDocRef, "stats", OnOpComplete);
            DeleteSubcollection(userDocRef, "gameHistory", OnOpComplete);
        }

        private void DeleteSubcollection(DocumentReference parentRef, string name, Action onComplete)
        {
            parentRef.Collection(name).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.Result.Count == 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                var batch = db.StartBatch();
                foreach (var doc in task.Result.Documents)
                    batch.Delete(doc.Reference);

                batch.CommitAsync().ContinueWithOnMainThread(commitTask =>
                {
                    if (commitTask.IsFaulted)
                        Debug.LogWarning($"[AuthService] Failed to delete {name}: {commitTask.Exception}");
                    onComplete?.Invoke();
                });
            });
        }

        private static string GetFirebaseErrorMessage(Exception ex)
        {
            if (ex is AggregateException agg && agg.InnerExceptions.Count > 0)
                return agg.InnerExceptions[0].Message;
            return ex?.Message ?? "An unknown error occurred";
        }
#endif
    }
}
