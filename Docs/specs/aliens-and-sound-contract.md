# Alien landing (superheroes) and sound hooks: implementation contract

Two agents work in parallel:
- **Agent A** builds the "Alien Landing" play section.
- **Agent K** builds the sound-cue system and adds cue calls across the existing game.

Read these first:
- `Docs/specs/knights-slice-contract.md`: Activities, battle system, builders, by-name asset lookup.
- `Docs/specs/voice-and-rewards-contract.md`
- `Docs/specs/classroom-numbers-contract.md`: LessonUI number cards.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print `compile-check: OK` before you report back.

**Player:** a 4-year-old who can't read. Everything is spoken; he never fails. Opponents don't hurt: they tumble or turn friendly. Controls stay one joystick plus the one action button.

**World layout:** the camera always looks NORTH.
- The school is north and the knights arena is east.
- The new alien landing field goes **WEST** (-X): the playground rectangle extends about 24 m toward -X, mirroring the knights arena on +X.

---

## K. Sound cues (agent K)

Audio packs will be imported later. Every meaningful action must already call a named cue, so audio only has to be filled into one table.

### Runtime: `Assets/CoolSchool/Scripts/Audio/` (namespace `CoolSchool.Audio`)
- `[CreateAssetMenu] public class SoundLibrary : ScriptableObject`
  - `List<SoundEntry> entries`, where `SoundEntry` is `{ string cue; List<AudioClip> clips; float volume = 1; Vector2 pitchRange = (0.95, 1.05); bool spatial = true; float cooldown = 0.05 }`.
  - `public SoundEntry Find(string cue)`.
- `public class Sfx : MonoBehaviour`, one per scene. Serialized: `SoundLibrary library`, `int poolSize` (12), `AudioMixerGroup output` (optional).
  - `public static void Play(string cue, Vector3? position = null)`
    - Picks a random clip with a random pitch inside the range. 3D when spatial and a position is given, otherwise 2D.
    - Respects the per-cue cooldown and uses a pooled AudioSource.
    - **A cue with no clips is silent.** In the editor, log `[Sfx] <cue>` once per cue so missing sounds are easy to spot.
  - `public static void PlayMusic(string cue)` and `StopMusic(float fade)`: one looping music source with a crossfade.
  - `public static float MusicVolume` and `SfxVolume`, kept in PlayerPrefs, for the parent area later.
- `public class SfxCue : MonoBehaviour`: `public void Play(string cue)`, so UnityEvents can be wired.
- `public class MusicZone : MonoBehaviour`. When the player enters a box or radius it calls `Sfx.PlayMusic(cue)`. Optional now; create it but don't place it.

### Cue names
Seed every cue into the library, with empty clip lists, from the editor seeder.

| Area | Cues |
|---|---|
| UI | `ui.tap`, `ui.button`, `ui.card_right`, `ui.card_wrong`, `ui.popup_open`, `ui.popup_close` |
| Player | `player.step`, `player.jump`, `player.land`, `player.bump` |
| Activities | `zone.enter`, `zone.invite_chime`, `activity.start`, `activity.end_cheer` |
| Tag | `tag.tagged`, `tag.whoosh` |
| Battle | `battle.sword_swing`, `battle.hit_confetti`, `battle.shield_block`, `battle.tumble`, `battle.giggle`, `battle.goblin_appear`, `battle.wave_horn`, `dragon.roar`, `dragon.fire`, `dragon.wing_flap`, `dragon.happy` |
| Imagination | `imagination.sparkle` |
| Rewards | `reward.star_fly`, `reward.star_land`, `reward.jar_full`, `reward.present_shake`, `reward.present_open`, `reward.item_reveal` |
| Lesson | `lesson.toy_pop`, `lesson.toy_into_basket`, `lesson.chalk`, `lesson.bell` |
| Aliens | `aliens.rocket_land`, `aliens.rocket_takeoff`, `aliens.zap`, `aliens.zap_hit`, `aliens.slime_throw`, `aliens.slime_splat`, `aliens.dance_party`, `aliens.jump_pad`, `aliens.rescue` |
| Music | `music.playground`, `music.knights`, `music.aliens`, `music.classroom`, `music.victory` |

