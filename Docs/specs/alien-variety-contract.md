# Alien landing variety: implementation contract

The alien landing (`Docs/specs/aliens-and-sound-contract.md`, part A) is always the same: 4 aliens, from the same arc at the rocket, the same tree, the same glowing disc, and a counting answer that is always 4. This contract makes every visit different. A visit still lasts about **3–5 minutes**, with the same order: land → zap → rescue → count → party.

Decided with the user on 2026-10-12:
- **Adaptive number of aliens, 2–8.** It uses the levels and a parent slider, like the pirates.
- **Aliens spawn all over the field.**
- **Four alien kinds:** big wobbly, tiny runner, shy peeker and robot.
- **Real trampolines**, which are always bouncy, also in free play.
- **Several trees.**
- **Four rescue kinds:** tree, rocket top, floating bubble and slime puddle.

Read these first:
- `Docs/specs/aliens-and-sound-contract.md`: the current alien landing.
- `Docs/specs/knights-slice-contract.md`: activities, the battle system, builders, and by-name asset lookup.
- `Docs/HANDOVER.md`: layout, tools and gotchas.
- The current code: `Scripts/Aliens/*.cs` and `Editor/Aliens/AlienFieldBuilder.cs`.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print `compile-check: OK` before you report back.

**Player:** a 4-year-old who can't read. Everything is spoken, and he can never fail. Every new step has a hint and a safety net. Controls stay one joystick plus the one action button. Every activity's `OnEnd` must survive being called at any moment (the back button), and must put everything back.

**Shared checkout:** other sessions are editing this working tree at the same time (pirates, the soapbox race and more). For any file outside `Scripts/Aliens` and `Editor/Aliens`:
- Re-read the file right before every edit.
- Make small, local Edit-tool changes only.
- Never rewrite a whole shared file.

---

## 1. Layout of the field

The camera always looks **NORTH**, so anything the kid must see goes north of where he usually stands. Builder coordinates are `(along, across)`:
- **along** is metres from the playground edge going west into the field (0–24).
- **across** is metres to the north (positive) or south (negative). The field runs from about −17 to +17.

The positions below are a **starting point**. The builder agent may move them to avoid overlaps, but the rules after the table always win. Log the final positions in the build log, as the builder does now.

| Thing | along, across | Notes |
|---|---|---|
| Activity zone | 4.5, 0 | unchanged |
| Rocket landing | 16, 14.5 | unchanged |
| Party line (10 spots) | about 3 → 14, across 5 | moved south of the trees, so no tree is between the dancers and the count camera |
| Tree 1 (`SM_Env_Tree_Large_01`, the current one) | 8, 13 | |
| Tree 2 (`SM_Generic_Tree_01..04` or Kids `SM_Env_Tree_01`, picked by name) | 21, 6 | |
| Tree 3 (another model from that list) | 2.5, 15 | |
| Tree 4 (another model from that list) | 22, 15 | |
| Open-field trampoline (bubble rescue) | 13, 9.5 | |
| Rocket trampoline | south side of the rocket, about 2.5 m from its centre | only shown during the rocket rescue |
| Slime puddle spots (3) | e.g. 6, 9 · 18, 2 · 12, 16 | |
| Cover objects (4 rocks or bushes) | spread over the field, at across ≥ 0 | for the shy aliens |
| Alien spawn spots (7–8) | rocket door, the north side of each tree, behind covers | |

Rules:
- Every trunk, trampoline, cover and puddle is at least 2.5 m from the party line and from the other things. The party line, with up to 9 dancers, has a clear view south to the count camera.
- Each tree's perch is on the **south**, camera-facing side of its crown, as now. Its trampoline sits directly below the perch, 0.5 m further south.
- Spawn spots are spread out: at least two in the east half of the field and at least two in the west half.
- The existing greenery scatter in `PlaygroundPrototypeBuilder` (around line 842) already keeps the alien field clear. Don't change that.

---

## 2. Alien count and level

- **Skill key `"aliens"`**, through `ProgressStore.Current.GetLevel/SetLevel` (no `PlayerProgress` change is needed). `MaxLevel = 3`.
- **Count per visit:**
  - Level 1: 2–4
  - Level 2: 3–6
  - Level 3: 5–8
  - Random within the range, but **never the same as the previous visit**. Keep the last count in a static field.
