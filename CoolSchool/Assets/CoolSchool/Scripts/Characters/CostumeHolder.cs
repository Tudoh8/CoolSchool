using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Dresses the kid up as a superhero: a cape on the back and a mask on the face.
    /// Like WeaponHolder, the pieces are made the first time they are needed and then just shown or hidden.
    /// Synty's kid attachments are made to fit the kid rig at the bone origin, so the offsets start at zero.
    /// </summary>
    public class CostumeHolder : MonoBehaviour
    {
        [SerializeField] GameObject capePrefab;
        [SerializeField] GameObject maskPrefab;
        [Header("Placement (relative to the chest / head bone)")]
        [SerializeField] Vector3 capeLocalPosition;
        [SerializeField] Vector3 capeLocalEuler;
        [SerializeField] Vector3 maskLocalPosition;
        [SerializeField] Vector3 maskLocalEuler;
        [SerializeField] float costumeScale = 1f;

        GameObject cape;
        GameObject mask;

        /// <summary>Puts the costume on (true) or takes it off (false).</summary>
        public void ShowCostume(bool show)
        {
            if (show) CreatePiecesIfNeeded();

            if (cape != null) cape.SetActive(show);
            if (mask != null) mask.SetActive(show);
        }

        void CreatePiecesIfNeeded()
        {
            if (cape != null && mask != null) return;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            if (cape == null)
                cape = Attach(capePrefab, ChestBone(animator), capeLocalPosition, capeLocalEuler);
            if (mask == null)
                mask = Attach(maskPrefab, animator.GetBoneTransform(HumanBodyBones.Head), maskLocalPosition, maskLocalEuler);
        }

        /// <summary>The cape hangs from the upper chest, or the chest or spine on rigs that have no upper chest.</summary>
        static Transform ChestBone(Animator animator)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
            return bone;
        }

        GameObject Attach(GameObject prefab, Transform bone, Vector3 position, Vector3 euler)
        {
            if (prefab == null || bone == null) return null;

            GameObject instance = Instantiate(prefab, bone);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(euler);
            instance.transform.localScale = Vector3.one * costumeScale;

            // A costume must never push kids around or block the camera.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
            return instance;
        }
    }
}
