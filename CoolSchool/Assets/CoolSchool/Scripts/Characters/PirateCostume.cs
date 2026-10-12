using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Dresses the kid up as a pirate for the pirate adventure: an eyepatch, and on the head the captain's hat once it has
    /// been won (hat.pirate), otherwise the bandana. A separate type from CostumeHolder (the superhero cape and mask), so
    /// GetComponent&lt;CostumeHolder&gt;() stays unambiguous.
    /// Like CostumeHolder, the pieces are made the first time they are needed and then just shown or hidden.
    /// All three pieces are drawn around the head joint, so AttachmentFit leaves them at the bone and the offsets are just tweaks
    /// (in the piece's own axes) that start at zero.
    /// </summary>
    public class PirateCostume : MonoBehaviour
    {
        [SerializeField] GameObject bandanaPrefab;
        [SerializeField] GameObject captainHatPrefab;
        [SerializeField] GameObject eyepatchPrefab;
        [Tooltip("The reward item that unlocks the captain's hat. Without it the kid wears the bandana.")]
        [SerializeField] string captainHatUnlockId = "hat.pirate";
        [Header("Placement (relative to the head bone)")]
        [SerializeField] Vector3 hatLocalPosition;
        [SerializeField] Vector3 hatLocalEuler;
        [SerializeField] Vector3 eyepatchLocalPosition;
        [SerializeField] Vector3 eyepatchLocalEuler;

        GameObject bandana;
        GameObject captainHat;
        GameObject eyepatch;

        /// <summary>Puts the costume on (true) or takes it off (false).</summary>
        public void Show(bool show)
        {
            if (show) CreatePiecesIfNeeded();

            bool captain = show && ProgressStore.Current.IsUnlocked(captainHatUnlockId);
            if (eyepatch != null) eyepatch.SetActive(show);
            if (captainHat != null) captainHat.SetActive(captain);
            // The bandana is the head piece of everyone who has not won the captain's hat yet.
            if (bandana != null) bandana.SetActive(show && !(captain && captainHat != null));
        }

        void CreatePiecesIfNeeded()
        {
            if (bandana != null && captainHat != null && eyepatch != null) return;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            if (bandana == null) bandana = Attach(bandanaPrefab, animator, head, hatLocalPosition, hatLocalEuler);
            if (captainHat == null) captainHat = Attach(captainHatPrefab, animator, head, hatLocalPosition, hatLocalEuler);
            if (eyepatch == null) eyepatch = Attach(eyepatchPrefab, animator, head, eyepatchLocalPosition, eyepatchLocalEuler);
        }

        static GameObject Attach(GameObject prefab, Animator animator, Transform bone, Vector3 position, Vector3 euler)
        {
            if (prefab == null || bone == null) return null;

            GameObject instance = Instantiate(prefab, bone);
            AttachmentFit.Place(instance.transform, animator, bone, position, euler, Vector3.one);
            instance.SetActive(false);

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
