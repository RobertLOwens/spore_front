// ============================================================================
// FILE: Visual/HexGridRenderer.cs
// PURPOSE: Creates and manages HexTileView objects from MapData
//          Shared meshes + MaterialPropertyBlock for SRP Batcher efficiency
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class HexGridRenderer : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        private Dictionary<HexCoordinate, HexTileView> tileViews
            = new Dictionary<HexCoordinate, HexTileView>();

        private Material hexFillMaterial;
        private Material hexBorderMaterial;
        private Mesh sharedHexMesh;
        private Mesh sharedBorderMesh;
        private Transform gridParent;

        private HexTileView selectedTile;

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Build the visual hex grid from engine MapData.
        /// Creates one HexTileView GameObject per tile.
        /// </summary>
        public void BuildGrid(MapData mapData)
        {
            ClearGrid();
            CreateSharedAssets();

            // Container for all tile objects
            var parentGO = new GameObject("HexGrid");
            parentGO.transform.SetParent(transform, false);
            gridParent = parentGO.transform;

            foreach (var kvp in mapData.tiles)
            {
                var coord = kvp.Key;
                var tileData = kvp.Value;

                var tileGO = new GameObject();
                tileGO.transform.SetParent(gridParent, false);

                var view = tileGO.AddComponent<HexTileView>();
                view.Initialize(
                    coord,
                    tileData,
                    sharedHexMesh,
                    sharedBorderMesh,
                    hexFillMaterial,
                    hexBorderMaterial
                );

                tileViews[coord] = view;
            }

            Debug.Log($"[HexGridRenderer] Built grid: {tileViews.Count} tiles");
        }

        /// <summary>
        /// Destroy all tile objects and reset state.
        /// </summary>
        public void ClearGrid()
        {
            if (gridParent != null)
            {
                Destroy(gridParent.gameObject);
                gridParent = null;
            }
            tileViews.Clear();
            selectedTile = null;
        }

        /// <summary>
        /// Look up a tile view by coordinate.
        /// </summary>
        public HexTileView GetTileView(HexCoordinate coord)
        {
            HexTileView view;
            tileViews.TryGetValue(coord, out view);
            return view;
        }

        /// <summary>
        /// Select a tile (deselects previous). Pass null coord to deselect all.
        /// </summary>
        public void SelectTile(HexCoordinate? coord)
        {
            // Deselect previous
            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
                selectedTile = null;
            }

            if (!coord.HasValue) return;

            var view = GetTileView(coord.Value);
            if (view != null)
            {
                view.SetSelected(true);
                selectedTile = view;
            }
        }

        /// <summary>
        /// Set hover state on a tile (clears previous hover).
        /// </summary>
        public void SetHoveredTile(HexCoordinate? coord)
        {
            // Simple approach: we don't track previous hover tile
            // since hover changes every frame — MaterialPropertyBlock is cheap
        }

        // ================================================================
        // Shared Asset Creation
        // ================================================================

        private void CreateSharedAssets()
        {
            sharedHexMesh = HexMeshUtility.CreateHexMesh();
            sharedBorderMesh = HexMeshUtility.CreateHexBorderMesh();

            // URP Unlit material for hex fill
            hexFillMaterial = CreateUnlitMaterial(SporefrontColors.ParchmentMid);
            hexFillMaterial.name = "HexFill";

            // URP Unlit material for hex borders
            hexBorderMaterial = CreateUnlitMaterial(SporefrontColors.HexBorder);
            hexBorderMaterial.name = "HexBorder";
            // Enable transparency for border
            SetMaterialTransparent(hexBorderMaterial);
        }

        private Material CreateUnlitMaterial(Color color)
        {
            // Sprites/Default is always available and works with MaterialPropertyBlock
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[HexGridRenderer] Sprites/Default shader not found!");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            var mat = new Material(shader);
            mat.color = color;
            mat.SetColor("_Color", color);
            return mat;
        }

        private void SetMaterialTransparent(Material mat)
        {
            // Sprites/Default already supports transparency via vertex/color alpha
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            // Clean up runtime materials
            if (hexFillMaterial != null) Destroy(hexFillMaterial);
            if (hexBorderMaterial != null) Destroy(hexBorderMaterial);
            if (sharedHexMesh != null) Destroy(sharedHexMesh);
            if (sharedBorderMesh != null) Destroy(sharedBorderMesh);
        }
    }
}
