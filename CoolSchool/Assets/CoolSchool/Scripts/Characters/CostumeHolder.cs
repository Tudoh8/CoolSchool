using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Dresses the kid up as a superhero: a cape on the back and a mask on the face.
    /// Like WeaponHolder, the pieces are made the first time they are needed and then just shown or hidden.
    /// AttachmentFit puts each piece in the right place under its bone: the cape is drawn in the character's model space (its
    /// geometry is where it hangs on the finished kid), so it gets the inverse of the chest bone's bind pose; the mask is drawn around
    /// the head joint, so it needs nothing. The offsets below are tweaks on top of that, in the piece's own axes, and start at zero.
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
                cape = Attach(capePrefab, animator, ChestBone(animator), capeLocalPosition, capeLocalEuler);
            if (mask == null)
                mask = Attach(maskPrefab, animator, animator.GetBoneTransform(HumanBodyBones.Head), maskLocalPosition, maskLocalEuler);
        }

        /// <summary>The cape hangs from the upper chest, or the chest or spine on rigs that have no upper chest.</summary>
        static Transform ChestBone(Animator animator)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
            return bone;
        }

        GameObject Attach(GameObject prefab, Animator animator, Transform bone, Vector3 position, Vector3 euler)
        {
            if (prefab == null || bone == null) return null;

            GameObject instance = Instantiate(prefab, bone);
            AttachmentFit.Place(instance.transform, animator, bone, position, euler, Vector3.one * costumeScale);

            // A costume must never push kids around or block the camera.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                // Destroy is not allowed in edit mode (the preview tools may dress the kid there).
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return instance;
        }
    }
}
