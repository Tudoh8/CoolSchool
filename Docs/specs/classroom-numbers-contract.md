# Classroom and first numbers lesson: implementation contract

Two agents work in parallel:
- Agent S (School) builds the school building and classroom, with its anchors.
- Agent L (Lesson) builds the lesson framework and the numbers lesson.

Read these first:
- `Docs/specs/knights-slice-contract.md` (architecture: Activities, builders, by-name asset lookup)
- `Docs/specs/voice-and-rewards-contract.md` (VoicePlayer and LineTable seeding; ProgressStore and RewardSystem)

**Verification:** run `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print `compile-check: OK` before you report back.

**The player** is a 4-year-old who can't read:
- Everything is spoken. Visuals are big and use digits and pictures, never words.
- He never fails. A wrong answer gets a friendly hint and another try.
- The lesson language is Dutch first, with English through the line tables.

## Where it lives (one world)

The camera always looks north (+Z), so "up" on screen is north. The school sits on the **north (+Z) side** of the playground and the knights arena is on the **east (+X) side**; the playground ring is centred at the origin.
- A gap in the north fence (about 3 m, centred at x = 0) leads to the school's front door.
- The door is in the school's **south** wall and faces the playground (and the camera). The blackboard is on the north wall, so the lesson camera looks north like the game camera.
- The player starts in the south of the playground (z = -(fenceHalf - 4)), facing north, and walks up to the school.

---

## S. School building and classroom (agent S)

### Editor: `Assets/CoolSchool/Editor/School/SchoolBuilder.cs`
- `public static SchoolResult Build(Transform parent, Vector3 fenceGapPoint, Vector3 doorFacing)` (the middle of the fence gap, and the direction from the school toward the playground; for the north school (0, 0, -1)) creates a root GameObject named **"School"**:
  - **Shell:** one classroom of about 10 m (x) by 8 m (z) by 3.5 m, made of Synty Generic modular pieces (`SM_Bld_Base_Wall`, `SM_Bld_Base_Wall_Window`, `SM_Bld_Base_Wall_Door_Double` or `SM_Bld_Base_Wall_Door`, `SM_Bld_Base_Floor`, the roof/ceiling pieces).
    - Find them by name and measure segment sizes from bounds.
    - The door is in the wall facing the playground (south wall for the north school), centred.
    - Put a school sign above the door if a fitting sign prefab exists. Otherwise skip it; there is no text.
  - **Interior props** (Kids pack unless noted, by name):
    - `SM_Prop_Blackboard_01` on the wall opposite the door (north wall), facing the door
    - a low **lesson table** (`SM_Prop_Table_01`, or `SM_Prop_Desk_01`) about 2 m in front of the blackboard
    - 4–6 kid desks (`SM_Prop_Desk_*`) with chairs (`SM_Prop_Chair_Desk_*`) behind it
    - lockers (`SM_Prop_Locker_*`) along a side wall
    - Town `SM_Prop_Bookshelf_*` and `SM_Prop_Clock_*`
    - a rug or `SM_Prop_FloorMat_*`, if found
    - one or two warm point lights inside
  - **Teacher:**
    - Town `SM_Chr_Mother_01`, falling back to City `Character_BusinessWoman`, as the child of a "Teacher" root at the TeacherSpot.
    - It uses a new **Teacher.controller** (editor-built): Idle → `HumanM@Idle01`, a "Talking" bool → a talking-gesture clip, and a "Wave" trigger → a wave clip.
    - Search the Conversation Gestures and Gestures MoCap packs and Kevin Iglesias by name for "Talk", "Converse" and "Wave". Use the first humanoid clip found and report which.
    - Applies runtime `TeacherCharacter`, see below.
  - **Interior reveal:** a runtime `BuildingInteriorReveal` component on "School".
    - serialized: `Renderer[] hideWhenInside` (roof, ceiling and the camera-side wall pieces: the door wall, which is the south wall), `Vector3 interiorCenter`, `Vector3 interiorSize`
    - Each frame it checks whether the player (first `PlayerController`) is inside the box, and fades or hides those renderers. Toggling `enabled` is fine; if you hide them, use `ShadowCastingMode.ShadowsOnly`.
    - This keeps the fixed-angle follow camera from looking at the roof.
  - **Anchors**, empty transforms under `School/LessonAnchors`, named exactly:
    - `TableSurface`: centre of the lesson table's top surface, with its forward axis pointing toward the lesson camera (south, the door side). Objects spawn here.
    - `LessonCamera`: a viewpoint behind and above the lesson table on the door side. It looks at the table with the blackboard in the background, framing the table about 3 m wide.
    - `BlackboardFace`: the centre of the blackboard's writing surface, with forward pointing into the room. A world-space canvas goes here.
    - `TeacherSpot`: beside the blackboard, facing the table.
    - `PlayerSeat`: where the player kid stands during the lesson, between the table and the camera, slightly to the side so the table stays visible.
    - `ClassroomZone`: just inside the door. The lesson's ActivityZone goes here.
  - Mark all geometry static and make sure everything has colliders (the existing `FinishEnvironmentObject` pattern). Make the helpers you need from PlaygroundPrototypeBuilder `internal static` rather than duplicating them.
- `public sealed class SchoolResult { public Transform root, tableSurface, lessonCamera, blackboardFace, teacherSpot, playerSeat, classroomZone; public Bounds footprint; public Vector3 doorPoint, doorFacing; public float yaw; }`
- PlaygroundPrototypeBuilder changes (agent S owns these):
  - Leave a gap of about 3 m centred at x = 0 in the **north** fence. Keep trees and benches out of the corridor between the playground and the school.
  - Call `SchoolBuilder.Build(...)` **before** the NavMesh bake, so the school is walkable and the doorway connects to the playground.
  - Extend the grass so it covers the school.
  - Keep the player spawn in the south of the playground, at about z = -(fenceHalf - 4), facing +Z (toward the school).

### Runtime: `Assets/CoolSchool/Scripts/School/` (namespace `CoolSchool.School`)
- `BuildingInteriorReveal` (above).
- `TeacherCharacter`:
  - Turns smoothly toward the player.
  - Sets the Animator bool "Talking" while `VoicePlayer.Instance.CurrentSpeaker == Speaker.Teacher && VoicePlayer.Instance.IsSpeaking`.
  - `public void Wave()` fires the "Wave" trigger.
  - Agent L adds `CurrentSpeaker` to VoicePlayer (see L). Until it exists, compile-check fails only on that member; wait and re-run as in the coordination note.

---

## L. Lesson framework and the numbers lesson (agent L)

### Runtime: `Assets/CoolSchool/Scripts/Lessons/` (namespace `CoolSchool.Lessons`)
- `public class LessonStage : MonoBehaviour`. It holds references to the anchors and the lesson UI. Serialized:
  - `Transform tableSurface`, `Transform lessonCamera`, `Transform blackboardFace`, `Transform playerSeat`
  - `CoolSchool.School.TeacherCharacter teacher` (type from agent S; use `MonoBehaviour teacher` and `SendMessage("Wave")` if you want to avoid the dependency)
  - `LessonUI ui`
- `public class LessonCameraRig`. It moves the main camera from its FollowCamera to the lesson viewpoint and back:
  - Disable FollowCamera and lerp the camera to `lessonCamera` over 0.8 s.
  - On exit, lerp back and re-enable FollowCamera.
- `public class LessonUI : MonoBehaviour`, on the TouchUI canvas.
  - `public void ShowNumberCards(int[] numbers, System.Action<int> onPicked)`: 3 big round cards, at least 300 px, along the bottom, each showing a digit in Inter Bold (`Inter-Bold.ttf` from ModularGameUIKit, used with `UnityEngine.UI.Text`). Each card has a different friendly colour.
  - `public void WiggleCard(int number)` and `public void CelebrateCard(int number)`: wiggle on wrong, grow and glow on right.
  - `public void HideCards()`.
  - `public void SetGameplayHudVisible(bool)`: hides the joystick zone, the action button and the star jar's touch area during lessons. Keep the star jar visible.
- `public class BlackboardDisplay : MonoBehaviour`: a world-space canvas at `BlackboardFace`.
  - `ShowNumber(int)`: a huge chalk-white digit.
  - `ShowDots(int)`: chalk dots in dice-like groups.
  - `Clear()`.
- `public class CountableObject : MonoBehaviour`, on spawned toys. It needs a collider.
  - `public bool Counted`
  - `public void Pop(int shownNumber)`: a bounce with a scale punch, a small sparkle, and a floating digit above the object (world-space canvas or Text) that stays.
  - `public void Highlight(bool)`
  - Tapping is detected by LessonController with a raycast from the touch or mouse position, using the Input System `Pointer.current`, only while the lesson waits for taps.
- `public enum ExerciseType { TapAndCount, HowMany, FindNumber, GiveMe }`
- `[CreateAssetMenu] public class CountableSet : ScriptableObject`. Entries: `string id` (e.g. `duck`), `GameObject prefab`, `float scale`, `string countPromptGroup`, `string howManyPromptGroup`, `string giveMePromptGroup`.
- `public class NumbersLesson : CoolSchool.Activities.Activity`. Serialized:
  - `LessonStage stage`, `LessonCameraRig cameraRig`, `BlackboardDisplay blackboard`, `CountableSet countables`
  - `int exercisesPerLesson` (4), `int starsPerCorrect` (1), `int minStars` (3)
  - `GameObject basket` (for GiveMe; spawned beside the table)
  - Flow:
    1. **OnBegin:** move the player to `playerSeat`, facing the table, with input disabled. Run the camera rig into the lesson view, hide the gameplay HUD, then say `lesson.intro`.
    2. **Exercises:** run `exercisesPerLesson` exercises chosen by the current level (below). Between exercises, clear the table with a small poof (`BattleEffects.Sparkle`).
    3. **Outro:** say `lesson.outro`, then call `RewardSystem.AddStars(max(minStars, correctFirstTry * starsPerCorrect), table position)`.
    4. **Power-up:** set `ProgressStore.Current.pendingPowerUp = "golden_sword"`, save, and say `lesson.powerup`.
    5. **End:** end after the stars have landed (about 3 s). OnEnd restores the camera, HUD and input.
  - **TapAndCount:**
    - Spawn N toys in a neat row or arc on the table and say the count prompt.
    - Each tap on an uncounted toy calls `Pop(k)` and says `number.k` (interrupt), where k is the running count.
    - After the last one: `blackboard.ShowNumber(N)`, say `lesson.total` then `number.N` (queued), and count it as correct.
  - **HowMany:**
    - Spawn N toys and say the how-many prompt. Show 3 number cards: N plus two distinct distractors in range, shuffled.
    - Right card: CelebrateCard, say `lesson.correct`, and the blackboard shows N.
    - Wrong card: WiggleCard, say `lesson.try_again`. On the 2nd miss, the hint mode starts: toys highlight one by one, each with its number word, then the cards return.
  - **FindNumber:**
    - Say `lesson.find_number`, then `number.N`.
    - Show 3 cards and the dots for N on the blackboard as a helper.
    - Right and wrong are handled like HowMany.
  - **GiveMe** (level 3+):
    - Spawn more toys than needed beside a basket. Say the give-me prompt, then `number.N`.
    - Tapping a toy moves it into the basket with an arc tween and counts it out loud.
    - When the basket holds N: say `lesson.correct`.
    - Tapping a basket toy takes it back out, so there is no wrong answer, only "not yet".
  - **Levels** (stored as `skillLevels["numbers"]`, 1–5):
    - L1: N 1–3, TapAndCount and HowMany
    - L2: N 1–5, TapAndCount, HowMany and FindNumber
    - L3: N 1–5, all four types
    - L4: N 1–7, all four
    - L5: N 1–10, all four
    - Three first-try corrects in a row move up a level. Two exercises in a row needing the hint move down. Never go below 1. Save after each change.
- `public static class PowerUps`: `public static bool TryConsume(string id)` reads and clears `ProgressStore.Current.pendingPowerUp`.
  - **Golden sword:** in `KnightsGame.OnBegin`, if `PowerUps.TryConsume("golden_sword")`, the player's `SwordFighter.HitMultiplier = 2` for that game. Each hit counts as 2 on Hittable.
  - Add `public int HitMultiplier { get; set; } = 1;` to SwordFighter and an optional `int amount = 1` parameter to `Hittable.TakeHit`, keeping existing callers working.
  - Make the sword visibly golden: `WeaponHolder` gets `SetGlow(Color? color)`, which sets the emission colour on the sword instance's material instance.
  - Reset all of this in KnightsGame.OnEnd.

### Changes to existing code owned by agent L
- `PlayerProgress`: add `List<SkillLevel> skillLevels` (serializable `{ string skill; int level; }`) with `GetLevel(string)` and `SetLevel(string,int)` helpers, plus `string pendingPowerUp`.
- `VoicePlayer`: add `public Speaker CurrentSpeaker { get; }`, the speaker of the line now playing.
- SwordFighter, Hittable, WeaponHolder and KnightsGame: the power-up changes above only.

### Editor (agent L): `Assets/CoolSchool/Editor/Lessons/`
- `CountableSetSeeder` creates `Assets/CoolSchool/Lessons/Countables.asset` with these entries, prefabs by name:

  | id | prefab | Dutch | English |
  |---|---|---|---|
  | duck | `SM_Prop_Toy_Duck_01` | eendjes | ducks |
  | ball | `SM_Prop_Ball_01` | ballen | balls |
  | bear | `SM_Prop_Plush_Bear_01` | beertjes | teddy bears |
  | block | `SM_Prop_Toy_Block_01` | blokken | blocks |
  | dino | `SM_Prop_Plush_Dino_01` | dino's | dinos |

  Scale each so its largest size is about 0.35 m. Prompt groups are named `lesson.count.<id>`, `lesson.howmany.<id>` and `lesson.giveme.<id>`.
- `LessonSetupBuilder.Setup()` finds the anchors by name (`School/LessonAnchors/...`) and creates:
  - the NumbersLesson activity with all components wired;
  - a basket from Kids `SM_Prop_Bucket_01` or a Town basket, beside the table;
  - an ActivityZone at `ClassroomZone`, built with the existing zone helper. That gives a beacon and ring; the icon is a "Star" or a book/abacus sprite found by name;
  - the ActivityInviter gets the classroom zone added, with invite group `invite.lesson`, if the inviter exists.
  - Call it from PlaygroundPrototypeBuilder after `VoiceSetupBuilder.Setup()`. Add exactly one line, re-reading the file right before editing.
- Voice lines: add to `VoiceLineSeeder` (Teacher speaker unless noted):
  - `number.1` … `number.10`: één, twee, drie, vier, vijf, zes, zeven, acht, negen, tien / one … ten
  - `lesson.intro.1`: "Welkom in de klas! Vandaag gaan we tellen." / "Welcome to class! Today we're going to count."
  - `lesson.total.1`: "Het zijn er…" / "There are…"
  - `lesson.correct.1`: "Goed zo!" / "Well done!"
  - `lesson.correct.2`: "Heel knap!" / "Very clever!"
  - `lesson.correct.3`: "Ja, dat klopt!" / "Yes, that's right!"
  - `lesson.try_again.1`: "Bijna! Probeer het nog eens." / "Almost! Try again."
  - `lesson.try_again.2`: "Tel maar mee!" / "Let's count together!"
  - `lesson.find_number.1`: "Waar is het getal…" / "Where is the number…"
  - `lesson.outro.1`: "Wat heb jij goed geteld! Hier zijn je sterren." / "You counted so well! Here are your stars."
  - `lesson.powerup.1`: "En je krijgt een gouden zwaard voor bij het kasteel!" / "And you get a golden sword for the castle!"
  - `invite.lesson.1`: "De bel gaat! Kom maar naar de klas!" / "There's the bell! Come to class!"
  - For each countable `<id>` with Dutch plural P (English p):
    - `lesson.count.<id>.1`: "Tel de P! Tik op elk… één voor één." / "Count the p! Tap each one."
    - `lesson.howmany.<id>.1`: "Hoeveel P zie je?" / "How many p can you see?"
    - `lesson.giveme.<id>.1`: "Doe deze P in de mand:" / "Put this many p in the basket:" (followed by the number line)
  - Write the per-countable Dutch carefully. For example, for ducks: "Tel de eendjes! Tik op elk eendje." Use the singular where natural: eendje, bal, beertje, blok, dino.

### Coordination note
- Agent S owns `Editor/School`, `Scripts/School`, and the fence, grass, spawn and school-call changes in PlaygroundPrototypeBuilder.
- Agent L owns `Editor/Lessons`, `Scripts/Lessons`, and the listed changes to Rewards, Voice and Battle.
- Both edit PlaygroundPrototypeBuilder. Re-read it immediately before every edit and keep your edits small.
- If compile-check fails **only** because the other agent's members don't exist yet, wait about a minute and re-run, for up to about 10 minutes. Never create the other agent's types yourself.
