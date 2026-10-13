using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Cuts the mesh of a Synty toy in two by a box in the toy's local space, so one half can move (a swing seat, a seesaw plank, the
    /// handle of the zip line). A triangle goes to the part its centroid lies in. Both halves are saved as mesh assets under
    /// Assets/CoolSchool/Playground/Meshes (made again on every build). The original renderer keeps the static half (and its collider);
    /// the moving half is a new object under a pivot and has no collider. A renderer whose whole mesh lies in the box moves as it is.
    /// </summary>
    internal static class ToyMeshSplitter
    {
        const string RootFolder = "Assets/CoolSchool";
        const string MeshFolder = RootFolder + "/Playground/Meshes";

        public static Transform Split(ToySetup setup, string partName, Bounds localBox, Vector3 worldPivot)
        {
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Playground");
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder + "/Playground", "Meshes");

            Transform root = setup.Root.transform;
            var pivotObject = new GameObject(partName + "Pivot");
            pivotObject.transform.SetParent(root, false);
            pivotObject.transform.SetPositionAndRotation(worldPivot, root.rotation);
            Transform pivot = pivotObject.transform;

            // Snapshot first: the split adds and moves objects.
            var filters = new List<MeshFilter>(root.GetComponentsInChildren<MeshFilter>(false));
            int index = 0;
            int movedTriangles = 0;
            foreach (MeshFilter filter in filters)
            {
                if (filter == null || filter.sharedMesh == null) continue;
                if (filter.transform.IsChildOf(setup.Anchors) || filter.transform.IsChildOf(pivot)) continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                movedTriangles += SplitOne(setup, filter, renderer, partName, localBox, pivot, index++);
            }

            setup.ClearStatic(pivot);
            setup.Log($"split '{partName}': {movedTriangles} triangle(s) moved to pivot at {ToySetup.Fmt(worldPivot)} "
                + $"(box centre {ToySetup.Fmt(localBox.center)}, size {ToySetup.Fmt(localBox.size)} in the toy's local space)");
            if (movedTriangles == 0)
                setup.Warn($"fallback: the split box for '{partName}' caught no triangles, so the part is empty.");
            return pivot;
        }

        /// <summary>Splits one renderer. Returns how many triangles went to the moving half.</summary>
        static int SplitOne(ToySetup setup, MeshFilter filter, MeshRenderer renderer, string partName, Bounds localBox, Transform pivot, int index)
        {
            Mesh source = filter.sharedMesh;
            Transform root = setup.Root.transform;
            Matrix4x4 toRootLocal = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            Vector3[] vertices = source.vertices;
            int subMeshCount = source.subMeshCount;
            var movingTriangles = new List<int>[subMeshCount];
            var staticTriangles = new List<int>[subMeshCount];
            int moving = 0;
            int stationary = 0;

            for (int s = 0; s < subMeshCount; s++)
            {
                movingTriangles[s] = new List<int>();
                staticTriangles[s] = new List<int>();
                int[] triangles = source.GetTriangles(s);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 centroid = (vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f;
                    bool inside = localBox.Contains(toRootLocal.MultiplyPoint3x4(centroid));
                    List<int> target = inside ? movingTriangles[s] : staticTriangles[s];
                    target.Add(triangles[t]);
                    target.Add(triangles[t + 1]);
                    target.Add(triangles[t + 2]);
                    if (inside) moving++;
                    else stationary++;
                }
            }

            if (moving == 0) return 0;

            // The whole renderer is inside the box: it moves as it is, without its collider.
            if (stationary == 0)
            {
                filter.transform.SetParent(pivot, true);
                foreach (Collider collider in filter.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                return moving;
            }

            string baseName = $"{setup.Root.name}_{partName}_{index}";
            Mesh staticMesh = Build(source, staticTriangles, baseName + "_static");
            Mesh movingMesh = Build(source, movingTriangles, baseName + "_moving");
            Mesh savedStatic = Save(staticMesh, baseName + "_static");
            Mesh savedMoving = Save(movingMesh, baseName + "_moving");

            // The original object keeps the static half and its collider(s).
            foreach (MeshCollider collider in filter.GetComponents<MeshCollider>())
                if (collider.sharedMesh == source) collider.sharedMesh = savedStatic;
            filter.sharedMesh = savedStatic;

            // The moving half: a copy of the renderer under the pivot, at the same world pose, without a collider.
            var part = new GameObject(filter.name + "_" + partName);
            part.layer = filter.gameObject.layer;
            part.transform.SetParent(filter.transform.parent, false);
            part.transform.localPosition = filter.transform.localPosition;
            part.transform.localRotation = filter.transform.localRotation;
            part.transform.localScale = filter.transform.localScale;
            part.transform.SetParent(pivot, true);

            var partFilter = part.AddComponent<MeshFilter>();
            partFilter.sharedMesh = savedMoving;
            var partRenderer = part.AddComponent<MeshRenderer>();
            EditorUtility.CopySerialized(renderer, partRenderer);
            partRenderer.sharedMaterials = renderer.sharedMaterials;
            return moving;
        }

        /// <summary>A mesh with only the given triangles of each submesh, with the vertices renumbered and every vertex stream kept.</summary>
        static Mesh Build(Mesh source, List<int>[] trianglesPerSubMesh, string name)
        {
            int vertexCount = source.vertexCount;
            var remap = new int[vertexCount];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            var used = new List<int>();
            foreach (List<int> triangles in trianglesPerSubMesh)
            {
                foreach (int old in triangles)
                {
                    if (remap[old] >= 0) continue;
                    remap[old] = used.Count;
                    used.Add(old);
                }
            }

            var mesh = new Mesh { name = name };
            if (used.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(Pick(source.vertices, used));
            Vector3[] normals = source.normals;
            if (normals.Length == vertexCount) mesh.SetNormals(Pick(normals, used));
            Vector4[] tangents = source.tangents;
            if (tangents.Length == vertexCount) mesh.SetTangents(Pick(tangents, used));
            Color[] colours = source.colors;
            if (colours.Length == vertexCount) mesh.SetColors(Pick(colours, used));
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector4>();
                source.GetUVs(channel, uvs);
                if (uvs.Count == vertexCount) mesh.SetUVs(channel, Pick(uvs.ToArray(), used));
            }

            mesh.subMeshCount = trianglesPerSubMesh.Length;
            for (int s = 0; s < trianglesPerSubMesh.Length; s++)
            {
                List<int> triangles = trianglesPerSubMesh[s];
                var remapped = new int[triangles.Count];
                for (int i = 0; i < remapped.Length; i++) remapped[i] = remap[triangles[i]];
                mesh.SetTriangles(remapped, s, true);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        static List<T> Pick<T>(T[] all, List<int> indices)
        {
            var result = new List<T>(indices.Count);
            foreach (int index in indices) result.Add(all[index]);
            return result;
        }

        static Mesh Save(Mesh mesh, string name)
        {
            string path = $"{MeshFolder}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
}
