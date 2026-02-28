// ============================================================================
// FILE: Visual/CursorManager.cs
// PURPOSE: Procedural context-sensitive cursors — move (boot), attack (sword),
//          gather (wheat). 32x32 Texture2D drawn at runtime.
// ============================================================================

using UnityEngine;

namespace Sporefront.Visual
{
    public enum CursorType
    {
        Default,
        Move,
        Attack,
        Gather,
        Hunt
    }

    public static class CursorManager
    {
        private const int Size = 32;

        private static CursorType currentType = CursorType.Default;
        private static Texture2D moveCursor;
        private static Texture2D attackCursor;
        private static Texture2D gatherCursor;
        private static Texture2D huntCursor;

        // ================================================================
        // Public API
        // ================================================================

        public static void SetCursor(CursorType type)
        {
            if (type == currentType) return;
            currentType = type;

            switch (type)
            {
                case CursorType.Move:
                    EnsureMoveCursor();
                    Cursor.SetCursor(moveCursor, new Vector2(16, 16), CursorMode.Auto);
                    break;
                case CursorType.Attack:
                    EnsureAttackCursor();
                    Cursor.SetCursor(attackCursor, new Vector2(16, 16), CursorMode.Auto);
                    break;
                case CursorType.Gather:
                    EnsureGatherCursor();
                    Cursor.SetCursor(gatherCursor, new Vector2(16, 16), CursorMode.Auto);
                    break;
                case CursorType.Hunt:
                    EnsureHuntCursor();
                    Cursor.SetCursor(huntCursor, new Vector2(16, 16), CursorMode.Auto);
                    break;
                default:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
            }
        }

        public static void ResetCursor() => SetCursor(CursorType.Default);

        // ================================================================
        // Lazy Cursor Generation
        // ================================================================

        private static void EnsureMoveCursor()
        {
            if (moveCursor != null) return;
            moveCursor = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            moveCursor.filterMode = FilterMode.Point;
            var pixels = new Color[Size * Size];

            // Boot/foot icon in teal
            Color teal = SporefrontColors.SporeTeal;
            Color outline = SporefrontColors.InkDark;

            // Boot sole (bottom)
            FillRect(pixels, 8, 4, 24, 8, outline);
            FillRect(pixels, 9, 5, 23, 7, teal);

            // Boot shaft (leg part)
            FillRect(pixels, 12, 8, 22, 22, outline);
            FillRect(pixels, 13, 9, 21, 21, teal);

            // Boot toe (front bump)
            FillRect(pixels, 6, 4, 10, 12, outline);
            FillRect(pixels, 7, 5, 9, 11, teal);

            // Boot cuff at top
            FillRect(pixels, 11, 20, 23, 24, outline);
            FillRect(pixels, 12, 21, 22, 23, teal);

            // Heel
            FillRect(pixels, 20, 2, 24, 5, outline);
            FillRect(pixels, 21, 3, 23, 4, teal);

            moveCursor.SetPixels(pixels);
            moveCursor.Apply();
        }

        private static void EnsureAttackCursor()
        {
            if (attackCursor != null) return;
            attackCursor = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            attackCursor.filterMode = FilterMode.Point;
            var pixels = new Color[Size * Size];

            Color red = SporefrontColors.SporeRed;
            Color outline = SporefrontColors.InkDark;
            Color highlight = new Color(
                Mathf.Min(1f, red.r + 0.2f),
                Mathf.Min(1f, red.g + 0.2f),
                Mathf.Min(1f, red.b + 0.2f), 1f);

            // Sword blade (diagonal from bottom-left to top-right)
            DrawLine(pixels, 6, 6, 24, 24, outline, 4);
            DrawLine(pixels, 7, 7, 23, 23, red, 2);
            DrawLine(pixels, 8, 9, 22, 23, highlight, 1);

            // Sword tip
            FillRect(pixels, 24, 24, 28, 28, outline);
            FillRect(pixels, 25, 25, 27, 27, red);

            // Cross-guard (perpendicular to blade at midpoint)
            DrawLine(pixels, 10, 18, 20, 8, outline, 3);
            DrawLine(pixels, 11, 17, 19, 9, red, 1);

            // Pommel (bottom-left)
            FillCircle(pixels, 5, 5, 3, outline);
            FillCircle(pixels, 5, 5, 2, red);

            attackCursor.SetPixels(pixels);
            attackCursor.Apply();
        }

