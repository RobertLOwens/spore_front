// ============================================================================
// FILE: Visual/ResourceIconGenerator.cs
// PURPOSE: Procedural stroke-style resource icons matching the parchment theme.
//          Generates Wood, Food, Stone, Ore, and Population icons as Sprites.
//          Each icon is a 48x48 white-on-transparent texture meant to be tinted
//          via Image.color (e.g., InkDark for standard, SporeRed for warnings).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Sporefront.Visual
{
    public static class ResourceIconGenerator
    {
        private const int Size = 48;
        private const float Scale = 2f;         // SVG 24x24 viewBox → 48px texture
        private const float StrokeW = 3.2f;     // 1.6 SVG stroke-width * 2x scale
        private const float AA = 1.2f;          // anti-alias softness in pixels

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static Sprite _noiseSprite;

        // ================================================================
        // Public API
        // ================================================================

        public static Sprite GetIcon(string name)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;

            float[] buf = new float[Size * Size];
            switch (name)
            {
                case "wood":       DrawWood(buf); break;
                case "food":       DrawFood(buf); break;
                case "stone":      DrawStone(buf); break;
                case "ore":        DrawOre(buf); break;
                case "population": DrawPopulation(buf); break;
                default: return null;
            }

            var sprite = BufToSprite(buf);
            _cache[name] = sprite;
            return sprite;
        }

        /// <summary>
        /// Small tiled noise texture for parchment paper-grain overlay.
        /// </summary>
        public static Sprite GetNoiseSprite()
        {
            if (_noiseSprite != null) return _noiseSprite;

            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[s * s];
            var rng = new System.Random(42);
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = (float)rng.NextDouble();
                pixels[i] = new Color(v, v, v, 1f);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _noiseSprite = Sprite.Create(tex, new Rect(0, 0, s, s),
                new Vector2(0.5f, 0.5f), 100f);
            return _noiseSprite;
        }

        // ================================================================
        // Coordinate Mapping — SVG (0,0 top-left) → Texture (0,0 bottom-left)
        // ================================================================

        static Vector2 P(float svgX, float svgY)
        {
            return new Vector2(svgX * Scale, (24f - svgY) * Scale);
        }

        // ================================================================
        // Drawing Primitives
        // ================================================================

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = Vector2.Dot(ab, ab);
            if (lenSq < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + t * ab);
        }

        static void StrokeLine(float[] buf, Vector2 a, Vector2 b, float width, float opacity = 1f)
        {
            float halfW = width * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfW - AA));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfW + AA));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfW - AA));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfW + AA));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = DistToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b);
                float alpha = Mathf.Clamp01(1f - (d - halfW) / AA) * opacity;
                int idx = y * Size + x;
                if (alpha > buf[idx]) buf[idx] = alpha;
            }
        }

        static void StrokeCircle(float[] buf, Vector2 center, float radius, float width, float opacity = 1f)
        {
            float halfW = width * 0.5f;
            float outer = radius + halfW + AA;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - outer));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.x + outer));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - outer));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.y + outer));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) - radius);
                float alpha = Mathf.Clamp01(1f - (d - halfW) / AA) * opacity;
                int idx = y * Size + x;
                if (alpha > buf[idx]) buf[idx] = alpha;
            }
        }

        static void StrokeEllipse(float[] buf, Vector2 center, float rx, float ry, float width, float opacity = 1f)
        {
            float halfW = width * 0.5f;
            float maxR = Mathf.Max(rx, ry) + halfW + AA;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - maxR));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.x + maxR));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - maxR));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.y + maxR));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float nx = (p.x - center.x) / rx;
                float ny = (p.y - center.y) / ry;
                float nr = Mathf.Sqrt(nx * nx + ny * ny);
                if (nr < 0.0001f) nr = 0.0001f;
                Vector2 onEllipse = center + new Vector2(nx / nr * rx, ny / nr * ry);
                float d = Vector2.Distance(p, onEllipse);
                float alpha = Mathf.Clamp01(1f - (d - halfW) / AA) * opacity;
                int idx = y * Size + x;
                if (alpha > buf[idx]) buf[idx] = alpha;
            }
        }

        static void FillCircle(float[] buf, Vector2 center, float radius, float opacity = 1f)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - AA));
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.x + radius + AA));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - AA));
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(center.y + radius + AA));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(1f - (d - radius) / AA) * opacity;
                int idx = y * Size + x;
                if (alpha > buf[idx]) buf[idx] = alpha;
            }
        }

        static void StrokePolygon(float[] buf, Vector2[] pts, float width, float opacity = 1f)
        {
            for (int i = 0; i < pts.Length; i++)
                StrokeLine(buf, pts[i], pts[(i + 1) % pts.Length], width, opacity);
        }

        // ================================================================
        // Icon Definitions (matching SVG designs in sporefront-resource-panel-assets)
        // ================================================================

        static void DrawWood(float[] buf)
        {
            float w = StrokeW;
            // Outer ellipse — log cross-section
            StrokeEllipse(buf, P(12, 13), 8 * Scale, 6 * Scale, w);
            // Inner ring
            StrokeEllipse(buf, P(12, 13), 4 * Scale, 3 * Scale, w, 0.5f);
            // Center dot
            FillCircle(buf, P(12, 13), 1f * Scale);
            // Small branch extending upward
            StrokeLine(buf, P(12, 7), P(12, 4), w);
            // Leaf crown (piecewise curve)
            StrokeLine(buf, P(9, 5), P(10.5f, 3.5f), w, 0.6f);
            StrokeLine(buf, P(10.5f, 3.5f), P(12, 2), w, 0.6f);
            StrokeLine(buf, P(12, 2), P(13.5f, 3.5f), w, 0.6f);
            StrokeLine(buf, P(13.5f, 3.5f), P(15, 5), w, 0.6f);
        }

        static void DrawFood(float[] buf)
        {
            float w = StrokeW;
            // Berry / mushroom cluster — three overlapping circles
            StrokeCircle(buf, P(9, 11), 4f * Scale, w);
            StrokeCircle(buf, P(16, 12), 3.5f * Scale, w);
            StrokeCircle(buf, P(12, 8), 3f * Scale, w);
            // Stem
            StrokeLine(buf, P(12, 16), P(12, 21), w);
            // Ground line
            StrokeLine(buf, P(9, 21), P(15, 21), w);
        }

        static void DrawStone(float[] buf)
        {
            float w = StrokeW;
            // Rough-hewn angular stone
            var poly = new[] { P(6, 18), P(3, 12), P(6, 6), P(12, 3), P(18, 6), P(21, 12), P(18, 18) };
            StrokePolygon(buf, poly, w);
            // Internal crack lines
            StrokeLine(buf, P(6, 6), P(10, 10), w, 0.4f);
            StrokeLine(buf, P(18, 6), P(15, 10), w, 0.4f);
        }

        static void DrawOre(float[] buf)
        {
            float w = StrokeW;
            // Faceted gem / ore chunk
            var poly = new[] { P(5, 10), P(9, 4), P(15, 4), P(19, 10), P(12, 20) };
            StrokePolygon(buf, poly, w);
            // Internal facet lines
            StrokeLine(buf, P(9, 4), P(7, 10), w, 0.5f);
            StrokeLine(buf, P(15, 4), P(17, 10), w, 0.5f);
            StrokeLine(buf, P(5, 10), P(12, 12), w, 0.4f);
            StrokeLine(buf, P(19, 10), P(12, 12), w, 0.4f);
            StrokeLine(buf, P(12, 12), P(12, 20), w, 0.4f);
        }

        static void DrawPopulation(float[] buf)
        {
            float w = StrokeW;
            // Left mushroom cap (approximated as ellipse)
            StrokeEllipse(buf, P(8, 9), 4f * Scale, 3f * Scale, w);
            // Left stem
            StrokeLine(buf, P(8, 12), P(8, 17), w);
            // Right mushroom cap (smaller)
            StrokeEllipse(buf, P(15, 11.5f), 3.5f * Scale, 2.5f * Scale, w);
            // Right stem
            StrokeLine(buf, P(15, 14), P(15, 17), w);
            // Ground curve (piecewise)
            StrokeLine(buf, P(5, 20), P(8, 17.5f), w);
            StrokeLine(buf, P(8, 17.5f), P(12, 17), w);
            StrokeLine(buf, P(12, 17), P(16, 17.5f), w);
            StrokeLine(buf, P(16, 17.5f), P(19, 20), w);
        }

        // ================================================================
        // Helpers
        // ================================================================

        static Sprite BufToSprite(float[] buf)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 1f, 1f, buf[i]);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
