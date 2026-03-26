// ============================================================================
// FILE: Visual/GameSceneManager.cs
// PURPOSE: Root MonoBehaviour — bootstraps game, wires engine to visuals
//          Attach to a "GameManager" GameObject in the scene
//          Integrates UIManager, engine tick, drag-to-move (#13)
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class GameSceneManager : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private GameState gameState;
        private HexGridRenderer gridRenderer;
        private CameraController cameraController;
        private UIManager uiManager;

        private bool gameStarted = false;

        private HexCoordinate? hoveredTile;
        private HexCoordinate? selectedTile;

        // Drag-to-move state (#13)
        private Vector3 mouseDownScreenPos;
        private bool mouseIsDown;
        private HexCoordinate? mouseDownTile;
        private const float DragThreshold = 5f; // pixels

        // Selected entity for drag-to-move
        private Guid? selectedEntityID;
        private bool selectedEntityIsArmy;

        // Multi-selection (drag-select box)
        private struct SelectedEntity
        {
            public Guid id;
            public bool isArmy;
        }
        private List<SelectedEntity> selectedEntities = new List<SelectedEntity>();
        private bool isDragSelecting;
        private bool mouseDownOnOwnedEntity;

        // Drag preview entity IDs (for per-frame tendril updates)
        private HashSet<Guid> dragPreviewEntityIDs = new HashSet<Guid>();

        // Focused entity (from clicking a card in SelectedEntitiesPanel)
        private Guid? focusedEntityID;
        private bool focusedEntityIsArmy;

        // Auto-save state
        private const double AutoSaveIntervalGameTime = 300.0; // 5 minutes game time

        // Online game state
        private bool isOnlineGame;
        private string onlineGameID;
        private float heartbeatTimer;
        private double lastAutoSaveGameTime;

        // Disconnect handling
        private bool opponentDisconnected;
        private float disconnectTimer;
        private GameObject disconnectBanner;
        private UnityEngine.UI.Text disconnectTimerLabel;

        // Reconnection
        private Data.GameSession pendingRejoinSession;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Start()
        {
            gridRenderer = GetComponent<HexGridRenderer>();
            if (gridRenderer == null)
                gridRenderer = gameObject.AddComponent<HexGridRenderer>();

            cameraController = Camera.main.GetComponent<CameraController>();
            if (cameraController == null)
                cameraController = Camera.main.gameObject.AddComponent<CameraController>();

            // Initialize Firebase before bootstrapping UI
            FirebaseInitializer.EnsureExists();

            if (FirebaseInitializer.IsReady)
            {
                OnFirebaseResolved(true);
            }
            else if (FirebaseInitializer.HasFailed)
            {
                OnFirebaseResolved(false);
            }
            else
            {
                FirebaseInitializer.OnFirebaseReady += () => OnFirebaseResolved(true);
                FirebaseInitializer.OnFirebaseFailed += (_) => OnFirebaseResolved(false);
            }
        }

        private void OnFirebaseResolved(bool success)
        {
            if (success)
            {
                AuthService.Instance.Initialize();
                UserStatsService.Instance.Initialize();
                GameSessionService.Instance.Initialize();
                MatchmakingService.Instance.Initialize();
            }

            BootstrapMainMenu();
            RouteAuthState(success);
        }

        private void RouteAuthState(bool firebaseAvailable)
        {
            if (!firebaseAvailable)
            {
                // Firebase required — show auth panel which will display an error
                Debug.LogWarning("[GameSceneManager] Firebase not available — showing auth screen");
                uiManager.ShowAuth();
                return;
            }

            var authState = AuthService.Instance.CurrentState;
            switch (authState)
            {
                case AuthState.SignedIn:
                    uiManager.ShowMainMenu();
                    CheckForActiveOnlineGame();
                    break;
                case AuthState.NeedsUsername:
                    uiManager.ShowDisplayName();
                    break;
                case AuthState.SignedOut:
                case AuthState.Unknown:
                default:
                    uiManager.ShowAuth();
                    break;
            }
        }

        private void Update()
        {
            if (gameStarted)
            {
                // Engine tick — drives game simulation
                GameEngine.Instance.Update(Time.timeAsDouble);

                HandleTileHover();
                HandleTileInteraction();
                HandleRightClick();

                // Auto-save check (offline only — online uses cloud snapshots)
                if (!isOnlineGame && gameState != null && !gameState.isPaused &&
                    gameState.currentTime - lastAutoSaveGameTime >= AutoSaveIntervalGameTime)
                {
                    lastAutoSaveGameTime = gameState.currentTime;
                    SaveManager.AutoSave(gameState);
                }
            }

            // Online heartbeat — periodic keepalive to detect disconnects
            if (isOnlineGame)
            {
                heartbeatTimer += Time.deltaTime;
                if (heartbeatTimer >= GameConfig.Online.HeartbeatIntervalSeconds)
                {
                    heartbeatTimer = 0;
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
                    var hbUid = Engine.AuthService.Instance.CurrentUID;
                    if (hbUid != null && onlineGameID != null)
                        Engine.GameSessionService.Instance.UpdateHeartbeat(onlineGameID, hbUid);
#endif
                }

                // Disconnect timer countdown
                UpdateDisconnectTimer();

                // Process command retry queue
                Engine.GameSessionService.Instance.ProcessRetryQueue();
            }

            // UI update (notification timers, etc.)
            if (uiManager != null)
                uiManager.UpdateUI();
        }

        // ================================================================
        // Phase 1: Main Menu — create empty state, UI, show main menu
        // ================================================================

        private void BootstrapMainMenu()
        {
            // 1. Create empty GameState (needed for UI initialization)
            gameState = new GameState(35, 35);

            // 2. Initialize UI (creates canvas, all panels, event wiring)
            var uiGO = new GameObject("UIManager");
            uiGO.transform.SetParent(transform, false);
            uiManager = uiGO.AddComponent<UIManager>();
            uiManager.Initialize(gameState, gridRenderer, cameraController);

            // 3. Listen for new game request from main menu
            uiManager.OnStartNewGame += StartNewGame;
            uiManager.OnStartOnlineGame += StartOnlineGame;
            uiManager.OnPlayArenaGame += StartArenaGame;
            uiManager.OnLoadGame += LoadGame;

            // 4. Listen for entity focus from SelectedEntitiesPanel card click
            uiManager.OnEntityFocused += (id, isArmy) =>
            {
                focusedEntityID = id;
                focusedEntityIsArmy = isArmy;
                selectedEntityID = id;
                selectedEntityIsArmy = isArmy;
            };

            // 5. Listen for surrender request from in-game menu
            uiManager.OnSurrenderRequested += HandleSurrenderRequested;

            // 6. Listen for rejoin request from main menu
            uiManager.OnRejoinGame += () =>
            {
                if (pendingRejoinSession != null)
                    RejoinOnlineGame(pendingRejoinSession);
            };

            // 7. Auth routing will show the appropriate panel (auth, displayName, or mainMenu)
            Debug.Log("[GameSceneManager] UI bootstrapped, awaiting auth routing");
        }

        // ================================================================
        // Phase 2: Start Game — populate world, start engine
        // ================================================================

        private void StartNewGame(GameSetupConfig config)
        {
            // 1. Create game state with configured dimensions
            var (width, height) = GetMapDimensions(config.mapSize);
            gameState = new GameState(width, height);

            // 2. Create players
            var human = new PlayerState("Player 1", "3A5E8B", false);
            human.faction = config.playerFaction;
            var ai = new PlayerState("AI Opponent", "8B3A3A", true);
            ai.faction = config.aiFaction;
            gameState.players[human.id] = human;
            gameState.players[ai.id] = ai;
            gameState.localPlayerID = human.id;

            // 3. Generate map scaled to configured size
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            float areaRatio = (float)(width * height) / (35f * 35f);

            MapGeneratorBase generator;
            var resolvedMapType = config.mapType;
            if (resolvedMapType == MapType.Random)
            {
                resolvedMapType = (seed % 2 == 0) ? MapType.MountainValley : MapType.Arabia;
            }

            switch (resolvedMapType)
            {
                case MapType.MountainValley:
                    var mvConfig = new MountainValleyMapConfig
                    {
                        slopeTreePocketCount = Mathf.RoundToInt(10 * areaRatio),
                        slopeMineralCount = Mathf.RoundToInt(6 * areaRatio),
                        valleyTreePocketCount = Mathf.RoundToInt(8 * areaRatio),
                        valleyAnimalCount = Mathf.RoundToInt(10 * areaRatio),
                        ridgeTreePocketCount = Mathf.RoundToInt(2 * areaRatio),
                        ridgeAnimalCount = Mathf.RoundToInt(3 * areaRatio),
                    };
                    generator = new MountainValleyMapGenerator(width, height, seed, mvConfig);
                    break;
                case MapType.Arabia:
                default:
                    var mapConfig = new ArabiaMapConfig
                    {
                        treePocketCount = Mathf.RoundToInt(25 * areaRatio),
                        mineralDepositCount = Mathf.RoundToInt(12 * areaRatio),
                    };
                    generator = new ArabiaMapGenerator(width, height, seed, mapConfig);
                    break;
            }
            var terrain = generator.GenerateTerrain();

            // 4. Apply terrain to map data
            foreach (var kvp in terrain)
            {
                gameState.mapData.SetTile(new TileData(
                    kvp.Key,
                    kvp.Value.terrain,
                    kvp.Value.elevation
                ));
            }

            // 5. Place starting resources and city centers
            var startPositions = generator.GetStartingPositions();
            PlaceStartingEntities(startPositions, human, ai, generator, config.startingResources);

            // 5b. Place neutral resources across the map (trees, minerals, animals)
            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);

            var neutralResources = generator.GenerateNeutralResources(10, startCoords);
            foreach (var placement in neutralResources)
            {
                var rp = new ResourcePointData(placement.coordinate, placement.resourceType);
                gameState.resourcePoints[rp.id] = rp;
                gameState.mapData.resourcePointIDs.Add(rp.id);
                gameState.mapData.resourcePointCoordinates[rp.id] = placement.coordinate;
            }

            // 6. Initialize engine
            gameState.visibilityMode = config.visibilityMode;
            GameEngine.Instance.Setup(gameState);

            // 6b. Force initial vision tick so PlayerState.visibleCoordinates
            //     is populated before any rendering happens
            GameEngine.Instance.visionEngine.Update(0);

            // 7. Build visual grid
            gridRenderer.BuildGrid(gameState.mapData);

            // 8. Set camera bounds and focus on player start
            cameraController.SetMapBounds(width, height);
            if (startPositions.Count > 0)
            {
                cameraController.FocusOn(startPositions[0].coordinate, 8f, false);
            }

            // 9. Subscribe to state changes
            GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;

            // 10. Transition UI from main menu to gameplay
            uiManager.OnGameStarted(gameState);

            // 11. Game is now running
            ResetOnlineFlags();
            gameStarted = true;

            Debug.Log($"[GameSceneManager] Game started — seed: {seed}, map: {width}x{height}, tiles: {terrain.Count}");
        }

        private (int width, int height) GetMapDimensions(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small:  return (GameConfig.MapDimensions.Small, GameConfig.MapDimensions.Small);
                case MapSize.Medium: return (GameConfig.MapDimensions.Medium, GameConfig.MapDimensions.Medium);
                case MapSize.Large:  return (GameConfig.MapDimensions.Large, GameConfig.MapDimensions.Large);
                case MapSize.Huge:   return (GameConfig.MapDimensions.Huge, GameConfig.MapDimensions.Huge);
                default:             return (GameConfig.MapDimensions.Medium, GameConfig.MapDimensions.Medium);
            }
        }

        // ================================================================
        // Phase 2a: Start Online Game — create session, stream commands
        // ================================================================

        private void StartOnlineGame(GameSetupConfig config)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            // Route to host or joiner path based on matchmaking result
            if (!string.IsNullOrEmpty(config.matchGameID))
            {
                if (config.matchIsHost)
                    StartOnlineGameAsHost(config);
                else
                    JoinOnlineGame(config);
                return;
            }

            // Legacy path: solo online game (host + AI, no matchmaking)
            StartOnlineGameLegacy(config);
