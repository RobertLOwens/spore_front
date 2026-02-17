// ============================================================================
// FILE: Visual/HexMetrics.cs
// PURPOSE: Hex geometry constants and coordinate conversion utilities
//          Uses odd-r offset coordinates matching HexCoordinate.cs
//          Flat-top hexes (vertex at top/bottom)
// ============================================================================

using UnityEngine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public static class HexMetrics
    {
        // ================================================================
        // Hex Geometry Constants (flat-top orientation)
        // ================================================================

        public const float OuterRadius = 1.0f;
        public static readonly float InnerRadius = OuterRadius * Mathf.Sqrt(3f) / 2f; // ~0.866
        public static readonly float HexWidth = InnerRadius * 2f;  // ~1.732
        public static readonly float HexHeight = OuterRadius * 2f; // 2.0

        // ================================================================
        // Coordinate Conversion
        // ================================================================

        /// <summary>
        /// Convert odd-r offset HexCoordinate to world position (XY plane, Z=0).
        /// Odd rows shift right by InnerRadius.
        /// </summary>
        public static Vector3 HexToWorldPosition(HexCoordinate coord)
        {
            float x = coord.q * HexWidth + (coord.r % 2 != 0 ? InnerRadius : 0f);
            float y = coord.r * (OuterRadius * 1.5f);
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Convert world position to the nearest HexCoordinate (approximate).
        /// </summary>
        public static HexCoordinate WorldToHex(Vector3 worldPos)
        {
            // Approximate row from y
            int r = Mathf.RoundToInt(worldPos.y / (OuterRadius * 1.5f));
            // Undo odd-row shift to get q
            float xOffset = (r % 2 != 0) ? InnerRadius : 0f;
            int q = Mathf.RoundToInt((worldPos.x - xOffset) / HexWidth);
            return new HexCoordinate(q, r);
        }

        // ================================================================
        // Hex Corners (flat-top: vertex at 90 and 270 degrees)
        // ================================================================

        /// <summary>
        /// Returns the 6 corner vertices of a flat-top hex centered at origin.
        /// Ordered clockwise from top vertex.
        /// </summary>
        public static Vector3[] GetHexCorners()
        {
            var corners = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i + 90f; // +90 puts first vertex at top
                float angleRad = angleDeg * Mathf.Deg2Rad;
                corners[i] = new Vector3(
                    OuterRadius * Mathf.Cos(angleRad),
                    OuterRadius * Mathf.Sin(angleRad),
                    0f
                );
            }
            return corners;
        }

        // ================================================================
        // Map Bounds
        // ================================================================

        /// <summary>
        /// Calculate world-space bounding box for a map of given size.
        /// Includes padding for camera constraints.
        /// </summary>
        public static Bounds GetMapBounds(int width, int height)
        {
            // Rightmost tile: last column of an odd row (has offset)
            float maxX = (width - 1) * HexWidth + InnerRadius;
            // Topmost tile: last row
            float maxY = (height - 1) * OuterRadius * 1.5f;

            float padding = OuterRadius * 2f;
            var center = new Vector3(maxX / 2f, maxY / 2f, 0f);
            var size = new Vector3(maxX + padding * 2f, maxY + padding * 2f, 0f);
            return new Bounds(center, size);
        }
    }
}
