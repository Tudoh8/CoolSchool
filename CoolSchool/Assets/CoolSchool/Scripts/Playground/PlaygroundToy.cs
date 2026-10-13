using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// On a playground toy's root (the Synty instance). Remembers whether the player is inside the touch zone, whether the toy is armed
    /// (it only starts when he walks into it, and not again until he has left the zone), and holds the toy's routine and sticker.
    /// </summary>
    public class PlaygroundToy : MonoBehaviour
    {
        [SerializeField] string stickerId;
        [Tooltip("The voice group suffix: slide, rocker, ... (both rockers say rocker).")]
        [SerializeField] string voiceName;
        [SerializeField] ToyRoutine routine;
        [SerializeField] Sprite stickerIcon;
        [SerializeField] ToyPoseOffsets poseOffsets;

        public string StickerId => stickerId;
        public string VoiceName => voiceName;
        public ToyRoutine Routine => routine;
        public Sprite StickerIcon => stickerIcon;
        public ToyPoseOffsets PoseOffsets => poseOffsets;

        /// <summary>False from the end of a routine until the player has left the touch zone. Also false until he was seen outside once.</summary>
        public bool Armed => armed;

        bool armed;
        bool playerInside;

        /// <summary>Called by ToyTouchZone when the player steps into the zone.</summary>
        public void PlayerEntered(PlayerController player)
        {
            playerInside = true;
            TryStartNow();
        }

        /// <summary>Called by ToyTouchZone when the player has left the zone: the toy is armed again.</summary>
        public void PlayerExited(PlayerController player)
        {
            playerInside = false;
            armed = true;
        }

        /// <summary>The routine ended (or started): do not start again until he has left the zone.</summary>
        internal void Disarm()
        {
            armed = false;
        }

        /// <summary>
        /// The routine is over: the toy stays disarmed while the kid is still inside the zone (he must walk out first), and is armed at once
        /// when he already landed outside of it.
        /// </summary>
        internal void RoutineEnded(Vector3 playerPosition)
        {
            ToyTouchZone zone = GetComponentInChildren<ToyTouchZone>(true);
            bool inside = zone != null && zone.Contains(playerPosition);
            playerInside = inside;
            armed = !inside;
        }

        void Update()
        {
            // A start can be refused for a moment (he was still in the air, or a toy had just ended): try again while he stays inside.
            if (playerInside && armed) TryStartNow();
        }

        void TryStartNow()
        {
            if (!armed || routine == null) return;

            ToyPlayer player = ToyPlayer.Instance;
            if (player != null) player.TryStart(this);
        }

        /// <summary>Play mode: starts the routine wherever the player is, ignoring the start rules.</summary>
        [ContextMenu("Play now")]
        void PlayNow()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("CoolSchool toys: 'Play now' works in play mode only.");
                return;
            }

            ToyPlayer player = ToyPlayer.Instance;
            if (player == null || routine == null)
            {
                Debug.LogWarning("CoolSchool toys: no ToyPlayer on the player, or this toy has no routine.");
                return;
            }

            player.TryStart(this, true);
        }
    }
}
