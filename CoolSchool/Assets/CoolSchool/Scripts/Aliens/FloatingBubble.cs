using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A see-through bubble with the baby alien inside. It bobs up and down and circles slowly over the open-field
    /// trampoline. The kid bounces up and pops it from inside (any touch within a couple of metres in the air), and the
    /// baby grabs on. If nobody pops it, it sinks to the ground by itself and pops there.
    /// </summary>
    public class FloatingBubble : MonoBehaviour
    {
        [Tooltip("Where the baby sits: the middle of the bubble. Optional: a child called Seat is made when empty.")]
        [SerializeField] Transform seat;
        [Tooltip("The transparent sphere. Optional: every renderer below this object is used when empty.")]
        [SerializeField] GameObject visual;
        [Tooltip("How close (metres) the kid must be to the bubble's middle, in the air, to pop it.")]
        [SerializeField] float popRadius = 1.8f;
        [SerializeField] float bobHeight = 0.5f;
        [SerializeField] float bobSpeed = 1.6f;
        [Tooltip("Radius of the slow circle it drifts around.")]
        [SerializeField] float circleRadius = 1f;
        [Tooltip("Radians per second.")]
        [SerializeField] float circleSpeed = 0.5f;
        [SerializeField] Color popConfettiColor = new Color(0.7f, 0.9f, 1f, 1f);

        const float SinkSeconds = 3f;

        Vector3 centre;
        bool floating;
        float phase;

        /// <summary>Where the baby sits. Parent the baby here while the bubble is up.</summary>
        public Transform Seat
        {
            get
            {
                EnsureSeat();
                return seat;
            }
        }

        public bool IsShown { get; private set; }

        public bool IsPopped { get; private set; }

        /// <summary>The middle of the bubble right now.</summary>
        public Vector3 Position => Seat.position;

        void Awake() => EnsureSeat();

        void EnsureSeat()
        {
            if (seat != null) return;

            seat = transform.Find("Seat");
            if (seat != null) return;

            var seatObject = new GameObject("Seat");
            seat = seatObject.transform;
            seat.SetParent(transform, false);
            seat.localPosition = Vector3.zero;
            seat.localRotation = Quaternion.identity;
        }

        /// <summary>The bubble pops into view with its middle at this point and starts to drift.</summary>
        public void Show(Vector3 middle)
        {
            EnsureSeat();
            ResetBubble();

            centre = middle;
            phase = Random.Range(0f, Mathf.PI * 2f);
            transform.position = CurrentPosition(0f);
            gameObject.SetActive(true);
            SetVisualActive(true);

            IsShown = true;
            IsPopped = false;
            floating = true;
            BattleEffects.Sparkle(middle);
            BattleEffects.Sparkle(middle + Vector3.up * 0.6f);
        }

        void Update()
        {
            if (!floating) return;

            transform.position = CurrentPosition(Time.time);
        }

        Vector3 CurrentPosition(float time)
        {
            float angle = phase + time * circleSpeed;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * circleRadius, Mathf.Sin(time * bobSpeed + phase) * bobHeight, Mathf.Sin(angle) * circleRadius);
            return centre + offset;
        }

        /// <summary>Pops the bubble when the kid is in the air close enough to its middle. Returns true if it popped.</summary>
        public bool TryPop(Transform player)
        {
            if (!IsShown || IsPopped || player == null) return false;

            Vector3 playerChest = player.position + Vector3.up * 0.8f;
            if (Vector3.Distance(playerChest, Seat.position) > popRadius) return false;

            Pop();
            return true;
        }

        /// <summary>
        /// Pops with a sound and confetti. Only the bubble skin goes; the object stays, so a baby still parented to the seat
        /// is never switched off with it (the caller hands the baby over first).
        /// </summary>
        public void Pop()
        {
            if (!IsShown || IsPopped) return;

            IsPopped = true;
            floating = false;

            Vector3 middle = Seat.position;
            Sfx.Play("aliens.bubble_pop", middle);
            BattleEffects.Confetti(middle, popConfettiColor);
            BattleEffects.Sparkle(middle);
            SetVisualActive(false);
        }

        /// <summary>The safety net: the bubble sinks gently down to this height (the middle) and pops there.</summary>
        public IEnumerator SinkAndPop(float groundMiddleY)
        {
            if (!IsShown || IsPopped) yield break;

            floating = false;
            Vector3 from = transform.position;
            Vector3 to = new Vector3(from.x, groundMiddleY, from.z);

            for (float elapsed = 0f; elapsed < SinkSeconds; elapsed += Time.deltaTime)
            {
                if (IsPopped) yield break;

                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / SinkSeconds));
                yield return null;
            }

            transform.position = to;
            Pop();
        }

        /// <summary>Back to hidden and still. The baby is reset by its own owner; do that first.</summary>
        public void ResetBubble()
        {
            floating = false;
            IsShown = false;
            IsPopped = false;
            SetVisualActive(true);
            gameObject.SetActive(false);
        }

        void SetVisualActive(bool active)
        {
            if (visual != null && visual != gameObject)
            {
                visual.SetActive(active);
                return;
            }

            // The baby sits under the seat: its own renderers must stay on when the skin pops.
            EnsureSeat();
            foreach (Renderer skin in GetComponentsInChildren<Renderer>(true))
                if (!skin.transform.IsChildOf(seat)) skin.enabled = active;
        }
    }
}
