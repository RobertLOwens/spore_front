// ============================================================================
// FILE: Visual/MenuTendrilAnimator.cs
// PURPOSE: Defines the tendril tree layout for the main menu, computes paths
//          relative to the center column, and drives growth + idle animation.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class MenuTendrilAnimator : MonoBehaviour
    {
        // ================================================================
        // References
        // ================================================================

        private UITendrilRenderer tendrilRenderer;
        private RectTransform centerColumnRT;
        private RectTransform canvasRT;
        private RawImage glowCircle;

        // ================================================================
        // Animation State
        // ================================================================

        private struct BranchTiming
        {
            public UITendrilRenderer.TendrilBranch branch;
            public float startDelay;
            public float duration;
        }

        private readonly List<BranchTiming> branchTimings = new List<BranchTiming>();
        private float animationTime;
        private bool animating;
        private bool grown;
        private bool treeBuilt;
        private Texture2D glowTexture;

        // ================================================================
        // Constants
        // ================================================================

        private const float TrunkDuration     = 3.15f;  // was 1.8
        private const float LimbDuration      = 2.10f;  // was 1.2
        private const float SubBranchDuration = 1.75f;  // was 1.0
        private const float TendrilDuration   = 1.40f;  // was 0.8
        private const float GlowSize = 48f;
        private const int MaxDepth = 4;
        private const int Seed = -1; // negative = random each time

        // Bridge trunk tracking — the point on the trunk where the bridge tendril originates
        private Vector2 bridgeTrunkPoint;
        private Vector2 bridgeTrunkDir;
        private Vector2 bridgeTrunkPenultimatePt;
        private List<UITendrilRenderer.StrandParams> bridgeTrunkStrands;
        private Color bridgeTrunkColor;
        private bool hasBridgeTrunk;

        // Canvas dimensions cached for helpers
        private float canvasW;
        private float canvasH;
        private float colHalfW;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(UITendrilRenderer renderer, RectTransform centerColumn, RectTransform canvas)
        {
            tendrilRenderer = renderer;
            centerColumnRT = centerColumn;
            canvasRT = canvas;
        }

        // ================================================================
        // Public API
        // ================================================================

        public bool IsGrown => grown;

        public void SnapToGrown()
        {
            if (!treeBuilt)
            {
                // Tree hasn't been built yet — do a full animation start
                StartAnimation();
                return;
            }
            animating = false;
            grown = true;
            for (int i = 0; i < branchTimings.Count; i++)
                branchTimings[i].branch.growthProgress = 1f;
            tendrilRenderer.MarkDirty();
        }

        public void StartAnimation()
        {
            StopAllCoroutines();
            animationTime = 0f;
            animating = true;
            grown = false;
            treeBuilt = false;
            StartCoroutine(BuildAndAnimate());
        }

        // ================================================================
        // Build Tree
        // ================================================================

        private IEnumerator BuildAndAnimate()
        {
            // Wait until after rendering so CanvasScaler has resolved
            yield return new WaitForEndOfFrame();

            // Force layout update and wait until canvas has valid dimensions
            Canvas.ForceUpdateCanvases();
            int maxWaits = 10;
            while ((canvasRT.rect.width <= 0f || canvasRT.rect.height <= 0f) && maxWaits > 0)
            {
                maxWaits--;
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            BuildTendrilTree();
            treeBuilt = true;
            animationTime = 0f;
        }

        private void BuildTendrilTree()
        {
            tendrilRenderer.Clear();
            branchTimings.Clear();

            canvasW = canvasRT.rect.width;
            canvasH = canvasRT.rect.height;
            colHalfW = centerColumnRT.rect.width * 0.5f;

            var rng = Seed < 0 ? new System.Random() : new System.Random(Seed);

            // ============================================================
            // TRUNK: snakes from bottom to top across full screen height,
            // weaving left and right to avoid the center column
            // ============================================================
            float colEdge = colHalfW + 25f;
            int trunkSegments = 8;
            var trunkPts = new List<Vector2>(trunkSegments + 1);
            float bottomY = -canvasH * 0.46f;
            float topY = canvasH * 0.46f;

            for (int i = 0; i <= trunkSegments; i++)
            {
                float t = (float)i / trunkSegments;
                float y = Mathf.Lerp(bottomY, topY, t);
                // Sinusoidal snake within the middle 2.5% of screen width
                float snakeX = Mathf.Sin(t * Mathf.PI * 2.5f + 0.3f) * canvasW * 0.0125f;
                // Add slight randomness
                snakeX += (float)(rng.NextDouble() * 6.0 - 3.0);
                var pt = new Vector2(snakeX, y);
                trunkPts.Add(pt);
            }

            // Split trunk into two intertwined branches — red and blue
            var redTrunk = tendrilRenderer.AddBranch(trunkPts, MakeTrunkStrandsHalf(0), 8f, 0.10f);
            redTrunk.branchColor = new Color(SporefrontColors.InkRed.r, SporefrontColors.InkRed.g, SporefrontColors.InkRed.b, 0.85f);

            branchTimings.Add(new BranchTiming
            {
                branch = redTrunk,
                startDelay = 0f,
                duration = TrunkDuration
            });

            var blueTrunk = tendrilRenderer.AddBranch(trunkPts, MakeTrunkStrandsHalf(1), 8f, 0.10f);
            blueTrunk.branchColor = new Color(SporefrontColors.InkGreen.r, SporefrontColors.InkGreen.g, SporefrontColors.InkGreen.b, 0.85f);

            branchTimings.Add(new BranchTiming
            {
                branch = blueTrunk,
                startDelay = 0f,
                duration = TrunkDuration
            });

            // ============================================================
            // Capture bridge point from trunk at ~45% height
            // ============================================================
            {
                float bridgeFrac = 0.45f;
                int bridgeSeg = Mathf.Min((int)(bridgeFrac * (trunkPts.Count - 1)), trunkPts.Count - 2);
                float bridgeSegFrac = bridgeFrac * (trunkPts.Count - 1) - bridgeSeg;
                bridgeTrunkPoint = Vector2.Lerp(trunkPts[bridgeSeg], trunkPts[bridgeSeg + 1], bridgeSegFrac);
                // Direction: rightward from trunk (bridge goes right toward game setup)
                bridgeTrunkDir = Vector2.right;
                // Penultimate: the trunk point just before
                bridgeTrunkPenultimatePt = trunkPts[bridgeSeg];
                bridgeTrunkStrands = MakeTrunkStrandsHalf(0);
                bridgeTrunkColor = new Color(SporefrontColors.InkRed.r, SporefrontColors.InkRed.g, SporefrontColors.InkRed.b, 0.85f);
                hasBridgeTrunk = true;
            }

            // ============================================================
            // BRANCHES: spawn from points along the trunk, angling outward
            // ============================================================
            // 6 limbs: three right, three left, at different heights
            int branchCount = 6;
            float[] limbPositions = { 0.15f, 0.30f, 0.45f, 0.60f, 0.75f, 0.90f };
            for (int i = 0; i < branchCount; i++)
            {
                float t = limbPositions[i];
                // Interpolate position along the trunk polyline
                float totalLen = 0f;
                var segLens = new float[trunkPts.Count - 1];
                for (int s = 0; s < trunkPts.Count - 1; s++)
                {
                    segLens[s] = (trunkPts[s + 1] - trunkPts[s]).magnitude;
                    totalLen += segLens[s];
                }
                float targetDist = t * totalLen;
                float walked = 0f;
                Vector2 spawnPt = trunkPts[0];
                for (int s = 0; s < segLens.Length; s++)
                {
                    if (walked + segLens[s] >= targetDist)
                    {
                        float segT = (targetDist - walked) / segLens[s];
                        spawnPt = Vector2.Lerp(trunkPts[s], trunkPts[s + 1], segT);
                        break;
                    }
                    walked += segLens[s];
                }

                // Alternate right/left: even index = right, odd = left
                float baseAngle = (i % 2 == 0) ? 0f : 180f;
                // Slight vertical tilt ±25° so they're not perfectly horizontal
                float verticalBias = (float)(rng.NextDouble() * 50.0 - 25.0);
                float angle = baseAngle + verticalBias;

                // Alternate red/blue: even index = SporeRed, odd = SporeTeal
                Color limbColor = (i % 2 == 0)
                    ? new Color(SporefrontColors.InkRed.r, SporefrontColors.InkRed.g, SporefrontColors.InkRed.b, 0.85f)
                    : new Color(SporefrontColors.InkGreen.r, SporefrontColors.InkGreen.g, SporefrontColors.InkGreen.b, 0.85f);

                float delay = TrunkDuration * t;
                GenerateBranch(spawnPt, angle, 1, delay, rng, limbColor);
            }
        }

        private void GenerateBranch(Vector2 start, float angleDeg, int depth, float parentDelay, System.Random rng, Color branchColor = default)
        {
            if (depth > MaxDepth) return;

            // Segment length decreases with depth
            float baseLength;
            switch (depth)
            {
                case 0: baseLength = canvasH * 0.25f; break;
                case 1: baseLength = canvasW * 0.45f; break;
                case 2: baseLength = canvasH * 0.12f; break;
                default: baseLength = canvasH * 0.05f; break;
            }
            float length = baseLength * (0.5f + (float)rng.NextDouble() * 1.0f);

            // Build control points along the direction with slight curvature
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            int pointCount = depth == 1 ? 7 : (depth == 0 ? 4 : 3);
            var pts = new List<Vector2>(pointCount);
            pts.Add(start);

            float limX = canvasW * 0.48f;
            float limY = canvasH * 0.48f;
            for (int i = 1; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                Vector2 basePoint = start + dir * (length * t);
                // Perpendicular offset for curvature
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float curvatureScale = depth == 1 ? 0.3f : 0.15f;
                float curvature = (float)(rng.NextDouble() * 2.0 - 1.0) * length * curvatureScale;
                Vector2 point = basePoint + perp * curvature;

                // If point is at or beyond the screen edge, clamp it and stop adding more points
                if (Mathf.Abs(point.x) >= limX || Mathf.Abs(point.y) >= limY)
                {
                    point = ClampToScreen(point);
                    pts.Add(point);
                    break;
                }
                pts.Add(point);
            }

            Vector2 endpoint = pts[pts.Count - 1];

            // Select strand style and timing based on depth
            List<UITendrilRenderer.StrandParams> strands;
            float tipWidth;
            float looseWidth;
            float duration;
            switch (depth)
            {
                case 0:
                    strands = MakeTrunkStrandsHalf(0);
                    tipWidth = 8f; looseWidth = 0.10f;
                    duration = TrunkDuration;
                    break;
                case 1:
                    strands = MakeLimbStrands();
                    tipWidth = 10f; looseWidth = 0.15f;
                    duration = LimbDuration;
                    break;
                case 2:
                    strands = MakeSubBranchStrands();
                    tipWidth = 5f; looseWidth = 0.20f;
                    duration = SubBranchDuration;
                    break;
                default:
                    strands = MakeTendrilStrands();
                    tipWidth = 3f; looseWidth = 0.25f;
                    duration = TendrilDuration;
                    break;
            }

            var branch = tendrilRenderer.AddBranch(pts, strands, tipWidth, looseWidth);
            if (branchColor != default) branch.branchColor = branchColor;

            branchTimings.Add(new BranchTiming
            {
                branch = branch,
                startDelay = parentDelay,
                duration = duration
            });

            // Stop recursing if we reached max depth
            if (depth >= MaxDepth) return;

            // Spawn sub-branches from points ALONG this branch, not all from the tip
            int childCount = depth <= 1 ? 10 : 5;
            float childLimX = canvasW * 0.44f;
            float childLimY = canvasH * 0.44f;
            for (int f = 0; f < childCount; f++)
            {
                // Spread evenly from 20% to 95% along the branch
                float spawnFrac = 0.2f + (float)f / (childCount - 1 + 0.001f) * 0.75f;
                // Interpolate spawn point along the control points
                int spawnSeg = Mathf.Min((int)(spawnFrac * (pts.Count - 1)), pts.Count - 2);
                float segFrac = spawnFrac * (pts.Count - 1) - spawnSeg;
                Vector2 spawnPt = Vector2.Lerp(pts[spawnSeg], pts[spawnSeg + 1], segFrac);

                // Skip spawn points too close to the screen edge
                if (Mathf.Abs(spawnPt.x) >= childLimX || Mathf.Abs(spawnPt.y) >= childLimY)
                    continue;

                // Child angle diverges from parent — alternate sides, wider spread
                float side = (f % 2 == 0) ? 1f : -1f;
                float diverge = 30f + (float)(rng.NextDouble() * 40.0);
                float childAngle = angleDeg + side * diverge;

                float childDelay = parentDelay + duration * spawnFrac + (float)f * 0.1f;
                GenerateBranch(spawnPt, childAngle, depth + 1, childDelay, rng, branchColor);
            }
        }

        private Vector2 DeflectFromColumn(Vector2 point)
        {
            float margin = colHalfW + 25f;
            if (point.x > -margin && point.x < margin)
            {
                // Push to the nearest edge
                if (point.x >= 0f)
                    point.x = margin;
                else
                    point.x = -margin;
            }
            return point;
        }

        private Vector2 ClampToScreen(Vector2 point)
        {
            float limX = canvasW * 0.48f;
            float limY = canvasH * 0.48f;
            point.x = Mathf.Clamp(point.x, -limX, limX);
            point.y = Mathf.Clamp(point.y, -limY, limY);
            return point;
        }

        // ================================================================
        // Strand Factories
        // ================================================================

        /// <summary>
        /// Returns half the trunk strands: half=0 gets strands 0,2; half=1 gets strand 1.
        /// Reduced from 5 to 3 strands to prevent overlap accumulation.
        /// </summary>
        private static List<UITendrilRenderer.StrandParams> MakeTrunkStrandsHalf(int half)
        {
            var all = new[]
            {
                new UITendrilRenderer.StrandParams { width = 3.0f, alpha = 1.0f, waveFrequency = 3.5f, wavePhase = 0.0f },
                new UITendrilRenderer.StrandParams { width = 2.5f, alpha = 1.0f, waveFrequency = 4.0f, wavePhase = 1.2f },
                new UITendrilRenderer.StrandParams { width = 2.8f, alpha = 1.0f, waveFrequency = 3.0f, wavePhase = 2.5f }
            };
            var result = new List<UITendrilRenderer.StrandParams>();
            for (int i = 0; i < all.Length; i++)
            {
                if (i % 2 == half) result.Add(all[i]);
            }
            return result;
        }

        private static List<UITendrilRenderer.StrandParams> MakeLimbStrands()
        {
            return new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams { width = 3.0f, alpha = 1.0f, waveFrequency = 1.2f, wavePhase = 0.5f },
                new UITendrilRenderer.StrandParams { width = 1.8f, alpha = 1.0f, waveFrequency = 1.5f, wavePhase = 2.0f }
            };
        }

        private static List<UITendrilRenderer.StrandParams> MakeSubBranchStrands()
        {
            return new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams { width = 1.8f, alpha = 1.0f, waveFrequency = 1.0f, wavePhase = 0.8f }
            };
        }

        private static List<UITendrilRenderer.StrandParams> MakeTendrilStrands()
        {
            return new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams { width = 1.5f, alpha = 1.0f, waveFrequency = 0.8f, wavePhase = 1.0f }
            };
        }

        // ================================================================
        // Glow Circle
        // ================================================================

        private void CreateGlowCircle()
        {
            if (glowCircle != null) return;

            // Generate soft circle texture
            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha *= alpha; // Quadratic falloff
                    glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            glowTexture.Apply();

            // Create RawImage
            var glowGO = new GameObject("TendrilGlow", typeof(RectTransform), typeof(RawImage));
            glowGO.transform.SetParent(tendrilRenderer.transform, false);
            var rt = glowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float canvasH = canvasRT.rect.height;
            rt.anchoredPosition = new Vector2(0f, -canvasH * 0.45f);
            rt.sizeDelta = new Vector2(GlowSize, GlowSize);

            glowCircle = glowGO.GetComponent<RawImage>();
            glowCircle.texture = glowTexture;
            glowCircle.color = Color.white;
            glowCircle.raycastTarget = false;
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (!treeBuilt) return;
            if (!animating && !grown) return;

            animationTime += Time.deltaTime;

            bool allDone = true;

            for (int i = 0; i < branchTimings.Count; i++)
            {
                var bt = branchTimings[i];
                float elapsed = animationTime - bt.startDelay;

                if (elapsed <= 0f)
                {
                    bt.branch.growthProgress = 0f;
                    allDone = false;
                }
                else if (elapsed < bt.duration)
                {
                    // Ease-out curve: 1 - (1-t)^2
                    float t = elapsed / bt.duration;
                    bt.branch.growthProgress = 1f - (1f - t) * (1f - t);
                    allDone = false;
                }
                else
                {
                    bt.branch.growthProgress = 1f;
                }

                // Idle pulse phase (always advancing)
                bt.branch.idlePulsePhase = Time.time * 2.0f + i * 0.7f;
            }

            if (allDone && animating)
            {
                animating = false;
                grown = true;
            }

            tendrilRenderer.MarkDirty();

        }

        // ================================================================
        // Layout Adaptation
        // ================================================================

        private void OnRectTransformDimensionsChange()
        {
            if (tendrilRenderer != null && treeBuilt)
            {
                // Rebuild paths for new dimensions
                BuildTendrilTree();
                if (grown)
                {
                    // Restore fully grown state
                    for (int i = 0; i < branchTimings.Count; i++)
                    {
                        branchTimings[i].branch.growthProgress = 1f;
                    }
                }
            }
        }

        // ================================================================
        // Public: Bridge Trunk Point for Page Transition
        // ================================================================

        /// <summary>
        /// Returns the point on the trunk where the bridge tendril originates,
        /// plus direction, penultimate point, strand params, and color.
        /// </summary>
        public bool GetBridgeTrunkPoint(out Vector2 point, out Vector2 direction,
                                        out Vector2 penultimatePt,
                                        out List<UITendrilRenderer.StrandParams> strandParams,
                                        out Color trunkColor)
        {
            if (hasBridgeTrunk)
            {
                point = bridgeTrunkPoint;
                direction = bridgeTrunkDir;
                penultimatePt = bridgeTrunkPenultimatePt;
                strandParams = bridgeTrunkStrands;
                trunkColor = bridgeTrunkColor;
                return true;
            }
            point = new Vector2(0f, 0f);
            direction = Vector2.right;
            penultimatePt = point - Vector2.right * 50f;
            strandParams = MakeTrunkStrandsHalf(0);
            trunkColor = new Color(SporefrontColors.InkRed.r, SporefrontColors.InkRed.g, SporefrontColors.InkRed.b, 0.85f);
            return false;
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            if (glowTexture != null)
            {
                Destroy(glowTexture);
                glowTexture = null;
            }
        }
    }
}
