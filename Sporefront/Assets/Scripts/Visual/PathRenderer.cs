// ============================================================================
// FILE: Visual/PathRenderer.cs
// PURPOSE: Thin glowing directional path lines for army/villager movement (#18)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class PathRenderer : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private List<GameObject> activePathObjects = new List<GameObject>();
        private const float LineWidth = 0.04f;
        private const float GlowWidth = 0.08f;
        private const float ZPosition = -0.02f;

        // ================================================================
        // Public API
        // ================================================================

        public void ShowPath(List<HexCoordinate> path, Color color)
        {
            if (path == null || path.Count < 2) return;

            var pathGO = new GameObject("Path");
            pathGO.transform.SetParent(transform, false);

            // Main thin line
            var mainLR = CreatePathLine(pathGO, "Line", color, LineWidth, path);

            // Glow underneath
            var glowColor = new Color(color.r, color.g, color.b, 0.3f);
            var glowLR = CreatePathLine(pathGO, "Glow", glowColor, GlowWidth, path);

            // Direction arrows at every 3rd segment
            CreateDirectionArrows(pathGO, path, color);

            activePathObjects.Add(pathGO);
        }

        public void ClearPaths()
        {
            foreach (var go in activePathObjects)
            {
                if (go != null) Destroy(go);
            }
            activePathObjects.Clear();
        }

        public void UpdatePaths(GameState gameState, Guid localPlayerID)
        {
            ClearPaths();

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) return;

            // Show paths for owned armies
            foreach (var armyID in player.ownedArmyIDs)
            {
                var army = gameState.GetArmy(armyID);
                if (army == null || army.currentPath == null || army.currentPath.Count < 2) continue;

                // Build remaining path from current position
                var remaining = new List<HexCoordinate>();
                int startIdx = Mathf.Max(0, army.pathIndex);
                for (int i = startIdx; i < army.currentPath.Count; i++)
                    remaining.Add(army.currentPath[i]);

                if (remaining.Count < 2) continue;

                Color color = army.isInCombat ? SporefrontColors.SporeRed :
                              army.isRetreating ? SporefrontColors.SporeAmber :
                              SporefrontColors.SporeGreen;
                ShowPath(remaining, color);
            }

            // Show paths for owned villager groups
            foreach (var groupID in player.ownedVillagerGroupIDs)
            {
                var group = gameState.GetVillagerGroup(groupID);
                if (group == null || group.currentPath == null || group.currentPath.Count < 2) continue;

                var remaining = new List<HexCoordinate>();
                int startIdx = Mathf.Max(0, group.pathIndex);
                for (int i = startIdx; i < group.currentPath.Count; i++)
                    remaining.Add(group.currentPath[i]);

                if (remaining.Count < 2) continue;
                ShowPath(remaining, SporefrontColors.SporeAmber);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private LineRenderer CreatePathLine(GameObject parent, string name, Color color,
            float width, List<HexCoordinate> path)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.sortingOrder = 4;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;

            var positions = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                positions[i] = HexMetrics.HexToWorldPosition(path[i]);
                positions[i].z = ZPosition;
            }

            lr.positionCount = positions.Length;
            lr.SetPositions(positions);
            return lr;
        }

        private void CreateDirectionArrows(GameObject parent, List<HexCoordinate> path, Color color)
        {
            for (int i = 0; i < path.Count - 1; i += 3)
            {
                if (i + 1 >= path.Count) break;

                Vector3 from = HexMetrics.HexToWorldPosition(path[i]);
                Vector3 to = HexMetrics.HexToWorldPosition(path[i + 1]);
                Vector3 midpoint = (from + to) * 0.5f;
                Vector3 direction = (to - from).normalized;

                var arrowGO = new GameObject("Arrow");
                arrowGO.transform.SetParent(parent.transform, false);

                // Small triangle mesh
                var meshFilter = arrowGO.AddComponent<MeshFilter>();
                var meshRenderer = arrowGO.AddComponent<MeshRenderer>();

                float size = 0.08f;
                var mesh = new Mesh();
                Vector3 right = new Vector3(-direction.y, direction.x, 0f);

                mesh.vertices = new Vector3[]
                {
                    direction * size,
                    -direction * size * 0.5f + right * size * 0.5f,
                    -direction * size * 0.5f - right * size * 0.5f
                };
                mesh.triangles = new int[] { 0, 1, 2 };
                mesh.RecalculateNormals();
                meshFilter.mesh = mesh;

                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                meshRenderer.material = mat;
                meshRenderer.sortingOrder = 4;

                arrowGO.transform.position = new Vector3(midpoint.x, midpoint.y, ZPosition);
            }
        }

        private void OnDestroy()
        {
            ClearPaths();
        }
    }
}
