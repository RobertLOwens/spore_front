---
description: Sporefront UI Reference
---

# Sporefront UI Reference

Build or modify Unity UGUI panels for the Sporefront project. All UI is constructed programmatically in C# — no prefabs. Follow the patterns, colors, spacing, and conventions documented below exactly.

## Fonts

- **Body**: `UIHelper.BodyFont` — IM Fell English (`Resources/Fonts/IMFellEnglish-Regular`)
- **Headers**: `UIHelper.HeaderFont` — Medieval Sharp (`Resources/Fonts/MedievalSharp-Regular`)
- Use `isHeader: true` on `CreateLabel()` to auto-select HeaderFont

| Constant | Size | Use |
|---|---|---|
| `UIConstants.FontCaption` | 14 | Badges, footnotes, progress overlays |
| `UIConstants.FontSmall` | 15 | Secondary labels, metadata |
| `UIConstants.FontBody` | 18 | Default body text, button labels |
| `UIConstants.FontSubheader` | 21 | Section card headers |
| `UIConstants.FontHeader` | 24 | Panel titles |
| `UIConstants.FontTitle` | 30 | Main menu titles only |

## Color System (SporefrontColors.cs)

### Parchment (light backgrounds)

| Name | Hex | Use |
|---|---|---|
| ParchmentCream | #F5EDD6 | Lightest highlight |
| ParchmentLight | #F2E8D5 | Ink progress bar track |
| ParchmentMid | #E4D5B7 | Default text on dark panels |
| ParchmentDark | #D4C4A0 | Sub-cards on parchment panels, ledger cards |
| ParchmentDeep | #C4B08A | Nested containers |
| ParchmentShadow | #A89970 | Slider handles, muted elements |

### Ink (text on parchment backgrounds)

| Name | Hex | Use |
|---|---|---|
| InkBlack | #1A1611 | Headers on parchment (`UIHelper.InkHeaderText`) |
| InkDark | #2C2418 | Body text on parchment (`UIHelper.InkBodyText`) |
| InkMid | #4A3D2E | Subtext, close button text (`UIHelper.InkSubText`) |
| InkLight | #6B5D4A | Tertiary info |
| InkFaded | #8A7D6A | Muted text, progress bar bg on dark panels |

### Dark UI Backgrounds (dark panels, HUD)

| Name | Hex | Use |
|---|---|---|
| BgDeep | #0D0B08 | HUD bars, deepest bg (`UIHelper.HudBg`) |
| BgSection | #151210 | Section backgrounds |
| BgCard | #1A1611 | Card backgrounds |
| BgElevated | #1E1B16 | Modal panels (`UIHelper.PanelBg` @ 0.95 alpha) |
| BgSurface | #252219 | Button backgrounds (`UIHelper.ButtonBg`) |

### Accent Colors

| Name | Hex | Light Variant |
|---|---|---|
| SporeRed | #8B3A3A | SporeRedLight #A85454 |
| SporeTeal | #3A6B6B | SporeTealLight #4A9090 |
| SporeGreen | #3A5E3A | SporeGreenLight #4A7A4A |
| SporeAmber | #8B6B3A | SporeAmberLight #A8854A |
| SporePurple | #5E3A5E | SporePurpleLight #7A5478 |

### Context Rule

- **Dark panels** (BgElevated, BgDeep): text = `ParchmentMid` (body), `ParchmentLight` (headers)
- **Parchment panels** (PanelParchmentBg): text = `InkBlack`/`InkDark` (use `UIHelper.InkHeaderText`, `UIHelper.InkBodyText`)

### Borders

- `SporefrontColors.BorderSubtle` — ParchmentMid @ 10% alpha (panel outlines)
- `SporefrontColors.BorderAccent` — ParchmentMid @ 18% alpha (dividers)

## Spacing Scale (UIConstants)

| Constant | Value | Use |
|---|---|---|
| SpaceXS | 6 | Tight gaps |
| SpaceSM | 8 | Default row/group spacing |
| SpaceMD | 16 | Section padding, card padding |
| SpaceLG | 24 | Between major sections |
| SpaceXL | 32 | Panel edge margins |
| ButtonPaddingH / V | 12 / 6 | Internal button label padding |
| SectionCardPadding | 16 | Inside section cards |
| SectionCardSpacing | 12 | Between items in section cards |
| ScrollContentSpacing | 10 | Between scroll items |
| ScrollContentPadding | 14 | Scroll viewport edge inset |

