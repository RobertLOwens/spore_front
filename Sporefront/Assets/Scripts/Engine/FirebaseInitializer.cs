// ============================================================================
// FILE: Engine/FirebaseInitializer.cs
// PURPOSE: Firebase SDK bootstrap — checks dependencies, initializes services.
//          Attach to a DontDestroyOnLoad GameObject or let GameSceneManager create it.
// ============================================================================

using System;
using UnityEngine;
#if FIREBASE_AUTH
using Firebase;
using Firebase.Extensions;
#endif

namespace Sporefront.Engine
{
    public class FirebaseInitializer : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static FirebaseInitializer instance;
        public static FirebaseInitializer Instance => instance;

        // ================================================================
        // Events
        // ================================================================

        public static event Action OnFirebaseReady;
        public static event Action<string> OnFirebaseFailed;

        // ================================================================
        // State
        // ================================================================

        public static bool IsReady { get; private set; }
        public static bool HasFailed { get; private set; }
        public static string FailureReason { get; private set; }

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
#if FIREBASE_AUTH
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    IsReady = true;
                    HasFailed = false;
                    Debug.Log("[FirebaseInitializer] Firebase initialized successfully");
                    OnFirebaseReady?.Invoke();
                }
                else
                {
                    IsReady = false;
                    HasFailed = true;
                    FailureReason = $"Firebase dependency check failed: {task.Result}";
                    Debug.LogError($"[FirebaseInitializer] {FailureReason}");
                    OnFirebaseFailed?.Invoke(FailureReason);
                }
            });
#else
            // Firebase SDK not installed — run in offline mode
            IsReady = false;
            HasFailed = true;
            FailureReason = "Firebase SDK not installed (FIREBASE_AUTH not defined)";
            Debug.LogWarning($"[FirebaseInitializer] {FailureReason}");
            OnFirebaseFailed?.Invoke(FailureReason);
#endif
        }

        // ================================================================
        // Static Helper
        // ================================================================

        /// <summary>
        /// Ensures a FirebaseInitializer exists in the scene.
        /// Returns the instance (may still be initializing).
        /// </summary>
        public static FirebaseInitializer EnsureExists()
        {
            if (instance != null) return instance;

            var go = new GameObject("FirebaseInitializer");
            return go.AddComponent<FirebaseInitializer>();
        }
    }
}
