# Knights & the Dragon slice: implementation contract

Shared contract between the **runtime** work (scripts in `Assets/CoolSchool/Scripts`) and the **editor/builder** work (`Assets/CoolSchool/Editor`). Both sides must use exactly the type names, public members and serialized field names below, because the builder wires components through `SerializedObject.FindProperty("<fieldName>")`.

Project: Unity 6.3 (6000.3.24f1), URP 17.3, new Input System only, AI Navigation 2.0, uGUI. Project root `CoolSchool/` inside the repo. Third-party packs live under `Assets/Imported/` (and the dragon under `Assets/Magic Pig Games (Infinity PBR)/`); always resolve assets **by name**, never hardcoded paths.

Player = a 4-year-old on an Android tablet. Rules: no failing, nobody gets hurt, opponents "tumble and giggle" with confetti and get back up, the player is never knocked out (only wobbles), one context button + joystick.

## Game flow

1. Scene starts in **FreePlay** (default activity): classmates wander.
2. Walking into a glowing **ActivityZone** starts an activity: the tag zone starts **TagGame** (90 s round, then back to FreePlay), and the castle zone starts **KnightsGame**.
3. KnightsGame:
   - Subtle **imagination** sparkle; flags and torches appear on the cardboard castle.
   - The player's action button turns into a sword button; the player gets a foam sword and shield, and 3 classmates become knight allies.
   - Wave 1 (2 goblin/troll kids), then wave 2 (3), then the **dragon** walks in.
   - Dragon fight: the dragon telegraphs, breathes *confetti* fire and whips its tail. The player and allies hit it 8 times, then it does a happy idle-break and flies away.
   - Victory: everyone cheers, then after 4 s the game ends and returns to FreePlay with everything restored.

## Existing code (already in repo; read before changing)
- `CoolSchool.Input.FloatingJoystick`, `CoolSchool.Input.ActionButton`
- `CoolSchool.Player.PlayerController` (CharacterController movement, `InputEnabled`, `SpeedMultiplier`, `Jumped` event; serialized `joystick`, `jumpButton`, `cameraTransform`)
- `CoolSchool.Characters.KidAnimator` (on the kid ROOT; Animator in children; params Speed/Jump/Cheer; serialized `animator`)
- `CoolSchool.CameraRig.FollowCamera`
- `CoolSchool.Tag.TagGame`, `TagParticipant`, `TagKidAI`
- Editor: `KidAnimatorBuilder`, `KidsMaterialUpgrader`, `PlaygroundPrototypeBuilder`

Kid hierarchy built by the builder: `<KidRoot>` (KidAnimator, CharacterController or NavMeshAgent+CapsuleCollider, TagParticipant, ...) → child = Synty kid prefab instance with Humanoid Animator.

## Runtime contract (namespace → type → members)

### CoolSchool.Activities
- `public abstract class Activity : MonoBehaviour`
  - `public bool IsRunning { get; private set; }`
  - `public event System.Action<Activity> Ended;`
  - `public void Begin()` (no-op if running; sets IsRunning, calls OnBegin)
  - `public void End()` (no-op if not running; clears IsRunning, calls OnEnd, raises Ended)
  - `protected abstract void OnBegin(); protected abstract void OnEnd();`
- `public class FreePlay : Activity`: empty OnBegin/OnEnd.
- `public class ActivityManager : MonoBehaviour`
  - `public static ActivityManager Instance { get; private set; }`
  - serialized: `Activity defaultActivity`
  - `public Activity Current { get; private set; }`
  - `public void StartActivity(Activity activity)`: ends Current (if any, without triggering a default restart loop), begins the new one, and listens to its `Ended` to start `defaultActivity` again.
  - Starts `defaultActivity` in Start.
- `public class ActivityZone : MonoBehaviour`
  - serialized: `Activity activity`, `float radius` (default 1.5), `GameObject visual`
  - Each frame: when the player (first `PlayerController` found) is within `radius` (flat distance), `ActivityManager.Instance.Current` is the default activity, and the zone is armed → `StartActivity(activity)`.
  - Re-arms only after the player has left the radius. Hides `visual` while its activity runs. Gentle pulse on `visual` (scale bob) while idle.

