using System.Collections.Generic;
using System.Linq;
using CoolSchool.Characters;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The three soapbox carts, the rival kids, the announcer with his flag, the spectators, and the two generated reveal prefabs.</summary>
    internal static partial class SoapboxStreetBuilder
    {
        const float CartLength = 1.7f;
        // How far behind the kid's seat the riding buddy sits (the body tail is ~0.5 m behind the seat).
        const float BuddySeatBehind = 0.38f;
        const string StickersPrefabPath = RaceFolder + "/Prefabs/CS_Cart_Stickers.prefab";
        const string GoldPrefabPath = RaceFolder + "/Prefabs/CS_Cart_Gold.prefab";

        /// <summary>One cart body: the catalog item id and the model it is made from.</summary>
        internal readonly struct BodyDef
        {
            public readonly string Id;
            public readonly PrefabRef Prefab;

            public BodyDef(string id, PrefabRef prefab)
            {
                Id = id;
                Prefab = prefab;
            }
        }

        /// <summary>
        /// Every cart body, in catalog order. Each cart gets all of them (only one is shown). Agent G2 adds the new bodies here (the id
        /// must be the catalog id, for example "cart.rocket"); fitting, seats, add-on anchors, paint and the picker follow from this
        /// list and from the catalog. The first body must stay racer 1: the reveal prefabs and the trail height are made from it.
        /// </summary>
        internal static readonly BodyDef[] BodyDefs =
        {
            new BodyDef("cart.racer_1", Kids("SM_Veh_Soapbox_Racer_01")),
            new BodyDef("cart.racer_2", Kids("SM_Veh_Soapbox_Racer_02")),
            new BodyDef("cart.racer_3", Kids("SM_Veh_Soapbox_Racer_03")),

            // The new bodies of the garage (agent G2). How each is put together (length, hidden parts, wheel base, seat) is in
            // SoapboxStreetBuilder.Bodies.cs (BodyStyles); the catalog order is the order here.
            new BodyDef("cart.rocket", Kids("SM_Prop_Cardboard_Rocket_01")),
            new BodyDef("cart.firetruck", new PrefabRef("SM_Veh_Firetruck_01", "PolygonTown")),
            new BodyDef("cart.wagon", Kids("SM_Prop_Red_Wagon_01")),
            new BodyDef("cart.horse", Kids("SM_Veh_Rocking_Horse_01")),
            new BodyDef("cart.police", new PrefabRef("SM_Veh_Car_Police_01", "PolygonCity")),
            new BodyDef("cart.boat", Kids("SM_Prop_Cardboard_Boat_01")),
            new BodyDef("cart.trike", Kids("SM_Veh_Trike_03")),
            new BodyDef("cart.convertible", new PrefabRef("SM_Veh_Convertable_01", "PolygonTown")),
        };

        static readonly PrefabRef[] RivalKidPrefabs =
        {
            Kids("SM_Chr_Kid_Schoolboy_02"),
            Kids("SM_Chr_Kid_Schoolgirl_02"),
        };

        static readonly PrefabRef[] RivalHelmetPrefabs =
        {
            Kids("SM_Chr_Attach_Helmet_Cardboard_02"),
            Kids("SM_Chr_Attach_Helmet_Cardboard_Robot_01"),
        };

        static readonly PrefabRef PlayerHelmetPrefab = Kids("SM_Chr_Attach_Helmet_Cardboard_01");
        static readonly PrefabRef FlagModelPrefab = Kids("SM_Prop_Flag_01");
        static readonly PrefabRef AnnouncerPrefab = Kids("SM_Chr_Father_01");

        static readonly PrefabRef[] SpectatorPrefabs =
        {
            Kids("SM_Chr_Kid_Cheerleader_01"),
            Kids("SM_Chr_Kid_Overalls_01"),
            Kids("SM_Chr_Kid_Summer_01"),
            Kids("SM_Chr_Kid_Tracksuit_01"),
        };

        /// <summary>A cart as built: the root with its controller, looks, the model and the seat.</summary>
        sealed class CartParts
        {
            public GameObject Root;
            public CartController Controller;
            public CartLooks Looks;
            public Transform Model;
            public Transform Seat;
            public Transform BuddySeat;
            public Bounds BodyBounds;
            public Vector3 SeatLocal;
        }

        // ---- Carts ----

        /// <summary>
        /// A cart root at the start line in a lane: CartController and CartLooks on the root, a "Model" child with every body of
        /// <see cref="BodyDefs"/> (only the first shown, each 1.7 m long, nose north), the Seat and BuddySeat under the model, the two
        /// number plates, the inactive add-ons (flag, stickers, eyes, balloons, propeller, spoiler) and, for the player's cart, the inactive
        /// trails. CartLooks is wired with the bodies' seat points and anchors, the add-ons, the paints and the trails.
        /// </summary>
        static CartParts BuildCart(string cartName, int lane, Transform parent, Material gold, bool withTrails, Font font)
        {
            var parts = new CartParts();
            var root = new GameObject(cartName);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(LaneX(lane), RoadY, StartLineZ);
            parts.Root = root;

            parts.Model = NewGroup("Model", root.transform);
            parts.Model.localPosition = Vector3.zero;

            // Every body in BodyDefs, fitted and with its seat and anchor points (FitBody); only the first is shown.
            var bodies = new List<BodyBuild>();
            for (int i = 0; i < BodyDefs.Length; i++)
            {
                // A racer through PlaceBody, one of the new bodies through its recipe (SoapboxStreetBuilder.Bodies.cs); null when the model is missing.
                GameObject body = PlaceBodyDef(BodyDefs[i], parts.Model, root.transform.position, out Bounds bounds, out Vector3 seatWorld);
                if (body == null) continue;

                body.name = i < 3 ? $"Racer_{i + 1:00}" : "Body_" + BodyDefs[i].Id.Replace("cart.", "");
                BodyFit fit = FitBody(BodyDefs[i].Id, body, bounds, seatWorld, root.transform.position);
                ApplyBodyTuning(BodyDefs[i].Id, body, bounds, root.transform.position, fit);
                bodies.Add(new BodyBuild { Id = BodyDefs[i].Id, Root = body, Fit = fit });
                if (bodies.Count == 1)
                {
                    parts.BodyBounds = bounds;
                    parts.SeatLocal = fit.Seat;
                }
            }

            if (bodies.Count == 0)
            {
                Debug.LogWarning("CoolSchool race street: SM_Veh_Soapbox_Racer_01 is missing; the cart is an empty frame with a seat on the road.");
                parts.BodyBounds = new Bounds(root.transform.position + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, CartLength));
                parts.SeatLocal = new Vector3(0f, 0.3f, -0.15f);
            }
            for (int i = 1; i < bodies.Count; i++)
                bodies[i].Root.SetActive(false);

            parts.Seat = NewLocalPoint("Seat", parts.Model, parts.SeatLocal);

            // A riding plush buddy sits behind the kid's seat, on the same surface (the pivot of the buddy is its feet).
            Vector3 buddyLocal = bodies.Count > 0 ? bodies[0].Fit.BuddySeat : parts.SeatLocal + new Vector3(0f, 0.02f, -BuddySeatBehind);
            parts.BuddySeat = NewLocalPoint("BuddySeat", parts.Model, buddyLocal);

            // The add-ons, the number plates and the trails, built once; CartLooks puts them on the body that is shown.
            Vector3 rootWorld = root.transform.position;
            List<AddonBuild> addons = BuildAddons(parts.Model, parts.BodyBounds, rootWorld);
            Transform plateFront = BuildPlate(parts.Model, "PlateFront", false, font, out Text plateFrontText);
            Transform plateBack = BuildPlate(parts.Model, "PlateBack", true, font, out Text plateBackText);
            List<TrailBuild> trails = withTrails ? BuildTrails(root.transform, parts.BodyBounds.min.z - rootWorld.z) : new List<TrailBuild>();
            List<CartLooks.PaintEntry> paints = BuildPaints(bodies, gold);

            parts.Controller = root.AddComponent<CartController>();
            SetObject(parts.Controller, "seat", parts.Seat);
            SetObject(parts.Controller, "buddySeat", parts.BuddySeat);
            SetObject(parts.Controller, "model", parts.Model);
            SetObjectArray(parts.Controller, "wheels", new Object[0]);

            parts.Looks = root.AddComponent<CartLooks>();
            WireLooks(parts.Looks, parts.Seat, parts.BuddySeat, bodies, addons, paints, trails, plateFront, plateBack, plateFrontText, plateBackText);
            return parts;
        }

        /// <summary>
        /// A racer body (nose toward +Z, 1.7 m long, centred on the point, standing on its y) as a child of the parent, and the seat
        /// position found by casting rays down at the body: the surface a kid would sit on, behind the middle of the cart.
        /// </summary>
        static GameObject PlaceBody(GameObject prefab, Transform parent, Vector3 rootWorld, string label, out Bounds bounds, out Vector3 seatWorld)
        {
            GameObject go = Spawn(prefab, parent);
            StripColliders(go);

            bounds = default;
            seatWorld = rootWorld + Vector3.up * 0.3f;
            if (!TryGetBounds(go, out bounds))
            {
                Debug.LogWarning($"CoolSchool race street: the {label} body has no renderers.");
                return go;
            }

            // Long side along Z.
            float yaw = bounds.size.x > bounds.size.z ? 90f : 0f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            TryGetBounds(go, out bounds);
            float longSide = bounds.size.z;
            if (longSide > 0.01f) go.transform.localScale *= CartLength / longSide;
            AlignToBounds(go, rootWorld.x, rootWorld.z, rootWorld.y, false);
            TryGetBounds(go, out bounds);

            using (var probes = new Probes(go))
            {
                bool noseAtHigh = NoseAtHighZFromWheels(go, label) ?? NoseAtHighZ(probes.Colliders, bounds, label);
                if (!noseAtHigh)
                {
                    go.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
                    AlignToBounds(go, rootWorld.x, rootWorld.z, rootWorld.y, false);
                    TryGetBounds(go, out bounds);
                    Physics.SyncTransforms();
                }

                // The seat: the median surface height along the centre line between 25% and 50% of the length from the tail.
                var heights = new List<float>();
                float topY = bounds.max.y + 1f;
                for (float f = 0.25f; f <= 0.501f; f += 0.05f)
                {
                    float z = Mathf.Lerp(bounds.min.z, bounds.max.z, f);
                    if (Hit(probes.Colliders, new Ray(new Vector3(bounds.center.x, topY, z), Vector3.down), bounds.size.y + 2f, out RaycastHit hit))
                        heights.Add(hit.point.y);
                }
                float seatY = bounds.min.y + bounds.size.y * 0.35f;
                if (heights.Count > 0)
                {
                    heights.Sort();
                    seatY = heights[heights.Count / 2];
                }
                else
                {
                    Debug.LogWarning($"CoolSchool race street: no seat surface found on the {label} body; using 35% of its height.");
                }
                seatWorld = new Vector3(bounds.center.x, seatY, Mathf.Lerp(bounds.min.z, bounds.max.z, 0.4f));
                Debug.Log($"CoolSchool race street: {label} body {bounds.size.ToString("0.00")} after scaling; nose {(noseAtHigh ? "kept north" : "turned north")}; "
                    + $"seat height {seatY - rootWorld.y:0.00} m above the road ({heights.Count} samples).");
            }
            return go;
        }

        /// <summary>Temporary mesh colliders on a model (its own are removed first), to cast rays against its triangles.</summary>
        sealed class Probes : System.IDisposable
        {
            readonly List<Collider> list = new List<Collider>();
            public Collider[] Colliders => list.ToArray();

            public Probes(GameObject model)
            {
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null) continue;
                    var probe = filter.gameObject.AddComponent<MeshCollider>();
                    probe.sharedMesh = filter.sharedMesh;
                    list.Add(probe);
                }
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                foreach (Collider probe in list)
                    if (probe != null) Object.DestroyImmediate(probe);
                list.Clear();
            }
        }

        static bool Hit(Collider[] colliders, Ray ray, float distance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            foreach (Collider collider in colliders)
            {
                if (collider == null) continue;
                if (collider.Raycast(ray, out RaycastHit hit, distance) && (!found || hit.distance < best.distance))
                {
                    best = hit;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// The nose end from the named wheel meshes (Wheel_f* = front, Wheel_r* = rear): true when the front wheels sit at the high-Z
        /// end. Null when the model has no such wheels or they are less than 5 cm apart along Z.
        /// </summary>
        static bool? NoseAtHighZFromWheels(GameObject body, string label)
        {
            float frontSum = 0f, rearSum = 0f;
            int frontCount = 0, rearCount = 0;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;
                if (name.IndexOf("Wheel_f", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    frontSum += renderer.bounds.center.z;
                    frontCount++;
                }
                else if (name.IndexOf("Wheel_r", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rearSum += renderer.bounds.center.z;
                    rearCount++;
                }
            }
            if (frontCount == 0 || rearCount == 0) return null;

            float frontZ = frontSum / frontCount, rearZ = rearSum / rearCount;
            if (Mathf.Abs(frontZ - rearZ) < 0.05f) return null;

            bool atHigh = frontZ > rearZ;
            Debug.Log($"CoolSchool race street: {label} nose from wheels: front z {frontZ:0.00}, rear z {rearZ:0.00} -> {(atHigh ? "north" : "south")}.");
            return atHigh;
        }

        /// <summary>
        /// Fallback for models without named wheels: which end of the body (long side along Z) is the nose: the narrower end; if both
        /// are equally wide, the lower end (the back has the backrest); if that tells nothing either, the north end.
        /// </summary>
        static bool NoseAtHighZ(Collider[] colliders, Bounds b, string label)
        {
            float WidthAt(float t)
            {
                float z = Mathf.Lerp(b.min.z, b.max.z, t);
                float best = 0f;
                for (int k = 1; k <= 4; k++)
                {
                    float y = Mathf.Lerp(b.min.y, b.max.y, k / 5f);
                    var from = new Ray(new Vector3(b.min.x - 1f, y, z), Vector3.right);
                    var to = new Ray(new Vector3(b.max.x + 1f, y, z), Vector3.left);
                    float span = b.size.x + 2f;
                    if (Hit(colliders, from, span, out RaycastHit a) && Hit(colliders, to, span, out RaycastHit c))
                        best = Mathf.Max(best, c.point.x - a.point.x);
                }
                return best;
            }

            float TopAt(float t)
            {
                float z = Mathf.Lerp(b.min.z, b.max.z, t);
                float best = float.MinValue;
                for (int k = -2; k <= 2; k++)
                {
                    var ray = new Ray(new Vector3(b.center.x + k * b.size.x * 0.15f, b.max.y + 1f, z), Vector3.down);
                    if (Hit(colliders, ray, b.size.y + 2f, out RaycastHit hit)) best = Mathf.Max(best, hit.point.y);
                }
                return best;
            }

            float wLow = WidthAt(0.12f), wHigh = WidthAt(0.88f);
            float hLow = TopAt(0.12f), hHigh = TopAt(0.88f);
            bool atHigh = true;
            string reason = "no difference found, keeping north";
            if (wLow > 0f && wHigh > 0f && Mathf.Abs(wLow - wHigh) > 0.07f * Mathf.Max(wLow, wHigh))
            {
                atHigh = wHigh < wLow;
                reason = "narrower end";
            }
            else if (hLow > float.MinValue && hHigh > float.MinValue && Mathf.Abs(hLow - hHigh) > 0.05f)
            {
                atHigh = hHigh < hLow;
                reason = "lower end";
            }
            Debug.Log($"CoolSchool race street: {label} nose: widths {wLow:0.00} / {wHigh:0.00}, tops {hLow:0.00} / {hHigh:0.00} -> {(atHigh ? "north" : "south")} ({reason}).");
            return atHigh;
        }

        // The flag (SoapboxStreetBuilder.Garage.cs: BuildCartFlag) and the other add-ons are built there.

        /// <summary>Two star stickers on each side of the body (sprites from the UI kit). Returns the group, or null without a star sprite.</summary>
        static GameObject BuildStickers(Transform parent, Bounds b, Vector3 rootWorld)
        {
            Sprite star = ActivityBeaconBuilder.FindSprite(new[] { "Star" });
            if (star == null)
            {
                Debug.LogWarning("CoolSchool race street: no star sprite found; the cart has no stickers.");
                return null;
            }

            Transform group = NewGroup("Stickers", parent);
            Color[] colours = { new Color(1f, 0.85f, 0.15f), new Color(1f, 0.4f, 0.7f) };
            float width = Mathf.Max(0.01f, star.bounds.size.x);
            float y = b.min.y + b.size.y * 0.55f;
            int index = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float zFraction in new[] { 0.38f, 0.68f })
                {
                    var sticker = new GameObject("Star");
                    sticker.transform.SetParent(group, false);
                    sticker.transform.position = new Vector3(b.center.x + side * (b.size.x * 0.5f + 0.012f), y, Mathf.Lerp(b.min.z, b.max.z, zFraction));
                    sticker.transform.rotation = Quaternion.Euler(0f, side * 90f, 0f);
                    sticker.transform.localScale = Vector3.one * (0.3f / width);
                    var renderer = sticker.AddComponent<SpriteRenderer>();
                    renderer.sprite = star;
                    renderer.color = colours[index++ % colours.Length];
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            return group.gameObject;
        }

        // ---- Rival kids ----

        /// <summary>A seated classmate: a kid under the cart's seat, with a cardboard helmet, no NavMeshAgent. Returns the KidAnimator.</summary>
        static KidAnimator CreateRivalKid(CartParts cart, int index, RaceInputs inputs, Transform parent)
        {
            PrefabRef kidRef = RivalKidPrefabs[index % RivalKidPrefabs.Length];
            string kidName = "Rival_" + kidRef.Name.Replace("SM_Chr_Kid_", "");
            GameObject kid = CreateKid(kidName, kidRef, inputs.KidController, cart.Seat.position, out float height);
            kid.transform.SetParent(cart.Seat, false);
            kid.transform.localPosition = Vector3.zero;
            kid.transform.localRotation = Quaternion.identity;
            kid.transform.localScale = Vector3.one;
            SetLayerRecursive(kid, inputs.KidsLayer);

            var helmet = kid.AddComponent<RaceHelmet>();
            SetObject(helmet, "helmetPrefab", LoadPrefab(RivalHelmetPrefabs[index % RivalHelmetPrefabs.Length]));
            return kid.GetComponent<KidAnimator>();
        }

        // ---- The announcer ----

        /// <summary>
        /// The neighbour with the chequered flag: SM_Chr_Father_01 with the Teacher controller (no TeacherCharacter), standing on his
        /// stand. The flag has its pivot at the grip (the bottom of the pole), local Z forward, and is held in the right hand.
        /// </summary>
        static RaceAnnouncer BuildAnnouncer(StreetLayout layout, RaceInputs inputs, Transform parent)
        {
            var root = new GameObject("Announcer");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(layout.AnnouncerStart.position, layout.AnnouncerStart.rotation);

            Animator animator = null;
            float height = 1.75f;
            GameObject prefab = LoadPrefab(AnnouncerPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    RuntimeAnimatorController controller = inputs.TeacherController != null
                        ? inputs.TeacherController
                        : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RootFolder + "/Animation/Teacher.controller");
                    if (controller != null) animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }
                if (TryGetBounds(model, out Bounds bounds)) height = Mathf.Clamp(bounds.size.y, 1f, 2.2f);
            }
            else
            {
                Debug.LogWarning("CoolSchool race street: SM_Chr_Father_01 was not found; the announcer is an empty object.");
            }

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            Transform flag = BuildHeldFlag(root.transform, animator);

            var announcer = root.AddComponent<RaceAnnouncer>();
            SetObject(announcer, "animator", animator);
            SetObject(announcer, "flag", flag);
            SetLayerRecursive(root, inputs.KidsLayer);
            return announcer;
        }

        /// <summary>
        /// The chequered flag in the right hand: a pivot "Flag" at the grip with the pole going up its local Y and the cloth beside the
        /// top; local Z points forward, so a swing about Z tips the pole sideways. The animator is stepped into its idle pose first
        /// so the flag is upright for the pose he really stands in.
        /// </summary>
        static Transform BuildHeldFlag(Transform announcerRoot, Animator animator)
        {
            var flag = new GameObject("Flag").transform;
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                for (int i = 0; i < 24; i++) animator.Update(0.05f);
            }

            Material pole = Mat("RaceFlagPole", new Color(0.9f, 0.9f, 0.88f), 0.3f);
            Prim(PrimitiveType.Cylinder, "Pole", flag, new Vector3(0f, 0.65f, 0f), new Vector3(0.03f, 0.65f, 0.03f), Quaternion.identity, pole);
            Material cloth = ChequerBannerMaterial("RaceFlagChequer", 1.8f, 1.8f);
            Prim(PrimitiveType.Cube, "Cloth", flag, new Vector3(0f, 1.1f, 0f) + new Vector3(0.27f, 0f, 0f), new Vector3(0.5f, 0.36f, 0.02f), Quaternion.identity, cloth);

            if (hand != null)
            {
                flag.position = hand.position;
                flag.rotation = Quaternion.LookRotation(announcerRoot.forward, Vector3.up);
                flag.SetParent(hand, true);
            }
            else
            {
                Debug.LogWarning("CoolSchool race street: the announcer has no right-hand bone; the flag hangs on his root.");
                flag.SetParent(announcerRoot, false);
                flag.localPosition = new Vector3(0.4f, 0.9f, 0.2f);
            }
            return flag;
        }

        // ---- Spectators ----

        static void BuildSpectators(StreetLayout layout, RaceInputs inputs, Transform parent)
        {
            Transform group = NewGroup("Spectators", parent);
            for (int i = 0; i < layout.SpectatorSpots.Length; i++)
            {
                Transform spot = layout.SpectatorSpots[i];
                PrefabRef kidRef = SpectatorPrefabs[i % SpectatorPrefabs.Length];
                GameObject kid = CreateKid("Spectator_" + kidRef.Name.Replace("SM_Chr_Kid_", ""), kidRef, inputs.KidController, spot.position, out float height);
                kid.transform.SetParent(group, true);
                kid.transform.rotation = spot.rotation;
                SetLayerRecursive(kid, inputs.KidsLayer);
            }
        }

        // ---- Generated reveal prefabs ----

        /// <summary>CS_Cart_Stickers (racer 01 with the stickers) and CS_Cart_Gold (racer 01 in gold), rebuilt each time.</summary>
        static void CreateCartPrefabs(Material gold)
        {
            GameObject body = LoadPrefab(BodyDefs[0].Prefab);
            if (body == null)
            {
                Debug.LogWarning("CoolSchool race street: racer 01 is missing, so the sticker and gold cart prefabs were not made.");
                return;
            }

            foreach ((string path, bool stickers, bool paint) in new[] { (StickersPrefabPath, true, false), (GoldPrefabPath, false, true) })
            {
                var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
                root.transform.position = Vector3.zero;
                GameObject model = PlaceBody(body, root.transform, Vector3.zero, "reveal cart", out Bounds bounds, out _);
                model.name = "Racer_01";
                if (stickers) BuildStickers(root.transform, bounds, Vector3.zero);
                if (paint && gold != null)
                {
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        var painted = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < painted.Length; i++) painted[i] = gold;
                        renderer.sharedMaterials = painted;
                    }
                }

                AssetDatabase.DeleteAsset(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            Debug.Log($"CoolSchool race street: made {StickersPrefabPath} and {GoldPrefabPath}.");
        }
    }
}