### Call sites (agent K adds these to EXISTING code)
- **Battle:**
  - `SwordFighter.Attack`: `battle.sword_swing`.
  - `Hittable` hit, blocked and defeated: `battle.hit_confetti`, `battle.shield_block`, `battle.tumble` (defeated only).
  - `BattleOpponentAI`: `battle.giggle` on defeat.
  - `KnightsGame`: `battle.wave_horn` on each wave and `music.knights` on begin.
  - `DragonBoss`: `dragon.roar` on enter, `dragon.fire` on breath, `dragon.happy` on defeat, `dragon.wing_flap` on fly-away.
- **Imagination:** `ImaginationEffect`: `imagination.sparkle`.
- **Tag:** `tag.tagged` and `music.playground` restore.
- **Activities:**
  - `ActivityZone` start: `zone.enter`.
  - `ActivityInviter` invite: `zone.invite_chime`.
  - `ActivityManager`: `music.playground` when the default activity resumes.
- **Player:**
  - `PlayerController` jump and land: `player.jump`, `player.land`. Land fires when grounded again after more than 0.2 s in the air.
  - `player.step`: a footstep every 0.35 s while running, from `KidAnimator` or `PlayerController`, player only.
- **Rewards:**
  - `RewardSystem` / `StarJarHud`: `reward.star_fly` and `reward.star_land`.
  - `PresentPopup`: `reward.present_shake`, `reward.present_open`, `reward.item_reveal`, `ui.popup_open`, `ui.popup_close`.
  - `reward.jar_full`.
- **Lesson:**
  - `NumbersLesson`: `lesson.bell` on begin, `music.classroom` on begin, `lesson.toy_pop`, `lesson.toy_into_basket`.
  - `LessonUI`: `ui.card_right`, `ui.card_wrong`.
  - `BlackboardDisplay` write: `lesson.chalk`.
- **UI:** `ActionButton` press: `ui.button`.

Add each call next to the existing effect call. Change no behaviour.

### Editor (agent K): `Assets/CoolSchool/Editor/Audio/`
- `SoundLibrarySeeder` creates or updates `Assets/CoolSchool/Audio/SoundLibrary.asset` with every cue above. It never clears clips that are already assigned.
- `SoundAutoAssign`, menu **CoolSchool/Audio/Auto-Assign Clips From Folder…**:
  - Pick a folder. For each cue, find AudioClips whose file name contains keywords mapped from the cue, and assign up to 4 of them.
  - Keywords: the cue's last part plus synonyms. For example `sword_swing` → swing, swoosh, whoosh, slash; `star_land` → star, twinkle, chime, coin; `hit_confetti` → pop, hit, confetti; `tumble` → fall, thud; `jump` → jump, hop; `zap` → zap, laser, blaster.
  - Keep the synonym table in code, where it is easy to extend.
  - Print a report of assigned and unassigned cues.
  - This lets the developer import packs and fill most cues with one click.
- `SoundSetupBuilder.Setup()`: creates the scene's "Sfx" object wired to the library, with an AudioListener staying on the main camera. Call it from PlaygroundPrototypeBuilder after `VoiceSetupBuilder.Setup()`: one line, and re-read the file before editing.
- Music ducking: when `VoicePlayer.IsSpeaking`, music volume drops to 40%. Implement it in `Sfx` by polling `CoolSchool.Voice.VoicePlayer.Instance`.

---

## A. Alien Landing (agent A)

### Fantasy
A cardboard rocket lands on the field west of the playground, and silly alien kids pop out. The player becomes a **superhero**, with a cape and mask appearing through the subtle imagination sparkle, and gets a **ray gun**. Zapped aliens don't get hurt: they turn into friends and start **dancing**.

