// ============================================================================
// FILE: Engine/GameEngine.cs
// PURPOSE: Main authoritative game engine - no Unity rendering dependencies
//          C# port of GameEngine.swift
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.AI;

namespace Sporefront.Engine
{
    /// <summary>
    /// The authoritative game engine that processes all game logic.
    /// Pure C# data layer - no Unity rendering dependencies.
    /// </summary>
    public class GameEngine
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static GameEngine _instance;
        public static GameEngine Instance
        {
            get
            {
                if (_instance == null) _instance = new GameEngine();
                return _instance;
            }
        }

        // ================================================================
        // State
        // ================================================================

        public GameState gameState { get; private set; }

        // ================================================================
        // Online Mode State
        // ================================================================

        /// <summary>Whether the engine is running in online (command-streaming) mode.</summary>
        public bool IsOnlineMode { get; private set; }

        /// <summary>Monotonically increasing command sequence counter for online ordering.</summary>
        private int commandSequence;

        /// <summary>Command IDs that originated locally — used to filter self-echo from Firestore.</summary>
        private HashSet<Guid> localCommandIDs = new HashSet<Guid>();

        /// <summary>Whether this client is the host (runs AI, assigns sequences).</summary>
        private bool isHost;

        // ================================================================
        // Subsystem Engines
        // ================================================================

        public readonly MovementEngine movementEngine;
        public readonly CombatEngine combatEngine;
        public readonly ResourceEngine resourceEngine;
        public readonly ConstructionEngine constructionEngine;
        public readonly TrainingEngine trainingEngine;
        public readonly VisionEngine visionEngine;
        public readonly DominationEngine dominationEngine;

        private AIController aiController;

        // ================================================================
        // Events (replaces Swift delegate pattern)
        // ================================================================

        public event Action<StateChangeBatch> OnStateChangesProduced;
        public event Action<Guid, EngineCommandResult> OnCommandCompleted;
        public event Action<double> OnEngineTick;
        /// <summary>Fired when a remote command's state hash doesn't match local state (desync detected).</summary>
        public event Action<int, long, long> OnDesyncDetected; // (sequence, remoteHash, localHash)

        // ================================================================
        // Tick Timing
        // ================================================================

        private double lastTickTime;

        // ================================================================
        // Update Intervals
        // ================================================================

        private double lastVisionUpdate;
        private double lastBuildingUpdate;
        private double lastTrainingUpdate;
        private double lastCombatUpdate;
        private double lastResourceUpdate;
        private double lastMovementUpdate;
        private double lastAIUpdate;
        private double lastEntrenchmentUpdate;
        private double lastResearchCheck;
        private double lastWinConditionCheck;
        private double lastDominationUpdate;

        // Starvation tracking: per-player accumulated time with zero food
        private Dictionary<Guid, double> starvationTimers = new Dictionary<Guid, double>();

        private readonly double visionUpdateInterval = GameConfig.EngineIntervals.VisionUpdate;
        private readonly double buildingUpdateInterval = GameConfig.EngineIntervals.BuildingUpdate;
        private readonly double trainingUpdateInterval = GameConfig.EngineIntervals.TrainingUpdate;
        private readonly double combatUpdateInterval = GameConfig.EngineIntervals.CombatUpdate;
        private readonly double resourceUpdateInterval = GameConfig.EngineIntervals.ResourceUpdate;
        private readonly double movementUpdateInterval = GameConfig.EngineIntervals.MovementUpdate;
        private readonly double aiUpdateInterval = GameConfig.EngineIntervals.AIUpdate;
        private readonly double entrenchmentCheckInterval = GameConfig.Entrenchment.CheckInterval;
        private readonly double researchCheckInterval = 1.0;
        private readonly double winConditionCheckInterval = GameConfig.Online.WinConditionCheckInterval;
        private readonly double starvationThreshold = GameConfig.Online.StarvationThresholdSeconds;

        // Reusable list to avoid per-frame allocation
        private readonly List<StateChange> allChanges = new List<StateChange>();

        // ================================================================
        // Initialization
        // ================================================================

        private GameEngine()
        {
            movementEngine = new MovementEngine();
            combatEngine = new CombatEngine();
            resourceEngine = new ResourceEngine();
            constructionEngine = new ConstructionEngine();
            trainingEngine = new TrainingEngine();
            visionEngine = new VisionEngine();
            dominationEngine = new DominationEngine();
        }

