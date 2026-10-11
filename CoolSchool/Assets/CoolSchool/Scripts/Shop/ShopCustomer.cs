using System.Collections;
using System.Collections.Generic;
using CoolSchool.Characters;
using UnityEngine;

namespace CoolSchool.Shop
{
    public enum CustomerKind { Kid, Knight, Alien, Dragon }

    /// <summary>
    /// One customer of the ice cream shop: a kid, a knight, an alien or the little dragon.
    /// It walks in a straight line by transform (no NavMeshAgent, no colliders), so it never pushes the player and never
    /// blocks a tap. Kids, knights and aliens use the Kid animator (KidAnimator drives Speed from how far they move);
    /// the dragon uses Dragon.controller and gets its Speed from here. A missing animator parameter is quietly ignored.
    /// Talking, paying and the cheer fallback are small squash-and-bounce moves on the model, because the animator
    /// controllers have no talk or reach clips.
    /// </summary>
    public class ShopCustomer : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CheerId = Animator.StringToHash("Cheer");
        static readonly int HappyId = Animator.StringToHash("Happy");

        const float ArriveDistance = 0.02f;
        const float TalkSeconds = 1.6f;
        const float CheerHopSeconds = 0.9f;
        const float CoinSeconds = 0.8f;
        const float ConeSettleSeconds = 0.25f;

        [SerializeField] CustomerKind kind;
        [SerializeField] Animator animator;
        [Tooltip("Present on kids, knights and aliens: it turns how far they move into the walk and run animation.")]
        [SerializeField] KidAnimator kidAnimator;
        [Tooltip("The model that squashes and bounces for the talk, coin and cheer moves.")]
        [SerializeField] Transform body;

        [Header("Points")]
        [Tooltip("Where a held cone sits. It follows the hand bone and always stays upright.")]
        [SerializeField] Transform handPoint;
        [Tooltip("A point just above the head, for the order bubble.")]
        [SerializeField] Transform bubbleAnchor;

        [Header("Hand")]
        [Tooltip("The right hand bone (the head bone for the dragon). Empty: the hand point stays where it is.")]
        [SerializeField] Transform handBone;
        [Tooltip("Where the hand point sits, in the bone's own space (the palm, or the front of the dragon's mouth).")]
        [SerializeField] Vector3 handBoneOffset;
        [Tooltip("An extra nudge in the customer's own space (x right, y up, z forward).")]
        [SerializeField] Vector3 handRootOffset;

        [Header("Moving")]
        [SerializeField] float walkSpeed = 1.6f;
        [Tooltip("Degrees per second when turning.")]
        [SerializeField] float turnSpeed = 420f;

        readonly HashSet<int> parameterIds = new HashSet<int>();
        RuntimeAnimatorController cachedController;
        Coroutine walkRoutine;
        Coroutine gestureRoutine;
        Transform heldCone;

        bool poseCaptured;
        Vector3 bodyPosition;
        Quaternion bodyRotation = Quaternion.identity;
        Vector3 bodyScale = Vector3.one;

        public CustomerKind Kind => kind;

        /// <summary>Where a held cone sits: the hand, or just in front of the dragon's mouth.</summary>
        public Transform HandPoint => handPoint != null ? handPoint : transform;

        /// <summary>A point just above the head, for the order bubble.</summary>
        public Transform BubbleAnchor => bubbleAnchor != null ? bubbleAnchor : transform;

        /// <summary>True until WalkTo or Leave has arrived.</summary>
        public bool IsWalking { get; private set; }

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (kidAnimator == null) kidAnimator = GetComponent<KidAnimator>();
            if (body == null) body = animator != null ? animator.transform : null;

            // The dragon's walk clip also moves its own body forward; this pins it in place and matches the stride to walkSpeed.
            // It does nothing for models without a DragonRoot bone (kids, knights, aliens).
            if (kidAnimator == null) DragonRootLock.Ensure(animator, walkSpeed);

