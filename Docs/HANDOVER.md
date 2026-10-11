# CoolSchool: agent handover

Last updated: 2026-10-11. Read this first, then the specs in `Docs/specs/`.

## What this is
CoolSchool is a Unity game the user (Timo) is making for his **4-year-old son**. It's a school in one explorable world where he learns numbers, letters, first words and simple sums through short lessons, and earns rewards for play sections (pretend battles, role-play, tag, and later racing).

**Design rules**
- **Dutch first, English later.** All spoken content lives in a line table with an NL and an EN column.
- **He can't read.** Everything is spoken; visuals are big icons and digits, never words.
- **He never fails.** Wrong answers get a hint and another try. Opponents "tumble and giggle" or turn into friends, and the player is never knocked out.
- **Controls:** a floating joystick on the left and one big context button on the right (tap or hold).
- **Platform:** Android tablet in landscape. The Android module is **not installed yet**, and it has never been tested on a device.

**Living design document** (Claude Doc): https://claude.ai/code/artifact/47efe6ed-b978-48bf-b425-2df32924a6a5. It covers the vision, controls, world, learning, play sections, rewards, voice, the parent area and the roadmap.

## How the user wants to work
- **All implementation goes to Sonnet subagents** (Agent tool with `model: "sonnet"`). The main agent designs, writes specs and contracts, reviews, runs builds, and coordinates.
- For parallel work: write a contract in `Docs/specs/` with exact type, member and serialized-field names. Give each agent its own files, and tell them to re-read shared files right before each edit.
- The user likes **multiple-choice questions** (AskUserQuestion) for decisions, and wants minimal manual effort. Automate anything you can, as was done for voice generation.
- He often has the Unity editor **open**. Batch-mode commands only work when it's **closed**, so ask him to close it.
- Commit only when he asks. Never push. The GitHub remote is `Tudoh8/CoolSchool`, and the repo contains paid assets.

## Repo layout
```
CoolSchool/                      Unity 6000.3.24f1 project (URP 17.3, new Input System only, AI Navigation 2.0)
  Assets/CoolSchool/             OUR code and generated assets
    Scripts/  Activities, Aliens, Audio, Battle, Camera, Characters, Input, Lessons, Player, Rewards, School, Tag, Voice, World
    Editor/   builders and tools (PlaygroundPrototypeBuilder, KnightsArenaBuilder, School/, Lessons/, Aliens/, Voice/, Rewards/, Audio/, CommandLineBuild)
    Scenes/Playground.unity      the generated world (don't hand-edit; rebuild it)
    Animation/ Materials/ Prefabs/Effects/ Voice/ (Lines.asset, Clips/nl/*.mp3) Rewards/ Lessons/ Audio/
  Assets/Imported/               third-party: Synty (PolygonKids/Town/City/Generic), Kevin Iglesias Human Animations,
                                 Gestures MoCap + Conversation Gestures, ModularGameUIKit (UI sprites, Inter fonts), DeepVoice
  Assets/ExplosiveLLC/           RPG Character Mecanim Animation Pack (sword/shield, pistol, mace, unarmed)
  Assets/Magic Pig Games (Infinity PBR)/  low-poly dragon (demo script DragonLP_Demo.cs was deleted on purpose)
  UserSettings/DeepVoiceInvoice.txt  user's DeepVoice invoice (git-ignored; never print it or commit it)
Docs/specs/                      contracts for each feature (read them before changing a system)
Docs/voice/                      exported line lists
Tools/compile-check.ps1          compiles Assets/CoolSchool with Unity's Roslyn while the editor is OPEN
Tools/unity-batch.ps1            runs Unity -batchmode -executeMethod (editor must be CLOSED), logs to Logs/
```

## Key tools and commands (run from the repo root)
- **Compile check.** Works with the editor open; every agent must get `compile-check: OK`.
  ```
  powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
  ```
- **Rebuild the whole scene** (editor closed). This is the same as the menu item **CoolSchool → Build Playground Prototype**.
  ```
  powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.CommandLineBuild.BuildPlaygroundFromCommandLine
  ```
- **Generate missing voice lines** through DeepVoice (editor closed). It only generates lines without a clip, and links them automatically.
  ```
  powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.VoiceBatchGenerator.GenerateMissingFromCommandLine
  ```
  - Add `-ExtraArgs '-voiceLanguage','en'` for English.
  - DeepVoice terms: only use it from inside Unity and don't overload the server. The tool runs requests one at a time with a 1.6 s pause, and temporarily allows http.
  - Voices per speaker are set in `Assets/CoolSchool/Voice/VoiceGenerationSettings.asset`: Teacher/Narrator Lily, Kid/Alien Freya, Goblin Clyde.
  - At runtime the pitch is raised per speaker (Kid 1.22, Goblin 1.35, Alien 1.5).
  - To redo a line, delete its mp3 and re-run.
  - Quota used so far: about 2,554 characters.