#else
            Debug.LogWarning("[GameSceneManager] Online games require Firebase. Starting offline instead.");
            StartNewGame(config);
#endif
        }

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        // ================================================================
        // Matchmaking Host Path
        // ================================================================

        private void StartOnlineGameAsHost(GameSetupConfig config)
        {
            var auth = Engine.AuthService.Instance;
            var uid = auth.CurrentUID;
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[GameSceneManager] Cannot start online game — not signed in");
                return;
            }

            var displayName = auth.CurrentDisplayName ?? "Player 1";

            // 1. Create game state (Arabia 35x35)
            int width = 35, height = 35;
            gameState = new GameState(width, height);

            // 2. Create two human players with pre-determined IDs from matchmaking
            var localPlayer = new PlayerState(displayName, "3A5E8B", false);
            if (!string.IsNullOrEmpty(config.matchLocalPlayerID))
                localPlayer.id = Guid.Parse(config.matchLocalPlayerID);
            localPlayer.faction = config.playerFaction;

            var opponent = new PlayerState(config.matchOpponentDisplayName ?? "Opponent", "8B3A3A", false);
            if (!string.IsNullOrEmpty(config.matchOpponentPlayerID))
                opponent.id = Guid.Parse(config.matchOpponentPlayerID);
            opponent.faction = config.matchOpponentFaction;

            gameState.AddPlayer(localPlayer);
            gameState.AddPlayer(opponent);
            gameState.localPlayerID = localPlayer.id;

            // 3. Generate Arabia map
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            var arabiaConfig = new ArabiaMapConfig();
            var generator = new ArabiaMapGenerator(width, height, seed, arabiaConfig);
            var terrain = generator.GenerateTerrain();

            foreach (var kvp in terrain)
            {
                gameState.mapData.SetTile(new TileData(
                    kvp.Key, kvp.Value.terrain, kvp.Value.elevation));
            }

            // 4. Place starting entities for both players
            var startPositions = generator.GetStartingPositions();
            PlaceStartingEntities(startPositions, localPlayer, opponent, generator, config.startingResources);

            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);
            var neutralResources = generator.GenerateNeutralResources(10, startCoords);
            foreach (var placement in neutralResources)
            {
                var rp = new ResourcePointData(placement.coordinate, placement.resourceType);
                gameState.resourcePoints[rp.id] = rp;
                gameState.mapData.resourcePointIDs.Add(rp.id);
                gameState.mapData.resourcePointCoordinates[rp.id] = placement.coordinate;
            }

            // 5. Create online session with both human players
            var sessionMapConfig = new Data.MapGenerationConfig("arabia", seed, width, height);
            var aiPlayersList = new List<(string displayName, Guid playerID, string colorHex, string faction)>();
            var session = Data.GameSession.CreateForMatchmaking(
                config.matchGameID,
                uid, displayName, localPlayer.id, "3A5E8B",
                config.playerFaction.ToString(),
                config.matchOpponentUID, config.matchOpponentDisplayName ?? "Opponent",
                opponent.id, "8B3A3A",
                config.matchOpponentFaction.ToString(),
                sessionMapConfig, aiPlayersList);

            Engine.GameSessionService.Instance.CreateGame(session, (success, error) =>
            {
                if (!success)
                {
                    Debug.LogError(string.Format("[GameSceneManager] Failed to create matchmaking game: {0}", error));
                    // Roll back local state — game was never created on the server
                    gameState = null;
                    isOnlineGame = false;
                    onlineGameID = null;
                    gameStarted = false;
                    uiManager?.SetMainMenuStatus("Failed to create game. Please try again.",
                        SporefrontColors.SporeRed);
                    uiManager?.ShowMainMenu();
                    return;
                }

                onlineGameID = config.matchGameID;
                isOnlineGame = true;

                // 6. Initialize engine (host — no AI in PvP)
                gameState.visibilityMode = config.visibilityMode;
                GameEngine.Instance.SetupOnline(gameState, 0, isHost: true);
                GameEngine.Instance.visionEngine.Update(0);

                // 7. Save initial snapshot — joiner will load this
                var snapshot = Data.GameSnapshot.Create(gameState, 0);
                if (snapshot != null)
                {
                    Engine.GameSessionService.Instance.SaveSnapshot(
                        onlineGameID, snapshot, null);
                }

                // 8. Start listeners
                Engine.GameSessionService.Instance.StartCommandListener(onlineGameID, 0);
                Engine.GameSessionService.Instance.StartSessionListener(onlineGameID);
                Engine.GameSessionService.Instance.OnCommandReceived += HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated += HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected += HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected += HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed += HandleCommandSubmitFailed;
                GameEngine.Instance.OnDesyncDetected += HandleDesyncDetected;
                Engine.GameSessionService.Instance.SetLocalUID(Engine.AuthService.Instance.CurrentUID);

                // 9. Build visual
                gridRenderer.BuildGrid(gameState.mapData);
                cameraController.SetMapBounds(width, height);
                if (startPositions.Count > 0)
                    cameraController.FocusOn(startPositions[0].coordinate, 8f, false);

                GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;
                uiManager.OnGameStarted(gameState);
                uiManager.SetOnlineMode(true);
                ResetOnlineFlags();
                gameStarted = true;

                Debug.Log(string.Format("[GameSceneManager] Online game started as HOST — ID: {0}", onlineGameID));
            });
        }

        // ================================================================
        // Matchmaking Joiner Path
        // ================================================================

        private void JoinOnlineGame(GameSetupConfig config)
        {
            var auth = Engine.AuthService.Instance;
            var uid = auth.CurrentUID;
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[GameSceneManager] Cannot join online game — not signed in");
                return;
            }

            onlineGameID = config.matchGameID;
            isOnlineGame = true;

            Debug.Log(string.Format("[GameSceneManager] Joining game as JOINER — ID: {0}", onlineGameID));

            // Show loading status
            uiManager?.SetMainMenuStatus("Joining game...");

            // Track whether the callback or timeout has fired (whichever fires first wins).
            // Use an array so the lambda captures a reference, not a value copy.
            bool[] snapshotHandled = { false };
            Coroutine timeoutCoroutine = StartCoroutine(SnapshotLoadTimeout(config, snapshotHandled));

            // Load the initial snapshot created by the host
            Engine.GameSessionService.Instance.LoadLatestSnapshot(onlineGameID, (snapshot, pendingCommands, error) =>
            {
                if (snapshotHandled[0]) return; // Timeout already triggered a retry
                snapshotHandled[0] = true;
                if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);

                if (snapshot == null)
                {
                    Debug.LogError(string.Format("[GameSceneManager] Failed to load snapshot for join: {0}", error ?? "no snapshot"));
                    // Retry after a short delay — host may still be saving
                    StartCoroutine(RetryJoinAfterDelay(config, 2.0f));
                    return;
                }

                // Check game version compatibility
                if (!string.IsNullOrEmpty(snapshot.gameVersion) &&
                    snapshot.gameVersion != Application.version)
                {
                    Debug.LogError($"[GameSceneManager] Version mismatch: host={snapshot.gameVersion}, local={Application.version}");
                    isOnlineGame = false;
                    onlineGameID = null;
                    uiManager?.SetMainMenuStatus(
                        $"Version mismatch: update required (host v{snapshot.gameVersion}, you v{Application.version})",
                        SporefrontColors.SporeRed);
                    uiManager.ShowMainMenu();
                    return;
                }

                // Reconstruct game state from snapshot
                try
                {
                    gameState = snapshot.ToGameState();
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("[GameSceneManager] Failed to restore game state: {0}", e.Message));
                    return;
                }

                // Set local player ID
                if (!string.IsNullOrEmpty(config.matchLocalPlayerID))
                    gameState.localPlayerID = Guid.Parse(config.matchLocalPlayerID);

                gameState.visibilityMode = config.visibilityMode;

                // Initialize engine as non-host (no AI control)
                GameEngine.Instance.SetupOnline(gameState, snapshot.commandSequence, isHost: false);
                GameEngine.Instance.visionEngine.Update(0);

                // Replay any commands that arrived after the snapshot
                int listenerStartSequence = snapshot.commandSequence;
                if (pendingCommands != null)
                {
                    foreach (var cmd in pendingCommands)
                    {
                        GameEngine.Instance.ExecuteRemoteCommand(cmd);
                        if (cmd.sequence > listenerStartSequence)
                            listenerStartSequence = cmd.sequence;
                    }
                }

                // Start listener AFTER the highest replayed sequence to avoid duplicates
                Engine.GameSessionService.Instance.StartCommandListener(onlineGameID, listenerStartSequence);
                Engine.GameSessionService.Instance.StartSessionListener(onlineGameID);
                Engine.GameSessionService.Instance.OnCommandReceived += HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated += HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected += HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected += HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed += HandleCommandSubmitFailed;
                GameEngine.Instance.OnDesyncDetected += HandleDesyncDetected;
                Engine.GameSessionService.Instance.SetLocalUID(Engine.AuthService.Instance.CurrentUID);

                // Build visual
                gridRenderer.BuildGrid(gameState.mapData);
                int width = gameState.mapData.width;
                int height = gameState.mapData.height;
                cameraController.SetMapBounds(width, height);

                // Focus on local player's city center
                var localPlayerID = gameState.localPlayerID;
                if (localPlayerID.HasValue)
                {
                    var buildings = gameState.GetBuildingsForPlayer(localPlayerID.Value);
                    if (buildings != null)
                    {
                        foreach (var b in buildings)
                        {
                            if (b.buildingType == BuildingType.CityCenter)
                            {
                                cameraController.FocusOn(b.coordinate, 8f, false);
                                break;
                            }
                        }
                    }
                }

                GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;
                uiManager.OnGameStarted(gameState);
                uiManager.SetOnlineMode(true);
                uiManager.SetMainMenuStatus(null); // Clear loading status
                ResetOnlineFlags();
                gameStarted = true;
                joinRetryCount = 0;

                Debug.Log(string.Format("[GameSceneManager] Online game joined — ID: {0}", onlineGameID));
            });
        }

        private int joinRetryCount = 0;

        private System.Collections.IEnumerator RetryJoinAfterDelay(GameSetupConfig config, float delay)
        {
            joinRetryCount++;
            if (joinRetryCount > GameConfig.Online.MaxJoinRetries)
            {
                Debug.LogError($"[GameSceneManager] Join failed after {GameConfig.Online.MaxJoinRetries} retries — giving up");
                isOnlineGame = false;
                onlineGameID = null;
                gameStarted = false;
                pendingRejoinSession = null;
                uiManager?.SetRejoinVisible(false);
                uiManager?.SetMainMenuStatus("Failed to join game. Please try again.",
                    SporefrontColors.SporeRed);
                uiManager?.ShowMainMenu();
                yield break;
            }

            uiManager?.SetMainMenuStatus($"Joining game... (attempt {joinRetryCount}/{GameConfig.Online.MaxJoinRetries})");
            Debug.Log($"[GameSceneManager] Retrying join (attempt {joinRetryCount}/{GameConfig.Online.MaxJoinRetries})...");
            yield return new WaitForSeconds(delay);
            JoinOnlineGame(config);
        }

        /// <summary>
        /// Timeout guard for LoadLatestSnapshot. If the async chain hangs,
        /// this fires after SnapshotLoadTimeoutSeconds and triggers a retry.
        /// Uses a shared bool[] so both the callback and timeout can read/write atomically.
        /// </summary>
        private System.Collections.IEnumerator SnapshotLoadTimeout(GameSetupConfig config, bool[] handled)
        {
            yield return new WaitForSeconds(GameConfig.Online.SnapshotLoadTimeoutSeconds);

            if (!handled[0])
            {
                handled[0] = true;
                Debug.LogWarning("[GameSceneManager] Snapshot load timed out — retrying");
                StartCoroutine(RetryJoinAfterDelay(config, GameConfig.Online.JoinRetryDelaySeconds));
            }
        }

        // ================================================================
        // Legacy Online Path (solo host + AI, no matchmaking)
        // ================================================================

        private void StartOnlineGameLegacy(GameSetupConfig config)
        {
            var auth = Engine.AuthService.Instance;
            var uid = auth.CurrentUID;
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[GameSceneManager] Cannot start online game — not signed in");
                return;
            }

            // 1. Create game state with configured dimensions
            var (width, height) = GetMapDimensions(config.mapSize);
            gameState = new GameState(width, height);

            // 2. Create players
            var displayName = auth.CurrentDisplayName ?? "Player 1";
            var human = new PlayerState(displayName, "3A5E8B", false);
            human.faction = config.playerFaction;
            var ai = new PlayerState("AI Opponent", "8B3A3A", true);
            ai.faction = config.aiFaction;
            gameState.players[human.id] = human;
            gameState.players[ai.id] = ai;
            gameState.localPlayerID = human.id;

            // 3. Generate deterministic map from seed
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            float areaRatio = (float)(width * height) / (35f * 35f);

            MapGeneratorBase generator;
            var resolvedMapType = config.mapType;
            if (resolvedMapType == MapType.Random)
                resolvedMapType = (seed % 2 == 0) ? MapType.MountainValley : MapType.Arabia;

            switch (resolvedMapType)
            {
                case MapType.MountainValley:
                    var mvConfig = new MountainValleyMapConfig
                    {
                        slopeTreePocketCount = Mathf.RoundToInt(10 * areaRatio),
                        slopeMineralCount = Mathf.RoundToInt(6 * areaRatio),
                        valleyTreePocketCount = Mathf.RoundToInt(8 * areaRatio),
                        valleyAnimalCount = Mathf.RoundToInt(10 * areaRatio),
                        ridgeTreePocketCount = Mathf.RoundToInt(2 * areaRatio),
                        ridgeAnimalCount = Mathf.RoundToInt(3 * areaRatio),
                    };
                    generator = new MountainValleyMapGenerator(width, height, seed, mvConfig);
                    break;
                case MapType.Arabia:
                default:
                    var mapConfig = new ArabiaMapConfig
                    {
                        treePocketCount = Mathf.RoundToInt(25 * areaRatio),
                        mineralDepositCount = Mathf.RoundToInt(12 * areaRatio),
                    };
                    generator = new ArabiaMapGenerator(width, height, seed, mapConfig);
                    break;
            }
            var terrain = generator.GenerateTerrain();

            // 4. Apply terrain to map data
            foreach (var kvp in terrain)
            {
                gameState.mapData.SetTile(new TileData(
                    kvp.Key,
                    kvp.Value.terrain,
                    kvp.Value.elevation
                ));
            }

            // 5. Place starting resources and city centers
            var startPositions = generator.GetStartingPositions();
            PlaceStartingEntities(startPositions, human, ai, generator, config.startingResources);

            var startCoords = new List<HexCoordinate>();
            foreach (var pos in startPositions)
                startCoords.Add(pos.coordinate);
            var neutralResources = generator.GenerateNeutralResources(10, startCoords);
            foreach (var placement in neutralResources)
            {
                var rp = new ResourcePointData(placement.coordinate, placement.resourceType);
                gameState.resourcePoints[rp.id] = rp;
                gameState.mapData.resourcePointIDs.Add(rp.id);
                gameState.mapData.resourcePointCoordinates[rp.id] = placement.coordinate;
            }

            // 6. Create online session
            var sessionMapConfig = new Data.MapGenerationConfig(
                resolvedMapType.ToString().ToLower(), seed, width, height);
            var aiPlayersList = new List<(string displayName, Guid playerID, string colorHex, string faction)>
            {
                ("AI Opponent", ai.id, "8B3A3A", config.aiFaction.ToString())
            };
            var session = Data.GameSession.Create(uid, displayName, human.id, "3A5E8B",
                config.playerFaction.ToString(), sessionMapConfig, aiPlayersList);

            Engine.GameSessionService.Instance.CreateGame(session, (success, error) =>
            {
                if (!success)
                {
                    Debug.LogError(string.Format("[GameSceneManager] Failed to create online game: {0}", error));
                    // Roll back local state
                    gameState = null;
                    isOnlineGame = false;
                    onlineGameID = null;
                    gameStarted = false;
                    uiManager?.SetMainMenuStatus("Failed to create game. Please try again.",
                        SporefrontColors.SporeRed);
                    uiManager?.ShowMainMenu();
                    return;
                }

                onlineGameID = session.gameID;
                isOnlineGame = true;

                // 7. Initialize engine in online mode (host runs AI)
                gameState.visibilityMode = config.visibilityMode;
                GameEngine.Instance.SetupOnline(gameState, 0, isHost: true);
                GameEngine.Instance.visionEngine.Update(0);

                // 8. Create initial snapshot for recovery
                var snapshot = Data.GameSnapshot.Create(gameState, 0);
                if (snapshot != null)
                {
                    Engine.GameSessionService.Instance.SaveSnapshot(
                        onlineGameID, snapshot, null);
                }

                // 9. Start listening for remote commands
                Engine.GameSessionService.Instance.StartCommandListener(onlineGameID, 0);
                Engine.GameSessionService.Instance.StartSessionListener(onlineGameID);
                Engine.GameSessionService.Instance.OnCommandReceived += HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated += HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected += HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected += HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed += HandleCommandSubmitFailed;
                GameEngine.Instance.OnDesyncDetected += HandleDesyncDetected;
                Engine.GameSessionService.Instance.SetLocalUID(Engine.AuthService.Instance.CurrentUID);

                // 10. Build visual grid
                gridRenderer.BuildGrid(gameState.mapData);

                // 11. Set camera
                cameraController.SetMapBounds(width, height);
                if (startPositions.Count > 0)
                    cameraController.FocusOn(startPositions[0].coordinate, 8f, false);

                // 12. Subscribe to state changes and start game
                GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;
                uiManager.OnGameStarted(gameState);
                uiManager.SetOnlineMode(true);
                ResetOnlineFlags();
                gameStarted = true;

                Debug.Log(string.Format("[GameSceneManager] Online game started — ID: {0}, seed: {1}", onlineGameID, seed));
            });
        }
