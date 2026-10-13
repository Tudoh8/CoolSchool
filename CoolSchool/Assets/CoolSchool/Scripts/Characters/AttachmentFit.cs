using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Works out where a Synty kid attachment (cape, mask, hats, helmets, eyepatch) must sit under a humanoid bone, so it
    /// fits whatever bone it hangs from.
    ///
    /// Synty makes its attachments in one of two ways, and both exist in the PolygonKids pack:
    /// - Bone-space: the model is drawn around its own origin, which is the joint it belongs on (every head piece: the mask,
    ///   hats, helmets, bandana and eyepatch sit 2 to 25 cm around the head joint, whose bind rotation is the identity).
    ///   These fit with zero offsets, so they are simply parented to the bone.
    /// - Model-space: the model is drawn where it stands on the finished character, with the character's feet at the origin
    ///   (the cape: its geometry is 8 to 95 cm up and 7 to 25 cm behind the origin, and its root bone lies exactly on the
    ///   kid's Spine_03). Parented to a bone with zero offsets it ends up turned with the bone's own axes. Synty's chest and
    ///   spine joints are turned about 90 degrees (their X axis points up the body), so the cape stuck out sideways.
    ///   These need the inverse of the bone's bind pose: the one local pose that puts the attachment back at the model's origin
    ///   when the character is in its bind pose. Because it is parented to the bone it then follows every animation.
    ///
    /// Which of the two a prefab is gets decided automatically, from where its mesh bounds lie: if the bounds' centre sits
    /// clearly closer to the bone when read as model space than when read as bone space (see ModelSpaceRatio), it is model
    /// space. Otherwise it is bone space and nothing changes. A character without a skinned mesh that uses the bone also
    /// counts as bone space.
    /// </summary>
    public static class AttachmentFit
    {
        /// <summary>
        /// How much closer to the bone the geometry must be as model space than as bone space (a ratio of the two distances)
        /// before a prefab counts as model space. Below 1 on purpose, so a piece that already fits is never touched.
        /// The cape scores about 0.64, the head pieces more than 5.
        /// </summary>
        const float ModelSpaceRatio = 0.8f;

        /// <summary>
        /// Puts a freshly made attachment in place under the bone. The attachment must already be a child of the bone.
        /// The tweak (position, Euler angles, scale) is applied on top of the fit, in the attachment's own axes, so it nudges the
        /// piece the same way whatever bone it hangs from.
        /// </summary>
        public static void Place(Transform attachment, Animator animator, Transform bone, Vector3 tweakPosition, Vector3 tweakEuler, Vector3 tweakScale)
        {
            Matrix4x4 tweak = Matrix4x4.TRS(tweakPosition, Quaternion.Euler(tweakEuler), tweakScale);
            Matrix4x4 fit = Matrix4x4.identity;

            // Measure from a clean slate, so the result does not depend on how the prefab root was set up.
            attachment.localPosition = Vector3.zero;
            attachment.localRotation = Quaternion.identity;
            attachment.localScale = Vector3.one;

            if (TryGetBoneBindPose(animator, bone, out Matrix4x4 boneInModel) && IsModelSpace(attachment, boneInModel))
                fit = boneInModel.inverse;

            Matrix4x4 local = fit * tweak;
            attachment.localPosition = local.GetPosition();
            attachment.localRotation = local.rotation;
            attachment.localScale = local.lossyScale;
        }

        /// <summary>
        /// The bone's pose in the character's own space (the animator's transform) while the character is in its bind pose,
        /// read from the bind poses of a skinned mesh that uses the bone. False when no skinned mesh uses it.
        /// </summary>
        public static bool TryGetBoneBindPose(Animator animator, Transform bone, out Matrix4x4 boneInModel)
        {
            boneInModel = Matrix4x4.identity;
            if (animator == null || bone == null) return false;

            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                Transform[] bones = renderer.bones;
                Matrix4x4[] bindPoses = mesh.bindposes;
                int count = Mathf.Min(bones.Length, bindPoses.Length);
                for (int i = 0; i < count; i++)
                {
                    if (bones[i] != bone) continue;

                    // bindPose = boneFromMesh, so the bone in mesh space is its inverse; then carry the mesh space over to the model.
                    Matrix4x4 meshInModel = animator.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    boneInModel = meshInModel * bindPoses[i].inverse;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when the attachment's geometry is drawn in the character's model space (see the class comment). The attachment
        /// must have an identity local pose when this is called.
        /// </summary>
        public static bool IsModelSpace(Transform attachment, Matrix4x4 boneInModel)
        {
            if (!TryMeasureCentre(attachment, out Vector3 centre)) return false;

            Vector3 bonePosition = boneInModel.GetPosition();
            float asBoneSpace = (boneInModel.MultiplyPoint3x4(centre) - bonePosition).magnitude;
            float asModelSpace = (centre - bonePosition).magnitude;
            return asModelSpace < asBoneSpace * ModelSpaceRatio;
        }

        /// <summary>The middle of the attachment's mesh bounds, in the attachment's own space. False when it has no mesh.</summary>
        static bool TryMeasureCentre(Transform attachment, out Vector3 centre)
        {
            centre = Vector3.zero;
            bool found = false;
            Bounds total = default;

            foreach (Renderer renderer in attachment.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                if (mesh == null) continue;

                // Mesh bounds are in the renderer's own space; carry the eight corners over to the attachment's space.
                Matrix4x4 toAttachment = attachment.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Bounds bounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = toAttachment.MultiplyPoint3x4(point);
                    if (!found) { total = new Bounds(point, Vector3.zero); found = true; }
                    else total.Encapsulate(point);
                }
            }

            if (found) centre = total.center;
            return found;
        }
    }
}
