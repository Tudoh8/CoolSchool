using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds Teacher.controller: an idle that loops, a talking gesture while the "Talking" bool is on
    /// (a few gestures one after the other, because the clips don't loop), and a wave on the "Wave" trigger.
    /// Clips are found by FBX file name; the first group that exists wins, so a female teacher gets the female clips.
    /// </summary>
    public static class TeacherAnimatorBuilder
    {
        static readonly string[][] IdleCandidates =
        {
            new[] { "HumanF@Idle01" },
            new[] { "HumanM@Idle01" },
        };

        static readonly string[][] TalkCandidates =
        {
            new[] { "HumanF@Talk01", "HumanF@Talk02", "HumanF@Talk03" },
            new[] { "HumanM@Talk01", "HumanM@Talk02", "HumanM@Talk03" },
            new[] { "Converse_01", "Converse_02", "Converse_03" },
        };

        static readonly string[][] WaveCandidates =
        {
            new[] { "HumanF@HandWave01" },
            new[] { "HumanM@HandWave01" },
            new[] { "Shoulder_Wave" },
        };

        /// <summary>The clips that ended up in the controller, for the build log.</summary>
        public sealed class Result
        {
            public AnimatorController Controller;
            public string IdleClip;
            public readonly List<string> TalkClips = new List<string>();
            public string WaveClip;
        }

        /// <summary>Returns a result whose Controller is null if not even an idle clip could be found.</summary>
        public static Result CreateOrReplace(string path)
        {
            var result = new Result();

            AnimationClip idle = FindFirst(IdleCandidates, out string idleName);
            if (idle == null)
            {
                Debug.LogWarning("CoolSchool: no idle clip found for the teacher (HumanF@Idle01 / HumanM@Idle01); she will stand stiff.");
                return result;
            }
            result.IdleClip = idleName;

            var talks = new List<AnimationClip>();
            foreach (string[] group in TalkCandidates)
            {
                foreach (string name in group)
                {
                    AnimationClip clip = FindClip(name);
                    if (clip == null) continue;

                    talks.Add(clip);
                    result.TalkClips.Add(name);
                }
                if (talks.Count > 0) break;
            }
            if (talks.Count == 0)
                Debug.LogWarning("CoolSchool: no talking gesture clip found for the teacher (HumanF@Talk01, Converse_01, ...).");

            AnimationClip wave = FindFirst(WaveCandidates, out string waveName);
            result.WaveClip = waveName;
            if (wave == null)
                Debug.LogWarning("CoolSchool: no wave clip found for the teacher (HumanF@HandWave01, Shoulder_Wave).");

            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;

            AddTalking(machine, idleState, talks);
            if (wave != null) AddWave(machine, idleState, wave);

            AssetDatabase.SaveAssets();
            result.Controller = controller;
            return result;
        }

        /// <summary>
        /// Idle goes to the first gesture while "Talking" is on; the gestures follow each other and loop;
        /// every gesture returns to idle as soon as "Talking" turns off.
        /// </summary>
        static void AddTalking(AnimatorStateMachine machine, AnimatorState idleState, List<AnimationClip> talks)
        {
            if (talks.Count == 0) return;

            var states = new List<AnimatorState>();
            for (int i = 0; i < talks.Count; i++)
            {
                AnimatorState state = machine.AddState("Talk" + (i + 1));
                state.motion = talks[i];
                states.Add(state);
            }

            AnimatorStateTransition start = idleState.AddTransition(states[0]);
            start.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            start.hasExitTime = false;
            start.duration = 0.25f;

            for (int i = 0; i < states.Count; i++)
            {
                // Listed first, so it wins over the next gesture.
                AnimatorStateTransition stop = states[i].AddTransition(idleState);
                stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
                stop.hasExitTime = false;
                stop.duration = 0.25f;

                AnimatorStateTransition next = states[i].AddTransition(states[(i + 1) % states.Count]);
                next.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
                next.hasExitTime = true;
                next.exitTime = 0.9f;
                next.duration = 0.3f;
            }
        }

        static void AddWave(AnimatorStateMachine machine, AnimatorState idleState, AnimationClip wave)
        {
            AnimatorState state = machine.AddState("Wave");
            state.motion = wave;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Wave");
            enter.canTransitionToSelf = false;
            enter.hasExitTime = false;
            enter.duration = 0.15f;

            AnimatorStateTransition back = state.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.25f;
        }

        static AnimationClip FindFirst(string[][] groups, out string foundName)
        {
            foreach (string[] group in groups)
            {
                foreach (string name in group)
                {
                    AnimationClip clip = FindClip(name);
                    if (clip == null) continue;

                    foundName = name;
                    return clip;
                }
            }

            foundName = null;
            return null;
        }

        /// <summary>The first real animation clip inside the FBX with this exact file name, skipping root-motion variants.</summary>
        static AnimationClip FindClip(string fileName)
        {
            foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Contains("[RM]") || path.Contains("/RootMotion/")) continue;
                if (Path.GetFileNameWithoutExtension(path) != fileName) continue;

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
