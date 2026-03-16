// ============================================================================
// FILE: Visual/UITendrilRenderer.cs
// PURPOSE: Custom MaskableGraphic for drawing animated multi-strand Catmull-Rom
//          spline paths. Used for the main menu tendril tree animation.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class UITendrilRenderer : MaskableGraphic
    {
        // ================================================================
        // Data Structures
        // ================================================================

        public struct StrandParams
        {
            public float width;
            public float alpha;
            public float waveFrequency;
            public float wavePhase;
        }

        public class TendrilBranch
        {
            public List<Vector2> controlPoints = new List<Vector2>();
            public List<StrandParams> strands = new List<StrandParams>();
            public float maxOffset = 6f;
            public float taperFraction = 0.15f;
            public float growthProgress; // 0..1
            public float idlePulsePhase;
            public Color branchColor = Color.white;
            public float glowAlpha = 0f;           // 0 = no glow; e.g. 0.06f for very faint
            public float glowWidthMultiplier = 3f; // how much wider the glow pass is
        }

        // ================================================================
        // State
        // ================================================================

        private readonly List<TendrilBranch> branches = new List<TendrilBranch>();
        private const int SubdivisionsPerSegment = 4;
        private const int MaxVertices = 60000;

        // ================================================================
        // Public API
        // ================================================================

        public void Clear()
        {
            branches.Clear();
            SetVerticesDirty();
        }

        public TendrilBranch AddBranch(List<Vector2> controlPoints, List<StrandParams> strands,
            float maxOffset, float taperFraction)
        {
            var branch = new TendrilBranch
            {
                controlPoints = controlPoints,
                strands = strands,
                maxOffset = maxOffset,
                taperFraction = taperFraction,
                growthProgress = 0f,
                idlePulsePhase = 0f
            };
            branches.Add(branch);
            SetVerticesDirty();
            return branch;
        }

        public void MarkDirty()
        {
            SetVerticesDirty();
        }

        // ================================================================
        // Catmull-Rom Math (Vector2 port from PathRenderer)
        // ================================================================

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector2 CatmullRomTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
                (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2
            );
        }

        /// Deterministic spatial hash — returns 0..1 for any input position + seed.
        private static float InkNoise(float position, float seed)
        {
            float x = position * 7.3f + seed * 131.7f;
            x = Mathf.Sin(x * 127.1f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        // ================================================================
        // Mesh Generation
        // ================================================================

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            foreach (var branch in branches)
            {
                if (branch.controlPoints.Count < 2 || branch.growthProgress <= 0f)
                    continue;

                // Glow pass — white, wider strands, very low alpha, drawn behind main strands
                if (branch.glowAlpha > 0f)
                {
                    foreach (var strand in branch.strands)
                    {
                        if (vh.currentVertCount >= MaxVertices) return;
                        var glowStrand = strand;
                        glowStrand.alpha = branch.glowAlpha;
                        glowStrand.width *= branch.glowWidthMultiplier;
                        DrawStrand(vh, branch, glowStrand, Color.white);
                    }
                }

                // Main strands
                foreach (var strand in branch.strands)
                {
                    if (vh.currentVertCount >= MaxVertices) return;
                    DrawStrand(vh, branch, strand);
                }
            }

        }

        private void DrawStrand(VertexHelper vh, TendrilBranch branch, StrandParams strand,
            Color? colorOverride = null)
        {
            var pts = branch.controlPoints;
            if (pts.Count < 2) return;

            // Build subdivided points along the spline
            var splinePoints = new List<Vector2>();
            var cumulativeDistances = new List<float>();
            float totalLength = 0f;

            // Compute cumulative distances between control points
            var segLengths = new List<float>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                segLengths.Add(len);
                totalLength += len;
            }

            if (totalLength < 0.1f) return;

            float distSoFar = 0f;

            for (int seg = 0; seg < pts.Count - 1; seg++)
            {
                float segLen = segLengths[seg];

                // Catmull-Rom control points with reflection at endpoints
                Vector2 p1 = pts[seg];
                Vector2 p2 = pts[seg + 1];
                Vector2 p0 = seg > 0 ? pts[seg - 1] : 2f * p1 - p2;
                Vector2 p3 = seg + 2 < pts.Count ? pts[seg + 2] : 2f * p2 - p1;

                int steps = (seg == pts.Count - 2) ? SubdivisionsPerSegment + 1 : SubdivisionsPerSegment;

                for (int sub = 0; sub < steps; sub++)
                {
                    float localT = sub / (float)SubdivisionsPerSegment;

                    Vector2 basePos = CatmullRom(p0, p1, p2, p3, localT);
                    Vector2 tangent = CatmullRomTangent(p0, p1, p2, p3, localT);

                    float dist = distSoFar + segLen * localT;
                    float t = dist / totalLength;

                    // Sinusoidal perpendicular offset
                    float wave = Mathf.Sin(t * strand.waveFrequency * Mathf.PI * 2f + strand.wavePhase);

                    // Strands spread apart toward the tip instead of converging
                    float spread = 1f + t * 0.8f;
                    // Per-strand drift so each strand ends in a different place
                    float drift = (strand.wavePhase - 2.5f) * t * 0.3f;
                    float offset = (wave + drift) * branch.maxOffset * spread;

                    // Perpendicular from tangent
                    Vector2 dir = tangent.normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = (p2 - p1).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);

                    float edgeJitter = (InkNoise(t * 3f, strand.wavePhase + 17f) - 0.5f) * strand.width * 0.45f;
                    Vector2 finalPos = basePos + perp * (offset + edgeJitter);
                    splinePoints.Add(finalPos);
                    cumulativeDistances.Add(t);
                }

                distSoFar += segLen;
            }

            if (splinePoints.Count < 2) return;

            // Compute per-vertex alpha based on growth progress and idle pulse
            float growthProgress = branch.growthProgress;
            float pulsePhase = branch.idlePulsePhase;

            Color baseColor = colorOverride.HasValue ? colorOverride.Value : branch.branchColor;

            // Draw quads between consecutive spline points
            for (int i = 0; i < splinePoints.Count - 1; i++)
            {
                Vector2 start = splinePoints[i];
                Vector2 end = splinePoints[i + 1];

                float tStart = cumulativeDistances[i];
                float tEnd = cumulativeDistances[i + 1];

                // Growth front clipping with fade band
                float fadeWidth = 0.08f;
                float alphaStart = ComputeGrowthAlpha(tStart, growthProgress, fadeWidth);
                float alphaEnd = ComputeGrowthAlpha(tEnd, growthProgress, fadeWidth);

                if (alphaStart <= 0f && alphaEnd <= 0f) continue;

                // Width taper at end
                float taperStart = 1f;
                float taperEnd = 1f;
                if (tStart > 1f - branch.taperFraction)
                    taperStart = Mathf.Max(0f, (1f - tStart) / branch.taperFraction);
                if (tEnd > 1f - branch.taperFraction)
                    taperEnd = Mathf.Max(0f, (1f - tEnd) / branch.taperFraction);

                // Ink doesn't breathe — no idle pulse
                float pulseStart = 1f;

                float finalAlphaStart = strand.alpha * alphaStart * pulseStart;
                float finalAlphaEnd = strand.alpha * alphaEnd * pulseStart;

                float widthStart = strand.width * taperStart;
                float widthEnd = strand.width * taperEnd;

                // Ink splotch — irregular density and thickness
                float noiseStart = InkNoise(tStart, strand.wavePhase);
                float noiseEnd   = InkNoise(tEnd,   strand.wavePhase);
                finalAlphaStart *= 0.2f + 0.8f * noiseStart;
                finalAlphaEnd   *= 0.2f + 0.8f * noiseEnd;
                widthStart      *= 0.4f + 1.2f * noiseStart;
                widthEnd        *= 0.4f + 1.2f * noiseEnd;

                // Large-scale ink pooling — slower spatial frequency for blob clusters
                float poolStart = InkNoise(tStart * 0.7f, strand.wavePhase + 99f);
                float poolEnd   = InkNoise(tEnd   * 0.7f, strand.wavePhase + 99f);
                if (poolStart > 0.6f) widthStart *= 1.0f + (poolStart - 0.6f) * 1.0f;
                if (poolEnd   > 0.6f) widthEnd   *= 1.0f + (poolEnd   - 0.6f) * 1.0f;

                AddLineQuad(vh, start, end, baseColor, widthStart, widthEnd,
                    finalAlphaStart, finalAlphaEnd);
            }
        }

        private static float ComputeGrowthAlpha(float t, float growthProgress, float fadeWidth)
        {
            if (t <= growthProgress - fadeWidth)
                return 1f;
            if (t >= growthProgress)
                return 0f;
            // Smoothstep-style fade
            float x = (growthProgress - t) / fadeWidth;
            return x * x * (3f - 2f * x);
        }

        private void AddLineQuad(VertexHelper vh, Vector2 start, Vector2 end, Color baseColor,
            float widthStart, float widthEnd, float alphaStart, float alphaEnd)
        {
            Vector2 dir = end - start;
            if (dir.sqrMagnitude < 0.01f) return;

            dir.Normalize();
            Vector2 perpStart = new Vector2(-dir.y, dir.x) * (widthStart * 0.5f);
            Vector2 perpEnd = new Vector2(-dir.y, dir.x) * (widthEnd * 0.5f);

            Color colorStart = new Color(baseColor.r, baseColor.g, baseColor.b, alphaStart);
            Color colorEnd = new Color(baseColor.r, baseColor.g, baseColor.b, alphaEnd);

            int baseIdx = vh.currentVertCount;

            vh.AddVert(start + perpStart, colorStart, Vector4.zero);
            vh.AddVert(start - perpStart, colorStart, Vector4.zero);
            vh.AddVert(end - perpEnd, colorEnd, Vector4.zero);
            vh.AddVert(end + perpEnd, colorEnd, Vector4.zero);

            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
        }
    }
}
