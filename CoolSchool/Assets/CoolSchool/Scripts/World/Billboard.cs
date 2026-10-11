using UnityEngine;

namespace CoolSchool.World
{
    /// <summary>
    /// Turns a world-space sprite to face the camera around the Y axis only (it never tips over),
    /// and bobs it gently up and down so it catches the eye.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("How far it bobs up and down, in metres.")]
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 2f;

        Transform cameraTransform;
        Vector3 baseLocalPosition;

        void Awake()
        {
            baseLocalPosition = transform.localPosition;
        }

        void LateUpdate()
        {
            if (cameraTransform == null)
            {
                Camera main = Camera.main;
                if (main != null) cameraTransform = main.transform;
            }

            if (cameraTransform != null)
            {
                // A sprite is seen from its back (-Z) side, so it looks away from the camera.
                Vector3 away = transform.position - cameraTransform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }
    }
}
