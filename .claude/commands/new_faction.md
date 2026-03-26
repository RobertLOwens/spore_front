---
description: New Faction Implementation Guide
---

# New Faction Implementation Guide

This skill walks through every file and method that needs updating when adding a new faction to Sporefront. Follow each phase in order — later phases depend on earlier ones compiling.

Before starting, decide on:
- **Faction name** and theme (e.g., "The Morels — Infantry & Woodland Stealth")
- **2-4 passive bonuses** (vision, gathering, speed, combat, build cost, etc.)
- **Tier III research blocks** (every faction blocks ~8 T3 research types; all factions share a cavalry T3 block)
- **1+ exclusive research chain** (2-3 techs gated to this faction)
- **0-1 exclusive building** (optional unique structure)
- **Unique mechanic** (what makes this faction play differently)

All paths below are relative to `Sporefront/Assets/`.

---

## Phase 1: Core Faction Definition

### 1.1 FactionType Enum
**File:** `Scripts/Models/FactionType.cs`

Add the new enum value:
```csharp
public enum FactionType { None, Morel, Muscaria, NewFaction }
```

Add entries to ALL extension methods in the same file:
- `DisplayName()` — human-readable name (e.g., "The Chanterelles")
- `Description()` — 1-2 sentence faction description
- `StartingBonusDescription()` — comma-separated bonus list for UI

### 1.2 Passive Bonus Methods
**File:** `Scripts/Models/FactionType.cs`

Add new extension methods for each passive bonus. Follow the existing pattern — each bonus is a static method on `FactionType`:

```csharp
// Examples from existing factions:
public static bool HasWoodlandCamouflage(this FactionType f) => f == FactionType.Morel;
public static double WoodGatheringBonus(this FactionType f) => f == FactionType.Morel ? 0.05 : 0.0;
public static bool HasToxicStrikes(this FactionType f) => f == FactionType.Muscaria;
public static double MountainBuildCostReduction(this FactionType f) => f == FactionType.Muscaria ? 0.15 : 0.0;
```

### 1.3 Blocked Research
**File:** `Scripts/Models/FactionType.cs` — `BlockedResearch()` method

Add a case returning `List<ResearchType>` of ~8 Tier III research types blocked for this faction. Every faction should block `CavalryMeleeAttackIII`, `CavalryMeleeArmorIII`, `CavalryPierceArmorIII` (shared cavalry block). Then pick 5 more T3 types that conflict with the faction's theme.

### 1.4 Research Restriction Description
**File:** `Scripts/Models/FactionType.cs` — `ResearchRestrictionDescription()` method

Add a case returning a human-readable string like: "Blocked from Tier III of: Infantry, Cavalry, Wood, Food research."

---

## Phase 2: Exclusive Research

### 2.1 Research Enum
**File:** `Scripts/Models/ResearchType.cs`

Add new enum values under the `// Faction-Specific Research` comment. Faction research does NOT follow the I/II/III suffix convention — use descriptive names instead (e.g., `BurnAreas`, `ToxicSpores`, `LethalSpores`).

### 2.2 Research Extension Methods
**File:** `Scripts/Models/ResearchTypeData.cs`

Add entries for the new research in ALL of these switch statements / methods:

| Method | What to add |
|--------|-------------|
| `DisplayName()` | Human-readable name |
| `Description()` | Tooltip text describing the effect |
| `ResearchTime()` | Custom time (add `if` check before the tier-based switch) |
| `Branch()` | Which `ResearchBranch` it belongs to |
| `BuildingRequirement()` | `(BuildingType, level)` tuple (add `if` check before tier-based logic) |
| `Prerequisites()` | `ResearchType[]` — empty for chain entry points, predecessor for chain links |
| `ResearchCost()` | `Dictionary<ResourceType, int>` |
| `ExclusiveFaction()` | Return the new `FactionType` |

