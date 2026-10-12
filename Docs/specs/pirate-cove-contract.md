# Pirate Cove: implementation contract

Date: 2026-10-12. A new play section (pretend battle + treasure hunt) north-east of the playground, behind the
knights castle. Three agents build it in parallel:

- **Agent F (flow):** the activity, the ship, the cannon and balloons, the skeleton boat and the skeletons.
- **Agent T (treasure):** the parrot, the map, digging, the chest and coins, the trophy, the costume, and all the
  shared small edits (voice, sound cues, parent area, reward catalog).
- **Agent B (builder):** the editor builder that places the cove and wires everything.

Read first: `Docs/HANDOVER.md`, `Docs/specs/knights-slice-contract.md`, `Docs/specs/aliens-and-sound-contract.md`,
`Docs/specs/letters-and-parent-area-contract.md`, `Docs/specs/big-castle-contract.md` (another session is building
the castle right now; see "Coordination").

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it only fails on a type another agent owns, wait about a minute and
retry, for up to about 10 minutes. Never create another agent's types.

**Player:** a 4-year-old who can't read. Everything is spoken; he never fails. Opponents never hurt: skeletons fall
apart into giggling bones and rebuild as friends. Controls stay one floating joystick plus the one action button.

## User decisions (2026-10-12)

- **Setting:** the Synty playground pirate ship on a big sand pit. When the adventure starts, "imagination mode"
  turns the sand into sea. Only assets we already have.
- **Place:** north-east, behind the castle (north of the arena's north fence).
- **Skeletons:** silly duel opponents. One sword hit: they fall apart into a pile of bones, giggle, rebuild, and
  join the crew.
- **Treasure:** picture-map route between landmarks → letter lock on the chest → count the gold coins.
- **Flow:** sail → (cannon) → skeletons → island → treasure, as one round with phases (about 4–6 minutes).
- **Cannon:** yes, at balloons on the skeleton boat; the parrot counts along.
- **Guide:** a talking parrot, **Lorre**. New speaker `Parrot`.
- **Steering:** the kid steers at the wheel with the joystick; it can't go wrong (the island pulls gently).
- **Crew:** 2 pirate classmates join (duel along, cheer, help dig).
- **Difficulty:** `pirates` level 1–3, adaptive, with a slider in the parent area.
- **Rewards:** pirate costume during play; stars = the counted coins; pirate items in the reward catalog; the
  opened treasure chest stays near the dock as a trophy that fills up over the rounds.

## Flow (`CoolSchool.Pirates.PirateAdventure : Activity`, agent F)

Practical change to the user's order: the duel happens on the island **beach**, not on the deck (the playground
ship's deck isn't reliable NavMesh). The skeletons sail ahead to the island after the cannon phase and wait there.

1. **Begin (at the dock).**
   - `imagination.Play(true)` (sparkle + overlay), then `sea.SetActive(true)` with a few `BattleEffects.Sparkle`
     bursts over it; `Sfx.Play("pirates.waves")`, `Sfx.PlayMusic("music.pirates")`.
   - Player costume on (`PirateCostume.Show(true)`): eyepatch, plus the captain hat if `hat.pirate` is unlocked,
     otherwise the bandana. Sword shown (`WeaponHolder.ShowWeapons(true)`), jump disabled.
   - The parrot flies in and perches on the player's shoulder. Voice `pirates.start`.
   - Crew: the two pirate kids appear with a sparkle at the dock (voice `pirates.crew`).
   - Board: the player is moved onto the helm with a short hop (`PlayerController.RideOn(ship.HelmSeat)`), crew onto
     `ship.CrewSeats` (agents and AI off, parented). Voice `pirates.steer`. Action-button icon: none needed yet.
2. **Sail.** `ship.StartSailing()`. Joystick X steers; see `PirateShip`. The wheel spins with the steering.
3. **Cannon (when `ship.Progress01 >= 0.4`).** `ship.PauseSailing(true)`. The skeleton boat appears from the west
   with N balloons (level table) and drifts back and forth in front of the ship (north of it, visible). The skeletons
   ride on it, waving. Voice `pirates.balloons`.
   - The action button gets the cannon icon; `cannon.Armed = true`. Each tap fires one confetti ball that always
     hits the nearest balloon. Each pop: confetti in the balloon's colour, `pirates.balloon_pop`, and the parrot
     counts: `number.<k>` (k = balloons popped so far).
   - Hint `pirates.cannon_hint` after 6 s without a tap.
   - All popped: voice `pirates.balloons_done`; the skeleton boat sails to the island's west beach
     (`skeletonBoat.SailToIsland()`). `cannon.Armed = false`, sailing resumes.
4. **Arrive.** `ship.ArrivedAtIsland` → voice `pirates.arrive`. The player hops off to `islandLandingPoint`
   (`PlayerController.StopRiding(point)`), the crew to `crewLandingPoints` (agents on). `islandBounds` on (invisible
   walls so nobody walks into the pretend sea).