## Corner Radius

| Constant | Value | Use |
|---|---|---|
| PanelCornerRadius | 12 | Modal/panel backgrounds (default) |
| ButtonCornerRadius | 10 | Buttons |
| SmallCornerRadius | 6 | Cards, progress bars, badges |

Rounded corners are procedural sprites via `UIHelper.GetRoundedRectSprite(radius)`.

## Modal Size Tiers (UIConstants)

| Tier | Width x Height |
|---|---|
| Small | 500 x 525 |
| Medium | 550 x 650 |
| Large | 650 x 700 |
| XL | 875 x (varies) |
| Detail | 600 x 775 |
| Build Menu | 650 x 725 |

## Standard Modal Structure

Every modal panel follows this skeleton:

```csharp
public void Initialize(Transform canvasTransform, Guid playerID)
{
    localPlayerID = playerID;

    // 1. Semi-transparent backdrop (click-to-close)
    backdrop = UIHelper.CreatePanel(canvasTransform, "MyPanelBackdrop",
        new Color(0, 0, 0, 0.4f));
    var bdRT = backdrop.GetComponent<RectTransform>();
    UIHelper.StretchFull(bdRT);
    backdropCG = backdrop.AddComponent<CanvasGroup>();
    var bdBtn = backdrop.AddComponent<Button>();
    bdBtn.transition = Selectable.Transition.None;
    bdBtn.onClick.AddListener(Close);

    // 2. Centered panel with parchment bg
    panel = UIHelper.CreatePanel(backdrop.transform, "MyPanel",
        UIHelper.PanelParchmentBg);
    var rt = panel.GetComponent<RectTransform>();
    UIHelper.SetFixedSize(rt, UIConstants.ModalMediumW, UIConstants.ModalMediumH);

    // 3. Tendril decoration
    PopupTendrilDecorator.Attach(rt);

    // 4. Click sink — prevents backdrop close when clicking panel
    var panelBtn = panel.AddComponent<Button>();
    panelBtn.transition = Selectable.Transition.None;

    // 5. Scroll content area (leaves 48px at bottom for close button)
    var scroll = UIHelper.CreateScrollView(panel.transform, "Scroll", out contentRT);
    var scrollRT = scroll.GetComponent<RectTransform>();
    UIHelper.StretchFull(scrollRT);
    scrollRT.offsetMin = new Vector2(0, 48);
    scrollRT.offsetMax = Vector2.zero;

    // 6. Close button pinned to bottom
    var closeBtn = UIHelper.CreateInkCloseButton(panel.transform, Close);
    var closeBtnRT = closeBtn.GetComponent<RectTransform>();
    closeBtnRT.anchorMin = new Vector2(0, 0);
    closeBtnRT.anchorMax = new Vector2(1, 0);
    closeBtnRT.pivot = new Vector2(0.5f, 0);
    closeBtnRT.offsetMin = new Vector2(12, 4);
    closeBtnRT.offsetMax = new Vector2(-12, 46);

    backdrop.SetActive(false);
}
```

### Show/Close with Fade

```csharp
public void Show(/* params */)
{
    if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
    backdrop.SetActive(true);
    fadeCoroutine = StartCoroutine(UIHelper.FadeIn(backdropCG));
}

public void Close()
{
    if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
    fadeCoroutine = StartCoroutine(UIHelper.FadeOut(backdropCG));
}
```

## UIHelper API Quick Reference

### Layout

```csharp
UIHelper.StretchFull(rt)                                       // anchor 0,0 to 1,1, zero offsets
UIHelper.SetFixedSize(rt, w, h)                                // center-anchored with explicit size
UIHelper.SetAnchors(rt, min, max, oMin, oMax)
UIHelper.CreateHorizontalRow(parent, height: 34, spacing: 8)   // returns HLG
UIHelper.CreateVerticalGroup(parent, spacing: 8)                // returns VLG
UIHelper.CreateScrollView(parent, name, out contentRT)          // returns ScrollRect
UIHelper.CreateSectionCard(parent, name, headerText?)           // returns VLG (tinted bg, 16px pad, 12px spacing)
UIHelper.CreateLedgerCard(parent, name, bgColor?)               // parchment card with ink border
UIHelper.CreateDivider(parent, color?, height: 1)
UIHelper.AddParchmentOverlay(parent, alpha: 0.25f)              // semi-transparent overlay
```