**Important:** `Tier()` reads the name suffix (III→3, II→2, else→1). Faction research with custom names returns Tier 1, which means `CityCenterLevelRequirement()` returns 1. This is fine — building requirements handle gating instead.

### 2.3 Research Validation
**File:** `Scripts/Commands/ResearchCommand.cs`

The existing validation already checks `BlockedResearch()` and `ExclusiveFaction()` generically — no changes needed unless the new faction has special validation logic.

**File:** `Scripts/AI/Commands/AIStartResearchCommand.cs`

Same — existing validation handles new factions automatically.

---

## Phase 3: Exclusive Building (if applicable)

### 3.1 Building Enum
**File:** `Scripts/Models/BuildingType.cs`

Add the new enum value. Then add entries to ALL extension methods:
- `DisplayName()`, `IconKey()`, `Category()`, `MaxLevel()`, `BuildCost()`, `BuildTime()`, `Description()`, `HexSize()`, `RequiredCityCenterLevel()`

### 3.2 Building Health
**File:** `Scripts/Data/BuildingData.cs` — `GetBaseHealth()` method

Add a case before the `default:` returning a constant from GameConfig:
```csharp
case BuildingType.NewBuilding: return GameConfig.BuildingHealth.NewBuilding;
```

**File:** `Scripts/Engine/GameConfig.cs` — `BuildingHealth` class

Add the health constant.

### 3.3 Faction Gating
**File:** `Scripts/Models/FactionType.cs` — `ExclusiveFaction()` (the `BuildingType` overload)

Add a case mapping the new building to the new faction.

### 3.4 Visual Label
**File:** `Scripts/Visual/EntityRenderer.cs` — `GetBuildingLabel()` method

Add a case returning a 1-2 character label (e.g., `"Fm"` for False Morel).

---

## Phase 4: Engine Mechanics

### 4.1 Game Constants
**File:** `Scripts/Engine/GameConfig.cs`

Add a new nested static class with all balance constants for the faction's unique mechanics:
```csharp
public static class NewFactionName
{
    public const double SomeBonus = 1.5;
    // ...
}
```

### 4.2 Engine Integration

Implement each passive bonus in the relevant engine. Read the existing faction implementations as patterns:

| Bonus Type | Engine File | Where to Hook |
|------------|------------|---------------|
| Vision range | `VisionEngine.cs` | Army/building vision calculation |
| Camouflage/stealth | `VisionEngine.cs` | `IsArmyCamouflaged()` or new method |
| Gathering rate | `ResourceEngine.cs` | Gather rate calculation |
| Movement speed | `MovementEngine.cs` | Movement speed multiplier |
| Build cost modifier | `BuildCommand.cs` | Cost calculation |
| Combat effects | `CombatEngine.cs` | Post-combat or on-damage hooks |
| On-death triggers | `CombatEngine.cs` | Building destruction in `ProcessArmyVsBuildingPhase()` |

### 4.3 Trap/Triggered Buildings

If the faction has a trap building (like False Morel), intercept in `CombatEngine.StartBuildingCombat()`:

```csharp
// After fetching the building:
if (building.buildingType == BuildingType.NewTrap)
{
    return HandleNewTrapAttack(attacker, building);
}
```

Create a private `HandleNewTrapAttack()` method that:
1. Instantly destroys the building (`building.TakeDamage(building.health)`)
2. Applies the on-death effect (poison via `ApplyPoisonToArmy()`, damage, debuff, etc.)
3. Cleans up attacker combat state (`isInCombat = false`, `combatTargetID = null`)
4. Removes the building (`gameState.RemoveBuilding(buildingID)`)
5. Emits state changes via `StateChangeBatch` + `GameEngine.Instance?.EmitStateChanges(batch)`
6. Returns a `CombatStartedChange` for notification

The `AutoStartBuildingCombat()` method already calls `StartBuildingCombat()`, so the interception handles both direct and auto-attack paths.

### 4.4 Research Bonus Scaling

