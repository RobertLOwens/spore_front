// ============================================================================
// FILE: Visual/HexMeshUtility.cs
// PURPOSE: Shared hex mesh generation — 6-triangle fill + thin border ring
//          Meshes are created once and shared across all tile views
// ============================================================================

using UnityEngine;

namespace Sporefront.Visual
{
    public static class HexMeshUtility
    {
        // ================================================================
        // Hex Fill Mesh (6 triangles, fan from center)
        // ================================================================

        /// <summary>
        /// Creates a flat-top hex mesh with 7 vertices (center + 6 corners)
        /// and 6 triangles in a fan pattern. UV mapped for potential texture use.
        /// </summary>
        public static Mesh CreateHexMesh()
        {
            var corners = HexMetrics.GetHexCorners();

            var vertices = new Vector3[7];
            var uv = new Vector2[7];
            var triangles = new int[18]; // 6 triangles * 3 indices

            // Center vertex
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);

            // Corner vertices
            for (int i = 0; i < 6; i++)
            {
                vertices[i + 1] = corners[i];
                // Map corners to UV space (0-1 range)
                uv[i + 1] = new Vector2(
                    (corners[i].x / HexMetrics.OuterRadius + 1f) * 0.5f,
                    (corners[i].y / HexMetrics.OuterRadius + 1f) * 0.5f
                );
            }

            // 6 triangles fanning from center
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;                   // center
                triangles[i * 3 + 1] = i + 1;           // current corner
                triangles[i * 3 + 2] = (i + 1) % 6 + 1; // next corner
            }

            var mesh = new Mesh();
            mesh.name = "HexFill";
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================
        // Hex Border Mesh (thin outline ring)
        // ================================================================

        /// <summary>
        /// Creates a hex border mesh as a thin ring of 6 quads (12 triangles).
        /// Produces "faint ruled lines" appearance at small lineWidth.
        /// </summary>
        public static Mesh CreateHexBorderMesh(float lineWidth = 0.03f)
        {
            var corners = HexMetrics.GetHexCorners();

            // Inner ring is slightly smaller than outer ring
            float shrink = 1f - (lineWidth / HexMetrics.OuterRadius);

            var vertices = new Vector3[12]; // 6 outer + 6 inner
            var triangles = new int[36];    // 6 quads * 2 tris * 3 indices

            for (int i = 0; i < 6; i++)
            {
                vertices[i] = corners[i];                // outer
                vertices[i + 6] = corners[i] * shrink;   // inner
            }

            // Build quads for each edge
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                int triBase = i * 6;

                // Triangle 1: outer[i], outer[next], inner[next]
                triangles[triBase + 0] = i;
                triangles[triBase + 1] = next;
                triangles[triBase + 2] = next + 6;

                // Triangle 2: outer[i], inner[next], inner[i]
                triangles[triBase + 3] = i;
                triangles[triBase + 4] = next + 6;
                triangles[triBase + 5] = i + 6;
            }

            var mesh = new Mesh();
            mesh.name = "HexBorder";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