- **Batch logs:** `Logs/batch-<Method>.log`.

## Architecture in brief
- **Generated world.** Everything is created by editor builders. `PlaygroundPrototypeBuilder.RunBuild()` orchestrates it:
  1. Kids-material URP upgrade.
  2. Animator controllers: Kid, KidClub override, Teacher, Dragon.
  3. Environment: playground ring, grass and fence, the school (`SchoolBuilder`), the castle (`KnightsArenaBuilder.BuildCastle`), the alien field (`AlienFieldBuilder.BuildField`).
  4. NavMesh bake.
  5. UI, player, classmates, camera.
  6. Tag, knights arena, then `VoiceSetupBuilder.Setup()`, `SoundSetupBuilder.Setup()`, `LessonSetupBuilder.Setup()`, the alien characters, and `RewardsSetupBuilder.Setup(player)`.
  7. Save the scene, plus build settings (scene first, landscape-only).

  Assets are always found **by name**, with a pack hint, and placed **by renderer bounds**, because Synty pivots are unknown. **After changing any builder, re-run the build.**
- **World layout.** The camera is fixed, looking **north (+Z)**, so joystick-up means forward. Anything south of the player is never visible.
  - The **school is north**: its door faces south, and its blackboard is on the north wall.
  - The **knights castle field is east** (+X).
  - The **alien field is west** (-X).
  - The **player spawns at the south** of the playground, at about (0, 0, -16.9).
  - The tag zone is at (0, 0, 4).
- **Activities** (`Scripts/Activities`): `ActivityManager` runs one `Activity` at a time; the default is `FreePlay`.
  - Walking into an `ActivityZone` starts its activity. Each zone has a ring, a light-beam beacon and a billboard icon.
  - `ActivityInviter` speaks invitations with a sparkle trail when he is idle for a while (order: classroom, tag, castle, aliens).
  - `Activity` has `onBegan`/`onEnded` UnityEvents.
- **Activities built so far:**
  - `TagGame`: 40 s rounds.
  - `KnightsGame`: goblin waves, then a confetti dragon. Tap swings the sword (auto-aim); hold raises the shield.
  - `NumbersLesson`: in the classroom; four exercise types and adaptive levels 1–5, stored in progress.
  - `AlienLanding`: ray gun zaps aliens into dancing friends; slime; trampoline rescue of the baby alien; a counting moment; then a party.
- **Battle system** (`Scripts/Battle`): `Hittable` (team, hits, stun, block), `SwordFighter`, `PlayerBattleInput`, `WeaponHolder` (bone attachments), `BattleOpponentAI`, `KnightAllyAI`, `DragonBoss`, `BattleEffects` (particle prefabs), `ImaginationEffect` (subtle "pretend" sparkle).
- **Characters.** Synty kids use a humanoid rig.
  - `KidAnimator` drives `Kid.controller` with Speed, Jump, Cheer, Attack(+Variant), Hit, Tumble, Laugh, Blocking, BlockHit, Shoot, Throw, Dance/Dancing/DanceVariant, and an "UpperBody" layer.
  - It ignores missing parameters safely.
  - `CostumeHolder` attaches the cape and mask.
- **Voice** (`Scripts/Voice`): a `LineTable` asset of keys and groups with NL/EN text and clips.
  - `VoicePlayer.Say(group)` handles the queue; the teacher has priority; a missing clip falls back to a debug subtitle.
  - The seed list lives in `Editor/Voice/VoiceLineSeeder.cs`. Add new lines there, rebuild or seed, then generate.
- **Audio** (`Scripts/Audio`): `Sfx.Play(cue, pos)` and `Sfx.PlayMusic(cue)`, with about 55 named cues listed in `Editor/Audio/SoundCues.cs`.
  - Every action already calls its cue, but **no audio clips exist yet**.
  - When the user imports audio packs, use **CoolSchool → Audio → Auto-Assign Clips From Folder…**, which matches by keyword through the synonyms in `SoundAutoAssign.cs`.
  - Music ducks to 40% while a voice line plays.
- **Rewards** (`Scripts/Rewards`): `ProgressStore` saves to `persistentDataPath/progress.json`.
  - Stars fill the jar; 10 stars give a present, which opens in a popup with a 3D reveal; items come from the `RewardCatalog` in order.
  - The equipped sword is applied automatically.
  - The `pendingPowerUp` "golden_sword" is earned in a lesson (a gold sword pops up above the table and flies to a badge left of the star jar: `GoldenSwordReward`, `GoldenSwordBadge`) and consumed in the next knights game (solid-gold sword, double hits, sparkle fanfare, `knights.golden_sword` line).
  - `AddStars(count, pos, announce)`: the "Een ster!" line only plays when the voice queue is idle; the lesson passes `announce: false`.
  - Debug menu items: **Give 10 Stars (test)** and **Reset Progress**.

