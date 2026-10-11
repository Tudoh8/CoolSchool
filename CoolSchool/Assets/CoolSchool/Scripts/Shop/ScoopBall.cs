using System.Collections;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// One ball of ice cream: a slightly squashed sphere in its flavour. It follows the finger while it is
    /// held, and the shop flies it onto the cone or back to its tub. It has no collider, so it never blocks a tap.
    /// </summary>
    public class ScoopBall : MonoBehaviour
    {
        public const float Radius = 0.14f;
        const float SquashY = 0.9f;
        const float FollowSharpness = 18f;

        public Flavour Flavour { get; private set; }

        /// <summary>The tub it came from, where it flies back to.</summary>
        public FlavourTub Tub { get; private set; }

        Vector3 baseScale;
        Coroutine squash;

        /// <summary>Makes a new scoop at the top of the tub, popping up out of it.</summary>
        public static ScoopBall Create(FlavourTub tub)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Scoop_" + tub.Flavour;
            Destroy(sphere.GetComponent<Collider>());

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (tub.ScoopMaterial != null) renderer.sharedMaterial = tub.ScoopMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ball = sphere.AddComponent<ScoopBall>();
            ball.Flavour = tub.Flavour;
            ball.Tub = tub;
            ball.baseScale = new Vector3(Radius * 2f, Radius * 2f * SquashY, Radius * 2f);

            sphere.transform.position = tub.ScoopPoint.position;
            ball.PopUp();
            return ball;
        }

        /// <summary>Follows a point smoothly, so the scoop does not jump to the finger.</summary>
        public void Follow(Vector3 point)
        {
            float k = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, point, k);
        }

        /// <summary>A squash and stretch, for pressing, plopping onto the cone and landing back in the tub.</summary>
        public void Squash()
        {
            RunScale(ShopMotion.Punch(transform, baseScale, new Vector3(1.3f, 0.65f, 1.3f), 0.3f));
        }

        /// <summary>A little hop in place with a squash, for counting the scoops on the finished cone.</summary>
        public IEnumerator Hop(float height, float seconds)
        {
            Vector3 start = transform.localPosition;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float hump = Mathf.Sin(Mathf.Clamp01(elapsed / seconds) * Mathf.PI);
                transform.localPosition = start + Vector3.up * (hump * height);
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f - hump * 0.08f, 1f + hump * 0.2f, 1f - hump * 0.08f));
                yield return null;
            }

            transform.localPosition = start;
            transform.localScale = baseScale;
        }

        void PopUp()
        {
            transform.localScale = Vector3.zero;
            RunScale(ShopMotion.PopIn(transform, baseScale, 0.2f));
        }

        void RunScale(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;

            if (squash != null) StopCoroutine(squash);
            squash = StartCoroutine(routine);
        }
    }
}
