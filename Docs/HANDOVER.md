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
  - Quota used so far: about 4,650 characters, plus 926 for the pirate lines, 1,456 for the back-button/race/alien-variety lines and 5,846 for the harder-numbers + toy-room lines (all 2026-10-12), so about 12,880 in total.
  - 2026-10-13: one run generated 202 lines (castle 83, zoo 76, toys 23, item 18, other 2), 5,685 characters; a later run by another session hit DeepVoice's HTTP 429 rate limit.
  - **All 503 Dutch lines have a clip (2026-10-12).** The last batch (145 harder-numbers lines + 43 toy-room/equipment/cart-picker lines) was 188 generated, 0 failed. English is not generated yet.
- **Batch logs:** `Logs/batch-<Method>.log`.
- **Preview pictures + grounding check** (editor closed): `-Method CoolSchool.EditorTools.PreviewCapture.BuildAndCaptureFromCommandLine` builds and saves the scene, runs `SceneGroundCheck` (`SceneGround:` log lines, also menu **CoolSchool → Check Scene Grounding**) and renders named shots (weapons on characters, ground contact, ship helm, skeleton boat, cove) to `Logs/preview/*.png`. `CaptureFromCommandLine` renders the saved scene without rebuilding; filter with `-ExtraArgs '-previewShots','ship,ground'`. Add shots in `BuildGroups()`. Look at the PNGs to verify visual changes before asking the user.
- **Weapons** are placed by `Scripts/Battle/WeaponFit.cs` from the hand/forearm bone positions and the weapon's mesh bounds (the old hand-guessed offsets were wrong). **Ground tiles** are aligned by their measured flat surface (`Editor/GroundSurface.cs`), not their bounds top; lumpy tiles get a flat collider.
- **Shop preview pictures** (editor closed): builds and saves the scene, then renders the shop camera (kid and dragon, 16:9 and 4:3) and the follow-camera view of the stall to `Logs/shop-preview/*.png`, and logs where key points land on screen (`ShopPreview:` lines). The staging is not saved.
  ```
  powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.ShopPreviewCapture.BuildAndCaptureFromCommandLine
  ```

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
  - **Back button** (contract `Docs/specs/quit-button-contract.md`): `QuitButton` (top-left, right of the parent gear) shows during any non-default activity; a tap (or Android back) freezes the game via `QuitPrompt` (teacher asks `quit.ask`, red cross = continue, green check = `Current.End()` + `quit.bye`). It is generic, so every activity's `OnEnd` must survive being called at any moment. Gameplay pointer polling checks `ParentGate.InputBlocked` (parent panel or quit prompt open).
