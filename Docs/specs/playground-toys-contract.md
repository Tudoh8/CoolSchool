# Playground Toys: implementation contract

Date: 2026-10-13. Today the 12 Synty play toys on the playground ring are static scenery. In free play, when the kid
**touches** a toy, he plays on it for about 5 to 10 seconds: he slides, swings, spins, crawls, climbs or hangs. The
first time he plays on each toy he earns a **sticker** for a sticker board on the playground.

Two agents build it in parallel:

- **Agent C (core):** the runtime framework, the animator poses, the stickers and the sticker board, the builder core,
  the shared small edits, and four toys: **slide, both rockers, roundabout, tunnel**.
- **Agent D (more toys):** **monkey bars, seesaw, swings, fort, tyre wall, track ride, ship**.

Read first: `Docs/HANDOVER.md` (architecture, tools, gotchas), `Docs/specs/toy-room-and-equipment-contract.md` (the
newest reward and silhouette patterns), `Docs/specs/audio-packs-contract.md` (sound cues).

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it fails only on a type the other agent owns, wait about a minute and
retry, for up to about 10 minutes. Never create the other agent's types. The Unity editor is **open**, so do **not** run
`Tools/unity-batch.ps1` (no batch builds or preview renders). The main agent runs the build and the preview shots
afterwards with the editor closed, and sends fixes back.

**Another session** may be building the castle adventure (`Docs/specs/castle-adventure-contract.md`) at the same time.
It also edits `PlayerProgress`, `VoiceLineSeeder`, `SoundCues`, `SoundAutoAssign`, `PreviewCapture.cs` and
`PlaygroundPrototypeBuilder`. **Re-read each shared file right before every edit**, keep the edits small and local, and
never reformat, reorder or "tidy" code you don't own.

**Player:** a 4-year-old who can't read. Everything is spoken; nothing can go wrong; no text on screen.

## User decisions (2026-10-13)

- **Touch to start:** walking into a toy starts its routine (free play only).
- **The joystick skips:** pushing the joystick ends the routine early and he hops off.
- **One sticker per toy**, the first time he plays on it, collected on a sticker board.

## Behaviour

**Start.** Each toy has a touch zone: a trigger `BoxCollider` around the toy's renderer bounds, grown by 0.25 m on each
horizontal side, from the ground to 2.2 m (or the bounds top if higher). The routine starts when the player enters it and
all of these hold:
- `ActivityManager.Instance.Current == ActivityManager.Instance.DefaultActivity` (free play).
- `!ParentGate.InputBlocked`, `player.IsGrounded`, `!player.IsRiding`, and no toy is playing.
- The toy is **armed**: a toy disarms when its routine ends and re-arms only when the player has left its touch zone.
  This stops him from bouncing straight back onto the toy he just hopped off.
- At least 1.0 s has passed since the last routine ended (any toy).

**Skip.** The kid is usually still pushing the joystick when he walks into the toy, so a push only counts as a skip after
the stick has been **released** first: the joystick magnitude (`PlayerController.MoveInput`) drops below 0.2, and later
rises above 0.55. The jump key or action button does **not** skip. On a skip, the routine hops him off within about
0.3 s (to the exit point or the nearest safe ground point), and the moving parts ease back to rest over 0.5 s.

**Every routine has the same shape**, lasting 5 to 10 s in total:
1. **Get on:** hop onto the first anchor (`PlayerController.RideOn`).
2. **Play:** move the anchor and the toy's parts, and set the pose. Call `MarkPlayed()` the moment the fun part starts.
3. **Get off:** hop off to the exit point (`PlayerController.StopRiding`), with a cheer if it wasn't skipped.

The voice is sparing. A "whee"-type Kid line plays on about half the routines. The sticker line plays only once per
toy.

**The camera** keeps following the player as it does now (`FollowCamera`). No special camera.

**While a toy plays,** `ActivityInviter` stays quiet: it treats a playing toy as "not idle" and resets its timer.

## The toys

