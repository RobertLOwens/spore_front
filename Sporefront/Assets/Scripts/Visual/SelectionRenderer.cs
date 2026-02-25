// ============================================================================
// FILE: Visual/SelectionRenderer.cs
// PURPOSE: Animated glowing hex selection outline — replaces flat tint (#15)
//          Also supports build preview outlines for multi-hex buildings (#19)
//          Entity highlights use pulsing circle rings.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class SelectionRenderer : MonoBehaviour
    {
        // ================================================================
        // Constants — Circle Selection Rendering
        // ================================================================

        private const int CircleSegments = 32;
        private const float CircleRadius = 0.35f;
        private const float CircleLineWidth = 0.012f;
        private const float CircleGlowWidth = 0.035f;
        private const float ZPosition = -0.018f;

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
        // State — Entity Tendrils (replaces hex-outline entity highlight)
        // ================================================================

        private GameObject entityTendrilRoot;
        private LineRenderer[] entityTendrilStrands;
        private float entityTendrilPhase;
        private Color entityTendrilColor;

        // Multi-entity tendrils (drag-select)
        private Dictionary<HexCoordinate, (GameObject root, LineRenderer[] strands)> multiEntityTendrils
            = new Dictionary<HexCoordinate, (GameObject, LineRenderer[])>();
        private float multiEntityTendrilPhase;
        private Color multiEntityTendrilColor;

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

        public void ShowEntityHighlight(HexCoordinate coord, Color color)
        {
            // Destroy previous
            if (entityTendrilRoot != null)
            {
                Destroy(entityTendrilRoot);
                entityTendrilRoot = null;
                entityTendrilStrands = null;
            }

            entityTendrilColor = color;
            Vector3 center = HexMetrics.HexToWorldPosition(coord);
            int seed = coord.GetHashCode();

            var result = BuildSelectionTendrils(center, color, seed);
            entityTendrilRoot = result.root;
            entityTendrilStrands = result.strands;
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

        public void ShowMultiEntityHighlight(List<HexCoordinate> coords, Color color)
        {
            ClearMultiEntityHighlight();
            multiEntityTendrilColor = color;

            foreach (var coord in coords)
            {
                Vector3 center = HexMetrics.HexToWorldPosition(coord);
                int seed = coord.GetHashCode();

                var result = BuildSelectionTendrils(center, color, seed);
                multiEntityTendrils[coord] = result;
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
                entityTendrilPhase += Time.deltaTime * 3.5f;
                AnimateTendrilStrands(entityTendrilStrands, entityTendrilColor, entityTendrilPhase);
            }

            // Animate multi-entity tendrils
            if (multiEntityTendrils.Count > 0)
            {
                multiEntityTendrilPhase += Time.deltaTime * 3.5f;
                foreach (var kvp in multiEntityTendrils)
                {
                    if (kvp.Value.strands != null)
                        AnimateTendrilStrands(kvp.Value.strands, multiEntityTendrilColor, multiEntityTendrilPhase);
                }
            }
        }

        private void AnimateTendrilStrands(LineRenderer[] strands, Color baseColor, float phase)
        {
            float pulse = 0.6f + 0.3f * Mathf.Sin(phase);

            for (int i = 0; i < strands.Length; i++)
            {
                if (strands[i] == null) continue;

                // Index 0 = main circle line, index 1 = glow
                float alpha = (i == 0) ? pulse : pulse * 0.45f;
                var c = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                strands[i].startColor = c;
                strands[i].endColor = c;
            }
        }

        // ================================================================
        // Tendril Construction
        // ================================================================

        private (GameObject root, LineRenderer[] strands) BuildSelectionTendrils(
            Vector3 center, Color color, int seed)
        {
            var rootGO = new GameObject("SelectionCircle");
            rootGO.transform.SetParent(transform, false);

            center.z = ZPosition;

            // Build circle points
            var points = new Vector3[CircleSegments + 1];
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                points[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * CircleRadius;
                points[i].z = ZPosition;
            }

            // Main circle line
            var mainGO = new GameObject("CircleLine");
            mainGO.transform.SetParent(rootGO.transform, false);
            var mainLR = mainGO.AddComponent<LineRenderer>();
            mainLR.useWorldSpace = true;
            mainLR.loop = false;
            mainLR.startWidth = CircleLineWidth;
            mainLR.endWidth = CircleLineWidth;
            mainLR.numCapVertices = 3;
            mainLR.numCornerVertices = 2;
            mainLR.sortingOrder = 5;
            mainLR.material = new Material(Shader.Find("Sprites/Default"));
            mainLR.startColor = color;
            mainLR.endColor = color;
            mainLR.positionCount = points.Length;
            mainLR.SetPositions(points);

            // Glow circle (wider, semi-transparent)
            var glowGO = new GameObject("CircleGlow");
            glowGO.transform.SetParent(rootGO.transform, false);
            var glowLR = glowGO.AddComponent<LineRenderer>();
            glowLR.useWorldSpace = true;
            glowLR.loop = false;
            glowLR.startWidth = CircleGlowWidth;
            glowLR.endWidth = CircleGlowWidth;
            glowLR.numCapVertices = 3;
            glowLR.numCornerVertices = 2;
            glowLR.sortingOrder = 4;
            glowLR.material = new Material(Shader.Find("Sprites/Default"));
            var glowColor = new Color(color.r, color.g, color.b, 0.4f);
            glowLR.startColor = glowColor;
            glowLR.endColor = glowColor;
            glowLR.positionCount = points.Length;
            glowLR.SetPositions(points);

            return (rootGO, new LineRenderer[] { mainLR, glowLR });
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
