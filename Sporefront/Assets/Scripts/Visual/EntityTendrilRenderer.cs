// ============================================================================
// FILE: Visual/EntityTendrilRenderer.cs
// PURPOSE: Mycelium-style tendrils that grow forward from moving entities
//          in their movement direction. Uses Catmull-Rom splines with
//          sinusoidal wave offsets, consistent with PathRenderer style.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class EntityTendrilRenderer : MonoBehaviour
    {
        // ================================================================
        // Constants
        // ================================================================

        private const int SubdivisionsPerSegment = 6;
        private const float ZPosition = -0.04f;
        private const int SortingOrder = 3;
        private const float GrowDuration = 0.6f;
        private const float ShrinkDuration = 1.0f;
        private const float DirectionRebuildThreshold = 30f; // degrees
        private const float DirectionSmoothSpeed = 5f;

        // Army config
        private const int ArmyTendrilCount = 5;
        private const float ArmyTendrilLength = 0.35f;
        private const float ArmyMaxOffset = 0.04f;
        private const int ArmyStrandCount = 2;
        private static readonly float[] ArmyStrandWidths = new[] { 0.006f, 0.005f };
        private static readonly float[] ArmyStrandAlphas = new[] { 0.6f, 0.45f };
        private static readonly float[] ArmyTendrilAngles = new[] { -50f, -25f, 0f, 25f, 50f };

        // Villager config
        private const int VillagerTendrilCount = 3;
        private const float VillagerTendrilLength = 0.25f;
        private const float VillagerMaxOffset = 0.03f;
        private const int VillagerStrandCount = 2;
        private static readonly float[] VillagerStrandWidths = new[] { 0.004f, 0.003f };
        private static readonly float[] VillagerStrandAlphas = new[] { 0.6f, 0.45f };
        private static readonly float[] VillagerTendrilAngles = new[] { -30f, 0f, 30f };

        // Shared strand wave params
        private static readonly (float freq, float phase)[] StrandWaveParams = new[]
        {
            (3.5f, 0.0f),
            (4.0f, 1.8f)
        };

        // ================================================================
        // State
        // ================================================================

        private EntityRenderer entityRenderer;
        private Dictionary<Guid, EntityTendrilData> activeTendrils = new Dictionary<Guid, EntityTendrilData>();
        private List<Guid> _toRemoveBuffer = new List<Guid>();

        // ================================================================
        // Public API
        // ================================================================

        public void SetEntityRenderer(EntityRenderer renderer)
        {
            entityRenderer = renderer;
        }

        public void UpdateTendrils(GameState gameState, Guid localPlayerID)
        {
            if (gameState == null || entityRenderer == null) { ClearAll(); return; }

            var player = gameState.GetPlayer(localPlayerID);
            if (player == null) { ClearAll(); return; }

            // Check armies
            foreach (var armyID in player.ownedArmyIDs)
            {
                var army = gameState.GetArmy(armyID);
                if (army == null) continue;

                bool isMoving = army.currentPath != null && army.currentPath.Count > 0 && army.movementSpeed > 0;
                if (isMoving)
                {
                    Vector2 direction = ComputeMovementDirection(army.coordinate, army.currentPath, army.pathIndex);
                    Color color = entityRenderer.GetEntityColor(armyID);

                    if (activeTendrils.TryGetValue(armyID, out var existing))
                    {
                        existing.isMoving = true;
                        UpdateDirection(existing, direction);
                    }
                    else
                    {
                        var data = CreateTendrils(armyID, direction, color, true);
                        if (data != null)
                            activeTendrils[armyID] = data;
                    }
                }
                else if (activeTendrils.TryGetValue(armyID, out var existing))
                {
                    existing.isMoving = false;
                }
            }

            // Check villager groups
            foreach (var groupID in player.ownedVillagerGroupIDs)
            {
                var group = gameState.GetVillagerGroup(groupID);
                if (group == null) continue;

                bool isMoving = group.currentPath != null && group.currentPath.Count > 0 && group.movementSpeed > 0;
                if (isMoving)
                {
                    Vector2 direction = ComputeMovementDirection(group.coordinate, group.currentPath, group.pathIndex);
                    Color color = entityRenderer.GetEntityColor(groupID);

                    if (activeTendrils.TryGetValue(groupID, out var existing))
                    {
                        existing.isMoving = true;
                        UpdateDirection(existing, direction);
                    }
                    else
                    {
                        var data = CreateTendrils(groupID, direction, color, false);
                        if (data != null)
                            activeTendrils[groupID] = data;
                    }
                }
                else if (activeTendrils.TryGetValue(groupID, out var existing))
                {
                    existing.isMoving = false;
                }
            }
        }

        public void AnimateTendrils()
        {
            if (entityRenderer == null) return;

            _toRemoveBuffer.Clear();
            float dt = Time.deltaTime;

            foreach (var kvp in activeTendrils)
            {
                var id = kvp.Key;
                var data = kvp.Value;

                // Update growth
                if (data.isMoving)
                {
                    data.growthProgress = Mathf.MoveTowards(data.growthProgress, 1f, dt / GrowDuration);
                }
                else
                {
                    data.growthProgress = Mathf.MoveTowards(data.growthProgress, 0f, dt / ShrinkDuration);
                    if (data.growthProgress <= 0f)
                    {
                        if (data.rootGO != null) Destroy(data.rootGO);
                        _toRemoveBuffer.Add(id);
                        continue;
                    }
                }

                // Reparent to entity transform each frame to follow movement
                Transform entityTransform = entityRenderer.GetEntityTransform(id);
                if (entityTransform == null)
                {
                    if (data.rootGO != null) Destroy(data.rootGO);
                    _toRemoveBuffer.Add(id);
                    continue;
                }
                if (data.rootGO != null)
                {
                    data.rootGO.transform.position = new Vector3(
                        entityTransform.position.x,
                        entityTransform.position.y,
                        ZPosition);
                }

                // Smooth direction
                data.smoothedDirection = Vector2.Lerp(data.smoothedDirection, data.targetDirection,
                    dt * DirectionSmoothSpeed).normalized;
                if (data.smoothedDirection.sqrMagnitude < 0.001f)
                    data.smoothedDirection = data.targetDirection;

                // Update gradients for growth animation
                UpdateGrowthGradients(data);
            }

            foreach (var id in _toRemoveBuffer)
                activeTendrils.Remove(id);
        }

        public void ClearAll()
        {
            foreach (var kvp in activeTendrils)
            {
                if (kvp.Value.rootGO != null) Destroy(kvp.Value.rootGO);
            }
            activeTendrils.Clear();
        }

        // ================================================================
        // Direction Computation
        // ================================================================

        private Vector2 ComputeMovementDirection(HexCoordinate current, List<HexCoordinate> path, int pathIndex)
        {
            int nextIdx = Mathf.Clamp(pathIndex, 0, path.Count - 1);
            HexCoordinate target = path[nextIdx];

            Vector3 from = HexMetrics.HexToWorldPosition(current);
            Vector3 to = HexMetrics.HexToWorldPosition(target);
            Vector2 dir = new Vector2(to.x - from.x, to.y - from.y);

            if (dir.sqrMagnitude < 0.001f && nextIdx + 1 < path.Count)
            {
                // Already at next tile, look ahead
                to = HexMetrics.HexToWorldPosition(path[nextIdx + 1]);
                dir = new Vector2(to.x - from.x, to.y - from.y);
            }

            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.up;
        }

        private void UpdateDirection(EntityTendrilData data, Vector2 newDirection)
        {
            float angle = Vector2.Angle(data.targetDirection, newDirection);
            data.targetDirection = newDirection;

            if (angle > DirectionRebuildThreshold)
            {
                RebuildSplinePoints(data);
            }
        }

        // ================================================================
        // Tendril Creation
        // ================================================================

        private EntityTendrilData CreateTendrils(Guid entityID, Vector2 direction, Color color, bool isArmy)
        {
            Transform entityTransform = entityRenderer.GetEntityTransform(entityID);
            if (entityTransform == null) return null;

            int tendrilCount = isArmy ? ArmyTendrilCount : VillagerTendrilCount;
            int strandCount = isArmy ? ArmyStrandCount : VillagerStrandCount;
            float[] angles = isArmy ? ArmyTendrilAngles : VillagerTendrilAngles;
            float length = isArmy ? ArmyTendrilLength : VillagerTendrilLength;
            float maxOffset = isArmy ? ArmyMaxOffset : VillagerMaxOffset;
            float[] strandWidths = isArmy ? ArmyStrandWidths : VillagerStrandWidths;
            float[] strandAlphas = isArmy ? ArmyStrandAlphas : VillagerStrandAlphas;

            var rootGO = new GameObject($"EntityTendril_{entityID.ToString().Substring(0, 8)}");
            rootGO.transform.SetParent(transform, false);
            rootGO.transform.position = new Vector3(
                entityTransform.position.x,
                entityTransform.position.y,
                ZPosition);

            int seed = entityID.GetHashCode();
            var tendrils = new TendrilStrandData[tendrilCount];

            for (int t = 0; t < tendrilCount; t++)
            {
                float angleRad = angles[t] * Mathf.Deg2Rad;
                Vector2 tendrilDir = RotateVector(direction, angleRad);
                Vector3 endPoint = new Vector3(tendrilDir.x * length, tendrilDir.y * length, 0f);

                var strands = new LineRenderer[strandCount];
                var gradients = new Gradient[strandCount];
                var colorKeysArr = new GradientColorKey[strandCount][];
                var alphaKeysArr = new GradientAlphaKey[strandCount][];

                for (int s = 0; s < strandCount; s++)
                {
                    strands[s] = CreateStrand(rootGO, t, s, endPoint, strandWidths[s],
                        color, maxOffset, seed, length);
                    gradients[s] = new Gradient();
                    colorKeysArr[s] = new GradientColorKey[2];
                    alphaKeysArr[s] = new GradientAlphaKey[4];
                }

                tendrils[t] = new TendrilStrandData
                {
                    strands = strands,
                    gradients = gradients,
                    endPoint = endPoint,
                    colorKeys = colorKeysArr,
                    alphaKeys = alphaKeysArr
                };
            }

            return new EntityTendrilData
            {
                rootGO = rootGO,
                tendrils = tendrils,
                growthProgress = 0f,
                isMoving = true,
                smoothedDirection = direction,
                targetDirection = direction,
                baseColor = color,
                isArmy = isArmy
            };
        }

        private LineRenderer CreateStrand(GameObject parent, int tendrilIndex, int strandIndex,
            Vector3 endPoint, float width, Color color, float maxOffset, int seed, float length)
        {
            var go = new GameObject($"TS_{tendrilIndex}_{strandIndex}");
            go.transform.SetParent(parent.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.sortingOrder = SortingOrder;

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;

            // Start fully transparent
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

            // Build spline points in local space (origin = entity center)
            BuildStrandPoints(lr, tendrilIndex, strandIndex, Vector3.zero, endPoint,
                maxOffset, seed, length);

            return lr;
        }

        private void BuildStrandPoints(LineRenderer lr, int tendrilIndex, int strandIndex,
            Vector3 start, Vector3 end, float maxOffset, int seed, float length)
        {
            // Simple two-segment spline: start -> midpoint -> end
            Vector3 dir = (end - start);
            float totalLen = dir.magnitude;
            if (totalLen < 0.001f)
            {
                lr.positionCount = 2;
                lr.SetPositions(new Vector3[] { start, end });
                return;
            }

            Vector3 midPoint = (start + end) * 0.5f;

            // Small lateral bend for organic feel
            Vector3 normDir = dir.normalized;
            Vector3 perp = new Vector3(-normDir.y, normDir.x, 0f);
            int combinedSeed = seed * 31 + tendrilIndex * 7 + strandIndex * 3;
            float bendAmount = ((combinedSeed & 0xFFFF) / (float)0xFFFF - 0.5f) * 0.04f;
            midPoint += perp * bendAmount;

            // Control points with reflection
            Vector3 p0 = 2f * start - midPoint;
            Vector3 p3 = 2f * end - midPoint;

            var waveParams = StrandWaveParams[strandIndex];
            float seedPhase = ((seed * 31 + strandIndex * 7 + tendrilIndex * 13) & 0xFFFF) / (float)0xFFFF * Mathf.PI * 2f;

            var points = new List<Vector3>();
            float seg1Len = Vector3.Distance(start, midPoint);
            float seg2Len = Vector3.Distance(midPoint, end);
            float segTotalLen = seg1Len + seg2Len;

            if (segTotalLen < 0.001f)
            {
                lr.positionCount = 2;
                lr.SetPositions(new Vector3[] { start, end });
                return;
            }

            // Segment 1: start -> midPoint
            for (int sub = 0; sub < SubdivisionsPerSegment; sub++)
            {
                float localT = sub / (float)SubdivisionsPerSegment;
                Vector3 basePos = CatmullRom(p0, start, midPoint, end, localT);
                Vector3 tangent = CatmullRomTangent(p0, start, midPoint, end, localT);

                float dist = seg1Len * localT;
                float t = dist / segTotalLen;

                float taper = 1f;
                if (t > 0.85f)
                    taper = (1f - t) / 0.15f;

                float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);
                float offset = wave * maxOffset * taper;

                Vector3 d = tangent.normalized;
                if (d.sqrMagnitude < 0.0001f) d = normDir;
                Vector3 p = new Vector3(-d.y, d.x, 0f);

                Vector3 finalPos = basePos + p * offset;
                finalPos.z = 0f;
                points.Add(finalPos);
            }

            // Segment 2: midPoint -> end
            for (int sub = 0; sub <= SubdivisionsPerSegment; sub++)
            {
                float localT = sub / (float)SubdivisionsPerSegment;
                Vector3 basePos = CatmullRom(start, midPoint, end, p3, localT);
                Vector3 tangent = CatmullRomTangent(start, midPoint, end, p3, localT);

                float dist = seg1Len + seg2Len * localT;
                float t = dist / segTotalLen;

                float taper = 1f;
                if (t > 0.85f)
                    taper = (1f - t) / 0.15f;

                float wave = Mathf.Sin(t * waveParams.freq * Mathf.PI * 2f + waveParams.phase + seedPhase);
                float offset = wave * maxOffset * taper;

                Vector3 d = tangent.normalized;
                if (d.sqrMagnitude < 0.0001f) d = normDir;
                Vector3 p = new Vector3(-d.y, d.x, 0f);

                Vector3 finalPos = basePos + p * offset;
                finalPos.z = 0f;
                points.Add(finalPos);
            }

            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
        }

        // ================================================================
        // Direction Update / Rebuild
        // ================================================================

        private void RebuildSplinePoints(EntityTendrilData data)
        {
            float[] angles = data.isArmy ? ArmyTendrilAngles : VillagerTendrilAngles;
            float length = data.isArmy ? ArmyTendrilLength : VillagerTendrilLength;
            float maxOffset = data.isArmy ? ArmyMaxOffset : VillagerMaxOffset;
            int seed = 0;
            if (data.rootGO != null)
            {
                string name = data.rootGO.name;
                // Extract seed from the original entity - use hashcode of name as fallback
                seed = name.GetHashCode();
            }

            for (int t = 0; t < data.tendrils.Length; t++)
            {
                float angleRad = angles[t] * Mathf.Deg2Rad;
                Vector2 tendrilDir = RotateVector(data.smoothedDirection, angleRad);
                Vector3 endPoint = new Vector3(tendrilDir.x * length, tendrilDir.y * length, 0f);
                data.tendrils[t].endPoint = endPoint;

                for (int s = 0; s < data.tendrils[t].strands.Length; s++)
                {
                    if (data.tendrils[t].strands[s] == null) continue;
                    BuildStrandPoints(data.tendrils[t].strands[s], t, s, Vector3.zero, endPoint,
                        maxOffset, seed, length);
                }
            }
        }

        // ================================================================
        // Growth Animation
        // ================================================================

        private void UpdateGrowthGradients(EntityTendrilData data)
        {
            // Skip if growth hasn't changed meaningfully since last update
            if (Mathf.Abs(data.growthProgress - data.lastAppliedGrowth) < 0.001f)
                return;
            data.lastAppliedGrowth = data.growthProgress;

            float[] strandAlphas = data.isArmy ? ArmyStrandAlphas : VillagerStrandAlphas;

            foreach (var tendril in data.tendrils)
            {
                for (int s = 0; s < tendril.strands.Length; s++)
                {
                    if (tendril.strands[s] == null) continue;

                    float alpha = strandAlphas[s];
                    Color baseColor = data.baseColor;
                    var gradient = tendril.gradients[s];
                    var ck = tendril.colorKeys[s];
                    var ak = tendril.alphaKeys[s];

                    // Mutate pre-allocated arrays in-place (no allocation)
                    ck[0] = new GradientColorKey(baseColor, 0f);
                    ck[1] = new GradientColorKey(baseColor, 1f);

                    if (data.growthProgress >= 0.99f)
                    {
                        ak[0] = new GradientAlphaKey(alpha, 0f);
                        ak[1] = new GradientAlphaKey(alpha, 1f);
                        ak[2] = new GradientAlphaKey(alpha, 1f);
                        ak[3] = new GradientAlphaKey(alpha, 1f);
                    }
                    else
                    {
                        float fadeWidth = 0.1f;
                        float fadeStart = Mathf.Max(0f, data.growthProgress - fadeWidth);
                        ak[0] = new GradientAlphaKey(alpha, 0f);
                        ak[1] = new GradientAlphaKey(alpha, fadeStart);
                        ak[2] = new GradientAlphaKey(0f, Mathf.Min(data.growthProgress + fadeWidth, 1f));
                        ak[3] = new GradientAlphaKey(0f, 1f);
                    }

                    gradient.SetKeys(ck, ak);
                    tendril.strands[s].colorGradient = gradient;
                }
            }
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

        private static Vector2 RotateVector(Vector2 v, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        // ================================================================
        // Internal Types
        // ================================================================

        private class EntityTendrilData
        {
            public GameObject rootGO;
            public TendrilStrandData[] tendrils;
            public float growthProgress;
            public float lastAppliedGrowth = -1f;
            public bool isMoving;
            public Vector2 smoothedDirection;
            public Vector2 targetDirection;
            public Color baseColor;
            public bool isArmy;
        }

        private class TendrilStrandData
        {
            public LineRenderer[] strands;
            public Gradient[] gradients;
            public Vector3 endPoint;
            // Pre-allocated gradient key arrays to avoid per-frame allocations
            public GradientColorKey[][] colorKeys;
            public GradientAlphaKey[][] alphaKeys;
        }
    }
}