A baby alien gets stuck up in a tree. The player runs onto a glowing **jump pad**, makes a super jump and rescues it. To finish, they count the dancing aliens together (number cards), throw a dance party, and the rocket takes off. Stars follow.

### Flow (`AlienLanding : CoolSchool.Activities.Activity`, namespace `CoolSchool.Aliens`)
1. **OnBegin**
   - Imagination sparkle on. The player gets the costume: cape `SM_Chr_Attach_Cape_01` and mask `SM_Chr_Attach_Superhero_Mask_01`, attached to the bones (see `CostumeHolder`), and the ray gun `SM_Wep_Ray_Gun_01` in the right hand.
   - The action-button icon becomes a Bolt or lightning sprite found by name. Jump is disabled.
   - Voice `aliens.start`; `Sfx.Play("aliens.rocket_land")`; music `music.aliens`.
   - The rocket descends from the sky to its landing spot over about 3 s, with sparkles.
2. **Wave of aliens**
   - 4 alien kids (`SM_Chr_Kid_Alien_01/02`) walk out of the rocket one by one.
   - They wander toward the player, and every 4–6 s one throws a slow, telegraphed slime ball: a green arc projectile, about 1 s of flight, with a ground marker.
   - A hit makes the player wobble (Hittable stun only; he can never be defeated). Voice `aliens.slime_warning` plays on the first throw.
   - Hint: voice `aliens.zap_hint` after 5 s if he hasn't zapped yet.
3. **Zapping**
   - Tap = `RayGunShooter.Fire()`. It auto-aims at the nearest non-friend alien within 9 m, plays the pistol attack animation, and sends a short bright beam (LineRenderer flash, 0.15 s) plus `BattleEffects.Sparkle` at the target.
   - Each alien has Hittable with hitsToDefeat 1. On Defeated it becomes a **friend**: green confetti, alien voice `aliens.zapped`, then it plays a dance loop near the rocket.
   - Friends never attack again.
4. **Rescue**
   - Once all aliens are friends, a baby alien (`SM_Chr_Baby_Alien_01`) is revealed stuck high in a tree near the field (placed by the builder about 4 m up). Voice `aliens.rescue`.
   - A glowing jump pad (`JumpPad`) lights up beneath it, with a pulsing ring and an arrow sparkle trail from the player using the existing pattern.
   - Running onto the pad launches the player straight up with a big arc (`PlayerController.Launch(Vector3 velocity)`, new, see below), about 1.5 s of air time.
   - At the top, if he is near the baby, it attaches to his arms and they float down together.
   - On landing: voice `aliens.rescued`, confetti. The baby runs to its friends.
5. **Count**
   - All friends line up in a row in front of the camera.
   - Voice `aliens.count`, then the existing `LessonUI.ShowNumberCards` with the right answer (5: 4 aliens plus the baby) and two distractors.
   - Right card: `lesson.correct`. Wrong card: wiggle plus `lesson.try_again`, then count along: each alien jumps as `number.k` plays. Always succeed.
   - If `LessonUI` isn't in the scene, skip this step.
6. **Party and finish**
   - Everyone dances, including the player, using the Cheer or dance trigger. Voice `aliens.victory`; `aliens.dance_party`.
   - After 4 s the rocket takes off upward, and the aliens wave and vanish with a sparkle. `RewardSystem` gives +5 stars: subscribe to the new `onVictory` UnityEvent, like knights.
   - End. **OnEnd** restores everything: costume off, ray gun hidden, icon reset, jump enabled, aliens and baby reset or inactive, rocket back in the sky and hidden.

### Components (namespace `CoolSchool.Aliens` unless noted)
- `AlienLanding` (above). Serialized refs, all wired by the builder:
  - player, player input, action button, bolt icon, imagination
  - rocket transform, rocket landing point, rocket sky point
  - alien spawn points, aliens array, baby alien, tree perch point, jump pad, party line points
  - `LessonUI` (optional)
  - UnityEvents `onVictory`, `onRescue`
