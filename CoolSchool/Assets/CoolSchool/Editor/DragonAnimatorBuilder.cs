using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the generic Dragon animator controller from the clips inside Dragon_LP.fbx
    /// (Infinity PBR "Low Poly Monster Bundle"). Idle and Walk follow the "Speed" float; the other
    /// states are one-shots fired by triggers; "Flying" takes the dragon off and keeps it in the air.
    /// </summary>
    public static class DragonAnimatorBuilder
    {
        public const string ModelName = "Dragon_LP";

        public const string IdleClip = "Idle";
        public const string WalkClip = "Walk";
        public const string BreatheFireClip = "Breathe Fire";
        public const string TailWhipClip = "Tail Whip L";
        public const string GotHitClip = "Got Hit 1";
        public const string HappyClip = "Idle Break";
        public const string TakeoffClip = "Idle Takeoff";
        public const string FlyClip = "Fly";
        public const string EatClip = "Attack 1";
        public const string SitClip = "Sit";
        public const string SittingClip = "Sitting";
        public const string RiseClip = "Rise";
        public const string GlideClip = "Fly Glide";

        /// <summary>Returns null when Dragon_LP.fbx or its Idle clip can't be found.</summary>
        public static AnimatorController CreateOrReplace(string path, List<string> missingClips)
        {
            Dictionary<string, AnimationClip> clips = FindDragonClips();
            if (clips.Count == 0)
            {
                missingClips.Add(ModelName + ".fbx");
                return null;
            }

            AnimationClip Get(string name)
            {
                if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
                missingClips.Add("Dragon: " + name);
                return null;
            }

            AnimationClip idle = Get(IdleClip);
            AnimationClip walk = Get(WalkClip);
            AnimationClip breathe = Get(BreatheFireClip);
            AnimationClip tail = Get(TailWhipClip);
            AnimationClip gotHit = Get(GotHitClip);
            AnimationClip happy = Get(HappyClip);
            AnimationClip takeoff = Get(TakeoffClip);
            AnimationClip fly = Get(FlyClip);
            AnimationClip eat = Get(EatClip);
            AnimationClip sit = Get(SitClip);
            AnimationClip sitting = Get(SittingClip);
            AnimationClip rise = Get(RiseClip);
            AnimationClip glide = Get(GlideClip);

            if (idle == null) return null;

            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("BreatheFire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TailWhip", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GotHit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Happy", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Flying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Gliding", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;

            // Flight: Takeoff then Fly. Without a Takeoff clip it goes straight to Fly.
            AnimatorState flyState = null;
            AnimatorState airborneEntry = null;
            if (fly != null)
            {
                flyState = machine.AddState("Fly");
                flyState.motion = fly;
                airborneEntry = flyState;

                var back = flyState.AddTransition(idleState);
                back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Flying");
                back.hasExitTime = false;
                back.duration = 0.3f;
            }

            // The ride: "Gliding" swaps the flapping Fly for a smooth glide (on the way down), and back.
            if (glide != null && flyState != null)
            {
                AnimatorState glideState = machine.AddState("Glide");
                glideState.motion = glide;

                AnimatorStateTransition toGlide = flyState.AddTransition(glideState);
                toGlide.AddCondition(AnimatorConditionMode.If, 0f, "Gliding");
                toGlide.hasExitTime = false;
                toGlide.duration = 0.3f;

                AnimatorStateTransition fromGlide = glideState.AddTransition(flyState);
                fromGlide.AddCondition(AnimatorConditionMode.IfNot, 0f, "Gliding");
                fromGlide.hasExitTime = false;
                fromGlide.duration = 0.3f;

                AnimatorStateTransition land = glideState.AddTransition(idleState);
                land.AddCondition(AnimatorConditionMode.IfNot, 0f, "Flying");
                land.hasExitTime = false;
                land.duration = 0.3f;
            }

            if (takeoff != null && flyState != null)
            {
                AnimatorState takeoffState = machine.AddState("Takeoff");
                takeoffState.motion = takeoff;
                airborneEntry = takeoffState;

                AnimatorStateTransition toFly = takeoffState.AddTransition(flyState);
                toFly.hasExitTime = true;
                toFly.exitTime = 0.9f;
                toFly.duration = 0.2f;
            }

            if (walk != null)
            {
                AnimatorState walkState = machine.AddState("Walk");
                walkState.motion = walk;

                AnimatorStateTransition start = idleState.AddTransition(walkState);
                start.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                start.hasExitTime = false;
                start.duration = 0.2f;

                AnimatorStateTransition stop = walkState.AddTransition(idleState);
                stop.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                stop.hasExitTime = false;
                stop.duration = 0.2f;

                AddTakeoffTransition(walkState, airborneEntry);
            }

            AddTakeoffTransition(idleState, airborneEntry);

            if (breathe != null) AddOneShot(machine, idleState, airborneEntry, "BreatheFire", breathe, 0.9f);
            if (tail != null) AddOneShot(machine, idleState, airborneEntry, "TailWhip", tail, 0.9f);
            if (gotHit != null) AddOneShot(machine, idleState, airborneEntry, "GotHit", gotHit, 0.85f);
            if (happy != null) AddOneShot(machine, idleState, airborneEntry, "Happy", happy, 0.95f);
            if (eat != null) AddOneShot(machine, idleState, airborneEntry, "Eat", eat, 0.9f);

            AddSitting(machine, idleState, sit, sitting, rise);

            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Lying down so the knight can climb on: Idle goes to Sit and then to the looping Sitting while the "Sitting" bool is on.
        /// When it goes off, the dragon gets up (the Rise clip when there is one) and is back in Idle. Missing clips are skipped.
        /// </summary>
        static void AddSitting(AnimatorStateMachine machine, AnimatorState idleState, AnimationClip sit, AnimationClip sitting, AnimationClip rise)
        {
            AnimationClip first = sit != null ? sit : sitting;
            if (first == null) return;

            AnimatorState sitState = machine.AddState("Sit");
            sitState.motion = first;
            AnimatorStateTransition down = idleState.AddTransition(sitState);
            down.AddCondition(AnimatorConditionMode.If, 0f, "Sitting");
            down.hasExitTime = false;
            down.duration = 0.2f;

            AnimatorState restState = sitState;
            if (sit != null && sitting != null)
            {
                restState = machine.AddState("Sitting");
                restState.motion = sitting;
                AnimatorStateTransition settle = sitState.AddTransition(restState);
                settle.hasExitTime = true;
                settle.exitTime = 0.95f;
                settle.duration = 0.15f;
            }

            // Getting up: through Rise when it exists, else straight to Idle. Also while the Sit clip is still playing.
            AnimatorState getUp = null;
            if (rise != null)
            {
                getUp = machine.AddState("Rise");
                getUp.motion = rise;
                AnimatorStateTransition done = getUp.AddTransition(idleState);
                done.hasExitTime = true;
                done.exitTime = 0.9f;
                done.duration = 0.2f;
            }

            foreach (AnimatorState from in new[] { sitState, restState })
            {
                AnimatorStateTransition up = from.AddTransition(getUp != null ? getUp : idleState);
                up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
                up.hasExitTime = false;
                up.duration = 0.2f;
                if (restState == sitState) break;
            }
        }

        /// <summary>Leaves a ground state for the takeoff (or flight) as soon as "Flying" turns on.</summary>
        static void AddTakeoffTransition(AnimatorState from, AnimatorState airborneEntry)
        {
            if (airborneEntry == null) return;

            AnimatorStateTransition up = from.AddTransition(airborneEntry);
            up.AddCondition(AnimatorConditionMode.If, 0f, "Flying");
            up.hasExitTime = false;
            up.duration = 0.2f;
        }

        /// <summary>
        /// A one-shot reached from any ground state by a trigger (never while flying) that returns to Idle.
        /// The takeoff transition is added first so "Flying" wins over the return to Idle.
        /// </summary>
        static void AddOneShot(AnimatorStateMachine machine, AnimatorState idle, AnimatorState airborneEntry, string trigger, AnimationClip clip, float exitTime)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Flying");
            enter.canTransitionToSelf = false;
            enter.hasExitTime = false;
            enter.duration = 0.1f;

            AddTakeoffTransition(state, airborneEntry);

            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = exitTime;
            back.duration = 0.2f;
        }

        /// <summary>All clips inside Dragon_LP.fbx keyed by clip name.</summary>
        static Dictionary<string, AnimationClip> FindDragonClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets(ModelName + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != ModelName) continue;

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        result.TryAdd(clip.name, clip);
                }
            }
            return result;
        }
    }
}
