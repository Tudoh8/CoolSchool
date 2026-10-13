# Castle Adventure: implementation contract

Date: 2026-10-13. A redesign of the knights game (`KnightsGame`) in the big cardboard castle. Today it is two goblin
waves and a dragon you bump until it flies away: no story, no choices, nothing to learn. It becomes a short adventure
with a mission, a few learning moments, varied goblins, a friendly dragon and a dragon ride.

Three agents build it in parallel:

- **Agent F (flow and fighting):** `KnightsGame` (rewritten as partial files), the goblin kinds, the thief, the
  counting perches, "help a friend", the portcullis hold, `BattleVoice`.
- **Agent M (moments and dragon):** the queen, the password, the wall patch, the cookies, the egg and baby dragon, the
  naming, the dragon's friendly mode and the ride, the name flag, the castle growth, the tower rescue, and all shared
  small edits (voice lines, sound cues, progress fields, parent row, reward catalog).
- **Agent B (builder):** the editor builder that places and wires everything.

Read first: `Docs/HANDOVER.md`, `Docs/specs/knights-slice-contract.md`, `Docs/specs/big-castle-contract.md` (the
castle geometry; its goblin door is outdated, goblins now climb ladders), `Docs/specs/pirate-cove-contract.md` (the
newest example of this kind of contract; the letter lock and coin count there are the pattern for our card questions),
`Docs/specs/harder-numbers-contract.md` (`NumberCurriculum`), `Docs/specs/letters-and-parent-area-contract.md`.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it only fails on a type another agent owns, wait about a minute and
retry, for up to about 10 minutes. Never create another agent's types.

**Player:** a 4-year-old who can't read. Everything is spoken; he never fails; nobody gets hurt (goblins tumble and
giggle). Controls stay one floating joystick plus the one action button, and tapping things on screen (like the
pirate coins). Every wait for the child has a hint after a while and a safety net that moves the game on.

## User decisions (2026-10-13)

- **Story:** a **queen** (a classmate dressed up) greets him at the gate and gives one **mission** per visit, rotating:
  guard the party **cake**, protect the golden **crown**, protect the dragon **egg**, or **rescue a knight** stuck on a
  tower.
- **Dragon:** not an enemy. It is hungry (or, in the egg mission, a sad mother looking for her egg). He helps it, they
  become friends, and he gets a **dragon ride** on a **fixed flight path** over the castle (tap = confetti fire).
- **Learning:** a mix of numbers and letters. Number moments: **count the goblins** on the wall, **feed the dragon** N
  cookies. Letter moments: a **password at the gate** (which picture starts with this sound), **his name on the
  flag**, **naming the baby dragon** by its first sound.
- **Prep step:** **patch the hole** in the wall with N blocks (counting along).
- **Fighting:** **different goblin types** (shield goblin, sneaky thief, big troll that shrinks, tiny one hiding in a
  barrel) and **help a friend** (a classmate knight calls for help). Not picked: pushing ladders over, a catapult.
- **Across visits:** the **castle grows** (a new flag or banner per win) and the egg mission earns a **baby dragon**
  plush buddy (Toy Room, buddy slot).
- **Length:** about 3–5 minutes.
- Kept from today: the walk-in castle, the ladders, the knights classmates, tap = sword (auto-aim), hold = shield, the
  golden sword power-up, the celebration at the gate, 5 stars at the end.

## One visit

### Choosing the visit (agent F, at `OnBegin`)

- **Mission:** the first visit ever (`castleVisits == 0`) is `Cake`. After that a random one of the four, never the same
  as `castleLastMission`. Save both (`castleVisits++`, `castleLastMission`) at `OnBegin`.
- **Number moment:** `Egg` mission → `Goblins` (count the goblins). Other missions → `Goblins` or `Cookies`, 50/50,
  never the same as last visit (`castleLastNumberMoment`). If counting is not unlocked yet
  (`!NumberCurriculum.IsUnlocked(NumberSkill.Counting)`) skip the questions but keep the counting along.
- **Letter moment at the end:** `Egg` mission and no baby dragon yet (`babyDragonLetter` empty) → `NameBabyDragon`.
  Otherwise → `NameFlag` if the child's name has at least one drawable letter, else none.
- **Password:** every visit.
- **Wall patch:** every visit (counting along only, no cards).

### Flow (`CoolSchool.Battle.KnightsGame : Activity`, agent F)

The zone moves **outside, in front of the gate** (agent B). The game starts there, with the portcullis held down.

1. **Begin.** As today: jump off, sword icon, golden sword check, imagination sparkle, `music.knights`, allies to
   fighting mode (but `KnightAllyAI` stays disabled until the first wave), hittables reset, opponents and dragon off.
   `portcullis.HoldClosed = true`. `playerInput.enabled = false` until the first wave. Pick the visit (above). Hide
   all mission props, then `missionProps.Show(mission)` (agent M; cake + slice / crown on its cushion / egg in its nest
   / nothing). Knight mission: the **last** ally is sent to the tower (`towerRescue.PlaceKnight(ally)`) and stays out
   of the fight until rescued.
2. **At the gate.** The player auto-walks to `gateTalkSpot` (reuse `WalkPlayerTo`) and faces north; his input is off.
   The queen waves (`queen.Wave()`), then `castle.queen_hello` and `castle.mission.<cake|crown|egg|knight>`.
   Knight mission: the stuck knight also calls `castle.tower_help` once.
3. **Password.** `yield return password.Run(level, letters, result)` (agent M; it says `castle.password_right`
   itself). Then `portcullis.HoldClosed = false` (the gate rises because the activity runs; `keepOpenWhileRunning`
   already does that).
4. **Into the courtyard.** Player input back on. The queen walks in to `queenSpot`. A guide ring and arrow
   (`guide.ShowAt(wall.StandPoint)`, agent M's `CastleGuideMarker`) shows where to go; `castle.wall` is said when he
   is there. After 25 s without arriving: walk him there (`WalkPlayerTo`).
5. **Wall patch.** `yield return wall.Run(blockCount, result)` (agent M). Input off during the moment, back on after.
   Guide hidden.
6. **Wave 1.** `playerInput.enabled = true`, allies' `KnightAllyAI.enabled = true`, `knights.start` (only the first
   time per visit; it explains the button). `onWaveStarted(1)`, `battle.wave_horn`, `knights.wave`.
   - **Number moment = Goblins:** the wave's goblins appear on the **wall perches** instead of the ladders
     (`ActivateOnPerch`), stand there and wave. When all are up (or after 4 s): player input off, allies paused
     (`KnightAllyAI.enabled = false`), `castle.count`, then the number-card question (`CardQuestion.AskNumber`,
     answer = goblins on the wall; count along = each perched goblin hops in turn while `number.<k>` plays).
     - If `NumberCurriculum.IsUnlocked(NumberSkill.OneMoreOneLess)` and 50%: **one more** (another goblin pops onto a
       free perch, `castle.count_more`) or **one less** (the last one hops back down behind the wall and vanishes,
       `castle.count_less`), then `castle.count_now` and a second question.
     - Then `castle.jump_down`; all perched goblins `JumpDown()` one after another (0.4 s apart); input and allies on.
   - Otherwise the goblins climb the ladders as today.
   - **Help a friend** (see below) if the level table says so.
   - The wave ends as today (all out, timeout, send home).
7. **Wave 2.** `onWaveStarted(2)`, horn, `knights.wave_more`. Ladders, plus the tiny goblin in a barrel (see goblin
   kinds) if the table has one. **Thief** in the cake/crown/egg missions: the wave's first `Normal` goblin becomes the
   thief. Help a friend. Level 3 has no third wave (keep the visit short).
8. **Tower rescue** (knight mission only). `castle.rescue`, then `yield return towerRescue.Run(player)` (agent M):
   the knight comes down and joins; that ally's AI goes back to normal.