- `AlienKidAI`:
  - Uses NavMeshAgent, Hittable [Opponents, hitsToDefeat 1], KidAnimator and `SlimeThrower`.
  - States: Wander toward player → Throw (telegraphed) → Friend (dance, follows the party spot).
  - `public bool IsFriend`.
- `SlimeThrower`: lobs a slime projectile (small green sphere with a trail) to a predicted spot. It telegraphs with a green ground circle for 1 s. Hero Hittables inside the circle on impact get TakeHit, and a block or shield isn't used here. Splat effect: green confetti.
- `RayGunShooter`:
  - On the player, enabled only during the activity, like `PlayerBattleInput`.
  - Reads the ActionButton (tap) and Space/Gamepad south.
  - Auto-aim, beam and Hittable.TakeHit on the target. Cooldown 0.35 s.
- `JumpPad`:
  - A trigger radius. When the player is on it and it's active, call `PlayerController.Launch`. Visual pulse.
  - `Sfx.Play("aliens.jump_pad")`.
- `RescueTarget`: the baby alien on the perch. `TryGrab(Transform player)` when the player is within 1.5 m in the air, then it parents to the player until he lands.
- `CostumeHolder` (namespace `CoolSchool.Characters`):
  - Like `WeaponHolder`: `ShowCostume(bool)` lazily instantiates attachment prefabs under humanoid bones (Head for the mask, Chest or UpperChest for the cape), with serialized offsets.
  - Synty Kids attachment prefabs are usually authored to fit the kid rig at the bone origin, so start with zero offsets.
- `PlayerController.Launch(Vector3 velocity)`: new public method. Sets the vertical velocity, adds horizontal carry, and keeps input enabled. Owned by agent A.
- `RewardSystem`: subscribe to `AlienLanding.onVictory` (+5 stars) the same way as knights. Agent A adds a serialized `alienLanding` field and its subscription; agent K does NOT touch RewardSystem's subscription code.

### Animations (agent A extends `KidAnimatorBuilder` and `KidAnimator`)
- New trigger `Dance` and int `DanceVariant` (0–2) → `Chicken Dance`, `Dance Taunt`, `Robo Taunt`.
  - These clips are in the Gestures MoCap pack and are named inside `Chicken_Dance_Taunt.fbx`, `Dance_Taunt.fbx` and `Robo_Taunt.fbx`. The clip is the first non-preview clip in each file.
  - Loop them while the bool `Dancing` is true.
  - **Only ever use these three dance clips plus `Victory_Taunt` from that pack.** The pack also contains rude gestures, which must never be used in a kids' game.
- `KidAnimator.SetDancing(bool)`, plus `PlayShoot()`: trigger `Shoot` on the UpperBody layer → `Pistol-Attack-R1` (RPG pack).
- Slime throw: trigger `Throw` → a throwing or casting clip from the RPG pack (`Unarmed-Attack-R1` or `Armed-Cast-R-Attack1`, whichever exists; report which).

### Builder (agent A): `Assets/CoolSchool/Editor/Aliens/AlienFieldBuilder.cs`
- **Playground extension:** PlaygroundPrototypeBuilder extends the rectangle about 24 m toward **-X**. Use its `ArenaDirection`-style constants: add `AlienFieldDirection = Vector3.left`. Grass, fence and trees adapt; keep the field's open ground clear.
- **The field contents:**
  - **Rocket:** `SM_Prop_Cardboard_Rocket_01`, scaled to about 5 m tall by bounds. Its landing point is near the field's far (west) end, and its sky point is 25 m above.
  - **Points:** 4 alien spawn points at the rocket's base, and party-line points in front of the camera side (south-east of the rocket, so the north-looking camera sees them).
  - **Tree and jump pad:** one large tree (`SM_Env_Tree_Large_01`) about 6 m from the rocket, with a perch point about 4 m up in its crown on the camera-facing side. The jump pad sits on the ground directly below the perch, offset 0.5 m south.
  - **Activity zone:** at the field's east edge, the playground side, with the existing zone helper. It gets a purple beacon and an icon found by name: `Head-Hero` from ModularGameUIKit, with fallback `Bolt`.
