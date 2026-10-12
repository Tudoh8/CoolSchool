using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// A cardboard racing helmet on the head bone, for the soapbox race. The player wears one only during the race
    /// (SoapboxRace calls Show); the rival kids get their own RaceHelmet that is shown all the time.
    /// Like PirateCostume the helmet is made the first time it is needed and then only shown or hidden.
    /// Synty's kid attachments are made to fit the kid rig at the bone origin, so the offsets start at zero.
    /// </summary>
    public class RaceHelmet : MonoBehaviour
    {
        [SerializeField] GameObject helmetPrefab;
        [Header("Placement (relative to the head bone)")]
        [SerializeField] Vector3 localPosition;
        [SerializeField] Vector3 localEuler;
        [SerializeField] float localScale = 1f;

        GameObject helmet;

        /// <summary>Puts the helmet on (true) or takes it off (false).</summary>
        public void Show(bool show)
        {
            if (show) CreateIfNeeded();
            if (helmet != null) helmet.SetActive(show);
        }

        void CreateIfNeeded()
        {
            if (helmet != null || helmetPrefab == null) return;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            helmet = Instantiate(helmetPrefab, head);
            helmet.transform.localPosition = localPosition;
            helmet.transform.localRotation = Quaternion.Euler(localEuler);
            helmet.transform.localScale = Vector3.one * localScale;
            helmet.SetActive(false);

            // A helmet must never push kids around or block the camera.
            foreach (Collider collider in helmet.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }
    }
}