- **Activities built so far:**
  - `TagGame`: 40 s rounds.
  - `KnightsGame`: goblin waves, then a confetti dragon. Tap swings the sword (auto-aim); hold raises the shield.
    **Castle adventure (2026-10-13, contract `Docs/specs/castle-adventure-contract.md`; compile-clean, not yet rebuilt or played):** the game is now a story visit. The zone sits outside the gate (gate − 8.5 m) and the portcullis is held down (`CastlePortcullis.HoldClosed`). A queen (`Scripts/Castle/CastleQueen`) gives one of 4 missions (cake / crown / egg / knight on the tower). The kid answers a letter-sound password with 3D word props, patches a hole in the back wall with N toy blocks, then fights 2 waves of goblin kinds (`GoblinKind`: shield, troll that shrinks, tiny goblin in a barrel, a thief that steals the mission loot) with "help a friend" calls. One number moment per visit: count the goblins on the wall perches with number cards, or feed the hungry dragon N cookies with a check button. The dragon is friendly (`DragonBoss.Friendly.cs`). In the egg mission it hatches a baby dragon, which the kid names by a letter (`DragonNames`) and which unlocks the earned-only `plush.dragon` buddy. Then comes a fixed-path dragon ride (`DragonRidePath`, tap = confetti fire) and a celebration with his name on a flag (`NameFlag`) and a growing castle (`CastleGrowth`, `castleWins`). Adaptive `castle` level 1–3 with a parent row. Moments live in `Scripts/Castle/`, the flow in `KnightsGame*.cs` partials, the builder in `Editor/Knights/CastleAdventureBuilder*.cs`. About 90 `castle.*` voice lines are seeded in `VoiceLineSeeder.Castle.cs`. First things to check in play: the peeking goblin behind the hole, the guide ring size, the ride path corners and the seat height on the saddle, the queen and stands framing at the gate.
    Since 2026-10-12 (contract `Docs/specs/big-castle-contract.md`) it plays inside a 5× walk-in cardboard castle (gate facing the camera, zone at the back of the courtyard (`CastleZoneBackOffset`) so the kid walks across it, the dragon flies in over the back wall; after it flies away the player and the three knights run to a `CelebrateSpot` just inside the gate and cheer facing the camera. The zone move and the celebration are written but not yet rebuilt or played). `CastleCutaway` (`Scripts/World`) ghosts the front wall while the player is inside. The inviter's sparkle trail now follows the NavMesh path.
    Later on 2026-10-12, user hand tweaks were baked into the builders: the gate wall `Piece_05` is 6× and unturned (pivot kept), `Piece_06` is a collider-less portcullis in the arch (scale 8/6.5/6) that rises when the player is within 7 m and lowers past 11.5 m (`CastlePortcullis`; stays up in the courtyard and during the game; cues `castle.gate_rise`/`castle.gate_drop`). The goblin door is gone: goblins climb 3 Synty ladders (imagination props) up the outside of the north wall and jump down (`WallLadder`, `LadderClimb`); tumbled goblins run to a ladder landing and sparkle away. The blackboard uses `PolygonKids_Material_04_A` (`SchoolBuilder.ApplyBlackboardMaterial`). **Not yet rebuilt or played.** `Docs/specs/big-castle-contract.md` still describes the old goblin door.
  - `NumbersLesson`: in the classroom; adaptive levels 1–9 stored in progress (`numbers` skill).
    - **Harder numbers** (2026-10-12, contract `Docs/specs/harder-numbers-contract.md`): `NumberCurriculum` (`Scripts/Lessons`) is the single source for the max number per level (3, 5, 5, 7, 10, 10, 15, 20, 20) and for the level that unlocks each `NumberSkill`.
    - **Classroom:** 12 exercise types. The new ones are QuickLook, Compare, OneMoreOneLess, SameAmount, NumberLine (after/before/gap), CountBack, CountOn and Hidden (cup trick). Supporting props: two `TenFrameTray`s (any count above 10 fills them), a `HidingCup`, and `FloatingDigit`. The blackboard got a number line and ten-frame dots.
    - **Play sections** read the numbers level and only use an idea once it is unlocked:
      - race: after-gates and the podium "who is 2nd?" (`SoapboxRace.Podium.cs`)
      - aliens: the baby joins or one leaves after the count, then a rocket countdown (`AlienLanding.Numbers.cs`)
      - shop: a tip coin (`IceCreamShop.Tip.cs`)
      - pirates: two piles, the bucket trick, count on (`PirateAdventure.Numbers.cs`; `debugForceExtra` forces one)
    - **Voice:** the 145 voice lines are in `VoiceLineSeeder.Numbers.cs`.
    - **Status:** rebuilt and voiced (2026-10-12), not played yet.
  - `AlienLanding`: ray gun zaps aliens into dancing friends; slime; trampoline rescue of the baby alien; a counting moment; then a party.
    Since 2026-10-12 (contract `Docs/specs/alien-variety-contract.md`) every visit differs:
    - **Count:** 2–8 aliens, adaptive `aliens` level 1–3 with a parent row, never the same count twice in a row.
    - **Spawns:** 2–3 spawn spots out of 8 spread over the field.
    - **Kinds:** Normal, Big (3 zaps, shrinks), Tiny (runs off, rests), Shy (hides behind `AlienCover`s, peeks) and Robot.
    - **Rescues:** one of four kinds (`AlienRescueSite`): one of 4 trees, the rocket nose, a `FloatingBubble`, or a `SlimeTrap` with zappable `SlimeBlob`s (`IZapTarget`). There are two baby models.
    - **Trampolines:** real Synty trampolines, always bouncy also in free play; only the rescue one glows. The lit pad aims its launch at the baby's seat (`JumpPad.AimAt`).
    - **Count camera:** framed at run time on the N dancers.
    - **Testing:** force a rescue or a count with `debugForceRescue` / `debugForceCount` on `AlienLanding`.
    - **Status:** not rebuilt or played. The 14 new voice lines are generated (2026-10-12).
  - `LettersLesson` (contract `Docs/specs/letters-and-parent-area-contract.md`): shares `ClassroomLesson` with `NumbersLesson`; the classroom zone alternates subjects via `ClassroomActivitySelector`. Dutch letter SOUNDS, lowercase, drawn from stroke data; the child's name letters first; exercises NewLetter, FindLetter, StartsWith (76 word props), Trace, SpellName. Sound spellings live in `Editor/Lessons/LetterWordTable.cs`; to fix one, edit it, delete its mp3 and regenerate.
  - **Parent area** (`Scripts/Parent`): hold the gear top-left 3 s; pauses the game (`ParentGate.IsOpen` must gate any direct pointer/keyboard polling); child name + level sliders per subject that set the current level (adaptive continues).
  - `IceCreamShop` (`Scripts/Shop`, contract `Docs/specs/ice-cream-shop-contract.md`): stall in the NW of the playground facing south; five customers (kid, knight, kid, alien, small dragon) order with picture bubbles; he drags or taps scoops from four colour tubs onto the cone (counting + colours), serves it, then taps the coins into the register; adaptive `shopLevel` 1–3.
  - `PirateAdventure` (`Scripts/Pirates`, builder `Editor/Pirates/PirateCoveBuilder*.cs`, contract `Docs/specs/pirate-cove-contract.md`): a sand cove north of the arena (x 20.9..MaxX, z 20.9..60.9), entered through a gap in the north fence at x ≈ 21–26. Imagination turns the sand into sea. He steers the playground ship at the wheel, pops balloons on a skeleton boat with the cannon (the parrot Lorre counts), duels skeletons on the island beach (they fall apart into giggling bones and join the crew), follows a picture map, digs up the chest, opens a letter lock and counts the coins (coins = stars). Adaptive `pirates` skill level 1–3 with a parent-area row. The trophy chest at the dock fills with `pirateCoinsTotal`; unlocking `plush.parrot` puts Lorre on his shoulder in free play.
  - `SoapboxRace` (`Scripts/Race`, builder `Editor/Race/SoapboxStreetBuilder*.cs` + `SoapboxTrackLayout.cs`, contract `Docs/specs/soapbox-race-contract.md`, 2026-10-12): a Temple-Run-style 3-lane runner on a new neighbourhood street north of the playground (x -22.6..-9.4, z 20.9..525, entered through a gap in the north fence at x ≈ -16). He races two classmate rivals (rubber-banded, he usually wins) in Synty soapbox carts: tap the left/right half = lane, action button = jump. Boxes/cones/puddles/sprinklers, animals crossing (polyperfect pack in `Assets/polyperfect/`, URP material copies in `Materials/Race/Animals/`), ramps, coins, number + letter-sound gates; podium at the finish; stars = 2 + right gates. New speaker `Announcer` (DeepVoice "Dave"); adaptive `race` level 1–3 with a parent row; cart looks (`cart.*` items) as presents.
  - `HideAndSeekGame` (`Scripts/Zoo`, builder `Editor/Zoo/ZooParkBuilder*.cs`, contract `Docs/specs/hide-and-seek-contract.md`, 2026-10-13): "De dieren zijn ontsnapt!" in a new zoo park west of the alien field (x −92.9..−44.9, z −14..30, entered through a 3 m gap in the main west fence at z ≈ −3; zone by the base tree just inside). A classmate zookeeper (Explorer kid + scout hat, speaker Kid). He counts with eyes closed (`EyesClosedOverlay`, `number.<k>`), then searches 3–5 of 10 polyperfect animals hidden in 12 spots (always partly visible: covers north of the prop, containers with a show fraction); hints escalate (peek/call → first-sound clue + position word → shake → sparkle trail → auto-find). Found animals join a conga line; checklist HUD with silhouettes (`ZooChecklistUI`); "how many left/found" number cards. Then his turn: he walks to a glowing spot and is found by the keeper (`PlayerController.RideOn` while hidden). Recap at the 10 pens ("Waar zat de olifant? Achter de heg!"); the zoo fills up over visits (`PlayerProgress.zooAnimals`, `ZooParkDisplay`). Adaptive `hideseek` level 1–3 + parent row "Verstoppertje"; stars = found + 1. 77 `zoo.*` voice lines generated. Preview group `zoo` (`Editor/Zoo/PreviewCapture.Zoo.cs`, incl. `zoo_hide_*`). Icons in `Assets/CoolSchool/Zoo/Icons` (bump `IconVersion` in `ZooIconRenderer` to redraw). Built and preview-checked, **not played yet**; the last polish (sign icons, pond colour, small hiders, trees outside the fence) still needs a rebuild + look. Calls for giraffe/hippo/zebra/crocodile/flamingo have no clip (giggle fallback).
