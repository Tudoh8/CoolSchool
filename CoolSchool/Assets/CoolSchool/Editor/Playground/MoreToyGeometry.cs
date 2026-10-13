using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Geometry helpers for the configure methods in PlaygroundToysBuilder.More.cs (monkey bars, seesaw, swings, fort, tyre wall, track ride,
    /// ship): a set of temporary mesh colliders for ray casts onto the toy's own triangles, bounds in the toy's local frame, the moving
    /// parts of a toy, simple poles and a prefab search. The Synty pivots are unknown, so everything is measured, never assumed.
    /// </summary>
    internal static class MoreToyGeometry
    {
        const string RootFolder = "Assets/CoolSchool";
        const string MaterialFolder = RootFolder + "/Materials/Playground";

        // ---- Meshes of a toy ----

        /// <summary>All mesh filters (with a renderer) under the toy's root, not counting the anchors group.</summary>
        public static List<MeshFilter> Meshes(ToySetup s)
        {
            var result = new List<MeshFilter>();
            foreach (MeshFilter filter in s.Root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                if (s.Anchors != null && filter.transform.IsChildOf(s.Anchors)) continue;
                result.Add(filter);
            }
            return result;
        }

        /// <summary>The child "Body" that PrepareToy moved the root's own mesh to (or null).</summary>
        public static Transform BodyOf(ToySetup s)
        {
            return s.Root.transform.Find("Body");
        }

        /// <summary>The mesh objects that are neither the body nor made by the builder: the moving parts the Synty model came with.</summary>
        public static List<Transform> MovingParts(ToySetup s)
        {
            Transform body = BodyOf(s);
            var parts = new List<Transform>();
            foreach (MeshFilter filter in Meshes(s))
            {
                Transform t = filter.transform;
                if (body != null && (t == body || t.IsChildOf(body))) continue;
                if (t.name.StartsWith("Pole", StringComparison.Ordinal) || t.name.StartsWith("Wheel", StringComparison.Ordinal)
                    || t.name.StartsWith("Teddy", StringComparison.Ordinal)) continue;
                parts.Add(t);
            }
            return parts;
        }

        /// <summary>Tight bounds of an object's meshes (its children too) in the frame's local space. False when it has no mesh.</summary>
        public static bool LocalBounds(Transform frame, Transform part, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var corners = new Vector3[8];
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                int i = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            corners[i++] = mesh.center + Vector3.Scale(mesh.extents, new Vector3(sx, sy, sz));

                foreach (Vector3 corner in corners)
                {
                    Vector3 p = toFrame.MultiplyPoint3x4(corner);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return found;
        }

        /// <summary>Tight bounds of all the toy's meshes (not the anchors) in its local space.</summary>
        public static bool LocalBoundsOfToy(ToySetup s, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (MeshFilter filter in Meshes(s))
            {
                if (!LocalBounds(s.Root.transform, filter.transform, out Bounds one)) continue;
                if (!found) { bounds = one; found = true; }
                else bounds.Encapsulate(one);
            }
            return found;
        }

        /// <summary>The renderer bounds of the whole toy (what the touch zone and the height map use), or an empty box.</summary>
        public static void RefreshBounds(ToySetup s)
        {
            if (PlaygroundPrototypeBuilder.TryGetBounds(s.Root, out Bounds bounds)) s.WorldBounds = bounds;
            s.Dirty();
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>Which of two world points is nearer to the middle of the playground (the origin). The toys face it.</summary>
        public static bool NearerToMiddle(Vector3 a, Vector3 b)
        {
            return Flat(a).sqrMagnitude <= Flat(b).sqrMagnitude;
        }

        // ---- Ray casts onto the toy's own triangles ----

        /// <summary>
        /// Temporary non-convex mesh colliders on every mesh of the toy (the originals are convex hulls, which would fool a height
        /// measurement). Disposing removes them again. Rays are cast with Collider.Raycast, so nothing else in the scene is hit.
        /// </summary>
        public sealed class Probe : IDisposable
        {
            readonly List<MeshCollider> colliders = new List<MeshCollider>();

            public Probe(ToySetup s)
            {
                foreach (MeshFilter filter in Meshes(s)) Add(filter);
                Physics.SyncTransforms();
            }

            /// <summary>Probes one object only (a moving part, for example the seat of a swing).</summary>
            public Probe(Transform only)
            {
                foreach (MeshFilter filter in only.GetComponentsInChildren<MeshFilter>(false))
                    if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null) Add(filter);
                Physics.SyncTransforms();
            }

            void Add(MeshFilter filter)
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                colliders.Add(collider);
            }

            /// <summary>The highest surface under the point (x, fromY, z) within the distance: where a ray going straight down first lands.</summary>
            public bool Top(float x, float z, float fromY, float distance, out RaycastHit hit)
            {
                hit = default;
                bool found = false;
                var ray = new Ray(new Vector3(x, fromY, z), Vector3.down);
                foreach (MeshCollider collider in colliders)
                {
                    if (collider == null) continue;
                    if (collider.Raycast(ray, out RaycastHit info, distance) && (!found || info.point.y > hit.point.y))
                    {
                        hit = info;
                        found = true;
                    }
                }
                return found;
            }

            public void Dispose()
            {
                foreach (MeshCollider collider in colliders)
                    if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                colliders.Clear();
                Physics.SyncTransforms();
            }
        }

        // ---- Poles ----

        public static Material PoleMaterial()
        {
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Materials");
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder + "/Materials", "Playground");
            string path = MaterialFolder + "/ToyPole.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            var material = new Material(lit) { name = "ToyPole" };
            material.SetColor("_BaseColor", new Color(0.95f, 0.72f, 0.18f));
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A round pole between two world points, a child of the toy's root, with no collider (he walks past it).</summary>
        public static Transform Pole(ToySetup s, string name, Vector3 bottom, Vector3 top, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(s.Root.transform, false);

            Vector3 axis = top - bottom;
            float length = Mathf.Max(0.05f, axis.magnitude);
            go.transform.SetPositionAndRotation((bottom + top) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis / length));
            // The cylinder primitive is 2 m tall and 1 m wide; the root is not scaled.
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            var renderer = go.GetComponent<MeshRenderer>();
            Material material = PoleMaterial();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        // ---- Prefabs ----

        /// <summary>
        /// The first prefab whose file name contains the word (case-insensitive), preferring the Kids pack, and an exact name over a partial
        /// one. Returns null when there is none.
        /// </summary>
        public static GameObject FindPrefab(string word, string preferredPack = "PolygonKids")
        {
            string best = null;
            int bestScore = int.MinValue;
            foreach (string guid in AssetDatabase.FindAssets(word + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) continue;

                int score = 0;
                if (path.Contains(preferredPack)) score += 100;
                if (string.Equals(file, word, StringComparison.OrdinalIgnoreCase)) score += 50;
                score -= file.Length;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = path;
                }
            }
            return best != null ? AssetDatabase.LoadAssetAtPath<GameObject>(best) : null;
        }

        /// <summary>Removes colliders and scripts from a freshly placed prop, so it is only something to look at.</summary>
        public static void MakeScenery(GameObject go)
        {
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
        }
    }
}