        // ================================================================
        // Setup
        // ================================================================

        /// <summary>
        /// Initialize the engine with a game state. Call this before starting
        /// the game loop.
        /// </summary>
        public void Setup(GameState gameState)
        {
            this.gameState = gameState;
            lastTickTime = gameState.currentTime;

            // Initialize subsystem engines with game state reference
            movementEngine.Setup(gameState);
            combatEngine.Setup(gameState);
            resourceEngine.Setup(gameState);
            constructionEngine.Setup(gameState);
            trainingEngine.Setup(gameState);
            visionEngine.Setup(gameState);

            // Initialize domination engine if in domination mode
            if (gameState.gameMode.UsesControlZones())
                dominationEngine.Setup(gameState);

            // Initialize AI controller
            aiController = AIController.Instance;
            aiController.Setup(gameState);

            // Register AI players
            foreach (var player in gameState.GetAIPlayers())
            {
                aiController.RegisterAIPlayer(player.id, AIDifficulty.Medium);
            }

            var aiCount = gameState.GetAIPlayers().Count;
            DebugLog.Log($"GameEngine initialized with {gameState.players.Count} players ({aiCount} AI)");
        }

        /// <summary>
        /// Initialize the engine in online mode. Sets up the game state and
        /// enables command streaming to Firestore. The host runs AI locally
        /// and streams AI commands; non-host receives them as remote commands.
        /// </summary>
        public void SetupOnline(GameState gameState, int startSequence, bool isHost)
        {
            Setup(gameState);
            IsOnlineMode = true;
            commandSequence = startSequence;
            this.isHost = isHost;
            localCommandIDs = new HashSet<Guid>();

            DebugLog.Log($"GameEngine online mode: host={isHost}, startSequence={startSequence}");
        }

        /// <summary>
        /// Execute a command received from the Firestore command listener.
        /// Skips commands that originated locally (self-echo filtering).
        /// </summary>
        public void ExecuteRemoteCommand(OnlineCommand onlineCmd)
        {
            // Skip if we originated this command (self-echo from Firestore listener)
            Guid cmdId;
            if (Guid.TryParse(onlineCmd.commandID, out cmdId) && localCommandIDs.Contains(cmdId))
                return;

            // Validate sender UID matches the expected player for this command.
            // AI commands are sent by the host, so validate host UID instead.
            var currentSession = GameSessionService.Instance?.CurrentSession;
            if (currentSession != null)
            {
                // Reject commands with no sender UID — all legitimate commands must have one
                if (string.IsNullOrEmpty(onlineCmd.senderUID))
                {
                    DebugLog.Log($"Rejected command with no senderUID (seq {onlineCmd.sequence})");
                    return;
                }

                if (onlineCmd.isAICommand)
                {
                    // AI commands must come from the host
                    if (onlineCmd.senderUID != currentSession.hostUID)
                    {
                        DebugLog.Log($"Rejected AI command from non-host sender {onlineCmd.senderUID} (seq {onlineCmd.sequence})");
                        return;
                    }
                }
                else if (currentSession.players.TryGetValue(onlineCmd.senderUID, out var senderPlayer))
                {
                    // Player commands: senderUID's playerID must match the command's playerID
                    if (onlineCmd.playerID != senderPlayer.playerID)
                    {
                        DebugLog.Log($"Rejected command: sender {onlineCmd.senderUID} tried to act as player {onlineCmd.playerID} (seq {onlineCmd.sequence})");
                        return;
                    }
                }
                else
                {
                    // senderUID not found in session players — reject
                    DebugLog.Log($"Rejected command from unknown sender {onlineCmd.senderUID} (seq {onlineCmd.sequence})");
                    return;
                }
            }

            var cmd = onlineCmd.ToEngineCommand();
            if (cmd != null)
            {
                ExecuteCommand(cmd);

                // Desync detection: if the remote command includes a state hash, compare it
                if (onlineCmd.stateHash != 0 && gameState != null)
                {
                    long localHash = gameState.ComputeStateHash();
                    if (localHash != onlineCmd.stateHash)
                    {
                        DebugLog.Log($"DESYNC DETECTED at seq {onlineCmd.sequence}: remote={onlineCmd.stateHash}, local={localHash}");
                        OnDesyncDetected?.Invoke(onlineCmd.sequence, onlineCmd.stateHash, localHash);
                    }
                }
            }
            else
            {
                DebugLog.Log($"Failed to deserialize remote command: {onlineCmd.commandType} (seq {onlineCmd.sequence})");
            }
        }

