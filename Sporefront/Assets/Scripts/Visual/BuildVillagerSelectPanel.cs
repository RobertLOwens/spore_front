// ============================================================================
// FILE: Visual/BuildVillagerSelectPanel.cs
// PURPOSE: Left-side slide-out panel for selecting a villager group to dispatch
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

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

        private BuildingType pendingBuildingType;
        private HexCoordinate pendingCoordinate;
        private int pendingRotation;

        // Walking time estimate: distance / (BaseSpeed * VillagerSpeedMultiplier) seconds
        private const double WalkSecondsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * GameConfig.Movement.VillagerSpeedMultiplier);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Left-anchored slide-out panel, 280px wide
            panel = UIHelper.CreatePanel(canvasTransform, "BuildVillagerSelectPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.15f);
            rt.anchorMax = new Vector2(0, 0.85f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(280, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "BuildVillagerScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Bottom cancel button
            var btnRow = UIHelper.CreatePanel(panel.transform, "ButtonRow", Color.clear);
            var btnRowRT = btnRow.GetComponent<RectTransform>();
            btnRowRT.anchorMin = Vector2.zero;
            btnRowRT.anchorMax = new Vector2(1, 0);
            btnRowRT.pivot = new Vector2(0.5f, 0);
            btnRowRT.offsetMin = new Vector2(8, 6);
            btnRowRT.offsetMax = new Vector2(-8, 42);

            var btnRowHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnRowHLG.spacing = 8f;
            btnRowHLG.childForceExpandWidth = true;
            btnRowHLG.childForceExpandHeight = true;
            btnRowHLG.childControlWidth = true;
            btnRowHLG.childControlHeight = true;

            var cancelBtn = UIHelper.CreateButton(btnRow.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);

            panel.SetActive(false);
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
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!panel.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Header
            var header = UIHelper.CreateLabel(contentRT,
                $"Select Builder",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            // Building info
            var infoLabel = UIHelper.CreateLabel(contentRT,
                $"  {pendingBuildingType.DisplayName()} at ({pendingCoordinate.q},{pendingCoordinate.r})",
                12, SporefrontColors.InkLight);
            var infoLE = infoLabel.gameObject.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 20;

            UIHelper.CreateDivider(contentRT);

            // Villager group list
            BuildVillagerGroupList(gameState);
        }

        // ================================================================
        // Villager Group List
        // ================================================================

        private void BuildVillagerGroupList(GameState gameState)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Villager Groups",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            var groups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
            if (groups == null || groups.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(contentRT,
                    "  No villager groups available", 12, SporefrontColors.InkFaded);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 20;
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

                // Walking time estimate
                int walkSeconds = distance > 0 ? Mathf.CeilToInt((float)(distance * WalkSecondsPerTile)) : 0;
                string walkTimeStr = walkSeconds > 0
                    ? (walkSeconds < 60 ? $"~{walkSeconds}s" : $"~{walkSeconds / 60}m{walkSeconds % 60}s")
                    : "On-site";

                var row = UIHelper.CreatePanel(contentRT, "VillagerRow", Color.clear);
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = isBusy ? 72 : 56;

                var vlg = row.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 2;
                vlg.padding = new RectOffset(8, 8, 2, 2);
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Name + count
                var nameRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{group.name} ({group.villagerCount})", 12,
                    isBusy ? SporefrontColors.SporeAmber : UIHelper.BodyTextColor);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1;

                var distLabel = UIHelper.CreateLabel(nameRow.transform,
                    $"{distance} tiles", 11, SporefrontColors.InkLight);
                var distLE = distLabel.gameObject.AddComponent<LayoutElement>();
                distLE.preferredWidth = 55;

                // Task + walk time
                var infoRow = UIHelper.CreateHorizontalRow(row.transform, 20f, 4f);
                var taskLabel = UIHelper.CreateLabel(infoRow.transform,
                    taskDesc, 11,
                    isBusy ? SporefrontColors.SporeAmber : SporefrontColors.InkLight);
                var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
                taskLE.flexibleWidth = 1;

                var walkLabel = UIHelper.CreateLabel(infoRow.transform,
                    walkTimeStr, 11, SporefrontColors.InkLight);
                var walkLE = walkLabel.gameObject.AddComponent<LayoutElement>();
                walkLE.preferredWidth = 55;

                // Action row
                var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);

                var capturedGroupID = group.id;
                var capturedType = pendingBuildingType;
                var capturedCoord = pendingCoordinate;
                var capturedRotation = pendingRotation;

                if (isBusy)
                {
                    // Warning label for busy villagers
                    var warnLabel = UIHelper.CreateLabel(actionRow.transform,
                        $"Will cancel {taskDesc}", 10, SporefrontColors.SporeAmber);
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
                    btnColor, UIHelper.HudTextColor, 11, () =>
                    {
                        OnVillagerSelected?.Invoke(capturedGroupID, capturedType, capturedCoord, capturedRotation);
                        Hide();
                    });
                var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 60;
                btnLE.preferredHeight = 24;
            }
        }
    }
}
