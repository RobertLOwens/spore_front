// ============================================================================
// FILE: Engine/GameEngine.cs
// PURPOSE: Main authoritative game engine - no Unity rendering dependencies
//          C# port of GameEngine.swift
// ============================================================================

using System;
using System.Collections.Generic;
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
        // Subsystem Engines
        // ================================================================

        public readonly MovementEngine movementEngine;
        public readonly CombatEngine combatEngine;
        public readonly ResourceEngine resourceEngine;
        public readonly ConstructionEngine constructionEngine;
        public readonly TrainingEngine trainingEngine;
        public readonly VisionEngine visionEngine;

        private AIController aiController;

        // ================================================================
        // Events (replaces Swift delegate pattern)
        // ================================================================

        public event Action<StateChangeBatch> OnStateChangesProduced;
        public event Action<Guid, EngineCommandResult> OnCommandCompleted;
        public event Action<double> OnEngineTick;

        // ================================================================
        // Tick Timing
        // ================================================================

        private double lastTickTime;
        private readonly double tickInterval = GameConfig.EngineIntervals.Tick;

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

        private readonly double visionUpdateInterval = GameConfig.EngineIntervals.VisionUpdate;
        private readonly double buildingUpdateInterval = GameConfig.EngineIntervals.BuildingUpdate;
        private readonly double trainingUpdateInterval = GameConfig.EngineIntervals.TrainingUpdate;
        private readonly double combatUpdateInterval = GameConfig.EngineIntervals.CombatUpdate;
        private readonly double resourceUpdateInterval = GameConfig.EngineIntervals.ResourceUpdate;
        private readonly double movementUpdateInterval = GameConfig.EngineIntervals.MovementUpdate;
        private readonly double aiUpdateInterval = GameConfig.EngineIntervals.AIUpdate;
        private readonly double entrenchmentCheckInterval = GameConfig.Entrenchment.CheckInterval;

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

            aiController?.Reset();
        }

        // ================================================================
        // Game Loop
        // ================================================================

        /// <summary>
        /// Main update function - call this every frame.
        /// </summary>
        public void Update(double currentTime)
        {
            if (gameState == null || gameState.isPaused) return;

            double adjustedTime = currentTime * gameState.gameSpeed;
            gameState.currentTime = adjustedTime;

            var allChanges = new List<StateChange>();

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
            if (adjustedTime - lastAIUpdate >= aiUpdateInterval)
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

            // Research completion updates (check all players every tick)
            var researchChanges = UpdateResearchCompletion(adjustedTime);
            allChanges.AddRange(researchChanges);

            // Unit upgrade completion updates (check all players every tick)
            var unitUpgradeChanges = UpdateUnitUpgradeCompletion(adjustedTime);
            allChanges.AddRange(unitUpgradeChanges);

            // Notify listeners if there are changes
            if (allChanges.Count > 0)
            {
                var batch = new StateChangeBatch(adjustedTime, allChanges);
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
            if (gameState == null) return new List<StateChange>();

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
            if (gameState == null) return new List<StateChange>();

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
            if (gameState == null) return new List<StateChange>();

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

            // Online session streaming skipped (not implemented)

            return EngineCommandResult.Success(batch.changes);
        }

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
        /// </summary>
        public void Pause()
        {
            if (gameState != null)
                gameState.isPaused = true;
        }

        /// <summary>
        /// Resume the game.
        /// </summary>
        public void Resume()
        {
            if (gameState != null)
                gameState.isPaused = false;
        }

        /// <summary>
        /// Set game speed (clamped to 0.1 - 10.0).
        /// </summary>
        public void SetGameSpeed(double speed)
        {
            if (gameState != null)
                gameState.gameSpeed = Math.Max(0.1, Math.Min(speed, 10.0));
        }
    }
}