- **Playground toys** (2026-10-13, contract `Docs/specs/playground-toys-contract.md`, `Scripts/Playground`, `Editor/Playground`):
  - In free play, touching one of the 12 ring toys plays a 5–10 s routine (`ToyPlayer` on the player, `PlaygroundToy` + a `ToyRoutine` subclass per toy). Pushing the joystick after letting go of it skips.
  - The first play on each toy earns a sticker (`ToyStickers`, `PlayerProgress.toyStickers`), worth 1 star; all 12 give 3 bonus stars. The `StickerBoard` stands about 4.5 m north of the spawn.
  - Kid poses: the `ToyPose` int on `Kid.controller` (SitSeat, SitSlide, Climb, Hang, HangMove, Crawl). The hip and hand offsets are measured at build time (`ToyPoseMeasure`; seat 0.30, slide 0.08, hang 1.76).
  - The Synty Kids playground pieces are a modular kit:
    - The slide chute now gets a tower (stairs, landing, platform at 2 m, railings, roof; `PlaygroundToysBuilder.SlideTower.cs`).
    - The flat monkey bars and track ride are lifted onto yellow poles.
    - The swings, seesaw plank and track handle are separate mesh objects that the routines move.
  - Every anchor is found from geometry at build time and logged (`PlaygroundToys:` lines). Preview shots: `-previewShots toy` (`toy_<id>_*`, plus `toy_<id>_markers_*`).
  - Menu: **CoolSchool → Playground Toys** (Give All / Clear Stickers, Clear Board Intro); each toy has a "Play now" context menu in play mode.
  - Status: rebuilt, voiced (23 `toys.*` lines) and checked in preview shots; **not played yet**. Not yet tuned: the timings, the skip feel, the sticker moment, and whether the NPC kids walk through the slide tower (the NavMesh is baked before the toys).
