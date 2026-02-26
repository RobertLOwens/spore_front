// ============================================================================
// FILE: Visual/SelectionRenderer.cs
// PURPOSE: Animated glowing hex selection outline — replaces flat tint (#15)
//          Also supports build preview outlines for multi-hex buildings (#19)
//          Entity highlights use pulsing 5-strand mycelium rings that match
//          PathRenderer's closed-loop Catmull-Rom style. Tendrils attach to
//          entity GameObjects so they follow during movement.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class SelectionRenderer : MonoBehaviour
    {
        // ================================================================
        // Constants — Mycelium Strand Parameters (matching PathRenderer)
        // ================================================================

        private const int StrandCount = 5;
        private const int CircleControlPoints = 12;
        private const int SubdivisionsPerSegment = 6;
        private const float CircleMaxOffset = 0.025f;
        private const float TendrilBaseRadius = 0.30f;
        private const float TendrilPulseSpeed = 3.5f;
        private const float ZPosition = -0.018f;

        private static readonly (float freq, float phase)[] StrandWaveParams = new[]
        {
            (3.5f, 0.0f),
            (4.0f, 1.2f),
            (3.0f, 2.5f),
            (4.5f, 3.8f),
            (3.8f, 5.0f)
        };

        private static readonly float[] StrandWidths = new[] { 0.008f, 0.007f, 0.009f, 0.006f, 0.010f };
        private static readonly float[] StrandAlphas = new[] { 1.0f, 0.75f, 0.85f, 0.65f, 0.95f };

        // ================================================================
        // References
        // ================================================================

        private EntityRenderer entityRenderer;

        // ================================================================
        // State — Tile Selection (amber hex outline, unchanged)
        // ================================================================

        private HexCoordinate? selectedCoord;
        private LineRenderer outlineRenderer;
        private LineRenderer glowRenderer;
        private float animationPhase;

        // Build preview
        private List<LineRenderer> previewOutlines = new List<LineRenderer>();
        private List<LineRenderer> previewGlows = new List<LineRenderer>();

        // ================================================================
        // State — Entity Tendrils (5-strand mycelium rings)
        // ================================================================

        private GameObject entityTendrilRoot;
        private LineRenderer[] entityTendrilStrands;
        private float entityTendrilPhase;
        private Color entityTendrilColor;

        // Multi-entity tendrils (drag-select) — keyed by entity ID
        private Dictionary<Guid, (GameObject root, LineRenderer[] strands)> multiEntityTendrils
            = new Dictionary<Guid, (GameObject, LineRenderer[])>();
        private float multiEntityTendrilPhase;
        private Color multiEntityTendrilColor;

        // Reusable buffer for diff-based updates (avoids per-frame allocation)
        private List<Guid> toRemoveBuffer = new List<Guid>();

        // ================================================================
        // Setup
        // ================================================================

        public void SetEntityRenderer(EntityRenderer er)
        {
            entityRenderer = er;
        }

        // ================================================================
        // Public API — Tile Selection (unchanged)
        // ================================================================

        public void ShowSelection(HexCoordinate coord)
        {
            selectedCoord = coord;
            EnsureRenderers();

            Vector3 center = HexMetrics.HexToWorldPosition(coord);
            var points = GetHexOutlinePoints(center);

            ApplyPoints(outlineRenderer, points);
            outlineRenderer.startWidth = 0.025f;
            outlineRenderer.endWidth = 0.025f;
            outlineRenderer.startColor = SporefrontColors.SporeAmber;
            outlineRenderer.endColor = SporefrontColors.SporeAmber;
            outlineRenderer.enabled = true;

            ApplyPoints(glowRenderer, points);
            glowRenderer.startWidth = 0.06f;
            glowRenderer.endWidth = 0.06f;
            var glowColor = new Color(
                SporefrontColors.SporeAmber.r,
                SporefrontColors.SporeAmber.g,
                SporefrontColors.SporeAmber.b, 0.4f);
            glowRenderer.startColor = glowColor;
            glowRenderer.endColor = glowColor;
            glowRenderer.enabled = true;
        }

        public void HideSelection()
        {
            selectedCoord = null;
            if (outlineRenderer != null) outlineRenderer.enabled = false;
            if (glowRenderer != null) glowRenderer.enabled = false;
        }

        // ================================================================
        // Public API — Build Preview (unchanged)
        // ================================================================

        public void ShowBuildPreview(List<HexCoordinate> coords, bool isValid)
        {
            ClearBuildPreview();
            var color = isValid ? SporefrontColors.SporeGreen : SporefrontColors.SporeRed;

            foreach (var coord in coords)
            {
                Vector3 center = HexMetrics.HexToWorldPosition(coord);
                var points = GetHexOutlinePoints(center);

                var outline = CreateLineRenderer("PreviewOutline", color, 0.025f, -0.016f);
                ApplyPoints(outline, points);
                previewOutlines.Add(outline);

                var glowColor = new Color(color.r, color.g, color.b, 0.35f);
                var glow = CreateLineRenderer("PreviewGlow", glowColor, 0.055f, -0.017f);
                ApplyPoints(glow, points);
                previewGlows.Add(glow);
            }
        }

        public void ClearBuildPreview()
        {
            foreach (var lr in previewOutlines) if (lr != null) Destroy(lr.gameObject);
            foreach (var lr in previewGlows) if (lr != null) Destroy(lr.gameObject);
            previewOutlines.Clear();
            previewGlows.Clear();
        }

        // ================================================================
        // Public API — Entity Tendril Highlights
        // ================================================================

        public void ShowEntityHighlight(HexCoordinate coord, Color color, Guid? entityID = null)
        {
            // Destroy previous
            if (entityTendrilRoot != null)
            {
                Destroy(entityTendrilRoot);
                entityTendrilRoot = null;
                entityTendrilStrands = null;
            }

            entityTendrilColor = color;
            int seed = coord.GetHashCode();

            // Try to attach to entity transform for movement tracking
            Transform entityTransform = null;
            if (entityID.HasValue && entityRenderer != null)
                entityTransform = entityRenderer.GetEntityTransform(entityID.Value);

            if (entityTransform != null)
            {
                // Local-space: parent to entity, center at zero
                var result = BuildSelectionTendrils(Vector3.zero, color, seed, localSpace: true);
                entityTendrilRoot = result.root;
                entityTendrilStrands = result.strands;
                entityTendrilRoot.transform.SetParent(entityTransform, false);
                entityTendrilRoot.transform.localPosition = Vector3.zero;
            }
            else
            {
                // World-space fallback: position at hex center
                Vector3 center = HexMetrics.HexToWorldPosition(coord);
                var result = BuildSelectionTendrils(center, color, seed, localSpace: false);
                entityTendrilRoot = result.root;
                entityTendrilStrands = result.strands;
            }
        }

        public void HideEntityHighlight()
        {
            if (entityTendrilRoot != null)
            {
                Destroy(entityTendrilRoot);
                entityTendrilRoot = null;
                entityTendrilStrands = null;
            }
        }

        public void ShowMultiEntityHighlight(List<Guid> entityIDs, Color color)
        {
            ClearMultiEntityHighlight();
            multiEntityTendrilColor = color;

            foreach (var id in entityIDs)
            {
                Transform entityTransform = entityRenderer != null
                    ? entityRenderer.GetEntityTransform(id) : null;

                if (entityTransform != null)
                {
                    int seed = id.GetHashCode();
                    var result = BuildSelectionTendrils(Vector3.zero, color, seed, localSpace: true);
                    result.root.transform.SetParent(entityTransform, false);
                    result.root.transform.localPosition = Vector3.zero;
                    multiEntityTendrils[id] = result;
                }
            }
        }

        /// <summary>
        /// Diff-based update: only adds/removes tendrils for changed entities.
        /// Call every frame during drag-select to avoid rebuilding all tendrils.
        /// </summary>
        public void UpdateMultiEntityHighlight(HashSet<Guid> entityIDs, Color color)
        {
            multiEntityTendrilColor = color;

            // Remove tendrils for entities no longer in set
            toRemoveBuffer.Clear();
            foreach (var kvp in multiEntityTendrils)
            {
                if (!entityIDs.Contains(kvp.Key))
                    toRemoveBuffer.Add(kvp.Key);
            }
            for (int i = 0; i < toRemoveBuffer.Count; i++)
            {
                var id = toRemoveBuffer[i];
                if (multiEntityTendrils.TryGetValue(id, out var entry))
                {
                    if (entry.root != null) Destroy(entry.root);
                    multiEntityTendrils.Remove(id);
                }
            }

            // Add tendrils for new entities
            foreach (var id in entityIDs)
            {
                if (multiEntityTendrils.ContainsKey(id)) continue;

                Transform entityTransform = entityRenderer != null
                    ? entityRenderer.GetEntityTransform(id) : null;

                if (entityTransform != null)
                {
                    int seed = id.GetHashCode();
                    var result = BuildSelectionTendrils(Vector3.zero, color, seed, localSpace: true);
                    result.root.transform.SetParent(entityTransform, false);
                    result.root.transform.localPosition = Vector3.zero;
                    multiEntityTendrils[id] = result;
                }
            }
        }

        public void ClearMultiEntityHighlight()
        {
            foreach (var kvp in multiEntityTendrils)
            {
                if (kvp.Value.root != null) Destroy(kvp.Value.root);
            }
            multiEntityTendrils.Clear();
        }

        // ================================================================
        // Animation
        // ================================================================

        private void Update()
        {
            // Animate amber tile selection glow (unchanged)
            if (glowRenderer != null && glowRenderer.enabled)
            {
                animationPhase += Time.deltaTime * 3f;
                float alpha = 0.25f + 0.15f * Mathf.Sin(animationPhase);
                var color = new Color(
                    SporefrontColors.SporeAmber.r,
                    SporefrontColors.SporeAmber.g,
                    SporefrontColors.SporeAmber.b, alpha);
                glowRenderer.startColor = color;
                glowRenderer.endColor = color;
            }

            // Animate entity tendrils — pulse alpha via gradient
            if (entityTendrilStrands != null)
            {
                entityTendrilPhase += Time.deltaTime * TendrilPulseSpeed;
                AnimateTendrilStrands(entityTendrilStrands, entityTendrilColor, entityTendrilPhase);
            }

            // Animate multi-entity tendrils
            if (multiEntityTendrils.Count > 0)
            {
                multiEntityTendrilPhase += Time.deltaTime * TendrilPulseSpeed;
                foreach (var kvp in multiEntityTendrils)
                {
                    if (kvp.Value.strands != null)
                        AnimateTendrilStrands(kvp.Value.strands, multiEntityTendrilColor, multiEntityTendrilPhase);
                }
            }
        }

        private void AnimateTendrilStrands(LineRenderer[] strands, Color baseColor, float phase)
        {
            for (int i = 0; i < strands.Length; i++)
            {
                if (strands[i] == null) continue;

                float staggeredPhase = phase + i * 0.6f;
                float pulse = 0.6f + 0.3f * Mathf.Sin(staggeredPhase);
                float alpha = StrandAlphas[i] * pulse;

                var gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(baseColor, 0f),
                        new GradientColorKey(baseColor, 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(alpha, 0f),
                        new GradientAlphaKey(alpha, 1f)
                    }
                );
                strands[i].colorGradient = gradient;
            }
        }

        // ================================================================
        // Tendril Construction — Closed-Loop Catmull-Rom Mycelium Strands
        // ================================================================

        private (GameObject root, LineRenderer[] strands) BuildSelectionTendrils(
            Vector3 center, Color color, int seed, bool localSpace)
        {
            var rootGO = new GameObject("SelectionTendrils");
            if (!localSpace)
                rootGO.transform.SetParent(transform, false);

            float zOffset = localSpace ? -0.005f : ZPosition;
            if (!localSpace)
                center.z = zOffset;

            // Generate control points evenly around a circle
            var controlPoints = new Vector3[CircleControlPoints];
            float[] cumulativeDistances = new float[CircleControlPoints + 1];
            cumulativeDistances[0] = 0f;

            for (int i = 0; i < CircleControlPoints; i++)
            {
                float angle = (i / (float)CircleControlPoints) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * TendrilBaseRadius;
                float y = Mathf.Sin(angle) * TendrilBaseRadius * HexMetrics.IsometricYScale;
                controlPoints[i] = center + new Vector3(x, y, zOffset);
            }

            // Compute cumulative distances around the closed loop
            for (int i = 0; i < CircleControlPoints; i++)
            {
                int next = (i + 1) % CircleControlPoints;
                float segLen = Vector3.Distance(controlPoints[i], controlPoints[next]);
                cumulativeDistances[i + 1] = cumulativeDistances[i] + segLen;
            }
            float totalLength = cumulativeDistances[CircleControlPoints];

            // Build 5 strands
            var allStrands = new LineRenderer[StrandCount];
            for (int s = 0; s < StrandCount; s++)
            {
                allStrands[s] = CreateMyceliumStrand(
                    rootGO, s, controlPoints, cumulativeDistances, totalLength,
                    color, StrandWidths[s], seed, zOffset, !localSpace);
            }

            return (rootGO, allStrands);
        }

        private LineRenderer CreateMyceliumStrand(GameObject parent, int strandIndex,
            Vector3[] controlPoints, float[] cumulativeDistances, float totalLength,
            Color color, float width, int seed, float zOffset, bool useWorldSpace)
        {
            var go = new GameObject($"Strand_{strandIndex}");
            go.transform.SetParent(parent.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = useWorldSpace;
            lr.loop = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.sortingOrder = 5;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;

            // Set initial gradient
            float strandAlpha = StrandAlphas[strandIndex];
            var initialGradient = new Gradient();
            initialGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(strandAlpha, 0f),
                    new GradientAlphaKey(strandAlpha, 1f)
                }
            );
            lr.colorGradient = initialGradient;

            int n = CircleControlPoints;
            var waveParams = StrandWaveParams[strandIndex];

            // Deterministic per-strand phase jitter from seed
            float seedPhase = ((seed * 31 + strandIndex * 7) & 0xFFFF) / (float)0xFFFF * Mathf.PI * 2f;

            // Subdivide each segment with Catmull-Rom (closed loop wrapping)
            var points = new List<Vector3>();
            float distSoFar = 0f;

            for (int seg = 0; seg < n; seg++)
            {
                float segLen = cumulativeDistances[seg + 1] - cumulativeDistances[seg];

                // Catmull-Rom control points with wrap-around indexing
                Vector3 p0 = controlPoints[((seg - 1) % n + n) % n];
                Vector3 p1 = controlPoints[seg];
                Vector3 p2 = controlPoints[(seg + 1) % n];
                Vector3 p3 = controlPoints[(seg + 2) % n];

                // Don't include endpoint on last segment (loop=true handles closure)
                int steps = SubdivisionsPerSegment;

                for (int sub = 0; sub < steps; sub++)
                {
                    float localT = sub / (float)SubdivisionsPerSegment;

                    Vector3 basePos = CatmullRom(p0, p1, p2, p3, localT);
                    Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, localT);

                    float dist = distSoFar + segLen * localT;
                    float t = totalLength > 0.001f ? dist / totalLength : 0f;

                    // Sinusoidal radial offset
                    float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);
                    float offset = wave * CircleMaxOffset;

                    // Perpendicular from spline tangent
                    Vector3 dir = tangent.normalized;
                    if (dir.sqrMagnitude < 0.0001f)
                        dir = (p2 - p1).normalized;
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

                    Vector3 finalPos = basePos + perp * offset;
                    finalPos.z = zOffset;
                    points.Add(finalPos);
                }

                distSoFar += segLen;
            }

            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            return lr;
        }

        // ================================================================
        // Catmull-Rom Spline (matching PathRenderer)
        // ================================================================

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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

        private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
                (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2
            );
        }

        // ================================================================
        // Helpers — Hex Outline (for tile selection + build preview)
        // ================================================================

        private void EnsureRenderers()
        {
            if (outlineRenderer == null)
                outlineRenderer = CreateLineRenderer("SelectionOutline",
                    SporefrontColors.SporeAmber, 0.025f, -0.015f);

            if (glowRenderer == null)
                glowRenderer = CreateLineRenderer("SelectionGlow",
                    SporefrontColors.SporeAmber, 0.06f, -0.016f);
        }

        private LineRenderer CreateLineRenderer(string name, Color color, float width, float zPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, zPos);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            // Use unlit material
            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.sortingOrder = 5;

            return lr;
        }

        private Vector3[] GetHexOutlinePoints(Vector3 center)
        {
            var corners = HexMetrics.GetHexCorners();
            var points = new Vector3[7]; // 6 corners + close loop
            for (int i = 0; i < 6; i++)
            {
                points[i] = center + corners[i];
                points[i].z = -0.015f;
            }
            points[6] = points[0]; // Close the loop
            return points;
        }

        private void ApplyPoints(LineRenderer lr, Vector3[] points)
        {
            lr.positionCount = points.Length;
            lr.SetPositions(points);
        }

        private void OnDestroy()
        {
            ClearBuildPreview();
            HideEntityHighlight();
            ClearMultiEntityHighlight();
        }
    }
}
