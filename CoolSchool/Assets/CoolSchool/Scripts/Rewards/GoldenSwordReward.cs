using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// The big "you won a golden sword" moment. A shiny golden sword pops in with a sparkle, floats and spins
    /// slowly while the teacher talks, and then flies to the golden sword badge next to the star jar and shrinks away.
    /// One of these lives in the scene. A lesson calls Begin, says its line, then calls Finish and waits for IsPlaying
    /// to turn false. The animation runs on this object, so it finishes properly even if the lesson ends early.
    /// </summary>
    public class GoldenSwordReward : MonoBehaviour
    {
        [Tooltip("The sword model, the same one the knights hold.")]
        [SerializeField] GameObject swordPrefab;
        [SerializeField] Color glow = new Color(1.6f, 1.1f, 0.2f, 1f);
        [Tooltip("How long the sword looks in metres, whatever the size of the model.")]
        [SerializeField] float displayLength = 0.9f;
        [SerializeField] float spinDegreesPerSecond = 100f;

        const float PopSeconds = 0.5f;
        const float MinShowSeconds = 1.4f;
        const float MaxShowSeconds = 14f;
        const float FlightSeconds = 0.95f;
        const float EndScale = 0.12f;
        const float BobMeters = 0.05f;
        const float ArcMeters = 0.6f;
        const float TiltDegrees = 15f;

        static GoldenSwordReward instance;

        Coroutine routine;
        bool finishRequested;
        bool badgeHeld;

        /// <summary>True from Begin until the sword has flown into the badge.</summary>
        public static bool IsPlaying => instance != null && instance.routine != null;

        /// <summary>
        /// The sword pops in at this world position. With holdBadge, the HUD badge stays hidden until the sword lands
        /// in it (use that when the sword is brand new; leave it off when the badge is already showing).
        /// </summary>
        public static void Begin(Vector3 worldPosition, bool holdBadge)
        {
            if (instance == null || instance.routine != null) return;

            instance.finishRequested = false;
            instance.routine = instance.StartCoroutine(instance.Run(worldPosition, holdBadge));
        }

        /// <summary>Time to fly to the badge. Does nothing when no sword is showing.</summary>
        public static void Finish()
        {
            if (instance != null) instance.finishRequested = true;
        }

        void OnEnable()
        {
            instance = this;
        }

        void OnDisable()
        {
            if (instance == this) instance = null;
            ReleaseBadge(false);
            routine = null;
        }

        IEnumerator Run(Vector3 position, bool holdBadge)
        {
            GoldenSwordBadge badge = GoldenSwordBadge.Instance;
            if (holdBadge && badge != null)
            {
                badge.HoldBack(true);
                badgeHeld = true;
            }

            Transform pivot = BuildSword(position);
            if (pivot != null)
            {
                Sfx.Play("reward.item_reveal", position);
                BattleEffects.Sparkle(position);
                BattleEffects.Confetti(position);

                yield return PopIn(pivot, position);
                yield return Spin(pivot, position);
                yield return FlyToBadge(pivot);

                DestroySword(pivot);
                Sfx.Play("reward.star_land");
            }

            ReleaseBadge(true);
            routine = null;
        }

        // ----- The sword ----------------------------------------------------------------------

        /// <summary>A golden copy of the knights' sword, centred on a pivot, standing upright and a little tilted.</summary>
        Transform BuildSword(Vector3 position)
        {
            if (swordPrefab == null) return null;

            var pivot = new GameObject("GoldenSwordReward").transform;
            pivot.position = position;
            pivot.localScale = Vector3.zero;

            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(pivot, false);
            tilt.localRotation = Quaternion.Euler(0f, 0f, TiltDegrees);

            GameObject model = Instantiate(swordPrefab);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }

            // Measure at the origin, then turn the longest side upright and put the middle of the sword on the pivot.
            Bounds bounds = MeasureBounds(model);
            Vector3 size = bounds.size;
            Quaternion upright = Quaternion.identity;
            if (size.x > size.y && size.x >= size.z) upright = Quaternion.Euler(0f, 0f, 90f);
            else if (size.z > size.y && size.z > size.x) upright = Quaternion.Euler(90f, 0f, 0f);

            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = longest > 0.0001f ? displayLength / longest : 1f;

            model.transform.SetParent(tilt, false);
            model.transform.localRotation = upright;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -(upright * bounds.center) * scale;

            WeaponHolder.ApplyGlow(model, glow);
            AddShine(pivot);
            return pivot;
        }

        static Bounds MeasureBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>A soft golden light, so the sword also shines on the table and the teacher.</summary>
        static void AddShine(Transform pivot)
        {
            var lightObject = new GameObject("Shine");
            lightObject.transform.SetParent(pivot, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, -0.5f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.3f);
            light.intensity = 2f;
            light.range = 3f;
            light.shadows = LightShadows.None;
        }

        static void DestroySword(Transform pivot)
        {
            if (pivot == null) return;

            // The golden look lives on copies of the materials; throw them away with the sword.
            foreach (Renderer renderer in pivot.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.materials)
                    Destroy(material);
            Destroy(pivot.gameObject);
        }

        // ----- The moments --------------------------------------------------------------------

        IEnumerator PopIn(Transform pivot, Vector3 position)
        {
            for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.deltaTime)
            {
                if (pivot == null) yield break;

                float k = elapsed / PopSeconds;
                pivot.localScale = Vector3.one * EaseOutBack(k);
                pivot.position = position;
                pivot.Rotate(0f, spinDegreesPerSecond * 2f * Time.deltaTime, 0f, Space.World);
                yield return null;
            }

            if (pivot != null) pivot.localScale = Vector3.one;
        }

        /// <summary>Floats and spins until the line is done (but at least a moment, and never forever).</summary>
        IEnumerator Spin(Transform pivot, Vector3 position)
        {
            float started = Time.time;
            while (pivot != null && Time.time - started < MaxShowSeconds)
            {
                if (finishRequested && Time.time - started >= MinShowSeconds) break;

                pivot.position = position + Vector3.up * (Mathf.Sin((Time.time - started) * 3f) * BobMeters);
                pivot.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
                yield return null;
            }
        }

        /// <summary>Flies in an arc to the badge's spot on the screen, shrinking and still spinning.</summary>
        IEnumerator FlyToBadge(Transform pivot)
        {
            Camera view = Camera.main;
            if (pivot == null || view == null) yield break;

            Sfx.Play("reward.star_fly");
            Vector3 from = pivot.position;
            float depth = Mathf.Max(0.5f, Vector3.Dot(from - view.transform.position, view.transform.forward));

            for (float elapsed = 0f; elapsed < FlightSeconds; elapsed += Time.deltaTime)
            {
                if (pivot == null) yield break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / FlightSeconds);
                Vector3 to = view.ScreenToWorldPoint(new Vector3(BadgeScreenPoint().x, BadgeScreenPoint().y, depth));
                Vector3 control = (from + to) * 0.5f + view.transform.up * ArcMeters;

                pivot.position = Quadratic(from, control, to, k);
                pivot.localScale = Vector3.one * Mathf.Lerp(1f, EndScale, k);
                pivot.Rotate(0f, spinDegreesPerSecond * 3f * Time.deltaTime, 0f, Space.World);
                yield return null;
            }
        }

        static Vector2 BadgeScreenPoint()
        {
            GoldenSwordBadge badge = GoldenSwordBadge.Instance;
            return badge != null ? badge.ScreenPosition : new Vector2(Screen.width * 0.85f, Screen.height * 0.88f);
        }

        /// <summary>Lets the badge show again and gives it a bounce, because the sword just landed in it.</summary>
        void ReleaseBadge(bool pop)
        {
            GoldenSwordBadge badge = GoldenSwordBadge.Instance;
            if (badgeHeld && badge != null) badge.HoldBack(false);
            if (pop && badge != null) badge.Pop();
            badgeHeld = false;
        }

        static float EaseOutBack(float k)
        {
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            float t = Mathf.Clamp01(k) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        static Vector3 Quadratic(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }
    }
}