5. **Skeletons.** They hop off the boat to `skeletonLandingPoints` with a sparkle; one says `pirates.skeletons`.
   `playerInput.enabled = true`, button icon = sword. Crew `KnightAllyAI.enabled = true`. Hint
   `pirates.fight_hint` after 5 s without a swing.
   - A skeleton hit `hitsToDefeat` times falls apart (`BonePile`), giggles (`pirates.skeleton_friend`), rebuilds
     and becomes a friend: it stops attacking and follows the player loosely (or cheers on the spot).
   - **Two waves (2026-10-12).** Wave 1 (the riders) hops off as above. When every wave-1 skeleton is a friend
     (they cheer where they stand), `pirates.skeletons` plays again and wave 2 appears on the boat's seats with a
     sparkle and jumps to the beach one after another, onto landing points 5-9 (more points, north of the player,
     spread north and east). Only one skeleton attacks at a time (the static token). The auto-befriend safety net
     covers both waves. Up to 9 friends: when the duel is over each takes its own place on a wide ring beside and
     behind the player (`SkeletonPirateAI.Follow(leader, slot)`), never in front of him. The party has 12 spots
     (8 in a row, 4 staggered behind), and the letter-lock helpers use a second row behind the chest.
   - **Only the player defeats.** Crew hits (`KnightAllyAI`, `SwordFighter.CanDefeat = false`) make a skeleton
     wobble and giggle but never take its last hit (`Hittable.TakeHit(..., canDefeat: false)`).
   - When all are friends: `playerInput.enabled = false`, crew AI off (they follow), voice `pirates.map`.
6. **Map route.** `map.Show(icons)` with the chosen landmarks' icons, then the X. The parrot flies to the first
   landmark and hovers above it; a sparkle trail on the ground (like `ActivityInviter`) leads from the player.
   Reaching a landmark (within 2.2 m): `map.SetReached(i + 1)`, `Sfx.Play("pirates.map_tick")`, voice
   `pirates.landmark`, the parrot flies on to the next one. After the last landmark: `digSpot.Show(true)` and the
   parrot flies to it. Voice `pirates.dig` when the player reaches it.
7. **Dig.** Button icon = spade; `digSpot.Armed = true`. Each tap: dig animation (`KidAnimator` attack trigger),
   sand puff, `pirates.dig`. After `tapsNeeded` taps `DigSpot.Dug` fires → `chest.Rise()` (`pirates.chest_rise`).
8. **Letter lock.** Voice `pirates.lock` then `letter.sound.<x>`, then `lessonUI.ShowLetterCards(...)`.
   Right: `lessonUI.CelebrateCard`, `chest.Unlock()` (lock pops off, lid opens, `pirates.lock_open`), voice
   `pirates.lock_right`. Wrong: wiggle, voice `pirates.lock_wrong`, the sound again; after the second mistake the
   right card pulses. Count a mistake for each wrong pick.
   - **Letter choice:** take the letters in `ProgressStore.Current.lettersIntroduced`. If the child's name's first
     letter is among them, use it with 50% chance, otherwise pick one at random. If none is introduced yet, use
     `LetterCurriculum.OrderFor(childName)[0]` and teach it first: voice `pirates.lock_new`, the sound, and the right
     card pulses from the start. Distractors: random from `LetterCurriculum.SchoolOrder` without the target; below
     level 3 also skip letters where `LetterCurriculum.AreSimilar(target, d)`. Mirror how `LettersLesson` builds its
     cards from the `LetterSet`.
9. **Count the treasure.** `chest.SpillCoins(count)` puts the coins in a fan in front of the chest (camera side).
   Voice `pirates.count`. `coinPicker.Armed = true`: each tapped coin flies into the chest, `pirates.coin`, and the
   parrot says `number.<k>`. When all are in: voice `pirates.how_many`, `lessonUI.ShowNumberCards` (right answer +
   distractors per level). Right: celebrate. Wrong: wiggle, `lesson.try_again`, then count along again (coins pop
   out and back in one by one with `number.<k>`), then the cards again. Never fail.
   - Then raise `TreasureCounted(count, chest.transform.position)` (RewardSystem gives `count` stars) and add `count`
     to `ProgressStore.Current.pirateCoinsTotal`, save.
10. **Party.** Everyone moves to `partyPoints` and dances (`KidAnimator.SetDancing(true)` with a random variant),
    friend skeletons too; the parrot circles above. Voice `pirates.victory`, `Sfx.Play("pirates.party")`.
    `onVictory.Invoke()` after 4 s, then `End()`.
