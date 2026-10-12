# Quit button contract (back button for every mini game)

Status: spec 2026-10-12. Decisions by the user: **tap, then confirm** with pictures; icon is a **back arrow**.

## Goal
During any activity other than free play (tag, knights, numbers/letters lesson, alien landing, ice cream shop,
pirate adventure, and any future `Activity`) a round back-arrow button sits in the top-left corner. Tapping it
freezes the game, the teacher asks "Wil je stoppen?", and two big picture buttons appear: a green check (stop)
and a red cross (keep playing). Check ends the current activity, so the `ActivityManager` returns to free play.
Cross unfreezes and the game continues exactly where it was.

It is generic: it works through `ActivityManager.Instance.Current` and `Activity.End()`. No per-activity code
except fixing any `OnEnd` that does not survive being called mid-flow (see Audit).

## Runtime (namespace `CoolSchool.Activities`, folder `Scripts/Activities`)

### `QuitButton : MonoBehaviour, IPointerClickHandler`
- Serialized: `QuitPrompt prompt`, `GameObject visual` (the child that is shown/hidden; the root stays active so
  `Update` keeps running).
- `Update`: `visual` is active only when `ActivityManager.Instance != null`, `Current != null`,
  `Current != DefaultActivity`, `Current.IsRunning`, `!ParentGate.InputBlocked`, and no `PresentPopup` is open.
  Gentle scale pop when it appears (unscaled time) is nice but optional.
- `OnPointerClick`: if visible, `prompt.Open()` and `Sfx.Play("ui.tap", ...)` if such a cue exists (check
  `Editor/Audio/SoundCues.cs`; use an existing UI cue, do not invent one if a fitting one exists, otherwise add
  `ui.back` to the cue list).
- Android back / Escape (new Input System `Keyboard.current.escapeKey.wasPressedThisFrame`): when the button is
  visible and nothing is gated, open the prompt. Make sure this does not clash with `ParentPanel`'s own Escape
  handling (it only reacts while the parent panel is open).

### `QuitPrompt : MonoBehaviour`
- Serialized: `GameObject root` (the overlay, inactive when closed), `Button stopButton`, `Button continueButton`,
  `RectTransform window` (for a small pop-in animation, unscaled time), `AudioSource voiceSource`.
- `public bool IsOpen`.
- `Open()`: if already open or `ParentGate.IsOpen`, return. Save `Time.timeScale` and `AudioListener.pause`,
  set `Time.timeScale = 0`, `AudioListener.pause = true` (same freeze as `ParentPanel`), set
  `ParentGate.QuitPromptOpen = true`, show `root`, play the question line.
- The question line: look up `quit.ask` in the `LineTable` (same table `VoicePlayer` uses; expose what is needed
  from `VoicePlayer`/`LineTable` with a small public method, e.g. `VoicePlayer.ClipFor(string group, out string text)`),
  and play its clip on `voiceSource`, which has `ignoreListenerPause = true` and the teacher pitch. The voice line
  that was already playing stays paused under the listener pause and resumes on Continue. Missing clip: log the
  text like `VoicePlayer` does.
- `Continue()` (cross, or Escape while open): stop `voiceSource`, restore time scale and audio pause, clear
  `QuitPromptOpen`, hide `root`.
- `Stop()` (check): restore exactly as `Continue()` first, then `ActivityManager.Instance.Current?.End()` (only if it
  is not the default activity), then `VoicePlayer.Say("quit.bye", interrupt: true)` so leftover queued lines from the
  game are cleared.
- Both buttons ignore taps for the first ~0.35 s after opening (unscaled), so the tap that opened it cannot hit them.
- `OnDisable`: if open, restore (never leave the game frozen).

### `ParentGate` (in `Scripts/Parent/ParentGate.cs`)
- Add `public static bool QuitPromptOpen { get; internal set; }` and
  `public static bool InputBlocked => IsOpen || QuitPromptOpen;`. Reset both in `ResetStatics`.
- Gameplay scripts that poll the pointer/keyboard and currently check `ParentGate.IsOpen` switch to
  `ParentGate.InputBlocked` (so a tap on the prompt does not also scoop ice cream or fire the cannon).
  `ParentPanel` and `ParentAreaButton` keep using `IsOpen` for their own state, but `ParentAreaButton` also ignores
  presses while `QuitPromptOpen`.

## Builder (`Editor/QuitButtonBuilder.cs`, namespace `CoolSchool.EditorTools`)
- `public static void Build()`; called from `PlaygroundPrototypeBuilder.RunBuild()` right after
  `ParentAreaBuilder.Build()`. Re-running replaces what was built before (find by name and destroy).
- Button `QuitButton` on the `TouchUI` canvas, anchored top-left, **right of the parent gear** (gear is at inset 110,
  hit size 150): centre at about (290, -110), hit area 160, visible circle 130 (white circle sprite with a soft dark
  outline/backdrop like the gear, but fully opaque and friendly-coloured, e.g. warm orange), `Arrow-Left` icon
  (`Icon-Symbols/Arrows/Arrow-Left`, white) ~80. It must **not** be in `LessonUI.gameplayHud` (that list is found by
  name: `JoystickZone`, `ActionButton`), so it stays visible in lessons. Keep it clear of the star jar / golden sword
  badge (top-right) and of the lesson cards.
- Prompt `QuitPrompt` on its own Screen Space Overlay canvas, `CanvasScaler` like the touch UI (1920x1080, match
  height), sorting order **900** (above the touch UI and the present popup, below the parent panel's 1000), with a
  `GraphicRaycaster`. Full-screen dim (black, alpha ~0.55, raycast target so nothing behind it gets taps), a rounded
  window (`Background` shape from `ModularGameUIKit/Common/Sprites/Shapes/`) with the back arrow shown big at the top,
  and two round buttons ~260 px: **left = red with `X-Delete-Close-Error`** (continue), **right = green with
  `Checkmark`** (stop). No text anywhere (he cannot read).
- Reuse helpers/patterns from `ParentAreaBuilder` (`LoadShape`, `ActivityBeaconBuilder.FindSprite`, `NewRect`,
  `SetAnchored`, `AddImage`) — make shared ones `internal` rather than copying, if that is simple.
- Wire serialized fields with `PlaygroundPrototypeBuilder.SetObject`.

## Voice lines (`Editor/Voice/VoiceLineSeeder.cs`)
- `quit.ask.1` Teacher: "Wil je stoppen met dit spel?" / "Do you want to stop this game?"
- `quit.bye.1` Teacher: "Oké! Dan gaan we weer lekker buiten spelen." / "Okay! Let's go and play outside again."
- `quit.bye.2` Teacher: "Prima! Je mag altijd weer terugkomen." / "Fine! You can always come back."

## Audit: every activity must survive `End()` at any moment
For `TagGame`, `KnightsGame`, `ClassroomLesson` (+ `NumbersLesson`, `LettersLesson`), `AlienLanding`,
`IceCreamShop`, `PirateAdventure`: read the whole flow and check that ending mid-flow leaves no stuck state:
coroutines on *other* objects (ship sailing, skeleton boat, dragon, customers, golden sword reward, rocket),
player input/jump/position/parenting (e.g. still parented to the ship or seated), camera rigs, HUD hidden,
costume/weapons/props still shown, `Time.timeScale`, music (free play restarts `music.playground`), half-awarded
progress or level changes (an early quit must not count as a level-up; stars already earned stay). Fix only real
problems, minimally, inside the activity's `OnEnd`/reset helpers. Report each activity's verdict.