### Changes to existing runtime code
- `TagGame` becomes `TagGame : Activity` (namespace stays `CoolSchool.Tag`).
  - The Start() logic moves to OnBegin (collect participants once, choose It).
  - New serialized `float roundSeconds = 90f`. On timeout: everyone cheers (`KidAnimator.PlayCheer` on all participants), wait 2 s, then End().
  - OnEnd: clear It, deactivate the `itMarker` GameObject (activate it again in OnBegin), unfreeze and restore player input (`InputEnabled = true`).
  - Must not auto-start any more.
- `TagKidAI`: when the TagGame is missing or not running → just `Wander()` (free play), no chasing or fleeing.
- `PlayerController`: add `public bool JumpEnabled { get; set; } = true;` and only jump when it is true.
- `ActionButton`: add serialized `UnityEngine.UI.Image icon`; `public void SetIcon(Sprite sprite)` (remembers the original sprite the first time); `public void ResetIcon()`.
- `KidAnimator`: add `PlayAttack()` (trigger "Attack"), `SetBlocking(bool)` (bool "Blocking"), `PlayHit()` (trigger "Hit"), `PlayTumble()` (trigger "Tumble"), `PlayLaugh()` (trigger "Laugh").
  - Every call checks the parameter exists, e.g. a cached set of parameter name hashes, so a missing parameter never throws or warns.
  - Upper-body layer: if the controller has a layer named "UpperBody", KidAnimator drives its weight smoothly to 1 while blocking or for 0.7 s after PlayAttack, otherwise to 0.

### CoolSchool.Battle
- `public enum BattleTeam { Heroes, Opponents }`
- `public class Hittable : MonoBehaviour`
  - serialized: `BattleTeam team`, `int hitsToDefeat` (3), `bool canBeDefeated` (true), `float stunSeconds` (0.6), `float hitRadius` (0.3; the dragon uses ~1.8), `Transform aimPoint` (optional)
  - props:
    - `Team`, `HitRadius`, `HitsTaken`
    - `IsStunned` (time-based)
    - `IsDefeated`
    - `IsBlocking { get; set; }`
    - `IsAlive => isActiveAndEnabled && !IsDefeated`
    - `Vector3 AimPosition` (aimPoint or transform.position + up*0.8)
  - events: `public event System.Action<Hittable> Hit; public event System.Action<Hittable> Blocked; public event System.Action<Hittable> Defeated;`
  - `public bool TakeHit(Vector3 attackerPosition)`. Returns false if not alive or stunned.
    - **Blocked:** if IsBlocking and the attacker is in front (dot of forward and flat direction to attacker > 0.2), call `BattleEffects.BlockSpark(AimPosition)`, raise Blocked and return false.
    - **Hit:** otherwise HitsTaken++, stun for stunSeconds, call `BattleEffects.Confetti(AimPosition)` and raise Hit.
    - **Defeated:** if canBeDefeated and HitsTaken ≥ hitsToDefeat, also set IsDefeated and raise Defeated.
    - Returns true.
  - `public void ResetState()` clears hits, stun, defeat and blocking.
  - Static registry: `public static IReadOnlyList<Hittable> All` (add in OnEnable, remove in OnDisable).
  - Hittable never plays animations itself; owners subscribe to the events.
- `public class SwordFighter : MonoBehaviour` (requires Hittable on the same GameObject for its own team)
  - serialized: `float reach` (1.3), `float aimRadius` (3), `float attackCooldown` (0.45), `float hitDelay` (0.25), `float turnSpeed` (900)
  - `public bool CanAttack`
  - `public bool Attack()`. Auto-aim: nearest alive Hittable of the other team within `aimRadius + target.HitRadius`. Snap-turn toward it (smooth over the hitDelay is fine).
    - Then `KidAnimator.PlayAttack()` if present.
    - After hitDelay, every alive opposing Hittable within `reach + HitRadius` and a 120° front cone gets `TakeHit(transform.position)`.
    - Also calls `BattleEffects.Swoosh(transform.position + forward*0.8 + up*0.9)`.
    - Returns false while on cooldown or stunned.
  - `public void SetBlocking(bool)` sets own Hittable.IsBlocking and calls `KidAnimator.SetBlocking`.
