using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// A golden star that floats along the dragon's flight path. It spins and bobs; when the dragon's back touches it, it pops
    /// with a sparkle and a ping. Stars are only shown during the ride. No collider, so nothing can bump into it.
    /// </summary>
    public class RideStar : MonoBehaviour
    {
        [SerializeField] float spinDegreesPerSecond = 120f;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 2.5f;

        const float PopSeconds = 0.3f;

        Vector3 basePosition;
        Vector3 baseScale = Vector3.one;
        bool haveBase;
        float popStart = -1f;

        /// <summary>True once the dragon has caught it this ride.</summary>
        public bool Caught { get; private set; }

        /// <summary>Remembers the star's place and size (the first time) and starts it fresh and visible.</summary>
        public void Show()
        {
            Remember();
            Caught = false;
            popStart = -1f;
            transform.localScale = baseScale;
            gameObject.SetActive(true);
        }

        /// <summary>The dragon got it: a sparkle, a ping, and it pops away.</summary>
        public void Catch()
        {
            if (Caught) return;

            Remember();
            Caught = true;
            popStart = Time.time;
            Sfx.Play("castle.star_ping", transform.position);
            BattleEffects.Sparkle(transform.position);
            BattleEffects.Confetti(transform.position, new Color(1f, 0.85f, 0.2f));
        }

        /// <summary>Back to the start: not caught, back in place and hidden.</summary>
        public void ResetStar()
        {
            Remember();
            Caught = false;
            popStart = -1f;
            transform.localPosition = basePosition;
            transform.localScale = baseScale;
            gameObject.SetActive(false);
        }

        void Remember()
        {
            if (haveBase) return;

            haveBase = true;
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
        }

        void Update()
        {
            Remember();

            if (Caught)
            {
                float k = popStart < 0f ? 1f : Mathf.Clamp01((Time.time - popStart) / PopSeconds);
                transform.localScale = baseScale * Mathf.Max(0f, 1.3f - 1.3f * k);
                if (k >= 1f) gameObject.SetActive(false);
                return;
            }

            transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + basePosition.x) * bobHeight);
        }
    }
}
