# Letters lesson and parent area: implementation contract

Two agents work in parallel:
- **Agent L (letters):** a second classroom lesson that teaches letters (sounds, recognition, first sounds of words, tracing, spelling his name). The classroom alternates between the numbers lesson and the letters lesson.
- **Agent P (parent area):** a locked parent screen with the child's name and a difficulty slider per subject.

Read these first:
- `Docs/HANDOVER.md`
- `Docs/specs/classroom-numbers-contract.md`
- `Docs/specs/voice-and-rewards-contract.md`
- the lesson code in `Scripts/Lessons/` (`NumbersLesson*.cs`, `LessonUI`, `BlackboardDisplay`, `CountableSet`, `LessonStage`, `LessonCameraRig`)
- `Editor/Lessons/`

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print `compile-check: OK`.

**Don't:**
- run Unity batch commands
- generate voice
- commit

The main agent does all three.

**Design rules:**
- The child is 4 and can't read yet. Everything he needs is spoken.
- Only the parent area may use written text, because parents read it.
- He never fails: a wrong answer gets a friendly wiggle and a hint, and the right answer glows after a second mistake.
- Use the new Input System only. Never write a bare `Input.` inside `CoolSchool.*`.
- UI uses `UnityEngine.UI` with `Inter-Bold`. No TextMeshPro.

---

## Shared data (`Scripts/Rewards/PlayerProgress.cs` and `ProgressStore.cs`)

Re-read these files right before each edit; both agents add fields.

| Field / API | Owner | Meaning |
|---|---|---|
| `GetLevel(skill)` / `SetLevel(skill, level)` | exists | Adaptive level per skill. The skills are `"numbers"` (1–5, existing), `"letters"` (1–5, new) and the shop's `shopLevel` field (1–3, existing). |
| `public string childName = "";` | P | Lowercase letters a–z only, trimmed, at most 12 characters. Empty means not set. |
| `public List<string> lettersIntroduced` | L | Letters already introduced, in the order they were introduced. |
| `public List<SkillLevel> letterScores` | L (or a similar structure) | First-try-correct counts per letter, for choosing which letters to review. |
| `public string lastLessonSubject = "";` | L | `"numbers"` or `"letters"`, so the classroom can alternate. |
| `public static void ProgressStore.SetChildName(string name)` | P | Normalises the name, saves, and fires `Changed`. |
| `public static int ProgressStore.ShopLevel { get; set; }` | P | Wraps `shopLevel`, clamped 1–3, saves. Optional; P may write the field directly. |

**Levels are a starting point, not a lock.** A parent moving a slider calls `SetLevel` (or sets `shopLevel`) and saves. From then on the lessons keep adapting from that level exactly as now. There is no "auto" toggle, because the slider just moves the current level. The lessons must read the level fresh at each lesson start, never cache it across lessons.

---

## L. Letters lesson (agent L)

### Classroom rotation
- **Alternating subjects.** Walking into the classroom zone starts **numbers** or **letters**, alternating via `lastLessonSubject`. The very first lesson ever is numbers.
- **Selector hook.** Implement it cleanly. Suggestion: an optional `IActivitySelector` / `ActivitySelector` component on the zone, which `ActivityZone` asks for the activity to start. Fall back to the zone's own `activity` when there is none. Keep the change to `ActivityZone` minimal.
- **Shared teacher code.** Both lessons use the same `LessonStage`, camera, teacher, `LessonUI`, blackboard and table. If it helps, extract a shared base class (for example `ClassroomLesson : Activity`) for what both need:
  - the camera in and out
  - seating the child
  - teacher gestures
  - `Say` / `WaitForSpeech`
  - the tap raycast
  - idle detection
  - the outro with stars and the golden sword
  - cleanup in `OnEnd`

  **NumbersLesson must behave exactly as before.** Keep its serialized field names, or migrate them in the builder.
- **Outro.** Same as numbers: stars (`announce: false`), then the golden sword moment (`GoldenSwordReward`, `lesson.powerup` / `lesson.powerup_waiting`).

### Letter data (ScriptableObjects, seeded by an editor seeder like `CountableSetSeeder`)
- **`LetterSet` asset.** One entry per letter a–z:
  - `char letter`
  - `string soundKey` (voice group `letter.sound.<x>`)
  - `List<LetterWord> words`
  - `Vector2[][]` stroke data for tracing (or a serializable equivalent, e.g. a list of strokes, each a list of points in a 0–1 box with y up)
- **`LetterWord`.** `string word` (Dutch, lowercase), `string voiceKey` (`letter.word.<word>`) and a prefab, which is a 3D prop found by name in the Synty packs, placed and scaled by renderer bounds like the countable toys.
- **Words.** Search the packs for the candidates below and only keep words with a fitting prop. Report which ones you kept. Aim for at least 2 words for each of the first-taught letters.

