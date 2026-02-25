// ============================================================================
// FILE: Visual/GameSceneManager.cs
// PURPOSE: Root MonoBehaviour — bootstraps game, wires engine to visuals
//          Attach to a "GameManager" GameObject in the scene
//          Integrates UIManager, engine tick, drag-to-move (#13)
// ============================================================================

using System;
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

        // Focused entity (from clicking a card in SelectedEntitiesPanel)
        private Guid? focusedEntityID;
        private bool focusedEntityIsArmy;

        // Auto-save state
        private const double AutoSaveIntervalGameTime = 300.0; // 5 minutes game time
        private double lastAutoSaveGameTime;

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

            BootstrapMainMenu();
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

                // Auto-save check
                if (gameState != null && !gameState.isPaused &&
                    gameState.currentTime - lastAutoSaveGameTime >= AutoSaveIntervalGameTime)
                {
                    lastAutoSaveGameTime = gameState.currentTime;
                    SaveManager.AutoSave(gameState);
                }
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

            // 5. Show main menu
            uiManager.ShowMainMenu();

            Debug.Log("[GameSceneManager] Main menu shown");
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
            var ai = new PlayerState("AI Opponent", "8B3A3A", true);
            gameState.players[human.id] = human;
            gameState.players[ai.id] = ai;
            gameState.localPlayerID = human.id;

            // 3. Generate Arabia map scaled to configured size
            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            float areaRatio = (float)(width * height) / (35f * 35f);
            var mapConfig = new ArabiaMapConfig
            {
                treePocketCount = Mathf.RoundToInt(25 * areaRatio),
                mineralDepositCount = Mathf.RoundToInt(12 * areaRatio),
            };
            var generator = new ArabiaMapGenerator(width, height, seed, mapConfig);
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
            PlaceStartingEntities(startPositions, human, ai, generator);

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
            ArabiaMapGenerator generator)
        {
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
                player.SetResource(ResourceType.Food, 200);
                player.SetResource(ResourceType.Wood, 200);
                player.SetResource(ResourceType.Ore, 100);
                player.SetResource(ResourceType.Stone, 100);

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
            else
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
                    CursorManager.SetCursor(CursorType.Gather);
                    return;
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
                var coords = new List<HexCoordinate>();
                var entityList = new List<(Guid id, bool isArmy)>();
                foreach (var entity in selectedEntities)
                {
                    entityList.Add((entity.id, entity.isArmy));
                    if (entity.isArmy)
                    {
                        var army = gameState.GetArmy(entity.id);
                        if (army != null) coords.Add(army.coordinate);
                    }
                    else
                    {
                        var vg = gameState.GetVillagerGroup(entity.id);
                        if (vg != null) coords.Add(vg.coordinate);
                    }
                }
                uiManager.OnMultiSelect(coords);
                uiManager.UpdateSelectedEntitiesPanel(null, false, entityList);
            }
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
            if (GameEngine.Instance != null)
                GameEngine.Instance.OnStateChangesProduced -= HandleStateChanges;
        }
    }
}
