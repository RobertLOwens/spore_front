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

        private Dictionary<Guid, PathVisualData> activePaths = new Dictionary<Guid, PathVisualData>();
        private const float LineWidth = 0.02f;
        private const float GlowWidth = 0.04f;
        private const float ZPosition = -0.02f;

        // ================================================================
        // Public API
        // ================================================================

        public void ShowPath(Guid entityID, List<HexCoordinate> path, Color color)
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

            activePaths[entityID] = new PathVisualData
            {
                rootGO = pathGO,
                mainLine = mainLR,
                glowLine = glowLR
            };
        }

        public void ClearPaths()
        {
            foreach (var kvp in activePaths)
            {
                if (kvp.Value.rootGO != null) Destroy(kvp.Value.rootGO);
            }
            activePaths.Clear();
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
                if (army == null || army.currentPath == null) continue;

                // Build remaining path starting from entity's current coordinate
                var remaining = new List<HexCoordinate>();
                remaining.Add(army.coordinate);
                int startIdx = Mathf.Max(0, army.pathIndex);
                for (int i = startIdx; i < army.currentPath.Count; i++)
                    remaining.Add(army.currentPath[i]);

                if (remaining.Count < 2) continue;

                Color color = army.isInCombat ? SporefrontColors.SporeRed :
                              army.isRetreating ? SporefrontColors.SporeAmber :
                              SporefrontColors.SporeGreen;
                ShowPath(armyID, remaining, color);
            }

            // Show paths for owned villager groups
            foreach (var groupID in player.ownedVillagerGroupIDs)
            {
                var group = gameState.GetVillagerGroup(groupID);
                if (group == null || group.currentPath == null) continue;

                // Build remaining path starting from entity's current coordinate
                var remaining = new List<HexCoordinate>();
                remaining.Add(group.coordinate);
                int startIdx = Mathf.Max(0, group.pathIndex);
                for (int i = startIdx; i < group.currentPath.Count; i++)
                    remaining.Add(group.currentPath[i]);

                if (remaining.Count < 2) continue;
                ShowPath(groupID, remaining, SporefrontColors.SporeAmber);
            }
        }

        /// <summary>
        /// Updates path start points each frame to track moving entities.
        /// Called from UIManager.UpdateUI after InterpolateMovingEntities.
        /// </summary>
        public void UpdatePathStartPoints(EntityRenderer entityRenderer)
        {
            foreach (var kvp in activePaths)
            {
                var pathData = kvp.Value;
                if (pathData.mainLine == null) continue;

                Vector3 bottomPos = entityRenderer.GetEntityBottomPosition(kvp.Key);
                if (bottomPos == Vector3.zero) continue;

                bottomPos.z = ZPosition;
                pathData.mainLine.SetPosition(0, bottomPos);
                if (pathData.glowLine != null)
                    pathData.glowLine.SetPosition(0, bottomPos);
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

        // ================================================================
        // Internal Types
        // ================================================================

        private struct PathVisualData
        {
            public GameObject rootGO;
            public LineRenderer mainLine;
            public LineRenderer glowLine;
        }
    }
}
