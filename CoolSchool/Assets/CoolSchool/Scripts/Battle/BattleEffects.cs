using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Pops the happy little particle effects: confetti when someone is bumped, sparks when a
    /// shield blocks, sparkles for imagination. One of these lives in the scene; everything else
    /// just calls the static methods. If there is no instance or no prefab, nothing happens.
    /// </summary>
    public class BattleEffects : MonoBehaviour
    {
        [SerializeField] ParticleSystem confettiPrefab;
        [SerializeField] ParticleSystem blockSparkPrefab;
        [SerializeField] ParticleSystem sparklePrefab;
        [SerializeField] ParticleSystem swooshPrefab;

        const float MaxLifetime = 6f;

        static BattleEffects instance;

        public static void Confetti(Vector3 position) => Spawn(instance != null ? instance.confettiPrefab : null, position);

        /// <summary>The same confetti pop, all in one colour (the aliens' green slime splat and friendship confetti).</summary>
        public static void Confetti(Vector3 position, Color tint) => Spawn(instance != null ? instance.confettiPrefab : null, position, tint);

        public static void BlockSpark(Vector3 position) => Spawn(instance != null ? instance.blockSparkPrefab : null, position);

        public static void Sparkle(Vector3 position) => Spawn(instance != null ? instance.sparklePrefab : null, position);

        public static void Swoosh(Vector3 position) => Spawn(instance != null ? instance.swooshPrefab : null, position);

        void Awake()
        {
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        static void Spawn(ParticleSystem prefab, Vector3 position, Color? tint = null)
        {
            if (prefab == null) return;

            ParticleSystem effect = Instantiate(prefab, position, Quaternion.identity);
            effect.gameObject.SetActive(true);

            ParticleSystem.MainModule main = effect.main;
            if (tint.HasValue)
            {
                // Two close shades of the colour, so it still looks like a handful of confetti.
                Color color = tint.Value;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.25f), Color.Lerp(color, Color.black, 0.2f));
            }
            effect.Play();

            float lifetime = main.duration + main.startLifetime.constantMax;
            Destroy(effect.gameObject, Mathf.Clamp(lifetime, 1f, MaxLifetime));
        }
    }
}
