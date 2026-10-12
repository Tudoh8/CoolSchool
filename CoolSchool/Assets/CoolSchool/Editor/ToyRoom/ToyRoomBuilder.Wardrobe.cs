using CoolSchool.Activities;
using CoolSchool.CameraRig;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.ToyRoom;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static CoolSchool.EditorTools.ModularBuilding;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>What the second step wires up.</summary>
    internal sealed class ToyRoomInputs
    {
        public PlayerController Player;
    }

    /// <summary>
    /// The Toy Room's second step: the dress-up mirror's activity, its zone and its picture menu (on the touch canvas), and the
    /// inviter's doorway spot. Plus the first step's mirror spots and the beacon above the door.
    /// </summary>
    internal static partial class ToyRoomBuilder
    {
        const string CanvasName = "TouchUI";
        const string CatalogPath = RootFolder + "/Rewards/Catalog.asset";

        // The mirror spots (see ToyRoomWardrobe): the kid stands in front of the mirror and faces the camera, which sits in front of him,
        // a little to his right (east), so he stands in the left of the picture and the menu has the right.
        const float StandFromWall = 1.0f;
        const float BuddySideShift = -0.55f;
        const float CameraSideShift = 0.9f;
        const float CameraDistance = 2.4f;
        const float CameraHeight = 1.5f;
        const float CameraLookHeight = 0.65f;
        const float ZoneRadius = 0.9f;

        // The picture menu (reference canvas 1920 x 1080): a window on the right with the tabs on its left edge.
        const float WindowWidth = 1000f;
        const float WindowHeight = 660f;
        const float TabSize = 135f;
        const float TabGap = 18f;
        const float CardAreaLeft = 190f;
        const float DoneSize = 150f;

        // A pink beacon with a gift on it, above the door (no ring: the room itself is not an activity).
        static readonly ActivityBeaconBuilder.Style ToyRoomBeacon = new ActivityBeaconBuilder.Style(
            "ToyRoom", new Color(2f, 0.7f, 1.6f, 1f), new[] { "Gift", "Star" });

        // ----- Step 1: the spots and the beacon ------------------------------------------------------

        static void BuildWardrobeSpots(Transform root, Plan plan, ToyRoomLayout layout)
        {
            Transform spots = NewGroup("WardrobeSpots", root);
            float mirrorX = MirrorCentreX(plan);
            float standZ = plan.HalfZ - StandFromWall;

            var stand = new Vector3(mirrorX, FloorY, standZ);
            var cameraPosition = new Vector3(mirrorX + CameraSideShift, CameraHeight, standZ - CameraDistance);
            var lookTarget = new Vector3(mirrorX + CameraSideShift, CameraLookHeight, standZ);

            // He faces the camera (south and a little to the east), not straight south.
            Quaternion faceCamera = FlatLookRotation(stand, cameraPosition);
            layout.StandSpot = NewAnchor("StandSpot", spots, stand, faceCamera);
            layout.BuddySpot = NewAnchor("BuddySpot", spots, stand + new Vector3(BuddySideShift, 0f, 0f), faceCamera);
            layout.ZoneSpot = NewAnchor("ZoneSpot", spots, stand + new Vector3(0f, 0f, -0.05f), Quaternion.identity);
            layout.CameraSpot = NewAnchor("CameraSpot", spots, cameraPosition, Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up));

            // Outside the zone circle, west of the mirror, facing south.
            layout.ExitSpot = NewAnchor("ExitSpot", spots, new Vector3(mirrorX - ZoneRadius, FloorY, standZ - ZoneRadius - 0.3f), Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>A light pillar and a floating gift in front of the door, so the building can be seen from afar. Built after the root has moved.</summary>
        static void BuildBeacon(Transform root, Plan plan, ToyRoomLayout layout)
        {
            Transform visual = NewGroup("Beacon", root);
            visual.position = root.position + new Vector3(0f, 0f, plan.SouthZ - 1.3f);
            ActivityBeaconBuilder.Build(visual, ToyRoomBeacon);

            // The shelf hides it while he is in the room or at the door (seen from the follow camera it would float over the room).
            if (layout.Shelf != null) SetObject(layout.Shelf, "beacon", visual.gameObject);
        }

        // ----- Step 2: everything alive -------------------------------------------------------------

        /// <summary>
        /// The wardrobe activity with its zone in front of the mirror and its picture menu on the touch canvas, wired to the catalog, the
        /// player, the buddy and the camera; the shelf gets its catalog; the inviter gets the doorway spot. Call after the rewards are
        /// built (the catalog, the buddy and the present popup exist then) and the touch UI exists.
        /// </summary>
        internal static ToyRoomWardrobe Build(ToyRoomLayout layout, ToyRoomInputs inputs)
        {
            if (layout == null || !layout.Built || inputs == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the Toy Room's wardrobe was not built (no room or no player).");
                return null;
            }

            RewardCatalog catalog = FindCatalog();
            if (catalog == null) Debug.LogWarning("CoolSchool: no reward catalog found; the Toy Room shelves and the wardrobe will be empty.");
            if (layout.Shelf != null) SetObject(layout.Shelf, "catalog", catalog);

            var wardrobeObject = new GameObject("ToyRoomWardrobe");
            wardrobeObject.transform.SetParent(layout.Root, false);
            var wardrobe = wardrobeObject.AddComponent<ToyRoomWardrobe>();

            WardrobePanel panel = BuildPanel(catalog);

            Camera mainCamera = Camera.main;
            SetObject(wardrobe, "player", inputs.Player);
            SetObject(wardrobe, "playerAnimator", inputs.Player.GetComponent<CoolSchool.Characters.KidAnimator>());
            SetObject(wardrobe, "playerWeapons", inputs.Player.GetComponent<CoolSchool.Battle.WeaponHolder>());
            SetObject(wardrobe, "buddy", Object.FindFirstObjectByType<BuddyFollower>());
            SetObject(wardrobe, "followCamera", mainCamera != null ? mainCamera.GetComponent<FollowCamera>() : null);
            SetObject(wardrobe, "standSpot", layout.StandSpot);
            SetObject(wardrobe, "buddySpot", layout.BuddySpot);
            SetObject(wardrobe, "cameraSpot", layout.CameraSpot);
            SetObject(wardrobe, "exitSpot", layout.ExitSpot);
            SetObject(wardrobe, "panel", panel);
            SetObject(wardrobe, "catalog", catalog);
            SetObject(wardrobe, "lessonUI", Object.FindFirstObjectByType<LessonUI>());
            if (Object.FindFirstObjectByType<BuddyFollower>() == null)
                Debug.LogWarning("CoolSchool: no BuddyFollower in the scene (the equipment setup builds it); the buddy will not stand beside him at the mirror.");

            CreateWardrobeZone(layout, wardrobe);
            WireInviter(layout);

            Debug.Log($"ToyRoom: wardrobe zone at {layout.ZoneSpot.position.ToString("0.0")}, stand {layout.StandSpot.position.ToString("0.0")}, "
                + $"camera {layout.CameraSpot.position.ToString("0.0")}, exit {layout.ExitSpot.position.ToString("0.0")}; "
                + $"{(layout.Shelf != null ? "shelf ready" : "NO SHELF")}, panel {(panel != null ? "built" : "NOT built")}.");
            return wardrobe;
        }

        static RewardCatalog FindCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            foreach (string guid in AssetDatabase.FindAssets("t:RewardCatalog"))
            {
                catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null) return catalog;
            }
            return null;
        }

        /// <summary>The zone in front of the mirror: a glowing flat ring, no beacon (the mirror is right there).</summary>
        static ActivityZone CreateWardrobeZone(ToyRoomLayout layout, ToyRoomWardrobe wardrobe)
        {
            var root = new GameObject("WardrobeZone");
            root.transform.SetParent(layout.Root, false);
            root.transform.position = layout.ZoneSpot.position;

            Transform visual = NewGroup("Visual", root.transform);
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GlowRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(visual, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(ZoneRadius * 2f, 0.01f, ZoneRadius * 2f);

            var renderer = ring.GetComponent<MeshRenderer>();
            Material material = ToyRoomAssets.Glow("ToyRoomZoneRing", new Color(1f, 0.55f, 0.85f), 0.4f);
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var zone = root.AddComponent<ActivityZone>();
            SetObject(zone, "activity", wardrobe);
            SetFloat(zone, "radius", ZoneRadius);
            SetObject(zone, "visual", visual.gameObject);
            return zone;
        }

        /// <summary>The next invitation after a present can lead to the doorway. Skips quietly if there is no inviter.</summary>
        static void WireInviter(ToyRoomLayout layout)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the Toy Room will not invite the kid.");
                return;
            }
            SetObject(inviter, "toyRoomSpot", layout.DoorSpot);
        }

        // ----- The picture menu ----------------------------------------------------------------------

        /// <summary>
        /// The window on the right of the touch canvas (switched off until the mirror opens it): a rounded back, four round tabs down
        /// the left edge with their pictures, the area where the cards are made at run time, and a green check to finish.
        /// Placed just below the present popup, so a present still covers it.
        /// </summary>
        static WardrobePanel BuildPanel(RewardCatalog catalog)
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool: no '{CanvasName}' canvas found, so the wardrobe has no picture menu.");
                return null;
            }

            Sprite background = ShopAssets.FindShape("Background");
            Sprite circle = ShopAssets.FindShape("Circle");
            Sprite outline = ActivityBeaconBuilder.FindSprite(new[] { "Background-Outline" }) ?? ShopAssets.FindShape("Circle-Outline");
            Sprite check = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark" });
            Sprite empty = ActivityBeaconBuilder.FindSprite(new[] { "X-Delete-Close-Error" });
            Sprite star = ActivityBeaconBuilder.FindSprite(new[] { "Star" });
            Sprite[] tabIcons =
            {
                ActivityBeaconBuilder.FindSprite(new[] { "Sword" }),
                ActivityBeaconBuilder.FindSprite(new[] { "Shield" }),
                ActivityBeaconBuilder.FindSprite(new[] { "Helmet" }),
                ActivityBeaconBuilder.FindSprite(new[] { "Chat-Heart", "Heart" }),
            };

            RectTransform root = UiRect("WardrobePanel", canvas.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // Just below the present popup, which must cover everything.
            Transform popup = canvas.transform.Find("PresentPopup");
            if (popup != null) root.SetSiblingIndex(popup.GetSiblingIndex());

            RectTransform window = UiRect("Window", root);
            window.anchorMin = window.anchorMax = new Vector2(1f, 0.5f);
            window.pivot = new Vector2(1f, 0.5f);
            window.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            window.anchoredPosition = new Vector2(-24f, -50f);
            Image back = UiImage(window, background, new Color(0.16f, 0.12f, 0.34f, 0.85f), true);
            if (background != null) back.type = Image.Type.Sliced;
            back.preserveAspect = false;

            var tabs = new Button[tabIcons.Length];
            for (int i = 0; i < tabs.Length; i++)
                tabs[i] = BuildTab(window, i, circle, tabIcons[i]);

            RectTransform cardArea = UiRect("Cards", window);
            cardArea.anchorMin = cardArea.anchorMax = new Vector2(0f, 1f);
            cardArea.pivot = new Vector2(0f, 1f);
            cardArea.anchoredPosition = new Vector2(CardAreaLeft, -30f);
            cardArea.sizeDelta = new Vector2(WindowWidth - CardAreaLeft - 12f, 340f);

            Button done = BuildDoneButton(window, circle, check);

            var panel = root.gameObject.AddComponent<WardrobePanel>();
            SetObject(panel, "catalog", catalog);
            SetObject(panel, "window", window);
            SetObjectArray(panel, "tabButtons", tabs);
            SetObject(panel, "cardArea", cardArea);
            SetObject(panel, "doneButton", done);
            SetObject(panel, "cardSprite", background);
            SetObject(panel, "ringSprite", outline);
            SetObject(panel, "circleSprite", circle);
            SetObject(panel, "checkSprite", check);
            SetObject(panel, "emptySprite", empty);
            SetObject(panel, "twinkleSprite", star);

            window.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>A round tab: a light circle (the button's own graphic, tinted by the panel) with its picture on top.</summary>
        static Button BuildTab(RectTransform window, int index, Sprite circle, Sprite icon)
        {
            RectTransform tab = UiRect("Tab_" + index, window);
            tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
            tab.pivot = new Vector2(0.5f, 0.5f);
            tab.sizeDelta = new Vector2(TabSize, TabSize);
            tab.anchoredPosition = new Vector2(CardAreaLeft * 0.5f, -(30f + TabSize * 0.5f + index * (TabSize + TabGap)));

            Image tabBack = UiImage(tab, circle, new Color(0.85f, 0.88f, 1f, 0.9f), true);
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = tabBack;
            button.transition = Selectable.Transition.None;

            RectTransform picture = UiRect("Icon", tab);
            picture.anchorMin = Vector2.zero;
            picture.anchorMax = Vector2.one;
            picture.offsetMin = new Vector2(24f, 24f);
            picture.offsetMax = new Vector2(-24f, -24f);
            UiImage(picture, icon, new Color(0.18f, 0.14f, 0.36f, 1f), false);
            return button;
        }

        static Button BuildDoneButton(RectTransform window, Sprite circle, Sprite check)
        {
            RectTransform rect = UiRect("Done", window);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(DoneSize, DoneSize);
            rect.anchoredPosition = new Vector2(-DoneSize * 0.5f - 20f, DoneSize * 0.5f + 20f);

            Image back = UiImage(rect, circle, new Color(0.3f, 0.76f, 0.35f, 1f), true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = back;

            RectTransform mark = UiRect("Check", rect);
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = Vector2.one;
            mark.offsetMin = new Vector2(28f, 28f);
            mark.offsetMax = new Vector2(-28f, -28f);
            UiImage(mark, check, Color.white, false);
            return button;
        }

        static RectTransform UiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static Image UiImage(RectTransform rect, Sprite sprite, Color color, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.preserveAspect = true;
            return image;
        }
    }
}
