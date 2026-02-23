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

        public const float SpaceXS = 3f;
        public const float SpaceSM = 5f;
        public const float SpaceMD = 10f;
        public const float SpaceLG = 15f;
        public const float SpaceXL = 20f;

        // ================================================================
        // Font Scale
        // ================================================================

        public const int FontCaption = 13;
        public const int FontSmall = 14;
        public const int FontBody = 18;
        public const int FontSubheader = 20;
        public const int FontHeader = 23;
        public const int FontTitle = 28;

        // ================================================================
        // Layout Chrome
        // ================================================================

        public const float TopBarHeight = 75f;
        public const float BottomBarHeight = 60f;
        public const float SidePanelWidth = 400f;

        // ================================================================
        // Modal Size Tiers
        // ================================================================

        // Widths
        public const float ModalSmallW = 500f;
        public const float ModalMediumW = 550f;
        public const float ModalLargeW = 650f;
        public const float ModalXLW = 875f;

        // Heights
        public const float ModalSmallH = 525f;
        public const float ModalMediumH = 650f;
        public const float ModalLargeH = 700f;

        // Detail / Build modals
        public const float ModalDetailW = 600f;
        public const float ModalDetailH = 775f;
        public const float ModalBuildMenuW = 650f;
        public const float ModalBuildMenuH = 725f;

        // Section card layout
        public const float SectionCardPadding = 13f;
        public const float SectionCardSpacing = 8f;

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