## Current state
- **Committed** as `1fb2a23e`, locally: everything up to and including the classroom, the lesson, the north re-layout and 73 Dutch voice clips.
- **Second commit**, on 2026-10-11 (the commit after `1fb2a23e`): the sound-cue system, the Alien Landing, 11 alien voice clips (84 in total), the rebuilt scene, `Docs/specs/aliens-and-sound-contract.md` and this file. The user has **not played the aliens section yet**, so ask him how it plays.
- **Played and approved by the user in the editor:** tag (rounds shortened to 40 s), knights ("good enough for now"), the school and lesson after the north re-layout ("looks good"), and the voices.
- **Known rough edges and tuning candidates:**
  - **Alien Landing (untested):**
    - ray-gun hand offset `RayGunShooter.GunLocalPosition/Euler`
    - cape and mask offsets (`CostumeHolder`)
    - baby perch visibility in the treehouse tree (`TreePerch`)
    - `JumpPad.launchSpeed`
    - camera framing of the party line
    - the carried baby plays a run animation
    - counting close-up (`CountCameraHeight/Back/Pitch` in `AlienFieldBuilder`); the baby waits at `BabyWaitPoint` behind that camera and is not counted
  - **Golden sword reward (untested):** size, height and orientation above the lesson table (`GoldenSwordHeight` in `NumbersLesson`, `displayLength` on `GoldenSwordReward`)
  - **Knights:** sword and shield hand offsets are constants at the top of `PlaygroundPrototypeBuilder`; trees may stand close to the castle wall ends.
  - **Teacher:** stands angled beside the board, but turns toward the player at runtime.
  - **Lesson UI:** check that the number cards don't cover the toys.
  - **Single number words** may have English-sounding TTS. Re-generate them if the user complains.

## Asset notes and gotchas
- **Kids pack:** its materials were Built-in Standard. `KidsMaterialUpgrader` converts them to URP/Lit, and it's idempotent. Town, City and Generic already use Synty URP shader graphs.
- **Gestures MoCap pack:** contains rude taunts. **Only** use `Chicken_Dance_Taunt`, `Dance_Taunt`, `Robo_Taunt` and `Victory_Taunt`. `AlienFieldBuilder` enforces a whitelist.
- **Building scenes in batch mode:** `EditorUtility.DisplayDialog` and `SaveCurrentModifiedScenesIfUserWantsTo` are skipped when `Application.isBatchMode`.
- **Placing prefabs:** use `PrefabUtility.InstantiatePrefab`. To strip vendor scripts from a prefab instance, unpack it first (as is done for the dragon).
- **Coding constraints:**
  - Prefer UI text with `UnityEngine.UI.Text` and the `Inter-Bold.ttf` font. TextMeshPro was avoided.
  - Use the new Input System only. Inside `CoolSchool.*` namespaces never write a bare `Input.`, because `CoolSchool.Input` is a namespace.
  - `compile-check.ps1` compiles only `Assets/CoolSchool`. Third-party types (e.g. DeepVoice) are reached through reflection.

## Roadmap and next options
The user chose and ordered the play-section focus as: pretend battles plus create & role-play first; racing and other things later.
- **Battles:** Knights done; Aliens & superheroes built but untested; **Pirates** next (playground ship, sword duels, cannon at balloons, treasure map).
- **Create & role-play (chosen):** **Shopkeeper** (ice cream, bakery or pizza: friends order with picture bubbles, coins), **Builder** (build the cardboard castle, then defend it), **Band & dress-up** (instruments, costume parade, photos).
- **Learning:** letters (starting with his name, which is not known yet), colours and shapes, then first words and sums. Reuse the lesson framework in `Scripts/Lessons`.
- **Character creator and parent area:** he builds his own kid and the teacher says his name. A locked parent area holds language, an optional time limit, progress, and volume.
- **Tablet:** install Android Build Support through Unity Hub, prepare a batch APK build, and test touch and performance on his tablet.
- **Audio:** the user will import "a lot of audio packs" later. Then run auto-assign and fill in the remaining cues.
- **Later:** the neighbourhood (safe street, houses, his home, shops), soapbox racing, the English language track.

## Memory
The persistent memory in `C:\Users\timo-\.claude\projects\C--Users-timo--Documents-GIT-CoolSchool\memory\` holds the vision and decisions, the delegate-to-Sonnet rule and the compile-check note. Keep it updated.
