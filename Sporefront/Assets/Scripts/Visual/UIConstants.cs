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

        // ================================================================
        // Tendril Wheel HUD
        // ================================================================

        // Container sizing — large enough for both rings + padding
        public const float WheelContainerSize = 700f;
        public const float WheelCornerPaddingH = 3f;   // horizontal inset — nearly flush
        public const float WheelCornerPaddingV = 1f;   // vertical inset — nearly flush

        // Right wheel (Actions) — dual-ring, bottom-right corner
        // Angles stay within 105°–155° to keep buttons away from edges.
        //
        // Outer ring: Commanders, Military, Combat (3 buttons)
        public const float WheelRightOuterRadius = 280f;
        public const float WheelRightOuterStartAngle = 155f;
        public const float WheelRightOuterEndAngle = 105f;
        public const int   WheelRightOuterCount = 3;
        // Inner ring: Entities, Research, Training (3 buttons)
        public const float WheelRightInnerRadius = 190f;
        public const float WheelRightInnerStartAngle = 148f;
        public const float WheelRightInnerEndAngle = 100f;
        public const int   WheelRightInnerCount = 3;

        // Left wheel (Info) — bottom-left corner
        // Angles 25°–75° mirror the right wheel's safe zone.
        public const float WheelLeftRadius = 200f;
        public const float WheelLeftStartAngle = 25f;
        public const float WheelLeftEndAngle = 75f;
        public const int   WheelLeftButtonCount = 3;

        // Button dimensions (85px — 33% larger than 64px)
        public const float WheelButtonSize = 85f;
        public const float WheelButtonBorder = 2.5f;
        public const float WheelButtonIconSize = 40f;
        public const float WheelButtonHoverScale = 1.1f;

        // Labels — parchment card tooltips
        public const int   WheelLabelFontSize = 18;

        // Key badge
        public const float WheelKeyBadgeSize = 18f;

        // Tendril connections — fully opaque, no alpha
        public const float WheelTendrilWidth = 3.5f;
        public const float WheelTendrilOpacity = 1.0f;
        public const float WheelInnerTendrilWidth = 2.8f;
        public const float WheelInnerTendrilOpacity = 1.0f;
        public const float WheelBranchHairWidth = 1.4f;
        public const float WheelBranchHairOpacity = 1.0f;

        // Animation timing
        public const float WheelPopDuration = 0.45f;
        public const float WheelPopStagger = 0.03f;
        public const float WheelHoverDuration = 0.2f;
        public const float WheelPulseDuration = 2.5f;
        public const float WheelSwayDuration = 14f;
        public const float WheelSwayAngle = 0.15f;

        // Bottom tendril border
        public const float WheelBorderHeight = 28f;
    }
}
