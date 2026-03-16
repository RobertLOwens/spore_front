// ============================================================================
// FILE: Visual/HUDTendrilAnimator.cs
// PURPOSE: Horizontal mycelium tendril background for HUD bars (resource bar
//          and nav bar). A trunk runs full width with perpendicular limbs,
//          sub-branches, and corner bulges — all behind buttons/labels.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sporefront.Visual
{
    public class HUDTendrilAnimator : MonoBehaviour
    {
        // ================================================================
        // Config (set before Initialize)
        // ================================================================

        public float barHeight = 80f;
        public Color trunkColor     = SporefrontColors.InkMid;
        public Color limbColor      = SporefrontColors.InkLight;
        public Color subBranchColor = SporefrontColors.InkFaded;
        public Color cornerColor    = SporefrontColors.ParchmentShadow;
        public float trunkAlpha     = 0.90f;
        public float limbAlpha      = 0.80f;
        public float subBranchAlpha = 0.65f;
        public float cornerAlpha    = 0.75f;
        public int seed = -1;

        public float limbSpacing        = 60f;   // px gap between mid-bar limbs; lower = denser
        public int   subBranchesPerLimb = 2;     // sub-branches per main limb
        public int   cornerArmCount     = 7;     // arms per corner bulge fan
        public float limbZoneHalfFrac   = 0.35f; // half-fraction of width covered by mid limbs
        public bool  thirdTrunkStrand   = false; // add a 3rd thin trunk strand for extra texture
        public float subBranchLengthFrac  = 0.15f; // sub-branch length as fraction of barH
        public float subBranchWidth       = 1.4f;  // strand width for sub-branches
        public int   subBranchStrandCount = 1;     // individual tendril strands per sub-branch

        public float trunkYFrac        = 0f;   // trunk vertical position: 0=center, +0.5=top edge, -0.5=bottom edge
        public float longLimbRightFrac = 0f;   // rightmost fraction of bar where limbs get length multiplier
        public float longLimbMultiplier = 2.0f; // length multiplier for right-zone limbs
        public float limbLengthMultiplier    = 1.0f; // per-bar scale on base limb length
        public int   longLimbSubBranchCount  = 0;    // sub-branches for right-zone limbs (0 = same as subBranchesPerLimb)

        // ================================================================
        // Runtime
        // ================================================================

        private UITendrilRenderer tendrilRenderer;
        private RectTransform panelRT;

        private struct BranchTiming
        {
            public UITendrilRenderer.TendrilBranch branch;
            public float startDelay;
            public float duration;
        }

        private List<BranchTiming> branchTimings = new List<BranchTiming>();
        private List<Vector2> trunkPts = new List<Vector2>();
        private float animationTime;
        private bool animating;
        private bool grown;
        private bool treeBuilt;
        private float barW;
        private float barH;

        public bool IsGrown => grown;

        // ================================================================
        // Timing Constants
        // ================================================================

        private const float TrunkDuration    = 1.4f;
        private const float LimbDuration     = 0.9f;
        private const float SubBranchDuration = 0.7f;
        private const float CornerDuration   = 1.1f;

        // ================================================================
        // Public API
        // ================================================================

        public void Initialize(UITendrilRenderer renderer, RectTransform panel)
        {
            tendrilRenderer = renderer;
            panelRT = panel;
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

        public void SnapToGrown()
        {
            if (!treeBuilt) return;
            for (int i = 0; i < branchTimings.Count; i++)
                branchTimings[i].branch.growthProgress = 1f;
            grown = true;
            animating = false;
            tendrilRenderer.MarkDirty();
        }

        // ================================================================
        // Build Coroutine
        // ================================================================

        private IEnumerator BuildAndAnimate()
        {
            yield return new WaitForEndOfFrame();

            Canvas.ForceUpdateCanvases();
            int maxWaits = 10;
            while ((panelRT.rect.width <= 0f || panelRT.rect.height <= 0f) && maxWaits > 0)
            {
                maxWaits--;
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            BuildTendrilTree();
            treeBuilt = true;
            animationTime = 0f;
        }

        // ================================================================
        // Tree Builder
        // ================================================================

        private void BuildTendrilTree()
        {
            tendrilRenderer.Clear();
            branchTimings.Clear();

            barW = panelRT.rect.width;
            barH = panelRT.rect.height;

            if (barW <= 0f || barH <= 0f) return;

            var rng = seed < 0 ? new System.Random() : new System.Random(seed);

            BuildTrunk(rng);
            BuildMidLimbs(rng);
            BuildCornerBulge(isLeft: true, rng: rng);
            BuildCornerBulge(isLeft: false, rng: rng);
        }

        // ================================================================
        // Trunk — horizontal sinusoidal spine
        // ================================================================

        private void BuildTrunk(System.Random rng)
        {
            trunkPts.Clear();
            float rngOffset = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            for (int i = 0; i < 10; i++)
            {
                float t = (float)i / 9f;
                float x = Mathf.Lerp(-barW * 0.5f, barW * 0.5f, t);
                float amplitude = barH * 0.08f * Mathf.Clamp01(1f - Mathf.Abs(trunkYFrac) * 1.8f);
                float y = barH * trunkYFrac + Mathf.Sin(t * Mathf.PI * 3f + rngOffset) * amplitude;
                y = Mathf.Clamp(y, -barH * 0.49f, barH * 0.49f);
                trunkPts.Add(new Vector2(x, y));
            }

            // Strand 1 — primary
            var strand1 = new UITendrilRenderer.StrandParams
            {
                width = 3.5f, alpha = trunkAlpha, waveFrequency = 2.0f, wavePhase = 0f
            };
            var b1 = tendrilRenderer.AddBranch(new List<Vector2>(trunkPts),
                new List<UITendrilRenderer.StrandParams> { strand1 }, 4f, 0.05f);
            b1.branchColor = trunkColor;
            branchTimings.Add(new BranchTiming { branch = b1, startDelay = 0f, duration = TrunkDuration });

            // Strand 2 — secondary, slightly transparent
            var strand2 = new UITendrilRenderer.StrandParams
            {
                width = 2.8f, alpha = trunkAlpha * 0.7f, waveFrequency = 2.5f, wavePhase = 1.8f
            };
            var b2 = tendrilRenderer.AddBranch(new List<Vector2>(trunkPts),
                new List<UITendrilRenderer.StrandParams> { strand2 }, 4f, 0.05f);
            b2.branchColor = trunkColor;
            branchTimings.Add(new BranchTiming { branch = b2, startDelay = 0f, duration = TrunkDuration });

            if (thirdTrunkStrand)
            {
                var strand3 = new UITendrilRenderer.StrandParams
                {
                    width = 1.8f, alpha = trunkAlpha * 0.45f, waveFrequency = 3.2f, wavePhase = 3.5f
                };
                var b3 = tendrilRenderer.AddBranch(new List<Vector2>(trunkPts),
                    new List<UITendrilRenderer.StrandParams> { strand3 }, 5f, 0.05f);
                b3.branchColor = trunkColor;
                branchTimings.Add(new BranchTiming { branch = b3, startDelay = 0f, duration = TrunkDuration });
            }
        }

        // ================================================================
        // Mid-Bar Limbs — perpendicular branches alternating up/down
        // ================================================================

        private void BuildMidLimbs(System.Random rng)
        {
            // Count: round barW/limbSpacing to even, min 8
            int count = Mathf.RoundToInt(barW / limbSpacing);
            count = Mathf.Max(8, count % 2 == 0 ? count : count + 1);

            for (int i = 0; i < count; i++)
            {
                float xFracInZone = (float)i / Mathf.Max(1, count - 1);
                float limbX = Mathf.Lerp(-barW * limbZoneHalfFrac, barW * limbZoneHalfFrac, xFracInZone);
                float limbY = GetTrunkYAtX(limbX);

                // Direction driven by trunk position; center falls back to alternating
                float baseAngle = trunkYFrac < -0.1f ? 90f        // trunk at bottom → limbs go UP
                                : trunkYFrac >  0.1f ? -90f       // trunk at top    → limbs go DOWN
                                : (i % 2 == 0) ? 90f : -90f;     // center: alternate
                float jitter = (float)(rng.NextDouble() * 24.0 - 12.0);
                float angle = baseAngle + jitter;

                float xFracFull = (limbX + barW * 0.5f) / barW;
                bool inRightZone = longLimbRightFrac > 0f && xFracFull >= (1f - longLimbRightFrac);
                float length = barH * (0.5f + (float)rng.NextDouble() * 0.10f)
                               * limbLengthMultiplier
                               * (inRightZone ? longLimbMultiplier : 1f);

                var limbPts = BuildArcPoints(new Vector2(limbX, limbY), angle, length, 4, rng, 0.05f);

                float phase = (float)(rng.NextDouble() * 6.28f);
                var strands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = 3.2f, alpha = limbAlpha, waveFrequency = 1.2f, wavePhase = phase
                    }
                };

                var branch = tendrilRenderer.AddBranch(limbPts, strands, 3f, 0.25f);
                branch.branchColor = limbColor;

                float limbDelay = TrunkDuration * xFracFull;
                branchTimings.Add(new BranchTiming { branch = branch, startDelay = limbDelay, duration = LimbDuration });

                // Sub-branches distributed along the limb (right-zone can use a higher count)
                int subCount = inRightZone && longLimbSubBranchCount > 0 ? longLimbSubBranchCount : subBranchesPerLimb;
                for (int s = 0; s < subCount; s++)
                {
                    float frac = subCount > 1
                        ? 0.30f + (0.85f - 0.30f) * s / (subCount - 1)
                        : 0.55f;

                    Vector2 subStart = SamplePolyline(limbPts, frac);
                    float side     = rng.NextDouble() > 0.5 ? 1f : -1f;
                    float diverge  = 35f + (float)(rng.NextDouble() * 25.0);
                    float subAngle = angle + side * diverge;
                    float subLength = barH * (subBranchLengthFrac + (float)(rng.NextDouble() * 0.07));

                    // Each tendril fans out at its own angle across an 80° spread
                    int strandCount = Mathf.Max(1, subBranchStrandCount);
                    float fanSpread = 80f;
                    for (int st = 0; st < strandCount; st++)
                    {
                        float fanT = strandCount > 1 ? (float)st / (strandCount - 1) : 0.5f;
                        float tendrilAngle = subAngle + Mathf.Lerp(-fanSpread * 0.5f, fanSpread * 0.5f, fanT);
                        float tendrilLength = subLength * (0.9f + (float)(rng.NextDouble() * 1.2f));
                        var tendrilPts = BuildArcPoints(subStart, tendrilAngle, tendrilLength, 2, rng, 0.06f);
                        var tendrilStrand = new List<UITendrilRenderer.StrandParams>
                        {
                            new UITendrilRenderer.StrandParams
                            {
                                width = subBranchWidth,
                                alpha = subBranchAlpha,
                                waveFrequency = 0.6f + (float)(rng.NextDouble() * 0.6f),
                                wavePhase = (float)(rng.NextDouble() * 6.28f)
                            }
                        };
                        var tendrilBranch = tendrilRenderer.AddBranch(tendrilPts, tendrilStrand, 2f, 0.35f);
                        tendrilBranch.branchColor = subBranchColor;
                        branchTimings.Add(new BranchTiming
                        {
                            branch = tendrilBranch,
                            startDelay = limbDelay + LimbDuration * frac,
                            duration = SubBranchDuration
                        });
                    }
                }
            }
        }

        // ================================================================
        // Corner Bulge — fan of arms at each end of the bar
        // ================================================================

        private void BuildCornerBulge(bool isLeft, System.Random rng)
        {
            if (cornerArmCount <= 0) return;
            float cornerX = isLeft ? -barW * 0.46f : barW * 0.46f;
            float cornerY = GetTrunkYAtX(cornerX);
            Vector2 origin = new Vector2(cornerX, cornerY);

            // Fan angles: LEFT points left (135°–225°), RIGHT points right (-45°–45°)
            float startAngle = isLeft ? 135f : -45f;
            float endAngle   = isLeft ? 225f : 45f;

            float centerDelay = TrunkDuration * 0.85f;
            float maxX = barW * 0.49f;

            for (int a = 0; a < cornerArmCount; a++)
            {
                float armT = (float)a / Mathf.Max(1, cornerArmCount - 1);
                float armAngle = Mathf.Lerp(startAngle, endAngle, armT)
                                 + (float)(rng.NextDouble() * 16.0 - 8.0);

                // Center arms (armT≈0.5) longer via cos falloff
                float cosScale = Mathf.Abs(Mathf.Cos((armT - 0.5f) * Mathf.PI));
                float armLength = barH * (0.35f + cosScale * 0.25f);

                float armAngleRad = armAngle * Mathf.Deg2Rad;
                Vector2 armDir = new Vector2(Mathf.Cos(armAngleRad), Mathf.Sin(armAngleRad));

                // 5 control points with outward curve bias
                var armPts = new List<Vector2>(5);
                armPts.Add(origin);
                Vector2 perp = new Vector2(-armDir.y, armDir.x);
                for (int j = 1; j < 5; j++)
                {
                    float t = (float)j / 4f;
                    Vector2 basePt = origin + armDir * (armLength * t);
                    float curvature = t * barH * 0.12f * (float)(rng.NextDouble() * 2.0 - 1.0);
                    Vector2 pt = basePt + perp * curvature;
                    pt.x = Mathf.Clamp(pt.x, -maxX, maxX);
                    armPts.Add(pt);
                }

                float armPhase = (float)(rng.NextDouble() * 6.28f);
                var armStrands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = 2.0f, alpha = cornerAlpha, waveFrequency = 1.5f, wavePhase = armPhase
                    }
                };

                var armBranch = tendrilRenderer.AddBranch(armPts, armStrands, 3.5f, 0.20f);
                armBranch.branchColor = cornerColor;

                // Center arm at TrunkDuration*0.85, others stagger outward
                float armDelay = centerDelay + (a - cornerArmCount / 2) * 0.04f;
                branchTimings.Add(new BranchTiming { branch = armBranch, startDelay = armDelay, duration = CornerDuration });

                // Sub-arms at 50% and 80%
                float[] subFracs = { 0.50f, 0.80f };
                foreach (float frac in subFracs)
                {
                    Vector2 subStart = SamplePolyline(armPts, frac);

                    float side = rng.NextDouble() > 0.5 ? 1f : -1f;
                    float subAngle = armAngle + side * (20f + (float)(rng.NextDouble() * 30.0));
                    float subLength = barH * 0.10f;

                    float subAngleRad = subAngle * Mathf.Deg2Rad;
                    Vector2 subDir = new Vector2(Mathf.Cos(subAngleRad), Mathf.Sin(subAngleRad));
                    Vector2 subEnd = subStart + subDir * subLength;
                    subEnd.x = Mathf.Clamp(subEnd.x, -maxX, maxX);

                    var subPts = new List<Vector2> { subStart, subEnd };
                    var subStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams
                        {
                            width = 1.2f, alpha = 0.22f,
                            waveFrequency = 1.0f, wavePhase = (float)(rng.NextDouble() * 6.28f)
                        }
                    };

                    var subArm = tendrilRenderer.AddBranch(subPts, subStrands, 2f, 0.30f);
                    subArm.branchColor = cornerColor;
                    branchTimings.Add(new BranchTiming
                    {
                        branch = subArm,
                        startDelay = armDelay + CornerDuration * frac,
                        duration = SubBranchDuration
                    });
                }
            }
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
                    float t = elapsed / bt.duration;
                    bt.branch.growthProgress = 1f - (1f - t) * (1f - t);
                    allDone = false;
                }
                else
                {
                    bt.branch.growthProgress = 1f;
                }

                // Idle pulse (always advancing)
                bt.branch.idlePulsePhase = Time.time * 1.5f + i * 0.6f;
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
            if (tendrilRenderer == null || !treeBuilt) return;
            BuildTendrilTree();
            if (grown)
            {
                for (int i = 0; i < branchTimings.Count; i++)
                    branchTimings[i].branch.growthProgress = 1f;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>Builds a short polyline from a start point in a given direction.</summary>
        private List<Vector2> BuildArcPoints(Vector2 start, float angleDeg, float length,
            int pointCount, System.Random rng, float wobbleFraction)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
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

        /// <summary>Samples a point at fraction t (0..1) along a polyline.</summary>
        private static Vector2 SamplePolyline(List<Vector2> pts, float t)
        {
            if (pts.Count < 2) return pts.Count > 0 ? pts[0] : Vector2.zero;
            int seg = Mathf.Min((int)(t * (pts.Count - 1)), pts.Count - 2);
            float segT = t * (pts.Count - 1) - seg;
            return Vector2.Lerp(pts[seg], pts[seg + 1], segT);
        }

        /// <summary>Finds trunk y coordinate at a given x by linear interpolation between trunk points.</summary>
        private float GetTrunkYAtX(float x)
        {
            if (trunkPts.Count < 2) return 0f;
            for (int i = 0; i < trunkPts.Count - 1; i++)
            {
                if (x >= trunkPts[i].x && x <= trunkPts[i + 1].x)
                {
                    float span = trunkPts[i + 1].x - trunkPts[i].x;
                    if (span < 0.001f) return trunkPts[i].y;
                    float tt = (x - trunkPts[i].x) / span;
                    return Mathf.Lerp(trunkPts[i].y, trunkPts[i + 1].y, tt);
                }
            }
            // Clamp to endpoints
            if (x <= trunkPts[0].x) return trunkPts[0].y;
            return trunkPts[trunkPts.Count - 1].y;
        }
    }
}
