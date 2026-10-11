using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// Turns the one big button into a ray gun: tap (or hold) to zap. It aims by itself at the nearest alien
    /// that is not a friend yet, so a small thumb never has to aim. A zapped alien is not hurt: it turns into a friend.
    /// Sits on the player, switched off until the alien landing turns it on. Like PlayerBattleInput, the player is
    /// never knocked out: when slime lands on him he only wobbles for a moment.
    /// </summary>
    [RequireComponent(typeof(Hittable))]
    public class RayGunShooter : MonoBehaviour
    {
        [SerializeField] ActionButton button;
        [Tooltip("Looks for an alien to zap within this distance.")]
        [SerializeField] float aimRadius = 9f;
        [SerializeField] float cooldownSeconds = 0.35f;
        [Tooltip("Seconds from starting the shot until the beam appears.")]
        [SerializeField] float hitDelay = 0.12f;
        [Tooltip("Degrees per second when turning toward the target.")]
        [SerializeField] float turnSpeed = 900f;

        [Header("Beam")]
        [SerializeField] Material beamMaterial;
        [SerializeField] Color beamColor = new Color(0.4f, 1f, 0.9f, 1f);
        [SerializeField] float beamWidth = 0.14f;
        [SerializeField] float beamSeconds = 0.15f;
        [Tooltip("How far the beam goes when there is nothing to hit.")]
        [SerializeField] float missLength = 5f;

        [Header("Ray gun in the right hand")]
        [SerializeField] GameObject gunPrefab;
        [SerializeField] Vector3 gunLocalPosition;
        [SerializeField] Vector3 gunLocalEuler;
        [SerializeField] float gunScale = 1f;

        Hittable hittable;
        KidAnimator kidAnimator;
        PlayerController playerController;
        GameObject gun;
        bool gunShown;
        LineRenderer beam;
        Coroutine fireRoutine;
        Coroutine beamRoutine;
        float nextFireTime;

        /// <summary>Set by the alien landing during the counting and the party: the kid stands still and the button does nothing.</summary>
        public bool ControlLocked { get; set; }

        /// <summary>How many times the gun has been fired since it was switched on (for the "tap the button" hint).</summary>
        public int ShotCount { get; private set; }

        public bool CanFire => !ControlLocked && hittable.IsAlive && !hittable.IsStunned && Time.time >= nextFireTime;

        void Awake()
        {
            hittable = GetComponent<Hittable>();
            kidAnimator = GetComponent<KidAnimator>();
            playerController = GetComponent<PlayerController>();
        }

        void OnEnable()
        {
            ShotCount = 0;
            hittable.Hit += OnHit;
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Defeated -= OnDefeated;
            }

            if (fireRoutine != null) StopCoroutine(fireRoutine);
            if (beamRoutine != null) StopCoroutine(beamRoutine);
            fireRoutine = null;
            beamRoutine = null;
            if (beam != null) beam.enabled = false;

            ControlLocked = false;
            if (playerController != null) playerController.InputEnabled = true;
        }

        void OnDestroy()
        {
            if (beam != null) Destroy(beam.gameObject);
        }

        void Update()
        {
            // A bumped player stands still for a moment, then moves again.
            if (playerController != null)
                playerController.InputEnabled = !ControlLocked && !hittable.IsStunned;

            if (IsButtonHeld()) Fire();
        }

        /// <summary>Shows or hides the ray gun in the right hand. It is made the first time it is needed.</summary>
        public void ShowGun(bool show)
        {
            gunShown = show;
            if (show) CreateGunIfNeeded();
            if (gun != null) gun.SetActive(show);
        }

        /// <summary>Zaps the nearest alien that is not a friend. Returns false while resting from the last shot or while wobbling.</summary>
        public bool Fire()
        {
            if (!CanFire) return false;

            nextFireTime = Time.time + cooldownSeconds;
            ShotCount++;

            AlienKidAI target = FindTarget();
            if (kidAnimator != null) kidAnimator.PlayShoot();
            Sfx.Play("aliens.zap", transform.position);

            if (fireRoutine != null) StopCoroutine(fireRoutine);
            fireRoutine = StartCoroutine(FireRoutine(target));
            return true;
        }

        IEnumerator FireRoutine(AlienKidAI target)
        {
            float elapsed = 0f;
            while (elapsed < hitDelay)
            {
                TurnToward(target, turnSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            fireRoutine = null;

            // Bumped before the shot left the gun: it is dropped.
            if (hittable.IsStunned) yield break;

            TurnToward(target, 360f);

            Vector3 muzzle = MuzzlePosition();
            bool hasTarget = target != null && target.Hittable.IsAlive;
            Vector3 end = hasTarget
                ? target.Hittable.AimPosition
                : muzzle + FlatForward() * missLength;

            ShowBeam(muzzle, end);

            if (hasTarget)
            {
                BattleEffects.Sparkle(end);
                Sfx.Play("aliens.zap_hit", end);
                target.Hittable.TakeHit(transform.position);
            }
        }

        /// <summary>The nearest alien that has not turned friendly yet, within reach of the gun.</summary>
        AlienKidAI FindTarget()
        {
            AlienKidAI best = null;
            float bestDistance = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (other == null || !other.IsAlive || other.Team != BattleTeam.Opponents) continue;

                var alien = other.GetComponent<AlienKidAI>();
                if (alien == null || alien.IsFriend) continue;

                float distance = FlatDistance(transform.position, other.transform.position);
                if (distance > aimRadius + other.HitRadius || distance >= bestDistance) continue;

                bestDistance = distance;
                best = alien;
            }
            return best;
        }

        // ----- Beam ----------------------------------------------------------------------------

        void ShowBeam(Vector3 from, Vector3 to)
        {
            if (beam == null) beam = CreateBeam();

            beam.SetPosition(0, from);
            beam.SetPosition(1, to);

            if (beamRoutine != null) StopCoroutine(beamRoutine);
            beamRoutine = StartCoroutine(BeamRoutine());
        }

        IEnumerator BeamRoutine()
        {
            beam.enabled = true;
            for (float elapsed = 0f; elapsed < beamSeconds; elapsed += Time.deltaTime)
            {
                // Bright at first, then thinner and thinner.
                float fade = 1f - elapsed / beamSeconds;
                beam.widthMultiplier = beamWidth * Mathf.Lerp(0.3f, 1f, fade);
                yield return null;
            }

            beam.enabled = false;
            beamRoutine = null;
        }

        LineRenderer CreateBeam()
        {
            // Not a child of the player: the beam is drawn in world space and must not turn with him.
            var go = new GameObject("RayGunBeam");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.startColor = beamColor;
            line.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.6f);
            line.widthMultiplier = beamWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (beamMaterial != null) line.sharedMaterial = beamMaterial;
            line.enabled = false;
            return line;
        }

        /// <summary>Where the beam starts: the ray gun in the hand, nudged forward to its barrel.</summary>
        Vector3 MuzzlePosition()
        {
            Vector3 forward = FlatForward();
            if (gun != null && gun.activeInHierarchy) return gun.transform.position + forward * 0.25f;
            return transform.position + Vector3.up * 1f + forward * 0.5f;
        }

        // ----- The gun in the hand -------------------------------------------------------------

        void CreateGunIfNeeded()
        {
            if (gun != null || gunPrefab == null) return;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;

            gun = Instantiate(gunPrefab, hand);
            gun.transform.localPosition = gunLocalPosition;
            gun.transform.localRotation = Quaternion.Euler(gunLocalEuler);
            gun.transform.localScale = Vector3.one * gunScale;
            gun.SetActive(gunShown);

            // A toy gun must not push kids around or block the camera.
            foreach (Collider collider in gun.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }

        // ----- Wobbles -------------------------------------------------------------------------

        void OnHit(Hittable who)
        {
            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        /// <summary>The player must never be knocked out, so if this ever happens he just gets up again.</summary>
        void OnDefeated(Hittable who)
        {
            who.ResetState();
        }

        // ----- Helpers -------------------------------------------------------------------------

        bool IsButtonHeld()
        {
            if (button != null && button.IsHeld) return true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.isPressed;
        }

        Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        void TurnToward(AlienKidAI target, float maxDegrees)
        {
            if (target == null || !target.Hittable.IsAlive) return;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, maxDegrees);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
