using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// A funny surprise in an empty hiding spot: a cat, a rabbit or a squirrel pops out with a hop, squeaks, runs a few metres to
    /// the side and puffs away in a sparkle. Switched off until it is needed. Nothing is physical; it just moves its transform.
    /// </summary>
    public class SurpriseCritter : MonoBehaviour
    {
        const float HopSeconds = 0.45f;
        const float HopHeight = 0.5f;
        const float RunMetres = 3f;
        const float RunSpeed = 3.6f;
        const float LookSeconds = 0.5f;

        Animator animator;
        HashSet<string> flags;
        Coroutine routine;

        public bool IsBusy => routine != null;

        /// <summary>Pops out at this point, hops, squeaks, runs 3 m east or west and puffs away. done is called just before it goes.</summary>
        public void PopOut(Vector3 at, Action done = null)
        {
            Stop();
            gameObject.SetActive(true);
            routine = StartCoroutine(PopRoutine(at, done));
        }

        /// <summary>Gone at once (no puff). Safe to call at any time.</summary>
        public void Hide()
        {
            Stop();
            gameObject.SetActive(false);
        }

        void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        IEnumerator PopRoutine(Vector3 at, Action done)
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            flags = null;

            // It starts a little below the ground and hops up out of it, facing the camera.
            Vector3 start = at + Vector3.up * -0.2f;
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.back, Vector3.up));
            SetMoving(false, false);
            Sfx.Play("zoo.surprise_pop", at);

            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / HopSeconds;
                transform.position = Vector3.Lerp(start, at, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * HopHeight);
                yield return null;
            }
            transform.position = at;

            // It looks at the camera for a moment...
            yield return new WaitForSeconds(LookSeconds);

            // ...then runs off to one side.
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Vector3 end = at + Vector3.right * (side * RunMetres);
            transform.rotation = Quaternion.LookRotation(Vector3.right * side, Vector3.up);
            SetMoving(true, true);
            while (Vector3.Distance(transform.position, end) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, end, RunSpeed * Time.deltaTime);
                yield return null;
            }

            SetMoving(false, false);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            routine = null;
            done?.Invoke();
            gameObject.SetActive(false);
        }

        /// <summary>Sets isWalking / isRunning on the Animator, but only those it has (the animals' controllers differ).</summary>
        void SetMoving(bool walking, bool running)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            if (flags == null)
            {
                flags = new HashSet<string>();
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Bool) flags.Add(parameter.name);
            }

            if (flags.Contains("isWalking")) animator.SetBool("isWalking", walking);
            if (flags.Contains("isRunning")) animator.SetBool("isRunning", running);
            if (flags.Contains("isJumping")) animator.SetBool("isJumping", false);
        }
    }
}