11. **OnEnd** restores everything, also when ended early mid-phase: sea off, islandBounds off, costume off, weapons
    hidden, button icon reset, jump on, `playerInput.enabled = false`, rider detached safely (if still riding,
    `StopRiding` at the ship's side on the dock), ship back at the dock with a sparkle (`ship.ResetToDock()`),
    skeleton boat and skeletons reset and inactive, crew inactive (sparkle), map hidden, dig spot and chest reset,
    lesson cards hidden, parrot returns (`ShoulderParrot` decides whether it stays on the shoulder), music back to
    playground (ActivityManager already does this when the default activity resumes).
    - **Adaptive level:** 0 mistakes → `pirates` level +1 (max 3); 2 or more → −1 (min 1). Use
      `ProgressStore.Current.GetLevel("pirates")` / `SetLevel("pirates", n)` and save.

### Level table

| | 1 | 2 | 3 |
|---|---|---|---|
| skeletons, wave 1 (ride the boat) | 3 | 3 | 4 |
| skeletons, wave 2 (jump off the boat after wave 1) | 3 | 4 | 5 |
| sword hits per skeleton | 1 | 1 | 2 |
| balloons | 3 | 5 | 6 |
| landmarks on the route | 2 | 3 | 3 |
| letter cards | 2 | 3 | 3 (similar letters allowed) |
| coins | 3–5 | 4–7 | 6–10 |
| number cards | 2 | 3 | 3 |

## World layout (agent B)

The camera looks north. Numbers below assume the castle contract: fence rectangle `MaxZ` = 20.9, arena
x = 20.9 .. `MaxX` (50.9 after the castle change; read it from `EnvironmentInfo`, never hard-code it).

- **Cove rectangle:** x from `info.FenceHalf` (20.9) to `info.MaxX`, z from `info.MaxZ` to `info.MaxZ + CoveDepth`
  (`CoveDepth = 40`). Call its width W and its south-west corner SW. `cx` = SW.x + W/2.
- **Entrance:** a 3 m gap in the main north fence centred on x = SW.x + 2.6 (≈ 23.5): between the playground part and
  the castle's reserved ground (castle x ≥ ~25). Remove the main fence pieces whose bounds overlap the gap after
  `BuildFence` (find them under the "Fence" group); don't change `BuildFence`.
- **Own fence:** west, north and east sides of the cove with the same fence prefab (the south side is the main
  fence). Reserve the whole cove rectangle in `PlaygroundPrototypeBuilder.ReservedAreas` so benches and trees stay
  out; scatter a few trees **outside** the cove's west and north fence is optional.
- **Ground:** sand tiles `SM_Env_Ground_Tile_Sand_01` over the whole cove at y = 0 (grass tiles under it are fine
  as long as the sand sits 1 cm higher). `BuildTileGrid` may become `internal` for this (one-word change).
- **Dock (south):** a wooden dock from planks (`SM_Gen_Prop_Plank_01/02`, a few barrels, the anchor) along the
  south strip, z ≈ SW.z + 2 .. SW.z + 6.
- **Ship:** `SM_Prop_Playground_Ship_01` (a second instance; the one in the playground ring stays), scaled so it is
  about 7 m long, bow pointing north, centred at (cx, SW.z + 9). **Not static** (it moves). Measure its bounds and
  place `HelmSeat` on the deck at the back, `CrewSeats` (2) beside it, the steering wheel
  (`SM_Veh_Steering_Wheel_01`, scaled to ~0.8 m, standing upright facing south) in front of the helm seat, and a
  primitive cannon at the bow (see below). Seats are empty children of the ship root.
- **Sea:** `SM_Generic_Water_Plane_01` (fallback `SM_Gen_Env_Water_Plane_01`, fallback a blue transparent quad),
  scaled to cover the cove except the dock strip, at y = 0.06, **inactive** at start. No collider.
- **Island (north):** an oval of sand tiles or `SM_Env_Ground_Round_Sand_01` raised 0.05 m, centre
  (cx, SW.z + 31), about 18 m wide × 12 m deep. Its south beach is the ship's arrival point
  `IslandDockPoint` at (cx, SW.z + 23) facing north; `IslandLandingPoint` 1.5 m north of where the bow stops; crew
  and skeleton landing points on the beach (skeletons west of the player, all within the camera's view, i.e. north
  of the player).
- **Landmarks (4) on the island,** spread out, all north of the landing point and ≥ 4 m apart: a big flag
  (`SM_Prop_Flag_01`, ~3 m), a sandcastle (`SM_Prop_Sand_Castle_01`, ~1.5 m), a big rock
  (`SM_Gen_Env_Rock_0x`, ~2 m) and a telescope on a barrel (`SM_Prop_Telescope_01` ~1 m). Static.
- **Dig spot:** `DigSpot` near the island's north middle, with a red X made of two flat red boxes (no colliders),
  hidden at start. The chest (`SM_Gen_Prop_Chest_01`, ~0.9 m wide) is buried under it (see `TreasureChest`).
- **Island bounds:** an inactive child with 8–12 box colliders around the island shore (1.5 m high, layer
  Ignore Raycast, no renderers). Inactive at bake time, so the NavMesh ignores it.
- **Skeleton boat:** `SM_Prop_Cardboard_Boat_01` scaled to ~4.5 m long, with 7 balloon slots: thin sticks
  (cylinders) with `BalloonTarget` spheres (0.6 m, bright colours, URP/Lit with a little emission) on top. Path
  points: start off the west side (x = SW.x + 2, z = SW.z + 17), drift between (cx − 5, SW.z + 17) and
  (cx + 5, SW.z + 17) — about 8 m north of the ship when it pauses — and end at the island's west beach. Skeleton
  seats (4) on the boat. Inactive at start.
- **Trophy:** at the dock's west end, visible from the entrance: a second chest model with `TreasureTrophy` and 4
  coin-pile tiers (`SM_Gen_Prop_Coin_Pile_01/02`, `SM_Gen_Prop_Coin_Stack_01/02`).
- **Zone:** `PirateZone` (ActivityZone, radius 2.0) on the dock in front of the ship's stern, with a beacon in a
  sea-blue colour and an icon found by name (try `Anchor`, `Ship`, `Map`, `Compass`, fall back to `Map-Target-Circle`
  sprites from ModularGameUIKit). Add it to the `ActivityInviter` with group `invite.pirates` (after the shop).
- **NavMesh:** the static cove geometry is built in `BuildEnvironment()` (before the bake). The dock, the island and
  the sand must be walkable. The ship's colliders stay as they are at the dock.

## Components

All runtime types are in namespace `CoolSchool.Pirates`, folder `Assets/CoolSchool/Scripts/Pirates/`, unless noted.
The builder wires the serialized fields **by these exact names**.

### Agent F

```csharp
public class PirateAdventure : Activity
{
    [Header("Player")]
    [SerializeField] PlayerController player;
    [SerializeField] PlayerBattleInput playerInput;
    [SerializeField] ActionButton actionButton;
    [SerializeField] WeaponHolder playerWeapons;
    [SerializeField] PirateCostume costume;
    [SerializeField] Sprite swordIcon;
    [SerializeField] Sprite cannonIcon;
    [SerializeField] Sprite spadeIcon;
    [SerializeField] ImaginationEffect imagination;

    [Header("Cove")]
    [SerializeField] PirateShip ship;
    [SerializeField] ShipCannon cannon;
    [SerializeField] SkeletonBoat skeletonBoat;
    [SerializeField] GameObject sea;
    [SerializeField] GameObject islandBounds;
    [SerializeField] Transform islandLandingPoint;
    [SerializeField] Transform[] crewLandingPoints;
    [SerializeField] Transform[] skeletonLandingPoints;
    [SerializeField] Transform[] landmarks;       // 4
    [SerializeField] Sprite[] landmarkIcons;      // same order as landmarks
    [SerializeField] DigSpot digSpot;
    [SerializeField] TreasureChest chest;
    [SerializeField] TreasureCoinPicker coinPicker;
    [SerializeField] Transform[] partyPoints;     // 12: front row of 8 on the island beach, camera side, plus 4 staggered behind

    [Header("Characters")]
    [SerializeField] SkeletonPirateAI[] skeletons; // 4, inactive
    [SerializeField] KnightAllyAI[] crew;          // 2 pirate kids, inactive
    [SerializeField] ParrotGuide parrot;

    [Header("UI")]
    [SerializeField] TreasureMapUI map;
    [SerializeField] LessonUI lessonUI;
    [SerializeField] LetterSet letters;

    public UnityEvent onVictory;
    public event System.Action<int, Vector3> TreasureCounted;   // coins, where
}
```

- `PlayerController` (Scripts/Player, agent F owns these additions):
  - `public bool IsRiding { get; }`
  - `public void RideOn(Transform seat)`: hops the player there (0.4 s arc), disables the CharacterController and
    walking input, parents to the seat (local pose zero), Speed 0 in the animator.
  - `public void StopRiding(Vector3 groundPoint)`: unparents (back to the original parent), hops to the point,
    re-enables the CharacterController and input.
- `PirateShip` (on the ship root):
  ```csharp
  [SerializeField] Transform helmSeat;
  [SerializeField] Transform[] crewSeats;
  [SerializeField] Transform wheel;              // spins around its local forward axis with the steering
  [SerializeField] Transform islandDockPoint;    // arrival pose
  [SerializeField] Bounds seaArea;               // world; the ship's position is clamped inside (x/z)
  [SerializeField] FloatingJoystick joystick;
  [SerializeField] float sailSpeed = 0.9f;
  [SerializeField] float maxSteerAngle = 35f;
  [SerializeField] float turnRate = 45f;
  [SerializeField] float islandPull = 0.35f;     // 0..1 how strongly the heading drifts back toward the dock point
  [SerializeField] float arriveRadius = 2.5f;
  public Transform HelmSeat { get; }
  public Transform[] CrewSeats { get; }
  public bool IsSailing { get; }
  public float Progress01 { get; }               // 0 at the dock, 1 at the island
  public event System.Action ArrivedAtIsland;
  public void StartSailing();
  public void PauseSailing(bool paused);
  public void ResetToDock();                     // back to the pose cached in Awake, with a sparkle
  ```
  Sailing: always moves forward; joystick X turns the heading within ±`maxSteerAngle` of "straight at the dock
  point"; the heading eases back toward it by `islandPull`. Inside `arriveRadius`, it glides into the dock pose over
  1 s and raises `ArrivedAtIsland`. A gentle bob and roll while the sea is active. Never stops at an edge (clamped).
  Keyboard: A/D or arrows also steer in the editor (use the joystick's value if it already includes keyboard).
- `ShipCannon` (on the cannon at the bow):
  ```csharp
  [SerializeField] ActionButton button;
  [SerializeField] Transform muzzle;
  [SerializeField] Transform barrel;      // recoils
  [SerializeField] SkeletonBoat targets;
  [SerializeField] float cooldown = 0.45f;
  [SerializeField] float flightSeconds = 0.7f;
  public bool Armed { get; set; }
  public event System.Action<BalloonTarget> Popped;
  ```
  Tap (button or Space / gamepad south; gate keyboard with `ParentGate.IsOpen`): fire a 0.25 m confetti ball (a
  small sphere with a trail) in an arc to the nearest unpopped balloon; it always hits. `pirates.cannon`, a puff at
  the muzzle, barrel recoil.
- `BalloonTarget`: `[SerializeField] Renderer balloon; [SerializeField] Color color;` `public bool IsPopped { get; }`,
  `public void Pop()` (hide, confetti in its colour, `pirates.balloon_pop`), `public void ResetBalloon()`; bobs
  gently.
- `SkeletonBoat` (on the boat root):
  ```csharp
  [SerializeField] Transform startPoint;
  [SerializeField] Transform driftLeft;
  [SerializeField] Transform driftRight;
  [SerializeField] Transform islandBeachPoint;
  [SerializeField] BalloonTarget[] balloons;   // 7; the first N are used
  [SerializeField] Transform[] skeletonSeats;  // 4
  [SerializeField] float speed = 1.6f;
  public Transform[] SkeletonSeats { get; }
  public IReadOnlyList<BalloonTarget> ActiveBalloons { get; }
  public bool AtIsland { get; }
  public void Appear(int balloonCount);        // activates at startPoint, sails to the drift line, drifts until told
  public void SailToIsland();
  public void ResetBoat();                     // inactive, back at startPoint, balloons reset
  ```
- `SkeletonPirateAI` (on each skeleton; mirror `AlienKidAI` / `BattleOpponentAI`, don't modify them):
  ```csharp
  [SerializeField] float moveSpeed = 1.5f;
  [SerializeField] float attackRange = 1.3f;
  [SerializeField] float windUpSeconds = 1.0f;   // telegraphed: raises the sword, wobbles
  [SerializeField] float recoverSeconds = 2.5f;
  [SerializeField] GameObject telegraph;          // optional ground ring
  public bool IsFriend { get; }
  public event System.Action<SkeletonPirateAI> BecameFriend;
  public void SetHitsToDefeat(int hits);
  public void Ride(Transform seat);               // agent off, parented, idle/wave
  public void HopOff(Vector3 groundPoint);        // hop arc, agent on
  public void Fight(Transform player);
  public void Follow(Transform target);           // friend: loose follow, 2–4 m
  public void Dance(bool on);
  public void ResetSkeleton();                    // not friend, hittable reset, inactive
  ```
  Components on each skeleton (added by the builder): NavMeshAgent, CapsuleCollider, `Hittable` (team Opponents),
  `KidAnimator` (Kid.controller), `WeaponHolder` (sword `SM_Wep_Sword_Wood_03`, no shield), `SkeletonPirateAI`,
  `BonePile`. A hit on the player: `TakeHit` (stun/wobble only, never defeated). Attack only one at a time across all
  skeletons (a static token), so it never gets crowded.
- `BonePile` (on each skeleton):
  ```csharp
  [SerializeField] Material boneMaterial;   // off-white URP/Lit, made by the builder
  [SerializeField] int boneCount = 8;
  [SerializeField] float apartSeconds = 1.4f;
  [SerializeField] float rebuildSeconds = 0.7f;
  public void FallApart(System.Action rebuilt);
  ```
  Hides the skinned renderers (and the sword), spawns `boneCount` cartoon bones (a thin capsule with two small spheres
  at each end, ~0.35 m) and a skull (a sphere, 0.25 m) from around the hips with small random impulses
  (Rigidbody), `pirates.bones_rattle`, `BattleEffects.Confetti`. After `apartSeconds` they turn kinematic and fly
  back to the body over `rebuildSeconds`, `pirates.bones_rebuild`, a sparkle, the renderers come back, then
  `rebuilt()`. Pool the pieces (create once).
- `RewardSystem` (agent F): `[SerializeField] PirateAdventure pirateAdventure;` and subscribe to
  `TreasureCounted` → `AddStars(Mathf.Clamp(count, 1, 10), position)` after `rewardDelaySeconds`, like the others.
- `PlayerProgress` (agent F): `public int pirateCoinsTotal;`.

### Agent T

- `ParrotGuide` (on the parrot `SM_Prop_Toy_Parrot_01`, scaled to ~0.45 m):
  ```csharp
  [SerializeField] Transform model;             // child that bobs/tilts; the root moves
  [SerializeField] float flySpeed = 6f;
  [SerializeField] Vector3 shoulderOffset = new Vector3(0.12f, 0.1f, 0f);
  public bool IsFlying { get; }
  public void PerchOnShoulder(Animator kid);    // RightShoulder bone (fallback UpperChest), offset, parented
  public void FlyTo(Vector3 point, System.Action arrived = null);   // arcing flight, wing-flap wobble, then hover-bob
  public void HoverAbove(Transform target, float height = 2.2f);    // follows it
  public void Circle(Vector3 centre, float radius, float height);
  public void Squawk();                         // hop + Sfx "pirates.parrot"
  public void Hide();
  ```
  No rig: fake the flapping with a fast small roll/pitch wobble and a bob. Face the flight direction.
- `ShoulderParrot` (on the parrot too): when `plush.parrot` is unlocked and the current activity is not the
  `PirateAdventure`, the parrot perches on the player's shoulder in free play; otherwise it hides (unless the
  adventure uses it). Listens to `ProgressStore.Changed` and polls `ActivityManager.Instance.Current`.
  `[SerializeField] ParrotGuide parrot; [SerializeField] Animator playerAnimator; [SerializeField] string unlockId = "plush.parrot";`
- `PirateCostume` (namespace `CoolSchool.Characters`, Scripts/Characters; a separate type so `AlienLanding`'s
  `GetComponent<CostumeHolder>()` stays unambiguous):
  ```csharp
  [SerializeField] GameObject bandanaPrefab;      // SM_Chr_Attach_Pirate_Bandana_01
  [SerializeField] GameObject captainHatPrefab;   // SM_Chr_Attach_Hat_Pirate_01
  [SerializeField] GameObject eyepatchPrefab;     // SM_Chr_Attach_Pirate_Eyepatch_01
  [SerializeField] string captainHatUnlockId = "hat.pirate";
  [SerializeField] Vector3 hatLocalPosition, hatLocalEuler, eyepatchLocalPosition, eyepatchLocalEuler;
  public void Show(bool show);   // all on the Head bone; zero offsets to start, like CostumeHolder; no colliders
  ```
- `TreasureMapUI` (on the touch canvas):
  ```csharp
  [SerializeField] RectTransform panel;     // parchment, top centre, ~40% of the screen width
  [SerializeField] Image[] stepIcons;       // 4
  [SerializeField] Image[] stepChecks;      // 4, green tick over each icon
  [SerializeField] Image finalX;            // the red X at the end
  [SerializeField] RectTransform[] dots;    // the dotted path between the steps
  public void Show(Sprite[] icons);         // unrolls (scale-in), only icons.Length steps visible
  public void SetReached(int count);        // ticks with a pop; the next icon pulses
  public void Hide();
  ```
- `DigSpot`:
  ```csharp
  [SerializeField] ActionButton button;
  [SerializeField] Transform player;
  [SerializeField] GameObject mark;          // the red X
  [SerializeField] float radius = 1.4f;
  [SerializeField] int tapsNeeded = 4;
  public bool Armed { get; set; }
  public bool PlayerNear { get; }
  public event System.Action Dug;
  public void Show(bool show);
  public void ResetSpot();
  ```
  A tap only counts when `PlayerNear`. Each tap: `KidAnimator` attack on the player, a sand puff (Confetti tinted
  sand colour or a small particle burst), `pirates.dig`, the X shrinks a bit.
- `TreasureChest` (on the buried chest root):
  ```csharp
  [SerializeField] Transform lid;            // the chest model's lid child if it has one; otherwise rotate a fake lid
  [SerializeField] GameObject lockVisual;    // a gold box padlock on the front, made by the builder
  [SerializeField] Transform[] coinSpots;    // 10, in a fan in front of the chest
  [SerializeField] GameObject coinPrefab;    // SM_Gen_Prop_Coin_01
  [SerializeField] float buriedDepth = 1f;
  public void Rise(System.Action done);      // up from the sand with a shake and sparkle
  public void Unlock(System.Action opened);  // lock pops off, lid opens, gold glow
  public TreasureCoin[] SpillCoins(int count);   // coins arc out to the first `count` spots
  public Vector3 IntoChestPoint { get; }
  public void ResetChest();                  // buried, locked, closed, coins gone
  ```
  If the Synty chest has no separate lid object, the builder reports it and the chest opens with a "pop": the whole
  chest does a squash and a gold light/sparkle comes out.
- `TreasureCoin`: `public bool Collected { get; }`, `public void FlyInto(Vector3 point, System.Action done)`,
  `public void Pulse(bool on)`, spins slowly; has a SphereCollider (radius generous, ~0.25 m) on a child.
- `TreasureCoinPicker` (on the chest): `[SerializeField] Camera cam;` `public bool Armed { get; set; }`,
  `public void SetCoins(TreasureCoin[] coins)`, `public event System.Action<TreasureCoin> Picked;`. Touch or mouse
  (new Input System, `Pointer.current`); picks the uncollected coin whose screen position is nearest the press,
  within 110 px (scaled by `Screen.dpi` if known). Gate with `ParentGate.IsOpen`.
- `TreasureTrophy` (on the trophy chest by the dock): `[SerializeField] GameObject[] tiers;`
  `[SerializeField] int[] thresholds = { 1, 10, 25, 50 };` `[SerializeField] Transform lid;` Shows tier i when
  `pirateCoinsTotal >= thresholds[i]`; the lid is open once the total is > 0. Listens to `ProgressStore.Changed`.
  `public void Celebrate()` (sparkle + `reward.star_land`), called by the adventure at the end.

**Shared small edits (agent T):**
- `VoiceLine.Speaker`: add `Parrot`. `VoicePlayer`: `[SerializeField, Range(0.5f, 2f)] float parrotPitch = 1.45f;`,
  in `PitchFor`, and Parrot may be interrupted like Kid/Goblin/Alien. `VoiceGenerationSettings`: `parrotVoice` =
  `Clyde` (wire it where the other speakers map to voices in the generator).
- `VoiceLineSeeder`: the lines below.
- `SoundCues` (+ seeder) cues: `pirates.waves`, `pirates.wheel`, `pirates.cannon`, `pirates.balloon_pop`,
  `pirates.bones_rattle`, `pirates.bones_rebuild`, `pirates.parrot`, `pirates.map_tick`, `pirates.dig`,
  `pirates.chest_rise`, `pirates.lock_open`, `pirates.coin`, `pirates.party`, `music.pirates`. `SoundAutoAssign`
  synonyms: cannon → cannon, boom, blast, explosion; balloon_pop → pop, balloon; bones_rattle → bone, rattle,
  skeleton, clatter; parrot → parrot, bird, squawk; dig → dig, shovel, sand; coin → coin, gold, pickup; waves →
  wave, sea, ocean, water; lock_open → unlock, lock, click.
- Parent area: a fourth row `ParentStrings.RowPirates` ("Piraten" / "Pirates"), skill `"pirates"`, max 3, below the
  shop row; grow the window if it doesn't fit.
- `RewardCatalogSeeder`: insert `plush.parrot` (PlushToy, `SM_Prop_Toy_Parrot_01`) after `shield.round`, and
  `sword.pirate` (Sword, `SM_Wep_Sword_Plastic_02`) after `hat.cap`. Keep `hat.pirate`. Make sure `hat.pirate`
  resolves to `SM_Chr_Attach_Hat_Pirate_01`.

### Agent B: `Assets/CoolSchool/Editor/Pirates/PirateCoveBuilder.cs`

```csharp
internal static class PirateCoveBuilder
{
    internal sealed class CoveLayout { /* everything Build() needs from BuildCove() */ }
    internal struct PirateInputs
    {
        public RuntimeAnimatorController KidController;
        public int KidsLayer;
        public PlayerController Player;
        public ActionButton ActionButton;
        public ImaginationEffect Imagination;
    }
    // Static geometry, before the NavMesh bake. Removes the main fence pieces in the entrance gap.
    internal static CoveLayout BuildCove(Transform root, PlaygroundPrototypeBuilder.EnvironmentInfo info);
    // Characters, the activity, UI and wiring, after the lesson/voice setup (needs LessonUI and the inviter).
    internal static void Build(CoveLayout layout, PirateInputs inputs);
}
```

- `PlaygroundPrototypeBuilder`: add `public PirateCoveBuilder.CoveLayout PirateCove;` to `EnvironmentInfo`; in
  `BuildEnvironment()` call `info.PirateCove = PirateCoveBuilder.BuildCove(root, info);` **after `BuildFence` and
  after the castle** (it needs the final `MaxX`), and before benches/greenery if the castle agent has moved those
  later — otherwise right after the castle line, relying on `ReservedAreas` being checked at placement time. In
  `RunBuild()` call `PirateCoveBuilder.Build(...)` right after the ice cream shop. Nothing else in that file.
- Characters: 9 skeletons (`SM_Chr_Kid_Skeleton_01`, inactive) with the components listed above; 2 pirate kids
  (`SM_Chr_Kid_Pirate_01`, `SM_Chr_Kid_Pirate_02`, inactive) built like the knight allies (NavMeshAgent, Hittable
  Heroes not defeatable, KidAnimator, WeaponHolder with `SM_Wep_Sword_Wood_01`, `KnightAllyAI` disabled); the parrot.
  All on the Kids layer where kids are.
- Player: add `PirateCostume` (prefabs found by name).
- UI: `TreasureMapUI` on the touch canvas (parchment-coloured panel sprite from ModularGameUIKit, `Inter-Bold`
  not needed; no text). The tick sprite: find `Check`/`Tick` by name, fallback a green circle.
- **Map icons:** render each landmark prefab to a 256×256 transparent PNG at build time with a temporary camera and
  `RenderTexture` (3/4 view, bounds-framed), save to `Assets/CoolSchool/Pirates/MapIcons/<name>.png`, import as
  Sprite. Put this in `Editor/Pirates/PirateMapIconRenderer.cs`. Skip re-rendering if the file exists.
- Materials (`Assets/CoolSchool/Materials/Pirates/`): `PirateBone.mat`, `PirateBalloon_<n>.mat` (or one material +
  `MaterialPropertyBlock` colours, agent F's choice; tell B which), `PirateRedX.mat`, `PirateLock.mat` (gold),
  `PirateCannon.mat` (dark grey). Sea material only if the water prefab is missing.
- Cannon: primitive cylinder barrel (~0.9 m) on a box base with two cylinder wheels, on the bow, pointing north;
  `muzzle` at the barrel tip.
- Wire `RewardSystem.pirateAdventure` in `RewardsSetupBuilder` (one line, like `alienLanding`).
- Log one summary line: cove rect, ship pose, island centre, landmark positions, zone position.

## Voice lines (agent T adds them to `VoiceLineSeeder`)

Group = key without the trailing number. Parrot = Lorre.

| key | speaker | Dutch | English |
|---|---|---|---|
| `invite.pirates.1` | Teacher | Ahoi! Het piratenschip wacht op jou! | Ahoy! The pirate ship is waiting for you! |
| `pirates.start.1` | Parrot | Ahoi, kapitein! Ik ben Lorre de papegaai. Kom aan boord! | Ahoy, captain! I'm Lorre the parrot. Come aboard! |
| `pirates.crew.1` | Kid | Ahoi! Wij varen mee! | Ahoy! We're sailing with you! |
| `pirates.steer.1` | Parrot | Jij bent de kapitein! Stuur het schip naar het eiland! | You're the captain! Steer the ship to the island! |
| `pirates.balloons.1` | Parrot | Kijk! Een skelettenboot vol ballonnen! Tik op de knop. Boem! | Look! A skeleton boat full of balloons! Tap the button. Boom! |
| `pirates.cannon_hint.1` | Parrot | Tik op de knop, dan schiet het kanon! | Tap the button to fire the cannon! |
| `pirates.balloons_done.1` | Parrot | Alle ballonnen zijn knal! De skeletten varen naar het eiland! | All the balloons popped! The skeletons are sailing to the island! |
| `pirates.arrive.1` | Parrot | Land in zicht! Iedereen van boord! | Land ahoy! Everybody off the ship! |
| `pirates.skeletons.1` | Goblin | Arrr! Wij zijn de skelettenpiraten! | Arrr! We're the skeleton pirates! |
| `pirates.fight_hint.1` | Parrot | Tik op de knop en zwaai met je zwaard! | Tap the button and swing your sword! |
| `pirates.skeleton_friend.1` | Goblin | Hihi, mijn botjes kriebelen! | Hehe, my bones tickle! |
| `pirates.skeleton_friend.2` | Goblin | Oeps, ik val uit elkaar! Mag ik bij jullie? | Oops, I fell apart! Can I join you? |
| `pirates.skeleton_friend.3` | Goblin | Klik klak, ik ben weer heel! Vrienden? | Clickety-clack, I'm back together! Friends? |
| `pirates.map.1` | Parrot | Een schatkaart! Volg de plaatjes naar de schat! | A treasure map! Follow the pictures to the treasure! |
| `pirates.landmark.1` | Parrot | Goed zo! Nu de volgende! | Well done! Now the next one! |
| `pirates.landmark.2` | Parrot | Gevonden! Verder maar! | Found it! Keep going! |
| `pirates.dig.1` | Parrot | Hier is de X! Tik op de knop om te graven! | Here's the X! Tap the button to dig! |
| `pirates.lock.1` | Parrot | De kist zit op slot! Het slot gaat open met de… | The chest is locked! The lock opens with the… |
| `pirates.lock_new.1` | Parrot | Dit is een nieuwe letter! Luister maar: | This is a new letter! Listen: |
| `pirates.lock_right.1` | Parrot | Klik! Het slot is open! | Click! The lock is open! |
| `pirates.lock_wrong.1` | Parrot | Bijna! Luister nog eens: | Almost! Listen again: |
| `pirates.count.1` | Parrot | Goud! Tik op elke munt, dan tellen we samen! | Gold! Tap every coin and we'll count together! |
| `pirates.how_many.1` | Parrot | Hoeveel munten hebben we gevonden? | How many coins did we find? |
| `pirates.victory.1` | Parrot | Hoera! We hebben de schat! Piratenfeest! | Hooray! We found the treasure! Pirate party! |

After the build, the main agent runs the batch voice generation for the new lines.

## Coordination

- **Castle session** (another Claude session, "Scaled cardboard castle for knights battle") owns
  `KnightsArenaBuilder.cs`, the `ArenaDepth` change and the order of `BuildEnvironment()`, plus `CastleCutaway`,
  `DragonBoss`, `KnightsGame` and `ActivityInviter`. We don't touch those files' logic. Our only edits in
  `PlaygroundPrototypeBuilder.cs` are the `EnvironmentInfo.PirateCove` field, the two call lines, and making
  `BuildTileGrid` internal if needed. The cove's ground is reserved for us: x from the playground's east fence line
  (20.9) to `MaxX`, z from `MaxZ` (20.9) to `MaxZ + 40`, plus the 3 m north-fence gap at x ≈ 22–25. The dragon's
  `DragonEntry` (high above the back wall, ~8 m north of it) lies in the air over our dock; that's fine.
- **Agent F owns:** `PirateAdventure*.cs`, `PirateShip`, `ShipCannon`, `BalloonTarget`, `SkeletonBoat`,
  `SkeletonPirateAI`, `BonePile`, the `PlayerController` riding methods, `RewardSystem` pirate subscription,
  `PlayerProgress.pirateCoinsTotal`.
- **Agent T owns:** `ParrotGuide`, `ShoulderParrot`, `PirateCostume`, `TreasureMapUI`, `DigSpot`, `TreasureChest`,
  `TreasureCoin`, `TreasureCoinPicker`, `TreasureTrophy`, and the shared small edits listed under agent T.
- **Agent B owns:** `Editor/Pirates/*`, the hook lines in `PlaygroundPrototypeBuilder`, the one line in
  `RewardsSetupBuilder`.
- Several sessions edit shared files (`PlaygroundPrototypeBuilder`, `PlayerController`, `VoiceLineSeeder`,
  `SoundCues`, `RewardSystem`). **Re-read each shared file right before every edit**, keep edits small and local,
  and never reformat or reorder other code.
- Nobody runs the batch build or commits; the main agent does.
