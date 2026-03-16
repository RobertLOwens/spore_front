// ============================================================================
// FILE: Visual/WheelTendrilAnimator.cs
// PURPOSE: Tendril decorations for the Tendril Wheel HUD — connection lines
//          from corner hubs to icon buttons, corner tendril masses, branch
//          hairs, and the bottom horizontal tendril border.
//          Supports dual-ring (inner/outer) layout for the right wheel.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sporefront.Visual
{
    public class WheelTendrilAnimator : MonoBehaviour
    {
        // ================================================================
        // References (set via Initialize)
        // ================================================================

        private UITendrilRenderer rightRenderer;
        private UITendrilRenderer leftRenderer;
        private UITendrilRenderer borderRenderer;
        private RectTransform rightContainerRT;
        private RectTransform leftContainerRT;

        // ================================================================
        // Growth Animation
        // ================================================================

        private struct BranchTiming
        {
            public UITendrilRenderer.TendrilBranch branch;
            public float startDelay;
            public float duration;
        }

        private readonly List<BranchTiming> rightTimings = new List<BranchTiming>();
        private readonly List<BranchTiming> leftTimings = new List<BranchTiming>();
        private readonly List<BranchTiming> borderTimings = new List<BranchTiming>();

        private float animationTime;
        private bool animating;
        private bool grown;
        private bool treeBuilt;

        // Sway
        private float swayPhaseRight;
        private float swayPhaseLeft;

        // ================================================================
        // Timing Constants
        // ================================================================

        private const float ConnectionDuration = 0.9f;
        private const float BranchHairDuration = 0.5f;
        private const float CornerMassDuration = 1.2f;
        private const float BorderDuration = 1.4f;

        public bool IsGrown => grown;

        // ================================================================
        // Public API
        // ================================================================

        public void Initialize(
            UITendrilRenderer rightClusterRenderer,
            UITendrilRenderer leftClusterRenderer,
            UITendrilRenderer bottomBorderRenderer,
            RectTransform rightContainer,
            RectTransform leftContainer)
        {
            rightRenderer = rightClusterRenderer;
            leftRenderer = leftClusterRenderer;
            borderRenderer = bottomBorderRenderer;
            rightContainerRT = rightContainer;
            leftContainerRT = leftContainer;
        }

        /// <summary>
        /// Build all tendril decorations. Right wheel uses dual-ring (outer + inner).
        /// </summary>
        public void BuildAll(
            List<Vector2> rightOuterPositions, List<Vector2> rightInnerPositions,
            Vector2 rightHub,
            List<Vector2> leftButtonPositions, Vector2 leftHub,
            float screenWidth)
        {
            StopAllCoroutines();
            animationTime = 0f;
            animating = true;
            grown = false;
            treeBuilt = false;

            StartCoroutine(BuildAndAnimate(
                rightOuterPositions, rightInnerPositions, rightHub,
                leftButtonPositions, leftHub,
                screenWidth));
        }

        public void SnapToGrown()
        {
            if (!treeBuilt) return;
            SnapTimings(rightTimings);
            SnapTimings(leftTimings);
            SnapTimings(borderTimings);
            grown = true;
            animating = false;
            MarkAllDirty();
        }

        // ================================================================
        // Build Coroutine
        // ================================================================

        private IEnumerator BuildAndAnimate(
            List<Vector2> rightOuterPositions, List<Vector2> rightInnerPositions,
            Vector2 rightHub,
            List<Vector2> leftPositions, Vector2 leftHub,
            float screenWidth)
        {
            yield return new WaitForEndOfFrame();

            BuildRightCluster(rightOuterPositions, rightInnerPositions, rightHub);
            BuildLeftCluster(leftPositions, leftHub);
            BuildBottomBorder(screenWidth);

            treeBuilt = true;
            animationTime = 0f;
        }

        // ================================================================
        // Right Cluster — Dual Ring (outer + inner)
        // ================================================================

        private void BuildRightCluster(
            List<Vector2> outerPositions, List<Vector2> innerPositions, Vector2 hub)
        {
            rightRenderer.Clear();
            rightTimings.Clear();

            var rng = new System.Random(42);

            // Corner tendril mass — dense decorative cluster
            BuildCornerMass(rightRenderer, rightTimings, hub, isRight: true, rng);

            // Outer ring connection tendrils + cradles
            for (int i = 0; i < outerPositions.Count; i++)
            {
                float delay = i * UIConstants.WheelPopStagger;
                BuildConnectionTendril(rightRenderer, rightTimings, hub, outerPositions[i],
                    delay, rng, isRight: true, isInnerRing: false);
                BuildButtonCradle(rightRenderer, rightTimings, outerPositions[i], hub,
                    delay, rng, isInnerRing: false);
            }

            // Inner ring connection tendrils + cradles
            float innerBaseDelay = outerPositions.Count * UIConstants.WheelPopStagger + 0.04f;
            for (int i = 0; i < innerPositions.Count; i++)
            {
                float delay = innerBaseDelay + i * UIConstants.WheelPopStagger;
                BuildConnectionTendril(rightRenderer, rightTimings, hub, innerPositions[i],
                    delay, rng, isRight: true, isInnerRing: true);
                BuildButtonCradle(rightRenderer, rightTimings, innerPositions[i], hub,
                    delay, rng, isInnerRing: true);
            }
        }

        // ================================================================
        // Left Cluster
        // ================================================================

        private void BuildLeftCluster(List<Vector2> buttonPositions, Vector2 hub)
        {
            leftRenderer.Clear();
            leftTimings.Clear();

            var rng = new System.Random(137);

            // Corner tendril mass
            BuildCornerMass(leftRenderer, leftTimings, hub, isRight: false, rng);

            // Connection tendrils + cradles
            for (int i = 0; i < buttonPositions.Count; i++)
            {
                float delay = i * UIConstants.WheelPopStagger;
                BuildConnectionTendril(leftRenderer, leftTimings, hub, buttonPositions[i],
                    delay, rng, isRight: false, isInnerRing: false);
                BuildButtonCradle(leftRenderer, leftTimings, buttonPositions[i], hub,
                    delay, rng, isInnerRing: false);
            }
        }

        // ================================================================
        // Connection Tendril — hub to button
        // ================================================================

        private void BuildConnectionTendril(
            UITendrilRenderer renderer, List<BranchTiming> timings,
            Vector2 hub, Vector2 buttonPos, float baseDelay,
            System.Random rng, bool isRight, bool isInnerRing)
        {
            // Select width/opacity based on ring
            float tendrilWidth = isInnerRing
                ? UIConstants.WheelInnerTendrilWidth
                : UIConstants.WheelTendrilWidth;
            float tendrilOpacity = isInnerRing
                ? UIConstants.WheelInnerTendrilOpacity
                : UIConstants.WheelTendrilOpacity;

            // Main connection line — quadratic bezier approximated as 3 control points
            float midX = (hub.x + buttonPos.x) * 0.5f + (float)(rng.NextDouble() - 0.5) * 28f;
            float midY = (hub.y + buttonPos.y) * 0.5f + (float)(rng.NextDouble() - 0.5) * 20f;

            var pts = new List<Vector2> { hub, new Vector2(midX, midY), buttonPos };

            float phase = (float)(rng.NextDouble() * 6.28f);
            var strands = new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams
                {
                    width = tendrilWidth,
                    alpha = tendrilOpacity,
                    waveFrequency = 1.0f,
                    wavePhase = phase
                }
            };

            var branch = renderer.AddBranch(pts, strands, 3f, 0.15f);
            branch.branchColor = UIHelper.InkMutedText;
            timings.Add(new BranchTiming
            {
                branch = branch, startDelay = baseDelay, duration = ConnectionDuration
            });

            // Branch hair 1 — splits off at 35-65% along the tendril
            float hairT1 = 0.35f + (float)rng.NextDouble() * 0.30f;
            Vector2 hairStart1 = SamplePolyline(pts, hairT1);
            float hairAngle1 = (isRight ? 180f : 0f) + (float)(rng.NextDouble() * 40f - 20f);
            float hairLen1 = (isInnerRing ? 14f : 20f) + (float)rng.NextDouble() * (isInnerRing ? 14f : 20f);
            Vector2 hairEnd1 = hairStart1 + AngleToDir(hairAngle1) * hairLen1;

            var hairPts1 = new List<Vector2> { hairStart1, hairEnd1 };
            var hairStrands1 = new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams
                {
                    width = UIConstants.WheelBranchHairWidth,
                    alpha = UIConstants.WheelBranchHairOpacity,
                    waveFrequency = 0.5f,
                    wavePhase = (float)(rng.NextDouble() * 6.28f)
                }
            };

            var hair1 = renderer.AddBranch(hairPts1, hairStrands1, 2f, 0.35f);
            hair1.branchColor = SporefrontColors.ParchmentDark;
            timings.Add(new BranchTiming
            {
                branch = hair1,
                startDelay = baseDelay + ConnectionDuration * hairT1,
                duration = BranchHairDuration
            });

            // Branch hair 2 — further along, 60-85% (skip for inner ring sometimes)
            float hair2Chance = isInnerRing ? 0.5f : 0.3f;
            if (rng.NextDouble() > hair2Chance)
            {
                float hairT2 = 0.60f + (float)rng.NextDouble() * 0.25f;
                Vector2 hairStart2 = SamplePolyline(pts, hairT2);
                float hairAngle2 = hairAngle1 + (float)(rng.NextDouble() * 60f - 30f);
                float hairLen2 = (isInnerRing ? 10f : 15f) + (float)rng.NextDouble() * (isInnerRing ? 10f : 15f);
                Vector2 hairEnd2 = hairStart2 + AngleToDir(hairAngle2) * hairLen2;

                var hairPts2 = new List<Vector2> { hairStart2, hairEnd2 };
                var hairStrands2 = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = UIConstants.WheelBranchHairWidth * 0.8f,
                        alpha = 1.0f,
                        waveFrequency = 0.4f,
                        wavePhase = (float)(rng.NextDouble() * 6.28f)
                    }
                };

                var hair2 = renderer.AddBranch(hairPts2, hairStrands2, 1.5f, 0.40f);
                hair2.branchColor = SporefrontColors.ParchmentDark;
                timings.Add(new BranchTiming
                {
                    branch = hair2,
                    startDelay = baseDelay + ConnectionDuration * hairT2,
                    duration = BranchHairDuration
                });
            }
        }

        // ================================================================
        // Button Cradle — tendril arc that wraps around each button
        // ================================================================

        private void BuildButtonCradle(
            UITendrilRenderer renderer, List<BranchTiming> timings,
            Vector2 buttonCenter, Vector2 hub, float baseDelay,
            System.Random rng, bool isInnerRing)
        {
            float btnHalf = UIConstants.WheelButtonSize * 0.5f;
            float cradleRadius = btnHalf + 12f; // well outside the button edge

            // Direction from hub to button (approach direction)
            Vector2 toButton = buttonCenter - hub;
            float approachAngle = Mathf.Atan2(toButton.y, toButton.x) * Mathf.Rad2Deg;

            // Cradle arc wraps around the far side of the button (away from hub).
            // Start 50° clockwise from approach, sweep 260° — wraps most of the way around.
            float arcStart = approachAngle + 50f;
            float arcSweep = 260f;
            int numPoints = 8;

            var cradlePts = new List<Vector2>();
            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / (numPoints - 1);
                float angle = arcStart + t * arcSweep;
                float rad = angle * Mathf.Deg2Rad;
                // Organic radius variation
                float rv = cradleRadius + (float)(rng.NextDouble() * 6f - 3f);
                cradlePts.Add(buttonCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rv);
            }

            float width = isInnerRing ? 2.0f : 2.5f;
            float alpha = 1.0f;

            var cradleStrands = new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams
                {
                    width = width, alpha = alpha,
                    waveFrequency = 1.8f,
                    wavePhase = (float)(rng.NextDouble() * 6.28f)
                }
            };

            var cradle = renderer.AddBranch(cradlePts, cradleStrands, 2.5f, 2.5f);
            cradle.branchColor = UIHelper.InkMutedText;
            timings.Add(new BranchTiming
            {
                branch = cradle,
                startDelay = baseDelay + ConnectionDuration * 0.8f,
                duration = BranchHairDuration * 1.2f
            });

            // Sub-branches: sprouts radiating outward from the cradle
            int subCount = isInnerRing ? 3 : 4;
            for (int s = 0; s < subCount; s++)
            {
                float subT = (subCount == 1) ? 0.5f
                    : 0.15f + (float)s / (subCount - 1) * 0.7f;
                Vector2 subStart = SamplePolyline(cradlePts, subT);

                // Direction outward from button center
                Vector2 outDir = (subStart - buttonCenter).normalized;
                float outAngle = Mathf.Atan2(outDir.y, outDir.x) * Mathf.Rad2Deg;
                outAngle += (float)(rng.NextDouble() * 30f - 15f);
                float subLen = 16f + (float)rng.NextDouble() * 20f;
                Vector2 subEnd = subStart + AngleToDir(outAngle) * subLen;

                var subPts = new List<Vector2> { subStart, subEnd };
                var subStrands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = 1.0f, alpha = 1.0f,
                        waveFrequency = 0.5f,
                        wavePhase = (float)(rng.NextDouble() * 6.28f)
                    }
                };

                var sub = renderer.AddBranch(subPts, subStrands, 1.5f, 0.3f);
                sub.branchColor = SporefrontColors.ParchmentDark;
                timings.Add(new BranchTiming
                {
                    branch = sub,
                    startDelay = baseDelay + ConnectionDuration * 0.8f
                        + BranchHairDuration * 0.4f * subT,
                    duration = BranchHairDuration * 0.6f
                });
            }
        }

        // ================================================================
        // Corner Tendril Mass
        // ================================================================

        private void BuildCornerMass(
            UITendrilRenderer renderer, List<BranchTiming> timings,
            Vector2 hub, bool isRight, System.Random rng)
        {
            int armCount = 10;
            float baseDelay = 0f;

            for (int a = 0; a < armCount; a++)
            {
                float armT = (float)a / (armCount - 1);

                // Fan angles: right hub fans upper-left; left hub fans upper-right
                float startAngle = isRight ? 100f : 10f;
                float endAngle = isRight ? 190f : 80f;
                float armAngle = Mathf.Lerp(startAngle, endAngle, armT)
                    + (float)(rng.NextDouble() * 16.0 - 8.0);

                // Center arms longer
                float cosScale = Mathf.Abs(Mathf.Cos((armT - 0.5f) * Mathf.PI));
                float armLength = 60f + cosScale * 80f;

                // Varying thickness, full opacity
                float thickness = 1.5f + (float)rng.NextDouble() * 4.5f;
                float opacity = 1.0f;

                Vector2 dir = AngleToDir(armAngle);
                Vector2 perp = new Vector2(-dir.y, dir.x);

                // 4 control points with organic curvature
                var armPts = new List<Vector2>(4);
                armPts.Add(hub);
                for (int j = 1; j < 4; j++)
                {
                    float t = (float)j / 3f;
                    Vector2 basePt = hub + dir * (armLength * t);
                    float curvature = t * 20f * (float)(rng.NextDouble() * 2.0 - 1.0);
                    armPts.Add(basePt + perp * curvature);
                }

                var armStrands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = thickness,
                        alpha = opacity,
                        waveFrequency = 1.2f + (float)(rng.NextDouble() * 0.8f),
                        wavePhase = (float)(rng.NextDouble() * 6.28f)
                    }
                };

                var armBranch = renderer.AddBranch(armPts, armStrands, 4f, 0.25f);
                armBranch.branchColor = UIHelper.InkMutedText;

                float armDelay = baseDelay + Mathf.Abs(a - armCount / 2) * 0.03f;
                timings.Add(new BranchTiming
                {
                    branch = armBranch, startDelay = armDelay, duration = CornerMassDuration
                });

                // Sub-hair at ~60%
                if (rng.NextDouble() > 0.4)
                {
                    Vector2 subStart = SamplePolyline(armPts, 0.6f);
                    float subAngle = armAngle + (float)(rng.NextDouble() * 50.0 - 25.0);
                    float subLen = 15f + (float)rng.NextDouble() * 20f;
                    Vector2 subEnd = subStart + AngleToDir(subAngle) * subLen;

                    var subPts = new List<Vector2> { subStart, subEnd };
                    var subStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams
                        {
                            width = 1.2f, alpha = 1.0f,
                            waveFrequency = 0.8f,
                            wavePhase = (float)(rng.NextDouble() * 6.28f)
                        }
                    };

                    var subBranch = renderer.AddBranch(subPts, subStrands, 2f, 0.35f);
                    subBranch.branchColor = SporefrontColors.ParchmentDark;
                    timings.Add(new BranchTiming
                    {
                        branch = subBranch,
                        startDelay = armDelay + CornerMassDuration * 0.6f,
                        duration = BranchHairDuration
                    });
                }
            }
        }

        // ================================================================
        // Bottom Tendril Border
        // ================================================================

        private void BuildBottomBorder(float screenWidth)
        {
            borderRenderer.Clear();
            borderTimings.Clear();

            var rng = new System.Random(256);
            float halfW = screenWidth * 0.5f;
            float borderH = UIConstants.WheelBorderHeight;

            // 3 horizontal curves at varying thicknesses
            float[] widths = { 4.0f, 2.5f, 1.8f };
            float[] alphas = { 1.0f, 1.0f, 1.0f };
            float[] yOffsets = { borderH * 0.45f, borderH * 0.30f, borderH * 0.60f };

            for (int c = 0; c < 3; c++)
            {
                var pts = new List<Vector2>();
                int numPoints = 10;

                for (int i = 0; i < numPoints; i++)
                {
                    float t = (float)i / (numPoints - 1);
                    float x = Mathf.Lerp(-halfW, halfW, t);

                    // Sinusoidal waviness
                    float waveY = Mathf.Sin(t * Mathf.PI * 3f + c * 1.2f)
                        * borderH * 0.15f;

                    // Thicken near edges (within 15% of each side)
                    float edgeDist = Mathf.Min(t, 1f - t);
                    float edgeThicken = edgeDist < 0.15f ? (1f - edgeDist / 0.15f) * 0.3f : 0f;

                    float y = yOffsets[c] + waveY - borderH * 0.5f;
                    pts.Add(new Vector2(x, y));
                }

                var strands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams
                    {
                        width = widths[c],
                        alpha = alphas[c],
                        waveFrequency = 2.0f + c * 0.5f,
                        wavePhase = c * 2.1f
                    }
                };

                var branch = borderRenderer.AddBranch(pts, strands, 2f, 0.05f);
                branch.branchColor = UIHelper.InkMutedText;

                borderTimings.Add(new BranchTiming
                {
                    branch = branch, startDelay = c * 0.15f, duration = BorderDuration
                });
            }
        }

        // ================================================================
        // Update — growth animation + corner sway
        // ================================================================

        private void Update()
        {
            if (!treeBuilt) return;
            if (!animating && !grown) return;

            if (animating)
            {
                animationTime += Time.deltaTime;
                bool allDone = true;

                allDone &= UpdateTimings(rightTimings);
                allDone &= UpdateTimings(leftTimings);
                allDone &= UpdateTimings(borderTimings);

                if (allDone)
                {
                    animating = false;
                    grown = true;
                }
            }

            // Corner sway — subtle rotation of the cluster renderers
            swayPhaseRight += Time.deltaTime;
            swayPhaseLeft += Time.deltaTime;

            float swayAngle = UIConstants.WheelSwayAngle;
            float swayDuration = UIConstants.WheelSwayDuration;

            if (rightContainerRT != null)
            {
                float rightSway = Mathf.Sin(swayPhaseRight * 2f * Mathf.PI / swayDuration) * swayAngle;
                rightContainerRT.localRotation = Quaternion.Euler(0f, 0f, rightSway);
            }

            if (leftContainerRT != null)
            {
                // Slightly offset phase for left side
                float leftSway = Mathf.Sin((swayPhaseLeft + 2f) * 2f * Mathf.PI / (swayDuration + 2f)) * swayAngle;
                leftContainerRT.localRotation = Quaternion.Euler(0f, 0f, leftSway);
            }

            MarkAllDirty();
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>Updates growth progress for a list of branch timings. Returns true when all done.</summary>
        private bool UpdateTimings(List<BranchTiming> timings)
        {
            bool allDone = true;
            for (int i = 0; i < timings.Count; i++)
            {
                var bt = timings[i];
                float elapsed = animationTime - bt.startDelay;

                if (elapsed <= 0f)
                {
                    bt.branch.growthProgress = 0f;
                    allDone = false;
                }
                else if (elapsed < bt.duration)
                {
                    float t = elapsed / bt.duration;
                    bt.branch.growthProgress = 1f - (1f - t) * (1f - t); // ease-out quad
                    allDone = false;
                }
                else
                {
                    bt.branch.growthProgress = 1f;
                }

                bt.branch.idlePulsePhase = Time.time * 1.5f + i * 0.6f;
            }
            return allDone;
        }

        private void SnapTimings(List<BranchTiming> timings)
        {
            for (int i = 0; i < timings.Count; i++)
                timings[i].branch.growthProgress = 1f;
        }

        private void MarkAllDirty()
        {
            if (rightRenderer != null) rightRenderer.MarkDirty();
            if (leftRenderer != null) leftRenderer.MarkDirty();
            if (borderRenderer != null) borderRenderer.MarkDirty();
        }

        private static Vector2 AngleToDir(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
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
