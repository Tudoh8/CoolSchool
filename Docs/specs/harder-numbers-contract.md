# Harder numbers (levels 6–9): implementation contract

He counts to 20 by heart, but the numbers lesson stops at 10. This work adds new number ideas and numbers up to 20 to the classroom, and then reuses them in the play sections. The rule: **the classroom introduces an idea, and play practises it.** A play section only uses an idea once the numbers level has unlocked it.

Decided with the user on 2026-10-12:
- Everything is built in one go by parallel agents.
- **Voice:** a full sentence per number wherever a number sits inside a sentence (e.g. "Welk getal komt na vijf?"). The old pattern of a sentence ending in "…" followed by a separate `number.N` stays where it already exists.
- **Hopscotch** in the playground comes in batch 2 and is not part of this contract.

Read first: `Docs/HANDOVER.md`, `Docs/specs/classroom-numbers-contract.md`, and the contract of any section you touch.

**Design rules (unchanged):**
- He can't read: everything is spoken, and the visuals are digits, dots and toys.
- He never fails: a wrong pick wiggles and he gets another try; after the second wrong pick, a hint plays.
- A quiet child is helped step by step, and in the end the game does the step for him.
- Every `OnEnd` must survive being called at any moment (the back button).

**Verification:** `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root must print `compile-check: OK`.
- Another session is building the Toy Room, equipment and the cart picker at the same time (`Docs/specs/toy-room-and-equipment-contract.md`).
- If the compile check fails **only** in files you don't own, report it and don't fix it.
- **Do not touch:** `Scripts/Rewards/*`, `ProgressStore`, `PlayerProgress`, `RewardSystem`, `PresentPopup`, `CartPicker`, `CartLooks`, `CartController`, `SoapboxStreetBuilder*`, `PlaygroundPrototypeBuilder`.
- Do not commit. Do not run Unity batch mode (the editor may be open).

---

## Shared: `NumberCurriculum` (exists already; read-only for agents)

`Scripts/Lessons/NumberCurriculum.cs`, namespace `CoolSchool.Lessons`:
- `enum NumberSkill { Counting, FindNumber, GiveMe, QuickLook, Compare, OneMoreOneLess, SameAmount, Ordinals, NumberLine, CountBack, CountOn, Hidden, Gap }`
- `NumberCurriculum.Skill` ("numbers")
- `MaxLevel` (9)
- `HighestNumber` (20)
- `CurrentLevel()`
- `MaxNumber(int level)` / `MaxNumber()`: 3, 5, 5, 7, 10, 10, 15, 20, 20
- `UnlockLevel(NumberSkill)`
- `IsUnlocked(NumberSkill[, int level])`

| Level | Max number | Unlocks |
|---|---|---|
| 1 | 3 | Counting (tap and count, how many) |
| 2 | 5 | FindNumber |
| 3 | 5 | GiveMe, QuickLook |
| 4 | 7 | Compare |
| 5 | 10 | OneMoreOneLess, SameAmount, Ordinals |
| 6 | 10 | NumberLine (after/before), CountBack |
| 7 | 15 | CountOn, Hidden; numbers above 10 appear (ten-frames) |
| 8 | 20 | Gap |
| 9 | 20 | everything mixed |

Nothing may ask for or say a number above 20.

## Shared: voice keys (agent V seeds all of them; everyone else only uses the keys)

**Number words (Teacher):**
- `number.0` = "Nul" / "Zero"
- `number.11` … `number.20` = Elf, Twaalf, Dertien, Veertien, Vijftien, Zestien, Zeventien, Achttien, Negentien, Twintig / Eleven … Twenty

Dutch words used inside sentences: één, twee, drie, vier, vijf, zes, zeven, acht, negen, tien, elf, twaalf, dertien, veertien, vijftien, zestien, zeventien, achttien, negentien, twintig.

| Key | Speaker | Dutch | English |
|---|---|---|---|
| `lesson.quick_look.1` | Teacher | Kijk goed! De stippen zijn er maar heel even. | Look closely! The dots are only there for a moment. |
| `lesson.quick_look_ask.1` | Teacher | Hoeveel stippen zag je? | How many dots did you see? |
| `lesson.compare_more.1` | Teacher | Waar zijn er meer? Tik op die kant! | Where are there more? Tap that side! |
| `lesson.compare_fewer.1` | Teacher | Waar zijn er minder? Tik op die kant! | Where are there fewer? Tap that side! |
| `lesson.compare_hint.1` | Teacher | We tellen ze allebei. Tel maar mee! | Let's count them both. Count along! |
| `lesson.same_amount.1` | Teacher | Doe er net zoveel in de mand als er in het bakje liggen! | Put just as many in the basket as there are in the tray! |
| `lesson.one_more.1` | Teacher | Kijk! Er komt er eentje bij! | Look! One more is coming! |
| `lesson.one_less.1` | Teacher | Kijk! Er gaat er eentje weg! | Look! One is going away! |
| `lesson.how_many_now.1` | Teacher | Hoeveel zijn het er nu? | How many are there now? |
| `lesson.number_after.N.1` (N = 1…19) | Teacher | Welk getal komt na {nl}? | Which number comes after {en}? |
| `lesson.number_before.N.1` (N = 2…20) | Teacher | Welk getal komt vóór {nl}? | Which number comes before {en}? |
| `lesson.number_gap.1` | Teacher | Welk getal mist er? Tel maar mee! | Which number is missing? Count along! |
| `lesson.count_back.1` | Teacher | Tik ze één voor één weg, en tel terug! | Tap them away one by one, and count backwards! |
| `lesson.count_back_done.1` | Teacher | Nul! Allemaal weg! | Zero! All gone! |
| `lesson.count_on.N.1` (N = 2…17) | Teacher | In de mand zitten er al {nl}. Tel maar verder! | There are already {en} in the basket. Keep counting! |
| `lesson.count_on_ask.1` | Teacher | Hoeveel zitten er nu in de mand? | How many are in the basket now? |
| `lesson.hidden.1` | Teacher | Kijk goed! De beker verstopt er een paar. | Watch closely! The cup hides some. |
| `lesson.hidden_ask.1` | Teacher | Hoeveel zitten er onder de beker? | How many are under the cup? |
| `lesson.hidden_hint.1` | Teacher | Zullen we eens onder de beker kijken? | Shall we look under the cup? |
| `race.gate_number.N.1` (N = 11…20) | Announcer | Rij door de {nl}! | Drive through the {en}! |
| `race.gate_after.N.1` (N = 1…19) | Announcer | Welk getal komt na {nl}? Rij erdoor! | Which number comes after {en}? Drive through it! |
| `race.podium_ask.1.1` / `.2.1` / `.3.1` | Announcer | Wie staat er op de eerste / tweede / derde plek? Tik maar aan! | Who is in first / second / third place? Tap them! |
| `race.podium_wrong.1` | Announcer | Bijna! Kijk maar naar de cijfers. | Almost! Look at the numbers. |
| `aliens.baby_joins.1` | Teacher | De baby-alien doet ook mee! Hoeveel aliens dansen er nu? | The baby alien joins in! How many aliens are dancing now? |
| `aliens.one_leaves.1` | Teacher | Oei, eentje rent even naar de raket! Hoeveel dansen er nu nog? | Oops, one runs off to the rocket! How many are still dancing? |
| `aliens.countdown.1` | Teacher | De raket gaat vertrekken! Tel mee terug. Tik op het getal! | The rocket is about to leave! Count down with me. Tap the number! |
| `aliens.liftoff.1` | Teacher | Lancering! Daar gaat de raket! | Lift-off! There goes the rocket! |
| `pirates.compare.1` | Parrot | Twee stapels goud! Welke stapel is het grootst? Tik erop! | Two piles of gold! Which pile is bigger? Tap it! |
| `pirates.compare_hint.1` | Parrot | Kraa! We tellen ze allebei. Tel maar mee! | Squawk! Let's count them both. Count along! |
| `pirates.compare_share.1` | Parrot | De kleine stapel is voor de skeletten. Kijk eens hoe blij ze zijn! | The small pile is for the skeletons. Look how happy they are! |
| `pirates.hidden.1` | Parrot | Kijk goed! Lorre verstopt een paar munten onder de emmer! | Watch closely! Lorre hides some coins under the bucket! |
| `pirates.hidden_ask.1` | Parrot | Hoeveel munten liggen er onder de emmer? | How many coins are under the bucket? |
| `pirates.hidden_reveal.1` | Parrot | Daar zijn ze! Tel maar mee! | There they are! Count along! |
| `pirates.count_on.N.1` (N = 2…17) | Parrot | In de kist zitten al {nl} munten. Tel maar verder! | There are already {en} coins in the chest. Keep counting! |
| `shop.tip.kid.1` | Kid | En nog eentje extra, omdat het zo lekker is! | And one extra, because it's so yummy! |
| `shop.tip.knight.1` | Kid | Voor de beste ijsverkoper: nog één muntje extra! | For the best ice cream seller: one extra coin! |
| `shop.tip.alien.1` | Alien | Bliep! Nog eentje extra voor jou! | Bleep! One extra for you! |
| `shop.tip.dragon.1` | Goblin | Grrroar! Nog een muntje extra! | Grrroar! One more coin! |
| `shop.how_many_now.1` | Narrator | Hoeveel muntjes zijn het nu? | How many coins are there now? |

The group is the key without its trailing `.1`. For example, say `lesson.number_after.7` and `race.podium_ask.2`.

---

## Agent V: voice seeds and the parent row

- **`Editor/Voice/VoiceLineSeeder.cs`** (shared with the other session: **re-read right before editing**, two small edits only, never reformat):
  1. Make the class `public static partial class VoiceLineSeeder`.
  2. In `CreateOrUpdate()`, after the `LetterSeeds()` loop, add `foreach (Seed seed in NumberSeeds()) ApplySeed(table, seed);`.
- **New `Editor/Voice/VoiceLineSeeder.Numbers.cs`:** the other part of the partial class, with `static IEnumerable<Seed> NumberSeeds()`.
  - It yields every line in the table above.
  - Use loops over a Dutch/English word table for the per-number lines.
  - Number words are capitalised when they stand alone, and lowercase inside sentences.
- **`Editor/Parent/ParentAreaBuilder.cs`:** the numbers row's `maxLevel` changes from 5 to `NumberCurriculum.MaxLevel` (9). The scene picks this up on the next rebuild. If `ParentPanel` clamps or labels levels anywhere else, keep it consistent.
- Do not generate audio (the main agent does that later with the editor closed).
- In your report, give the number of new lines and the total Dutch character count, because the user pays per character.

## Agent A: the classroom

**Files:** `Scripts/Lessons/NumbersLesson.cs`, `NumbersLesson.Exercises.cs`, a new `NumbersLesson.Exercises2.cs` (put the new exercises here so the old file stays readable; split further if it passes ~500 lines), `ExerciseType.cs`, `BlackboardDisplay.cs`, `LessonUI.cs` (only if two-digit cards need it), new `TenFrameTray.cs`, new `HidingCup.cs`, `Editor/Lessons/LessonSetupBuilder.cs`.

**Levels:**
- Remove `MaxLevel`, `MaxNumberByLevel` and `Skill` from `NumbersLesson` and use `NumberCurriculum` everywhere.
- Level up and level down stay as they are (3 clean exercises up, 2 helped exercises down).
- Old saves at level 5 continue at 5 and can now climb.

**`ExerciseType`:** add `QuickLook`, `Compare`, `OneMoreOneLess`, `SameAmount`, `NumberLine`, `CountBack`, `CountOn`, `Hidden`. Each type maps to its `NumberSkill` (TapAndCount and HowMany map to Counting).

**`ChooseType`:**
- The first exercise is always TapAndCount.
- After that, the options are every type whose skill is unlocked at the current level. GiveMe still needs the basket, and Hidden needs the cup.
- Weights: a type unlocked **at exactly** the current level counts 3×, all others 1×.
- Never the same type twice in a row.

**Numbers:**
- TapAndCount, HowMany and FindNumber use 1…`MaxNumber(level)`.
- GiveMe stays at 10 or below.
- `BuildChoices` keeps three cards. Above 10, the distractors are near the answer (±1…±3) so a teen is compared with teens.

### Ten-frames (`TenFrameTray`, new, `Scripts/Lessons`)
- A flat, friendly-coloured tray with 2 rows × 5 shallow cells. The cell size is serialized (default 0.2 m).
- The builder makes **two** of them, inactive, under the school, like the basket. Build them from primitives or simple meshes with a URP material asset saved under `Materials/Lessons/`. Their colliders must not block toy taps.
- API:
  - `void Show(Vector3 center, Quaternion facing)`
  - `void Hide()`
  - `Vector3 SlotPosition(int index)`: 0…9. Top row first, left to right **as the camera sees it**; table x is mirrored, see `ToyLayout`.
  - `float CellSize`
- `NumbersLesson` gets `[SerializeField] TenFrameTray[] tenFrames`, wired by the builder.
- **When trays are used:**
  - Any toy count above 10 fills tray 1 completely, then tray 2, so 14 reads as "a full ten and 4".
  - Compare and SameAmount use a tray per group.
- Toys in a tray are scaled to fit a cell: one serialized multiplier, default 0.45, on top of `Countable.scale`.
- Both trays side by side must stay within `maxRowWidth`.
- `ClearProps` hides the trays.

### Blackboard (`BlackboardDisplay`)
- `ShowDots(count)` for counts above 10 draws two chalk ten-frames of dots: a full ten plus the rest. It keeps `PulseDot(i)` working.
- `ShowNumberLine(int[] values, int blankIndex)`: up to 5 big chalk numbers in a row, with a chalk box holding "?" at `blankIndex`.
- `PulseLineSlot(int index)`.
- `FillBlank(int value)`: the "?" turns into the number with a chalk pop.

### The new exercises
All exercises use the existing pieces:
- `AskForNumber` (with its hint and idle flow)
- `HandleIdle`
- `exerciseMadeMistake` / `exerciseNeededHelp`
- `CelebrateAnswer` / `ShowTotal`
- `Cheer`

| Type | Flow |
|---|---|
| **QuickLook** (L3) | n = 1…min(max, 6). Say `lesson.quick_look`, then the board shows `ShowDots(n)` for 1.5 s (1.0 s from level 6) and clears. Say `lesson.quick_look_ask`, then `AskForNumber(n, min(max, 6))`. The hint shows the dots again and counts them (`CountDotsAloud`). |
| **Compare** (L4) | Two groups of the same toy, one in each tray. Counts are 1…min(max, 10) and differ by ≥ 2 (≥ 1 from level 6). Up to level 5 the question is always "more"; from level 6 it is 50/50 `lesson.compare_more` / `lesson.compare_fewer`. A tap on **any toy** of a group picks that group. Right: the group's toys hop, then `lesson.correct` + `Cheer`. Wrong: the tapped group wobbles, then `lesson.try_again`; the 2nd wrong pick plays the hint: `lesson.compare_hint`, then both groups are counted aloud (`Pop` with numbers), then the right group pulses. Idle help: pulse the right group. Auto-solve: pick it. |
| **OneMoreOneLess** (L5) | n = 2…max−1. Toys appear, then `ShowTotal(n)` (the board shows n, "Het zijn er…" + `number.n`). Then 50/50: **one more** (an extra toy appears at the end of the layout; the layout is reserved for n+1 up front) with `lesson.one_more`, or **one less** (the last toy hops off with a sparkle and is removed from `toys`) with `lesson.one_less`. The board clears. Say `lesson.how_many_now`, then `AskForNumber(n±1, max)` with the hint `CountToysAloud`. |
| **SameAmount** (L5) | Left: n toys in a tray (n = 2…min(max, 8)). Right: the basket with n+3 loose toys behind it (reuse the GiveMe put-in / take-out code; the basket is full at n). Say `lesson.same_amount`. When the basket holds n: the tray toys are counted, then the basket toys, both ending on n, then `CelebrateAnswer(n)`. Idle help: count the tray toys aloud, then pulse the next loose toy. Auto-solve: put one in. |
| **NumberLine** (L6) | Four consecutive numbers within 1…max. The **after** variant shows `k, k+1, k+2, ?` and asks `lesson.number_after.(k+2)`. The **before** variant shows `?, k+1, k+2, k+3` and asks `lesson.number_before.(k+1)`. From level 8, a **gap** variant (`Gap` skill): the blank is one of the two middle slots, `lesson.number_gap`. Cards: the answer plus two neighbours (±1, ±2) inside 1…max. Hint: the teacher counts along the line (`PulseLineSlot` + `number.x`) and stops before the blank, then the answer card pulses. Right: `FillBlank`, then `CelebrateAnswer`. |
| **CountBack** (L6) | n = 5…min(max, 10) toys in a row; the board shows n. Say `lesson.count_back`. Every tap on any toy: it hops away with a sparkle, the board shows what is left, and `number.left` is said (down to `number.0`). At zero: `lesson.count_back_done` + `Cheer`. It can never be wrong. Idle help: pulse a toy. Auto-solve: remove one. |
| **CountOn** (L7) | The basket with a floating chalk-style digit K above it (a small world-space label; it stays K). K = 2…min(17, max−1). M = 1…min(3, max−K) toys lie on the table. Say `lesson.count_on.K`. **Levels 7:** each tap flies a toy into the basket while the teacher says K+i; at the end, `ShowTotal(K+M)`. **Level 8 and up:** the taps only plop (no voice), then `lesson.count_on_ask` + `AskForNumber(K+M, max)`. Its hint counts on aloud: the digit pulses + `number.K`, then K+1…K+M while each basket toy hops. |
| **Hidden** (L7) | Total T = 3…5 (L7), 3…10 (L8+), capped by max. h = 1…T−1 will be hidden. Toys appear, then `ShowTotal(T)` (the digit **stays on the board** as a reminder). Say `lesson.hidden`: the cup lands on the right side of the table and the h rightmost toys hop under it (shrink and switch off). Say `lesson.hidden_ask`, then `AskForNumber(h, T−1)`. The hint: `lesson.hidden_hint`, the cup lifts, the hidden toys hop out beside it and are counted aloud, then the cup comes down over them again. Right: the cup lifts, the toys hop out, then `CelebrateAnswer(h)`. |

**`HidingCup` (new, `Scripts/Lessons`):**
- An upside-down cup or pot. The builder finds it by name: `SM_Prop_Pot_01` from PolygonKids, else `SM_Gen_Prop_Pot_01`, else a primitive cylinder with a bright material. It is placed inactive, scaled to about 0.35 m wide, with its colliders removed.
- API:
  - `IEnumerator CoverAt(Vector3 tablePoint)`: drops in from above with a little bounce and `Sfx.Play("lesson.chalk")` (or another existing cue).
  - `IEnumerator Lift()`
  - `void Hide()`
  - `Vector3 UnderPoint`
- `NumbersLesson` gets `[SerializeField] HidingCup hidingCup`. `ClearProps` hides it.

**Builder (`LessonSetupBuilder`):** create the two trays and the cup, and wire `tenFrames` and `hidingCup` the same way the basket is wired.

## Agent B: race gates and the podium

**Files:**
- `Scripts/Race/SoapboxRace.Gates.cs` (the main work)
- a new `Scripts/Race/SoapboxRace.Podium.cs`
- **one-line** edits in `SoapboxRace.cs` and `SoapboxRace.Finish.cs`

The other session's agent R also edits `SoapboxRace*.cs`: **re-read each file right before each edit**, keep the edits tiny, and never reformat.

- **`SoapboxRace.cs`:** delete the `MaxNumberByLevel` field (and its comment).
- **Gates:**
  - Use `NumberCurriculum.MaxNumber()`; numbers go up to 20.
  - Check that two-digit glyphs fit on the `LearningGate` arches and in the HUD target. If they don't, shrink the font for two digits in that code; `LearningGate` and `RaceHud` are not owned by agent R, but keep the edits minimal.
- **After-gates:**
  - When `NumberSkill.NumberLine` is unlocked, each number gate has a 50% chance to become an after-gate. Add `bool after` to `GateQuestion`.
  - Ask about N with N = 1…max−1; the answer is N+1.
  - The two other lanes are picked from {N, N+2, N−1, N+3} inside 1…max. N itself is a good distractor.
  - Voice: `race.gate_after.N` (question and reminder).
  - HUD target glyph: `N→?`. If Inter lacks "→", use `N ?`.
  - Lane rules, bonuses and level-1 lane moving stay as they are.
- **Podium (`SoapboxRace.Podium.cs`):** `IEnumerator RunPodiumQuestion()`.
  - **Hook:** `RunFinish` calls it once, right after the confetti and **before** the stars line `int stars = …`. Only call it when `NumberSkill.Ordinals` is unlocked.
  - **Digits:** world-space digits "1", "2" and "3" float above the three podium places, facing the camera, in the Inter font with a white outline. They show for the whole podium time and are removed at the end and in `OnEnd`.
  - **Question:** ask `race.podium_ask.P`, where P is a random place.
  - **Tapping:**
    - Find the nearest of the three characters (player and rivals) on screen to the finger, within about 15% of the screen height. Skip taps while `ParentGate.InputBlocked`.
    - Right: that character cheers or hops, then `race.gate_right`.
    - Wrong: the tapped one hops, then `race.podium_wrong`.
  - **Help:** after the 2nd wrong tap, or 8 s without a tap, the right place's digit pulses and a sparkle plays over the character every second. After 16 s the game answers itself (sparkle + `race.gate_right`).
  - The whole question never takes more than 20 s.
  - It has no effect on stars or the race level.

## Agent C: aliens and the ice cream shop

**Files:** `Scripts/Aliens/AlienLanding.cs` (split off a partial `AlienLanding.Numbers.cs` for the new code), `Scripts/Shop/IceCreamShop.cs` (+ a new partial `IceCreamShop.Tip.cs` if useful).

### Aliens
- **`MakeCards`:** the range becomes 1…20.
- **Follow-up after the count** (`NumberSkill.OneMoreOneLess` unlocked): after the dancers' count is answered, choose 50/50.
  - **One more (the baby):** needs the baby waiting (`BabyWaitsForCount`). The baby walks into the shot to the next party spot and dances. Say `aliens.baby_joins`, then show cards for count+1. Wrong: wiggle, dim, `lesson.try_again`, then count along including the baby. Right: celebrate. `RunBabyJoinsParty` must then do nothing (the baby is already there).
  - **One less:** needs ≥ 3 dancers. The last dancer hops out of the shot and is hidden. Say `aliens.one_leaves`, then cards for count−1, then count along over the remaining dancers. The dancer comes back with a sparkle to its party spot before the party.
  - If the chosen variant isn't possible, use the other one; if neither is possible, skip.
  - The follow-up does **not** change the aliens level.
- **Rocket countdown** (`NumberSkill.CountBack` unlocked), in `RunParty` right before take-off:
  - Say `aliens.countdown`. Start at 10, or at 20 when the numbers level is 9.
  - One big number card in the middle (`lessonUI.ShowNumberCards(new[] { k }, …)`).
  - Tapping the card, or 1.6 s without a tap, goes to k−1, says `number.k` (interrupt) and shows the new card. Taps are at least 0.7 s apart.
  - At 0: `number.0`, hide the card, `aliens.liftoff`, then the take-off as now.
  - Hide the gameplay HUD while the card shows and restore it after (also in `OnEnd`).

### Ice cream shop
- **Tip coin** (`NumberSkill.OneMoreOneLess` unlocked): after all of a customer's coins are tapped into the register, there is a 50% chance (at most 2 customers per visit) that the customer says `shop.tip.<kind>` (kid / knight / alien / dragon).
  - One extra coin hops onto the counter. Say `shop.how_many_now`, then number cards (`lessonUI`) for count+1.
  - Wrong: wiggle, dim, `lesson.try_again`, then the narrator counts 1…count and the tip coin hops on count+1; then the right card pulses.
  - Right: celebrate, then the tip coin flies into the register with the existing coin cue.
  - The tip does not change `shopLevel` or the stars.

## Agent D: pirates

**Files:** `Scripts/Pirates/PirateAdventure*.cs` (new code in a new partial `PirateAdventure.Numbers.cs`), `TreasureChest.cs`, `TreasureCoin.cs` (if needed), `Editor/Pirates/PirateCoveBuilder*.cs` (the bucket only). The other session changes one line in `ShoulderParrot.cs`; don't touch that file.

- **`MakeNumberCards`:** the range becomes 1…20.
- **Choosing an extra:** in `RunCountTreasure`, before the coins spill, pick one extra for this visit from the unlocked ones plus "none":
  - Compare (L4)
  - Hidden (L7)
  - CountOn (L7)
  - never the same as last visit (a static field)
  - Every count stays inside `NumberCurriculum.MaxNumber()` and inside what the chest can spill.
- **Stars:** `TreasureCounted` and `pirateCoinsTotal` count only the coins that came out of the chest **this** visit (the chosen pile, the hidden-trick total, or the M new coins). Never a remembered K.

| Extra | Flow |
|---|---|
| **Compare** | The chest spills **two piles** (add `TreasureCoin[][] SpillPiles(int left, int right)` to `TreasureChest`: two fans side by side, as the camera sees them). The pile sizes come from the pirates level's coin range and differ by ≥ 2. Say `pirates.compare`. A tap on any coin (through `TreasureCoinPicker`) picks its pile. Right (the bigger pile): its coins pulse, Lorre squawks, then `pirates.compare_share`; the small pile's coins fly toward the skeleton crew and sparkle away. Wrong: `mistakes++`, then `pirates.compare_hint`, both piles counted aloud (coin pulse + `number.k`), then the bigger pile pulses. After that, the bigger pile is counted into the chest with the existing tap flow and `AskHowMany`. |
| **Hidden** | T coins (3…5, or 3…10 from numbers level 8) spill, and Lorre counts them himself (pulse + `number.k`). Say `pirates.hidden`: the **bucket** drops onto the right side of the fan and h = 1…T−1 coins slide under it and switch off. Say `pirates.hidden_ask`, then cards (answer h, choices inside 1…T−1, `NumberCardsByLevel`). Wrong: `mistakes++`, wiggle, `lesson.try_again`; after the 2nd wrong pick, `pirates.hidden_reveal`: the bucket lifts, the coins are counted aloud, the bucket drops again, and the right card pulses. Right: the bucket lifts and the coins come back. Then all T are counted into the chest as usual (skip the second `AskHowMany`; the trick was the question). |
| **CountOn** | The chest already holds K coins (K = 2…min(17, max−3)). A gold digit K floats above the chest (world-space label, stays K). Say `pirates.count_on.K`. M = 1…3 new coins spill. Each tap: the coin flies in and Lorre says `number.(K+i)`. Then `AskHowMany(K+M)`; its count-along hint says K, then K+1…K+M. |

- **The bucket (builder):**
  - Spawn one inactive upside-down sand bucket in the cove, found by name: `SM_Prop_Bucket_01` from PolygonTown, else from PolygonKids.
  - About 0.45 m wide, colliders removed, wired as `[SerializeField] Transform hidingBucket` on `PirateAdventure`.
  - The drop and lift animations live in `PirateAdventure.Numbers.cs`. Hide the bucket on reset and in `OnEnd`.

---

## File ownership (summary)

- **V:** `VoiceLineSeeder.cs` (2 small edits), `VoiceLineSeeder.Numbers.cs`, `ParentAreaBuilder.cs`.
- **A:** `NumbersLesson*.cs`, `ExerciseType.cs`, `BlackboardDisplay.cs`, `LessonUI.cs`, `TenFrameTray.cs`, `HidingCup.cs`, `LessonSetupBuilder.cs`.
- **B:** `SoapboxRace.Gates.cs`, `SoapboxRace.Podium.cs`, one-line edits in `SoapboxRace.cs` and `SoapboxRace.Finish.cs`, minimal font edits in `LearningGate.cs` / `RaceHud.cs`.
- **C:** `AlienLanding*.cs`, `IceCreamShop*.cs`.
- **D:** `PirateAdventure*.cs`, `TreasureChest.cs`, `TreasureCoin.cs`, `PirateCoveBuilder*.cs`.
- **Main agent:** `NumberCurriculum.cs`, this contract, `Docs/HANDOVER.md`, voice generation, rebuild and review.

## Done when
- `compile-check: OK`.
- The scene is rebuilt, and the Dutch voice lines are generated.
- In the editor with the parent slider at 9: the lesson shows the new exercises with ten-frames, the cup and the number line.
- With the slider at 5–7: race after-gates and the podium question, the aliens follow-up and countdown, the shop tip coin, and the pirate extras all show up.