#endif

        // ================================================================
        // Online Event Handlers
        // ================================================================

        private void HandleRemoteCommand(Data.OnlineCommand onlineCmd)
        {
            GameEngine.Instance.ExecuteRemoteCommand(onlineCmd);
        }

        private void HandleSessionUpdate(Data.GameSession session)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (session.status == Data.GameSessionStatus.Paused && !gameState.isPaused)
            {
                gameState.isPaused = true;
            }
            else if (session.status == Data.GameSessionStatus.Playing && gameState.isPaused)
            {
                gameState.isPaused = false;
            }
            else if (session.status == Data.GameSessionStatus.Finished)
            {
                Debug.Log("[GameSceneManager] Online game finished");
                // If game over wasn't triggered locally (e.g., latency), show it now
                if (gameState != null && !gameState.isGameOver)
                {
                    gameState.isGameOver = true;
                    // The remote client may not know the result — default to defeat
                    var stats = GatherGameOverStats();
                    uiManager?.ShowGameOver(false, "The game has ended.", stats);
                }
            }

            // Check opponent heartbeat for disconnect detection
            Engine.GameSessionService.Instance.CheckOpponentHeartbeat(session);

            // Check for opponent that explicitly left
            foreach (var kvp in session.players)
            {
                if (kvp.Key == Engine.AuthService.Instance.CurrentUID) continue;
                if (kvp.Value.isAI) continue;

                if (kvp.Value.status == Data.PlayerSessionStatus.Left && gameState != null && !gameState.isGameOver)
                {
                    // Opponent left — treat as surrender, award victory
                    gameState.isGameOver = true;
                    var stats = GatherGameOverStats();
                    uiManager?.ShowGameOver(true, "Your opponent has left the game.", stats);
                }
            }
