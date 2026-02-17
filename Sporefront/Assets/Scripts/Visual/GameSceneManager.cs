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

            // 4. Show main menu
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
            GameEngine.Instance.Setup(gameState);

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
                case MapSize.Small:  return (25, 25);
                case MapSize.Medium: return (35, 35);
                case MapSize.Large:  return (50, 50);
                case MapSize.Huge:   return (65, 65);
                default:             return (35, 35);
            }
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
            if (uiManager != null && uiManager.IsPointerOverUI()) return;
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
            }
            else
            {
                hoveredTile = null;
            }
        }

        private void HandleTileInteraction()
        {
            // Skip interaction if pointer is over UI
            if (uiManager != null && uiManager.IsPointerOverUI()) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mousePos = mouse.position.ReadValue();

            // Track left mouse button press (#13)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                mouseIsDown = true;
                mouseDownScreenPos = mousePos;

                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
                mouseWorld.z = 0f;
                mouseDownTile = HexMetrics.WorldToHex(mouseWorld);
            }

            // On left mouse button release
            if (mouse.leftButton.wasReleasedThisFrame && mouseIsDown)
            {
                mouseIsDown = false;
                float dragDistance = Vector3.Distance(mousePos, mouseDownScreenPos);

                Vector3 mouseUpWorld = Camera.main.ScreenToWorldPoint(mousePos);
                mouseUpWorld.z = 0f;
                var releaseCoord = HexMetrics.WorldToHex(mouseUpWorld);

                if (dragDistance < DragThreshold)
                {
                    // Normal click — select tile
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
                        GameEngine.Instance.ExecuteCommand(cmd);
                    }
                }

                mouseDownTile = null;
            }
        }

        private void HandleTileClick(HexCoordinate coord)
        {
            var view = gridRenderer.GetTileView(coord);
            if (view == null) return;

            // Check if action mode (move/attack target) consumes the click
            if (uiManager != null && uiManager.HandleActionModeClick(coord))
                return;

            // Select tile
            gridRenderer.SelectTile(coord);
            selectedTile = coord;

            // Track selected entity for drag-to-move (#13)
            UpdateSelectedEntity(coord);

            // Notify UI
            if (uiManager != null)
                uiManager.OnTileSelected(coord);

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
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            if (GameEngine.Instance != null)
                GameEngine.Instance.OnStateChangesProduced -= HandleStateChanges;
        }
    }
}
