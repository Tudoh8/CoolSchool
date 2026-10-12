using System;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A blob of green slime around the stuck baby. One zap from the ray gun pops it (green confetti), so it is a ray gun
    /// target like an alien. Only switched on during the slime rescue. It wobbles a little so it is easy to see.
    /// </summary>
    [RequireComponent(typeof(Hittable))]
    public class SlimeBlob : MonoBehaviour, IZapTarget
    {
        [SerializeField] Color popColor = new Color(0.35f, 0.9f, 0.25f, 1f);
        [SerializeField] float wobbleAmount = 0.12f;
        [SerializeField] float wobbleSpeed = 5f;

        Hittable hittable;
        Vector3 baseScale = Vector3.one;
        float phase;
        bool scaleCached;

        public Hittable Hittable
        {
            get
            {
                CacheReferences();
                return hittable;
            }
        }

        /// <summary>True while it is on and not popped.</summary>
        public bool IsActive => isActiveAndEnabled && Hittable.IsAlive;

        public bool CanBeZapped => IsActive;

        /// <summary>Raised when the blob pops.</summary>
        public event Action<SlimeBlob> Popped;

        void Awake() => CacheReferences();

        void CacheReferences()
        {
            if (hittable != null) return;

            hittable = GetComponent<Hittable>();

            // Whatever the builder set: a blob is an opponent that pops with one zap.
            if (hittable.Team != BattleTeam.Opponents) hittable.SetTeam(BattleTeam.Opponents);
            hittable.SetHitsToDefeat(1);

            baseScale = transform.localScale;
            scaleCached = true;
        }

        void OnEnable()
        {
            CacheReferences();
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null) hittable.Defeated -= OnDefeated;
            if (scaleCached) transform.localScale = baseScale;
        }

        void Update()
        {
            float wobble = 1f + Mathf.Sin(Time.time * wobbleSpeed + phase) * wobbleAmount;
            transform.localScale = new Vector3(baseScale.x * wobble, baseScale.y / wobble, baseScale.z * wobble);
        }

        /// <summary>Switches the blob on at this spot (a fresh, unpopped blob).</summary>
        public void Show(Vector3 position)
        {
            CacheReferences();
            hittable.ResetState();
            phase = UnityEngine.Random.Range(0f, 6.28f);

            transform.position = position;
            transform.localScale = baseScale;
            gameObject.SetActive(true);
            BattleEffects.Sparkle(position + Vector3.up * 0.3f);
        }

        void OnDefeated(Hittable who)
        {
            Pop();
        }

        /// <summary>Pops it by itself (the safety net) without a zap.</summary>
        public void PopByItself()
        {
            if (!IsActive) return;

            Pop();
        }

        void Pop()
        {
            Vector3 middle = transform.position + Vector3.up * 0.3f;
            BattleEffects.Confetti(middle, popColor);
            Sfx.Play("aliens.slime_pop", middle);

            gameObject.SetActive(false);
            Popped?.Invoke(this);
        }

        /// <summary>Back to switched off, whatever happened to it.</summary>
        public void ResetBlob()
        {
            CacheReferences();
            hittable.ResetState();
            gameObject.SetActive(false);
        }
    }
}