#endif
        }

        private void HandleOpponentDisconnected()
        {
            if (gameState == null || gameState.isGameOver) return;

            opponentDisconnected = true;
            disconnectTimer = (float)GameConfig.Online.AbandonTimeoutSeconds;

            // Pause game while opponent is disconnected
            gameState.isPaused = true;

            // Show disconnect banner
            ShowDisconnectBanner();

            Debug.Log("[GameSceneManager] Opponent disconnected — game paused, waiting for reconnection");
        }

        private void HandleOpponentReconnected()
        {
            if (gameState == null) return;

            opponentDisconnected = false;
            disconnectTimer = 0;

            // Resume game
            gameState.isPaused = false;

            // Hide disconnect banner
            HideDisconnectBanner();

            Debug.Log("[GameSceneManager] Opponent reconnected — game resumed");
        }

        private void HandleCommandSubmitFailed(string commandType)
        {
            Debug.LogError($"[GameSceneManager] Command '{commandType}' submission permanently failed — game may be desynced");

            // Show a warning to the player
            if (uiManager != null)
            {
                uiManager.ShowCommandFailure("Network Error: A command failed to sync. The game may be out of sync.");
            }
        }

        private bool desyncWarningShown = false;
        private bool abandonTimeoutProcessed = false;

        private void HandleDesyncDetected(int sequence, long remoteHash, long localHash)
        {
            Debug.LogError($"[GameSceneManager] DESYNC at command {sequence}: remote={remoteHash}, local={localHash}");

            if (!desyncWarningShown && uiManager != null)
            {
                desyncWarningShown = true;
                uiManager.ShowCommandFailure(
                    "Warning: Game state mismatch detected. The game may be out of sync. "
                    + "Consider leaving and rejoining to resync.");
            }
        }

        /// <summary>
        /// Reset per-game online state flags. Call at the start of any new/loaded/joined game.
        /// </summary>
        private void ResetOnlineFlags()
        {
            desyncWarningShown = false;
            abandonTimeoutProcessed = false;
            opponentDisconnected = false;
        }

        private void ShowDisconnectBanner()
        {
            if (disconnectBanner != null) return;

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) return;

            disconnectBanner = new GameObject("DisconnectBanner", typeof(RectTransform),
                typeof(CanvasRenderer));
            disconnectBanner.transform.SetParent(canvas.transform, false);

            var bannerRT = disconnectBanner.GetComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0.15f, 0.82f);
            bannerRT.anchorMax = new Vector2(0.85f, 0.96f);
            bannerRT.offsetMin = Vector2.zero;
            bannerRT.offsetMax = Vector2.zero;

            // Override sorting to render above game
            var overrideCanvas = disconnectBanner.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 250;
            disconnectBanner.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Layout
            var layout = disconnectBanner.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var bg = disconnectBanner.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.15f, 0.1f, 0.08f, 0.9f);

            disconnectTimerLabel = UIHelper.CreateLabel(disconnectBanner.transform,
                "Opponent disconnected. Waiting for reconnection...",
                UIConstants.FontBody, SporefrontColors.ParchmentLight,
                TextAnchor.MiddleCenter);

            // "Leave Game" button
            var leaveBtn = UIHelper.CreateButton(disconnectBanner.transform,
                "Leave Game", bgColor: SporefrontColors.SporeRed, onClick: () =>
            {
                // Award victory and end the game
                opponentDisconnected = false;
                gameState.isPaused = false;
                HideDisconnectBanner();

                if (!gameState.isGameOver)
                {
                    gameState.isGameOver = true;
                    var stats = GatherGameOverStats();
                    uiManager?.ShowGameOver(true,
                        "Your opponent has disconnected.", stats);

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
                    if (onlineGameID != null)
                    {
                        Engine.GameSessionService.Instance.UpdateSessionStatus(
                            onlineGameID, Data.GameSessionStatus.Finished);
                    }
#endif
                }
            });
            var btnLE = leaveBtn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            btnLE.preferredHeight = 30f;
            btnLE.preferredWidth = 140f;
        }

        private void HideDisconnectBanner()
        {
            if (disconnectBanner != null)
            {
                Destroy(disconnectBanner);
                disconnectBanner = null;
                disconnectTimerLabel = null;
            }
        }

        private void UpdateDisconnectTimer()
        {
            if (!opponentDisconnected || gameState == null || gameState.isGameOver) return;

            disconnectTimer -= Time.deltaTime;

            // Update banner text
            if (disconnectTimerLabel != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(disconnectTimer));
                int minutes = seconds / 60;
                int secs = seconds % 60;
                disconnectTimerLabel.text = string.Format(
                    "Opponent disconnected. Waiting for reconnection... ({0}:{1:D2})",
                    minutes, secs);
            }

            // Timer expired — opponent abandoned the game
            if (disconnectTimer <= 0 && !abandonTimeoutProcessed)
            {
                abandonTimeoutProcessed = true;
                opponentDisconnected = false;
                gameState.isPaused = false;
                HideDisconnectBanner();

                // Auto-win: opponent abandoned
                if (!gameState.isGameOver)
                {
                    gameState.isGameOver = true;
                    var stats = GatherGameOverStats();
                    uiManager?.ShowGameOver(true,
                        "Your opponent has abandoned the game.", stats);

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
                    if (onlineGameID != null)
                    {
                        Engine.GameSessionService.Instance.UpdateSessionStatus(
                            onlineGameID, Data.GameSessionStatus.Finished);
                    }
#endif
                }

                Debug.Log("[GameSceneManager] Opponent abandon timeout — awarding victory");
            }
        }

        private void CleanupOnlineGame()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (isOnlineGame && onlineGameID != null)
            {
                Engine.GameSessionService.Instance.OnCommandReceived -= HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated -= HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected -= HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected -= HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed -= HandleCommandSubmitFailed;
                GameEngine.Instance.OnDesyncDetected -= HandleDesyncDetected;
                Engine.GameSessionService.Instance.StopCommandListener();

                var cleanupUid = Engine.AuthService.Instance.CurrentUID;
                if (cleanupUid != null)
                    Engine.GameSessionService.Instance.LeaveSession(onlineGameID, cleanupUid);

                // Clean up disconnect banner
                if (disconnectBanner != null)
                    Destroy(disconnectBanner);
                opponentDisconnected = false;

                isOnlineGame = false;
                onlineGameID = null;
            }
#endif
        }

        // ================================================================
        // Reconnection — Check for Active Game & Rejoin
        // ================================================================

        private void CheckForActiveOnlineGame()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var uid = Engine.AuthService.Instance.CurrentUID;
            if (string.IsNullOrEmpty(uid)) return;

            Engine.GameSessionService.Instance.FindActiveGame(uid, session =>
            {
                if (session != null)
                {
                    pendingRejoinSession = session;
                    uiManager?.SetRejoinVisible(true);
                    Debug.Log(string.Format("[GameSceneManager] Active game found — ID: {0}", session.gameID));
                }
                else
                {
                    pendingRejoinSession = null;
                    uiManager?.SetRejoinVisible(false);
                }
            });