If exclusive research modifies a mechanic, check for completed research in the engine:
```csharp
var owner = gameState.GetPlayer(ownerID);
if (owner != null && owner.HasCompletedResearch(ResearchType.NewResearch.ToString()))
{
    value *= GameConfig.NewFaction.ResearchMultiplier;
}
```

---

## Phase 5: Visual Deception (if applicable)

### 5.1 Entity Disguise
**File:** `Scripts/Visual/EntityRenderer.cs` — `ComputeDesiredStates()` method

If the faction has buildings that should appear as a different entity type to enemies:

In the building `foreach` loop, after the fog filter:

1. **Visibility filter** — Enemy disguise buildings should only show on `Visible` tiles (like armies), not `Explored`:
```csharp
if (building.buildingType == BuildingType.DisguiseBuilding &&
    isEnemy && level != VisibilityLevel.Visible) continue;
```

2. **Disguise rendering** — When no friendly unit is adjacent (distance <= 1), render as `EntityVisualType.Army` instead of `Building`:
```csharp
if (building.buildingType == BuildingType.DisguiseBuilding && isEnemy && building.IsOperational)
{
    bool isRevealed = /* check proximity of friendly armies and villagers */;
    if (!isRevealed)
    {
        entitiesPerTile[coord].Add(new EntityPlacement
        {
            id = building.id,
            type = EntityVisualType.Army, // Disguise!
            color = color,
            label = null
        });
        continue; // Skip normal building rendering
    }
}
```

---

## Phase 6: AI Faction-Aware Strategy

### 6.1 Research Scoring
**File:** `Scripts/AI/AIResearchPlanner.cs` — `ScoreResearch()` method

Add an `else if (faction == FactionType.NewFaction)` block before `return score;`:

- Boost exclusive research chain: +12 to +20
- Boost synergistic standard research: +8 to +12
- Penalize T3-blocked category research: -5

