using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The dragon parts of the castle adventure: the seat on its back (for the ride), the baby dragon prefab, the goblins' dizzy stars.</summary>
    internal static partial class CastleAdventureBuilder
    {
        const float BabyDragonHeight = 0.6f;
        /// <summary>The seat stands this far above the highest point of the back (when no saddle is measured).</summary>
        const float SeatOverBack = 0.05f;
        /// <summary>Back vertices count when they are this close (horizontally) to the spine bone.</summary>
        const float SeatSearchRadius = 0.45f;

        /// <summary>Which model the tiny goblin turned out to be (for the summary line).</summary>
        internal static string TinyGoblinModel = "";

        /// <summary>What was used for the dragon's saddle (for the summary line).</summary>
        static string saddleSummary = "";

        // ----- The seat on the dragon's back ------------------------------------------------------------

        /// <summary>
        /// A "Seat" point on top of the dragon's back, a child of its spine bone (English DragonSpine2, else Spine1, else the root at 60%
        /// of the model's height) and facing the dragon's forward (+Z of the dragon root). The saddle that the model already has is
        /// turned on (a mesh named Saddle, else Carapace, else Bridle, or a Saddle blend shape); without one, a red flat saddle is added.
        /// Called while the dragon root is still at the origin, turned the way it will be once it is placed.
        /// </summary>
        internal static Transform CreateDragonSeat(GameObject model, Transform dragonRoot)
        {
            Transform spine = FindBone(model, "DragonSpine2") ?? FindBone(model, "DragonSpine1");
            bool fallbackParent = spine == null;
            float modelHeight = TryGetBounds(model, out Bounds modelBounds) ? modelBounds.size.y : 3.5f;
            Vector3 anchor = fallbackParent ? dragonRoot.position + Vector3.up * modelHeight * 0.6f : spine.position;

            // 1. A saddle, carapace or bridle the model already has: turn it on.
            string saddleUsed = EnableSaddle(model, out Renderer saddleRenderer);

            // 2. The top of the back: the saddle's top when there is one, else the highest body vertex close to the spine, plus 5 cm.
            float seatY = anchor.y + 0.4f;
            Vector2 seatXZ = new Vector2(anchor.x, anchor.z);
            string measured = "estimated from the spine bone";
            if (saddleRenderer != null && TryMeasureTop(saddleRenderer, anchor, SeatSearchRadius * 1.5f, modelBounds, out float saddleTop))
            {
                seatY = saddleTop;
                measured = "top of the " + saddleRenderer.name + " mesh";
                if (TryGetBounds(saddleRenderer.gameObject, out Bounds saddleBounds)) seatXZ = new Vector2(saddleBounds.center.x, saddleBounds.center.z);
            }
            else
            {
                Renderer body = FindBodyRenderer(model);
                if (body != null && TryMeasureTop(body, anchor, SeatSearchRadius, modelBounds, out float backTop))
                {
                    seatY = backTop + SeatOverBack;
                    measured = "highest point of " + body.name + " over the spine + " + SeatOverBack.ToString("0.00") + " m";
                }
            }

            var seat = new GameObject("Seat").transform;
            seat.SetParent(fallbackParent ? dragonRoot : spine, true);
            seat.SetPositionAndRotation(new Vector3(seatXZ.x, seatY, seatXZ.y), Quaternion.identity);

            if (saddleRenderer == null)
            {
                // No saddle on the model: a red flat saddle (a flattened box) sitting on the back, under the seat point.
                Transform saddle = MakePrimitive(PrimitiveType.Cube, "Saddle", seat, Vector3.zero, Vector3.one, Quaternion.identity, SaddleMat);
                SetWorldScale(saddle, new Vector3(0.5f, 0.08f, 0.7f));
                saddle.position = seat.position - Vector3.up * 0.04f;
                saddleUsed = "a red flat box added on the seat (the model has no saddle)";
            }
            saddleSummary = saddleUsed;

            Debug.Log($"CoolSchool castle: dragon seat on {(fallbackParent ? "the dragon root (no spine bone)" : spine.name)} at {seat.position.ToString("0.00")}, "
                + $"{measured}; saddle: {saddleUsed}.");
            return seat;
        }

        static Transform FindBone(GameObject model, string text)
        {
            return model.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.GetComponent<Renderer>() == null && t.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Turns on the model's own saddle: the first renderer named Saddle, else Carapace, else Bridle (its object and parents are
        /// activated), and any "Saddle" blend shape is set to full. Returns what was used, or an empty text.
        /// </summary>
        static string EnableSaddle(GameObject model, out Renderer renderer)
        {
            renderer = null;
            Renderer[] all = model.GetComponentsInChildren<Renderer>(true);
            string used = "";

            foreach (string word in new[] { "saddle", "carapace", "bridle" })
            {
                Renderer found = all.FirstOrDefault(r => r.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (found == null) continue;

                for (Transform t = found.transform; t != null && t != model.transform.parent; t = t.parent)
                    t.gameObject.SetActive(true);
                found.enabled = true;
                renderer = found;
                used = "the model's own '" + found.name + "' mesh turned on";
                break;
            }

            foreach (SkinnedMeshRenderer skinned in all.OfType<SkinnedMeshRenderer>())
            {
                if (skinned.sharedMesh == null) continue;

                for (int i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                {
                    if (skinned.sharedMesh.GetBlendShapeName(i).IndexOf("saddle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    skinned.SetBlendShapeWeight(i, 100f);
                    if (renderer == null) renderer = skinned;
                    used = (used.Length > 0 ? used + "; " : "") + "blend shape '" + skinned.sharedMesh.GetBlendShapeName(i) + "' set to 100";
                }
            }
            return used;
        }

        /// <summary>The biggest skinned mesh named Body, else the biggest skinned mesh, else the biggest mesh.</summary>
        static Renderer FindBodyRenderer(GameObject model)
        {
            Renderer[] all = model.GetComponentsInChildren<Renderer>(false).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();
            Renderer named = all.Where(r => r.name.IndexOf("body", System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
            return named ?? all.OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
        }

        /// <summary>
        /// The highest world y among the renderer's vertices (as posed right now) that lie within `radius` metres, horizontally, of the
        /// anchor. Skinned meshes are baked first. False when too few vertices are found or the result is not plausible (outside the
        /// model's bounds), so the caller can fall back.
        /// </summary>
        static bool TryMeasureTop(Renderer renderer, Vector3 anchor, float radius, Bounds modelBounds, out float top)
        {
            top = 0f;
            Mesh mesh = null;
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
            bool temporary = false;

            try
            {
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    mesh = new Mesh();
                    temporary = true;
                    skinned.BakeMesh(mesh, false);
                }
                else if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }

                if (mesh == null) return false;

                Vector3[] vertices = mesh.vertices;
                int near = 0;
                float best = float.MinValue;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                    float dx = world.x - anchor.x;
                    float dz = world.z - anchor.z;
                    if (dx * dx + dz * dz > radius * radius) continue;

                    near++;
                    best = Mathf.Max(best, world.y);
                }

                if (near < 3) return false;
                if (modelBounds.size.y > 0.01f && (best < modelBounds.min.y || best > modelBounds.max.y + 0.5f)) return false;

                top = best;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("CoolSchool castle: could not measure '" + renderer.name + "': " + exception.Message);
                return false;
            }
            finally
            {
                if (temporary && mesh != null) Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>Sets a transform's world scale (the bone it hangs from may be scaled).</summary>
        static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(
                worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)),
                worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)),
                worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parent.z)));
        }

        // ----- The baby dragon prefab ---------------------------------------------------------------------

        /// <summary>
        /// Creates (or overwrites) Assets/CoolSchool/Prefabs/BabyDragon.prefab: the Dragon_LP model with the vendor scripts removed, about
        /// 0.6 m tall, its Animator on the Dragon controller, no colliders, the pivot at its feet, and without the saddle and bridle.
        /// Works on its own (the reward catalog seeder calls it); the prefab keeps its GUID when it is overwritten.
        /// </summary>
        internal static GameObject EnsureBabyDragonPrefab()
        {
            GameObject source = LoadPrefab(DragonModelPrefab);
            if (source == null)
            {
                Debug.LogWarning("CoolSchool castle: the Dragon_LP model is missing, so there is no baby dragon prefab.");
                return null;
            }

            EnsureFolder(RootFolder, "Prefabs");

            var root = new GameObject("BabyDragon");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            KnightsArenaBuilder.StripForeignScripts(model);

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DragonControllerPath);
                if (controller != null) animator.runtimeAnimatorController = controller;
                else Debug.LogWarning("CoolSchool castle: the Dragon controller is missing; the baby dragon keeps the vendor controller.");
            }

            KnightsArenaBuilder.FaceModelForward(model, KnightsArenaBuilder.FindMouthBone(model));

            // A baby has no saddle, bridle or armour.
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string rendererName = renderer.name.ToLowerInvariant();
                if (rendererName.Contains("saddle") || rendererName.Contains("bridle") || rendererName.Contains("reins") || rendererName.Contains("carapace"))
                    renderer.gameObject.SetActive(false);
            }

            if (TryGetBounds(model, out Bounds measured) && measured.size.y > 0.01f)
                model.transform.localScale *= BabyDragonHeight / measured.size.y;
            AlignToBounds(model, 0f, 0f, 0f, false);

            StripColliders(root);

            string path = BabyDragonPrefabPath;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"CoolSchool castle: baby dragon prefab saved to {path} ({BabyDragonHeight:0.0} m tall, pivot at the feet).");
            return prefab;
        }

        // ----- Dizzy stars for the goblins ----------------------------------------------------------------

        /// <summary>
        /// A ring of three small glowing yellow balls (the ring is 0.35 m wide) just above a goblin's head, hidden. BattleOpponentAI
        /// spins it and shows it while the goblin is dizzy.
        /// </summary>
        internal static GameObject CreateDizzyStars(Transform kid, float height, Material material)
        {
            var root = new GameObject("DizzyStars");
            root.transform.SetParent(kid, false);
            root.transform.localPosition = new Vector3(0f, height + 0.12f, 0f);

            const int count = 3;
            const float ringRadius = 0.175f;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count * Mathf.Deg2Rad;
                MakePrimitive(PrimitiveType.Sphere, "Star_" + (i + 1), root.transform, new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius),
                    Vector3.one * 0.09f, Quaternion.identity, material);
            }

            root.SetActive(false);
            return root;
        }
    }
}
