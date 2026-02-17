// ============================================================================
// FILE: Visual/ResearchTreePanel.cs
// PURPOSE: Modal panel for research tree — branch columns, node states, progress
//          Ported from ResearchViewController.swift
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
    public class ResearchTreePanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<ResearchType> OnStartResearch;
        public event Action OnCancelResearch;
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform treeContentRT;
        private Guid localPlayerID;
        private ResearchCategory currentCategory = ResearchCategory.Economic;
        private ResearchType? selectedNode;

        // Active research bar references
        private Image activeProgressFill;
        private Text activeProgressLabel;
        private Text activeNameLabel;
        private GameObject activeResearchBar;

        // Tab button references
        private Button economicTabBtn;
        private Button militaryTabBtn;

        // Node detail popup
        private GameObject nodeDetailPopup;
        private RectTransform nodeDetailContentRT;

        // ================================================================
        // Constants
        // ================================================================

        private const float NodeWidth = 130f;
        private const float NodeHeight = 40f;
        private const float NodeSpacingV = 12f;
        private const float BranchSpacingH = 20f;
        private const float BranchHeaderHeight = 40f;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "ResearchTreeBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 750x550
            panel = UIHelper.CreatePanel(backdrop.transform, "ResearchTreePanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 750, 550);

            // Title
            var titleLabel = UIHelper.CreateLabel(panel.transform, "Research Tree",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleRT = titleLabel.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.offsetMin = new Vector2(8, -32);
            titleRT.offsetMax = new Vector2(-8, -4);

            // Active research bar (below title)
            BuildActiveResearchBar();

            // Tab buttons (below active research)
            BuildTabButtons();

            // Scrollable tree area
            var treeScroll = UIHelper.CreateScrollView(panel.transform, "TreeScroll", out treeContentRT);
            treeScroll.horizontal = true;
            var treeScrollRT = treeScroll.GetComponent<RectTransform>();
            treeScrollRT.anchorMin = new Vector2(0, 0);
            treeScrollRT.anchorMax = new Vector2(1, 1);
            treeScrollRT.offsetMin = new Vector2(6, 44);
            treeScrollRT.offsetMax = new Vector2(-6, -106);

            // Override content layout for horizontal tree
            var contentVLG = treeContentRT.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null) DestroyImmediate(contentVLG);
            var hlg = treeContentRT.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = BranchSpacingH;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.padding = new RectOffset(8, 8, 8, 8);

            // Content size fitter - need both horizontal and vertical for scroll
            var csf = treeContentRT.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Node detail popup (hidden by default)
            BuildNodeDetailPopup();

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 38);

            backdrop.SetActive(false);
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Active Research Bar
        // ================================================================

        private void BuildActiveResearchBar()
        {
            activeResearchBar = UIHelper.CreatePanel(panel.transform, "ActiveResearchBar",
                new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                    SporefrontColors.InkDark.b, 0.3f));
            var barRT = activeResearchBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 1);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.pivot = new Vector2(0.5f, 1);
            barRT.offsetMin = new Vector2(6, -68);
            barRT.offsetMax = new Vector2(-6, -36);

            var hlg = activeResearchBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            activeNameLabel = UIHelper.CreateLabel(activeResearchBar.transform,
                "No active research", 12, SporefrontColors.InkLight);
            var nameLE = activeNameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 180;

            var (bg, fill) = UIHelper.CreateProgressBar(activeResearchBar.transform, 14f,
                SporefrontColors.InkFaded, SporefrontColors.SporeTeal);
            activeProgressFill = fill;
            var barBgLE = bg.gameObject.AddComponent<LayoutElement>();
            barBgLE.flexibleWidth = 1;
            barBgLE.preferredHeight = 14;

            activeProgressLabel = UIHelper.CreateLabel(activeResearchBar.transform,
                "", 11, SporefrontColors.InkLight, TextAnchor.MiddleRight);
            var progLabelLE = activeProgressLabel.gameObject.AddComponent<LayoutElement>();
            progLabelLE.preferredWidth = 60;

            var cancelBtn = UIHelper.CreateButton(activeResearchBar.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 10, () =>
                {
                    OnCancelResearch?.Invoke();
                });
            var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            cancelLE.preferredWidth = 50;
            cancelLE.preferredHeight = 22;
        }

        // ================================================================
        // Tab Buttons
        // ================================================================

        private void BuildTabButtons()
        {
            var tabContainer = UIHelper.CreatePanel(panel.transform, "TabContainer", Color.clear);
            var tabRT = tabContainer.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0, 1);
            tabRT.anchorMax = new Vector2(1, 1);
            tabRT.pivot = new Vector2(0.5f, 1);
            tabRT.offsetMin = new Vector2(6, -102);
            tabRT.offsetMax = new Vector2(-6, -72);

            var hlg = tabContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(8, 8, 0, 0);
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            economicTabBtn = UIHelper.CreateButton(tabContainer.transform, "Economic",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 13, () =>
                {
                    currentCategory = ResearchCategory.Economic;
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                });

            militaryTabBtn = UIHelper.CreateButton(tabContainer.transform, "Military",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 13, () =>
                {
                    currentCategory = ResearchCategory.Military;
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                });
        }

        // ================================================================
        // Node Detail Popup
        // ================================================================

        private void BuildNodeDetailPopup()
        {
            nodeDetailPopup = UIHelper.CreatePanel(panel.transform, "NodeDetailPopup",
                new Color(UIHelper.PanelBg.r, UIHelper.PanelBg.g, UIHelper.PanelBg.b, 0.98f));
            var popupRT = nodeDetailPopup.GetComponent<RectTransform>();
            popupRT.anchorMin = new Vector2(0.5f, 0);
            popupRT.anchorMax = new Vector2(0.5f, 0);
            popupRT.pivot = new Vector2(0.5f, 0);
            popupRT.sizeDelta = new Vector2(350, 240);
            popupRT.anchoredPosition = new Vector2(0, 44);

            var scroll = UIHelper.CreateScrollView(nodeDetailPopup.transform, "PopupScroll", out nodeDetailContentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 30);
            scrollRT.offsetMax = Vector2.zero;

            // Close popup button
            var closePopup = UIHelper.CreateButton(nodeDetailPopup.transform, "X",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 11, () =>
                {
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                });
            var closePopupRT = closePopup.GetComponent<RectTransform>();
            closePopupRT.anchorMin = new Vector2(1, 1);
            closePopupRT.anchorMax = new Vector2(1, 1);
            closePopupRT.pivot = new Vector2(1, 1);
            closePopupRT.sizeDelta = new Vector2(28, 24);
            closePopupRT.anchoredPosition = new Vector2(-4, -4);

            nodeDetailPopup.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            selectedNode = null;
            nodeDetailPopup.SetActive(false);
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
            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            // Update active research bar
            UpdateActiveResearchBar(player, gameState);

            // Update tab highlighting
            UpdateTabHighlights();

            // Clear tree content
            for (int i = treeContentRT.childCount - 1; i >= 0; i--)
                Destroy(treeContentRT.GetChild(i).gameObject);

            // Get branches for current category
            var branches = GetBranchesForCategory(currentCategory);

            foreach (var branch in branches)
            {
                BuildBranchColumn(branch, player, gameState);
            }

            // Update node detail if selected
            if (selectedNode.HasValue)
            {
                RebuildNodeDetail(selectedNode.Value, player, gameState);
            }
        }

        // ================================================================
        // Active Research Bar Update
        // ================================================================

        private void UpdateActiveResearchBar(PlayerState player, GameState gameState)
        {
            if (!string.IsNullOrEmpty(player.activeResearchType) && player.activeResearchStartTime.HasValue)
            {
                ResearchType researchType;
                if (Enum.TryParse(player.activeResearchType, out researchType))
                {
                    var activeResearch = new ActiveResearch(researchType, player.activeResearchStartTime.Value);
                    double currentTime = gameState.currentTime;
                    double progress = activeResearch.GetProgress(currentTime);
                    double remaining = activeResearch.GetRemainingTime(currentTime);

                    activeNameLabel.text = researchType.DisplayName();
                    activeNameLabel.color = UIHelper.BodyTextColor;

                    var fillRT = activeProgressFill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)progress), 1);

                    int remSec = (int)remaining;
                    activeProgressLabel.text = remSec > 60 ? $"{remSec / 60}m {remSec % 60}s" : $"{remSec}s";
                    return;
                }
            }

            activeNameLabel.text = "No active research";
            activeNameLabel.color = SporefrontColors.InkLight;
            var defaultFillRT = activeProgressFill.GetComponent<RectTransform>();
            defaultFillRT.anchorMax = new Vector2(0, 1);
            activeProgressLabel.text = "";
        }

        // ================================================================
        // Tab Highlighting
        // ================================================================

        private void UpdateTabHighlights()
        {
            if (economicTabBtn != null)
            {
                var econImg = economicTabBtn.GetComponent<Image>();
                econImg.color = currentCategory == ResearchCategory.Economic
                    ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark;
            }
            if (militaryTabBtn != null)
            {
                var milImg = militaryTabBtn.GetComponent<Image>();
                milImg.color = currentCategory == ResearchCategory.Military
                    ? SporefrontColors.SporeRed : SporefrontColors.ParchmentDark;
            }
        }

        // ================================================================
        // Branch Column
        // ================================================================

        private void BuildBranchColumn(ResearchBranch branch, PlayerState player, GameState gameState)
        {
            var columnGO = new GameObject("Branch_" + branch, typeof(RectTransform));
            columnGO.transform.SetParent(treeContentRT, false);

            var vlg = columnGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = NodeSpacingV;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 8);

            var columnLE = columnGO.AddComponent<LayoutElement>();
            columnLE.preferredWidth = NodeWidth + 8;

            // Branch header
            var headerPanel = UIHelper.CreatePanel(columnGO.transform, "BranchHeader",
                SporefrontColors.ParchmentDark);
            var headerPanelLE = headerPanel.AddComponent<LayoutElement>();
            headerPanelLE.preferredWidth = NodeWidth;
            headerPanelLE.preferredHeight = BranchHeaderHeight;

            var headerVLG = headerPanel.AddComponent<VerticalLayoutGroup>();
            headerVLG.padding = new RectOffset(4, 4, 2, 2);
            headerVLG.spacing = 1;
            headerVLG.childForceExpandWidth = true;
            headerVLG.childForceExpandHeight = false;
            headerVLG.childControlWidth = true;
            headerVLG.childControlHeight = false;

            var branchName = UIHelper.CreateLabel(headerPanel.transform, branch.DisplayName(),
                12, UIHelper.HeaderTextColor, TextAnchor.MiddleCenter, true);
            var bnLE = branchName.gameObject.AddComponent<LayoutElement>();
            bnLE.preferredHeight = 18;

            // Gate building info
            var gateBuilding = branch.GateBuildingType();
            if (gateBuilding.HasValue)
            {
                var gateLabel = UIHelper.CreateLabel(headerPanel.transform,
                    $"Requires: {gateBuilding.Value.DisplayName()}", 9,
                    SporefrontColors.InkLight, TextAnchor.MiddleCenter);
                var gateLE = gateLabel.gameObject.AddComponent<LayoutElement>();
                gateLE.preferredHeight = 14;
            }
            else
            {
                var noGateLabel = UIHelper.CreateLabel(headerPanel.transform,
                    "No building required", 9,
                    SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var noGateLE = noGateLabel.gameObject.AddComponent<LayoutElement>();
                noGateLE.preferredHeight = 14;
            }

            // Research nodes in this branch
            var researchTypes = GetResearchTypesInBranch(branch);
            foreach (var rt in researchTypes)
            {
                BuildResearchNode(columnGO.transform, rt, player, gameState);
            }
        }

        // ================================================================
        // Research Node
        // ================================================================

        private void BuildResearchNode(Transform parent, ResearchType researchType,
            PlayerState player, GameState gameState)
        {
            var state = GetNodeState(researchType, player);

            Color nodeBg;
            Color textColor;
            switch (state)
            {
                case NodeState.Completed:
                    nodeBg = SporefrontColors.SporeGreen;
                    textColor = UIHelper.HudTextColor;
                    break;
                case NodeState.Researching:
                    nodeBg = SporefrontColors.SporeTeal;
                    textColor = UIHelper.HudTextColor;
                    break;
                case NodeState.Available:
                    nodeBg = SporefrontColors.ParchmentLight;
                    textColor = UIHelper.BodyTextColor;
                    break;
                default: // Locked
                    nodeBg = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                        SporefrontColors.InkDark.b, 0.6f);
                    textColor = SporefrontColors.InkFaded;
                    break;
            }

            var nodePanel = UIHelper.CreatePanel(parent, "Node_" + researchType, nodeBg);
            var nodeLE = nodePanel.AddComponent<LayoutElement>();
            nodeLE.preferredWidth = NodeWidth;
            nodeLE.preferredHeight = NodeHeight;

            var nodeVLG = nodePanel.AddComponent<VerticalLayoutGroup>();
            nodeVLG.padding = new RectOffset(4, 4, 3, 3);
            nodeVLG.spacing = 1;
            nodeVLG.childForceExpandWidth = true;
            nodeVLG.childForceExpandHeight = false;
            nodeVLG.childControlWidth = true;
            nodeVLG.childControlHeight = false;

            var nameLabel = UIHelper.CreateLabel(nodePanel.transform,
                researchType.DisplayName(), 10, textColor, TextAnchor.MiddleCenter);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 16;

            // Status indicator
            string statusText;
            switch (state)
            {
                case NodeState.Completed: statusText = "[Done]"; break;
                case NodeState.Researching: statusText = "[Researching...]"; break;
                case NodeState.Available: statusText = $"Tier {researchType.Tier()}"; break;
                default: statusText = "Locked"; break;
            }
            var statusLabel = UIHelper.CreateLabel(nodePanel.transform, statusText, 9,
                textColor, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 13;

            // Click handler
            var capturedType = researchType;
            var clickBtn = nodePanel.AddComponent<Button>();
            clickBtn.transition = Selectable.Transition.None;
            clickBtn.onClick.AddListener(() =>
            {
                selectedNode = capturedType;
                nodeDetailPopup.SetActive(true);
            });
        }

        // ================================================================
        // Node Detail Popup Content
        // ================================================================

        private void RebuildNodeDetail(ResearchType researchType, PlayerState player, GameState gameState)
        {
            for (int i = nodeDetailContentRT.childCount - 1; i >= 0; i--)
                Destroy(nodeDetailContentRT.GetChild(i).gameObject);

            var state = GetNodeState(researchType, player);

            // Name
            var nameLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                researchType.DisplayName(),
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 26;

            // Description
            var descLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                researchType.Description(), 12, SporefrontColors.InkLight,
                TextAnchor.MiddleLeft);
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 20;

            UIHelper.CreateDivider(nodeDetailContentRT);

            // Cost
            var cost = researchType.Cost();
            var costParts = new List<string>();
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0)
                    costParts.Add($"{UIHelper.ResourceIcon(kvp.Key)}{kvp.Value}");
            }
            var costLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                $"Cost: {string.Join("  ", costParts)}", 12, UIHelper.BodyTextColor);
            costLabel.supportRichText = true;
            var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
            costLE.preferredHeight = 20;

            // Research time
            double researchTime = researchType.ResearchTime();
            int timeSec = (int)researchTime;
            string timeStr = timeSec >= 60 ? $"{timeSec / 60}m {timeSec % 60}s" : $"{timeSec}s";
            var timeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                $"Time: {timeStr}", 12, UIHelper.BodyTextColor);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredHeight = 20;

            // Prerequisites
            var prereqs = researchType.Prerequisites();
            if (prereqs.Length > 0)
            {
                var prereqNames = prereqs.Select(p => p.DisplayName()).ToArray();
                var prereqLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    $"Requires: {string.Join(", ", prereqNames)}", 11, SporefrontColors.InkLight);
                var prereqLE = prereqLabel.gameObject.AddComponent<LayoutElement>();
                prereqLE.preferredHeight = 18;
            }

            // Building requirement
            var buildingReq = researchType.BuildingRequirement();
            if (buildingReq.HasValue)
            {
                var bldgLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    $"Building: {buildingReq.Value.buildingType.DisplayName()} Lv.{buildingReq.Value.level}",
                    11, SporefrontColors.InkLight);
                var bldgLE = bldgLabel.gameObject.AddComponent<LayoutElement>();
                bldgLE.preferredHeight = 18;
            }

            UIHelper.CreateDivider(nodeDetailContentRT);

            // Bonuses
            var bonuses = researchType.Bonuses();
            if (bonuses.Length > 0)
            {
                var bonusHeader = UIHelper.CreateLabel(nodeDetailContentRT, "Bonuses:",
                    12, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, false);
                var bonusHeaderLE = bonusHeader.gameObject.AddComponent<LayoutElement>();
                bonusHeaderLE.preferredHeight = 18;

                foreach (var bonus in bonuses)
                {
                    var bonusLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"  {bonus.DisplayString}", 11, SporefrontColors.SporeGreen);
                    var bLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
                    bLE.preferredHeight = 16;
                }
            }

            UIHelper.CreateDivider(nodeDetailContentRT);

            // Start Research button (only if available)
            if (state == NodeState.Available)
            {
                bool canAfford = player != null && player.CanAfford(cost);
                var capturedType = researchType;

                var startBtn = UIHelper.CreateButton(nodeDetailContentRT, "Start Research",
                    canAfford ? SporefrontColors.SporeGreen : SporefrontColors.InkFaded,
                    canAfford ? UIHelper.HudTextColor : SporefrontColors.InkLight, 13, () =>
                    {
                        OnStartResearch?.Invoke(capturedType);
                        selectedNode = null;
                        nodeDetailPopup.SetActive(false);
                    });
                startBtn.interactable = canAfford;
                var startLE = startBtn.gameObject.AddComponent<LayoutElement>();
                startLE.preferredHeight = 32;

                if (!canAfford)
                {
                    var affordLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        "Insufficient resources", 10, SporefrontColors.SporeRed,
                        TextAnchor.MiddleCenter);
                    var affordLE = affordLabel.gameObject.AddComponent<LayoutElement>();
                    affordLE.preferredHeight = 16;
                }
            }
            else if (state == NodeState.Completed)
            {
                var completeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    "Research Complete", 13, SporefrontColors.SporeGreen,
                    TextAnchor.MiddleCenter);
                var compLE = completeLabel.gameObject.AddComponent<LayoutElement>();
                compLE.preferredHeight = 28;
            }
            else if (state == NodeState.Researching)
            {
                var researchingLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    "Currently Researching...", 13, SporefrontColors.SporeTeal,
                    TextAnchor.MiddleCenter);
                var resLE = researchingLabel.gameObject.AddComponent<LayoutElement>();
                resLE.preferredHeight = 28;
            }
            else
            {
                var lockedLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    "Prerequisites not met", 12, SporefrontColors.SporeRed,
                    TextAnchor.MiddleCenter);
                var lockLE = lockedLabel.gameObject.AddComponent<LayoutElement>();
                lockLE.preferredHeight = 28;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private enum NodeState { Locked, Available, Researching, Completed }

        private NodeState GetNodeState(ResearchType researchType, PlayerState player)
        {
            if (player == null) return NodeState.Locked;

            // Completed
            if (player.completedResearch.Contains(researchType.ToString()))
                return NodeState.Completed;

            // Currently researching
            if (player.activeResearchType == researchType.ToString())
                return NodeState.Researching;

            // Check prerequisites
            var prereqs = researchType.Prerequisites();
            foreach (var prereq in prereqs)
            {
                if (!player.completedResearch.Contains(prereq.ToString()))
                    return NodeState.Locked;
            }

            // Check CC level
            // (Simplified - assumes CC meets requirement if prereqs are met)

            return NodeState.Available;
        }

        private ResearchBranch[] GetBranchesForCategory(ResearchCategory category)
        {
            if (category == ResearchCategory.Economic)
            {
                return new[]
                {
                    ResearchBranch.Gathering,
                    ResearchBranch.Commerce,
                    ResearchBranch.Infrastructure
                };
            }
            else
            {
                return new[]
                {
                    ResearchBranch.Logistics,
                    ResearchBranch.MeleeEquipment,
                    ResearchBranch.RangedEquipment,
                    ResearchBranch.SiegeFortification
                };
            }
        }

        private List<ResearchType> GetResearchTypesInBranch(ResearchBranch branch)
        {
            var allTypes = (ResearchType[])Enum.GetValues(typeof(ResearchType));
            return allTypes.Where(rt => rt.Branch() == branch)
                           .OrderBy(rt => rt.Tier())
                           .ThenBy(rt => rt.ToString())
                           .ToList();
        }
    }
}
