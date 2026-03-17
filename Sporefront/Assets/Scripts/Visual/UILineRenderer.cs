// ============================================================================
// FILE: Visual/UILineRenderer.cs
// PURPOSE: Custom MaskableGraphic for drawing connection lines between
//          research tree nodes using L-shaped (right-angle) paths
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class UILineRenderer : MaskableGraphic
    {
        public struct LineSegment
        {
            public Vector2 start;
            public Vector2 end;
            public Color color;
        }

        private readonly List<LineSegment> lines = new List<LineSegment>();
        private float lineWidth = 2f;

        public void SetLineWidth(float width)
        {
            lineWidth = width;
            SetVerticesDirty();
        }

        public void Clear()
        {
            lines.Clear();
            SetVerticesDirty();
        }

        public void AddLine(Vector2 start, Vector2 end, Color color)
        {
            lines.Add(new LineSegment { start = start, end = end, color = color });
            SetVerticesDirty();
        }

        /// <summary>
        /// Adds an L-shaped path: horizontal from start to midX, then vertical to end Y,
        /// then horizontal to end X.
        /// </summary>
        public void AddLPath(Vector2 start, Vector2 end, Color color)
        {
            float midX = (start.x + end.x) * 0.5f;
            var corner1 = new Vector2(midX, start.y);
            var corner2 = new Vector2(midX, end.y);

            lines.Add(new LineSegment { start = start, end = corner1, color = color });
            lines.Add(new LineSegment { start = corner1, end = corner2, color = color });
            lines.Add(new LineSegment { start = corner2, end = end, color = color });
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            foreach (var line in lines)
            {
                AddLineQuad(vh, line.start, line.end, line.color);
            }
        }

        private void AddLineQuad(VertexHelper vh, Vector2 start, Vector2 end, Color color)
        {
            Vector2 dir = (end - start);
            if (dir.sqrMagnitude < 0.01f) return;

            dir.Normalize();
            Vector2 perp = new Vector2(-dir.y, dir.x) * (lineWidth * 0.5f);

            int baseIdx = vh.currentVertCount;

            vh.AddVert(start + perp, color, Vector4.zero);
            vh.AddVert(start - perp, color, Vector4.zero);
            vh.AddVert(end - perp, color, Vector4.zero);
            vh.AddVert(end + perp, color, Vector4.zero);

            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
        }
    }
}
