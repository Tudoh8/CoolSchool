using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The little movements of a cart add-on, driven by the speed of the cart it sits on: a propeller that spins faster the faster
    /// the cart goes, googly eye pupils that bounce about, and balloons that sway on their strings. Put on the add-on's root; every
    /// part is optional. It only runs while the add-on is switched on.
    /// </summary>
    public class CartAddonMotion : MonoBehaviour
    {
        [Header("Spinning (the propeller)")]
        [SerializeField] Transform spinner;
        [Tooltip("Degrees per second while the cart stands still.")]
        [SerializeField] float idleSpinSpeed = 220f;
        [Tooltip("More degrees per second for each metre per second of the cart's speed.")]
        [SerializeField] float spinPerSpeed = 140f;

        [Header("Googly eyes")]
        [Tooltip("The black pupils (children of the white eyes). They bounce around inside the eye.")]
        [SerializeField] Transform[] pupils;
        [Tooltip("How far a pupil moves from its rest place, in metres.")]
        [SerializeField] float pupilRange = 0.03f;

        [Header("Balloons")]
        [Tooltip("Groups that turn about their own origin (the tie point of the string) like balloons in the wind.")]
        [SerializeField] Transform[] swayers;
        [SerializeField] float swayDegrees = 9f;

        CartController cart;
        Vector3[] pupilRest;
        Quaternion[] swayRest;

        void Awake()
        {
            cart = GetComponentInParent<CartController>();

            pupilRest = new Vector3[pupils != null ? pupils.Length : 0];
            for (int i = 0; i < pupilRest.Length; i++)
                if (pupils[i] != null) pupilRest[i] = pupils[i].localPosition;

            swayRest = new Quaternion[swayers != null ? swayers.Length : 0];
            for (int i = 0; i < swayRest.Length; i++)
                if (swayers[i] != null) swayRest[i] = swayers[i].localRotation;
        }

        void Update()
        {
            float speed = cart != null ? cart.Speed : 0f;
            float time = Time.time;

            if (spinner != null)
                spinner.Rotate(0f, (idleSpinSpeed + spinPerSpeed * speed) * Time.deltaTime, 0f, Space.Self);

            if (pupils != null)
            {
                float energy = 0.35f + Mathf.Clamp01(speed / 8f) * 0.65f;
                for (int i = 0; i < pupils.Length; i++)
                {
                    if (pupils[i] == null) continue;

                    // Two out-of-step wobbles, a different one for each pupil, so the eyes never look in step.
                    float phase = i * 1.9f;
                    var offset = new Vector3(Mathf.Sin(time * 9.3f + phase), Mathf.Sin(time * 6.1f + phase * 1.7f), 0f);
                    pupils[i].localPosition = pupilRest[i] + offset * (pupilRange * energy);
                }
            }

            if (swayers != null)
            {
                float energy = 0.5f + Mathf.Clamp01(speed / 8f) * 0.5f;
                for (int i = 0; i < swayers.Length; i++)
                {
                    if (swayers[i] == null) continue;

                    float phase = i * 2.1f;
                    float roll = Mathf.Sin(time * 1.9f + phase) * swayDegrees * energy;
                    float pitch = Mathf.Sin(time * 1.3f + phase * 0.6f) * swayDegrees * 0.6f * energy - speed * 0.8f;
                    swayers[i].localRotation = swayRest[i] * Quaternion.Euler(pitch, 0f, roll);
                }
            }
        }
    }
}
