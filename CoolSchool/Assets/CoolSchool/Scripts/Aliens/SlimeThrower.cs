using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// Lobs a slime ball in a slow arc. It is always clear where it will land: a green circle appears on the ground
    /// first and fills up while the ball is in the air, so there is plenty of time to step out of it.
    /// Slime only makes a heroes' wobble (a Hittable hit); nobody is hurt, and a shield does not matter here.
    /// </summary>
    public class SlimeThrower : MonoBehaviour
    {
        [Tooltip("Where the ball leaves the alien. Optional: without it the ball starts at the alien's chest.")]
        [SerializeField] Transform hand;
        [SerializeField] Material slimeMaterial;
        [SerializeField] Material markerMaterial;
        [SerializeField] Material trailMaterial;
        [Tooltip("The alien swings its arm for this long before the ball leaves.")]
        [SerializeField] float windUpSeconds = 0.5f;
        [Tooltip("How long the ball is in the air.")]
        [SerializeField] float flightSeconds = 1f;
        [SerializeField] float arcHeight = 2.2f;
        [Tooltip("How big the circle on the ground is.")]
        [SerializeField] float markerRadius = 1.1f;
        [Tooltip("0 aims where the target is now, 1 aims where it will be if it keeps walking.")]
        [SerializeField, Range(0f, 1f)] float leadFactor = 0.5f;
        [SerializeField] float ballSize = 0.35f;

        static readonly Color SlimeGreen = new Color(0.35f, 0.9f, 0.25f, 1f);

        readonly List<GameObject> spawned = new List<GameObject>();
        Coroutine throwRoutine;

        public bool IsThrowing => throwRoutine != null;

        void OnDisable()
        {
            CancelThrow();
        }

        /// <summary>Throws a slime ball at the target. Does nothing while a throw is already going on.</summary>
        public void Throw(Transform target)
        {
            if (target == null || throwRoutine != null || !isActiveAndEnabled) return;

            throwRoutine = StartCoroutine(ThrowRoutine(target));
        }

        /// <summary>Stops a throw in progress and removes the ball and the circle (the alien turned friendly).</summary>
        public void CancelThrow()
        {
            if (throwRoutine != null) StopCoroutine(throwRoutine);
            throwRoutine = null;
            DestroySpawned();
        }

        IEnumerator ThrowRoutine(Transform target)
        {
            // Where the ball will land: ahead of a walking target, on the ground.
            Vector3 impact = GroundPoint(target.position + VelocityOf(target) * ((windUpSeconds + flightSeconds) * leadFactor));

            GameObject marker = CreateMarker(impact, out Transform fill);
            spawned.Add(marker);

            // The telegraph: the circle grows in while the alien swings its arm back.
            float total = windUpSeconds + flightSeconds;
            for (float elapsed = 0f; elapsed < windUpSeconds; elapsed += Time.deltaTime)
            {
                SetFill(fill, elapsed / total);
                yield return null;
            }

            Sfx.Play("aliens.slime_throw", transform.position);
            GameObject ball = CreateBall();
            spawned.Add(ball);
            Vector3 start = LaunchPoint();

            for (float elapsed = 0f; elapsed < flightSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / flightSeconds;
                Vector3 position = Vector3.Lerp(start, impact, t);
                position.y += arcHeight * 4f * t * (1f - t);
                ball.transform.position = position;
                SetFill(fill, (windUpSeconds + elapsed) / total);
                yield return null;
            }

            Splat(impact);
            DestroySpawned();
            throwRoutine = null;
        }

        /// <summary>Heroes standing in the circle get bumped: a wobble and confetti, never worse.</summary>
        void Splat(Vector3 impact)
        {
            BattleEffects.Confetti(impact + Vector3.up * 0.2f, SlimeGreen);
            Sfx.Play("aliens.slime_splat", impact);

            // Copy first: a hit can switch things on or off while we are looping.
            var heroes = new List<Hittable>(Hittable.All);
            foreach (Hittable hero in heroes)
            {
                if (hero == null || hero.Team != BattleTeam.Heroes || !hero.IsAlive) continue;

                Vector3 offset = hero.transform.position - impact;
                offset.y = 0f;
                if (offset.magnitude <= markerRadius + hero.HitRadius) hero.TakeHit(impact);
            }
        }

        // ----- Things that fly and glow --------------------------------------------------------

        GameObject CreateBall()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "SlimeBall";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.position = LaunchPoint();
            ball.transform.localScale = Vector3.one * ballSize;

            var renderer = ball.GetComponent<MeshRenderer>();
            if (slimeMaterial != null) renderer.sharedMaterial = slimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.widthMultiplier = ballSize * 0.8f;
            trail.startColor = SlimeGreen;
            trail.endColor = new Color(SlimeGreen.r, SlimeGreen.g, SlimeGreen.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            return ball;
        }

        /// <summary>A flat green circle on the ground, with a second disc inside it that grows to fill it (read it as a timer).</summary>
        GameObject CreateMarker(Vector3 position, out Transform fill)
        {
            var root = new GameObject("SlimeMarker");
            root.transform.position = position + Vector3.up * 0.04f;

            CreateDisc("Outline", root.transform, markerRadius * 2f, 0f);
            fill = CreateDisc("Fill", root.transform, markerRadius * 2f, 0.01f);
            SetFill(fill, 0f);
            return root;
        }

        Transform CreateDisc(string discName, Transform parent, float diameter, float lift)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = discName;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, lift, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);

            var renderer = disc.GetComponent<MeshRenderer>();
            if (markerMaterial != null) renderer.sharedMaterial = markerMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return disc.transform;
        }

        /// <summary>The inner disc is 15% to 100% of the circle's size.</summary>
        void SetFill(Transform fill, float progress)
        {
            if (fill == null) return;

            float diameter = markerRadius * 2f * Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(progress));
            fill.localScale = new Vector3(diameter, 0.01f, diameter);
        }

        void DestroySpawned()
        {
            foreach (GameObject item in spawned)
                if (item != null) Destroy(item);
            spawned.Clear();
        }

        // ----- Helpers -------------------------------------------------------------------------

        Vector3 LaunchPoint()
        {
            return hand != null ? hand.position : transform.position + Vector3.up * 1f + transform.forward * 0.3f;
        }

        /// <summary>The point on the ground this alien stands on (the playground is flat).</summary>
        Vector3 GroundPoint(Vector3 point)
        {
            point.y = transform.position.y;
            return point;
        }

        /// <summary>How fast the target is walking, from its controller or agent. Zero if it has neither.</summary>
        static Vector3 VelocityOf(Transform target)
        {
            Vector3 velocity = Vector3.zero;

            var body = target.GetComponent<CharacterController>();
            if (body != null) velocity = body.velocity;

            var agent = target.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled) velocity = agent.velocity;

            velocity.y = 0f;
            return velocity;
        }
    }
}
