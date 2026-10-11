# Voice lines and rewards: implementation contract

Two features built in parallel by two agents. Each agent owns its own files. The integration points below are fixed so the two agents don't collide. Read `Docs/specs/knights-slice-contract.md` for the existing architecture (Activities, TagGame, KnightsGame, battle scripts, builders).

**Verification:** run `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It compiles all of `Assets/CoolSchool` with Unity's compiler while the editor stays open. It must print `compile-check: OK` before you report back.

Player: a 4-year-old who can't read. Everything important is **spoken**, and visuals are big and simple. Dutch first, English added later through the same tables.

---

## A. Voice lines (agent A)

### Runtime: `Assets/CoolSchool/Scripts/Voice/` (namespace `CoolSchool.Voice`)
- `public enum GameLanguage { Dutch, English }`
- `public static class LanguageSettings`
  - `public static GameLanguage Current { get; set; }`, saved in PlayerPrefs `"cs.language"`, default Dutch.
  - `public static event System.Action Changed;`
- `[System.Serializable] public class VoiceLine`
  - fields: `string key` (unique, e.g. `tag.start.1`), `string group` (e.g. `tag.start`; several lines can share a group), `Speaker speaker`, `string dutch`, `string english`, `AudioClip dutchClip`, `AudioClip englishClip`
  - `public enum Speaker { Teacher, Kid, Goblin, Narrator }`
- `[CreateAssetMenu] public class LineTable : ScriptableObject`
  - `public List<VoiceLine> lines`
  - `public VoiceLine Pick(string group)`: random line from the group, avoiding the last one played for that group.
  - `public VoiceLine Find(string key)`
- `public class VoicePlayer : MonoBehaviour` (one per scene)
  - serialized: `LineTable table`, `AudioSource source` (2D), `bool showDebugSubtitles` (editor/dev only, default true in editor)
  - `public static VoicePlayer Instance`
  - `public static void Say(string group, bool interrupt = false)`: picks a line and queues it.
    - While a Teacher line is playing, Kid and Goblin lines are dropped rather than queued, so the teacher is never drowned out.
    - The queue holds at most 2 waiting lines.
    - Lines repeated within 1.5 s are dropped.
  - Missing clip: log `[Voice] <text>` and show the debug subtitle for 2 s, so the game is testable before any audio exists.
  - The debug subtitle is drawn with `OnGUI` only when `showDebugSubtitles` is on.
  - `public bool IsSpeaking`.
- `public class VoiceCue : MonoBehaviour`: a tiny component with `public void Say(string group)` and `public void SayInterrupt(string group)`. Builders wire UnityEvents to it with persistent string listeners.
- `public class BattleVoice : MonoBehaviour`
  - serialized: `CoolSchool.Battle.KnightsGame game`
  - At runtime it subscribes in code to:
    - every opponent's `Hittable.Defeated` → `knights.goblin_down`
    - `DragonBoss.FireTelegraphStarted` (new event, see below) → `knights.fire_warning`
  - It plays the `knights.shield_hint` line once, 6 s after the first wave starts.

### Runtime changes owned by agent A
- `Activity`: add `public UnityEngine.Events.UnityEvent onBegan;` and `public UnityEngine.Events.UnityEvent onEnded;`. Invoke them after OnBegin and after OnEnd.
- `TagGame`: add `public UnityEvent onRoundFinished;`, invoked when the round timer runs out, before the cheer.
- `DragonBoss`: add `public event System.Action FireTelegraphStarted;`, raised when the fire warning appears.

### Editor (agent A): `Assets/CoolSchool/Editor/Voice/`
- `VoiceLineSeeder`: creates or updates `Assets/CoolSchool/Voice/Lines.asset` (LineTable) from the seed list below.
  - Lines are matched by key. Text is updated, existing clip references are never cleared, and new keys are added.
- Menu **CoolSchool/Voice/Export Lines for DeepVoice**: writes `Docs/voice/lines_nl.txt` (and `lines_en.txt`).
  - Format: one block per line, the suggested file name on its own line, then the text, then a blank line.
  - File name = `nl_` + key with `.` replaced by `_` (e.g. `nl_tag_start_1`), and `en_` for English.
  - Grouped by speaker, with a header suggesting the voice: Teacher → a warm female Multi voice, Kid → a young voice, Goblin → a silly voice.
- Menu **CoolSchool/Voice/Link Generated Clips**: finds AudioClips in the project named exactly like those file names and assigns them to `dutchClip` / `englishClip`. It reports how many were linked and which are missing.
- `VoiceSetupBuilder.Setup(...)`, a static method called by the playground builder. It creates a "Voice" GameObject with VoicePlayer (table + AudioSource), VoiceCue and BattleVoice, then wires persistent listeners with `UnityEditor.Events.UnityEventTools.AddStringPersistentListener` to `VoiceCue.Say`:
  - TagGame.onBegan → `tag.start`
  - TagGame.onPlayerTaggedSomeone → `tag.tagged_someone`
  - TagGame.onPlayerBecameIt → `tag.became_it`
  - TagGame.onRoundFinished → `tag.round_finished`
  - KnightsGame.onBegan → `knights.start`
  - KnightsGame.onWaveStarted → `knights.wave`. Use a small `VoiceCue.SayWave(int)` that picks `knights.wave` for wave 1 and `knights.wave_more` after that, wired with `AddPersistentListener`.
  - KnightsGame.onDragonArrived → `knights.dragon`
  - KnightsGame.onVictory → `knights.victory`
  - The welcome line `general.welcome` plays from a `VoiceCue` call 1 s after the scene starts. A small `SayOnStart` component is fine.
- Integration: add exactly one call to `VoiceSetupBuilder.Setup(...)` near the end of `PlaygroundPrototypeBuilder.Build`, after TagGame and KnightsGame exist. Re-read the file right before editing; another agent adds a similar one-line call.

### Seed lines (Dutch / English)

| key | group | speaker | Dutch | English |
|---|---|---|---|---|
| general.welcome.1 | general.welcome | Teacher | Hoi! Wat fijn dat je er bent. Kom, we gaan spelen! | Hi! So nice to see you. Come on, let's play! |
| tag.start.1 | tag.start | Teacher | Tikkertje! Jij bent hem. Tik de andere kinderen! | Tag! You're it. Go and tag the other kids! |
| tag.tagged_someone.1 | tag.tagged_someone | Kid | Oh nee, getikt! | Oh no, you got me! |
| tag.tagged_someone.2 | tag.tagged_someone | Kid | Haha, jij bent snel! | Haha, you're fast! |
| tag.tagged_someone.3 | tag.tagged_someone | Kid | Oké, nu ben ik hem! | Okay, now I'm it! |
| tag.became_it.1 | tag.became_it | Kid | Hihi, nu ben jij hem! | Hehe, now you're it! |
| tag.became_it.2 | tag.became_it | Kid | Getikt! Jij bent hem! | Tagged! You're it! |
| tag.round_finished.1 | tag.round_finished | Teacher | Wat een goed spel! Even uitrusten. | What a great game! Time for a little rest. |
| knights.start.1 | knights.start | Teacher | Ridders! Bescherm het kasteel! Tik op de knop om met je zwaard te zwaaien. | Knights! Protect the castle! Tap the button to swing your sword. |
| knights.shield_hint.1 | knights.shield_hint | Teacher | Houd de knop ingedrukt, dan pak je je schild! | Hold the button down to raise your shield! |
| knights.wave.1 | knights.wave | Kid | Daar komen de kobolden! | Here come the goblins! |
| knights.wave_more.1 | knights.wave_more | Kid | Nog meer kobolden! | Even more goblins! |
| knights.goblin_down.1 | knights.goblin_down | Goblin | Hihihi, je hebt me! | Hehehe, you got me! |
| knights.goblin_down.2 | knights.goblin_down | Goblin | Oei, ik val om! | Whoops, I'm falling over! |
| knights.goblin_down.3 | knights.goblin_down | Goblin | Haha, jij bent sterk! | Haha, you're strong! |
| knights.dragon.1 | knights.dragon | Teacher | Kijk! Een draak! Hij wil alleen spelen. Pas op voor zijn confettivuur! | Look! A dragon! He just wants to play. Watch out for his confetti fire! |
| knights.fire_warning.1 | knights.fire_warning | Kid | Schild omhoog! | Shields up! |
| knights.fire_warning.2 | knights.fire_warning | Kid | Pas op, vuur! | Watch out, fire! |
| knights.victory.1 | knights.victory | Teacher | Hoera! Jullie hebben het kasteel gered! | Hooray! You saved the castle! |
| reward.star.1 | reward.star | Narrator | Een ster! | A star! |
| reward.jar_full.1 | reward.jar_full | Teacher | De pot is vol! Je krijgt een cadeautje! | The jar is full! You get a present! |
| reward.present_tap.1 | reward.present_tap | Teacher | Tik op het cadeau! | Tap the present! |
| reward.present_open.1 | reward.present_open | Teacher | Wauw! Kijk eens wat erin zit! | Wow! Look what's inside! |

---

## B. Rewards: stars, star jar, presents (agent B)

### Runtime: `Assets/CoolSchool/Scripts/Rewards/` (namespace `CoolSchool.Rewards`)
- `[System.Serializable] public class PlayerProgress`
  - fields: `int starsInJar`, `int totalStars`, `int presentsOpened`, `List<string> unlockedItems`, `string equippedSword`
- `public static class ProgressStore`
  - `public static PlayerProgress Current` (lazy load)
  - `Load()` / `Save()` as JSON (`JsonUtility`) in `Application.persistentDataPath/progress.json`
  - Save is called after every change.
  - `public static event System.Action Changed;`
  - `ResetAll()`, for the editor menu and later the parent area.
- `public enum RewardKind { Sword, Shield, Costume, Hat, PlushToy }`
- `[System.Serializable] public class RewardItem`: `string id`, `RewardKind kind`, `GameObject prefab` (for the 3D reveal), `Vector3 revealEuler`, `float revealScale` (1)
- `[CreateAssetMenu] public class RewardCatalog : ScriptableObject`
  - `public List<RewardItem> items` (in the order they are given)
  - `public RewardItem NextLocked(PlayerProgress p)`: the first item not yet unlocked, or null.
- `public class RewardSystem : MonoBehaviour` (one per scene)
  - serialized:
    - `RewardCatalog catalog`, `int jarCapacity` (10)
    - `StarJarHud hud`, `PresentPopup popup`
    - `CoolSchool.Tag.TagGame tagGame`, `CoolSchool.Battle.KnightsGame knightsGame`
    - `int starsForTagRound` (3), `int starsForKnightsVictory` (5)
  - Static `public static void AddStars(int count, Vector3 worldPosition)`:
    - Each star flies (UI) from the world position's screen point to the jar, one by one about 0.15 s apart.
    - Each landing fills the jar and calls `VoicePlayer.Say("reward.star")` only for the first star of a batch.
    - When `starsInJar >= jarCapacity`, the jar empties into a present: `VoicePlayer.Say("reward.jar_full")`, then `popup.Show(item)` with `catalog.NextLocked`. If everything is unlocked, show a plush-toy bonus or just confetti.
  - Subscribes in OnEnable (code listeners) to `tagGame.onRoundFinished` (+starsForTagRound at the player) and `knightsGame.onVictory` (+starsForKnightsVictory). Null-safe.
  - The reward plays after the activity's own victory moment, so delay it about 1.5 s.
- `public class StarJarHud : MonoBehaviour`
  - A jar or star icon in the top-right corner of the TouchUI. An `Image` with fillAmount (vertical) shows starsInJar / jarCapacity.
  - It bounces when a star lands.
  - Serialized: `Image fill`, `RectTransform target` (where stars fly to), `Image flyingStarPrefab` or a sprite to spawn.
  - `public void Refresh(int inJar, int capacity)`.
- `public class PresentPopup : MonoBehaviour`
  - Full-screen dim panel with a big Gift image. The first tap shakes it, the second tap opens it (or one tap, then an auto-open after the shake).
  - On opening: `VoicePlayer.Say("reward.present_open")`, `BattleEffects.Confetti` at the camera front, and a **3D reveal**.
  - **3D reveal:** a dedicated `RevealStage`, a disabled camera at y = −100 rendering to a RenderTexture shown in a RawImage. It instantiates the item prefab there, spinning slowly.
  - On showing the present: `VoicePlayer.Say("reward.present_tap")`.
  - A tap on the revealed item closes the popup. On close the item is unlocked; a Sword is also equipped (`equippedSword = id`).
  - The game is NOT paused (timeScale stays 1), but `PlayerController.InputEnabled = false` while the popup is open.
- `public class EquippedSwordApplier : MonoBehaviour`, on the player.
  - serialized: `RewardCatalog catalog`
  - On Start and on `ProgressStore.Changed`: if `equippedSword` is set, call `WeaponHolder.SetSwordPrefab(prefab)`.
  - New `WeaponHolder.SetSwordPrefab(GameObject)` (owned by agent B): replaces the sword prefab and destroys an already-created sword instance, so it is recreated next time.
- Voice calls go through `CoolSchool.Voice.VoicePlayer.Say(...)`, which agent A writes in parallel. Until it exists, compile-check fails only on those references. See the coordination note below.

### Editor (agent B): `Assets/CoolSchool/Editor/Rewards/`
- `RewardCatalogSeeder`: creates or updates `Assets/CoolSchool/Rewards/Catalog.asset` in this order:
  - `sword.plastic` → `SM_Wep_Sword_Plastic_01`
  - `hat.knight_helmet` → `SM_Chr_Attach_Cardboard_KnightHelmet_01`
  - `plush.dino` → `SM_Prop_Plush_Dino_01`
  - `sword.wood` → `SM_Wep_Sword_Wood_02`
  - `shield.round` → `SM_Wep_Shield_03`
  - `plush.unicorn` → `SM_Prop_Plush_Unicorn_01`
  - `hat.cap` → `SM_Chr_Attach_Hat_Cap_Back_01`
  - `sword.foam_big` → `SM_Wep_Sword_Foam_03`
  - `plush.bear` → `SM_Prop_Plush_Bear_01`
  - `hat.pirate` → any pirate hat attachment found by name containing "Pirate" and "Hat", else skip
  - Prefabs are resolved by name.
- `RewardsSetupBuilder.Setup(...)`, a static method called by the playground builder. It creates:
  - a RewardSystem GameObject wired to the catalog, TagGame, KnightsGame, the HUD and the popup;
  - the StarJarHud and PresentPopup UI on the TouchUI canvas, using ModularGameUIKit sprites found by name: `Star` (prefer `Icon-Symbols/Stars/Star.png`) and `Gift` (prefer `Icons/Gift.png`). Use plain Images built in code, not the kit's popup prefabs;
  - the RevealStage (camera, RenderTexture asset `Assets/CoolSchool/Rewards/Reveal.renderTexture`, a light);
  - EquippedSwordApplier on the player.
- Menu **CoolSchool/Rewards/Reset Progress** and **CoolSchool/Rewards/Give 10 Stars (test)**. The test menu works in Play mode by calling `RewardSystem.AddStars`.
- Integration: add exactly one call to `RewardsSetupBuilder.Setup(...)` near the end of `PlaygroundPrototypeBuilder.Build`, after TagGame and KnightsGame exist. Re-read the file right before editing; agent A adds a similar call.

### Coordination note
Agent A owns `Activity.onBegan/onEnded`, `TagGame.onRoundFinished`, `DragonBoss.FireTelegraphStarted` and everything in `Voice/`. Agent B owns `WeaponHolder.SetSwordPrefab` and everything in `Rewards/`.

If compile-check fails **only** because the other agent's members don't exist yet, wait about a minute and re-run it, up to about 10 minutes. Never create the other agent's types or members yourself. If it still fails, report which members were missing.
