using System.Collections.Generic;
using System.Linq;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The eight new cart bodies of the garage (contract Docs/specs/cart-garage-contract.md, part G2): the rocket, the fire truck, the
    /// wagon, the rocking horse, the police car, the boat, the trike and the convertible. The three racers are placed by
    /// <c>PlaceBody</c>; these get their own recipe (<see cref="BodyStyle"/>): a length, parts that are hidden (the wagon's pull handle,
    /// the fire truck's ladders), a clipped copy of the boat's mesh (its sail is cut off), the rocket lying down, a generated wheel base
    /// for the bodies that have no wheels of their own (rocket, boat, rocking horse), and the seat: where along the body he sits and
    /// which surface the ray probe may pick. Everything the garage needs from a body (seat points, add-on anchors, paint) then comes
    /// from FitBody and BuildPaints like for the racers. All generated names start with Body, so no other builder shares them.
    /// The numbers were worked out from the models' real shapes (top-surface heights along the middle line, see HANDOVER.md).
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        /// <summary>The wheel base lifts the body this high above the road (the plank's top).</summary>
        internal const float WheelBaseHeight = 0.2f;
        const float BaseWheelRadius = 0.15f;
        const float BaseWheelWidth = 0.12f;
        /// <summary>The highest seat surface that is fine: a seated kid with his helmet is about 0.75 m taller, and the gate boards hang at 1.66 m.</summary>
        const float MaxSeatHeight = 0.7f;
        const float SeatedKidHeight = 0.75f;

        /// <summary>Which part of a mesh to keep, in the model's own space (metres, before scaling). A triangle with a corner outside is dropped.</summary>
        readonly struct HullClip
        {
            public readonly float MaxY;
            public readonly float MinZ;
            public readonly float MaxAbsX;

            public HullClip(float maxY, float minZ, float maxAbsX)
            {
                MaxY = maxY;
                MinZ = minZ;
                MaxAbsX = maxAbsX;
            }
        }

        /// <summary>How one of the new bodies is put together. Fractions along the body run from the tail (0) to the nose (1).</summary>
        sealed class BodyStyle
        {
            /// <summary>The name of the generated reveal prefab (CS_Cart_Rocket...), also the catalog seed's prefab.</summary>
            public string RevealName;
            /// <summary>The model's nose-to-tail length in metres (the racers are 1.7). Shorter where the seat would be too high.</summary>
            public float Length = CartLength;
            /// <summary>The model stands upright (the rocket): it is tipped onto its side with the nose forward and the porthole up.</summary>
            public bool LieDown;
            /// <summary>Parts (by object name) that are switched off: they would be in the kid's way or stick out of the cart.</summary>
            public string[] Hide = new string[0];
            /// <summary>When set, the model's mesh is replaced by a copy with only this part of it (the boat without its sail).</summary>
            public HullClip? Clip;
            /// <summary>A generated plank with four chunky wheels under the body (for bodies without wheels).</summary>
            public bool WheelBase;
            public float BaseLength = 1.1f;
            /// <summary>Half the distance between the left and right wheel (centre to centre).</summary>
            public float TrackHalf = 0.36f;
            /// <summary>Where the kid's hips are along the body.</summary>
            public float SeatAlong = 0.4f;
            /// <summary>The ray probe reads the surface under the hips between these two fractions (the seat's height is the median).</summary>
            public float SeatFrom = 0.3f;
            public float SeatTo = 0.45f;
            /// <summary>Part (by object name) whose middle sets the seat's sideways spot: the convertible's kid sits at the steering wheel.</summary>
            public string SeatSidePart;
            /// <summary>How far behind the kid the riding buddy sits, and how far above the kid's seat height.</summary>
            public float BuddyBehind = BuddySeatBehind;
            public float BuddyLift;
            /// <summary>Where the propeller stands, as a fraction along the body (-1 = FitBody's own spot, a little behind the seat), and whether it stands in the middle.</summary>
            public float TopAlong = -1f;
            public bool TopCentre;
            /// <summary>Where the spoiler stands, as a fraction along the body (-1 = FitBody's own spot).</summary>
            public float SpoilerAlong = -1f;
            /// <summary>The back number plate goes on the plank's tail end (the body itself is hollow or curved there).</summary>
            public bool PlateBackOnBase;
            /// <summary>The body is mostly white (the police car), so a paint also tints its white panels (CartPaintMaker's paintWhites). Needs materials no other body uses.</summary>
            public bool PaintWhites;
        }

        /// <summary>The recipes by catalog id. The racers have none (PlaceBody).</summary>
        static readonly Dictionary<string, BodyStyle> BodyStyles = new Dictionary<string, BodyStyle>
        {
            // The cardboard rocket lies down: the kid sits on the narrow tube behind the porthole, the box with the fins is the tail.
            // The box is hollow at its tail, so the back plate goes on the plank.
            ["cart.rocket"] = new BodyStyle
            {
                RevealName = "CS_Cart_Rocket", Length = 1.55f, LieDown = true, WheelBase = true, BaseLength = 1.15f, TrackHalf = 0.36f,
                SeatAlong = 0.45f, SeatFrom = 0.38f, SeatTo = 0.52f, BuddyBehind = 0.35f, BuddyLift = 0.06f, PlateBackOnBase = true,
            },
            // The ladders and the nozzle are hidden (the ladder runs over the whole roof); the kid rides on the flat rear bed.
            ["cart.firetruck"] = new BodyStyle
            {
                RevealName = "CS_Cart_Firetruck", Hide = new[] { "Ladder", "Nozzle" }, SeatAlong = 0.38f, SeatFrom = 0.3f, SeatTo = 0.45f,
            },
            // The pull handle is hidden (it stands 2 m high at this size); the kid sits on the floor of the tub, the spoiler on the tail wall.
            ["cart.wagon"] = new BodyStyle
            {
                RevealName = "CS_Cart_Wagon", Length = 1.6f, Hide = new[] { "Handle" }, SeatAlong = 0.45f, SeatFrom = 0.38f, SeatTo = 0.52f, SpoilerAlong = 0.03f,
            },
            // The seat plate between the backrest and the neck; on the plank base, and short so the saddle is not too high. The propeller
            // stands on the backrest's edge beside his back.
            ["cart.horse"] = new BodyStyle
            {
                RevealName = "CS_Cart_Horse", Length = 1.05f, WheelBase = true, BaseLength = 0.95f, TrackHalf = 0.34f,
                SeatAlong = 0.37f, SeatFrom = 0.32f, SeatTo = 0.42f, BuddyBehind = 0.16f, TopAlong = 0.21f, PlateBackOnBase = true,
            },
            // The kid rides on the roof like on a ride-on toy, in front of the light bar.
            ["cart.police"] = new BodyStyle
            {
                RevealName = "CS_Cart_Police", SeatAlong = 0.38f, SeatFrom = 0.3f, SeatTo = 0.42f, BuddyBehind = 0.2f, PaintWhites = true,
            },
            // Only the hull is kept (the sail, the mast, the tiller and the oar are cut away; the numbers are the model's own metres).
            // The propeller and the spoiler stand on the stern wall.
            ["cart.boat"] = new BodyStyle
            {
                RevealName = "CS_Cart_Boat", Length = 1.5f, Clip = new HullClip(0.46f, -0.44f, 0.30f), WheelBase = true, BaseLength = 1.2f, TrackHalf = 0.5f,
                SeatAlong = 0.36f, SeatFrom = 0.3f, SeatTo = 0.42f, TopAlong = 0.03f, TopCentre = true, SpoilerAlong = 0.03f,
            },
            // Trike 03 is the lowest of the three: the kid sits on the saddle over the rear wheels, and the cart is shorter.
            ["cart.trike"] = new BodyStyle
            {
                RevealName = "CS_Cart_Trike", Length = 1.45f, SeatAlong = 0.22f, SeatFrom = 0.18f, SeatTo = 0.28f, BuddyBehind = 0.2f, TopAlong = 0.1f,
            },
            // The kid sits in the seat at the steering wheel; the buddy rides on the rear deck.
            ["cart.convertible"] = new BodyStyle
            {
                RevealName = "CS_Cart_Convertible", SeatAlong = 0.41f, SeatFrom = 0.38f, SeatTo = 0.46f, SeatSidePart = "SteeringW", BuddyLift = 0.2f,
            },
        };

        // Meshes made during one build are shared by the carts and the reveal prefab (a second DeleteAsset/CreateAsset would orphan the first one's users).
        static readonly Dictionary<string, Mesh> BodyMeshCache = new Dictionary<string, Mesh>();

        /// <summary>Forget the clipped meshes of earlier builds. Call at the start of the carts step of a build.</summary>
        static void ClearBodyCaches()
        {
            BodyMeshCache.Clear();
        }

        /// <summary>
        /// Corrections to what FitBody found for one of the new bodies, while the body is active: where the riding buddy sits, the back
        /// plate on the plank where the body is hollow, and the propeller and spoiler spots the recipe names. Racers keep what FitBody made.
        /// </summary>
        static void ApplyBodyTuning(string bodyId, GameObject body, Bounds bounds, Vector3 rootWorld, BodyFit fit)
        {
            if (!BodyStyles.TryGetValue(bodyId, out BodyStyle style)) return;

            fit.BuddySeat = fit.Seat + new Vector3(0f, 0.02f + style.BuddyLift, -style.BuddyBehind);

            if (style.WheelBase && style.PlateBackOnBase)
                fit.Anchors[(int)CartAnchor.PlateBack] = new Vector3(fit.BoundsCenter.x, 0.16f, fit.BoundsCenter.z - style.BaseLength * 0.5f - 0.01f);

            if (style.TopAlong < 0f && style.SpoilerAlong < 0f) return;

            using (var probes = new Probes(body))
            {
                Collider[] colliders = probes.Colliders;
                if (style.TopAlong >= 0f)
                {
                    float x = style.TopCentre ? bounds.center.x : bounds.center.x + bounds.size.x * 0.2f;
                    float z = Mathf.Lerp(bounds.min.z, bounds.max.z, style.TopAlong);
                    if (ProbeTop(colliders, bounds, x, z, out float y)) fit.Anchors[(int)CartAnchor.Top] = new Vector3(x, y, z) - rootWorld;
                }
                if (style.SpoilerAlong >= 0f)
                {
                    float z = Mathf.Lerp(bounds.min.z, bounds.max.z, style.SpoilerAlong);
                    if (ProbeTop(colliders, bounds, bounds.center.x, z, out float y)) fit.Anchors[(int)CartAnchor.Spoiler] = new Vector3(bounds.center.x, y, z) - rootWorld;
                }
            }
        }

        // ----- Placing a body -----------------------------------------------------------------------

        /// <summary>
        /// A body for the cart's Model (or a reveal prefab): a racer through <see cref="PlaceBody"/>, one of the new bodies through
        /// its <see cref="BodyStyle"/>. Returns the object that holds it (CartLooks switches it on and off as a whole); the bounds cover
        /// the body and its wheel base, and seatWorld is the kid's hip point on it. All bodies stand on the road, nose north, centred on rootWorld.
        /// </summary>
        static GameObject PlaceBodyDef(BodyDef def, Transform parent, Vector3 rootWorld, out Bounds bounds, out Vector3 seatWorld)
        {
            if (!BodyStyles.TryGetValue(def.Id, out BodyStyle style))
            {
                GameObject prefab = LoadPrefab(def.Prefab);
                if (prefab == null)
                {
                    bounds = default;
                    seatWorld = rootWorld + Vector3.up * 0.3f;
                    return null;
                }
                return PlaceBody(prefab, parent, rootWorld, def.Id, out bounds, out seatWorld);
            }
            return PlaceStyledBody(def, style, parent, rootWorld, out bounds, out seatWorld);
        }

        static GameObject PlaceStyledBody(BodyDef def, BodyStyle style, Transform parent, Vector3 rootWorld, out Bounds bounds, out Vector3 seatWorld)
        {
            string label = def.Id;
            bounds = default;
            seatWorld = rootWorld + Vector3.up * 0.3f;

            GameObject prefab = style.Clip.HasValue ? LoadReadablePrefab(def.Prefab) : LoadPrefab(def.Prefab);
            if (prefab == null) return null;

            var holder = new GameObject("Body_" + def.Id.Replace("cart.", ""));
            holder.transform.SetParent(parent, false);
            holder.transform.position = rootWorld;

            GameObject model = Spawn(prefab, holder.transform);
            StripColliders(model);
            HideParts(model, style.Hide);
            if (style.Clip.HasValue) ClipHull(def.Id, model, style.Clip.Value);

            if (style.LieDown)
                model.transform.rotation = Quaternion.AngleAxis(180f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);

            if (!TryGetBounds(model, out Bounds modelBounds))
            {
                Debug.LogWarning($"CoolSchool cart bodies: the {label} body has no renderers.");
                return holder;
            }

            // Long side along Z (the rocket is tipped onto its side first), scaled to the style's length, standing on the plank or the road.
            float yaw = modelBounds.size.x > modelBounds.size.z * 1.05f ? 90f : 0f;
            if (yaw != 0f) model.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.rotation;
            TryGetBounds(model, out modelBounds);
            if (modelBounds.size.z > 0.01f) model.transform.localScale *= style.Length / modelBounds.size.z;
            float baseY = rootWorld.y + (style.WheelBase ? WheelBaseHeight : 0f);
            AlignToBounds(model, rootWorld.x, rootWorld.z, baseY, false);
            TryGetBounds(model, out modelBounds);

            // The nose: from the wheels when the model has them, otherwise the recipe's own direction (nose north).
            bool? noseNorth = NoseAtHighZFromWheels(model, label);
            if (noseNorth == false)
            {
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.rotation;
                AlignToBounds(model, rootWorld.x, rootWorld.z, baseY, false);
                TryGetBounds(model, out modelBounds);
            }
            Physics.SyncTransforms();

            // The seat: the surface under the hips, read from the real mesh.
            float seatX = modelBounds.center.x;
            if (!string.IsNullOrEmpty(style.SeatSidePart))
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(false))
                {
                    if (renderer.name.IndexOf(style.SeatSidePart, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    seatX = Mathf.Clamp(renderer.bounds.center.x, modelBounds.center.x - 0.2f, modelBounds.center.x + 0.2f);
                    break;
                }
            }

            float seatY;
            int samples = 0;
            using (var probes = new Probes(model))
            {
                var heights = new List<float>();
                float topY = modelBounds.max.y + 1f;
                for (float f = style.SeatFrom; f <= style.SeatTo + 0.001f; f += 0.02f)
                {
                    float z = Mathf.Lerp(modelBounds.min.z, modelBounds.max.z, f);
                    foreach (float side in new[] { -0.06f, 0f, 0.06f })
                    {
                        if (Hit(probes.Colliders, new Ray(new Vector3(seatX + side, topY, z), Vector3.down), modelBounds.size.y + 2f, out RaycastHit hit))
                            heights.Add(hit.point.y);
                    }
                }
                samples = heights.Count;
                heights.Sort();
                seatY = heights.Count > 0 ? heights[heights.Count / 2] : modelBounds.min.y + modelBounds.size.y * 0.5f;
                if (heights.Count == 0) Debug.LogWarning($"CoolSchool cart bodies: no seat surface found on the {label} body; using half its height.");
            }
            seatWorld = new Vector3(seatX, seatY, Mathf.Lerp(modelBounds.min.z, modelBounds.max.z, style.SeatAlong));

            if (style.WheelBase) BuildWheelBase(holder.transform, style, modelBounds, rootWorld);

            TryGetBounds(holder, out bounds);
            float seatHeight = seatY - rootWorld.y;
            Debug.Log($"CoolSchool cart bodies: {label} {bounds.size.ToString("0.00")} m (body {modelBounds.size.ToString("0.00")}); seat height {seatHeight:0.00} m above the road "
                + $"at {style.SeatAlong * 100f:0}% along ({samples} samples), seat x {seatX - rootWorld.x:0.00}; kid's top about {seatHeight + SeatedKidHeight:0.00} m; "
                + $"wheel base {(style.WheelBase ? "yes" : "no")}.");
            if (seatHeight > MaxSeatHeight)
                Debug.LogWarning($"CoolSchool cart bodies: the {label} seat is {seatHeight:0.00} m high (wanted at most {MaxSeatHeight:0.00}): with the kid on it he may not fit under the gates; shorten its Length.");
            return holder;
        }

        /// <summary>Switches off every renderer whose object name contains one of the words.</summary>
        static void HideParts(GameObject model, string[] words)
        {
            if (words == null || words.Length == 0) return;

            int hidden = 0;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string objectName = renderer.gameObject.name;
                if (!words.Any(word => objectName.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                renderer.gameObject.SetActive(false);
                hidden++;
            }
            if (hidden == 0) Debug.LogWarning($"CoolSchool cart bodies: nothing to hide on {model.name} ({string.Join(", ", words)}): the model has changed?");
        }

        /// <summary>A see-through material (the windows of the cars). BuildPaints leaves it out, so the glass keeps its colour when the cart is painted.</summary>
        static bool IsGlass(Material material)
        {
            if (material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").a < 0.95f) return true;
            return material.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----- A hull without its sail --------------------------------------------------------------

        /// <summary>The prefab with Read/Write switched on for its meshes (once, in the model importer), so a mesh copy can be made from them.</summary>
        static GameObject LoadReadablePrefab(PrefabRef reference)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            bool reimported = false;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || mesh.isReadable) continue;

                string meshAsset = AssetDatabase.GetAssetPath(mesh);
                var importer = AssetImporter.GetAtPath(meshAsset) as ModelImporter;
                if (importer == null) continue;

                Debug.Log($"CoolSchool cart bodies: switching on Read/Write for {meshAsset} so the hull can be cut out of it.");
                importer.isReadable = true;
                importer.SaveAndReimport();
                reimported = true;
            }
            return reimported ? LoadPrefab(reference) : prefab;
        }

        /// <summary>Replaces each mesh of the model with a saved copy that keeps only the triangles inside the clip. Falls back to the whole mesh if nothing is left.</summary>
        static void ClipHull(string bodyId, GameObject model, HullClip clip)
        {
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null) continue;
                if (!source.isReadable)
                {
                    Debug.LogWarning($"CoolSchool cart bodies: the {bodyId} mesh is not readable, so it keeps its sail.");
                    continue;
                }

                string meshName = "Body" + char.ToUpperInvariant(bodyId[5]) + bodyId.Substring(6) + "Hull_" + source.name;
                if (!BodyMeshCache.TryGetValue(meshName, out Mesh clipped) || clipped == null)
                {
                    Mesh copy = ClippedCopy(source, filter.transform, model.transform, clip, meshName);
                    if (copy == null)
                    {
                        Debug.LogWarning($"CoolSchool cart bodies: nothing was left of the {bodyId} mesh after clipping; it keeps its sail.");
                        continue;
                    }
                    clipped = SaveMesh(copy, meshName);
                    BodyMeshCache[meshName] = clipped;
                    Debug.Log($"CoolSchool cart bodies: {bodyId} hull: kept {clipped.triangles.Length / 3} of {source.triangles.Length / 3} triangles.");
                }
                filter.sharedMesh = clipped;
            }
        }

        static Mesh ClippedCopy(Mesh source, Transform meshSpace, Transform modelSpace, HullClip clip, string meshName)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            Vector2[] uvs = source.uv;
            Color[] colours = source.colors;
            int[] triangles = source.triangles;
            Matrix4x4 toModel = modelSpace.worldToLocalMatrix * meshSpace.localToWorldMatrix;

            var map = new Dictionary<int, int>();
            var keptVertices = new List<Vector3>();
            var keptNormals = new List<Vector3>();
            var keptTangents = new List<Vector4>();
            var keptUvs = new List<Vector2>();
            var keptColours = new List<Color>();
            var keptTriangles = new List<int>();

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                bool keep = true;
                for (int k = 0; k < 3 && keep; k++)
                {
                    Vector3 p = toModel.MultiplyPoint3x4(vertices[triangles[t + k]]);
                    if (p.y > clip.MaxY || p.z < clip.MinZ || Mathf.Abs(p.x) > clip.MaxAbsX) keep = false;
                }
                if (!keep) continue;

                for (int k = 0; k < 3; k++)
                {
                    int old = triangles[t + k];
                    if (!map.TryGetValue(old, out int index))
                    {
                        index = keptVertices.Count;
                        map[old] = index;
                        keptVertices.Add(vertices[old]);
                        if (normals.Length == vertices.Length) keptNormals.Add(normals[old]);
                        if (tangents.Length == vertices.Length) keptTangents.Add(tangents[old]);
                        if (uvs.Length == vertices.Length) keptUvs.Add(uvs[old]);
                        if (colours.Length == vertices.Length) keptColours.Add(colours[old]);
                    }
                    keptTriangles.Add(index);
                }
            }
            if (keptTriangles.Count == 0) return null;

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = keptVertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(keptVertices);
            if (keptNormals.Count == keptVertices.Count) mesh.SetNormals(keptNormals);
            if (keptTangents.Count == keptVertices.Count) mesh.SetTangents(keptTangents);
            if (keptUvs.Count == keptVertices.Count) mesh.SetUVs(0, keptUvs);
            if (keptColours.Count == keptVertices.Count) mesh.SetColors(keptColours);
            mesh.SetTriangles(keptTriangles, 0);
            if (keptNormals.Count != keptVertices.Count) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ----- The wheel base -----------------------------------------------------------------------

        /// <summary>
        /// A soapbox chassis under a body without wheels: a low wooden plank on two axles with four chunky wheels (dark tyres, yellow
        /// hubs). The plank's top is <see cref="WheelBaseHeight"/> above the road and the body stands on it.
        /// </summary>
        static void BuildWheelBase(Transform holder, BodyStyle style, Bounds model, Vector3 rootWorld)
        {
            Transform group = NewGroup("WheelBase", holder);
            Material wood = Mat("BodyBasePlank", new Color(0.86f, 0.62f, 0.34f), 0.25f);
            Material tyre = Mat("BodyBaseTyre", new Color(0.1f, 0.1f, 0.12f), 0.3f);
            Material hub = Mat("BodyBaseHub", new Color(1f, 0.82f, 0.2f), 0.5f);
            Material metal = Mat("BodyBaseAxle", new Color(0.55f, 0.57f, 0.6f), 0.5f, 0.4f);

            float cx = model.center.x - rootWorld.x;
            float cz = model.center.z - rootWorld.z;
            float half = style.TrackHalf;
            float wheelZ = style.BaseLength * 0.5f - 0.14f;
            float plankWidth = 2f * (half - BaseWheelWidth * 0.5f) - 0.04f;

            Prim(PrimitiveType.Cube, "Plank", group, new Vector3(cx, WheelBaseHeight - 0.035f, cz), new Vector3(plankWidth, 0.07f, style.BaseLength),
                Quaternion.identity, wood);

            foreach (float zSign in new[] { -1f, 1f })
            {
                float z = cz + zSign * wheelZ;
                Prim(PrimitiveType.Cylinder, "Axle", group, new Vector3(cx, BaseWheelRadius, z), new Vector3(0.06f, half, 0.06f), Quaternion.Euler(0f, 0f, 90f), metal);
                Prim(PrimitiveType.Cube, "AxleBlock", group, new Vector3(cx, WheelBaseHeight - 0.09f, z), new Vector3(0.16f, 0.07f, 0.12f), Quaternion.identity, wood);

                foreach (float side in new[] { -1f, 1f })
                {
                    float x = cx + side * half;
                    Prim(PrimitiveType.Cylinder, "Wheel", group, new Vector3(x, BaseWheelRadius, z),
                        new Vector3(BaseWheelRadius * 2f, BaseWheelWidth * 0.5f, BaseWheelRadius * 2f), Quaternion.Euler(0f, 0f, 90f), tyre);
                    Prim(PrimitiveType.Cylinder, "Hub", group, new Vector3(x + side * (BaseWheelWidth * 0.5f), BaseWheelRadius, z),
                        new Vector3(0.12f, 0.015f, 0.12f), Quaternion.Euler(0f, 0f, 90f), hub);
                }
            }
        }

        // ----- The reveal prefabs -------------------------------------------------------------------

        /// <summary>
        /// CS_Cart_Rocket, CS_Cart_Firetruck... one for each new body, rebuilt each time: the body with its wheel base (no kid, no paint), nose
        /// north, standing on the origin. They are the present's model in the reveal and the picker's and the pit lane's picture. The catalog seeder
        /// finds them by path, so this runs before the catalog is seeded.
        /// </summary>
        static void CreateBodyPrefabs()
        {
            EnsureFolder(RaceFolder, "Prefabs");

            int made = 0;
            foreach (BodyDef def in BodyDefs)
            {
                if (!BodyStyles.TryGetValue(def.Id, out BodyStyle style)) continue;

                var root = new GameObject(style.RevealName);
                root.transform.position = Vector3.zero;
                GameObject body = PlaceBodyDef(def, root.transform, Vector3.zero, out _, out _);
                if (body == null)
                {
                    Debug.LogWarning($"CoolSchool cart bodies: the model of {def.Id} is missing, so {style.RevealName} was not made.");
                    Object.DestroyImmediate(root);
                    continue;
                }

                string path = RaceFolder + "/Prefabs/" + style.RevealName + ".prefab";
                AssetDatabase.DeleteAsset(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                made++;
            }
            Debug.Log($"CoolSchool cart bodies: made {made} of {BodyStyles.Count} reveal prefabs under {RaceFolder}/Prefabs.");
        }
    }
}
