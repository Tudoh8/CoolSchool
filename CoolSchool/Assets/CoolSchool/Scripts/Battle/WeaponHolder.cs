using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Puts a foam sword in the kid's right hand and a shield on the left arm.
    /// The weapons are made the first time they are needed and then just shown or hidden.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        [SerializeField] GameObject swordPrefab;
        [SerializeField] GameObject shieldPrefab;
        [Header("Placement (relative to the hand / lower arm bone)")]
        [SerializeField] Vector3 swordLocalPosition;
        [SerializeField] Vector3 swordLocalEuler;
        [SerializeField] Vector3 shieldLocalPosition;
        [SerializeField] Vector3 shieldLocalEuler;
        [SerializeField] float weaponScale = 1f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        GameObject sword;
        GameObject shield;
        bool shown;
        Color? glow;

        public void ShowWeapons(bool show)
        {
            shown = show;
            if (show) CreateWeaponsIfNeeded();

            if (sword != null) sword.SetActive(show);
            if (shield != null) shield.SetActive(show);
        }

        /// <summary>Swaps in another sword, e.g. one won from a present. A sword already in the hand is replaced.</summary>
        public void SetSwordPrefab(GameObject prefab)
        {
            if (prefab == null || prefab == swordPrefab) return;

            swordPrefab = prefab;
            if (sword != null)
            {
                Destroy(sword);
                sword = null;
            }

            // If the weapons are out right now, the new sword should appear straight away.
            if (shown) ShowWeapons(true);
        }

        /// <summary>
        /// Makes the sword glow in this colour (the golden sword), or back to normal with null.
        /// The glow is set on the sword's own material copies, so other swords are never changed.
        /// </summary>
        public void SetGlow(Color? color)
        {
            bool wasGlowing = glow.HasValue;
            glow = color;
            if (sword == null || (!color.HasValue && !wasGlowing)) return;

            if (color.HasValue)
            {
                ApplyGlow(sword, color.Value);
                return;
            }

            // Simplest way back to the normal look: throw the glowing sword away and make a fresh one.
            Destroy(sword);
            sword = null;
            if (shown) ShowWeapons(true);
        }

        void CreateWeaponsIfNeeded()
        {
            if (sword != null && shield != null) return;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            if (sword == null)
            {
                sword = Attach(swordPrefab, animator.GetBoneTransform(HumanBodyBones.RightHand), swordLocalPosition, swordLocalEuler);
                if (sword != null && glow.HasValue) ApplyGlow(sword, glow.Value);
            }
            if (shield == null)
                shield = Attach(shieldPrefab, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), shieldLocalPosition, shieldLocalEuler);
        }

        GameObject Attach(GameObject prefab, Transform bone, Vector3 position, Vector3 euler)
        {
            if (prefab == null || bone == null) return null;

            GameObject instance = Instantiate(prefab, bone);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(euler);
            instance.transform.localScale = Vector3.one * weaponScale;
            RemoveColliders(instance);
            return instance;
        }

        /// <summary>Emission plus a warm tint on every material copy of the sword.</summary>
        static void ApplyGlow(GameObject weapon, Color color)
        {
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                // Reading .materials makes copies, so the shared material assets stay untouched.
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty(EmissionColorId))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor(EmissionColorId, color);
                    }

                    if (material.HasProperty(BaseColorId))
                        material.SetColor(BaseColorId, Color.Lerp(material.GetColor(BaseColorId), color, 0.6f));
                }
            }
        }

        /// <summary>Toy weapons must not push kids around or block the camera.</summary>
        static void RemoveColliders(GameObject instance)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }
    }
}
