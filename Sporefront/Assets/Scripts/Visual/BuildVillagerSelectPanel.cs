// ============================================================================
// FILE: Visual/BuildVillagerSelectPanel.cs
// PURPOSE: Centered modal panel for selecting a villager group to dispatch
//          as builder. Shows all villager groups sorted by distance with
//          walking time estimates and current task info.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class BuildVillagerSelectPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, BuildingType, HexCoordinate, int> OnVillagerSelected; // vgID, type, coord, rotation
        public event Action OnClose;
        public event Action OnBack;
        public event Action<Guid, HexCoordinate> OnPreviewPathRequested; // villagerGroupID, buildCoordinate
        public event Action OnPreviewPathCleared;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject modalPanel;
        private RectTransform contentRT;
        private Text infoLabel;
        private Guid localPlayerID;

        private BuildingType pendingBuildingType;
        private HexCoordinate pendingCoordinate;
        private int pendingRotation;
        private Guid? previewedVillagerID;
        private GameState cachedGameState;

        // Walking time estimate: distance / (BaseSpeed * VillagerSpeedMultiplier) seconds
        private const double WalkSecondsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * GameConfig.Movement.VillagerSpeedMultiplier);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Full-screen backdrop with click-to-dismiss
            backdrop = UIHelper.CreatePanel(canvasTransform, "BuildVillagerBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Centered modal panel
            modalPanel = UIHelper.CreatePanel(backdrop.transform, "BuildVillagerModal", UIHelper.PanelParchmentBg);
            var rt = modalPanel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalSmallW, UIConstants.ModalSmallH);
            PopupTendrilDecorator.Attach(rt);

            // Header "Select Builder" — fixed at top
            var header = UIHelper.CreateLabel(modalPanel.transform, "Select Builder",
                UIConstants.FontTitle, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.offsetMin = new Vector2(12, -40);
            headerRT.offsetMax = new Vector2(-12, -6);

            // Building info label — fixed below header
            infoLabel = UIHelper.CreateLabel(modalPanel.transform, "",
                UIConstants.FontSmall, UIHelper.InkMutedText);
            var infoRT = infoLabel.GetComponent<RectTransform>();
            infoRT.anchorMin = new Vector2(0, 1);
            infoRT.anchorMax = new Vector2(1, 1);
            infoRT.pivot = new Vector2(0.5f, 1);
            infoRT.offsetMin = new Vector2(12, -62);
            infoRT.offsetMax = new Vector2(-12, -42);

            // Scroll area for villager list
            var scroll = UIHelper.CreateScrollView(modalPanel.transform, "BuildVillagerScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 52); // Space for back button
            scrollRT.offsetMax = new Vector2(0, -64); // Space for header + info label

            // Back button at bottom
            var backBtn = UIHelper.CreateButton(modalPanel.transform, "Back",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                OnBackClicked);
            var backBtnRT = backBtn.GetComponent<RectTransform>();
            backBtnRT.anchorMin = new Vector2(0, 0);
            backBtnRT.anchorMax = new Vector2(1, 0);
            backBtnRT.pivot = new Vector2(0.5f, 0);
            backBtnRT.offsetMin = new Vector2(12, 4);
            backBtnRT.offsetMax = new Vector2(-12, 48);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState, BuildingType buildingType, HexCoordinate coordinate, int rotation)
        {
            pendingBuildingType = buildingType;
            pendingCoordinate = coordinate;
            pendingRotation = rotation;
            previewedVillagerID = null;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            ClearPreview();
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Back Navigation
        // ================================================================

        private void OnBackClicked()
        {
            ClearPreview();
            backdrop.SetActive(false);
            OnBack?.Invoke();
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            cachedGameState = gameState;

            // Update info label
            if (infoLabel != null)
                infoLabel.text = $"  {pendingBuildingType.DisplayName()} at ({pendingCoordinate.q},{pendingCoordinate.r})";

            // Clear scroll content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Villager group list
            BuildVillagerGroupList(gameState);
        }

        // ================================================================
        // Villager Group List
        // ================================================================

        private void BuildVillagerGroupList(GameState gameState)
        {
            // Guard: if previewed villager is no longer available, reset
            if (previewedVillagerID.HasValue)
            {
                bool found = false;
                var checkGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                if (checkGroups != null)
                    foreach (var g in checkGroups)
                        if (g.id == previewedVillagerID.Value) { found = true; break; }
                if (!found) ClearPreview();
            }

            var sectionLabel = UIHelper.CreateLabel(contentRT, "Villager Groups",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var groups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            if (groups == null || groups.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "  No villager groups available", UIConstants.FontSmall, UIHelper.InkMutedText);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 24;
                return;
            }

            // Sort by distance to build site
            groups.Sort((a, b) =>
                a.coordinate.Distance(pendingCoordinate).CompareTo(
                    b.coordinate.Distance(pendingCoordinate)));

            foreach (var group in groups)
            {
                if (group.villagerCount <= 0) continue;

                int distance = group.coordinate.Distance(pendingCoordinate);
                bool isBusy = group.currentTask != null && !group.currentTask.IsIdle;
                string taskDesc = isBusy ? group.currentTask.DisplayName : "Idle";
                bool isSelected = previewedVillagerID.HasValue && previewedVillagerID.Value == group.id;

                // Walking time estimate
                int walkSeconds = distance > 0 ? Mathf.CeilToInt((float)(distance * WalkSecondsPerTile)) : 0;
                string walkTimeStr = walkSeconds > 0
                    ? (walkSeconds < 60 ? $"~{walkSeconds}s" : $"~{walkSeconds / 60}m{walkSeconds % 60}s")
                    : "On-site";

                Color rowBg = isSelected
                    ? Color.Lerp(Color.clear, SporefrontColors.SporeAmber, 0.12f)
                    : Color.clear;
                var row = UIHelper.CreatePanel(contentRT, "VillagerRow", rowBg);
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = isBusy ? 100 : 80;

                var vlg = row.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 4;
                vlg.padding = new RectOffset(10, 10, 4, 4);
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Name + count
                var nameRow = UIHelper.CreateHorizontalRow(row.transform, 26f, 4f);
                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{group.name} ({group.villagerCount})", UIConstants.FontBody,
                    isBusy ? SporefrontColors.SporeAmber : UIHelper.InkBodyText);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                var distLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{distance} tiles", UIConstants.FontSmall, UIHelper.InkMutedText);
                var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
                distLE.preferredWidth = 70;

                // Task + walk time
                var infoRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);
                var taskLabel = UIHelper.CreateLabel(infoRow.transform,
                    taskDesc, UIConstants.FontSmall,
                    isBusy ? SporefrontColors.SporeAmber : UIHelper.InkMutedText);
                var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
                taskLE.flexibleWidth = 1;

                var walkLabel = UIHelper.CreateLabel(infoRow.transform,
                    walkTimeStr, UIConstants.FontSmall, UIHelper.InkMutedText);
                var walkLE = walkLabel.gameObject.AddComponent<LayoutElement>();
                walkLE.preferredWidth = 70;

                // Action row
                var actionRow = UIHelper.CreateHorizontalRow(row.transform, 30f, 4f);

                var capturedGroupID = group.id;

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

                Color btnColor = isBusy ? SporefrontColors.SporeAmber : SporefrontColors.SporeGreen;
                var selectBtn = UIHelper.CreateButton(actionRow.transform,
                    isSelected ? "Selected" : "Select",
                    btnColor, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                    {
                        previewedVillagerID = capturedGroupID;
                        OnPreviewPathRequested?.Invoke(capturedGroupID, pendingCoordinate);
                        Rebuild(cachedGameState);
                    });
                var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 85;
                btnLE.preferredHeight = 30;
            }

            // Confirm Builder button — only when a villager is previewed
            if (previewedVillagerID.HasValue)
            {
                UIHelper.CreateDivider(contentRT);
                var capturedType = pendingBuildingType;
                var capturedCoord = pendingCoordinate;
                var capturedRotation = pendingRotation;
                var capturedVGID = previewedVillagerID.Value;
                var confirmBtn = UIHelper.CreateButton(contentRT, "Confirm Builder",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontBody, () =>
                    {
                        OnVillagerSelected?.Invoke(capturedVGID, capturedType, capturedCoord, capturedRotation);
                        ClearPreview();
                        Hide();
                    });
                var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                confirmLE.preferredHeight = 44;
            }
        }

        private void ClearPreview()
        {
            if (previewedVillagerID.HasValue)
            {
                previewedVillagerID = null;
                OnPreviewPathCleared?.Invoke();
            }
        }
    }
}
