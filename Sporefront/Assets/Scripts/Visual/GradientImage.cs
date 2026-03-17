// ============================================================================
// FILE: Visual/GradientImage.cs
// PURPOSE: UI graphic that renders a solid vertical gradient between two colors.
//          Use as a lightweight scrim: opaque at one edge, transparent at the other.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    [AddComponentMenu("UI/Gradient Image")]
    public class GradientImage : MaskableGraphic
    {
        // Color at the top edge of the rect
        public Color topColor    = Color.clear;
        // Color at the bottom edge of the rect
        public Color bottomColor = Color.clear;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();

            UIVertex v = UIVertex.simpleVert;
            v.uv0 = Vector2.zero;

            // Bottom-left
            v.position = new Vector3(r.xMin, r.yMin);
            v.color    = bottomColor;
            vh.AddVert(v);

            // Bottom-right
            v.position = new Vector3(r.xMax, r.yMin);
            v.color    = bottomColor;
            vh.AddVert(v);

            // Top-right
            v.position = new Vector3(r.xMax, r.yMax);
            v.color    = topColor;
            vh.AddVert(v);

            // Top-left
            v.position = new Vector3(r.xMin, r.yMax);
            v.color    = topColor;
            vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
