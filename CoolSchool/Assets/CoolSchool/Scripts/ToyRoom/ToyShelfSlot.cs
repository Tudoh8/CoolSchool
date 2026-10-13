using CoolSchool.Battle;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.ToyRoom
{
    /// <summary>
    /// One place in the Toy Room where one catalog item is shown. The builder puts the slot where the item stands (its origin is
    /// the spot on the shelf, and its forward is the way the item looks: toward the follow camera). The ToyShelf tells the slot
    /// what to show: the real model when the item is owned (gold variants painted gold), or the same model as a black silhouette
    /// when it is not, so he can see there is more to win. The model is made at run time from the catalog's prefab, fitted to
    /// displaySize and stood on the spot (swords and shields stand up, everything else keeps the pose its model has).
    /// </summary>
    public class ToyShelfSlot : MonoBehaviour
    {
        [SerializeField] string itemId;
        [Tooltip("The model's biggest side is scaled to this.")]
        [SerializeField] float displaySize = 0.5f;
        [Tooltip("URP/Unlit, black: put on every renderer slot of a locked item.")]
        [SerializeField] Material silhouetteMaterial;

        const float HopSeconds = 0.55f;
        const float HopScale = 0.25f;
        const float HopHeight = 0.12f;

        Transform holder;
        GameObject model;
        RewardItem shownItem;
        bool shownOwned;
        float hopStart = float.NegativeInfinity;

        public string ItemId => itemId;

        /// <summary>True while the real model (not the silhouette) is shown.</summary>
        public bool IsOwnedShown => model != null && shownOwned;

        /// <summary>True when this slot has any model on display.</summary>
        public bool HasModel => model != null;

        /// <summary>The item on display (null before the first Show).</summary>
        public RewardItem Item => shownItem;

        /// <summary>
        /// Shows the item: the real model when owned, a black silhouette when not. Does nothing when the same thing is already on
        /// display, so it is cheap to call on every progress change.
        /// </summary>
        public void Show(RewardItem item, bool owned)
        {
            if (item == null || item.prefab == null)
            {
                Clear();
                return;
            }

            if (model != null && shownItem == item && shownOwned == owned) return;

            Clear();
            shownItem = item;
            shownOwned = owned;

            holder = new GameObject("Display").transform;
            holder.SetParent(transform, false);

            model = Instantiate(item.prefab, holder);
            model.name = item.id;
            model.transform.localPosition = Vector3.zero;
            // Carts keep their wheels down (the reveal tilt is only there to show the seat while they spin).
            model.transform.localRotation = item.kind == RewardKind.Cart ? Quaternion.identity : Quaternion.Euler(item.revealEuler);
            Prepare(model);

            if (item.kind == RewardKind.Sword || item.kind == RewardKind.Shield) StandUp(model.transform, item.kind == RewardKind.Shield);
            FitAndPlace(model.transform);

            if (owned)
            {
                if (item.IsGold) GoldPaint.Apply(model);
            }
            else
            {
                Blacken(model);
            }
        }

        /// <summary>Takes the model away.</summary>
        public void Clear()
        {
            if (holder != null) DestroyNow(holder.gameObject);
            holder = null;
            model = null;
            shownItem = null;
            shownOwned = false;
        }

        /// <summary>A little hop (a scale bounce) to say hello, for an item he walks past.</summary>
        public void Hop()
        {
            hopStart = Time.time;
        }

        /// <summary>The middle of the item, a little above the shelf, for sparkles.</summary>
        public Vector3 SparklePoint => transform.position + Vector3.up * Mathf.Max(0.2f, displaySize * 0.5f);

        void Update()
        {
            if (holder == null) return;

            float age = Time.time - hopStart;
            if (age < 0f || age > HopSeconds)
            {
                if (holder.localScale != Vector3.one) holder.localScale = Vector3.one;
                if (holder.localPosition != Vector3.zero) holder.localPosition = Vector3.zero;
                return;
            }

            float k = Mathf.Sin(age / HopSeconds * Mathf.PI);
            holder.localScale = Vector3.one * (1f + HopScale * k);
            holder.localPosition = new Vector3(0f, HopHeight * k, 0f);
        }

        void OnDisable()
        {
            if (holder == null) return;
            holder.localScale = Vector3.one;
            holder.localPosition = Vector3.zero;
        }

        // ----- Making the model ----------------------------------------------------------------------

        /// <summary>No colliders (it is a picture, not a thing to bump into) and no shadows (cheaper).</summary>
        static void Prepare(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                DestroyNow(collider);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Every renderer slot of the model gets the black material, so only its outline shows.</summary>
        void Blacken(GameObject root)
        {
            Material black = silhouetteMaterial != null ? silhouetteMaterial : FallbackBlack();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                var materials = new Material[count];
                for (int i = 0; i < count; i++) materials[i] = black;
                renderer.sharedMaterials = materials;
            }
        }

        static Material fallbackBlack;

        static Material FallbackBlack()
        {
            if (fallbackBlack != null) return fallbackBlack;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            fallbackBlack = new Material(shader) { name = "ToyRoomSilhouetteFallback", hideFlags = HideFlags.HideAndDontSave };
            fallbackBlack.SetColor("_BaseColor", Color.black);
            fallbackBlack.SetColor("_Color", Color.black);
            return fallbackBlack;
        }

        /// <summary>
        /// Stands a sword or a shield up with its flat face toward the camera (the slot's forward). The axes come from the weapon's own
        /// measured shape (the same one that puts it in the kid's hand): a sword's blade goes up with its handle at the bottom; a
        /// shield's long side goes up and its front looks at the camera.
        /// </summary>
        void StandUp(Transform item, bool shield)
        {
            WeaponFit.Shape shape = WeaponFit.MeasureShape(item.gameObject);
            if (!shape.Valid || shape.ThinAxis == shape.LongAxis) return;

            Vector3 up;
            Vector3 face;
            if (shield)
            {
                // The front is the side of the thin axis the pivot is farther from (the pivot is on the back).
                float faceLow = shape.Centre[shape.ThinAxis] - shape.Size[shape.ThinAxis] * 0.5f;
                float faceHigh = shape.Centre[shape.ThinAxis] + shape.Size[shape.ThinAxis] * 0.5f;
                up = AxisVector(shape.LongAxis, 1f);
                face = AxisVector(shape.ThinAxis, Mathf.Abs(faceLow) <= Mathf.Abs(faceHigh) ? 1f : -1f);
            }
            else
            {
                up = AxisVector(shape.LongAxis, -shape.GripSign);
                face = AxisVector(shape.ThinAxis, 1f);
            }

            // The model's (up, face) frame is turned onto the slot's (up, forward).
            item.localRotation = Quaternion.Inverse(Quaternion.LookRotation(face, up));
        }

        static Vector3 AxisVector(int axis, float sign)
        {
            var v = Vector3.zero;
            v[axis] = sign;
            return v;
        }
        /// <summary>Scales the model so its biggest side is displaySize and stands it with its bottom middle on the slot's origin.</summary>
        void FitAndPlace(Transform item)
        {
            if (!TryLocalBounds(item.gameObject, transform, out Bounds bounds)) return;

            float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (biggest > 0.0001f) item.localScale *= displaySize / biggest;

            if (!TryLocalBounds(item.gameObject, transform, out bounds)) return;

            // The bounds are in the slot's frame; the model sits under the holder, which has no offset, so slot frame = holder frame.
            item.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }

        /// <summary>
        /// The bounds of all meshes of the root, in the space of another transform (so a slot turned in the world measures the
        /// same as one that is not). Uses each mesh's own bounds, so it is exact for boxes turned by quarter turns.
        /// </summary>
        public static bool TryLocalBounds(GameObject root, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                var meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || !meshRenderer.enabled) continue;

                Encapsulate(ref bounds, ref found, filter.sharedMesh.bounds, filter.transform, space);
            }

            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (skinned.sharedMesh == null) continue;
                Encapsulate(ref bounds, ref found, skinned.sharedMesh.bounds, skinned.transform, space);
            }
            return found;
        }

        static void Encapsulate(ref Bounds bounds, ref bool found, Bounds meshBounds, Transform from, Transform space)
        {
            Vector3 c = meshBounds.center;
            Vector3 e = meshBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 point = space.InverseTransformPoint(from.TransformPoint(corner));
                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        static void DestroyNow(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
