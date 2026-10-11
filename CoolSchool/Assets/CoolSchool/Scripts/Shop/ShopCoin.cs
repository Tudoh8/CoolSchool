using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// A gold coin on the counter. It lies tilted toward the camera and rocks a little so the gold glints, pulses when it is the
    /// one to tap, and is flown into the register by the shop.
    /// It has no collider: the shop picks the coin nearest to the finger, so small fingers never miss.
    /// </summary>
    public class ShopCoin : MonoBehaviour
    {
        /// <summary>The coin is big enough to read and to tap from the shop camera.</summary>
        public const float Diameter = 0.19f;
        public const float Thickness = 0.055f;
        /// <summary>How far toward the camera the flat face is turned from straight up (0 is flat, 1 faces the camera).</summary>
        const float TiltTowardCamera = 0.3f;
        const float RockDegrees = 9f;
        const float RockSpeed = 2.6f;
        const float PulseSpeed = 7f;
        const float PulseAmount = 0.2f;

        /// <summary>True once the child has tapped it and it is on its way to the register.</summary>
        public bool Taken { get; set; }

        bool pulsing;
        Vector3 baseScale;
        Quaternion baseRotation;
        float rockPhase;

        /// <summary>The rotation of a coin lying on the counter, turned a little toward the camera so the gold face shows.</summary>
        public static Quaternion LieRotation(Vector3 towardsCamera)
        {
            Vector3 normal = Vector3.Slerp(Vector3.up, towardsCamera.normalized, TiltTowardCamera);
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        /// <summary>Makes a coin that lies tilted toward the camera, popping in at the position.</summary>
        public static ShopCoin Create(Material gold, Vector3 position, Vector3 towardsCamera)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "ShopCoin";
            Destroy(disc.GetComponent<Collider>());

            var renderer = disc.GetComponent<MeshRenderer>();
            if (gold != null) renderer.sharedMaterial = gold;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The flat face (the cylinder's up) is turned a little toward the camera; the cylinder is 2 m tall by default, so it is squashed.
            var coin = disc.AddComponent<ShopCoin>();
            coin.baseRotation = LieRotation(towardsCamera);
            coin.rockPhase = Random.value * Mathf.PI * 2f;
            disc.transform.rotation = coin.baseRotation;
            disc.transform.position = position;

            coin.baseScale = new Vector3(Diameter, Thickness * 0.5f, Diameter);
            disc.transform.localScale = Vector3.zero;
            coin.StartCoroutine(ShopMotion.PopIn(disc.transform, coin.baseScale, 0.3f));
            return coin;
        }

        public void SetPulse(bool on)
        {
            pulsing = on;
            if (!on && !Taken) transform.localScale = baseScale;
        }

        void Update()
        {
            // Rocks gently on the counter, so the gold catches the light.
            float rock = Time.time * RockSpeed + rockPhase;
            transform.rotation = baseRotation * Quaternion.Euler(Mathf.Sin(rock) * RockDegrees, 0f, Mathf.Cos(rock * 0.8f) * RockDegrees);

            if (pulsing && !Taken)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
                transform.localScale = baseScale * (1f + beat * PulseAmount);
            }
        }
    }
}
