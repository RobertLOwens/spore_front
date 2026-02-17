// ============================================================================
// FILE: Visual/GatherPanel.cs
// PURPOSE: Left-side slide-out panel for assigning villager groups to gather
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

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private Guid? currentResourcePointID;
        private Guid localPlayerID;
        private Guid? selectedVillagerGroupID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Left-anchored slide-out panel, 280px wide
            panel = UIHelper.CreatePanel(canvasTransform, "GatherPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.15f);
            rt.anchorMax = new Vector2(0, 0.85f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(280, 0);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "GatherScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 44);
            scrollRT.offsetMax = Vector2.zero;

            // Bottom button row
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

        public void Show(GameState gameState, Guid resourcePointID)
        {
            currentResourcePointID = resourcePointID;
            selectedVillagerGroupID = null;
            Rebuild(gameState);
            panel.SetActive(true);
        }

        public void Hide()
        {
            currentResourcePointID = null;
            selectedVillagerGroupID = null;
            panel.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh(GameState gameState)
        {
            if (!currentResourcePointID.HasValue || !panel.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentResourcePointID.HasValue) return;
            var rp = gameState.GetResourcePoint(currentResourcePointID.Value);
            if (rp == null) { Hide(); return; }

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            bool isHuntable = rp.resourceType.IsHuntable();

            // Header
            var header = UIHelper.CreateLabel(contentRT,
                isHuntable ? "Hunt" : "Gather",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            UIHelper.CreateDivider(contentRT);

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
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 22;

            // Type
            var typeLabel = UIHelper.CreateLabel(contentRT,
                $"  Type: {rp.resourceType.DisplayName()}", 12);
            var typeLE = typeLabel.gameObject.AddComponent<LayoutElement>();
            typeLE.preferredHeight = 20;

            // Yields
            var yieldLabel = UIHelper.CreateLabel(contentRT,
                $"  Yields: {UIHelper.ResourceIcon(rp.resourceType.ResourceYield())} {rp.resourceType.ResourceYield().DisplayName()}", 12);
            var yieldLE = yieldLabel.gameObject.AddComponent<LayoutElement>();
            yieldLE.preferredHeight = 20;

            // Remaining amount
            var remainLabel = UIHelper.CreateLabel(contentRT,
                $"  Remaining: {rp.remainingAmount}/{rp.resourceType.InitialAmount()}", 12);
            var remainLE = remainLabel.gameObject.AddComponent<LayoutElement>();
            remainLE.preferredHeight = 20;

            // Remaining bar
            float pct = rp.resourceType.InitialAmount() > 0
                ? (float)rp.remainingAmount / rp.resourceType.InitialAmount()
                : 0f;
            var (bg, fill) = UIHelper.CreateProgressBar(contentRT, 12f,
                SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
            var barLE = bg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = 12;

            // Current gatherers
            var gatherersLabel = UIHelper.CreateLabel(contentRT,
                $"  Gatherers: {rp.totalVillagersGathering}/{ResourcePointData.MaxVillagersPerTile}", 12,
                SporefrontColors.InkLight);
            var gatherersLE = gatherersLabel.gameObject.AddComponent<LayoutElement>();
            gatherersLE.preferredHeight = 20;

            // Health (huntable animals)
            if (rp.resourceType.IsHuntable())
            {
                var healthLabel = UIHelper.CreateLabel(contentRT,
                    $"  Health: {(int)rp.currentHealth}/{(int)rp.resourceType.MaxHealth()}", 12);
                var healthLE = healthLabel.gameObject.AddComponent<LayoutElement>();
                healthLE.preferredHeight = 20;

                float hpPct = rp.resourceType.MaxHealth() > 0
                    ? (float)(rp.currentHealth / rp.resourceType.MaxHealth())
                    : 0f;
                var (hpBg, hpFill) = UIHelper.CreateProgressBar(contentRT, 12f,
                    SporefrontColors.InkFaded, SporefrontColors.SporeRed);
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
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Villager Groups",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
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

            // Sort by distance to resource point
            groups.Sort((a, b) =>
                a.coordinate.Distance(rp.coordinate).CompareTo(b.coordinate.Distance(rp.coordinate)));

            foreach (var group in groups)
            {
                if (group.villagerCount <= 0) continue;

                int distance = group.coordinate.Distance(rp.coordinate);
                bool isBusy = group.currentTask != null && !group.currentTask.IsIdle;
                string taskDesc = isBusy ? group.currentTask.DisplayName : "Idle";

                var row = UIHelper.CreatePanel(contentRT, "VillagerRow", Color.clear);
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 56;

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

                // Task + action
                var actionRow = UIHelper.CreateHorizontalRow(row.transform, 24f, 4f);
                var taskLabel = UIHelper.CreateLabel(actionRow.transform,
                    taskDesc, 11,
                    isBusy ? SporefrontColors.SporeAmber : SporefrontColors.InkLight);
                var taskLE = taskLabel.gameObject.AddComponent<LayoutElement>();
                taskLE.flexibleWidth = 1;

                // Confirm button
                var capturedGroupID = group.id;
                var capturedRPID = rp.id;
                string btnText = isHuntable ? "Hunt" : "Gather";
                Color btnColor = isHuntable ? SporefrontColors.SporeRed : SporefrontColors.SporeGreen;

                var confirmBtn = UIHelper.CreateButton(actionRow.transform, btnText,
                    btnColor, UIHelper.HudTextColor, 11, () =>
                    {
                        if (isHuntable)
                            OnHuntConfirmed?.Invoke(capturedGroupID, capturedRPID);
                        else
                            OnGatherConfirmed?.Invoke(capturedGroupID, capturedRPID);
                        Hide();
                    });
                var btnLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 60;
                btnLE.preferredHeight = 24;
            }
        }
    }
}
