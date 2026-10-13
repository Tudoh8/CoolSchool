using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The baby dragon that hatches from the egg: a small copy of the dragon model (about 0.6 m) that plays its Idle and hops
    /// around by transform. It peeps, hops after the knights to the celebration and hides again when the visit is over.
    /// The same model is the plush buddy he can take along (the prefab BabyDragon).
    /// </summary>
    public class BabyDragon : MonoBehaviour
    {
        const float HopSeconds = 0.35f;
        const float HopHeight = 0.28f;
        const float HopLength = 0.9f;

        Vector3 restScale = Vector3.one;
        bool haveScale;
        Coroutine moving;

        /// <summary>Pops up at a spot with a hop and a peep.</summary>
        public void Hatch(Vector3 position)
        {
            EnsureReady();
            StopMoving();

            transform.position = position;
            gameObject.SetActive(true);
            BattleEffects.Sparkle(position + Vector3.up * 0.3f);
            Sfx.Play("castle.baby_peep", position);
            moving = StartCoroutine(HatchRoutine(position));
        }

        /// <summary>Hops over to a point (little hops, facing the way it goes). arrived is called when it is there.</summary>
        public void HopTo(Vector3 point, Action arrived = null)
        {
            EnsureReady();
            StopMoving();

            if (!isActiveAndEnabled)
            {
                transform.position = point;
                arrived?.Invoke();
                return;
            }
            moving = StartCoroutine(HopToRoutine(point, arrived));
        }

        /// <summary>A hop on the spot and a peep.</summary>
        public void Hop()
        {
            if (!isActiveAndEnabled) return;

            Sfx.Play("castle.baby_peep", transform.position);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.5f);
            StartCoroutine(HopRoutine());
        }

        /// <summary>Away again, at its normal size, ready for the next visit.</summary>
        public void Hide()
        {
            EnsureReady();
            StopAllCoroutines();
            moving = null;
            transform.localScale = restScale;
            gameObject.SetActive(false);
        }

        void EnsureReady()
        {
            if (haveScale) return;

            haveScale = true;
            restScale = transform.localScale;
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
                animator.applyRootMotion = false;
        }

        void StopMoving()
        {
            if (moving != null && isActiveAndEnabled) StopCoroutine(moving);
            moving = null;
        }

        void OnDisable()
        {
            moving = null;
        }

        IEnumerator HatchRoutine(Vector3 position)
        {
            transform.localScale = Vector3.zero;
            for (float elapsed = 0f; elapsed < 0.45f; elapsed += Time.deltaTime)
            {
                float k = elapsed / 0.45f;
                transform.localScale = restScale * Lessons.LessonMotion.OutBack(k);
                transform.position = position + Vector3.up * (Lessons.LessonMotion.Hump(k) * 0.5f);
                yield return null;
            }

            transform.localScale = restScale;
            transform.position = position;
            moving = null;
        }

        IEnumerator HopRoutine()
        {
            Vector3 start = transform.position;
            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                transform.position = start + Vector3.up * (Lessons.LessonMotion.Hump(elapsed / HopSeconds) * HopHeight);
                yield return null;
            }
            transform.position = start;
        }

        IEnumerator HopToRoutine(Vector3 point, Action arrived)
        {
            // Never longer than a few seconds, wherever the spot is.
            float giveUp = Time.time + 6f;
            while (CastleMotion.FlatDistance(transform.position, point) > 0.05f && Time.time < giveUp)
            {
                Vector3 start = transform.position;
                Vector3 toPoint = point - start;
                toPoint.y = 0f;
                float step = Mathf.Min(HopLength, toPoint.magnitude);
                Vector3 end = start + toPoint.normalized * step;
                end.y = point.y;
                if (toPoint.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toPoint.normalized, Vector3.up);

                for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
                {
                    float k = elapsed / HopSeconds;
                    transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (Lessons.LessonMotion.Hump(k) * HopHeight);
                    yield return null;
                }
                transform.position = end;
            }

            Vector3 final = transform.position;
            final.x = point.x;
            final.z = point.z;
            final.y = point.y;
            transform.position = final;
            moving = null;
            arrived?.Invoke();
        }
    }
}