- `public class PlayerBattleInput : MonoBehaviour` (on the Player root; **disabled by default**, enabled by KnightsGame)
  - serialized: `CoolSchool.Input.ActionButton button`, `float holdToBlockSeconds` (0.25)
  - Tap = Attack; hold longer than holdToBlockSeconds = SetBlocking(true) until release. Keyboard Space / Gamepad buttonSouth behave the same (new Input System device API is fine).
  - While own Hittable.IsStunned: `PlayerController.InputEnabled = false`, otherwise true.
  - On Hit: `KidAnimator.PlayHit()`. OnDisable: stop blocking and restore InputEnabled.
- `public class WeaponHolder : MonoBehaviour` (on kid roots)
  - serialized: `GameObject swordPrefab`, `GameObject shieldPrefab`, `Vector3 swordLocalPosition`, `Vector3 swordLocalEuler`, `Vector3 shieldLocalPosition`, `Vector3 shieldLocalEuler`, `float weaponScale` (1)
  - `public void ShowWeapons(bool show)`. Lazily instantiates the prefabs under `Animator.GetBoneTransform(HumanBodyBones.RightHand)` and `LeftLowerArm` (shield), using the offsets. Toggles active. Removes colliders from the instances.
- `public class BattleOpponentAI : MonoBehaviour` (NavMeshAgent, Hittable [Opponents], SwordFighter, KidAnimator)
  - serialized: `float moveSpeed` (1.6), `float windUpSeconds` (0.9), `float recoverSeconds` (2.5), `float preferPlayerChance` (0.6), `GameObject telegraph` (optional "!" object shown during wind-up)
  - `public void Activate(Vector3 spawnPosition, Transform retreatPoint)`: SetActive(true), Warp, ResetState, start fighting.
  - Loop: Approach nearest alive hero (player preferred) → stop at ~1.4 m → WindUp (stand still, face the target, telegraph on, short crouch/scale pulse) → `SwordFighter.Attack()` → Recover (step back a little).
  - On Hit: `PlayHit()` and pause. On Defeated: `PlayTumble()`, then `PlayLaugh()` after ~1.5 s, then run to retreatPoint and `gameObject.SetActive(false)`.
  - `public bool IsOut` (defeated or inactive).
- `public class KnightAllyAI : MonoBehaviour` (NavMeshAgent, Hittable [Heroes, canBeDefeated=false], SwordFighter, KidAnimator; **disabled by default**)
  - Follows the player at 2–4 m. If an alive opponent is within 5 m, it goes to it and attacks every 1.5–2.5 s. On Hit: PlayHit.
  - Speed 2.6.
- `public class DragonBoss : MonoBehaviour` (Animator with the generic "Dragon" controller below; Hittable [Opponents, hitsToDefeat 8, hitRadius 1.8, stunSeconds 0.8])
  - serialized: `Animator animator`, `Transform fireOrigin`, `ParticleSystem fireConfetti`, `GameObject fireWarning`, `float walkSpeed` (1.5), `float fireRange` (5), `float fireAngle` (50), `float telegraphSeconds` (1.2), `float fireSeconds` (1.5), `float attackInterval` (3.5)
  - `public void Enter(Vector3 from, Vector3 arenaCenter)`: activate at `from`, walk (transform-based, no NavMesh) to arenaCenter facing it, then fight.
  - Fight loop:
    - Turn toward the player.
    - If the player is behind/beside and close: trigger "TailWhip"; heroes within 3 m behind get TakeHit.
    - Otherwise breathe fire: fireWarning on for telegraphSeconds, then trigger "BreatheFire", fireConfetti plays for fireSeconds. During that time heroes within fireRange and fireAngle get TakeHit once per breath (blocking protects).
  - On Hit: trigger "GotHit" (at most once per 0.6 s).
  - On Defeated: stop attacking, trigger "Happy", big confetti, then after 2 s set bool "Flying" true and move up and away for 4 s, then deactivate and raise `public event System.Action FlewAway`.
  - `public void ResetDragon()`.
  - Animator params: float "Speed", triggers "BreatheFire", "TailWhip", "GotHit", "Happy", bool "Flying".
