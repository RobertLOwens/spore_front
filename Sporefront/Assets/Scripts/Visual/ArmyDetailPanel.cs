// ============================================================================
// FILE: Visual/ArmyDetailPanel.cs
// PURPOSE: Center modal for army inspection — composition, commander, stamina
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class ArmyDetailPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnMoveRequested;
        public event Action<Guid> OnAttackRequested;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private Guid? currentArmyID;

        // Cached references for incremental updates
        private Image staminaFillImage;
        private Text staminaLabel;
        private Image entrenchFillImage;
        private Text entrenchTimeLabel;

        // Structural fingerprint
        private Guid cachedArmyID;
        private int cachedTotalUnits;
        private bool cachedIsInCombat;
        private bool cachedIsEntrenched;
        private bool cachedIsEntrenching;
        private bool cachedIsRetreating;
        private bool cachedHasPath;
        private Guid? cachedCommanderID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "ArmyDetailBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            backdropCG = backdrop.AddComponent<CanvasGroup>();
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Main panel — centered 380x450
            panel = UIHelper.CreatePanel(backdrop.transform, "ArmyDetailPanel", UIHelper.PanelParchmentBg);
            var rt = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalSmallW, UIConstants.ModalMediumH);
            PopupTendrilDecorator.Attach(rt);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "ArmyScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Close);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 42);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(Guid armyID, GameState gameState)
        {
            currentArmyID = armyID;
            Rebuild(gameState);
            backdrop.SetActive(true);
            FadeIn();
        }

        public void Close()
        {
            currentArmyID = null;
            FadeOut();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentArmyID.HasValue || !backdrop.activeSelf) return;
            var army = gameState.GetArmy(currentArmyID.Value);
            if (army == null) { Close(); return; }

            // Check fingerprint — if structure unchanged, do incremental update only
            if (FingerprintMatches(army))
            {
                IncrementalUpdate(army, gameState);
                return;
            }
            Rebuild(gameState);
        }

        // ================================================================
        // Fingerprint & Incremental Update
        // ================================================================

        private bool FingerprintMatches(ArmyData army)
        {
            return army.id == cachedArmyID
                && army.GetTotalUnits() == cachedTotalUnits
                && army.isInCombat == cachedIsInCombat
                && army.isEntrenched == cachedIsEntrenched
                && army.isEntrenching == cachedIsEntrenching
                && army.isRetreating == cachedIsRetreating
                && (army.currentPath != null && army.currentPath.Count > 0) == cachedHasPath
                && army.commanderID == cachedCommanderID;
        }

        private void CacheFingerprint(ArmyData army)
        {
            cachedArmyID = army.id;
            cachedTotalUnits = army.GetTotalUnits();
            cachedIsInCombat = army.isInCombat;
            cachedIsEntrenched = army.isEntrenched;
            cachedIsEntrenching = army.isEntrenching;
            cachedIsRetreating = army.isRetreating;
            cachedHasPath = army.currentPath != null && army.currentPath.Count > 0;
            cachedCommanderID = army.commanderID;
        }

        private void IncrementalUpdate(ArmyData army, GameState gameState)
        {
            // Update stamina bar fill
            if (staminaFillImage != null && army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    float pct = (float)(commander.stamina / CommanderData.MaxStamina);
                    var fillRT = staminaFillImage.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);

                    if (staminaLabel != null)
                        staminaLabel.text = $"Stamina: {(int)commander.stamina}/{(int)CommanderData.MaxStamina}";
                }
            }

            // Update entrenchment progress bar
            if (entrenchFillImage != null && army.isEntrenching && army.entrenchmentStartTime.HasValue)
            {
                double elapsed = gameState.currentTime - army.entrenchmentStartTime.Value;
                double buildTime = GameConfig.Entrenchment.BuildTime;
                float progress = Mathf.Clamp01((float)(elapsed / buildTime));
                var fillRT = entrenchFillImage.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(progress, 1);

                if (entrenchTimeLabel != null)
                {
                    double remaining = buildTime - elapsed;
                    entrenchTimeLabel.text = $"Entrenching: {UIHelper.FormatTime(remaining)}";
                }
            }
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentArmyID.HasValue) return;
            var army = gameState.GetArmy(currentArmyID.Value);
            if (army == null) { Close(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);
            staminaFillImage = null;
            staminaLabel = null;
            entrenchFillImage = null;
            entrenchTimeLabel = null;

            bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;

            // Header: Army name + status
            string status = army.isEntrenched ? "Entrenched" :
                            army.isEntrenching ? "Entrenching" :
                            army.isInCombat ? "In Combat" :
                            army.isRetreating ? "Retreating" :
                            army.currentPath != null && army.currentPath.Count > 0 ? "Moving" :
                            "Idle";

            var header = UIHelper.CreateLabel(contentRT, army.name,
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            var statusLabel = UIHelper.CreateLabel(contentRT, status, UIConstants.FontCaption,
                UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 20;

            // Entrenchment progress bar
            if (army.isEntrenching && army.entrenchmentStartTime.HasValue)
            {
                double currentTime = gameState.currentTime;
                double elapsed = currentTime - army.entrenchmentStartTime.Value;
                double buildTime = GameConfig.Entrenchment.BuildTime;
                float progress = Mathf.Clamp01((float)(elapsed / buildTime));
                double remaining = buildTime - elapsed;

                var (bg, fill) = UIHelper.CreateInkProgressBar(contentRT, 14f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(progress, 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.preferredHeight = 14;
                entrenchFillImage = fill;

                var timeLabel = UIHelper.CreateLabel(contentRT,
                    $"Entrenching: {UIHelper.FormatTime(remaining)}", UIConstants.FontCaption,
                    SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
                var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
                timeLE.preferredHeight = 18;
                entrenchTimeLabel = timeLabel;
            }

            if (army.isEntrenched)
            {
                int defBonus = (int)(GameConfig.Entrenchment.DefenseBonus * 100);
                int covered = army.entrenchedCoveredTiles != null ? army.entrenchedCoveredTiles.Count : 0;
                var entrenchLabel = UIHelper.CreateLabel(contentRT,
                    $"Entrenched: +{defBonus}% Defense | Covering {covered} tiles",
                    UIConstants.FontCaption, SporefrontColors.SporeTeal, TextAnchor.MiddleCenter);
                var entrenchLE = entrenchLabel.gameObject.AddComponent<LayoutElement>();
                entrenchLE.preferredHeight = 20;
            }

            UIHelper.CreateDivider(contentRT);

            // Composition
            BuildCompositionSection(army);
            UIHelper.CreateDivider(contentRT);

            // Commander
            if (army.commanderID.HasValue)
            {
                var commander = gameState.GetCommander(army.commanderID.Value);
                if (commander != null)
                {
                    BuildCommanderSection(commander);
                    UIHelper.CreateDivider(contentRT);
                }
            }

            // Stamina bar
            BuildStaminaBar(army, gameState);
            UIHelper.CreateDivider(contentRT);

            // Pending reinforcements
            if (isOwned && army.pendingReinforcements.Count > 0)
            {
                BuildReinforcementsSection(army);
                UIHelper.CreateDivider(contentRT);
            }

            // Actions (owned armies only)
            if (isOwned)
                BuildActionsSection(army, gameState);

            CacheFingerprint(army);
        }

        // ================================================================
        // Composition
        // ================================================================

        private void BuildCompositionSection(ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Composition",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            if (army.militaryComposition != null)
            {
                foreach (var kvp in army.militaryComposition)
                {
                    if (kvp.Value <= 0) continue;
                    var label = UIHelper.CreateLabel(contentRT,
                        $"  {kvp.Key.DisplayName()}: {kvp.Value}", UIConstants.FontCaption);
                    var le = label.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 20;
                }
            }

            var totalLabel = UIHelper.CreateLabel(contentRT,
                $"  Total: {army.GetTotalUnits()} units", UIConstants.FontCaption,
                UIHelper.InkMutedText);
            var totalLE = totalLabel.gameObject.AddComponent<LayoutElement>();
            totalLE.preferredHeight = 20;
        }

        // ================================================================
        // Commander
        // ================================================================

        private void BuildCommanderSection(CommanderData commander)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Commander",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var nameLabel = UIHelper.CreateLabel(contentRT,
                $"  {commander.name} — {commander.rank}", UIConstants.FontCaption);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 20;

            var specialtyLabel = UIHelper.CreateLabel(contentRT,
                $"  Specialty: {commander.specialty}", UIConstants.FontCaption, UIHelper.InkMutedText);
            var specLE = specialtyLabel.gameObject.AddComponent<LayoutElement>();
            specLE.preferredHeight = 20;

            // Stats
            var statsRow = UIHelper.CreateHorizontalRow(contentRT, 20f, 6f);

            CreateStatLabel(statsRow.transform, "Ldr", commander.Leadership);
            CreateStatLabel(statsRow.transform, "Tac", commander.Tactics);
            CreateStatLabel(statsRow.transform, "Log", commander.Logistics);
            CreateStatLabel(statsRow.transform, "Rat", commander.Rationing);
            CreateStatLabel(statsRow.transform, "End", commander.Endurance);
        }

        private void CreateStatLabel(Transform parent, string abbrev, int value)
        {
            var label = UIHelper.CreateLabel(parent, $"{abbrev}:{value}", UIConstants.FontCaption,
                UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 50;
        }

        // ================================================================
        // Stamina
        // ================================================================

        private void BuildStaminaBar(ArmyData army, GameState gameState)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 20f, 4f);

            CommanderData commander = null;
            if (army.commanderID.HasValue)
                commander = gameState.GetCommander(army.commanderID.Value);

            if (commander != null)
            {
                var label = UIHelper.CreateLabel(row.transform,
                    $"Stamina: {(int)commander.stamina}/{(int)CommanderData.MaxStamina}", UIConstants.FontCaption);
                var labelLE = label.gameObject.AddComponent<LayoutElement>();
                labelLE.preferredWidth = 130;
                staminaLabel = label;

                var (bg, fill) = UIHelper.CreateInkProgressBar(row.transform, 14f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
                float pct = (float)(commander.stamina / CommanderData.MaxStamina);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
                var barLE = bg.gameObject.AddComponent<LayoutElement>();
                barLE.flexibleWidth = 1;
                barLE.preferredHeight = 14;
                staminaFillImage = fill;
            }
            else
            {
                var label = UIHelper.CreateLabel(row.transform,
                    "No Commander", UIConstants.FontCaption, UIHelper.InkMutedText);
                var labelLE = label.gameObject.AddComponent<LayoutElement>();
                labelLE.flexibleWidth = 1;
            }
        }

        // ================================================================
        // Reinforcements
        // ================================================================

        private void BuildReinforcementsSection(ArmyData army)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Pending Reinforcements",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            foreach (var reinforcement in army.pendingReinforcements)
            {
                var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

                var infoLabel = UIHelper.CreateLabel(row.transform,
                    $"{reinforcement.GetTotalUnits()} units en route", UIConstants.FontCaption);
                var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
                infoLE.flexibleWidth = 1;

                var capturedID = reinforcement.reinforcementID;
                var cancelBtn = UIHelper.CreateButton(row.transform, "Cancel",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                    {
                        var cmd = new CancelReinforcementCommand(localPlayerID, capturedID);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
                cancelLE.preferredWidth = 60;
                cancelLE.preferredHeight = 28;
            }
        }

        // ================================================================
        // Actions
        // ================================================================

        private void BuildActionsSection(ArmyData army, GameState gameState)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 32f, 8f);

            // Move
            if (!army.isInCombat)
            {
                var moveBtn = UIHelper.CreateButton(row.transform, "Move",
                    SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontCaption, () =>
                    {
                        Close();
                        OnMoveRequested?.Invoke(army.id);
                    });
                var moveLE = moveBtn.gameObject.AddComponent<LayoutElement>();
                moveLE.preferredWidth = 80;
                moveLE.preferredHeight = 32;
            }

            // Entrench
            if (!army.isEntrenched && !army.isEntrenching && !army.isInCombat)
            {
                var entrenchBtn = UIHelper.CreateButton(row.transform, "Entrench",
                    SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontCaption, () =>
                    {
                        var cmd = new EntrenchCommand(localPlayerID, army.id);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var eLE = entrenchBtn.gameObject.AddComponent<LayoutElement>();
                eLE.preferredWidth = 80;
                eLE.preferredHeight = 32;
            }

            // Garrison — available when army is at an owned building
            if (!army.isInCombat)
            {
                var building = gameState.GetBuilding(army.coordinate);
                if (building != null && building.ownerID.HasValue &&
                    building.ownerID.Value == localPlayerID && building.IsOperational)
                {
                    var capturedBuildingID = building.id;
                    var capturedArmyID = army.id;
                    var garrisonBtn = UIHelper.CreateButton(row.transform, "Garrison",
                        SporefrontColors.SporeTeal, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                        {
                            var cmd = new GarrisonArmyCommand(localPlayerID, capturedArmyID, capturedBuildingID);
                            GameEngine.Instance.ExecuteCommand(cmd);
                            Close();
                        });
                    var gLE = garrisonBtn.gameObject.AddComponent<LayoutElement>();
                    gLE.preferredWidth = 80;
                    gLE.preferredHeight = 32;
                }
            }

            // Retreat
            if (!army.isRetreating)
            {
                var retreatBtn = UIHelper.CreateButton(row.transform, "Retreat",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                    {
                        var cmd = new RetreatCommand(localPlayerID, army.id);
                        GameEngine.Instance.ExecuteCommand(cmd);
                    });
                var rLE = retreatBtn.gameObject.AddComponent<LayoutElement>();
                rLE.preferredWidth = 80;
                rLE.preferredHeight = 32;
            }

            // Attack
            if (!army.isInCombat && !army.isRetreating)
            {
                var attackBtn = UIHelper.CreateButton(row.transform, "Attack",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                    {
                        Close();
                        OnAttackRequested?.Invoke(army.id);
                    });
                var aLE = attackBtn.gameObject.AddComponent<LayoutElement>();
                aLE.preferredWidth = 80;
                aLE.preferredHeight = 32;
            }
        }
    }
}
