# Big walk-in cardboard castle: contract

Date: 2026-10-12. Replaces the flat cardboard castle wall in the knights arena with a large castle built from the
same PolygonKids cardboard castle pieces, scaled up 5×. The kid walks in through the gate and the whole knights
battle (goblin waves + dragon) happens in the courtyard.

User decisions (2026-10-12):
- **Scale 5×.**
- **Front wall turns see-through** (faint ghost) while the player is inside; fades back when he leaves.
- **The dragon flies in over the back wall**, lands in the courtyard, and flies back out over it at the end.

## The pieces (measured from the FBX files, metres at scale 1; ×5 in game)

All pivots at the bottom centre. "Front" means the piece's local +Z.

| Piece | What | Size at 1× (x × y × z) | At 5× |
|---|---|---|---|
| `SM_Prop_Cardboard_Castle_Piece_01/02/03` | square tower with battlements (three variants) | 0.57 × 1.09 × 0.57 | 2.85 × 5.47 × 2.85 |
| `SM_Prop_Cardboard_Castle_Piece_04` | straight wall panel, long along local **Z**, thin in X | 0.08 × 0.78 × 0.99 | 0.4 × 3.9 × 5.0 |
| `SM_Prop_Cardboard_Castle_Piece_05` | gate: wall with an arch opening, wide along local **X**, thin in Z | 1.27 × 0.86 × 0.05 | 6.35 × 4.3 × 0.26 |
| `SM_Prop_Cardboard_Castle_Piece_06` | flat arched door panel, wide along X, thin in Z | 0.43 × 0.62 × 0.03 | 2.1 × 3.1 × 0.15 |

The arch opening in piece 05 is about 0.63 m wide and 0.48 m (sides) to 0.66 m (apex) high at 1×, so about
3.15 m wide and 2.4–3.3 m high in game. Colliders are MeshColliders (`FinishEnvironmentObject`), so the arch stays
walkable. Always measure with `TryGetBounds` after scaling; never trust the numbers above blindly.

## World layout

The camera always looks north (+Z). The arena is east of the playground (`ArenaDirection = +X`).
`ArenaDepth` grows from 26 to **30** m, so the arena runs x ≈ 20.9 .. 50.9 and z ≈ -20.9 .. 20.9.

The castle is a rectangle in **world X/Z** (the castle code may stop pretending to support any arena axis; say so in a
comment):

```
              north fence (z ≈ 20.9)
   T ── panel ── panel ── panel ── T        back (north) wall; goblin door (piece 06) on its inner face, centred
   |                               |
 panel                           panel
   |          courtyard            |      side walls: 3 panels each (N-S)
 panel     (dragon lands here)   panel
   |                               |
 panel                           panel
   |                               |
   T ── panel ──  GATE  ── panel ── T        front (south) wall, facing the camera; gate (piece 05) centred
                   ▲
               approach
```

- **T** = corner towers. Use pieces 01, 02, 03, 01 (NW, NE, SW, SE or any order).
- **Front wall** (south side, -Z): corner tower, panel, gate, panel, corner tower. The gate faces -Z (toward
  the camera), so its yaw is 180° if its front is local +Z.
- **Back wall** (north side): panels filling the span between the two north towers.
- **Side walls**: 3 panels each between the corner towers.
- **Panels:** each wall span between two towers is filled with `n = max(1, round(span / panelLength))` panels, each
  stretched along its length axis (local Z) so the span is filled exactly (no gaps; a few cm of overlap is fine).
  E-W walls are yawed 90°.
- The front span sets the castle width: towers + 2 panels + gate. The side spans (3 panels) set the depth.
- **Placement:** the back wall's outer face is 1.5 m inside the north fence. The castle is centred in x in the
  arena, leaving more room on the west (playground) side than on the east, with at least 1 m behind it to the east
  fence. With the numbers above, the castle is about 22 × 20.7 m. It spans x ≈ 27 .. 49 and z ≈ -1.3 .. 19.4,
  with the gate at about (38, 0, -1.3).
- **Goblin door:** piece 06, standing on the ground against the inner face of the back wall at its centre, facing
  south into the courtyard. It is decoration only (no collider), so the NavMesh reaches right up to the wall.
- **Reserved ground:** reserve the castle footprint plus a 2 m margin, and the approach strip in front of the gate
  (gate width + 2 m wide, 6 m deep), in `PlaygroundPrototypeBuilder.ReservedAreas`. Do this **before** the benches,
  bins and greenery are placed. The castle build therefore moves earlier in `BuildEnvironment()`, before
  `PlaceBenchesAndBins`/`ScatterGreenery`. It is still before the NavMesh bake.

## Points and zone (builder, `KnightsArenaBuilder.Build`)

| Point | Where |
|---|---|
| `CastleZone` (ActivityZone, radius 2.0) | inside the courtyard, centred on the gate in x, 3 m north of the front wall's inner face |
| `OpponentSpawn_1..4` | in a row 1.8 m in front of the goblin door, across −1.5, −0.5, 0.5, 1.5 m |
| `RetreatPoint` | 0.8 m in front of the goblin door (goblins run "home" through the door and vanish) |
| `ArenaCenter` (dragon landing) | courtyard centre in x, 1 m north of the courtyard's z-centre |
| `DragonEntry` | above and behind the back wall: x = courtyard centre, z = back wall outer face + 8 m, y = 9 m |

