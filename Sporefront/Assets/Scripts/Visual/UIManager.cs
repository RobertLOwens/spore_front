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
        // Panels
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

            // Resource bar (top HUD)
            var rbGO = new GameObject("ResourceBarManager");
            rbGO.transform.SetParent(transform, false);
            resourceBar = rbGO.AddComponent<ResourceBarPanel>();
            resourceBar.Initialize(ct);

            // Notifications (top-right)
            var notifGO = new GameObject("NotificationManager");
            notifGO.transform.SetParent(transform, false);
            notifications = notifGO.AddComponent<NotificationPanel>();
            notifications.Initialize(ct);

            // Tile info (right panel)
            var tiGO = new GameObject("TileInfoManager");
            tiGO.transform.SetParent(transform, false);
            tileInfo = tiGO.AddComponent<TileInfoPanel>();
            tileInfo.Initialize(ct);

            // Entity list (left panel)
            var elGO = new GameObject("EntityListManager");
            elGO.transform.SetParent(transform, false);
            entityList = elGO.AddComponent<EntityListPanel>();
            entityList.Initialize(ct);

            // Building detail (modal)
            var bdGO = new GameObject("BuildingDetailManager");
            bdGO.transform.SetParent(transform, false);
            buildingDetail = bdGO.AddComponent<BuildingDetailPanel>();
            buildingDetail.Initialize(ct, localPlayerID);

            // Army detail (modal)
            var adGO = new GameObject("ArmyDetailManager");
            adGO.transform.SetParent(transform, false);
            armyDetail = adGO.AddComponent<ArmyDetailPanel>();
            armyDetail.Initialize(ct, localPlayerID);

            // Action panel (build menu / target modes)
            var apGO = new GameObject("ActionPanelManager");
            apGO.transform.SetParent(transform, false);
            actionPanel = apGO.AddComponent<ActionPanel>();
            actionPanel.Initialize(ct, localPlayerID);
        }

        // ================================================================
        // Event Wiring
        // ================================================================

        private void WireEvents()
        {
            // TileInfoPanel events
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

            // EntityListPanel events
            entityList.OnBuildingDetailRequested += (id) => buildingDetail.Show(id, gameState);
            entityList.OnArmyDetailRequested += (id) => armyDetail.Show(id, gameState);
            entityList.OnMoveArmyRequested += (id) => actionPanel.ShowMoveTarget(id, true);
            entityList.OnAttackRequested += (id) => actionPanel.ShowAttackTarget(id);
            entityList.OnMoveVillagerRequested += (id) => actionPanel.ShowMoveTarget(id, false);

            // ArmyDetailPanel events
            armyDetail.OnMoveRequested += (id) => actionPanel.ShowMoveTarget(id, true);
            armyDetail.OnAttackRequested += (id) => actionPanel.ShowAttackTarget(id);

            // ActionPanel events
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

            // Notification click → camera focus
            notifications.OnNotificationClicked += (coord) =>
            {
                cameraController.FocusOn(coord, -1f, true);
                OnTileSelected(coord);
            };
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
        // State Change Handling
        // ================================================================

        public void HandleStateChanges(StateChangeBatch batch)
        {
            // Refresh resource bar every batch
            resourceBar.Refresh(gameState, localPlayerID);

            // Refresh visible detail panels
            if (tileInfo.IsVisible) tileInfo.Refresh(gameState);
            if (entityList.IsVisible) entityList.Refresh(gameState);
            if (buildingDetail.IsVisible) buildingDetail.Refresh(gameState);
            if (armyDetail.IsVisible) armyDetail.Refresh(gameState);

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
