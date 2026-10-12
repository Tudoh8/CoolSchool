using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Characters;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Swings a foam sword. Aims for the player: it turns toward the nearest opponent by itself,
    /// so a small thumb never has to aim. Used by the player, the knight classmates and the goblins.
    /// </summary>
    [RequireComponent(typeof(Hittable))]
    public class SwordFighter : MonoBehaviour
    {
        // Total width of the swing in front of the fighter.
        const float ConeDegrees = 120f;

        [Tooltip("How far the sword reaches, measured to the edge of whoever is hit.")]
        [SerializeField] float reach = 1.3f;
        [Tooltip("Looks for someone to swing at within this distance.")]
        [SerializeField] float aimRadius = 3f;
        [SerializeField] float attackCooldown = 0.45f;
        [Tooltip("Seconds from starting the swing until it bumps someone.")]
        [SerializeField] float hitDelay = 0.25f;
        [Tooltip("Degrees per second when turning toward the target.")]
        [SerializeField] float turnSpeed = 900f;

        Hittable self;
        KidAnimator kidAnimator;
        float nextAttackTime;
        Coroutine attackRoutine;
        readonly List<Hittable> buffer = new List<Hittable>();

        /// <summary>How many hits each swing counts for. 2 with the golden sword.</summary>
        public int HitMultiplier { get; set; } = 1;

        /// <summary>False for allies: their hits make an opponent wobble but never defeat it. Only the player defeats.</summary>
        public bool CanDefeat { get; set; } = true;

        public bool CanAttack => self != null && self.IsAlive && !self.IsStunned && Time.time >= nextAttackTime;

        void Awake()
        {
            self = GetComponent<Hittable>();
            kidAnimator = GetComponent<KidAnimator>();
        }

        void OnDisable()
        {
            attackRoutine = null;
            if (self != null) self.IsBlocking = false;
        }

        /// <summary>Starts a swing. Returns false while resting from the last one or while wobbling.</summary>
        public bool Attack()
        {
            if (!CanAttack) return false;

            nextAttackTime = Time.time + attackCooldown;
            Hittable target = FindTarget();

            if (kidAnimator != null) kidAnimator.PlayAttack();

            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackRoutine(target));
            return true;
        }

        public void SetBlocking(bool blocking)
        {
            if (self != null) self.IsBlocking = blocking;
            if (kidAnimator != null) kidAnimator.SetBlocking(blocking);
        }

        IEnumerator AttackRoutine(Hittable target)
        {
            float elapsed = 0f;
            while (elapsed < hitDelay)
            {
                TurnToward(target, turnSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            attackRoutine = null;

            // Bumped before the sword landed: the swing is dropped.
            if (!self.IsAlive || self.IsStunned) yield break;

            TurnToward(target, 360f);
            DealHits();
            BattleEffects.Swoosh(transform.position + transform.forward * 0.8f + Vector3.up * 0.9f);
            Sfx.Play("battle.sword_swing", transform.position);
        }

        Hittable FindTarget()
        {
            Hittable best = null;
            float bestDistance = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (!IsOpponent(other)) continue;

                float distance = Hittable.FlatDistance(transform.position, other.transform.position);
                if (distance > aimRadius + other.HitRadius || distance >= bestDistance) continue;

                bestDistance = distance;
                best = other;
            }
            return best;
        }

        void DealHits()
        {
            // Copy first: a hit can switch things on or off while we are looping.
            buffer.Clear();
            buffer.AddRange(Hittable.All);

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            foreach (Hittable other in buffer)
            {
                if (other == null || !IsOpponent(other)) continue;

                float distance = Hittable.FlatDistance(transform.position, other.transform.position);
                if (distance > reach + other.HitRadius) continue;

                Vector3 direction = Hittable.FlatDirection(transform.position, other.transform.position);
                bool inCone = direction == Vector3.zero || Vector3.Angle(forward, direction) <= ConeDegrees * 0.5f;
                if (inCone) other.TakeHit(transform.position, HitMultiplier, CanDefeat);
            }
        }

        bool IsOpponent(Hittable other)
        {
            return other != null && other != self && other.Team != self.Team && other.IsAlive;
        }

        void TurnToward(Hittable target, float maxDegrees)
        {
            if (target == null || !target.IsAlive) return;

            Vector3 direction = Hittable.FlatDirection(transform.position, target.transform.position);
            if (direction == Vector3.zero) return;

            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, maxDegrees);
        }
    }
}
