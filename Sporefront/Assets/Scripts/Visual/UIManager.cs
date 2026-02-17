// ============================================================================
// FILE: Visual/UIManager.cs
// PURPOSE: Central UI coordinator — creates Canvas, manages all panels,
//          routes tile selections and state changes
// ============================================================================

using System;
using System.Collections.Generic;
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
        // Core HUD Panels
        // ================================================================

        private ResourceBarPanel resourceBar;
        private TileInfoPanel tileInfo;
        private EntityListPanel entityList;
        private BuildingDetailPanel buildingDetail;
        private ArmyDetailPanel armyDetail;
        private ActionPanel actionPanel;
        private NotificationPanel notifications;
        private SelectionRenderer selectionRenderer;
        private PathRenderer pathRenderer;

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

        // ================================================================
        // Settings & Notifications
        // ================================================================

        private SettingsPanel settings;
        private NotificationInboxPanel notificationInbox;

        // ================================================================
        // AI & Evolution
        // ================================================================

        private EvolutionPanel evolution;
        private GenomeSelectionPanel genomeSelection;
        private ArenaResultsPanel arenaResults;
        private SpectatorOverlay spectatorOverlay;

        // ================================================================
        // State
        // ================================================================

        private GameState gameState;
        private CameraController cameraController;
        private Canvas canvas;
        private Guid localPlayerID;
        private HexCoordinate? selectedCoord;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(GameState state, HexGridRenderer gridRenderer,
            CameraController camController)
        {
            gameState = state;
            cameraController = camController;
            localPlayerID = state.localPlayerID ?? Guid.Empty;

            EnsureEventSystem();
            CreateCanvas();
            CreatePanels();
            WireEvents();

            // Create renderers
            var selGO = new GameObject("SelectionRenderer");
            selGO.transform.SetParent(gridRenderer.transform, false);
            selectionRenderer = selGO.AddComponent<SelectionRenderer>();

            var pathGO = new GameObject("PathRenderer");
            pathGO.transform.SetParent(gridRenderer.transform, false);
            pathRenderer = pathGO.AddComponent<PathRenderer>();
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
            resourceBar.Initialize(ct);

            notifications = CreatePanelComponent<NotificationPanel>("Notifications");
            notifications.Initialize(ct);

            tileInfo = CreatePanelComponent<TileInfoPanel>("TileInfo");
            tileInfo.Initialize(ct);

            entityList = CreatePanelComponent<EntityListPanel>("EntityList");
            entityList.Initialize(ct);

            buildingDetail = CreatePanelComponent<BuildingDetailPanel>("BuildingDetail");
            buildingDetail.Initialize(ct, localPlayerID);

            armyDetail = CreatePanelComponent<ArmyDetailPanel>("ArmyDetail");
            armyDetail.Initialize(ct, localPlayerID);

            actionPanel = CreatePanelComponent<ActionPanel>("ActionPanel");
            actionPanel.Initialize(ct, localPlayerID);

            // ---- Navigation & Flow ----

            mainMenu = CreatePanelComponent<MainMenuPanel>("MainMenu");
            mainMenu.Initialize(ct);

            gameSetup = CreatePanelComponent<GameSetupPanel>("GameSetup");
            gameSetup.Initialize(ct);

            gameOver = CreatePanelComponent<GameOverPanel>("GameOver");
            gameOver.Initialize(ct);

            about = CreatePanelComponent<AboutPanel>("About");
            about.Initialize(ct);

            // ---- Overviews ----

            buildingsOverview = CreatePanelComponent<BuildingsOverviewPanel>("BuildingsOverview");
            buildingsOverview.Initialize(ct, localPlayerID);

            entitiesOverview = CreatePanelComponent<EntitiesOverviewPanel>("EntitiesOverview");
            entitiesOverview.Initialize(ct, localPlayerID);

            militaryOverview = CreatePanelComponent<MilitaryOverviewPanel>("MilitaryOverview");
            militaryOverview.Initialize(ct, localPlayerID);

            resourceOverview = CreatePanelComponent<ResourceOverviewPanel>("ResourceOverview");
            resourceOverview.Initialize(ct, localPlayerID);

            trainingOverview = CreatePanelComponent<TrainingOverviewPanel>("TrainingOverview");
            trainingOverview.Initialize(ct, localPlayerID);

            // ---- Commander & Research ----

            commander = CreatePanelComponent<CommanderPanel>("Commander");
            commander.Initialize(ct, localPlayerID);

            researchTree = CreatePanelComponent<ResearchTreePanel>("ResearchTree");
            researchTree.Initialize(ct, localPlayerID);

            // ---- Combat ----

            liveCombat = CreatePanelComponent<LiveCombatPanel>("LiveCombat");
            liveCombat.Initialize(ct, localPlayerID);

            combatHistory = CreatePanelComponent<CombatHistoryPanel>("CombatHistory");
            combatHistory.Initialize(ct, localPlayerID);

            combatDetail = CreatePanelComponent<CombatDetailPanel>("CombatDetail");
            combatDetail.Initialize(ct, localPlayerID);

            // ---- Action Panels ----

            gatherPanel = CreatePanelComponent<GatherPanel>("GatherPanel");
            gatherPanel.Initialize(ct, localPlayerID);

            reinforcePanel = CreatePanelComponent<ReinforcePanel>("ReinforcePanel");
            reinforcePanel.Initialize(ct, localPlayerID);

            villagerDeploy = CreatePanelComponent<VillagerDeployPanel>("VillagerDeploy");
            villagerDeploy.Initialize(ct, localPlayerID);

            // ---- Settings & Notifications ----

            settings = CreatePanelComponent<SettingsPanel>("Settings");
            settings.Initialize(ct, localPlayerID);

            notificationInbox = CreatePanelComponent<NotificationInboxPanel>("NotificationInbox");
            notificationInbox.Initialize(ct, localPlayerID);

            // ---- AI & Evolution ----

            evolution = CreatePanelComponent<EvolutionPanel>("Evolution");
            evolution.Initialize(ct);

            genomeSelection = CreatePanelComponent<GenomeSelectionPanel>("GenomeSelection");
            genomeSelection.Initialize(ct);

            arenaResults = CreatePanelComponent<ArenaResultsPanel>("ArenaResults");
            arenaResults.Initialize(ct);

            spectatorOverlay = CreatePanelComponent<SpectatorOverlay>("SpectatorOverlay");
            spectatorOverlay.Initialize(ct);
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
            // ---- TileInfoPanel ----
            tileInfo.OnBuildingDetailRequested += (id) => buildingDetail.Show(id, gameState);
            tileInfo.OnArmyDetailRequested += (id) => armyDetail.Show(id, gameState);
            tileInfo.OnBuildRequested += (coord) => actionPanel.ShowBuildMenu(coord, gameState);
            tileInfo.OnMoveRequested += (entityID, coord) =>
                actionPanel.ShowMoveTarget(entityID, false);
            tileInfo.OnAttackRequested += (armyID, coord) =>
                actionPanel.ShowAttackTarget(armyID);
            tileInfo.OnGatherRequested += (vgID, rpID) =>
            {
                var cmd = new GatherCommand(localPlayerID, vgID, rpID);
                GameEngine.Instance.ExecuteCommand(cmd);
            };

            // ---- EntityListPanel ----
            entityList.OnBuildingDetailRequested += (id) => buildingDetail.Show(id, gameState);
            entityList.OnArmyDetailRequested += (id) => armyDetail.Show(id, gameState);
            entityList.OnMoveArmyRequested += (id) => actionPanel.ShowMoveTarget(id, true);
            entityList.OnAttackRequested += (id) => actionPanel.ShowAttackTarget(id);
            entityList.OnMoveVillagerRequested += (id) => actionPanel.ShowMoveTarget(id, false);

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
            actionPanel.OnCancelled += () => selectionRenderer.ClearBuildPreview();

            // ---- NotificationPanel (toast) ----
            notifications.OnNotificationClicked += (coord) =>
            {
                cameraController.FocusOn(coord, -1f, true);
                OnTileSelected(coord);
            };

            // ---- MainMenuPanel ----
            mainMenu.OnNewGame += () => { mainMenu.Hide(); gameSetup.Show(); };
            mainMenu.OnSettings += () => { settings.Show(); };
            mainMenu.OnAbout += () => { about.Show(); };
            mainMenu.OnEvolveAI += () => { mainMenu.Hide(); evolution.Show(); };
            mainMenu.OnSpectateAI += () => { mainMenu.Hide(); genomeSelection.Show(); };

            // ---- GameSetupPanel ----
            gameSetup.OnBack += () => { gameSetup.Hide(); mainMenu.Show(); };

            // ---- GameOverPanel ----
            gameOver.OnReturnToMenu += () => { gameOver.Hide(); mainMenu.Show(); };

            // ---- AboutPanel ----
            about.OnClose += () => about.Hide();

            // ---- Overview panels (close) ----
            buildingsOverview.OnClose += () => buildingsOverview.Hide();
            buildingsOverview.OnBuildingSelected += (id) =>
            {
                buildingsOverview.Hide();
                buildingDetail.Show(id, gameState);
            };

            entitiesOverview.OnClose += () => entitiesOverview.Hide();
            entitiesOverview.OnEntitySelected += (id, isArmy) =>
            {
                entitiesOverview.Hide();
                if (isArmy)
                    armyDetail.Show(id, gameState);
                else
                    buildingDetail.Show(id, gameState);
            };

            militaryOverview.OnClose += () => militaryOverview.Hide();

            resourceOverview.OnClose += () => resourceOverview.Hide();

            trainingOverview.OnClose += () => trainingOverview.Hide();
            trainingOverview.OnBuildingSelected += (id) =>
            {
                trainingOverview.Hide();
                buildingDetail.Show(id, gameState);
            };

            // ---- Commander ----
            commander.OnClose += () => commander.Hide();
            commander.OnRecruitCommander += (specialty) =>
            {
                // Commander recruitment handled via command
            };

            // ---- Research ----
            researchTree.OnClose += () => researchTree.Hide();
            researchTree.OnStartResearch += (type) =>
            {
                // Find a suitable research building (Library, University)
                var researchBuilding = gameState.FindResearchBuilding(localPlayerID);
                if (researchBuilding != null)
                {
                    var cmd = new ResearchCommand(localPlayerID, type.ToString(), researchBuilding.id);
                    GameEngine.Instance.ExecuteCommand(cmd);
                }
            };

            // ---- Combat panels ----
            liveCombat.OnClose += () => liveCombat.Hide();
            liveCombat.OnFocusCombat += (coord) =>
            {
                cameraController.FocusOn(coord, -1f, true);
            };

            combatHistory.OnClose += () => combatHistory.Hide();
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

            combatDetail.OnClose += () => combatDetail.Hide();

            // ---- Action panels ----
            gatherPanel.OnClose += () => gatherPanel.Hide();
            gatherPanel.OnGatherConfirmed += (vgID, rpID) =>
            {
                var cmd = new GatherCommand(localPlayerID, vgID, rpID);
                GameEngine.Instance.ExecuteCommand(cmd);
                gatherPanel.Hide();
            };
            gatherPanel.OnHuntConfirmed += (vgID, rpID) =>
            {
                // Hunt uses same gather command - resource engine handles the difference
                var cmd = new GatherCommand(localPlayerID, vgID, rpID);
                GameEngine.Instance.ExecuteCommand(cmd);
                gatherPanel.Hide();
            };

            reinforcePanel.OnClose += () => reinforcePanel.Hide();
            reinforcePanel.OnReinforceConfirmed += (srcArmyID, targetArmyID, units) =>
            {
                var cmd = new ReinforceArmyCommand(localPlayerID, srcArmyID, targetArmyID, units);
                GameEngine.Instance.ExecuteCommand(cmd);
                reinforcePanel.Hide();
            };

            villagerDeploy.OnClose += () => villagerDeploy.Hide();
            villagerDeploy.OnDeployNew += (buildingID, count) =>
            {
                var cmd = new DeployVillagersCommand(localPlayerID, buildingID, count);
                GameEngine.Instance.ExecuteCommand(cmd);
                villagerDeploy.Hide();
            };
            villagerDeploy.OnJoinExisting += (buildingID, targetGroupID) =>
            {
                var building = gameState.GetBuilding(buildingID);
                int count = building != null ? building.villagerGarrison : 1;
                if (count > 0)
                {
                    var cmd = new JoinVillagerGroupCommand(localPlayerID, buildingID, targetGroupID, count);
                    GameEngine.Instance.ExecuteCommand(cmd);
                }
                villagerDeploy.Hide();
            };

            // ---- Settings ----
            settings.OnClose += () => settings.Hide();

            // ---- Notification Inbox ----
            notificationInbox.OnClose += () => notificationInbox.Hide();
            notificationInbox.OnNotificationTapped += (coord) =>
            {
                if (coord.HasValue)
                {
                    notificationInbox.Hide();
                    cameraController.FocusOn(coord.Value, -1f, true);
                    OnTileSelected(coord.Value);
                }
            };

            // ---- Evolution ----
            evolution.OnClose += () => { evolution.Hide(); mainMenu.Show(); };

            // ---- Genome Selection ----
            genomeSelection.OnClose += () => { genomeSelection.Hide(); mainMenu.Show(); };

            // ---- Arena Results ----
            arenaResults.OnBack += () => { arenaResults.Hide(); gameSetup.Show(); };

            // ---- Spectator ----
            spectatorOverlay.OnExit += () => { spectatorOverlay.Hide(); mainMenu.Show(); };
        }

        // ================================================================
        // Tile Selection Routing
        // ================================================================

        public void OnTileSelected(HexCoordinate coord)
        {
            selectedCoord = coord;
            selectionRenderer.ShowSelection(coord);
            tileInfo.Show(coord, gameState, localPlayerID);
            entityList.Show(coord, gameState, localPlayerID);
        }

        public void OnTileDeselected()
        {
            selectedCoord = null;
            selectionRenderer.HideSelection();
            tileInfo.Hide();
            entityList.Hide();
        }

        /// <summary>
        /// Returns true if the click was consumed by an active action mode (move/attack target).
        /// </summary>
        public bool HandleActionModeClick(HexCoordinate coord)
        {
            return actionPanel.HandleTargetClick(coord);
        }

        public bool IsActionModeActive => actionPanel.IsActive;

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

        public void ShowMainMenu() => mainMenu.Show();
        public void HideMainMenu() => mainMenu.Hide();
        public void ShowGameSetup() => gameSetup.Show();
        public void ShowGameOver(bool isVictory, string reason, GameOverStats stats) =>
            gameOver.Show(isVictory, reason, stats);
        public void ShowSettings() => settings.Show();
        public void ShowAbout() => about.Show();

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

        public void ShowNotificationInbox() => notificationInbox.Show();

        public void ShowEvolution() => evolution.Show();
        public void ShowGenomeSelection() => genomeSelection.Show();
        public void ShowSpectatorOverlay() => spectatorOverlay.Show();
        public void HideSpectatorOverlay() => spectatorOverlay.Hide();

        // ================================================================
        // State Change Handling
        // ================================================================

        public void HandleStateChanges(StateChangeBatch batch)
        {
            // Refresh resource bar every batch
            resourceBar.Refresh(gameState, localPlayerID);

            // Refresh visible core panels
            if (tileInfo.IsVisible) tileInfo.Refresh(gameState);
            if (entityList.IsVisible) entityList.Refresh(gameState);
            if (buildingDetail.IsVisible) buildingDetail.Refresh(gameState);
            if (armyDetail.IsVisible) armyDetail.Refresh(gameState);

            // Refresh visible overview panels
            if (buildingsOverview.IsVisible) buildingsOverview.Refresh(gameState);
            if (entitiesOverview.IsVisible) entitiesOverview.Refresh(gameState);
            if (militaryOverview.IsVisible) militaryOverview.Refresh(gameState);
            if (resourceOverview.IsVisible) resourceOverview.Refresh(gameState);
            if (trainingOverview.IsVisible) trainingOverview.Refresh(gameState);

            // Refresh visible detail panels
            if (commander.IsVisible) commander.Refresh(gameState);
            if (researchTree.IsVisible) researchTree.Refresh(gameState);
            if (liveCombat.IsVisible) liveCombat.Refresh(gameState);
            if (combatHistory.IsVisible) combatHistory.Refresh(gameState);

            // Refresh visible action panels
            if (gatherPanel.IsVisible) gatherPanel.Refresh(gameState);
            if (reinforcePanel.IsVisible) reinforcePanel.Refresh(gameState);
            if (villagerDeploy.IsVisible) villagerDeploy.Refresh(gameState);

            // Update path renderer
            pathRenderer.UpdatePaths(gameState, localPlayerID);

            // Route notification-worthy changes
            foreach (var change in batch.changes)
            {
                RouteNotification(change);
            }
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
                    notifications.ShowNotification(
                        new BuildingCompletedNotification(
                            building.buildingType, building.coordinate));
                }
            }
            else if (change is TrainingCompletedChange tcc)
            {
                var building = gameState.GetBuilding(tcc.buildingID);
                if (building != null && building.ownerID.HasValue && building.ownerID.Value == localPlayerID)
                {
                    notifications.ShowNotification(
                        new TrainingCompletedNotification(
                            tcc.unitType, tcc.quantity, building.coordinate));
                }
            }
            else if (change is ResourcePointDepletedChange rpdc)
            {
                var rp = gameState.GetResourcePoint(rpdc.coordinate);
                notifications.ShowNotification(
                    new ResourcePointDepletedNotification(
                        rpdc.resourceType, rpdc.coordinate));
            }
            else if (change is CombatStartedChange csc)
            {
                // Notify if local player is a defender
                var defender = gameState.GetArmy(csc.defenderID);
                if (defender != null && defender.ownerID.HasValue && defender.ownerID.Value == localPlayerID)
                {
                    notifications.ShowNotification(
                        new ArmyAttackedNotification(
                            defender.name, csc.coordinate));
                }
            }
            else if (change is ResearchCompletedChange rcc)
            {
                if (rcc.playerID == localPlayerID)
                {
                    notifications.ShowNotification(
                        new ResearchCompletedNotification(rcc.researchType));
                }
            }
            else if (change is UnitUpgradeCompletedChange uucc)
            {
                if (uucc.playerID == localPlayerID)
                {
                    notifications.ShowNotification(
                        new ResearchCompletedNotification($"{uucc.unitType} Tier {uucc.tier} Upgrade"));
                }
            }
        }

        // ================================================================
        // Update
        // ================================================================

        public void UpdateUI()
        {
            notifications.UpdateNotifications();
        }
    }
}
