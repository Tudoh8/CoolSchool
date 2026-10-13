# Hide and Seek in the Zoo Park: implementation contract

Date: 2026-10-13. A new play section: **"De dieren zijn ontsnapt!"** The zoo animals have escaped and are hiding in a
new park west of the alien field. He counts with his eyes closed, then searches; after that it's his turn to hide and
the zookeeper (and the animals he found) search for him. Found animals go back to their pens, so the zoo fills up over
the visits.

Three agents build it in parallel:

- **Agent F (flow):** the activity and everything that moves: `HideAndSeekGame` (partial files), `ZooAnimal`,
  `HidingSpot`, `CongaLine`, `ZooKeeper`, `SparkleTrail`, `SurpriseCritter`, the reward hook.
- **Agent T (things, UI and shared edits):** `ZooAnimalKind` data, `ZooPen`, `ZooChecklistUI`, `EyesClosedOverlay`,
  `ZooProgress` helpers, and all shared small edits (voice lines, sound cues, progress fields, parent row).
- **Agent B (builder):** `Editor/Zoo/ZooParkBuilder*.cs` that builds the park and wires everything, the icon renderer,
  preview shots, and the hook lines in `PlaygroundPrototypeBuilder`.

Read first: `Docs/HANDOVER.md` (architecture, tools, gotchas), `Docs/specs/pirate-cove-contract.md` (the newest example
of this kind of contract: an area outside the main fence, an entrance gap, a guide, card questions),
`Docs/specs/harder-numbers-contract.md` (`NumberCurriculum`), `Docs/specs/letters-and-parent-area-contract.md`
(letter sounds, parent rows), `Docs/specs/soapbox-race-contract.md` (how polyperfect animals are made URP and animated).

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it only fails on a type another agent owns, wait about a minute and
retry, for up to about 10 minutes. Never create another agent's types. The Unity editor is **open**: do **not** run
`Tools/unity-batch.ps1` (no batch builds, voice generation or preview renders). The main agent does those afterwards.

**Player:** a 4-year-old who can't read. Everything is spoken; nothing he does can fail; no words on screen (icons and
digits are fine). Controls: the floating joystick only (walking). This game needs **no action button** and no tapping
in the world; the only taps are the number cards.

## User decisions (2026-10-13)

- **Theme:** escaped zoo animals (polyperfect Low Poly Animated Animals), with a zookeeper.
- **Place:** a new park **west** of the playground. The alien field already fills the west end of the fenced area, so
  the park lies beyond it, through a gap in the main west fence.
- **Roles:** he seeks first; then a bonus turn where **he hides** and the seeker **always finds him** (no "buut vrij").
- **Across visits:** the **zoo fills up**: found animals live in their pens; new kinds appear over the visits.
- **Learning (all four):** count along while the seeker counts; position words (achter / in / onder / boven) in the
  hints and the recap; the animal's first sound as a clue; found / left counts.

Main-agent defaults (not asked): the zookeeper is a classmate in an explorer outfit (speaker `Kid`); 10 animal kinds,
3–5 hide per round; 12 hiding spots, empty ones hold a funny surprise; adaptive `hideseek` level 1–3 with a parent
row; about 4–5 minutes per visit; stars = animals found + 1.

## The big rule: the camera sees everything

The camera is fixed, looking north from above and behind the player. Nothing can be hidden out of view. A hider is
always **partly visible**, and that is the joke: a giraffe's head above the hedge, a hippo's eyes in the pond, a
penguin's head poking out of the bin. "Behind" a cover means **north** of it (the camera's far side). In containers (bin,
box, house, leaves, pond) a fixed **show fraction** of the animal stands above the rim (see "Poses per spot"); in the tent
the animal stands inside at ground level and only its head sticks out of the front.

## Flow (`CoolSchool.Zoo.HideAndSeekGame : Activity`, agent F)

1. **Begin (at the zone by the base tree).**
   - `Sfx.PlayMusic("music.zoo")`. The keeper (`ZooKeeper`) runs in from its home spot with a sparkle; voice
     `zoo.start` ("Help! De dieren zijn ontsnapt!").
   - Pick the hiders (see "Choosing the hiders"). For each hider that currently stands in its pen: the pen's gate swings
     open, the animal does a little hop and a puff (`BattleEffects.Sparkle`), and is hidden (`ZooAnimal.Hide()`), so
     the pen looks empty. Voice `zoo.escaped`.
   - `checklist.Show(icons of the hiders)` (all black silhouettes).
2. **Count with eyes closed.** Voice `zoo.close_eyes`. The keeper turns to face the base tree (north). The player is
   frozen (`player.InputEnabled = false`) and turned to face north. `eyes.Close()` fades a dark overlay in (0.4 s).
   - While the overlay is closed, every hider is put into its spot (`spot.Occupy(animal)`), and giggles plus little
     footsteps play (`zoo.giggle`, `zoo.patter`) so it sounds like the animals are running off.
   - Count from 1 to `seekCountTo` (level table), one number every `countStepSeconds` (0.9 s): `eyes.ShowDigit(k)`
     (big digit, pop) and voice `number.<k>`, interrupting the previous number if it is still playing. He counts along.
   - Voice `zoo.ready` ("Wie niet weg is, is gezien! We komen eraan!"), `eyes.Open()`, input back on.
3. **Seek.** He walks around. A spot is **checked** when the player comes within its `checkRadius` (measured to the
   edge of its cover bounds, like the pirate landmarks). The keeper wanders behind him loosely (`keeper.Follow(player)`).
   - **Hider found:** `spot.Reveal()`: the animal pops out with a hop, confetti (`BattleEffects.Confetti`),
     `zoo.found_pop`, then its own call (`ZooAnimal.Call()`). Voice `zoo.found.<id>` ("Gevonden! De olifant!"). If it
     is a kind he had never found before: `zoo.found_new` first ("Een nieuw dier!"). `checklist.SetFound(i)` (the
     silhouette turns into the coloured icon with a pop). The animal joins the conga line behind the player
     (`conga.Add(animal)`).
   - Then the **left** count: if animals remain, voice `zoo.left.<n>` ("Nog twee dieren!", n = 1..4). Once per round,
     if `NumberCurriculum.IsUnlocked(NumberSkill.Hidden)` and at least 2 remain, ask instead: voice `zoo.how_many_left`,
     `lessonUI.ShowNumberCards(...)` (right answer = remaining, distractors per level). The checklist shows the black
     silhouettes, so he can count them. Right: `lessonUI.CelebrateCard`, voice `lesson.right` if it exists, otherwise
     `zoo.right`. Wrong: wiggle and dim the card, voice `zoo.count_silhouettes` ("Tel de zwarte dieren maar!"), then
     the checklist pulses its silhouettes one by one with `number.<k>`, then the cards again. Count a mistake for each
     wrong pick.
   - **Empty spot:** `spot.Reveal()` with no hider: a surprise critter pops out (see `SurpriseCritter`), voice
     `zoo.surprise` ("Oh! Dat is geen olifant!"), and the spot can't surprise again this round.
   - All found: voice `zoo.all_found` ("Alle dieren gevonden!"), `checklist` celebrates. Continue with step 5.
