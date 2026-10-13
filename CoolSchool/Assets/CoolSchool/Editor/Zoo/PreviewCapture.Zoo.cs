using System;
using System.Collections.Generic;
using System.Linq;
using CoolSchool.Zoo;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The zoo park shots of PreviewCapture (group names start with "zoo"): the follow-camera view from the zone, the entrance gap, a
    /// top view, the pens row with all animals in and a gate with its sign, the zookeeper, and one shot for every hiding spot with a
    /// fitting animal put in its hide pose (the camera 4 m back at the follow camera's angle), so you can see what shows.
    /// </summary>
    public static partial class PreviewCapture
    {
        // Each spot with the animals that hide in it for the shots: the first of a spot is the usual one, the others the extreme ones.
        static readonly (string spot, string kind)[] ZooSpotShots =
        {
            ("hedge", "giraffe"), ("hedge", "elephant"), ("hedge", "crocodile"),
            ("tree", "elephant"), ("tree", "zebra"),
            ("treetop", "monkey"),
            ("pond", "hippo"), ("pond", "crocodile"), ("pond", "flamingo"),
            ("bin", "monkey"), ("bin", "penguin"),
            ("tent", "lion"), ("tent", "zebra"),
            ("box", "penguin"), ("box", "monkey"),
            ("bush", "tiger"), ("bush", "giraffe"),
            ("house", "giraffe"), ("house", "tiger"),
            ("leaves", "hippo"), ("leaves", "crocodile"),
            ("table", "penguin"), ("table", "lion"),
            ("rock", "flamingo"), ("rock", "elephant"),
        };

        /// <summary>Where an animal root and its model stood before a shot moved them.</summary>
        sealed class ZooRest
        {
            public Transform Root;
            public Vector3 Position;
            public Quaternion Rotation;
            public Transform Model;
            public Vector3 ModelPosition;
            public bool ModelActive;
        }

        static void AddZooGroups(List<Group> groups)
        {
            // From the zone with the follow camera's pose (the player standing in it), the entrance gap, a top view, the pens.
            var park = new Group { Name = "zoo_park", Stage = StageZooPlayerAtZone };
            park.Shots.Add(new Shot { Name = "from_zone", Frame = () => ZooLookFromZone(50f) });
            park.Shots.Add(new Shot { Name = "from_zone_wide", Frame = () => ZooLookFromZone(70f) });
            park.Shots.Add(new Shot { Name = "entrance", Frame = ZooEntranceView });
            park.Shots.Add(new Shot { Name = "top", Frame = ZooTopView });
            groups.Add(park);

            var pens = new Group { Name = "zoo_pens", Stage = HideZoneVisuals };
            pens.Shots.Add(new Shot { Name = "east_half", Frame = () => ZooPensView(0, 4) });
            pens.Shots.Add(new Shot { Name = "west_half", Frame = () => ZooPensView(5, 9) });
            pens.Shots.Add(new Shot { Name = "all", Frame = ZooPensAllView });
            pens.Shots.Add(new Shot { Name = "gate_sign", Frame = ZooGateSignView });
            groups.Add(pens);

            // The zookeeper next to the zone.
            var keeper = new Group { Name = "zoo_keeper", Stage = StageZooKeeper };
            keeper.Shots.Add(new Shot { Name = "front", Frame = () => ZooKeeperView(0f) });
            keeper.Shots.Add(new Shot { Name = "three_quarter", Frame = () => ZooKeeperView(40f) });
            keeper.Shots.Add(new Shot { Name = "head", Frame = () => ZooKeeperHeadView() });
            groups.Add(keeper);

            // One shot per spot and animal: the animal in its hide pose, seen the way the follow camera sees it.
            var rests = new List<ZooRest>();
            var spots = new Group { Name = "zoo_spot", Stage = () => { HideZoneVisuals(); ZooCaptureRests(rests); } };
            foreach ((string spot, string kind) in ZooSpotShots)
            {
                string spotId = spot;
                string kindId = kind;
                spots.Shots.Add(new Shot { Name = spotId + "_" + kindId, Frame = () => ZooSpotView(rests, spotId, kindId) });
            }
            groups.Add(spots);

            // One shot per spot he can hide in: the child standing on his own hide point, seen the way the follow camera sees him.
            var hideGroup = new Group { Name = "zoo_hide", Stage = HideZoneVisuals };
            foreach (string spotId in ZooPlayerSpotShots)
            {
                string id = spotId;
                hideGroup.Shots.Add(new Shot { Name = id, Frame = () => ZooPlayerHideView(id) });
            }
            groups.Add(hideGroup);
        }

        // ----- Finding things -----

        static GameObject ZooFind(string objectName)
        {
            return AllSceneObjects().FirstOrDefault(go => go.name == objectName);
        }

        static Vector3 ZooZonePosition()
        {
            GameObject zone = ZooFind("ZooZone");
            if (zone != null) return zone.transform.position;
            GameObject point = ZooFind("ZonePoint");
            return point != null ? point.transform.position : Vector3.zero;
        }

        /// <summary>The park's middle on the ground: the pen walk point's x (u = 24 of 48) and v = 8.</summary>
        static Vector3 ZooParkCentre()
        {
            GameObject walk = ZooFind("PenWalkPoint");
            return new Vector3(walk != null ? walk.transform.position.x : ZooZonePosition().x - 17f, 0f, 8f);
        }

        static Transform ZooPenStand(int index)
        {
            GameObject animal = ZooFind("Pen_" + ZooAnimalKind.Order[index].Id);
            return animal != null ? animal.transform.Find("StandPoint") : null;
        }

        // ----- Staging -----

        /// <summary>The player (a stand-in for the follow camera's target) stands in the zone, facing north.</summary>
        static void StageZooPlayerAtZone()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            DisableMovers(player);
            player.transform.SetPositionAndRotation(ZooZonePosition(), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            Physics.SyncTransforms();
        }

        static void StageZooKeeper()
        {
            HideZoneVisuals();
            GameObject keeper = ZooFind("ZooKeeper");
            if (keeper == null) throw new InvalidOperationException("no ZooKeeper");

            ActivateWithParents(keeper);
            DisableMovers(keeper);
            Vector3 zone = ZooZonePosition();
            keeper.transform.SetPositionAndRotation(zone + new Vector3(-1.2f, 0f, 1.2f), Quaternion.LookRotation(Vector3.back, Vector3.up));
            ResetAnimators(keeper);
            StepAnimators(keeper, 0.6f);
            Physics.SyncTransforms();
        }

        static void ZooCaptureRests(List<ZooRest> rests)
        {
            rests.Clear();
            foreach (GameObject go in AllSceneObjects().Where(g => g.name.StartsWith("ZooAnimal_")))
            {
                Transform model = go.transform.Find("Model");
                rests.Add(new ZooRest
                {
                    Root = go.transform,
                    Position = go.transform.position,
                    Rotation = go.transform.rotation,
                    Model = model,
                    ModelPosition = model != null ? model.localPosition : Vector3.zero,
                    ModelActive = model != null && model.gameObject.activeSelf,
                });
            }
        }

        static void ZooRestoreRests(List<ZooRest> rests)
        {
            foreach (ZooRest rest in rests)
            {
                rest.Root.SetPositionAndRotation(rest.Position, rest.Rotation);
                if (rest.Model == null) continue;
                rest.Model.localPosition = rest.ModelPosition;
                rest.Model.gameObject.SetActive(rest.ModelActive);
            }
        }

        // ----- Cameras -----

        static void ZooLookFromZone(float fov)
        {
            Vector3 zone = ZooZonePosition();
            LookAt(zone + new Vector3(0f, 6.5f, -7f), zone + new Vector3(0f, 0.8f, 0f), fov);
            cam.farClipPlane = 300f;
        }

        /// <summary>From inside the alien field, looking west through the gap in the main fence into the park.</summary>
        static void ZooEntranceView()
        {
            GameObject home = ZooFind("KeeperHome");
            Vector3 zone = ZooZonePosition();
            // The main fence is at zone x + 7 (u = 7), the gap at v = -3.
            float fenceX = zone.x + 7f;
            Vector3 gap = new Vector3(fenceX, 0f, zone.z);
            LookAt(gap + new Vector3(9f, 3.8f, -2f), gap + new Vector3(-6f, 0.8f, 1f), 55f);
            cam.farClipPlane = 300f;
            if (home == null) Debug.LogWarning($"{LogPrefix}zoo entrance: no KeeperHome found.");
        }

        static void ZooTopView()
        {
            Vector3 centre = ZooParkCentre();
            LookAt(centre + new Vector3(0f, 85f, -30f), centre + new Vector3(0f, 0f, 3f), 60f);
            cam.farClipPlane = 400f;
        }

        /// <summary>The pens from first to last, seen from the walk in front of them at the follow camera's angle.</summary>
        static void ZooPensView(int first, int last)
        {
            Transform a = ZooPenStand(first);
            Transform b = ZooPenStand(last);
            if (a == null || b == null) throw new InvalidOperationException("no pens");

            Vector3 middle = (a.position + b.position) * 0.5f;
            LookAt(middle + new Vector3(0f, 6.5f, -12f), middle + new Vector3(0f, 0.8f, 0f), 50f);
            cam.farClipPlane = 300f;
        }

        static void ZooPensAllView()
        {
            Transform a = ZooPenStand(0);
            Transform b = ZooPenStand(ZooAnimalKind.Order.Length - 1);
            if (a == null || b == null) throw new InvalidOperationException("no pens");

            Vector3 middle = (a.position + b.position) * 0.5f;
            LookAt(middle + new Vector3(0f, 17f, -17f), middle + new Vector3(0f, 0.5f, 1f), 62f);
            cam.farClipPlane = 300f;
        }

        /// <summary>The first pen's gate and sign from the front, close.</summary>
        static void ZooGateSignView()
        {
            Transform stand = ZooPenStand(0);
            if (stand == null) throw new InvalidOperationException("no pen");

            Vector3 gate = stand.position + new Vector3(0f, 0f, -2.2f);
            LookAt(gate + new Vector3(-0.4f, 1.5f, -4.2f), gate + new Vector3(-0.4f, 0.7f, 0f), 45f);
        }

        static void ZooKeeperView(float azimuth)
        {
            GameObject keeper = ZooFind("ZooKeeper");
            if (keeper == null) throw new InvalidOperationException("no ZooKeeper");

            Orbit(keeper.transform.position + Vector3.up * 0.7f, keeper.transform, azimuth, 3.2f, 8f, 32f);
        }

        static void ZooKeeperHeadView()
        {
            GameObject keeper = ZooFind("ZooKeeper");
            if (keeper == null) throw new InvalidOperationException("no ZooKeeper");

            Animator animator = keeper.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 centre = head != null ? head.position : keeper.transform.position + Vector3.up * 1.1f;
            Orbit(centre, keeper.transform, 30f, 1.3f, 6f, 30f);
        }

        // ----- The hiding spots -----

        /// <summary>
        /// Puts the animal in its hide pose at the spot (north of a cover, sunk in a container, up in the crown) and looks at it from
        /// 4 m back and above, the way the follow camera would. A tall prop or animal gets a bit more room.
        /// </summary>
        static void ZooSpotView(List<ZooRest> rests, string spotId, string kindId)
        {
            ZooRestoreRests(rests);

            GameObject spot = ZooFind("Spot_" + spotId);
            GameObject animal = ZooFind("ZooAnimal_" + kindId);
            if (spot == null) throw new InvalidOperationException("no Spot_" + spotId);
            if (animal == null) throw new InvalidOperationException("no ZooAnimal_" + kindId);

            Transform hide = spot.transform.Find("HidePoint");
            if (hide == null) throw new InvalidOperationException("no HidePoint on Spot_" + spotId);

            // The real hide pose: HidingSpot.Occupy places the animal (north of a cover, from the rim height and the show fraction in a
            // container, head out of the tent, up in the crown). It starts no coroutine, so it works in edit mode too.
            HidingSpot component = spot.GetComponent<HidingSpot>();
            var zooAnimal = animal.GetComponent<ZooAnimal>();
            Transform model = animal.transform.Find("Model");
            if (component != null && zooAnimal != null)
            {
                component.Occupy(zooAnimal);
            }
            else
            {
                animal.transform.SetPositionAndRotation(hide.position, Quaternion.LookRotation(Vector3.back, Vector3.up));
                if (model != null) model.gameObject.SetActive(true);
            }
            Physics.SyncTransforms();

            ResetAnimators(animal);
            StepAnimators(animal, 0.3f);

            // The camera: aimed at the middle of the prop and the animal, 4 m back (more for something tall).
            Bounds propBounds = BoundsOf(component != null && component.transform.childCount > 0 ? component.transform.GetChild(0).gameObject : spot);
            TryGetBoundsOf(animal, out Bounds shown);
            Bounds both = propBounds;
            both.Encapsulate(shown);
            float tall = Mathf.Max(both.size.y, 1.5f);
            float back = Mathf.Max(4f, tall * 1.25f);
            Vector3 target = new Vector3(both.center.x, Mathf.Min(both.center.y, 2.2f) + 0.2f, both.center.z);
            LookAt(target + new Vector3(0f, back * 0.85f, -back), target, 50f);
            cam.farClipPlane = 300f;
        }

        // The spots he can hide in (his turn to hide).
        static readonly string[] ZooPlayerSpotShots = { "hedge", "tree", "bush", "rock", "table", "bin", "box", "leaves" };

        /// <summary>The player stands on the spot's PlayerHidePoint (facing south); the camera is 5 m back at the follow camera's angle.</summary>
        static void ZooPlayerHideView(string spotId)
        {
            GameObject spot = ZooFind("Spot_" + spotId);
            GameObject player = GameObject.Find("Player");
            if (spot == null) throw new InvalidOperationException("no Spot_" + spotId);
            if (player == null) throw new InvalidOperationException("no Player");

            Transform seat = spot.transform.Find("PlayerHidePoint");
            if (seat == null) throw new InvalidOperationException("no PlayerHidePoint on Spot_" + spotId);

            DisableMovers(player);
            player.transform.SetPositionAndRotation(seat.position, seat.rotation);
            Physics.SyncTransforms();

            Vector3 target = seat.position + new Vector3(0f, 0.9f, 0f);
            LookAt(target + new Vector3(0f, 4.2f, -5.5f), target, 50f);
            cam.farClipPlane = 300f;
        }

        static bool TryGetBoundsOf(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(false);
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            return found;
        }
    }
}
