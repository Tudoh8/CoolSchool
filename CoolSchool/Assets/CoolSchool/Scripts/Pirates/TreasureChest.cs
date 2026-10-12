using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The treasure chest. Place its root where the chest should stand once it is dug up (on the sand); at the start it sinks
    /// into the sand by buriedDepth and hides, and Rise brings it back up with a shake. Unlock pops the golden padlock off and
    /// opens the lid (or, without a separate lid, squashes the whole chest with a pop) in a golden glow. SpillCoins then
    /// throws the gold coins out in a fan in front of the chest, to be counted. ResetChest puts everything back.
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        [Tooltip("The chest model's lid child (its pivot is the hinge). Leave empty if the model has none: the chest opens with a pop.")]
        [SerializeField] Transform lid;
        [Tooltip("A gold padlock on the front. Pops off when the chest is unlocked.")]
        [SerializeField] GameObject lockVisual;
        [Tooltip("10 places in a fan in front of the chest (on the sand, camera side) where the coins land.")]
        [SerializeField] Transform[] coinSpots;
        [SerializeField] GameObject coinPrefab;
        [SerializeField] float buriedDepth = 1f;

        [Header("Opening")]
        [Tooltip("How far the lid turns open, around its own axes. The Synty chest lid opens by turning backwards around X.")]
        [SerializeField] Vector3 lidOpenEuler = new Vector3(-110f, 0f, 0f);
        [Tooltip("How high above a coin spot the middle of a coin hangs (a coin stands on its edge).")]
        [SerializeField] float coinHeight = 0.3f;

        static readonly Color SandColor = new Color(0.86f, 0.74f, 0.5f, 1f);
        static readonly Color GoldColor = new Color(1f, 0.8f, 0.3f, 1f);

        const float RiseSeconds = 1.4f;
        const float ShakeMetres = 0.035f;
        const float LockPopSeconds = 0.55f;
        const float LidOpenSeconds = 0.7f;
        const float PopSquashSeconds = 0.5f;
        const float SpillDelaySeconds = 0.09f;
        const float SpillFlySeconds = 0.55f;
        const float SpillArcHeight = 1f;
        const float GlowIntensity = 2.2f;
        const float GlowRange = 3.5f;
        const int FanSize = 10;

        /// <summary>Where a coin disappears to: just above the mouth of the chest.</summary>
        public Vector3 IntoChestPoint => risenPosition + mouthOffset;

        Vector3 risenPosition;
        Vector3 baseScale = Vector3.one;
        Vector3 mouthOffset = Vector3.up * 0.5f;
        Quaternion lidClosedRotation = Quaternion.identity;
        Transform lockParent;
        Vector3 lockLocalPosition;
        Quaternion lockLocalRotation = Quaternion.identity;
        Vector3 lockLocalScale = Vector3.one;
        readonly List<TreasureCoin> coins = new List<TreasureCoin>();
        Light glow;
        Coroutine mainRoutine;
        bool unlocked;

        void Awake()
        {
            risenPosition = transform.position;
            baseScale = transform.localScale;
            if (lid != null) lidClosedRotation = lid.localRotation;

            if (lockVisual != null)
            {
                Transform t = lockVisual.transform;
                lockParent = t.parent;
                lockLocalPosition = t.localPosition;
                lockLocalRotation = t.localRotation;
                lockLocalScale = t.localScale;
            }

            // The mouth is the middle of the chest, near the top: the coins vanish into it.
            if (TreasureMotion.TryGetBounds(gameObject, out Bounds bounds))
            {
                Vector3 mouth = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.8f, bounds.center.z);
                mouthOffset = mouth - transform.position;
            }

            ResetChest();
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Up out of the sand with a shake and a sparkle. The callback runs when it stands there.</summary>
        public void Rise(System.Action done)
        {
            StopMain();
            SetVisible(true);
            mainRoutine = StartCoroutine(RiseRoutine(done));
        }

        /// <summary>The padlock pops off, the lid opens and the gold glows. The callback runs when it is open.</summary>
        public void Unlock(System.Action opened)
        {
            if (unlocked)
            {
                opened?.Invoke();
                return;
            }

            StopMain();
            SetVisible(true);
            mainRoutine = StartCoroutine(UnlockRoutine(opened));
        }

        /// <summary>
        /// Throws the first `count` coins out in the fan in front of the chest. Coins from an earlier call are removed first.
        /// The coins are not children of the chest; they are cleared by the next call or by ResetChest.
        /// </summary>
        public TreasureCoin[] SpillCoins(int count)
        {
            DestroyCoins();
            count = Mathf.Clamp(count, 0, FanSize);

            var result = new TreasureCoin[count];
            for (int i = 0; i < count; i++)
            {
                TreasureCoin coin = TreasureCoin.Create(coinPrefab, IntoChestPoint, TreasureCoin.Diameter, false);
                coin.gameObject.SetActive(false);
                coins.Add(coin);
                result[i] = coin;
                StartCoroutine(SpillOne(coin, SpotFor(i), i * SpillDelaySeconds));
            }
            return result;
        }

        /// <summary>Back to the start: buried and hidden, locked, closed, no coins.</summary>
        public void ResetChest()
        {
            StopMain();
            StopAllCoroutines();
            DestroyCoins();

            unlocked = false;
            transform.localScale = baseScale;
            if (lid != null) lid.localRotation = lidClosedRotation;

            if (lockVisual != null)
            {
                lockVisual.transform.SetParent(lockParent, false);
                lockVisual.transform.localPosition = lockLocalPosition;
                lockVisual.transform.localRotation = lockLocalRotation;
                lockVisual.transform.localScale = lockLocalScale;
                lockVisual.SetActive(true);
            }

            if (glow != null)
            {
                Destroy(glow.gameObject);
                glow = null;
            }

            transform.position = risenPosition + Vector3.down * buriedDepth;
            SetVisible(false);
        }

        // ----- Rising ------------------------------------------------------------------------------

        IEnumerator RiseRoutine(System.Action done)
        {
            Vector3 buried = risenPosition + Vector3.down * buriedDepth;
            transform.position = buried;
            Sfx.Play("pirates.chest_rise", risenPosition);
            BattleEffects.Confetti(risenPosition + Vector3.up * 0.05f, SandColor);

            float nextPuff = 0.35f;
            for (float elapsed = 0f; elapsed < RiseSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / RiseSeconds;
                float k = Mathf.SmoothStep(0f, 1f, t);
                Vector3 shake = Random.insideUnitSphere * (ShakeMetres * (1f - t));
                shake.y = 0f;
                transform.position = Vector3.Lerp(buried, risenPosition, k) + shake;

                if (elapsed >= nextPuff)
                {
                    nextPuff += 0.35f;
                    BattleEffects.Confetti(risenPosition + Vector3.up * 0.05f, SandColor);
                }
                yield return null;
            }

            transform.position = risenPosition;
            BattleEffects.Sparkle(IntoChestPoint);
            BattleEffects.Sparkle(risenPosition + Vector3.up * 0.2f);
            mainRoutine = null;
            done?.Invoke();
        }

        // ----- Opening -----------------------------------------------------------------------------

        IEnumerator UnlockRoutine(System.Action opened)
        {
            unlocked = true;
            Sfx.Play("pirates.lock_open", IntoChestPoint);

            if (lockVisual != null) StartCoroutine(PopLockOff());
            yield return new WaitForSeconds(0.2f);

            EnsureGlow();
            StartCoroutine(FadeGlow(GlowIntensity, 0.5f));
            BattleEffects.Sparkle(IntoChestPoint);
            BattleEffects.Confetti(IntoChestPoint, GoldColor);

            if (lid != null)
            {
                Quaternion open = lidClosedRotation * Quaternion.Euler(lidOpenEuler);
                yield return TreasureMotion.Rotate(lid, lid.localRotation, open, LidOpenSeconds);
            }
            else
            {
                // No separate lid: the whole chest squashes and pops.
                yield return TreasureMotion.Punch(transform, baseScale, new Vector3(1.18f, 0.8f, 1.18f), PopSquashSeconds);
            }

            BattleEffects.Sparkle(IntoChestPoint + Vector3.up * 0.2f);
            mainRoutine = null;
            opened?.Invoke();
        }

        /// <summary>The padlock jumps up and toward the camera, spinning and shrinking, then is gone.</summary>
        IEnumerator PopLockOff()
        {
            Transform padlock = lockVisual.transform;
            padlock.SetParent(null, true);
            Vector3 startPosition = padlock.position;
            Vector3 startScale = padlock.localScale;
            Quaternion startRotation = padlock.rotation;
            // The camera looks north, so "toward the camera" is south.
            Vector3 endPosition = startPosition + new Vector3(0f, 0.9f, -0.7f);

            for (float elapsed = 0f; elapsed < LockPopSeconds; elapsed += Time.deltaTime)
            {
                if (padlock == null) yield break;

                float t = elapsed / LockPopSeconds;
                padlock.position = Vector3.Lerp(startPosition, endPosition, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.35f);
                padlock.rotation = startRotation * Quaternion.Euler(0f, 0f, t * 540f);
                padlock.localScale = startScale * Mathf.Lerp(1f, 0f, t * t);
                yield return null;
            }

            if (padlock != null) lockVisual.SetActive(false);
        }

        void EnsureGlow()
        {
            if (glow == null)
            {
                var go = new GameObject("ChestGlow");
                go.transform.SetParent(transform, true);
                go.transform.position = IntoChestPoint + Vector3.up * 0.15f;
                glow = go.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = GoldColor;
                glow.range = GlowRange;
                glow.shadows = LightShadows.None;
                glow.intensity = 0f;
            }
            glow.enabled = true;
        }

        IEnumerator FadeGlow(float target, float seconds)
        {
            if (glow == null) yield break;

            float from = glow.intensity;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (glow == null) yield break;

                glow.intensity = Mathf.Lerp(from, target, elapsed / seconds);
                yield return null;
            }
            if (glow != null) glow.intensity = target;
        }

        // ----- Coins -------------------------------------------------------------------------------

        IEnumerator SpillOne(TreasureCoin coin, Vector3 target, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (coin == null) yield break;

            coin.transform.position = IntoChestPoint;
            coin.gameObject.SetActive(true);
            coin.Appear();
            BattleEffects.Sparkle(IntoChestPoint);

            Vector3 start = coin.transform.position;
            for (float elapsed = 0f; elapsed < SpillFlySeconds; elapsed += Time.deltaTime)
            {
                // Tapped in mid-air: the coin flies into the chest by itself now.
                if (coin == null || coin.Collected) yield break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / SpillFlySeconds);
                coin.transform.position = Vector3.Lerp(start, target, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * SpillArcHeight);
                yield return null;
            }

            if (coin != null && !coin.Collected) coin.transform.position = target;
        }

        /// <summary>Where coin i comes to rest: the camera-side spot from the builder, or a fan in front of the chest.</summary>
        Vector3 SpotFor(int index)
        {
            if (coinSpots != null && index < coinSpots.Length && coinSpots[index] != null)
                return coinSpots[index].position + Vector3.up * coinHeight;

            // Two rows of five, in front of the chest (south, toward the camera).
            int column = index % 5;
            int row = index / 5;
            Vector3 centre = risenPosition + new Vector3(0f, coinHeight, -1.1f - row * 0.5f);
            return centre + new Vector3((column - 2) * 0.5f, 0f, 0f);
        }

        void DestroyCoins()
        {
            foreach (TreasureCoin coin in coins)
                if (coin != null) Destroy(coin.gameObject);
            coins.Clear();
        }

        // ----- Helpers -----------------------------------------------------------------------------

        void StopMain()
        {
            if (mainRoutine == null) return;

            StopCoroutine(mainRoutine);
            mainRoutine = null;
        }

        /// <summary>Hidden while buried, so nothing can peek through the sand.</summary>
        void SetVisible(bool show)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = show;
        }

        void OnDestroy()
        {
            DestroyCoins();
        }
    }
}
