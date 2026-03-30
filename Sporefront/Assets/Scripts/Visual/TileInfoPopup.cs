// ============================================================================
// FILE: Visual/TileInfoPopup.cs
// PURPOSE: Compact floating info popup that appears near the selected tile.
//          Shows terrain, building, army, villager, and resource summaries
//          with a quick-action sidebar for common operations.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class TileInfoPopup : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<HexCoordinate> OnInfoRequested;
        public event Action<HexCoordinate> OnBuildRequested;
        public event Action<Guid> OnMoveRequested;       // entityID (army preferred, fallback villager)
        public event Action<Guid> OnEntrenchRequested;    // armyID
        public event Action<Guid> OnAttackRequested;      // armyID
        public event Action<Guid> OnGatherSelectionRequested;  // resourcePointID — opens GatherPanel
        public event Action<Guid> OnRetreatRequested;       // armyID
        public event Action<Guid> OnTrainVillagerRequested;  // buildingID
        public event Action<Guid, int> OnDeployVillagersRequested;  // buildingID, count
        public event Action<Guid> OnUpgradeBuildingRequested;        // buildingID

        // ================================================================
        // State
        // ================================================================

        private GameObject root;
        private RectTransform rootRT;
        private Canvas canvas;
        private RectTransform canvasRT;

        private HexCoordinate? currentCoord;
        private Guid localPlayerID;
        private GameState cachedState;

        // Content labels (toggled via SetActive)
        private GameObject terrainRow;
        private Text terrainLabel;
        private GameObject buildingRow;
        private Text buildingLabel;
        private GameObject armyRow;
        private Text armyLabel;
        private GameObject villagerRow;
        private Text villagerLabel;
        private GameObject scoutRow;
        private Text scoutLabel;
        private GameObject resourceRow;
        private Text resourceLabel;

        // Quick action sidebar buttons
        private GameObject sidebar;
        private GameObject infoBtn;
        private GameObject buildBtn;
        private GameObject moveBtn;
        private GameObject digInBtn;
        private GameObject attackBtn;
        private GameObject gatherBtn;
        private GameObject trainBtn;
        private GameObject retreatBtn;

        // Inline content action buttons
        private GameObject deployBtn;
        private Text deployBtnText;
        private GameObject upgradeBtn;
        private Text upgradeBtnText;
        private Button upgradeBtnComponent;

        // Positioning constants
        private const float PopupWidth = 280f;
        private const float SidebarWidth = 90f;
        private const float OffsetX = 60f;
        private const float OffsetY = 40f;
        private const float SafeTop = 75f;    // resource bar
        private const float SafeBottom = 100f; // tendril wheel buttons extend ~195px at corners

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Canvas parentCanvas)
        {
            canvas = parentCanvas;
            canvasRT = canvas.GetComponent<RectTransform>();

            // Root panel
            root = new GameObject("TileInfoPopup");
            root.transform.SetParent(canvasTransform, false);
            rootRT = root.AddComponent<RectTransform>();
            rootRT.pivot = new Vector2(0f, 1f); // top-left pivot for positioning
            rootRT.sizeDelta = new Vector2(PopupWidth + SidebarWidth, 0f); // auto-height

            // Content area (left side) — parchment background
            var contentGO = UIHelper.CreatePanel(root.transform, "PopupContent",
                new Color(SporefrontColors.ParchmentMid.r, SporefrontColors.ParchmentMid.g,
                    SporefrontColors.ParchmentMid.b, 0.97f));
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 0);
            contentRT.anchorMax = new Vector2(0, 1);
            contentRT.pivot = new Vector2(0, 1);
            contentRT.offsetMin = new Vector2(0, 0);
            contentRT.offsetMax = new Vector2(PopupWidth, 0);
            PopupTendrilDecorator.Attach(contentRT);

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 4f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            // Create cached label rows
            terrainRow = CreateLabelRow(contentGO.transform, out terrainLabel,
                UIConstants.FontSubheader, true);
            buildingRow = CreateLabelRow(contentGO.transform, out buildingLabel,
                UIConstants.FontBody, false);

            // Inline action buttons below building info
            deployBtn = CreateContentButton(contentGO.transform, "Deploy Villagers",
                SporefrontColors.SporeGreen, out deployBtnText, () =>
                {
                    if (cachedState == null || !currentCoord.HasValue) return;
                    var b = cachedState.GetBuilding(currentCoord.Value);
                    if (b != null && b.ownerID.HasValue && b.ownerID.Value == localPlayerID
                        && b.IsOperational && b.villagerGarrison > 0)
                        OnDeployVillagersRequested?.Invoke(b.id, b.villagerGarrison);
                });

            upgradeBtn = CreateContentButton(contentGO.transform, "Upgrade",
                SporefrontColors.SporeAmber, out upgradeBtnText, () =>
                {
                    if (cachedState == null || !currentCoord.HasValue) return;
                    var b = cachedState.GetBuilding(currentCoord.Value);
                    if (b != null && b.ownerID.HasValue && b.ownerID.Value == localPlayerID && b.CanUpgrade)
                        OnUpgradeBuildingRequested?.Invoke(b.id);
                });
            upgradeBtnComponent = upgradeBtn.GetComponentInChildren<Button>();

            armyRow = CreateLabelRow(contentGO.transform, out armyLabel,
                UIConstants.FontBody, false);
            villagerRow = CreateLabelRow(contentGO.transform, out villagerLabel,
                UIConstants.FontBody, false);
            scoutRow = CreateLabelRow(contentGO.transform, out scoutLabel,
                UIConstants.FontBody, false);
            resourceRow = CreateLabelRow(contentGO.transform, out resourceLabel,
                UIConstants.FontBody, false);

            // LayoutElement on content so HLG knows its width
            var contentLE = contentGO.AddComponent<LayoutElement>();
            contentLE.preferredWidth = PopupWidth;

            // Auto-size root to match content height; width is fixed (PopupWidth + SidebarWidth)
            var rootCSF = root.AddComponent<ContentSizeFitter>();
            rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rootCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var rootHLG = root.AddComponent<HorizontalLayoutGroup>();
            rootHLG.childForceExpandWidth = false;
            rootHLG.childForceExpandHeight = true;
            rootHLG.childControlWidth = false;
            rootHLG.childControlHeight = true;

            // Quick action sidebar (right side) — darker parchment
            sidebar = UIHelper.CreatePanel(root.transform, "Sidebar",
                new Color(SporefrontColors.ParchmentDeep.r, SporefrontColors.ParchmentDeep.g,
                    SporefrontColors.ParchmentDeep.b, 0.95f));
            var sidebarRT = sidebar.GetComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(1, 0);
            sidebarRT.anchorMax = new Vector2(1, 1);
            sidebarRT.pivot = new Vector2(1, 1);
            sidebarRT.offsetMin = new Vector2(-SidebarWidth, 0);
            sidebarRT.offsetMax = new Vector2(0, 0);

            var sidebarLE = sidebar.AddComponent<LayoutElement>();
            sidebarLE.preferredWidth = SidebarWidth;

            var sideVLG = sidebar.AddComponent<VerticalLayoutGroup>();
            sideVLG.padding = new RectOffset(4, 4, 6, 6);
            sideVLG.spacing = 4f;
            sideVLG.childForceExpandWidth = true;
            sideVLG.childForceExpandHeight = false;
            sideVLG.childAlignment = TextAnchor.UpperCenter;

            // Create sidebar buttons
            infoBtn = CreateSidebarButton(sidebar.transform, "Info",
                SporefrontColors.InkLight, () =>
                {
                    if (currentCoord.HasValue)
                        OnInfoRequested?.Invoke(currentCoord.Value);
                });

            buildBtn = CreateSidebarButton(sidebar.transform, "Build",
                SporefrontColors.SporeGreen, () =>
                {
                    if (currentCoord.HasValue)
                        OnBuildRequested?.Invoke(currentCoord.Value);
                });

            moveBtn = CreateSidebarButton(sidebar.transform, "Move",
                SporefrontColors.SporeTeal, () =>
                {
                    var entityID = FindOwnedMoveableEntity();
                    if (entityID.HasValue)
                        OnMoveRequested?.Invoke(entityID.Value);
                });

            digInBtn = CreateSidebarButton(sidebar.transform, "Dig In",
                SporefrontColors.SporeAmber, () =>
                {
                    var armyID = FindOwnedEntrenchableArmy();
                    if (armyID.HasValue)
                        OnEntrenchRequested?.Invoke(armyID.Value);
                });

            attackBtn = CreateSidebarButton(sidebar.transform, "Attack",
                SporefrontColors.SporeRed, () =>
                {
                    var armyID = FindOwnedAttackableArmy();
                    if (armyID.HasValue)
                        OnAttackRequested?.Invoke(armyID.Value);
                });

            gatherBtn = CreateSidebarButton(sidebar.transform, "Gather",
                SporefrontColors.SporeGreen, () =>
                {
                    if (cachedState == null || !currentCoord.HasValue) return;
                    var rp = cachedState.GetResourcePoint(currentCoord.Value);
                    if (rp != null && !rp.IsDepleted())
                        OnGatherSelectionRequested?.Invoke(rp.id);
                });

            trainBtn = CreateSidebarButton(sidebar.transform, "Train",
                SporefrontColors.SporeGreen, () =>
                {
                    if (cachedState == null || !currentCoord.HasValue) return;
                    var b = cachedState.GetBuilding(currentCoord.Value);
                    if (b != null && b.ownerID.HasValue && b.ownerID.Value == localPlayerID
                        && b.IsOperational && b.CanTrainVillagers())
                        OnTrainVillagerRequested?.Invoke(b.id);
                });

            retreatBtn = CreateSidebarButton(sidebar.transform, "Retreat",
                SporefrontColors.SporeRed, () =>
                {
                    var armyID = FindOwnedRetreatingCandidate();
                    if (armyID.HasValue)
                        OnRetreatRequested?.Invoke(armyID.Value);
                });

            root.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(HexCoordinate coord, GameState gameState, Guid playerID)
        {
            currentCoord = coord;
            localPlayerID = playerID;
            cachedState = gameState;
            UpdateContent(gameState);
            root.SetActive(true);
        }

        public void Hide()
        {
            currentCoord = null;
            root.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !root.activeSelf) return;
            cachedState = gameState;
            UpdateContent(gameState);
        }

        public bool IsVisible => root != null && root.activeSelf;

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Content Update
        // ================================================================

        private void UpdateContent(GameState gameState)
        {
            if (!currentCoord.HasValue) return;
            var coord = currentCoord.Value;

            var tileNullable = gameState.mapData.GetTile(coord);
            if (!tileNullable.HasValue)
            {
                root.SetActive(false);
                return;
            }
            var tile = tileNullable.Value;

            // Terrain
            terrainLabel.text = $"{tile.terrain} ({coord.q},{coord.r})";
            terrainRow.SetActive(true);

            // Building
            var building = gameState.GetBuilding(coord);
            if (building != null)
            {
                string status = building.state == BuildingState.Completed
                    ? "Completed"
                    : building.state.ToString();
                buildingLabel.text = $"{building.buildingType.DisplayName()} Lv.{building.level} — {status}";
                buildingRow.SetActive(true);

                bool isOwned = building.ownerID.HasValue && building.ownerID.Value == localPlayerID;

                // Deploy button
                if (isOwned && building.IsOperational && building.villagerGarrison > 0)
                {
                    deployBtnText.text = $"Deploy Villagers ({building.villagerGarrison})";
                    deployBtn.SetActive(true);
                }
                else
                {
                    deployBtn.SetActive(false);
                }

                // Upgrade button
                if (isOwned && building.CanUpgrade)
                {
                    var cost = building.GetUpgradeCost();
                    var player = gameState.GetPlayer(localPlayerID);
                    bool canAfford = player != null && player.CanAfford(cost);
                    int nextLevel = building.level + 1;
                    upgradeBtnText.text = $"Upgrade to Lv.{nextLevel} ({UIHelper.FormatCost(cost)})";
                    if (upgradeBtnComponent != null) upgradeBtnComponent.interactable = canAfford;
                    upgradeBtn.SetActive(true);
                }
                else
                {
                    upgradeBtn.SetActive(false);
                }
            }
            else
            {
                buildingRow.SetActive(false);
                deployBtn.SetActive(false);
                upgradeBtn.SetActive(false);
            }

            // Armies
            var armies = gameState.GetArmies(coord);
            if (armies != null && armies.Count > 0)
            {
                int totalUnits = 0;
                foreach (var army in armies)
                    totalUnits += army.GetTotalUnits();

                if (armies.Count == 1)
                    armyLabel.text = $"Army: {totalUnits} units";
                else
                    armyLabel.text = $"{armies.Count} Armies ({totalUnits} units)";
                armyRow.SetActive(true);
            }
            else
            {
                armyRow.SetActive(false);
            }

            // Villagers
            var villagers = gameState.GetVillagerGroups(coord);
            if (villagers != null && villagers.Count > 0)
            {
                int totalVillagers = 0;
                string task = null;
                foreach (var vg in villagers)
                {
                    totalVillagers += vg.villagerCount;
                    if (task == null && vg.currentTask != null && !vg.currentTask.IsIdle)
                        task = vg.currentTask.DisplayName;
                }
                string taskStr = task ?? "Idle";
                villagerLabel.text = $"{totalVillagers}x Villagers — {taskStr}";
                villagerRow.SetActive(true);
            }
            else
            {
                villagerRow.SetActive(false);
            }

            // Scouts
            var scoutID = gameState.mapData.GetScoutID(coord);
            if (scoutID.HasValue)
            {
                var scout = gameState.GetScout(scoutID.Value);
                if (scout != null)
                {
                    scoutLabel.text = $"Scout — Stamina: {(int)scout.stamina}/{(int)scout.maxStamina}";
                    scoutRow.SetActive(true);
                }
                else
                {
                    scoutRow.SetActive(false);
                }
            }
            else
            {
                scoutRow.SetActive(false);
            }

            // Resource
            var rp = gameState.GetResourcePoint(coord);
            if (rp != null && !rp.IsDepleted())
            {
                resourceLabel.text = $"{rp.resourceType} ({rp.remainingAmount})";
                resourceRow.SetActive(true);
            }
            else
            {
                resourceRow.SetActive(false);
            }

            // Update sidebar button visibility
            UpdateSidebarButtons(gameState, coord, building, armies, villagers);
        }

        // ================================================================
        // Sidebar Button Visibility
        // ================================================================

        private void UpdateSidebarButtons(GameState gameState, HexCoordinate coord,
            BuildingData building, List<ArmyData> armies, List<VillagerGroupData> villagers)
        {
            // Info — always visible
            infoBtn.SetActive(true);

            // Build — empty buildable tile
            buildBtn.SetActive(building == null && gameState.CanBuildAt(coord, localPlayerID));

            // Move — own army or villager present
            bool hasOwnMoveable = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID && !army.isInCombat)
                    {
                        hasOwnMoveable = true;
                        break;
                    }
                }
            }
            if (!hasOwnMoveable && villagers != null)
            {
                foreach (var vg in villagers)
                {
                    if (vg.ownerID.HasValue && vg.ownerID.Value == localPlayerID)
                    {
                        hasOwnMoveable = true;
                        break;
                    }
                }
            }
            moveBtn.SetActive(hasOwnMoveable);

            // Dig In — own army, not entrenched/entrenching/in-combat
            bool hasEntrenchable = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isEntrenched && !army.isEntrenching && !army.isInCombat)
                    {
                        hasEntrenchable = true;
                        break;
                    }
                }
            }
            digInBtn.SetActive(hasEntrenchable);

            // Attack — own army, not in combat/retreating
            bool hasAttackable = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isInCombat && !army.isRetreating)
                    {
                        hasAttackable = true;
                        break;
                    }
                }
            }
            attackBtn.SetActive(hasAttackable);

            // Gather — tile has non-depleted resource and player has any villager groups
            bool canGather = false;
            var rp = gameState.GetResourcePoint(coord);
            if (rp != null && !rp.IsDepleted())
            {
                var playerVillagers = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                canGather = playerVillagers != null && playerVillagers.Count > 0;
            }
            gatherBtn.SetActive(canGather);

            // Train — owned operational building that can train villagers
            bool canTrain = building != null && building.ownerID.HasValue
                && building.ownerID.Value == localPlayerID
                && building.IsOperational && building.CanTrainVillagers();
            trainBtn.SetActive(canTrain);

            // Retreat — owned army that is entrenched/entrenching/in-combat, not already retreating
            bool hasRetreatable = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isRetreating
                        && (army.isEntrenched || army.isEntrenching || army.isInCombat))
                    {
                        hasRetreatable = true;
                        break;
                    }
                }
            }
            retreatBtn.SetActive(hasRetreatable);
        }

        // ================================================================
        // Tile-Following Positioning (LateUpdate)
        // ================================================================

        private void LateUpdate()
        {
            if (!IsVisible || !currentCoord.HasValue || canvas == null) return;

            Vector3 worldPos = HexMetrics.HexToWorldPosition(currentCoord.Value);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // For ScreenSpaceOverlay canvas, pass null camera
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, null, out localPoint);

            // Default: position to upper-right of tile
            float popupTotalWidth = PopupWidth + SidebarWidth;
            float xPos = localPoint.x + OffsetX;
            float yPos = localPoint.y + OffsetY;

            // Smart flip: if popup would go off right edge, flip to left side
            float canvasWidth = canvasRT.rect.width;
            float canvasHeight = canvasRT.rect.height;

            // Convert to canvas-space bounds (canvas pivot may vary, use rect)
            float rightEdge = xPos + popupTotalWidth - canvasRT.rect.xMin;
            if (xPos + popupTotalWidth > canvasRT.rect.xMax)
            {
                xPos = localPoint.x - OffsetX - popupTotalWidth;
                rootRT.pivot = new Vector2(0f, 1f);
            }
            else
            {
                rootRT.pivot = new Vector2(0f, 1f);
            }

            // Clamp to safe area
            float popupHeight = rootRT.rect.height;
            if (popupHeight <= 0) popupHeight = 200f; // estimate before layout

            // Scale factor for safe area conversion
            float scaleFactor = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            float safeTopLocal = SafeTop / scaleFactor;
            float safeBottomLocal = SafeBottom / scaleFactor;

            // Clamp Y: popup top shouldn't go above (canvasMax.y - safeTop)
            float maxY = canvasRT.rect.yMax - safeTopLocal;
            float minY = canvasRT.rect.yMin + safeBottomLocal + popupHeight;
            yPos = Mathf.Clamp(yPos, minY, maxY);

            // Clamp X
            float minX = canvasRT.rect.xMin;
            float maxX = canvasRT.rect.xMax - popupTotalWidth;
            xPos = Mathf.Clamp(xPos, minX, maxX);

            rootRT.anchoredPosition = new Vector2(xPos, yPos);
        }

        // ================================================================
        // Entity Finders (for sidebar actions)
        // ================================================================

        private Guid? FindOwnedMoveableEntity()
        {
            if (cachedState == null || !currentCoord.HasValue) return null;
            var coord = currentCoord.Value;

            // Prefer army
            var armies = cachedState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID && !army.isInCombat)
                        return army.id;
                }
            }

            // Fallback to villager
            var villagers = cachedState.GetVillagerGroups(coord);
            if (villagers != null)
            {
                foreach (var vg in villagers)
                {
                    if (vg.ownerID.HasValue && vg.ownerID.Value == localPlayerID)
                        return vg.id;
                }
            }

            return null;
        }

        private Guid? FindOwnedEntrenchableArmy()
        {
            if (cachedState == null || !currentCoord.HasValue) return null;
            var coord = currentCoord.Value;

            var armies = cachedState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isEntrenched && !army.isEntrenching && !army.isInCombat)
                        return army.id;
                }
            }
            return null;
        }

        private Guid? FindOwnedAttackableArmy()
        {
            if (cachedState == null || !currentCoord.HasValue) return null;
            var coord = currentCoord.Value;

            var armies = cachedState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isInCombat && !army.isRetreating)
                        return army.id;
                }
            }
            return null;
        }

        private Guid? FindOwnedRetreatingCandidate()
        {
            if (cachedState == null || !currentCoord.HasValue) return null;
            var coord = currentCoord.Value;

            var armies = cachedState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value == localPlayerID
                        && !army.isRetreating
                        && (army.isEntrenched || army.isEntrenching || army.isInCombat))
                        return army.id;
                }
            }
            return null;
        }

        // ================================================================
        // UI Construction Helpers
        // ================================================================

        private GameObject CreateLabelRow(Transform parent, out Text label, int fontSize, bool bold)
        {
            var row = new GameObject("LabelRow");
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = bold ? 26f : 22f;

            label = UIHelper.CreateLabel(row.transform, "",
                fontSize, bold ? UIHelper.InkHeaderText : UIHelper.InkBodyText,
                TextAnchor.MiddleLeft, bold);
            var labelRT = label.GetComponent<RectTransform>();
            UIHelper.StretchFull(labelRT);

            return row;
        }

        private GameObject CreateContentButton(Transform parent, string text,
            Color bgColor, out Text label, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                bgColor, UIHelper.ButtonText, UIConstants.FontCaption, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
            label = btn.GetComponentInChildren<Text>();
            UIHelper.EnableAutoFit(label, 10, UIConstants.FontCaption);
            var go = btn.gameObject;
            go.SetActive(false);
            return go;
        }

        private GameObject CreateSidebarButton(Transform parent, string text,
            Color bgColor, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                bgColor, UIHelper.HudTextColor, UIConstants.FontSmall, onClick);
            var label = UIHelper.GetButtonLabel(btn);
            UIHelper.EnableAutoFit(label, 9, UIConstants.FontSmall);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 82f;
            le.preferredHeight = 40f;
            return btn.gameObject;
        }
    }
}