Sticker ids, Dutch names and routines. "Anchor" means a `Transform` the kid is parented to while riding. The kid's
**root is at his feet**, so for a sitting pose the anchor sits below the seat surface by the pose's hip height (see
*Poses*).

| Prefab (in `Environment/Equipment`) | Sticker id | NL name (voice group) | Agent | Routine |
|---|---|---|---|---|
| `SM_Prop_Playground_Slide_01` | `slide` | de glijbaan | C | Hop onto the ladder foot; **Climb** up the ladder line (~1.5 s); at the top, **SitSlide** facing down the chute; slide down along the chute surface, accelerating (~1.3 s), with a `toys.whee` line; slide off the end onto the ground and cheer. |
| `SM_Prop_Playground_Rocker_01`, `_Rocker_02` | `rocker_1`, `rocker_2` | de wipkip | C | Hop onto the seat; **SitSeat**; rock ±14° about the spring top at about 1.1 Hz with a small up/down bob, for 6 s; `toys.spring_boing` on each swing. Rock only the body if one of the two objects is the body; otherwise rock the whole toy. |
| `SM_Prop_Playground_Roundabout_01` (Town) | `roundabout` | de draaimolen | C | Hop onto the edge of the disc facing along the turn; **SitSeat**; spin up to one turn per 2.2 s, hold, then slow down (7 s); hop off, then play `PlayHit` (wobbly stun) with a `toys.dizzy` line. |
| `SM_Prop_Playground_Tunnel_01` | `tunnel` | de tunnel | C | Hop to one mouth (the end nearest to the player); **Crawl** through along the tunnel axis (~2.5 s); at the other mouth, pop out with a jump and `toys.peekaboo`. |
| `SM_Prop_Playground_Monkey_Bars_01` | `monkey_bars` | het klimrek | D | Hop up to **Hang** under the first bar; **HangMove** along the bars to the other end (turned 90° so the sideways shimmy follows the bars), with short **Hang** pauses; drop down at the end. |
| `SM_Prop_Playground_Seesaw_01` | `seesaw` | de wip | D | A plush teddy sits on the far seat for good (the builder places one, see the builder section). Hop onto the near seat; **SitSeat**; the plank tilts between −12° and +12° about its middle at about 0.6 Hz for 7 s; `toys.seesaw_thump` at each end. |
| `SM_Prop_Playground_Swings_01` | `swings` | de schommel | D | Hop onto the seat nearest to the player; **SitSeat**; swing about the top bar, building up from 8° to 35° and back down (8 s); `toys.swing_creak` at each end; `toys.whee` near the top. |
| `SM_Prop_Playground_Fort_01` (Town) | `fort` | het fort | D | **Climb** up to the highest flat platform; cheer at the top with `toys.top`; jump down in an arc to the exit point. If the fort has a slide, sliding down it instead is a welcome extra. |
| `SM_Prop_Playground_Tyrewall_01` | `tyre_wall` | de bandenmuur | D | **Climb** up the tyre face; stand on top and cheer with `toys.top`; jump down the far side. |
| `SM_Prop_Playground_Track_Ride_01` | `track_ride` | de kabelbaan | D | Hop up to **Hang** from the handle at the high end; zoom along the cable to the low end (ease in, ease out, ~2.5 s); a little swing back and forth; drop. The handle is split off and travels with him (see the builder section). |
| `SM_Prop_Playground_Ship_01` | `ship` | het schip | D | This is the playground copy, not the pirate cove ship. Hop onto the deck at the wheel (or at its middle); stand idle and steer (sway his yaw ±20°) for 5 s with `toys.ahoy`; cheer; hop off. |

**Moving parts.** Each Synty toy is a single mesh (the rockers have two objects), so the builder makes its moving parts
in one of two ways:
- **Whole object:** for the roundabout, tilt or spin the whole mesh about a pivot. A seesaw whose base tilts a little is
  acceptable if the split below fails.