        /// <summary>
        /// Reset the engine, clearing all state and timing.
        /// </summary>
        public void Reset()
        {
            gameState = null;
            lastTickTime = 0;
            lastVisionUpdate = 0;
            lastBuildingUpdate = 0;
            lastTrainingUpdate = 0;
            lastCombatUpdate = 0;
            lastResourceUpdate = 0;
            lastMovementUpdate = 0;
            lastAIUpdate = 0;
            lastEntrenchmentUpdate = 0;
            lastResearchCheck = 0;
            lastWinConditionCheck = 0;
            lastDominationUpdate = 0;
            starvationTimers.Clear();

            // Reset online mode state
            IsOnlineMode = false;
            commandSequence = 0;
            localCommandIDs.Clear();
            isHost = false;

            aiController?.Reset();
        }

        // ================================================================
        // Game Loop
        // ================================================================

        /// <summary>
        /// Main update function - call this every frame.
        /// In online mode, subsystem engines run independently on both clients using
        /// their local Time.timeAsDouble. Minor timing differences between machines
        /// are tolerated because: (1) game speed is locked to 1.0, (2) state mutations
        /// come from deterministic commands, not subsystem timing, and (3) desync
        /// detection (OnDesyncDetected) catches any actual divergence.
        /// </summary>
        public void Update(double currentTime)
        {
            if (gameState == null || gameState.isPaused || gameState.isGameOver) return;

            double adjustedTime = currentTime * gameState.gameSpeed;
            gameState.currentTime = adjustedTime;

            allChanges.Clear();

            // Vision updates (4x per second)
            if (adjustedTime - lastVisionUpdate >= visionUpdateInterval)
            {
                var visionChanges = visionEngine.Update(adjustedTime);
                allChanges.AddRange(visionChanges);
                lastVisionUpdate = adjustedTime;
            }

            // Movement updates (10x per second)
            if (adjustedTime - lastMovementUpdate >= movementUpdateInterval)
            {
                var movementChanges = movementEngine.Update(adjustedTime);
                allChanges.AddRange(movementChanges);

                // Check for armies that completed movement to a tile with active stack combat
                foreach (var change in movementChanges)
                {
                    var movedChange = change as ArmyMovedChange;
                    if (movedChange != null && (movedChange.path == null || movedChange.path.Count == 0))
                    {
                        var reinforceChanges = combatEngine.AddDefenderToStackCombat(
                            movedChange.armyID, movedChange.to, adjustedTime);
                        allChanges.AddRange(reinforceChanges);
                    }
                }

                lastMovementUpdate = adjustedTime;
            }

            // Building construction/upgrade updates (2x per second)
            if (adjustedTime - lastBuildingUpdate >= buildingUpdateInterval)
            {
                var constructionChanges = constructionEngine.Update(adjustedTime);
                allChanges.AddRange(constructionChanges);

                // Recalculate collection rates when a building upgrade completes (affects camp level bonus)
                foreach (var change in constructionChanges)
                {
                    var upgradeChange = change as BuildingUpgradeCompletedChange;
                    if (upgradeChange != null)
                    {
                        var building = gameState?.GetBuilding(upgradeChange.buildingID);
                        if (building != null && building.ownerID.HasValue)
                        {
                            resourceEngine.UpdateCollectionRates(building.ownerID.Value);
                        }
                    }
                }

                // Auto-gather: when a resource camp/farm finishes, start the builder gathering
                foreach (var change in constructionChanges)
                {
                    var completedChange = change as BuildingCompletedChange;
                    if (completedChange != null)
                    {
                        var building = gameState?.GetBuilding(completedChange.buildingID);
                        if (building != null && building.ownerID.HasValue)
                        {
                            var autoGatherChanges = TryAutoGatherAfterConstruction(building, constructionChanges);
                            allChanges.AddRange(autoGatherChanges);

                            // Remobilize stranded armies when a home base is completed
                            var homeBaseTypes = new HashSet<BuildingType>
                            {
                                BuildingType.CityCenter, BuildingType.WoodenFort, BuildingType.Castle
                            };
                            if (homeBaseTypes.Contains(building.buildingType))
                            {
                                var remobilizeChanges = RemobilizeStrandedArmies(building);
                                allChanges.AddRange(remobilizeChanges);
                            }
                        }
                    }
                }

                lastBuildingUpdate = adjustedTime;
            }

            // Training updates (1x per second)
            if (adjustedTime - lastTrainingUpdate >= trainingUpdateInterval)
            {
                var trainingChanges = trainingEngine.Update(adjustedTime);
                allChanges.AddRange(trainingChanges);
                lastTrainingUpdate = adjustedTime;
            }

            // Resource gathering updates (2x per second)
            if (adjustedTime - lastResourceUpdate >= resourceUpdateInterval)
            {
                // Process gather arrivals (villagers reaching resource points)
                var gatherArrivalChanges = resourceEngine.ProcessGatherArrivals();
                allChanges.AddRange(gatherArrivalChanges);

                var resourceChanges = resourceEngine.Update(adjustedTime);
                allChanges.AddRange(resourceChanges);
                lastResourceUpdate = adjustedTime;
            }

            // Combat updates (1x per second)
            if (adjustedTime - lastCombatUpdate >= combatUpdateInterval)
            {
                var combatChanges = combatEngine.Update(adjustedTime);
                allChanges.AddRange(combatChanges);
                lastCombatUpdate = adjustedTime;
            }

            // AI updates (2x per second)
            // In online mode, only the host runs AI locally — non-host receives
            // AI commands as remote commands from the Firestore command stream.
            if ((!IsOnlineMode || isHost) && adjustedTime - lastAIUpdate >= aiUpdateInterval)
            {
                // Process hunt arrivals (villagers reaching animals)
                aiController.ProcessHuntArrivals(gameState);

                // Generate and execute AI commands
                var aiCommands = aiController.Update(adjustedTime);
                foreach (var cmd in aiCommands)
                {
                    ExecuteCommand(cmd);
                }

                lastAIUpdate = adjustedTime;
            }

            // Entrenchment progress updates
            if (adjustedTime - lastEntrenchmentUpdate >= entrenchmentCheckInterval)
            {
                var entrenchmentChanges = UpdateEntrenchmentProgress(adjustedTime);
                allChanges.AddRange(entrenchmentChanges);
                lastEntrenchmentUpdate = adjustedTime;
            }

            // Research and unit upgrade completion updates (1x per second)
            if (adjustedTime - lastResearchCheck >= researchCheckInterval)
            {
                var researchChanges = UpdateResearchCompletion(adjustedTime);
                allChanges.AddRange(researchChanges);

                var unitUpgradeChanges = UpdateUnitUpgradeCompletion(adjustedTime);
                allChanges.AddRange(unitUpgradeChanges);

                lastResearchCheck = adjustedTime;
            }

            // Domination updates (1x per second, only in domination mode)
            if (gameState.gameMode.UsesControlZones() &&
                adjustedTime - lastDominationUpdate >= GameConfig.Domination.UpdateInterval)
            {
                var dominationChanges = dominationEngine.Update(adjustedTime);
                allChanges.AddRange(dominationChanges);
                lastDominationUpdate = adjustedTime;
            }

            // Win condition checks (1x per second)
            if (!gameState.isGameOver && adjustedTime - lastWinConditionCheck >= winConditionCheckInterval)
            {
                var winChanges = CheckWinConditions(adjustedTime);
                allChanges.AddRange(winChanges);
                lastWinConditionCheck = adjustedTime;
            }

            // Notify listeners if there are changes
            if (allChanges.Count > 0)
            {
                var batch = new StateChangeBatch(adjustedTime, new List<StateChange>(allChanges));
                OnStateChangesProduced?.Invoke(batch);
            }

            // Notify tick
            OnEngineTick?.Invoke(adjustedTime);
            lastTickTime = adjustedTime;
        }