9. **The dragon.** Allies stop fighting (`KnightAllyAI.enabled = false`, they turn to watch). `castle.dragon_arrives`.
   `dragon.FlyInFriendly(dragonEntry.position, arenaCenter.position)`; wait for `Arrived`. Player input stays on (so
   he can hold the shield). `yield return dragon.BreatheOnce()` (the existing warning + one confetti breath; the
   existing `knights.fire_warning` line comes from `BattleVoice`). Then `playerInput.enabled = false` (no more sword),
   the twist:
   - Egg mission: `dragon.SetMood(DragonMood.Sad)`, `castle.dragon_egg`, then `yield return egg.RunGiveBack(dragon,
     result)` (agent M: tap the egg, it rolls to the dragon, hatches, the baby says hi). Then, if the letter moment is
     `NameBabyDragon`: `yield return naming.Run(level, result)` (agent M; unlocks the buddy).
   - Other missions: `dragon.SetMood(DragonMood.Hungry)`, `castle.dragon_hungry`. Number moment = Cookies →
     `yield return cookies.Run(dragon, cookieCount, result)`; otherwise `yield return cookies.RunQuick(dragon)` (one
     big cookie, one tap).
   - `dragon.SetMood(DragonMood.Happy)`, `castle.dragon_friend`.
10. **Dragon ride.** `yield return dragon.LieDown()`. `player.RideOn(dragon.Seat)` and `KidAnimator.SetSitting(true)`.
    The action button gets the fire icon (`fireIcon`); each press → `dragon.BreatheConfettiForward()`.
    `castle.ride_start`. `yield return dragon.FlyPath(ridePath)` (agent M; takes off, follows the path, catches the
    floating stars, lands back at `arenaCenter`, about 20 s). `castle.ride_land`. `yield return dragon.LieDown()`,
    `player.StopRiding(rideDismountPoint.position)`, `SetSitting(false)`, button icon reset. `yield return
    dragon.StandUp()`.
11. **Celebration.** `finished = true`. Everyone gathers at the gate as today (`GatherAtGate`); the queen walks to
    `queenCelebrateSpot`; the baby dragon (egg mission) hops to `babyCelebrateSpot`. Then:
    - Letter moment `NameFlag`: `yield return nameFlag.Run(level, result)` (agent M).
    - `castleWins++`, save, `yield return growth.CelebrateNewest()` (agent M; `castle.grow` + the new flag pops up).
    - `CheerAll()`, the queen cheers, confetti, `castle.victory`, `onVictory.Invoke()` (the reward system gives the
      5 stars, as today), the dragon waves and flies away (`dragon.FlyAwayHappy()`, `castle.dragon_bye`).
    - Adaptive level (below), wait `victorySeconds`, `End()`.
12. **OnEnd** must survive being called at any moment (the back button): stop all routines, `StopWalks`,
    `RestoreAllyAgents`, if the player is riding `StopRiding(rideDismountPoint.position)` and `SetSitting(false)`,
    `lessonUI.HideCards()`, button icon reset, player input/jump on, weapons hidden, golden sword off/restored as
    today, `portcullis.HoldClosed = false`, every moment's `ResetMoment()` (password, wall, cookies, egg, naming,
    name flag, tower rescue, guide), `missionProps.ResetAll()`, `queen.ResetQueen()`, `dragon.ResetDragon()`,
    opponents off, barrels upright, allies' help markers off, the stuck knight restored (`towerRescue.ResetMoment()`
    gives him back), imagination off.

**Adaptive level:** skill `"castle"`, 1–3 (`ProgressStore.Current.GetLevel/SetLevel`, save). Mistakes = the sum of
all `MomentResult.Mistakes` of this visit (password, number questions, cookies, name flag; naming the dragon never
counts). 0 → +1 (max 3); 2 or more → −1 (min 1). Only when the visit finished (not on the back button).

### Level table

Numbers are always capped by `NumberCurriculum.MaxNumber()` (and at least 2).

| | 1 | 2 | 3 |
|---|---|---|---|
| password pictures | 2 | 3 | 3 |
| wall blocks | 3 | 4–5 | 5–6 |
| goblins to count (wave 1, number moment = Goblins) | 2–3 | 3–5 | 4–6 |
| wave 1 (no counting) | 2 Normal | 2 Normal, 1 Shield | 2 Normal, 1 Shield |
| wave 1 kinds when counting | Normal only | Normal + 1 Shield | Normal + 1 Shield + Big if ≥ 5 |
| wave 2 | 1 Normal, 1 Shield, 1 Tiny | 1 Normal, 1 Shield, 1 Big, 1 Tiny | 2 Normal, 1 Shield, 1 Big, 1 Tiny |
| sword hits: Normal / Shield / Big / Tiny | 1 / 1 / 3 / 1 | 2 / 2 / 3 / 1 | 2 / 2 / 3 / 1 |
| cookies the dragon wants | 2–3 | 3–5 | 4–6 |
| number cards | 2 | 3 | 3 |
| letter cards (name flag, baby naming) | 2 | 3 | 3 |
| help a friend | wave 2 | waves 1 and 2 | waves 1 and 2 |

The golden sword still doubles every hit. The thief always needs 1 hit.

### Goblin kinds (agent F)

`public enum GoblinKind { Normal, Shield, Big, Tiny }` in `CoolSchool.Battle`. `BattleOpponentAI` gets
`[SerializeField] GoblinKind kind;` (`public GoblinKind Kind`), set by the builder per pool member. The pool is 8
(see the builder): 4 Normal, 2 Shield, 1 Big, 1 Tiny. `KnightsGame` picks free opponents of the right kind per wave.
Hits per kind come from the level table (`Hittable.SetHitsToDefeat`).

- **Normal:** today's goblin.
- **Shield:** has a shield in its `WeaponHolder` (builder). `Hittable.IsBlocking = true` while it approaches and winds
  up (Hittable already blocks hits from the front with a spark and `battle.shield_block`); the shield is down
  (`IsBlocking = false`) during its recover time (2.5 s) and while dizzy. If its swing lands on a target that is
  blocking at that moment, it gets **dizzy** for 3 s: shield down, a ring of 3 small spinning yellow stars above its
  head (a child object the builder makes, `dizzyStars`), `castle.dizzy` (once per visit), `castle.dizzy_stars` cue.
  The first time a shield goblin blocks the player's sword in a visit: `castle.shield_goblin` (Teacher hint).
- **Big (troll):** visual scale 1.35 at activation, `moveSpeed` 1.2. Each real hit shrinks the visual by 0.12 of its
  start size (smooth over 0.25 s, `castle.troll_shrink` cue), so it ends small when it tumbles. `castle.troll` when it
  lands in the courtyard (once per visit). Scale restored on reset.
- **Tiny:** never climbs. `KnightsGame` calls `HideInBarrel(barrel)` with one of the builder's `CastleBarrel`s: it
  sits inside, hidden (renderers off, agent off). The barrel wobbles every 2–3 s (`castle.barrel_wobble`), and every
  other wobble the goblin peeks up 0.4 m for 0.6 s and giggles. `castle.barrel` the first time. When the player is
  within 2.5 m, or after 20 s: the barrel tips over (rotates 80° in 0.3 s, `castle.barrel_tip`), the goblin hops out to
  the NavMesh beside it (`castle.barrel_out`) and runs around (`moveSpeed` 2.6, picks random courtyard points 3–6 m
  away, prefers points away from the player, never attacks). One hit → tumble and go home as usual. `CastleBarrel`
  (agent F, on each barrel root): `Wobble()`, `TipOver()`, `ResetBarrel()` (upright), `Transform Inside`,
  `Vector3 HopOutPoint`.

### The thief (agent F)