| Letter | Candidate words |
|---|---|
| a | appel, auto, anker |
| b | bal, beer, boom, bank, boot, banaan, brood, bus, bloem, boek |
| d | deur, doos, draak |
| e | eend |
| f | fiets, fles |
| g | gitaar, glas |
| h | huis, hond, hoed, helm |
| j | jas, jurk |
| k | kat, kasteel, kaas, kist, kroon |
| l | lamp, lepel, ladder |
| m | maan, muis, mand, muts |
| o | oven |
| p | pop, pet, paraplu, peer, pizza |
| r | raket, robot, ring |
| s | sok, ster, schep, stoel |
| t | taart, tafel, trein, tent, tas, tractor |
| v | vis, vlag, vork, vliegtuig |
| w | wiel, wortel |
| z | zon, zwaard, zeep |

  Avoid words that start with a digraph sound (sch is okay to skip; ij, ui, eu, oe, ei are not single letters).
- **Strokes.** Author stroke data for all 26 lowercase letters in a simple school-print style: a single-storey "a" and "g" if you can, written the way Dutch kindergartens teach. Strokes go in writing order and direction. Keep each letter to 1–3 strokes, with points roughly 0.05–0.1 apart.

### Letter order
1. The letters of `childName` come first, in name order, without duplicates.
2. Then the school order: `m r s v i k a p n t e b o d l h w z j g f u c y x q`. Skip letters already taken.
3. **Changing the name later.** Letters already introduced stay introduced. Only the order of the letters not yet introduced changes.

### Exercises
Each lesson has 4 exercises, like numbers.

| Type | What happens |
|---|---|
| **NewLetter** (intro) | When a new letter is due, it is written big on the blackboard with a chalk sound and sparkle. The teacher says "Dit is een nieuwe letter!", then the sound (`letter.sound.x`), then a word with a 3D prop popping up on the table: "mmm… maan". Then it goes straight into a short **Trace** of that letter. |
| **FindLetter** | The teacher says "Waar is de…" plus the sound. 2 or 3 big letter cards appear (the same `LessonUI` number cards, showing a lowercase letter; add a letter mode if needed). Tap the right one. A wrong card wiggles and dims, the sound is repeated, and the right card pulses after the second mistake. |
| **StartsWith** | 2 or 3 props stand on the table (different first letters). The teacher says "Wat begint met…" plus the sound. Tap the right prop. Then the teacher says the word, then "begint met", then the sound, e.g. "Maan… begint met… mmm". |
| **Trace** | The letter is shown big on the blackboard as a dotted path, with a pulsing start dot and arrows. The child drags a finger along each stroke in order, and a chalk line and sparkle trail follow the finger. Be very generous: progress along the stroke counts when the finger is within about 12% of the board height, and lifting the finger keeps the progress. After 12 s idle, a ghost hand or dot shows the stroke. When finished, the letter fills in and the teacher says `letters.trace_done`. |
| **SpellName** (level 5 and up, only when `childName` is set) | Empty slots on the board, one per letter of the name. Shuffled letter cards appear below. Each slot is filled in order: the teacher says the letter's sound, the child taps the card, and it flies to the slot. A wrong card wiggles. When the name is done, all letters bounce and the teacher says `letters.name_done`. Names longer than 8 letters: only the first 8. |

### Levels (`"letters"`, 1–5)

| Level | Exercises |
|---|---|
| 1 | NewLetter or Trace, FindLetter with 2 cards (letters that look very different) |
| 2 | FindLetter with 3 cards, StartsWith with 2 props |
| 3 | StartsWith with 3 props, Trace of review letters |
| 4 | All types mixed over all introduced letters; similar-looking letters may be mixed (b/d, m/n) |
| 5 | Also SpellName (when a name is set) |

- **When a new letter is introduced:** once the current newest letter has 3 first-try-correct answers (across its exercises), or after every 2nd lesson at the latest. Introduce at most one new letter per lesson. A lesson needs at least 1 introduced letter, so the first lesson always introduces one.
- **Level up and down,** after each lesson, the same as numbers:
  - up when there were no mistakes
  - down when there were 3 or more mistakes
  - clamped to 1–5

### Voice lines (agent L adds them to `VoiceLineSeeder.cs`)

All are `Speaker.Teacher`.

| Key | NL | EN |
|---|---|---|
| lesson.letters.intro.1 | Welkom in de klas! Vandaag gaan we letters leren. | Welcome to class! Today we're learning letters. |
| letters.new.1 | Dit is een nieuwe letter! | This is a new letter! |
| letters.find.1 | Waar is de… | Where is the… |
| letters.starts_with.1 | Wat begint met… | What starts with… |
| letters.begins_with.1 | …begint met… | …starts with… |
| letters.trace.1 | Teken de letter met je vinger. Volg de stippen! | Draw the letter with your finger. Follow the dots! |
| letters.trace_done.1 | Wat mooi getekend! | Beautifully drawn! |
| letters.name.1 | Weet je wat hier komt te staan? Jouw naam! Zoek de letters. | Do you know what goes here? Your name! Find the letters. |
| letters.name_done.1 | Dat is jouw naam! Knap gedaan! | That's your name! Well done! |
| letter.word.<word>.1 | the word with a capital and a full stop, e.g. "Maan." | the English word, e.g. "Moon." |
| letter.sound.<x>.1 | see the sound spellings below | the English letter sound, e.g. "mmm" |