- `public class BattleEffects : MonoBehaviour` (one per scene)
  - serialized: `ParticleSystem confettiPrefab`, `ParticleSystem blockSparkPrefab`, `ParticleSystem sparklePrefab`, `ParticleSystem swooshPrefab`
  - Static: `Confetti(Vector3)`, `BlockSpark(Vector3)`, `Sparkle(Vector3)`, `Swoosh(Vector3)`. Each instantiates, plays, and destroys after the duration; it is a no-op if no instance or prefab exists.
- `public class ImaginationEffect : MonoBehaviour`
  - serialized: `CanvasGroup overlay` (full-screen soft gold image), `GameObject[] imaginationProps`, `float flashSeconds` (1.0), `float maxOverlayAlpha` (0.35)
  - `public void Play(bool on)`: overlay alpha 0 → max → 0 over flashSeconds (unscaled time), `BattleEffects.Sparkle` around the player, and `imaginationProps` toggled to `on`.
- `public class KnightsGame : CoolSchool.Activities.Activity` (namespace CoolSchool.Battle)
  - serialized fields:
    - `CoolSchool.Player.PlayerController player`
    - `PlayerBattleInput playerInput`
    - `CoolSchool.Input.ActionButton actionButton`
    - `Sprite swordIcon`
    - `ImaginationEffect imagination`
    - `Transform[] opponentSpawnPoints`
    - `Transform retreatPoint`
    - `BattleOpponentAI[] opponents`
    - `int[] waveSizes` ({2, 3})
    - `DragonBoss dragon`
    - `Transform dragonEntry`
    - `Transform arenaCenter`
    - `KnightAllyAI[] allies`
    - `WeaponHolder[] weaponHolders`
    - `float victorySeconds` (4)
    - UnityEvents `onWaveStarted` (`UnityEvent<int>`), `onDragonArrived`, `onVictory`
  - OnBegin:
    - Player and HUD: `player.JumpEnabled = false`, `playerInput.enabled = true`, `actionButton.SetIcon(swordIcon)`, every weaponHolder ShowWeapons(true), `imagination.Play(true)`.
    - Allies: disable their `TagKidAI` and enable KnightAllyAI.
    - Opponents and dragon: all opponents inactive, dragon reset/inactive.
    - Start the flow coroutine: waves (reuse the opponent pool; wait until all active opponents `IsOut`), then the dragon `Enter(dragonEntry.position, arenaCenter.position)`, then wait for FlewAway, then every hero cheers and onVictory, then wait victorySeconds, then End().
  - OnEnd: stop the coroutine and undo everything above:
    - Restore the player: `JumpEnabled = true`, playerInput disabled, `ResetIcon`, weapons hidden.
    - Restore the allies: KnightAllyAI disabled, TagKidAI enabled; call ResetState on all Hittables involved.
    - Reset the opponents and dragon: opponents inactive, dragon reset/inactive.
    - Turn the imagination effect off.

## Editor contract (what the builder creates)
- **Kid.controller** (`KidAnimatorBuilder`). Keep the existing params (Speed, Jump, Cheer) and add triggers Attack, Hit, Tumble, Laugh, BlockHit, bool Blocking and int AttackVariant.
  - **Base layer:**
    - "Hit" → `HumanM@Stun01`
    - "Tumble" → `HumanM@Knockdown01 - Fall` → `HumanM@Knockdown01 - Ground` (about 1 s) → `HumanM@Knockdown01 - StandUp` → Locomotion
    - "Laugh" → `HumanM@Laugh01`
    - Any-state transitions, no self-transition.
  - **Layer "UpperBody":**
    - Avatar mask `Human Body Upper Mask` (Kevin Iglesias, find by name), override, weight 0 (KidAnimator drives it).
    - States: "Empty" (default, no motion, writeDefaultValues false); Attack trigger + AttackVariant 0/1/2 → `Armed-Shield-Attack1/2/3` → Empty; Blocking true → `Armed-Shield-Block` (loop) → Empty when false; BlockHit trigger while blocking → `Armed-Shield-Block-GetHit1` → back to Block.
    - Sword/shield clips come from the ExplosiveLLC *RPG Character Mecanim Animation Pack*; clip name = FBX file name without the `RPG-Character@` prefix and extension. If missing, skip those states and report it in the dialog. (Replaces the earlier Nephilite plan.)
  - **Opponents** (club goblins/trolls) use an `AnimatorOverrideController` `KidClub.overrideController` based on Kid.controller that swaps the three attack clips for `Mace-Attack-R1/R2/R3`.
  - Clips are found by FBX file name (first non-`__preview__` AnimationClip in that file), skipping `[RM]`/RootMotion variants.
