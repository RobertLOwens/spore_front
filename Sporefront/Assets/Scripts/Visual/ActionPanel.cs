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
            buildBackdrop.SetActive(false);
            targetBanner.SetActive(false);
            OnCancelled?.Invoke();
        }

        // ================================================================
        // Target Mode Resolution
        // ================================================================

        /// <summary>
        /// Returns true if the click was consumed by an active action mode.
        /// </summary>
        public bool HandleTargetClick(HexCoordinate coord)
        {
            if (CurrentMode == ActionMode.MoveTarget)
            {
                var cmd = new MoveCommand(localPlayerID, moveEntityID, coord, moveIsArmy);
                GameEngine.Instance.ExecuteCommand(cmd);
                Cancel();
                return true;
            }

            if (CurrentMode == ActionMode.AttackTarget)
            {
                var cmd = new AttackCommand(localPlayerID, attackArmyID, coord);
                GameEngine.Instance.ExecuteCommand(cmd);
                Cancel();
                return true;
            }

            return false;
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

            // Scroll area for building list
            var scroll = UIHelper.CreateScrollView(buildPanel.transform, "BuildScroll", out buildContentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 48); // Space for close button
            scrollRT.offsetMax = new Vector2(0, -44); // Space for header

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

            // Card background — lighter for available, muted for unavailable
            var cardBg = available
                ? new Color(SporefrontColors.ParchmentLight.r, SporefrontColors.ParchmentLight.g,
                    SporefrontColors.ParchmentLight.b, 0.6f)
                : new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g,
                    SporefrontColors.ParchmentDark.b, 0.35f);

            var card = UIHelper.CreatePanel(parent, bt.ToString(), cardBg, UIHelper.SmallCornerRadius);
            var cardLE = card.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 72;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Row 1: Name + CC level badge
            var nameRow = UIHelper.CreateHorizontalRow(card.transform, 22f, 4f);
            var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                bt.DisplayName(), UIConstants.FontBody,
                available ? UIHelper.HeaderTextColor : SporefrontColors.InkFaded,
                TextAnchor.MiddleLeft, false);
            nameLabel.fontStyle = FontStyle.Bold;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

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
                canAfford ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
            costLabel.supportRichText = true;
            var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
            costLE.preferredHeight = 18;

            // Row 3: Rotate + Build buttons
            var btnRow = UIHelper.CreateHorizontalRow(card.transform, 26f, 4f);

            // Spacer to push buttons right
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(btnRow.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1;

            // Rotation control for multi-hex buildings (#19)
            if (bt.HexSize() > 1 || bt.RequiresRotation())
            {
                var rotateBtn = UIHelper.CreateButton(btnRow.transform, "Rotate",
                    SporefrontColors.ParchmentDark, UIHelper.ButtonText, UIConstants.FontCaption, null);
                var rotBtnLE = rotateBtn.gameObject.AddComponent<LayoutElement>();
                rotBtnLE.preferredWidth = 52;

                var capturedType = bt;
                rotateBtn.onClick.AddListener(() =>
                {
                    buildRotation = (buildRotation + 1) % 6;
                    UpdateBuildPreview(capturedType);
                });
            }

            // Build button
            var buildBtn = UIHelper.CreateButton(btnRow.transform, "Build",
                available ? SporefrontColors.SporeGreen : SporefrontColors.InkFaded,
                available ? UIHelper.HudTextColor : SporefrontColors.InkLight,
                UIConstants.FontSmall, null);
            buildBtn.interactable = available;
            var buildBtnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
            buildBtnLE.preferredWidth = 60;

            if (available && buildCoord.HasValue)
            {
                var capturedType = bt;
                var capturedCoord = buildCoord.Value;
                var capturedRotation = buildRotation;
                buildBtn.onClick.AddListener(() =>
                {
                    OnBuildTypeSelected?.Invoke(capturedType, capturedCoord, capturedRotation);
                    Cancel();
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

        private void CreateTargetBanner(Transform canvasTransform)
        {
            targetBanner = UIHelper.CreatePanel(canvasTransform, "TargetBanner", UIHelper.HudBg);
            var rt = targetBanner.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 1);
            rt.anchorMax = new Vector2(0.7f, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -80);
            rt.offsetMax = new Vector2(0, -44);

            var row = UIHelper.CreateHorizontalRow(targetBanner.transform, 36f, 8f);
            var rowRT = row.GetComponent<RectTransform>();
            UIHelper.StretchFull(rowRT);
            row.padding = new RectOffset(12, 12, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;

            var label = UIHelper.CreateLabel(row.transform, "Click destination", 14,
                UIHelper.HudTextColor, TextAnchor.MiddleCenter);
            label.gameObject.name = "BannerText";
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            var cancelBtn = UIHelper.CreateButton(row.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12,
                () => Cancel());
            var btnLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 60;

            targetBanner.SetActive(false);
        }

        private void SetTargetBanner(string message)
        {
            var label = targetBanner.GetComponentInChildren<Text>();
            if (label != null) label.text = message;
        }

    }
}