- **Mesh split:** `ToyMeshSplitter` (editor) cuts a mesh in two by a box in the toy's local space (a triangle goes to the
  part its centroid lies in). It saves both halves as mesh assets under `Assets/CoolSchool/Playground/Meshes/`,
  re-made on every build, and swaps the original renderer for a static half and a moving half under a pivot. Use it for
  the swing seats and chains, the seesaw plank and the track-ride handle. A moving half gets **no collider**.

**Toys with moving parts lose their static flags** (`StaticEditorFlags.BatchingStatic` is set by
`FinishEnvironmentObject`). Static batching would freeze them, so clear the flags on the moved transforms and all their
children.

## Poses (animator)

The int parameter `ToyPose` on `Kid.controller` sets the pose, using the `ToyPose` enum values. A value above 0 enters
that pose's state from Any State (no self transition, 0.15 s blend). `ToyPose == 0` returns to locomotion (0.2 s). Add
the states in `KidAnimatorBuilder` after `AddSit`, in the same style. A missing clip goes to `missingClips` and skips that
state; the routine still runs, the kid just stands.

| `ToyPose` | Value | Clip (first one found) | Use |
|---|---|---|---|
| `None` | 0 | — | — |
| `SitSeat` | 1 | `HumanM@SitMedium01 - Loop`, `HumanF@SitMedium01 - Loop`, `HumanM@SitHigh01 - Loop` | swing, rocker, roundabout, seesaw |
| `SitSlide` | 2 | `HumanM@SitGround01 - Loop`, `HumanF@SitGround01 - Loop` | slide |
| `Climb` | 3 | RPG `Climb-Ladder-Up` (fallback `Climb-Ladder-Idle`) | slide ladder, fort, tyre wall |
| `Hang` | 4 | RPG `Ledge-Idle` | monkey bars, track ride |
| `HangMove` | 5 | RPG `Ledge-Right` (fallback `Ledge-Left`) | monkey bars |
| `Crawl` | 6 | RPG `Crawl-Forward` (fallback `Crawl-Idle`) | tunnel |

- The RPG clips may hold root motion. The kid's animator has `applyRootMotion = false`, so humanoid root motion is
  dropped and they play in place, unless their import has "Bake Into Pose" set. Check each clip's
  `ModelImporter.clipAnimations` (`lockRootPositionXZ`, `lockRootHeightY`, `keepOriginal*`) and **do not** change the
  vendor import settings. If a clip would drift, say so in the report.
- Make clips loop the same way as `AddSit` (a self transition at exit time 1 when `!isLooping`).
- **Never** use `Ride_the_Bull_*`, `Glory_Spin` or any Gestures MoCap clip outside the whitelist in the HANDOVER.

