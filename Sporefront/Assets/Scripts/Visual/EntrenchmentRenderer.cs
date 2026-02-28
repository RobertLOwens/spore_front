// ============================================================================
// FILE: Visual/EntrenchmentRenderer.cs
// PURPOSE: Mycelium-style tendril visuals for entrenched armies.
//          Grows organic Catmull-Rom spline strands from army center
//          outward to neighbor hex centers during entrenchment.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class EntrenchmentRenderer : MonoBehaviour
    {
        // ================================================================
        // Constants
        // ================================================================

        private const int StrandCount = 3;
        private const float MaxOffset = 0.06f;
        private const int SubdivisionsPerSegment = 8;
        private const float ZPosition = -0.02f;

        private static readonly float[] StrandWidths = new[] { 0.006f, 0.005f, 0.007f };
        private static readonly float[] StrandAlphas = new[] { 1.0f, 0.8f, 0.9f };

        private static readonly (float freq, float phase)[] StrandWaveParams = new[]
        {
            (3.5f, 0.0f),
            (4.0f, 1.8f),
            (3.0f, 3.6f)
        };

        // ================================================================
        // State
        // ================================================================

        private Dictionary<Guid, EntrenchVisualData> activeVisuals = new Dictionary<Guid, EntrenchVisualData>();

        // ================================================================
        // Public API
        // ================================================================

        public void UpdateEntrenchment(GameState gameState, Guid localPlayerID)
        {
            if (gameState == null) { ClearAll(); return; }

            var localPlayer = gameState.GetPlayer(localPlayerID);
            var currentArmyIDs = new HashSet<Guid>();

            foreach (var kvp in gameState.armies)
            {
                var army = kvp.Value;
                if (!army.isEntrenching && !army.isEntrenched) continue;

                // Fog filter: own armies always shown; enemy only when visible
                if (localPlayer != null && (!army.ownerID.HasValue || army.ownerID.Value != localPlayerID))
                {
                    if (!localPlayer.IsVisible(army.coordinate)) continue;
                }

                currentArmyIDs.Add(army.id);

                // Determine current color
                Color color = army.isEntrenched ? SporefrontColors.SporeTeal : SporefrontColors.SporeAmber;

                // Determine target tiles
                HashSet<HexCoordinate> targetTiles;
                if (army.isEntrenched && army.entrenchedCoveredTiles != null && army.entrenchedCoveredTiles.Count > 0)
                {
                    // Entrenched: only tiles actually covered (excluding army's own tile)
                    targetTiles = new HashSet<HexCoordinate>();
                    foreach (var tile in army.entrenchedCoveredTiles)
                    {
                        if (!tile.Equals(army.coordinate))
                            targetTiles.Add(tile);
                    }
                }
                else
                {
                    // Entrenching: grow toward all 6 in-bounds neighbors
                    targetTiles = new HashSet<HexCoordinate>();
                    var neighbors = army.coordinate.Neighbors();
                    foreach (var n in neighbors)
                    {
                        if (n.q >= 0 && n.q < gameState.mapData.width &&
                            n.r >= 0 && n.r < gameState.mapData.height)
                        {
                            targetTiles.Add(n);
                        }
                    }
                }

                // Check if existing visual is still valid
                if (activeVisuals.TryGetValue(army.id, out var existing))
                {
                    bool colorChanged = !ColorsMatch(existing.baseColor, color);
                    bool tilesChanged = !TileSetsMatch(existing.targetTiles, targetTiles);

                    if (!colorChanged && !tilesChanged)
                        continue; // No change needed

                    // Rebuild
                    if (existing.rootGO != null) Destroy(existing.rootGO);
                    activeVisuals.Remove(army.id);
                }

                // Build new visual
                if (targetTiles.Count > 0)
                {
                    var visual = BuildTendrilVisual(army, targetTiles, color);
                    activeVisuals[army.id] = visual;
                }
            }

            // Remove visuals for armies that are no longer entrenching/entrenched
            var toRemove = new List<Guid>();
            foreach (var id in activeVisuals.Keys)
            {
                if (!currentArmyIDs.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                if (activeVisuals[id].rootGO != null) Destroy(activeVisuals[id].rootGO);
                activeVisuals.Remove(id);
            }
        }

        public void AnimateGrowth(GameState gameState)
        {
            if (gameState == null) return;

            foreach (var kvp in activeVisuals)
            {
                var armyID = kvp.Key;
                var visual = kvp.Value;

                var army = gameState.GetArmy(armyID);
                if (army == null) continue;

                float progress;
                if (army.isEntrenched)
                {
                    progress = 1.0f;
                }
                else if (army.isEntrenching && army.entrenchmentStartTime.HasValue)
                {
                    double elapsed = gameState.currentTime - army.entrenchmentStartTime.Value;
                    progress = Mathf.Clamp01((float)(elapsed / GameConfig.Entrenchment.BuildTime));
                }
                else
                {
                    progress = 0f;
                }

                // Update gradient on each strand to animate growth front
                foreach (var tendril in visual.tendrils)
                {
                    for (int s = 0; s < tendril.strands.Length; s++)
                    {
                        if (tendril.strands[s] == null) continue;

                        float alpha = StrandAlphas[s];
                        Color baseColor = visual.baseColor;
                        var gradient = tendril.gradients[s];

                        if (progress >= 0.99f)
                        {
                            // Fully grown: uniform alpha
                            gradient.SetKeys(
                                new GradientColorKey[]
                                {
                                    new GradientColorKey(baseColor, 0f),
                                    new GradientColorKey(baseColor, 1f)
                                },
                                new GradientAlphaKey[]
                                {
                                    new GradientAlphaKey(alpha, 0f),
                                    new GradientAlphaKey(alpha, 1f)
                                }
                            );
                        }
                        else
                        {
                            // Growing: alpha clips at growth front
                            float fadeWidth = 0.1f;
                            float fadeStart = Mathf.Max(0f, progress - fadeWidth);
                            gradient.SetKeys(
                                new GradientColorKey[]
                                {
                                    new GradientColorKey(baseColor, 0f),
                                    new GradientColorKey(baseColor, 1f)
                                },
                                new GradientAlphaKey[]
                                {
                                    new GradientAlphaKey(alpha, 0f),
                                    new GradientAlphaKey(alpha, fadeStart),
                                    new GradientAlphaKey(0f, Mathf.Min(progress + fadeWidth, 1f)),
                                    new GradientAlphaKey(0f, 1f)
                                }
                            );
                        }

                        tendril.strands[s].colorGradient = gradient;
                    }
                }
            }
        }

        public void ClearAll()
        {
            foreach (var kvp in activeVisuals)
            {
                if (kvp.Value.rootGO != null) Destroy(kvp.Value.rootGO);
            }
            activeVisuals.Clear();
        }

        // ================================================================
        // Tendril Construction
        // ================================================================

        private EntrenchVisualData BuildTendrilVisual(ArmyData army, HashSet<HexCoordinate> targetTiles, Color color)
        {
            var rootGO = new GameObject($"Entrench_{army.id.ToString().Substring(0, 8)}");
            rootGO.transform.SetParent(transform, false);

            Vector3 centerWorld = HexMetrics.HexToWorldPosition(army.coordinate);
            centerWorld.z = ZPosition;

            int seed = army.id.GetHashCode();
            var tendrils = new List<TendrilData>();

            // All covered tiles = army tile + target tiles
            var allCoveredTiles = new HashSet<HexCoordinate>(targetTiles);
            allCoveredTiles.Add(army.coordinate);

            foreach (var tile in targetTiles)
            {
                Vector3 tileCenter = HexMetrics.HexToWorldPosition(tile);
                tileCenter.z = ZPosition;

                // For each of 6 directions, check if edge is outer (neighbor not in covered set)
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinate edgeNeighbor = tile.Neighbor(d);
                    if (allCoveredTiles.Contains(edgeNeighbor))
                        continue; // Inner edge — skip

                    // Outer edge: compute edge midpoint
                    Vector3 neighborWorld = HexMetrics.HexToWorldPosition(edgeNeighbor);
                    neighborWorld.z = ZPosition;
                    Vector3 edgeMidpoint = (tileCenter + neighborWorld) * 0.5f;
                    edgeMidpoint.z = ZPosition;

                    int edgeSeed = tile.GetHashCode() * 31 + d;
                    var tendril = BuildTendril(rootGO, centerWorld, tileCenter, edgeMidpoint, color, seed, edgeSeed);
                    tendrils.Add(tendril);
                }
            }

            return new EntrenchVisualData
            {
                rootGO = rootGO,
                baseColor = color,
                targetTiles = new HashSet<HexCoordinate>(targetTiles),
                tendrils = tendrils
            };
        }

        private TendrilData BuildTendril(GameObject parent, Vector3 start, Vector3 waypoint, Vector3 end,
            Color color, int armySeed, int edgeSeed)
        {
            // Use waypoint (tile center) as spline midpoint with small lateral bend
            Vector3 dir = (waypoint - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            // Deterministic bend amount from combined seed (reduced: waypoint provides curve)
            int combinedSeed = armySeed * 31 + edgeSeed;
            float bendAmount = ((combinedSeed & 0xFFFF) / (float)0xFFFF - 0.5f) * 0.06f;
            Vector3 mid = waypoint + perp * bendAmount;
            mid.z = ZPosition;

            // Build spline points: start -> mid -> end using Catmull-Rom
            // Control points with reflection at endpoints
            Vector3 p0 = 2f * start - mid;
            Vector3 p3 = 2f * end - mid;

            var strands = new LineRenderer[StrandCount];
            var gradients = new Gradient[StrandCount];

            for (int s = 0; s < StrandCount; s++)
            {
                strands[s] = CreateTendrilStrand(parent, s, start, mid, end, p0, p3,
                    color, StrandWidths[s], armySeed);
                gradients[s] = new Gradient();
            }

            return new TendrilData
            {
                strands = strands,
                gradients = gradients
            };
        }

        private LineRenderer CreateTendrilStrand(GameObject parent, int strandIndex,
            Vector3 start, Vector3 mid, Vector3 end, Vector3 p0, Vector3 p3,
            Color color, float width, int seed)
        {
            var go = new GameObject($"TStrand_{strandIndex}");
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

            // Initial gradient: fully transparent (growth animation will reveal)
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
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            lr.colorGradient = initialGradient;

            // Build points with Catmull-Rom spline + sinusoidal offset
            var points = new List<Vector3>();
            var waveParams = StrandWaveParams[strandIndex];
            float seedPhase = ((seed * 31 + strandIndex * 7) & 0xFFFF) / (float)0xFFFF * Mathf.PI * 2f;

            // Segment 1: start -> mid
            float seg1Len = Vector3.Distance(start, mid);
            // Segment 2: mid -> end
            float seg2Len = Vector3.Distance(mid, end);
            float totalLen = seg1Len + seg2Len;

            if (totalLen < 0.001f)
            {
                lr.positionCount = 2;
                lr.SetPositions(new Vector3[] { start, end });
                return lr;
            }

            // Segment 1: Catmull-Rom from start to mid
            for (int sub = 0; sub < SubdivisionsPerSegment; sub++)
            {
                float localT = sub / (float)SubdivisionsPerSegment;
                Vector3 basePos = CatmullRom(p0, start, mid, end, localT);
                Vector3 tangent = CatmullRomTangent(p0, start, mid, end, localT);

                float dist = seg1Len * localT;
                float t = dist / totalLen;

                float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);
                float offset = wave * MaxOffset;

                Vector3 d = tangent.normalized;
                if (d.sqrMagnitude < 0.0001f) d = (mid - start).normalized;
                Vector3 perp = new Vector3(-d.y, d.x, 0f);

                Vector3 finalPos = basePos + perp * offset;
                finalPos.z = ZPosition;
                points.Add(finalPos);
            }

            // Segment 2: Catmull-Rom from mid to end (include endpoint)
            for (int sub = 0; sub <= SubdivisionsPerSegment; sub++)
            {
                float localT = sub / (float)SubdivisionsPerSegment;
                Vector3 basePos = CatmullRom(start, mid, end, p3, localT);
                Vector3 tangent = CatmullRomTangent(start, mid, end, p3, localT);

                float dist = seg1Len + seg2Len * localT;
                float t = dist / totalLen;

                // Taper near the end
                float taper = 1f;
                if (t > 0.85f)
                    taper = (1f - t) / 0.15f;

                float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);
                float offset = wave * MaxOffset * taper;

                Vector3 d = tangent.normalized;
                if (d.sqrMagnitude < 0.0001f) d = (end - mid).normalized;
                Vector3 perp = new Vector3(-d.y, d.x, 0f);

                Vector3 finalPos = basePos + perp * offset;
                finalPos.z = ZPosition;
                points.Add(finalPos);
            }

            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            return lr;
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
        // Utility
        // ================================================================

        private static bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b);
        }

        private static bool TileSetsMatch(HashSet<HexCoordinate> a, HashSet<HexCoordinate> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            foreach (var tile in a)
            {
                if (!b.Contains(tile)) return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        // ================================================================
        // Internal Types
        // ================================================================

        private class EntrenchVisualData
        {
            public GameObject rootGO;
            public Color baseColor;
            public HashSet<HexCoordinate> targetTiles;
            public List<TendrilData> tendrils;
        }

        private class TendrilData
        {
            public LineRenderer[] strands;
            public Gradient[] gradients;
        }
    }
}