#endif
        }

        private void RejoinOnlineGame(Data.GameSession session)
        {
            RejoinOnlineGame(session, 0);
        }

        private void RejoinOnlineGame(Data.GameSession session, int attempt)
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (session == null)
            {
                Debug.LogWarning("[GameSceneManager] Cannot rejoin — no session");
                return;
            }

            var auth = Engine.AuthService.Instance;
            var uid = auth.CurrentUID;
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[GameSceneManager] Cannot rejoin — not signed in");
                return;
            }

            onlineGameID = session.gameID;
            isOnlineGame = true;

            Debug.Log(string.Format("[GameSceneManager] Rejoining game — ID: {0}, attempt: {1}",
                onlineGameID, attempt));

            // Load the latest snapshot + any pending commands
            Engine.GameSessionService.Instance.LoadLatestSnapshot(onlineGameID, (snapshot, pendingCommands, error) =>
            {
                if (snapshot == null)
                {
                    if (attempt < GameConfig.Online.MaxJoinRetries)
                    {
                        Debug.LogWarning(string.Format(
                            "[GameSceneManager] Rejoin snapshot not available (attempt {0}/{1}), retrying...",
                            attempt + 1, GameConfig.Online.MaxJoinRetries));
                        StartCoroutine(RetryRejoinAfterDelay(session, attempt + 1));
                    }
                    else
                    {
                        Debug.LogError(string.Format(
                            "[GameSceneManager] Rejoin failed after {0} attempts: {1}",
                            GameConfig.Online.MaxJoinRetries, error ?? "no snapshot"));
                        isOnlineGame = false;
                        onlineGameID = null;
                    }
                    return;
                }

                // Reconstruct game state from snapshot
                try
                {
                    gameState = snapshot.ToGameState();
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("[GameSceneManager] Rejoin state restore failed: {0}", e.Message));
                    isOnlineGame = false;
                    onlineGameID = null;
                    return;
                }

                // Set local player ID from session data
                if (session.players.ContainsKey(uid))
                {
                    var playerIDStr = session.players[uid].playerID;
                    if (!string.IsNullOrEmpty(playerIDStr))
                        gameState.localPlayerID = Guid.Parse(playerIDStr);
                }

                // Determine if this player is the host (host runs AI)
                bool isHost = session.hostUID == uid;

                // Initialize engine in online mode
                GameEngine.Instance.SetupOnline(gameState, snapshot.commandSequence, isHost: isHost);
                GameEngine.Instance.visionEngine.Update(0);

                // Replay any commands that arrived after the snapshot
                int listenerStartSequence = snapshot.commandSequence;
                if (pendingCommands != null)
                {
                    foreach (var cmd in pendingCommands)
                    {
                        GameEngine.Instance.ExecuteRemoteCommand(cmd);
                        if (cmd.sequence > listenerStartSequence)
                            listenerStartSequence = cmd.sequence;
                    }
                }

                // Start listener AFTER the highest replayed sequence to avoid duplicates
                Engine.GameSessionService.Instance.StartCommandListener(onlineGameID, listenerStartSequence);
                Engine.GameSessionService.Instance.StartSessionListener(onlineGameID);

                // Unsubscribe first to prevent duplicate handlers on retry
                Engine.GameSessionService.Instance.OnCommandReceived -= HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated -= HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected -= HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected -= HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed -= HandleCommandSubmitFailed;

                Engine.GameSessionService.Instance.OnCommandReceived += HandleRemoteCommand;
                Engine.GameSessionService.Instance.OnSessionUpdated += HandleSessionUpdate;
                Engine.GameSessionService.Instance.OnOpponentDisconnected += HandleOpponentDisconnected;
                Engine.GameSessionService.Instance.OnOpponentReconnected += HandleOpponentReconnected;
                Engine.GameSessionService.Instance.OnCommandSubmitFailed += HandleCommandSubmitFailed;
                Engine.GameSessionService.Instance.SetLocalUID(uid);

                // Update heartbeat immediately so opponent sees us as reconnected
                Engine.GameSessionService.Instance.UpdateHeartbeat(onlineGameID, uid);

                // Update player status back to Active
                Engine.GameSessionService.Instance.UpdatePlayerStatus(
                    onlineGameID, uid, Data.PlayerSessionStatus.Active);

                // Build visual grid
                gridRenderer.BuildGrid(gameState.mapData);
                int width = gameState.mapData.width;
                int height = gameState.mapData.height;
                cameraController.SetMapBounds(width, height);

                // Focus on local player's city center
                var localPlayerID = gameState.localPlayerID;
                if (localPlayerID.HasValue)
                {
                    var buildings = gameState.GetBuildingsForPlayer(localPlayerID.Value);
                    if (buildings != null)
                    {
                        foreach (var b in buildings)
                        {
                            if (b.buildingType == BuildingType.CityCenter)
                            {
                                cameraController.FocusOn(b.coordinate, 8f, false);
                                break;
                            }
                        }
                    }
                }

                // Subscribe to state changes and start game
                GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;
                uiManager.OnGameStarted(gameState);
                uiManager.SetOnlineMode(true);
                ResetOnlineFlags();
                gameStarted = true;

                // Clear rejoin state
                pendingRejoinSession = null;
                uiManager?.SetRejoinVisible(false);

                Debug.Log(string.Format("[GameSceneManager] Rejoined online game — ID: {0}, isHost: {1}",
                    onlineGameID, isHost));
            });
