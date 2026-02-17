// ============================================================================
// FILE: Visual/SelectionRenderer.cs
// PURPOSE: Animated glowing hex selection outline — replaces flat tint (#15)
//          Also supports build preview outlines for multi-hex buildings (#19)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class SelectionRenderer : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private HexCoordinate? selectedCoord;
        private LineRenderer outlineRenderer;
        private LineRenderer glowRenderer;
        private float animationPhase;

        // Build preview
        private List<LineRenderer> previewOutlines = new List<LineRenderer>();
        private List<LineRenderer> previewGlows = new List<LineRenderer>();

        // ================================================================
        // Public API
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
        // Animation
        // ================================================================

        private void Update()
        {
            if (glowRenderer == null || !glowRenderer.enabled) return;

            animationPhase += Time.deltaTime * 3f;
            float alpha = 0.25f + 0.15f * Mathf.Sin(animationPhase);
            var color = new Color(
                SporefrontColors.SporeAmber.r,
                SporefrontColors.SporeAmber.g,
                SporefrontColors.SporeAmber.b, alpha);
            glowRenderer.startColor = color;
            glowRenderer.endColor = color;
        }

        // ================================================================
        // Helpers
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
        }
    }
}