- **Adapt after the count question:**
  - Right card first time: level +1.
  - Wrong card: level −1.
  - Clamp to 1..3, the same idea as `PirateAdventure.AdaptLevel`.
- **Parent area:**
  - Add a row `"aliens"` (max 3) to `ParentAreaBuilder`.
  - Add `ParentStrings.RowAliens = "row.aliens"` with `{ "Aliens", "Aliens" }`.
  - There are already 5 rows, and the window (1051 px tall on a 1080 canvas) cannot grow. Re-space the rows so six fit above the close button (whose top is at y = 931), for example a 96 px step starting at 330. They must not overlap.

---

## 3. Alien kinds

`public enum AlienKind { Normal, Big, Tiny, Shy, Robot }`, in `CoolSchool.Aliens`.

**Pool (built by the builder):**
- 8 alien kids, alternating `SM_Chr_Kid_Alien_01` and `SM_Chr_Kid_Alien_02`.
- 2 robots: `SM_Chr_Kid_Robot_01` and `SM_Chr_Kid_Cardboard_Robot_01`. Both are Kids-rig characters with the same components.
- The robot GameObjects get a serialized `isRobot = true`.
- The kind of a non-robot alien is chosen **at run time**, so any alien kid can be Normal, Big, Tiny or Shy.

**Mix per visit:**
- Number of specials:

  | Level | Specials | Allowed kinds |
  |---|---|---|
  | 1 | `1` | Tiny, Shy or Robot (no Big) |
  | 2 | `Random 1..2` | any |
  | 3 | `Random 1..3` | any |

- The rest are Normal.
- At most 1 Big and at most 2 Robots per visit.
- Shuffle the spawn order.

| Kind | Look | Behaviour | Zaps | Throws slime |
|---|---|---|---|---|
| Normal | as now | as now: walks toward the player and keeps its distance | 1 | yes |
| Big | scale 1.45, slight purple tint | walks slower (1.0 m/s). Each zap that isn't the last: a squash wobble, a giggle (`aliens.big_hit`) and it shrinks one step (1.45 → 1.3 → 1.15). On becoming a friend it is back to scale 1.0. | 3 | yes |
| Tiny | scale 0.7, slight yellow tint | When the player is within 6 m it runs (3.6 m/s, `aliens.tiny_tease` sometimes) to a NavMesh point about 10 m away, inside the field bounds. Then it **rests for 3.5 s**: it stands still, puffs sparkles and says `aliens.tiny_tired` once. Zappable at any time within aim range; resting is the easy moment. | 1 | no |
| Shy | normal size, slight blue tint | Walks straight to a free `AlienCover` and hides behind it, at the cover's north side, out of sight. **While hidden it cannot be zapped.** Every 3–5 s it steps out to the peek point for 2 s (`aliens.shy_peek`, sparkle), then steps back. When the player is within 3 m of its cover, it peeks and **stays out**. Safety net: after 3 peeks it comes out for good and acts like a Normal. With no free cover, it acts like a Normal. | 1 | no |
| Robot | robot model | like a Normal. Its zap line is `aliens.robot_zapped`, and its party dance is always variant 2 (Robo Taunt). | 1 | yes |

- **Tints:** use a `MaterialPropertyBlock` on the renderers (`_BaseColor`, multiplied gently). **Reset** the tint and scale in `ResetAlien`.
- **After the zap:** every kind becomes a friend the same way as now: confetti, a cheer, a run to its party spot, and a dance. The counting treats all kinds the same.
- **Kind hints:** the first time a Big, Tiny or Shy alien appears in a visit, the teacher says its hint once: `aliens.big_hint`, `aliens.tiny_hint` or `aliens.shy_hint`.
- **The wave safety net stays.** After 150 s, every alien left becomes a friend, including hidden shy aliens.

### Ray-gun targets
- Add `public interface IZapTarget { bool CanBeZapped { get; } Hittable Hittable { get; } }`.
- `AlienKidAI` implements it: `CanBeZapped` = not a friend and not hidden.
- `RayGunShooter.FindTarget` changes from "`AlienKidAI` that isn't a friend" to "**any `IZapTarget` on an Opponents `Hittable` with `CanBeZapped`**", with the same nearest-within-range rule. That is how slime blobs (section 5) become targets.

---

## 4. Spawning from all over the field

