// ============================================================================
// FILE: Visual/TileInfoPanel.cs
// PURPOSE: Right-side tile details panel with contextual info
//          Shows terrain, buildings, armies, villagers, resources, actions
//          Merged: absorbs EntityListPanel's inline action buttons
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class TileInfoPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnBuildingDetailRequested;
        public event Action<Guid> OnArmyDetailRequested;
        public event Action<HexCoordinate> OnBuildRequested;
        public event Action<Guid, HexCoordinate> OnMoveRequested;
        public event Action<Guid> OnArmyMoveRequested;
        public event Action<Guid, HexCoordinate> OnAttackRequested;
        public event Action<Guid> OnGatherSelectionRequested; // resourcePointID — opens GatherPanel with villager selection
        public event Action<Guid> OnHuntRequested; // resourcePointID — opens GatherPanel in hunt mode
        public event Action<Guid, HexCoordinate, bool> OnMoveEntityToTile; // entityID, destination, isArmy
        public event Action<Guid, HexCoordinate> OnAttackEntityToTile; // armyID, target coordinate
        public event Action OnCloseRequested;
        public event Action<Guid> OnTrainVillagerRequested;
        public event Action<Guid, int> OnDeployVillagersRequested;  // buildingID, count
        public event Action<Guid> OnUpgradeBuildingRequested;        // buildingID
        public event Action<Guid, HexCoordinate, bool, bool> OnPreviewPathRequested; // entityID, destination, isArmy, isAttack
        public event Action OnPreviewPathCleared;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private HexCoordinate? currentCoord;
        private Guid localPlayerID;
        private bool showingMoveSelection;
        private bool showingAttackSelection;
        private GameState cachedGameState;

        // Entrenchment confirmation state
        private bool showingEntrenchConfirm;
        private Guid pendingConfirmArmyID;
        private bool pendingConfirmIsAttack;

        // Preview state
        private Guid? previewedEntityID;
        private bool previewIsArmy;

        // Modal header label
        private Text modalHeaderLabel;

        // Cached references for incremental updates
        private Image buildingHPFill;
        private Image entrenchmentFill;
        private Text entrenchmentTimeLabel;

        // Structural fingerprint
        private bool hasCachedFingerprint;
        private int cachedBuildingCount; // 0 or 1
        private BuildingState cachedBuildingState;
        private int cachedBuildingLevel;
        private int cachedArmyCount;
        private int cachedVillagerCount;
        private bool cachedHasResource;
        private int cachedBuildingGarrison;
        private int cachedArmyStateHash;

        // Walking time estimate: seconds per tile for villagers
        private const double VillagerSecsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * GameConfig.Movement.VillagerSpeedMultiplier);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop for click-to-dismiss
            backdrop = new GameObject("TileInfoBackdrop");
            backdrop.transform.SetParent(canvasTransform, false);
            var backdropRT = backdrop.AddComponent<RectTransform>();
            backdropRT.anchorMin = Vector2.zero;
            backdropRT.anchorMax = Vector2.one;
            backdropRT.offsetMin = Vector2.zero;
            backdropRT.offsetMax = Vector2.zero;
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.3f);
            var backdropBtn = backdrop.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(() => OnCloseRequested?.Invoke());
            backdrop.SetActive(false);

            // Center-screen modal panel (~420x600)
            panel = UIHelper.CreatePanel(canvasTransform, "TileInfoPanel", UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 600f);

            // Header bar (terrain name + close button)
            var headerBar = UIHelper.CreateHorizontalRow(panel.transform, 36f, 4f);
            var headerBarRT = headerBar.GetComponent<RectTransform>();
            headerBarRT.anchorMin = new Vector2(0, 1);
            headerBarRT.anchorMax = new Vector2(1, 1);
            headerBarRT.pivot = new Vector2(0.5f, 1);
            headerBarRT.offsetMin = new Vector2(8, -40);
            headerBarRT.offsetMax = new Vector2(-8, -4);

            modalHeaderLabel = UIHelper.CreateLabel(headerBar.transform, "",
                UIConstants.FontHeader, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var headerLabelLE = modalHeaderLabel.gameObject.AddComponent<LayoutElement>();
            headerLabelLE.flexibleWidth = 1;

            var closeBtn = UIHelper.CreateButton(headerBar.transform, "X",
                SporefrontColors.ParchmentDeep, UIHelper.HudTextColor, 12, () => OnCloseRequested?.Invoke());
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredWidth = 28;
            closeBtnLE.preferredHeight = 28;

            // ScrollView (below header)
            var scroll = UIHelper.CreateScrollView(panel.transform, "TileScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMax = new Vector2(0, -44); // below header bar

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(HexCoordinate coord, GameState gameState, Guid playerID)
        {
            currentCoord = coord;
            localPlayerID = playerID;
            showingMoveSelection = false;
            showingAttackSelection = false;
            showingEntrenchConfirm = false;
            hasCachedFingerprint = false;
            ClearPreview();

            // Update modal header
            var tileNullable = gameState.mapData.GetTile(coord);
            if (tileNullable.HasValue && modalHeaderLabel != null)
                modalHeaderLabel.text = $"{tileNullable.Value.terrain} ({coord.q},{coord.r})";

            Rebuild(gameState);
            if (backdrop != null) backdrop.SetActive(true);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentCoord = null;
            showingMoveSelection = false;
            showingAttackSelection = false;
            showingEntrenchConfirm = false;
            ClearPreview();
            panel.SetActive(false);
            if (backdrop != null) backdrop.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !panel.activeSelf) return;

            // Skip full rebuild if structure hasn't changed
            if (hasCachedFingerprint && !showingMoveSelection && !showingAttackSelection && TileFingerprintMatches(gameState))
            {
                IncrementalTileUpdate(gameState);
                return;
            }

            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild Content
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentCoord.HasValue) return;
            var coord = currentCoord.Value;
            cachedGameState = gameState;

            // Clear existing content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            buildingHPFill = null;
            entrenchmentFill = null;
            entrenchmentTimeLabel = null;

            // Move/Attack selection views — show entity list instead of normal content
            var panelImg = panel.GetComponent<Image>();
            if (showingEntrenchConfirm)
            {
                panelImg.color = Color.Lerp(UIHelper.PanelParchmentBg, SporefrontColors.SporeAmber, 0.08f);
                BuildEntrenchConfirmView(gameState, coord);
                return;
            }
            if (showingMoveSelection)
            {
                panelImg.color = Color.Lerp(UIHelper.PanelParchmentBg, SporefrontColors.SporeTeal, 0.06f);
                BuildMoveSelectionView(gameState, coord);
                return;
            }
            if (showingAttackSelection)
            {
                panelImg.color = Color.Lerp(UIHelper.PanelParchmentBg, SporefrontColors.SporeRed, 0.06f);
                BuildAttackSelectionView(gameState, coord);
                return;
            }
            panelImg.color = UIHelper.PanelParchmentBg;

            var tileNullable = gameState.mapData.GetTile(coord);
            if (!tileNullable.HasValue) return;
            var tile = tileNullable.Value;

            // 1. Header: terrain + coordinate
            var header = UIHelper.CreateLabel(contentRT, $"{tile.terrain} ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // 2. Building section
            var building = gameState.GetBuilding(coord);
            if (building != null)
            {
                BuildBuildingSection(building, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 3. Armies section
            var armies = gameState.GetArmies(coord);
            if (armies != null && armies.Count > 0)
            {
                BuildArmiesSection(armies, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 4. Villagers section
            var villagers = gameState.GetVillagerGroups(coord);
            if (villagers != null && villagers.Count > 0)
            {
                BuildVillagersSection(villagers, coord, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 4b. Scouts section
            var scoutID = gameState.mapData.GetScoutID(coord);
            if (scoutID.HasValue)
            {
                var scout = gameState.GetScout(scoutID.Value);
                if (scout != null)
                {
                    BuildScoutSection(scout, coord, gameState);
                    UIHelper.CreateDivider(contentRT);
                }
            }

            // 5. Resource point section
            var rp = gameState.GetResourcePoint(coord);
            if (rp != null && !rp.IsDepleted())
            {
                BuildResourceSection(rp, gameState);
                UIHelper.CreateDivider(contentRT);
            }

            // 6. Actions: "Build Here" on empty buildable tiles
            if (building == null && gameState.CanBuildAt(coord, localPlayerID))
            {
                var buildBtn = UIHelper.CreateButton(contentRT, "Build Here",
                    SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                    () => OnBuildRequested?.Invoke(coord));
                var btnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 38;
            }

            // 7. "Move Here" — destination-first move flow
            var moveHereBtn = UIHelper.CreateButton(contentRT, "Move Here",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                () => { showingMoveSelection = true; Rebuild(cachedGameState); });
            var moveHereBtnLE = moveHereBtn.gameObject.AddComponent<LayoutElement>();
            moveHereBtnLE.preferredHeight = 38;

            // 8. "Attack Here" — destination-first attack flow (when tile has enemy entities)
            bool hasEnemyEntities = false;
            if (armies != null)
            {
                foreach (var army in armies)
                {
                    if (army.ownerID.HasValue && army.ownerID.Value != localPlayerID)
                    {
                        hasEnemyEntities = true;
                        break;
                    }
                }
            }
            if (hasEnemyEntities)
            {
                var attackHereBtn = UIHelper.CreateButton(contentRT, "Attack Here",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody,
                    () => { showingAttackSelection = true; Rebuild(cachedGameState); });
                var attackHereBtnLE = attackHereBtn.gameObject.AddComponent<LayoutElement>();
                attackHereBtnLE.preferredHeight = 38;
            }

            // Cache fingerprint for incremental refresh
            CacheTileFingerprint(gameState);
        }

        // ================================================================
        // Fingerprint & Incremental Update
        // ================================================================

        private int ComputeArmyStateHash(GameState gameState)
        {
            var coord = currentCoord.Value;
            var armies = gameState.GetArmies(coord);
            if (armies == null || armies.Count == 0) return 0;
            int hash = 17;
            foreach (var army in armies)
            {
                hash = hash * 31 + (army.isInCombat ? 1 : 0);
                hash = hash * 31 + (army.isEntrenched ? 2 : 0);
                hash = hash * 31 + (army.isEntrenching ? 4 : 0);
                hash = hash * 31 + (army.isRetreating ? 8 : 0);
                hash = hash * 31 + army.GetTotalUnits();
            }
            return hash;
        }

        private bool TileFingerprintMatches(GameState gameState)
        {
            var coord = currentCoord.Value;
            var building = gameState.GetBuilding(coord);
            int buildingCount = building != null ? 1 : 0;
            var armies = gameState.GetArmies(coord);
            int armyCount = armies != null ? armies.Count : 0;
            var villagers = gameState.GetVillagerGroups(coord);
            int villagerCount = villagers != null ? villagers.Count : 0;
            var rp = gameState.GetResourcePoint(coord);
            bool hasResource = rp != null && !rp.IsDepleted();

            if (buildingCount != cachedBuildingCount) return false;
            if (armyCount != cachedArmyCount) return false;
            if (villagerCount != cachedVillagerCount) return false;
            if (hasResource != cachedHasResource) return false;

            if (building != null)
            {
                if (building.state != cachedBuildingState) return false;
                if (building.level != cachedBuildingLevel) return false;
                if (building.villagerGarrison != cachedBuildingGarrison) return false;
            }

            // Check army combat/entrench/retreat state changes
            if (ComputeArmyStateHash(gameState) != cachedArmyStateHash) return false;

            return true;
        }

        private void CacheTileFingerprint(GameState gameState)
        {
            var coord = currentCoord.Value;
            var building = gameState.GetBuilding(coord);
            cachedBuildingCount = building != null ? 1 : 0;
            cachedBuildingState = building != null ? building.state : BuildingState.Planning;
            cachedBuildingLevel = building != null ? building.level : 0;
            cachedBuildingGarrison = building != null ? building.villagerGarrison : 0;
            var armies = gameState.GetArmies(coord);
            cachedArmyCount = armies != null ? armies.Count : 0;
            var villagers = gameState.GetVillagerGroups(coord);
            cachedVillagerCount = villagers != null ? villagers.Count : 0;
            var rp = gameState.GetResourcePoint(coord);
            cachedHasResource = rp != null && !rp.IsDepleted();
            cachedArmyStateHash = ComputeArmyStateHash(gameState);
            hasCachedFingerprint = true;
        }

        private void IncrementalTileUpdate(GameState gameState)
        {
            var coord = currentCoord.Value;
            cachedGameState = gameState;

            // Update building HP bar
            var building = gameState.GetBuilding(coord);
            if (building != null && building.maxHealth > 0 && buildingHPFill != null)
            {
                float hpPct = (float)(building.health / building.maxHealth);
                var fillRT = buildingHPFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
            }

            // Update entrenchment progress bar
            if (entrenchmentFill != null)
            {
                var armies = gameState.GetArmies(coord);
                if (armies != null)
                {
                    foreach (var army in armies)
                    {
                        if (army.isEntrenching && army.entrenchmentStartTime.HasValue)
                        {
                            double elapsed = gameState.currentTime - army.entrenchmentStartTime.Value;
                            double buildTime = GameConfig.Entrenchment.BuildTime;
                            float progress = Mathf.Clamp01((float)(elapsed / buildTime));
                            var fillRT = entrenchmentFill.GetComponent<RectTransform>();
                            fillRT.anchorMax = new Vector2(progress, 1);

                            if (entrenchmentTimeLabel != null)
                            {
                                double remaining = buildTime - elapsed;
                                entrenchmentTimeLabel.text = $"Entrenching: {UIHelper.FormatTime(remaining)}";
                            }
                            break; // only one entrenching army at a time on a tile
                        }
                    }
                }
            }
        }

        // ================================================================
        // Section Builders
        // ================================================================

        private void BuildBuildingSection(BuildingData building, GameState gameState)
        {
            bool isOwned = building.ownerID.HasValue && building.ownerID.Value == localPlayerID;
            string displayName = building.buildingType.DisplayName();

            var label = UIHelper.CreateLabel(contentRT,
                $"{displayName} Lv.{building.level}", UIConstants.FontBody, null, TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 30;

            // State
            string stateStr = building.state.ToString();
            var stateLabel = UIHelper.CreateLabel(contentRT, stateStr, UIConstants.FontBody);
            var stateLE = stateLabel.gameObject.AddComponent<LayoutElement>();
            stateLE.preferredHeight = 24;

            // HP bar
            if (building.maxHealth > 0)
            {
                var (bg, fill) = UIHelper.CreateInkProgressBar(contentRT, 14f,
                    UIHelper.InkMutedText, SporefrontColors.SporeGreen);
                float hpPct = (float)(building.health / building.maxHealth);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.preferredHeight = 14;
                buildingHPFill = fill;
            }

            // Details button (owned buildings only)
            if (isOwned && building.IsOperational)
            {
                var detailBtn = UIHelper.CreateButton(contentRT, "Details",
                    SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                    () => OnBuildingDetailRequested?.Invoke(building.id));
                var btnLE = detailBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 34;
            }

            // Quick train villager button (City Center / Neighborhood)
            if (isOwned && building.IsOperational && building.CanTrainVillagers())
            {
                int queueCount = building.villagerTrainingQueue.Count;
                string btnLabel = queueCount > 0
                    ? $"Train Villager ({queueCount} queued)"
                    : "Train Villager (50f)";

                var capturedBuildingID = building.id;
                var trainBtn = UIHelper.CreateButton(contentRT, btnLabel,
                    SporefrontColors.SporeGreen, UIHelper.ButtonText, UIConstants.FontBody,
                    () => OnTrainVillagerRequested?.Invoke(capturedBuildingID));
                var trainLabel = UIHelper.GetButtonLabel(trainBtn);
                UIHelper.EnableAutoFit(trainLabel, 11, UIConstants.FontBody);
                var trainBtnLE = trainBtn.gameObject.AddComponent<LayoutElement>();
                trainBtnLE.preferredHeight = 36;
            }

            // Quick deploy villagers button
            if (isOwned && building.IsOperational && building.villagerGarrison > 0)
            {
                var capturedBuildingID = building.id;
                var capturedCount = building.villagerGarrison;
                var deployBtn = UIHelper.CreateButton(contentRT,
                    $"Deploy Villagers ({building.villagerGarrison})",
                    SporefrontColors.SporeGreen, UIHelper.ButtonText, UIConstants.FontBody,
                    () => OnDeployVillagersRequested?.Invoke(capturedBuildingID, capturedCount));
                var deployLabel = UIHelper.GetButtonLabel(deployBtn);
                UIHelper.EnableAutoFit(deployLabel, 11, UIConstants.FontBody);
                var deployBtnLE = deployBtn.gameObject.AddComponent<LayoutElement>();
                deployBtnLE.preferredHeight = 36;
            }

            // Quick upgrade button (all building types)
            if (isOwned && building.CanUpgrade)
            {
                var cost = building.GetUpgradeCost();
                var player = gameState.GetPlayer(localPlayerID);
                bool canAfford = player != null && player.CanAfford(cost);
                int nextLevel = building.level + 1;

                var capturedBuildingID = building.id;
                var upgradeBtn = UIHelper.CreateButton(contentRT,
                    $"Upgrade to Lv.{nextLevel} ({UIHelper.FormatCost(cost)})",
                    canAfford ? SporefrontColors.SporeAmber : UIHelper.InkMutedText,
                    canAfford ? UIHelper.ButtonText : UIHelper.InkMutedText,
                    UIConstants.FontBody,
                    () => OnUpgradeBuildingRequested?.Invoke(capturedBuildingID));
                upgradeBtn.interactable = canAfford;
                var upgradeLabel = UIHelper.GetButtonLabel(upgradeBtn);
                UIHelper.EnableAutoFit(upgradeLabel, 11, UIConstants.FontBody);
                var upgradeBtnLE = upgradeBtn.gameObject.AddComponent<LayoutElement>();
                upgradeBtnLE.preferredHeight = 36;
            }
        }

        private void BuildArmiesSection(List<ArmyData> armies, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Armies",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            foreach (var army in armies)
            {
                bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;
                string status = UIHelper.FormatArmyStatus(army);
                int total = army.GetTotalUnits();

                // Row 1: Name + status + unit count
                var nameRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{army.name}{status} ({total})", UIConstants.FontBody);
                nameLabel.supportRichText = true;
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;
                nameLE.preferredHeight = 30;

                // Entrenchment progress bar
                if (army.isEntrenching && army.entrenchmentStartTime.HasValue)
                {
                    double currentTime = gameState.currentTime;
                    double elapsed = currentTime - army.entrenchmentStartTime.Value;
                    double buildTime = GameConfig.Entrenchment.BuildTime;
                    float progress = Mathf.Clamp01((float)(elapsed / buildTime));
                    double remaining = buildTime - elapsed;

                    var (bg, fill) = UIHelper.CreateInkProgressBar(contentRT, 14f,
                        UIHelper.InkMutedText, SporefrontColors.SporeAmber);
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(progress, 1);
                    var barLE = bg.gameObject.AddComponent<LayoutElement>();
                    barLE.preferredHeight = 14;
                    entrenchmentFill = fill;

                    var timeLabel = UIHelper.CreateLabel(contentRT,
                        $"Entrenching: {UIHelper.FormatTime(remaining)}", UIConstants.FontCaption,
                        SporefrontColors.SporeAmber);
                    var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
                    timeLE.preferredHeight = 18;
                    entrenchmentTimeLabel = timeLabel;
                }

                if (army.isEntrenched)
                {
                    int defBonus = (int)(GameConfig.Entrenchment.DefenseBonus * 100);
                    int covered = army.entrenchedCoveredTiles != null ? army.entrenchedCoveredTiles.Count : 0;
                    var entrenchLabel = UIHelper.CreateLabel(contentRT,
                        $"Entrenched: +{defBonus}% Defense | Covering {covered} tiles",
                        UIConstants.FontCaption, SporefrontColors.SporeTeal);
                    var entrenchLE = entrenchLabel.gameObject.AddComponent<LayoutElement>();
                    entrenchLE.preferredHeight = 20;
                }

                if (isOwned && army.isStranded)
                {
                    // Stranded army — show status label and only Info button
                    var strandedLabel = UIHelper.CreateLabel(contentRT,
                        "Stranded — build a home base to retreat",
                        UIConstants.FontCaption, SporefrontColors.SporeRed);
                    var strandedLE = strandedLabel.gameObject.AddComponent<LayoutElement>();
                    strandedLE.preferredHeight = 20;

                    var btnRow = UIHelper.CreateHorizontalRow(contentRT, 34f, 3f);
                    var capturedArmyID = army.id;
                    CreateActionButton(btnRow.transform, "Info", () =>
                        OnArmyDetailRequested?.Invoke(capturedArmyID));
                }
                else if (isOwned)
                {
                    // Row 2: Action buttons
                    var btnRow = UIHelper.CreateHorizontalRow(contentRT, 34f, 3f);

                    // Stop — only when army is actively moving
                    if (army.currentPath != null && army.pathIndex < army.currentPath.Count && !army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Stop", () =>
                        {
                            var cmd = new StopMovementCommand(localPlayerID, capturedArmyID);
                            UIManager.ExecutePlayerCommand(cmd);
                        });
                    }

                    // Entrench
                    if (!army.isEntrenched && !army.isEntrenching && !army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Dig In", () =>
                        {
                            var cmd = new EntrenchCommand(localPlayerID, capturedArmyID);
                            UIManager.ExecutePlayerCommand(cmd);
                        });
                    }

                    // Retreat
                    if (!army.isRetreating)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Retreat", () =>
                        {
                            var cmd = new RetreatCommand(localPlayerID, capturedArmyID);
                            UIManager.ExecutePlayerCommand(cmd);
                        });
                    }

                    // Move
                    if (!army.isInCombat)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Move", () =>
                            OnArmyMoveRequested?.Invoke(capturedArmyID));
                    }

                    // Garrison — at owned operational building with garrison capacity
                    if (!army.isInCombat)
                    {
                        var building = gameState.GetBuilding(army.coordinate);
                        if (building != null && building.ownerID.HasValue &&
                            building.ownerID.Value == localPlayerID && building.IsOperational &&
                            building.GetGarrisonCapacity() > 0)
                        {
                            var capturedBuildingID = building.id;
                            var capturedArmyID = army.id;
                            CreateActionButton(btnRow.transform, "Garrison", () =>
                            {
                                var cmd = new GarrisonArmyCommand(localPlayerID, capturedArmyID, capturedBuildingID);
                                UIManager.ExecutePlayerCommand(cmd);
                            });
                        }
                    }

                    // Attack
                    if (!army.isInCombat && !army.isRetreating)
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Attack", () =>
                            OnAttackRequested?.Invoke(capturedArmyID, army.coordinate));
                    }

                    // Info
                    {
                        var capturedArmyID = army.id;
                        CreateActionButton(btnRow.transform, "Info", () =>
                            OnArmyDetailRequested?.Invoke(capturedArmyID));
                    }
                }
                else if (army.ownerID.HasValue && army.ownerID.Value != localPlayerID)
                {
                    // Enemy army — label tag (use "Attack Here" button for destination-first flow)
                    var enemyTag = UIHelper.CreateLabel(nameRow.transform, "Enemy",
                        UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
                    var tagLE = enemyTag.gameObject.AddComponent<LayoutElement>();
                    tagLE.preferredWidth = 48;
                    tagLE.preferredHeight = 30;
                }
            }
        }

        private void BuildVillagersSection(List<VillagerGroupData> groups, HexCoordinate coord, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Villagers",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            foreach (var group in groups)
            {
                bool isOwned = group.ownerID.HasValue && group.ownerID.Value == localPlayerID;
                string task = group.currentTask != null ? group.currentTask.DisplayName : "Idle";

                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

                var label = UIHelper.CreateLabel(row.transform,
                    $"{group.villagerCount}x — {task}", UIConstants.FontBody);
                var labelLE = label.gameObject.AddComponent<LayoutElement>();
                labelLE.flexibleWidth = 1;

                if (isOwned)
                {
                    var capturedGroupID = group.id;

                    var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                        SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                        () => OnMoveRequested?.Invoke(capturedGroupID, coord));
                    var moveBtnLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                    moveBtnLE.preferredWidth = 50;
                    moveBtnLE.preferredHeight = 28;

                    // Cancel task button
                    if (group.currentTask != null && !group.currentTask.IsIdle)
                    {
                        var cancelBtn = UIHelper.CreateButton(row.transform, "Cancel",
                            SporefrontColors.ParchmentDeep, UIHelper.HudTextColor, UIConstants.FontBody,
                            () =>
                            {
                                var cmd = new StopGatheringCommand(localPlayerID, capturedGroupID);
                                UIManager.ExecutePlayerCommand(cmd);
                            });
                        var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
                        cancelLE.preferredWidth = 56;
                        cancelLE.preferredHeight = 28;
                    }
                }
            }
        }

        private void BuildScoutSection(ScoutData scout, HexCoordinate coord, GameState gameState)
        {
            var sectionHeader = UIHelper.CreateLabel(contentRT, "Mycelium Scout",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var headerLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            bool isOwned = scout.ownerID.HasValue && scout.ownerID.Value == localPlayerID;

            // Stats row
            var statsRow = UIHelper.CreateHorizontalRow(contentRT, 22f, 4f);
            var hpLabel = UIHelper.CreateLabel(statsRow.transform,
                $"HP: {(int)scout.hp}/{(int)scout.maxHp}", UIConstants.FontCaption, UIHelper.InkBodyText);
            var hpLE = hpLabel.gameObject.AddComponent<LayoutElement>();
            hpLE.flexibleWidth = 1;

            var staminaLabel = UIHelper.CreateLabel(statsRow.transform,
                $"Stamina: {(int)scout.stamina}/{(int)scout.maxStamina}", UIConstants.FontCaption, UIHelper.InkBodyText);
            var staminaLE = staminaLabel.gameObject.AddComponent<LayoutElement>();
            staminaLE.flexibleWidth = 1;

            var visionLabel = UIHelper.CreateLabel(statsRow.transform,
                $"Vision: {scout.visionRange}", UIConstants.FontCaption, UIHelper.InkMutedText);
            var visionLE = visionLabel.gameObject.AddComponent<LayoutElement>();
            visionLE.preferredWidth = 60;

            if (isOwned)
            {
                var capturedScoutID = scout.id;
                var actionRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);
                var spacer = new GameObject("Spacer");
                spacer.transform.SetParent(actionRow.transform, false);
                var spacerLE = spacer.AddComponent<LayoutElement>();
                spacerLE.flexibleWidth = 1;

                var moveBtn = UIHelper.CreateButton(actionRow.transform, "Move",
                    SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                    () => OnMoveRequested?.Invoke(capturedScoutID, coord));
                var moveBtnLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                moveBtnLE.preferredWidth = 50;
                moveBtnLE.preferredHeight = 28;
            }
        }

        private void BuildResourceSection(ResourcePointData rp, GameState gameState)
        {
            var label = UIHelper.CreateLabel(contentRT,
                $"Resource: {rp.resourceType} ({rp.remainingAmount})", UIConstants.FontBody, null,
                TextAnchor.MiddleLeft);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 26;

            bool isHuntable = rp.resourceType.IsHuntable() && rp.currentHealth > 0;

            if (isHuntable)
            {
                // Hunt button — opens GatherPanel in hunt mode with villager selection
                var capturedRPID = rp.id;
                var huntBtn = UIHelper.CreateButton(contentRT, "Hunt",
                    SporefrontColors.SporeRed,
                    UIHelper.HudTextColor, UIConstants.FontBody,
                    () => OnHuntRequested?.Invoke(capturedRPID));
                var btnLE = huntBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 34;
            }
            else if (rp.resourceType.RequiresCamp())
            {
                // Camp-required resources: check camp coverage via ResourceEngine
                bool hasCoverage = GameEngine.Instance.resourceEngine.HasCampCoverage(
                    rp.coordinate, rp.resourceType, gameState);

                if (hasCoverage)
                {
                    // Gather button — opens GatherPanel with villager selection
                    var capturedRPID = rp.id;
                    var gatherBtn = UIHelper.CreateButton(contentRT, "Gather",
                        SporefrontColors.SporeGreen,
                        UIHelper.HudTextColor, UIConstants.FontBody,
                        () => OnGatherSelectionRequested?.Invoke(capturedRPID));
                    var btnLE = gatherBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredHeight = 34;
                }
                else
                {
                    // Show "Needs [Camp Type]" label
                    string campName = rp.resourceType == ResourcePointType.Trees
                        ? "Lumber Camp"
                        : "Mining Camp";
                    var needsLabel = UIHelper.CreateLabel(contentRT,
                        $"Needs {campName}", UIConstants.FontBody, UIHelper.InkMutedText,
                        TextAnchor.MiddleLeft);
                    var needsLE = needsLabel.gameObject.AddComponent<LayoutElement>();
                    needsLE.preferredHeight = 26;
                }
            }
            else
            {
                // Non-camp resources (Farmland, Forage, Carcasses): open GatherPanel with villager selection
                var capturedRPID = rp.id;
                var gatherBtn = UIHelper.CreateButton(contentRT, "Gather",
                    SporefrontColors.SporeGreen,
                    UIHelper.HudTextColor, UIConstants.FontBody,
                    () => OnGatherSelectionRequested?.Invoke(capturedRPID));
                var btnLE = gatherBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredHeight = 34;
            }
        }

        private void BuildMoveSelectionView(GameState gameState, HexCoordinate coord)
        {
            // Guard: if previewed entity is no longer in the lists, reset
            if (previewedEntityID.HasValue)
            {
                bool found = false;
                var checkArmies = gameState.GetArmiesForPlayer(localPlayerID);
                if (checkArmies != null)
                    foreach (var a in checkArmies)
                        if (a.id == previewedEntityID.Value) { found = true; break; }
                if (!found)
                {
                    var checkVGs = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                    if (checkVGs != null)
                        foreach (var v in checkVGs)
                            if (v.id == previewedEntityID.Value) { found = true; break; }
                }
                if (!found) ClearPreview();
            }

            // Breadcrumb header with back arrow
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

            var backArrow = UIHelper.CreateButton(headerRow.transform, "<",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                () => { showingMoveSelection = false; ClearPreview(); Rebuild(cachedGameState); });
            var arrowLE = backArrow.gameObject.AddComponent<LayoutElement>();
            arrowLE.preferredWidth = 28;
            arrowLE.preferredHeight = 28;

            var header = UIHelper.CreateLabel(headerRow.transform, $"Move to ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.flexibleWidth = 1;
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // Armies section — sorted by distance
            var armies = gameState.GetArmiesForPlayer(localPlayerID);
            bool hasArmies = false;
            if (armies != null && armies.Count > 0)
            {
                var movableArmies = armies.Where(a => !a.isInCombat).ToList();
                movableArmies.Sort((a, b) =>
                    a.coordinate.Distance(coord).CompareTo(b.coordinate.Distance(coord)));

                foreach (var army in movableArmies)
                {
                    if (!hasArmies)
                    {
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Armies",
                            UIConstants.FontSubheader, UIHelper.InkHeaderText,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasArmies = true;
                    }

                    BuildArmyMoveRow(army, coord, false);
                }
            }

            // Villagers section — sorted by distance
            var villagerGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            bool hasVillagers = false;
            if (villagerGroups != null && villagerGroups.Count > 0)
            {
                var sortedGroups = villagerGroups.Where(g => g.villagerCount > 0).ToList();
                sortedGroups.Sort((a, b) =>
                    a.coordinate.Distance(coord).CompareTo(b.coordinate.Distance(coord)));

                foreach (var group in sortedGroups)
                {
                    if (!hasVillagers)
                    {
                        if (hasArmies) UIHelper.CreateDivider(contentRT);
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Villagers",
                            UIConstants.FontSubheader, UIHelper.InkHeaderText,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasVillagers = true;
                    }

                    int distance = group.coordinate.Distance(coord);
                    bool isBusy = group.currentTask != null && !group.currentTask.IsIdle;
                    string taskDesc = isBusy ? group.currentTask.DisplayName : "Idle";
                    bool isSelected = previewedEntityID.HasValue && previewedEntityID.Value == group.id;

                    // Walking time estimate
                    int walkSeconds = distance > 0 ? Mathf.CeilToInt((float)(distance * VillagerSecsPerTile)) : 0;
                    string walkTimeStr = walkSeconds > 0
                        ? (walkSeconds < 60 ? $"~{walkSeconds}s" : $"~{walkSeconds / 60}m{walkSeconds % 60}s")
                        : "Here";

                    Color rowBg = isSelected
                        ? Color.Lerp(Color.clear, SporefrontColors.SporeTeal, 0.12f)
                        : Color.clear;
                    var row = UIHelper.CreatePanel(contentRT, "VillagerRow", rowBg);
                    var rowLE = row.AddComponent<LayoutElement>();
                    rowLE.preferredHeight = isBusy ? 72 : 56;

                    var vlg = row.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 2;
                    vlg.padding = new RectOffset(8, 8, 2, 2);
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;

                    // Line 1: Name (count) | distance
                    var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                    var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                        $"{group.name} ({group.villagerCount})", UIConstants.FontBody,
                        isBusy ? SporefrontColors.SporeAmber : UIHelper.InkBodyText);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;

                    var distLabel = UIHelper.CreateLabel(nameRow.transform,
                        $"{distance} tiles", UIConstants.FontCaption, UIHelper.InkMutedText);
                    var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
                    distLE.preferredWidth = 55;

                    // Line 2: Task status | walk time
                    var infoRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                    var taskLabel = UIHelper.CreateLabel(infoRow.transform,
                        taskDesc, UIConstants.FontCaption,
                        isBusy ? SporefrontColors.SporeAmber : UIHelper.InkMutedText);
                    var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
                    taskLE.flexibleWidth = 1;

                    var walkLabel = UIHelper.CreateLabel(infoRow.transform,
                        walkTimeStr, UIConstants.FontCaption, UIHelper.InkMutedText);
                    var walkLE = walkLabel.gameObject.AddComponent<LayoutElement>();
                    walkLE.preferredWidth = 55;

                    // Action row
                    var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);

                    if (isBusy)
                    {
                        var warnLabel = UIHelper.CreateLabel(actionRow.transform,
                            $"Will cancel {taskDesc}", UIConstants.FontCaption, SporefrontColors.SporeAmber);
                        var warnLE = warnLabel.gameObject.AddComponent<LayoutElement>();
                        warnLE.flexibleWidth = 1;
                    }
                    else
                    {
                        var spacer = new GameObject("Spacer");
                        spacer.transform.SetParent(actionRow.transform, false);
                        var spacerLE = spacer.AddComponent<LayoutElement>();
                        spacerLE.flexibleWidth = 1;
                    }

                    var capturedID = group.id;
                    Color btnColor = isBusy ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDeep;
                    Color btnTextColor = isBusy ? UIHelper.HudTextColor : UIHelper.ButtonText;
                    var selectBtn = UIHelper.CreateButton(actionRow.transform,
                        isSelected ? "Selected" : "Select",
                        btnColor, btnTextColor, UIConstants.FontBody,
                        () =>
                        {
                            previewedEntityID = capturedID;
                            previewIsArmy = false;
                            OnPreviewPathRequested?.Invoke(capturedID, coord, false, false);
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 70;
                    btnLE.preferredHeight = 24;
                }
            }

            // Scouts section — sorted by distance
            var allScouts = gameState.GetScoutsForPlayer(localPlayerID);
            bool hasScouts = false;
            if (allScouts != null && allScouts.Count > 0)
            {
                var sortedScouts = new List<ScoutData>(allScouts);
                sortedScouts.Sort((a, b) =>
                    a.coordinate.Distance(coord).CompareTo(b.coordinate.Distance(coord)));

                foreach (var scout in sortedScouts)
                {
                    if (!hasScouts)
                    {
                        if (hasArmies || hasVillagers) UIHelper.CreateDivider(contentRT);
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Scouts",
                            UIConstants.FontSubheader, UIHelper.InkHeaderText,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasScouts = true;
                    }

                    int distance = scout.coordinate.Distance(coord);
                    bool isSelected = previewedEntityID.HasValue && previewedEntityID.Value == scout.id;

                    Color rowBg = isSelected
                        ? Color.Lerp(Color.clear, SporefrontColors.SporeTeal, 0.12f)
                        : Color.clear;
                    var row = UIHelper.CreatePanel(contentRT, "ScoutRow", rowBg);
                    var rowLE = row.AddComponent<LayoutElement>();
                    rowLE.preferredHeight = 56;

                    var vlg = row.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 2;
                    vlg.padding = new RectOffset(8, 8, 2, 2);
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;

                    var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                    var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                        "Mycelium Scout", UIConstants.FontBody, UIHelper.InkBodyText);
                    var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                    nameLE.flexibleWidth = 1;

                    var distLabel = UIHelper.CreateLabel(nameRow.transform,
                        $"{distance} tiles", UIConstants.FontCaption, UIHelper.InkMutedText);
                    var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
                    distLE.preferredWidth = 55;

                    var infoRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                    var staminaLabel = UIHelper.CreateLabel(infoRow.transform,
                        $"Stamina: {(int)scout.stamina}/{(int)scout.maxStamina}", UIConstants.FontCaption, UIHelper.InkMutedText);
                    var staminaLE = staminaLabel.gameObject.AddComponent<LayoutElement>();
                    staminaLE.flexibleWidth = 1;

                    var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);
                    var spacer = new GameObject("Spacer");
                    spacer.transform.SetParent(actionRow.transform, false);
                    var spacerLE = spacer.AddComponent<LayoutElement>();
                    spacerLE.flexibleWidth = 1;

                    var capturedID = scout.id;
                    var selectBtn = UIHelper.CreateButton(actionRow.transform,
                        isSelected ? "Selected" : "Select",
                        SporefrontColors.ParchmentDeep, UIHelper.ButtonText, UIConstants.FontBody,
                        () =>
                        {
                            previewedEntityID = capturedID;
                            previewIsArmy = false;
                            OnPreviewPathRequested?.Invoke(capturedID, coord, false, false);
                            Rebuild(cachedGameState);
                        });
                    var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 70;
                    btnLE.preferredHeight = 24;
                }
            }

            if (!hasArmies && !hasVillagers && !hasScouts)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No movable entities",
                    UIConstants.FontBody, null, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
            }

            // Confirm Move button — only when an entity is previewed
            if (previewedEntityID.HasValue)
            {
                UIHelper.CreateDivider(contentRT);
                var confirmBtn = UIHelper.CreateButton(contentRT, "Confirm Move",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontBody,
                    () =>
                    {
                        var entityID = previewedEntityID.Value;
                        bool isArmy = previewIsArmy;

                        // Check if entrenched army — route through entrenchment confirm
                        if (isArmy)
                        {
                            var army = cachedGameState.GetArmy(entityID);
                            if (army != null && (army.isEntrenched || army.isEntrenching))
                            {
                                pendingConfirmArmyID = entityID;
                                pendingConfirmIsAttack = false;
                                showingEntrenchConfirm = true;
                                showingMoveSelection = false;
                                ClearPreview();
                                Rebuild(cachedGameState);
                                return;
                            }
                        }

                        OnMoveEntityToTile?.Invoke(entityID, coord, isArmy);
                        showingMoveSelection = false;
                        ClearPreview();
                        Rebuild(cachedGameState);
                    });
                var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                confirmLE.preferredHeight = 38;
            }
        }

        private void BuildAttackSelectionView(GameState gameState, HexCoordinate coord)
        {
            // Guard: if previewed entity is no longer in the lists, reset
            if (previewedEntityID.HasValue)
            {
                bool found = false;
                var checkArmies = gameState.GetArmiesForPlayer(localPlayerID);
                if (checkArmies != null)
                    foreach (var a in checkArmies)
                        if (a.id == previewedEntityID.Value && !a.isInCombat && !a.isRetreating)
                        { found = true; break; }
                if (!found) ClearPreview();
            }

            // Breadcrumb header with back arrow
            var headerRow = UIHelper.CreateHorizontalRow(contentRT, 30f, 4f);

            var backArrow = UIHelper.CreateButton(headerRow.transform, "<",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                () => { showingAttackSelection = false; ClearPreview(); Rebuild(cachedGameState); });
            var arrowLE = backArrow.gameObject.AddComponent<LayoutElement>();
            arrowLE.preferredWidth = 28;
            arrowLE.preferredHeight = 28;

            var header = UIHelper.CreateLabel(headerRow.transform, $"Attack ({coord.q},{coord.r})",
                UIConstants.FontHeader, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.flexibleWidth = 1;
            headerLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);

            // List player's armies that can attack — sorted by distance
            var armies = gameState.GetArmiesForPlayer(localPlayerID);
            bool hasArmies = false;
            if (armies != null && armies.Count > 0)
            {
                var attackableArmies = armies.Where(a => !a.isInCombat && !a.isRetreating).ToList();
                attackableArmies.Sort((a, b) =>
                    a.coordinate.Distance(coord).CompareTo(b.coordinate.Distance(coord)));

                foreach (var army in attackableArmies)
                {
                    if (!hasArmies)
                    {
                        var sectionHeader = UIHelper.CreateLabel(contentRT, "Your Armies",
                            UIConstants.FontSubheader, UIHelper.InkHeaderText,
                            TextAnchor.MiddleLeft, true);
                        var shLE = sectionHeader.gameObject.AddComponent<LayoutElement>();
                        shLE.preferredHeight = 28;
                        hasArmies = true;
                    }

                    BuildArmyMoveRow(army, coord, true);
                }
            }

            if (!hasArmies)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT, "No available armies",
                    UIConstants.FontBody, null, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 30;
            }

            // Confirm Attack button — only when an army is previewed
            if (previewedEntityID.HasValue)
            {
                UIHelper.CreateDivider(contentRT);
                var confirmBtn = UIHelper.CreateButton(contentRT, "Confirm Attack",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody,
                    () =>
                    {
                        var armyID = previewedEntityID.Value;

                        // Check if entrenched army — route through entrenchment confirm
                        var army = cachedGameState.GetArmy(armyID);
                        if (army != null && (army.isEntrenched || army.isEntrenching))
                        {
                            pendingConfirmArmyID = armyID;
                            pendingConfirmIsAttack = true;
                            showingEntrenchConfirm = true;
                            showingAttackSelection = false;
                            ClearPreview();
                            Rebuild(cachedGameState);
                            return;
                        }

                        OnAttackEntityToTile?.Invoke(armyID, coord);
                        showingAttackSelection = false;
                        ClearPreview();
                        Rebuild(cachedGameState);
                    });
                var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                confirmLE.preferredHeight = 38;
            }
        }

        // ================================================================
        // Shared Army Row (Move / Attack selection)
        // ================================================================

        private void BuildArmyMoveRow(ArmyData army, HexCoordinate coord, bool isAttack)
        {
            bool isEntrenched = army.isEntrenched || army.isEntrenching;
            bool isMoving = army.currentPath != null && army.pathIndex < army.currentPath.Count;
            int total = army.GetTotalUnits();
            int distance = army.coordinate.Distance(coord);
            bool isSelected = previewedEntityID.HasValue && previewedEntityID.Value == army.id;

            // Travel time: distance / (BaseSpeed * 1.6 / SlowestUnitMoveSpeed)
            double secsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * (1.6 / army.SlowestUnitMoveSpeed));
            int travelSeconds = distance > 0 ? Mathf.CeilToInt((float)(distance * secsPerTile)) : 0;
            string travelTimeStr = travelSeconds > 0
                ? (travelSeconds < 60 ? $"~{travelSeconds}s" : $"~{travelSeconds / 60}m{travelSeconds % 60}s")
                : "Here";

            // Status
            string status;
            bool isBusy;
            if (army.isEntrenching) { status = "Entrenching"; isBusy = true; }
            else if (army.isEntrenched) { status = "Entrenched"; isBusy = true; }
            else if (isMoving) { status = "Moving"; isBusy = true; }
            else { status = "Idle"; isBusy = false; }

            // Tinted row background when selected
            Color rowBg = isSelected
                ? Color.Lerp(Color.clear, isAttack ? SporefrontColors.SporeRed : SporefrontColors.SporeTeal, 0.12f)
                : Color.clear;
            var row = UIHelper.CreatePanel(contentRT, "ArmyRow", rowBg);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = isEntrenched ? 72 : 56;

            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(8, 8, 2, 2);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Line 1: Name (count) | distance
            var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
            var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                $"{army.name} ({total})", UIConstants.FontBody,
                isBusy ? SporefrontColors.SporeAmber : UIHelper.InkBodyText);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            var distLabel = UIHelper.CreateLabel(nameRow.transform,
                $"{distance} tiles", UIConstants.FontCaption, UIHelper.InkMutedText);
            var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
            distLE.preferredWidth = 55;

            // Line 2: Status | travel time
            var infoRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
            var statusLabel = UIHelper.CreateLabel(infoRow.transform,
                status, UIConstants.FontCaption,
                isBusy ? SporefrontColors.SporeAmber : UIHelper.InkMutedText);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.flexibleWidth = 1;

            var timeLabel = UIHelper.CreateLabel(infoRow.transform,
                travelTimeStr, UIConstants.FontCaption, UIHelper.InkMutedText);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredWidth = 55;

            // Action row
            var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);

            if (isEntrenched)
            {
                var warnLabel = UIHelper.CreateLabel(actionRow.transform,
                    "Will abandon entrenchment", UIConstants.FontCaption, SporefrontColors.SporeAmber);
                var warnLE = warnLabel.gameObject.AddComponent<LayoutElement>();
                warnLE.flexibleWidth = 1;
            }
            else
            {
                var spacer = new GameObject("Spacer");
                spacer.transform.SetParent(actionRow.transform, false);
                var spacerLE = spacer.AddComponent<LayoutElement>();
                spacerLE.flexibleWidth = 1;
            }

            var capturedID = army.id;
            Color btnColor = isAttack ? SporefrontColors.SporeRed
                : (isEntrenched ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDeep);
            Color btnTextColor = isAttack || isEntrenched ? UIHelper.HudTextColor : UIHelper.ButtonText;
            var selectBtn = UIHelper.CreateButton(actionRow.transform,
                isSelected ? "Selected" : "Select",
                btnColor, btnTextColor, UIConstants.FontBody,
                () =>
                {
                    previewedEntityID = capturedID;
                    previewIsArmy = true;
                    OnPreviewPathRequested?.Invoke(capturedID, coord, true, isAttack);
                    Rebuild(cachedGameState);
                });
            var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 70;
            btnLE.preferredHeight = 24;
        }

        // ================================================================
        // Entrenchment Confirmation View
        // ================================================================

        private void BuildEntrenchConfirmView(GameState gameState, HexCoordinate coord)
        {
            var army = gameState.GetArmy(pendingConfirmArmyID);
            string armyName = army != null ? army.name : "Army";
            string action = pendingConfirmIsAttack ? "Attacking" : "Moving";

            // Warning header
            var warnHeader = UIHelper.CreateLabel(contentRT, "Abandon Entrenchment?",
                UIConstants.FontHeader, SporefrontColors.SporeAmber, TextAnchor.MiddleCenter, true);
            var whLE = warnHeader.gameObject.AddComponent<LayoutElement>();
            whLE.preferredHeight = 32;

            UIHelper.CreateDivider(contentRT);

            // Warning message
            var msg = UIHelper.CreateLabel(contentRT,
                $"{action} {armyName} will abandon its entrenched position, losing all defensive bonuses.",
                UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleCenter);
            var msgLE = msg.gameObject.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 50;

            // Entrenchment details
            if (army != null && army.isEntrenched)
            {
                int defBonus = (int)(GameConfig.Entrenchment.DefenseBonus * 100);
                int covered = army.entrenchedCoveredTiles != null ? army.entrenchedCoveredTiles.Count : 0;
                var detailLabel = UIHelper.CreateLabel(contentRT,
                    $"Current bonus: +{defBonus}% Defense, covering {covered} tiles",
                    UIConstants.FontCaption, SporefrontColors.SporeTeal, TextAnchor.MiddleCenter);
                var detailLE = detailLabel.gameObject.AddComponent<LayoutElement>();
                detailLE.preferredHeight = 22;
            }

            UIHelper.CreateDivider(contentRT);

            // Confirm button
            var confirmBtn = UIHelper.CreateButton(contentRT, $"Confirm {(pendingConfirmIsAttack ? "Attack" : "Move")}",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody,
                () =>
                {
                    showingEntrenchConfirm = false;
                    if (pendingConfirmIsAttack)
                    {
                        OnAttackEntityToTile?.Invoke(pendingConfirmArmyID, coord);
                    }
                    else
                    {
                        OnMoveEntityToTile?.Invoke(pendingConfirmArmyID, coord, true);
                    }
                    Rebuild(cachedGameState);
                });
            var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
            confirmLE.preferredHeight = 38;

            // Cancel button
            var cancelBtn = UIHelper.CreateButton(contentRT, "Cancel",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                () =>
                {
                    showingEntrenchConfirm = false;
                    Rebuild(cachedGameState);
                });
            var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            cancelLE.preferredHeight = 38;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateActionButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.ParchmentDeep, UIHelper.HudTextColor, UIConstants.FontBody, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 56;
            le.preferredHeight = 34;
            return btn;
        }

        private void ClearPreview()
        {
            if (previewedEntityID.HasValue)
            {
                previewedEntityID = null;
                OnPreviewPathCleared?.Invoke();
            }
        }
    }
}