        // ================================================================
        // Entrenchment Progress
        // ================================================================

        /// <summary>
        /// Check and update entrenchment progress for all entrenching armies.
        /// </summary>
        private List<StateChange> UpdateEntrenchmentProgress(double currentTime)
        {
            if (gameState == null) return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            foreach (var army in gameState.armies.Values)
            {
                if (!army.isEntrenching || !army.entrenchmentStartTime.HasValue)
                    continue;

                double elapsed = currentTime - army.entrenchmentStartTime.Value;
                double progress = Math.Min(1.0, elapsed / GameConfig.Entrenchment.BuildTime);

                if (progress >= 1.0)
                {
                    // Entrenchment complete - compute coverage before marking entrenched
                    // so getEntrenchedArmiesCovering won't include this army itself
                    var coverage = gameState.ComputeEntrenchmentCoverage(army);

                    army.isEntrenching = false;
                    army.isEntrenched = true;
                    army.entrenchmentStartTime = null;
                    army.entrenchedCoveredTiles = coverage;

                    changes.Add(new ArmyEntrenchedChange
                    {
                        armyID = army.id,
                        coordinate = army.coordinate
                    });
                    DebugLog.Log($"Army {army.name} is now entrenched at ({army.coordinate.q}, {army.coordinate.r})");
                }
                else
                {
                    changes.Add(new ArmyEntrenchmentProgressChange
                    {
                        armyID = army.id,
                        progress = progress
                    });
                }
            }

            return changes;
        }