### Elements

```csharp
UIHelper.CreateLabel(parent, text, fontSize?, color?, alignment?, isHeader?)
UIHelper.CreateButton(parent, text, bgColor?, textColor?, fontSize?, onClick?)
UIHelper.CreateInkCloseButton(parent, onClick)                  // italic "Close" with underline
UIHelper.CreateKeyBadge(parent, key, size: 18)                  // shortcut badge (e.g., "W")
UIHelper.CreateSlider(parent, min, max, wholeNumbers: true, onChange?)
UIHelper.CreateProgressBar(parent, height: 16, bgColor?, fillColor?)
UIHelper.CreateProgressBarWithLabel(parent, height: 16, bgColor?, fillColor?)
UIHelper.CreateInkProgressBar(parent, height: 10, bgColor?, fillColor?)
UIHelper.CreateInkProgressBarWithLabel(parent, height: 10, bgColor?, fillColor?)
UIHelper.CreatePanel(parent, name, bgColor, cornerRadius: -1)   // -1 = PanelCornerRadius
```

### Button Colors

```csharp
UIHelper.StandardButtonColors(bg)   // 15%/12% white lerp for hover/pressed
UIHelper.CardButtonColors(bg)       // 10%/8% subtle hover for list items
// DisabledAlpha = 0.35f
```

### Utilities

```csharp
UIHelper.FormatCost(Dictionary<ResourceType, int> cost)   // "W50 S20 O10"
UIHelper.FormatTime(double seconds)                        // "2m 15s" or "Done"
UIHelper.FormatArmyStatus(ArmyData army)                   // "[E]", "[C]", "[R]" in color
UIHelper.ResourceIcon(ResourceType type)                   // "W", "F", "S", "O"
UIHelper.GetResourceBarColor(ResourceType type)            // watercolor fills
UIHelper.AddTooltip(gameObject, text)                      // hover tooltip via EventTrigger
UIHelper.GetRoundedRectSprite(cornerRadius)                // cached procedural sprite
```

### Animation

```csharp
UIHelper.FadeIn(canvasGroup, duration: 0.15f)    // coroutine, 0->1 alpha
UIHelper.FadeOut(canvasGroup, duration: 0.15f)    // coroutine, 1->0 then deactivate
PopupTendrilDecorator.Attach(panelRT, seed?)      // corner tendrils for modals
```

## Panel Conventions

1. **Namespace**: `Sporefront.Visual`
2. **Class**: `MonoBehaviour` with `Initialize(Transform canvasTransform, Guid playerID)`
3. **Events**: Declare as `public event Action<...>` at top of class
4. **State fields**: Private, grouped under `// State` comment block
5. **Show/Close**: Use `CanvasGroup` fade, `backdrop.SetActive(false)` on init
6. **Rebuild pattern**: Full rebuild on structural changes, incremental update for dynamic values (progress bars, labels)
7. **Section dividers**: Use `// ================================================================` comment blocks
8. **Parchment modals**: Use `UIHelper.PanelParchmentBg` + `UIHelper.InkHeaderText` / `InkBodyText`
9. **Dark panels** (HUD, sidebars): Use `UIHelper.PanelBg` or `UIHelper.HudBg` + `ParchmentMid` / `ParchmentLight` text
10. **Fingerprinting**: Cache structural state (IDs, counts) and only full-rebuild when structure changes; update labels/progress bars incrementally

## Key Reference Files

- `Sporefront/Assets/Scripts/Visual/UIConstants.cs` — All spacing, font, modal size constants
- `Sporefront/Assets/Scripts/Visual/UIHelper.cs` — All element creation utilities
- `Sporefront/Assets/Scripts/Visual/SporefrontColors.cs` — Full color palette
- `Sporefront/Assets/Scripts/Visual/BuildingDetailPanel.cs` — Reference modal implementation
- `Sporefront/Assets/Scripts/Visual/ResearchTreePanel.cs` — Complex panel with tabs/scroll/detail
- `Sporefront/Assets/Scripts/Visual/PopupTendrilDecorator.cs` — Tendril attachment for modals
- `Sporefront/Assets/sporefront-style-guide.html` — Visual mood board and full CSS design system

## User Request

$ARGUMENTS