- Each visit picks **2–3 spawn spots** at random.
  - Prefer spots at least 6 m from the player and at `across ≥ player across − 2`, so they are in view.
  - When too few spots qualify, fall back to any spots.
- Aliens are dealt round-robin over the chosen spots, one every 1.2 s, as now.
- Each appearance gets a sparkle pop and `Sfx.Play("aliens.spawn")`.
- On the first spawn, the teacher says `aliens.arrive` ("they come from everywhere"), right after `aliens.start`.
- Shy aliens head for their cover straight away.

---

## 5. Rescues

Each visit picks one rescue at random:
- **Tree** has weight 2. **Rocket**, **Bubble** and **Slime** have weight 1 each.
- Never the same rescue kind twice in a row. For Tree, never the same tree twice in a row. Keep both in static fields.
- When a rescue's references are missing, fall back to Tree.

The baby is one of **two baby models** (`SM_Chr_Baby_Alien_01` / `_02`), picked at random each visit. The builder creates both, each with the current `RescueTarget` setup.

Common to every rescue:
- A sparkle trail from the player to the target (the active trampoline, or the slime puddle).
- The rescue voice line again every 25 s.
- A 120 s safety net, after which the baby frees itself.
- On success: `aliens.rescued`, confetti and `onRescue`.
- Then the existing baby-waits-for-count flow continues unchanged.

| Rescue | Setup | How he rescues | Voice |
|---|---|---|---|
| **Tree** | The baby sits on a random tree's perch. That tree's trampoline glows. | As now: bounce up, the baby jumps into his arms, and they float down. | `aliens.rescue` (existing) |
| **Rocket** | The baby sits on the rocket's nose (a perch point that is a child of the rocket, at the top of its bounds, slightly south). The rocket trampoline pops in with a sparkle and glows. | Same as Tree. This trampoline's launch speed is higher, so the jump reaches the nose: compute it from the perch height and the player's gravity, about 1 m above the nose. | `aliens.rescue_rocket` |
| **Bubble** | The baby sits inside a see-through floating bubble about 4.5 m above the open-field trampoline. The bubble bobs ±0.5 m and circles slowly (1 m radius). That trampoline glows. | Bounce up. In the air, within 1.8 m of the bubble, it **pops** (`Sfx aliens.bubble_pop`, confetti) and the baby grabs on. Then they float down as for Tree. Safety net after 90 s: the bubble sinks to the ground and pops by itself. | `aliens.rescue_bubble` |
| **Slime** | The baby stands stuck in a green puddle at one of the slime spots, with **3, 4 or 5 slime blobs** around it (level 1/2/3). The baby wiggles and waves. | Zap the blobs: each one is an `IZapTarget` that pops on one zap (green confetti, `Sfx aliens.slime_pop`). When the last one pops, the puddle shrinks away, the baby hops free (`aliens.slime_free`) and runs to the player. No trampoline. Safety net after 60 s: the remaining blobs pop one by one by themselves. | `aliens.rescue_slime` |

---

## 6. Trampolines

- They replace the glowing disc with the Synty model **`SM_Prop_Trampoline_01`** (PolygonTown), found by name.
- They are scaled by bounds to about 2.4 m across, and the frame stays low.
- **Always bouncy:** every tree trampoline and the open-field trampoline launch the player whenever he runs onto them, also in free play. The conditions are:
  - the player is grounded,
  - `player.InputEnabled` is true,
  - `!ParentGate.InputBlocked`,
  - and the trampoline is not in its cooldown.

  The rocket trampoline is only there during the rocket rescue.
- **Detection:** the existing flat-radius check, with the radius at about the trampoline's rim, so the bounce starts as soon as he touches the edge.
- **No blocking collider:** strip the prop's colliders, so the CharacterController can never get stuck on the frame.
- **NavMesh:** aliens should walk around the trampolines. Mark each footprint not-walkable for the bake (check how the bake collects geometry: `NavMeshModifier` with the area overridden, or an obstacle that is only there during the bake). The player is not a NavMesh agent, so he is unaffected.
- **Squash:** on each launch the model squashes (scale y 0.8 → 1.0 over about 0.25 s).
- **Highlight:** `JumpPad` keeps its glow visual (the ring, the pulse and the light), but it only shows that on the **active** rescue trampoline.
  - `SetLit(bool)` now means "highlight". The model is always visible.
  - Add a serialized `bool alwaysBouncy`: launch when `alwaysBouncy || IsLit`.
  - The launch speed stays serialized per trampoline.