        // ================================================================
        // Research Completion
        // ================================================================

        /// <summary>
        /// Check and complete any finished research for all players.
        /// </summary>
        private List<StateChange> UpdateResearchCompletion(double currentTime)
        {
            if (gameState == null) return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            foreach (var player in gameState.players.Values)
            {
                // Check if player has active research
                if (player.activeResearchType == null ||
                    !player.activeResearchStartTime.HasValue)
                    continue;

                ResearchType researchType;
                if (!Enum.TryParse<ResearchType>(player.activeResearchType, out researchType))
                    continue;

                // Check if research is complete
                double elapsed = currentTime - player.activeResearchStartTime.Value;
                if (elapsed >= researchType.ResearchTime())
                {
                    // Complete the research
                    player.CompleteResearch(player.activeResearchType);

                    changes.Add(new ResearchCompletedChange
                    {
                        playerID = player.id,
                        researchType = player.activeResearchType
                    });

                    if (player.isAI)
                    {
                        DebugLog.Log($"AI completed research: {researchType.DisplayName()}");
                    }
                    else
                    {
                        DebugLog.Log($"Player completed research: {researchType.DisplayName()}");
                    }
                }
            }

            return changes;
        }

        // ================================================================
        // Unit Upgrade Completion
        // ================================================================

        /// <summary>
        /// Check and complete any finished unit upgrades for all players.
        /// </summary>
        private List<StateChange> UpdateUnitUpgradeCompletion(double currentTime)
        {
            if (gameState == null) return StateChange.EmptyChanges;

            var changes = new List<StateChange>();

            foreach (var player in gameState.players.Values)
            {
                if (player.activeUnitUpgrade == null ||
                    !player.activeUnitUpgradeStartTime.HasValue)
                    continue;

                UnitUpgradeType upgradeType;
                if (!Enum.TryParse<UnitUpgradeType>(player.activeUnitUpgrade, out upgradeType))
                    continue;

                double elapsed = currentTime - player.activeUnitUpgradeStartTime.Value;
                if (elapsed >= upgradeType.UpgradeTime())
                {
                    player.CompleteUnitUpgrade(player.activeUnitUpgrade);

                    changes.Add(new UnitUpgradeCompletedChange
                    {
                        playerID = player.id,
                        unitType = upgradeType.GetUnitType().ToString(),
                        tier = upgradeType.Tier()
                    });

                    if (player.isAI)
                    {
                        DebugLog.Log($"AI completed unit upgrade: {upgradeType.DisplayName()}");
                    }
                    else
                    {
                        DebugLog.Log($"Player completed unit upgrade: {upgradeType.DisplayName()}");
                    }
                }
            }

            return changes;
        }

        // ================================================================
        // Win Condition Checks
        // ================================================================