            CapturePose();
        }

        void LateUpdate()
        {
            // The cone follows the hand but stays upright, whatever the wrist does.
            if (handPoint == null || handBone == null) return;

            Vector3 position = handBone.TransformPoint(handBoneOffset) + transform.TransformVector(handRootOffset);
            handPoint.SetPositionAndRotation(position, transform.rotation);
        }

        // ----- Coming and going ----------------------------------------------------------------

        /// <summary>Switches the customer on at the position, standing still and facing lookAt.</summary>
        public void Appear(Vector3 position, Vector3 lookAt)
        {
            StopMoving();
            transform.position = position;
            FaceInstantly(lookAt);

            gameObject.SetActive(true);
            RestoreBodyPose();
            SetSpeed(0f);
        }

        /// <summary>Walks in a straight line to the position, then turns to face lookAt and stands still.</summary>
        public void WalkTo(Vector3 position, Vector3 lookAt)
        {
            StartWalk(position, lookAt, true, false);
        }

        /// <summary>Walks to the exit, then switches off.</summary>
        public void Leave(Vector3 exit)
        {
            StartWalk(exit, exit, false, true);
        }

        /// <summary>Switches the customer off and destroys the cone it holds.</summary>
        public void Hide()
        {
            StopMoving();
            if (heldCone != null) Destroy(heldCone.gameObject);
            heldCone = null;

            RestoreBodyPose();
            gameObject.SetActive(false);
        }

        void StartWalk(Vector3 target, Vector3 lookAt, bool faceAtEnd, bool hideAtEnd)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (walkRoutine != null) StopCoroutine(walkRoutine);
            walkRoutine = StartCoroutine(WalkRoutine(target, lookAt, faceAtEnd, hideAtEnd));
        }

        IEnumerator WalkRoutine(Vector3 target, Vector3 lookAt, bool faceAtEnd, bool hideAtEnd)
        {
            IsWalking = true;
            SetSpeed(walkSpeed);

            while (FlatDistance(transform.position, target) > ArriveDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
                TurnToward(target - transform.position);
                yield return null;
            }

            transform.position = target;
            IsWalking = false;
            SetSpeed(0f);

            if (hideAtEnd)
            {
                walkRoutine = null;
                Hide();
                yield break;
            }

            // Arrived: now turn to face where the customer should look. IsWalking is already false.
            if (faceAtEnd)
            {
                Vector3 toLook = lookAt - transform.position;
                toLook.y = 0f;
                while (toLook.sqrMagnitude > 0.0001f && !TurnToward(toLook))
                    yield return null;
            }
            walkRoutine = null;
        }

        /// <summary>Turns a little toward the flat direction. Returns true once it faces that way.</summary>
        bool TurnToward(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, wanted) < 0.5f;
        }

        void FaceInstantly(Vector3 lookAt)
        {
            Vector3 direction = lookAt - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        void StopMoving()
        {
            StopAllCoroutines();
            walkRoutine = null;
            gestureRoutine = null;
            IsWalking = false;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ----- Gestures ------------------------------------------------------------------------

        /// <summary>A short nod-and-bounce while the customer's line plays.</summary>
        public void Talk()
        {
            StartGesture(TalkRoutine());
        }

        /// <summary>Happy jump or clap: the cheer clip for kids, the happy move for the dragon, else a hop.</summary>
        public void Cheer()
        {
            if (TrySetTrigger(CheerId) || TrySetTrigger(HappyId)) return;

            StartGesture(HopRoutine());
        }

        /// <summary>A small reach forward, as if putting a coin on the counter.</summary>
        public void PutCoin()
        {
            StartGesture(ReachRoutine());
        }

        IEnumerator TalkRoutine()
        {
            float time = 0f;
            while (time < TalkSeconds)
            {
                time += Time.deltaTime;
                float fade = Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / TalkSeconds));
                float beat = Mathf.Sin(time * Mathf.PI * 2f * 2.2f);
                float nod = Mathf.Sin(time * Mathf.PI * 2f * 1.1f);
                float sway = Mathf.Sin(time * Mathf.PI * 2f * 0.8f);

                if (body != null)
                {
                    body.localScale = Vector3.Scale(bodyScale, new Vector3(1f - 0.025f * beat * fade, 1f + 0.05f * beat * fade, 1f - 0.025f * beat * fade));
                    body.localPosition = bodyPosition + Vector3.up * (0.03f * Mathf.Abs(beat) * fade);
                    body.localRotation = bodyRotation * Quaternion.Euler(5f * nod * fade, 0f, 4f * sway * fade);
                }
                yield return null;
            }
        }

        IEnumerator HopRoutine()
        {
            float time = 0f;
            while (time < CheerHopSeconds)
            {
                time += Time.deltaTime;
                float phase = Mathf.Clamp01(time / CheerHopSeconds);
                float hop = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f));
                float squash = 1f - 0.08f * (1f - hop) * Mathf.Sin(phase * Mathf.PI);

                if (body != null)
                {
                    body.localPosition = bodyPosition + Vector3.up * (0.3f * hop);
                    body.localScale = Vector3.Scale(bodyScale, new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash)));
                }
                yield return null;
            }
        }

        IEnumerator ReachRoutine()
        {
            float time = 0f;
            while (time < CoinSeconds)
            {
                time += Time.deltaTime;
                float reach = Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / CoinSeconds));

                if (body != null)
                {
                    body.localRotation = bodyRotation * Quaternion.Euler(14f * reach, 0f, 0f);
                    body.localPosition = bodyPosition + Vector3.forward * (0.1f * reach);
                }
                yield return null;
            }
        }

        void StartGesture(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;

            if (gestureRoutine != null) StopCoroutine(gestureRoutine);
            RestoreBodyPose();
            gestureRoutine = StartCoroutine(RunGesture(routine));
        }

        IEnumerator RunGesture(IEnumerator routine)
        {
            yield return routine;
            RestoreBodyPose();
            gestureRoutine = null;
        }

        void CapturePose()
        {
            if (poseCaptured || body == null) return;

            poseCaptured = true;
            bodyPosition = body.localPosition;
            bodyRotation = body.localRotation;
            bodyScale = body.localScale;
        }

        void RestoreBodyPose()
        {
            if (!poseCaptured || body == null) return;

            body.localPosition = bodyPosition;
            body.localRotation = bodyRotation;
            body.localScale = bodyScale;
        }

        // ----- The cone ------------------------------------------------------------------------

        /// <summary>
        /// Takes the cone into the hand (in front of the mouth for the dragon). It keeps its world scale and
        /// settles smoothly into place. There is no lick or eat clip, so the customer just holds it.
        /// </summary>
        public void HoldCone(Transform cone)
        {
            if (cone == null) return;

            heldCone = cone;
            Vector3 worldScale = cone.lossyScale;
            Transform anchor = HandPoint;
            cone.SetParent(anchor, true);

            Vector3 parentScale = anchor.lossyScale;
            cone.localScale = new Vector3(
                parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
                parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
                parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);

            if (isActiveAndEnabled) StartCoroutine(SettleCone(cone));
            else
            {
                cone.localPosition = Vector3.zero;
                cone.localRotation = Quaternion.identity;
            }
        }

        IEnumerator SettleCone(Transform cone)
        {
            Vector3 startPosition = cone.localPosition;
            Quaternion startRotation = cone.localRotation;

            float time = 0f;
            while (time < ConeSettleSeconds && cone != null && cone.parent == HandPoint)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / ConeSettleSeconds));
                cone.localPosition = Vector3.Lerp(startPosition, Vector3.zero, t);
                cone.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, t);
                yield return null;
            }

            if (cone != null && cone.parent == HandPoint)
            {
                cone.localPosition = Vector3.zero;
                cone.localRotation = Quaternion.identity;
            }
        }

        // ----- Animator helpers: a missing parameter or controller is simply ignored -----------

        /// <summary>The dragon needs its Speed set from here. Kids get theirs from KidAnimator.</summary>
        void SetSpeed(float value)
        {
            if (kidAnimator != null) return;
            if (HasParameter(SpeedId)) animator.SetFloat(SpeedId, value);
        }

        bool TrySetTrigger(int id)
        {
            if (!HasParameter(id)) return false;

            animator.SetTrigger(id);
            return true;
        }

        bool HasParameter(int id)
        {
            if (animator == null || !animator.isActiveAndEnabled) return false;
            if (animator.runtimeAnimatorController == null) return false;

            if (animator.runtimeAnimatorController != cachedController)
            {
                cachedController = animator.runtimeAnimatorController;
                parameterIds.Clear();
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    parameterIds.Add(parameter.nameHash);
            }
            return parameterIds.Contains(id);
        }
    }
}