---

## 7. Party line and count camera

- The builder makes **10 party spots** in a row facing south: 8 aliens, the baby and a spare.
- At run time a visit with N aliens uses a **centred, contiguous slice** of N spots for the aliens and the next spot for the baby. That way the group is always around the middle of the line.
- **The count camera is framed at run time,** before `EnterCountShot`. `AlienLanding` moves `countCameraPoint` so that:
  - it is centred on the N alien spots,
  - it is pulled back far enough that the outer two plus a 1 m margin fit in the horizontal field of view (camera FOV 50° vertical, landscape 16:9),
  - and the height and pitch stay as now (2.2 m, 21°), with the dancers in the upper part of the screen above the number cards.
- `babyWaitPoint` must stay out of shot: move it to 3 m behind the framed camera.
- `MakeCards` already allows answers 1–9. Keep it.

---

## 8. Components and wiring

All in namespace `CoolSchool.Aliens`.

- **`AlienKidAI` (changed):**
  - Serialized `bool isRobot`.
  - `public AlienKind Kind`.
  - Implements `IZapTarget`.
  - `Activate(spawn, player, spot, lookAt, dance, AlienKind kind, AlienCover cover, Bounds field)`. The old signature can go: `AlienLanding` is the only caller.
  - `ResetAlien` also resets the kind, scale, tint, cover claim and `Hittable.hitsToDefeat`. Add a setter on `Hittable` if one is missing, like `SetHitsToDefeat` used by the pirates.
- **`AlienCover` (new):** a cover prop.
  - `Transform hidePoint` (north of the prop, hidden) and `Transform peekPoint` (beside it, visible).
  - `bool IsTaken` with `Claim` and `Release`.
- **`AlienRescueSite` (new):**
  - `RescueKind kind` (`Tree`, `Rocket`, `Bubble`, `Slime`), `Transform perch`, `JumpPad pad` (may be null), `FloatingBubble bubble` (Bubble only), `SlimeTrap slime` (Slime only).
  - `void ResetSite()`.
- **`FloatingBubble` (new):**
  - `Show(Vector3 centre)`, `Transform Seat`, `bool TryPop(Transform player)`, `void Pop()`, `void ResetBubble()`.
  - Visual: a URP transparent sphere, about 1.6 m across, faint iridescent tint. Material made by the builder.
- **`SlimeTrap` (new):**
  - The puddle visual, `Transform babySpot`, and a pool of 5 `SlimeBlob`.
  - `Show(int blobs)`, `bool IsCleared`, `IEnumerator PopRemaining()` (the safety net), `ResetTrap()`.
- **`SlimeBlob` (new):**
  - A green sphere with a `Hittable` (Opponents team, 1 hit), implementing `IZapTarget`. It pops on Defeated.
  - Only active during the slime rescue.
- **`RescueTarget` (small change):**
  - `ShowAt(Transform perch)` keeps working for the tree, rocket and bubble seats.
  - The baby must follow a moving seat: parent it to the seat while it is stuck, and unparent it on grab or reset.
  - Add a stuck-wiggle for the slime rescue.
- **`AlienLanding` (changed):**
  - The new serialized fields replace the old single ones:
    - `AlienKidAI[] aliens`: the pool of 10.
    - `RescueTarget[] babies` (2): replaces `babyAlien`.
    - `AlienRescueSite[] rescueSites`: replaces `treePerchPoint` and `jumpPad`.
    - `AlienCover[] covers`.
    - `Transform[] alienSpawnPoints`: 7–8, spread out.
    - `Transform[] partyLinePoints` (10).
    - `Bounds fieldBounds`.
  - Everything else stays as it is: the rocket, the camera, the lessonUI, the events and the timing.
  - `OnEnd` and `ResetEverything` must reset every rescue site, every baby and every cover, put the trampoline highlights out, and hide the rocket trampoline.
- **`RayGunShooter`:** the `FindTarget` change from section 3. Nothing else.