        /// <summary>
        /// Check for game-ending conditions: city center destroyed or starvation.
        /// Modeled on GameSimulator win condition logic.
        /// </summary>
        private List<StateChange> CheckWinConditions(double currentTime)
        {
            if (gameState == null || gameState.isGameOver) return StateChange.EmptyChanges;

            // Don't check win conditions in the early game grace period
            double elapsed = currentTime - gameState.gameStartTime;
            if (elapsed < GameConfig.Online.WinConditionGracePeriod) return StateChange.EmptyChanges;

            var allPlayers = gameState.players.Values;

            // Need at least 2 players for win conditions
            if (allPlayers.Count < 2) return StateChange.EmptyChanges;

            // Single pass over all players: check city center, update starvation, emit warnings
            double checkInterval = winConditionCheckInterval;
            var warnings = new List<StateChange>();
            PlayerState starvationLoser = null;

            foreach (var player in allPlayers)
            {
                // --- City Center Destroyed (Conquest mode only) ---
                if (gameState.gameMode == GameMode.Conquest)
                {
                    var cityCenter = gameState.GetCityCenter(player.id);
                    if (cityCenter == null)
                    {
                        var opponent = allPlayers.FirstOrDefault(p => p.id != player.id);
                        if (opponent != null)
                        {
                            gameState.isGameOver = true;
                            return new List<StateChange>
                            {
                                new GameOverChange
                                {
                                    reason = GameOverReason.CityCenterDestroyed.DisplayMessage(),
                                    winnerID = opponent.id,
                                    reasonType = GameOverReason.CityCenterDestroyed
                                }
                            };
                        }
                    }
                }

                // --- Starvation timer update ---
                if (player.GetResource(Models.ResourceType.Food) <= 0)
                {
                    if (!starvationTimers.ContainsKey(player.id))
                        starvationTimers[player.id] = 0;
                    starvationTimers[player.id] += checkInterval;
                }
                else
                {
                    starvationTimers[player.id] = 0;
                }

                // --- Starvation warnings and threshold check ---
                double timer;
                if (starvationTimers.TryGetValue(player.id, out timer) && timer > 0)
                {
                    if (timer < starvationThreshold)
                    {
                        double remaining = starvationThreshold - timer;
                        if (remaining <= 45)
                        {
                            warnings.Add(new StarvationWarningChange
                            {
                                playerID = player.id,
                                secondsRemaining = remaining
                            });
                        }
                    }
                    else if (starvationLoser == null)
                    {
                        starvationLoser = player;
                    }
                }
            }

            // --- Resolve starvation game over (only if opponent is NOT also past threshold) ---
            if (starvationLoser != null)
            {
                var opponent = allPlayers.FirstOrDefault(p => p.id != starvationLoser.id);
                if (opponent != null)
                {
                    double opponentTimer;
                    starvationTimers.TryGetValue(opponent.id, out opponentTimer);
                    if (opponentTimer < starvationThreshold)
                    {
                        gameState.isGameOver = true;
                        return new List<StateChange>
                        {
                            new GameOverChange
                            {
                                reason = GameOverReason.Starvation.DisplayMessage(),
                                winnerID = opponent.id,
                                reasonType = GameOverReason.Starvation
                            }
                        };
                    }
                }
            }

            return warnings.Count > 0 ? warnings : StateChange.EmptyChanges;
        }

        // ================================================================
        // Command Execution
        // ================================================================

        /// <summary>
        /// Execute a command and return the result.
        /// </summary>
        public EngineCommandResult ExecuteCommand(IEngineCommand command)
        {
            if (gameState == null)
            {
                return EngineCommandResult.Failure("Game engine not initialized");
            }

            // Validate the command
            var validationResult = command.Validate(gameState);
            if (!validationResult.Succeeded)
            {
                return EngineCommandResult.Failure(validationResult.FailureReason ?? "Validation failed");
            }

            // Execute the command
            var changeBuilder = new StateChangeBuilder(gameState.currentTime, command.Id);
            var executionResult = command.Execute(gameState, changeBuilder);

            if (!executionResult.Succeeded)
            {
                return EngineCommandResult.Failure(executionResult.FailureReason ?? "Execution failed");
            }

            // Build and notify changes
            var batch = changeBuilder.Build();
            if (batch.changes.Count > 0)
            {
                OnStateChangesProduced?.Invoke(batch);
            }

            OnCommandCompleted?.Invoke(command.Id, EngineCommandResult.Success(batch.changes));

            // Online command streaming — submit locally-executed commands to Firestore
            // so the remote client can replay them. Add to localCommandIDs BEFORE
            // streaming to prevent self-echo race condition (listener firing before add).
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            if (IsOnlineMode)
            {
                bool isLocalOrigin = !localCommandIDs.Contains(command.Id);
                localCommandIDs.Add(command.Id);
                if (isLocalOrigin)
                {
                    StreamCommandToFirestore(command);
                }
            }
#else
            if (IsOnlineMode)
                localCommandIDs.Add(command.Id);
#endif

            return EngineCommandResult.Success(batch.changes);
        }

