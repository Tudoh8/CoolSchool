using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.CameraRig;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Player;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>Step 2 of the street: everything alive (track items, carts, kids, announcer, HUD, the race with all its wiring, the zone).</summary>
    internal static partial class SoapboxStreetBuilder
    {
        const string InviteGroup = "invite.race";
        internal const string CartPickCameraSpotName = "CartPickCameraSpot";

        /// <summary>What Build needs from the rest of the scene.</summary>
        internal struct RaceInputs
        {
            public RuntimeAnimatorController KidController;
            public RuntimeAnimatorController TeacherController;
            public int KidsLayer;
            public PlayerController Player;
            public FloatingJoystick Joystick;
            public ActionButton ActionButton;
        }

        /// <summary>
        /// Adds the track with its items, the three carts with their kids, the announcer and the spectators, the HUD, the camera
        /// component and the SoapboxRace with every field wired, and the zone (added to the inviter last).
        /// Call after the NavMesh is baked, after the player and the touch UI exist, after the pirate cove, before the parent gear and the rewards.
        /// </summary>
        internal static void Build(StreetLayout layout, RaceInputs inputs)
        {
            if (layout == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the soapbox street was not built (no street layout or no player).");
                return;
            }

            Transform root = NewGroup("RaceObjects", layout.Root);
            Font font = FindFont();
            layout.ReturnPoint.position = SnapToNavMesh(layout.ReturnPoint.position);

            // ---- Track and its items ----
            var trackObject = new GameObject("RaceTrack");
            trackObject.transform.SetParent(root, false);
            trackObject.transform.position = layout.StartLine.position;
            Transform itemsRoot = NewGroup("TrackItems", trackObject.transform);
            ItemCounts counts = BuildTrackItems(itemsRoot, font);
            Transform animalPoints = NewGroup("AnimalSidewalkPoints", root);
            Transform animalFollowers = NewGroup("AnimalFollowers", root);
            counts.Animals = BuildAnimals(itemsRoot, animalPoints, animalFollowers);

            var track = trackObject.AddComponent<RaceTrack>();
            SetObject(track, "startLine", layout.StartLine);
            SetObject(track, "itemsRoot", itemsRoot);
            SetFloat(track, "laneWidth", LaneWidth);
            SetFloat(track, "finishDistance", FinishDistance);

            // ---- Carts and rivals ----
            Material gold = Mat("CartGold", new Color(1f, 0.78f, 0.2f), 0.7f, 0.8f);
            Transform carts = NewGroup("Carts", root);
            CartPaintMaker.ClearCache();
            ClearBodyCaches();
            // Only the player's cart gets the trails; the rivals never use one.
            CartParts playerCart = BuildCart("PlayerCart", 1, carts, gold, true, font);
            CartParts rivalA = BuildCart("RivalCart_A", 0, carts, gold, false, font);
            CartParts rivalB = BuildCart("RivalCart_B", 2, carts, gold, false, font);
            var rivals = new RivalCartAI[2];
            CartParts[] rivalCarts = { rivalA, rivalB };
            for (int i = 0; i < rivals.Length; i++)
            {
                KidAnimator driver = CreateRivalKid(rivalCarts[i], i, inputs, root);
                rivals[i] = rivalCarts[i].Root.AddComponent<RivalCartAI>();
                SetObject(rivals[i], "cart", rivalCarts[i].Controller);
                SetObject(rivals[i], "driver", driver);
            }
            CreateCartPrefabs(gold);
            CreateGaragePrefabs();
            CreateBodyPrefabs();

            // ---- The player's helmet ----
            var playerHelmet = inputs.Player.gameObject.AddComponent<RaceHelmet>();
            SetObject(playerHelmet, "helmetPrefab", LoadPrefab(PlayerHelmetPrefab));

            // ---- People ----
            RaceAnnouncer announcer = BuildAnnouncer(layout, inputs, root);
            BuildSpectators(layout, inputs, root);

            var finishArch = layout.FinishArchRoot.gameObject.AddComponent<FinishArch>();
            SetObject(finishArch, "banner", layout.FinishBanner);

            // ---- Camera ----
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            RaceChaseCamera chase = null;
            FollowCamera follow = null;
            if (camera != null)
            {
                follow = camera.GetComponent<FollowCamera>();
                chase = camera.gameObject.AddComponent<RaceChaseCamera>();
                chase.enabled = false;
                // The long street must be visible far ahead.
                if (camera.farClipPlane < 250f) camera.farClipPlane = 250f;
            }
            else
            {
                Debug.LogWarning("CoolSchool race street: no camera found, so there is no chase camera.");
            }

            // ---- HUD ----
            RaceHud hud = BuildHud(font);

            // ---- Cart picker: the cards, and the camera spot in front of the player's cart looking back at it ----
            CartPicker cartPicker = BuildCartPicker(font);
            Transform cartPickCameraSpot = BuildCartPickCameraSpot(root, playerCart.Root.transform.position);
            BuildPitLane(root);

            // ---- The race ----
            var raceObject = new GameObject("SoapboxRace");
            raceObject.transform.SetParent(root, false);
            var input = raceObject.AddComponent<RaceInput>();
            SetObject(input, "actionButton", inputs.ActionButton);
            var race = raceObject.AddComponent<SoapboxRace>();
            WireRace(race, layout, inputs, track, playerCart, playerHelmet, rivals, input, hud, announcer, chase, follow, finishArch);

            // The cart picker. The riding buddy ("buddy") is left empty on purpose: the rewards setup builds the BuddyFollower AFTER
            // the race (RewardsSetupBuilder.Setup runs last), so it does not exist yet. SoapboxRace finds it in the scene when
            // the race begins, which also works when the buddy is built later or by hand.
            SetObject(race, "cartPicker", cartPicker);
            SetObject(race, "cartPickCameraSpot", cartPickCameraSpot);

            // ---- The zone ----
            Material ring = KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/RaceZoneRing.mat", new Color(1f, 0.3f, 0.25f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("RaceZone", layout.Root, layout.ZonePosition, 2f, race, ring, RaceBeacon);
            AddToInviter(zone);

            Summarize(layout, counts);
        }

        /// <summary>
        /// Front-left of the player's cart (the cart faces north), high enough to see over the rival in lane 0, looking back at the cart.
        /// The aim point is a little below the cart, so the cart sits in the upper middle of the picture, above the cards.
        /// </summary>
        static Transform BuildCartPickCameraSpot(Transform parent, Vector3 cartPosition)
        {
            Vector3 position = cartPosition + new Vector3(-2.6f, 2.0f, 3.4f);
            Vector3 aim = cartPosition + new Vector3(0f, -0.2f, 0f);
            Transform spot = NewPoint(CartPickCameraSpotName, parent, position);
            spot.rotation = Quaternion.LookRotation(aim - position, Vector3.up);
            return spot;
        }

        static void WireRace(SoapboxRace race, StreetLayout layout, RaceInputs inputs, RaceTrack track, CartParts playerCart, RaceHelmet playerHelmet,
            RivalCartAI[] rivals, RaceInput input, RaceHud hud, RaceAnnouncer announcer, RaceChaseCamera chase, FollowCamera follow, FinishArch finishArch)
        {
            // Player
            SetObject(race, "player", inputs.Player);
            SetObject(race, "playerAnimator", inputs.Player.GetComponent<KidAnimator>());
            SetObject(race, "playerWeapons", inputs.Player.GetComponent<WeaponHolder>());
            SetObject(race, "helmet", playerHelmet);
            SetObject(race, "joystick", inputs.Joystick);
            SetObject(race, "actionButton", inputs.ActionButton);
            SetObject(race, "followCamera", follow);
            SetObject(race, "chaseCamera", chase);

            // Race
            SetObject(race, "track", track);
            SetObject(race, "playerCart", playerCart.Controller);
            SetObject(race, "playerLooks", playerCart.Looks);
            SetObjectArray(race, "rivals", rivals);
            SetObject(race, "input", input);
            SetObject(race, "hud", hud);
            SetObject(race, "announcer", announcer);
            SetObject(race, "announcerStartSpot", layout.AnnouncerStart);
            SetObject(race, "announcerFinishSpot", layout.AnnouncerFinish);
            SetObjectArray(race, "podiumSpots", layout.PodiumSpots);
            SetObject(race, "podiumCameraSpot", layout.PodiumCameraSpot);
            SetObject(race, "returnPoint", layout.ReturnPoint);
            SetFloat(race, "gateLeadSeconds", 5f);
            SetObject(race, "finishArch", finishArch);
        }

        /// <summary>Adds the race zone at the END of the inviter's turns, with its own invite line. Skips quietly if there is no inviter.</summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the race will not invite the kid.");
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

        static void Summarize(StreetLayout layout, ItemCounts counts)
        {
            string perLevel = string.Join("; ", Enumerable.Range(1, 3).Select(level =>
            {
                SoapboxTrackLayout.Count(level, out int rows, out int obstacles, out int coins, out int ramps, out int gates, out int animals);
                return $"level {level}: {rows} rows / {obstacles} obstacles, {coins} coins, {ramps} ramps, {gates} gates, {animals} animals";
            }));
            int renderers = layout.Root.GetComponentsInChildren<Renderer>(true).Length;
            Debug.Log($"CoolSchool race: street x {FenceWestX:0.0}..{FenceEastX:0.0}, z {layout.Area.yMin:0.0}..{StreetEndZ:0.0}; start line z {StartLineZ:0.0}, "
                + $"finish z {ZAt(FinishDistance):0.0}, podium z {ZAt(PodiumDistance):0.0}; zone {layout.ZonePosition.ToString("0.0")}; "
                + $"{counts.Obstacles} obstacles, {counts.Coins} coins, {counts.Ramps} ramps, {counts.Gates} gates, {counts.Animals} animals in all; {perLevel}; "
                + $"{layout.HouseCount} houses; {renderers} renderers in the whole street.");
            if (renderers > 6000)
                Debug.LogWarning($"CoolSchool race: the street has {renderers} renderers (wanted under about 6000).");
        }
    }
}
