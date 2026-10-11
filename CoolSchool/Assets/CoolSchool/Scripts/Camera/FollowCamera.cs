using UnityEngine;

namespace CoolSchool.CameraRig
{
    /// <summary>
    /// Follows the target from a fixed angle so "up" on the joystick always means
    /// "away from the camera". Pulls in closer when something blocks the view.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 6.5f, -7f);
        [Tooltip("Point on the target to look at, relative to its feet.")]
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] float smoothTime = 0.2f;

        [Header("Obstacles")]
        [SerializeField] LayerMask obstacleMask = 1;
        [SerializeField] float probeRadius = 0.3f;
        [SerializeField] float minDistance = 2.5f;

        Vector3 velocity;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Where the camera wants to be right now, for cutscenes that hand the camera back.</summary>
        public Pose DesiredPose()
        {
            if (target == null) return new Pose(transform.position, transform.rotation);

            Vector3 position = DesiredPosition();
            return new Pose(position, Quaternion.LookRotation(target.position + lookOffset - position, Vector3.up));
        }

        /// <summary>Jumps straight to the wanted pose and stops all smoothing motion.</summary>
        public void Snap()
        {
            velocity = Vector3.zero;
            Pose pose = DesiredPose();
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        void Start()
        {
            if (target != null) transform.position = DesiredPosition();
        }

        void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
            transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position, Vector3.up);
        }

        Vector3 DesiredPosition()
        {
            Vector3 focus = target.position + lookOffset;
            Vector3 wanted = target.position + offset;
            Vector3 toCamera = wanted - focus;
            float distance = toCamera.magnitude;

            if (Physics.SphereCast(focus, probeRadius, toCamera / distance, out RaycastHit hit, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(hit.distance, minDistance);
            }

            return focus + toCamera.normalized * distance;
        }
    }
}
