// ============================================================================
// FILE: Visual/ActionPanel.cs
// PURPOSE: Build menu with castle/fort rotation (#19) + move/attack target modes
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

        // ================================================================
        // State
        // ================================================================

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
            buildPanel.SetActive(true);
            targetBanner.SetActive(false);
        }

        public void ShowMoveTarget(Guid entityID, bool isArmy)
        {
            CurrentMode = ActionMode.MoveTarget;
            moveEntityID = entityID;
            moveIsArmy = isArmy;

            buildPanel.SetActive(false);
            SetTargetBanner(isArmy ? "Click destination for army" : "Click destination for villagers");
            targetBanner.SetActive(true);
        }

        public void ShowAttackTarget(Guid armyID)
        {
            CurrentMode = ActionMode.AttackTarget;
            attackArmyID = armyID;

            buildPanel.SetActive(false);
            SetTargetBanner("Click target to attack");
            targetBanner.SetActive(true);
        }

        public void Cancel()
        {
            CurrentMode = ActionMode.None;
            buildPanel.SetActive(false);
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
            buildPanel = UIHelper.CreatePanel(canvasTransform, "BuildPanel", UIHelper.PanelBg);
            var rt = buildPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.3f);
            rt.anchorMax = new Vector2(0, 0.7f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(250, 0); // Right of entity list
            rt.offsetMax = new Vector2(550, 0);

            // Header
            var headerRow = UIHelper.CreateHorizontalRow(buildPanel.transform, 32f, 4f);
            var headerRT = headerRow.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 32);

            var title = UIHelper.CreateLabel(headerRow.transform, "Build",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var closeBtn = UIHelper.CreateButton(headerRow.transform, "X",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12,
                () => Cancel());
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredWidth = 28;
            closeBtnLE.preferredHeight = 28;

            // Scroll area for building list
            var scroll = UIHelper.CreateScrollView(buildPanel.transform, "BuildScroll", out buildContentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 0);
            scrollRT.offsetMax = new Vector2(0, -36);

            buildPanel.SetActive(false);
        }

        private void RebuildBuildMenu(GameState gameState)
        {
            // Clear
            for (int i = buildContentRT.childCount - 1; i >= 0; i--)
                Destroy(buildContentRT.GetChild(i).gameObject);

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            // List buildable building types
            var buildingTypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));
            foreach (var bt in buildingTypes)
            {
                if (bt == BuildingType.Road) continue; // Skip road for now

                var cost = bt.BuildCost();
                bool canAfford = player.CanAfford(cost);
                int requiredCC = bt.RequiredCityCenterLevel();
                int currentCC = gameState.GetCityCenterLevel(localPlayerID);
                bool meetsLevel = currentCC >= requiredCC;
                bool available = canAfford && meetsLevel;

                var row = UIHelper.CreatePanel(buildContentRT, bt.ToString(), Color.clear);
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 44;

                var vlg = row.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 2;
                vlg.padding = new RectOffset(4, 4, 2, 2);
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Name + cost row
                var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);

                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    bt.DisplayName(), 12, available ? UIHelper.BodyTextColor : SporefrontColors.InkFaded);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                // Cost string
                string costStr = FormatCost(cost, canAfford);
                var costLabel = UIHelper.CreateLabel(nameRow.transform, costStr, 10,
                    canAfford ? SporefrontColors.InkLight : SporefrontColors.SporeRed);
                costLabel.supportRichText = true;
                var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
                costLE.preferredWidth = 120;

                // Build button row
                var btnRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);

                // Rotation control for multi-hex buildings (#19)
                if (bt.HexSize() > 1 || bt.RequiresRotation())
                {
                    var rotateBtn = UIHelper.CreateButton(btnRow.transform, "Rot",
                        SporefrontColors.ParchmentDark, UIHelper.ButtonText, 10, null);
                    var rotBtnLE = rotateBtn.gameObject.AddComponent<LayoutElement>();
                    rotBtnLE.preferredWidth = 36;

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
                    available ? UIHelper.HudTextColor : SporefrontColors.InkLight, 11, null);
                buildBtn.interactable = available;
                var buildBtnLE = buildBtn.gameObject.AddComponent<LayoutElement>();
                buildBtnLE.preferredWidth = 50;

                if (available && buildCoord.HasValue)
                {
                    var capturedType = bt;
                    var capturedCoord = buildCoord.Value;
                    buildBtn.onClick.AddListener(() =>
                    {
                        var cmd = new BuildCommand(localPlayerID, capturedType, capturedCoord, buildRotation);
                        GameEngine.Instance.ExecuteCommand(cmd);
                        Cancel();
                    });
                }
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

        // ================================================================
        // Helpers
        // ================================================================

        private string FormatCost(Dictionary<ResourceType, int> cost, bool canAfford)
        {
            var parts = new List<string>();
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0)
                    parts.Add($"{UIHelper.ResourceIcon(kvp.Key)}{kvp.Value}");
            }
            return string.Join(" ", parts);
        }
    }
}