- **KidAnimator additions:** `PlayAttack()` sets AttackVariant to a random 0–2 (if the parameter exists) before the trigger; new `PlayBlockHit()` (trigger "BlockHit"). `PlayerBattleInput` and `KnightAllyAI` call `PlayBlockHit()` on their Hittable's `Blocked` event.
- **Dragon.controller** (generic). States: Idle (default), Walk (Speed > 0.1), BreatheFire, TailWhip, GotHit, Happy (Idle Break), and Takeoff → Fly while Flying.
  - Clips by name from `Dragon_LP.fbx`: "Idle", "Walk", "Breathe Fire", "Tail Whip L", "Got Hit 1", "Idle Break", "Idle Takeoff", "Fly".
- **Particle prefabs** in `Assets/CoolSchool/Prefabs/Effects/`:
  - Confetti: burst of about 40 small multi-coloured quads with gravity.
  - BlockSpark: white star burst.
  - Sparkle: gold twinkles.
  - Swoosh: a short white arc or puff.
  - FireConfetti: a looping forward cone of orange/yellow/red confetti.
  - All use a URP `Universal Render Pipeline/Particles/Unlit` material.
- **Playground builder changes.** The playground area becomes a rectangle extended toward +X (east) by an arena of about 26 m depth; the castle stands at the far east end facing west, and the dragon enters from behind it. Grass, fence and trees adapt, and trees stay out of the arena.
  - Activities:
    - An `ActivityManager` with a `FreePlay` default.
    - `TagGame` with its zone at (0, 0, 4).
    - `KnightsArenaBuilder.Build(...)` for the castle.
  - Arena contents:
    - A cardboard castle (`SM_Prop_Cardboard_Castle_Piece_01..06`) as a wall with a gate at the far end, facing the playground.
    - Imagination props: `SM_Prop_Flag_01/02` on top and `SM_Prop_Torch_01` at the gate, inactive by default.
    - Opponent spawn points inside the gate, a retreat point behind the castle, a dragon entry point behind the castle, and an arena centre.
    - The castle ActivityZone at the arena front, on the castle's west (playground) side.
    - Opponent pool: `SM_Chr_Kid_Goblin_01`, `SM_Chr_Kid_Goblin_02`, `SM_Chr_Kid_Troll_01`, `SM_Chr_Kid_Goblin_01`. They carry `SM_Wep_Club_Inflatable_01` / `SM_Wep_Goblin_Club_01` and start inactive.
    - Dragon: `Dragon_LP` prefab, scaled to about 3.5 m tall, with the Dragon.controller, inactive.
    - The first 3 classmates get Hittable (Heroes, not defeatable), SwordFighter, KnightAllyAI (disabled) and WeaponHolder (`SM_Wep_Sword_Wood_01` + `SM_Wep_Shield_02`).
    - The player gets Hittable, SwordFighter, PlayerBattleInput (disabled) and WeaponHolder (`SM_Wep_Sword_Foam_01` + `SM_Wep_Shield_01`).
  - Scene objects: BattleEffects, plus an ImaginationEffect overlay on the TouchUI canvas (raycastTarget off).
  - Sword icon: a sprite whose name contains "Sword" (prefer ModularGameUIKit), if found.