        private static void EnsureGatherCursor()
        {
            if (gatherCursor != null) return;
            gatherCursor = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            gatherCursor.filterMode = FilterMode.Point;
            var pixels = new Color[Size * Size];

            Color green = SporefrontColors.SporeGreen;
            Color amber = SporefrontColors.SporeAmber;
            Color stem = SporefrontColors.InkMid;

            // Three wheat stalks

            // Center stalk
            DrawLine(pixels, 16, 2, 16, 26, stem, 2);
            // Wheat head (center)
            FillEllipse(pixels, 16, 26, 3, 5, amber);
            FillEllipse(pixels, 16, 26, 2, 4, new Color(amber.r + 0.1f, amber.g + 0.1f, amber.b, 1f));

            // Left stalk
            DrawLine(pixels, 9, 4, 11, 22, stem, 2);
            FillEllipse(pixels, 11, 23, 3, 4, amber);

            // Right stalk
            DrawLine(pixels, 22, 4, 21, 22, stem, 2);
            FillEllipse(pixels, 21, 23, 3, 4, amber);

            // Small leaves on center stalk
            DrawLine(pixels, 16, 14, 21, 17, green, 2);
            DrawLine(pixels, 16, 10, 11, 13, green, 2);

            gatherCursor.SetPixels(pixels);
            gatherCursor.Apply();
        }

        private static void EnsureHuntCursor()
        {
            if (huntCursor != null) return;
            huntCursor = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            huntCursor.filterMode = FilterMode.Point;
            var pixels = new Color[Size * Size];

            Color red = SporefrontColors.SporeRed;
            Color outline = SporefrontColors.InkDark;

            // Crosshair circle
            DrawCircleOutline(pixels, 16, 16, 10, outline, 2);
            DrawCircleOutline(pixels, 16, 16, 9, red, 1);

            // Crosshair lines (horizontal)
            DrawLine(pixels, 2, 16, 10, 16, outline, 2);
            DrawLine(pixels, 3, 16, 9, 16, red, 1);
            DrawLine(pixels, 22, 16, 30, 16, outline, 2);
            DrawLine(pixels, 23, 16, 29, 16, red, 1);

            // Crosshair lines (vertical)
            DrawLine(pixels, 16, 2, 16, 10, outline, 2);
            DrawLine(pixels, 16, 3, 16, 9, red, 1);
            DrawLine(pixels, 16, 22, 16, 30, outline, 2);
            DrawLine(pixels, 16, 23, 16, 29, red, 1);

            // Center dot
            FillCircle(pixels, 16, 16, 2, outline);
            FillCircle(pixels, 16, 16, 1, red);

            huntCursor.SetPixels(pixels);
            huntCursor.Apply();
        }

        private static void DrawCircleOutline(Color[] pixels, int cx, int cy, int r, Color color, int thickness)
        {
            for (int y = cy - r - thickness; y <= cy + r + thickness; y++)
            {
                for (int x = cx - r - thickness; x <= cx + r + thickness; x++)
                {
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (dist >= r - thickness * 0.5f && dist <= r + thickness * 0.5f)
                        SetPixelSafe(pixels, x, y, color);
                }
            }
        }

        // ================================================================
        // Drawing Helpers
        // ================================================================

        private static void FillRect(Color[] pixels, int x1, int y1, int x2, int y2, Color color)
        {
            for (int y = y1; y <= y2; y++)
                for (int x = x1; x <= x2; x++)
                    SetPixelSafe(pixels, x, y, color);
        }

        private static void DrawLine(Color[] pixels, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int half = thickness / 2;

            while (true)
            {
                for (int ox = -half; ox <= half; ox++)
                    for (int oy = -half; oy <= half; oy++)
                        SetPixelSafe(pixels, x0 + ox, y0 + oy, color);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private static void FillCircle(Color[] pixels, int cx, int cy, int r, Color color)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        SetPixelSafe(pixels, x, y, color);
        }

        private static void FillEllipse(Color[] pixels, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                        SetPixelSafe(pixels, x, y, color);
                }
            }
        }

        private static void SetPixelSafe(Color[] pixels, int x, int y, Color color)
        {
            if (x >= 0 && x < Size && y >= 0 && y < Size)
                pixels[y * Size + x] = color;
        }
    }
}
