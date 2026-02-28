// ============================================================================
// FILE: Visual/PathRenderer.cs
// PURPOSE: Mycelium-style directional path lines for army/villager movement
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
        // Constants
        // ================================================================

        private const int StrandCount = 5;
        private const float MaxOffset = 0.08f;
        private const int SubdivisionsPerSegment = 6;
        private const float TaperFraction = 0.15f;
        private const float ZPosition = -0.02f;
        private const float ArrowSize = 0.05f;
        private const float FadeWidth = 0.05f;

        // Per-strand wave parameters: (frequency, phase)
        private static readonly (float freq, float phase)[] StrandWaveParams = new[]
        {
            (3.5f, 0.0f),
            (4.0f, 1.2f),
            (3.0f, 2.5f),
            (4.5f, 3.8f),
            (3.8f, 5.0f)
        };

        // Per-strand width variation
        private static readonly float[] StrandWidths = new[] { 0.008f, 0.007f, 0.009f, 0.006f, 0.010f };

        // Per-strand alpha variation
        private static readonly float[] StrandAlphas = new[] { 1.0f, 0.75f, 0.85f, 0.65f, 0.95f };

        // ================================================================
        // State
        // ================================================================

        private Dictionary<Guid, PathVisualData> activePaths = new Dictionary<Guid, PathVisualData>();
        private PathVisualData previewPathData;
        private static readonly Guid PreviewSeedID = new Guid("00000000-0000-0000-0000-000000000001");

        // ================================================================
        // Public API
        // ================================================================

        public void ClearPaths()
        {
            foreach (var kvp in activePaths)
            {
                if (kvp.Value.rootGO != null) Destroy(kvp.Value.rootGO);
            }
            activePaths.Clear();
            ClearPreviewPath();
        }

        public void ShowPreviewPath(List<HexCoordinate> path, Color color)
        {
            ClearPreviewPath();
            if (path == null || path.Count < 2) return;
            previewPathData = BuildPathVisual(PreviewSeedID, path, color);
        }

        public void ClearPreviewPath()
        {
            if (previewPathData != null)
            {
                if (previewPathData.rootGO != null) Destroy(previewPathData.rootGO);
                previewPathData = null;
            }
        }

        public void UpdatePaths(GameState gameState, Guid localPlayerID)
        {
            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) { ClearPaths(); return; }

            var currentEntityIDs = new HashSet<Guid>();

            // Show paths for owned armies
            foreach (var armyID in player.ownedArmyIDs)
            {
                var army = gameState.GetArmy(armyID);
                if (army == null || army.currentPath == null || army.currentPath.Count == 0) continue;

                var destination = army.currentPath[army.currentPath.Count - 1];
                Color color = army.isInCombat ? SporefrontColors.SporeRed :
                              army.isRetreating ? SporefrontColors.SporeAmber :
                              SporefrontColors.SporeGreen;

                // Cache hit: skip rebuild if destination and color unchanged
                if (activePaths.TryGetValue(armyID, out var existing) &&
                    existing.destination.Equals(destination) &&
                    ColorsMatch(existing.baseColor, color))
                {
                    currentEntityIDs.Add(armyID);
                    continue;
                }

                // Remove stale visual
                if (activePaths.TryGetValue(armyID, out var old))
                {
                    if (old.rootGO != null) Destroy(old.rootGO);
                    activePaths.Remove(armyID);
                }

                // Build remaining path from entity's current coordinate
                var remaining = new List<HexCoordinate>();
                remaining.Add(army.coordinate);
                int startIdx = Mathf.Max(0, army.pathIndex);
                for (int i = startIdx; i < army.currentPath.Count; i++)
                    remaining.Add(army.currentPath[i]);

                if (remaining.Count >= 2)
                {
                    BuildPath(armyID, remaining, color, destination);
                    currentEntityIDs.Add(armyID);
                }
            }

            // Show paths for owned villager groups
            foreach (var groupID in player.ownedVillagerGroupIDs)
            {
                var group = gameState.GetVillagerGroup(groupID);
                if (group == null || group.currentPath == null || group.currentPath.Count == 0) continue;

                var destination = group.currentPath[group.currentPath.Count - 1];
                Color color = SporefrontColors.SporeAmber;

                // Cache hit
                if (activePaths.TryGetValue(groupID, out var existing) &&
                    existing.destination.Equals(destination) &&
                    ColorsMatch(existing.baseColor, color))
                {
                    currentEntityIDs.Add(groupID);
                    continue;
                }

                // Remove stale visual
                if (activePaths.TryGetValue(groupID, out var old))
                {
                    if (old.rootGO != null) Destroy(old.rootGO);
                    activePaths.Remove(groupID);
                }

                var remaining = new List<HexCoordinate>();
                remaining.Add(group.coordinate);
                int startIdx = Mathf.Max(0, group.pathIndex);
                for (int i = startIdx; i < group.currentPath.Count; i++)
                    remaining.Add(group.currentPath[i]);

                if (remaining.Count >= 2)
                {
                    BuildPath(groupID, remaining, color, destination);
                    currentEntityIDs.Add(groupID);
                }
            }

            // Clean up entities that no longer have active paths
            var toRemove = new List<Guid>();
            foreach (var id in activePaths.Keys)
            {
                if (!currentEntityIDs.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                if (activePaths[id].rootGO != null) Destroy(activePaths[id].rootGO);
                activePaths.Remove(id);
            }
        }

        /// <summary>
        /// Updates path gradients each frame to clip behind moving entities.
        /// Called from UIManager.UpdateUI after InterpolateMovingEntities.
        /// </summary>
        public void UpdatePathStartPoints(EntityRenderer entityRenderer)
        {
            foreach (var kvp in activePaths)
            {
                var pathData = kvp.Value;
                if (pathData.strands == null || pathData.hexWorldPositions == null) continue;

                Vector3 entityPos = entityRenderer.GetEntityBottomPosition(kvp.Key);
                if (entityPos == Vector3.zero) continue;

                float fraction = ComputeEntityFraction(pathData, entityPos);

                // Update strand gradients to clip behind entity (reuse pre-allocated Gradient objects)
                for (int s = 0; s < pathData.strands.Length; s++)
                {
                    if (pathData.strands[s] == null) continue;

                    float strandAlpha = StrandAlphas[s];
                    Color baseColor = pathData.baseColor;

                    var gradient = pathData.strandGradients[s];

                    if (fraction < 0.01f)
                    {
                        // Entity near start — show full path
                        gradient.SetKeys(
                            new GradientColorKey[]
                            {
                                new GradientColorKey(baseColor, 0f),
                                new GradientColorKey(baseColor, 1f)
                            },
                            new GradientAlphaKey[]
                            {
                                new GradientAlphaKey(strandAlpha, 0f),
                                new GradientAlphaKey(strandAlpha, 1f)
                            }
                        );
                    }
                    else
                    {
                        float fadeStart = Mathf.Max(0f, fraction - FadeWidth);
                        gradient.SetKeys(
                            new GradientColorKey[]
                            {
                                new GradientColorKey(baseColor, 0f),
                                new GradientColorKey(baseColor, 1f)
                            },
                            new GradientAlphaKey[]
                            {
                                new GradientAlphaKey(0f, 0f),
                                new GradientAlphaKey(0f, fadeStart),
                                new GradientAlphaKey(strandAlpha, fraction),
                                new GradientAlphaKey(strandAlpha, 1f)
                            }
                        );
                    }

                    pathData.strands[s].colorGradient = gradient;
                }

                // Toggle arrow visibility based on entity progress
                if (pathData.arrows != null)
                {
                    foreach (var arrow in pathData.arrows)
                    {
                        if (arrow.go != null) arrow.go.SetActive(arrow.fraction > fraction);
                    }
                }
            }
        }

        // ================================================================
        // Path Construction
        // ================================================================

        private void BuildPath(Guid entityID, List<HexCoordinate> path, Color color, HexCoordinate destination)
        {
            var visual = BuildPathVisual(entityID, path, color);
            if (visual == null) return;
            visual.destination = destination;
            activePaths[entityID] = visual;
        }

        private PathVisualData BuildPathVisual(Guid seedID, List<HexCoordinate> path, Color color)
        {
            if (path == null || path.Count < 2) return null;

            var pathGO = new GameObject("Path");
            pathGO.transform.SetParent(transform, false);

            // Build world-space positions for hex centers
            var hexWorldPositions = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                hexWorldPositions[i] = HexMetrics.HexToWorldPosition(path[i]);
                hexWorldPositions[i].z = ZPosition;
            }

            // Compute cumulative distances along hex centers
            var cumulativeDistances = new float[hexWorldPositions.Length];
            cumulativeDistances[0] = 0f;
            for (int i = 1; i < hexWorldPositions.Length; i++)
            {
                cumulativeDistances[i] = cumulativeDistances[i - 1] +
                    Vector3.Distance(hexWorldPositions[i - 1], hexWorldPositions[i]);
            }
            float totalPathLength = cumulativeDistances[hexWorldPositions.Length - 1];

            // Deterministic seed from entity ID
            int seed = seedID.GetHashCode();

            // Create 5 mycelium strands
            var strands = new LineRenderer[StrandCount];
            for (int s = 0; s < StrandCount; s++)
            {
                strands[s] = CreateMyceliumStrand(pathGO, s, hexWorldPositions,
                    cumulativeDistances, totalPathLength, color, StrandWidths[s], seed);
            }

            // Direction arrows with fraction tracking
            var arrows = CreateDirectionArrows(pathGO, hexWorldPositions,
                cumulativeDistances, totalPathLength, color);

            // Pre-allocate gradients per strand for reuse in UpdatePathStartPoints
            var strandGradients = new Gradient[StrandCount];
            for (int s = 0; s < StrandCount; s++)
                strandGradients[s] = new Gradient();

            return new PathVisualData
            {
                rootGO = pathGO,
                strands = strands,
                baseColor = color,
                hexWorldPositions = hexWorldPositions,
                cumulativeDistances = cumulativeDistances,
                totalPathLength = totalPathLength,
                arrows = arrows,
                strandGradients = strandGradients
            };
        }

        private LineRenderer CreateMyceliumStrand(GameObject parent, int strandIndex,
            Vector3[] hexPositions, float[] cumulativeDistances, float totalLength,
            Color color, float width, int seed)
        {
            var go = new GameObject($"Strand_{strandIndex}");
            go.transform.SetParent(parent.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.sortingOrder = 4;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;

            // Set initial gradient (full visible — per-frame update will clip)
            float strandAlpha = StrandAlphas[strandIndex];
            var initialGradient = new Gradient();
            initialGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(strandAlpha, 0f),
                    new GradientAlphaKey(strandAlpha, 1f)
                }
            );
            lr.colorGradient = initialGradient;

            if (totalLength < 0.001f)
            {
                lr.positionCount = hexPositions.Length;
                lr.SetPositions(hexPositions);
                return lr;
            }

            // Subdivide path using Catmull-Rom spline with sinusoidal offset
            var points = new List<Vector3>();
            float distSoFar = 0f;
            var waveParams = StrandWaveParams[strandIndex];

            // Deterministic per-strand phase jitter from seed
            float seedPhase = ((seed * 31 + strandIndex * 7) & 0xFFFF) / (float)0xFFFF * Mathf.PI * 2f;

            for (int seg = 0; seg < hexPositions.Length - 1; seg++)
            {
                float segLen = cumulativeDistances[seg + 1] - cumulativeDistances[seg];

                // Catmull-Rom control points with reflection at endpoints
                Vector3 p1 = hexPositions[seg];
                Vector3 p2 = hexPositions[seg + 1];
                Vector3 p0 = seg > 0 ? hexPositions[seg - 1] : 2f * p1 - p2;
                Vector3 p3 = seg + 2 < hexPositions.Length ? hexPositions[seg + 2] : 2f * p2 - p1;

                // Include endpoint only on the last segment
                int steps = (seg == hexPositions.Length - 2)
                    ? SubdivisionsPerSegment + 1
                    : SubdivisionsPerSegment;

                for (int sub = 0; sub < steps; sub++)
                {
                    float localT = sub / (float)SubdivisionsPerSegment;

                    // Catmull-Rom spline position and tangent
                    Vector3 basePos = CatmullRom(p0, p1, p2, p3, localT);
                    Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, localT);

                    float dist = distSoFar + segLen * localT;
                    float t = dist / totalLength; // 0..1 along full path

                    // Sinusoidal perpendicular offset
                    float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);

                    // End taper only (start is handled by gradient clipping)
                    float taper = 1f;
                    if (t > 1f - TaperFraction)
                        taper = (1f - t) / TaperFraction;

                    float offset = wave * MaxOffset * taper;

                    // Perpendicular from spline tangent (smooth direction)
                    Vector3 dir = tangent.normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = (p2 - p1).normalized;
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

                    Vector3 finalPos = basePos + perp * offset;
                    finalPos.z = ZPosition;
                    points.Add(finalPos);
                }

                distSoFar += segLen;
            }

            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            return lr;
        }

        private List<ArrowData> CreateDirectionArrows(GameObject parent, Vector3[] hexPositions,
            float[] cumulativeDistances, float totalLength, Color color)
        {
            var arrows = new List<ArrowData>();
            if (totalLength < 0.001f) return arrows;

            for (int i = 3; i < hexPositions.Length - 1; i += 3)
            {
                Vector3 from = hexPositions[i];
                Vector3 to = hexPositions[i + 1];
                Vector3 midpoint = (from + to) * 0.5f;
                Vector3 direction = (to - from).normalized;

                // Compute fraction at segment midpoint
                float segLen = Vector3.Distance(from, to);
                float fraction = (cumulativeDistances[i] + segLen * 0.5f) / totalLength;

                var arrowGO = new GameObject("Arrow");
                arrowGO.transform.SetParent(parent.transform, false);

                var meshFilter = arrowGO.AddComponent<MeshFilter>();
                var meshRenderer = arrowGO.AddComponent<MeshRenderer>();

                float size = ArrowSize;
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

                arrows.Add(new ArrowData { go = arrowGO, fraction = fraction });
            }

            return arrows;
        }

        // ================================================================
        // Spline Math
        // ================================================================

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
                (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2
            );
        }

        // ================================================================
        // Entity Tracking
        // ================================================================

        private float ComputeEntityFraction(PathVisualData pathData, Vector3 entityPos)
        {
            if (pathData.totalPathLength < 0.001f) return 0f;

            float bestDistSq = float.MaxValue;
            float bestFraction = 0f;

            for (int i = 0; i < pathData.hexWorldPositions.Length - 1; i++)
            {
                Vector3 a = pathData.hexWorldPositions[i];
                Vector3 b = pathData.hexWorldPositions[i + 1];

                // 2D projection (ignore z)
                float abx = b.x - a.x;
                float aby = b.y - a.y;
                float segLenSq = abx * abx + aby * aby;
                if (segLenSq < 0.00001f) continue;

                float apx = entityPos.x - a.x;
                float apy = entityPos.y - a.y;
                float t = Mathf.Clamp01((apx * abx + apy * aby) / segLenSq);

                float closestX = a.x + abx * t;
                float closestY = a.y + aby * t;
                float dx = entityPos.x - closestX;
                float dy = entityPos.y - closestY;
                float distSq = dx * dx + dy * dy;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    float segLen = Mathf.Sqrt(segLenSq);
                    bestFraction = (pathData.cumulativeDistances[i] + segLen * t) / pathData.totalPathLength;
                }
            }

            return Mathf.Clamp01(bestFraction);
        }

        // ================================================================
        // Utility
        // ================================================================

        private static bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b);
        }

        private void OnDestroy()
        {
            ClearPaths();
        }

        // ================================================================
        // Internal Types
        // ================================================================

        private class PathVisualData
        {
            public GameObject rootGO;
            public LineRenderer[] strands;
            public HexCoordinate destination;
            public Color baseColor;
            public Vector3[] hexWorldPositions;
            public float[] cumulativeDistances;
            public float totalPathLength;
            public List<ArrowData> arrows;
            public Gradient[] strandGradients;
        }

        private struct ArrowData
        {
            public GameObject go;
            public float fraction;
        }
    }
}
