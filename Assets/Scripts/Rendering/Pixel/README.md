# Pixel Art Rendering Pipeline

This folder contains the runtime side of the modular pixel-art fish renderer.
Editor-side procedural generators live under `Assets/Editor/Pixel/`.

## One-time setup

Run from the Unity menu, in order:

1. **Tools / Aquarium / Pixel Art / 0. Build Everything (Pixel Art)** — single
   click that runs every step below and configures the active scene.

Or run them individually:

1. **1. Bootstrap Pixel Art Foundation** — creates `PixelArtSettings.asset`,
   `FishPartLibrary.asset`, `M_FishPalette.mat`, the named sorting layers, and
   the `PixelPerfectCamera` on the active main camera.
2. **2. Generate Fish Part Sprites** — procedurally rasterises every body /
   tail / fin / eye / mouth variant (with animation frames) at the resolution
   in `PixelArtSettings.pixelsPerUnit` and registers them in the library.
3. **3. Convert PrototypeFish Prefab** — strips the legacy mesh-based body
   from `Assets/Prefabs/Fish/PrototypeFish.prefab` and wires
   `FishCompositor` + `PixelFishAnimator` instead.
4. **4. Setup Pixel Aquarium Scene** — builds water, gravel, plants, glass,
   and bubbles using pixel-art sprites.

## Architecture

```
FishData (genome)
   │
   ▼
FishRenderer.ApplyGenome(fish)            ← public entry point used by spawners
   │
   ▼
FishCompositor.Apply(fish, geneLibrary)   ← picks parts, lays them out, applies palette
   │
   ├── PixelArtPalette.FromGenome(...)    ← gene → colours / pattern (single source of truth)
   │   (pushed via MaterialPropertyBlock on every active SpriteRenderer)
   │
   └── one SpriteRenderer per PixelPartType slot
       ├── Body, Tail, DorsalFin, PectoralFin, Eye, Mouth (active)
       └── ScalesOverlay, Accessory                       (reserved)

PixelFishAnimator
   │
   ▼ (each Update)
FishCompositor.SetSlotFrame(slot, frameIndex)  ← swaps per-part sprite, keeps body anchors aligned
```

## Adding new content

### A new body / tail / fin variant

1. Bump the relevant variant count in `PixelArtSettings.asset` (e.g.
   `bodyShapeCount = 9`).
2. Extend the matching parametric silhouette in `PixelFishSilhouettes.cs`
   (`BodyHalfHeight`, `TailContainsPx`, `FinContainsPx`, etc.) with a new
   `case` branch.
3. Bump the matching `GeneDefinition.discreteStates` (e.g. `body_shape` → 9).
4. Re-run **Tools / Aquarium / Pixel Art / 2. Generate Fish Part Sprites**.

The library's `Get(type, variant)` falls back to variant 0 if a variant is
missing, so you can ship gradually.

### A new part type (e.g. whiskers)

1. Add a value to `PixelPartType` in `PixelPartType.cs`. Keep `Count` last.
2. Add a slot sorting order in `FishCompositor.slotSortingOrders`.
3. Add a generator entry in `PixelFishGenerator` that produces `FishPart`
   SOs with `partType = PixelPartType.Whiskers`.
4. Wire the new gene → variant mapping in `FishCompositor.Apply(...)`.

### A new gene that should affect visuals

1. Add the gene to `GeneDefinitionsGenerator.Phase24Genes` and re-run
   **Tools / Aquarium / Generate All Gene Definitions**.
2. Read the phenotype in either:
   - `PixelArtPalette.FromGenome(...)` if it's a palette / shader value, or
   - `FishCompositor.Apply(...)` if it picks a part variant / tier.

The genome layer (`FishGenome`, `GeneLibrary`, `GeneDefinition`) is
deliberately untouched by this pipeline — adding new genes is decoupled from
the visual code.

### Bumping resolution

Change `PixelArtSettings.pixelsPerUnit` (and optionally `bodyBoundsPixels`),
re-run the foundation + generator menus. Every part regenerates at the new
resolution; no code changes required. The pixel-perfect camera resolution
auto-updates on the next foundation bootstrap.

## Sprite encoding

Generated body sprites pack four channels:

| channel | meaning                                                     |
|---------|-------------------------------------------------------------|
| R       | brightness tier (0 = darkest, 1 = highlight)                |
| G       | pattern mask (0 normally; can be hand-painted in authored sprites) |
| B       | body interior (0 = silhouette outline pixel, 1 = interior)  |
| A       | silhouette alpha                                            |

`FishPalette.shader` decodes these channels and applies the per-fish palette
+ procedural pattern. Hand-authored sprites can follow the same convention
to drop into the same shader without code changes.

## Tags

`FishPart.tags[]` is a free-form string list used for content filtering
(e.g. "rare", "fancy", "glow"). Selection logic for tagged drops can be
added by inspecting `library.Parts` and filtering by tag — none is enforced
yet.
