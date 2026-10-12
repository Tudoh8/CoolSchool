# Soapbox Race ("Zeepkistrace"): implementation contract

Date: 2026-10-12. A Temple-Run-style lane runner on a new street north of the playground (the start of the
neighbourhood). Four agents build it in parallel:

- **Agent R (race core):** the activity flow, the cart, the input, the chase camera, the HUD logic, the rival AI.
- **Agent T (track):** the track, every track item (obstacles, coins, ramps, animals, learning gates).
- **Agent C (shared content):** voice, sound cues, parent row, reward catalog + cart looks, sitting animation,
  race helmet, the announcer component.
- **Agent W (world builder):** the editor builder that places the street and wires everything.

Read first: `Docs/HANDOVER.md`, `Docs/specs/pirate-cove-contract.md` (the closest example of a section: same
patterns for zones, builders, voice, parent rows, rewards), then the files you touch.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it only fails on a type another agent owns, wait about a minute and
retry, for up to about 15 minutes. Never create another agent's types.

**Player:** a 4-year-old who can't read. Everything is spoken; he never fails; no text UI (digits and letter glyphs
are fine). The race never stops for a mistake.

## User decisions (2026-10-12)

- **Battle =** race against **2 classmate rivals** in their own soapbox carts. Bumping a rival makes it spin out,
  giggling. Rubber-banded: it's always close and **he usually wins**; on the podium everyone celebrates and he gets
  stars even when 2nd.
- **Controls:** tap the **left / right half** of the screen = one lane over (3 lanes). The big action button =
  **jump**. The joystick is hidden during the race. (Keyboard A/D/arrows + Space in the editor.)
- **Crashes:** wobble and slow down a little (and drop a coin), never stop, no game over.
- **Length:** one fixed, hand-made track, about 60–75 s, with a finish arch.
- **Learning:** **number gates** and **letter-sound gates**: three arches, one per lane; the announcer says "Rij
  door de drie!"; right lane = sparkle + bonus coins; wrong lane = no bonus, the right arch flashes. No counting
  at the finish.
- **Obstacles:** cardboard boxes & cones, puddles & sprinklers, animals crossing (they always hop aside), ramps for
  big slow-motion jumps with coins in the air.
- **Difficulty:** adaptive `race` level 1–3 with a slider in the parent area.
- **Cart looks:** unlockable presents (flag, stickers, other cart bodies, a golden cart).
- **Voice:** a **new announcer character** (new speaker `Announcer`), a friendly neighbour with a chequered flag.
- **Animals:** the user imported **polyperfect Low Poly Animated Animals** (`Assets/polyperfect/...`). Its materials
  are Built-in Standard (they render pink/wrong in URP) and its prefabs carry polyperfect wander scripts.

## Coordinates and terms

- The camera looks north (+Z). **Lane 0 = left on screen = west (−X)**, lane 1 = middle, lane 2 = right (east).
- `StreetX = -16`. Lane centres x = `StreetX + (lane - 1) * LaneWidth`, `LaneWidth = 2.2`.
- **Distance** `d` = metres along the track from the start line. The track is straight along +Z:
  world z = `StartLineZ + d`, `StartLineZ = 30`. `FinishDistance = 450` (z = 480). Carts brake in the run-out
  (d 450..470), the podium stands at d ≈ 478 (z ≈ 508). The street ends at z ≈ 525 (a hedge, a turning circle).
- Always go through `RaceTrack.PointAt(d, xOffset)` / `RaceTrack.Forward` rather than assuming +Z, so the track
  could curve later. (Today: `PointAt = startLine.position + Vector3.forward * d + Vector3.right * xOffset`.)

## Flow (`CoolSchool.Race.SoapboxRace : Activity`, agent R)

