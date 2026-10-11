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
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly Color GoldColor = new Color(1f, 0.78f, 0.16f, 1f);

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

        /// <summary>Where the sword is right now (for sparkles). Near the kid's hand height if it is not out.</summary>
        public Vector3 SwordPosition => sword != null ? sword.transform.position : transform.position + Vector3.up * 1f;

        /// <summary>
        /// Turns every material copy of a weapon solid shining gold. The Synty kid materials have a texture atlas
        /// and an emission MASK, so changing only the colours would barely show: the textures are swapped for plain
        /// white ones so the whole weapon turns gold and glows. Public so the lesson's reward sword looks the same.
        /// </summary>
        public static void ApplyGlow(GameObject weapon, Color color)
        {
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                // Reading .materials makes copies, so the shared material assets stay untouched.
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, Texture2D.whiteTexture);
                    if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, Texture2D.whiteTexture);
                    if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, GoldColor);
                    if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, 0.3f);
                    if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, 0.75f);

                    if (material.HasProperty(EmissionColorId))
                    {
                        material.EnableKeyword("_EMISSION");
                        if (material.HasProperty(EmissionMapId)) material.SetTexture(EmissionMapId, Texture2D.whiteTexture);
                        material.SetColor(EmissionColorId, color * 0.55f);
                    }
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
