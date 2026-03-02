// ============================================================================
// FILE: Visual/ResearchTreePanel.cs
// PURPOSE: Modal panel for research tree — track-per-row section cards,
//          connection lines, enhanced lock states, anchored popup, progress
// ============================================================================

using System;
using System.Collections;
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
        private RectTransform panelRT;
        private RectTransform treeContentRT;
        private ScrollRect treeScroll;
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

        // Connection lines
        private UILineRenderer lineRenderer;
        private Dictionary<ResearchType, RectTransform> nodePositions = new Dictionary<ResearchType, RectTransform>();
        private Coroutine pendingLineDrawCoroutine;

        // Tier header references
        private RectTransform tierHeaderRT;

        // ================================================================
        // Constants
        // ================================================================

        private const float NodeWidth = 148f;
        private const float NodeHeight = 50f;
        private const float NodeSpacingH = 10f;
        private const float TrackRowHeight = 54f;
        private const float TrackLabelWidth = 120f;
        private const float BranchHeaderH = 38f;
        private const float PopupWidth = 320f;
        private const float PopupHeight = 280f;

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

            // Main panel — 80% of screen via anchors
            panel = UIHelper.CreatePanel(backdrop.transform, "ResearchTreePanel", UIHelper.PanelBg);
            panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.1f, 0.1f);
            panelRT.anchorMax = new Vector2(0.9f, 0.9f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

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

            // Tier column headers (fixed above scroll area)
            BuildTierHeaders();

            // Scrollable tree area
            treeScroll = UIHelper.CreateScrollView(panel.transform, "TreeScroll", out treeContentRT);
            treeScroll.horizontal = false;
            treeScroll.vertical = true;
            var treeScrollRT = treeScroll.GetComponent<RectTransform>();
            treeScrollRT.anchorMin = new Vector2(0, 0);
            treeScrollRT.anchorMax = new Vector2(1, 1);
            treeScrollRT.offsetMin = new Vector2(6, 48);
            treeScrollRT.offsetMax = new Vector2(-6, -130);

            // Override content layout for section cards stacked vertically
            var contentVLG = treeContentRT.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null)
            {
                contentVLG.spacing = 12;
                contentVLG.childAlignment = TextAnchor.UpperLeft;
                contentVLG.childForceExpandWidth = true;
                contentVLG.childForceExpandHeight = false;
                contentVLG.childControlWidth = true;
                contentVLG.childControlHeight = true;
                contentVLG.padding = new RectOffset(8, 8, 8, 8);
            }

            // Content size fitter
            var csf = treeContentRT.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Line renderer for connection lines (child of content, behind nodes)
            var lineGO = new GameObject("ConnectionLines", typeof(RectTransform), typeof(CanvasRenderer));
            lineGO.transform.SetParent(treeContentRT, false);
            lineGO.transform.SetAsFirstSibling();
            lineRenderer = lineGO.AddComponent<UILineRenderer>();
            lineRenderer.raycastTarget = false;
            var lineRT = lineGO.GetComponent<RectTransform>();
            lineRT.anchorMin = Vector2.zero;
            lineRT.anchorMax = Vector2.one;
            lineRT.offsetMin = Vector2.zero;
            lineRT.offsetMax = Vector2.zero;

            // Exclude line renderer from VLG layout
            var lineLE = lineGO.AddComponent<LayoutElement>();
            lineLE.ignoreLayout = true;

            // Node detail popup (hidden by default)
            BuildNodeDetailPopup();

            // Close button — bottom-right
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontSmall, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(1, 0);
            closeBtnRT.sizeDelta = new Vector2(120, 36);
            closeBtnRT.anchoredPosition = new Vector2(-12, 8);

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
                new Color(SporefrontColors.BgSection.r, SporefrontColors.BgSection.g,
                    SporefrontColors.BgSection.b, 0.6f));
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
                "No active research", UIConstants.FontCaption, SporefrontColors.ParchmentShadow);
            var nameLE = activeNameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 180;

            var (bg, fill) = UIHelper.CreateProgressBar(activeResearchBar.transform, 14f,
                SporefrontColors.ParchmentShadow, SporefrontColors.SporeTeal);
            activeProgressFill = fill;
            var barBgLE = bg.gameObject.AddComponent<LayoutElement>();
            barBgLE.flexibleWidth = 1;
            barBgLE.preferredHeight = 14;

            activeProgressLabel = UIHelper.CreateLabel(activeResearchBar.transform,
                "", UIConstants.FontCaption, SporefrontColors.ParchmentShadow, TextAnchor.MiddleRight);
            var progLabelLE = activeProgressLabel.gameObject.AddComponent<LayoutElement>();
            progLabelLE.preferredWidth = 60;

            var cancelBtn = UIHelper.CreateButton(activeResearchBar.transform, "Cancel",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                {
                    OnCancelResearch?.Invoke();
                });
            var cancelLE = cancelBtn.gameObject.AddComponent<LayoutElement>();
            cancelLE.preferredWidth = 60;
            cancelLE.preferredHeight = 26;
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
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            economicTabBtn = UIHelper.CreateButton(tabContainer.transform, "Economic",
                SporefrontColors.BgSurface, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                {
                    currentCategory = ResearchCategory.Economic;
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var econTabLE = economicTabBtn.gameObject.AddComponent<LayoutElement>();
            econTabLE.preferredWidth = 140;
            econTabLE.preferredHeight = 28;

            militaryTabBtn = UIHelper.CreateButton(tabContainer.transform, "Military",
                SporefrontColors.BgSurface, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                {
                    currentCategory = ResearchCategory.Military;
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var milTabLE = militaryTabBtn.gameObject.AddComponent<LayoutElement>();
            milTabLE.preferredWidth = 140;
            milTabLE.preferredHeight = 28;
        }

        // ================================================================
        // Tier Column Headers
        // ================================================================

        private void BuildTierHeaders()
        {
            var headerContainer = UIHelper.CreatePanel(panel.transform, "TierHeaders", Color.clear);
            tierHeaderRT = headerContainer.GetComponent<RectTransform>();
            tierHeaderRT.anchorMin = new Vector2(0, 1);
            tierHeaderRT.anchorMax = new Vector2(1, 1);
            tierHeaderRT.pivot = new Vector2(0.5f, 1);
            tierHeaderRT.offsetMin = new Vector2(6, -130);
            tierHeaderRT.offsetMax = new Vector2(-6, -106);

            var hlg = headerContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = NodeSpacingH;
            hlg.padding = new RectOffset(8, 8, 0, 0);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Spacer: scroll padding + section card padding + track label width
            float spacerWidth = UIConstants.ScrollContentPadding + UIConstants.SectionCardPadding + TrackLabelWidth;
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(headerContainer.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredWidth = spacerWidth;
            spacerLE.preferredHeight = 20;

            // Tier labels
            for (int tier = 1; tier <= 3; tier++)
            {
                var tierLabel = UIHelper.CreateLabel(headerContainer.transform,
                    $"Tier {tier}", UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
                    TextAnchor.MiddleCenter);
                var tierLE = tierLabel.gameObject.AddComponent<LayoutElement>();
                tierLE.preferredWidth = NodeWidth;
                tierLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Node Detail Popup
        // ================================================================

        private void BuildNodeDetailPopup()
        {
            nodeDetailPopup = UIHelper.CreatePanel(panel.transform, "NodeDetailPopup",
                new Color(UIHelper.PanelBg.r, UIHelper.PanelBg.g, UIHelper.PanelBg.b, 0.98f));
            var popupRT = nodeDetailPopup.GetComponent<RectTransform>();
            popupRT.anchorMin = new Vector2(0.5f, 0.5f);
            popupRT.anchorMax = new Vector2(0.5f, 0.5f);
            popupRT.pivot = new Vector2(0, 0.5f);
            popupRT.sizeDelta = new Vector2(PopupWidth, PopupHeight);

            var scroll = UIHelper.CreateScrollView(nodeDetailPopup.transform, "PopupScroll", out nodeDetailContentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 30);
            scrollRT.offsetMax = Vector2.zero;

            // Close popup button
            var closePopup = UIHelper.CreateButton(nodeDetailPopup.transform, "X",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontCaption, () =>
                {
                    selectedNode = null;
                    nodeDetailPopup.SetActive(false);
                });
            var closePopupRT = closePopup.GetComponent<RectTransform>();
            closePopupRT.anchorMin = new Vector2(1, 1);
            closePopupRT.anchorMax = new Vector2(1, 1);
            closePopupRT.pivot = new Vector2(1, 1);
            closePopupRT.sizeDelta = new Vector2(28, 28);
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
            if (pendingLineDrawCoroutine != null)
            {
                StopCoroutine(pendingLineDrawCoroutine);
                pendingLineDrawCoroutine = null;
            }
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

            // Clear tree content (skip line renderer at index 0)
            for (int i = treeContentRT.childCount - 1; i >= 0; i--)
            {
                var child = treeContentRT.GetChild(i);
                if (child.GetComponent<UILineRenderer>() != null) continue;
                Destroy(child.gameObject);
            }

            nodePositions.Clear();

            // Get branches for current category
            var branches = GetBranchesForCategory(currentCategory);

            foreach (var branch in branches)
            {
                BuildBranchSection(branch, player, gameState);
            }

            // Deferred line drawing — cancel any pending draw
            if (pendingLineDrawCoroutine != null)
                StopCoroutine(pendingLineDrawCoroutine);
            lineRenderer.Clear();
            pendingLineDrawCoroutine = StartCoroutine(DeferredDrawLines(player, gameState));

            // Move line renderer to back
            lineRenderer.transform.SetAsFirstSibling();

            // Update node detail if selected
            if (selectedNode.HasValue)
            {
                RebuildNodeDetail(selectedNode.Value, player, gameState);
            }
        }

        private IEnumerator DeferredDrawLines(PlayerState player, GameState gameState)
        {
            yield return null; // wait one frame for layout to settle
            if (!backdrop.activeSelf)
            {
                pendingLineDrawCoroutine = null;
                yield break;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(treeContentRT);
            DrawConnectionLines(player, gameState);
            pendingLineDrawCoroutine = null;
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
            activeNameLabel.color = SporefrontColors.ParchmentShadow;
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
                    ? SporefrontColors.SporeAmber : SporefrontColors.BgSurface;
            }
            if (militaryTabBtn != null)
            {
                var milImg = militaryTabBtn.GetComponent<Image>();
                milImg.color = currentCategory == ResearchCategory.Military
                    ? SporefrontColors.SporeRed : SporefrontColors.BgSurface;
            }
        }

        // ================================================================
        // Branch Section (section card with header + track rows)
        // ================================================================

        private void BuildBranchSection(ResearchBranch branch, PlayerState player, GameState gameState)
        {
            var cardVLG = UIHelper.CreateSectionCard(treeContentRT, "Branch_" + branch);

            // --- Header row ---
            var headerRow = UIHelper.CreateHorizontalRow(cardVLG.transform, BranchHeaderH, 8f);
            headerRow.childForceExpandWidth = false;
            headerRow.childForceExpandHeight = true;
            headerRow.childControlWidth = false;
            headerRow.childControlHeight = true;

            // Branch name
            var branchName = UIHelper.CreateLabel(headerRow.transform, branch.DisplayName(),
                UIConstants.FontSubheader, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, true);
            branchName.fontStyle = FontStyle.Bold;
            var bnLE = branchName.gameObject.AddComponent<LayoutElement>();
            bnLE.preferredWidth = TrackLabelWidth;
            bnLE.preferredHeight = BranchHeaderH;

            // Progress count
            var researchTypes = GetResearchTypesInBranch(branch);
            int completedCount = researchTypes.Count(rt =>
                player.completedResearch.Contains(rt.ToString()));
            int totalCount = researchTypes.Count;

            var progressLabel = UIHelper.CreateLabel(headerRow.transform,
                $"{completedCount}/{totalCount}",
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow, TextAnchor.MiddleLeft);
            var progLE = progressLabel.gameObject.AddComponent<LayoutElement>();
            progLE.preferredWidth = 40;

            // Progress bar
            var (progBg, progFill) = UIHelper.CreateProgressBar(headerRow.transform, 8f,
                SporefrontColors.InkFaded, SporefrontColors.SporeGreen);
            var progBgLE = progBg.gameObject.AddComponent<LayoutElement>();
            progBgLE.preferredHeight = 8;
            progBgLE.flexibleWidth = 1;

            if (totalCount > 0)
            {
                var fillRT = progFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2((float)completedCount / totalCount, 1);
            }

            // Gate building info
            var gateBuilding = branch.GateBuildingType();
            if (gateBuilding.HasValue)
            {
                var gateLabel = UIHelper.CreateLabel(headerRow.transform,
                    $"Requires: {gateBuilding.Value.DisplayName()}",
                    UIConstants.FontCaption, SporefrontColors.SporeAmber, TextAnchor.MiddleRight);
                var gateLE = gateLabel.gameObject.AddComponent<LayoutElement>();
                gateLE.preferredWidth = 140;
            }

            // --- Track rows ---
            var tracks = GetTracksInBranch(branch);
            foreach (var track in tracks)
            {
                BuildTrackRow(cardVLG.transform, track, player, gameState);
            }
        }

        // ================================================================
        // Track Row (single HLG: label + 3 nodes)
        // ================================================================

        private void BuildTrackRow(Transform parent, List<ResearchType> track, PlayerState player, GameState gameState)
        {
            var row = UIHelper.CreateHorizontalRow(parent, TrackRowHeight, NodeSpacingH);
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.padding = new RectOffset(0, 0, 2, 2);

            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = TrackRowHeight;

            // Track label
            string trackName = GetTrackDisplayName(track[0]);
            var trackLabel = UIHelper.CreateLabel(row.transform, trackName,
                UIConstants.FontCaption, SporefrontColors.ParchmentShadow, TextAnchor.MiddleLeft);
            var labelLE = trackLabel.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = TrackLabelWidth;
            labelLE.preferredHeight = NodeHeight;

            // Nodes ordered by tier
            foreach (var rt in track.OrderBy(r => r.Tier()))
            {
                BuildResearchNode(row.transform, rt, player, gameState);
            }
        }

        // ================================================================
        // Research Node
        // ================================================================

        private void BuildResearchNode(Transform parent, ResearchType researchType,
            PlayerState player, GameState gameState)
        {
            var state = GetNodeState(researchType, player, gameState);

            Color nodeBg;
            Color textColor;
            string statusText;

            switch (state)
            {
                case NodeState.Completed:
                    nodeBg = SporefrontColors.SporeGreen;
                    textColor = UIHelper.HudTextColor;
                    statusText = "Completed";
                    break;
                case NodeState.Researching:
                    nodeBg = SporefrontColors.SporeTeal;
                    textColor = UIHelper.HudTextColor;
                    statusText = "Researching...";
                    break;
                case NodeState.Available:
                    nodeBg = SporefrontColors.BgCard;
                    textColor = UIHelper.BodyTextColor;
                    statusText = "Available";
                    break;
                case NodeState.LockedPrereq:
                    nodeBg = new Color(SporefrontColors.BgSection.r, SporefrontColors.BgSection.g,
                        SporefrontColors.BgSection.b, 0.5f);
                    textColor = SporefrontColors.ParchmentShadow;
                    statusText = GetPrereqLockText(researchType, player);
                    break;
                case NodeState.LockedBuilding:
                    nodeBg = new Color(SporefrontColors.BgSection.r, SporefrontColors.BgSection.g,
                        SporefrontColors.BgSection.b, 0.5f);
                    textColor = new Color(SporefrontColors.SporeAmber.r, SporefrontColors.SporeAmber.g,
                        SporefrontColors.SporeAmber.b, 0.7f);
                    statusText = GetBuildingLockText(researchType);
                    break;
                case NodeState.LockedCCLevel:
                    nodeBg = new Color(SporefrontColors.BgSection.r, SporefrontColors.BgSection.g,
                        SporefrontColors.BgSection.b, 0.5f);
                    textColor = new Color(SporefrontColors.SporeAmber.r, SporefrontColors.SporeAmber.g,
                        SporefrontColors.SporeAmber.b, 0.7f);
                    statusText = $"Need: CC Lv.{researchType.CityCenterLevelRequirement()}";
                    break;
                default:
                    nodeBg = SporefrontColors.BgSection;
                    textColor = SporefrontColors.ParchmentShadow;
                    statusText = "Locked";
                    break;
            }

            var nodePanel = UIHelper.CreatePanel(parent, "Node_" + researchType, nodeBg);
            var nodeLE = nodePanel.AddComponent<LayoutElement>();
            nodeLE.preferredWidth = NodeWidth;
            nodeLE.preferredHeight = NodeHeight;

            var nodeVLG = nodePanel.AddComponent<VerticalLayoutGroup>();
            nodeVLG.padding = new RectOffset(6, 6, 4, 4);
            nodeVLG.spacing = 2;
            nodeVLG.childForceExpandWidth = true;
            nodeVLG.childForceExpandHeight = false;
            nodeVLG.childControlWidth = true;
            nodeVLG.childControlHeight = false;

            var nameLabel = UIHelper.CreateLabel(nodePanel.transform,
                researchType.DisplayName(), UIConstants.FontCaption, textColor, TextAnchor.MiddleCenter);
            var nameNLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameNLE.preferredHeight = 22;

            // Status indicator
            var statusLabel = UIHelper.CreateLabel(nodePanel.transform, statusText, 11,
                textColor, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 13;

            // Store node position for connection lines
            var nodeRT = nodePanel.GetComponent<RectTransform>();
            nodePositions[researchType] = nodeRT;

            // Click handler
            var capturedType = researchType;
            var clickBtn = nodePanel.AddComponent<Button>();
            clickBtn.transition = Selectable.Transition.None;
            clickBtn.onClick.AddListener(() =>
            {
                selectedNode = capturedType;
                nodeDetailPopup.SetActive(true);
                PositionPopupNearNode(nodeRT);
                RebuildNodeDetail(capturedType, GameEngine.Instance.GetGameState().GetPlayer(localPlayerID),
                    GameEngine.Instance.GetGameState());
            });
        }

        // ================================================================
        // Connection Lines
        // ================================================================

        private void DrawConnectionLines(PlayerState player, GameState gameState)
        {
            var allTypes = GetResearchTypesInBranch(GetBranchesForCategory(currentCategory));

            foreach (var rt in allTypes)
            {
                var prereqs = rt.Prerequisites();
                if (prereqs.Length == 0) continue;

                RectTransform targetRT;
                if (!nodePositions.TryGetValue(rt, out targetRT)) continue;

                foreach (var prereq in prereqs)
                {
                    RectTransform sourceRT;
                    if (!nodePositions.TryGetValue(prereq, out sourceRT)) continue;

                    // Get positions relative to the content container
                    Vector2 sourcePos = GetNodeRightEdge(sourceRT);
                    Vector2 targetPos = GetNodeLeftEdge(targetRT);

                    Color lineColor = GetLineColor(prereq, rt, player);
                    lineRenderer.AddLPath(sourcePos, targetPos, lineColor);
                }
            }
        }

        private Vector2 GetNodeRightEdge(RectTransform nodeRT)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(treeContentRT, nodeRT);
            return new Vector2((float)bounds.max.x, (float)bounds.center.y);
        }

        private Vector2 GetNodeLeftEdge(RectTransform nodeRT)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(treeContentRT, nodeRT);
            return new Vector2((float)bounds.min.x, (float)bounds.center.y);
        }

        private Color GetLineColor(ResearchType source, ResearchType target, PlayerState player)
        {
            bool sourceCompleted = player.completedResearch.Contains(source.ToString());
            bool targetCompleted = player.completedResearch.Contains(target.ToString());

            if (sourceCompleted && targetCompleted)
                return new Color(SporefrontColors.SporeGreen.r, SporefrontColors.SporeGreen.g,
                    SporefrontColors.SporeGreen.b, 0.6f);

            if (sourceCompleted)
            {
                // Check if target is available (all prereqs met)
                var prereqs = target.Prerequisites();
                bool allPrereqsMet = prereqs.All(p => player.completedResearch.Contains(p.ToString()));
                if (allPrereqsMet)
                    return new Color(SporefrontColors.SporeTeal.r, SporefrontColors.SporeTeal.g,
                        SporefrontColors.SporeTeal.b, 0.8f);
                else
                    return new Color(SporefrontColors.ParchmentShadow.r, SporefrontColors.ParchmentShadow.g,
                        SporefrontColors.ParchmentShadow.b, 0.4f);
            }

            return new Color(SporefrontColors.ParchmentShadow.r, SporefrontColors.ParchmentShadow.g,
                SporefrontColors.ParchmentShadow.b, 0.2f);
        }

        // ================================================================
        // Popup Positioning
        // ================================================================

        private void PositionPopupNearNode(RectTransform nodeRT)
        {
            var popupRT = nodeDetailPopup.GetComponent<RectTransform>();

            // Convert node center to panel local space
            Vector3 worldPos = nodeRT.TransformPoint(nodeRT.rect.center);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panelRT, RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out localPos);

            // Default: right of node
            popupRT.anchorMin = new Vector2(0.5f, 0.5f);
            popupRT.anchorMax = new Vector2(0.5f, 0.5f);
            popupRT.pivot = new Vector2(0, 0.5f);
            float xPos = localPos.x + NodeWidth / 2f + 10f;
            float yPos = localPos.y;

            // Edge clamp
            float panelW = panelRT.rect.width;
            float panelH = panelRT.rect.height;

            if (xPos + PopupWidth > panelW / 2f)
            {
                popupRT.pivot = new Vector2(1, 0.5f);
                xPos = localPos.x - NodeWidth / 2f - 10f;
            }
            yPos = Mathf.Clamp(yPos, -panelH / 2f + PopupHeight / 2f + 10f,
                panelH / 2f - PopupHeight / 2f - 10f);

            popupRT.anchoredPosition = new Vector2(xPos, yPos);
        }

        // ================================================================
        // Node Detail Popup Content
        // ================================================================

        private void RebuildNodeDetail(ResearchType researchType, PlayerState player, GameState gameState)
        {
            for (int i = nodeDetailContentRT.childCount - 1; i >= 0; i--)
                Destroy(nodeDetailContentRT.GetChild(i).gameObject);

            var state = GetNodeState(researchType, player, gameState);

            // Name
            var nameLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                researchType.DisplayName(),
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 26;

            // Description
            var descLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                researchType.Description(), UIConstants.FontCaption, SporefrontColors.ParchmentShadow,
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
                $"Cost: {string.Join("  ", costParts)}", UIConstants.FontCaption, UIHelper.BodyTextColor);
            costLabel.supportRichText = true;
            var costLE = costLabel.gameObject.AddComponent<LayoutElement>();
            costLE.preferredHeight = 20;

            // Research time
            double researchTime = researchType.ResearchTime();
            int timeSec = (int)researchTime;
            string timeStr = timeSec >= 60 ? $"{timeSec / 60}m {timeSec % 60}s" : $"{timeSec}s";
            var timeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                $"Time: {timeStr}", UIConstants.FontCaption, UIHelper.BodyTextColor);
            var timeLE = timeLabel.gameObject.AddComponent<LayoutElement>();
            timeLE.preferredHeight = 20;

            // Prerequisites
            var prereqs = researchType.Prerequisites();
            if (prereqs.Length > 0)
            {
                var prereqNames = prereqs.Select(p => p.DisplayName()).ToArray();
                var prereqLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    $"Requires: {string.Join(", ", prereqNames)}", UIConstants.FontCaption, SporefrontColors.ParchmentShadow);
                var prereqLE = prereqLabel.gameObject.AddComponent<LayoutElement>();
                prereqLE.preferredHeight = 18;
            }

            // Building requirement
            var buildingReq = researchType.BuildingRequirement();
            if (buildingReq.HasValue)
            {
                var bldgLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    $"Building: {buildingReq.Value.buildingType.DisplayName()} Lv.{buildingReq.Value.level}",
                    UIConstants.FontCaption, SporefrontColors.ParchmentShadow);
                var bldgLE = bldgLabel.gameObject.AddComponent<LayoutElement>();
                bldgLE.preferredHeight = 18;
            }

            // CC level requirement
            int ccReq = researchType.CityCenterLevelRequirement();
            if (ccReq > 1)
            {
                var ccLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    $"City Center: Level {ccReq}", UIConstants.FontCaption, SporefrontColors.ParchmentShadow);
                var ccLE = ccLabel.gameObject.AddComponent<LayoutElement>();
                ccLE.preferredHeight = 18;
            }

            UIHelper.CreateDivider(nodeDetailContentRT);

            // Bonuses
            var bonuses = researchType.Bonuses();
            if (bonuses.Length > 0)
            {
                var bonusHeader = UIHelper.CreateLabel(nodeDetailContentRT, "Bonuses:",
                    UIConstants.FontCaption, UIHelper.HeaderTextColor, TextAnchor.MiddleLeft, false);
                var bonusHeaderLE = bonusHeader.gameObject.AddComponent<LayoutElement>();
                bonusHeaderLE.preferredHeight = 18;

                foreach (var bonus in bonuses)
                {
                    var bonusLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"  {bonus.DisplayString}", UIConstants.FontCaption, SporefrontColors.SporeGreen);
                    var bLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
                    bLE.preferredHeight = 16;
                }
            }

            UIHelper.CreateDivider(nodeDetailContentRT);

            // Action / status area
            switch (state)
            {
                case NodeState.Available:
                    bool canAfford = player != null && player.CanAfford(cost);
                    var capturedType = researchType;

                    var startBtn = UIHelper.CreateButton(nodeDetailContentRT, "Start Research",
                        canAfford ? SporefrontColors.SporeGreen : SporefrontColors.ParchmentShadow,
                        canAfford ? UIHelper.HudTextColor : SporefrontColors.ParchmentShadow, UIConstants.FontSmall, () =>
                        {
                            OnStartResearch?.Invoke(capturedType);
                            selectedNode = null;
                            nodeDetailPopup.SetActive(false);
                        });
                    startBtn.interactable = canAfford;
                    var startLE = startBtn.gameObject.AddComponent<LayoutElement>();
                    startLE.preferredHeight = 34;

                    if (!canAfford)
                    {
                        var affordLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                            "Insufficient resources", UIConstants.FontCaption, SporefrontColors.SporeRed,
                            TextAnchor.MiddleCenter);
                        var affordLE = affordLabel.gameObject.AddComponent<LayoutElement>();
                        affordLE.preferredHeight = 16;
                    }
                    break;

                case NodeState.Completed:
                    var completeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        "Research Complete", 13, SporefrontColors.SporeGreen,
                        TextAnchor.MiddleCenter);
                    var compLE = completeLabel.gameObject.AddComponent<LayoutElement>();
                    compLE.preferredHeight = 28;
                    break;

                case NodeState.Researching:
                    var researchingLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        "Currently Researching...", 13, SporefrontColors.SporeTeal,
                        TextAnchor.MiddleCenter);
                    var resLE = researchingLabel.gameObject.AddComponent<LayoutElement>();
                    resLE.preferredHeight = 28;
                    break;

                case NodeState.LockedPrereq:
                    var prereqLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        GetPrereqLockText(researchType, player), UIConstants.FontCaption,
                        SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
                    var prereqLockLE = prereqLockLabel.gameObject.AddComponent<LayoutElement>();
                    prereqLockLE.preferredHeight = 28;
                    break;

                case NodeState.LockedBuilding:
                    var bldgLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        GetBuildingLockText(researchType), UIConstants.FontCaption,
                        SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
                    var bldgLockLE = bldgLockLabel.gameObject.AddComponent<LayoutElement>();
                    bldgLockLE.preferredHeight = 28;
                    break;

                case NodeState.LockedCCLevel:
                    var ccLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"Need: City Center Level {researchType.CityCenterLevelRequirement()}",
                        UIConstants.FontCaption, SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
                    var ccLockLE = ccLockLabel.gameObject.AddComponent<LayoutElement>();
                    ccLockLE.preferredHeight = 28;
                    break;
            }
        }

        // ================================================================
        // Enhanced Node State
        // ================================================================

        private enum NodeState { LockedPrereq, LockedBuilding, LockedCCLevel, Available, Researching, Completed }

        private NodeState GetNodeState(ResearchType researchType, PlayerState player, GameState gameState)
        {
            if (player == null) return NodeState.LockedPrereq;

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
                    return NodeState.LockedPrereq;
            }

            // Check building requirement
            var buildingReq = researchType.BuildingRequirement();
            if (buildingReq.HasValue)
            {
                bool hasBldg = false;
                var buildings = gameState.GetBuildingsForPlayer(localPlayerID);
                foreach (var bld in buildings)
                {
                    if (bld.buildingType == buildingReq.Value.buildingType &&
                        bld.level >= buildingReq.Value.level &&
                        (bld.state == BuildingState.Completed || bld.state == BuildingState.Upgrading))
                    {
                        hasBldg = true;
                        break;
                    }
                }
                if (!hasBldg) return NodeState.LockedBuilding;
            }

            // Check CC level
            int ccReq = researchType.CityCenterLevelRequirement();
            if (ccReq > 1)
            {
                int ccLevel = gameState.GetCityCenterLevel(localPlayerID);
                if (ccLevel < ccReq) return NodeState.LockedCCLevel;
            }

            return NodeState.Available;
        }

        // ================================================================
        // Lock Text Helpers
        // ================================================================

        private string GetPrereqLockText(ResearchType researchType, PlayerState player)
        {
            var prereqs = researchType.Prerequisites();
            foreach (var prereq in prereqs)
            {
                if (!player.completedResearch.Contains(prereq.ToString()))
                    return $"Need: {prereq.DisplayName()}";
            }
            return "Locked";
        }

        private string GetBuildingLockText(ResearchType researchType)
        {
            var req = researchType.BuildingRequirement();
            if (req.HasValue)
                return $"Need: {req.Value.buildingType.DisplayName()}";
            return "Need: Building";
        }

        // ================================================================
        // Track Grouping Helpers
        // ================================================================

        private string GetTrackName(ResearchType rt)
        {
            string name = rt.ToString();
            if (name.EndsWith("III")) return name.Substring(0, name.Length - 3);
            if (name.EndsWith("II")) return name.Substring(0, name.Length - 2);
            if (name.EndsWith("I")) return name.Substring(0, name.Length - 1);
            return name;
        }

        private List<List<ResearchType>> GetTracksInBranch(ResearchBranch branch)
        {
            var branchTypes = GetResearchTypesInBranch(branch);
            var trackMap = new Dictionary<string, List<ResearchType>>();
            var trackOrder = new List<string>();

            foreach (var rt in branchTypes)
            {
                string trackName = GetTrackName(rt);
                if (!trackMap.ContainsKey(trackName))
                {
                    trackMap[trackName] = new List<ResearchType>();
                    trackOrder.Add(trackName);
                }
                trackMap[trackName].Add(rt);
            }

            var result = new List<List<ResearchType>>();
            foreach (var tn in trackOrder)
            {
                result.Add(trackMap[tn].OrderBy(r => r.Tier()).ToList());
            }
            return result;
        }

        private string GetTrackDisplayName(ResearchType tierOneType)
        {
            string displayName = tierOneType.DisplayName();
            // Strip trailing " I" suffix
            if (displayName.EndsWith(" I") && !displayName.EndsWith(" II") && !displayName.EndsWith(" III"))
                return displayName.Substring(0, displayName.Length - 2);
            return displayName;
        }

        // ================================================================
        // Branch / Category Helpers
        // ================================================================

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

        private List<ResearchType> GetResearchTypesInBranch(ResearchBranch[] branches)
        {
            var allTypes = (ResearchType[])Enum.GetValues(typeof(ResearchType));
            var branchSet = new HashSet<ResearchBranch>(branches);
            return allTypes.Where(rt => branchSet.Contains(rt.Branch()))
                           .OrderBy(rt => rt.Tier())
                           .ThenBy(rt => rt.ToString())
                           .ToList();
        }
    }
}