- **Battle system** (`Scripts/Battle`): `Hittable` (team, hits, stun, block), `SwordFighter`, `PlayerBattleInput`, `WeaponHolder` (bone attachments), `BattleOpponentAI`, `KnightAllyAI`, `DragonBoss`, `BattleEffects` (particle prefabs), `ImaginationEffect` (subtle "pretend" sparkle).
- **Characters.** Synty kids use a humanoid rig.
  - `KidAnimator` drives `Kid.controller` with Speed, Jump, Cheer, Attack(+Variant), Hit, Tumble, Laugh, Blocking, BlockHit, Shoot, Throw, Dance/Dancing/DanceVariant, and an "UpperBody" layer.
  - It ignores missing parameters safely.
  - `CostumeHolder` attaches the cape and mask.
- **Voice** (`Scripts/Voice`): a `LineTable` asset of keys and groups with NL/EN text and clips.
  - `VoicePlayer.Say(group)` handles the queue; the teacher has priority; a missing clip falls back to a debug subtitle.
  - The seed list lives in `Editor/Voice/VoiceLineSeeder.cs`. Add new lines there, rebuild or seed, then generate.
- **Audio** (`Scripts/Audio`): `Sfx.Play(cue, pos)` and `Sfx.PlayMusic(cue)`, with the named cues listed in `Editor/Audio/SoundCues.cs`. Every action already calls its cue.
  - The source of truth is the picks file `Assets/CoolSchool/Audio/SoundPackPicks.txt` (cue, volume, pitch, clips from the Asset Store packs). The contract is `Docs/specs/audio-packs-contract.md`.
  - `python -I Tools/import-audio-clips.py` copies only the picked clips, with their package GUIDs, into `Assets/Imported/Audio/<Pack>/`.
  - **CoolSchool → Audio → Apply Sound Pack Picks** (or batch `-Method CoolSchool.EditorTools.SoundPackPicks.ApplyFromCommandLine`) fills `SoundLibrary.asset`. A scene rebuild runs it too, and the picks file wins over hand edits for the cues it lists.
  - Import settings (Android tablet) are applied by `Apply` and by `ImportedAudioPostprocessor`: clips under `Ambient Loops` stream as stereo Vorbis 0.5; all others are Decompress On Load, mono, Vorbis 0.6; sample rate 44100.
  - **Ambience layer:** `PlayMusic("music.X")` also starts `ambience.X`, crossfading two looping sources; the classroom has none, so it is quiet. Ambience follows `SfxVolume` and ducks to 60% while a voice line plays; `StopMusic` does not stop it.
  - Music ducks to 40% while a voice line plays.
  - **No music exists yet:** none of the installed packs (Phantom Magic, Action RPG SFX, Environment Sounds, Bestiary, Monster Sounds, plus stray clips from Feel and the Ultimate Clean GUI Pack) has music.
  - **Status:** the picks were made from file names and are not auditioned, and have not been applied in Unity yet. Audition first: `battle.giggle` and `aliens.giggle` (monster laughs pitched up), `dragon.roar`, `shop.scoop`, `ui.card_wrong`.
