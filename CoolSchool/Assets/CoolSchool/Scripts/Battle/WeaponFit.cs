using System.Text;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Works out where a toy weapon sits on a humanoid kid, from the rig and from the weapon's own bounds, so it is right for
    /// every character and every weapon prefab without hand-tuned offsets.
    ///
    /// The hand is described by a frame made of bone positions only (wrist to middle knuckle = fingers, index minus middle =
    /// across the palm, thumb side as the sign), so the Synty rig's odd bone axes do not matter. A sword is gripped like a
    /// fist: the blade leaves the thumb side of the fist, raised a little toward the wrist, so on a hanging arm it points
    /// forward and slightly up. A shield lies on the back of the left forearm, its face pointing away from the arm.
    /// </summary>
    public static class WeaponFit
    {
        /// <summary>How far the blade is raised from "straight out of the thumb side" toward the wrist, in degrees.</summary>
        const float BladeRaiseDegrees = 25f;

        /// <summary>The middle of the fist, as a fraction of the weapon's length measured from the grip end.</summary>
        const float GripFromEnd = 0.15f;

        /// <summary>How far the fist's middle lies from the wrist, as a fraction of the wrist-to-knuckle distance.</summary>
        const float PalmAlong = 0.5f;

        /// <summary>How far the grip lies toward the palm side of that line, as a fraction of the wrist-to-knuckle distance.</summary>
        const float PalmDeep = 0.2f;

        /// <summary>Forearm radius as a fraction of the elbow-to-wrist distance (the shield rests on top of it).</summary>
        const float ForearmRadius = 0.25f;

        /// <summary>Where along the forearm, from the elbow, the shield's middle sits (0 = elbow, 1 = wrist).</summary>
        const float ShieldAlong = 0.55f;

        /// <summary>A hand in the space of its own bone. The vectors are unit length; Palm points out of the palm.</summary>
        public struct HandFrame
        {
            public Vector3 Finger;
            public Vector3 Side;
            public Vector3 Palm;
            public float Length;
            public bool Valid;
        }

        /// <summary>A forearm in the space of its own bone: Axis points to the wrist, Back points out of the back of the hand.</summary>
        public struct ForearmFrame
        {
            public Vector3 Axis;
            public Vector3 Back;
            public float Length;
            public bool Valid;
        }

        /// <summary>A weapon model measured in the space of its own root.</summary>
        public struct Shape
        {
            public Vector3 Centre;
            public Vector3 Size;
            public int LongAxis;
            public int MidAxis;
            public int ThinAxis;
            /// <summary>+1 or -1: on which side of the long axis the grip end (sword handle) or the shield's front lies.</summary>
            public float GripSign;
            public bool PivotNearEnd;
            public bool Valid;
        }

        // ----- The rig --------------------------------------------------------------------------

        /// <summary>
        /// Measures a hand from bone positions at the pose the character is in right now. The result is in the hand bone's
        /// own space, so it stays true while the hand moves.
        /// </summary>
        public static bool TryMeasureHand(Animator animator, bool right, out Transform hand, out HandFrame frame)
        {
            frame = default;
            hand = null;
            if (animator == null || !animator.isHuman) return false;

            hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform middle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform index = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform thumb = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform lower = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            if (hand == null) return false;

            Vector3 finger;
            if (middle != null) finger = middle.position - hand.position;
            else if (lower != null) finger = hand.position - lower.position;
            else return false;

            float length = finger.magnitude;
            if (length < 0.001f) return false;
            finger /= length;

            // Across the palm toward the thumb: the thumb decides the sign, the index-minus-middle line gives the direction.
            Vector3 thumbSide = thumb != null ? Vector3.ProjectOnPlane(thumb.position - hand.position, finger) : Vector3.zero;
            Vector3 side = Vector3.zero;
            if (index != null && middle != null) side = Vector3.ProjectOnPlane(index.position - middle.position, finger);
            if (side.magnitude < 0.004f) side = thumbSide;
            else if (thumbSide.sqrMagnitude > 0f && Vector3.Dot(side, thumbSide) < 0f) side = -side;
            if (side.sqrMagnitude < 1e-8f) return false;
            side.Normalize();

            // Right hand: fingers x thumb-side points out of the palm; the left hand is the mirror image.
            Vector3 palm = right ? Vector3.Cross(finger, side) : Vector3.Cross(side, finger);

            frame.Finger = hand.InverseTransformDirection(finger).normalized;
            frame.Side = hand.InverseTransformDirection(side).normalized;
            frame.Palm = hand.InverseTransformDirection(palm).normalized;
            frame.Length = length;
            frame.Valid = true;
            return true;
        }

        /// <summary>
        /// Measures the left forearm. Call it while the wrist is relaxed (the rest pose), because "the back of the hand" is read
        /// from the hand at that moment. Axis and length do not depend on the pose.
        /// </summary>
        public static bool TryMeasureForearm(Animator animator, out Transform forearm, out ForearmFrame frame)
        {
            frame = default;
            forearm = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            if (forearm == null) return false;
            if (!TryMeasureHand(animator, false, out Transform hand, out HandFrame handFrame)) return false;

            Vector3 axisWorld = hand.position - forearm.position;
            float length = axisWorld.magnitude;
            if (length < 0.001f) return false;

            Vector3 axis = forearm.InverseTransformDirection(axisWorld / length).normalized;
            Vector3 back = forearm.InverseTransformDirection(hand.TransformDirection(-handFrame.Palm));
            back = Vector3.ProjectOnPlane(back, axis);
            if (back.sqrMagnitude < 1e-8f) return false;

            frame.Axis = axis;
            frame.Back = back.normalized;
            frame.Length = length;
            frame.Valid = true;
            return true;
        }

        // ----- The weapon model -----------------------------------------------------------------

        /// <summary>
        /// Measures a weapon model from the bounds of its meshes, in the space of its own root: the long axis is the blade (or
        /// the handle and head of a club), the thinnest axis is the flat of a blade or the face of a shield.
        /// </summary>
        public static Shape MeasureShape(GameObject model)
        {
            var shape = new Shape();
            if (model == null) return shape;

            Matrix4x4 toRoot = model.transform.worldToLocalMatrix;
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                if (mesh == null) continue;

                Matrix4x4 toLocal = toRoot * renderer.transform.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = toLocal.MultiplyPoint3x4(corner);
                    if (!any) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    any = true;
                }
            }
            if (!any) return shape;

            shape.Centre = bounds.center;
            shape.Size = bounds.size;

            // Sort the three axes by size.
            int longAxis = 0, thinAxis = 0;
            for (int i = 1; i < 3; i++)
            {
                if (bounds.size[i] > bounds.size[longAxis]) longAxis = i;
                if (bounds.size[i] < bounds.size[thinAxis]) thinAxis = i;
            }
            if (thinAxis == longAxis) thinAxis = (longAxis + 2) % 3;
            shape.LongAxis = longAxis;
            shape.ThinAxis = thinAxis;
            shape.MidAxis = 3 - longAxis - thinAxis;

            // Grip end: the end of the long axis nearer the model's pivot (the Synty weapons are pivoted at the grip).
            float low = bounds.min[longAxis];
            float high = bounds.max[longAxis];
            float length = Mathf.Max(0.0001f, high - low);
            float toLow = Mathf.Abs(low);
            float toHigh = Mathf.Abs(high);
            shape.GripSign = toLow <= toHigh ? -1f : 1f;
            shape.PivotNearEnd = Mathf.Min(toLow, toHigh) <= 0.3f * length;
            shape.Valid = true;
            return shape;
        }

        /// <summary>The unit vector along a model axis (0 = x, 1 = y, 2 = z), in the model root's space.</summary>
        static Vector3 AxisVector(int axis, float sign)
        {
            var v = Vector3.zero;
            v[axis] = sign;
            return v;
        }

        /// <summary>A rotation whose local x, y, z axes are the given (orthonormal, right-handed) vectors.</summary>
        static Quaternion FromBasis(Vector3 y, Vector3 z)
        {
            return Quaternion.LookRotation(z, y);
        }

        // ----- Sword ----------------------------------------------------------------------------

        /// <summary>
        /// Where the sword root goes in the right hand bone's space: the grip in the middle of the fist, the blade out of the
        /// thumb side, raised toward the wrist. The weapon is scaled by <paramref name="scale"/>.
        /// </summary>
        public static bool FitSword(GameObject sword, float scale, HandFrame hand, out Vector3 localPosition, out Quaternion localRotation, out string report)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            report = "no fit";

            Shape shape = MeasureShape(sword);
            if (!shape.Valid || !hand.Valid) return false;

            // Wanted frame in the hand's space: x = blade, y = width of the blade (edge to edge), z = flat of the blade.
            float raise = BladeRaiseDegrees * Mathf.Deg2Rad;
            Vector3 blade = (hand.Side * Mathf.Cos(raise) - hand.Finger * Mathf.Sin(raise)).normalized;
            Vector3 edge = (hand.Finger * Mathf.Cos(raise) + hand.Side * Mathf.Sin(raise)).normalized;
            Vector3 flat = Vector3.Cross(blade, edge);

            // The weapon's own frame: x = grip to tip, y = the middle-sized axis, z = the rest.
            Vector3 modelBlade = AxisVector(shape.LongAxis, -shape.GripSign);
            Vector3 modelEdge = AxisVector(shape.MidAxis, 1f);
            Vector3 modelFlat = Vector3.Cross(modelBlade, modelEdge);

            localRotation = FromBasis(edge, flat) * Quaternion.Inverse(FromBasis(modelEdge, modelFlat));

            // The grip point: on the long axis a bit in from the grip end, in the middle of the other two.
            float low = shape.Centre[shape.LongAxis] - shape.Size[shape.LongAxis] * 0.5f;
            float high = shape.Centre[shape.LongAxis] + shape.Size[shape.LongAxis] * 0.5f;
            Vector3 grip = shape.Centre;
            grip[shape.LongAxis] = shape.GripSign < 0f
                ? low + GripFromEnd * shape.Size[shape.LongAxis]
                : high - GripFromEnd * shape.Size[shape.LongAxis];

            // The middle of the fist: halfway from the wrist to the knuckle, a little toward the palm.
            Vector3 fist = (hand.Finger * PalmAlong + hand.Palm * PalmDeep) * hand.Length;
            localPosition = fist - localRotation * (grip * scale);

            report = DescribeShape(shape, "blade/handle axis");
            return true;
        }

        // ----- Shield ---------------------------------------------------------------------------

        /// <summary>
        /// Where the shield root goes in the left lower arm bone's space: lying on the back of the forearm, its face pointing
        /// away from the arm, its long side along the arm.
        /// </summary>
        public static bool FitShield(GameObject shield, float scale, ForearmFrame arm, out Vector3 localPosition, out Quaternion localRotation, out string report)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            report = "no fit";

            Shape shape = MeasureShape(shield);
            if (!shape.Valid || !arm.Valid) return false;

            // The thinnest axis is the face. Its sign: the front is the side the pivot is farther from (the pivot is on the back).
            float faceLow = shape.Centre[shape.ThinAxis] - shape.Size[shape.ThinAxis] * 0.5f;
            float faceHigh = shape.Centre[shape.ThinAxis] + shape.Size[shape.ThinAxis] * 0.5f;
            float faceSign = Mathf.Abs(faceLow) <= Mathf.Abs(faceHigh) ? 1f : -1f;

            Vector3 modelFace = AxisVector(shape.ThinAxis, faceSign);
            Vector3 modelUp = AxisVector(shape.LongAxis, 1f);

            // Wanted frame in the forearm's space: y = along the arm toward the wrist, z = out of the back of the arm.
            Vector3 up = arm.Axis;
            Vector3 face = Vector3.ProjectOnPlane(arm.Back, up).normalized;
            localRotation = FromBasis(up, face) * Quaternion.Inverse(FromBasis(modelUp, modelFace));

            // The shield's middle: part of the way along the forearm, lifted off the arm by its radius and half the shield's thickness.
            float lift = arm.Length * ForearmRadius + shape.Size[shape.ThinAxis] * 0.5f * scale;
            Vector3 target = arm.Axis * (arm.Length * ShieldAlong) + face * lift;
            localPosition = target - localRotation * (shape.Centre * scale);

            report = DescribeShape(shape, "face axis") + $", face sign {faceSign:+0;-0}";
            return true;
        }

        static string DescribeShape(Shape shape, string what)
        {
            var text = new StringBuilder();
            text.Append("size ").Append(shape.Size.ToString("0.000"));
            text.Append(", long axis ").Append("xyz"[shape.LongAxis]).Append(" (").Append(shape.Size[shape.LongAxis].ToString("0.000")).Append(" m)");
            text.Append(", thin axis ").Append("xyz"[shape.ThinAxis]);
            text.Append(", grip end at ").Append(shape.GripSign < 0f ? "-" : "+").Append("xyz"[shape.LongAxis]);
            text.Append(shape.PivotNearEnd ? " (pivot near that end)" : " (PIVOT NEAR THE MIDDLE: guessed)");
            text.Append(", ").Append(what).Append(" = ").Append("xyz"[shape.LongAxis]);
            return text.ToString();
        }
    }
}
