// ============================================================================
// FILE: Visual/PopupTendrilDecorator.cs
// PURPOSE: Adds instant-grown mycelium corner tendrils to any popup/modal
//          panel. Attach via the static factory: PopupTendrilDecorator.Attach(panelRT)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class PopupTendrilDecorator : MonoBehaviour
    {
        // ================================================================
        // Config
        // ================================================================

        public int seed = -1;
        public Color primaryColor  = SporefrontColors.ParchmentShadow;
        public Color accentColor   = SporefrontColors.ParchmentDark;

        // ================================================================
        // Runtime
        // ================================================================

        private UITendrilRenderer tendrilRenderer;
        private RectTransform panelRT;
        private bool built;

        // ================================================================
        // Static Factory
        // ================================================================

        /// <summary>
        /// One-liner: creates the decorator child on panelRT, behind all content.
        /// </summary>
        public static PopupTendrilDecorator Attach(RectTransform panelRT, int seed = -1)
        {
            var go = new GameObject("PopupTendrils", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panelRT, false);
            rt.SetAsFirstSibling();
            UIHelper.StretchFull(rt);

            var layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            var renderer = go.AddComponent<UITendrilRenderer>();
            renderer.raycastTarget = false;

            // Parchment overlay — sits on top of tendrils but below popup content
            var overlayGO = new GameObject("ParchmentOverlay", typeof(RectTransform), typeof(Image));
            overlayGO.transform.SetParent(panelRT, false);
            overlayGO.transform.SetSiblingIndex(rt.GetSiblingIndex() + 1);
            var overlayRT = (RectTransform)overlayGO.transform;
            UIHelper.StretchFull(overlayRT);
            var overlayImg = overlayGO.GetComponent<Image>();
            overlayImg.color = new Color(SporefrontColors.ParchmentMid.r, SporefrontColors.ParchmentMid.g, SporefrontColors.ParchmentMid.b, 0.22f);
            overlayImg.raycastTarget = false;
            var overlayLayout = overlayGO.AddComponent<LayoutElement>();
            overlayLayout.ignoreLayout = true;

            var decorator = go.AddComponent<PopupTendrilDecorator>();
            decorator.panelRT       = panelRT;
            decorator.tendrilRenderer = renderer;
            decorator.primaryColor  = SporefrontColors.ParchmentShadow;
            decorator.accentColor   = SporefrontColors.ParchmentDark;
            if (seed >= 0) decorator.seed = seed;

            return decorator;
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void OnEnable()
        {
            if (!built)
                StartCoroutine(BuildWhenReady());
        }

        // ================================================================
        // Build Coroutine
        // ================================================================

        private IEnumerator BuildWhenReady()
        {
            yield return new WaitForEndOfFrame();
            if (panelRT == null) yield break;
            Canvas.ForceUpdateCanvases();

            int maxWaits = 10;
            while ((panelRT.rect.width <= 0f || panelRT.rect.height <= 0f) && maxWaits > 0)
            {
                maxWaits--;
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            Build();
        }

        // ================================================================
        // Build
        // ================================================================

        private void Build()
        {
            if (tendrilRenderer == null || panelRT == null) return;
            tendrilRenderer.Clear();

            float W = panelRT.rect.width;
            float H = panelRT.rect.height;
            if (W <= 0f || H <= 0f) return;

            var rng = seed < 0 ? new System.Random() : new System.Random(seed);
            float shortSide = Mathf.Min(W, H);

            // 4 corners: (originX, originY, arcStart, arcEnd)
            var corners = new (float ox, float oy, float startDeg, float endDeg)[]
            {
                (-W * 0.5f, -H * 0.5f,   0f,  90f),   // bottom-left
                ( W * 0.5f, -H * 0.5f,  90f, 180f),   // bottom-right
                ( W * 0.5f,  H * 0.5f, 180f, 270f),   // top-right
                (-W * 0.5f,  H * 0.5f, 270f, 360f),   // top-left
            };

            // ── Corner arms ──
            const int ArmsPerCorner = 10;

            foreach (var corner in corners)
            {
                Vector2 origin = new Vector2(corner.ox, corner.oy);

                for (int a = 0; a < ArmsPerCorner; a++)
                {
                    float armT = (float)a / Mathf.Max(1, ArmsPerCorner - 1);
                    float angleDeg = Mathf.Lerp(corner.startDeg, corner.endDeg, armT)
                                     + (float)(rng.NextDouble() * 10.0 - 5.0);

                    // Center arms longer via cosine falloff
                    float cosScale = Mathf.Abs(Mathf.Cos((armT - 0.5f) * Mathf.PI));
                    float armLength = shortSide * (0.10f + cosScale * 0.14f + (float)(rng.NextDouble() * 0.06f));

                    var armPts = BuildArcPoints(origin, angleDeg, armLength, 5, rng, 0.08f);

                    float armPhase = (float)(rng.NextDouble() * 6.28f);
                    var armStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams
                        {
                            width = 3.0f, alpha = 0.85f,
                            waveFrequency = 1.5f, wavePhase = armPhase
                        }
                    };

                    var armBranch = tendrilRenderer.AddBranch(armPts, armStrands, 3f, 0.20f);
                    armBranch.branchColor = primaryColor;
                    armBranch.growthProgress = 1f;

                    // 2 sub-branches at 40% and 70% along the arm
                    float[] subFracs = { 0.40f, 0.70f };
                    foreach (float frac in subFracs)
                    {
                        Vector2 subStart = SamplePolyline(armPts, frac);
                        float side      = rng.NextDouble() > 0.5 ? 1f : -1f;
                        float diverge   = 25f + (float)(rng.NextDouble() * 30.0);
                        float subAngle  = angleDeg + side * diverge;
                        float subLength = armLength * (0.40f + (float)(rng.NextDouble() * 0.20f));

                        var subPts = BuildArcPoints(subStart, subAngle, subLength, 3, rng, 0.10f);
                        var subStrands = new List<UITendrilRenderer.StrandParams>
                        {
                            new UITendrilRenderer.StrandParams
                            {
                                width = 2.0f, alpha = 0.70f,
                                waveFrequency = 1.5f,
                                wavePhase = (float)(rng.NextDouble() * 6.28f)
                            }
                        };

                        var subBranch = tendrilRenderer.AddBranch(subPts, subStrands, 2f, 0.25f);
                        subBranch.branchColor = accentColor;
                        subBranch.growthProgress = 1f;

                        // Fine tertiary tendril off the sub-branch tip
                        if (rng.NextDouble() > 0.3)
                        {
                            Vector2 tertiaryStart = SamplePolyline(subPts, 0.8f);
                            float tertiaryAngle = subAngle + (float)(rng.NextDouble() * 50.0 - 25.0);
                            float tertiaryLength = subLength * 0.5f;
                            var tertiaryPts = BuildArcPoints(tertiaryStart, tertiaryAngle, tertiaryLength, 2, rng, 0.06f);
                            var tertiaryStrands = new List<UITendrilRenderer.StrandParams>
                            {
                                new UITendrilRenderer.StrandParams
                                {
                                    width = 1.5f, alpha = 0.55f,
                                    waveFrequency = 0.8f,
                                    wavePhase = (float)(rng.NextDouble() * 6.28f)
                                }
                            };

                            var tertiaryBranch = tendrilRenderer.AddBranch(tertiaryPts, tertiaryStrands, 1.5f, 0.30f);
                            tertiaryBranch.branchColor = accentColor;
                            tertiaryBranch.growthProgress = 1f;
                        }
                    }
                }
            }

            // ── Edge runners — thin tendrils hugging each panel edge ──
            // (left, bottom, right, top) connecting adjacent corners
            var edges = new (Vector2 from, Vector2 to)[]
            {
                (new Vector2(-W * 0.5f, -H * 0.5f), new Vector2(-W * 0.5f,  H * 0.5f)),  // left
                (new Vector2(-W * 0.5f, -H * 0.5f), new Vector2( W * 0.5f, -H * 0.5f)),  // bottom
                (new Vector2( W * 0.5f, -H * 0.5f), new Vector2( W * 0.5f,  H * 0.5f)),  // right
                (new Vector2(-W * 0.5f,  H * 0.5f), new Vector2( W * 0.5f,  H * 0.5f)),  // top
            };

            foreach (var edge in edges)
            {
                Vector2 dir = (edge.to - edge.from).normalized;
                float edgeLength = Vector2.Distance(edge.from, edge.to);
                // Inward perpendicular (toward panel center)
                Vector2 inward = new Vector2(-dir.y, dir.x);
                if (Vector2.Dot(inward, -edge.from.normalized) < 0f) inward = -inward;

                // 2 runner strands per edge, starting at ~15% and ~85% along
                for (int r = 0; r < 2; r++)
                {
                    float startFrac = r == 0 ? 0.08f : 0.60f;
                    float runFrac   = 0.25f + (float)(rng.NextDouble() * 0.15f);
                    Vector2 runStart = edge.from + dir * (edgeLength * startFrac);
                    float runLength = edgeLength * runFrac;

                    // Slight inward drift so they don't sit exactly on the edge
                    float drift = 2f + (float)(rng.NextDouble() * 4.0);
                    var runPts = new List<Vector2>(5);
                    for (int p = 0; p < 5; p++)
                    {
                        float t = (float)p / 4f;
                        Vector2 basePt = runStart + dir * (runLength * t);
                        float wobble = drift + (float)(Mathf.Sin(t * Mathf.PI * 2f) * 3f);
                        runPts.Add(basePt + inward * wobble);
                    }

                    var runStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams
                        {
                            width = 1.8f, alpha = 0.65f,
                            waveFrequency = 1.0f,
                            wavePhase = (float)(rng.NextDouble() * 6.28f)
                        }
                    };

                    var runBranch = tendrilRenderer.AddBranch(runPts, runStrands, 2f, 0.30f);
                    runBranch.branchColor = accentColor;
                    runBranch.growthProgress = 1f;

                    // Small perpendicular sprout off the runner
                    Vector2 sproutStart = SamplePolyline(runPts, 0.5f);
                    float sproutAngle = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg
                                        + (float)(rng.NextDouble() * 40.0 - 20.0);
                    float sproutLength = shortSide * 0.06f;
                    var sproutPts = BuildArcPoints(sproutStart, sproutAngle, sproutLength, 2, rng, 0.06f);
                    var sproutStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams
                        {
                            width = 1.5f, alpha = 0.50f,
                            waveFrequency = 0.6f,
                            wavePhase = (float)(rng.NextDouble() * 6.28f)
                        }
                    };

                    var sproutBranch = tendrilRenderer.AddBranch(sproutPts, sproutStrands, 1.5f, 0.35f);
                    sproutBranch.branchColor = accentColor;
                    sproutBranch.growthProgress = 1f;
                }
            }

            tendrilRenderer.MarkDirty();
            built = true;
        }

        // ================================================================
        // Layout Adaptation
        // ================================================================

        private void OnRectTransformDimensionsChange()
        {
            if (!built || tendrilRenderer == null) return;
            Build();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static List<Vector2> BuildArcPoints(Vector2 start, float angleDeg, float length,
            int pointCount, System.Random rng, float wobbleFraction)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 perp = new Vector2(-dir.y, dir.x);

            var pts = new List<Vector2>(pointCount);
            pts.Add(start);
            for (int j = 1; j < pointCount; j++)
            {
                float t = (float)j / (pointCount - 1);
                Vector2 basePt = start + dir * (length * t);
                float wobble = (float)(rng.NextDouble() * 2.0 - 1.0) * length * wobbleFraction;
                pts.Add(basePt + perp * wobble);
            }
            return pts;
        }

        private static Vector2 SamplePolyline(List<Vector2> pts, float t)
        {
            if (pts.Count < 2) return pts.Count > 0 ? pts[0] : Vector2.zero;
            int seg = Mathf.Min((int)(t * (pts.Count - 1)), pts.Count - 2);
            float segT = t * (pts.Count - 1) - seg;
            return Vector2.Lerp(pts[seg], pts[seg + 1], segT);
        }
    }
}
