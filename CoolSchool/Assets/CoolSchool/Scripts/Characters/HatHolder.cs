using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Puts the hat the child chose on the head bone (cap, knight's helmet, pirate hat, gold versions).
    /// Lives on the player. EquipmentApplier tells it which hat to wear and hides it in the activities that dress the head.
    /// Every hat is made the first time it is worn and then only shown or hidden, so swapping back is instant.
    /// Every hat is drawn around the head joint, so AttachmentFit leaves it at the bone and the offsets are just tweaks (in the
    /// hat's own axes) that start at zero.
    /// </summary>
    public class HatHolder : MonoBehaviour
    {
        [Header("Placement (relative to the head bone)")]
        [SerializeField] Vector3 localPosition;
        [SerializeField] Vector3 localEuler;

        struct HatKey
        {
            public GameObject Prefab;
            public bool Gold;
        }

        readonly Dictionary<HatKey, GameObject> hats = new Dictionary<HatKey, GameObject>();

        GameObject wantedPrefab;
        bool wantedGold;
        bool hidden;
        GameObject current;

        /// <summary>The hat he should be wearing (null = none), also while it is hidden.</summary>
        public GameObject WantedPrefab => wantedPrefab;

        /// <summary>Wears this hat (a gold copy is painted gold). Null takes the hat off.</summary>
        public void SetHat(GameObject prefab, bool gold)
        {
            wantedPrefab = prefab;
            wantedGold = prefab != null && gold;
            Refresh();
        }

        /// <summary>Hides the hat (true) or shows it again (false), for activities that dress the head.</summary>
        public void SetHidden(bool hide)
        {
            hidden = hide;
            Refresh();
        }

        void Update()
        {
            // The head bone may not have been there when the hat was asked for (switched off): try again.
            if (wantedPrefab != null && !hidden && current == null) Refresh();
        }

        void Refresh()
        {
            GameObject target = null;
            if (wantedPrefab != null && !hidden) target = GetOrCreate(wantedPrefab, wantedGold);

            if (current != null && current != target) current.SetActive(false);
            current = target;
            if (current != null) current.SetActive(true);
        }

        GameObject GetOrCreate(GameObject prefab, bool gold)
        {
            var key = new HatKey { Prefab = prefab, Gold = gold };
            if (hats.TryGetValue(key, out GameObject existing) && existing != null) return existing;

            // Bones are not available while switched off. Nothing is remembered, so a later call tries again.
            if (!gameObject.activeInHierarchy) return null;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return null;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return null;

            GameObject instance = Instantiate(prefab, head);
            AttachmentFit.Place(instance.transform, animator, head, localPosition, localEuler, Vector3.one);
            instance.SetActive(false);

            // A hat must never push kids around or block the camera.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                // Destroy is not allowed in edit mode (the preview tools may put hats on there).
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            if (gold) GoldPaint.Apply(instance);
            hats[key] = instance;
            return instance;
        }
    }
}