**Sound spellings (NL).** Dutch kindergarten sounds. These go to TTS and will be tuned by ear after generation, so put them in one clearly marked table in the seeder:

| Letters | Spelling |
|---|---|
| m, s, r, f, v, z, l, n, w, h, g | "Mmm.", "Sss.", "Rrr.", "Fff.", "Vvv.", "Zzz.", "Lll.", "Nnn.", "Www.", "Hhh.", "Ggg." |
| a, e, i, o, u (short vowels) | "Ah.", "Eh.", "Ih.", "Oh.", "Uh." |
| p, t, k, b, d | "Puh.", "Tuh.", "Kuh.", "Buh.", "Duh." |
| j | "Juh." |
| c | "Kuh." (as in "cadeau") |
| y | "Ie." |
| x | "Ks." |
| q | "Kuh." |

Words: generate one `letter.word.<word>` line for each word kept.

### Builder
- **Scene objects.** Extend `LessonSetupBuilder` (and a `LetterSetSeeder`) so the letters lesson, its data and the rotation hook end up in the generated scene.
- **Blackboard.** It must be able to show a big lowercase letter, a dotted trace path, the chalk line drawn by the finger, and name slots. Extend `BlackboardDisplay` or add a `LetterBoard` component on the same board.
- **Sound cues.** Add `lesson.trace_progress` (soft chalk scratch while tracing) and `lesson.letter_pop` to `SoundCues.cs` and `SoundAutoAssign.cs`.

---

## P. Parent area (agent P)

- **The lock.** A small, low-contrast gear icon sits in the **top-left** corner of the TouchUI canvas, away from the joystick and the action button, and is always visible.
  - Opening it needs a **hold of 3 seconds**: a ring fills around the gear and resets on release.
  - A tap alone does nothing. The point is that a 4-year-old won't open it by accident.
- **The panel** (`ParentPanel`, screen-space overlay above everything, built by an editor builder): a calm, readable panel with written text, using ModularGameUIKit panel and button sprites and the `Inter-Bold` font.
  - While it is open, the game pauses: `Time.timeScale = 0`, voice and audio paused (`AudioListener.pause = true`), and gameplay input ignored. Everything resumes on close.
  - **Name** ("Naam van je kind"): an `InputField`. It is saved through `ProgressStore.SetChildName` when the panel closes (or on end-edit), so only a–z are kept, lowercase. Show a hint that the letters of the name are taught first.
  - **Difficulty** ("Moeilijkheid"): one row per subject.
    - **Getallen:** `"numbers"`, 1–5.
    - **Letters:** `"letters"`, 1–5.
    - **IJswinkel:** `shopLevel`, 1–3.

    Each row has a whole-number `Slider` with big handles, the current level shown as a number, and a short caption under it, e.g. "Past zich automatisch aan; de schuif zet het huidige niveau." Moving a slider immediately sets that level and saves. The slider shows the current value each time the panel opens, because the game may have changed it in the meantime.
  - **Close:** a big "Sluiten" button.
  - Leave room or structure for later settings (language, volume, time limit). Don't build those now.
- **Text.** Keep the parent texts in one small place (a static NL/EN string table in code is fine), with Dutch shown. Don't put them in the voice `LineTable`.
- **Builder.** `Editor/Parent/ParentAreaBuilder.cs`, called from `PlaygroundPrototypeBuilder.RunBuild()` after the UI exists (re-read that file right before editing; keep the edit tiny). Also add the gear's sprite lookup.

## Coordination
- **File ownership.**
  - Agent L owns `Scripts/Lessons/*`, `Editor/Lessons/*`, `ActivityZone.cs` (the selector hook), `VoiceLineSeeder.cs` and `SoundCues.cs` / `SoundAutoAssign.cs`.
  - Agent P owns `Scripts/Parent/*` (namespace `CoolSchool.Parent`) and `Editor/Parent/*`.
  - Both touch `PlayerProgress.cs`, `ProgressStore.cs` and `PlaygroundPrototypeBuilder.cs`. Re-read them right before each edit and keep edits minimal.
- **Compile errors from the other agent.** If the compile check fails only in the other agent's in-progress files, wait briefly and re-run.
- **Report back:**
  - the files changed and created
  - the words kept per letter (L)
  - what needs tuning in play
  - anything you couldn't verify
