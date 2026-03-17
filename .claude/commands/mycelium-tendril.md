# Mycelium Tendril Creator

Create an animated mycelium tendril tree effect for a Unity UI panel. The system uses two components: a **renderer** (`UITendrilRenderer`) and an **animator** that defines the tree structure and drives growth animation.

## Architecture

The tendril system has two layers:

1. **UITendrilRenderer** (`Scripts/Visual/UITendrilRenderer.cs`) — A `MaskableGraphic` that draws multi-strand Catmull-Rom spline paths as procedural mesh quads. Handles strand wave offsets, growth progress clipping, width taper, idle pulse, and per-branch coloring. Max 60,000 vertices.

2. **Animator MonoBehaviour** — Builds the tree topology (trunk, limbs, sub-branches, tendrils) using recursive `GenerateBranch()`, then drives `growthProgress` per branch over time with cascading delays. See `Scripts/Visual/MenuTendrilAnimator.cs` for the reference implementation.

## How to Add Tendrils to a Panel

### Step 1: Create the tendril layer

```csharp
// Create a full-screen RectTransform behind your content
var tendrilGO = new GameObject("TendrilLayer", typeof(RectTransform), typeof(CanvasRenderer));
tendrilGO.transform.SetParent(parentPanel.transform, false);
var tendrilRT = tendrilGO.GetComponent<RectTransform>();
UIHelper.StretchFull(tendrilRT);
tendrilGO.transform.SetAsFirstSibling(); // render behind everything

var tendrilRenderer = tendrilGO.AddComponent<UITendrilRenderer>();
tendrilRenderer.raycastTarget = false; // don't block clicks
```

### Step 2: Add an animator

Either reuse `MenuTendrilAnimator` or create a custom one. The animator needs:
- A reference to the `UITendrilRenderer`
- A `RectTransform` for the canvas (to get dimensions)
- Optionally a center column `RectTransform` to avoid

```csharp
var animator = tendrilGO.AddComponent<MenuTendrilAnimator>();
animator.Initialize(tendrilRenderer, centerColumnRT, canvasRT);
animator.StartAnimation(); // begin growth
```

### Step 3: Build branches (if making a custom animator)

```csharp
// Define control points for a path
var points = new List<Vector2> { startPos, midPos, endPos };

// Define strand appearance
var strands = new List<UITendrilRenderer.StrandParams>
{
    new UITendrilRenderer.StrandParams
    {
        width = 4.0f,          // pixel width of this strand
        alpha = 0.85f,         // base opacity
        waveFrequency = 3.0f,  // sinusoidal wave cycles along path
        wavePhase = 0.0f       // phase offset (use different values per strand)
    }
};

// Add the branch: maxOffset=8f controls strand spread, taperFraction=0.15f controls tip taper
var branch = tendrilRenderer.AddBranch(points, strands, maxOffset: 8f, taperFraction: 0.15f);
branch.branchColor = SporefrontColors.SporeRed; // or any Color
branch.growthProgress = 0f; // animate 0→1 to grow
```

### Step 4: Animate in Update()

```csharp
// Increment growthProgress per branch over time
float t = elapsed / duration;
branch.growthProgress = 1f - (1f - t) * (1f - t); // ease-out

// After all branches reach 1.0, add idle pulse
branch.idlePulsePhase = Time.time * 2.0f + branchIndex * 0.7f;

// Mark dirty to re-render
tendrilRenderer.MarkDirty();
```

## Key Parameters

### Branch depth hierarchy (from MenuTendrilAnimator)

| Depth | Role | Strands | Width | Duration | Children |
|-------|------|---------|-------|----------|----------|
| 0 | Trunk | 2-3 thick | 4-5px | 1.8s | 6 limbs |
| 1 | Limb | 2 medium | 3-5px | 1.2s | 10 sub-branches |
| 2 | Sub-branch | 1 thin | 2.4px | 1.0s | 5 tendrils |
| 3 | Tendril | 1 fine | 1.8px | 0.8s | 5 sub-tendrils |
| 4 | Sub-tendril | 1 fine | 1.8px | 0.8s | none (max depth) |

### Strand presets

- **Trunk**: 5 interleaved strands split into two halves (red/teal), width 3-4.5px, high wave frequency (3-4.5)
- **Limb**: 2 strands, width 3.5-5px, lower wave frequency (1.2-1.5)
- **Sub-branch**: 1 strand, width 2.4px, wave frequency 1.0
- **Tendril**: 1 strand, width 1.8px, wave frequency 0.8

### Recursive branching pattern

Children spawn from points **along** the parent branch (20%-95%), not from the tip. Each child alternates side (left/right) with 30-70 degrees divergence from parent angle. Spawn points near the screen edge are skipped.

### Colors used

- `SporefrontColors.SporeRed` — warm red for even-indexed limbs
- `SporefrontColors.SporeTeal` — cool teal for odd-indexed limbs
- Trunk uses both colors intertwined

## Reference Files

- `Sporefront/Assets/Scripts/Visual/UITendrilRenderer.cs` — the renderer
- `Sporefront/Assets/Scripts/Visual/MenuTendrilAnimator.cs` — reference animator
- `Sporefront/Assets/Scripts/Visual/MainMenuPanel.cs` — integration example (see `BuildContent()`)

## User Request

$ARGUMENTS
