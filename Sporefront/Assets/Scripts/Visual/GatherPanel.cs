// ============================================================================
// FILE: Visual/GatherPanel.cs
// PURPOSE: Centered modal panel for assigning villager groups to gather
//          or hunt a resource point. Shows resource info and available groups.
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
    public class GatherPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid, Guid> OnGatherConfirmed;  // villagerGroupID, resourcePointID
        public event Action<Guid, Guid> OnHuntConfirmed;    // villagerGroupID, resourcePointID
        public event Action OnClose;
        public event Action<Guid, HexCoordinate> OnPreviewPathRequested; // villagerGroupID, resourceCoordinate
        public event Action OnPreviewPathCleared;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject modalPanel;
        private RectTransform contentRT;
        private Text headerLabel;
        private Guid? currentResourcePointID;
        private Guid localPlayerID;
        private Guid? selectedVillagerGroupID;
        private GameState cachedGameState;
        private bool cachedIsHuntable;

        // Walking time estimate: distance / (BaseSpeed * VillagerSpeedMultiplier) seconds
        private const double WalkSecondsPerTile = 1.0 / (GameConfig.Movement.BaseSpeed * GameConfig.Movement.VillagerSpeedMultiplier);

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Full-screen backdrop with click-to-dismiss
            backdrop = UIHelper.CreatePanel(canvasTransform, "GatherBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Centered modal panel
            modalPanel = UIHelper.CreatePanel(backdrop.transform, "GatherModal", UIHelper.PanelParchmentBg);
            var rt = modalPanel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(rt, UIConstants.ModalSmallW, UIConstants.ModalSmallH);
            PopupTendrilDecorator.Attach(rt);

            // Header — fixed at top (text changes between "Hunt"/"Gather")
            headerLabel = UIHelper.CreateLabel(modalPanel.transform, "Gather",
                UIConstants.FontTitle, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            var headerRT = headerLabel.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.offsetMin = new Vector2(12, -40);
            headerRT.offsetMax = new Vector2(-12, -6);

            // Scroll area for resource info + villager list
            var scroll = UIHelper.CreateScrollView(modalPanel.transform, "GatherScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 52); // Space for close button
            scrollRT.offsetMax = new Vector2(0, -42); // Space for header

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

        public void Show(GameState gameState, Guid resourcePointID)
        {
            currentResourcePointID = resourcePointID;
            selectedVillagerGroupID = null;
            Rebuild(gameState);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            ClearPreview();
            currentResourcePointID = null;
            selectedVillagerGroupID = null;
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentResourcePointID.HasValue || !backdrop.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentResourcePointID.HasValue) return;
            cachedGameState = gameState;
            var rp = gameState.GetResourcePoint(currentResourcePointID.Value);
            if (rp == null) { Hide(); return; }

            // Clear scroll content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            bool isHuntable = rp.resourceType.IsHuntable();
            cachedIsHuntable = isHuntable;

            // Update header text
            if (headerLabel != null)
                headerLabel.text = isHuntable ? "Hunt" : "Gather";

            // Resource point info
            BuildResourceInfo(rp);
            UIHelper.CreateDivider(contentRT);

            // Available villager groups
            BuildVillagerGroupList(gameState, rp, isHuntable);
        }

        // ================================================================
        // Resource Info Section
        // ================================================================

        private void BuildResourceInfo(ResourcePointData rp)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Resource Point",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            // Type
            var typeLabel = UIHelper.CreateLabel(contentRT,
                $"  Type: {rp.resourceType.DisplayName()}", UIConstants.FontSmall);
            var typeLE = typeLabel.gameObject.AddComponent<LayoutElement>();
            typeLE.preferredHeight = 24;

            // Yields
            var yieldLabel = UIHelper.CreateLabel(contentRT,
                $"  Yields: {UIHelper.ResourceIcon(rp.resourceType.ResourceYield())} {rp.resourceType.ResourceYield().DisplayName()}", UIConstants.FontSmall);
            var yieldLE = yieldLabel.gameObject.AddComponent<LayoutElement>();
            yieldLE.preferredHeight = 24;

            // Remaining amount
            var remainLabel = UIHelper.CreateLabel(contentRT,
                $"  Remaining: {rp.remainingAmount}/{rp.resourceType.InitialAmount()}", UIConstants.FontSmall);
            var remainLE = remainLabel.gameObject.AddComponent<LayoutElement>();
            remainLE.preferredHeight = 24;

            // Remaining bar
            float pct = rp.resourceType.InitialAmount() > 0
                ? (float)rp.remainingAmount / rp.resourceType.InitialAmount()
                : 0f;
            var (bg, fill) = UIHelper.CreateInkProgressBar(contentRT, 12f,
                UIHelper.InkMutedText, SporefrontColors.SporeGreen);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = 12;

            // Current gatherers
            var gatherersLabel = UIHelper.CreateLabel(contentRT,
                $"  Gatherers: {rp.totalVillagersGathering}/{ResourcePointData.MaxVillagersPerTile}", UIConstants.FontSmall,
                UIHelper.InkMutedText);
            var gatherersLE = gatherersLabel.gameObject.AddComponent<LayoutElement>();
            gatherersLE.preferredHeight = 24;

            // Health (huntable animals)
            if (rp.resourceType.IsHuntable())
            {
                var healthLabel = UIHelper.CreateLabel(contentRT,
                    $"  Health: {(int)rp.currentHealth}/{(int)rp.resourceType.MaxHealth()}", UIConstants.FontSmall);
                var healthLE = healthLabel.gameObject.AddComponent<LayoutElement>();
                healthLE.preferredHeight = 24;

                float hpPct = rp.resourceType.MaxHealth() > 0
                    ? (float)(rp.currentHealth / rp.resourceType.MaxHealth())
                    : 0f;
                var (hpBg, hpFill) = UIHelper.CreateInkProgressBar(contentRT, 12f,
                    UIHelper.InkMutedText, SporefrontColors.SporeRed);
                var hpFillRT = hpFill.GetComponent<RectTransform>();
                hpFillRT.anchorMax = new Vector2(Mathf.Clamp01(hpPct), 1);
                var hpBarLE = hpBg.gameObject.AddComponent<LayoutElement>();
                hpBarLE.preferredHeight = 12;
            }
        }

        // ================================================================
        // Villager Group List
        // ================================================================

        private void BuildVillagerGroupList(GameState gameState, ResourcePointData rp, bool isHuntable)
        {
            // Guard: if previewed villager is no longer available, reset
            if (selectedVillagerGroupID.HasValue)
            {
                bool found = false;
                var checkGroups = gameState.GetVillagerGroupsForPlayer(localPlayerID);
                if (checkGroups != null)
                    foreach (var g in checkGroups)
                        if (g.id == selectedVillagerGroupID.Value) { found = true; break; }
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

            // Sort by distance to resource point
            groups.Sort((a, b) =>
                a.coordinate.Distance(rp.coordinate).CompareTo(b.coordinate.Distance(rp.coordinate)));

            foreach (var group in groups)
            {
                if (group.villagerCount <= 0) continue;

                int distance = group.coordinate.Distance(rp.coordinate);
                bool isBusy = group.currentTask != null && !group.currentTask.IsIdle;
                string taskDesc = isBusy ? group.currentTask.DisplayName : "Idle";
                bool isSelected = selectedVillagerGroupID.HasValue && selectedVillagerGroupID.Value == group.id;

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
                var capturedRPCoord = rp.coordinate;

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

                Color btnColor = isHuntable ? SporefrontColors.SporeRed : SporefrontColors.SporeGreen;
                var selectBtn = UIHelper.CreateButton(actionRow.transform,
                    isSelected ? "Selected" : "Select",
                    btnColor, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                    {
                        selectedVillagerGroupID = capturedGroupID;
                        OnPreviewPathRequested?.Invoke(capturedGroupID, capturedRPCoord);
                        Rebuild(cachedGameState);
                    });
                var btnLE = selectBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 85;
                btnLE.preferredHeight = 30;
            }

            // Confirm button — only when a villager is previewed
            if (selectedVillagerGroupID.HasValue)
            {
                UIHelper.CreateDivider(contentRT);
                var capturedVGID = selectedVillagerGroupID.Value;
                var capturedRPID = rp.id;
                string confirmText = isHuntable ? "Confirm Hunt" : "Confirm Gather";
                Color confirmColor = isHuntable ? SporefrontColors.SporeRed : SporefrontColors.SporeGreen;
                var confirmBtn = UIHelper.CreateButton(contentRT, confirmText,
                    confirmColor, UIHelper.HudTextColor, UIConstants.FontBody, () =>
                    {
                        if (isHuntable)
                            OnHuntConfirmed?.Invoke(capturedVGID, capturedRPID);
                        else
                            OnGatherConfirmed?.Invoke(capturedVGID, capturedRPID);
                        ClearPreview();
                        Hide();
                    });
                var confirmLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                confirmLE.preferredHeight = 44;
            }
        }

        private void ClearPreview()
        {
            if (selectedVillagerGroupID.HasValue)
            {
                selectedVillagerGroupID = null;
                OnPreviewPathCleared?.Invoke();
            }
        }
    }
}
