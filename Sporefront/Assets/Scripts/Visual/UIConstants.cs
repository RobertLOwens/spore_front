// ============================================================================
// FILE: Visual/UIConstants.cs
// PURPOSE: Centralized UI layout constants — spacing, font sizes, modal tiers,
//          interaction values, and animation durations
// ============================================================================

namespace Sporefront.Visual
{
    public static class UIConstants
    {
        // ================================================================
        // Spacing Scale
        // ================================================================

        public const float SpaceXS = 2f;
        public const float SpaceSM = 4f;
        public const float SpaceMD = 8f;
        public const float SpaceLG = 12f;
        public const float SpaceXL = 16f;

        // ================================================================
        // Font Scale
        // ================================================================

        public const int FontCaption = 10;
        public const int FontSmall = 11;
        public const int FontBody = 14;
        public const int FontSubheader = 16;
        public const int FontHeader = 18;
        public const int FontTitle = 22;

        // ================================================================
        // Layout Chrome
        // ================================================================

        public const float TopBarHeight = 60f;
        public const float BottomBarHeight = 48f;
        public const float SidePanelWidth = 260f;

        // ================================================================
        // Modal Size Tiers
        // ================================================================

        // Widths
        public const float ModalSmallW = 400f;
        public const float ModalMediumW = 440f;
        public const float ModalLargeW = 520f;
        public const float ModalXLW = 700f;

        // Heights
        public const float ModalSmallH = 420f;
        public const float ModalMediumH = 520f;
        public const float ModalLargeH = 560f;

        // ================================================================
        // Interaction
        // ================================================================

        public const float HoverLerpAmount = 0.15f;
        public const float PressedLerpAmount = 0.12f;
        public const float DisabledAlpha = 0.5f;

        // ================================================================
        // Animation
        // ================================================================

        public const float PanelFadeDuration = 0.15f;
        public const float TooltipDelay = 0.5f;
    }
}