**Builder (`Editor/Aliens/AlienFieldBuilder.cs`):**
- **Step 1, `BuildField`**, before the NavMesh bake: the trees, perches, trampolines (incl. the rocket one), covers, slime spots, spawn points, party spots and field bounds.
- **Step 2, `Build`**, after the bake: the alien pool, the robots, both babies, the bubble, the slime traps and blobs, the materials, and the wiring.
- `AlienCount = 4` goes. Size the pool from the level table (max 8 aliens plus 2 robots).
- The count shot (`WireCountShot`) only places the default camera point; the run time re-frames it.
- If the `PlaygroundPrototypeBuilder` call needs any change, keep it to the existing two lines.

---

## 9. Voice lines (added to `VoiceLineSeeder`) and sound cues

Add these with the existing `Seed(...)` pattern. Speakers already exist.

| key | speaker | Dutch | English |
|---|---|---|---|
| `aliens.arrive.1` | Teacher | Kijk goed, ze komen overal vandaan! | Look closely, they're coming from everywhere! |
| `aliens.big_hint.1` | Teacher | Wat een grote alien! Flits hem een paar keer! | What a big alien! Zap it a few times! |
| `aliens.big_hit.1` | Alien | Hihi, ik krimp! | Hehe, I'm shrinking! |
| `aliens.big_hit.2` | Alien | Oei, ik word kleiner! | Oops, I'm getting smaller! |
| `aliens.tiny_hint.1` | Teacher | Die kleine is snel! Als hij moe is, kun je hem flitsen! | That little one is fast! When he's tired, you can zap him! |
| `aliens.tiny_tease.1` | Alien | Hihi, vang me dan! | Hehe, catch me! |
| `aliens.tiny_tired.1` | Alien | Pfoe, ik ben moe! | Phew, I'm tired! |
| `aliens.shy_hint.1` | Teacher | Daar verstopt er eentje! Loop ernaartoe! | One is hiding there! Walk over to it! |
| `aliens.shy_peek.1` | Alien | Kiekeboe! | Peekaboo! |
| `aliens.robot_zapped.1` | Alien | Bliep bliep! Robotdans! | Beep beep! Robot dance! |
| `aliens.rescue_rocket.1` | Teacher | Help! De baby-alien zit bovenop de raket! Spring op de trampoline! | Help! The baby alien is on top of the rocket! Jump on the trampoline! |
| `aliens.rescue_bubble.1` | Teacher | Kijk! De baby-alien zweeft in een bel! Spring omhoog en pak hem! | Look! The baby alien is floating in a bubble! Jump up and grab him! |
| `aliens.rescue_slime.1` | Teacher | Oh nee, de baby-alien zit vast in het slijm! Flits het slijm weg! | Oh no, the baby alien is stuck in the slime! Zap the slime away! |
| `aliens.slime_free.1` | Alien | Dankjewel! Ik ben weer vrij! | Thank you! I'm free again! |

That's about 650 Dutch characters of DeepVoice quota. The main agent generates them after the build.

New sound cues go into `SoundLibrarySeeder` with empty clip lists: `aliens.spawn`, `aliens.giggle`, `aliens.shrink`, `aliens.peek`, `aliens.robot_beep`, `aliens.bubble_pop`, `aliens.slime_pop`. The trampoline keeps `aliens.jump_pad`.

---

## 10. Done when
- Compile-check prints OK.
- The scene rebuild (by the user, through the existing builder menu) logs every new position, with no warnings about missing prefabs.
- Two visits in a row differ in count, spawn spots, mix of kinds and rescue.
- Each rescue kind can be forced for testing: a serialized `debugForceRescue` (the default is random) and a `debugForceCount` (0 = adaptive) on `AlienLanding`.
- The back button in the middle of each phase leaves the field as before the game. Trampolines stay visible and bouncy in free play.
- `Docs/HANDOVER.md` gets a short entry under Activities and under the known rough edges: untested, and the tuning candidates (trampoline scale and launch speeds, bubble height, shy cover points, count-camera framing for 8).

## Ownership (if split over two agents)
- **Agent R (run time):** `Scripts/Aliens/*`, `IZapTarget`, the `Hittable` setter (if needed), `RayGunShooter.FindTarget`.
- **Agent B (builder):**
  - `Editor/Aliens/*`.
  - The `ParentAreaBuilder` and `ParentStrings` row.
  - The `VoiceLineSeeder` lines and the `SoundLibrarySeeder` cues.
  - Agent B wires through the serialized field names above. Until R's types exist, compile-check fails on those types only; re-run it after a minute, for up to about 10 minutes. Never create the other agent's types.
