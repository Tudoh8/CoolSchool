using System;
using System.Collections.Generic;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Anything a foam sword can bump: the player, a knight classmate, a goblin or the dragon.
    /// Nobody gets hurt: a "hit" only makes confetti and a short wobble, and a "defeated"
    /// opponent tumbles and giggles. The owner plays the animations by listening to the events.
    /// </summary>
    public class Hittable : MonoBehaviour
    {
        [SerializeField] BattleTeam team = BattleTeam.Heroes;
        [Tooltip("Hits that make an opponent tumble over.")]
        [SerializeField] int hitsToDefeat = 3;
        [Tooltip("Turn off for the player and knight classmates: they only wobble and never fall.")]
        [SerializeField] bool canBeDefeated = true;
        [Tooltip("Seconds after a hit during which nothing else can hit this one.")]
        [SerializeField] float stunSeconds = 0.6f;
        [Tooltip("How big this one is for sword swings. The dragon uses about 1.8.")]
        [SerializeField] float hitRadius = 0.3f;
        [Tooltip("Where confetti pops and fighters aim. Optional.")]
        [SerializeField] Transform aimPoint;

        static readonly List<Hittable> all = new List<Hittable>();

        /// <summary>Every active Hittable in the scene.</summary>
        public static IReadOnlyList<Hittable> All => all;

        public event Action<Hittable> Hit;
        public event Action<Hittable> Blocked;
        public event Action<Hittable> Defeated;

        public BattleTeam Team => team;
        public float HitRadius => hitRadius;
        public int HitsTaken { get; private set; }
        public bool IsDefeated { get; private set; }
        public bool IsBlocking { get; set; }
        public bool IsStunned => Time.time < stunnedUntil;
        public bool IsAlive => isActiveAndEnabled && !IsDefeated;

        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 0.8f;

        float stunnedUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => all.Clear();

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        /// <summary>
        /// Somebody swings at this one. Returns true when it was a real (confetti) hit,
        /// false when it was blocked, too soon after the last hit, or this one is already down.
        /// Amount is how many hits it counts for (2 with the golden sword).
        /// </summary>
        public bool TakeHit(Vector3 attackerPosition, int amount = 1)
        {
            if (!IsAlive || IsStunned) return false;

            if (IsBlocking && IsAttackerInFront(attackerPosition))
            {
                BattleEffects.BlockSpark(AimPosition);
                Sfx.Play("battle.shield_block", AimPosition);
                Blocked?.Invoke(this);
                return false;
            }

            HitsTaken += Mathf.Max(1, amount);
            stunnedUntil = Time.time + stunSeconds;

            // Decide first, so a Hit listener can already see IsDefeated and skip its wobble.
            bool defeated = canBeDefeated && HitsTaken >= hitsToDefeat;
            if (defeated) IsDefeated = true;

            BattleEffects.Confetti(AimPosition);
            Sfx.Play("battle.hit_confetti", AimPosition);
            if (defeated) Sfx.Play("battle.tumble", AimPosition);
            Hit?.Invoke(this);

            if (defeated) Defeated?.Invoke(this);
            return true;
        }

        public void ResetState()
        {
            HitsTaken = 0;
            stunnedUntil = 0f;
            IsDefeated = false;
            IsBlocking = false;
        }

        bool IsAttackerInFront(Vector3 attackerPosition)
        {
            Vector3 toAttacker = FlatDirection(transform.position, attackerPosition);
            if (toAttacker == Vector3.zero) return true;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            return Vector3.Dot(forward.normalized, toAttacker) > 0.2f;
        }

        /// <summary>Distance on the ground, ignoring height.</summary>
        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Unit direction on the ground from a to b, or zero when they are on top of each other.</summary>
        internal static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
        }
    }
}