4. **Hints (never stuck).** A timer counts the seconds since the last find (or since the eyes opened). Times come from
   the level table. The target is the **remaining hider nearest to the player**.
   - Always: each hidden animal **peeks** every `peekInterval` seconds (level table, random in the range): it moves
     from its hide pose to its peek pose and back (`HidingSpot.Peek()`), with `zoo.peek`. Within `callRadius` (8 m) of
     the player its giggle or call plays every 4–6 s from the animal's position (3D sound, so it is louder nearby).
   - At **T1:** the first-sound clue: voice `zoo.clue` ("Ik hoor een dier met de…") then `letter.sound.<x>` of the
     target's first sound (skip the clue if `VoicePlayer.TryGetLine` has no clip for it). Then the position hint
     `zoo.hint.<spotId>` ("Ik hoor iets achter de heg!").
   - At **T2:** the target spot shakes hard (`HidingSpot.Shake()`, leaves/sparkle burst), voice `zoo.shake` ("Kijk!
     Daar beweegt iets!").
   - At **T3:** a sparkle trail on the ground from the player to the target spot (`SparkleTrail.Show(from, to)`), and
     again every 12 s while nothing is found. Voice `zoo.trail` ("Volg de sterretjes!"). Count each T3 as a mistake.
   - Every find resets the timer and the hint stage.
5. **His turn to hide.** Voice `zoo.your_turn` ("Nu ben jij aan de beurt! Verstop je maar!").
   - The first number waits until `zoo.your_turn` has finished (voice not busy, at most 7 s), so it is not cut off.
   - Pick 3 **player spots** among **hedge, bush, rock, table, tree, bin, box, leaves** (not pond, treetop, tent or house:
     he would not stay visible there), the nearest to the player, preferring spots north of him. They glow
     (`spot.SetPlayerGlow(true)`: a pulsing ring on the ground at the spot's **south** side, where he can see and reach it).
   - The keeper walks to the base-ish point nearest to the player (`keeper.CountAt(point)`: faces north, away from
     the camera) and counts aloud 1..`hideCountTo` (level table) with `number.<k>` at 1 s per number; the found animals
     stand around the keeper with their eyes "closed" (they face north too). He can count along.
   - He has **reached** a glowing spot when he is within 1.0 m of its cover bounds edge (`HidingSpot.DistanceTo`) or
     within 1.4 m of the glow, whichever comes first: input off, `PlayerController.RideOn(spot.PlayerHidePoint)` (a hop
     onto the point; the controller sleeps and he is parented to it, so gravity can't pull him down; he faces south),
     the other glows go out, `zoo.hide_in` (a rustle). He stays visible but tucked in: head and shoulders show (behind
     a cover at ground height, raised in a bin or box so the top ~45% of him shows over the rim, sunk in the leaves).
     When found he hops out with `StopRiding(spot.PopPoint)` (south of the prop); `OnEnd` also calls `StopRiding` if he
     is still riding.
   - If the count ends before he is hidden: the keeper says `zoo.not_yet` ("Ben je al verstopt? Ik tel nog even
     door!"), a sparkle trail leads to the nearest glowing spot, and the keeper counts on (k+1, k+2, ...) until he is
     in. He is never "caught in the open".
   - Then the keeper turns: `zoo.seek_start` ("Klaar of niet, ik kom!"). The keeper and the conga animals search
     `decoyChecks` other spots (level table), nearest-first, walking there on the NavMesh. At each: the keeper peeks
     (`PlayLaugh`), a surprise critter or nothing, voice `zoo.seek_wrong` (4 variants: "Zit hij hier? Nee… een
     poes!"). Then they walk to his spot: `zoo.seek_close` ("Hmmm… ik hoor iets…"), a 1.5 s pause, then
     `zoo.seek_found` ("Gevonden! Hihi!"). The player hops out (`KidAnimator.PlayJump` then `PlayCheer`), confetti,
     everyone laughs (`PlayLaugh` on the keeper, a giggle per animal). Input on.
   - The player may be frozen in his spot for a long time; the whole search takes at most about 25 s (cap the walking:
     if the keeper hasn't reached a spot within 8 s, it skips the rest of the way with a hop).
6. **Back to the zoo.** Voice `zoo.back_home` ("Breng de dieren terug naar de dierentuin!"). A sparkle trail from the
   player to `penWalkPoint` (in front of the pens), refreshed every 6 s; the conga follows him. After 60 s, or when he
   arrives (within 2 m): the recap. Each found animal in turn (in finding order):
   - first he (and the conga) walks over the NavMesh to 2.5 m south of that pen's gate point, so the pen is on screen,
   - then it walks from the conga to its pen's gate and hops in (`pen.TakeIn(animal)`: gate opens, the animal hops to the
     pen's stand point, gate closes, `zoo.pen_hop`; if it is a newly found kind the pen's sign turns from silhouette to
     colour with a sparkle, `pen.LightSign()`),
   - voice `zoo.where.<id>` ("Waar zat de olifant?"), a 1.2 s pause (he can answer out loud), then
     `zoo.spot.<spotId>` ("Achter de heg!").
7. **How many did we find?** Voice `zoo.how_many_found`, `lessonUI.ShowNumberCards` (right answer = number found,
   distractors per level). Right: celebrate. Wrong: wiggle, `lesson.try_again` (or `zoo.count_silhouettes`), then the
   pens' animals each hop once in turn with `number.<k>`, then the cards again. Count mistakes.
8. **Party.** The keeper and the player go to `partyPoints` and dance (`KidAnimator.SetDancing(true)`), the animals in
   their pens do happy hops; `Sfx.Play("zoo.party")`, voice `zoo.victory`. Raise
   `RoundFinished(found + 1, player position)` (RewardSystem gives the stars). Update progress (below), save.
   `onVictory.Invoke()` after 4 s, then `End()`.
9. **OnEnd** (also when ended early, at any moment, through the back button):
   - overlay open, checklist hidden, lesson cards hidden, trails stopped, glows off, input on, player not frozen;
   - every animal that is found or hidden goes back: animals of known kinds (`ProgressStore`) appear in their pens
     (`pen.PlaceInstantly(animal)`), the rest are hidden; spots reset (`spot.ResetSpot()`); conga cleared;
   - the keeper goes home (sparkle), dancing off; critters hidden;
   - music back to the playground (ActivityManager does this when the default activity resumes).

### Choosing the hiders

`ZooAnimalKind.Order` is the fixed order of the 10 kinds (table below). `known` = `ProgressStore.Current.zooAnimals`
(ids of kinds he has found at least once).

- N = hiders from the level table.
- Take the **new** kinds first: the first unknown kinds in `Order`. On the very first visit (known is empty) all N are
  new. On later visits take **one** new kind (if any are left), unless fewer than N−1 known kinds exist, then take as
  many new kinds as needed to make N.
- Fill the rest at random from the known kinds, never the same set twice in a row if avoidable (remember the last set
  in a field, not saved).
- **Spots:** assign each hider a spot from its kind's allowed list (table below), each spot at most one hider, at random
  with a simple backtracking search; spots of hiders must be at least 6 m apart if possible (relax if not). Never use a
  spot within 7 m of the player's position at the start of the count.

### Progress and level

- When the round ends normally (step 8): add every found kind to `zooAnimals` (no duplicates), `zooRounds++`, save.
  Found kinds also count when the game is ended early (add them in `OnEnd` if not added yet), so a pen once seen
  filled stays filled.
- **Adaptive level:** mistakes = number of T3 trails + wrong card picks. 0 mistakes → `hideseek` level +1 (max 3);
  3 or more → −1 (min 1). `ProgressStore.Current.GetLevel("hideseek")` / `SetLevel("hideseek", n)`, save.

### Level table

| | 1 | 2 | 3 |
|---|---|---|---|
| hiders | 3 | 4 | 5 |
| seek count to (`seekCountTo`) | 5 | 10 | 10 |
| hide count to (`hideCountTo`) | clamp(MaxNumber, 5, 10) | clamp(MaxNumber, 5, 15) | clamp(MaxNumber, 5, 20) |
| peek interval (s) | 4–6 | 6–9 | 8–12 |
| hint times T1 / T2 / T3 (s) | 8 / 16 / 26 | 12 / 24 / 36 | 18 / 32 / 45 |
| decoy checks in his turn | 2 | 2 | 3 |
| number cards | 2 | 3 | 3 |

`MaxNumber` is `NumberCurriculum.MaxNumber()` (the numbers level). Number-card distractors: distinct numbers from
1..max(5, answer + 2), never the answer, random.

## The animals

| id | Dutch (line text) | first sound | polyperfect prefab | height (m) | allowed spots |
|---|---|---|---|---|---|
| `elephant` | de olifant | o | `Elephant_Female` | 2.0 | hedge, tree, bush, rock |
| `giraffe` | de giraf | g | `Giraffe` | 3.4 | hedge, tree, bush, house |
| `monkey` | de aap | a | `Chimpanzee` | 0.9 | treetop, house, box, bin, tent |
| `penguin` | de pinguïn | p | `Penguin` | 0.6 | bin, box, tent, table, leaves |
| `hippo` | het nijlpaard | n | `Hippo` | 1.2 | pond, hedge, leaves |
| `lion` | de leeuw | l | `Lion_Male` | 1.1 | leaves, bush, tent, rock, table |
| `zebra` | de zebra | z | `Zebra` | 1.5 | hedge, bush, tree, tent |
| `crocodile` | de krokodil | k | `Crocodile` | 0.45 | pond, leaves, hedge, table |
| `flamingo` | de flamingo | f | `Flamingo` | 1.3 | pond, bush, hedge, rock |
| `tiger` | de tijger | t | `Tiger` | 1.0 | bush, leaves, tent, house, rock |

This is also `ZooAnimalKind.Order`. Calls: a sound cue per kind, `zoo.call.<id>`; when the library has no clip for it,
`ZooAnimal.Call()` plays `zoo.giggle` pitched by size (big 0.8, small 1.3). Agent T fills what polyperfect has in the
picks file: `SFX_Elephant_Trumpeting_1/2/3` (elephant), `SFX_Penguin` (penguin), `SFX_Gorilla_Growl` pitched up 1.4
(monkey), `SFX_Bear_Growl` pitched up 1.3 (lion and tiger). The rest stay empty for the main agent to fill.

## The hiding spots

| id | prop (found by name; fallback in brackets) | size (fit by bounds) | kind | hint/recap words |
|---|---|---|---|---|
| `hedge` | `SM_Env_Hedge_01` (`_02`) | 4 m long, 1.3 m tall | cover | achter de heg |
| `tree` | `SM_Generic_Tree_01` (`SM_Env_Tree_01`) | 6 m tall | cover (thin) | achter de boom |
| `treetop` | `SM_Env_Tree_03` (`SM_Generic_Tree_02`) | 6 m tall, perch at 3.2 m | perch | boven in de boom |
| `pond` | a flat light blue-green disc (not realistic water) with small rocks (0.4–0.7 m) | 5 m wide | container | in de vijver |
| `bin` | `SM_Env_Park_Rubbish_Bin_01` | ~1.5 m tall, ~1.1 m wide (a giant bin) | container | in de prullenbak |
| `tent` | `SM_Prop_Blanket_Fort_01` | 2.4 m wide | container | in de tent |
| `box` | `SM_Prop_Cardboard_Box_01` (`SM_Gen_Prop_Cardboard_Box_01`) | 1.2 m tall | container | in de doos |
| `bush` | `SM_Gen_Env_Bush_Large_01` (`SM_Env_Bush_01`) | 2 m tall | cover | achter de struik |
| `house` | `SM_Prop_Cardboard_Building_01` | 2.6 m tall | container | in het huisje |
| `leaves` | `SM_Gen_Env_Leaves_Pile_01` | 2.8 m wide, 0.7 m tall | container (low) | onder de bladeren |
| `table` | `SM_Prop_PicnicTable_01` (`SM_Env_Park_Table_01`) | 2 m long | cover (under) | onder de tafel |
| `rock` | `SM_Gen_Env_Rock_02` (`_05`) | 1.7 m tall | cover | achter de steen |

**Poses per spot** (empty children made by agent B, read by `HidingSpot`):
- `HidePoint`: where a hider stands. Covers: 0.4 m + half the animal's depth north of the cover's north bounds edge,
  centred (agent F offsets by the kind's size at run time; agent B places the point at the cover's north edge + 0.4 m).
  Containers: the container's centre on the ground, and a **show fraction**: `HidingSpot.rimHeight` (the height of the
  rim, roof or water above the ground, set by the builder) and `showFraction` (the part of the animal's height above
  the rim) put the animal's model bottom at `rimHeight - (1 - showFraction) * height` (never below the ground unless
  `sinkFraction > 0`, which only pond and leaves have). Show fractions: bin 0.4, box 0.4, leaves 0.45, house 0.35, pond
  0.45 (per kind in the pond: flamingo 0.9, it stands in the water; crocodile 0.5; hippo 0.45). **Tent:** `frontOut`
  0.35: the animal stands inside at ground level facing south, pushed south so its front 35% (the head) sticks out past
  the tent's south bounds edge. Perch: in the crown on its south side at 3.2 m (like the alien tree perches). Under the
  table: under its middle, sinkFraction 0.
- `PeekPoint`: covers: 0.8 m beside the cover's east or west end (alternate), at ground; containers and the table:
  the hide point raised by 35% of the animal's height (the tent: 0.5 m further south); perch: 0.3 m further south and
  0.2 m lower.
- `PlayerHidePoint` (hedge, tree, bush, rock, table, bin, box, leaves only; none on pond, treetop, tent, house; a 1.2 m
  kid, facing south): hedge: right behind it at ground; tree: half behind the trunk; a tall bush or rock: at its
  rounded side end, behind it (it would hide him completely otherwise); table: behind its north side at ground; bin and
  box: inside, raised so the top 45% of him is above the rim; leaves: the middle, sunk so the top 45% shows. The glow
  ring (1 m radius) lies on the ground 1 m south of the prop's south edge.
- `CheckRadius` 1.4 m to the cover bounds edge.

Animals in a spot face **south** (toward the camera) so the face peeks out; the elephant, giraffe and zebra behind a
cover face east or west (sideways, so their length shows on both sides of a thin tree).

## World layout (agent B)

Coordinates: the main fence rectangle comes from `EnvironmentInfo` (today `MinX` = −44.9, `MinZ` = −20.9,
`MaxZ` = 20.9; read them, never hard-code). Park coordinates: **u** = metres west of the main west fence
(`x = info.MinX − u`), **v** = world z.

- **Park rectangle:** u 0..48, v −14..30. Reserve it in `ReservedAreas`. Own fence (same prefab as the main fence,
  built like `PirateCoveBuilder.BuildCoveFence`) on the west, north and south sides plus the parts of the east side
  that stick out beyond the main fence (v 20.9..30); the main west fence is the rest of the east side.
- **Entrance:** a 3 m gap in the main west fence centred on v = −3 (between the alien field's slime spot at
  v ≈ 2 and its south edge). Remove the main fence pieces whose bounds overlap the gap after `BuildFence`, like
  `PirateCoveBuilder.CutEntrance`. Check that no alien-field prop's bounds come within 2 m of the gap on the inside;
  log a warning if one does.
- **Ground:** grass tiles over the park (like the cove's grass).
- **Dressing** (`ZooParkBuilder.Decor.cs`, static, before the NavMesh bake): 10–14 trees of several kinds along the west
  and south fence, 3 benches and 2 lamp posts beside a path of round stepping stones (entrance gap → zone → pen walk
  point; the stones keep 1.6 m from the hiding spots), flower patches and little bushes under 0.6 m. Everything else
  stays ≥ 3 m from every hiding spot (and its glow), pen, the zone, the count point, the keeper's home, the party
  points, the pen walk point and the walk from the entrance. Colliders only on trunks, benches and lamp posts.
- **Build order:** what is taken away from the main playground (its grass west of the fence, the fence pieces in the
  entrance gap) is destroyed as the very last step of `BuildPark`, so a failed park build leaves no hole.
- **Base tree and zone:** the base tree (`SM_Env_Tree_Large_01`, ~7 m) at (u 7, v 1). `ZooZone` (ActivityZone,
  radius 2.0) at (u 7, v −3), just inside the entrance, so standing in it he looks north at the tree and the park.
  Beacon in a jungle green, icon found by name (try `Paw`, `Animal`, `Eye`, `Search`, fall back to a ModularGameUIKit
  circle sprite). Add the zone to the `ActivityInviter` with group `invite.zoo` (after the existing ones).
- **Keeper home:** (u 9.5, v −1.5) facing south-east (toward the zone).
- **Spots** (centre of the prop), all north of the zone, at least 5 m apart:

  | spot | u | v | | spot | u | v |
  |---|---|---|---|---|---|---|
  | hedge | 15 | 8 | | bush | 29 | 17 |
  | tree | 23 | 6 | | house | 37 | 15 |
  | treetop | 33 | 8 | | leaves | 44 | 12 |
  | pond | 41 | 5 | | table | 6 | 19 |
  | bin | 4 | 11 | | rock | 18 | 20 |
  | tent | 12 | 15 | | box | 21 | 13 |

  Adjust a spot by up to 2 m if its bounds hit another; log the final positions.
- **Pens:** 10 pens in one row along the north fence, v 24..28.5, u 2..46 (4.4 m each), in `ZooAnimalKind.Order` from
  east to west (elephant nearest the entrance). Each pen: a low fence (the main fence prefab scaled to 0.7 m tall) on
  west, north and east, and on the south side two short pieces with a 1.6 m gate gap in the middle; a gate
  (`SM_Env_Fence_Wood_Gate_01`, scaled to fit the gap, hinged on its west end) that `ZooPen` swings; a ground patch
  (sand tile for the savanna kinds, grass for the rest; agent B's choice); a sign beside the gate on its east side (a
  primitive wooden post and a 0.7 × 0.5 m light board, ~1.1 m tall; the cardboard sign has "LemonAid" printed on it)
  with a `SpriteRenderer` quad on the board's south face showing the kind's icon. Pens are
  static; the gate is not. `StandPoint` in the middle of the pen facing south; `GatePoint` 1 m south of the gate.
- **Pen walk point:** (u 24, v 21.5) facing north. **Party points:** 6 points in a row at v 21.5, u 18..30, facing
  south.
- **NavMesh:** all park ground walkable; the pens' fences, covers and containers carve as obstacles (static colliders),
  but the pen interiors may stay unwalkable (animals hop in, they don't path in). Containers have no colliders
  blocking the player hop (use a collider only on the outer walls, or none).

## Components

All runtime types in namespace `CoolSchool.Zoo`, folder `Assets/CoolSchool/Scripts/Zoo/`. The builder wires the
serialized fields **by these exact names**.

### Agent F

```csharp
public partial class HideAndSeekGame : Activity
{
    [Header("Player")]
    [SerializeField] PlayerController player;
    [SerializeField] KidAnimator playerAnimator;

    [Header("Park")]
    [SerializeField] ZooKeeper keeper;
    [SerializeField] ZooAnimal[] animals;          // 10, in ZooAnimalKind.Order
    [SerializeField] Sprite[] icons;               // 10, same order (made by ZooIconRenderer)
    [SerializeField] ZooPen[] pens;                // 10, same order
    [SerializeField] HidingSpot[] spots;           // 12
    [SerializeField] SurpriseCritter[] critters;   // 3
    [SerializeField] CongaLine conga;
    [SerializeField] SparkleTrail trail;
    [SerializeField] Transform penWalkPoint;
    [SerializeField] Transform[] partyPoints;      // 6
    [SerializeField] Transform countPoint;         // in front of the base tree, where they count in step 2

    [Header("UI")]
    [SerializeField] ZooChecklistUI checklist;
    [SerializeField] EyesClosedOverlay eyes;
    [SerializeField] LessonUI lessonUI;

    [Header("Tuning")]
    [SerializeField] float countStepSeconds = 0.9f;
    [SerializeField] float callRadius = 8f;
    [SerializeField] int debugForceLevel = 0;      // 1..3 overrides the saved level (0 = off)
    [SerializeField] bool debugSkipToHideTurn;     // after the count, find everything at once

    public UnityEvent onVictory;
    public event System.Action<int, Vector3> RoundFinished;   // stars, where
}
```
Split it into partial files: `HideAndSeekGame.cs` (fields, begin/end, flow coroutine), `HideAndSeekGame.Seek.cs`
(checks, hints, finds, the left question), `HideAndSeekGame.HideTurn.cs`, `HideAndSeekGame.Recap.cs` (pens, count
question, party). Gate keyboard/pointer polling with `ParentGate.InputBlocked`. Every wait for the child has a safety
net (the hints) and `OnEnd` must survive being called at any moment (the back button).

- `ZooAnimal` (on each animal root; the polyperfect model is a child):
  ```csharp
  [SerializeField] string kindId;
  [SerializeField] Animator animator;          // the polyperfect Animator (isWalking / isRunning bools if present)
  [SerializeField] Transform model;            // the child that bobs, sinks and hops
  [SerializeField] float height = 1f;
  [SerializeField] AudioSource voice;          // 3D, for the call and giggle (spatialBlend 1, min 2 m, max 14 m)
  public string KindId { get; }
  public float Height { get; }
  public bool IsVisible { get; }
  public void Hide();                          // inactive model, no sound
  public void ShowAt(Vector3 position, Quaternion rotation, float sink);   // model sunk by sink * height
  public void HopTo(Vector3 position, Quaternion rotation, float seconds, System.Action done = null);   // arc
  public void WalkTo(Vector3 position, float speed);          // straight, no agent; sets isWalking
  public void SetMoving(bool walking, bool running);         // the Animator bools (mirror AnimalCrosser.SetAnimation)
  public void Call();                          // Sfx cue zoo.call.<id> at the animal, or the pitched giggle
  public void Giggle();
  public void HappyHop();
  ```
  Follow the conga with `WalkTo` every frame from `CongaLine`; no NavMeshAgent on animals.
- `HidingSpot` (on each spot root):
  ```csharp
  [SerializeField] string spotId;              // hedge, tree, ...
  [SerializeField] HidingSpotKind kind;        // Cover, Container, Perch, Under
  [SerializeField] Transform cover;            // the prop (its renderers give the bounds; it shakes)
  [SerializeField] Transform hidePoint;
  [SerializeField] Transform peekPoint;
  [SerializeField] Transform playerHidePoint;  // null for pond and treetop
  [SerializeField] float sinkFraction;         // > 0: the ground may swallow the animal (pond, leaves)
  [SerializeField] float rimHeight;            // containers: rim/roof/water height above the ground
  [SerializeField] float showFraction;         // containers: the part of the animal above the rim
  [SerializeField] float frontOut;             // the tent: the part of the animal's length out of the front
  [SerializeField] float checkRadius = 1.4f;
  [SerializeField] GameObject playerGlow;      // a pulsing ring, inactive
  public string SpotId { get; }
  public bool CanHidePlayer { get; }
  public ZooAnimal Hider { get; }
  public bool Checked { get; }
  public Bounds CoverBounds { get; }
  public float DistanceTo(Vector3 point);      // to the cover bounds edge, flat
  public void Occupy(ZooAnimal animal);        // put it in the hide pose (sizes, facing, sink)
  public void Peek();                          // hide pose → peek pose → back, ~1.6 s, a small rustle
  public void Shake();                         // the cover wobbles hard for 1 s, leaves/sparkle burst
  public ZooAnimal Reveal();                   // marks checked, pops the hider out (returns it) or returns null
  public void SetPlayerGlow(bool on);
  public void ResetSpot();
  ```
  `public enum HidingSpotKind { Cover, Container, Perch, Under }` in its own file.
- `CongaLine` (on the game): `public void Add(ZooAnimal a)`, `public void Clear()`, `public IReadOnlyList<ZooAnimal>
  Members`, `public Transform Leader { get; set; }` (the player, or the keeper during his hide turn). Records the
  leader's path as breadcrumbs (every 0.25 m); member i walks to the breadcrumb at distance `1.4 + sum of sizes` behind,
  runs when far. Members face their walking direction. When the leader stands still they stop in a loose line.
- `ZooKeeper` (on the keeper kid; `KidAnimator` + `NavMeshAgent`, like the knight allies):
  ```csharp
  [SerializeField] NavMeshAgent agent;
  [SerializeField] KidAnimator kidAnimator;
  [SerializeField] Transform home;
  public void AppearAtHome();                  // sparkle, active, at home
  public void GoHome();                        // sparkle, inactive
  public void Follow(Transform target, float distance = 3f);
  public void WalkTo(Vector3 point, System.Action arrived, float timeout = 8f);   // hops the rest on timeout
  public void FaceNorth();
  public void Stop();
  ```
- `SparkleTrail` (on the game; mirror `ActivityInviter.ShowTrail`, which is private, don't change it):
  `public void Show(Transform from, Vector3 to, float seconds = 5f)`, `public void Stop()`. Sparkles along the NavMesh
  path every 1.5 m, refreshed every 0.7 s while showing.
- `SurpriseCritter` (on each critter: polyperfect `Cat_Orange`, `Rabbit_White`, `Squirrel`, inactive):
  `public void PopOut(Vector3 at, System.Action done = null)`: appears with a hop out of the spot, its call or a
  squeak (`zoo.surprise_pop`), runs 3 m east or west, puffs away (sparkle) and goes inactive.
- `RewardSystem` (agent F): `[SerializeField] HideAndSeekGame hideAndSeekGame;` and subscribe to `RoundFinished` →
  `AddStars(Mathf.Clamp(stars, 1, 10), position)` after `rewardDelaySeconds`, like the pirates.

### Agent T

- `ZooAnimalKind` (plain static data, `Scripts/Zoo/ZooAnimalKind.cs`):
  ```csharp
  public enum ZooSize { Small, Medium, Big, Tall, Low }
  public sealed class ZooAnimalKind
  {
      public string Id; public char FirstSound; public string PrefabName; public float Height; public ZooSize Size;
      public string[] AllowedSpots;
      public static readonly ZooAnimalKind[] Order;     // the 10 rows of the table above, in order
      public static ZooAnimalKind Get(string id);
  }
  ```
  Size: elephant Big, giraffe Tall, monkey Small, penguin Small, hippo Big, lion Medium, zebra Medium, crocodile Low,
  flamingo Medium, tiger Medium.
- `ZooProgress` (static helpers): `public static bool IsKnown(string id)`, `public static void MarkFound(string id)`
  (adds to `zooAnimals`, no save), `public static int KnownCount`.
- `ZooPen` (on each pen root):
  ```csharp
  [SerializeField] string kindId;
  [SerializeField] Transform gate;             // swings 90° around its hinge (the gate's pivot or a hinge parent made by B)
  [SerializeField] Transform standPoint;
  [SerializeField] Transform gatePoint;
  [SerializeField] SpriteRenderer sign;        // the icon; black when the kind is unknown
  public string KindId { get; }
  public Transform StandPoint { get; }
  public Transform GatePoint { get; }
  public void OpenGate(bool open);             // 0.4 s swing, Sfx zoo.gate
  public void TakeIn(ZooAnimal animal, System.Action done);   // gate opens, animal walks to the gate point then hops in, gate closes
  public void PlaceInstantly(ZooAnimal animal);
  public void LightSign();                     // black → white tint with a pop and a sparkle
  public void RefreshSign();                   // from ZooProgress.IsKnown
  ```
  A `ZooParkDisplay` component (on the park root) shows, in free play, the animals of known kinds in their pens
  (`PlaceInstantly`, a slow idle wander of ±0.8 m inside the pen every few seconds) and hides the rest; refreshes signs.
  It listens to `ProgressStore.Changed` and does nothing while `HideAndSeekGame` is running.
  `[SerializeField] ZooPen[] pens; [SerializeField] ZooAnimal[] animals; [SerializeField] HideAndSeekGame game;`
- `ZooChecklistUI` (on the touch canvas, top centre, like the treasure map):
  ```csharp
  [SerializeField] RectTransform panel;
  [SerializeField] Image[] slots;              // 5 icon images
  [SerializeField] Image[] slotBacks;          // 5 round backs
  public void Show(Sprite[] icons);            // only icons.Length slots; all black silhouettes (Image.color black)
  public void SetFound(int index);             // colour (white), pop
  public IEnumerator CountSilhouettes();       // pulses each still-black slot in turn with number.<k>; for the left question
  public void Celebrate();                     // all slots hop
  public void Hide();
  ```
- `EyesClosedOverlay` (on the touch canvas, full screen, above the HUD, below the parent gear and quit prompt):
  ```csharp
  [SerializeField] CanvasGroup group;          // dark panel, alpha 0.93 when closed
  [SerializeField] Text digit;                 // Inter-Bold, very big, white with a soft shadow
  public bool IsClosed { get; }
  public void Close();                         // fade 0.4 s
  public void ShowDigit(int number);           // pop
  public void Open();                          // fade 0.4 s, digit cleared
  ```
  Add two soft round "peek holes" (lighter blobs) near the middle so it reads as eyes covered by hands, not a black
  screen; no hand art needed.

**Shared small edits (agent T):**
- `PlayerProgress` (save-compatible, in a short commented block at the end of the field list):
  `public List<string> zooAnimals = new List<string>(); public int zooRounds;`
- `VoiceLineSeeder`: the lines below, in a new partial file `Editor/Voice/VoiceLineSeeder.Zoo.cs` with
  `static IEnumerable<Seed> ZooSeeds()`, and one line in `VoiceLineSeeder.cs`'s apply loop next to `CastleSeeds()` /
  `NumberSeeds()` (follow how those are added).
- `SoundCues` (+ synonyms in `SoundAutoAssign` the way the others are): `music.zoo`, `ambience.zoo`, `zoo.giggle`,
  `zoo.patter`, `zoo.found_pop`, `zoo.peek`, `zoo.shake`, `zoo.surprise_pop`, `zoo.gate`, `zoo.pen_hop`,
  `zoo.hide_in`, `zoo.party`, and `zoo.call.<id>` for the 10 kinds. Synonyms: giggle → giggle, laugh, chuckle;
  patter → footstep, step, patter, run; found_pop → pop, surprise, boing; peek → rustle, leaves, bush; shake → rustle,
  shake, leaves; gate → gate, creak, door; pen_hop → hop, jump, boing; hide_in → rustle, cloth, whoosh; party →
  party, fanfare, celebrate. In `Assets/CoolSchool/Audio/SoundPackPicks.txt` add the polyperfect picks listed under
  "The animals" and `ambience.zoo` = `SFX_Environment_Jungle` (only if the picks file format supports clips from
  `Assets/polyperfect`; otherwise list them in your report and leave them out). Re-read the picks file right before
  editing it.
- Parent area: a row `ParentStrings.RowHideSeek` ("Verstoppertje" / "Hide and seek"), skill `"hideseek"`, max 3,
  below the last existing row (another session is adding a castle row: re-read the files and go after whatever is
  there); grow the window if it doesn't fit.

### Agent B: `Assets/CoolSchool/Editor/Zoo/ZooParkBuilder.cs` (+ partial files)

```csharp
internal static partial class ZooParkBuilder
{
    internal sealed class ParkLayout { /* everything Build() needs from BuildPark() */ }
    internal struct ZooInputs
    {
        public RuntimeAnimatorController KidController;
        public int KidsLayer;
        public PlayerController Player;
    }
    // Static geometry, before the NavMesh bake. Removes the main west fence pieces in the entrance gap.
    internal static ParkLayout BuildPark(Transform root, PlaygroundPrototypeBuilder.EnvironmentInfo info);
    // Characters, the activity, UI and wiring, after the lesson/voice setup (needs LessonUI and the inviter).
    internal static void Build(ParkLayout layout, ZooInputs inputs);
}
```

- `PlaygroundPrototypeBuilder`: add `public ZooParkBuilder.ParkLayout ZooPark;` to `EnvironmentInfo`; in
  `BuildEnvironment()` call `info.ZooPark = ZooParkBuilder.BuildPark(root, info);` **right after the alien field
  line** (it needs the final `MinX` and the fence); in `RunBuild()` call `ZooParkBuilder.Build(...)` right after the
  soapbox street (before `ParentAreaBuilder.Build()`). Nothing else in that file. Another session edits this file too:
  re-read it right before the edit.
- **Animals:** copy the race's polyperfect handling (`SoapboxStreetBuilder.MakeAnimal`, `UrpCopy`,
  `FindVendorMaterial`) into the zoo builder (copy, don't call or change the race's private methods). URP material
  copies go to `Assets/CoolSchool/Materials/Zoo/Animals/`. For each kind: a root `ZooAnimal_<id>` with `ZooAnimal`,
  the stripped model as child scaled to the kind's height, a 3D `AudioSource` (no clip, playOnAwake off). All start
  in their pens' stand points (the display hides unknown ones at run time). Not static.
- **Keeper:** `SM_Chr_Kid_Explorer_01` (fallback `SM_Chr_Kid_Scout_Shorts_01`) built like the knight allies
  (Kid.controller, `KidAnimator`, `NavMeshAgent`, no weapons) + `SM_Chr_Attach_ScoutHat_01` on the head (use
  `AttachmentFit` the way the costumes do). `ZooKeeper` component; home point; inactive at start. Kids layer.
- **Critters:** `Cat_Orange`, `Rabbit_White`, `Squirrel` (heights 0.45 / 0.35 / 0.3), stripped like the animals, each
  with `SurpriseCritter`, inactive.
- **Spots:** each prop placed by bounds at its size, its root `Spot_<id>` with `HidingSpot`, the pose children, and a
  `playerGlow` ring (a flat cylinder, emissive soft yellow material `ZooGlow.mat`, no collider, inactive) around the
  player hide point. The pond: if `SM_Env_Park_Pond_01` is missing or looks like water the user dislikes, a flat
  dark-green disc with a rim of rocks (`SM_Env_Park_Pond_Rocks_0x`) is fine; agent B decides and reports.
- **Icons:** `Editor/Zoo/ZooIconRenderer.cs`: render each kind's prepared model to a 256×256 transparent PNG (3/4 side
  view: camera 55° off the facing toward its left side, slightly above, orthographic, the animal fills 85% of the larger
  side), saved to `Assets/CoolSchool/Zoo/Icons/<id>.png`, imported as Sprite (copy `PirateMapIconRenderer`). The PNGs
  are overwritten in place (GUIDs stay) whenever `IconVersion` differs from the number in `Icons/version.txt`.
  Wire them into `HideAndSeekGame.icons` (same order as `animals`;
  the game passes them to the checklist) and into each pen's `sign` sprite.
- **UI:** `ZooChecklistUI` panel on the touch canvas (a rounded panel sprite from ModularGameUIKit, 5 slots);
  `EyesClosedOverlay` full screen, sibling order above the HUD/checklist and below the parent gear, quit button and
  present popup (it is built before them, so put it right after the existing HUD elements). `LessonUI` is the existing
  one (find it like the pirate builder does).
- **Activity:** `HideAndSeekGame` on a `ZooGame` object under the park root with `CongaLine` and `SparkleTrail`;
  everything wired by the names above; the zone; the inviter entry (`invite.zoo`); the `ZooParkDisplay`.
- Wire `RewardSystem.hideAndSeekGame` in `RewardsSetupBuilder` (one line, like `pirateAdventure`).
- **Preview shots** (`Editor/Zoo/PreviewCapture.Zoo.cs`, added to `PreviewCapture.BuildGroups()` with one line,
  group name `zoo`): the follow-camera view from the zone looking north; a top-down of the park; every spot with a
  fitting animal placed in its hide pose (one shot per spot, follow-camera angle, 4 m back) so the main agent can see
  what shows; the pens row with all animals in.
- Log one summary line: park rect, gap, zone, base tree, spot positions, pen row.

## Voice lines (agent T, `VoiceLineSeeder.Zoo.cs`)

Group = key without the trailing number. The keeper speaks as `Kid`. Numbers use the existing `number.<k>` lines,
letter sounds the existing `letter.sound.<x>` lines, right/wrong the existing `lesson.*` lines where they exist.

| key | speaker | Dutch | English |
|---|---|---|---|
| `invite.zoo.1` | Teacher | Help! De dieren uit de dierentuin zijn ontsnapt! Kom je zoeken? | Help! The zoo animals have escaped! Will you come and look? |
| `zoo.start.1` | Kid | Help! Ik ben de dierenverzorger, en de dieren zijn ontsnapt! | Help! I'm the zookeeper, and the animals have escaped! |
| `zoo.escaped.1` | Kid | Ze hebben zich verstopt in het park. Zoek jij mee? | They're hiding in the park. Will you help me look? |
| `zoo.close_eyes.1` | Kid | Eerst ogen dicht en tellen! Tel je mee? | First close your eyes and count! Count with me? |
| `zoo.ready.1` | Kid | Wie niet weg is, is gezien! We komen eraan! | Ready or not, here we come! |
| `zoo.found_new.1` | Kid | Een nieuw dier! | A new animal! |
| `zoo.found.elephant.1` | Kid | Gevonden! De olifant! | Found you! The elephant! |
| `zoo.found.giraffe.1` | Kid | Gevonden! De giraf! | Found you! The giraffe! |
| `zoo.found.monkey.1` | Kid | Gevonden! De aap! | Found you! The monkey! |
| `zoo.found.penguin.1` | Kid | Gevonden! De pinguïn! | Found you! The penguin! |
| `zoo.found.hippo.1` | Kid | Gevonden! Het nijlpaard! | Found you! The hippo! |
| `zoo.found.lion.1` | Kid | Gevonden! De leeuw! | Found you! The lion! |
| `zoo.found.zebra.1` | Kid | Gevonden! De zebra! | Found you! The zebra! |
| `zoo.found.crocodile.1` | Kid | Gevonden! De krokodil! | Found you! The crocodile! |
| `zoo.found.flamingo.1` | Kid | Gevonden! De flamingo! | Found you! The flamingo! |
| `zoo.found.tiger.1` | Kid | Gevonden! De tijger! | Found you! The tiger! |
| `zoo.left.1.1` | Kid | Nog één dier! | One more animal! |
| `zoo.left.2.1` | Kid | Nog twee dieren! | Two more animals! |
| `zoo.left.3.1` | Kid | Nog drie dieren! | Three more animals! |
| `zoo.left.4.1` | Kid | Nog vier dieren! | Four more animals! |
| `zoo.how_many_left.1` | Kid | Hoeveel dieren zijn er nog verstopt? | How many animals are still hiding? |
| `zoo.count_silhouettes.1` | Kid | Tel de zwarte dieren maar! | Count the black animals! |
| `zoo.right.1` | Kid | Goed zo! | Well done! |
| `zoo.all_found.1` | Kid | Hoera! Alle dieren gevonden! | Hooray! We found all the animals! |
| `zoo.surprise.1` | Kid | Oh! Dat is geen olifant! | Oh! That's no elephant! |
| `zoo.surprise.2` | Kid | Hihi, verkeerd dier! | Hehe, wrong animal! |
| `zoo.surprise.3` | Kid | Hier niet! Zoek maar verder! | Not here! Keep looking! |
| `zoo.clue.1` | Kid | Ik hoor een dier met de… | I can hear an animal with the… |
| `zoo.hint.hedge.1` | Kid | Ik hoor iets achter de heg! | I can hear something behind the hedge! |
| `zoo.hint.tree.1` | Kid | Ik hoor iets achter de boom! | I can hear something behind the tree! |
| `zoo.hint.treetop.1` | Kid | Ik hoor iets boven in de boom! | I can hear something up in the tree! |
| `zoo.hint.pond.1` | Kid | Ik hoor iets in de vijver! | I can hear something in the pond! |
| `zoo.hint.bin.1` | Kid | Ik hoor iets in de prullenbak! | I can hear something in the bin! |
| `zoo.hint.tent.1` | Kid | Ik hoor iets in de tent! | I can hear something in the tent! |
| `zoo.hint.box.1` | Kid | Ik hoor iets in de doos! | I can hear something in the box! |
| `zoo.hint.bush.1` | Kid | Ik hoor iets achter de struik! | I can hear something behind the bush! |
| `zoo.hint.house.1` | Kid | Ik hoor iets in het huisje! | I can hear something in the little house! |
| `zoo.hint.leaves.1` | Kid | Ik hoor iets onder de bladeren! | I can hear something under the leaves! |
| `zoo.hint.table.1` | Kid | Ik hoor iets onder de tafel! | I can hear something under the table! |
| `zoo.hint.rock.1` | Kid | Ik hoor iets achter de steen! | I can hear something behind the rock! |
| `zoo.shake.1` | Kid | Kijk! Daar beweegt iets! | Look! Something's moving over there! |
| `zoo.trail.1` | Kid | Volg de sterretjes! | Follow the little stars! |
| `zoo.your_turn.1` | Kid | Nu ben jij aan de beurt! Loop naar een lichtje en verstop je! | Now it's your turn! Walk to a light and hide! |
| `zoo.not_yet.1` | Kid | Ben je al verstopt? Ik tel nog even door! | Are you hidden yet? I'll keep counting! |
| `zoo.seek_start.1` | Kid | Klaar of niet, ik kom! | Ready or not, here I come! |
| `zoo.seek_wrong.1` | Kid | Zit je hier? Nee… jij bent het niet! | Are you here? No… that's not you! |
| `zoo.seek_wrong.2` | Kid | Hier dan? Nee, niemand! | Here then? No, nobody! |
| `zoo.seek_wrong.3` | Kid | Hmm, hier ook niet! Waar ben je toch? | Hmm, not here either! Where are you? |
| `zoo.seek_wrong.4` | Kid | Oei, verkeerd! Wat ben jij goed verstopt! | Oops, wrong! You're hiding so well! |
| `zoo.seek_close.1` | Kid | Hmmm… ik hoor iets… | Hmmm… I can hear something… |
| `zoo.seek_found.1` | Kid | Gevonden! Hihi, wat een goede verstopplek! | Found you! Hehe, what a good hiding spot! |
| `zoo.seek_found.2` | Kid | Boe! Gevonden! Daar zat je! | Boo! Found you! There you are! |
| `zoo.back_home.1` | Kid | Breng de dieren terug naar de dierentuin! | Bring the animals back to the zoo! |
| `zoo.where.elephant.1` | Kid | Waar zat de olifant? | Where was the elephant? |
| `zoo.where.giraffe.1` | Kid | Waar zat de giraf? | Where was the giraffe? |
| `zoo.where.monkey.1` | Kid | Waar zat de aap? | Where was the monkey? |
| `zoo.where.penguin.1` | Kid | Waar zat de pinguïn? | Where was the penguin? |
| `zoo.where.hippo.1` | Kid | Waar zat het nijlpaard? | Where was the hippo? |
| `zoo.where.lion.1` | Kid | Waar zat de leeuw? | Where was the lion? |
| `zoo.where.zebra.1` | Kid | Waar zat de zebra? | Where was the zebra? |
| `zoo.where.crocodile.1` | Kid | Waar zat de krokodil? | Where was the crocodile? |
| `zoo.where.flamingo.1` | Kid | Waar zat de flamingo? | Where was the flamingo? |
| `zoo.where.tiger.1` | Kid | Waar zat de tijger? | Where was the tiger? |
| `zoo.spot.hedge.1` | Kid | Achter de heg! | Behind the hedge! |
| `zoo.spot.tree.1` | Kid | Achter de boom! | Behind the tree! |
| `zoo.spot.treetop.1` | Kid | Boven in de boom! | Up in the tree! |
| `zoo.spot.pond.1` | Kid | In de vijver! | In the pond! |
| `zoo.spot.bin.1` | Kid | In de prullenbak! | In the bin! |
| `zoo.spot.tent.1` | Kid | In de tent! | In the tent! |
| `zoo.spot.box.1` | Kid | In de doos! | In the box! |
| `zoo.spot.bush.1` | Kid | Achter de struik! | Behind the bush! |
| `zoo.spot.house.1` | Kid | In het huisje! | In the little house! |
| `zoo.spot.leaves.1` | Kid | Onder de bladeren! | Under the leaves! |
| `zoo.spot.table.1` | Kid | Onder de tafel! | Under the table! |
| `zoo.spot.rock.1` | Kid | Achter de steen! | Behind the rock! |
| `zoo.how_many_found.1` | Kid | Hoeveel dieren hebben we gevonden? | How many animals did we find? |
| `zoo.victory.1` | Kid | Alle dieren zijn weer thuis! Dank je wel, dierenvriend! | All the animals are home again! Thank you, animal friend! |

## Coordination

- **Other sessions are working in this repo right now:** the castle adventure, playground toys, the cart garage and a
  rewards/gift-list session. They edit `PlayerProgress`, `VoiceLineSeeder`, `SoundCues`, `SoundAutoAssign`, the picks
  file, `ParentStrings`/`ParentAreaBuilder`, `RewardSystem`, `RewardsSetupBuilder`, `ActivityInviter`,
  `PreviewCapture.cs` and `PlaygroundPrototypeBuilder`. **Re-read each shared file right before every edit**, keep the
  edits small and local, and never reformat, reorder or "tidy" code you don't own. Don't touch `KnightsGame`,
  `Castle*`, `Playground*` toy code, `CartPicker`/cart code, or the reward catalog.
- **Agent F owns:** `Scripts/Zoo/HideAndSeekGame*.cs`, `ZooAnimal`, `HidingSpot`, `HidingSpotKind`, `CongaLine`,
  `ZooKeeper`, `SparkleTrail`, `SurpriseCritter`, the `RewardSystem` subscription.
- **Agent T owns:** `ZooAnimalKind` (+ `ZooSize`), `ZooProgress`, `ZooPen`, `ZooParkDisplay`, `ZooChecklistUI`,
  `EyesClosedOverlay`, `Editor/Voice/VoiceLineSeeder.Zoo.cs`, and the shared small edits listed under agent T.
- **Agent B owns:** `Editor/Zoo/*`, the hook lines in `PlaygroundPrototypeBuilder`, one line in `RewardsSetupBuilder`,
  one line in `PreviewCapture.BuildGroups()`, and the zone's entry in the inviter wiring (builder side only).
- Nobody runs the batch build, voice generation, or commits; the main agent does.
- When done, each agent reports: files touched, anything in this contract it changed or couldn't do, and open
  questions. Nobody edits `Docs/HANDOVER.md`; the main agent does that.
