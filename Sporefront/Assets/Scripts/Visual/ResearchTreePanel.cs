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
using UnityEngine.EventSystems;
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

        // Right detail panel (persistent, not a popup)
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

        private const float NodeMinWidth = 110f;
        private const float NodeHeight = 56f;
        private const float NodeSpacingH = 6f;
        private const float TrackRowHeight = 64f;
        private const float TrackLabelWidth = 80f;
        private const float BranchHeaderH = 52f;
        private const float DetailPanelWidth = 495f;

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
            panel = UIHelper.CreatePanel(backdrop.transform, "ResearchTreePanel", UIHelper.PanelParchmentBg);
            panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.1f, 0.1f);
            panelRT.anchorMax = new Vector2(0.9f, 0.9f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            PopupTendrilDecorator.Attach(panelRT);

            // Title
            var titleLabel = UIHelper.CreateLabel(panel.transform, "Research Tree",
                UIHelper.DefaultHeaderFontSize + 2, UIHelper.InkHeaderText,
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

            // Body container — horizontal split: left scroll + right detail panel
            var bodyContainer = new GameObject("ResearchBody", typeof(RectTransform));
            bodyContainer.transform.SetParent(panel.transform, false);
            var bodyRT = bodyContainer.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0, 0);
            bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.offsetMin = new Vector2(6, 48);
            bodyRT.offsetMax = new Vector2(-6, -106);

            var bodyHLG = bodyContainer.AddComponent<HorizontalLayoutGroup>();
            bodyHLG.spacing = 0;
            bodyHLG.padding = new RectOffset(0, 0, 0, 0);
            bodyHLG.childForceExpandWidth = false;
            bodyHLG.childForceExpandHeight = true;
            bodyHLG.childControlWidth = true;
            bodyHLG.childControlHeight = true;

            // Left — scrollable tree area
            treeScroll = UIHelper.CreateScrollView(bodyContainer.transform, "TreeScroll", out treeContentRT);
            treeScroll.horizontal = false;
            treeScroll.vertical = true;
            var treeScrollLE = treeScroll.gameObject.AddComponent<LayoutElement>();
            treeScrollLE.flexibleWidth = 1f;
            treeScrollLE.flexibleHeight = 1f;

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

            // Right — persistent detail panel
            var detailPanel = UIHelper.CreatePanel(bodyContainer.transform, "DetailPanel", SporefrontColors.ParchmentDark);
            var detailPanelLE = detailPanel.AddComponent<LayoutElement>();
            detailPanelLE.preferredWidth = DetailPanelWidth;
            detailPanelLE.flexibleHeight = 1f;

            // Plain VLG content container — no scroll needed
            var detailContent = new GameObject("DetailContent", typeof(RectTransform));
            detailContent.transform.SetParent(detailPanel.transform, false);
            nodeDetailContentRT = detailContent.GetComponent<RectTransform>();
            UIHelper.StretchFull(nodeDetailContentRT);

            var detailVLG = detailContent.AddComponent<VerticalLayoutGroup>();
            detailVLG.spacing = 0;
            detailVLG.padding = new RectOffset(20, 20, 16, 16);
            detailVLG.childForceExpandWidth = true;
            detailVLG.childForceExpandHeight = false;
            detailVLG.childControlWidth = true;
            detailVLG.childControlHeight = false;

            // Ink-styled close annotation
            var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Hide);
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(12, 4);
            closeBtnRT.offsetMax = new Vector2(-12, 36);

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
                new Color(SporefrontColors.ParchmentDeep.r, SporefrontColors.ParchmentDeep.g,
                    SporefrontColors.ParchmentDeep.b, 0.7f));
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
                "No active research", UIConstants.FontCaption, UIHelper.InkMutedText);
            var nameLE = activeNameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 180;

            var (bg, fill) = UIHelper.CreateInkProgressBar(activeResearchBar.transform, 14f,
                SporefrontColors.SporeTeal);
            activeProgressFill = fill;
            var barBgLE = bg.gameObject.AddComponent<LayoutElement>();
            barBgLE.flexibleWidth = 1;
            barBgLE.preferredHeight = 14;

            activeProgressLabel = UIHelper.CreateLabel(activeResearchBar.transform,
                "", UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleRight);
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
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontSmall, () =>
                {
                    currentCategory = ResearchCategory.Economic;
                    selectedNode = null;
                    Rebuild(GameEngine.Instance.GetGameState());
                });
            var econTabLE = economicTabBtn.gameObject.AddComponent<LayoutElement>();
            econTabLE.preferredWidth = 140;
            econTabLE.preferredHeight = 28;

            militaryTabBtn = UIHelper.CreateButton(tabContainer.transform, "Military",
                SporefrontColors.ParchmentDeep, UIHelper.InkBodyText, UIConstants.FontSmall, () =>
                {
                    currentCategory = ResearchCategory.Military;
                    selectedNode = null;
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
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // Spacer: scroll padding + section card padding + track label width
            float spacerWidth = UIConstants.ScrollContentPadding + UIConstants.SectionCardPadding + TrackLabelWidth;
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(headerContainer.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredWidth = spacerWidth;
            spacerLE.preferredHeight = 20;

            // Tier labels — flexible width to match card columns
            for (int tier = 1; tier <= 3; tier++)
            {
                var tierLabel = UIHelper.CreateLabel(headerContainer.transform,
                    $"Tier {tier}", UIConstants.FontCaption, UIHelper.InkMutedText,
                    TextAnchor.MiddleCenter);
                var tierLE = tierLabel.gameObject.AddComponent<LayoutElement>();
                tierLE.flexibleWidth = 1;
                tierLE.minWidth = NodeMinWidth;
                tierLE.preferredHeight = 20;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(GameState gameState)
        {
            selectedNode = null;
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

            // Always refresh detail panel — empty state when nothing selected
            if (selectedNode.HasValue)
                RebuildNodeDetail(selectedNode.Value, player, gameState);
            else
                RebuildNodeDetailEmpty();
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
                    activeNameLabel.color = UIHelper.InkBodyText;

                    var fillRT = activeProgressFill.GetComponent<RectTransform>();
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01((float)progress), 1);

                    int remSec = (int)remaining;
                    activeProgressLabel.text = remSec > 60 ? $"{remSec / 60}m {remSec % 60}s" : $"{remSec}s";
                    return;
                }
            }

            activeNameLabel.text = "No active research";
            activeNameLabel.color = UIHelper.InkMutedText;
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
                    ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDeep;
            }
            if (militaryTabBtn != null)
            {
                var milImg = militaryTabBtn.GetComponent<Image>();
                milImg.color = currentCategory == ResearchCategory.Military
                    ? SporefrontColors.SporeRed : SporefrontColors.ParchmentDeep;
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

            // Branch name — large category title
            var branchName = UIHelper.CreateLabel(headerRow.transform, branch.DisplayName(),
                UIConstants.FontTitle + 10, UIHelper.InkHeaderText, TextAnchor.MiddleLeft, true);
            branchName.fontStyle = FontStyle.BoldAndItalic;
            var bnLE = branchName.gameObject.AddComponent<LayoutElement>();
            bnLE.preferredWidth = 200;
            bnLE.preferredHeight = BranchHeaderH;

            // Progress count
            var researchTypes = GetResearchTypesInBranch(branch);
            int completedCount = researchTypes.Count(rt =>
                player.completedResearch.Contains(rt.ToString()));
            int totalCount = researchTypes.Count;

            var progressLabel = UIHelper.CreateLabel(headerRow.transform,
                $"{completedCount}/{totalCount}",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
            var progLE = progressLabel.gameObject.AddComponent<LayoutElement>();
            progLE.preferredWidth = 40;

            // Progress bar
            var (progBg, progFill) = UIHelper.CreateInkProgressBar(headerRow.transform, 8f,
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
            row.childControlWidth = true;
            row.childControlHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.padding = new RectOffset(0, 0, 2, 2);

            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = TrackRowHeight;

            // Progressive reveal: show completed tiers + first non-completed tier
            var visibleCards = GetVisibleCards(track, player, gameState);

            foreach (var rt in visibleCards)
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
            bool isLocked = state == NodeState.LockedPrereq
                         || state == NodeState.LockedBuilding
                         || state == NodeState.LockedCCLevel;

            // ── Per-state palette ──────────────────────────────────────
            Color nodeBg, titleColor, borderColor;
            float borderWidth, nodeAlpha;

            switch (state)
            {
                case NodeState.Available:
                    // Warm mid-parchment — distinct from the panel bg without being harsh white
                    nodeBg      = SporefrontColors.ParchmentMid;
                    titleColor  = SporefrontColors.InkDark;
                    borderColor = SporefrontColors.InkDark;
                    borderWidth = 2f;
                    nodeAlpha   = 1f;
                    break;

                case NodeState.Completed:
                    nodeBg      = SporefrontColors.ParchmentDeep;
                    titleColor  = SporefrontColors.InkMid;
                    borderColor = SporefrontColors.SporeAmber;
                    borderWidth = 2f;
                    nodeAlpha   = 0.88f;
                    break;

                case NodeState.Researching:
                    nodeBg      = SporefrontColors.ParchmentMid;
                    titleColor  = SporefrontColors.InkDark;
                    borderColor = SporefrontColors.SporeRed;
                    borderWidth = 2.5f;
                    nodeAlpha   = 1f;
                    break;

                default: // LockedPrereq / LockedBuilding / LockedCCLevel
                    // Darker base so locked reads as recessed/unavailable
                    nodeBg      = SporefrontColors.ParchmentDark;
                    titleColor  = SporefrontColors.InkFaded;
                    borderColor = new Color(SporefrontColors.InkFaded.r,
                                           SporefrontColors.InkFaded.g,
                                           SporefrontColors.InkFaded.b, 0.35f);
                    borderWidth = 1.5f;
                    nodeAlpha   = 0.65f;
                    break;
            }

            // ── Card panel ────────────────────────────────────────────
            var nodePanel = UIHelper.CreatePanel(parent, "Node_" + researchType, nodeBg);
            var nodeLE    = nodePanel.AddComponent<LayoutElement>();
            nodeLE.flexibleWidth   = 1f;       // expand to fill available row space
            nodeLE.minWidth        = NodeMinWidth;
            nodeLE.preferredHeight = NodeHeight;

            var outline = nodePanel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor    = borderColor;
                outline.effectDistance = new Vector2(borderWidth, -borderWidth);
            }

            var cg = nodePanel.AddComponent<CanvasGroup>();
            cg.alpha = nodeAlpha;
            if (isLocked) { cg.blocksRaycasts = false; cg.interactable = false; }

            if (state == NodeState.Available)
            {
                var shadow = nodePanel.AddComponent<Shadow>();
                shadow.effectColor    = new Color(0.17f, 0.14f, 0.09f, 0.28f);
                shadow.effectDistance = new Vector2(0f, -2f);
            }
            if (state == NodeState.Researching)
            {
                var glow = nodePanel.AddComponent<Shadow>();
                glow.effectColor    = new Color(SporefrontColors.SporeRed.r,
                    SporefrontColors.SporeRed.g, SporefrontColors.SporeRed.b, 0.4f);
                glow.effectDistance = new Vector2(0f, -3f);
            }

            // ── Selected state — thicker outline ─────────────────────
            if (selectedNode.HasValue && selectedNode.Value == researchType && outline != null)
                outline.effectDistance = new Vector2(
                    outline.effectDistance.x + 1.5f,
                    outline.effectDistance.y - 1.5f);

            // ── Horizontal layout group (wide card) ───────────────────
            var hlg = nodePanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = 4f;
            hlg.padding              = new RectOffset(8, 8, 0, 0);
            hlg.childAlignment       = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth    = false;
            hlg.childControlHeight   = true;

            // ── Title label (centered, bold) ──────────────────────────
            string displayText = state == NodeState.Completed
                ? $"{researchType.DisplayName()}  \u2713"
                : researchType.DisplayName();
            var titleLbl = UIHelper.CreateLabel(nodePanel.transform,
                displayText, UIConstants.FontSubheader,
                titleColor, TextAnchor.MiddleCenter);
            titleLbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleLbl.fontStyle = FontStyle.Bold;
            titleLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // ── Progress bar — In Progress, pinned to card bottom ─────
            if (state == NodeState.Researching)
            {
                float progress = 0f;
                if (player.activeResearchStartTime.HasValue)
                {
                    var ar = new ActiveResearch(researchType, player.activeResearchStartTime.Value);
                    progress = Mathf.Clamp01((float)ar.GetProgress(gameState.currentTime));
                }

                var barGO  = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
                barGO.transform.SetParent(nodePanel.transform, false);
                var barImg = barGO.GetComponent<Image>();
                barImg.color         = SporefrontColors.SporeRed;
                barImg.raycastTarget = false;
                var barRT = barGO.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0, 0);
                barRT.anchorMax = new Vector2(progress, 0);
                barRT.pivot     = new Vector2(0, 0);
                barRT.offsetMin = Vector2.zero;
                barRT.offsetMax = new Vector2(0, 4);
                barGO.AddComponent<LayoutElement>().ignoreLayout = true;

                var pulser = nodePanel.AddComponent<TechCardBorderPulse>();
                pulser.SetTarget(outline);
            }

            // ── Store RT for connection lines ─────────────────────────
            var nodeRT = nodePanel.GetComponent<RectTransform>();
            nodePositions[researchType] = nodeRT;

            // ── Click + hover (non-locked only) ───────────────────────
            if (!isLocked)
            {
                var capturedType = researchType;
                var clickBtn = nodePanel.AddComponent<Button>();
                clickBtn.transition = Selectable.Transition.None;
                clickBtn.onClick.AddListener(() =>
                {
                    selectedNode = capturedType;
                    Rebuild(GameEngine.Instance.GetGameState());
                });

                if (state == NodeState.Available)
                {
                    var hover = nodePanel.AddComponent<TechCardHover>();
                    hover.SetTarget(nodePanel.GetComponent<Image>(), outline);
                }
            }
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
                    return new Color(SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                        SporefrontColors.InkBorder.b, 0.5f);
            }

            return new Color(SporefrontColors.InkBorder.r, SporefrontColors.InkBorder.g,
                SporefrontColors.InkBorder.b, 0.3f);
        }

        // ================================================================
        // Node Detail Panel Content
        // ================================================================

        private void RebuildNodeDetailEmpty()
        {
            for (int i = nodeDetailContentRT.childCount - 1; i >= 0; i--)
                Destroy(nodeDetailContentRT.GetChild(i).gameObject);

            var hint = UIHelper.CreateLabel(nodeDetailContentRT,
                "Select a tech\nto view details",
                UIConstants.FontSubheader, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            hint.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void RebuildNodeDetail(ResearchType researchType, PlayerState player, GameState gameState)
        {
            for (int i = nodeDetailContentRT.childCount - 1; i >= 0; i--)
                Destroy(nodeDetailContentRT.GetChild(i).gameObject);

            var state = GetNodeState(researchType, player, gameState);

            // ── Tech name ─────────────────────────────────────────────
            var nameLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                researchType.DisplayName(),
                UIConstants.FontTitle, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            nameLabel.fontStyle = FontStyle.Bold;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 40;

            UIHelper.CreateDivider(nodeDetailContentRT);

            // ── EFFECTS section ───────────────────────────────────────
            CreateSectionLabel(nodeDetailContentRT, "EFFECTS");
            var bonuses = researchType.Bonuses();
            foreach (var bonus in bonuses)
            {
                var effectLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                    bonus.DisplayString, UIConstants.FontBody,
                    UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                effectLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }

            // ── COST section ──────────────────────────────────────────
            CreateSectionLabel(nodeDetailContentRT, "COST");
            var cost = researchType.Cost();
            foreach (var kvp in cost)
            {
                if (kvp.Value > 0)
                {
                    var costLine = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"{UIHelper.ResourceIcon(kvp.Key)} {kvp.Value} {kvp.Key}",
                        UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                    costLine.supportRichText = true;
                    costLine.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                }
            }

            // Research time (under COST)
            double researchTime = researchType.ResearchTime();
            int timeSec = (int)researchTime;
            string timeStr = timeSec >= 60 ? $"{timeSec / 60}m {timeSec % 60}s" : $"{timeSec}s";
            var timeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                $"{timeStr} research time", UIConstants.FontBody, UIHelper.InkMutedText,
                TextAnchor.MiddleLeft);
            timeLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            // ── PREREQUISITES section ─────────────────────────────────
            var prereqs = researchType.Prerequisites();
            var buildingReq = researchType.BuildingRequirement();
            int ccReq = researchType.CityCenterLevelRequirement();
            bool hasPrereqs = prereqs.Length > 0 || buildingReq.HasValue || ccReq > 1;

            CreateSectionLabel(nodeDetailContentRT, "PREREQUISITES");
            if (!hasPrereqs)
            {
                var noneLbl = UIHelper.CreateLabel(nodeDetailContentRT,
                    "None", UIConstants.FontBody, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
                noneLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            }
            else
            {
                if (prereqs.Length > 0)
                {
                    foreach (var p in prereqs)
                    {
                        var pLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                            p.DisplayName(), UIConstants.FontBody,
                            UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                        pLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                    }
                }
                if (buildingReq.HasValue)
                {
                    var bldgLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"{buildingReq.Value.buildingType.DisplayName()} Lv.{buildingReq.Value.level}",
                        UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                    bldgLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                }
                if (ccReq > 1)
                {
                    var ccLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"City Center Level {ccReq}",
                        UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
                    ccLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                }
            }

            // ── Spacer ────────────────────────────────────────────────
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(nodeDetailContentRT, false);
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // ── Action / status area ──────────────────────────────────
            switch (state)
            {
                case NodeState.Available:
                    bool canAfford = player != null && player.CanAfford(cost);
                    var capturedType = researchType;

                    var startBtn = UIHelper.CreateButton(nodeDetailContentRT, "Start Research",
                        canAfford ? SporefrontColors.SporeGreen : SporefrontColors.ParchmentDeep,
                        canAfford ? UIHelper.HudTextColor : UIHelper.InkMutedText, UIConstants.FontSubheader, () =>
                        {
                            OnStartResearch?.Invoke(capturedType);
                            selectedNode = null;
                        });
                    startBtn.interactable = canAfford;
                    var startLE = startBtn.gameObject.AddComponent<LayoutElement>();
                    startLE.preferredHeight = 48;

                    if (!canAfford)
                    {
                        var affordLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                            "Insufficient resources", UIConstants.FontBody, SporefrontColors.SporeRed,
                            TextAnchor.MiddleCenter);
                        affordLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                    }
                    break;

                case NodeState.Completed:
                    var completeLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        "Research Complete", UIConstants.FontBody, SporefrontColors.SporeGreen,
                        TextAnchor.MiddleCenter);
                    completeLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                    break;

                case NodeState.Researching:
                    var researchingLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        "Currently Researching...", UIConstants.FontBody, SporefrontColors.SporeTeal,
                        TextAnchor.MiddleCenter);
                    researchingLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                    break;

                case NodeState.LockedPrereq:
                    var prereqLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        GetPrereqLockText(researchType, player), UIConstants.FontBody,
                        SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
                    prereqLockLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                    break;

                case NodeState.LockedBuilding:
                    var bldgLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        GetBuildingLockText(researchType), UIConstants.FontBody,
                        SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
                    bldgLockLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                    break;

                case NodeState.LockedCCLevel:
                    var ccLockLabel = UIHelper.CreateLabel(nodeDetailContentRT,
                        $"Need: City Center Level {researchType.CityCenterLevelRequirement()}",
                        UIConstants.FontBody, SporefrontColors.SporeAmber, TextAnchor.MiddleCenter);
                    ccLockLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                    break;
            }
        }

        // ================================================================
        // Detail Panel Helpers
        // ================================================================

        private void CreateSectionLabel(RectTransform parent, string text)
        {
            var label = UIHelper.CreateLabel(parent,
                text, UIConstants.FontSmall, UIHelper.InkMutedText, TextAnchor.LowerLeft);
            label.fontStyle = FontStyle.Normal;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 32; // includes top gap as section separator
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

        // Returns the tier-chain cards that should be visible: all completed tiers
        // plus the first non-completed tier. Higher locked tiers stay hidden.
        private List<ResearchType> GetVisibleCards(List<ResearchType> tierChain, PlayerState player, GameState gameState)
        {
            var visible = new List<ResearchType>();
            foreach (var tech in tierChain)
            {
                visible.Add(tech);
                if (GetNodeState(tech, player, gameState) != NodeState.Completed)
                    break;
            }
            return visible;
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

    // ================================================================
    // Tech Card Hover — Available state hover effect
    // ================================================================

    public class TechCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image bgImage;
        private Outline outline;
        private Color normalBg;
        private Color hoverBg;

        public void SetTarget(Image bg, Outline outlineComp)
        {
            bgImage  = bg;
            outline  = outlineComp;
            normalBg = SporefrontColors.ParchmentMid;   // matches Available card bg
            hoverBg  = SporefrontColors.ParchmentDark;  // slightly deeper on hover
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (bgImage != null) bgImage.color = hoverBg;
            if (outline != null) outline.effectDistance = new Vector2(3f, -3f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (bgImage != null) bgImage.color = normalBg;
            if (outline != null) outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    // ================================================================
    // Tech Card Border Pulse — In Progress animated border
    // ================================================================

    public class TechCardBorderPulse : MonoBehaviour
    {
        private Outline outline;
        private float pulseSpeed = 1.8f;

        public void SetTarget(Outline outlineComp)
        {
            outline = outlineComp;
        }

        private void Update()
        {
            if (outline == null) return;
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSpeed)) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.6f, 1f, t);
            outline.effectColor = new Color(
                SporefrontColors.SporeRed.r,
                SporefrontColors.SporeRed.g,
                SporefrontColors.SporeRed.b,
                alpha);
        }
    }
}
