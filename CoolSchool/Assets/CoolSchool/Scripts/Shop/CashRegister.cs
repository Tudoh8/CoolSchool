using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// The cash register on the counter. Coins fly into it, it bumps a little for each one,
    /// and after the last coin it hops and goes ka-ching.
    /// </summary>
    public class CashRegister : MonoBehaviour
    {
        [Tooltip("The register prop that hops. Must not be static.")]
        [SerializeField] Transform register;
        [Tooltip("Where the coins fly to: the top of the register.")]
        [SerializeField] Transform coinTarget;

        const float HopHeight = 0.15f;

        Vector3 basePosition;
        Vector3 baseScale = Vector3.one;
        bool remembered;
        Coroutine motion;

        public Vector3 CoinTarget => coinTarget != null ? coinTarget.position : transform.position;

        void Remember()
        {
            if (remembered || register == null) return;

            remembered = true;
            basePosition = register.localPosition;
            baseScale = register.localScale;
        }

        /// <summary>A coin arrived: a small squash.</summary>
        public void Bump()
        {
            Remember();
            if (register == null || !isActiveAndEnabled) return;

            BattleEffects.Sparkle(CoinTarget);
            Play(BumpRoutine());
        }

        /// <summary>The last coin: a hop, a sparkle and ka-ching.</summary>
        public void KaChing()
        {
            Remember();
            Sfx.Play("shop.register", CoinTarget);
            BattleEffects.Sparkle(CoinTarget + Vector3.up * 0.1f);
            BattleEffects.Confetti(CoinTarget + Vector3.up * 0.4f);
            if (register != null && isActiveAndEnabled) Play(HopRoutine());
        }

        /// <summary>Puts the register back exactly where it stood, even if it was cut off mid-hop.</summary>
        public void ResetNow()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            if (!remembered || register == null) return;

            register.localPosition = basePosition;
            register.localScale = baseScale;
        }

        void OnDisable()
        {
            ResetNow();
        }

        void Play(IEnumerator routine)
        {
            ResetNow();
            motion = StartCoroutine(routine);
        }

        IEnumerator BumpRoutine()
        {
            yield return ShopMotion.Punch(register, baseScale, new Vector3(1.1f, 0.85f, 1.1f), 0.22f);
            motion = null;
        }

        IEnumerator HopRoutine()
        {
            const float seconds = 0.5f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float hump = Mathf.Sin(Mathf.Clamp01(elapsed / seconds) * Mathf.PI);
                register.localPosition = basePosition + Vector3.up * (hump * HopHeight);
                register.localScale = Vector3.Scale(baseScale, new Vector3(1f - hump * 0.06f, 1f + hump * 0.12f, 1f - hump * 0.06f));
                yield return null;
            }

            register.localPosition = basePosition;
            register.localScale = baseScale;
            motion = null;
        }
    }
}
