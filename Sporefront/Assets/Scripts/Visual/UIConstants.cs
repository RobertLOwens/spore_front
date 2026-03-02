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

        public const float SpaceXS = 6f;
        public const float SpaceSM = 8f;
        public const float SpaceMD = 16f;
        public const float SpaceLG = 24f;
        public const float SpaceXL = 32f;

        // ================================================================
        // Font Scale
        // ================================================================

        public const int FontCaption = 14;
        public const int FontSmall = 15;
        public const int FontBody = 18;
        public const int FontSubheader = 21;
        public const int FontHeader = 24;
        public const int FontTitle = 30;

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
        public const float SectionCardPadding = 16f;
        public const float SectionCardSpacing = 12f;

        // ================================================================
        // Interactive Element Sizing
        // ================================================================

        public const float MinButtonHeight = 36f;
        public const float ButtonPaddingH = 12f;
        public const float ButtonPaddingV = 6f;

        // Scroll content defaults
        public const float ScrollContentSpacing = 10f;
        public const float ScrollContentPadding = 14f;

        // ================================================================
        // Interaction
        // ================================================================

        public const float HoverLerpAmount = 0.15f;
        public const float PressedLerpAmount = 0.12f;
        public const float DisabledAlpha = 0.35f;

        // ================================================================
        // Animation
        // ================================================================

        public const float PanelFadeDuration = 0.15f;
        public const float TooltipDelay = 0.5f;
    }
}
