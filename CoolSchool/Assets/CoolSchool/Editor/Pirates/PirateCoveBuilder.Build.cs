using System.Collections.Generic;
using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Pirates;
using CoolSchool.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>Step 2 of the pirate cove: everything alive (characters, runtime components, the map UI, the activity and its wiring).</summary>
    internal static partial class PirateCoveBuilder
    {
        const string CanvasName = "TouchUI";
        const int SkeletonCount = 9;   // two waves: up to 4 + 5
        const int CrewCount = 2;
        const string InviteGroup = "invite.pirates";

        static readonly PrefabRef SkeletonPrefab = Kids("SM_Chr_Kid_Skeleton_01");
        static readonly PrefabRef SkeletonSword = Kids("SM_Wep_Sword_Wood_03");
        static readonly PrefabRef CrewSword = Kids("SM_Wep_Sword_Wood_01");
        static readonly PrefabRef ParrotPrefab = Kids("SM_Prop_Toy_Parrot_01");
        static readonly PrefabRef CoinPrefab = new PrefabRef("SM_Gen_Prop_Coin_01", "PolygonGeneric");
        static readonly PrefabRef BandanaPrefab = Kids("SM_Chr_Attach_Pirate_Bandana_01");
        static readonly PrefabRef CaptainHatPrefab = Kids("SM_Chr_Attach_Hat_Pirate_01");
        static readonly PrefabRef EyepatchPrefab = Kids("SM_Chr_Attach_Pirate_Eyepatch_01");

        static readonly PrefabRef[] CrewPrefabs =
        {
            Kids("SM_Chr_Kid_Pirate_01"),
            Kids("SM_Chr_Kid_Pirate_02"),
        };

        /// <summary>What Build needs from the rest of the scene.</summary>
        internal struct PirateInputs
        {
            public RuntimeAnimatorController KidController;
            public int KidsLayer;
            public PlayerController Player;
            public ActionButton ActionButton;
            public ImaginationEffect Imagination;
        }

        // ----- Step 2: everything alive --------------------------------------------------------

        /// <summary>
        /// Adds the runtime components to the static cove, creates the skeletons, the pirate crew, the parrot, the costume, the
        /// treasure map, the PirateAdventure with every field wired, and the zone (added to the inviter after the shop).
        /// Call after the NavMesh is baked, after the player exists and after the lesson UI and the inviter exist.
        /// </summary>
        internal static PirateAdventure Build(CoveLayout c, PirateInputs inputs)
        {
            if (c == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the pirate cove was not built (no cove layout or no player).");
                return null;
            }

            SnapPoints(c.CrewLandingPoints);
            SnapPoints(c.SkeletonLandingPoints);
            SnapPoints(c.PartyPoints);
            if (c.IslandLandingPoint != null) c.IslandLandingPoint.position = SnapToNavMesh(c.IslandLandingPoint.position);

            Transform root = NewGroup("AdventureObjects", c.Root);
            var adventure = new GameObject("PirateAdventure").AddComponent<PirateAdventure>();
            adventure.transform.SetParent(root, false);

            // The static pieces come alive.
            PirateShip ship = BuildShipComponent(c);
            SkeletonBoat boat = BuildBoatComponent(c);
            ShipCannon cannon = BuildCannonComponent(c, inputs, boat);
            DigSpot digSpot = BuildDigSpotComponent(c, inputs);
            TreasureChest chest = BuildChestComponent(c);
            TreasureCoinPicker picker = chest != null ? chest.gameObject.AddComponent<TreasureCoinPicker>() : null;
            if (picker != null) SetObject(picker, "cam", Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>());
            TreasureTrophy trophy = BuildTrophyComponent(c);

            // The people.
            Transform people = NewGroup("Characters", root);
            Material boneMaterial = LitMaterial("PirateBone", new Color(0.95f, 0.92f, 0.82f), 0.25f);
            Material telegraphMaterial = KnightsArenaBuilder.CreateEmissiveMaterial(MaterialFolder + "/PirateTelegraph.mat", new Color(1f, 0.55f, 0.15f));
            SkeletonPirateAI[] skeletons = CreateSkeletons(c, inputs, people, boneMaterial, telegraphMaterial);
            KnightAllyAI[] crew = CreateCrew(c, inputs, people);
            ParrotGuide parrot = CreateParrot(c, inputs, people);
            PirateCostume costume = DressUpPlayer(inputs);

            // The map and its pictures.
            TreasureMapUI map = BuildMapUI();
            Sprite[] icons = PirateMapIconRenderer.RenderAll(c.LandmarkPrefabs);

            WireAdventure(adventure, c, inputs, ship, cannon, boat, digSpot, chest, picker, trophy, skeletons, crew, parrot, costume, map, icons);
            SetObject(adventure, "guideMarker", BuildGuideMarker(root));
            SetObject(adventure, "hidingBucket", BuildHidingBucket(c, root));
            SetObject(adventure, "digitFont", FindDigitFont());

            Material ring = KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/PirateZoneRing.mat", new Color(0.3f, 0.7f, 1f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("PirateZone", c.Root, c.ZonePosition, ZoneRadius, adventure, ring, PirateBeacon);
            AddToInviter(zone);

            Debug.Log($"CoolSchool pirate cove: {skeletons.Length} skeletons, {crew.Length} crew, parrot {(parrot != null ? "yes" : "no")}, "
                + $"{icons.Count(s => s != null)} of {icons.Length} map icons, zone {c.ZonePosition.ToString("0.0")}, "
                + $"landing {(c.IslandLandingPoint != null ? c.IslandLandingPoint.position.ToString("0.0") : "missing")}.");
            return adventure;
        }

        // ---- The hiding bucket (the "how many are under the bucket?" game) ----

        const float HidingBucketWidth = 0.45f;
        // The town pack's bucket first, else the kids pack's (LoadPrefab prefers the pack named in the reference).
        static readonly PrefabRef HidingBucketPrefab = new PrefabRef("SM_Prop_Bucket_01", "PolygonTown");

        /// <summary>
        /// An upside-down bucket about 0.45 m wide, without colliders, switched off. The returned wrapper's origin is the point where
        /// the bucket touches the sand, so the game only has to place the wrapper on the ground. Null if the prefab is missing.
        /// </summary>
        static Transform BuildHidingBucket(CoveLayout c, Transform parent)
        {
            GameObject prefab = LoadPrefab(HidingBucketPrefab);
            if (prefab == null) return null;

            Transform wrapper = NewGroup("HidingBucket", parent);
            wrapper.position = c.Chest != null ? c.Chest.position : c.SouthWest;

            GameObject model = Spawn(prefab, wrapper);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            // Upside down, sized by its widest side, resting its rim on the wrapper's origin.
            model.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            if (TryGetBounds(model, out Bounds bounds))
            {
                float widest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (widest > 0.01f) model.transform.localScale *= HidingBucketWidth / widest;
            }
            AlignToBounds(model, wrapper.position.x, wrapper.position.z, wrapper.position.y, false);

            wrapper.gameObject.SetActive(false);
            return wrapper;
        }

        /// <summary>The Inter-Bold font for the gold digit above the chest (null if it is not in the project: the digit then uses the default font).</summary>
        static Font FindDigitFont()
        {
            foreach (string guid in AssetDatabase.FindAssets("Inter-Bold t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "Inter-Bold") continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }
            return null;
        }

        static void SnapPoints(Transform[] points)
        {
            if (points == null) return;
            foreach (Transform point in points)
                if (point != null) point.position = SnapToNavMesh(point.position);
        }

        // ---- Components on the static cove ----

        static PirateShip BuildShipComponent(CoveLayout c)
        {
            if (c.Ship == null) return null;

            var ship = c.Ship.gameObject.AddComponent<PirateShip>();
            SetObject(ship, "helmSeat", c.HelmSeat);
            SetObjectArray(ship, "crewSeats", c.CrewSeats);
            SetObject(ship, "wheel", c.Wheel);
            SetObject(ship, "islandDockPoint", c.IslandDockPoint);
            SetBounds(ship, "seaArea", c.SeaArea);
            SetObject(ship, "joystick", Object.FindFirstObjectByType<FloatingJoystick>());
            return ship;
        }

        static SkeletonBoat BuildBoatComponent(CoveLayout c)
        {
            if (c.Boat == null) return null;

            // BalloonTarget goes on each slot (stick and sphere), so the whole balloon with its stick hides when it is not used.
            var targets = new List<BalloonTarget>();
            Color[] colours =
            {
                new Color(1f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.85f, 0.3f),
                new Color(0.2f, 0.55f, 1f), new Color(0.65f, 0.3f, 0.95f), new Color(1f, 0.45f, 0.75f),
            };
            for (int i = 0; i < c.Balloons.Length; i++)
            {
                var target = c.Balloons[i].AddComponent<BalloonTarget>();
                SetObject(target, "balloon", c.BalloonRenderers[i]);
                SetColor(target, "color", colours[i % colours.Length]);
                targets.Add(target);
            }

            var boat = c.Boat.gameObject.AddComponent<SkeletonBoat>();
            SetObject(boat, "startPoint", c.BoatStart);
            SetObject(boat, "driftLeft", c.BoatDriftLeft);
            SetObject(boat, "driftRight", c.BoatDriftRight);
            SetObject(boat, "islandBeachPoint", c.BoatIslandBeach);
            SetObjectArray(boat, "balloons", targets);
            SetObjectArray(boat, "skeletonSeats", c.SkeletonSeats);
            return boat;
        }

        static ShipCannon BuildCannonComponent(CoveLayout c, PirateInputs inputs, SkeletonBoat boat)
        {
            if (c.CannonBarrel == null || c.CannonBarrel.parent == null) return null;

            var cannon = c.CannonBarrel.parent.gameObject.AddComponent<ShipCannon>();
            SetObject(cannon, "button", inputs.ActionButton);
            SetObject(cannon, "muzzle", c.CannonMuzzle);
            SetObject(cannon, "barrel", c.CannonBarrel);
            SetObject(cannon, "targets", boat);
            return cannon;
        }

        static DigSpot BuildDigSpotComponent(CoveLayout c, PirateInputs inputs)
        {
            if (c.DigSpot == null) return null;

            var spot = c.DigSpot.gameObject.AddComponent<DigSpot>();
            SetObject(spot, "button", inputs.ActionButton);
            SetObject(spot, "player", inputs.Player.transform);
            SetObject(spot, "mark", c.RedX);
            return spot;
        }

        static TreasureChest BuildChestComponent(CoveLayout c)
        {
            if (c.Chest == null) return null;

            var chest = c.Chest.gameObject.AddComponent<TreasureChest>();
            SetObject(chest, "lid", c.ChestLid);
            SetObject(chest, "lockVisual", c.LockVisual);
            SetObjectArray(chest, "coinSpots", c.CoinSpots);
            SetObject(chest, "coinPrefab", LoadPrefab(CoinPrefab));
            return chest;
        }

        static TreasureTrophy BuildTrophyComponent(CoveLayout c)
        {
            if (c.Trophy == null) return null;

            var trophy = c.Trophy.gameObject.AddComponent<TreasureTrophy>();
            SetObjectArray(trophy, "tiers", c.TrophyTiers.Where(t => t != null).ToArray());
            SetObject(trophy, "lid", c.TrophyLid);
            return trophy;
        }

        // ---- Characters ----

        /// <summary>Nine skeleton kids (two waves) with their bone piles and swords, switched off until the duel on the beach.</summary>
        static SkeletonPirateAI[] CreateSkeletons(CoveLayout c, PirateInputs inputs, Transform parent, Material boneMaterial, Material telegraphMaterial)
        {
            Transform group = NewGroup("Skeletons", parent);
            var result = new List<SkeletonPirateAI>();

            for (int i = 0; i < SkeletonCount; i++)
            {
                Vector3 spot = c.SkeletonLandingPoints != null && c.SkeletonLandingPoints.Length > 0
                    ? c.SkeletonLandingPoints[i % c.SkeletonLandingPoints.Length].position
                    : c.IslandCentre;
                GameObject root = CreateKid($"Skeleton_{i + 1}", SkeletonPrefab, inputs.KidController, SnapToNavMesh(spot), out float height);
                root.transform.SetParent(group, true);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 1.5f;

                // A trigger, so the player walks straight through: a crowd of skeletons must never shove him off a spot he needs.
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.radius = 0.25f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                var hittable = root.AddComponent<Hittable>();
                SetEnum(hittable, "team", (int)BattleTeam.Opponents);
                SetInt(hittable, "hitsToDefeat", 1);
                SetBool(hittable, "canBeDefeated", true);
                SetFloat(hittable, "stunSeconds", 0.4f);

                AddWeaponHolder(root, SkeletonSword, default);

                var bones = root.AddComponent<BonePile>();
                SetObject(bones, "boneMaterial", boneMaterial);

                var ai = root.AddComponent<SkeletonPirateAI>();
                SetObject(ai, "telegraph", CreateTelegraph(root.transform, telegraphMaterial));

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
                result.Add(ai);
            }
            return result.ToArray();
        }

        /// <summary>A flat glowing ring on the ground at the skeleton's feet, shown during the wind-up.</summary>
        static GameObject CreateTelegraph(Transform kid, Material material)
        {
            var ring = new GameObject("Telegraph");
            ring.transform.SetParent(kid, false);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            Primitive(PrimitiveType.Cylinder, "Disc", ring.transform, Vector3.zero, new Vector3(1.5f, 0.01f, 1.5f), Quaternion.identity, material);
            ring.SetActive(false);
            return ring;
        }

        /// <summary>Two pirate classmates built like the knight allies: they cannot be defeated, and their AI stays off until needed.</summary>
        static KnightAllyAI[] CreateCrew(CoveLayout c, PirateInputs inputs, Transform parent)
        {
            Transform group = NewGroup("Crew", parent);
            var result = new List<KnightAllyAI>();

            for (int i = 0; i < CrewCount; i++)
            {
                Vector3 spot = c.CrewSpot + new Vector3(-i * 1.1f, 0f, 0f);
                string kidName = CrewPrefabs[i % CrewPrefabs.Length].Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid($"Crew_{kidName}", CrewPrefabs[i % CrewPrefabs.Length], inputs.KidController, SnapToNavMesh(spot), out float height);
                root.transform.SetParent(group, true);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 1.6f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                var hittable = root.AddComponent<Hittable>();
                SetEnum(hittable, "team", (int)BattleTeam.Heroes);
                SetBool(hittable, "canBeDefeated", false);
                root.AddComponent<SwordFighter>();
                KnightAllyAI ally = root.AddComponent<KnightAllyAI>();
                ally.enabled = false;
                AddWeaponHolder(root, CrewSword, default);

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
                result.Add(ally);
            }
            return result.ToArray();
        }

        /// <summary>Lorre: the toy parrot scaled to about 45 cm on a "Parrot" root (the model child bobs, the root flies).</summary>
        static ParrotGuide CreateParrot(CoveLayout c, PirateInputs inputs, Transform parent)
        {
            var root = new GameObject("Parrot");
            root.transform.SetParent(parent, false);
            root.transform.position = c.ParrotSpot;

            GameObject prefab = LoadPrefab(ParrotPrefab);
            Transform model = null;
            if (prefab != null)
            {
                GameObject instance = Spawn(prefab, root.transform);
                instance.name = "ParrotModel";
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                if (TryGetBounds(instance, out Bounds bounds))
                {
                    float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (biggest > 0.001f) instance.transform.localScale *= 0.45f / biggest;
                    TryGetBounds(instance, out bounds);
                    instance.transform.position += root.transform.position - bounds.center;
                }
                model = instance.transform;
            }
            else
            {
                Debug.LogWarning("CoolSchool pirate cove: the toy parrot prefab is missing, so Lorre has no body.");
            }

            var guide = root.AddComponent<ParrotGuide>();
            SetObject(guide, "model", model);

            var shoulder = root.AddComponent<ShoulderParrot>();
            SetObject(shoulder, "parrot", guide);
            SetObject(shoulder, "playerAnimator", inputs.Player.GetComponentInChildren<Animator>());
            return guide;
        }

        /// <summary>The pirate costume on the player: eyepatch, bandana and captain's hat (the component decides which hat).</summary>
        static PirateCostume DressUpPlayer(PirateInputs inputs)
        {
            var costume = inputs.Player.gameObject.AddComponent<PirateCostume>();
            SetObject(costume, "bandanaPrefab", LoadPrefab(BandanaPrefab));
            SetObject(costume, "captainHatPrefab", LoadPrefab(CaptainHatPrefab));
            SetObject(costume, "eyepatchPrefab", LoadPrefab(EyepatchPrefab));
            return costume;
        }

        // ---- The treasure map ----

        /// <summary>
        /// The parchment at the top centre of the touch canvas (about 40% of the screen width): four picture slots, four ticks, the red X
        /// and twelve dots as a starting set. The component lays them out each time the map is shown, for any number of landmarks
        /// (it copies the first slot or dot when the route needs more, and narrows the parchment on a squarer screen).
        /// Pictures only, no words.
        /// </summary>
        static TreasureMapUI BuildMapUI()
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool: no '{CanvasName}' canvas found, so the treasure map was not built.");
                return null;
            }

            // A full-screen holder that never blocks touches; the parchment is its child and starts switched off.
            RectTransform holder = NewUiRect("TreasureMap", canvas.transform);
            holder.anchorMin = Vector2.zero;
            holder.anchorMax = Vector2.one;
            holder.offsetMin = Vector2.zero;
            holder.offsetMax = Vector2.zero;

            Sprite background = ShopAssets.FindShape("Background");
            Sprite outline = ShopAssets.FindShape("Background-Outline");
            Sprite circle = ShopAssets.FindShape("Circle");
            Sprite tick = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark", "Check", "Tick" });
            Sprite cross = ActivityBeaconBuilder.FindSprite(new[] { "X-Delete-Close-Error", "X-Delete-Close-Error-Thin" });

            RectTransform panel = NewUiRect("Parchment", holder);
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -24f);
            panel.sizeDelta = new Vector2(770f, 220f);
            // The parchment and its edge fill the whole wide panel: with the aspect kept they would shrink to a small square in
            // the middle, and the pictures (laid out over the full width) would hang off it.
            StretchToFill(UiImage(panel, background, new Color(0.97f, 0.87f, 0.62f, 0.96f)));
            RectTransform edge = NewUiRect("Edge", panel);
            Stretch(edge);
            StretchToFill(UiImage(edge, outline, new Color(0.5f, 0.32f, 0.13f, 1f)));

            var dots = new List<RectTransform>();
            for (int i = 0; i < 12; i++)
            {
                RectTransform dot = NewUiRect($"Dot_{i + 1}", panel);
                CentreAnchored(dot, new Vector2(16f, 16f));
                UiImage(dot, circle, new Color(0.5f, 0.32f, 0.13f, 1f));
                dots.Add(dot);
            }

            var icons = new List<Image>();
            var checks = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                RectTransform icon = NewUiRect($"Step_{i + 1}", panel);
                CentreAnchored(icon, new Vector2(130f, 130f));
                icons.Add(UiImage(icon, null, Color.white));

                RectTransform check = NewUiRect($"Tick_{i + 1}", panel);
                CentreAnchored(check, new Vector2(70f, 70f));
                checks.Add(UiImage(check, tick, new Color(0.18f, 0.75f, 0.25f, 1f)));
            }

            RectTransform x = NewUiRect("RedX", panel);
            CentreAnchored(x, new Vector2(120f, 120f));
            Image xImage = UiImage(x, cross, new Color(0.88f, 0.1f, 0.1f, 1f));

            var map = holder.gameObject.AddComponent<TreasureMapUI>();
            SetObject(map, "panel", panel);
            SetObjectArray(map, "stepIcons", icons);
            SetObjectArray(map, "stepChecks", checks);
            SetObject(map, "finalX", xImage);
            SetObjectArray(map, "dots", dots);

            foreach (Image icon in icons) icon.gameObject.SetActive(false);
            foreach (Image check in checks) check.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            return map;
        }

        /// <summary>
        /// The bouncing arrow and glowing ring the adventure puts over the landmark to go to next. Built from primitives; the
        /// arrow's cone mesh is made by the component. Inactive at start, on the Ignore Raycast layer so the camera ignores it.
        /// </summary>
        static TreasureGuideMarker BuildGuideMarker(Transform parent)
        {
            var root = new GameObject("TreasureGuideMarker").transform;
            root.SetParent(parent, false);

            Material arrowMaterial = KnightsArenaBuilder.CreateEmissiveMaterial(MaterialFolder + "/PirateGuideArrow.mat", new Color(1f, 0.8f, 0.1f));
            Material ringMaterial = KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/PirateGuideRing.mat", new Color(1f, 0.85f, 0.2f), 0.55f);

            // The arrow points down with its tip at its own origin: the cone (mesh made at runtime) and a shaft on top of it.
            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root, false);
            var head = new GameObject("Head");
            head.transform.SetParent(arrow, false);
            var headFilter = head.AddComponent<MeshFilter>();
            var headRenderer = head.AddComponent<MeshRenderer>();
            headRenderer.sharedMaterial = arrowMaterial;
            headRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // A cylinder is 2 m tall and 1 m wide at scale 1: a 0.7 m shaft from the cone's top up to 1.7 m in all.
            float shaftLength = 0.7f;
            Primitive(PrimitiveType.Cylinder, "Shaft", arrow, new Vector3(0f, TreasureGuideMarker.ConeHeight + shaftLength * 0.5f, 0f),
                new Vector3(0.5f, shaftLength * 0.5f, 0.5f), Quaternion.identity, arrowMaterial);

            // The ring on the ground: a flat disc one metre across (the component scales it to the landmark).
            Transform ring = Primitive(PrimitiveType.Cylinder, "Ring", root, Vector3.zero, new Vector3(1f, 0.01f, 1f), Quaternion.identity, ringMaterial);

            var marker = root.gameObject.AddComponent<TreasureGuideMarker>();
            SetObject(marker, "arrow", arrow);
            SetObject(marker, "arrowHead", headFilter);
            SetObject(marker, "ring", ring);

            SetLayerRecursive(root.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
            root.gameObject.SetActive(false);
            return marker;
        }

        static RectTransform NewUiRect(string rectName, Transform parent)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void CentreAnchored(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static Image UiImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>
        /// Makes a panel picture fill its rect: no kept aspect, and sliced (rounded corners stay round) when the sprite has
        /// borders, otherwise simply stretched.
        /// </summary>
        static void StretchToFill(Image image)
        {
            image.preserveAspect = false;
            if (image.sprite != null && image.sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        }

        // ---- The activity ----

        static void WireAdventure(PirateAdventure adventure, CoveLayout c, PirateInputs inputs, PirateShip ship, ShipCannon cannon, SkeletonBoat boat,
            DigSpot digSpot, TreasureChest chest, TreasureCoinPicker picker, TreasureTrophy trophy, SkeletonPirateAI[] skeletons, KnightAllyAI[] crew, ParrotGuide parrot,
            PirateCostume costume, TreasureMapUI map, Sprite[] icons)
        {
            // Player
            SetObject(adventure, "player", inputs.Player);
            SetObject(adventure, "playerInput", inputs.Player.GetComponent<PlayerBattleInput>());
            SetObject(adventure, "actionButton", inputs.ActionButton);
            SetObject(adventure, "playerWeapons", inputs.Player.GetComponent<WeaponHolder>());
            SetObject(adventure, "costume", costume);
            SetObject(adventure, "swordIcon", ActivityBeaconBuilder.FindSprite(new[] { "Sword" }));
            SetObject(adventure, "cannonIcon", ActivityBeaconBuilder.FindSprite(new[] { "Cannon", "Bomp", "Bomb" }));
            SetObject(adventure, "spadeIcon", ActivityBeaconBuilder.FindSprite(new[] { "Shovel", "Spade" }));
            SetObject(adventure, "imagination", CreateImagination(inputs.Imagination));

            // Cove
            SetObject(adventure, "ship", ship);
            SetObject(adventure, "cannon", cannon);
            SetObject(adventure, "skeletonBoat", boat);
            SetObject(adventure, "islandLandingPoint", c.IslandLandingPoint);
            SetObjectArray(adventure, "crewLandingPoints", c.CrewLandingPoints);
            SetObjectArray(adventure, "skeletonLandingPoints", c.SkeletonLandingPoints);
            SetObjectArray(adventure, "landmarks", c.Landmarks);
            SetObjectArray(adventure, "landmarkIcons", icons);
            SetObject(adventure, "digSpot", digSpot);
            SetObject(adventure, "chest", chest);
            SetObject(adventure, "coinPicker", picker);
            SetObjectArray(adventure, "partyPoints", c.PartyPoints);
            SetObject(adventure, "trophy", trophy);

            // Characters
            SetObjectArray(adventure, "skeletons", skeletons);
            SetObjectArray(adventure, "crew", crew);
            SetObject(adventure, "parrot", parrot);

            // UI
            SetObject(adventure, "map", map);
            SetObject(adventure, "lessonUI", Object.FindFirstObjectByType<LessonUI>());
            SetObject(adventure, "letters", AssetDatabase.LoadAssetAtPath<LetterSet>(LetterSetSeeder.AssetPath));
        }

        /// <summary>The adventure's own imagination glow: the shared gold overlay, without the castle's flags and torches.</summary>
        static ImaginationEffect CreateImagination(ImaginationEffect castleEffect)
        {
            var effectObject = new GameObject("PirateImagination");
            var effect = effectObject.AddComponent<ImaginationEffect>();

            if (castleEffect != null)
            {
                var source = new SerializedObject(castleEffect);
                SerializedProperty overlay = source.FindProperty("overlay");
                if (overlay != null) SetObject(effect, "overlay", overlay.objectReferenceValue);
            }
            SetFloat(effect, "flashSeconds", 1f);
            SetFloat(effect, "maxOverlayAlpha", 0.35f);
            return effect;
        }

        /// <summary>Adds the pirate zone at the END of the inviter's turns (after the shop), with its own invite line. Skips quietly if there is no inviter.</summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the pirate cove will not invite the kid.");
                return;
            }

            var serialized = new SerializedObject(inviter);
            SerializedProperty zones = serialized.FindProperty("zones");
            SerializedProperty groups = serialized.FindProperty("inviteGroups");
            if (zones == null || groups == null) return;

            // The groups line up with the zones; pad them first in case an earlier zone was missing.
            while (groups.arraySize < zones.arraySize)
            {
                groups.InsertArrayElementAtIndex(groups.arraySize);
                groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = string.Empty;
            }

            zones.InsertArrayElementAtIndex(zones.arraySize);
            zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = zone;
            groups.InsertArrayElementAtIndex(groups.arraySize);
            groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = InviteGroup;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Small serialized helpers the shared builder does not have ----

        static void SetColor(Object target, string field, Color value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
