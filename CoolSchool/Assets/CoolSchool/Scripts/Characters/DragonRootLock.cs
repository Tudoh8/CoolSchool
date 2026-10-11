using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Keeps the Infinity PBR Dragon_LP in place under its own transform, so our scripts (ShopCustomer, DragonBoss)
    /// are the only thing that moves it.
    ///
    /// Why: Dragon_LP.fbx has no "Root node" set in its generic rig import (motionNodeName is empty), so Mecanim does not
    /// treat the walk as root motion. The Walk, Fly and Takeoff clips animate the "DragonRoot" bone itself forward
    /// (Walk: 2.4 m per 1.33 s loop; Fly: 18 m per loop), and the whole skeleton under it slides ahead and then snaps
    /// back when the clip loops. Our code also moves the transform, so the dragon moved twice.
    ///
    /// Fix: every LateUpdate the DragonRoot bone's horizontal local position goes back to its rest value (height is left
    /// to the clip, for the takeoff lift). It runs before the other LateUpdates (the cone follows the head bone) so
    /// they all see the pinned pose. While the Walk state plays, the animator speed is matched to the speed our code
    /// moves the dragon, so the feet do not slide much. No vendor asset is changed.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public class DragonRootLock : MonoBehaviour
    {
        const string RootBoneName = "DragonRoot";
        const string WalkStateName = "Walk";

        // The Walk clip moves DragonRoot 2.4 m in 40 frames at 30 fps (read from Dragon_LP.fbx), at model scale 1.
        const float ClipWalkSpeed = 1.8f;
        const float MinWalkAnimSpeed = 0.5f;
        const float MaxWalkAnimSpeed = 2f;

        static readonly int WalkStateId = Animator.StringToHash(WalkStateName);

        [SerializeField] Animator animator;
        [Tooltip("How fast the owning script moves the dragon on the ground, in m/s. 0: the walk plays at its own speed.")]
        [SerializeField] float walkSpeed;

        Transform rootBone;
        Vector3 restLocalPosition;

        /// <summary>How fast the owning script moves the dragon on the ground, in m/s. 0: the walk plays at its own speed.</summary>
        public float WalkSpeed
        {
            get => walkSpeed;
            set => walkSpeed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Adds (or finds) the lock on the animator's object. Returns null when the model has no DragonRoot bone,
        /// for instance a kid, so it is safe to call for any animator.
        /// </summary>
        public static DragonRootLock Ensure(Animator animator, float walkSpeed)
        {
            if (animator == null) return null;

            var existing = animator.GetComponent<DragonRootLock>();
            if (existing == null && FindRootBone(animator.transform) == null) return null;

            DragonRootLock found = existing != null ? existing : animator.gameObject.AddComponent<DragonRootLock>();
            found.animator = animator;
            found.WalkSpeed = walkSpeed;
            found.CaptureRest();
            return found;
        }

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            CaptureRest();
        }

        void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
        }

        void CaptureRest()
        {
            if (rootBone != null) return;

            rootBone = FindRootBone(transform);
            if (rootBone != null) restLocalPosition = rootBone.localPosition;
        }

        static Transform FindRootBone(Transform start)
        {
            foreach (Transform candidate in start.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == RootBoneName) return candidate;
            }
            return null;
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            animator.speed = WalkingAnimatorSpeed();
        }

        /// <summary>Slows or speeds up the Walk clip so its stride matches how fast the dragon really moves.</summary>
        float WalkingAnimatorSpeed()
        {
            if (walkSpeed <= 0f || !IsWalkPlaying()) return 1f;

            // Model scale: a dragon scaled to 3.5 m strides further than one scaled to 2.6 m.
            float scale = transform.TransformVector(Vector3.forward).magnitude;
            float natural = ClipWalkSpeed * Mathf.Max(0.01f, scale);
            return Mathf.Clamp(walkSpeed / natural, MinWalkAnimSpeed, MaxWalkAnimSpeed);
        }

        bool IsWalkPlaying()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == WalkStateId) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == WalkStateId;
        }

        void LateUpdate()
        {
            if (rootBone == null) return;

            Vector3 local = rootBone.localPosition;
            local.x = restLocalPosition.x;
            local.z = restLocalPosition.z;
            rootBone.localPosition = local;
        }
    }
}
