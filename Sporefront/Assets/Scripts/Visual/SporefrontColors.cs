// ============================================================================
// FILE: Visual/SporefrontColors.cs
// PURPOSE: Style guide color constants — "ink on parchment" palette
// ============================================================================

using UnityEngine;
using Sporefront.Data;

namespace Sporefront.Visual
{
    public static class SporefrontColors
    {
        // ================================================================
        // Parchment Palette
        // ================================================================

        public static readonly Color ParchmentCream  = HexColor("F5EDD6");
        public static readonly Color ParchmentLight  = HexColor("F2E8D5");
        public static readonly Color ParchmentMid    = HexColor("E4D5B7");
        public static readonly Color ParchmentDark   = HexColor("D4C4A0");
        public static readonly Color ParchmentDeep   = HexColor("C4B08A");
        public static readonly Color ParchmentShadow = HexColor("A89970");

        // ================================================================
        // Ink Palette
        // ================================================================

        public static readonly Color InkBlack = HexColor("1A1611");
        public static readonly Color InkDark  = HexColor("2C2418");
        public static readonly Color InkMid   = HexColor("4A3D2E");
        public static readonly Color InkLight = HexColor("6B5D4A");
        public static readonly Color InkFaded = HexColor("8A7D6A");
        public static readonly Color InkBorder = HexColor("6B5A3E");
        public static readonly Color InkWarmText = HexColor("C8B89A");

        // ================================================================
        // Spore Accent Colors
        // ================================================================

        public static readonly Color SporeRed    = HexColor("8B3A3A");
        public static readonly Color SporePurple = HexColor("5E3A5E");
        public static readonly Color SporeGreen  = HexColor("3A5E3A");
        public static readonly Color SporeAmber  = HexColor("8B6B3A");
        public static readonly Color SporeTeal   = HexColor("3A6B6B");

        // Ink-soaked accent variants (desaturated, warm-shifted for parchment integration)
        public static readonly Color InkRed   = HexColor("5E3530");
        public static readonly Color InkGreen = HexColor("3A4D3A");

        // ================================================================
        // Resource Node Colors
        // ================================================================

        public static readonly Color ResourceWood  = HexColor("5B4A2E");  // dark brown (trees/wood)
        public static readonly Color ResourceFood  = HexColor("4A6B3A");  // muted green (forage/food)
        public static readonly Color ResourceStone = HexColor("7A7060");  // gray-brown (stone)
        public static readonly Color ResourceOre   = HexColor("5A5A6A");  // blue-gray (ore)
        public static readonly Color ResourceHunt  = HexColor("6B4A3A");  // reddish-brown (animals)

        public static Color GetResourceColor(ResourcePointType type)
        {
            switch (type)
            {
                case ResourcePointType.Trees:        return ResourceWood;
                case ResourcePointType.Forage:       return ResourceFood;
                case ResourcePointType.Farmland:     return ResourceFood;
                case ResourcePointType.OreMine:      return ResourceOre;
                case ResourcePointType.StoneQuarry:  return ResourceStone;
                case ResourcePointType.Deer:         return ResourceHunt;
                case ResourcePointType.WildBoar:     return ResourceHunt;
                default:                             return InkMid;
            }
        }

        // ================================================================
        // Dark UI Backgrounds (from style guide)
        // ================================================================

        public static readonly Color BgDeep     = HexColor("0D0B08"); // deepest background, HUD bars, map edges
        public static readonly Color BgSection  = HexColor("151210"); // section/panel backgrounds
        public static readonly Color BgCard     = HexColor("1A1611"); // card backgrounds
        public static readonly Color BgElevated = HexColor("1E1B16"); // elevated panels, modals
        public static readonly Color BgSurface  = HexColor("252219"); // interactive surfaces, buttons

        // ================================================================
        // Dark UI Borders
        // ================================================================

        public static readonly Color BorderSubtle = new Color(
            ParchmentMid.r, ParchmentMid.g, ParchmentMid.b, 0.10f);
        public static readonly Color BorderAccent = new Color(
            ParchmentMid.r, ParchmentMid.g, ParchmentMid.b, 0.18f);

        // ================================================================
        // Light Accent Variants (hover/active states)
        // ================================================================

        public static readonly Color SporeRedLight    = HexColor("A85454");
        public static readonly Color SporePurpleLight = HexColor("7A5478");
        public static readonly Color SporeGreenLight  = HexColor("4A7A4A");
        public static readonly Color SporeAmberLight  = HexColor("A8854A");
        public static readonly Color SporeTealLight   = HexColor("4A9090");

        // ================================================================
        // Hex Border
        // ================================================================

        public static readonly Color HexBorder = new Color(
            InkFaded.r, InkFaded.g, InkFaded.b, 0.4f
        );

        // ================================================================
        // Terrain Color Mapping
        // ================================================================

        public static Color GetTerrainColor(TerrainType terrain, int elevation)
        {
            switch (terrain)
            {
                case TerrainType.Plains:
                    return ParchmentMid;

                case TerrainType.Hill:
                    // Slightly darker for higher elevations
                    return Color.Lerp(ParchmentDark, ParchmentDeep,
                        Mathf.Clamp01(elevation / 3f));

                case TerrainType.Mountain:
                    return ParchmentShadow;

                case TerrainType.Water:
                    // Cool tint on parchment
                    return Color.Lerp(ParchmentMid,
                        new Color(0.7f, 0.75f, 0.8f, 1f), 0.25f);

                case TerrainType.Desert:
                    return ParchmentLight;

                default:
                    return ParchmentMid;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        public static Color ParsePlayerColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return InkMid;

            string hex = hexColor.StartsWith("#") ? hexColor : "#" + hexColor;
            Color color;
            if (ColorUtility.TryParseHtmlString(hex, out color))
                return color;
            return InkMid;
        }

        private static Color HexColor(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + hex, out color))
                return color;
            return Color.magenta; // Obvious error color
        }
    }
}