The dragon's inactive root is placed at `DragonEntry`, as before. Snap the spawns to the NavMesh, as before.

## Imagination props

They stay hidden until the imagination effect plays.
- Flags on the two **north** corner towers, scaled so they are about 2.2 m tall.
- Torches on each side of the gate outside (1 m in front, 0.8 m beside the opening), and on each side of the goblin
  door inside. Scale them so they are about 2.0 m tall.
- They go in `layout.ImaginationProps`, as before.

## Front-wall cutaway

- **Builder.** Every renderer of the front-wall pieces (the two south towers, the two front panels and the gate)
  goes in `CastleCutaway.frontRenderers`. Those GameObjects get layer **Ignore Raycast**, so `FollowCamera`
  (whose `obstacleMask` already excludes Ignore Raycast) doesn't pull in against them. Keep their colliders, so they
  still block walking and are still in the NavMesh bake: `NavMeshSurface` collects all layers.
- **Ghost material.** The builder creates `Assets/CoolSchool/Materials/CastleGhost.mat`: a copy of the front pieces'
  (URP/Lit) material with the same `_BaseMap`, made alpha-blended with
  `BattleEffectsBuilder.SetAlphaBlended`, and `_BaseColor` alpha 1.
- **`insideArea`** is the courtyard's inner rectangle as a world `Bounds`: the inner faces of the four walls, any
  height.
- The `CastleCutaway` component lives on the castle group.

### `CoolSchool.World.CastleCutaway` (new, `Scripts/World/CastleCutaway.cs`)

```csharp
[SerializeField] Renderer[] frontRenderers;
[SerializeField] Material ghostMaterial;      // alpha-blended copy of the cardboard material
[SerializeField] Bounds insideArea;           // world space; only x/z are tested
[SerializeField] float ghostAlpha = 0.22f;
[SerializeField] float fadeSeconds = 0.35f;
public bool PlayerInside { get; }
```

Each frame it finds the `PlayerController`, caching it like `ActivityZone` does. The player is inside when their
x/z lie in `insideArea`.
- **Going in:** swap every material slot of each front renderer to one runtime instance of `ghostMaterial`. Then
  fade that instance's `_BaseColor` alpha from 1 down to `ghostAlpha` over `fadeSeconds`.
- **Going out:** fade the alpha back up to 1, then restore the original `sharedMaterials`, which were cached in
  `Awake`.
- It must survive toggling back and forth mid-fade.
- Shadows keep their default casting.
- It never touches colliders.
- Destroy the runtime material instance in `OnDestroy`.

## Dragon fly-in (runtime)

`DragonBoss` gets:

```csharp
/// Appears in the air at "from", flies down to land at the arena centre, then fights as usual.
public void FlyIn(Vector3 from, Vector3 arenaCenterPosition)
```

- **Setup.** It does the same as `Enter`, but keeps `from.y`. `groundY` is `arenaCenterPosition.y`.
  `awayDirection` is the flat direction from the centre back toward `from`, so at the end it flies away over the
  back wall again.
- **Flight.** It faces the flat direction to the centre, sets `Flying` true and plays `Sfx.Play("dragon.wing_flap")`.
  It moves toward the landing point at `FlyInSpeed` = 5 m/s (const). Its height eases down, so it touches down at
  the centre with smoothstep on the flat-distance fraction.
- **Landing.** It sets `Flying` false. It plays `BattleEffects.Confetti` at its feet and `Sfx.Play("dragon.roar")`,
  then waits 0.4 s. Then `Arrived`, then `FightRoutine` (the same as `EnterRoutine`'s ending).
- Ground snapping must not fight the flight: `EnterRoutine` forces `position.y = groundY`, and the flight must not.

`KnightsGame` gets `[SerializeField] bool dragonFliesIn = true;`. `RunDragon` calls `dragon.FlyIn(entry, center)`
when it is true, else `dragon.Enter(...)` as before. The fallback `entry` when `dragonEntry` is null stays
`center + forward * 10`.

When an opponent is activated in `RunWave`, add `BattleEffects.Sparkle(spawnPosition + Vector3.up)`, so goblins
"pop out of the door" visibly.

## Inviter trail follows the NavMesh

`ActivityInviter.ShowTrail` currently draws sparkles along a straight line, which would cross the castle wall. Instead:
1. Sample the player position and the zone position onto the NavMesh (`NavMesh.SamplePosition`, 2 m).
2. Call `NavMesh.CalculatePath`.
3. If it is `PathComplete` (or `PathPartial` with corners), place sparkles every `TrailSpacing` along the polyline
   of `path.corners`. Start after `TrailStartDistance`, stop `Radius` before the end, and keep the `MaxTrailPoints`
   cap.
4. Otherwise fall back to the current straight line.

Reuse one `NavMeshPath` instance. This helps every zone, not only the castle.

## Files and owners

- **Builder agent:** `Editor/KnightsArenaBuilder.cs`, `Editor/PlaygroundPrototypeBuilder.cs` (only `ArenaDepth`,
  the order of `BuildEnvironment`, and anything strictly needed to wire it).
- **Runtime agent:** `Scripts/World/CastleCutaway.cs` (new), `Scripts/Battle/DragonBoss.cs`,
  `Scripts/Battle/KnightsGame.cs`, `Scripts/Activities/ActivityInviter.cs`.
