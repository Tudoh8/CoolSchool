using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoolSchool.Activities;
using CoolSchool.CameraRig;
using CoolSchool.Player;
using CoolSchool.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// A way to SEE the ice cream shop without playing: builds the playground, then (without saving) stages a customer with
    /// a 3-scoop cone and the coins, renders the shop camera to PNG files at 16:9 and 4:3, renders the follow camera's view
    /// of the stall from the shop zone, and logs where the key points land on the screen. The staging is thrown away afterwards.
    /// Run with: Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.ShopPreviewCapture.BuildAndCaptureFromCommandLine
    /// Pictures go to Logs/shop-preview/. Log lines start with "ShopPreview:".
    /// </summary>
    public static class ShopPreviewCapture
    {
        const string LogPrefix = "ShopPreview: ";
        // The repository's Logs folder (the Unity project is one folder down from the repository root).
        static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Logs/shop-preview"));
        const float InsideMin = 0.03f;
        const float InsideMax = 0.97f;
        const int CoinCount = 3;
        const float MarkerSize = 0.14f;

        /// <summary>Builds and saves the playground like CommandLineBuild, then renders the previews. Exits 0 on success, 1 on failure.</summary>
        public static void BuildAndCaptureFromCommandLine()
        {
            int code = 1;
            try
            {
                if (!PlaygroundPrototypeBuilder.RunBuild())
                {
                    Debug.LogError(LogPrefix + "the playground build was cancelled.");
                }
                else
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log(LogPrefix + "playground built and saved; staging the previews (not saved).");
                    Capture();
                    code = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        // ----- The scene's shop, read back through its serialized fields -----------------------------

        sealed class ShopRefs
        {
            public IceCreamShop Shop;
            public Transform OrderSpot;
            public Transform ShopkeeperSpot;
            public Transform ShopCamera;
            public Transform CoinSpot;
            public Transform ConeHolder;
            public IceCreamCone ConeTemplate;
            public CashRegister Register;
            public Material Gold;
            public float DragonExtra;
            public ShopCustomer[] Customers;
            public FlavourTub[] Tubs;
            public Transform Stall;
        }

        static ShopRefs ReadShop()
        {
            var shop = UnityEngine.Object.FindFirstObjectByType<IceCreamShop>(FindObjectsInactive.Include);
            if (shop == null) throw new InvalidOperationException("no IceCreamShop in the scene");

            var so = new SerializedObject(shop);
            var refs = new ShopRefs
            {
                Shop = shop,
                OrderSpot = Ref<Transform>(so, "orderSpot"),
                ShopkeeperSpot = Ref<Transform>(so, "shopkeeperSpot"),
                ShopCamera = Ref<Transform>(so, "shopCamera"),
                CoinSpot = Ref<Transform>(so, "coinSpot"),
                ConeHolder = Ref<Transform>(so, "coneHolder"),
                ConeTemplate = Ref<IceCreamCone>(so, "coneTemplate"),
                Register = Ref<CashRegister>(so, "register"),
                Gold = Ref<Material>(so, "goldMaterial"),
                DragonExtra = so.FindProperty("dragonExtraDistance").floatValue,
                Customers = Array<ShopCustomer>(so, "customers"),
                Tubs = Array<FlavourTub>(so, "tubs"),
            };
            refs.Stall = refs.ConeHolder.parent;
            return refs;
        }

        static T Ref<T>(SerializedObject so, string field) where T : UnityEngine.Object
        {
            return so.FindProperty(field).objectReferenceValue as T;
        }

        static T[] Array<T>(SerializedObject so, string field) where T : UnityEngine.Object
        {
            SerializedProperty list = so.FindProperty(field);
            var result = new T[list.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as T;
            return result;
        }

        // ----- The three previews --------------------------------------------------------------------

        static void Capture()
        {
            Directory.CreateDirectory(OutputFolder);

            Camera cam = Camera.main;
            if (cam == null) throw new InvalidOperationException("no main camera");
            ShopRefs shop = ReadShop();

            var follow = cam.GetComponent<FollowCamera>();
            if (follow != null) follow.enabled = false;

            string scenePath = EditorSceneManager.GetActiveScene().path;

            // (a) the kid and (b) the dragon, at the shop camera.
            foreach (int customerIndex in new[] { 0, 4 })
            {
                if (customerIndex >= shop.Customers.Length || shop.Customers[customerIndex] == null) continue;

                string name = shop.Customers[customerIndex].Kind == CustomerKind.Dragon ? "dragon" : "kid";
                var staged = new List<GameObject>();
                StageCustomerScene(shop, customerIndex, staged, out Transform anchor);

                cam.transform.SetPositionAndRotation(shop.ShopCamera.position, shop.ShopCamera.rotation);
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1600, 1200) })
                {
                    string label = name + "_" + (size.x == 1920 ? "16x9" : "4x3");
                    RenderAndSave(cam, size, label);
                    CheckShopPoints(cam, label, shop, anchor, size);
                }

                foreach (GameObject go in staged) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                staged.Clear();
                shop.Customers[customerIndex].gameObject.SetActive(false);
            }

            // (c) the follow camera's view from the shop zone, looking north at the stall.
            StagePlaygroundView(cam, follow, shop);
            RenderAndSave(cam, new Vector2Int(1920, 1080), "playground_16x9");
            CheckSign(cam, "playground_16x9", shop, new Vector2Int(1920, 1080));

            // (d) a close look at the sign from the south, to judge its shape and size.
            Transform sign = shop.Stall.Find("IceCreamSign");
            if (sign != null)
            {
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer r in sign.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                    first = false;
                }
                Vector3 eye = bounds.center + shop.Stall.forward * 8f + Vector3.up * 0.5f;
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bounds.center - eye, Vector3.up));
                RenderAndSave(cam, new Vector2Int(1280, 960), "sign_closeup");
            }

            cam.ResetAspect();

            // Throw the staging away: the saved scene is the plain build.
            if (!string.IsNullOrEmpty(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Debug.Log(LogPrefix + "staging discarded, scene reloaded from " + scenePath);
            }
            Debug.Log(LogPrefix + "pictures are in " + Path.GetFullPath(OutputFolder));
        }

        // ----- Staging ---------------------------------------------------------------------------

        static void StageCustomerScene(ShopRefs shop, int customerIndex, List<GameObject> staged, out Transform anchor)
        {
            foreach (ShopCustomer other in shop.Customers) if (other != null) other.gameObject.SetActive(false);

            ShopCustomer customer = shop.Customers[customerIndex];
            bool dragon = customer.Kind == CustomerKind.Dragon;
            Vector3 position = shop.OrderSpot.position;
            if (dragon) position -= shop.OrderSpot.forward * shop.DragonExtra;
            customer.transform.SetPositionAndRotation(position, shop.OrderSpot.rotation);
            customer.gameObject.SetActive(true);
            foreach (Animator animator in customer.GetComponentsInChildren<Animator>())
            {
                // The animator does not run in edit mode: step it so the customer is not left in the bind pose.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);
                animator.Update(0.4f);
            }

            // At runtime the zones hide their glow while an activity runs, and the player stands at the shopkeeper spot.
            HideZoneVisuals();
            StagePlayer(shop);
            anchor = customer.BubbleAnchor;

            // A cone with three different scoops in the holder.
            IceCreamCone cone = UnityEngine.Object.Instantiate(shop.ConeTemplate, shop.ConeHolder.position, shop.ConeHolder.rotation, shop.Stall);
            cone.gameObject.SetActive(true);
            cone.name = "PreviewCone";
            staged.Add(cone.gameObject);
            for (int i = 0; i < 3; i++)
            {
                FlavourTub tub = shop.Tubs[i % shop.Tubs.Length];
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "PreviewScoop" + i;
                UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.GetComponent<MeshRenderer>().sharedMaterial = tub.ScoopMaterial;
                ball.transform.SetParent(cone.transform, true);
                ball.transform.position = cone.ScoopPosition(i);
                ball.transform.localScale = new Vector3(ScoopBall.Radius * 2f, ScoopBall.Radius * 2f * 0.9f, ScoopBall.Radius * 2f);
                staged.Add(ball);
            }

            // The coins, exactly where the game puts them (same position helper, same rotation, same size).
            for (int i = 0; i < CoinCount; i++)
            {
                Vector3 coinPosition = IceCreamShop.CoinPosition(shop.CoinSpot, i, CoinCount);
                var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.name = "PreviewCoin" + i;
                UnityEngine.Object.DestroyImmediate(coin.GetComponent<Collider>());
                if (shop.Gold != null) coin.GetComponent<MeshRenderer>().sharedMaterial = shop.Gold;
                coin.transform.SetPositionAndRotation(coinPosition, ShopCoin.LieRotation(shop.ShopCamera.position - coinPosition));
                coin.transform.localScale = new Vector3(ShopCoin.Diameter, ShopCoin.Thickness * 0.5f, ShopCoin.Diameter);
                staged.Add(coin);
            }

            // The order bubble is a screen overlay and does not render: a magenta ball marks its anchor.
            staged.Add(Marker("PreviewBubbleMarker", anchor.position));
            Physics.SyncTransforms();
        }

        static void HideZoneVisuals()
        {
            foreach (ActivityZone zone in UnityEngine.Object.FindObjectsByType<ActivityZone>(FindObjectsSortMode.None))
            {
                var visual = new SerializedObject(zone).FindProperty("visual").objectReferenceValue as GameObject;
                if (visual != null) visual.SetActive(false);
            }
        }

        static void StagePlayer(ShopRefs shop)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player == null || shop.ShopkeeperSpot == null) return;

            player.transform.SetPositionAndRotation(shop.ShopkeeperSpot.position, shop.ShopkeeperSpot.rotation);
            Physics.SyncTransforms();
        }

        static GameObject Marker(string name, Vector3 position)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(unlit != null ? unlit : Shader.Find("Sprites/Default"));
            material.SetColor("_BaseColor", Color.magenta);
            material.color = Color.magenta;
            marker.GetComponent<MeshRenderer>().sharedMaterial = material;
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * MarkerSize;
            return marker;
        }

        static void StagePlaygroundView(Camera cam, FollowCamera follow, ShopRefs shop)
        {
            foreach (ShopCustomer other in shop.Customers) if (other != null) other.gameObject.SetActive(false);

            var zone = GameObject.Find("ShopZone");
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (zone == null || player == null || follow == null)
            {
                Debug.LogWarning(LogPrefix + "no ShopZone, player or follow camera: the playground view is skipped.");
                return;
            }

            player.transform.position = zone.transform.position;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms();
            Pose pose = follow.DesiredPose();
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Debug.Log(LogPrefix + $"follow camera from the shop zone {zone.transform.position.ToString("0.0")}: at {pose.position.ToString("0.0")}.");
        }

        // ----- Rendering -------------------------------------------------------------------------

        static void RenderAndSave(Camera cam, Vector2Int size, string label)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.aspect = size.x / (float)size.y;

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var picture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(OutputFolder, label + ".png");
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Debug.Log(LogPrefix + "wrote " + Path.GetFullPath(path));

            UnityEngine.Object.DestroyImmediate(picture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        // ----- Checks ----------------------------------------------------------------------------

        static void CheckShopPoints(Camera cam, string label, ShopRefs shop, Transform anchor, Vector2Int size)
        {
            cam.aspect = size.x / (float)size.y;
            Physics.SyncTransforms();
            List<Renderer> renderers = SolidRenderers();
            var stagedCone = GameObject.Find("PreviewCone");

            foreach (FlavourTub tub in shop.Tubs)
                CheckPoint(cam, label, "tub " + tub.Flavour, tub.ScoopPoint.position, tub.transform, renderers);

            if (stagedCone != null)
            {
                var cone = stagedCone.GetComponent<IceCreamCone>();
                CheckPoint(cam, label, "cone base", shop.ConeHolder.position + Vector3.up * 0.2f, stagedCone.transform, renderers);
                CheckPoint(cam, label, "cone 3rd scoop top", cone.ScoopPosition(2) + Vector3.up * (ScoopBall.Radius * 0.9f), stagedCone.transform, renderers);
            }

            for (int i = 0; i < CoinCount; i++)
            {
                var coin = GameObject.Find("PreviewCoin" + i);
                if (coin != null) CheckPoint(cam, label, "coin " + (i + 1), coin.transform.position, coin.transform, renderers);
            }

            var keeper = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (keeper != null) CheckBounds(cam, label, "shopkeeper (the player; may be partly visible)", keeper.gameObject, true);

            Vector3 registerTarget = shop.Register.CoinTarget;
            CheckPoint(cam, label, "register coin target", registerTarget, shop.Register.transform, renderers);
            Transform registerProp = shop.Stall.Find("RegisterProp");
            if (registerProp != null) CheckBounds(cam, label, "register prop", registerProp.gameObject, true);

            CheckPoint(cam, label, "bubble anchor", anchor.position, anchor.root, renderers, true);
            CheckPoint(cam, label, "customer head (anchor - 0.35)", anchor.position - Vector3.up * 0.35f, anchor.root, renderers, true);

            // Nothing of the stall's structure may be in the picture.
            foreach (string part in new[] { "IceCreamSign", "Post" })
                foreach (Transform t in shop.Stall.GetComponentsInChildren<Transform>(true))
                    if (t.name == part) CheckBounds(cam, label, "stall part " + part + " (must be OUT)", t.gameObject, false);
        }

        static void CheckSign(Camera cam, string label, ShopRefs shop, Vector2Int size)
        {
            cam.aspect = size.x / (float)size.y;
            Transform sign = shop.Stall.Find("IceCreamSign");
            if (sign != null)
            {
                CheckBounds(cam, label, "sign (must be IN)", sign.gameObject, true);
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer r in sign.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                    first = false;
                }
                Debug.Log($"{LogPrefix}[{label}] sign world bounds: min {bounds.min.ToString("0.0")} max {bounds.max.ToString("0.0")}");
            }
            Transform counter = shop.Stall.Find("Counter");
            if (counter != null) CheckBounds(cam, label, "counter", counter.gameObject, true);
        }

        /// <summary>The renderers that can hide something: visible, opaque, and not a particle or a glow.</summary>
        static List<Renderer> SolidRenderers()
        {
            var list = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Material m = r.sharedMaterial;
                if (m != null && m.renderQueue >= 3000) continue;
                list.Add(r);
            }
            return list;
        }

        static void CheckPoint(Camera cam, string label, string what, Vector3 point, Transform ignore, List<Renderer> renderers, bool skipOcclusion = false)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            bool inside = vp.z > 0f && vp.x >= InsideMin && vp.x <= InsideMax && vp.y >= InsideMin && vp.y <= InsideMax;

            string occluders = "-";
            if (!skipOcclusion && vp.z > 0f)
            {
                Vector3 origin = cam.transform.position;
                Vector3 toPoint = point - origin;
                float distance = toPoint.magnitude;
                var ray = new Ray(origin, toPoint / distance);
                var names = new StringBuilder();

                if (Physics.Raycast(ray, out RaycastHit hit, distance - 0.03f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(ignore))
                    names.Append("collider:" + hit.collider.name + "@" + hit.distance.ToString("0.00") + " ");

                foreach (Renderer r in renderers)
                {
                    if (r.transform.IsChildOf(ignore)) continue;
                    if (r.bounds.Contains(point)) continue;
                    if (r.bounds.IntersectRay(ray, out float enter) && enter < distance - 0.03f)
                        names.Append("bounds:" + r.name + "@" + enter.ToString("0.00") + " ");
                }
                if (names.Length > 0) occluders = names.ToString().TrimEnd();
            }

            Debug.Log($"{LogPrefix}[{label}] {what}: vp=({vp.x:0.00},{vp.y:0.00}) {(inside ? "IN" : "OUT")} occluded-by={occluders}");
        }

        /// <summary>Where the screen rectangle of an object's renderers lies; reports whether it touches the picture at all and whether it is wholly inside.</summary>
        static void CheckBounds(Camera cam, string label, string what, GameObject target, bool expectedInside)
        {
            bool any = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool behind = false;

            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 vp = cam.WorldToViewportPoint(corner);
                    if (vp.z <= 0f) { behind = true; continue; }
                    any = true;
                    min = Vector2.Min(min, vp);
                    max = Vector2.Max(max, vp);
                }
            }

            if (!any)
            {
                Debug.Log($"{LogPrefix}[{label}] {what}: not projectable (behind the camera or no renderer) -> {(expectedInside ? "PROBLEM" : "OUT, fine")}");
                return;
            }

            bool touches = max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f;
            bool whollyInside = min.x >= InsideMin && max.x <= InsideMax && min.y >= InsideMin && max.y <= InsideMax && !behind;
            string verdict = expectedInside
                ? (whollyInside ? "IN" : (touches ? "PARTLY IN (cut off)" : "OUT"))
                : (touches ? "IN THE PICTURE (bad)" : "OUT (good)");
            Debug.Log($"{LogPrefix}[{label}] {what}: screen rect x {min.x:0.00}..{max.x:0.00}, y {min.y:0.00}..{max.y:0.00} -> {verdict}");
        }
    }
}