- **Aliens:** 4 alien kids (inactive), each with NavMeshAgent, CapsuleCollider, Hittable, AlienKidAI, SlimeThrower and KidAnimator, plus 1 baby alien (inactive), all using Kid.controller.
- **Player:** add CostumeHolder (cape + mask), RayGunShooter (disabled, button wired), and a ray-gun WeaponHolder-like attachment. Simplest is a second `WeaponHolder` with the ray gun as its "sword" and no shield, if WeaponHolder supports it; otherwise a minimal `PropHolder`.
- **Invites:** add the alien zone to the ActivityInviter with group `invite.aliens`, after classroom, tag and castle.
- Call `AlienFieldBuilder.Build(...)` from PlaygroundPrototypeBuilder: the static geometry before the NavMesh bake, and the characters after, like `KnightsArenaBuilder`. Re-read the file before every edit.

### Voice lines (agent A adds them to `VoiceLineSeeder`)
Add `Speaker.Alien` with pitch 1.5 in VoicePlayer. Generation voice: `Freya`, added to VoiceGenerationSettings.

| key | speaker | Dutch | English |
|---|---|---|---|
| `aliens.start.1` | Teacher | Kijk! Er landt een raket! Er komen aliens uit! Jij bent de superheld! | Look! A rocket is landing! Aliens are coming out! You're the superhero! |
| `aliens.zap_hint.1` | Teacher | Tik op de knop, dan flits je met je straalpistool! | Tap the button to zap with your ray gun! |
| `aliens.slime_warning.1` | Kid | Pas op, slijm! | Watch out, slime! |
| `aliens.zapped.1` | Alien | Wiehoe! Ik wil dansen! | Wheee! I want to dance! |
| `aliens.zapped.2` | Alien | Hihi, jij bent mijn vriend! | Hehe, you're my friend! |
| `aliens.zapped.3` | Alien | Bliep bloep, wat leuk! | Bleep bloop, what fun! |
| `aliens.rescue.1` | Teacher | Help! De baby-alien zit vast in de boom! Spring op de trampoline! | Help! The baby alien is stuck in the tree! Jump on the trampoline! |
| `aliens.rescued.1` | Teacher | Je hebt hem gered! Wat een held! | You saved him! What a hero! |
| `aliens.count.1` | Teacher | Hoeveel aliens dansen er? | How many aliens are dancing? |
| `aliens.victory.1` | Teacher | Hoera! Alle aliens zijn onze vrienden! Feest! | Hooray! All the aliens are our friends! Party! |
| `invite.aliens.1` | Teacher | Kom snel! Er zijn aliens geland! | Come quickly! Aliens have landed! |

After the build, the main agent runs the batch voice generation for the new lines.

### Coordination
- Agent K owns: `Scripts/Audio`, `Editor/Audio`, and adding `Sfx.Play` calls to existing scripts.
- Agent A owns: `Scripts/Aliens`, `Editor/Aliens`, `CostumeHolder`, `PlayerController.Launch`, the KidAnimator/KidAnimatorBuilder additions, the VoicePlayer `Speaker.Alien` pitch, the voice seeder lines, and the RewardSystem alien subscription.
- Agent A calls `CoolSchool.Audio.Sfx.Play(...)` in its new code. Until K's type exists, compile-check fails only on that type; wait about a minute and re-run, for up to about 10 minutes. Never create the other agent's types.
- Both agents edit some of the same files: PlaygroundPrototypeBuilder, KidAnimator, PlayerController and VoicePlayer. Re-read each file right before every edit and keep edits small and local.
