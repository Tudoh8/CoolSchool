using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The camera during the soapbox race: it rides behind the player's cart along the track, looking up the street. Along the
    /// track it is rigidly tied to the cart (no lag), and it only smooths the sideways and up/down movement. It starts from the
    /// pose the follow camera left, blends in, and at the end flies to the podium spot. It sits on the main camera and is
    /// switched off outside the race (the FollowCamera takes over then).
    /// </summary>
    public class RaceChaseCamera : MonoBehaviour
    {
        [Tooltip("Relative to the cart along the track: x sideways, y up, z along (negative = behind).")]
        [SerializeField] Vector3 offset = new Vector3(0f, 3.0f, -6.0f);
        [Tooltip("How far up the street the camera looks.")]
        [SerializeField] float lookAhead = 6f;
        [SerializeField] float lookHeight = 1.0f;
        [Tooltip("0 to 1: how much the camera follows the cart's sideways offset.")]
        [SerializeField, Range(0f, 1f)] float lateralFollow = 0.6f;
        [SerializeField] float smoothTime = 0.15f;

        enum Mode { Following, Moving }

        public CartController Target { get; set; }

        Mode mode = Mode.Following;

        // Blending in from where the camera was, or flying to a spot.
        bool blending;
        float blendT;
        float blendSeconds = 1f;
        Vector3 blendFromPosition;
        Quaternion blendFromRotation;

        Transform spot;

        // Smoothed sideways and vertical follow.
        float lateral;
        float lateralVelocity;
        float vertical;
        float verticalVelocity;

        /// <summary>Starts following the cart, gliding from the camera's current pose over some seconds.</summary>
        public void BlendIn(float seconds)
        {
            enabled = true;
            mode = Mode.Following;
            spot = null;
            blendFromPosition = transform.position;
            blendFromRotation = transform.rotation;
            blendSeconds = Mathf.Max(0.01f, seconds);
            blendT = 0f;
            blending = true;

            lateral = Target != null ? Target.LaneX * lateralFollow : 0f;
            vertical = 0f;
            lateralVelocity = 0f;
            verticalVelocity = 0f;
        }

        /// <summary>Stops following and glides to the spot's pose (the podium view).</summary>
        public void MoveTo(Transform destination, float seconds)
        {
            if (destination == null) return;

            enabled = true;
            mode = Mode.Moving;
            spot = destination;
            blendFromPosition = transform.position;
            blendFromRotation = transform.rotation;
            blendSeconds = Mathf.Max(0.01f, seconds);
            blendT = 0f;
            blending = true;
        }

        void LateUpdate()
        {
            Vector3 position;
            Quaternion rotation;

            if (mode == Mode.Moving)
            {
                if (spot == null) return;

                position = spot.position;
                rotation = spot.rotation;
            }
            else
            {
                if (Target == null || Target.Track == null) return;

                ComputeFollowPose(out position, out rotation);
            }

            if (blending)
            {
                blendT += Time.deltaTime / blendSeconds;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendT));
                position = Vector3.Lerp(blendFromPosition, position, k);
                rotation = Quaternion.Slerp(blendFromRotation, rotation, k);
                if (blendT >= 1f) blending = false;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        void ComputeFollowPose(out Vector3 position, out Quaternion rotation)
        {
            RaceTrack track = Target.Track;

            lateral = Mathf.SmoothDamp(lateral, Target.LaneX * lateralFollow, ref lateralVelocity, smoothTime);
            vertical = Mathf.SmoothDamp(vertical, Target.Height * 0.5f, ref verticalVelocity, smoothTime);

            position = track.PointAt(Target.Distance + offset.z, lateral + offset.x) + Vector3.up * (offset.y + vertical);
            Vector3 look = track.PointAt(Target.Distance + lookAhead, lateral) + Vector3.up * (lookHeight + vertical * 0.5f);
            Vector3 forward = look - position;
            rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : transform.rotation;
        }
    }
}
