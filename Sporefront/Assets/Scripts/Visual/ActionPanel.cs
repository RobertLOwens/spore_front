// ============================================================================
// FILE: Visual/ActionPanel.cs
// PURPOSE: Build menu with castle/fort rotation (#19) + move/attack target modes
//          Centered modal with Economic/Military category grouping
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
    public class ActionPanel : MonoBehaviour
    {
        // ================================================================
        // Types
        // ================================================================

        public enum ActionMode
        {
            None,
            BuildSelect,
            MoveTarget,
            AttackTarget
        }

        // ================================================================
        // Events
        // ================================================================

        public event Action OnCancelled;
        public event Action<HexCoordinate, List<HexCoordinate>> OnBuildPreviewChanged;
        public event Action<BuildingType, HexCoordinate, int> OnBuildTypeSelected;

        // ================================================================
        // State
        // ================================================================

        private GameObject buildBackdrop;
        private GameObject buildPanel;
        private GameObject targetBanner;
        private RectTransform buildContentRT;

        public ActionMode CurrentMode { get; private set; }

        private HexCoordinate? buildCoord;
        private BuildingType selectedBuildingType;
        private int buildRotation;
        private Guid moveEntityID;
        private bool moveIsArmy;
        private Guid attackArmyID;
        private Guid localPlayerID;
        private Text rotationLabel;

        // Entrenchment confirmation state
        private bool showingEntrenchConfirm;
        private HexCoordinate pendingConfirmTarget;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;
            CreateBuildPanel(canvasTransform);
            CreateTargetBanner(canvasTransform);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Build Mode (#19)
        // ================================================================

        public void ShowBuildMenu(HexCoordinate coord, GameState gameState)
        {
            buildCoord = coord;
            CurrentMode = ActionMode.BuildSelect;
            buildRotation = 0;
            if (rotationLabel != null) rotationLabel.text = "Rotation: 0";
            RebuildBuildMenu(gameState);
            buildBackdrop.SetActive(true);
            targetBanner.SetActive(false);
        }

        public void ShowMoveTarget(Guid entityID, bool isArmy)
        {
            CurrentMode = ActionMode.MoveTarget;
            moveEntityID = entityID;
            moveIsArmy = isArmy;

            buildBackdrop.SetActive(false);
            SetTargetBanner(isArmy ? "Click destination for army" : "Click destination for villagers");
            targetBanner.SetActive(true);
        }

        public void ShowAttackTarget(Guid armyID)
        {
            CurrentMode = ActionMode.AttackTarget;
            attackArmyID = armyID;

            buildBackdrop.SetActive(false);
            SetTargetBanner("Click target to attack");
            targetBanner.SetActive(true);
        }

        public void Cancel()
        {
            CurrentMode = ActionMode.None;
            showingEntrenchConfirm = false;
            buildBackdrop.SetActive(false);
            targetBanner.SetActive(false);
            OnCancelled?.Invoke();
        }

        /// <summary>
        /// Hides the build menu UI without changing CurrentMode or firing OnCancelled.
        /// Used when transitioning to the builder select panel so the build preview stays visible.
        /// </summary>
        public void HideBuildMenu()
        {
            buildBackdrop.SetActive(false);
        }

        // ================================================================
        // Target Mode Resolution
        // ================================================================

        /// <summary>
        /// Returns true if the click was consumed by an active action mode.
        /// </summary>
        public bool HandleTargetClick(HexCoordinate coord)
        {
            // If already showing entrench confirmation, ignore map clicks
            if (showingEntrenchConfirm)
                return true;

            if (CurrentMode == ActionMode.MoveTarget)
            {
                // Check if army is entrenched — warn before proceeding
                if (moveIsArmy && IsEntityEntrenched(moveEntityID))
                {
                    pendingConfirmTarget = coord;
                    ShowEntrenchConfirmation(true);
                    return true;
                }

                var cmd = new MoveCommand(localPlayerID, moveEntityID, coord, moveIsArmy);
                UIManager.ExecutePlayerCommand(cmd);
                Cancel();
                return true;
            }

            if (CurrentMode == ActionMode.AttackTarget)
            {
                // Check if army is entrenched — warn before proceeding
                if (IsEntityEntrenched(attackArmyID))
                {
                    pendingConfirmTarget = coord;
                    ShowEntrenchConfirmation(false);
                    return true;
                }

                var cmd = new AttackCommand(localPlayerID, attackArmyID, coord);
                UIManager.ExecutePlayerCommand(cmd);
                Cancel();
                return true;
            }

            return false;
        }

        private bool IsEntityEntrenched(Guid entityID)
        {
            var army = GameEngine.Instance.gameState?.GetArmy(entityID);
            return army != null && (army.isEntrenched || army.isEntrenching);
        }

        private void ShowEntrenchConfirmation(bool isMove)
        {
            showingEntrenchConfirm = true;
            // Rebuild the banner with confirmation UI
            RebuildTargetBanner(isMove);
        }

        private void ConfirmEntrenchAction()
        {
            showingEntrenchConfirm = false;

            if (CurrentMode == ActionMode.MoveTarget)
            {
                var cmd = new MoveCommand(localPlayerID, moveEntityID, pendingConfirmTarget, moveIsArmy);
                UIManager.ExecutePlayerCommand(cmd);
            }
            else if (CurrentMode == ActionMode.AttackTarget)
            {
                var cmd = new AttackCommand(localPlayerID, attackArmyID, pendingConfirmTarget);
                UIManager.ExecutePlayerCommand(cmd);
            }

            Cancel();
        }

        private void CancelEntrenchConfirm()
        {
            showingEntrenchConfirm = false;
            // Restore original target banner
            string msg = CurrentMode == ActionMode.MoveTarget
                ? (moveIsArmy ? "Click destination for army" : "Click destination for villagers")
                : "Click target to attack";
            RebuildTargetBannerDefault(msg);
        }

        public bool IsActive => CurrentMode != ActionMode.None;

        // ================================================================
        // Build Menu UI
        // ================================================================

        private void CreateBuildPanel(Transform canvasTransform)
        {
            // Full-screen backdrop with click-to-dismiss
            buildBackdrop = UIHelper.CreatePanel(canvasTransform, "BuildBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = buildBackdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = buildBackdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Cancel);

            // Centered modal panel
            buildPanel = UIHelper.CreatePanel(buildBackdrop.transform, "BuildPanel", UIHelper.PanelBg);
            var rt = buildPanel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalBuildMenuW, UIConstants.ModalBuildMenuH);

            // Header
            var header = UIHelper.CreateLabel(buildPanel.transform, "Build",
                UIConstants.FontTitle, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.offsetMin = new Vector2(12, -40);
            headerRT.offsetMax = new Vector2(-12, -6);

            // Rotation label (between header and scroll)
            rotationLabel = UIHelper.CreateLabel(buildPanel.transform, "Rotation: 0",
                UIConstants.FontSmall, SporefrontColors.ParchmentShadow, TextAnchor.MiddleCenter);
            var rotLabelRT = rotationLabel.GetComponent<RectTransform>();
            rotLabelRT.anchorMin = new Vector2(0, 1);
            rotLabelRT.anchorMax = new Vector2(1, 1);
            rotLabelRT.pivot = new Vector2(0.5f, 1);
            rotLabelRT.offsetMin = new Vector2(12, -58);
            rotLabelRT.offsetMax = new Vector2(-12, -42);

            // Scroll area for building list
            var scroll = UIHelper.CreateScrollView(buildPanel.transform, "BuildScroll", out buildContentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 48); // Space for close button
            scrollRT.offsetMax = new Vector2(0, -60); // Space for header + rotation label

            // Close button at bottom
            var closeBtn = UIHelper.CreateButton(buildPanel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontBody, Cancel);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 46);

            buildBackdrop.SetActive(false);
        }

        private void RebuildBuildMenu(GameState gameState)
        {
            // Clear
            for (int i = buildContentRT.childCount - 1; i >= 0; i--)
                Destroy(buildContentRT.GetChild(i).gameObject);

            // Adjust scroll content spacing
            var contentVLG = buildContentRT.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null)
            {
                contentVLG.spacing = UIConstants.SectionCardSpacing;
                contentVLG.padding = new RectOffset(12, 12, 8, 8);
            }

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            int currentCC = gameState.GetCityCenterLevel(localPlayerID);

            // Partition building types by category
            var economicTypes = new List<BuildingType>();
            var militaryTypes = new List<BuildingType>();
            var buildingTypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));
            foreach (var bt in buildingTypes)
            {
                if (bt == BuildingType.Road) continue;
                if (bt.Category() == BuildingCategory.Economic)
                    economicTypes.Add(bt);
                else
                    militaryTypes.Add(bt);
            }

            // Economic section
            if (economicTypes.Count > 0)
            {
                var econCard = UIHelper.CreateSectionCard(buildContentRT, "EconomicCard", "Economic");
                foreach (var bt in economicTypes)
                    BuildBuildingEntry(econCard.transform, bt, player, currentCC);
            }

            // Military section
            if (militaryTypes.Count > 0)
            {
                var milCard = UIHelper.CreateSectionCard(buildContentRT, "MilitaryCard", "Military");
                foreach (var bt in militaryTypes)
                    BuildBuildingEntry(milCard.transform, bt, player, currentCC);
            }
        }

        private void BuildBuildingEntry(Transform parent, BuildingType bt, PlayerState player, int currentCC)
        {
            var cost = bt.BuildCost();
            bool canAfford = player.CanAfford(cost);
            int requiredCC = bt.RequiredCityCenterLevel();
            bool meetsLevel = currentCC >= requiredCC;
            bool available = canAfford && meetsLevel;

            // Card background — subtle light tint for available, dimmer for unavailable
            var cardBg = available
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(1f, 1f, 1f, 0.03f);

            var card = UIHelper.CreatePanel(parent, bt.ToString(), cardBg, UIHelper.SmallCornerRadius);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 100;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Row 1: Name + CC level badge
            var nameRow = UIHelper.CreateHorizontalRow(card.transform, 28f, 4f);
            var nameRowLE = nameRow.gameObject.AddComponent<LayoutElement>();
            nameRowLE.preferredHeight = 28;
            var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                bt.DisplayName(), UIConstants.FontBody,
                available ? UIHelper.HeaderTextColor : SporefrontColors.ParchmentShadow,
                TextAnchor.MiddleLeft, false);
            nameLabel.fontStyle = FontStyle.Bold;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 28;

            if (requiredCC > 1)
            {
                var badgeColor = meetsLevel ? SporefrontColors.SporeTeal : SporefrontColors.SporeRed;
                var badge = UIHelper.CreateLabel(nameRow.transform,
                    $"CC {requiredCC}", UIConstants.FontCaption, badgeColor);
                badge.fontStyle = FontStyle.Bold;
                var badgeLE = badge.gameObject.AddComponent<LayoutElement>();
                badgeLE.preferredWidth = 40;
            }

            // Row 2: Cost
            string costStr = UIHelper.FormatCost(cost);
            var costLabel = UIHelper.CreateLabel(card.transform, costStr, UIConstants.FontSmall,
                canAfford ? SporefrontColors.ParchmentShadow : SporefrontColors.SporeRed);
            costLabel.supportRichText = true;
            var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
            costLE.preferredHeight = 20;

            // Row 3: Rotate + Build buttons
            var btnRow = UIHelper.CreateHorizontalRow(card.transform, 32f, 4f);
            var btnRowLE = btnRow.gameObject.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 32;

            // Spacer to push buttons right
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(btnRow.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1;

            // Rotation control for multi-hex buildings (#19)
            if (bt.HexSize() > 1 || bt.RequiresRotation())
            {
                var rotateBtn = UIHelper.CreateButton(btnRow.transform, "Rotate",
                    SporefrontColors.BgSurface, UIHelper.ButtonText, UIConstants.FontCaption, null);
                var rotBtnLE = rotateBtn.gameObject.AddComponent<LayoutElement>();
                rotBtnLE.preferredWidth = 62;

                var capturedType = bt;
                rotateBtn.onClick.AddListener(() =>
                {
                    buildRotation = (buildRotation + 1) % 6;
                    if (rotationLabel != null) rotationLabel.text = $"Rotation: {buildRotation}";
                    UpdateBuildPreview(capturedType);
                });
            }

            // Build button
            var buildBtn = UIHelper.CreateButton(btnRow.transform, "Build",
                available ? SporefrontColors.SporeGreen : SporefrontColors.ParchmentShadow,
                available ? UIHelper.HudTextColor : SporefrontColors.ParchmentShadow,
                UIConstants.FontSmall, null);
            buildBtn.interactable = available;
            var buildBtnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
            buildBtnLE.preferredWidth = 72;

            if (available && buildCoord.HasValue)
            {
                var capturedType = bt;
                var capturedCoord = buildCoord.Value;
                var capturedRotation = buildRotation;
                buildBtn.onClick.AddListener(() =>
                {
                    OnBuildTypeSelected?.Invoke(capturedType, capturedCoord, capturedRotation);
                    HideBuildMenu();
                });
            }
        }

        private void UpdateBuildPreview(BuildingType bt)
        {
            if (!buildCoord.HasValue) return;
            var occupied = bt.GetOccupiedCoordinates(buildCoord.Value, buildRotation);
            OnBuildPreviewChanged?.Invoke(buildCoord.Value, occupied);
        }

        // ================================================================
        // Target Banner UI
        // ================================================================

        private Transform bannerParent;

        private void CreateTargetBanner(Transform canvasTransform)
        {
            bannerParent = canvasTransform;

            targetBanner = UIHelper.CreatePanel(canvasTransform, "TargetBanner", UIHelper.HudBg);
            var rt = targetBanner.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 1);
            rt.anchorMax = new Vector2(0.7f, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -80);
            rt.offsetMax = new Vector2(0, -44);

            BuildDefaultBannerContent("Click destination");

            targetBanner.SetActive(false);
        }

        private void ClearBannerContent()
        {
            for (int i = targetBanner.transform.childCount - 1; i >= 0; i--)
                Destroy(targetBanner.transform.GetChild(i).gameObject);

            // Remove any layout group added by confirmation mode
            var vlg = targetBanner.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Destroy(vlg);
        }

        private void BuildDefaultBannerContent(string message)
        {
            ClearBannerContent();

            var rt = targetBanner.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(0, -80);
            rt.offsetMax = new Vector2(0, -44);
            targetBanner.GetComponent<Image>().color = UIHelper.HudBg;

            var row = UIHelper.CreateHorizontalRow(targetBanner.transform, 36f, 8f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(12, 12, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;

            var label = UIHelper.CreateLabel(row.transform, message, 14,
                UIHelper.HudTextColor, TextAnchor.MiddleCenter);
            label.gameObject.name = "BannerText";
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            var cancelBtn = UIHelper.CreateButton(row.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption,
                () => Cancel());
            var btnLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 60;
        }

        private void RebuildTargetBannerDefault(string message)
        {
            BuildDefaultBannerContent(message);
        }

        private void RebuildTargetBanner(bool isMove)
        {
            ClearBannerContent();

            // Expand banner for confirmation content
            var rt = targetBanner.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(0, -120);
            rt.offsetMax = new Vector2(0, -44);
            targetBanner.GetComponent<Image>().color =
                Color.Lerp(UIHelper.HudBg, SporefrontColors.SporeAmber, 0.15f);

            var vlg = targetBanner.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 6, 6);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Warning text
            string action = isMove ? "Moving" : "Attacking";
            var warnLabel = UIHelper.CreateLabel(targetBanner.transform,
                $"{action} will abandon your entrenched position!",
                13, SporefrontColors.SporeAmber, TextAnchor.MiddleCenter, true);
            var warnLE = warnLabel.gameObject.AddComponent<LayoutElement>();
            warnLE.preferredHeight = 24;

            // Buttons row
            var btnRow = UIHelper.CreateHorizontalRow(targetBanner.transform, 32f, 8f);
            btnRow.childAlignment = TextAnchor.MiddleCenter;

            var confirmBtn = UIHelper.CreateButton(btnRow.transform, "Confirm",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption,
                () => ConfirmEntrenchAction());
            var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
            confirmLE.preferredWidth = 80;
            confirmLE.preferredHeight = 30;

            var cancelBtn = UIHelper.CreateButton(btnRow.transform, "Cancel",
                SporefrontColors.BgSurface, UIHelper.ButtonText, UIConstants.FontCaption,
                () => CancelEntrenchConfirm());
            var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            cancelLE.preferredWidth = 80;
            cancelLE.preferredHeight = 30;
        }

        private void SetTargetBanner(string message)
        {
            BuildDefaultBannerContent(message);
        }

    }
}
