using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the shared Kid animator controller from Kevin Iglesias' Human Animations (male) clips:
    /// idle/walk/run/sprint blended on "Speed", plus "Jump", "Cheer", "Hit", "Tumble" and "Laugh" triggers.
    /// An "UpperBody" layer (Kevin Iglesias upper-body mask) plays the sword-and-shield moves from the
    /// ExplosiveLLC RPG Character Mecanim Animation Pack, when imported: three attacks picked by the
    /// "AttackVariant" int on the "Attack" trigger, the shield guard ("Blocking" bool) and a "BlockHit" bump.
    /// </summary>
    public static class KidAnimatorBuilder
    {
        public const string IdleClip = "HumanM@Idle01";
        public const string WalkClip = "HumanM@Walk01_Forward";
        public const string RunClip = "HumanM@Run01_Forward";
        public const string SprintClip = "HumanM@Sprint01_Forward";
        public const string JumpClip = "HumanM@Jump01";
        public const string CheerClip = "HumanM@Cheer01";
        public const string HitClip = "HumanM@Stun01";
        public const string FallClip = "HumanM@Knockdown01 - Fall";
        public const string GroundClip = "HumanM@Knockdown01 - Ground";
        public const string StandUpClip = "HumanM@Knockdown01 - StandUp";
        public const string LaughClip = "HumanM@Laugh01";

        /// <summary>The pack the sword, shield and mace clips come from (used in messages).</summary>
        public const string CombatPackName = "RPG Character Mecanim Animation Pack";
        public const string ShieldBlockClip = "Armed-Shield-Block";
        public const string ShieldBlockHitClip = "Armed-Shield-Block-GetHit1";

        /// <summary>The three sword-and-shield attacks, picked by AttackVariant 0, 1, 2.</summary>
        public static readonly string[] ShieldAttackClips =
        {
            "Armed-Shield-Attack1", "Armed-Shield-Attack2", "Armed-Shield-Attack3",
        };

        /// <summary>One-handed mace attacks swapped in for the opponents' club swings.</summary>
        public static readonly string[] MaceAttackClips =
        {
            "Mace-Attack-R1", "Mace-Attack-R2", "Mace-Attack-R3",
        };

        /// <summary>The ray gun shot of the superhero, and the arm swing the aliens throw their slime with (both RPG pack).</summary>
        public const string ShootClip = "Pistol-Attack-R1";
        public const string ThrowClip = "Unarmed-Attack-R1";

        /// <summary>
        /// The dances, picked by DanceVariant 0, 1, 2: file names in the Gestures MoCap Collection.
        /// That pack also has rude gestures. These three are the only taunts the game may ever use.
        /// </summary>
        public static readonly string[] DanceClips =
        {
            "Chicken_Dance_Taunt", "Dance_Taunt", "Robo_Taunt",
        };

        public const string UpperMaskName = "Human Body Upper Mask";
        public const string UpperBodyLayerName = "UpperBody";

        /// <summary>
        /// Returns null when the core locomotion clips are missing (Human Animations not imported).
        /// Optional clips that can't be found are added to missingClips and their states are skipped.
        /// </summary>
        public static AnimatorController CreateOrReplace(string path, List<string> missingClips)
        {
            Dictionary<string, AnimationClip> clips = FindHumanAnimationClips();

            AnimationClip Get(string name)
            {
                if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
                missingClips.Add(name);
                return null;
            }

            AnimationClip idle = Get(IdleClip);
            AnimationClip walk = Get(WalkClip);
            AnimationClip run = Get(RunClip);
            AnimationClip sprint = Get(SprintClip);
            AnimationClip jump = Get(JumpClip);
            AnimationClip cheer = Get(CheerClip);
            AnimationClip hit = Get(HitClip);
            AnimationClip fall = Get(FallClip);
            AnimationClip ground = Get(GroundClip);
            AnimationClip standUp = Get(StandUpClip);
            AnimationClip laugh = Get(LaughClip);

            if (idle == null || walk == null || run == null) return null;

            var attacks = new AnimationClip[ShieldAttackClips.Length];
            for (int i = 0; i < attacks.Length; i++)
            {
                attacks[i] = FindCombatClip(ShieldAttackClips[i]);
                if (attacks[i] == null) missingClips.Add(ShieldAttackClips[i]);
            }

            AnimationClip block = FindCombatClip(ShieldBlockClip);
            AnimationClip blockHit = FindCombatClip(ShieldBlockHitClip);
            if (block == null) missingClips.Add(ShieldBlockClip);
            if (blockHit == null) missingClips.Add(ShieldBlockHitClip);

            AnimationClip shoot = FindCombatClip(ShootClip);
            AnimationClip throwArm = FindCombatClip(ThrowClip);
            if (shoot == null) missingClips.Add(ShootClip);
            if (throwArm == null) missingClips.Add(ThrowClip);

            var dances = new AnimationClip[DanceClips.Length];
            for (int i = 0; i < dances.Length; i++)
            {
                dances[i] = FindGestureClip(DanceClips[i]);
                if (dances[i] == null) missingClips.Add(DanceClips[i]);
            }

            AvatarMask upperMask = FindAvatarMask(UpperMaskName);
            bool wantUpperBody = attacks.Any(a => a != null) || block != null || shoot != null || throwArm != null;
            if (wantUpperBody && upperMask == null) missingClips.Add(UpperMaskName);

            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Tumble", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Laugh", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("BlockHit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dance", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dancing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("DanceVariant", AnimatorControllerParameterType.Int);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            // Thresholds are in metres per second.
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.5f);
            tree.AddChild(run, 4f);
            if (sprint != null) tree.AddChild(sprint, 6f);
            machine.defaultState = locomotion;

            if (jump != null) AddOneShot(machine, locomotion, "Jump", jump, exitTime: 0.8f);
            if (cheer != null) AddOneShot(machine, locomotion, "Cheer", cheer, exitTime: 0.9f);
            if (hit != null) AddOneShot(machine, locomotion, "Hit", hit, exitTime: 0.85f);
            if (laugh != null) AddOneShot(machine, locomotion, "Laugh", laugh, exitTime: 0.9f);
            AddTumble(machine, locomotion, fall, ground, standUp);
            AddDances(machine, locomotion, dances);

            if (wantUpperBody && upperMask != null)
                AddUpperBodyLayer(controller, upperMask, attacks, block, blockHit, shoot, throwArm);

            AssetDatabase.SaveAssets();
            return controller;
        }

        static void AddOneShot(AnimatorStateMachine machine, AnimatorState returnTo, string trigger, AnimationClip clip, float exitTime)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.canTransitionToSelf = false;
            enter.hasExitTime = false;
            enter.duration = 0.1f;

            AnimatorStateTransition back = state.AddTransition(returnTo);
            back.hasExitTime = true;
            back.exitTime = exitTime;
            back.duration = 0.2f;
        }

        /// <summary>Tumble: fall, lie on the ground for about a second, stand up, back to locomotion.</summary>
        static void AddTumble(AnimatorStateMachine machine, AnimatorState returnTo, AnimationClip fall, AnimationClip ground, AnimationClip standUp)
        {
            var chain = new List<AnimatorState>();
            var exitTimes = new List<float>();

            AnimatorState Add(string name, AnimationClip clip, float exitTime)
            {
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                chain.Add(state);
                exitTimes.Add(exitTime);
                return state;
            }

            if (fall != null) Add("Tumble", fall, 0.9f);
            if (ground != null) Add("TumbleGround", ground, Mathf.Clamp01(1f / Mathf.Max(0.1f, ground.length)));
            if (standUp != null) Add("TumbleStandUp", standUp, 0.9f);
            if (chain.Count == 0) return;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(chain[0]);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Tumble");
            enter.canTransitionToSelf = false;
            enter.hasExitTime = false;
            enter.duration = 0.1f;

            for (int i = 0; i < chain.Count; i++)
            {
                AnimatorState next = i + 1 < chain.Count ? chain[i + 1] : returnTo;
                AnimatorStateTransition step = chain[i].AddTransition(next);
                step.hasExitTime = true;
                step.exitTime = exitTimes[i];
                step.duration = i + 1 < chain.Count ? 0.1f : 0.2f;
            }
        }

        /// <summary>
        /// Adds the "UpperBody" layer: masked to the upper body, override blending, weight 0
        /// (KidAnimator raises it while attacking or blocking). The default state is an empty one,
        /// so the base layer's legs and idle show through.
        /// Attack: Empty goes to one of three swings when the Attack trigger fires, chosen by AttackVariant 0/1/2.
        /// Block: Blocking true raises the shield; the BlockHit trigger plays a bump and returns to the guard.
        /// </summary>
        static void AddUpperBodyLayer(AnimatorController controller, AvatarMask mask, AnimationClip[] attacks,
            AnimationClip block, AnimationClip blockHit, AnimationClip shoot, AnimationClip throwArm)
        {
            controller.AddLayer(UpperBodyLayerName);

            // controller.layers returns copies: change the copy and assign it back.
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].avatarMask = mask;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 0f;
            controller.layers = layers;

            AnimatorStateMachine machine = controller.layers[index].stateMachine;

            AnimatorState empty = machine.AddState("Empty");
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] == null) continue;

                AnimatorState swing = machine.AddState("Attack" + (i + 1));
                swing.motion = attacks[i];
                swing.writeDefaultValues = false;

                AnimatorStateTransition enter = empty.AddTransition(swing);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "AttackVariant");
                enter.hasExitTime = false;
                enter.duration = 0.05f;

                AnimatorStateTransition back = swing.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }

            AddArmOneShot(machine, empty, "Shoot", shoot);
            AddArmOneShot(machine, empty, "Throw", throwArm);

            if (block == null) return;

            AnimatorState guard = machine.AddState("ShieldBlock");
            guard.motion = block;
            guard.writeDefaultValues = false;

            AnimatorStateTransition raise = empty.AddTransition(guard);
            raise.AddCondition(AnimatorConditionMode.If, 0f, "Blocking");
            raise.hasExitTime = false;
            raise.duration = 0.1f;

            AnimatorStateTransition lower = guard.AddTransition(empty);
            lower.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");
            lower.hasExitTime = false;
            lower.duration = 0.1f;

            if (blockHit == null) return;

            AnimatorState bump = machine.AddState("ShieldBlockHit");
            bump.motion = blockHit;
            bump.writeDefaultValues = false;

            AnimatorStateTransition take = guard.AddTransition(bump);
            take.AddCondition(AnimatorConditionMode.If, 0f, "BlockHit");
            take.hasExitTime = false;
            take.duration = 0.05f;

            // Lowering the shield mid-bump wins over returning to the guard.
            AnimatorStateTransition drop = bump.AddTransition(empty);
            drop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");
            drop.hasExitTime = false;
            drop.duration = 0.1f;

            AnimatorStateTransition recover = bump.AddTransition(guard);
            recover.hasExitTime = true;
            recover.exitTime = 0.9f;
            recover.duration = 0.1f;
        }

        /// <summary>An upper-body one-shot (the ray gun shot, the slime throw): the trigger plays the clip, then it goes back to Empty.</summary>
        static void AddArmOneShot(AnimatorStateMachine machine, AnimatorState empty, string trigger, AnimationClip clip)
        {
            if (clip == null) return;

            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;
            state.writeDefaultValues = false;

            AnimatorStateTransition enter = empty.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;

            AnimatorStateTransition back = state.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.1f;
        }

        /// <summary>
        /// Dance states in the base layer. The Dance trigger plus DanceVariant 0, 1, 2 starts one from anywhere;
        /// it plays on (looping) until the Dancing bool is cleared, then blends back to locomotion.
        /// </summary>
        static void AddDances(AnimatorStateMachine machine, AnimatorState returnTo, AnimationClip[] dances)
        {
            for (int i = 0; i < dances.Length; i++)
            {
                if (dances[i] == null) continue;

                AnimatorState dance = machine.AddState("Dance" + (i + 1));
                dance.motion = dances[i];

                AnimatorStateTransition enter = machine.AddAnyStateTransition(dance);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Dance");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "DanceVariant");
                enter.canTransitionToSelf = false;
                enter.hasExitTime = false;
                enter.duration = 0.15f;

                AnimatorStateTransition stop = dance.AddTransition(returnTo);
                stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dancing");
                stop.hasExitTime = false;
                stop.duration = 0.2f;

                // The mocap clips are set to loop. If one is not, restart it from its own end so the dance never freezes.
                if (!dances[i].isLooping)
                {
                    AnimatorStateTransition again = dance.AddTransition(dance);
                    again.hasExitTime = true;
                    again.exitTime = 1f;
                    again.duration = 0.1f;
                }
            }
        }

        /// <summary>
        /// A clip from the Gestures MoCap Collection: the first clip of the FBX with this file name.
        /// Only the dance clips named in DanceClips may be asked for; the pack has rude gestures that must never be used.
        /// </summary>
        static AnimationClip FindGestureClip(string fileName)
        {
            if (Array.IndexOf(DanceClips, fileName) < 0)
            {
                Debug.LogError($"CoolSchool: '{fileName}' is not an approved gesture clip and was not loaded.");
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsRootMotionOrNotFbx(path) || Path.GetFileNameWithoutExtension(path) != fileName) continue;

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// Creates an override of the kid controller for the club-carrying opponents: the three shield attacks
        /// are swapped for the Mace-Attack-R1/R2/R3 clips. Returns null if the base controller has no attack
        /// states or none of the mace clips exist. Missing mace clips are added to missingClips.
        /// </summary>
        public static AnimatorOverrideController CreateClubOverride(string path, AnimatorController baseController, List<string> missingClips)
        {
            if (baseController == null) return null;

            var overrideController = new AnimatorOverrideController(baseController) { name = Path.GetFileNameWithoutExtension(path) };
            int swapped = 0;
            for (int i = 0; i < ShieldAttackClips.Length; i++)
            {
                AnimationClip shieldClip = FindCombatClip(ShieldAttackClips[i]);
                AnimationClip maceClip = FindCombatClip(MaceAttackClips[i]);
                if (maceClip == null) missingClips.Add(MaceAttackClips[i]);
                if (shieldClip == null || maceClip == null) continue;

                overrideController[shieldClip] = maceClip;
                swapped++;
            }

            if (swapped == 0) return null;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(overrideController, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        }

        /// <summary>
        /// Human Animations clips keyed by both FBX file name and clip name (they are normally equal).
        /// Root-motion variants are skipped; the game moves kids itself.
        /// </summary>
        static Dictionary<string, AnimationClip> FindHumanAnimationClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("HumanM@ t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsRootMotionOrNotFbx(path)) continue;

                AnimationClip first = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        first ??= clip;
                        result.TryAdd(clip.name, clip);
                    }
                }

                if (first != null) result.TryAdd(Path.GetFileNameWithoutExtension(path), first);
            }
            return result;
        }

        /// <summary>
        /// A clip of the RPG Character Mecanim Animation Pack by clip name. The FBX is named
        /// "RPG-Character@" + clip name (extension .fbx or .FBX); null if it can't be found.
        /// </summary>
        static AnimationClip FindCombatClip(string clipName)
        {
            foreach (string guid in AssetDatabase.FindAssets(clipName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("[RM]") || path.Contains("/RootMotion/")) continue;
                if (!Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase)) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName != clipName && fileName != "RPG-Character@" + clipName) continue;

                AnimationClip first = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                    if (clip.name == clipName) return clip;
                    first ??= clip;
                }

                if (first != null) return first;
            }
            return null;
        }

        /// <summary>True for clip names that belong to the RPG Character Mecanim Animation Pack.</summary>
        public static bool IsCombatPackClip(string clipName)
        {
            return clipName.StartsWith("Armed-Shield-") || clipName.StartsWith("Mace-")
                || clipName == ShootClip || clipName == ThrowClip;
        }

        static AvatarMask FindAvatarMask(string maskName)
        {
            foreach (string guid in AssetDatabase.FindAssets(maskName + " t:AvatarMask"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != maskName) continue;

                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
                if (mask != null) return mask;
            }
            return null;
        }

        static bool IsRootMotionOrNotFbx(string path)
        {
            return !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                || path.Contains("[RM]") || path.Contains("/RootMotion/");
        }
    }
}