        // ================================================================
        // Online Command Streaming
        // ================================================================

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
        /// <summary>
        /// Serialize a locally-executed command and submit it to Firestore
        /// so the remote client can replay it. Also checks whether a snapshot
        /// should be created based on command count or elapsed time.
        /// </summary>
        private void StreamCommandToFirestore(IEngineCommand command)
        {
            commandSequence++;

            // Determine if this is an AI command — AI commands use the
            // AICommandEnvelope serialization path for type safety
            bool isAI = command.GetType().Namespace == "Sporefront.AI.Commands";

            Data.OnlineCommand onlineCmd;
            if (isAI)
            {
                var envelope = Data.AICommandEnvelope.From((BaseEngineCommand)command);
                if (envelope != null)
                    onlineCmd = Data.OnlineCommand.CreateFromAIEnvelope(commandSequence, envelope);
                else
                    onlineCmd = Data.OnlineCommand.CreateFromCommand(commandSequence, command, true);
            }
            else
            {
                onlineCmd = Data.OnlineCommand.CreateFromCommand(commandSequence, command);
            }

            var session = GameSessionService.Instance.CurrentSession;
            if (session == null) return;

            // Tag with sender UID for server-side validation
            onlineCmd.senderUID = AuthService.Instance?.CurrentUID;

            // Attach state hash periodically for desync detection
            if (commandSequence % GameConfig.Online.DesyncCheckInterval == 0 && gameState != null)
            {
                onlineCmd.stateHash = gameState.ComputeStateHash();
            }

            GameSessionService.Instance.SubmitCommand(
                session.gameID,
                onlineCmd,
                (success, error) =>
                {
                    if (!success)
                        DebugLog.Log($"Failed to submit online command: {error}");
                }
            );

            // Check if we should create a periodic snapshot for crash recovery
            if (GameSessionService.Instance.ShouldCreateSnapshot())
            {
                var snapshot = Data.GameSnapshot.Create(gameState, commandSequence);
                if (snapshot != null)
                {
                    GameSessionService.Instance.SaveSnapshot(
                        session.gameID,
                        snapshot,
                        (success, error) =>
                        {
                            if (!success)
                                DebugLog.Log($"Failed to save snapshot: {error}");
                        }
                    );

                    // Prune localCommandIDs to prevent unbounded growth.
                    // After a snapshot, old IDs are no longer needed for self-echo filtering
                    // since reconnecting clients will replay from the snapshot.
                    localCommandIDs.Clear();
                }
            }
        }
#endif

        // ================================================================
        // Convenience Methods
        // ================================================================

        /// <summary>
        /// Get the current game state.
        /// </summary>
        public GameState GetGameState()
        {
            return gameState;
        }

        /// <summary>
        /// Get a player by ID.
        /// </summary>
        public PlayerState GetPlayer(Guid id)
        {
            return gameState?.GetPlayer(id);
        }

        /// <summary>
        /// Get the local player.
        /// </summary>
        public PlayerState GetLocalPlayer()
        {
            return gameState?.GetLocalPlayer();
        }

        /// <summary>
        /// Check if the game is paused.
        /// </summary>
        public bool IsPaused()
        {
            return gameState?.isPaused ?? true;
        }

        /// <summary>
        /// Pause the game.
        /// In online mode, only the disconnect system can pause (via isPaused directly).
        /// Player-initiated pause is blocked to prevent desync.
        /// </summary>
        public void Pause()
        {
            if (gameState == null) return;
            if (IsOnlineMode) return; // Prevent desync — online pause managed by disconnect system
            gameState.isPaused = true;
        }

        /// <summary>
        /// Resume the game.
        /// In online mode, only the disconnect system can resume (via isPaused directly).
        /// </summary>
        public void Resume()
        {
            if (gameState == null) return;
            if (IsOnlineMode) return; // Prevent desync — online resume managed by disconnect system
            gameState.isPaused = false;
        }

