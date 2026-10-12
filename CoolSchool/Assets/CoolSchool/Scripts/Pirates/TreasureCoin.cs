using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// A gold coin from the treasure chest. It stands on its edge facing the camera, spins slowly so the gold glints, pulses
    /// when it is the one to tap, and flies into the chest when it is collected.
    /// It is made around the Synty coin model by Create: the model is turned so its flat face points at the camera and it is
    /// scaled to a size a small finger can hit. A trigger sphere on a child gives it a generous touch area.
    /// </summary>
    public class TreasureCoin : MonoBehaviour
    {
        /// <summary>How wide a coin is, in metres, whatever size the model has.</summary>
        public const float Diameter = 0.34f;
        const float PickRadius = 0.25f;
        const float SpinDegreesPerSecond = 80f;
        // The face leans back a little, so the coin is seen slightly from above.
        const float LeanBackDegrees = 12f;
        const float PulseSpeed = 7f;
        const float PulseAmount = 0.22f;
        const float FlySeconds = 0.5f;
        const float FlyArcHeight = 0.8f;
        const float SpawnPopSeconds = 0.3f;
        const float HopSeconds = 0.4f;

        /// <summary>True once the coin has been tapped and is flying into (or is in) the chest.</summary>
        public bool Collected { get; private set; }

        Transform spinner;
        Vector3 baseScale = Vector3.one;
        float spinAngle;
        bool pulsing;
        Coroutine flyRoutine;

        /// <summary>
        /// Makes a coin from the model at the position, popping into being (unless popIn is false: then it stays invisible until
        /// Appear). The model gets a new parent, is turned so that its thinnest direction points at the camera and is scaled
        /// so that it is `diameter` metres wide.
        /// </summary>
        public static TreasureCoin Create(GameObject modelPrefab, Vector3 position, float diameter = Diameter, bool popIn = true)
        {
            var root = new GameObject("TreasureCoin");
            root.transform.position = position;
            var coin = root.AddComponent<TreasureCoin>();

            var spin = new GameObject("Spin");
            spin.transform.SetParent(root.transform, false);
            coin.spinner = spin.transform;

            if (modelPrefab != null) coin.FitModel(modelPrefab, diameter);
            else coin.MakeFallbackDisc(diameter);

            // A generous touch area; a trigger, so it never pushes anyone around.
            var pick = new GameObject("Pick");
            pick.transform.SetParent(root.transform, false);
            var sphere = pick.AddComponent<SphereCollider>();
            sphere.radius = PickRadius;
            sphere.isTrigger = true;

            coin.spinAngle = Random.value * 360f;
            coin.baseScale = Vector3.one;
            root.transform.localScale = Vector3.zero;
            if (popIn) coin.Appear();
            return coin;
        }

        /// <summary>Pops into being (from nothing, with a little overshoot). For a coin made with popIn false.</summary>
        public void Appear()
        {
            if (!isActiveAndEnabled)
            {
                transform.localScale = baseScale;
                return;
            }
            StartCoroutine(TreasureMotion.PopIn(transform, baseScale, SpawnPopSeconds));
        }

        /// <summary>Flies into the chest (an arc, getting smaller), then switches off. The callback runs when it arrives.</summary>
        public void FlyInto(Vector3 point, System.Action done)
        {
            if (Collected) return;

            Collected = true;
            pulsing = false;
            if (flyRoutine != null) StopCoroutine(flyRoutine);

            if (!isActiveAndEnabled)
            {
                done?.Invoke();
                return;
            }
            flyRoutine = StartCoroutine(FlyRoutine(point, done));
        }

        /// <summary>The coin to tap next grows and shrinks a little.</summary>
        public void Pulse(bool on)
        {
            pulsing = on;
            if (!on && !Collected) transform.localScale = baseScale;
        }

        /// <summary>A little hop on the spot (a friendly "not that one" wiggle, or a cheer). Ignored once collected.</summary>
        public void Hop(float height = 0.3f)
        {
            if (Collected || !isActiveAndEnabled) return;

            StartCoroutine(HopRoutine(height));
        }

        System.Collections.IEnumerator HopRoutine(float height)
        {
            Vector3 start = transform.position;
            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                if (Collected) yield break;

                transform.position = start + Vector3.up * (Mathf.Sin(elapsed / HopSeconds * Mathf.PI) * height);
                yield return null;
            }
            if (!Collected) transform.position = start;
        }

        void Update()
        {
            if (spinner != null)
            {
                spinAngle += SpinDegreesPerSecond * Time.deltaTime;
                spinner.localRotation = Quaternion.Euler(-LeanBackDegrees, spinAngle, 0f);
            }

            if (pulsing && !Collected)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
                transform.localScale = baseScale * (1f + beat * PulseAmount);
            }
        }

        System.Collections.IEnumerator FlyRoutine(Vector3 point, System.Action done)
        {
            Vector3 startScale = transform.localScale;
            Vector3 startPosition = transform.position;

            for (float elapsed = 0f; elapsed < FlySeconds; elapsed += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / FlySeconds);
                transform.position = Vector3.Lerp(startPosition, point, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * FlyArcHeight);
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, k);
                yield return null;
            }

            transform.position = point;
            flyRoutine = null;
            done?.Invoke();
            gameObject.SetActive(false);
        }

        // ----- Building the coin -------------------------------------------------------------------

        /// <summary>
        /// Puts the model under the spinner, turned so the coin's flat face looks along the spinner's forward (+Z, toward the
        /// viewer after the coin is turned to face the camera) and moved so the coin's middle is the spinner's origin.
        /// The coin's thin direction is found from the bounds: the shortest side of a disc is its face direction.
        /// </summary>
        void FitModel(GameObject modelPrefab, float diameter)
        {
            GameObject model = Instantiate(modelPrefab, spinner);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }

            // The spinner must sit at the root's origin with no turn or scale for the measuring below to be simple.
            spinner.localPosition = Vector3.zero;
            spinner.localRotation = Quaternion.identity;
            spinner.localScale = Vector3.one;
            transform.localScale = Vector3.one;

            if (!TreasureMotion.TryGetBounds(model, out Bounds first)) return;

            Vector3 size = first.size;
            Vector3 thin = Vector3.forward;
            if (size.x <= size.y && size.x <= size.z) thin = Vector3.right;
            else if (size.y <= size.x && size.y <= size.z) thin = Vector3.up;

            model.transform.localRotation = Quaternion.FromToRotation(thin, Vector3.forward);

            TreasureMotion.TryGetBounds(model, out Bounds turned);
            float widest = Mathf.Max(turned.size.x, turned.size.y);
            if (widest > 0.0001f) model.transform.localScale *= diameter / widest;

            TreasureMotion.TryGetBounds(model, out Bounds scaled);
            model.transform.position += transform.position - scaled.center;
        }

        /// <summary>No model given: a plain gold disc, so the game still works.</summary>
        void MakeFallbackDisc(float diameter)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "FallbackCoin";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(spinner, false);
            // A cylinder stands along Y; turn it so the round face looks along Z.
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(diameter, diameter * 0.08f, diameter);

            var renderer = disc.GetComponent<MeshRenderer>();
            renderer.material.color = new Color(1f, 0.78f, 0.16f, 1f);
        }
    }
}
