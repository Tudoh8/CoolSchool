using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.World
{
    /// <summary>
    /// The castle gate's portcullis: a welcome gesture. It rises when the kid comes near the gate and lowers again
    /// (with a little bounce at the bottom) when he has walked far enough away. Two radii give hysteresis, so it never
    /// flutters at the edge. It stays up while he is inside the courtyard and while the knights game runs, so it never
    /// closes in his face. It has no collider (the gate stays walkable) and only moves up and down from where it
    /// was built, which is its closed position.
    /// </summary>
    public class CastlePortcullis : MonoBehaviour
    {
        const string RiseCue = "castle.gate_rise";
        const string DropCue = "castle.gate_drop";
        const float BounceDecay = 7f;
        const float BounceFrequency = 3.5f;
        const float BounceSeconds = 0.9f;

        [Tooltip("The kid. Found automatically when empty.")]
        [SerializeField] Transform player;
        [Tooltip("World space. Only x and z are tested: while the kid is in here the gate stays up.")]
        [SerializeField] Bounds courtyard;
        [Tooltip("While this activity runs (the knights game) the gate stays up. Optional.")]
        [SerializeField] Activity keepOpenWhileRunning;

        [Header("Distances (metres on the ground, from the gate)")]
        [Tooltip("The gate rises when the kid is closer than this.")]
        [SerializeField] float openRadius = 7f;
        [Tooltip("The gate lowers when the kid is farther than this. The camera looks north from behind him, so the closed gate must still be on screen at this distance when he walks away south.")]
        [SerializeField] float closeRadius = 11.5f;

        [Header("Motion")]
        [Tooltip("How far it rises, in metres: its bottom ends just above the top of the arch.")]
        [SerializeField] float liftHeight = 3.96f;
        [SerializeField] float riseSeconds = 1.2f;
        [SerializeField] float lowerSeconds = 1.5f;
        [Tooltip("Height of the first bounce when it lands, in metres. 0 = no bounce.")]
        [SerializeField] float bounceHeight = 0.12f;

        Vector3 closedLocalPosition;
        Vector3 closedWorldPosition;
        float openness;
        bool wantsOpen;
        bool evaluated;
        float sinceLanded = float.MaxValue;

        /// <summary>0 = closed (down), 1 = fully raised.</summary>
        public float Openness => openness;

        /// <summary>
        /// While true the gate stays down (or lowers) whatever the distance or the running activity. The castle adventure
        /// holds it shut until the child has said the password. Off by default, so free play is unchanged.
        /// </summary>
        public bool HoldClosed { get; set; }

        void Awake()
        {
            closedLocalPosition = transform.localPosition;
            closedWorldPosition = transform.position;
        }

        void OnValidate()
        {
            openRadius = Mathf.Max(0.5f, openRadius);
            closeRadius = Mathf.Max(openRadius + 1f, closeRadius);
            riseSeconds = Mathf.Max(0.05f, riseSeconds);
            lowerSeconds = Mathf.Max(0.05f, lowerSeconds);
        }

        void Update()
        {
            if (player == null)
            {
                var controller = FindAnyObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }

            bool previousWish = wantsOpen;
            wantsOpen = DecideWish(wantsOpen);

            // First look: if the kid already stands close, the gate is simply up (no rise, no sound).
            if (!evaluated && player != null)
            {
                evaluated = true;
                if (wantsOpen) openness = 1f;
            }
            else if (wantsOpen && !previousWish && openness < 1f)
            {
                Sfx.Play(RiseCue, transform.position);
            }

            float seconds = wantsOpen ? riseSeconds : lowerSeconds;
            float before = openness;
            openness = Mathf.MoveTowards(openness, wantsOpen ? 1f : 0f, Time.deltaTime / seconds);

            // It just touched down: a thud and a small bounce.
            if (before > 0f && openness <= 0f)
            {
                sinceLanded = 0f;
                Sfx.Play(DropCue, transform.position);
            }
            else if (openness > 0f)
            {
                sinceLanded = float.MaxValue;
            }
            else if (sinceLanded < BounceSeconds)
            {
                sinceLanded += Time.deltaTime;
            }

            ApplyHeight();
        }

        /// <summary>Open when close or inside or playing, closed when far; in between, whatever it was doing.</summary>
        bool DecideWish(bool current)
        {
            if (HoldClosed) return false;
            if (keepOpenWhileRunning != null && keepOpenWhileRunning.IsRunning) return true;
            if (player == null) return current;

            Vector3 position = player.position;
            if (InsideCourtyard(position)) return true;

            Vector3 toPlayer = position - closedWorldPosition;
            toPlayer.y = 0f;
            float sqr = toPlayer.sqrMagnitude;
            if (sqr < openRadius * openRadius) return true;
            if (sqr > closeRadius * closeRadius) return false;
            return current;
        }

        bool InsideCourtyard(Vector3 position)
        {
            if (courtyard.size == Vector3.zero) return false;

            Vector3 min = courtyard.min;
            Vector3 max = courtyard.max;
            return position.x >= min.x && position.x <= max.x && position.z >= min.z && position.z <= max.z;
        }

        void ApplyHeight()
        {
            float height = liftHeight * Mathf.SmoothStep(0f, 1f, openness);

            if (openness <= 0f && sinceLanded < BounceSeconds)
            {
                float t = sinceLanded;
                height += bounceHeight * Mathf.Exp(-BounceDecay * t) * Mathf.Abs(Mathf.Sin(t * Mathf.PI * BounceFrequency));
            }

            transform.localPosition = closedLocalPosition + Vector3.up * height;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 centre = Application.isPlaying ? closedWorldPosition : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(centre, openRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(centre, closeRadius);
        }
    }
}
