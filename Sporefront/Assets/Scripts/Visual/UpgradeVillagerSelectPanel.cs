// ============================================================================
// FILE: Visual/UpgradeVillagerSelectPanel.cs
// PURPOSE: Centered modal panel for selecting a villager group to dispatch
//          as upgrader. Shows all villager groups sorted by distance with
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
    public class UpgradeVillagerSelectPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, Guid> OnVillagerSelected; // villagerGroupID, buildingID
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject modalPanel;
        private RectTransform contentRT;
        private Text infoLabel;
        private Guid localPlayerID;

        private Guid pendingBuildingID;
        private BuildingType pendingBuildingType;
        private HexCoordinate pendingCoordinate;
        private int pendingLevel;

        // Walking time estimate: distance / (BaseSpeed * VillagerSpeedMultiplier) seconds
        private const double WalkSecondsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * GameConfig.Movement.VillagerSpeedMultiplier);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Full-screen backdrop with click-to-dismiss
            backdrop = UIHelper.CreatePanel(canvasTransform, "UpgradeVillagerBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Centered modal panel
            modalPanel = UIHelper.CreatePanel(backdrop.transform, "UpgradeVillagerModal", UIHelper.PanelParchmentBg);
            var rt = modalPanel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalSmallW, UIConstants.ModalSmallH);
            PopupTendrilDecorator.Attach(rt);

            // Header "Select Upgrader" — fixed at top
            var header = UIHelper.CreateLabel(modalPanel.transform, "Select Upgrader",
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
            var scroll = UIHelper.CreateScrollView(modalPanel.transform, "UpgradeVillagerScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 52); // Space for close button
            scrollRT.offsetMax = new Vector2(0, -64); // Space for header + info label

            // Close button at bottom
            var closeBtn = UIHelper.CreateButton(modalPanel.transform, "Close",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontBody,
                Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 48);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState, Guid buildingID, BuildingType buildingType, HexCoordinate coordinate, int currentLevel)
        {
            pendingBuildingID = buildingID;
            pendingBuildingType = buildingType;
            pendingCoordinate = coordinate;
            pendingLevel = currentLevel;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
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
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            // Update info label
            int nextLevel = pendingLevel + 1;
            if (infoLabel != null)
                infoLabel.text = $"  {pendingBuildingType.DisplayName()} Lv.{pendingLevel} -> Lv.{nextLevel} at ({pendingCoordinate.q},{pendingCoordinate.r})";

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

            // Sort by distance to building
            groups.Sort((a, b) =>
                a.coordinate.Distance(pendingCoordinate).CompareTo(
                    b.coordinate.Distance(pendingCoordinate)));

            foreach (var group in groups)
            {
                if (group.villagerCount <= 0) continue;

                int distance = group.coordinate.Distance(pendingCoordinate);
                bool isBusy = group.currentTask != null && !group.currentTask.IsIdle;
                string taskDesc = isBusy ? group.currentTask.DisplayName : "Idle";

                // Walking time estimate
                int walkSeconds = distance > 0 ? Mathf.CeilToInt((float)(distance * WalkSecondsPerTile)) : 0;
                string walkTimeStr = walkSeconds > 0
                    ? (walkSeconds < 60 ? $"~{walkSeconds}s" : $"~{walkSeconds / 60}m{walkSeconds % 60}s")
                    : "On-site";

                var row = UIHelper.CreatePanel(contentRT, "VillagerRow", Color.clear);
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
                var capturedBuildingID = pendingBuildingID;

                if (isBusy)
                {
                    // Warning label for busy villagers
                    var warnLabel = UIHelper.CreateLabel(actionRow.transform,
                        $"Will cancel {taskDesc}", UIConstants.FontCaption, SporefrontColors.SporeAmber);
                    var warnLE = warnLabel.gameObject.AddComponent<LayoutElement>();
                    warnLE.flexibleWidth = 1;
                }
                else
                {
                    // Spacer for idle villagers
                    var spacer = new GameObject("Spacer");
                    spacer.transform.SetParent(actionRow.transform, false);
                    var spacerLE = spacer.AddComponent<LayoutElement>();
                    spacerLE.flexibleWidth = 1;
                }

                Color btnColor = isBusy ? SporefrontColors.SporeAmber : SporefrontColors.SporeGreen;
                var selectBtn = UIHelper.CreateButton(actionRow.transform, "Select",
                    btnColor, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                    {
                        OnVillagerSelected?.Invoke(capturedGroupID, capturedBuildingID);
                        Hide();
                    });
                var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 85;
                btnLE.preferredHeight = 30;
            }
        }
    }
}
