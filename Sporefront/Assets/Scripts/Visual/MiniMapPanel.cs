// ============================================================================
// FILE: Visual/MiniMapPanel.cs
// PURPOSE: Texture2D-based mini map — bottom-right HUD overlay showing terrain,
//          fog of war, entity markers, and camera viewport rectangle.
//          Click/drag to pan the main camera.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class MiniMapPanel : MonoBehaviour
    {
        // ================================================================
        // Constants
        // ================================================================

        private const float PanelSize = 200f;
        private const float BorderWidth = 4f;
        private const float PanelMargin = 8f;
        private const float BottomBarOffset = 60f; // clear the menu bar
        private const int MaxPixelsPerHex = 3;
        private const int MinPixelsPerHex = 2;
        private const float ApplyThrottle = 0.25f; // max texture upload rate

        // Viewport rect
        private static readonly Color ViewportColor = new Color(
            SporefrontColors.SporeAmber.r,
            SporefrontColors.SporeAmber.g,
            SporefrontColors.SporeAmber.b, 0.9f);

        // ================================================================
        // State
        // ================================================================

        private GameObject root;
        private RawImage mapImage;
        private Texture2D mapTexture;
        private CameraController cameraController;
        private Camera cam;
        private Guid localPlayerID;

        private int texWidth;
        private int texHeight;
        private int pixelsPerHex;
        private int mapWidth;
        private int mapHeight;

        // Cached terrain layer (fog + entities painted on top each refresh)
        private Color[] terrainPixels;
        private Color[] displayPixels; // terrain + entities (base layer)
        private Color[] uploadPixels;  // displayPixels + viewport rect (uploaded to GPU)

        private bool isDirty;
        private float lastApplyTime;
        private bool isInitialized;

        // Incremental fog tracking — only repaint changed tiles
        private HashSet<HexCoordinate> dirtyTerrainCoords = new HashSet<HexCoordinate>();

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, CameraController camController)
        {
            cameraController = camController;
            cam = camController.GetComponent<Camera>();

            // --- Root panel (border/background) ---
            root = UIHelper.CreatePanel(canvasTransform, "MiniMap",
                UIHelper.PanelBg, UIHelper.SmallCornerRadius);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(1, 0);
            rootRT.anchorMax = new Vector2(1, 0);
            rootRT.pivot = new Vector2(1, 0);
            float totalSize = PanelSize + BorderWidth * 2;
            rootRT.sizeDelta = new Vector2(totalSize, totalSize);
            rootRT.anchoredPosition = new Vector2(
                -PanelMargin,
                BottomBarOffset + PanelMargin);

            // --- RawImage for the map texture ---
            var imgGO = new GameObject("MiniMapImage", typeof(RectTransform), typeof(RawImage));
            imgGO.transform.SetParent(root.transform, false);
            mapImage = imgGO.GetComponent<RawImage>();

            var imgRT = imgGO.GetComponent<RectTransform>();
            imgRT.anchorMin = Vector2.zero;
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = new Vector2(BorderWidth, BorderWidth);
            imgRT.offsetMax = new Vector2(-BorderWidth, -BorderWidth);

            // Make the RawImage receive pointer events
            mapImage.raycastTarget = true;

            // Wire click/drag events via EventTrigger on the RawImage
            var trigger = imgGO.AddComponent<EventTrigger>();

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => PanCameraToClick((PointerEventData)data));
            trigger.triggers.Add(clickEntry);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => PanCameraToClick((PointerEventData)data));
            trigger.triggers.Add(dragEntry);

            root.SetActive(false);
            isInitialized = true;
        }

        // ================================================================
        // Show / Hide
        // ================================================================

        public bool IsVisible => root != null && root.activeSelf;

        public void Show() { if (root != null) root.SetActive(true); }
        public void Hide() { if (root != null) root.SetActive(false); }

        // ================================================================
        // Full Rebuild (on game start / load)
        // ================================================================

        public void BuildMap(GameState gameState, Guid playerID)
        {
            localPlayerID = playerID;

            mapWidth = gameState.mapData.width;
            mapHeight = gameState.mapData.height;

            // Scale: 3px/hex for maps <=32, else 2px/hex
            pixelsPerHex = (mapWidth <= 32 && mapHeight <= 32) ? MaxPixelsPerHex : MinPixelsPerHex;
            texWidth = mapWidth * pixelsPerHex;
            texHeight = mapHeight * pixelsPerHex;

            // Create texture
            if (mapTexture != null) Destroy(mapTexture);
            mapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point;
            mapTexture.wrapMode = TextureWrapMode.Clamp;

            int pixelCount = texWidth * texHeight;
            terrainPixels = new Color[pixelCount];
            displayPixels = new Color[pixelCount];
            uploadPixels = new Color[pixelCount];

            // Paint terrain
            var player = gameState.GetPlayer(localPlayerID);
            bool hasFog = gameState.visibilityMode == VisibilityMode.Normal && player != null;

            foreach (var kvp in gameState.mapData.tiles)
            {
                var coord = kvp.Key;
                var tile = kvp.Value;

                Color color = SporefrontColors.GetTerrainColor(tile.terrain, tile.elevation);

                // Apply fog darkening
                if (hasFog)
                {
                    var vis = player.GetVisibilityLevel(coord);
                    color = ApplyFog(color, vis);
                }

                PaintHex(terrainPixels, coord.q, coord.r, color);
            }

            // Copy terrain to display, then paint entities
            System.Array.Copy(terrainPixels, displayPixels, terrainPixels.Length);
            PaintEntities(gameState, player, hasFog);

            System.Array.Copy(displayPixels, uploadPixels, displayPixels.Length);
            mapTexture.SetPixels(uploadPixels);
            mapTexture.Apply();
            mapImage.texture = mapTexture;

            isDirty = false;
            lastApplyTime = Time.unscaledTime;
        }

        // ================================================================
        // Dirty-tile tracking (called from UIManager fog loop)
        // ================================================================

        public void MarkTerrainDirty(HexCoordinate coord)
        {
            dirtyTerrainCoords.Add(coord);
        }

        // ================================================================
        // Incremental Refresh (called from HandleStateChanges)
        // ================================================================

        public void RefreshIncremental(GameState gameState, bool hasEntityChange, bool hasFogChange)
        {
            if (!isInitialized || mapTexture == null) return;

            var player = gameState.GetPlayer(localPlayerID);
            bool hasFog = gameState.visibilityMode == VisibilityMode.Normal && player != null;

            // Only repaint dirty terrain tiles (fog changes) instead of all 4096+
            if (hasFogChange && dirtyTerrainCoords.Count > 0)
            {
                foreach (var coord in dirtyTerrainCoords)
                {
                    if (gameState.mapData.tiles.TryGetValue(coord, out var tile))
                    {
                        Color color = SporefrontColors.GetTerrainColor(tile.terrain, tile.elevation);
                        if (hasFog)
                        {
                            var vis = player.GetVisibilityLevel(coord);
                            color = ApplyFog(color, vis);
                        }
                        PaintHex(terrainPixels, coord.q, coord.r, color);
                    }
                }
                dirtyTerrainCoords.Clear();
            }

            if (hasEntityChange || hasFogChange)
            {
                // Copy terrain to display, then paint entities on top
                System.Array.Copy(terrainPixels, displayPixels, terrainPixels.Length);
                PaintEntities(gameState, player, hasFog);
                isDirty = true;
            }
        }

        // Full rebuild — used on game start, save load, spectator switch
        public void RebuildFull(GameState gameState)
        {
            if (!isInitialized || mapTexture == null) return;

            var player = gameState.GetPlayer(localPlayerID);
            bool hasFog = gameState.visibilityMode == VisibilityMode.Normal && player != null;

            // Rebuild entire terrain layer
            foreach (var kvp in gameState.mapData.tiles)
            {
                var coord = kvp.Key;
                var tile = kvp.Value;

                Color color = SporefrontColors.GetTerrainColor(tile.terrain, tile.elevation);

                if (hasFog)
                {
                    var vis = player.GetVisibilityLevel(coord);
                    color = ApplyFog(color, vis);
                }

                PaintHex(terrainPixels, coord.q, coord.r, color);
            }

            // Copy terrain, paint entities on top
            System.Array.Copy(terrainPixels, displayPixels, terrainPixels.Length);
            PaintEntities(gameState, player, hasFog);

            dirtyTerrainCoords.Clear();
            isDirty = true;
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // LateUpdate — viewport rect + throttled texture apply
        // ================================================================

        private void LateUpdate()
        {
            if (!IsVisible || mapTexture == null || cam == null) return;

            // Copy base layer and draw viewport rect on top
            System.Array.Copy(displayPixels, uploadPixels, displayPixels.Length);
            DrawViewportRect();

            // Throttled texture upload (viewport rect changes every frame when panning)
            if (Time.unscaledTime - lastApplyTime >= ApplyThrottle || isDirty)
            {
                mapTexture.SetPixels(uploadPixels);
                mapTexture.Apply();
                isDirty = false;
                lastApplyTime = Time.unscaledTime;
            }
        }

        // ================================================================
        // Viewport Rectangle
        // ================================================================

        private void DrawViewportRect()
        {
            if (cam == null || uploadPixels == null) return;

            // Get camera world-space bounds
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cameraController.transform.position;

            // Convert camera corners to hex coordinates
            float minWorldX = camPos.x - halfWidth;
            float maxWorldX = camPos.x + halfWidth;
            float minWorldY = camPos.y - halfHeight;
            float maxWorldY = camPos.y + halfHeight;

            // Convert world positions to pixel positions
            int pxMinX = WorldXToPixel(minWorldX);
            int pxMaxX = WorldXToPixel(maxWorldX);
            int pxMinY = WorldYToPixel(minWorldY);
            int pxMaxY = WorldYToPixel(maxWorldY);

            // Clamp to texture bounds
            pxMinX = Mathf.Clamp(pxMinX, 0, texWidth - 1);
            pxMaxX = Mathf.Clamp(pxMaxX, 0, texWidth - 1);
            pxMinY = Mathf.Clamp(pxMinY, 0, texHeight - 1);
            pxMaxY = Mathf.Clamp(pxMaxY, 0, texHeight - 1);

            // Draw rectangle outline (1px thick) onto uploadPixels
            DrawHLine(pxMinX, pxMaxX, pxMinY, ViewportColor);
            DrawHLine(pxMinX, pxMaxX, pxMaxY, ViewportColor);
            DrawVLine(pxMinX, pxMinY, pxMaxY, ViewportColor);
            DrawVLine(pxMaxX, pxMinY, pxMaxY, ViewportColor);
        }

        private void DrawHLine(int x0, int x1, int y, Color color)
        {
            if (y < 0 || y >= texHeight) return;
            for (int x = x0; x <= x1; x++)
            {
                if (x >= 0 && x < texWidth)
                    uploadPixels[y * texWidth + x] = color;
            }
        }

        private void DrawVLine(int x, int y0, int y1, Color color)
        {
            if (x < 0 || x >= texWidth) return;
            for (int y = y0; y <= y1; y++)
            {
                if (y >= 0 && y < texHeight)
                    uploadPixels[y * texWidth + x] = color;
            }
        }

        // ================================================================
        // Click / Drag → Pan Camera
        // ================================================================

        private void PanCameraToClick(PointerEventData eventData)
        {
            if (cameraController == null || mapImage == null) return;

            // Convert click position to local position within the RawImage
            RectTransform imgRT = mapImage.GetComponent<RectTransform>();
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                imgRT, eventData.position, eventData.pressEventCamera, out localPoint))
                return;

            // Normalize to 0-1 within the image rect
            Rect rect = imgRT.rect;
            float normalizedX = (localPoint.x - rect.x) / rect.width;
            float normalizedY = (localPoint.y - rect.y) / rect.height;

            // Clamp to valid range
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);

            // Convert to hex coordinate
            int hexQ = Mathf.RoundToInt(normalizedX * (mapWidth - 1));
            int hexR = Mathf.RoundToInt(normalizedY * (mapHeight - 1));

            var coord = new HexCoordinate(hexQ, hexR);
            cameraController.FocusOn(coord, -1f, true);
        }

        // ================================================================
        // Pixel Painting Helpers
        // ================================================================

        private void PaintHex(Color[] pixels, int hexQ, int hexR, Color color)
        {
            int baseX = hexQ * pixelsPerHex;
            int baseY = hexR * pixelsPerHex;

            for (int dy = 0; dy < pixelsPerHex; dy++)
            {
                for (int dx = 0; dx < pixelsPerHex; dx++)
                {
                    int px = baseX + dx;
                    int py = baseY + dy;
                    if (px >= 0 && px < texWidth && py >= 0 && py < texHeight)
                        pixels[py * texWidth + px] = color;
                }
            }
        }

        private void PaintDot(Color[] pixels, int hexQ, int hexR, Color color, int dotSize = -1)
        {
            if (dotSize < 0) dotSize = Mathf.Max(1, pixelsPerHex - 1);

            int centerX = hexQ * pixelsPerHex + pixelsPerHex / 2;
            int centerY = hexR * pixelsPerHex + pixelsPerHex / 2;
            int half = dotSize / 2;

            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int px = centerX + dx;
                    int py = centerY + dy;
                    if (px >= 0 && px < texWidth && py >= 0 && py < texHeight)
                        pixels[py * texWidth + px] = color;
                }
            }
        }

        // ================================================================
        // Entity Painting
        // ================================================================

        private void PaintEntities(GameState gameState, PlayerState localPlayer, bool hasFog)
        {
            // Paint buildings
            foreach (var kvp in gameState.mapData.buildingCoordinates)
            {
                var coord = kvp.Value;
                if (hasFog && localPlayer != null)
                {
                    var vis = localPlayer.GetVisibilityLevel(coord);
                    if (vis == VisibilityLevel.Unexplored) continue;
                }

                var building = gameState.GetBuilding(kvp.Key);
                if (building == null) continue;

                Color dotColor = SporefrontColors.InkMid;
                if (building.ownerID.HasValue)
                {
                    var owner = gameState.GetPlayer(building.ownerID.Value);
                    if (owner != null)
                        dotColor = SporefrontColors.ParsePlayerColor(owner.colorHex);
                }

                PaintDot(displayPixels, coord.q, coord.r, dotColor);
            }

            // Paint armies (slightly brighter/larger)
            foreach (var kvp in gameState.mapData.armyCoordinates)
            {
                var coord = kvp.Value;
                if (hasFog && localPlayer != null)
                {
                    var vis = localPlayer.GetVisibilityLevel(coord);
                    if (vis == VisibilityLevel.Unexplored) continue;
                    // Only show enemy armies if currently visible
                    var army = gameState.GetArmy(kvp.Key);
                    if (army != null && army.ownerID.HasValue && army.ownerID.Value != localPlayerID
                        && vis != VisibilityLevel.Visible)
                        continue;
                }

                var armyData = gameState.GetArmy(kvp.Key);
                if (armyData == null) continue;

                Color dotColor = SporefrontColors.InkLight;
                if (armyData.ownerID.HasValue)
                {
                    var owner = gameState.GetPlayer(armyData.ownerID.Value);
                    if (owner != null)
                    {
                        dotColor = SporefrontColors.ParsePlayerColor(owner.colorHex);
                        // Brighten army dots slightly
                        dotColor = Color.Lerp(dotColor, Color.white, 0.2f);
                    }
                }

                PaintDot(displayPixels, coord.q, coord.r, dotColor, pixelsPerHex);
            }

            // Paint villager groups
            foreach (var kvp in gameState.mapData.villagerGroupCoordinates)
            {
                var coord = kvp.Value;
                if (hasFog && localPlayer != null)
                {
                    var vis = localPlayer.GetVisibilityLevel(coord);
                    if (vis == VisibilityLevel.Unexplored) continue;
                    // Only show own villagers or if visible
                    var vg = gameState.GetVillagerGroup(kvp.Key);
                    if (vg != null && vg.ownerID.HasValue && vg.ownerID.Value != localPlayerID
                        && vis != VisibilityLevel.Visible)
                        continue;
                }

                var vgData = gameState.GetVillagerGroup(kvp.Key);
                if (vgData == null) continue;

                Color dotColor = SporefrontColors.InkFaded;
                if (vgData.ownerID.HasValue)
                {
                    var owner = gameState.GetPlayer(vgData.ownerID.Value);
                    if (owner != null)
                        dotColor = Color.Lerp(SporefrontColors.ParsePlayerColor(owner.colorHex),
                            Color.white, 0.1f);
                }

                PaintDot(displayPixels, coord.q, coord.r, dotColor, Mathf.Max(1, pixelsPerHex - 1));
            }
        }

        // ================================================================
        // Coordinate Conversion Helpers
        // ================================================================

        private int WorldXToPixel(float worldX)
        {
            // Inverse of HexMetrics.HexToWorldPosition x component
            // x = q * HexWidth + (r%2!=0 ? InnerRadius : 0)
            // Approximate: ignore row offset, just use center mapping
            float hexQ = worldX / HexMetrics.HexWidth;
            return Mathf.RoundToInt(hexQ * pixelsPerHex);
        }

        private int WorldYToPixel(float worldY)
        {
            // Inverse of HexMetrics.HexToWorldPosition y component
            // y = r * (OuterRadius * 1.5) * IsometricYScale
            float rowSpacing = HexMetrics.OuterRadius * 1.5f * HexMetrics.IsometricYScale;
            float hexR = worldY / rowSpacing;
            return Mathf.RoundToInt(hexR * pixelsPerHex);
        }

        // ================================================================
        // Fog Helper
        // ================================================================

        private static Color ApplyFog(Color baseColor, VisibilityLevel level)
        {
            switch (level)
            {
                case VisibilityLevel.Unexplored:
                    return Color.Lerp(baseColor, SporefrontColors.InkBlack, 0.82f);
                case VisibilityLevel.Explored:
                    return Color.Lerp(baseColor, SporefrontColors.InkDark, 0.35f);
                default:
                    return baseColor;
            }
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            if (mapTexture != null) Destroy(mapTexture);
        }
    }
}
