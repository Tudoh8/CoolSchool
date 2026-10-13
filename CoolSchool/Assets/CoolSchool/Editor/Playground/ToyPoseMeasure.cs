using System.Collections.Generic;
using System.Linq;
using CoolSchool.Player;
using CoolSchool.Playground;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Measures, once per build, how high the player kid's hips are above his feet when he sits (on a seat, or on the ground) and how high
    /// his hands are when he hangs. It poses him exactly the way the preview shots do: the "ToyPose" parameter of his animator is set and the
    /// animator is stepped in edit mode, then the hips and hands are read from the posed bones. (An earlier version evaluated the clip through
    /// a PlayableGraph, which left the bones unposed and measured his standing hips for every pose.) The player's bones are put back afterwards.
    /// A result that looks like the standing pose, or is outside believable bounds, is replaced by the fallback constants.
    /// </summary>
    internal static class ToyPoseMeasure
    {
        const float StepSeconds = 0.05f;
        const float SettleSeconds = 1.0f;

        public static ToyPoseOffsets Measure(PlayerController player)
        {
            ToyPoseOffsets result = ToyPoseOffsets.Defaults;
            Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("PlaygroundToys: pose offsets: the player has no humanoid animator with a controller, using the defaults "
                    + $"(seat {result.sitSeatHips}, slide {result.sitSlideHips}, hang {result.hangHands}).");
                return result;
            }

            // The animator poses the real bones of the scene's player: remember every transform and put it all back afterwards.
            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            var saved = new List<(Vector3 position, Quaternion rotation, Vector3 scale)>(transforms.Length);
            foreach (Transform t in transforms) saved.Add((t.localPosition, t.localRotation, t.localScale));
            bool rootMotion = animator.applyRootMotion;
            AnimatorCullingMode culling = animator.cullingMode;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            bool hasParameter = animator.parameters.Any(p => p.name == "ToyPose");
            float standing = float.NaN, seat = float.NaN, slide = float.NaN, hang = float.NaN, standingHands = float.NaN;
            try
            {
                if (!hasParameter) throw new System.InvalidOperationException("the kid controller has no ToyPose parameter");

                Pose(animator, ToyPose.None);
                standing = Height(player, animator, HumanBodyBones.Hips);
                standingHands = Hands(player, animator);

                Pose(animator, ToyPose.SitSeat);
                seat = Height(player, animator, HumanBodyBones.Hips);
                Pose(animator, ToyPose.SitSlide);
                slide = Height(player, animator, HumanBodyBones.Hips);
                Pose(animator, ToyPose.Hang);
                hang = Hands(player, animator);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("PlaygroundToys: pose offsets: measuring failed (" + exception.Message + "), using the defaults where needed.");
            }
            finally
            {
                animator.SetInteger("ToyPose", 0);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] == null) continue;
                    transforms[i].localPosition = saved[i].position;
                    transforms[i].localRotation = saved[i].rotation;
                    transforms[i].localScale = saved[i].scale;
                }
                animator.applyRootMotion = rootMotion;
                animator.cullingMode = culling;
            }

            // A pose that did not play leaves the hips at their standing height (the earlier bug): do not trust that.
            result.sitSeatHips = Accept(seat, 0.12f, 0.7f, result.sitSeatHips, "sit-on-a-seat hips", standing, true);
            result.sitSlideHips = Accept(slide, 0.03f, 0.45f, result.sitSlideHips, "sit-on-the-ground hips", standing, true);
            result.hangHands = Accept(hang, 0.6f, 2.5f, result.hangHands, "hang hands", standingHands, true);
            Debug.Log($"PlaygroundToys: pose offsets: sitSeatHips {result.sitSeatHips:0.000}, sitSlideHips {result.sitSlideHips:0.000}, hangHands {result.hangHands:0.000} "
                + $"(raw: standing hips {standing:0.000}, standing hands {standingHands:0.000}, seat {seat:0.000}, slide {slide:0.000}, hang {hang:0.000}; "
                + "the player root is at his feet; measured on the posed bones)");
            return result;
        }

        static float Accept(float measured, float min, float max, float fallback, string label, float unposed, bool mustDiffer)
        {
            bool believable = !float.IsNaN(measured) && measured >= min && measured <= max;
            bool posed = !mustDiffer || float.IsNaN(unposed) || Mathf.Abs(measured - unposed) > 0.015f;
            if (believable && posed) return measured;

            Debug.LogWarning($"PlaygroundToys: pose offsets: {label} measured {measured:0.000} (unposed {unposed:0.000}), "
                + $"{(believable ? "the same as the unposed kid, so the pose did not play" : "not believable")}; using {fallback:0.00}.");
            return fallback;
        }

        /// <summary>Rebinds the animator, sets the pose and steps it long enough for the blend into the pose to finish.</summary>
        static void Pose(Animator animator, ToyPose pose)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.SetInteger("ToyPose", (int)pose);
            for (float t = 0f; t < SettleSeconds; t += StepSeconds) animator.Update(StepSeconds);
        }

        static float Height(PlayerController player, Animator animator, HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            return t != null ? t.position.y - player.transform.position.y : float.NaN;
        }

        /// <summary>The height of the hands (the average of the two) above the player's root.</summary>
        static float Hands(PlayerController player, Animator animator)
        {
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (left == null || right == null) return float.NaN;
            return (left.position.y + right.position.y) * 0.5f - player.transform.position.y;
        }
    }
}
