// ============================================================================
// FILE: Visual/HexTileView.cs
// PURPOSE: MonoBehaviour per hex tile — terrain color, selection, hover
//          Uses MaterialPropertyBlock to avoid per-tile material cloning
// ============================================================================

using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class HexTileView : MonoBehaviour
    {
        // ================================================================
        // State
        // ================================================================

        public HexCoordinate Coordinate { get; private set; }
        public TerrainType TerrainType { get; private set; }
        public int Elevation { get; private set; }

        private bool _isSelected;
        private bool _isHovered;

        private MeshRenderer fillRenderer;
        private MeshRenderer borderRenderer;
        private MaterialPropertyBlock fillBlock;

        private Color baseColor;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        // ================================================================
        // Initialization
        // ================================================================

        /// <summary>
        /// Set up this tile with shared meshes and materials.
        /// Call once after instantiation.
        /// </summary>
        public void Initialize(
            HexCoordinate coordinate,
            TileData tileData,
            Mesh hexMesh,
            Mesh borderMesh,
            Material fillMaterial,
            Material borderMaterial)
        {
            Coordinate = coordinate;
            TerrainType = tileData.terrain;
            Elevation = tileData.elevation;

            gameObject.name = $"Hex({coordinate.q},{coordinate.r})";
            transform.position = HexMetrics.HexToWorldPosition(coordinate);

            // Fill mesh
            var fillFilter = gameObject.AddComponent<MeshFilter>();
            fillFilter.sharedMesh = hexMesh;
            fillRenderer = gameObject.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = fillMaterial;

            // Border child
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(transform, false);
            // Slight Z offset so border renders on top
            borderGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            var borderFilter = borderGO.AddComponent<MeshFilter>();
            borderFilter.sharedMesh = borderMesh;
            borderRenderer = borderGO.AddComponent<MeshRenderer>();
            borderRenderer.sharedMaterial = borderMaterial;

            // Per-tile color via MaterialPropertyBlock
            fillBlock = new MaterialPropertyBlock();
            baseColor = SporefrontColors.GetTerrainColor(TerrainType, Elevation);
            ApplyColor(baseColor);
        }

        // ================================================================
        // Selection / Hover
        // ================================================================

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            UpdateVisuals();
        }

        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;
            UpdateVisuals();
        }

        // ================================================================
        // Visual Update
        // ================================================================

        private void UpdateVisuals()
        {
            Color color = baseColor;

            // Selection visuals delegated to SelectionRenderer with glow effect (#15)
            // Only apply hover tinting here
            if (_isHovered && !_isSelected)
            {
                // Subtle brighten on hover
                color = Color.Lerp(color, Color.white, 0.15f);
            }

            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            fillRenderer.GetPropertyBlock(fillBlock);
            fillBlock.SetColor(ColorProperty, color);
            fillRenderer.SetPropertyBlock(fillBlock);
        }
    }
}