**Scoring magnitudes reference:**
- Counter enemy composition: +20 (tactical, should override faction preference)
- Diversity penalty: -15
- State-based (Peace/Attack): +25 to +30
- Faction nudge: +6 to +8 (moderate, doesn't override tactics)
- Faction research synergy: +8 to +20

### 6.2 Unit Training
**File:** `Scripts/AI/AIMilitaryPlanner.cs` — `ScoreUnitTraining()` method

Add faction unit preferences before `return score;`:
- Boost favored unit categories: +6
- Penalize T3-blocked categories: -4 to -5
- Keep Scout at neutral (all factions need scouts)

### 6.3 Target Scoring
**File:** `Scripts/AI/AIMilitaryPlanner.cs` — `ScoreAllTargets()` method

If the faction favors attacking armies (e.g., poison attrition), boost army target scores. If it favors building destruction, boost building scores.

### 6.4 Resource Priorities
**File:** `Scripts/AI/AIEconomyPlanner.cs` — `AnalyzeResourceNeeds()` method

After the urgency loop, multiply favored resource urgencies by 1.10:
```csharp
else if (faction == FactionType.NewFaction)
{
    urgency[ResourceType.FavoredResource] = Math.Min(2.0, urgency[ResourceType.FavoredResource] * 1.10);
}
```

### 6.5 Military Building Priority
**File:** `Scripts/AI/AIEconomyPlanner.cs` — `TryBuildMilitaryBuilding()` method

Add a faction-specific priority array that reorders Barracks/ArcheryRange/Stable/SiegeWorkshop based on the faction's favored unit types.

### 6.6 Building Placement
**File:** `Scripts/AI/AIEconomyPlanner.cs` — `FindFactionPreferredBuildLocation()` method

Add terrain preference logic for the new faction (e.g., prefer desert tiles, coastal tiles, etc.).

### 6.7 Defensive Placement
**File:** `Scripts/AI/AIDefensePlanner.cs` — `FindDefenseBuildLocation()` method

Add terrain scoring for the new faction in the scored placement approach.

### 6.8 Entrenchment
**File:** `Scripts/AI/AIDefensePlanner.cs` — `GenerateEntrenchmentCommands()` method

Add terrain preference scoring in the candidate ordering lambda.

### 6.9 Simulation AI
**File:** `Scripts/AI/SimulationAIController.cs` — `SelectBestResearch()` method

The existing `BlockedResearch()` and `ExclusiveFaction()` filters handle new factions automatically.

---

## Phase 7: UI Integration

### 7.1 Game Setup
**File:** `Scripts/Visual/GameSetupPanel.cs` — `BuildFactionSection()` method

Add a new button for the faction in both the player and AI faction toggle sections. The faction info card updates automatically via `DisplayName()`, `StartingBonusDescription()`, and `Description()`.

### 7.2 Research Tree
**File:** `Scripts/Visual/ResearchTreePanel.cs`

The existing `GetNodeState()` already checks `BlockedResearch()` and `ExclusiveFaction()` generically. The `LockedFaction` node state and "Faction Blocked" label handle new factions automatically. No changes needed unless the new faction has special research tree behavior.

---

## Phase 8: Documentation

### 8.1 CLAUDE.md
Add under `### Faction System`:
- Faction table with bonuses, effects, and engine file references
- Unique research chain description
- Unique building system section (if applicable) with constants and engine hooks
- Add new files to `## Key File Locations` if any new files were created

### 8.2 README.md
Add under `### Factions`:
- Faction description and theme
- Bonuses table
- Exclusive building description
- Exclusive research table with effects and requirements
- Tier III research restrictions

---

## Checklist

Use this to verify nothing was missed:

- [ ] `FactionType.cs` — enum + all extension methods + blocked research + restriction description
- [ ] `ResearchType.cs` — new enum values
- [ ] `ResearchTypeData.cs` — ALL 8 extension methods for each new research
- [ ] `BuildingType.cs` — enum + all extension methods (if exclusive building)
- [ ] `BuildingData.cs` — `GetBaseHealth()` case (if exclusive building)
- [ ] `GameConfig.cs` — new constants class + building health constant
- [ ] Engine file(s) — bonus logic implemented
- [ ] `CombatEngine.cs` — trap building interception (if applicable)
- [ ] `EntityRenderer.cs` — building label + visual deception (if applicable)
- [ ] `ResearchCommand.cs` — validation works (usually automatic)
- [ ] `AIStartResearchCommand.cs` — validation works (usually automatic)
- [ ] `AIResearchPlanner.cs` — faction synergy scoring
- [ ] `AIMilitaryPlanner.cs` — unit training + target scoring
- [ ] `AIEconomyPlanner.cs` — resource urgency + military priority + build placement
- [ ] `AIDefensePlanner.cs` — defense placement + entrenchment
- [ ] `GameSetupPanel.cs` — faction toggle buttons
- [ ] `CLAUDE.md` — updated
- [ ] `README.md` — updated

## Key API Reference

```csharp
// Terrain access
gameState.mapData.GetTerrain(coord)          // → TerrainType? (Plains, Water, Mountain, Desert, Hill)
gameState.GetResourcePoint(coord)             // → ResourcePointData (check .resourceType for Trees, etc.)
gameState.CanBuildAt(coord, playerID)          // → bool
gameState.FindBuildLocation(center, radius, playerID) // → HexCoordinate?

// Player/faction checks
player.faction                                // → FactionType
player.HasCompletedResearch("ResearchName")   // → bool
player.GetResource(ResourceType.Wood)         // → double
player.HasResource(ResourceType.Wood, amount) // → bool

// Combat engine patterns
ApplyPoisonToArmy(army, dps, sourcePlayerID, canStack, changes)  // existing method
building.TakeDamage(amount)                   // sets health, may set state to Destroyed
gameState.RemoveBuilding(buildingID)          // cleanup
var batch = new StateChangeBatch(changes, Guid.Empty);
GameEngine.Instance?.EmitStateChanges(batch); // emit state changes from combat engine
```