1. **Begin (start area).** The player walked into the `RaceZone` by the start line.
   - `Sfx.PlayMusic("music.race")`. Player costume: `RaceHelmet.Show(true)`. Weapons hidden, `JumpEnabled = false`
     (walking jump; the race jump is the cart's).
   - The announcer turns to the player and says `race.welcome`.
   - **Board:** `player.RideOn(playerCart.Seat)` then `KidAnimator.SetSitting(true)` (the rivals already sit in
     their carts, helmets on, waving now and then). Joystick hidden (`joystick.gameObject.SetActive(false)`),
     action button `ResetIcon()` (the default icon is the jump arrow).
   - Camera: disable `FollowCamera`, enable `RaceChaseCamera` (blends from the current pose over 1.2 s).
   - Cart looks: `playerLooks.ApplyPlayerLooks(ProgressStore.Current)`; the rivals get the two bodies the player
     doesn't use (`CartLooks.SetBody`).
   - Track: `track.Prepare(level)` (activates the items for this level, resets everything).
   - **How-to** (`race.howto`) when `!PlayerProgress.raceHowtoHeard` or level 1: HUD shows the two tap arrows
     (left/right halves, pulsing) and a jump hint on the button; set `raceHowtoHeard = true`, save. The arrows fade
     after the first two lane changes or 12 s into the race.
2. **Countdown.** HUD big digits 3, 2, 1 (one per second), each with `race.count3` / `race.count2` /
   `race.count1` (interrupt) and `Sfx.Play("race.countdown_beep")`; then `race.go` + `race.go_horn`; the announcer
   waves the flag (`announcer.WaveFlag()`). All three carts `StartRunning()`. Input on.
   - The announcer then moves to `announcerFinishSpot` (`announcer.MoveTo`, out of view behind the camera).
3. **Race.** Per frame:
   - Player target speed = level speed (table), × 0.75 while a gate is announced and not passed yet (time to
     think), ramps keep their own speed.
   - `RaceInput.LaneChange` → `playerCart.ChangeLane(dir)`; `RaceInput.Jump` → `playerCart.Jump()`.
   - **Bump:** when the player changes lane into a lane where a rival is within ±1.6 m of distance, the rival
     `SpinOut()`s (giggle `race.bump`, Kid voice, at most once per 6 s), the player gets a tiny wobble only.
   - HUD: coin count, current place (1–3, a medal-coloured digit), progress strip (three cart dots along a bar).
   - Announcer lines from track events: `race.crash` (≤ 1 per 8 s, 50%), `race.splash` (≤ 1 per 8 s), animal
     warnings `race.animal_<kind>` when an animal starts crossing ahead, `race.ramp` on the first ramp each race,
     `race.final` at d = 380, a rival taunt `race.rival` (Kid) once at d ≈ 60 and once when a rival overtakes
     (≤ 1 per 20 s). Never talk over a gate prompt (track `VoicePlayer.IsSpeaking` + a "gate busy" flag).
4. **Gates.** `track.Gates` (in distance order, only the active ones). Before the race, `SoapboxRace` builds a
   question per gate (below) and calls `gate.Setup(options[3], correctLane, kind)`.
   - When `gate.Distance - playerCart.Distance <= playerCart.Speed * GateLeadSeconds` (5 s): HUD target bubble shows
     the digit / letter big at the top centre; voice: number → `race.gate_number.<n>` (interrupt); letter →
     `race.gate_letter` then `letter.sound.<x>` (queued right after). The gate's arches light up.
   - `gate.Passed(lane)` (raised by the gate) → right: `gate.Celebrate()` (confetti + sparkle), +5 coins flying to
     the HUD (`race.coin` ×5 quickly), `race.gate_right`, `Sfx.Play("race.gate_ding")`. Wrong: `gate.ShowRight()`
     (the right arch flashes), `race.gate_wrong` — gentle, no penalty. Count right/wrong. Hide the bubble.
   - **Question choice.**
     - Order: gate 1 number, gate 2 letter, gate 3 number. A letter gate becomes a number gate when
       `ProgressStore.Current.lettersIntroduced` is empty.
     - **Numbers:** max = `{3,5,5,7,10}[numbersLevel-1]` (`GetLevel("numbers")`, clamped 1..5; mirror
       `NumbersLesson.MaxNumberByLevel`, copy the table with a comment). Target random 1..max, two distinct
       distractors from 1..max (if max < 3 use 1..3). Not the same target twice in one race.
     - **Letters:** like the pirate lock: from `lettersIntroduced`; the child's name's first letter (if
       introduced) with 50% chance, otherwise random. Distractors from `LetterCurriculum.SchoolOrder` without the
       target; below race level 3 skip `LetterCurriculum.AreSimilar(target, d)`. Lowercase.
     - The right lane is random; at level 1 it's never the lane the player's cart is in when the prompt starts
       (so he has to steer, but just one lane).
5. **Finish.** Each cart's `FinishedAt` is recorded when it crosses `FinishDistance`. All carts brake to a stop in
   the run-out (`StopRunning(brake: true)`). Places = order of crossing.
   - Input off, arrows/bubble hidden, `Sfx.Play("race.cheer")`, `BattleEffects.Confetti` over the finish arch.
   - **Podium.** The player `StopRiding(podiumSpots[place-1])` (sitting off first: `SetSitting(false)`), the rivals
     hop out to their places (`RivalCartAI.HopOutTo(point)`). The announcer (now at the finish) says `race.win`
     (1st) or `race.second` (2nd/3rd). Everyone cheers / dances (`KidAnimator.PlayCheer` then `SetDancing`),
     confetti on the podium. The chase camera moves to `podiumCameraSpot` (look at the podium) over 1 s.
   - **Stars:** `stars = Mathf.Clamp(2 + gatesRight, 1, 5)` → raise `RaceFinished(stars, podium position)`; add
     the race's coins to `PlayerProgress.raceCoinsTotal`, save.
   - **Adaptive level:** 0 wrong gates and ≤ 2 crashes → level +1 (max 3); ≥ 2 wrong gates or ≥ 6 crashes → −1
     (min 1). `GetLevel("race")` / `SetLevel("race", n)`, save.
   - After 5 s: `race.end`, a sparkle, and everyone is back at the start: the player teleported (CharacterController
     off → position → on) to `returnPoint` (start area, facing south toward the playground gap is fine but facing
     north keeps the camera sane — face north), carts and rivals reset at the start line. Then `End()`.
6. **OnEnd** restores everything, also when ended mid-race: the player off the cart (`SetSitting(false)`, then
   `StopRiding(returnPoint)` if riding, otherwise teleport), helmet off, joystick on, button icon reset, walking
   jump on, `RaceChaseCamera` off + `FollowCamera` on + `Snap()`, all carts reset (`ResetToStart`), rivals seated
   at the start, `track.ResetAll()`, HUD hidden, the announcer back at `announcerStartSpot`, music back (the
   ActivityManager already does this).

### Level table

| | 1 | 2 | 3 |
|---|---|---|---|
| player cruise speed (m/s) | 6.0 | 7.0 | 8.0 |
| active track items | `minLevel <= 1` | `<= 2` | `<= 3` |
| gates | 2 | 3 | 3 |
| chance he wins (rivals' final settle) | 0.9 | 0.8 | 0.7 |
| rival dodge chance (per obstacle) | 0.85 | 0.9 | 0.9 |

## Components

All runtime types are in namespace `CoolSchool.Race`, folder `Assets/CoolSchool/Scripts/Race/`, unless noted. The
builder wires the serialized fields **by these exact names**.

### Agent R

```csharp
public class SoapboxRace : Activity          // split into partial files if > ~400 lines (SoapboxRace.Gates.cs, .Finish.cs)
{
    [Header("Player")]
    [SerializeField] PlayerController player;
    [SerializeField] KidAnimator playerAnimator;
    [SerializeField] WeaponHolder playerWeapons;
    [SerializeField] RaceHelmet helmet;              // CoolSchool.Characters, agent C
    [SerializeField] FloatingJoystick joystick;
    [SerializeField] ActionButton actionButton;
    [SerializeField] FollowCamera followCamera;
    [SerializeField] RaceChaseCamera chaseCamera;    // on the main camera, disabled at start

    [Header("Race")]
    [SerializeField] RaceTrack track;
    [SerializeField] CartController playerCart;
    [SerializeField] CartLooks playerLooks;
    [SerializeField] RivalCartAI[] rivals;           // 2
    [SerializeField] RaceInput input;
    [SerializeField] RaceHud hud;
    [SerializeField] RaceAnnouncer announcer;        // agent C
    [SerializeField] Transform announcerStartSpot;
    [SerializeField] Transform announcerFinishSpot;
    [SerializeField] Transform[] podiumSpots;        // 3: 1st (middle, highest), 2nd, 3rd
    [SerializeField] Transform podiumCameraSpot;
    [SerializeField] Transform returnPoint;          // start area, on the NavMesh
    [SerializeField] float gateLeadSeconds = 5f;

    public event System.Action<int, Vector3> RaceFinished;   // stars, where
}
```

- `CartController` (on each cart root; the player cart and both rival carts):
  ```csharp
  [SerializeField] Transform seat;             // where the kid's root sits (SitGround pose: hips on the seat)
  [SerializeField] Transform model;            // child that wobbles / tilts / spins (the bodies live under it)
  [SerializeField] Transform[] wheels;         // optional, spun by speed (may be empty: Synty carts are one mesh)
  [SerializeField] float laneChangeSeconds = 0.22f;
  [SerializeField] float jumpHeight = 1.4f;
  [SerializeField] float jumpSeconds = 0.75f;
  [SerializeField] float acceleration = 4f;    // m/s² toward TargetSpeed
  public RaceTrack Track { get; set; }
  public Transform Seat { get; }
  public int Lane { get; }                     // 0..2 (target lane once a change starts)
  public float LaneX { get; }                  // current lateral offset (smoothed)
  public float Distance { get; }
  public float Height { get; }                 // above the road
  public bool IsAirborne { get; }
  public float Speed { get; }
  public float TargetSpeed { get; set; }
  public bool Running { get; }
  public bool Finished { get; }
  public float FinishTime { get; }
  public int Crashes { get; }
  public bool IsPlayer { get; set; }
  public event System.Action<CartController> CrossedFinish;
  public void PlaceAtStart(int lane);          // pose at the start line, speed 0, not running
  public void StartRunning();
  public void StopRunning(bool brake);         // brake: decelerate to 0 over ~1.5 s
  public bool ChangeLane(int direction);       // -1 / +1; at the edge: a small side-wiggle, returns false
  public bool Jump();                          // only when not airborne
  public void Launch(float height, float seconds);   // ramps: a big arc, slow tilt, coins in the air
  public void Bonk(float speedFactor, float seconds); // crash: wobble + slow, Crashes++
  public void Splash(float speedFactor, float seconds); // like Bonk but a happy shake, not counted as a crash
  public void Brake(float speedFactor, float seconds);  // animals: a short brake dip, no crash
  public void SpinOut();                        // rivals: a 360° spin of the model, falls back ~4 m, recovers
  public void ResetToStart(int lane);
  ```
  Movement is kinematic (no Rigidbody, no physics collisions): each frame `Distance += Speed * dt`, pose =
  `Track.PointAt(Distance, LaneX) + up * Height`, facing `Track.Forward`, a little roll into lane changes, a slight
  bob. It calls `Track.Sweep(this, previousDistance, Distance)` each frame **after** moving (agent T decides what
  happens). It crosses the finish when `Distance >= Track.FinishDistance`. `Sfx.Play("race.lane")` on a lane change
  (player only), `race.jump` / `race.land` for the player.
- `RaceInput` (on the race root): `public bool Active { get; set; }`, `public event System.Action<int> LaneChange;`
  (−1 / +1), `public event System.Action Jump;`.
  `[SerializeField] ActionButton actionButton; [SerializeField] float swipePixels = 60f;`
  New Input System only (`Pointer.current`, `Touchscreen.current` for multi-touch, `Keyboard.current`). A press that
  starts over a UI element (`EventSystem.current.IsPointerOverGameObject(pointerId)`: the action button, the parent
  gear, the star jar) is ignored for lanes. **Lane change on press:** left half → −1, right half → +1 (instant
  response matters more than swipes; swipes are not supported in v1, `swipePixels` is reserved). Each touch counts
  once (multi-touch: each new finger is its own press). The action button's `Pressed` → Jump. Keyboard: A/Left → −1, D/Right → +1, Space/W/Up → Jump. All gated by
  `ParentGate.IsOpen` and `Active`.
- `RaceChaseCamera` (on the main camera, `enabled = false` at start):
  ```csharp
  [SerializeField] Vector3 offset = new Vector3(0f, 3.0f, -6.0f);   // relative to the cart along Track.Forward
  [SerializeField] float lookAhead = 6f;
  [SerializeField] float lookHeight = 1.0f;
  [SerializeField] float lateralFollow = 0.6f;   // 0..1 how much the camera follows the cart's lane offset
  [SerializeField] float smoothTime = 0.15f;
  public CartController Target { get; set; }
  public void BlendIn(float seconds);            // from the camera's current pose
  public void MoveTo(Transform spot, float seconds);   // podium view; stops following
  ```
- `RaceHud` (on the touch canvas; UI built by agent W, no text except digits/letters with `Inter-Bold`):
  ```csharp
  [SerializeField] GameObject root;              // everything; hidden outside the race
  [SerializeField] Text countdownText;           // centre, huge digit
  [SerializeField] RectTransform coinCounter;    // top left (right of the parent gear), coin icon + digits
  [SerializeField] Text coinText;
  [SerializeField] Image placeBadge;             // top right of the counter: circle tinted gold/silver/bronze
  [SerializeField] Text placeText;               // "1" / "2" / "3"
  [SerializeField] RectTransform progressBar;    // top centre strip
  [SerializeField] RectTransform[] progressDots; // 3: player first (bigger), then the rivals
  [SerializeField] RectTransform targetBubble;   // top centre under the strip
  [SerializeField] Text targetText;              // the digit or lowercase letter
  [SerializeField] Image leftArrow, rightArrow;  // how-to arrows on each half
  public void Show(bool show);
  public void Countdown(int n);                  // n = 3,2,1 → digit pop; 0 → hide
  public void SetCoins(int coins, bool pop);
  public void FlyCoinsFromWorld(Vector3 world, int count);   // little coin icons arc into the counter
  public void SetPlace(int place);
  public void SetProgress(float player01, float rivalA01, float rivalB01);
  public void ShowTarget(string glyph);          // null/empty hides
  public void ShowArrows(bool show);
  ```
- `RivalCartAI` (on each rival cart, next to its `CartController`):
  ```csharp
  [SerializeField] CartController cart;
  [SerializeField] KidAnimator driver;          // the seated rival kid (child of the cart seat)
  [SerializeField] float lookAhead = 18f;
  [SerializeField] float gapAmplitude = 5f;     // rubber band: desired gap to the player oscillates ±this
  [SerializeField] float gapPeriod = 14f;       // seconds; each rival gets its own phase
  public CartController PlayerCart { get; set; }
  public void Prepare(int level, bool playerWins, int rivalIndex);   // decided before the start (level table)
  public void Begin();                          // starts the cart, rubber banding, lane AI
  public void HopOutTo(Vector3 point);          // to the podium: driver unparented, hop, stands, SetSitting(false)
  public void SeatDriver();                     // back in the cart, sitting
  public void ResetRival(int lane);
  ```
  Rubber band: target speed = player speed + k·(player.Distance + desiredGap − cart.Distance), clamped to
  0.6–1.35 × the level speed. In the last 60 m the "settle": if `playerWins`, desiredGap goes to −2.5 / −4 m
  (behind); otherwise rivalIndex 0 settles 1.5 m **ahead**, the other behind. Lane AI: look ahead `lookAhead` m for
  items that block its lane (`RaceTrack.Blocks(lane, fromD, toD)`); with the dodge chance switch to a free lane,
  otherwise jump if `item.Jumpable` (70%) or take the bonk (fun). Never move into the player's lane while within
  ±3 m of the player's distance; if the rubber band would put it inside the player's cart (same lane, |gap| < 2.5),
  change lane, or hold the gap. Gate lanes: random. Wave/cheer now and then through `driver` (upper body only:
  `PlayCheer` is fine when it doesn't break the sitting pose; otherwise skip it).

**Shared small edits (agent R):**
- `PlayerProgress`: `public bool raceHowtoHeard;`, `public int raceCoinsTotal;`.
- `RewardSystem`: `[SerializeField] SoapboxRace soapboxRace;` and subscribe to `RaceFinished` →
  `AddStars(stars, position)` after `rewardDelaySeconds`, exactly like the pirate subscription.

### Agent T

```csharp
public class RaceTrack : MonoBehaviour
{
    [SerializeField] Transform startLine;         // position = lane 1 at d = 0, forward = +Z
    [SerializeField] float laneWidth = 2.2f;
    [SerializeField] float finishDistance = 450f;
    [SerializeField] Transform itemsRoot;          // all RaceTrackItems are children (any depth)
    public float LaneWidth { get; }
    public float FinishDistance { get; }
    public Vector3 Forward { get; }
    public Vector3 PointAt(float distance, float xOffset);
    public float LaneOffset(int lane);            // (lane - 1) * laneWidth
    public CartController PlayerCart { get; set; }
    public IReadOnlyList<LearningGate> Gates { get; }   // active ones, by distance
    public int Level { get; }
    public event System.Action<RaceTrackItem, CartController> ItemHit;   // obstacles that bonked/splashed/braked a cart
    public event System.Action<AnimalCrosser> AnimalCrossing;            // an animal starts walking into the road
    public event System.Action<RaceRamp, CartController> RampTaken;
    public event System.Action<RaceCoin, CartController> CoinCollected;  // player only
    public void Prepare(int level);               // collects items (sorted by distance), activates minLevel <= level, ResetItem() each
    public void ResetAll();
    public void Sweep(CartController cart, float fromDistance, float toDistance);   // called by every cart each frame
    public bool Blocks(int lane, float fromDistance, float toDistance, out RaceTrackItem item);   // for the rival AI
}

public abstract class RaceTrackItem : MonoBehaviour
{
    [SerializeField] protected int lane = 1;        // first lane covered
    [SerializeField] protected int laneSpan = 1;    // 1..3
    [SerializeField] protected float length = 1f;   // along the track
    [SerializeField] protected int minLevel = 1;
    [SerializeField] protected float clearance = 0.8f;   // a cart higher than this passes over it
    public float Distance { get; }                  // set by the track in Prepare (from the position)
    public int MinLevel { get; }
    public bool Covers(int lane);
    public virtual bool Blocking => true;           // false for coins, gates, ramps
    public virtual bool Jumpable => clearance < 1.2f;
    public abstract void OnCartPassing(CartController cart, RaceTrack track);   // called once per cart when its front passes Distance (within length)
    public virtual void ResetItem() { }
}
```

Item types (each its own file):
- `RaceObstacle : RaceTrackItem` with `public enum ObstacleKind { Box, Cone, BoxStack, Puddle, Sprinkler }` and
  `[SerializeField] ObstacleKind kind; [SerializeField] Transform visual;`. When a cart passes in a covered lane
  with `Height < clearance`:
  - Box / BoxStack / Cone: the visual tumbles away (scripted arc + spin to the side, then it lies there; a
    Rigidbody is fine if it is reset), `Sfx.Play("race.bonk")`, `cart.Bonk(0.6f, 1.0f)` (cone: 0.75, 0.6),
    `ItemHit`. The player drops one coin (the race decides; coin −1, min 0, a coin pops out of the cart).
  - Puddle / Sprinkler: a water splash (`BattleEffects.Confetti(pos, light blue)` or a small particle burst),
    `race.splash`, `cart.Splash(0.85f, 0.6f)`, `ItemHit`.
  - Sprinkler: a rotating jet (2–3 stretched transparent light-blue capsules/quads) that sweeps across its lanes;
    the hit test is simply "covered lane".
  - Clearance: Box 0.8, Cone 0.7, Puddle 0.3, Sprinkler 0.9, BoxStack 1.8 (not jumpable: dodge it).
- `RaceCoin : RaceTrackItem` (`Blocking = false`), `[SerializeField] float height = 0.6f;` spins and bobs; picked
  when the cart's lane covers it and |cart.Height + 0.6 − height| < 1.0. Player: hide, `race.coin`,
  `CoinCollected`. Rivals: hide too (no event). Coins on ramps' arcs float at the arc height.
- `RaceRamp : RaceTrackItem` (`Blocking = false`), `[SerializeField] float launchHeight = 2.6f; [SerializeField] float airSeconds = 1.5f;`
  A cart in its lane (`Height < 0.3`) → `cart.Launch(...)`, `race.ramp_whoosh`, `RampTaken`.
- `AnimalCrosser : RaceTrackItem` with
  ```csharp
  [SerializeField] Animator animator;          // polyperfect controller: bools isWalking / isRunning / isJumping, set only if they exist
  [SerializeField] AudioClip sound;            // copied from the prefab's wander script by the builder
  [SerializeField] string kind = "cat";        // cat | dog | hen | rabbit (voice line race.animal_<kind>)
  [SerializeField] Transform sidewalkPoint;    // where it waits (west or east sidewalk)
  [SerializeField] Transform[] followers;      // optional: chicks following the hen
  [SerializeField] float triggerDistance = 30f;
  [SerializeField] float walkSpeed = 1.4f;
  ```
  Waits on the sidewalk (idle). When `PlayerCart` is within `triggerDistance`, it walks into the road to stand in
  its `lane` (followers trail behind it in a little line), raises `AnimalCrossing`. When **any** cart comes within
  ~3 m in its lane, it hops aside (a jump arc to the nearest free lane edge / back to the sidewalk, run animation,
  `sound` at its position) — it is never hit. If the hop happens for the player's cart: `cart.Brake(0.85f, 0.5f)`,
  `ItemHit`. Followers do the same hop. `ResetItem()` puts everything back on the sidewalk.
  `Blocking = true`, `Jumpable = true` (clearance 1.0: jumping over it is fine too, then it doesn't hop).
- `LearningGate : RaceTrackItem` (`Blocking = false`):
  ```csharp
  public enum GateKind { Number, Letter }
  [SerializeField] Text[] glyphs;               // 3, world-space canvas text on each arch's board (lane 0..2)
  [SerializeField] Renderer[] archGlow;         // 3, emissive strips that light up / flash
  public GateKind Kind { get; }
  public int CorrectLane { get; }
  public bool Announced { get; set; }
  public event System.Action<LearningGate, int, bool> Passed;   // gate, player lane, correct
  public void Setup(string[] options, int correctLane, GateKind kind);   // options per lane
  public void LightUp(bool on);
  public void Celebrate();                      // confetti + sparkle over the right arch
  public void ShowRight();                      // the right arch flashes 3×
  ```
  Raises `Passed` only for the player's cart. Covers all three lanes.
- `FinishArch` (not a track item): `[SerializeField] Transform banner;` `public void Celebrate();` (confetti bursts
  along the banner, `race.cheer`).

### Agent C

- **Voice:** `VoiceLine.Speaker` add `Announcer` **at the end** (serialized as an int). `VoicePlayer`:
  `[SerializeField, Range(0.5f, 2f)] float announcerPitch = 1.0f;` in `PitchFor`; the announcer may be interrupted
  like Kid/Goblin/Alien/Parrot. `VoiceGenerationSettings`: `announcerVoice = "Dave"` (wire it where the other
  speakers map to voices in the generator).
- `VoiceLineSeeder`: the lines below.
- `SoundCues` (+ seeder) cues: `race.lane`, `race.jump`, `race.land`, `race.bonk`, `race.splash`, `race.coin`,
  `race.gate_ding`, `race.countdown_beep`, `race.go_horn`, `race.ramp_whoosh`, `race.spinout`, `race.cheer`,
  `race.brake`, `music.race`. `SoundAutoAssign` synonyms: lane → whoosh, swish, swipe; jump → jump, hop, boing;
  land → land, thud; bonk → bonk, cardboard, box, hit, impact; splash → splash, water, puddle; coin → coin, pickup,
  collect; gate_ding → ding, chime, correct, success; countdown_beep → beep, countdown, tick; go_horn → horn, start,
  air horn; ramp_whoosh → whoosh, swoosh, ramp; spinout → spin, skid, tyre; cheer → cheer, crowd, applause;
  brake → brake, skid, squeak; music.race → race, fast, upbeat, chase.
- **Parent area:** a row `ParentStrings.RowRace` (`row.race`, "Zeepkistrace" / "Soapbox race"), skill `"race"`,
  max 3, below the pirates row; grow the window if it doesn't fit (the builder in `Editor/Parent/` makes the rows).
- **Rewards:** `RewardKind` add `Cart` **at the end**. `RewardCatalogSeeder` inserts:
  `cart.flag` (Cart, `SM_Prop_Flag_01`) after `plush.dino`; `cart.racer_2` (Cart, `SM_Veh_Soapbox_Racer_02`) after
  `shield.round`; `cart.stickers` (Cart, `CS_Cart_Stickers` — a prefab agent W generates) after `hat.cap`;
  `cart.racer_3` (Cart, `SM_Veh_Soapbox_Racer_03`) after `plush.bear`; `cart.gold` (Cart, `CS_Cart_Gold`, generated)
  at the end. If the generated prefab doesn't exist yet when the seeder runs, it logs and leaves the prefab empty
  (the race builder runs earlier in the same build, so normally it exists). Check how `PresentPopup`/`RevealStage`
  frame a ~1.6 m cart (revealScale) and set a sensible `revealScale`/`revealEuler` per item.
- `CartLooks` (Scripts/Race, on each cart):
  ```csharp
  [SerializeField] GameObject[] bodies;        // 3: racer 01, 02, 03 (children of the cart's model), one active
  [SerializeField] GameObject flag;            // inactive by default
  [SerializeField] GameObject stickers;        // inactive by default
  [SerializeField] Material goldMaterial;      // replaces every body renderer's materials when gold is on
  public int BodyIndex { get; }
  public void SetBody(int index);
  public void SetExtras(bool flag, bool stickers, bool gold);
  public static int PlayerBodyIndex(PlayerProgress progress);   // cart.racer_3 → 2, else cart.racer_2 → 1, else 0
  public void ApplyPlayerLooks(PlayerProgress progress);       // body + extras from the unlocked cart.* ids
  ```
  Cache the original materials so gold can be switched off again.
- `KidAnimator` (Scripts/Characters): `public void SetSitting(bool sitting)` (bool parameter `Sitting`, ignored if
  missing, like the others). `KidAnimatorBuilder`: a `Sit` state on the base layer with
  `HumanM@SitGround01 - Loop` (fallback `HumanF@SitGround01 - Loop`, then `HumanM@SitLow01 - Loop`), entered from
  Any State while `Sitting` is true (short blend, can't re-enter itself), back to locomotion when false. The upper
  body layer must not fight it (cheer/wave on the upper body is fine). Report the clip used. Make sure
  `PlayerController`'s riding `ForceIdle` doesn't override sitting (sitting wins).
- `RaceHelmet` (namespace `CoolSchool.Characters`, Scripts/Characters; like `PirateCostume`):
  `[SerializeField] GameObject helmetPrefab; [SerializeField] Vector3 localPosition, localEuler; [SerializeField] float localScale = 1f;`
  `public void Show(bool show);` on the Head bone. The player gets `SM_Chr_Attach_Helmet_Cardboard_01`; rivals get
  their own `RaceHelmet` with `..._02` and the robot helmet `SM_Chr_Attach_Helmet_Cardboard_Robot_01`, shown always.
- `RaceAnnouncer` (Scripts/Race, on the announcer, `SM_Chr_Father_01` with the Teacher animator controller):
  ```csharp
  [SerializeField] Animator animator;
  [SerializeField] Transform flag;             // chequered flag in the right hand (built by W)
  public void FaceTowards(Vector3 point);      // smooth yaw
  public void WaveFlag();                      // flag swings for 2 s (+ a cheer/wave trigger if the controller has one)
  public void Cheer();
  public void MoveTo(Transform spot);          // teleport with a sparkle
  ```
  Between races it stands at the start, faces the player when near (≤ 6 m) and waves the flag every ~8 s.

### Agent W: `Assets/CoolSchool/Editor/Race/SoapboxStreetBuilder*.cs`

```csharp
internal static class SoapboxStreetBuilder
{
    internal sealed class StreetLayout { /* everything Build() needs from BuildStreet() */ }
    internal struct RaceInputs
    {
        public RuntimeAnimatorController KidController;
        public RuntimeAnimatorController TeacherController;   // load the asset by path like the school does
        public int KidsLayer;
        public PlayerController Player;
        public FloatingJoystick Joystick;
        public ActionButton ActionButton;
    }
    internal static StreetLayout BuildStreet(Transform root, PlaygroundPrototypeBuilder.EnvironmentInfo info);   // static geometry, before the NavMesh bake
    internal static void Build(StreetLayout layout, RaceInputs inputs);   // carts, kids, track items, UI, wiring; after the pirate cove
}
```

- `PlaygroundPrototypeBuilder`: add `public SoapboxStreetBuilder.StreetLayout RaceStreet;` to `EnvironmentInfo`; in
  `BuildEnvironment()` call `info.RaceStreet = SoapboxStreetBuilder.BuildStreet(root, info);` right after the
  pirate cove line (before benches/greenery, so `ReservedAreas` keeps them out); in `RunBuild()` call
  `SoapboxStreetBuilder.Build(...)` right after the pirate cove. Nothing else in that file.
- **Entrance:** a 4 m gap in the main north fence (z = `info.MaxZ`) centred on x = `StreetX`; remove the fence
  pieces whose bounds overlap it after `BuildFence` (like the pirate cove does). Check the ice cream stall's
  reserved rect doesn't overlap the gap; log it.
- **Area:** west of the school (school footprint x −6.6..6.6, z 22.7..33.5 — read `info.School.footprint`, keep
  ≥ 2 m clear) and north of it. Reserve x `StreetX − 18 .. StreetX + 14` for z from `MaxZ` to z ≈ 530 in
  `ReservedAreas` (minus nothing; the school is east of it until z ≈ 36, so on the east side below z = 40 keep the
  yard narrow: houses on the east side start at z ≥ 42). Grass tiles where the main grass doesn't cover (main grass
  = fence rect + 4 m and school footprint + 4 m).
- **Street:** Synty road tiles (`SM_Env_Road_01` etc., measured) 8 m wide centred on `StreetX` from z = `MaxZ` + 1
  to z ≈ 520; sidewalks (`SM_Env_Sidewalk_Straight_01`, fallback `SM_Env_Sidewalk_01`) both sides; a turning circle
  or a hedge wall at the north end. Flat (y = 0); align tiles by their measured flat surface (`GroundSurface`).
- **Houses:** `SM_Bld_House_Preset_01..11` alternating both sides, fronts facing the street (measure each preset's
  bounds and door side; if unknown, front = the side with the door prefab child), front edge ~5 m from the
  sidewalk, about every 22–26 m, with hedges (`SM_Env_Hedge_0x`), picket fences, trees, a few parked cars on
  driveways (never in the lanes), mailboxes/lamps if found. Static. **Performance:** these presets have many
  children; mark static, log the total renderer count of the street, and keep it reasonable (< ~6000 renderers).
- **Enclosure:** fence or hedges along the outer sides of the yards and the north end so nobody walks out of the
  world; a fence between the race area and the school's west side.
- **Start area** (d < 0): the `RaceZone` (ActivityZone, radius 2.0) at (StreetX, 0, StartLineZ − 4), beacon in a
  racing-red colour, icon found by name (try `Flag`, `Trophy`, `Car`, `Race`, fallback `Map-Target-Circle`). A
  start banner across the street at the start line (two posts + a chequered banner: a generated chequer texture
  asset `Assets/CoolSchool/Race/Chequer.png`, URP/Lit). The announcer on a small stand at the west sidewalk by the
  start line, `announcerStartSpot` there; `returnPoint` at (StreetX, 0, StartLineZ − 7) facing north. Inviter:
  add the zone with group `invite.race` (last).
- **Carts:** three carts at the start line, lane 0 rival A, lane 1 the player, lane 2 rival B. Each cart root:
  `CartController`, `CartLooks`, a `model` child holding the three bodies (`SM_Veh_Soapbox_Racer_01/02/03`, each
  scaled to ~1.7 m long, nose north, only one active), a flag (`SM_Prop_Flag_01` on a thin pole at the back, ~0.9 m
  high, inactive) and stickers (2 star quads on the sides using a star sprite from ModularGameUIKit, inactive).
  `seat` measured from body 01 (the seat's top surface, centred; the SitGround pose has the hips at the root).
  Rivals: `RivalCartAI`, a seated Synty kid (two distinct kids from the classmate set, `KidAnimator` +
  KidController, no NavMeshAgent, parented to the seat, `SetSitting(true)` via a sitting default — set the
  animator bool in `Start`, e.g. a tiny `SitOnStart` is not needed if `RivalCartAI.SeatDriver()` runs in Start),
  `RaceHelmet` on each. Gold material `Assets/CoolSchool/Materials/Race/CartGold.mat` (URP/Lit gold, metallic 0.8,
  smoothness 0.7).
- **Generated prefabs** for the reveal: `Assets/CoolSchool/Race/Prefabs/CS_Cart_Stickers.prefab` (racer 01 with the
  stickers) and `CS_Cart_Gold.prefab` (racer 01, gold), rebuilt each build.
- **Track items** (children of `TrackItems` under the track root), placed by a deterministic layout in
  `SoapboxTrackLayout.cs` (a static table of `(distance, kind, lane, laneSpan, minLevel)`). Rules:
  - Nothing in d 0..40 except coins; nothing blocking in the last 20 m before the finish.
  - Gates at d = 120 (L1), 250 (L1), 370 (L2). No blocking items from 30 m before to 10 m after a gate.
  - Ramps at d = 200 (L1, middle lane) and 320 (L2, a side lane), with coin arcs over them (5 coins along the arc);
    no blocking items 15 m after a ramp.
  - Animals: cat d = 90 (L1), hen + 3 chicks d = 285 (L1), dog d = 160 (L2), rabbit d = 410 (L3). Sidewalk side
    alternates.
  - Level 1: ~14 obstacle rows, at most one lane blocked per row, ≥ 14 m between rows. Level 2 adds ~8 rows (some
    block two lanes, one puddle across all three lanes), level 3 adds ~8 rows incl. 3 BoxStacks, rows ≥ 10 m apart.
    Never block all three lanes with non-jumpable items.
  - Coins: ~25 lines of 5 coins (1.5 m apart) in lanes that are free, all levels.
  - Visuals: Box `SM_Gen_Prop_Cardboard_Box_0x` (~0.7 m), BoxStack two boxes (~1.5 m), Cone `SM_Prop_Cone_01`
    (~0.7 m), Puddle a flat light-blue transparent ellipse (scaled cylinder, URP/Lit transparent) ~1.8 × 1.4 m,
    Sprinkler `SM_Prop_Sprinkler_01` + jets, Ramp `SM_Gen_Env_Road_Ramp_01` (fallback `SM_Prop_Ramp_01`, ~1.8 m
    wide, 0.5 m high), Coin `SM_Gen_Prop_Coin_01` (~0.45 m, standing up, gold, slight emission).
  - Gates: three arches per gate (two posts + a board, ~2 m wide, 2.6 m high), a world-space canvas on each board
    with an `Inter-Bold` `Text` (white, big, dark outline), an emissive strip (`archGlow`). Bright colours per
    arch (not as a teaching goal; just cheerful).
- **Animals** (polyperfect, found by name: `Cat_Orange`, `Dog_GoldenRetriever`, `Hen` + `Chick` ×3, `Rabbit_White`):
  instantiate, **unpack completely**, read the wander script's `animalSound` clip (SerializedObject on that
  MonoBehaviour, field `animalSound`) into `AnimalCrosser.sound`, then remove every MonoBehaviour that is not ours
  (and any NavMeshAgent/Rigidbody/Collider they bring). **URP materials:** for each Standard material they use, make
  a URP/Lit copy in `Assets/CoolSchool/Materials/Race/Animals/` (`_BaseMap` ← `_MainTex`, `_BaseColor` ← `_Color`,
  smoothness low) and assign the copies; never modify the polyperfect assets. Scale: cat ~0.45 m tall, dog ~0.7,
  hen ~0.45, chick ~0.18, rabbit ~0.35.
- **Finish:** the finish arch (`FinishArch`) at d = 450 with a chequered banner and bunting; the podium (three
  blocks 1st middle 0.9 m, 2nd left 0.6 m, 3rd right 0.4 m, gold/silver/bronze URP/Lit) at d ≈ 478 on the street
  centre, `podiumSpots` on top; `announcerFinishSpot` beside the podium; `podiumCameraSpot` ~7 m south of the
  podium at 3 m height looking at it; 4 cheering spectators (Synty kids, `KidAnimator`, dancing/cheering on a loop
  via a tiny `Spectator` component or by setting the animator on Start) on the sidewalks near the finish.
- **Camera:** add `RaceChaseCamera` (disabled) to the main camera. Check the camera's far clip plane (≥ 250 m is
  enough to see well up the street).
- **UI:** `RaceHud` on the touch canvas (all elements as in agent R's list; coin icon from ModularGameUIKit by name
  `Coin`, fallback a gold circle; arrows by name `Arrow`; place badge a circle). Below the parent gear and the
  reward popup in sibling order (the popup must stay on top).
- **Wiring:** `SoapboxRace` with all fields; `RewardSystem.soapboxRace` in `RewardsSetupBuilder` (one line, like
  `pirateAdventure`); `RaceInput.actionButton`; each `RivalCartAI.cart/driver`; `track.PlayerCart` is set by the
  race at runtime (but wire `RaceTrack.startLine/itemsRoot`).
- **Preview shots** (`PreviewCapture.BuildGroups()`): `race_start` (follow-camera view of the start area from the
  playground gap), `race_chase` (the chase-camera pose behind the player cart at the start line, and at d = 110 in
  front of the first gate), `race_street` (high overview), `race_seat` (close-up of a seated kid in a cart, side
  view), `race_animals` (each animal on its sidewalk spot), `race_podium`. Stage the player seated in the cart with
  `SetSitting` for `race_chase`/`race_seat`.
- Log one summary line: street rect, start line, finish, podium, zone, number of items per level, renderer count.

## Voice lines (agent C adds them to `VoiceLineSeeder`)

Group = key without the trailing number. Announcer = the new neighbour with the flag.

| key | speaker | Dutch | English |
|---|---|---|---|
| `invite.race.1` | Teacher | Wie doet er mee met de zeepkistrace? | Who wants to join the soapbox race? |
| `race.welcome.1` | Announcer | Welkom bij de grote zeepkistrace! Stap maar in je kar! | Welcome to the big soapbox race! Hop into your cart! |
| `race.howto.1` | Announcer | Tik links of rechts om te sturen. Tik op de knop om te springen! | Tap left or right to steer. Tap the button to jump! |
| `race.count3.1` | Announcer | Drie! | Three! |
| `race.count2.1` | Announcer | Twee! | Two! |
| `race.count1.1` | Announcer | Eén! | One! |
| `race.go.1` | Announcer | Start! | Go! |
| `race.gate_number.1.1` … `race.gate_number.10.1` | Announcer | Rij door de één! / twee / drie / vier / vijf / zes / zeven / acht / negen / tien | Drive through the one! … ten |
| `race.gate_letter.1` | Announcer | Rij door de letter… | Drive through the letter… |
| `race.gate_right.1` | Announcer | Ja, goed zo! | Yes, well done! |
| `race.gate_right.2` | Announcer | Super! Precies goed! | Super! Exactly right! |
| `race.gate_right.3` | Announcer | Knap gedaan! | Nicely done! |
| `race.gate_wrong.1` | Announcer | Bijna! Kijk, daar was hij! | Almost! Look, there it was! |
| `race.crash.1` | Announcer | Boem! Gelukkig is het zacht! | Boom! Good thing it's soft! |
| `race.crash.2` | Announcer | Oeps! Rij maar door! | Oops! Keep going! |
| `race.splash.1` | Announcer | Plons! Lekker nat! | Splash! Nice and wet! |
| `race.animal_cat.1` | Announcer | Kijk uit, een poes! | Watch out, a cat! |
| `race.animal_dog.1` | Announcer | Kijk uit, een hond! | Watch out, a dog! |
| `race.animal_hen.1` | Announcer | Kijk uit, een kip met kuikentjes! | Watch out, a hen with her chicks! |
| `race.animal_rabbit.1` | Announcer | Kijk uit, een konijn! | Watch out, a rabbit! |
| `race.ramp.1` | Announcer | Een schans! Hup, de lucht in! | A ramp! Up into the air! |
| `race.final.1` | Announcer | Bijna bij de finish! Zet hem op! | Almost at the finish! Go for it! |
| `race.win.1` | Announcer | Jij hebt gewonnen! Hoera voor de kampioen! | You won! Hooray for the champion! |
| `race.second.1` | Announcer | Wat een spannende race! Jullie waren allemaal supersnel! | What an exciting race! You were all super fast! |
| `race.end.1` | Announcer | Tot de volgende race! | See you at the next race! |
| `race.rival.1` | Kid | Ik ga je inhalen! | I'm going to catch you! |
| `race.rival.2` | Kid | Wie is er het snelst? | Who's the fastest? |
| `race.bump.1` | Kid | Wiee! Ik draai rondjes! | Wheee! I'm spinning! |
| `race.bump.2` | Kid | Hihi, botsauto! | Hehe, bumper car! |

(The number gate lines are ten separate groups `race.gate_number.<n>`, key `race.gate_number.<n>.1`.)

After the build, the main agent runs the batch voice generation for the new lines.

## Coordination

- **Agent R owns:** `SoapboxRace*.cs`, `CartController`, `RaceInput`, `RaceChaseCamera`, `RaceHud`, `RivalCartAI`,
  the `PlayerProgress` fields, the `RewardSystem` subscription.
- **Agent T owns:** `RaceTrack`, `RaceTrackItem`, `RaceObstacle`, `RaceCoin`, `RaceRamp`, `AnimalCrosser`,
  `LearningGate`, `FinishArch`.
- **Agent C owns:** `Speaker.Announcer` + `VoicePlayer`/`VoiceGenerationSettings`/generator voice wiring,
  `VoiceLineSeeder` lines, `SoundCues` (+ seeder, `SoundAutoAssign`), `ParentStrings`/parent row builder,
  `RewardKind.Cart`, `RewardCatalogSeeder`, `CartLooks`, `KidAnimator.SetSitting` + `KidAnimatorBuilder` Sit state,
  `RaceHelmet`, `RaceAnnouncer`.
- **Agent W owns:** `Editor/Race/*`, the hook lines in `PlaygroundPrototypeBuilder`, the line in
  `RewardsSetupBuilder`, the race shots in `PreviewCapture`.
- Shared files (`PlaygroundPrototypeBuilder`, `PlayerController`, `VoiceLineSeeder`, `SoundCues`, `RewardSystem`,
  `PlayerProgress`, `KidAnimator`, `PreviewCapture`): **re-read each right before every edit**, keep edits small and
  local, never reformat or reorder other code. Nobody else changes `PlayerController`; if you need something there,
  report it instead.
- Nobody runs the batch build or commits; the main agent does.
