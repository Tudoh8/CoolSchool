using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The cannon at the bow of the ship. While it is armed, every tap on the action button (or space) shoots a small
    /// confetti ball in a high arc at the nearest balloon on the skeleton boat. The ball always hits: nobody has to aim.
    /// </summary>
    public class ShipCannon : MonoBehaviour
    {
        [SerializeField] ActionButton button;
        [SerializeField] Transform muzzle;
        [Tooltip("Slides back a little with every shot.")]
        [SerializeField] Transform barrel;
        [SerializeField] SkeletonBoat targets;
        [SerializeField] float cooldown = 0.45f;
        [SerializeField] float flightSeconds = 0.7f;
        [Header("Look")]
        [SerializeField] float ballSize = 0.25f;
        [SerializeField] float arcHeight = 1.8f;
        [SerializeField] float recoilMeters = 0.15f;

        /// <summary>Taps do something only while this is on.</summary>
        public bool Armed
        {
            get => armed;
            set
            {
                armed = value;
                if (!armed) tapQueued = false;
            }
        }

        /// <summary>Raised when a ball has hit and the balloon has popped.</summary>
        public event Action<BalloonTarget> Popped;

        bool armed;
        bool tapQueued;
        float nextFireTime;
        Vector3 barrelBase;
        bool barrelCached;
        Coroutine recoilRoutine;
        readonly HashSet<BalloonTarget> claimed = new HashSet<BalloonTarget>();
        readonly List<GameObject> ballPool = new List<GameObject>();

        void OnEnable()
        {
            if (button != null) button.Pressed += OnButtonPressed;
        }

        void OnDisable()
        {
            if (button != null) button.Pressed -= OnButtonPressed;
            tapQueued = false;
            claimed.Clear();
            RestoreBarrel();
            foreach (GameObject ball in ballPool)
                if (ball != null) ball.SetActive(false);
        }

        void OnButtonPressed()
        {
            if (armed) tapQueued = true;
        }

        void Update()
        {
            if (!armed) return;

            bool tapped = tapQueued;
            tapQueued = false;
            if (!tapped && !CoolSchool.Parent.ParentGate.InputBlocked && KeyOrPadPressed()) tapped = true;

            if (tapped && Time.time >= nextFireTime) Fire();
        }

        static bool KeyOrPadPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        // ----- Shooting ----------------------------------------------------------------------------

        void Fire()
        {
            if (targets == null) targets = FindAnyObjectByType<SkeletonBoat>(FindObjectsInactive.Include);

            BalloonTarget target = NearestFreeBalloon();
            if (target == null) return;

            nextFireTime = Time.time + cooldown;
            claimed.Add(target);

            Vector3 from = MuzzlePosition();
            Sfx.Play("pirates.cannon", from);
            BattleEffects.Confetti(from, new Color(0.9f, 0.9f, 0.9f));
            BattleEffects.Sparkle(from);
            Recoil();

            StartCoroutine(FlyBall(target));
        }

        /// <summary>The closest balloon that is not popped and not already being shot at.</summary>
        BalloonTarget NearestFreeBalloon()
        {
            if (targets == null) return null;

            Vector3 from = MuzzlePosition();
            BalloonTarget nearest = null;
            float best = float.MaxValue;
            foreach (BalloonTarget balloon in targets.ActiveBalloons)
            {
                if (balloon == null || balloon.IsPopped || claimed.Contains(balloon)) continue;

                float distance = (balloon.AimPoint - from).sqrMagnitude;
                if (distance >= best) continue;

                best = distance;
                nearest = balloon;
            }
            return nearest;
        }

        IEnumerator FlyBall(BalloonTarget target)
        {
            GameObject ball = GetBall(target.BalloonColor);
            Vector3 start = MuzzlePosition();
            ball.transform.position = start;
            ball.SetActive(true);

            float duration = Mathf.Max(0.1f, flightSeconds);
            float nextSparkle = 0f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float k = elapsed / duration;
                // The balloon keeps drifting, so the end of the arc is looked up again every frame: it always hits.
                Vector3 end = target.AimPoint;
                ball.transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arcHeight);

                if (Time.time >= nextSparkle)
                {
                    nextSparkle = Time.time + 0.12f;
                    BattleEffects.Sparkle(ball.transform.position);
                }
                yield return null;
            }

            ball.SetActive(false);
            claimed.Remove(target);
            if (target.IsPopped) yield break;

            target.Pop();
            Popped?.Invoke(target);
        }

        // ----- The ball and the recoil -------------------------------------------------------------

        /// <summary>A small coloured sphere with a trail. Balls are made once and reused.</summary>
        GameObject GetBall(Color color)
        {
            GameObject ball = null;
            foreach (GameObject candidate in ballPool)
            {
                if (candidate != null && !candidate.activeSelf)
                {
                    ball = candidate;
                    break;
                }
            }
            if (ball == null)
            {
                ball = CreateBall();
                ballPool.Add(ball);
            }

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.5f);

            var rendererOfBall = ball.GetComponent<MeshRenderer>();
            if (rendererOfBall != null) rendererOfBall.SetPropertyBlock(block);

            var trail = ball.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.SetPropertyBlock(block);
                trail.Clear();
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
            return ball;
        }

        GameObject CreateBall()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "CannonBall";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * ballSize;
            ball.SetActive(false);

            // Same material as the balloons, so it needs no material of its own.
            var rendererOfBall = ball.GetComponent<Renderer>();
            Material material = SharedBalloonMaterial();
            if (material != null) rendererOfBall.sharedMaterial = material;

            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.widthMultiplier = ballSize;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.sharedMaterial = material != null ? material : rendererOfBall.sharedMaterial;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            return ball;
        }

        Material SharedBalloonMaterial()
        {
            if (targets == null) return null;

            foreach (BalloonTarget balloon in targets.GetComponentsInChildren<BalloonTarget>(true))
            {
                Renderer found = balloon.GetComponentInChildren<Renderer>(true);
                if (found != null && found.sharedMaterial != null) return found.sharedMaterial;
            }
            return null;
        }

        Vector3 MuzzlePosition()
        {
            return muzzle != null ? muzzle.position : transform.position + transform.forward * 0.6f + Vector3.up * 0.4f;
        }

        void Recoil()
        {
            if (barrel == null) return;

            if (!barrelCached)
            {
                barrelBase = barrel.localPosition;
                barrelCached = true;
            }
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(RecoilRoutine());
        }

        IEnumerator RecoilRoutine()
        {
            // "Back" is away from the muzzle, whichever way the barrel model points.
            Vector3 away = muzzle != null ? (barrel.position - muzzle.position).normalized : -barrel.forward;
            Transform parent = barrel.parent;
            Vector3 localAway = parent != null ? parent.InverseTransformVector(away * recoilMeters) : away * recoilMeters;

            const float outSeconds = 0.06f;
            const float backSeconds = 0.3f;
            for (float elapsed = 0f; elapsed < outSeconds; elapsed += Time.deltaTime)
            {
                barrel.localPosition = barrelBase + localAway * (elapsed / outSeconds);
                yield return null;
            }
            for (float elapsed = 0f; elapsed < backSeconds; elapsed += Time.deltaTime)
            {
                barrel.localPosition = barrelBase + localAway * (1f - Mathf.SmoothStep(0f, 1f, elapsed / backSeconds));
                yield return null;
            }

            barrel.localPosition = barrelBase;
            recoilRoutine = null;
        }

        void RestoreBarrel()
        {
            recoilRoutine = null;
            if (barrel != null && barrelCached) barrel.localPosition = barrelBase;
        }
    }
}
