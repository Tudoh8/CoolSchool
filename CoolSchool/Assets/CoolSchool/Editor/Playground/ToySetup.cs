using System.Collections.Generic;
using System.Text;
using CoolSchool.Playground;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The shared helper that every toy's Configure method gets (the contract is Docs/specs/playground-toys-contract.md). It owns the toy's
    /// "ToyAnchors" group, answers height questions with ray casts against the toy's own meshes (temporary probe colliders, removed again
    /// at the end of the configure), makes pivots, splits meshes, and logs everything with the "PlaygroundToys:" prefix.
    /// </summary>
    internal sealed class ToySetup
    {
        public GameObject Root;            // the Synty instance
        public PlaygroundToy Toy;
        public string StickerId;
        public Bounds WorldBounds;         // renderer bounds
        public Transform Anchors;          // child group "ToyAnchors" for anchors, pivots and the exit point
        public ToyPoseOffsets PoseOffsets;

        readonly List<Collider> probes = new List<Collider>();
        bool probesStale = true;
        List<Vector3> vertexCache;

        // ---- Anchors, pivots, parts ----

        /// <summary>Makes an empty transform under the anchors group, at a world pose. The anchor's forward is the way the kid faces.</summary>
        public Transform AddPoint(string name, Vector3 worldPosition, Quaternion worldRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Anchors, false);
            go.transform.SetPositionAndRotation(worldPosition, worldRotation);
            Log($"anchor {name} at {Fmt(worldPosition)}, facing {Fmt(worldRotation * Vector3.forward)}");
            return go.transform;
        }

        /// <summary>
        /// Makes a pivot at a world position (turned like the toy's root, so its local axes are the toy's) under the toy's root and moves the
        /// given transforms under it, keeping their world pose. The static flags of everything that moves are cleared.
        /// </summary>
        public Transform MakePivot(string name, Vector3 worldPivot, params Transform[] moveUnder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root.transform, false);
            go.transform.SetPositionAndRotation(worldPivot, Root.transform.rotation);

            if (moveUnder != null)
            {
                foreach (Transform t in moveUnder)
                {
                    if (t == null || t == Root.transform || go.transform.IsChildOf(t))
                    {
                        Log($"fallback: pivot {name} skipped a transform that cannot be moved.");
                        continue;
                    }
                    t.SetParent(go.transform, true);
                }
            }

            ClearStatic(go.transform);
            Log($"pivot {name} at {Fmt(worldPivot)} with {(moveUnder == null ? 0 : moveUnder.Length)} part(s) under it");
            probesStale = true;
            return go.transform;
        }

        /// <summary>
        /// Cuts the mesh(es) of the toy in two by a box in the toy's local space (a triangle goes to the part its centroid lies in), saves both
        /// halves as mesh assets, and gives the moving half a pivot at the world position. Returns that pivot (the moving half has no collider).
        /// </summary>
        public Transform SplitMesh(string partName, Bounds localBox, Vector3 worldPivot)
        {
            Transform pivot = ToyMeshSplitter.Split(this, partName, localBox, worldPivot);
            probesStale = true;
            vertexCache = null;
            return pivot;
        }

        /// <summary>Clears the static flags (batching) on a transform and all its children, so it can move.</summary>
        public void ClearStatic(Transform t)
        {
            if (t == null) return;

            foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) & ~StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>Adds the routine component to the root, assigns it on the PlaygroundToy and gives it its exit point.</summary>
        public T SetRoutine<T>(Transform exitPoint) where T : ToyRoutine
        {
            T routine = Root.GetComponent<T>();
            if (routine == null) routine = Root.AddComponent<T>();

            PlaygroundPrototypeBuilder.SetObject(routine, "exitPoint", exitPoint);
            PlaygroundPrototypeBuilder.SetObject(Toy, "routine", routine);
            Log($"routine {typeof(T).Name} set, exit point {(exitPoint != null ? Fmt(exitPoint.position) : "none")}");
            return routine;
        }

        public void Log(string message)
        {
            Debug.Log($"PlaygroundToys: {StickerId}: {message}");
        }

        public void Warn(string message)
        {
            Debug.LogWarning($"PlaygroundToys: {StickerId}: {message}");
        }

        // ---- Height questions ----

        /// <summary>A ray straight down onto this toy's own meshes only. Returns the highest hit.</summary>
        public bool TopAt(float x, float z, out Vector3 hit)
        {
            return TopAtFiltered(x, z, null, out hit);
        }

        /// <summary>Like TopAt, but only onto the meshes at or under the given parts (the chute, without the tower round it).</summary>
        public bool TopAtParts(IList<Transform> parts, float x, float z, out Vector3 hit)
        {
            return TopAtFiltered(x, z, parts, out hit);
        }

        bool TopAtFiltered(float x, float z, IList<Transform> parts, out Vector3 hit)
        {
            hit = default;
            EnsureProbes();
            Physics.SyncTransforms();

            float top = WorldBounds.max.y + 3f;
            float reach = top - WorldBounds.min.y + 2f;
            var ray = new Ray(new Vector3(x, top, z), Vector3.down);
            bool found = false;
            float bestY = float.MinValue;
            foreach (Collider probe in probes)
            {
                if (probe == null) continue;
                if (parts != null && !IsUnderAny(probe.transform, parts)) continue;
                if (probe.Raycast(ray, out RaycastHit info, reach) && info.point.y > bestY)
                {
                    bestY = info.point.y;
                    hit = info.point;
                    found = true;
                }
            }
            return found;
        }

        static bool IsUnderAny(Transform t, IList<Transform> parts)
        {
            foreach (Transform part in parts)
                if (part != null && t.IsChildOf(part)) return true;
            return false;
        }

        /// <summary>A ray straight down onto anything except this toy (the ground). Prefers the hit nearest the bottom of the toy.</summary>
        public bool GroundAt(float x, float z, out Vector3 hit)
        {
            hit = default;
            Physics.SyncTransforms();

            float top = WorldBounds.max.y + 6f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, top - WorldBounds.min.y + 30f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestY = float.MinValue;
            float lowestY = float.MaxValue;
            Vector3 lowest = default;
            foreach (RaycastHit info in hits)
            {
                if (info.collider == null || info.collider.transform.IsChildOf(Root.transform)) continue;

                if (info.point.y < lowestY)
                {
                    lowestY = info.point.y;
                    lowest = info.point;
                }
                // The ground is the highest surface that is still near the foot of the toy.
                if (info.point.y <= WorldBounds.min.y + 0.5f && info.point.y > bestY)
                {
                    bestY = info.point.y;
                    hit = info.point;
                    found = true;
                }
            }

            if (!found && lowestY < float.MaxValue)
            {
                hit = lowest;
                found = true;
            }
            return found;
        }

        /// <summary>The ground height at x, z (the toy's own foot height when nothing was hit).</summary>
        public float GroundY(float x, float z)
        {
            return GroundAt(x, z, out Vector3 hit) ? hit.y : WorldBounds.min.y;
        }

        /// <summary>
        /// Writes an ASCII height map of TopAt over the footprint to the log: north (+z) at the top, east (+x) to the right, one character per
        /// cell. The character is the height above the foot of the toy in steps of one cell (0 to 9, then a to z); '.' means nothing there.
        /// </summary>
        public void LogHeightMap(float cell = 0.25f)
        {
            cell = Mathf.Max(0.05f, cell);
            Bounds b = WorldBounds;
            int columns = Mathf.Clamp(Mathf.CeilToInt(b.size.x / cell), 1, 120);
            int rows = Mathf.Clamp(Mathf.CeilToInt(b.size.z / cell), 1, 120);

            var text = new StringBuilder();
            text.AppendLine($"PlaygroundToys: {StickerId}: height map, cell {cell:0.00} m, step {cell:0.00} m per character above y {b.min.y:0.00}; "
                + $"x {b.min.x:0.00}..{b.max.x:0.00} (to the right), z {b.min.z:0.00}..{b.max.z:0.00} (up); toy forward {Fmt(Root.transform.forward)}; "
                + $"bounds {Fmt(b.size)} centre {Fmt(b.center)}");
            for (int row = rows - 1; row >= 0; row--)
            {
                float z = b.min.z + (row + 0.5f) * (b.size.z / rows);
                text.Append("  ");
                for (int column = 0; column < columns; column++)
                {
                    float x = b.min.x + (column + 0.5f) * (b.size.x / columns);
                    if (!TopAt(x, z, out Vector3 hit)) { text.Append('.'); continue; }

                    int step = Mathf.Clamp(Mathf.RoundToInt((hit.y - b.min.y) / cell), 0, 35);
                    text.Append(step < 10 ? (char)('0' + step) : (char)('a' + step - 10));
                }
                text.AppendLine($"   z={z:0.00}");
            }
            Debug.Log(text.ToString());
        }

        // ---- Geometry ----

        /// <summary>Every vertex of the toy's meshes in world space (thinned out to at most about 60000 points).</summary>
        public List<Vector3> WorldVertices()
        {
            if (vertexCache != null) return vertexCache;

            var all = new List<Vector3>();
            var buffer = new List<Vector3>();
            foreach (MeshFilter filter in Root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;

                buffer.Clear();
                filter.sharedMesh.GetVertices(buffer);
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                foreach (Vector3 v in buffer) all.Add(toWorld.MultiplyPoint3x4(v));
            }

            if (all.Count > 60000)
            {
                int stride = Mathf.CeilToInt(all.Count / 60000f);
                var thinned = new List<Vector3>(all.Count / stride + 1);
                for (int i = 0; i < all.Count; i += stride) thinned.Add(all[i]);
                all = thinned;
            }

            vertexCache = all;
            return all;
        }

        /// <summary>Bounds of one object's renderers in world space (an empty bounds at its position if it has none).</summary>
        public static Bounds BoundsOf(Transform t)
        {
            bool found = false;
            Bounds bounds = new Bounds(t.position, Vector3.zero);
            foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(false))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        // ---- Probes ----

        void EnsureProbes()
        {
            if (!probesStale) return;

            RemoveProbes();
            foreach (MeshFilter filter in Root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;

                var probe = filter.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                probes.Add(probe);
            }
            Physics.SyncTransforms();
            probesStale = false;
        }

        void RemoveProbes()
        {
            foreach (Collider probe in probes)
                if (probe != null) Object.DestroyImmediate(probe);
            probes.Clear();
        }

        /// <summary>Called by the builder after the configure: the probe colliders go away again.</summary>
        internal void Finish()
        {
            RemoveProbes();
            probesStale = true;
        }

        internal void Dirty()
        {
            probesStale = true;
            vertexCache = null;
        }

        /// <summary>Runs set on a named serialized field and applies it. Warns when the field does not exist.</summary>
        internal static void SetSerialized(Object target, string field, System.Action<SerializedProperty> set)
        {
            if (target == null) return;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"PlaygroundToys: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            set(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Formatting ----

        internal static string Fmt(Vector3 v)
        {
            return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        }
    }
}
