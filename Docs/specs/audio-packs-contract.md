# Audio packs contract (2026-10-12)

Fills the sound cues with clips from the user's Asset Store audio packs, and adds a looping **ambience** layer.
Only the picked clips are imported (the packs are 0.1 to 0.65 GB each), with their original package GUIDs and .meta files.

## Source of truth: `CoolSchool/Assets/CoolSchool/Audio/SoundPackPicks.txt`
- Lines starting with `#` and blank lines are ignored.
- Data line, **tab separated**: `cue \t volume \t pitchMin \t pitchMax \t clips`.
  Empty volume = 1; empty pitch = 0.95..1.05. Clips are separated by ` | ` (space, pipe, space), and each is `<KEY>/<path inside the pack>`.
- Pack keys (identical table in Python and C#):

| Key | Package file (under `C:/Users/Timo/AppData/Roaming/Unity/Asset Store-5.x/`) | Path prefix inside the package | Project folder |
|---|---|---|---|
| PM | `Epic Sounds and FX/AudioSound FX/Phantom Magic Sounds.unitypackage` | `Assets/Phantom Magic/` | `Assets/Imported/Audio/PhantomMagic/` |
| AR | `Audio Alchemy Room/AudioSound FX/Action RPG SFX Pack v2.unitypackage` | `Assets/Action RPG SFX V2/` | `Assets/Imported/Audio/ActionRPG/` |
| ENV | `PlaceHolder Inc/AudioSound FX/Environment Sounds Pack.unitypackage` | `Assets/Environment Sounds/` | `Assets/Imported/Audio/EnvironmentSounds/` |
| BES | `Khron Studio/AudioSound FXCreatures/Bestiary Monster Bundle Vol 1.unitypackage` | `Assets/BestiaryMonsterBundle/` | `Assets/Imported/Audio/Bestiary/` |
| MON | `Cafofo/AudioSound FXCreatures/Monster Sounds Pack.unitypackage` | `Assets/Monster Sounds Pack/` | `Assets/Imported/Audio/MonsterSounds/` |
| FEEL | `More Mountains/ScriptingEffects/Feel.unitypackage` | `Assets/Feel/` | `Assets/Imported/Audio/Feel/` |
| GUI | `gamevanilla/Textures MaterialsGUI Skins/Ultimate Clean GUI Pack.unitypackage` | `Assets/UltimateCleanGUIPack/Common/Sounds/` | `Assets/Imported/Audio/UltimateCleanGUI/` |
| POLY | (already in the project, never imported) | n/a | `Assets/polyperfect/Low Poly Animated Animals/Sounds/` |

So `PM/Magic UI/Magic Game UI Button.wav` comes from package path `Assets/Phantom Magic/Magic UI/Magic Game UI Button.wav`
and lands at `Assets/Imported/Audio/PhantomMagic/Magic UI/Magic Game UI Button.wav` (project-relative; on disk under `CoolSchool/`).

## Part A: importer `Tools/import-audio-clips.py` (Python 3, standard library only)
- Usage from the repo root: `python -I Tools/import-audio-clips.py` (optional `--dry-run`).
- Reads the picks file, collects the unique clip specs, groups them by pack, and for each package:
  1. builds the pathname → guid index (a .unitypackage is a gzipped tar of `<guid>/pathname`, `<guid>/asset`, `<guid>/asset.meta`),
  2. writes `asset` to the project path and `asset.meta` next to it as `<file>.meta` (keeps the package GUID).
- Idempotent: skips a clip whose file and .meta already exist with the same size. Never touches files outside `CoolSchool/Assets/Imported/Audio/`.
- Before writing, checks that no existing `.meta` under `CoolSchool/Assets` already uses the same GUID at a different path (reports and skips that clip).
- Prints a report and exits non-zero if any pick is missing from its package. It never guesses another clip.

## Part B: Unity side (namespace and naming as below)
- `SoundCues` (Editor): add `ambience.playground`, `ambience.knights`, `ambience.aliens`, `ambience.classroom`, `ambience.shop`, `ambience.pirates`, `ambience.race` to `All`, plus `public static bool IsAmbience(string cue) => cue.StartsWith("ambience.");`.
  `SoundLibrarySeeder`: ambience entries are `spatial = false`, `cooldown = 0`.
- `Sfx` (runtime): an ambience layer with two looping AudioSources that crossfade, like the music channels.
  - `public static void PlayAmbience(string cue)` and `public static void StopAmbience(float fade)`.
  - `StartMusic(cue)` first calls the ambience for the same area: `music.X` → `ambience.X`, so no activity code changes.
  - If the ambience cue has no clips, the current bed fades out (the classroom is quiet). If the new cue's picked clip is the clip already playing, it keeps playing without a restart.
  - Volume = fade level × entry volume × `SfxVolume` × ambience duck (0.6 while a voice line plays). Ambience is not stopped by `StopMusic`.
- `Editor/Audio/SoundPackPicks.cs` (`CoolSchool.EditorTools.SoundPackPicks`):
  - `public const string PicksPath = "Assets/CoolSchool/Audio/SoundPackPicks.txt";`
  - `public static string Apply(SoundLibrary library)`: for every pick line, it loads the clips (path from the key table), replaces the entry's `clips`, `volume` and `pitchRange`, and applies the import settings below to each clip (reimporting only when a setting changed). Missing clips are reported, not fatal. Cues not in the file are left alone. Returns the report.
  - Menu `CoolSchool/Audio/Apply Sound Pack Picks`.
  - Batch entry `public static void ApplyFromCommandLine()`: CreateOrUpdate the library, Apply, log the report, save, `EditorApplication.Exit(0)` (1 on exception).
  - `SoundSetupBuilder.Setup()` calls `SoundPackPicks.Apply(library)` right after `SoundLibrarySeeder.CreateOrUpdate()`, so a scene rebuild keeps the picks. **The picks file wins** over hand edits for the cues it lists.
- Import settings (Android tablet):
  - Clips under an `Ambient Loops` folder: Streaming, Vorbis quality 0.5, stereo kept, sample rate overridden to 44100.
  - All other clips under `Assets/Imported/Audio/`: Decompress On Load, Vorbis quality 0.6, Force To Mono on, sample rate overridden to 44100 (Phantom Magic is 96 kHz), preload on.
  - These are set in `Apply`, and also by an `AssetPostprocessor` (`ImportedAudioPostprocessor`, `OnPreprocessAudio`) for new files under `Assets/Imported/Audio/`.