**Hip heights.** For sitting and hanging, the anchor must sit at the seat surface (or bar height) minus the pose's hip
offset (or hand offset). Agent C works these out at build time by sampling each pose clip on the player kid with
`AnimationClip.SampleAnimation`, then reading the hips height (for `SitSeat` and `SitSlide`) and the hands height
(for `Hang` and `HangMove`) above the root. It stores them on each `PlaygroundToy` (`ToyPoseOffsets`, see below) and
logs them (`PlaygroundToys: pose offsets ...`). If sampling fails, fall back to these constants: SitSeat hips 0.45,
SitSlide hips 0.12, Hang hands 1.35 (for the Synty kid size; scale by the kid's height ratio if you have it).

## Runtime API (namespace `CoolSchool.Playground`, folder `Scripts/Playground/`)

Agent C owns every type in this section, except the routine subclasses marked D. The signatures are exact, so agent D
can code against them.

```csharp
public enum ToyPose { None = 0, SitSeat = 1, SitSlide = 2, Climb = 3, Hang = 4, HangMove = 5, Crawl = 6 }

/// Hip/hand heights above the kid's root for each pose, measured by the builder.
[System.Serializable] public struct ToyPoseOffsets
{
    public float sitSeatHips;   // ~0.45
    public float sitSlideHips;  // ~0.12
    public float hangHands;     // ~1.35
}

/// On the toy's root (the Synty instance). Owns the touch zone's state and the toy's routine.
public class PlaygroundToy : MonoBehaviour
{
    [SerializeField] string stickerId;
    [SerializeField] string voiceName;           // the voice group suffix: "slide", "rocker", ... (both rockers: "rocker")
    [SerializeField] ToyRoutine routine;
    [SerializeField] Sprite stickerIcon;
    [SerializeField] ToyPoseOffsets poseOffsets;

    public string StickerId { get; }
    public string VoiceName { get; }
    public ToyRoutine Routine { get; }
    public Sprite StickerIcon { get; }
    public ToyPoseOffsets PoseOffsets { get; }
    public bool Armed { get; }                   // false from the end of a routine until the player leaves the zone

    // Called by ToyTouchZone.
    public void PlayerEntered(PlayerController player);
    public void PlayerExited(PlayerController player);

    [ContextMenu("Play now")] void PlayNow();    // play mode: starts the routine wherever the player is
}

/// On the "TouchZone" child (trigger BoxCollider). Forwards enter/exit of the player to its PlaygroundToy.
public class ToyTouchZone : MonoBehaviour { [SerializeField] PlaygroundToy toy; }

/// On the player. Runs at most one toy routine at a time, watches the joystick for a skip, and gives the sticker.
public class ToyPlayer : MonoBehaviour
{
    public static ToyPlayer Instance { get; }
    public static event System.Action<PlaygroundToy> Started;
    public static event System.Action<PlaygroundToy> Finished;
    public bool IsPlaying { get; }
    public PlaygroundToy CurrentToy { get; }
    public bool TryStart(PlaygroundToy toy);    // checks every start rule above; false when not allowed
    public void StopNow();                      // ends at once: parts SnapToRest, kid StopRiding to the exit, pose None
}

/// Base of every toy routine. On the toy root, next to PlaygroundToy.
public abstract class ToyRoutine : MonoBehaviour
{
    [SerializeField] protected Transform exitPoint;   // on the ground, where he lands after getting off
    public Transform ExitPoint => exitPoint;

    /// get on -> play -> get off. Must check ctx.SkipRequested often (use ctx.Wait) and, when it is set, go straight to
    /// ctx.HopOff. Must always end with the kid off the toy (ctx.HopOff), also when skipped.
    public abstract System.Collections.IEnumerator Play(ToyPlayContext ctx);

    /// Puts moving parts back to rest at once (StopNow, OnDisable). Default: nothing.
    public virtual void SnapToRest() { }

    /// Eases moving parts back to rest (after a skip or at the end). Default: SnapToRest and finish.
    public virtual System.Collections.IEnumerator EaseToRest(float seconds) { SnapToRest(); yield break; }
}

/// Handed to ToyRoutine.Play. Wraps the player and the shared helpers.
public sealed class ToyPlayContext
{
    public PlayerController Player { get; }
    public KidAnimator Kid { get; }
    public PlaygroundToy Toy { get; }
    public bool SkipRequested { get; }

    public System.Collections.IEnumerator HopOnto(Transform anchor);      // RideOn + wait until mounted (~0.45 s)
    public System.Collections.IEnumerator HopOff(Vector3 groundPoint);    // pose None, StopRiding, wait until !IsRiding
    public void SetPose(ToyPose pose);
    public void MarkPlayed();                                             // the fun part has started: the sticker is earned
    public void Say(string group, float chance = 1f);                     // VoicePlayer.Say with a random chance
    public void Cue(string cue);                                          // Sfx.Play(cue, player position)
    public System.Collections.IEnumerator Wait(float seconds);            // returns early when SkipRequested
}

/// The sticker collection, stored in PlayerProgress.toyStickers.
public static class ToyStickers
{
    public static readonly string[] AllIds =
        { "slide", "roundabout", "swings", "tunnel", "monkey_bars", "rocker_1", "ship", "seesaw", "fort", "rocker_2", "tyre_wall", "track_ride" };
    public static bool Has(string id);
    public static int Count { get; }
    public static bool Give(string id);   // adds + saves (ProgressStore.Save raises Changed); true when it is new
}

/// The board on the playground: one sticker spot per id (silhouette until earned).
public class StickerBoard : MonoBehaviour
{
    public static StickerBoard Instance { get; }
    public Transform SpotFor(string stickerId);         // null if unknown
    public void Refresh();                              // shows owned stickers, silhouettes for the rest
    public IEnumerator PlaceSticker(string stickerId);  // pops the sticker into its spot with a sparkle
}
```

`PlayerController` gets one small addition (agent C): `public Vector2 MoveInput => InputEnabled &&
!CoolSchool.Parent.ParentGate.InputBlocked ? ReadMoveInput() : Vector2.zero;`

`KidAnimator` gets one (agent C): `public void SetToyPose(ToyPose pose)`, which sets the int `ToyPose` if the parameter
exists. `KidAnimator` is in `CoolSchool.Characters`, so either reference `CoolSchool.Playground.ToyPose` or take an `int`
and keep an `int` overload; agent C picks one and documents it on the method.

**Routine classes** (each in its own file in `Scripts/Playground/`, deriving from `ToyRoutine`, with serialized
`Transform` fields for its anchors and pivots, set by the builder):
- Agent C: `SlideRoutine`, `RockerRoutine` (both rockers), `RoundaboutRoutine`, `TunnelRoutine`.
- Agent D: `MonkeyBarsRoutine`, `SeesawRoutine`, `SwingRoutine`, `ClimbTopRoutine` (fort and tyre wall),
  `TrackRideRoutine`, `ShipWheelRoutine`.

A routine moves its anchor (and the kid with it, since he is parented) by changing the anchor's transform over time.
The anchor's forward is the way the kid faces.

## Sticker moment

When a routine has finished (the kid is back on the ground), if `MarkPlayed()` was called and `ToyStickers.Give(id)`
returns true:
1. A round sticker (the toy's icon on a white disc with a coloured rim, made at run time) pops up above the kid with a
   sparkle and `toys.sticker_pop`, and spins once.
2. The Narrator says `toys.sticker.<voiceName>`.
3. The sticker flies to the star jar HUD and fades out, with `toys.sticker_stick`. The board itself updates through
   `StickerBoard.Refresh()`, or through `PlaceSticker` if the board is on screen.
4. `RewardSystem.AddStars(1, kid position, announce: false)`.
5. On the 12th sticker: the Narrator says `toys.sticker_all` and he gets 3 more stars (so 15 in total).

Skipping still earns the sticker, as long as `MarkPlayed()` was reached.

## The sticker board

- **Look:** a free-standing board (a wooden-framed panel on two legs, made from Synty pieces or simple shapes) with 12
  spots in a 4 x 3 grid. Each spot is a round sticker showing the toy's icon, black like the Toy Room silhouettes until
  earned.
- **Place:** north of the spawn (0, 0, −16.9), so the fixed north-looking camera sees it. Use a free spot on the
  playground's south side, within about 6 m of the spawn, facing south (toward the camera). Avoid the equipment ring,
  benches, bins and ActivityZones, using the occupied/reserved data the builder has, or the bounds. Log the position.
- **Intro:** the first time ever that the player comes within 3 m of the board in free play, the Narrator says
  `toys.board_intro` (stored in `PlayerProgress.toyBoardIntroHeard`).
- It listens to `ProgressStore.Changed` and calls `Refresh()`.

**Icons:** render each toy prefab to `Assets/CoolSchool/Playground/StickerIcons/<id>.png` (256 x 256, transparent, a
three-quarter view). Only render a missing PNG, as `PirateCoveBuilder` does with its map icons; delete one to re-render
it. Reuse `RewardIconRenderer`'s render code if it can be called, rather than copying it. The silhouette is the same
sprite tinted black, as the Toy Room does it.

## Builder (editor, namespace `CoolSchool.EditorTools`, folder `Editor/Playground/`)

`public static partial class PlaygroundToysBuilder` (agent C) with:

```csharp
public sealed class ToyInputs { public PlayerController Player; }
public static void Build(ToyInputs inputs);   // called from RunBuild right after ToyRoomBuilder.Build
```

- **Steps:** find `Environment/Equipment`. For each child whose name starts with one of the prefab names in the table,
  call `PrepareToy`, then the toy's `Configure*` method. Then put `ToyPlayer` on the player, build the sticker board,
  and render the icons.
- **Hook in `PlaygroundPrototypeBuilder.RunBuild()`** (agent C, one line plus a progress line, right after
  `ToyRoomBuilder.Build(...)`): `PlaygroundToysBuilder.Build(new PlaygroundToysBuilder.ToyInputs { Player =
  player.GetComponent<PlayerController>() });`. Nothing else in that file changes.
- **The per-toy configure methods** are C# partial methods:
  - Agent C declares all of them in `PlaygroundToysBuilder.cs`: `static partial void ConfigureSlide(ToySetup s);`,
    `ConfigureRocker`, `ConfigureRoundabout`, `ConfigureTunnel`, `ConfigureMonkeyBars`, `ConfigureSeesaw`,
    `ConfigureSwings`, `ConfigureFort`, `ConfigureTyreWall`, `ConfigureTrackRide`, `ConfigureShip`.
  - Agent C implements the first four in `PlaygroundToysBuilder.Core.cs`; agent D implements the other seven in
    `PlaygroundToysBuilder.More.cs`.
  - After configuring, a toy with no routine (`s.Toy.Routine == null`) gets its `PlaygroundToy` and touch zone removed,
    with a warning, so a half-done toy never breaks the game.

`internal sealed class ToySetup` (agent C, in its own file), the shared helper:

```csharp
internal sealed class ToySetup
{
    public GameObject Root;            // the Synty instance
    public PlaygroundToy Toy;
    public string StickerId;
    public Bounds WorldBounds;         // renderer bounds
    public Transform Anchors;          // child group "ToyAnchors" for anchors, pivots and the exit point
    public ToyPoseOffsets PoseOffsets;

    public Transform AddPoint(string name, Vector3 worldPosition, Quaternion worldRotation);   // under Anchors
    public Transform MakePivot(string name, Vector3 worldPivot, params Transform[] moveUnder); // reparents, keeps world pose
    public Transform SplitMesh(string partName, Bounds localBox, Vector3 worldPivot);          // ToyMeshSplitter; returns the pivot of the moving half
    public bool TopAt(float x, float z, out Vector3 hit);       // ray down onto this toy's own colliders only
    public bool GroundAt(float x, float z, out Vector3 hit);    // ray down onto anything except this toy (the ground)
    public void LogHeightMap(float cell = 0.25f);               // ASCII height map of TopAt over the footprint, to the log
    public void ClearStatic(Transform t);                       // clears static flags on t and its children
    public T SetRoutine<T>(Transform exitPoint) where T : ToyRoutine;  // adds the routine, assigns it on PlaygroundToy
    public void Log(string message);                            // "PlaygroundToys: <id>: ..."
}
```

- **Finding the shape.** Synty pivots and orientations are unknown, and you cannot open Unity, so work out anchors from
  the geometry at build time, never from guessed constants alone:
  - Use `TopAt` and `GroundAt` (call `Physics.SyncTransforms()` first; the toys have MeshColliders from
    `FinishEnvironmentObject`), the bounds, and the mesh vertices in local space.
  - Call `LogHeightMap` for every toy, so the main agent can read each shape in the batch log.
  - Log every anchor you place in world space.
  - Make each configure robust: when a feature can't be found (for example the ladder side), fall back to something
    sensible and log a `PlaygroundToys: <id>: fallback ...` line.
- **The teddy on the seesaw** (agent D): find a teddy or plush prefab by name in the Kids pack (search the asset
  database for `Teddy`, then `Plush`, then `Bear`, in `t:Prefab`). Sit it on the far seat and parent it to the plank
  pivot. Leave it out if none is found.
- **The touch zone:** `PrepareToy` adds the child `TouchZone` (a trigger `BoxCollider` in world-aligned size; compute it
  in the toy's local space so it follows the toy's rotation) with `ToyTouchZone`. It goes on the default layer. Check
  how `ActivityZone` detects the player and do the same.
- **The pose offsets:** `PrepareToy` fills `s.PoseOffsets` from one shared measurement (sample once per build) and
  writes them to the `PlaygroundToy`.

**Menu items** (agent C, `Editor/Playground/PlaygroundToysMenu.cs`): **CoolSchool → Playground Toys → Give All
Stickers**, **Clear Stickers**, **Clear Board Intro**.

**Preview shots** (agent C writes the framework, both agents add shots):
- Agent C adds `static void AddToyGroups(List<Group> groups)` in `Editor/Playground/PreviewCapture.Toys.cs`
  (`partial class PreviewCapture`) and hooks it into `BuildGroups()` in `PreviewCapture.cs` with one line.
- Inside it, agent C calls a partial method `static partial void AddMoreToyGroups(List<Group> groups);`, which agent D
  implements in `Editor/Playground/PreviewCapture.Toys.More.cs`.
- Agent C writes a helper that stages the player kid mounted on a named anchor of a toy, posed with a `ToyPose` (clip
  sampled), at a given routine progress where useful. For example, the slide halfway down. It frames the toy from the
  camera side and from a side angle.
- Agent C adds shots for its four toys plus `toy_board` (the sticker board, half the stickers owned). Agent D adds shots
  for its seven.
- Name every shot `toy_<id>_<moment>`, so `-previewShots toy` renders them all.

## Voice lines

Agent C adds them in a new partial file `Editor/Voice/VoiceLineSeeder.Toys.cs` with `static IEnumerable<Seed>
ToySeeds()`, and one line in `VoiceLineSeeder.cs`'s apply loop, next to `NumberSeeds()`. `Kid` is Freya, pitched up at
run time; `Narrator` is Lily.

| Key | Speaker | NL | EN |
|---|---|---|---|
| `toys.whee.1` | Kid | Wiiieee! | Wheee! |
| `toys.whee.2` | Kid | Joepie! | Yippee! |
| `toys.whee.3` | Kid | Wat leuk! | This is fun! |
| `toys.whee.4` | Kid | Nog een keer! | One more time! |
| `toys.dizzy.1` | Kid | Ik ben duizelig! | I'm dizzy! |
| `toys.dizzy.2` | Kid | Alles draait! | Everything is spinning! |
| `toys.peekaboo.1` | Kid | Kiekeboe! | Peekaboo! |
| `toys.top.1` | Kid | Ik ben helemaal boven! | I'm all the way up! |
| `toys.top.2` | Kid | Kijk eens hoe hoog! | Look how high I am! |
| `toys.ahoy.1` | Kid | Ahoi! Land in zicht! | Ahoy! Land ho! |
| `toys.sticker.slide.1` | Narrator | Een sticker van de glijbaan! | A slide sticker! |
| `toys.sticker.roundabout.1` | Narrator | Een sticker van de draaimolen! | A roundabout sticker! |
| `toys.sticker.swings.1` | Narrator | Een sticker van de schommel! | A swing sticker! |
| `toys.sticker.tunnel.1` | Narrator | Een sticker van de tunnel! | A tunnel sticker! |
| `toys.sticker.monkey_bars.1` | Narrator | Een sticker van het klimrek! | A monkey bars sticker! |
| `toys.sticker.rocker.1` | Narrator | Een sticker van de wipkip! | A spring rider sticker! |
| `toys.sticker.ship.1` | Narrator | Een sticker van het schip! | A ship sticker! |
| `toys.sticker.seesaw.1` | Narrator | Een sticker van de wip! | A seesaw sticker! |
| `toys.sticker.fort.1` | Narrator | Een sticker van het fort! | A fort sticker! |
| `toys.sticker.tyre_wall.1` | Narrator | Een sticker van de bandenmuur! | A tyre wall sticker! |
| `toys.sticker.track_ride.1` | Narrator | Een sticker van de kabelbaan! | A zip line sticker! |
| `toys.sticker_all.1` | Narrator | Alle speeltuinstickers! Wat knap van jou! | All the playground stickers! Well done! |
| `toys.board_intro.1` | Narrator | Dit is je stickerbord. Speel met alles in de speeltuin, dan krijg je stickers! | This is your sticker board. Play with everything on the playground to get stickers! |

Check how groups are named in `VoiceLineSeeder` (a key `a.b.1` gives the group `a.b`) and follow that pattern exactly.
Check how `VoiceSetupBuilder` or the seeder registers new groups, so a rebuild seeds them. The main agent generates the
clips.

## Sound cues

Agent C adds these to `SoundCues.All` under a `// Playground toys` comment, plus synonyms in `SoundAutoAssign` the way
the existing cues have them. If the installed packs have a fitting clip for a cue (look at `SoundPackPicks.txt` and
`Tools/import-audio-clips.py`, as described in the audio contract), add a pick line too; otherwise leave it to auto
assign.

| Cue | Sound |
|---|---|
| `toys.slide_whoosh` | slide, whoosh |
| `toys.swing_creak` | creak, swing |
| `toys.spin` | whirl, spin |
| `toys.spring_boing` | boing, spring |
| `toys.climb_step` | step, wood, climb |
| `toys.crawl` | rustle, crawl |
| `toys.zip` | zip, whoosh |
| `toys.seesaw_thump` | thump, wood |
| `toys.sticker_pop` | pop |
| `toys.sticker_stick` | sticker, slap, stick |

## Progress fields

Agent C adds these to `PlayerProgress`, in a short commented block at the end of the field list:

```csharp
public List<string> toyStickers = new List<string>();  // playground toy stickers earned (ToyStickers ids)
public bool toyBoardIntroHeard;                       // the sticker board was introduced once
```

## Other small edits (agent C)

- **`ActivityInviter`:** treat `ToyPlayer.Instance != null && ToyPlayer.Instance.IsPlaying` as not idle (reset the
  timer).
- **Ending activities:** if an activity starts while a toy plays (it shouldn't, but the parent area or a debug menu
  could start one), `ToyPlayer` calls `StopNow()`. Poll `ActivityManager.Instance.Current` each frame while playing;
  don't add events to `ActivityManager`.
- **`ToyPlayer.OnDisable`** and the routine's `OnDisable` must leave a clean kid: not riding, pose None, `ForceIdle`
  off, and parts at rest.

## Ownership

- **Agent C owns:**
  - All of `Scripts/Playground/` except D's routine files.
  - `Editor/Playground/PlaygroundToysBuilder.cs`, `PlaygroundToysBuilder.Core.cs`, `ToySetup.cs`,
    `ToyMeshSplitter.cs`, `PlaygroundToysMenu.cs` and `PreviewCapture.Toys.cs`.
  - `Editor/Voice/VoiceLineSeeder.Toys.cs`.
  - The shared small edits: `KidAnimatorBuilder`, `KidAnimator`, `PlayerController.MoveInput`, `PlayerProgress`,
    `SoundCues`, `SoundAutoAssign`, the picks file, `VoiceLineSeeder.cs` (one line), `PreviewCapture.cs` (one line),
    `PlaygroundPrototypeBuilder.cs` (the hook only) and `ActivityInviter`.
- **Agent D owns:**
  - `Scripts/Playground/{MonkeyBarsRoutine, SeesawRoutine, SwingRoutine, ClimbTopRoutine, TrackRideRoutine,
    ShipWheelRoutine}.cs`.
  - `Editor/Playground/PlaygroundToysBuilder.More.cs` and `Editor/Playground/PreviewCapture.Toys.More.cs`.
  - If D needs something in C's API that is missing, add a helper in its own files (an extension method or a private
    helper). Don't edit C's files, and mention it in the report.

## Report back

- The files you created or changed.
- `compile-check: OK`.
- Per toy, how the builder finds its anchors and parts, and which fallbacks exist.
- Anything you were unsure of that the main agent should look at in the preview shots.
