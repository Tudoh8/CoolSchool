using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Puts a foam sword in the kid's right hand and a shield on the left arm.
    /// The weapons are made the first time they are needed and then just shown or hidden.
    /// Where they sit is worked out from the rig and the weapon's own bounds (see WeaponFit), so it is right for every
    /// character and for any sword swapped in later.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        [SerializeField] GameObject swordPrefab;
        [SerializeField] GameObject shieldPrefab;
        [Header("Extra tweak on top of the computed fit (in the weapon's own axes, default none)")]
        [SerializeField] Vector3 swordLocalPosition;
        [SerializeField] Vector3 swordLocalEuler;
        [SerializeField] Vector3 shieldLocalPosition;
        [SerializeField] Vector3 shieldLocalEuler;
        [SerializeField] float weaponScale = 1f;

        // Logged once per weapon prefab, so the detected axes can be checked in the console.
        static readonly System.Collections.Generic.HashSet<string> LoggedFits = new System.Collections.Generic.HashSet<string>();

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

        // The rig, measured once before the animator first moves the bones (the wrist is relaxed then).
        bool measured;
        WeaponFit.HandFrame rightHand;
        WeaponFit.ForearmFrame leftForearm;

        void Awake()
        {
            MeasureRig();
        }

        /// <summary>
        /// Reads the right hand and the left forearm from the bones. Done in Awake (the animator has not moved anything yet);
        /// anywhere else it is done on first use. Bones may not be available while switched off, so then it tries again later.
        /// </summary>
        void MeasureRig()
        {
            if (measured) return;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return;

            WeaponFit.TryMeasureHand(animator, true, out _, out rightHand);
            WeaponFit.TryMeasureForearm(animator, out _, out leftForearm);
            measured = true;
        }

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

            MeasureRig();

            if (sword == null)
            {
                sword = Attach(swordPrefab, animator.GetBoneTransform(HumanBodyBones.RightHand), false);
                if (sword != null && glow.HasValue) ApplyGlow(sword, glow.Value);
            }
            if (shield == null)
                shield = Attach(shieldPrefab, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), true);
        }

        GameObject Attach(GameObject prefab, Transform bone, bool isShield)
        {
            if (prefab == null || bone == null) return null;

            GameObject instance = Instantiate(prefab, bone);
            instance.transform.localScale = Vector3.one * weaponScale;
            RemoveColliders(instance);

            Vector3 position;
            Quaternion rotation;
            string report;
            bool fitted = isShield
                ? WeaponFit.FitShield(instance, weaponScale, leftForearm, out position, out rotation, out report)
                : WeaponFit.FitSword(instance, weaponScale, rightHand, out position, out rotation, out report);

            if (!fitted)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                Debug.LogWarning($"CoolSchool: could not fit {prefab.name} on {name} ({report}); it is left at the bone.");
            }
            else if (LoggedFits.Add((isShield ? "shield " : "sword ") + prefab.name))
            {
                Debug.Log($"CoolSchool weapon fit: {(isShield ? "shield" : "sword")} {prefab.name}: {report}.");
            }

            Vector3 tweakPosition = isShield ? shieldLocalPosition : swordLocalPosition;
            Vector3 tweakEuler = isShield ? shieldLocalEuler : swordLocalEuler;
            instance.transform.localRotation = rotation * Quaternion.Euler(tweakEuler);
            instance.transform.localPosition = position + rotation * tweakPosition;
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
                // Destroy is not allowed in edit mode (the preview tools put weapons on there).
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
        }
    }
}