`public void BecomeThief(MissionLoot loot, Transform[] runPoints)` on `BattleOpponentAI`, called right after
`Activate` for the chosen goblin. After it lands it runs to the loot (speed 2.6), `loot.Grab(hand)` (the right hand
bone, fallback the root + 1 m up), `castle.thief_grab.<mission>`, `castle.thief_chase` (Kid), then runs from run point
to run point around the courtyard (speed 2.2; the player runs at 4, so he catches it), never attacking. Its hits to
defeat are 1. Defeated → `loot.ReturnHome()` (the loot flies back in an arc with a sparkle), `castle.loot_back`, then
it tumbles and goes home like the others. Safety: after 40 s it trips by itself (same as defeated). Allies' hits never
defeat (they already can't). The wave does not end before the thief is out.

`MissionLoot` (agent F, `CoolSchool.Battle`, on the cake slice, the crown and the egg):
`public bool IsHome { get; }`, `public void Grab(Transform hand)` (parents to the hand at a small offset, scale ×0.6),
`public void ReturnHome()` (unparents, arcs back to its cached home pose over 0.8 s, sparkle), `public void
ResetLoot()` (home at once). It never blocks anything (no colliders).

### Counting perches (agent F)

The builder makes 7 `Transform` perches on top of the back wall (inner edge, spread evenly between the outer
ladders, facing south) and 7 matching landing points on the courtyard floor (NavMesh), in `wallPerches` /
`perchLandings`.

- `public void ActivateOnPerch(Transform perch, Vector3 landing)` (`BattleOpponentAI`): placed 1.2 m below the perch,
  behind the wall, agent off, active; hops up onto the perch (0.4 s arc, `battle.goblin_appear`, sparkle), faces
  south, then cheers/waves (`KidAnimator.PlayCheer` every 2.5 s) and bobs. `public bool IsOnPerch { get; }`.
- `public void JumpDown()`: hop arc to `landing` (0.6 s), `agent.Warp`, then the normal fight routine. If nobody
  calls it within 40 s it jumps by itself.
- `public void HopBackDown()`: (one less) hops back behind the wall, sparkle, inactive.
- `public void PerchHop()`: a small hop on the spot (for counting along).

### Help a friend (agent F)

- `KnightAllyAI`: `public void CallForHelp(bool on)` shows/hides `helpMarker` (a builder-made child: a pulsing blue
  "!" like the goblin telegraph, 0.45 m above the head) and while on: no attacking, it backs away slowly from the
  nearest goblin. `public bool IsCallingForHelp { get; }`.
- `BattleOpponentAI`: `public void ForceTarget(Hittable target)` / `ClearForcedTarget()`: while set, `PickTarget`
  always returns it (if alive).
- `KnightsGame`: once per wave that the table allows, 6–10 s after the first goblin landed (perched ones after they
  jumped), pick a random active ally (not the stuck knight) and the 1–2 active goblins nearest to it (not the thief,
  not the tiny one); `ForceTarget(ally)`, `CallForHelp(true)`, `castle.help_me`. It ends when the player is within
  2.5 m of the ally, or those goblins are out, or after 25 s: `CallForHelp(false)`, `ClearForcedTarget()`, and (unless
  it timed out) `castle.thanks`.

### Portcullis hold (agent F)

`CastlePortcullis`: `public bool HoldClosed { get; set; }`. While true the gate stays down (or lowers) whatever the
distance or the running activity. Default false; free play is unchanged.

## World layout (agent B)

Numbers from the last build log: courtyard x 28.5..47.3, z 0.4..17.8; gate (37.9, 0, 0.2) facing south; ladders'
landings z 16.0 at x 33.4 / 37.9 / 42.4; dragon lands at (37.9, 0, 10.1); celebrate spot about (37.9, 0, 2.9).
**Always compute from `ArenaLayout`** (courtyard bounds, gate position, ladder spots), never hard-code these. The
camera looks north from 7 m behind and 6.5 m above the player; everything a moment shows must be north of where the
player stands and within about 6 m sideways.

- **Zone:** move `CastleZone` out of the courtyard to in front of the gate: `(gate.x, 0, gate.z − 8.5)` (outside the
  portcullis' 7 m open radius), snapped to the NavMesh; if there is no NavMesh there, step north until there is, but
  stay ≥ 7.2 m from the gate. Keep its ring and beacon.
- **Gate talk spot:** `(gate.x, 0, gate.z − 4)`, facing north.
- **Queen:** home at `(gate.x + 2.6, 0, gate.z − 1.4)` facing south-west (toward the talk spot); `queenSpot` in the
  courtyard at `(courtyard.max.x − 3.5, 0, courtyard.min.z + 6)`; `queenCelebrateSpot` = celebrate spot + (2.8, 0, 0.6).
- **Password stands:** 3 small cardboard pedestals (`SM_Gen_Prop_Cardboard_Box_0x` scaled to ~0.6 m cubes, or
  primitives with the cardboard material) at `(gate.x − 1.6 / 0 / +1.6, 0, gate.z − 2.2)`, no colliders, hidden at
  start. Each has an empty `PropSpot` child on top.
- **Wall patch:** on the inner face of the back wall, centred between ladder 1 and ladder 2 (x halfway), bottom at the
  floor: a "hole" = a dark irregular decal (a flat quad, near-black brown, unlit or lit with no specular; a jagged
  outline mesh or a simple rounded quad is fine) about 1.0 m wide × 1.6 m high, 2 cm in front of the wall. 8 block
  slots in front of the hole (2 columns × 4 rows, 0.42 m blocks, bottom row on the floor), each with a ghost block
  (transparent white, alpha 0.25). A peeking goblin head: a `SM_Chr_Kid_Goblin_01` model (no AI, no collider, scaled 1,
  animator `KidController`) placed behind the hole so only its head shows through when it "peeks" (agent M moves it up
  and down by 0.5 m). A pile spot 2.2 m west of the hole and 2 m into the courtyard, where up to 10 toy blocks
  (`SM_Prop_Toy_Block_01..04`, ~0.42 m) lie in a loose heap. `StandPoint` 3.6 m south of the hole.
- **Mission spot (west):** `(courtyard.min.x + 3.5, 0, courtyard.center.z)`: a small table (`SM_Gen_Prop_Table_01`, about
  0.75 m high) with three children, each hidden at start: the cake (`SM_Prop_Cake_01`, ~0.5 m) with a separate
  slice (`SM_Prop_Cake_Slice_01`) beside it (the slice is the loot), the golden crown (`SM_Chr_Attach_Crown_01` scaled
  to ~0.35 m) on a red cushion (a flattened cube), and the egg in a nest (the egg: a sphere scaled (0.45, 0.6, 0.45),
  cream with a few coloured spots (small flattened spheres); the nest: a torus-like ring of brown cylinders or a
  flattened brown sphere with the egg sitting in it). The nest stands on the ground, not on the table.
- **Run points (thief):** 8 points on a loose ring around the courtyard centre, 5–7 m out, on the NavMesh, inside the
  courtyard, not at the gate.
- **Barrels:** 2 barrels (`SM_Gen_Prop_Barrel_Wood_01`/`_02`, ~0.9 m high, not static, **no colliders** so the NavMesh ignores them)
  at `(courtyard.min.x + 3, z − 4 from the back wall)` and `(courtyard.max.x − 3, same z)`.
- **Perches:** see "Counting perches". Height = the measured top of the back wall (the panels' renderer bounds max y).
- **Cookie plate:** a table 2.8 m west of the dragon's landing spot and 1.5 m south of it, with a plate (a flattened
  cylinder) and 10 cookie spots in two rows on it. Cookies: flattened brown cylinders (0.16 m wide) with 3 dark chip
  spheres each; one big cookie (0.4 m) for `RunQuick`, hidden at start. A bowl (`SM_Prop_PetBowl_01` scaled to ~0.5 m, or a
  flattened cylinder) on the ground 1.4 m in front of the dragon's landing spot (camera side).
- **Dragon:** keep `CreateDragon`. Add a `Seat` transform: a child of the dragon's spine bone (`English DragonSpine2`,
  fallback `Spine1`, fallback the root at 60% of the model height), positioned on top of the back (measure: the highest
  point of the body renderer above that bone + 0.05 m), facing the dragon's forward. If the model has a saddle mesh or
  blend shape (`Saddle`, `Carapace`, `Bridle` names or a "Saddle" blend shape), turn it on; otherwise add a red
  flattened-cube saddle on the seat bone. Log what was used.
- **Ride path:** a `DragonRidePath` with 10–12 points: from the landing spot up to 6 m, out over the back wall, a
  counter-clockwise loop around the castle at 7–9 m height and about 3 m outside the walls (stay inside the arena
  rectangle), back over the gate, and down to the landing spot. 8 floating stars (`RideStar`, a gold emissive star
  shape or a gold sphere with a sparkle, 0.4 m, no collider) along the path, 1 m above it.
  `rideDismountPoint` = landing spot + (−2, 0, −1.5).
- **Tower rescue:** the back-east tower (north-east corner). `TowerTop` on its roof (measured renderer bounds max y,
  centre of the top); `RescueRing` (a glow ring, radius 1.2, hidden) on the courtyard floor at the tower's inner
  corner, 1.5 m inside; `RescueLadder` (`SM_Prop_Ladder_01`, scaled to reach the tower top, leaning, hidden) between
  them.
- **Baby dragon:** at the nest; `babyCelebrateSpot` = celebrate spot + (−2.2, 0, 0.4).
- **Name flag:** a flag pole (a thin cylinder, 3.2 m) at celebrate spot + (2.6, 0, 1.4) with a cloth banner
  (a thin box 1.8 × 1.0 m, castle-blue, hanging from the top, facing south) and a world-space `Canvas` (scale 0.004)
  on its south face for the name letters. Visible always (empty banner with a gold shield emblem until he has a name
  on it).
- **Castle growth tiers** (all hidden at start, `CastleGrowth.tiers` in this order): 1–4 = a flag (`SM_Prop_Flag_01`
  or `_02`, ~2.2 m) on each tower top, the two back towers first; 5–6 = banners (tall thin coloured boxes 0.8 × 2 m)
  on the inner back wall either side of the hole; 7 = a golden flag (the flag model with a gold emissive material) on
  the gate tower. Thresholds `{1, 2, 3, 4, 6, 8, 11}` (castle wins).
- **NavMesh:** nothing new may block the gate or the courtyard; all new props have no colliders (or are not static).

## Components

New runtime types of agent M live in `Assets/CoolSchool/Scripts/Castle/`, namespace `CoolSchool.Castle`, unless
noted. The builder wires serialized fields **by these exact names**.

### Agent F

```csharp
public enum CastleMission { Cake, Crown, Egg, Knight }
public enum CastleNumberMoment { Goblins, Cookies }
public enum CastleLetterMoment { None, NameFlag, NameBabyDragon }

public partial class KnightsGame : Activity   // split into KnightsGame.cs, .Visit.cs, .Waves.cs, .Dragon.cs, .Finale.cs
{
    // existing fields stay (player, playerInput, actionButton, swordIcon, imagination, ladders, opponents, dragon,
    // dragonEntry, arenaCenter, dragonFliesIn, allies, weaponHolders, victorySeconds, celebrateSpot, goldenSwordGlow,
    // onWaveStarted, onDragonArrived, onVictory). waveSizes is no longer used (keep the field, ignore it).
    [Header("Castle adventure")]
    [SerializeField] CastlePortcullis portcullis;
    [SerializeField] Transform gateTalkSpot;
    [SerializeField] Transform[] wallPerches;        // 7
    [SerializeField] Transform[] perchLandings;      // 7
    [SerializeField] CastleBarrel[] barrels;         // 2
    [SerializeField] Transform[] thiefRunPoints;     // 8
    [SerializeField] MissionLoot cakeLoot;
    [SerializeField] MissionLoot crownLoot;
    [SerializeField] MissionLoot eggLoot;
    [SerializeField] Transform rideDismountPoint;
    [SerializeField] Sprite fireIcon;
    [SerializeField] LessonUI lessonUI;
    [SerializeField] LetterSet letters;

    [Header("Castle moments (agent M)")]
    [SerializeField] CoolSchool.Castle.CastleQueen queen;
    [SerializeField] CoolSchool.Castle.CastleMissionProps missionProps;
    [SerializeField] CoolSchool.Castle.CastleGuideMarker guide;
    [SerializeField] CoolSchool.Castle.PasswordGate password;
    [SerializeField] CoolSchool.Castle.WallPatch wall;
    [SerializeField] CoolSchool.Castle.CookiePlate cookies;
    [SerializeField] CoolSchool.Castle.DragonEgg egg;
    [SerializeField] CoolSchool.Castle.DragonNaming naming;
    [SerializeField] CoolSchool.Castle.BabyDragon babyDragon;
    [SerializeField] CoolSchool.Castle.DragonRidePath ridePath;
    [SerializeField] CoolSchool.Castle.NameFlag nameFlag;
    [SerializeField] CoolSchool.Castle.CastleGrowth growth;
    [SerializeField] CoolSchool.Castle.TowerRescue towerRescue;
    [SerializeField] Transform queenCelebrateSpot;
    [SerializeField] Transform babyCelebrateSpot;

    public CastleMission Mission { get; }    // this visit's
    public int Level { get; }                // castle level at OnBegin
}
```

- `BattleOpponentAI` additions: `Kind`, `ActivateOnPerch`, `IsOnPerch`, `JumpDown`, `HopBackDown`, `PerchHop`,
  `HideInBarrel(CastleBarrel)`, `BecomeThief(MissionLoot, Transform[])`, `IsThief`, `ForceTarget`,
  `ClearForcedTarget`, `[SerializeField] GameObject dizzyStars`. Every new state is cleared in `Activate`, `SendHome`
  and `OnDisable`, and the visual scale is restored.
- `KnightAllyAI`: `CallForHelp(bool)`, `IsCallingForHelp`, `[SerializeField] GameObject helpMarker`.
- `CastlePortcullis.HoldClosed`.
- `BattleVoice`: keep `knights.goblin_down` (but not for the thief: it has `castle.loot_back`), `knights.fire_warning`,
  `knights.shield_hint` (now 6 s after wave 1 starts **only if** the player has not blocked yet this visit).
- `CardQuestion` users: the goblin count calls agent M's `CardQuestion.AskNumber` (below).
- `PlayerProgress` fields are added by agent M; use them.

### Agent M

**Shared helpers**

```csharp
public class MomentResult { public int Mistakes; }

public static class CardQuestion
{
    // Number cards: the right answer plus (cardCount − 1) distractors near it (±1..±2, ≥ 1, ≤ MaxNumber, no doubles),
    // shuffled. Wrong pick: WiggleCard + DimCard, "lesson.try_again", result.Mistakes++, run countAlong, then ask again
    // with the same cards (the dimmed one stays dim). The right card pulses after 12 s without a tap; after 26 s it is
    // solved for him (count along, celebrate). Right: CelebrateCard, a praise line ("lesson.right" group if it exists,
    // else none), HideCards after 0.8 s. Gate input with ParentGate.InputBlocked (LessonUI's buttons already are).
    public static IEnumerator AskNumber(LessonUI ui, int answer, int cardCount, Func<IEnumerator> countAlong, MomentResult result);

    // Letter cards like the pirate lock: target + distractors from LetterCurriculum.SchoolOrder, introduced letters
    // first, never the same sound group, below cardCount 3 skip AreSimilar. prompt() is said at the start and after a
    // wrong pick. Same pulse/solve timings. countMistakes false = a free choice (any card is right; onPicked gets it).
    public static IEnumerator AskLetter(LessonUI ui, LetterSet letters, char target, int cardCount, Action prompt,
        MomentResult result, bool countMistakes = true, Action<char> onPicked = null);

    public static int[] NumberChoices(int answer, int cardCount);
}

// Taps on 3D things (like TreasureCoinPicker): the nearest registered target on screen within 110 px (scaled by dpi),
// only while Armed and !ParentGate.InputBlocked. One instance in the scene (on the KnightsGame object or the castle).
public class WorldTapPicker : MonoBehaviour
{
    [SerializeField] Camera cam;
    public bool Armed { get; set; }
    public event Action<Transform> Tapped;
    public void SetTargets(IEnumerable<Transform> targets);   // replaces the list; centre = renderer bounds centre
    public void Clear();
}

// A bouncing gold arrow 2.2 m above a point and a glowing ring on the ground (like the pirates' TreasureGuideMarker).
public class CastleGuideMarker : MonoBehaviour
{
    [SerializeField] Transform arrow; [SerializeField] Transform ring;
    public void ShowAt(Vector3 point); public void Hide();
}
```

**The queen** (`CastleQueen`, on the queen kid: `SM_Chr_Kid_Princess_01` with `SM_Chr_Attach_Hat_PaperCrown_01` on
the head bone, NavMeshAgent, KidAnimator, no Hittable):
`[SerializeField] Transform home; [SerializeField] float walkSpeed = 2.2f;` `public void WalkTo(Vector3 point, Action
arrived = null)` (gives up after 10 s and warps), `public void Wave()`, `public void Cheer()`, `public void
Face(Vector3 direction)`, `public void ResetQueen()` (warp home, face the talk spot, idle). In free play she stands at
home and waves now and then when the player is within 10 m.

**Mission props** (`CastleMissionProps`): `[SerializeField] GameObject cake; GameObject crown; GameObject egg;`
(each the whole set: cake + slice, crown + cushion, egg + nest), `public void Show(CastleMission mission)`,
`public void ResetAll()` (all hidden, loot home).

**Password** (`PasswordGate`):
```csharp
[SerializeField] Transform[] stands;        // 3, each with a PropSpot child
[SerializeField] WorldTapPicker picker;
[SerializeField] CastleQueen queen;
public IEnumerator Run(int level, LetterSet letters, MomentResult result);
public void ResetMoment();
```
- Target letter: from `lettersIntroduced` that has words (`LetterEntry.HasWords`), the name's first letter with 50%
  if known; none known → `LetterCurriculum.OrderFor(childName)[0]` and introduce it: `castle.password_new` + the
  sound, and that first time no mistake counts (still taught; don't call `LetterCurriculum.Introduce`, the classroom
  does that).
- Pictures: the right word (`letters.PickWord(target)`) + distractor words from other sound groups (copy
  `LettersLesson.PickOtherWords`' rules), count from the table; the stands rise from the ground (0.4 s, sparkle),
  each word prop is instantiated on its `PropSpot`, scaled so its largest side is 0.45 m (use `LetterWord.scale`).
- Prompt: `castle.password` then `letter.sound.<x>` (the entry's `soundKey`). Tap: the tapped prop hops and its word
  is said (`LetterWord.voiceKey`). Right: the prop spins with confetti, `castle.password_right`, the stands sink. Wrong: wobble, `lesson.try_again`, the sound again, `Mistakes++`; the right
  one pulses (scale bob) after 2 misses or 15 s. After 45 s it is solved for him.

**Wall patch** (`WallPatch`):
```csharp
[SerializeField] Transform standPoint;      // public Vector3 StandPoint
[SerializeField] Transform[] slots;         // 8, bottom-left first, row by row
[SerializeField] GameObject[] ghosts;       // 8
[SerializeField] Transform[] pileBlocks;    // 10 toy blocks in a heap
[SerializeField] Transform peekGoblin;      // moves up 0.5 m to peek
[SerializeField] WorldTapPicker picker;
public IEnumerator Run(int blockCount, MomentResult result);
public void ResetMoment();
```
Shows the first `blockCount` ghosts; `castle.wall`, the goblin peeks (`castle.wall_peek`, giggle). Each tapped pile
block flies (arc 0.5 s) into the next free slot, `castle.block_place`, and `number.<k>` is said. Hint
`castle.wall_hint` after 8 s without a tap; after 30 s more the remaining blocks fly in by themselves (counting
along). Full: the goblin pops up once more (`castle.wall_done`, giggle) and drops away; sparkle over the patch.
The patch stays until `ResetMoment` (blocks back to the pile, ghosts hidden, goblin hidden). No mistakes.

**Cookies** (`CookiePlate`):
```csharp
[SerializeField] Transform[] cookies;       // 10 small ones on the plate
[SerializeField] Transform bigCookie;
[SerializeField] Transform bowl;            // where given cookies land, in front of the dragon
[SerializeField] WorldTapPicker picker;
[SerializeField] ActionButton button;
[SerializeField] Sprite checkIcon;          // a green tick, found by name by the builder
public IEnumerator Run(DragonBoss dragon, int wanted, MomentResult result);
public IEnumerator RunQuick(DragonBoss dragon);
public void ResetMoment();
```
- `Run`: `castle.cookies_want.<wanted>` (the dragon), `castle.cookies_hint`. Tapped cookies fly into the bowl,
  `number.<k>` for each. The action button shows `checkIcon`; pressing it is "done":
  - bowl == wanted: the dragon eats (`dragon.Eat(bowl.position)`, the bowl's cookies vanish with crumbs),
    `castle.cookies_yum`.
  - fewer: `castle.cookies_more`, count along what is in the bowl (each cookie hops, `number.<k>`), keep going,
    `Mistakes++`.
  - more: `castle.cookies_too_many`, the extra ones fly back to the plate one by one, then count along, keep going,
    `Mistakes++`.
  Hint after 15 s idle; after 60 s it is solved for him (the right amount ends up in the bowl and he eats). The button
  icon is reset at the end.
- `RunQuick`: `castle.cookie_big`; the big cookie appears with a sparkle; one tap → it flies to the dragon's mouth,
  `dragon.Eat`, `castle.cookies_yum`. Solved by itself after 20 s.

**Egg and baby dragon**
```csharp
public class DragonEgg : MonoBehaviour      // on the egg (which also has F's MissionLoot)
{
    [SerializeField] WorldTapPicker picker;
    [SerializeField] BabyDragon baby;
    [SerializeField] GameObject[] shellPieces;  // 4–6 curved bits, hidden; the builder makes them
    public IEnumerator RunGiveBack(DragonBoss dragon, MomentResult result);
    // castle.egg_tap; tap the egg → it rolls/arcs to the dragon's feet (1.2 m in front of it); the dragon nuzzles
    // (dragon.Nuzzle()); the egg wobbles 3× (castle.egg_crack), bursts (shell pieces fly, confetti), the baby appears
    // (baby.Hatch(position)), castle.egg_hatch, castle.baby_hello. Solved by itself after 25 s.
    public void ResetMoment();   // egg whole and back in the nest (MissionLoot.ResetLoot), shells hidden
}

public class BabyDragon : MonoBehaviour     // a small copy of the dragon model (~0.6 m), Animator with the Dragon controller
{
    public void Hatch(Vector3 position);    // pops up with a hop, peep (castle.baby_peep)
    public void HopTo(Vector3 point, Action arrived = null);
    public void Hop();                      // on the spot, peep
    public void Hide();
}

public class DragonNaming : MonoBehaviour
{
    [SerializeField] LessonUI lessonUI; [SerializeField] LetterSet letters; [SerializeField] BabyDragon baby;
    [SerializeField] RewardCatalog catalog;
    public IEnumerator Run(int level, MomentResult result);
    public void ResetMoment();
}
```
`DragonNaming.Run`: `castle.baby_name` ("…with which sound does its name start?"), then `CardQuestion.AskLetter` with
`countMistakes: false` (any card is right). Cards: introduced letters that have a name in `DragonNames` (see below),
newest first; fewer than 2 known → fill from `LetterCurriculum.SchoolOrder` letters that have a name. After the pick:
`letter.sound.<x>`, then `castle.baby_named.<x>` ("It's called Vonkie!"), the baby hops with confetti. Save
`babyDragonLetter = x`, unlock `plush.dragon` with `RewardSystem.GiveEarnedItem(item)`, `castle.baby_buddy`.

`DragonNames` (static, `CoolSchool.Castle`): `public static string NameFor(char letter)` and `public static bool
Has(char letter)`. Names (Dutch, the same in English): m Mimi, r Rocco, s Sproet, v Vonkie, i Ikkie, k Kiki, a Appie,
p Pip, n Noekie, t Tuut, e Eddie, b Bolle, o Okkie, d Dribbel, l Lolly, h Hikkie, w Wiebel, z Zoef, j Joppie,
g Guus, f Fikkie, u Ukkie. No name for c, y, x, q.

**Dragon friendly mode** (`DragonBoss`, `CoolSchool.Battle`; agent M owns these additions, keep the old fight code):
```csharp
public enum DragonMood { Idle, Sad, Hungry, Happy }
public Transform Seat { get; }                      // [SerializeField] Transform seat
public Transform MouthPoint { get; }                // fireOrigin
public void FlyInFriendly(Vector3 from, Vector3 landing);  // FlyIn, then idle turning slowly to the player; no fight loop; raises Arrived
public IEnumerator BreatheOnce();                   // the existing warning + BreatheFire, once
public void SetMood(DragonMood mood);               // Sad: head low (animator speed 0.6 on Idle, a "StunnedStanding" or "Sitting"-free sad idle if found), a sigh every 5 s;
                                                    // Hungry: castle.tummy_rumble every 4 s + a small shake; Happy: the Happy trigger once, then idle
public IEnumerator Eat(Vector3 foodPoint);          // turn to it, "Eat" trigger (clip "Attack 1"), castle.cookie_munch, crumbs (small brown confetti)
public void Nuzzle();                               // "Eat" trigger without the munch, a heart-ish sparkle
public IEnumerator LieDown();                       // "Sitting" bool true (clips "Sit" then "Sitting"), wait 1 s
public IEnumerator StandUp();                       // "Sitting" false, wait 0.8 s
public IEnumerator FlyPath(DragonRidePath path);    // Takeoff/Fly, follow the points with a smooth Catmull-Rom curve at ~6 m/s,
                                                    // banking (roll ±20° into turns) and facing the travel direction, castle.ride_whoosh at the start;
                                                    // stars within 1.6 m of the Seat pop (sparkle, castle.star_ping); lands at the last point
public void BreatheConfettiForward();               // a 0.6 s confetti burst from the mouth (no hits), 0.5 s cooldown
public void FlyAwayHappy();                         // the existing take-off-and-away part of the defeated routine, no FlewAway wait needed (it still raises it)
```
Hits from the player's or the allies' swords never "defeat" the friendly dragon: in friendly mode `OnDefeated` is
ignored and `Hittable.ResetState()` is called after each hit (or the team is set to Heroes while friendly and back
in `ResetDragon`).

`DragonAnimatorBuilder` (agent M): add parameters `Eat` (trigger) and `Sitting` (bool); states `Eat` (clip
"Attack 1", exit time back to Idle), `Sit` (clip "Sit") → `Sitting` (clip "Sitting", loop) on `Sitting` true, back to
Idle on false; use "Fly Glide" for a `Glide` state if FlyPath wants it (optional, `Gliding` bool). Missing clips are
skipped like today.

**Ride path** (`DragonRidePath`): `[SerializeField] Transform[] points; [SerializeField] RideStar[] stars;`
`public Vector3 Evaluate(float t)` (0..1 along a Catmull-Rom through the points), `public float Length`,
`public void ResetStars()`. `RideStar`: `public bool Caught { get; }`, `public void Catch()`, `public void
ResetStar()`, spins and bobs. Stars are shown only during the ride.

**Name flag** (`NameFlag`):
```csharp
[SerializeField] RectTransform letterRow;   // on the banner's world-space canvas
[SerializeField] GameObject emblem;         // the gold shield, shown until the name is on it
[SerializeField] LessonUI lessonUI; [SerializeField] LetterSet letters; [SerializeField] CastleQueen queen;
[SerializeField] Sprite dot;
public bool CanRun { get; }                 // the child's name has a drawable letter
public IEnumerator Run(int level, MomentResult result);
public void ResetMoment();
```
`castle.flag` ("…a flag with your name! With which letter does your name start?"), then `CardQuestion.AskLetter` for
the **first** letter of his name (prompt: `letter.sound.<x>`). Right: that card flies to the banner, then the whole
name writes itself letter by letter with `LetterGlyph.Build` (chalk-dot letters, white, each popping in with a soft
`castle.flag_pop`, no voice per letter), `castle.flag_done`, the banner waves
(a sine sway). Save `castleNameFlagDone = true`. When that is already true at scene start the name is shown at once
(free play too) and the emblem hidden; a name change in the parent area updates it (`ProgressStore.Changed`).

**Castle growth** (`CastleGrowth`): `[SerializeField] GameObject[] tiers; [SerializeField] int[] thresholds =
{ 1, 2, 3, 4, 6, 8, 11 };` Shows every tier whose threshold ≤ `castleWins` (listens to `ProgressStore.Changed`).
`public IEnumerator CelebrateNewest()`: if a tier was just reached, it pops in (scale 0 → 1.15 → 1, sparkle trail
from the queen to it, `castle.flag_pop`), `castle.grow`; otherwise nothing (no line).

**Tower rescue** (`TowerRescue`):
```csharp
[SerializeField] Transform towerTop; [SerializeField] GameObject rescueRing; [SerializeField] GameObject ladder;
public void PlaceKnight(KnightAllyAI ally);   // agent and AI off, warped/parented to towerTop, waves (Cheer every 3 s), "!" help marker on
public IEnumerator Run(Transform player);     // ring on; wait until the player is within 1.6 m (hint castle.rescue again after 15 s; after 40 s go on);
                                              // the ladder sparkles in, the knight hops down in 3 hops along it to the ring, cheers, castle.rescue_down,
                                              // agent back on (warp), KnightAllyAI.enabled = true only if KnightsGame still fights (it doesn't: leave it off)
public void ResetMoment();                    // knight back to normal (unparented, warped to the ring, agent on), ring and ladder hidden
```

**Shared small edits (agent M)**

- `PlayerProgress`: `public int castleVisits; public int castleWins; public string castleLastMission = "";
  public string castleLastNumberMoment = ""; public string babyDragonLetter = ""; public bool castleNameFlagDone;`
- Parent area: a 7th row `ParentStrings.RowCastle` ("Kasteel" / "Castle"), skill `"castle"`, max 3, after the aliens
  row. Make 7 rows fit above the close button (tighter spacing, e.g. 84 px from y 322, or a slightly taller window);
  check `ParentPanel` has no hard-coded row count.
- `RewardItem`: `public bool earnedOnly;` (never a present). `RewardCatalog.NextLocked` skips `earnedOnly` items.
  `RewardCatalogSeeder`: `plush.dragon` (PlushToy, prefab `Assets/CoolSchool/Prefabs/BabyDragon.prefab` built by agent
  B, `earnedOnly = true`) right after `plush.parrot`. No gold variant. Make sure the seeder can take a prefab by path
  (the others are found by name) and keeps the flag.
- `RewardSystem`: `public static void GiveEarnedItem(RewardItem item)`: unlock + `unseenItems` + save now; the reveal
  popup with the swap question (`PresentPopup.Show(item)`) waits for free play like presents do (queue it in the same
  routine).
- `BuddyFollower`: must cope with the baby dragon prefab (it has an Animator; keep it playing Idle, don't strip it).
- Voice: `VoiceLineSeeder` (+ a new partial `VoiceLineSeeder.Castle.cs` for the generated families) gets every line in
  the table below. Speakers: queen and knights = `Kid`, goblins and the big dragon = `Goblin`, baby dragon = `Alien`,
  hints = `Teacher`.
- Sound cues (`SoundCues` + `SoundAutoAssign` synonyms): `castle.block_place` (block, wood, place, thud),
  `castle.cookie_munch` (eat, munch, crunch, chew), `castle.tummy_rumble` (rumble, growl, stomach, belly),
  `castle.egg_crack` (crack, egg, shell, break), `castle.baby_peep` (peep, chirp, squeak, baby),
  `castle.barrel_wobble` (wobble, wood, knock), `castle.barrel_tip` (barrel, fall, wood, thud),
  `castle.flag_pop` (pop, reward, flag, whoosh), `castle.star_ping` (star, ping, collect, coin, sparkle),
  `castle.dizzy_stars` (dizzy, stars, bird, tweet), `castle.troll_shrink` (shrink, deflate, magic, slide),
  `castle.ride_whoosh` (whoosh, wind, wing, fly), `castle.ladder_appear` (magic, sparkle, appear). Leave them out of
  `SoundPackPicks.txt` (auto-assign fills them; the user auditions later).

### Agent B: `Assets/CoolSchool/Editor/Knights/CastleAdventureBuilder.cs`

```csharp
internal static class CastleAdventureBuilder
{
    // Static props that must exist before the NavMesh bake (none of them block it): stands, hole and blocks, tables,
    // barrels, flag pole, growth tiers. Called from KnightsArenaBuilder.BuildCastle's caller right after the castle.
    internal static void BuildProps(KnightsArenaBuilder.ArenaLayout layout);
    // Characters, components and wiring. Needs the game, the dragon, the ladders, the allies AND the LessonUI and
    // LetterSet, which LessonSetupBuilder.Setup() creates after the arena is built. So it is called from
    // PlaygroundPrototypeBuilder.RunBuild() after the lesson setup, next to PirateCoveBuilder.Build (keep the layout
    // from the castle step in EnvironmentInfo, like PirateCove does, and find the game/dragon/opponents in the scene).
    internal static void Build(KnightsArenaBuilder.ArenaLayout layout, CastleInputs inputs);
}
```
`CastleInputs` holds what `Build` needs (KidController, KidsLayer, Player, ActionButton, the allies, the sword icon);
agent B defines it.

- `KnightsArenaBuilder` edits (agent B owns them): the zone position (above), the opponent pool (8, see below), the
  calls into `CastleAdventureBuilder`, `ArenaLayout` keeps whatever new measurements are needed (back wall top y,
  tower tops), `WireGame` sets the new fields.
- **Opponent pool (8):** `Normal`: `SM_Chr_Kid_Goblin_01` ×3 + `SM_Chr_Kid_Goblin_02` ×1 (club); `Shield`:
  `SM_Chr_Kid_Goblin_02` ×2 (club + a shield: `SM_Wep_Shield_01`, fallback `_02` / `_03`); `Big`: `SM_Chr_Kid_Troll_01` (inflatable club); `Tiny`: `SM_Chr_Baby_Goblin_01`
  if it has a humanoid avatar that works with the Kid controller (check `Animator.isHuman`/avatar), otherwise
  `SM_Chr_Kid_Goblin_01` with the model scaled 0.6; log which. Each gets `dizzyStars` (3 small yellow emissive
  spheres on a ring 0.35 m wide, just above the head, hidden; agent F spins it).
- **Allies:** add the `helpMarker` to each ally (a blue emissive "!" like `CreateTelegraph`, hidden). Allies are made
  in `PlaygroundPrototypeBuilder` (find where `KnightAllyAI` is added and add the marker there, or add it from
  `CastleAdventureBuilder.Build` to `inputs.Allies`; prefer the latter, so `PlaygroundPrototypeBuilder` is untouched).
- **Queen:** `SM_Chr_Kid_Princess_01` with the paper crown on the head bone (use `AttachmentFit` like the other hats
  if it applies), NavMeshAgent, KidAnimator (`KidController`), `CastleQueen`; Kids layer.
- **Baby dragon prefab:** `Assets/CoolSchool/Prefabs/BabyDragon.prefab`: the Dragon_LP model (vendor scripts
  stripped like `CreateDragon` does), scaled to ~0.6 m tall, Animator with the Dragon controller, no Hittable, no
  colliders, pivot at the feet. Create/overwrite at build time (before `RewardCatalogSeeder` runs, or make the seeder
  call the builder's `EnsureBabyDragonPrefab()`; agree on `internal static GameObject EnsureBabyDragonPrefab()` in
  `CastleAdventureBuilder`, agent M's seeder calls it). The scene's `BabyDragon` component sits on an instance of it.
- **Everything in the layout section,** plus the `WorldTapPicker` (camera = main camera), the `CastleGuideMarker`
  (gold arrow + ring, materials like the pirates' `PirateGuideArrow.mat`/`PirateGuideRing.mat`, new copies under
  `Materials/Castle/`), all components wired.
- **Icons:** `fireIcon` = the `Fire` sprite (`Fire.png`, found by name; fallback the sword icon) and the cookies'
  `checkIcon` = `Checkmark-Success-Transparency` (fallback `Checkmark`, then a green circle sprite).
- **VoiceSetupBuilder:** `WireKnightsGame` stops wiring `knights.start`, `knights.dragon` and `knights.victory` to the
  game's events (the flow says its own lines now). Keep the inviter.
- **RewardsSetupBuilder:** nothing changes (onVictory still gives the stars).
- Materials under `Assets/CoolSchool/Materials/Castle/`: `CastleHole.mat`, `CastleGhostBlock.mat`, `CastleCookie.mat`,
  `CastleChip.mat`, `CastleEgg.mat`, `CastleEggSpot_<n>.mat` (3 colours), `CastleNest.mat`, `CastleCushion.mat`,
  `CastleBanner.mat`, `CastleGold.mat`, `CastleStar.mat`, `CastleDizzy.mat`, `CastleHelp.mat`, `CastleSaddle.mat`.
- Log one summary line: zone, talk spot, queen home, stands, hole, mission spot, barrels, perches (count and height),
  cookie table, tower top, flag pole, path length, opponents with kinds, the tiny goblin's model and the saddle choice.

## Voice lines

Group = key without the trailing number. Lines that end with "…" are followed by another clip (a sound or a number).

| key | speaker | Dutch | English |
|---|---|---|---|
| `castle.queen_hello.1` | Kid | Hallo, ridder! Ik ben de koningin van het kasteel. Wat fijn dat je er bent! | Hello, knight! I'm the queen of the castle. I'm so glad you're here! |
| `castle.mission.cake.1` | Kid | Vanavond is er feest, met een grote taart! Maar de kobolden willen de taart stelen. Help je ons? | Tonight there's a party with a big cake! But the goblins want to steal the cake. Will you help us? |
| `castle.mission.crown.1` | Kid | Mijn gouden kroon ligt klaar voor het feest. Maar de kobolden willen hem pakken! Help je ons? | My golden crown is ready for the party. But the goblins want to grab it! Will you help us? |
| `castle.mission.egg.1` | Kid | Kijk, we hebben een groot ei gevonden! De kobolden willen het hebben. Pas jij erop? | Look, we found a big egg! The goblins want it. Will you look after it? |
| `castle.mission.knight.1` | Kid | Oh nee! Een ridder zit vast boven op de toren! En de kobolden komen eraan. Help je ons? | Oh no! A knight is stuck on top of the tower! And the goblins are coming. Will you help us? |
| `castle.tower_help.1` | Kid | Help! Ik zit vast! | Help! I'm stuck! |
| `castle.password.1` | Kid | Eerst het wachtwoord! Tik op het plaatje dat begint met… | First the password! Tap the picture that starts with… |
| `castle.password_new.1` | Kid | Dit is een nieuwe letter! Luister maar: | This is a new letter! Listen: |
| `castle.password_right.1` | Kid | Goed zo, dat is het wachtwoord! De poort gaat open! | Well done, that's the password! The gate is opening! |
| `castle.wall.1` | Kid | Oh nee, er zit een gat in de muur! Tik op de blokken, dan maken we het dicht! | Oh no, there's a hole in the wall! Tap the blocks and we'll close it! |
| `castle.wall_peek.1` | Goblin | Hihi! Ik kom zo naar binnen! | Hehe! I'm coming in! |
| `castle.wall_hint.1` | Teacher | Tik op een blok! | Tap a block! |
| `castle.wall_done.1` | Goblin | Hé! Het gat is dicht! | Hey! The hole is closed! |
| `castle.count.1` | Kid | Kijk, daar op de muur! Hoeveel kobolden zie je? | Look, up on the wall! How many goblins do you see? |
| `castle.count_more.1` | Goblin | Ik ook! Ik doe ook mee! | Me too! I'm joining in! |
| `castle.count_less.1` | Goblin | Oei, ik ben bang! Ik ga weg! | Eek, I'm scared! I'm leaving! |
| `castle.count_now.1` | Kid | Hoeveel kobolden zijn er nu? | How many goblins are there now? |
| `castle.jump_down.1` | Goblin | Hop! Daar komen we! | Hop! Here we come! |
| `castle.shield_goblin.1` | Teacher | Die kobold heeft een schild! Wacht tot hij het laat zakken! | That goblin has a shield! Wait until he lowers it! |
| `castle.dizzy.1` | Goblin | Oei, ik zie sterretjes! | Whoa, I'm seeing stars! |
| `castle.troll.1` | Goblin | Ik ben de grote trol! Hohoho! | I'm the big troll! Ho ho ho! |
| `castle.barrel.1` | Goblin | Hihi, ik zit in de ton! Vind je mij? | Hehe, I'm in the barrel! Can you find me? |
| `castle.barrel_out.1` | Goblin | Gevonden! Hihi! | You found me! Hehe! |
| `castle.thief_grab.cake.1` | Goblin | Hihi! Een stuk taart voor mij! | Hehe! A piece of cake for me! |
| `castle.thief_grab.crown.1` | Goblin | Hihi! Nu ben ik de koning! | Hehe! Now I'm the king! |
| `castle.thief_grab.egg.1` | Goblin | Hihi! Dat ei is van mij! | Hehe! That egg is mine! |
| `castle.thief_chase.1` | Kid | Snel, vang die kobold! | Quick, catch that goblin! |
| `castle.loot_back.1` | Kid | Goed zo! Je hebt het teruggepakt! | Well done! You got it back! |
| `castle.help_me.1` | Kid | Help! Help mij! | Help! Help me! |
| `castle.help_me.2` | Kid | Help! Ze zitten achter mij aan! | Help! They're chasing me! |
| `castle.thanks.1` | Kid | Dankjewel! Jij bent een echte ridder! | Thank you! You're a real knight! |
| `castle.rescue.1` | Kid | Nu de ridder op de toren! Loop naar de toren! | Now the knight on the tower! Walk to the tower! |
| `castle.rescue_down.1` | Kid | Joepie, ik ben vrij! Dankjewel! | Yippee, I'm free! Thank you! |
| `castle.dragon_arrives.1` | Kid | Kijk! Een draak! | Look! A dragon! |
| `castle.dragon_hungry.1` | Kid | Wacht! Hoor je dat? Zijn buik rommelt! De draak heeft gewoon honger! | Wait! Do you hear that? Its tummy is rumbling! The dragon is just hungry! |
| `castle.dragon_egg.1` | Kid | Wacht! De draak is verdrietig. Ze zoekt haar ei! | Wait! The dragon is sad. She's looking for her egg! |
| `castle.cookies_want.<n>.1` (n = 2..8) | Goblin | Ik wil {twee…acht} koekjes! | I want {two…eight} cookies! |
| `castle.cookies_hint.1` | Kid | Tik op de koekjes. Is het genoeg? Tik dan op de knop! | Tap the cookies. Is it enough? Then tap the button! |
| `castle.cookies_more.1` | Goblin | Nog meer! Ik heb nog honger! | More! I'm still hungry! |
| `castle.cookies_too_many.1` | Goblin | Oei, dat zijn er te veel! | Oops, that's too many! |
| `castle.cookies_yum.1` | Goblin | Mmm, lekker! Precies genoeg! | Mmm, yummy! Just right! |
| `castle.cookie_big.1` | Kid | Geef de draak dit grote koekje! Tik erop! | Give the dragon this big cookie! Tap it! |
| `castle.egg_tap.1` | Kid | Tik op het ei, dan brengen we het naar de draak! | Tap the egg and we'll bring it to the dragon! |
| `castle.egg_hatch.1` | Kid | Kijk! Het ei gaat open! | Look! The egg is hatching! |
| `castle.baby_hello.1` | Alien | Piep! Hallo mama! | Peep! Hello mummy! |
| `castle.baby_name.1` | Kid | Het draakje heeft nog geen naam! Kies een letter. Met die klank begint zijn naam! | The little dragon has no name yet! Pick a letter. Its name starts with that sound! |
| `castle.baby_named.<x>.1` (22 letters) | Kid | Het draakje heet {name}! | The little dragon is called {name}! |
| `castle.baby_buddy.1` | Kid | Het draakje wil met jou mee! Het is nu jouw vriendje! | The little dragon wants to come with you! Now it's your buddy! |
| `castle.dragon_friend.1` | Goblin | Dankjewel! Nu zijn we vrienden. Klim maar op mijn rug! | Thank you! Now we're friends. Climb onto my back! |
| `castle.ride_start.1` | Kid | Houd je goed vast! Tik op de knop voor confettivuur! | Hold on tight! Tap the button for confetti fire! |
| `castle.ride_land.1` | Goblin | Daar zijn we weer! | Here we are again! |
| `castle.flag.1` | Kid | Voor jou, ridder: een vlag met jouw naam! Met welke letter begint jouw naam? | For you, knight: a flag with your name! Which letter does your name start with? |
| `castle.flag_done.1` | Kid | Kijk! Jouw naam op de vlag! | Look! Your name on the flag! |
| `castle.grow.1` | Kid | En kijk: er staat een nieuwe vlag op het kasteel! | And look: there's a new flag on the castle! |
| `castle.victory.1` | Kid | Hoera! Jullie hebben het kasteel gered! Feest! | Hooray! You saved the castle! Party time! |
| `castle.dragon_bye.1` | Goblin | Dag! Tot de volgende keer! | Bye! See you next time! |

Number words for `castle.cookies_want`: copy the word lists from `VoiceLineSeeder.Numbers.cs`. The 22 names for
`castle.baby_named` come from `DragonNames`.

After the build, the main agent runs the batch voice generation for the new lines.

## Coordination

- **Agent F owns:** `KnightsGame*.cs`, `BattleOpponentAI`, `GoblinKind`, `CastleBarrel`, `MissionLoot`,
  `KnightAllyAI`, `CastlePortcullis` (`HoldClosed` only), `BattleVoice`, the enums `CastleMission` /
  `CastleNumberMoment` / `CastleLetterMoment` (in `Scripts/Battle/CastleVisit.cs`).
- **Agent M owns:** everything in `Scripts/Castle/`, the `DragonBoss` additions, `DragonAnimatorBuilder`, and the
  shared small edits (`PlayerProgress`, `ParentStrings`, `ParentAreaBuilder`/`ParentPanel` row, `RewardItem`,
  `RewardCatalog`, `RewardCatalogSeeder`, `RewardSystem.GiveEarnedItem`, `BuddyFollower`, `VoiceLineSeeder*`,
  `SoundCues`, `SoundAutoAssign`).
- **Agent B owns:** `Editor/Knights/*`, `KnightsArenaBuilder.cs`, the `VoiceSetupBuilder.WireKnightsGame` change, and
  the small hooks in `PlaygroundPrototypeBuilder` (an `EnvironmentInfo` field for the castle layout if needed, the
  `BuildProps` call and the `Build` call after the lesson setup; nothing else in that file).
- `DragonBoss` is shared: agent M adds the friendly API; agent F only calls it; agent B only wires `seat`.
- Agent F depends on agent M's types (`CardQuestion`, `MomentResult`, the moment components, `DragonBoss` additions)
  and agent B on everyone's. Use the exact names above; if a type is not there yet, wait and retry the compile check.
- **Re-read each shared file right before every edit**, keep edits small and local, never reformat or reorder other
  code.
- Nobody runs the batch build, generates voices or commits; the main agent does.
