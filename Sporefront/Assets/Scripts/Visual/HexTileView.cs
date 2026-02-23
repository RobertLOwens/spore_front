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
        private VisibilityLevel _visibilityLevel = VisibilityLevel.Visible;

        private MeshRenderer fillRenderer;
        private MeshRenderer borderRenderer;
        private MaterialPropertyBlock fillBlock;

        private Color baseColor;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

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

        public void SetVisibility(VisibilityLevel level)
        {
            if (_visibilityLevel == level) return;
            _visibilityLevel = level;
            UpdateVisuals();
        }

        public VisibilityLevel CurrentVisibility => _visibilityLevel;

        // ================================================================
        // Visual Update
        // ================================================================

        private void UpdateVisuals()
        {
            Color color = baseColor;

            // Apply fog darkening based on visibility level
            switch (_visibilityLevel)
            {
                case VisibilityLevel.Unexplored:
                    color = Color.Lerp(color, SporefrontColors.InkBlack, 0.82f);
                    break;
                case VisibilityLevel.Explored:
                    color = Color.Lerp(color, SporefrontColors.InkDark, 0.35f);
                    break;
                // Visible: full baseColor, no change
            }

            // Selection visuals delegated to SelectionRenderer with glow effect (#15)
            // Only apply hover tinting here; suppress hover on non-visible tiles
            if (_isHovered && !_isSelected && _visibilityLevel == VisibilityLevel.Visible)
            {
                // Subtle brighten on hover
                color = Color.Lerp(color, Color.white, 0.15f);
            }

            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            fillRenderer.GetPropertyBlock(fillBlock);
            fillBlock.SetColor(BaseColorProperty, color);
            fillBlock.SetColor(ColorProperty, color);
            fillRenderer.SetPropertyBlock(fillBlock);
        }
    }
}