- **Rewards** (`Scripts/Rewards`): `ProgressStore` saves to `persistentDataPath/progress.json`.
  - Stars fill the jar; 15 stars give a present (`jarCapacity`, was 10), which opens in a popup with a 3D reveal; items come from the `RewardCatalog` in order.
  - **Equipment and Toy Room** (2026-10-12, contract `Docs/specs/toy-room-and-equipment-contract.md`, rebuilt, voiced and checked in preview shots `Logs/preview/toyroom_*`, `wardrobe_*`, `race_cart_picker_camera`; **not played yet**):
    - Four slots on the kid (`EquipSlot`: Sword, Shield, Hat, Buddy), applied by `EquipmentApplier` (replaces `EquippedSwordApplier`) through `WeaponHolder.SetSword/ShieldPrefab`, `HatHolder` and `BuddyFollower` (a plush that hops after him and rides in the race cart; the parrot stays `ShoulderParrot`'s). Per-activity hat/buddy rules live in the applier.
    - The catalog has 4 starters (foam sword, shield, cart 1, the bell horn), 61 presents (17 + 18 added 2026-10-13, then 18 cart garage presents and the 8 new cart bodies) and 11 gold variants (`goldOf`, painted by `GoldPaint`), then medals; the gold variants are mixed in with the garage presents (order in `RewardCatalogSeeder`; the 8 new bodies are mixed in too). Icons are rendered by `RewardIconRenderer` to `Rewards/Icons/` (locked = icon tinted black).
    - A new sword/shield/hat/buddy asks the swap question in `PresentPopup` (green check / red cross); nothing is equipped automatically any more.
    - The Toy Room (`Scripts/ToyRoom`, `Editor/ToyRoom`) is a small building in the NE playground corner: shelves with every item (silhouettes when locked), a medal board, a hat board on the east wall (the 7 newest hats on pegs) and the dress-up mirror (`ToyRoomWardrobe` activity). `SchoolBuilder`'s modular helpers moved to `Editor/School/ModularBuilding.cs`.
    - The soapbox race opens `CartPicker` before boarding. Since 2026-10-13 it is the **cart garage** (contract `Docs/specs/cart-garage-contract.md`, part G1; rebuilt and checked in preview shots `Logs/preview/race_cart_*`, `race_pit_lane_*` on 2026-10-13, **not played yet**): six tabs (body, paint, add-ons, trail, horn, number) with cards made from the catalog (`RewardItem.cartPart`: Body/Paint/Addon/Trail/Horn, set by the seeder; locked ones are black silhouettes). Items: 5 paints (red, blue, green, pink, purple; gold is a paint too), 4 new add-ons (googly eyes, balloons, propeller, spoiler; plus the flag and stickers), 4 trails (rainbow, stars, bubbles, glitter), 6 horns (bell starter, cow, dog, cat, elephant, magic horn; clips are the polyperfect animal sounds and Phantom Magic ones, named in `SoundPackPicks.txt` as `race.horn_*`), and a free number plate 1-10 (shows 4 until chosen). Paint = recoloured copies of the body materials (`CartPaintMaker`, saved in `Assets/CoolSchool/Race/Paint/`: saturated texels move to the paint colour, greys and darks stay); add-ons sit on per-body anchor points found by ray probes (`SoapboxStreetBuilder.FitBody`); the number plates are world-space UI Text. A round honk button on the race HUD plays the horn (`SoapboxRace.Honk.cs`, `Sfx.PlayShort` cuts long clips at 1.5 s); rivals get random looks and honk back sometimes. New cart presents are not Toy Room news: the teacher says `invite.race.new` and the race zone is invited first (`PlayerProgress.unseenCartItems`, cleared when the picker opens). The save keeps `equippedCart` plus `cartPaint`, `cartTrail`, `cartHorn`, `cartNumber`, `cartExtrasOff` (every add-on switched off); an old "gold paint on" becomes the chosen paint once (`cartPaintMigrated`). Adding a paint = a line in `CartGarageData` (+ a voice line); a body = `SoapboxStreetBuilder.BodyDefs` + a Cart seed. 36 new voice lines (19 `item.cart.*`, 6 `race.tab_*`, `invite.race.new`, `race.number.1..10`) need generating (`VoiceLineSeeder.Garage.cs`).
    - **Cart bodies and the pit lane (2026-10-13, contract `cart-garage-contract.md`, part G2; rebuilt and checked in preview shots on 2026-10-13: seats, wheel bases and the pit lane look right; the user keeps the kneel-on-the-roof fire truck and police car; **not played yet**):** eight new bodies in `SoapboxStreetBuilder.BodyDefs`: `cart.rocket` (`SM_Prop_Cardboard_Rocket_01`), `cart.firetruck` (PolygonTown), `cart.wagon` (`SM_Prop_Red_Wagon_01`), `cart.horse` (`SM_Veh_Rocking_Horse_01`), `cart.police` (PolygonCity), `cart.boat` (`SM_Prop_Cardboard_Boat_01`), `cart.trike` (`SM_Veh_Trike_03`, the lowest of the three) and `cart.convertible` (PolygonTown), so a cart has 11 bodies. Each has a recipe in `SoapboxStreetBuilder.Bodies.cs` (`BodyStyles`): length (1.7 m, shorter for the horse 1.05, trike 1.45, boat 1.5, rocket 1.55, wagon 1.6, so the seated kid stays under about 1.45 m: the gate boards hang at 1.66), parts hidden (wagon handle, fire-truck ladders and nozzle), a clipped copy of the boat's mesh (hull only: `Meshes/BodyBoatHull_*`; this switches Read/Write on in the boat FBX's importer once), the rocket lying down (porthole up, fins sideways), a generated wheel base (plank + 4 chunky wheels, `Body...` materials) under the rocket, horse and boat, and the seat (a fraction along the body plus a probe window; the convertible's kid sits at its steering wheel). The kid sits on the rear bed (fire truck), the roof (police), the tube (rocket), the saddle (horse, trike) and in the tub/hull/seat (wagon, boat, convertible). The numbers (seat height per body, anchors) come from the models' real shapes and are logged at build time as `CoolSchool cart bodies:` lines; read those and the `race_cart_bodies_*` shots first. Reveal prefabs `CS_Cart_<Name>` (body + wheel base, no kid) are made by `CreateBodyPrefabs` before the catalog is seeded. Windows (transparent materials) are not repainted (`IsGlass`).
    - **Police paint (2026-10-13, rebuilt and checked in `race_cart_looks_police_blue`: it turns blue):** the police car is white, so the normal paint rule left it unchanged. Its `BodyStyle` has `PaintWhites = true`: `CartPaintMaker.Painted(..., paintWhites)` also tints light, nearly grey texels toward the paint (keeping their brightness) and saves them as `Paint_<colour>_Whites_<material>`; `BuildPaints` only does this for materials no other body uses (it logs a warning otherwise). The rocket/boat cardboard is tan and still only paints its plank and decorations.
    - **Pit lane:** 11 painted bays on the east sidewalk beside the start (x -10.9, z 22.9..43.5, skipping the banner post at z 30), one per body, cars parked nose to the road. `Scripts/Race/PitLane` reuses the Toy Room's `ToyShelfSlot` (owned = model, locked = black `PitLaneSilhouette`) and says a body's `item.<id>` line once per visit when he walks within 1.5 m (default free play only, voice queue idle, 2.5 s apart). Built by `SoapboxStreetBuilder.PitLane.cs`.
    - **Voice (8 more lines):** `item.cart.rocket|firetruck|wagon|horse|police|boat|trike|convertible.1` in `VoiceLineSeeder.Bodies.cs` (De raket! De brandweerauto! De bolderkar! Het hobbelpaard! De politieauto! De bootkar! De driewieler! De open auto!), so 44 garage lines in all. **Voice status (2026-10-13): 10 generated (the five paints, eyes, balloons, propeller, spoiler, rainbow trail); 34 still missing** (the other trails, all horns, the 8 bodies, the tab prompts, `race.number.*`, `invite.race.new`): DeepVoice answers HTTP 429 (the monthly limit; raising it had not taken effect yet). Retry `VoiceBatchGenerator.GenerateMissingFromCommandLine`; until then those lines show as subtitles.
    - **Preview shots (G2):** `race_cart_bodies_<body>_side|front` (22), `race_cart_lineup_front|top`, `race_cart_looks_*` (fire truck in every paint, plus a few bodies), `race_cart_addons_chase_*` (every add-on + plate + rainbow trail from the chase camera: do the balloons block the view?), `race_pit_lane_*` and `race_pit_lane_locked_*`, `race_cart_picker_tabs_*` (all 6 tabs, everything won) and `race_cart_picker_locked_*`. Known doubtful spots to check there: the kid's legs through the horse's neck and the trike's frame, balloon strings above the gate boards on tall bodies (horse, trike), the propeller/spoiler spots on the boat and horse, and whether the paint turns the plank and hubs of the wheel base too (it does, on purpose).
    - Test menus: **Unlock All**, **Unlock Next Present**, **Give Medal**. (The 43 toy-room lines are generated.)
  - The `pendingPowerUp` "golden_sword" is earned in a lesson (a gold sword pops up above the table and flies to a badge left of the star jar: `GoldenSwordReward`, `GoldenSwordBadge`) and consumed in the next knights game (solid-gold sword, double hits, sparkle fanfare, `knights.golden_sword` line).
  - `AddStars(count, pos, announce)`: the "Een ster!" line only plays when the voice queue is idle; the lesson passes `announce: false`.
  - **More presents (2026-10-13), rebuilt, voiced (19 lines: 18 `item.*` names and `reward.swap_wand`) and checked in the Toy Room and wardrobe preview shots; not played yet:** 18 new presents (crown, lion, magic wand, astronaut helmet, rabbit, silver shield, wizard hat, elephant, red foam sword, viking helmet, duck, wizard staff, propeller cap, pig, fireman helmet, monster, ninja sword, cowboy hat) sit between `cart.gold` and the gold variants, and the jar is 15 stars. The wand and staff ask "toverstaf" (`PresentPopup.IsWand`). Toy Room: nine swords in a zigzag, hat board, plush shelf 7 + 7 at size 0.38, wardrobe cards 6 per row at 124 px.
  - Debug menu items: **Give 15 Stars (test)** and **Reset Progress**.

## Current state
- **Committed** as `1fb2a23e`, locally: everything up to and including the classroom, the lesson, the north re-layout and 73 Dutch voice clips.
- **Second commit**, on 2026-10-11 (the commit after `1fb2a23e`): the sound-cue system, the Alien Landing, 11 alien voice clips (84 in total), the rebuilt scene, `Docs/specs/aliens-and-sound-contract.md` and this file. The user has **not played the aliens section yet**, so ask him how it plays.
- **Played and approved by the user in the editor:** tag (rounds shortened to 40 s), knights ("good enough for now"), the school and lesson after the north re-layout ("looks good"), and the voices.
- **Play-test fixes (2026-10-13), compile-clean, need a rebuild (menu Build Playground Prototype) and a look:**
  - **Cape:** `SM_Chr_Attach_Cape_01` is authored in the kid's model space (the head pieces are in head-bone space), so it stuck out sideways. `Scripts/Characters/AttachmentFit` places every costume, hat and helmet piece from the rig's bind pose, deciding per prefab; shots `costume_*` (`Editor/Characters/PreviewCapture.Costumes.cs`).
  - **Presents:** `RewardSystem` only opens a present back in free play (stars still land at once), and `AlienLanding` gives its stars after the rocket countdown. A present had interrupted the countdown.
  - **Treasure map:** the parchment and edge images kept their aspect, shrinking to a square in the middle of the 770-wide bar (`StretchToFill` in `PirateCoveBuilder.Build.cs`).
  - **Lesson camera:** it now looks about 41° down (it was about 13°); the table is 1.37 m deep and 1.8 m from the board; toys go in one row or two staggered rows; the basket stands at the right end; only the newest basket toy shows its number. Shots `lesson_*` (`Editor/Lessons/PreviewCapture.Lesson.cs`). Play-test the toy size on screen and the card overlap.
- **Back button (2026-10-12):** code done and compile-clean; the scene was rebuilt on 2026-10-12 (with the race), and the three `quit.*` voice lines are generated.
- **Soapbox race (2026-10-12):** built into the scene and checked in preview pictures (`Logs/preview/race_*.png`), **not played yet**. The 38 announcer/race voice lines are generated (Dave for the announcer); spot-check the one-word countdown (`Drie!`, `Twee!`, `Eén!`, `Start!`) by ear. Fixes made in code after that build (compile-clean, **need a rebuild + preview check**): main grass tiles under the street are removed (`RemoveMainGrass`), the cat gets `Cat_Orange_URP.mat` (its FBX imports no materials), ramp shots `race_chase_ramp_side`/`_top` plus a height-profile log line. Still open: the ramp may face the wrong way (its painted arrow points south and it looked flat in the chase shot; if the side shot shows it rising toward the arrow, reverse the probe in `BuildRamp`); house presets are single meshes so their fronts use the default +Z (check they face the street). Tuning candidates: speeds 6/7/8 m/s, rubber band in `RivalCartAI`, chase camera offset, helmet offsets (`RaceHelmet`), seat height, animal hop distances (`AnimalCrosser`). The four finish spectators only idle (no dance hook yet).
- **Known rough edges and tuning candidates:**
  - **Alien Landing (untested):**
    - ray-gun hand offset `RayGunShooter.GunLocalPosition/Euler`
    - cape and mask offsets (`CostumeHolder`)
    - baby perch visibility in the treehouse tree (`TreePerch`)
    - `JumpPad.launchSpeed`
    - camera framing of the party line
    - the carried baby plays a run animation
    - counting close-up (`CountCameraHeight/Back/Pitch` in `AlienFieldBuilder`); the baby waits at `BabyWaitPoint` behind that camera and is not counted
    - variety (2026-10-12, untested):
      - trampoline scale (2.4 m) and launch speeds, and `JumpPad.aimStandOff`
      - whether the rocket-nose baby is reachable (the perch is 1 m south of the rocket's centre, the pad about 2.3 m south)
      - bubble height (4.5 m)
      - shy cover hide/peek points
      - count-camera framing for 8 aliens
      - tree, rock and bush model choices and their bounds scaling
      - field bounds stop at across −8 (`BoundsAcrossMin`), so tiny aliens stay on screen
  - **Golden sword reward (untested):** size, height and orientation above the lesson table (`GoldenSwordHeight` in `NumbersLesson`, `displayLength` on `GoldenSwordReward`)
  - **Ice cream shop (untested):** shop camera pose and framing (the dragon is 2.8 m tall), Synty prop facing (counter, sign, register may need a 180° turn), cone-in-hand offsets (`ShopCustomerBuilder`), drag/drop radii (`IceCreamShop.Touch.cs`), order bubble size. Customers have no talk/lick clips (procedural nods).
  - **Pirate cove (untested):** ship bow direction and helm/crew/wheel heights (deck found at 2.38 m), skeleton-boat seat height (no deck found, 2.19 m fallback), `PirateShip.sailSpeed` (0.9, ~15 s trip), map icons in `Assets/CoolSchool/Pirates/MapIcons/` (delete a PNG to re-render), parrot shoulder offset, chest lid hinge, camera near the main north fence at the dock.
  - **Knights:** sword and shield hand offsets are constants at the top of `PlaygroundPrototypeBuilder`; trees may stand close to the castle wall ends.
  - **Teacher:** stands angled beside the board, but turns toward the player at runtime.
  - **Lesson UI:** check that the number cards don't cover the toys.
  - **Single number words** may have English-sounding TTS. Re-generate them if the user complains.

## Asset notes and gotchas
- **Kids pack:** its materials were Built-in Standard. `KidsMaterialUpgrader` converts them to URP/Lit, and it's idempotent. Town, City and Generic already use Synty URP shader graphs.
- **Colour grading:** only the Amplify **LUT Pack** textures are imported (`Assets/Imported/AmplifyColor/LUT/`, original package GUIDs). Amplify Color itself is a Built-in-pipeline camera effect and does nothing in URP, so it is not imported. `Editor/ColorGradingSetup.cs` puts a URP Color Lookup (`LutPath` Vibrance_B, `LutContribution` 0.75) on `Assets/Settings/SampleSceneProfile.asset` (the default profile of both RP assets) and turns on `renderPostProcessing` for the main camera. Both run on every rebuild, or use the menu item **CoolSchool → Apply Color Grading**. Turning post-processing on also brings in the profile's existing Neutral tonemapping, Bloom and Vignette.
- **Gestures MoCap pack:** contains rude taunts. **Only** use `Chicken_Dance_Taunt`, `Dance_Taunt`, `Robo_Taunt` and `Victory_Taunt`. `AlienFieldBuilder` enforces a whitelist.
- **Building scenes in batch mode:** `EditorUtility.DisplayDialog` and `SaveCurrentModifiedScenesIfUserWantsTo` are skipped when `Application.isBatchMode`.
- **Placing prefabs:** use `PrefabUtility.InstantiatePrefab`. To strip vendor scripts from a prefab instance, unpack it first (as is done for the dragon).
- **Coding constraints:**
  - Prefer UI text with `UnityEngine.UI.Text` and the `Inter-Bold.ttf` font. TextMeshPro was avoided.
  - Use the new Input System only. Inside `CoolSchool.*` namespaces never write a bare `Input.`, because `CoolSchool.Input` is a namespace.
  - `compile-check.ps1` compiles only `Assets/CoolSchool`. Third-party types (e.g. DeepVoice) are reached through reflection.

## Roadmap and next options
The user chose and ordered the play-section focus as: pretend battles plus create & role-play first; racing and other things later.
- **Battles:** Knights done; Aliens & superheroes built but untested; Pirates built 2026-10-12, untested.
- **Create & role-play (chosen):** **Shopkeeper** (ice cream shop built, untested; bakery or pizza could follow), **Builder** (build the cardboard castle, then defend it), **Band & dress-up** (instruments, costume parade, photos).
- **Learning:** letters (starting with his name, which is not known yet), colours and shapes, then first words and sums. Reuse the lesson framework in `Scripts/Lessons`.
- **Character creator and parent area:** he builds his own kid and the teacher says his name. A locked parent area holds language, an optional time limit, progress, and volume.
- **Tablet:** install Android Build Support through Unity Hub, prepare a batch APK build, and test touch and performance on his tablet.
- **Audio:** apply the picks in Unity and audition them (see the Audio bullet above), then find music; `SoundAutoAssign` can fill any cues the picks file leaves empty.
- **Later:** the neighbourhood (safe street, houses, his home, shops), soapbox racing, the English language track.

## Memory
The persistent memory in `C:\Users\timo-\.claude\projects\C--Users-timo--Documents-GIT-CoolSchool\memory\` holds the vision and decisions, the delegate-to-Sonnet rule and the compile-check note. Keep it updated.
