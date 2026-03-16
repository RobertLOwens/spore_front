// ============================================================================
// FILE: Visual/MenuTransitionController.cs
// PURPOSE: Orchestrates the tendril-based sliding transition between
//          MainMenuPanel and GameSetupPanel. A bridge tendril grows from
//          the main menu trunk rightward, the view pans to reveal the
//          game setup page, and arrival tendrils fan out in the top-left.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Sporefront.Visual
{
    public class MenuTransitionController : MonoBehaviour
    {
        // ================================================================
        // Phase State Machine
        // ================================================================

        private enum Phase
        {
            Idle,
            BridgeGrowing,
            Sliding,
            ArrivalGrowing,
            Complete,
            ArrivalShrinking,
            ReverseSliding
        }

        // ================================================================
        // References
        // ================================================================

        private RectTransform slidingContainer;
        private UITendrilRenderer bridgeTendril;
        private UITendrilRenderer.TendrilBranch bridgeBranch;
        private MainMenuPanel mainMenu;
        private GameSetupPanel gameSetup;
        private MenuTendrilAnimator tendrilAnimator;
        private RectTransform canvasRT;

        // ================================================================
        // State
        // ================================================================

        private Phase currentPhase = Phase.Idle;
        private float phaseTimer;
        private float canvasWidth;
        private float canvasHeight;
        private Vector2 bridgeContainerEnd; // bridge endpoint in container-local coords
        private Vector2 bridgeArrivalDir;   // direction the bridge is heading at its endpoint
        private Color bridgeColor;          // color inherited from the source limb

        // ================================================================
        // Timing Constants
        // ================================================================

        private const float BridgeGrowDuration    = 2.10f;  // 1.40 × 1.5
        private const float BridgeCrossThreshold  = 0.5f;   // unchanged (fraction)
        private const float SlideDuration         = 1.575f; // 1.05 × 1.5
        private const float ArrivalGrowDuration   = 2.10f;  // 1.40 × 1.5
        private const float ArrivalShrinkDuration = 1.3125f;// 0.875 × 1.5
        private const float ReverseSlideDuration  = 1.575f; // 1.05 × 1.5

        // ================================================================
        // Public Properties
        // ================================================================

        public bool IsTransitioning =>
            currentPhase != Phase.Idle && currentPhase != Phase.Complete;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, MainMenuPanel menu, GameSetupPanel setup)
        {
            mainMenu = menu;
            gameSetup = setup;
            canvasRT = canvasTransform.GetComponent<RectTransform>();

            // Find the tendril animator on the main menu
            tendrilAnimator = menu.GetComponentInChildren<MenuTendrilAnimator>(true);

            CacheCanvasDimensions();
            BuildSlidingContainer(canvasTransform);
        }

        private void CacheCanvasDimensions()
        {
            canvasWidth = canvasRT.rect.width;
            canvasHeight = canvasRT.rect.height;

            // Fallback for unresolved canvas
            if (canvasWidth <= 0f) canvasWidth = 1920f;
            if (canvasHeight <= 0f) canvasHeight = 1080f;
        }

        private void BuildSlidingContainer(Transform canvasTransform)
        {
            // Create sliding container (2x canvas width)
            var containerGO = new GameObject("SlidingMenuContainer", typeof(RectTransform));
            containerGO.transform.SetParent(canvasTransform, false);
            slidingContainer = containerGO.GetComponent<RectTransform>();

            // Anchor to fill the canvas vertically, but be 2x wide
            slidingContainer.anchorMin = new Vector2(0f, 0f);
            slidingContainer.anchorMax = new Vector2(1f, 1f);
            slidingContainer.offsetMin = Vector2.zero;
            slidingContainer.offsetMax = new Vector2(canvasWidth, 0f);
            slidingContainer.pivot = new Vector2(0f, 0.5f);
            slidingContainer.anchoredPosition = Vector2.zero;

            // Reparent both panels under the sliding container
            ReparentPanel(mainMenu, isLeft: true);
            ReparentPanel(gameSetup, isLeft: false);

            // Create bridge tendril renderer (spans full container)
            var bridgeGO = new GameObject("BridgeTendril", typeof(RectTransform), typeof(CanvasRenderer));
            bridgeGO.transform.SetParent(slidingContainer, false);
            var bridgeRT = bridgeGO.GetComponent<RectTransform>();
            bridgeRT.anchorMin = Vector2.zero;
            bridgeRT.anchorMax = Vector2.one;
            bridgeRT.offsetMin = Vector2.zero;
            bridgeRT.offsetMax = Vector2.zero;

            bridgeTendril = bridgeGO.AddComponent<UITendrilRenderer>();
            bridgeTendril.raycastTarget = false;

            // Render bridge behind both panels so it doesn't cover UI content
            bridgeGO.transform.SetAsFirstSibling();
        }

        private void ReparentPanel(MonoBehaviour panel, bool isLeft)
        {
            // Find the panel's root RectTransform (the "panel" GameObject)
            // Panels create a child called "*Panel" — find it
            var panelRT = FindPanelRect(panel);
            if (panelRT == null) return;

            panelRT.SetParent(slidingContainer, false);

            if (isLeft)
            {
                // Left half: anchored to left side, full height, canvas width
                panelRT.anchorMin = new Vector2(0f, 0f);
                panelRT.anchorMax = new Vector2(0f, 1f);
                panelRT.pivot = new Vector2(0f, 0.5f);
                panelRT.anchoredPosition = Vector2.zero;
                panelRT.sizeDelta = new Vector2(canvasWidth, 0f);
            }
            else
            {
                // Right half: offset by canvasWidth
                panelRT.anchorMin = new Vector2(0f, 0f);
                panelRT.anchorMax = new Vector2(0f, 1f);
                panelRT.pivot = new Vector2(0f, 0.5f);
                panelRT.anchoredPosition = new Vector2(canvasWidth, 0f);
                panelRT.sizeDelta = new Vector2(canvasWidth, 0f);
            }
        }

        private RectTransform FindPanelRect(MonoBehaviour panelComponent)
        {
            // Use the panel's PanelRT property if available — the panel GO
            // is created as a child of the Canvas, not the MonoBehaviour's GO.
            if (panelComponent is MainMenuPanel mmp)
                return mmp.PanelRT;
            if (panelComponent is GameSetupPanel gsp)
                return gsp.PanelRT;

            // Fallback: search children for a GO ending in "Panel"
            var parentTransform = panelComponent.transform;
            for (int i = 0; i < parentTransform.childCount; i++)
            {
                var child = parentTransform.GetChild(i);
                if (child.name.EndsWith("Panel"))
                    return child.GetComponent<RectTransform>();
            }
            if (parentTransform.childCount > 0)
                return parentTransform.GetChild(0).GetComponent<RectTransform>();
            return null;
        }

        // ================================================================
        // Forward Transition: MainMenu → GameSetup
        // ================================================================

        public void TransitionToGameSetup()
        {
            if (IsTransitioning) return;

            CacheCanvasDimensions();
            RebuildContainerLayout();

            // Build the bridge tendril path
            BuildBridgeTendril();

            // Ensure bridge renders behind both panels
            bridgeTendril.transform.SetAsFirstSibling();

            currentPhase = Phase.BridgeGrowing;
            phaseTimer = 0f;
        }

        private void BuildBridgeTendril()
        {
            bridgeTendril.Clear();
            bridgeBranch = null;

            // Get bridge origin from the trunk
            Vector2 trunkPoint = Vector2.zero;
            Vector2 trunkDir = Vector2.right;
            Vector2 trunkPenultimate = trunkPoint - Vector2.right * 50f;
            List<UITendrilRenderer.StrandParams> trunkStrands = null;
            Color trunkColor = new Color(SporefrontColors.InkRed.r, SporefrontColors.InkRed.g,
                SporefrontColors.InkRed.b, 0.85f);

            if (tendrilAnimator != null)
                tendrilAnimator.GetBridgeTrunkPoint(out trunkPoint, out trunkDir,
                                                     out trunkPenultimate, out trunkStrands,
                                                     out trunkColor);
            bridgeColor = trunkColor;

            // trunkPoint is in panel-local coords (center = 0,0).
            // Convert to container-local coords: MainMenu panel center is at
            // canvasWidth/2 from container left edge.
            float halfW = canvasWidth * 0.5f;
            float halfH = canvasHeight * 0.5f;
            Vector2 containerStart = new Vector2(halfW + trunkPoint.x, trunkPoint.y);

            // End: top-left area of the game setup panel
            // GameSetup panel left edge is at canvasWidth from container left,
            // panel center is at canvasWidth * 1.5
            Vector2 containerEnd = new Vector2(
                canvasWidth + canvasWidth * 0.18f,
                halfH * 0.85f
            );
            bridgeContainerEnd = containerEnd;

            // Build control points from trunk rightward, curving up toward destination
            float spanX = containerEnd.x - containerStart.x;

            var pts = new List<Vector2>
            {
                containerStart,
                containerStart + trunkDir * (spanX * 0.10f),
                containerStart + trunkDir * (spanX * 0.22f) + Vector2.up * (containerEnd.y - containerStart.y) * 0.1f,
                new Vector2(
                    Mathf.Lerp(containerStart.x, containerEnd.x, 0.45f),
                    Mathf.Lerp(containerStart.y, containerEnd.y, 0.35f)
                ),
                new Vector2(
                    Mathf.Lerp(containerStart.x, containerEnd.x, 0.75f),
                    Mathf.Lerp(containerStart.y, containerEnd.y, 0.7f)
                ),
                containerEnd
            };

            // Compute arrival direction from last two control points
            bridgeArrivalDir = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;

            // Convert to bridge-tendril-local coords.
            // Bridge RT is stretch-full on the container whose pivot is (0, 0.5),
            // so the bridge RT center (local 0,0) is at (canvasWidth, 0) from
            // the container's left edge.
            float bridgeCenterX = canvasWidth;
            for (int i = 0; i < pts.Count; i++)
            {
                pts[i] = new Vector2(pts[i].x - bridgeCenterX, pts[i].y);
            }

            // Clone the trunk's strand params to match its visual style
            var strands = new List<UITendrilRenderer.StrandParams>();
            if (trunkStrands != null && trunkStrands.Count > 0)
            {
                foreach (var s in trunkStrands)
                    strands.Add(new UITendrilRenderer.StrandParams
                    {
                        width = s.width,
                        alpha = s.alpha,
                        waveFrequency = s.waveFrequency,
                        wavePhase = s.wavePhase
                    });
            }
            else
            {
                strands.Add(new UITendrilRenderer.StrandParams { width = 5.0f, alpha = 1.0f, waveFrequency = 1.2f, wavePhase = 0.5f });
                strands.Add(new UITendrilRenderer.StrandParams { width = 3.5f, alpha = 1.0f, waveFrequency = 1.5f, wavePhase = 2.0f });
            }

            bridgeBranch = bridgeTendril.AddBranch(pts, strands, 8f, 0.12f);
            bridgeBranch.branchColor = new Color(bridgeColor.r, bridgeColor.g, bridgeColor.b,
                bridgeColor.a * 0.6f);
            bridgeBranch.growthProgress = 0f;
        }

        // ================================================================
        // Reverse Transition: GameSetup → MainMenu
        // ================================================================

        public void TransitionToMainMenu()
        {
            if (IsTransitioning) return;

            CacheCanvasDimensions();

            // Start by shrinking arrival tendrils
            currentPhase = Phase.ArrivalShrinking;
            phaseTimer = 0f;

            // Show main menu so it's visible during slide back
            mainMenu.Show();
        }

        // ================================================================
        // Instant Reset (for Start Game / Play Arena)
        // ================================================================

        public void ResetToMainMenu()
        {
            currentPhase = Phase.Idle;
            phaseTimer = 0f;

            // Reset container position
            if (slidingContainer != null)
                slidingContainer.anchoredPosition = Vector2.zero;

            // Clear bridge tendril
            if (bridgeTendril != null)
                bridgeTendril.Clear();
            bridgeBranch = null;

            // Clear arrival tendrils
            gameSetup.ClearArrivalTendrils();

            // Ensure correct panel visibility
            gameSetup.Hide();
        }

        // ================================================================
        // Update Loop
        // ================================================================

        private void Update()
        {
            CheckForResize();

            if (currentPhase == Phase.Idle || currentPhase == Phase.Complete)
                return;

            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.BridgeGrowing:
                    UpdateBridgeGrowing();
                    break;
                case Phase.Sliding:
                    UpdateSliding();
                    break;
                case Phase.ArrivalGrowing:
                    UpdateArrivalGrowing();
                    break;
                case Phase.ArrivalShrinking:
                    UpdateArrivalShrinking();
                    break;
                case Phase.ReverseSliding:
                    UpdateReverseSliding();
                    break;
            }

            // Keep bridge tendril dirty for rendering
            if (bridgeBranch != null)
            {
                bridgeBranch.idlePulsePhase = Time.time * 2.0f;
                bridgeTendril.MarkDirty();
            }
        }

        private void UpdateBridgeGrowing()
        {
            float t = phaseTimer / BridgeGrowDuration;

            if (bridgeBranch != null)
            {
                // Ease-out: 1 - (1-t)^2
                float eased = Mathf.Clamp01(t);
                eased = 1f - (1f - eased) * (1f - eased);
                bridgeBranch.growthProgress = eased;
            }

            // When bridge has crossed the screen edge, start sliding
            if (t >= BridgeCrossThreshold && currentPhase == Phase.BridgeGrowing)
            {
                currentPhase = Phase.Sliding;
                phaseTimer = 0f;

                // Show game setup (still off-screen right)
                gameSetup.Show();
            }
        }

        private void UpdateSliding()
        {
            float t = Mathf.Clamp01(phaseTimer / SlideDuration);
            // Ease-in-out: smoothstep
            float eased = t * t * (3f - 2f * t);

            slidingContainer.anchoredPosition = new Vector2(-canvasWidth * eased, 0f);

            // Continue growing bridge tendril during slide
            if (bridgeBranch != null)
            {
                float bridgeT = (BridgeCrossThreshold * BridgeGrowDuration + phaseTimer) / BridgeGrowDuration;
                bridgeT = Mathf.Clamp01(bridgeT);
                float bridgeEased = 1f - (1f - bridgeT) * (1f - bridgeT);
                bridgeBranch.growthProgress = bridgeEased;
            }

            if (t >= 1f)
            {
                // Slide complete
                slidingContainer.anchoredPosition = new Vector2(-canvasWidth, 0f);
                mainMenu.Hide();

                if (bridgeBranch != null)
                    bridgeBranch.growthProgress = 1f;

                // Start arrival tendrils
                currentPhase = Phase.ArrivalGrowing;
                phaseTimer = 0f;

                // Convert bridge endpoint from container coords to game setup panel-local coords.
                // GameSetup panel center = canvasWidth * 1.5 from container left edge (panel
                // left at canvasWidth, width = canvasWidth, pivot = left-center).
                float arrivalX = bridgeContainerEnd.x - canvasWidth * 1.5f;
                float arrivalY = bridgeContainerEnd.y;
                gameSetup.StartArrivalTendrils(new Vector2(arrivalX, arrivalY), bridgeArrivalDir, bridgeColor);
            }
        }

        private void UpdateArrivalGrowing()
        {
            float t = Mathf.Clamp01(phaseTimer / ArrivalGrowDuration);
            gameSetup.UpdateArrivalTendrilGrowth(t);

            if (t >= 1f)
            {
                currentPhase = Phase.Complete;
                phaseTimer = 0f;
            }
        }

        private void UpdateArrivalShrinking()
        {
            float t = Mathf.Clamp01(phaseTimer / ArrivalShrinkDuration);
            gameSetup.UpdateArrivalTendrilGrowth(1f - t);

            if (t >= 1f)
            {
                gameSetup.ClearArrivalTendrils();

                // Start reverse slide
                currentPhase = Phase.ReverseSliding;
                phaseTimer = 0f;
            }
        }

        private void UpdateReverseSliding()
        {
            float t = Mathf.Clamp01(phaseTimer / ReverseSlideDuration);
            // Ease-in-out
            float eased = t * t * (3f - 2f * t);

            slidingContainer.anchoredPosition = new Vector2(-canvasWidth * (1f - eased), 0f);

            // Shrink bridge tendril simultaneously
            if (bridgeBranch != null)
            {
                bridgeBranch.growthProgress = 1f - t;
            }

            if (t >= 1f)
            {
                // Reverse slide complete
                slidingContainer.anchoredPosition = Vector2.zero;
                gameSetup.Hide();

                // Clear bridge
                bridgeTendril.Clear();
                bridgeBranch = null;

                currentPhase = Phase.Idle;
                phaseTimer = 0f;
            }
        }

        // ================================================================
        // Layout Helpers
        // ================================================================

        private void RebuildContainerLayout()
        {
            if (slidingContainer == null) return;

            // Update container size for current canvas dimensions
            slidingContainer.offsetMax = new Vector2(canvasWidth, 0f);

            // Re-position game setup panel
            var setupRT = FindPanelRect(gameSetup);
            if (setupRT != null)
            {
                setupRT.anchoredPosition = new Vector2(canvasWidth, 0f);
                setupRT.sizeDelta = new Vector2(canvasWidth, 0f);
            }

            var menuRT = FindPanelRect(mainMenu);
            if (menuRT != null)
            {
                menuRT.sizeDelta = new Vector2(canvasWidth, 0f);
            }
        }

        // ================================================================
        // Screen Resize Handling
        // ================================================================

        private float lastCanvasWidth;
        private float lastCanvasHeight;

        private void CheckForResize()
        {
            float w = canvasRT.rect.width;
            float h = canvasRT.rect.height;
            if (w <= 0f || h <= 0f) return;
            if (Mathf.Approximately(w, lastCanvasWidth) && Mathf.Approximately(h, lastCanvasHeight))
                return;

            lastCanvasWidth = w;
            lastCanvasHeight = h;

            if (!IsTransitioning)
            {
                // Just update dimensions for next transition
                CacheCanvasDimensions();
                RebuildContainerLayout();
                return;
            }

            // Cancel transition and snap to target state
            CacheCanvasDimensions();
            RebuildContainerLayout();

            if (currentPhase == Phase.BridgeGrowing || currentPhase == Phase.Sliding ||
                currentPhase == Phase.ArrivalGrowing)
            {
                // Snap forward to Complete
                slidingContainer.anchoredPosition = new Vector2(-canvasWidth, 0f);
                mainMenu.Hide();
                gameSetup.Show();
                if (bridgeBranch != null)
                    bridgeBranch.growthProgress = 1f;
                currentPhase = Phase.Complete;
            }
            else if (currentPhase == Phase.ArrivalShrinking || currentPhase == Phase.ReverseSliding)
            {
                // Snap back to Idle
                ResetToMainMenu();
                mainMenu.Show();
            }

            phaseTimer = 0f;
        }
    }
}
