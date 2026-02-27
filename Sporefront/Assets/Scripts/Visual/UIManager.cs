// ============================================================================
// FILE: Visual/UIManager.cs
// PURPOSE: Central UI coordinator — creates Canvas, manages all panels,
//          routes tile selections and state changes
// ============================================================================

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class UIManager : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<GameSetupConfig> OnStartNewGame;
        public event Action<ArenaConfig> OnPlayArenaGame;
        public event Action<string> OnLoadGame;
        public event Action<Guid, bool> OnEntityFocused; // (entityID, isArmy)

        // ================================================================
        // Core HUD Panels
        // ================================================================

        private ResourceBarPanel resourceBar;
        private MenuBarPanel menuBar;
        private TileInfoPopup tileInfoPopup;
        private TileInfoPanel tileInfo;
        private BuildingDetailPanel buildingDetail;
        private ArmyDetailPanel armyDetail;
        private ActionPanel actionPanel;
        private NotificationPanel notifications;
        private SelectionRenderer selectionRenderer;
        private PathRenderer pathRenderer;
        private EntityRenderer entityRenderer;
        private EntrenchmentRenderer entrenchmentRenderer;
        private SelectionBoxRenderer selectionBoxRenderer;
        private SelectedEntitiesPanel selectedEntitiesPanel;
        private MiniMapPanel miniMap;

        // ================================================================
        // Navigation & Flow Panels
        // ================================================================

        private MainMenuPanel mainMenu;
        private GameSetupPanel gameSetup;
        private GameOverPanel gameOver;
        private AboutPanel about;

        // ================================================================
        // Overview Panels
        // ================================================================

        private BuildingsOverviewPanel buildingsOverview;
        private EntitiesOverviewPanel entitiesOverview;
        private MilitaryOverviewPanel militaryOverview;
        private ResourceOverviewPanel resourceOverview;
        private TrainingOverviewPanel trainingOverview;

        // ================================================================
        // Commander & Research
        // ================================================================

        private CommanderPanel commander;
        private ResearchTreePanel researchTree;

        // ================================================================
        // Combat Panels
        // ================================================================

        private LiveCombatPanel liveCombat;
        private CombatHistoryPanel combatHistory;
        private CombatDetailPanel combatDetail;

        // ================================================================
        // Action Panels
        // ================================================================

        private GatherPanel gatherPanel;
        private ReinforcePanel reinforcePanel;
        private VillagerDeployPanel villagerDeploy;
        private BuildVillagerSelectPanel buildVillagerSelect;
        private UpgradeVillagerSelectPanel upgradeVillagerSelect;

        // ================================================================
        // Settings & Notifications
        // ================================================================

        private SettingsPanel settings;
        private NotificationInboxPanel notificationInbox;
        private SaveLoadPanel saveLoad;

        // ================================================================
        // Auth & Account
        // ================================================================

        private AuthPanel authPanel;
        private DisplayNamePanel displayNamePanel;
        private AccountPanel accountPanel;

        // ================================================================
        // AI & Evolution
        // ================================================================

        private EvolutionPanel evolution;
        private GenomeSelectionPanel genomeSelection;
        private ArenaResultsPanel arenaResults;
        private SpectatorOverlay spectatorOverlay;

        // ================================================================
        // Tooltip
        // ================================================================

        private TooltipManager tooltip;

        // ================================================================
        // State
        // ================================================================

        private GameState gameState;
        private HexGridRenderer gridRenderer;
        private CameraController cameraController;
        private Canvas canvas;
        private Guid localPlayerID;
        private HexCoordinate? selectedCoord;
        private HexCoordinate? pendingBuildCoord;

        // Thread-safe queue for callbacks from background threads (e.g., ArenaSimulator)
        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(GameState state, HexGridRenderer hexGridRenderer,
            CameraController camController)
        {
            gameState = state;
            gridRenderer = hexGridRenderer;
            cameraController = camController;
            localPlayerID = state.localPlayerID ?? Guid.Empty;

            EnsureEventSystem();
            CreateCanvas();
            CreatePanels();
            WireEvents();

            // Create renderers (under grid renderer transform)
            var selGO = new GameObject("SelectionRenderer");
            selGO.transform.SetParent(gridRenderer.transform, false);
            selectionRenderer = selGO.AddComponent<SelectionRenderer>();

            var pathGO = new GameObject("PathRenderer");
            pathGO.transform.SetParent(gridRenderer.transform, false);
            pathRenderer = pathGO.AddComponent<PathRenderer>();

            var entityGO = new GameObject("EntityRenderer");
            entityGO.transform.SetParent(gridRenderer.transform, false);
            entityRenderer = entityGO.AddComponent<EntityRenderer>();
            selectionRenderer.SetEntityRenderer(entityRenderer);

            var entrenchGO = new GameObject("EntrenchmentRenderer");
            entrenchGO.transform.SetParent(gridRenderer.transform, false);
            entrenchmentRenderer = entrenchGO.AddComponent<EntrenchmentRenderer>();

            // Selection box (screen-space overlay on canvas)
            var selBoxGO = new GameObject("SelectionBoxRenderer");
            selBoxGO.transform.SetParent(transform, false);
            selectionBoxRenderer = selBoxGO.AddComponent<SelectionBoxRenderer>();
            selectionBoxRenderer.Initialize(canvas);
        }

        // ================================================================
        // Canvas + EventSystem
        // ================================================================

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("UICanvas");
            canvasGO.transform.SetParent(transform, false);

            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        private void CreatePanels()
        {
            var ct = canvas.transform;

            // ---- Core HUD ----

            resourceBar = CreatePanelComponent<ResourceBarPanel>("ResourceBar");
            InitPanel("ResourceBar", () => resourceBar.Initialize(ct));

            menuBar = CreatePanelComponent<MenuBarPanel>("MenuBar");
            InitPanel("MenuBar", () => menuBar.Initialize(ct));

            notifications = CreatePanelComponent<NotificationPanel>("Notifications");
            InitPanel("Notifications", () => notifications.Initialize(ct));

            tileInfoPopup = CreatePanelComponent<TileInfoPopup>("TileInfoPopup");
            InitPanel("TileInfoPopup", () => tileInfoPopup.Initialize(ct, canvas));

            tileInfo = CreatePanelComponent<TileInfoPanel>("TileInfo");
            InitPanel("TileInfo", () => tileInfo.Initialize(ct));

            buildingDetail = CreatePanelComponent<BuildingDetailPanel>("BuildingDetail");
            InitPanel("BuildingDetail", () => buildingDetail.Initialize(ct, localPlayerID));

            armyDetail = CreatePanelComponent<ArmyDetailPanel>("ArmyDetail");
            InitPanel("ArmyDetail", () => armyDetail.Initialize(ct, localPlayerID));

            actionPanel = CreatePanelComponent<ActionPanel>("ActionPanel");
            InitPanel("ActionPanel", () => actionPanel.Initialize(ct, localPlayerID));

            selectedEntitiesPanel = CreatePanelComponent<SelectedEntitiesPanel>("SelectedEntities");
            InitPanel("SelectedEntities", () => selectedEntitiesPanel.Initialize(ct));

            miniMap = CreatePanelComponent<MiniMapPanel>("MiniMap");
            InitPanel("MiniMap", () => miniMap.Initialize(ct, cameraController));

            // ---- Overviews ----

            buildingsOverview = CreatePanelComponent<BuildingsOverviewPanel>("BuildingsOverview");
            InitPanel("BuildingsOverview", () => buildingsOverview.Initialize(ct, localPlayerID));

            entitiesOverview = CreatePanelComponent<EntitiesOverviewPanel>("EntitiesOverview");
            InitPanel("EntitiesOverview", () => entitiesOverview.Initialize(ct, localPlayerID));

            militaryOverview = CreatePanelComponent<MilitaryOverviewPanel>("MilitaryOverview");
            InitPanel("MilitaryOverview", () => militaryOverview.Initialize(ct, localPlayerID));

            resourceOverview = CreatePanelComponent<ResourceOverviewPanel>("ResourceOverview");
            InitPanel("ResourceOverview", () => resourceOverview.Initialize(ct, localPlayerID));

            trainingOverview = CreatePanelComponent<TrainingOverviewPanel>("TrainingOverview");
            InitPanel("TrainingOverview", () => trainingOverview.Initialize(ct, localPlayerID));

            // ---- Commander & Research ----

            commander = CreatePanelComponent<CommanderPanel>("Commander");
            InitPanel("Commander", () => commander.Initialize(ct, localPlayerID));

            researchTree = CreatePanelComponent<ResearchTreePanel>("ResearchTree");
            InitPanel("ResearchTree", () => researchTree.Initialize(ct, localPlayerID));

            // ---- Combat ----

            liveCombat = CreatePanelComponent<LiveCombatPanel>("LiveCombat");
            InitPanel("LiveCombat", () => liveCombat.Initialize(ct, localPlayerID));

            combatHistory = CreatePanelComponent<CombatHistoryPanel>("CombatHistory");
            InitPanel("CombatHistory", () => combatHistory.Initialize(ct, localPlayerID));

            combatDetail = CreatePanelComponent<CombatDetailPanel>("CombatDetail");
            InitPanel("CombatDetail", () => combatDetail.Initialize(ct, localPlayerID));

            // ---- Action Panels ----

            gatherPanel = CreatePanelComponent<GatherPanel>("GatherPanel");
            InitPanel("GatherPanel", () => gatherPanel.Initialize(ct, localPlayerID));

            reinforcePanel = CreatePanelComponent<ReinforcePanel>("ReinforcePanel");
            InitPanel("ReinforcePanel", () => reinforcePanel.Initialize(ct, localPlayerID));

            villagerDeploy = CreatePanelComponent<VillagerDeployPanel>("VillagerDeploy");
            InitPanel("VillagerDeploy", () => villagerDeploy.Initialize(ct, localPlayerID));

            buildVillagerSelect = CreatePanelComponent<BuildVillagerSelectPanel>("BuildVillagerSelect");
            InitPanel("BuildVillagerSelect", () => buildVillagerSelect.Initialize(ct, localPlayerID));

            upgradeVillagerSelect = CreatePanelComponent<UpgradeVillagerSelectPanel>("UpgradeVillagerSelect");
            InitPanel("UpgradeVillagerSelect", () => upgradeVillagerSelect.Initialize(ct, localPlayerID));

            // ---- Settings & Notifications ----

            settings = CreatePanelComponent<SettingsPanel>("Settings");
            InitPanel("Settings", () => settings.Initialize(ct, localPlayerID));

            notificationInbox = CreatePanelComponent<NotificationInboxPanel>("NotificationInbox");
            InitPanel("NotificationInbox", () => notificationInbox.Initialize(ct, localPlayerID));

            // ---- AI & Evolution ----

            evolution = CreatePanelComponent<EvolutionPanel>("Evolution");
            InitPanel("Evolution", () => evolution.Initialize(ct));

            genomeSelection = CreatePanelComponent<GenomeSelectionPanel>("GenomeSelection");
            InitPanel("GenomeSelection", () => genomeSelection.Initialize(ct));

            arenaResults = CreatePanelComponent<ArenaResultsPanel>("ArenaResults");
            InitPanel("ArenaResults", () => arenaResults.Initialize(ct));

            spectatorOverlay = CreatePanelComponent<SpectatorOverlay>("SpectatorOverlay");
            InitPanel("SpectatorOverlay", () => spectatorOverlay.Initialize(ct));

            // ---- Save/Load ----

            saveLoad = CreatePanelComponent<SaveLoadPanel>("SaveLoad");
            InitPanel("SaveLoad", () => saveLoad.Initialize(ct));

            // ---- Full-screen Navigation (created last so they render on top) ----

            mainMenu = CreatePanelComponent<MainMenuPanel>("MainMenu");
            InitPanel("MainMenu", () => mainMenu.Initialize(ct));

            gameSetup = CreatePanelComponent<GameSetupPanel>("GameSetup");
            InitPanel("GameSetup", () => gameSetup.Initialize(ct));

            gameOver = CreatePanelComponent<GameOverPanel>("GameOver");
            InitPanel("GameOver", () => gameOver.Initialize(ct));

            about = CreatePanelComponent<AboutPanel>("About");
            InitPanel("About", () => about.Initialize(ct));

            // ---- Auth & Account (on top of about) ----

            authPanel = CreatePanelComponent<AuthPanel>("Auth");
            InitPanel("Auth", () => authPanel.Initialize(ct));

            displayNamePanel = CreatePanelComponent<DisplayNamePanel>("DisplayName");
            InitPanel("DisplayName", () => displayNamePanel.Initialize(ct));

            accountPanel = CreatePanelComponent<AccountPanel>("Account");
            InitPanel("Account", () => accountPanel.Initialize(ct));

            // ---- Tooltip (last, so it renders on top) ----
            tooltip = CreatePanelComponent<TooltipManager>("Tooltip");
            InitPanel("Tooltip", () => tooltip.Initialize(canvas));
        }

        private void InitPanel(string name, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] Failed to initialize {name}: {ex}");
            }
        }

        private T CreatePanelComponent<T>(string name) where T : MonoBehaviour
        {
            var go = new GameObject(name + "Manager");
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        // ================================================================
        // Event Wiring
        // ================================================================

        private void WireEvents()
        {
            // ---- TileInfoPopup (compact floating) ----
            tileInfoPopup.OnInfoRequested += (coord) =>
                tileInfo.Show(coord, gameState, localPlayerID);
            tileInfoPopup.OnBuildRequested += (coord) =>
                actionPanel.ShowBuildMenu(coord, gameState);
            tileInfoPopup.OnMoveRequested += (entityID) =>
            {
                // Determine if entity is army or villager
                bool isArmy = gameState.GetArmy(entityID) != null;
                actionPanel.ShowMoveTarget(entityID, isArmy);
            };
            tileInfoPopup.OnEntrenchRequested += (armyID) =>
            {
                var cmd = new EntrenchCommand(localPlayerID, armyID);
                ExecutePlayerCommand(cmd);
            };
            tileInfoPopup.OnAttackRequested += (armyID) =>
                actionPanel.ShowAttackTarget(armyID);
            tileInfoPopup.OnGatherSelectionRequested += (rpID) =>
            {
                gatherPanel.Show(gameState, rpID);
            };
            tileInfoPopup.OnRetreatRequested += (armyID) =>
            {
                var cmd = new RetreatCommand(localPlayerID, armyID);
                ExecutePlayerCommand(cmd);
            };
            tileInfoPopup.OnTrainVillagerRequested += (buildingID) =>
            {
                var cmd = new TrainVillagerCommand(localPlayerID, buildingID, 1);
                ExecutePlayerCommand(cmd);
            };
            tileInfoPopup.OnDeployVillagersRequested += (buildingID, count) =>
            {
                var cmd = new DeployVillagersCommand(localPlayerID, buildingID, count);
                ExecutePlayerCommand(cmd);
            };
            tileInfoPopup.OnUpgradeBuildingRequested += (buildingID) =>
            {
                var cmd = new UpgradeCommand(localPlayerID, buildingID, null);
                ExecutePlayerCommand(cmd);
            };

            // ---- TileInfoPanel (detail modal) ----
            tileInfo.OnCloseRequested += () => OnTileDeselected();
            tileInfo.OnBuildingDetailRequested += (id) => buildingDetail.Show(id, gameState);
            tileInfo.OnArmyDetailRequested += (id) => armyDetail.Show(id, gameState);
            tileInfo.OnBuildRequested += (coord) => actionPanel.ShowBuildMenu(coord, gameState);
            tileInfo.OnMoveRequested += (entityID, coord) =>
                actionPanel.ShowMoveTarget(entityID, false);
            tileInfo.OnArmyMoveRequested += (id) =>
                actionPanel.ShowMoveTarget(id, true);
            tileInfo.OnAttackRequested += (armyID, coord) =>
                actionPanel.ShowAttackTarget(armyID);
            tileInfo.OnGatherSelectionRequested += (rpID) =>
            {
                gatherPanel.Show(gameState, rpID);
            };
            tileInfo.OnTrainVillagerRequested += (buildingID) =>
            {
                var cmd = new TrainVillagerCommand(localPlayerID, buildingID, 1);
                ExecutePlayerCommand(cmd);
            };
            tileInfo.OnDeployVillagersRequested += (buildingID, count) =>
            {
                var cmd = new DeployVillagersCommand(localPlayerID, buildingID, count);
                ExecutePlayerCommand(cmd);
            };
            tileInfo.OnUpgradeBuildingRequested += (buildingID) =>
            {
                var cmd = new UpgradeCommand(localPlayerID, buildingID, null);
                ExecutePlayerCommand(cmd);
            };
            tileInfo.OnHuntRequested += (rpID) =>
            {
                gatherPanel.Show(gameState, rpID);
            };
            tileInfo.OnMoveEntityToTile += (entityID, destination, isArmy) =>
            {
                var cmd = new MoveCommand(localPlayerID, entityID, destination, isArmy);
                ExecutePlayerCommand(cmd);
            };
            tileInfo.OnAttackEntityToTile += (armyID, targetCoordinate) =>
            {
                var cmd = new AttackCommand(localPlayerID, armyID, targetCoordinate);
                ExecutePlayerCommand(cmd);
            };
            tileInfo.OnPreviewPathRequested += (entityID, destination, isArmy, isAttack) =>
            {
                // Look up entity coordinate
                HexCoordinate? entityCoord = null;
                if (isArmy)
                {
                    var army = gameState.GetArmy(entityID);
                    if (army != null) entityCoord = army.coordinate;
                }
                else
                {
                    var vg = gameState.GetVillagerGroup(entityID);
                    if (vg != null) entityCoord = vg.coordinate;
                }
                if (!entityCoord.HasValue) return;

                // Show entity hex highlight (attached to entity for movement tracking)
                Color highlightColor = isAttack ? SporefrontColors.SporeRed
                    : (isArmy ? SporefrontColors.SporeTeal : SporefrontColors.SporeTeal);
                selectionRenderer.ShowEntityHighlight(entityCoord.Value, highlightColor, entityID);

                // Find path and show preview
                var path = gameState.mapData.FindPath(entityCoord.Value, destination,
                    localPlayerID, gameState, allowImpassableDestination: isAttack);
                if (path != null && path.Count >= 1)
                {
                    // Prepend entity's current coordinate if not already the start
                    if (path.Count == 0 || !path[0].Equals(entityCoord.Value))
                        path.Insert(0, entityCoord.Value);
                    Color pathColor = isAttack ? SporefrontColors.SporeRed : SporefrontColors.SporeTeal;
                    pathRenderer.ShowPreviewPath(path, pathColor);
                }
            };
            tileInfo.OnPreviewPathCleared += () =>
            {
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
            };

            // ---- ArmyDetailPanel ----
            armyDetail.OnMoveRequested += (id) => actionPanel.ShowMoveTarget(id, true);
            armyDetail.OnAttackRequested += (id) => actionPanel.ShowAttackTarget(id);

            // ---- ActionPanel ----
            actionPanel.OnBuildPreviewChanged += (coord, occupied) =>
            {
                bool valid = true;
                foreach (var c in occupied)
                {
                    if (!gameState.CanBuildAt(c, localPlayerID))
                    {
                        valid = false;
                        break;
                    }
                }
                selectionRenderer.ShowBuildPreview(occupied, valid);
            };
            actionPanel.OnCancelled += () =>
            {
                selectionRenderer.ClearBuildPreview();
                ClearMultiSelectHighlights();
                selectedEntitiesPanel?.Hide();
            };
            actionPanel.OnBuildTypeSelected += (buildingType, coord, rotation) =>
            {
                pendingBuildCoord = coord;
                buildVillagerSelect.Show(gameState, buildingType, coord, rotation);
            };

            // ---- BuildVillagerSelectPanel ----
            buildVillagerSelect.OnVillagerSelected += (vgID, buildingType, coord, rotation) =>
            {
                var cmd = new BuildCommand(localPlayerID, buildingType, coord, rotation, vgID);
                ExecutePlayerCommand(cmd);
                pendingBuildCoord = null;
                selectionRenderer.ClearBuildPreview();
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
            };
            buildVillagerSelect.OnBack += () =>
            {
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
                if (pendingBuildCoord.HasValue)
                    actionPanel.ShowBuildMenu(pendingBuildCoord.Value, gameState);
            };
            buildVillagerSelect.OnClose += () =>
            {
                pendingBuildCoord = null;
                selectionRenderer.ClearBuildPreview();
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
            };
            buildVillagerSelect.OnPreviewPathRequested += (vgID, buildCoord) =>
            {
                var vg = gameState.GetVillagerGroup(vgID);
                if (vg == null) return;

                selectionRenderer.ShowEntityHighlight(vg.coordinate, SporefrontColors.SporeAmber, vgID);

                var path = gameState.mapData.FindPath(vg.coordinate, buildCoord,
                    localPlayerID, gameState);
                if (path != null && path.Count >= 1)
                {
                    if (path.Count == 0 || !path[0].Equals(vg.coordinate))
                        path.Insert(0, vg.coordinate);
                    pathRenderer.ShowPreviewPath(path, SporefrontColors.SporeAmber);
                }
            };
            buildVillagerSelect.OnPreviewPathCleared += () =>
            {
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
            };

            // ---- BuildingDetailPanel upgrade flow ----
            buildingDetail.OnUpgradeRequested += (buildingID, buildingType, coord, level) =>
            {
                upgradeVillagerSelect.Show(gameState, buildingID, buildingType, coord, level);
            };

            buildingDetail.OnCancelUpgradeRequested += (buildingID) =>
            {
                var cmd = new CancelUpgradeCommand(localPlayerID, buildingID);
                ExecutePlayerCommand(cmd);
            };

            buildingDetail.OnCancelDemolishRequested += (buildingID) =>
            {
                var cmd = new CancelDemolishCommand(localPlayerID, buildingID);
                ExecutePlayerCommand(cmd);
            };

            buildingDetail.OnDemolishRequested += (buildingID) =>
            {
                var cmd = new DemolishCommand(localPlayerID, buildingID);
                ExecutePlayerCommand(cmd);
            };

            // ---- UpgradeVillagerSelectPanel ----
            upgradeVillagerSelect.OnVillagerSelected += (vgID, buildingID) =>
            {
                var cmd = new UpgradeCommand(localPlayerID, buildingID, vgID);
                ExecutePlayerCommand(cmd);
            };
            upgradeVillagerSelect.OnClose += () => { };

            // ---- NotificationPanel (toast) ----
            notifications.OnNotificationClicked += (coord) =>
            {
                cameraController.FocusOn(coord, -1f, true);
                OnTileSelected(coord);
            };

            // ---- MainMenuPanel ----
            mainMenu.OnNewGame += () => { mainMenu.Hide(); gameSetup.Show(); };
            mainMenu.OnResumeGame += () => { mainMenu.Hide(); saveLoad.ShowLoad(); };
            mainMenu.OnLoadGame += () => { mainMenu.Hide(); saveLoad.ShowLoad(); };
            mainMenu.OnSettings += () => { settings.Show(); };
            mainMenu.OnAbout += () => { about.Show(); };
            mainMenu.OnEvolveAI += () => { mainMenu.Hide(); evolution.Show(); };
            mainMenu.OnSpectateAI += () => { mainMenu.Hide(); genomeSelection.Show(); };

            // ---- ResourceBarPanel (top-right buttons) ----
            resourceBar.OnNotificationClicked += () => notificationInbox.Show();
            resourceBar.OnCombatLogClicked += () => ShowCombatHistory();
            resourceBar.OnSettingsClicked += () => settings.Show();
            resourceBar.OnMainMenuClicked += () =>
            {
                resourceBar.Hide();
                menuBar.Hide();
                mainMenu.Show();
            };

            // ---- MenuBarPanel (bottom nav) ----
            menuBar.OnResearchClicked += () => researchTree.Show(gameState);
            menuBar.OnMilitaryClicked += () => militaryOverview.Show(gameState);
            menuBar.OnBuildingsClicked += () => buildingsOverview.Show(gameState);
            menuBar.OnEntitiesClicked += () => entitiesOverview.Show(gameState);
            menuBar.OnCommandersClicked += () => commander.Show(gameState);
            menuBar.OnResourcesClicked += () => resourceOverview.Show(gameState);
            menuBar.OnTrainingClicked += () => trainingOverview.Show(gameState);
            menuBar.OnCombatClicked += () => ShowCombatHistory();

            // ---- GameSetupPanel ----
            gameSetup.OnBack += () => { gameSetup.Hide(); mainMenu.Show(); };
            gameSetup.OnStartGame += (config) => { gameSetup.Hide(); OnStartNewGame?.Invoke(config); };
            gameSetup.OnPlayArena += (arenaConfig) =>
            {
                gameSetup.Hide();
                OnPlayArenaGame?.Invoke(arenaConfig);
            };
            gameSetup.OnAutoSim += (arenaConfig, runs) =>
            {
                gameSetup.Hide();
                ArenaSimulator.RunBatch(arenaConfig.armyConfig, arenaConfig.scenarioConfig, runs, (results) =>
                {
                    // RunBatch callback comes from a background thread — dispatch to main thread
                    mainThreadActions.Enqueue(() => arenaResults.ShowBatch(results, arenaConfig.scenarioConfig));
                });
            };

            // ---- GameOverPanel ----
            gameOver.OnReturnToMenu += () => { gameOver.Hide(); mainMenu.Show(); };

            // ---- Overview panels ----
            buildingsOverview.OnBuildingSelected += (id) =>
            {
                buildingsOverview.Hide();
                buildingDetail.Show(id, gameState);
            };

            entitiesOverview.OnEntitySelected += (id, isArmy) =>
            {
                entitiesOverview.Hide();
                if (isArmy)
                    armyDetail.Show(id, gameState);
                else
                    buildingDetail.Show(id, gameState);
            };

            trainingOverview.OnBuildingSelected += (id) =>
            {
                trainingOverview.Hide();
                buildingDetail.Show(id, gameState);
            };

            // ---- Commander ----
            commander.OnRecruitCommander += (specialty) =>
            {
                var cmd = new RecruitCommanderCommand(localPlayerID, specialty);
                ExecutePlayerCommand(cmd);
            };

            // ---- Research ----
            researchTree.OnStartResearch += (type) =>
            {
                // Find a suitable research building (Library, University)
                var researchBuilding = gameState.FindResearchBuilding(localPlayerID);
                if (researchBuilding != null)
                {
                    var cmd = new ResearchCommand(localPlayerID, type.ToString(), researchBuilding.id);
                    ExecutePlayerCommand(cmd);
                }
                else
                {
                    ShowCommandFailure("No research building available.");
                }
            };

            researchTree.OnCancelResearch += () =>
            {
                var cmd = new CancelResearchCommand(localPlayerID);
                ExecutePlayerCommand(cmd);
            };

            // ---- Combat panels ----
            liveCombat.OnFocusCombat += (coord) =>
            {
                cameraController.FocusOn(coord, -1f, true);
            };

            combatHistory.OnViewCombatDetail += (id) =>
            {
                // Search detailed records by ID
                string idStr = id.ToString();
                var detailedRecords = GameEngine.Instance.combatEngine.GetDetailedCombatHistory();
                DetailedCombatRecord found = null;
                foreach (var dr in detailedRecords)
                {
                    if (dr.Id == idStr)
                    {
                        found = dr;
                        break;
                    }
                }
                if (found != null)
                    combatDetail.Show(found);
            };
            combatHistory.OnViewLiveCombat += (id) =>
            {
                combatHistory.Hide();
                liveCombat.Show(id, gameState);
            };

            // ---- Action panels ----
            gatherPanel.OnGatherConfirmed += (vgID, rpID) =>
            {
                var cmd = new GatherCommand(localPlayerID, vgID, rpID);
                ExecutePlayerCommand(cmd);
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
                gatherPanel.Hide();
            };
            gatherPanel.OnHuntConfirmed += (vgID, rpID) =>
            {
                var cmd = new HuntCommand(localPlayerID, vgID, rpID);
                ExecutePlayerCommand(cmd);
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
                gatherPanel.Hide();
            };
            gatherPanel.OnPreviewPathRequested += (vgID, resourceCoord) =>
            {
                var vg = gameState.GetVillagerGroup(vgID);
                if (vg == null) return;

                selectionRenderer.ShowEntityHighlight(vg.coordinate, SporefrontColors.SporeAmber, vgID);

                var path = gameState.mapData.FindPath(vg.coordinate, resourceCoord,
                    localPlayerID, gameState);
                if (path != null && path.Count >= 1)
                {
                    if (path.Count == 0 || !path[0].Equals(vg.coordinate))
                        path.Insert(0, vg.coordinate);
                    pathRenderer.ShowPreviewPath(path, SporefrontColors.SporeAmber);
                }
            };
            gatherPanel.OnPreviewPathCleared += () =>
            {
                pathRenderer.ClearPreviewPath();
                selectionRenderer.HideEntityHighlight();
            };

            reinforcePanel.OnReinforceConfirmed += (srcArmyID, targetArmyID, units) =>
            {
                var cmd = new ReinforceArmyCommand(localPlayerID, srcArmyID, targetArmyID, units);
                ExecutePlayerCommand(cmd);
                reinforcePanel.Hide();
            };

            villagerDeploy.OnDeployNew += (buildingID, count) =>
            {
                var cmd = new DeployVillagersCommand(localPlayerID, buildingID, count);
                ExecutePlayerCommand(cmd);
                villagerDeploy.Hide();
            };
            villagerDeploy.OnJoinExisting += (buildingID, targetGroupID) =>
            {
                var building = gameState.GetBuilding(buildingID);
                int count = building != null ? building.villagerGarrison : 1;
                if (count > 0)
                {
                    var cmd = new JoinVillagerGroupCommand(localPlayerID, buildingID, targetGroupID, count);
                    ExecutePlayerCommand(cmd);
                }
                villagerDeploy.Hide();
            };
            villagerDeploy.OnMerge += (groupA, groupB, countA, countB) =>
            {
                ShowCommandFailure("Merge not yet implemented.");
            };

            // ---- Notification Inbox ----
            notificationInbox.OnNotificationTapped += (coord) =>
            {
                if (coord.HasValue)
                {
                    notificationInbox.Hide();
                    cameraController.FocusOn(coord.Value, -1f, true);
                    OnTileSelected(coord.Value);
                }
            };
            notificationInbox.OnClose += () =>
                resourceBar.UpdateNotificationBadge(notificationInbox.GetUnreadCount());
            notificationInbox.OnMarkAllRead += () =>
                resourceBar.UpdateNotificationBadge(notificationInbox.GetUnreadCount());

            // ---- Save/Load ----
            saveLoad.OnSaveRequested += (saveName) =>
            {
                if (gameState != null)
                {
                    bool success = SaveManager.Save(gameState, saveName);
                    if (success)
                        notifications.ShowNotification("Game Saved", saveName, SporefrontColors.SporeGreen);
                    else
                        ShowCommandFailure("Failed to save game.");
                }
            };
            saveLoad.OnLoadRequested += (saveID) =>
            {
                OnLoadGame?.Invoke(saveID);
            };
            saveLoad.OnClose += () =>
            {
                if (mainMenu.IsVisible) { /* already showing */ }
            };

            // ---- Evolution ----
            evolution.OnClose += () => { mainMenu.Show(); };

            // ---- Genome Selection ----
            genomeSelection.OnClose += () => { mainMenu.Show(); };

            // ---- Arena Results ----
            arenaResults.OnBack += () => { arenaResults.Hide(); gameSetup.Show(); };

            // ---- Spectator ----
            spectatorOverlay.OnExit += () => { mainMenu.Show(); };

            // ---- Auth & Account ----
            authPanel.OnAuthSuccess += () =>
            {
                authPanel.Hide();
                var state = AuthService.Instance.CurrentState;
                if (state == AuthState.NeedsUsername)
                    displayNamePanel.Show();
                else
                    mainMenu.Show();
            };
            displayNamePanel.OnUsernameSet += () =>
            {
                displayNamePanel.Hide();
                mainMenu.Show();
            };
            accountPanel.OnSignedOut += () =>
            {
                HideAllPanels();
                authPanel.Show();
            };
            accountPanel.OnAccountDeleted += () =>
            {
                HideAllPanels();
                authPanel.Show();
            };
            if (mainMenu != null)
            {
                mainMenu.OnAccount += () => { accountPanel.Show(); };
            }
            settings.OnAccountRequested += () => { accountPanel.Show(); };
            settings.OnSignOutRequested += () =>
            {
                HideAllPanels();
                authPanel.Show();
            };

            // ---- SelectedEntitiesPanel (card click → focus entity) ----
            selectedEntitiesPanel.OnEntityCardClicked += (entityID, isArmy) =>
            {
                HexCoordinate? coord = null;
                if (isArmy)
                {
                    var army = gameState.GetArmy(entityID);
                    if (army != null) coord = army.coordinate;
                }
                else
                {
                    var vg = gameState.GetVillagerGroup(entityID);
                    if (vg != null) coord = vg.coordinate;
                }

                if (coord.HasValue)
                {
                    selectionRenderer.ClearMultiEntityHighlight();
                    selectionRenderer.ShowEntityHighlight(coord.Value, SporefrontColors.SporeTeal, entityID);
                    tileInfoPopup.Show(coord.Value, gameState, localPlayerID);
                }

                OnEntityFocused?.Invoke(entityID, isArmy);
            };
        }

        // ================================================================
        // Tile Selection Routing
        // ================================================================

        public void OnTileSelected(HexCoordinate coord)
        {
            selectedCoord = coord;
            selectionRenderer.ClearMultiEntityHighlight();
            selectionRenderer.ShowSelection(coord);
            tileInfoPopup.Show(coord, gameState, localPlayerID);

            // Show entity highlight if tile has an owned entity
            Guid? foundEntityID = null;
            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID)
                    {
                        foundEntityID = army.id;
                        break;
                    }
                }
            }
            if (!foundEntityID.HasValue)
            {
                var villagers = gameState.GetVillagerGroups(coord);
                if (villagers != null)
                {
                    foreach (var vg in villagers)
                    {
                        if (vg.ownerID.HasValue && vg.ownerID.Value == localPlayerID)
                        {
                            foundEntityID = vg.id;
                            break;
                        }
                    }
                }
            }

            if (foundEntityID.HasValue)
                selectionRenderer.ShowEntityHighlight(coord, SporefrontColors.SporeTeal, foundEntityID.Value);
            else
                selectionRenderer.HideEntityHighlight();
        }

        public void OnTileDeselected()
        {
            selectedCoord = null;
            selectionRenderer.HideSelection();
            selectionRenderer.HideEntityHighlight();
            tileInfoPopup.Hide();
            tileInfo.Hide();
            selectedEntitiesPanel?.Hide();
        }

        /// <summary>
        /// Returns true if the click was consumed by an active action mode (move/attack target).
        /// </summary>
        public bool HandleActionModeClick(HexCoordinate coord)
        {
            return actionPanel.HandleTargetClick(coord);
        }

        public bool IsActionModeActive => actionPanel.IsActive;

        public SelectionBoxRenderer SelectionBox => selectionBoxRenderer;

        public void OnMultiSelect(List<Guid> entityIDs)
        {
            if (entityIDs.Count > 0)
                selectionRenderer.ShowMultiEntityHighlight(entityIDs, SporefrontColors.SporeTeal);
        }

        public void UpdateDragPreview(HashSet<Guid> entityIDs)
        {
            selectionRenderer.UpdateMultiEntityHighlight(entityIDs, SporefrontColors.SporeTeal);
        }

        public void ClearDragPreviewHighlights()
        {
            selectionRenderer.ClearMultiEntityHighlight();
        }

        public void ClearMultiSelectHighlights()
        {
            selectionRenderer.ClearMultiEntityHighlight();
        }

        public void UpdateSelectedEntitiesPanel(Guid? singleID, bool isArmy,
            List<(Guid id, bool isArmy)> multi)
        {
            selectedEntitiesPanel?.UpdateSelection(gameState, singleID, isArmy, multi);
        }

        public void CancelActionMode()
        {
            actionPanel.Cancel();
        }

        /// <summary>
        /// Hides all major panels — used when auth state changes require a full UI reset.
        /// </summary>
        public void HideAllPanels()
        {
            mainMenu.Hide();
            gameSetup.Hide();
            gameOver.Hide();
            about.Hide();
            settings.Hide();
            saveLoad.Hide();
            authPanel.Hide();
            displayNamePanel.Hide();
            accountPanel.Hide();
            resourceBar.Hide();
            menuBar.Hide();
        }

        /// <summary>
        /// Returns true if the pointer is over a UI element (for click gating).
        /// </summary>
        public bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // ================================================================
        // Public Panel Access — for GameSceneManager and external callers
        // ================================================================

        /// <summary>
        /// Called by GameSceneManager after game world is populated.
        /// Refreshes localPlayerID, hides main menu, shows gameplay HUD.
        /// </summary>
        public void OnGameStarted(GameState state)
        {
            gameState = state;
            localPlayerID = state.localPlayerID ?? Guid.Empty;

            // 1. Hide any modal/overlay panels first
            mainMenu.Hide();
            saveLoad.Hide();

            // 2. Show essential HUD immediately
            resourceBar.Show();
            resourceBar.Refresh(state, localPlayerID);
            menuBar.Show();

            // 3. Propagate localPlayerID to all panels that cached it during Initialize
            tileInfoPopup.UpdateLocalPlayerID(localPlayerID);
            buildingDetail.UpdateLocalPlayerID(localPlayerID);
            armyDetail.UpdateLocalPlayerID(localPlayerID);
            actionPanel.UpdateLocalPlayerID(localPlayerID);
            buildingsOverview.UpdateLocalPlayerID(localPlayerID);
            entitiesOverview.UpdateLocalPlayerID(localPlayerID);
            militaryOverview.UpdateLocalPlayerID(localPlayerID);
            resourceOverview.UpdateLocalPlayerID(localPlayerID);
            trainingOverview.UpdateLocalPlayerID(localPlayerID);
            commander.UpdateLocalPlayerID(localPlayerID);
            researchTree.UpdateLocalPlayerID(localPlayerID);
            liveCombat.UpdateLocalPlayerID(localPlayerID);
            combatHistory.UpdateLocalPlayerID(localPlayerID);
            combatDetail.UpdateLocalPlayerID(localPlayerID);
            gatherPanel.UpdateLocalPlayerID(localPlayerID);
            reinforcePanel.UpdateLocalPlayerID(localPlayerID);
            villagerDeploy.UpdateLocalPlayerID(localPlayerID);
            buildVillagerSelect.UpdateLocalPlayerID(localPlayerID);
            upgradeVillagerSelect.UpdateLocalPlayerID(localPlayerID);
            settings.UpdateLocalPlayerID(localPlayerID);
            notificationInbox.UpdateLocalPlayerID(localPlayerID);
            selectedEntitiesPanel.UpdateLocalPlayerID(localPlayerID);
            miniMap.UpdateLocalPlayerID(localPlayerID);

            // Build and show mini map
            miniMap.BuildMap(state, localPlayerID);
            miniMap.Show();

            // Initialize fog of war
            if (state.visibilityMode == VisibilityMode.Normal)
            {
                entityRenderer.SetFogContext(localPlayerID, true);
                var localPlayer = state.GetPlayer(localPlayerID);
                if (localPlayer != null)
                    gridRenderer.ApplyInitialFog(localPlayer);
            }
            else
            {
                entityRenderer.SetFogContext(localPlayerID, false);
            }

            // Initial entity render (respects fog context set above)
            entityRenderer.UpdateEntities(state);
            entrenchmentRenderer?.UpdateEntrenchment(state, localPlayerID);
        }

        public void ShowMainMenu() => mainMenu.Show();
        public void HideMainMenu() => mainMenu.Hide();
        public void ShowGameSetup() => gameSetup.Show();
        public void ShowGameOver(bool isVictory, string reason, GameOverStats stats)
        {
            gameOver.Show(isVictory, reason, stats);

            // Record stats to Firestore if signed in
            if (AuthService.Instance.CurrentState == AuthState.SignedIn)
            {
                var endStats = new GameEndStats
                {
                    timePlayed = stats.timePlayed,
                    battlesWon = stats.battlesWon,
                    battlesLost = stats.battlesLost,
                    unitsKilled = stats.unitsKilled,
                    unitsLost = stats.unitsLost,
                    buildingsBuilt = stats.buildingsBuilt,
                    resourcesGathered = stats.resourcesGathered
                };
                UserStatsService.Instance.RecordGameEnd(
                    AuthService.Instance.CurrentUID, isVictory, endStats, reason, null);
            }
        }
        public void ShowSettings() => settings.Show();
        public void ShowAbout() => about.Show();

        public void ShowAuth() => authPanel.Show();
        public void ShowDisplayName() => displayNamePanel.Show();
        public void ShowAccount() => accountPanel.Show();

        public void ShowBuildingsOverview() => buildingsOverview.Show(gameState);
        public void ShowEntitiesOverview() => entitiesOverview.Show(gameState);
        public void ShowMilitaryOverview() => militaryOverview.Show(gameState);
        public void ShowResourceOverview() => resourceOverview.Show(gameState);
        public void ShowTrainingOverview() => trainingOverview.Show(gameState);

        public void ShowCommander() => commander.Show(gameState);
        public void ShowResearchTree() => researchTree.Show(gameState);

        public void ShowLiveCombat(Guid combatID) => liveCombat.Show(combatID, gameState);
        public void ShowCombatHistory() => combatHistory.Show(gameState);

        public void ShowGatherPanel(Guid resourcePointID) =>
            gatherPanel.Show(gameState, resourcePointID);
        public void ShowReinforcePanel(Guid targetArmyID) =>
            reinforcePanel.Show(gameState, targetArmyID);
        public void ShowVillagerDeploy(Guid buildingID) =>
            villagerDeploy.Show(gameState, buildingID);

        public void ShowCommandFailure(string reason)
        {
            var notif = new CommandFailedNotification(reason);
            notifications.ShowNotification(notif);
        }

        /// <summary>
        /// Execute a player command and show a toast if it fails.
        /// Use this instead of GameEngine.Instance.ExecuteCommand for player-initiated commands.
        /// </summary>
        public static void ExecutePlayerCommand(IEngineCommand command)
        {
            var result = GameEngine.Instance.ExecuteCommand(command);
            if (!result.Succeeded)
            {
                var uiManager = UnityEngine.Object.FindObjectOfType<UIManager>();
                uiManager?.ShowCommandFailure(result.FailureReason ?? "Command failed");
            }
        }

        public void ShowNotificationInbox() => notificationInbox.Show();

        public void ShowSavePanel() => saveLoad.ShowSave();
        public void ShowLoadPanel() => saveLoad.ShowLoad();

        public void ShowEvolution() => evolution.Show();
        public void ShowGenomeSelection() => genomeSelection.Show();
        public void ShowSpectatorOverlay() => spectatorOverlay.Show();
        public void HideSpectatorOverlay() => spectatorOverlay.Hide();

        // ================================================================
        // State Change Handling
        // ================================================================

        public void HandleStateChanges(StateChangeBatch batch)
        {
            // Single-pass: classify all changes, process fog updates and notifications inline
            StateChangeFlags flags = StateChangeFlags.None;

            foreach (var change in batch.changes)
            {
                flags |= StateChange.ClassifyChange(change);

                // Inline fog tile updates
                if (change is FogOfWarUpdatedChange fogChange && fogChange.playerID == localPlayerID)
                {
                    VisibilityLevel level;
                    switch (fogChange.visibility)
                    {
                        case "visible":   level = VisibilityLevel.Visible; break;
                        case "explored":  level = VisibilityLevel.Explored; break;
                        default:          level = VisibilityLevel.Unexplored; break;
                    }
                    gridRenderer.UpdateTileFog(fogChange.coordinate, level);
                    miniMap.MarkTerrainDirty(fogChange.coordinate);
                }

                // Inline notification routing
                RouteNotification(change);
            }

            // --- Conditional panel refreshes based on accumulated flags ---

            const StateChangeFlags resourceFlags = StateChangeFlags.Resources | StateChangeFlags.Buildings
                | StateChangeFlags.Training | StateChangeFlags.Villagers;
            if ((flags & resourceFlags) != 0)
                resourceBar.Refresh(gameState, localPlayerID);

            // Core info panels — refresh on broad entity/building/fog changes
            const StateChangeFlags tileFlags = StateChangeFlags.Buildings | StateChangeFlags.Armies
                | StateChangeFlags.Villagers | StateChangeFlags.FogOfWar | StateChangeFlags.Combat
                | StateChangeFlags.Garrison;
            if ((flags & tileFlags) != 0)
            {
                if (tileInfoPopup.IsVisible) tileInfoPopup.Refresh(gameState);
                if (tileInfo.IsVisible) tileInfo.Refresh(gameState);
                if (buildingDetail.IsVisible) buildingDetail.Refresh(gameState);
                if (armyDetail.IsVisible) armyDetail.Refresh(gameState);
            }

            // Overview panels
            if ((flags & (StateChangeFlags.Buildings | StateChangeFlags.Garrison)) != 0)
                if (buildingsOverview.IsVisible) buildingsOverview.Refresh(gameState);

            if ((flags & StateChangeFlags.Resources) != 0)
                if (resourceOverview.IsVisible) resourceOverview.Refresh(gameState);

            if ((flags & (StateChangeFlags.Armies | StateChangeFlags.Combat)) != 0)
                if (militaryOverview.IsVisible) militaryOverview.Refresh(gameState);

            if ((flags & StateChangeFlags.Training) != 0)
                if (trainingOverview.IsVisible) trainingOverview.Refresh(gameState);

            if ((flags & (StateChangeFlags.Armies | StateChangeFlags.Villagers)) != 0)
                if (entitiesOverview.IsVisible) entitiesOverview.Refresh(gameState);

            // Detail panels
            if ((flags & StateChangeFlags.Research) != 0)
                if (researchTree.IsVisible) researchTree.Refresh(gameState);

            if ((flags & (StateChangeFlags.Commander | StateChangeFlags.Armies)) != 0)
                if (commander.IsVisible) commander.Refresh(gameState);

            if ((flags & StateChangeFlags.Combat) != 0)
            {
                if (liveCombat.IsVisible) liveCombat.Refresh(gameState);
                if (combatHistory.IsVisible) combatHistory.Refresh(gameState);
            }

            // Action panels
            const StateChangeFlags actionFlags = StateChangeFlags.Villagers | StateChangeFlags.Buildings
                | StateChangeFlags.Armies | StateChangeFlags.Resources;
            if ((flags & actionFlags) != 0)
            {
                if (gatherPanel.IsVisible) gatherPanel.Refresh(gameState);
                if (reinforcePanel.IsVisible) reinforcePanel.Refresh(gameState);
                if (villagerDeploy.IsVisible) villagerDeploy.Refresh(gameState);
                if (buildVillagerSelect.IsVisible) buildVillagerSelect.Refresh(gameState);
                if (upgradeVillagerSelect.IsVisible) upgradeVillagerSelect.Refresh(gameState);
            }

            // Map renderers — conditional
            if ((flags & StateChangeFlags.Movement) != 0)
                pathRenderer.UpdatePaths(gameState, localPlayerID);

            if ((flags & (StateChangeFlags.Armies | StateChangeFlags.Buildings
                | StateChangeFlags.Villagers | StateChangeFlags.FogOfWar)) != 0)
                entityRenderer.UpdateEntities(gameState);

            if ((flags & StateChangeFlags.Entrenchment) != 0)
                entrenchmentRenderer?.UpdateEntrenchment(gameState, localPlayerID);

            // Mini map — incremental refresh
            if (miniMap.IsVisible)
            {
                bool hasEntityChange = (flags & (StateChangeFlags.Armies | StateChangeFlags.Buildings
                    | StateChangeFlags.Villagers)) != 0;
                bool hasFogChange = (flags & StateChangeFlags.FogOfWar) != 0;
                if (hasEntityChange || hasFogChange)
                    miniMap.RefreshIncremental(gameState, hasEntityChange, hasFogChange);
            }

            // Update notification badge
            resourceBar.UpdateNotificationBadge(notificationInbox.GetUnreadCount());
        }

        // ================================================================
        // Notification Routing
        // ================================================================

        private void RouteNotification(StateChange change)
        {
            if (change is BuildingCompletedChange bcc)
            {
                var building = gameState.GetBuilding(bcc.buildingID);
                if (building != null && building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var notif = new BuildingCompletedNotification(
                        building.buildingType, building.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is TrainingCompletedChange tcc)
            {
                var building = gameState.GetBuilding(tcc.buildingID);
                if (building != null && building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var notif = new TrainingCompletedNotification(
                        tcc.unitType, tcc.quantity, building.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is ResourcePointDepletedChange rpdc)
            {
                var notif = new ResourcePointDepletedNotification(
                    rpdc.resourceType, rpdc.coordinate);
                notifications.ShowNotification(notif);
                notificationInbox.AddNotification(notif);
            }
            else if (change is CombatStartedChange csc)
            {
                // Notify if local player is a defender
                var defender = gameState.GetArmy(csc.defenderID);
                if (defender != null && defender.ownerID.HasValue && defender.ownerID.Value == localPlayerID)
                {
                    var notif = new ArmyAttackedNotification(
                        defender.name, csc.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is ResearchCompletedChange rcc)
            {
                if (rcc.playerID == localPlayerID)
                {
                    var notif = new ResearchCompletedNotification(rcc.researchType);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is UnitUpgradeCompletedChange uucc)
            {
                if (uucc.playerID == localPlayerID)
                {
                    var notif = new ResearchCompletedNotification($"{uucc.unitType} Tier {uucc.tier} Upgrade");
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is BuildingUpgradeCompletedChange bucc)
            {
                var building = gameState.GetBuilding(bucc.buildingID);
                if (building != null && building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var notif = new UpgradeCompletedNotification(
                        building.buildingType, bucc.newLevel, building.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is ArmyEntrenchedChange aec)
            {
                var army = gameState.GetArmy(aec.armyID);
                if (army != null && army.ownerID.HasValue && army.ownerID.Value == localPlayerID)
                {
                    var notif = new EntrenchmentCompletedNotification(
                        army.name, aec.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is VillagerCasualtiesChange vcc)
            {
                var group = gameState.GetVillagerGroup(vcc.villagerGroupID);
                if (group != null && group.ownerID.HasValue && group.ownerID.Value == localPlayerID)
                {
                    var notif = new VillagerAttackedNotification(group.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is VillagerTrainingCompletedChange vtcc)
            {
                var building = gameState.GetBuilding(vtcc.buildingID);
                if (building != null && building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    var notif = new TrainingCompletedNotification(
                        "Villager", vtcc.quantity, building.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is CommanderCreatedChange ccChange)
            {
                if (ccChange.ownerID == localPlayerID)
                {
                    if (commander.IsVisible) commander.Refresh(gameState);
                }
            }
            else if (change is CommanderXPGainedChange xpChange)
            {
                var cmdr = gameState.GetCommander(xpChange.commanderID);
                if (cmdr != null && cmdr.ownerID.HasValue && cmdr.ownerID.Value == localPlayerID)
                {
                    if (xpChange.didLevelUp || xpChange.didRankUp)
                    {
                        string cmdrName = cmdr.name ?? "Commander";
                        string msg = xpChange.didRankUp
                            ? $"{cmdrName} promoted to {xpChange.newRank}!"
                            : $"{cmdrName} reached level {xpChange.newLevel}!";
                        var notif = new ResearchCompletedNotification(msg);
                        notifications.ShowNotification(notif);
                        notificationInbox.AddNotification(notif);
                    }
                    if (commander.IsVisible) commander.Refresh(gameState);
                }
            }
            else if (change is ArmyStrandedChange asc)
            {
                var army = gameState.GetArmy(asc.armyID);
                if (army != null && army.ownerID.HasValue && army.ownerID.Value == localPlayerID)
                {
                    var notif = new ArmyStrandedNotification(army.name, asc.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is AttackCancelledChange accChange)
            {
                var army = gameState.GetArmy(accChange.armyID);
                if (army != null && army.ownerID.HasValue && army.ownerID.Value == localPlayerID)
                {
                    var notif = new AttackCancelledNotification(army.name, accChange.coordinate);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is ArmyRemobilizedChange arc)
            {
                var army = gameState.GetArmy(arc.armyID);
                if (army != null && army.ownerID.HasValue && army.ownerID.Value == localPlayerID)
                {
                    var notif = new ArmyRemobilizedNotification(army.name, arc.destination);
                    notifications.ShowNotification(notif);
                    notificationInbox.AddNotification(notif);
                }
            }
            else if (change is ResourcesChangedChange rchg)
            {
                if (rchg.playerID == localPlayerID)
                {
                    ResourceType resType;
                    if (System.Enum.TryParse<ResourceType>(rchg.resourceType, out resType))
                    {
                        int cap = gameState.GetStorageCapacity(localPlayerID, resType);
                        if (rchg.newAmount >= cap && rchg.oldAmount < cap)
                        {
                            var notif = new ResourcesMaxedNotification(resType);
                            notifications.ShowNotification(notif);
                            notificationInbox.AddNotification(notif);
                        }
                    }
                }
            }
        }

        // ================================================================
        // Update
        // ================================================================

        public void UpdateUI()
        {
            // Drain main-thread action queue (arena sim callbacks, etc.)
            while (mainThreadActions.TryDequeue(out var action))
                action?.Invoke();

            notifications.UpdateNotifications();
            InterpolateProgressBars();

            if (entityRenderer != null && gameState != null)
            {
                entityRenderer.InterpolateMovingEntities(gameState);
                entityRenderer.UpdateBuildingBars(gameState);
                pathRenderer?.UpdatePathStartPoints(entityRenderer);
                entrenchmentRenderer?.AnimateGrowth(gameState);
            }
        }

        // ================================================================
        // Per-Frame Progress Bar Interpolation
        // ================================================================

        private void InterpolateProgressBars()
        {
            if (!buildingDetail.IsVisible || gameState == null) return;

            var (constructionFill, upgradeFill) = buildingDetail.GetProgressFillRefs();

            if (constructionFill != null && buildingDetail.CurrentBuildingID.HasValue)
            {
                var building = gameState.GetBuilding(buildingDetail.CurrentBuildingID.Value);
                if (building != null && building.state == BuildingState.Constructing)
                {
                    float target = Mathf.Clamp01((float)building.constructionProgress);
                    var fillRT = constructionFill.GetComponent<RectTransform>();
                    float current = fillRT.anchorMax.x;
                    float smoothed = Mathf.Lerp(current, target, Time.deltaTime * 10f);
                    fillRT.anchorMax = new Vector2(smoothed, 1);
                }
            }

            if (upgradeFill != null && buildingDetail.CurrentBuildingID.HasValue)
            {
                var building = gameState.GetBuilding(buildingDetail.CurrentBuildingID.Value);
                if (building != null && building.state == BuildingState.Upgrading)
                {
                    float target = Mathf.Clamp01((float)building.upgradeProgress);
                    var fillRT = upgradeFill.GetComponent<RectTransform>();
                    float current = fillRT.anchorMax.x;
                    float smoothed = Mathf.Lerp(current, target, Time.deltaTime * 10f);
                    fillRT.anchorMax = new Vector2(smoothed, 1);
                }
            }
        }
    }
}