        /// <summary>
        /// Set game speed (clamped to 0.1 - 10.0).
        /// Locked to 1.0 in online mode to prevent desync.
        /// </summary>
        public void SetGameSpeed(double speed)
        {
            if (gameState == null) return;
            if (IsOnlineMode) return; // Prevent desync — speed must be synchronized
            gameState.gameSpeed = Math.Max(0.1, Math.Min(speed, 10.0));
        }

        // ================================================================
        // Auto-Gather After Construction
        // ================================================================

        /// <summary>
        /// When a MiningCamp, LumberCamp, or Farm finishes construction, automatically
        /// start the builder villager gathering from the nearby resource point.
        /// </summary>
        private List<StateChange> RemobilizeStrandedArmies(BuildingData building)
        {
            var changes = new List<StateChange>();
            if (!building.ownerID.HasValue) return changes;

            Guid ownerID = building.ownerID.Value;
            foreach (var army in gameState.armies.Values)
            {
                if (!army.isStranded) continue;
                if (!army.ownerID.HasValue || army.ownerID.Value != ownerID) continue;

                var retreatPath = gameState.mapData.FindPath(
                    army.coordinate, building.coordinate, ownerID, gameState);

                if (retreatPath != null && retreatPath.Count > 0)
                {
                    army.isStranded = false;
                    army.isRetreating = true;
                    army.homeBaseID = building.id;
                    army.currentPath = retreatPath;
                    army.pathIndex = 0;
                    army.movementProgress = 0.0;

                    changes.Add(new ArmyRemobilizedChange
                    {
                        armyID = army.id,
                        destination = building.coordinate
                    });
                }
            }
            return changes;
        }

        private List<StateChange> TryAutoGatherAfterConstruction(BuildingData building, List<StateChange> constructionChanges)
        {
            var changes = new List<StateChange>();

            // Only auto-gather for resource-producing buildings
            if (building.buildingType != BuildingType.MiningCamp &&
                building.buildingType != BuildingType.LumberCamp &&
                building.buildingType != BuildingType.Farm)
                return changes;

            // Find the builder villager that was just released to idle at the building coordinate
            // Look for VillagerGroupTaskChangedChange with task="idle" in constructionChanges
            Guid? builderGroupID = null;
            foreach (var change in constructionChanges)
            {
                var taskChange = change as VillagerGroupTaskChangedChange;
                if (taskChange != null && taskChange.task == "idle")
                {
                    var group = gameState.GetVillagerGroup(taskChange.groupID);
                    if (group != null && group.coordinate.Equals(building.coordinate) &&
                        group.ownerID.HasValue && group.ownerID.Value == building.ownerID.Value)
                    {
                        builderGroupID = group.id;
                        break;
                    }
                }
            }

            if (!builderGroupID.HasValue) return changes;

            // Find the resource point
            ResourcePointData resourcePoint = null;

            if (building.buildingType == BuildingType.MiningCamp ||
                building.buildingType == BuildingType.LumberCamp)
            {
                // Resource point is at the building coordinate (preserved during placement)
                resourcePoint = gameState.GetResourcePoint(building.coordinate);
            }
            else if (building.buildingType == BuildingType.Farm)
            {
                // Farm: search adjacent tiles for Farmland resource point
                var neighbors = building.coordinate.Neighbors();
                foreach (var neighbor in neighbors)
                {
                    var rp = gameState.GetResourcePoint(neighbor);
                    if (rp != null && rp.resourceType == ResourcePointType.Farmland)
                    {
                        resourcePoint = rp;
                        break;
                    }
                }
            }

            if (resourcePoint == null) return changes;

            // Start gathering
            bool success = resourceEngine.StartGathering(builderGroupID.Value, resourcePoint.id);
            if (success)
            {
                changes.Add(new VillagerGroupTaskChangedChange
                {
                    groupID = builderGroupID.Value,
                    task = "gathering",
                    targetCoordinate = resourcePoint.coordinate
                });

                DebugLog.Log($"Auto-gather: Villager {builderGroupID.Value} started gathering at {building.buildingType} ({building.coordinate})");
            }

            return changes;
        }
    }
}