#endif
        }

        private System.Collections.IEnumerator RetryRejoinAfterDelay(Data.GameSession session, int attempt)
        {
            yield return new WaitForSeconds(GameConfig.Online.JoinRetryDelaySeconds);
            Debug.Log(string.Format("[GameSceneManager] Retrying rejoin (attempt {0})...", attempt));
            RejoinOnlineGame(session, attempt);
        }

        // ================================================================
        // Phase 2b: Start Arena Game — playable combat on 7x7 map
        // ================================================================

        private void StartArenaGame(ArenaConfig arenaConfig)
        {
            var scenario = arenaConfig.scenarioConfig;
            var army = arenaConfig.armyConfig;

            // 1. Create arena game state
            int arenaSize = GameConfig.MapDimensions.Arena;
            gameState = new GameState(arenaSize, arenaSize);

            // 2. Generate arena terrain
            for (int r = 0; r < arenaSize; r++)
            {
                for (int q = 0; q < arenaSize; q++)
                {
                    var coord = new HexCoordinate(q, r);
                    gameState.mapData.SetTile(new TileData(coord, TerrainType.Plains, 0));
                }
            }
            var enemyPos = new HexCoordinate(4, 3);
            var playerPos = new HexCoordinate(2, 3);
            int elevation = scenario.enemyTerrain == TerrainType.Hill ? 1 :
                           (scenario.enemyTerrain == TerrainType.Mountain ? 2 : 0);
            gameState.mapData.SetTile(new TileData(enemyPos, scenario.enemyTerrain, elevation));

            // 3. Create players (defender is not AI-controlled in arena — player fights manually)
            var human = new PlayerState("Attacker", "3A5E8B", false);
            var ai = new PlayerState("Defender", "8B3A3A", false);
            gameState.AddPlayer(human);
            gameState.AddPlayer(ai);
            gameState.localPlayerID = human.id;

            // Set diplomacy
            human.SetDiplomacyStatus(ai.id, DiplomacyStatus.Enemy);
            ai.SetDiplomacyStatus(human.id, DiplomacyStatus.Enemy);

            // 4. Create city centers (home bases)
            var attackerCity = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0), human.id);
            attackerCity.state = BuildingState.Completed;
            attackerCity.health = attackerCity.maxHealth;
            gameState.AddBuilding(attackerCity);

            var defenderCity = new BuildingData(BuildingType.CityCenter, new HexCoordinate(6, 6), ai.id);
            defenderCity.state = BuildingState.Completed;
            defenderCity.health = defenderCity.maxHealth;
            gameState.AddBuilding(defenderCity);

            // 5. Apply unit tier upgrades
            foreach (var kvp in scenario.playerUnitTiers)
            {
                if (kvp.Value <= 0) continue;
                var upgrades = UnitUpgradeTypeExtensions.UpgradesForUnit(kvp.Key);
                upgrades.Sort((a, b) => a.Tier().CompareTo(b.Tier()));
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.Tier() <= kvp.Value)
                        human.CompleteUnitUpgrade(upgrade.ToString());
                }
            }
            foreach (var kvp in scenario.enemyUnitTiers)
            {
                if (kvp.Value <= 0) continue;
                var upgrades = UnitUpgradeTypeExtensions.UpgradesForUnit(kvp.Key);
                upgrades.Sort((a, b) => a.Tier().CompareTo(b.Tier()));
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.Tier() <= kvp.Value)
                        ai.CompleteUnitUpgrade(upgrade.ToString());
                }
            }

            // 6. Create player (attacker) army
            var attackerArmy = new ArmyData("Attacker Army", playerPos, human.id);
            var attackerCmdr = new CommanderData("Attacker Cmdr", scenario.playerCommanderSpecialty, human.id);
            attackerCmdr.level = scenario.playerCommanderLevel;
            attackerCmdr.rank = CommanderRankExtensions.RankForLevel(scenario.playerCommanderLevel);
            attackerArmy.commanderID = attackerCmdr.id;
            attackerArmy.homeBaseID = attackerCity.id;
            gameState.AddCommander(attackerCmdr);
            foreach (var kvp in army.playerArmy)
            {
                if (kvp.Value > 0)
                    attackerArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
            }
            gameState.AddArmy(attackerArmy);

            // Extra attacker armies (stacking)
            int playerExtraCount = Math.Abs(scenario.playerArmyCount) - 1;
            if (playerExtraCount > 0)
            {
                var adjacentHexes = playerPos.Neighbors();
                bool stacked = scenario.playerArmyCount > 1;
                for (int i = 0; i < playerExtraCount; i++)
                {
                    var coord = stacked ? playerPos : (i < adjacentHexes.Count ? adjacentHexes[i] : playerPos);
                    var extraArmy = new ArmyData($"Attacker Army {i + 2}", coord, human.id);
                    var extraCmdr = new CommanderData($"Attacker Cmdr {i + 2}", scenario.playerCommanderSpecialty, human.id);
                    extraCmdr.level = scenario.playerCommanderLevel;
                    extraCmdr.rank = CommanderRankExtensions.RankForLevel(scenario.playerCommanderLevel);
                    extraArmy.commanderID = extraCmdr.id;
                    extraArmy.homeBaseID = attackerCity.id;
                    gameState.AddCommander(extraCmdr);
                    foreach (var kvp in army.playerArmy)
                    {
                        if (kvp.Value > 0)
                            extraArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
                    }
                    gameState.AddArmy(extraArmy);
                }
            }

            // 7. Create enemy (defender) army
            var defenderArmy = new ArmyData("Defender Army", enemyPos, ai.id);
            var defenderCmdr = new CommanderData("Defender Cmdr", scenario.enemyCommanderSpecialty, ai.id);
            defenderCmdr.level = scenario.enemyCommanderLevel;
            defenderCmdr.rank = CommanderRankExtensions.RankForLevel(scenario.enemyCommanderLevel);
            defenderArmy.commanderID = defenderCmdr.id;
            defenderArmy.homeBaseID = defenderCity.id;
            gameState.AddCommander(defenderCmdr);
            foreach (var kvp in army.enemyArmy)
            {
                if (kvp.Value > 0)
                    defenderArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
            }
            gameState.AddArmy(defenderArmy);

            // Extra defender armies (stacking)
            var extraDefenderArmies = new List<ArmyData>();
            int enemyExtraCount = Math.Abs(scenario.enemyArmyCount) - 1;
            if (enemyExtraCount > 0)
            {
                var adjacentHexes = enemyPos.Neighbors();
                bool stacked = scenario.enemyArmyCount > 1;
                for (int i = 0; i < enemyExtraCount; i++)
                {
                    var coord = stacked ? enemyPos : (i < adjacentHexes.Count ? adjacentHexes[i] : enemyPos);
                    var extraArmy = new ArmyData($"Defender Army {i + 2}", coord, ai.id);
                    var extraCmdr = new CommanderData($"Defender Cmdr {i + 2}", scenario.enemyCommanderSpecialty, ai.id);
                    extraCmdr.level = scenario.enemyCommanderLevel;
                    extraCmdr.rank = CommanderRankExtensions.RankForLevel(scenario.enemyCommanderLevel);
                    extraArmy.commanderID = extraCmdr.id;
                    extraArmy.homeBaseID = defenderCity.id;
                    gameState.AddCommander(extraCmdr);
                    foreach (var kvp in army.enemyArmy)
                    {
                        if (kvp.Value > 0)
                            extraArmy.AddMilitaryUnits(kvp.Key, kvp.Value);
                    }
                    gameState.AddArmy(extraArmy);
                    extraDefenderArmies.Add(extraArmy);
                }
            }

            // 8. Apply entrenchment
            if (scenario.enemyEntrenched)
            {
                var allDefenders = new List<ArmyData> { defenderArmy };
                allDefenders.AddRange(extraDefenderArmies);
                foreach (var a in allDefenders)
                {
                    var coverage = gameState.ComputeEntrenchmentCoverage(a);
                    a.isEntrenched = true;
                    a.entrenchedCoveredTiles = coverage;
                }
            }

            // 9. Place enemy building + garrison
            if (scenario.enemyBuilding.HasValue)
            {
                var buildingData = new BuildingData(scenario.enemyBuilding.Value, enemyPos, ai.id);
                buildingData.state = BuildingState.Completed;
                buildingData.health = buildingData.maxHealth;
                if (scenario.garrisonArchers > 0)
                    buildingData.AddToGarrison(MilitaryUnitType.Archer, scenario.garrisonArchers);
                gameState.AddBuilding(buildingData);
            }

            // 10. Initialize engine — full visibility for arena
            gameState.visibilityMode = VisibilityMode.Full;
            GameEngine.Instance.Setup(gameState);
            GameEngine.Instance.visionEngine.Update(0);

            // 11. Build visual grid
            gridRenderer.BuildGrid(gameState.mapData);

            // 12. Set camera bounds and focus on player army
            cameraController.SetMapBounds(arenaSize, arenaSize);
            cameraController.FocusOn(playerPos, 5f, false);

            // 13. Subscribe to state changes
            GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;

            // 14. Transition UI to gameplay
            uiManager.OnGameStarted(gameState);

            // 15. Game is now running
            gameStarted = true;

            Debug.Log("[GameSceneManager] Arena game started");
        }

        // ================================================================
        // Load Game
        // ================================================================

        private void LoadGame(string saveID)
        {
            var loadedState = SaveManager.Load(saveID);
            if (loadedState == null)
            {
                Debug.LogError("[GameSceneManager] Failed to load save: " + saveID);
                return;
            }

            // Unsubscribe from previous engine events
            if (gameStarted && GameEngine.Instance != null)
                GameEngine.Instance.OnStateChangesProduced -= HandleStateChanges;

            // Replace game state
            gameState = loadedState;

            // Initialize engine with loaded state
            GameEngine.Instance.Setup(gameState);
            GameEngine.Instance.visionEngine.Update(0);

            // Rebuild visual grid
            gridRenderer.BuildGrid(gameState.mapData);

            // Set camera bounds
            cameraController.SetMapBounds(gameState.mapData.width, gameState.mapData.height);

            // Focus on local player's city center
            if (gameState.localPlayerID.HasValue)
            {
                var cc = gameState.GetCityCenter(gameState.localPlayerID.Value);
                if (cc != null)
                    cameraController.FocusOn(cc.coordinate, 8f, false);
            }

            // Subscribe to state changes
            GameEngine.Instance.OnStateChangesProduced += HandleStateChanges;

            // Transition UI
            uiManager.OnGameStarted(gameState);

            // Reset auto-save timer
            lastAutoSaveGameTime = gameState.currentTime;

            // Game is now running
            gameStarted = true;

            Debug.Log($"[GameSceneManager] Loaded game from save {saveID}");
        }

        // ================================================================
        // Starting Entity Placement
        // ================================================================

        private void PlaceStartingEntities(
            List<PlayerStartPosition> startPositions,
            PlayerState human,
            PlayerState ai,
            MapGeneratorBase generator,
            StartingResources startingResources = StartingResources.Medium)
        {
            // Determine resource amounts based on tier
            int food, wood, ore, stone;
            switch (startingResources)
            {
                case StartingResources.Small:
                    food = 200; wood = 200; ore = 100; stone = 100;
                    break;
                case StartingResources.Large:
                    food = 500; wood = 500; ore = 300; stone = 300;
                    break;
                default: // Medium
                    food = 300; wood = 300; ore = 200; stone = 200;
                    break;
            }

            var playerList = new List<PlayerState> { human, ai };

            for (int i = 0; i < startPositions.Count && i < playerList.Count; i++)
            {
                var pos = startPositions[i];
                var player = playerList[i];

                // Place city center building
                var cc = new BuildingData(
                    BuildingType.CityCenter,
                    pos.coordinate,
                    player.id
                );
                cc.state = BuildingState.Completed;
                cc.health = cc.maxHealth;
                gameState.AddBuilding(cc);

                // Place starting resources around each player
                var resources = generator.GenerateStartingResources(pos.coordinate);
                foreach (var placement in resources)
                {
                    var rp = new ResourcePointData(
                        placement.coordinate,
                        placement.resourceType
                    );
                    gameState.resourcePoints[rp.id] = rp;
                    gameState.mapData.resourcePointIDs.Add(rp.id);
                    gameState.mapData.resourcePointCoordinates[rp.id] = placement.coordinate;
                }

                // Give starting resources
                player.SetResource(ResourceType.Food, food);
                player.SetResource(ResourceType.Wood, wood);
                player.SetResource(ResourceType.Ore, ore);
                player.SetResource(ResourceType.Stone, stone);

                // Spawn starting villagers (5 per player)
                var spawnCoord = gameState.mapData.FindNearestWalkable(pos.coordinate, 3, player.id, gameState);
                var villagerCoord = spawnCoord ?? pos.coordinate;
                var villagers = new VillagerGroupData("Villagers", villagerCoord, 5, player.id);
                gameState.AddVillagerGroup(villagers);
            }
        }

        // ================================================================
        // State Change Handler
        // ================================================================

        private void HandleStateChanges(StateChangeBatch batch)
        {
            // Route to UIManager for panel updates and notifications
            if (uiManager != null)
                uiManager.HandleStateChanges(batch);

            // Check for starvation warnings and game over
            foreach (var change in batch.changes)
            {
                var gameOverChange = change as GameOverChange;
                if (gameOverChange != null)
                {
                    HandleGameOver(gameOverChange);
                    break;
                }

                var starvationWarning = change as StarvationWarningChange;
                if (starvationWarning != null && gameState?.localPlayerID.HasValue == true
                    && starvationWarning.playerID == gameState.localPlayerID.Value)
                {
                    int secs = (int)starvationWarning.secondsRemaining;
                    uiManager?.ShowCommandFailure(
                        $"Starvation! Your people have no food. Defeat in {secs}s!");
                }
            }
        }

        private void HandleSurrenderRequested()
        {
            if (gameState == null || gameState.isGameOver) return;
            if (!gameState.localPlayerID.HasValue) return;

            var cmd = new Commands.SurrenderCommand(gameState.localPlayerID.Value);
            GameEngine.Instance.ExecuteCommand(cmd);
        }

        private void HandleGameOver(GameOverChange gameOverChange)
        {
            bool isVictory = gameState.localPlayerID.HasValue &&
                             gameOverChange.winnerID.HasValue &&
                             gameOverChange.winnerID.Value == gameState.localPlayerID.Value;

            // Use context-aware message based on winner/loser perspective
            string displayReason = gameOverChange.reasonType.DisplayMessage(isVictory);

            var stats = GatherGameOverStats();
            uiManager?.ShowGameOver(isVictory, displayReason, stats);

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            // Update online session status
            if (isOnlineGame && onlineGameID != null)
            {
                var uid = Engine.AuthService.Instance.CurrentUID;
                Engine.GameSessionService.Instance.UpdateSessionStatus(
                    onlineGameID, Data.GameSessionStatus.Finished);
                if (uid != null)
                {
                    Engine.GameSessionService.Instance.UpdatePlayerStatus(
                        onlineGameID, uid,
                        isVictory ? Data.PlayerSessionStatus.Active : Data.PlayerSessionStatus.Defeated);
                }

                // Clear rejoin state so the user isn't offered rejoin for a finished game
                pendingRejoinSession = null;
                uiManager?.SetRejoinVisible(false);
            }
#endif
        }

        private GameOverStats GatherGameOverStats()
        {
            if (gameState == null) return GameOverStats.Empty;

            float timePlayed = (float)(gameState.currentTime - gameState.gameStartTime);

            // Gather basic stats from player state
            int battlesWon = 0, battlesLost = 0, unitsKilled = 0, unitsLost = 0;
            int buildingsBuilt = 0, resourcesGathered = 0;

            if (gameState.localPlayerID.HasValue)
            {
                var player = gameState.GetPlayer(gameState.localPlayerID.Value);
                if (player != null)
                {
                    buildingsBuilt = gameState.GetBuildingsForPlayer(player.id).Count;
                    resourcesGathered = (int)(
                        player.GetResource(Models.ResourceType.Food) +
                        player.GetResource(Models.ResourceType.Wood) +
                        player.GetResource(Models.ResourceType.Stone) +
                        player.GetResource(Models.ResourceType.Ore));
                }
            }

            return new GameOverStats
            {
                timePlayed = timePlayed,
                battlesWon = battlesWon,
                battlesLost = battlesLost,
                unitsKilled = unitsKilled,
                unitsLost = unitsLost,
                buildingsBuilt = buildingsBuilt,
                resourcesGathered = resourcesGathered
            };
        }


        // ================================================================
        // Tile Interaction — hover, click, drag-to-move (#13)
        // ================================================================

        private void HandleTileHover()
        {
            // Skip hover if pointer is over UI
            if (uiManager != null && uiManager.IsPointerOverUI())
            {
                CursorManager.SetCursor(CursorType.Default);
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint((Vector3)mouse.position.ReadValue());
            mouseWorld.z = 0f;
            var coord = HexMetrics.WorldToHex(mouseWorld);

            if (hoveredTile.HasValue && hoveredTile.Value == coord) return;

            // Clear previous hover
            if (hoveredTile.HasValue)
            {
                var prevView = gridRenderer.GetTileView(hoveredTile.Value);
                if (prevView != null) prevView.SetHovered(false);
            }

            // Set new hover
            var view = gridRenderer.GetTileView(coord);
            if (view != null)
            {
                view.SetHovered(true);
                hoveredTile = coord;
                UpdateCursor(coord);
            }
            else
            {
                hoveredTile = null;
                CursorManager.SetCursor(CursorType.Default);
            }
        }

        private void HandleTileInteraction()
        {
            // Only block new interactions when pointer is over UI; allow ongoing drags to continue
            bool pointerOverUI = uiManager != null && uiManager.IsPointerOverUI();
            if (pointerOverUI && !mouseIsDown) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mousePos = mouse.position.ReadValue();

            // Track left mouse button press (#13)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (pointerOverUI) return; // Don't start new interaction on UI
                mouseIsDown = true;
                isDragSelecting = false;
                mouseDownScreenPos = mousePos;

                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
                mouseWorld.z = 0f;
                mouseDownTile = HexMetrics.WorldToHex(mouseWorld);

                // Check if press tile has an owned entity (for drag-to-move disambiguation)
                mouseDownOnOwnedEntity = HasOwnedEntityAt(mouseDownTile.Value);
            }

            // During left mouse hold — check for drag threshold
            if (mouseIsDown && mouse.leftButton.isPressed)
            {
                float dragDistance = Vector3.Distance(mousePos, mouseDownScreenPos);
                if (dragDistance >= DragThreshold && !isDragSelecting)
                {
                    // Disambiguate: drag on owned entity = drag-to-move, else = drag-select
                    if (!mouseDownOnOwnedEntity)
                    {
                        isDragSelecting = true;
                        uiManager.SelectionBox.BeginSelection(mouseDownScreenPos);
                    }
                }

                if (isDragSelecting)
                {
                    uiManager.SelectionBox.UpdateSelection(mousePos);
                    UpdateDragPreview();
                }
            }

            // On left mouse button release
            if (mouse.leftButton.wasReleasedThisFrame && mouseIsDown)
            {
                mouseIsDown = false;
                float dragDistance = Vector3.Distance(mousePos, mouseDownScreenPos);

                Vector3 mouseUpWorld = Camera.main.ScreenToWorldPoint(mousePos);
                mouseUpWorld.z = 0f;
                var releaseCoord = HexMetrics.WorldToHex(mouseUpWorld);

                if (isDragSelecting)
                {
                    // Resolve drag-select box
                    var screenRect = uiManager.SelectionBox.EndSelection();
                    ResolveDragSelect(screenRect);
                    isDragSelecting = false;
                }
                else if (dragDistance < DragThreshold)
                {
                    // Normal click — select tile, clear multi-selection
                    ClearMultiSelection();
                    HandleTileClick(releaseCoord);
                }
                else if (selectedEntityID.HasValue && mouseDownTile.HasValue)
                {
                    // Drag-to-move (#13): entity was selected, dragged to new tile
                    var view = gridRenderer.GetTileView(releaseCoord);
                    if (view != null && releaseCoord != mouseDownTile.Value)
                    {
                        var localID = gameState.localPlayerID ?? Guid.Empty;
                        var cmd = new MoveCommand(localID, selectedEntityID.Value,
                            releaseCoord, selectedEntityIsArmy);
                        UIManager.ExecutePlayerCommand(cmd);
                    }
                }

                mouseDownTile = null;
            }
        }

        private void HandleTileClick(HexCoordinate coord)
        {
            var view = gridRenderer.GetTileView(coord);
            if (view == null) return;

            // Clear any focused entity from a previous card click
            focusedEntityID = null;

            // Check if action mode (move/attack target) consumes the click
            if (uiManager != null && uiManager.HandleActionModeClick(coord))
            {
                ClearMultiSelection();
                return;
            }

            // Select tile
            gridRenderer.SelectTile(coord);
            selectedTile = coord;

            // Track selected entity for drag-to-move (#13)
            UpdateSelectedEntity(coord);

            // Notify UI — collect all owned entities on tile for panel
            if (uiManager != null)
            {
                uiManager.OnTileSelected(coord);

                var tileEntities = CollectOwnedEntities(coord);
                if (tileEntities.Count > 1)
                    uiManager.UpdateSelectedEntitiesPanel(null, false, tileEntities);
                else
                    uiManager.UpdateSelectedEntitiesPanel(selectedEntityID, selectedEntityIsArmy, null);
            }

            Debug.Log($"[GameSceneManager] Selected tile ({coord.q},{coord.r}) — {view.TerrainType}");
        }

        /// <summary>
        /// Track the first owned entity on the selected tile for drag-to-move (#13).
        /// </summary>
        private void UpdateSelectedEntity(HexCoordinate coord)
        {
            selectedEntityID = null;
            selectedEntityIsArmy = false;

            var localID = gameState.localPlayerID ?? Guid.Empty;

            // Prefer armies
            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localID && !army.isInCombat)
                    {
                        selectedEntityID = army.id;
                        selectedEntityIsArmy = true;
                        return;
                    }
                }
            }

            // Then villager groups
            var groups = gameState.GetVillagerGroups(coord);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group.ownerID.HasValue && group.ownerID.Value == localID)
                    {
                        selectedEntityID = group.id;
                        selectedEntityIsArmy = false;
                        return;
                    }
                }
            }
        }

        // ================================================================
        // Right-Click — move or attack
        // ================================================================

        private void HandleRightClick()
        {
            if (uiManager != null && uiManager.IsPointerOverUI()) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (!mouse.rightButton.wasReleasedThisFrame) return;

            // Skip if right-click was consumed by camera panning
            if (cameraController.IsRightClickPanning) return;

            // If action mode is active, right-click cancels it
            if (uiManager != null && uiManager.IsActionModeActive)
            {
                uiManager.CancelActionMode();
                return;
            }

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint((Vector3)mouse.position.ReadValue());
            mouseWorld.z = 0f;
            var targetCoord = HexMetrics.WorldToHex(mouseWorld);

            // Verify target tile exists
            var view = gridRenderer.GetTileView(targetCoord);
            if (view == null) return;

            var entities = GetSelectedEntities();
            if (entities.Count == 0) return;

            var localID = gameState.localPlayerID ?? Guid.Empty;

            if (HasEnemyAtCoord(targetCoord, localID))
            {
                // Attack with selected armies (villagers can't attack)
                foreach (var entity in entities)
                {
                    if (entity.isArmy)
                    {
                        var cmd = new AttackCommand(localID, entity.id, targetCoord);
                        UIManager.ExecutePlayerCommand(cmd);
                    }
                }
            }
            else if (!TryHandleResourceRightClick(targetCoord, localID, entities))
            {
                // Move each selected entity
                foreach (var entity in entities)
                {
                    var cmd = new MoveCommand(localID, entity.id, targetCoord, entity.isArmy);
                    UIManager.ExecutePlayerCommand(cmd);
                }
            }

            ClearMultiSelection();
        }

        // ================================================================
        // Context-Sensitive Cursor
        // ================================================================

        private void UpdateCursor(HexCoordinate coord)
        {
            var entities = GetSelectedEntities();
            if (entities.Count == 0)
            {
                CursorManager.SetCursor(CursorType.Default);
                return;
            }

            bool hasArmy = false, hasVillager = false;
            foreach (var e in entities)
            {
                if (e.isArmy) hasArmy = true;
                else hasVillager = true;
            }

            var localID = gameState.localPlayerID ?? Guid.Empty;

            // Priority: Attack > Gather > Move > Default
            if (hasArmy && HasEnemyAtCoord(coord, localID))
            {
                CursorManager.SetCursor(CursorType.Attack);
                return;
            }

            if (hasVillager)
            {
                var rp = gameState.GetResourcePoint(coord);
                if (rp != null && !rp.IsDepleted())
                {
                    if (rp.resourceType.IsHuntable() && rp.IsAlive())
                    {
                        CursorManager.SetCursor(CursorType.Hunt);
                        return;
                    }
                    if (rp.resourceType.IsGatherable())
                    {
                        // Only show gather cursor if camp coverage is satisfied
                        if (!rp.resourceType.RequiresCamp() ||
                            GameEngine.Instance.resourceEngine.HasCampCoverage(rp.coordinate, rp.resourceType, gameState))
                        {
                            CursorManager.SetCursor(CursorType.Gather);
                            return;
                        }
                    }
                }
            }

            if (gameState.mapData.IsWalkable(coord))
            {
                CursorManager.SetCursor(CursorType.Move);
                return;
            }

            CursorManager.SetCursor(CursorType.Default);
        }

        // ================================================================
        // Multi-Select Helpers
        // ================================================================

        private List<SelectedEntity> GetSelectedEntities()
        {
            // Focused entity takes priority (card clicked in SelectedEntitiesPanel)
            if (focusedEntityID.HasValue)
                return new List<SelectedEntity> { new SelectedEntity { id = focusedEntityID.Value, isArmy = focusedEntityIsArmy } };

            if (selectedEntities.Count > 0)
                return selectedEntities;

            // Fall back to single selected entity
            if (selectedEntityID.HasValue)
                return new List<SelectedEntity> { new SelectedEntity { id = selectedEntityID.Value, isArmy = selectedEntityIsArmy } };

            return new List<SelectedEntity>();
        }

        private bool HasEnemyAtCoord(HexCoordinate coord, Guid localID)
        {
            // Check armies
            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value != localID &&
                        gameState.GetDiplomacyStatus(localID, army.ownerID.Value) == DiplomacyStatus.Enemy)
                        return true;
                }
            }

            // Check buildings
            var building = gameState.GetBuilding(coord);
            if (building != null && building.ownerID.HasValue && building.ownerID.Value != localID &&
                gameState.GetDiplomacyStatus(localID, building.ownerID.Value) == DiplomacyStatus.Enemy)
                return true;

            // Check villager groups
            var groups = gameState.GetVillagerGroups(coord);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group.ownerID.HasValue && group.ownerID.Value != localID &&
                        gameState.GetDiplomacyStatus(localID, group.ownerID.Value) == DiplomacyStatus.Enemy)
                        return true;
                }
            }

            return false;
        }

        private bool TryHandleResourceRightClick(HexCoordinate targetCoord, Guid localID, List<SelectedEntity> entities)
        {
            var rp = gameState.GetResourcePoint(targetCoord);
            if (rp == null || rp.IsDepleted()) return false;

            bool isHuntable = rp.resourceType.IsHuntable() && rp.IsAlive();
            bool isGatherable = rp.resourceType.IsGatherable();

            if (!isHuntable && !isGatherable) return false;

            // Camp coverage check for resources that require it
            if (isGatherable && rp.resourceType.RequiresCamp())
            {
                if (!GameEngine.Instance.resourceEngine.HasCampCoverage(rp.coordinate, rp.resourceType, gameState))
                    return false;
            }

            foreach (var entity in entities)
            {
                if (!entity.isArmy)
                {
                    // Villagers get gather/hunt commands
                    if (isHuntable)
                    {
                        var cmd = new HuntCommand(localID, entity.id, rp.id);
                        UIManager.ExecutePlayerCommand(cmd);
                    }
                    else
                    {
                        var cmd = new GatherCommand(localID, entity.id, rp.id);
                        UIManager.ExecutePlayerCommand(cmd);
                    }
                }
                else
                {
                    // Armies in mixed selection just move to the tile
                    var cmd = new MoveCommand(localID, entity.id, targetCoord, true);
                    UIManager.ExecutePlayerCommand(cmd);
                }
            }

            return true;
        }

        private bool HasOwnedEntityAt(HexCoordinate coord)
        {
            var localID = gameState.localPlayerID ?? Guid.Empty;

            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localID)
                        return true;
                }
            }

            var groups = gameState.GetVillagerGroups(coord);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group.ownerID.HasValue && group.ownerID.Value == localID)
                        return true;
                }
            }

            return false;
        }

        private void ResolveDragSelect(Rect screenRect)
        {
            selectedEntities.Clear();

            if (screenRect.width < 5f && screenRect.height < 5f) return;

            var localID = gameState.localPlayerID ?? Guid.Empty;
            var cam = Camera.main;

            // Select owned armies
            foreach (var kvp in gameState.armies)
            {
                var army = kvp.Value;
                if (!army.ownerID.HasValue || army.ownerID.Value != localID) continue;
                if (army.isInCombat) continue;

                Vector3 worldPos = HexMetrics.HexToWorldPosition(army.coordinate);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                    selectedEntities.Add(new SelectedEntity { id = army.id, isArmy = true });
            }

            // Select owned villager groups
            foreach (var kvp in gameState.villagerGroups)
            {
                var group = kvp.Value;
                if (!group.ownerID.HasValue || group.ownerID.Value != localID) continue;

                Vector3 worldPos = HexMetrics.HexToWorldPosition(group.coordinate);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                    selectedEntities.Add(new SelectedEntity { id = group.id, isArmy = false });
            }

            if (selectedEntities.Count > 0)
            {
                var entityIDs = new List<Guid>();
                var entityList = new List<(Guid id, bool isArmy)>();
                foreach (var entity in selectedEntities)
                {
                    entityIDs.Add(entity.id);
                    entityList.Add((entity.id, entity.isArmy));
                }
                uiManager.OnMultiSelect(entityIDs);
                uiManager.UpdateSelectedEntitiesPanel(null, false, entityList);
            }
        }

        private void UpdateDragPreview()
        {
            if (uiManager == null || gameState == null) return;

            var screenRect = uiManager.SelectionBox.GetCurrentScreenRect();
            if (screenRect.width < 1f && screenRect.height < 1f)
            {
                if (dragPreviewEntityIDs.Count > 0)
                {
                    dragPreviewEntityIDs.Clear();
                    uiManager.ClearDragPreviewHighlights();
                }
                return;
            }

            var localID = gameState.localPlayerID ?? Guid.Empty;
            var cam = Camera.main;

            dragPreviewEntityIDs.Clear();

            // Check owned armies
            foreach (var kvp in gameState.armies)
            {
                var army = kvp.Value;
                if (!army.ownerID.HasValue || army.ownerID.Value != localID) continue;
                if (army.isInCombat) continue;

                Vector3 worldPos = HexMetrics.HexToWorldPosition(army.coordinate);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                    dragPreviewEntityIDs.Add(army.id);
            }

            // Check owned villager groups
            foreach (var kvp in gameState.villagerGroups)
            {
                var group = kvp.Value;
                if (!group.ownerID.HasValue || group.ownerID.Value != localID) continue;

                Vector3 worldPos = HexMetrics.HexToWorldPosition(group.coordinate);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                    dragPreviewEntityIDs.Add(group.id);
            }

            uiManager.UpdateDragPreview(dragPreviewEntityIDs);
        }

        private List<(Guid id, bool isArmy)> CollectOwnedEntities(HexCoordinate coord)
        {
            var result = new List<(Guid, bool)>();
            var localID = gameState.localPlayerID ?? Guid.Empty;

            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localID)
                        result.Add((army.id, true));
                }
            }

            var groups = gameState.GetVillagerGroups(coord);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group.ownerID.HasValue && group.ownerID.Value == localID)
                        result.Add((group.id, false));
                }
            }

            return result;
        }

        private void ClearMultiSelection()
        {
            focusedEntityID = null;
            selectedEntities.Clear();
            uiManager?.ClearMultiSelectHighlights();
            uiManager?.UpdateSelectedEntitiesPanel(null, false, null);
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            CleanupOnlineGame();
            Engine.MatchmakingService.Instance.Cleanup();

            if (GameEngine.Instance != null)
                GameEngine.Instance.OnStateChangesProduced -= HandleStateChanges;
        }

        private void OnApplicationQuit()
        {
            Engine.MatchmakingService.Instance.Cleanup();
            CleanupOnlineGame();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Engine.MatchmakingService.Instance.IsInQueue)
            {
                Engine.MatchmakingService.Instance.LeaveQueue(null);
            }
        }
    }
}
