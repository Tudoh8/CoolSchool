using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Aliens;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the alien landing field at the west end of the playground (-X, mirroring the knights arena on +X):
    /// a cardboard rocket, four trees with a perch each, real trampolines (one under every perch, one in the open field
    /// for the bubble, one beside the rocket), rocks and bushes for shy aliens to hide behind, slime puddle spots, spawn
    /// points all over the field, the party line, the glowing activity zone, the alien pool (kids and robots), two babies,
    /// the floating bubble, the slime traps, the superhero costume and ray gun for the player, and the AlienLanding that
    /// ties it all together (contract Docs/specs/alien-variety-contract.md).
    /// Two steps, like the knights arena, because the NavMesh must be baked after the static geometry exists but before
    /// any character: BuildField (static geometry) and Build (everything alive).
    /// All positions are worked out as a distance along the field (from its east edge, the playground side, toward the
    /// west end) and a distance across it (positive is north). The camera looks north, so the things the kid must see
    /// (the trees, the trampolines, the party line) are laid out north of where he stands.
    /// </summary>
    internal static class AlienFieldBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string ZoneRingMaterialPath = RootFolder + "/Materials/AlienZoneRing.mat";
        const string SlimeBallMaterialPath = RootFolder + "/Materials/AlienSlimeBall.mat";
        const string SlimeMarkerMaterialPath = RootFolder + "/Materials/AlienSlimeMarker.mat";
        const string PadMaterialPath = RootFolder + "/Materials/AlienJumpPad.mat";
        const string PadRingMaterialPath = RootFolder + "/Materials/AlienJumpPadRing.mat";
        const string BubbleMaterialPath = RootFolder + "/Materials/AlienBubble.mat";
        const string PuddleMaterialPath = RootFolder + "/Materials/AlienSlimePuddle.mat";
        // Made by BattleEffectsBuilder: a plain URP particle material that takes its colour from the vertex colour.
        const string ParticleMaterialPath = RootFolder + "/Materials/ParticleQuad.mat";

        // ---- Layout (metres along the field from the playground side, and across it, positive = north) ----
        const float ZoneAlong = 4.5f;
        const float ZoneRadius = 1.8f;

        // The rocket stands at the north-west of the field.
        const float RocketAlong = 16f;
        const float RocketAcross = 14.5f;
        const float RocketHeight = 5f;
        const float RocketSkyHeight = 25f;

        // The baby sits at the top of the rocket, a little to the south (toward its trampoline).
        const float RocketPerchSouth = 0.3f;
        const float RocketPerchBelowTop = 0.5f;

        // The open-field trampoline (bubble rescue); the bubble hangs 4.5 m above it.
        const float OpenPadAlong = 13f;
        const float OpenPadAcross = 9.5f;
        const float BubbleHeight = 4.5f;

        // Trees: the perch is in the crown on the south side, the trampoline 0.5 m further south.
        const float PerchHeight = 4f;
        const float PerchSouthMin = 1f;
        const float PerchSouthMax = 2f;
        const float PadSouthOfPerch = 0.5f;
        const float TrunkRadius = 0.5f;

        // Trampolines (Synty SM_Prop_Trampoline_01), scaled by bounds to this width.
        const float TrampolineSize = 2.4f;
        const float TrampolineRim = 1.15f;
        // Player gravity is -20; a launch of sqrt(2 * 20 * h) throws the feet h metres up.
        const float Gravity = 20f;
        const float OpenPadLaunchSpeed = 14.5f;

        // The party line: 10 spots south of the trees, facing the camera.
        const int PartySpots = 10;
        const float PartyAlongStart = 2.8f;
        const float PartyAcross = 5f;
        const float PartySpacing = 1.3f;

        // The pool: 8 alien kids and 2 robots (the most one visit can use).
        const int AlienKidCount = 8;

        // The counting close-up default (AlienLanding frames the real one for the number of aliens): 4.5 m back and 2.2 m up,
        // pitched 21 degrees down. The baby waits 3 m behind that camera.
        const float CountCameraHeight = 2.2f;
        const float CountCameraBack = 4.5f;
        const float CountCameraPitch = 21f;
        const float BabyWaitBack = 7.5f;

        // Things that must keep this far from each other (trunks, trampolines, covers, puddles, the party line).
        const float Clearance = 2.5f;

        // Where aliens may roam: along the field minus a 1 m / 1.5 m margin, and from 8 m south of the middle to 17 m north.
        const float BoundsAlongMin = 1f;
        const float BoundsAlongMargin = 1.5f;
        const float BoundsAcrossMin = -8f;
        const float BoundsAcrossMax = 17f;

        const int SlimeTrapCount = 3;
        const int BlobsPerTrap = 5;
        // SlimeTrap puts the blobs on a ring of its own radius (1.6 m) when it shows them; this is just where they rest in the scene.
        const float BlobRingRadius = 1.6f;
        const float BlobSize = 0.65f;
        const float PuddleDiameter = 2.6f;

        // Weapon and costume offsets relative to the hand / chest / head bone. These are guesses: tune them on the components.
        // The ray gun's barrel points along its own +Z, the hand's fingers along +X, so the gun is turned a quarter circle.
        static readonly Vector3 GunLocalPosition = new Vector3(0.05f, 0.02f, 0f);
        static readonly Vector3 GunLocalEuler = new Vector3(0f, 90f, 0f);

        static readonly PrefabRef RocketPrefab = Kids("SM_Prop_Cardboard_Rocket_01");
        static readonly PrefabRef TrampolinePrefab = new PrefabRef("SM_Prop_Trampoline_01", "PolygonTown");
        static readonly PrefabRef CapePrefab = Kids("SM_Chr_Attach_Cape_01");
        static readonly PrefabRef MaskPrefab = Kids("SM_Chr_Attach_Superhero_Mask_01");
        static readonly PrefabRef RayGunPrefab = Kids("SM_Wep_Ray_Gun_01");

        /// <summary>One tree: which model, where, how tall (0 = as the model is) and which way it faces.</summary>
        readonly struct TreeSpec
        {
            public readonly PrefabRef Prefab;
            public readonly float Along;
            public readonly float Across;
            public readonly float Height;
            public readonly float Yaw;

            public TreeSpec(PrefabRef prefab, float along, float across, float height, float yaw)
            {
                Prefab = prefab;
                Along = along;
                Across = across;
                Height = height;
                Yaw = yaw;
            }
        }

        // Tree 1 is the big treehouse tree of the first version (house toward the camera). The others are picked by name.
        static readonly TreeSpec[] TreeSpecs =
        {
            new TreeSpec(Kids("SM_Env_Tree_Large_01"), 8f, 13f, 0f, 180f),
            new TreeSpec(Kids("SM_Generic_Tree_02"), 21f, 6f, 7f, 0f),
            new TreeSpec(Kids("SM_Generic_Tree_03"), 2.5f, 15f, 7f, 90f),
            new TreeSpec(Kids("SM_Env_Tree_01"), 21.5f, 14.5f, 7f, 200f),
        };

        /// <summary>One cover prop for shy aliens, and where it stands. The rocks come from PolygonGeneric, the bushes from Kids and Town.</summary>
        readonly struct CoverSpec
        {
            public readonly PrefabRef Prefab;
            public readonly float Along;
            public readonly float Across;
            public readonly float Yaw;

            public CoverSpec(PrefabRef prefab, float along, float across, float yaw)
            {
                Prefab = prefab;
                Along = along;
                Across = across;
                Yaw = yaw;
            }
        }

        const float CoverHeight = 2.2f;
        const float CoverMaxWidth = 3.2f;

        static readonly CoverSpec[] CoverSpecs =
        {
            new CoverSpec(new PrefabRef("SM_Gen_Env_Rock_02", "PolygonGeneric"), 10f, 8f, 20f),
            new CoverSpec(Kids("SM_Env_Bush_01"), 18.5f, 8f, 0f),
            new CoverSpec(new PrefabRef("SM_Env_Bush_02", "PolygonTown"), 1.8f, 7.5f, 0f),
            new CoverSpec(new PrefabRef("SM_Gen_Env_Rock_05", "PolygonGeneric"), 5.5f, 16f, 140f),
        };

        // The three slime puddle spots.
        static readonly Vector2[] SlimeSpots =
        {
            new Vector2(5.5f, 9.3f),
            new Vector2(17.5f, 2f),
            new Vector2(11f, 15.5f),
        };

        static readonly PrefabRef[] AlienPrefabs =
        {
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
        };

        static readonly PrefabRef[] RobotPrefabs =
        {
            Kids("SM_Chr_Kid_Robot_01"),
            Kids("SM_Chr_Kid_Cardboard_Robot_01"),
        };

        static readonly PrefabRef[] BabyPrefabs =
        {
            Kids("SM_Chr_Baby_Alien_01"),
            Kids("SM_Chr_Baby_Alien_02"),
        };

        // Purple beacon with a hero face (or a lightning bolt if the hero icon is missing).
        static readonly ActivityBeaconBuilder.Style AlienBeacon = new ActivityBeaconBuilder.Style(
            "Aliens", new Color(1.6f, 0.7f, 2.2f, 1f), new[] { "Head-Hero", "Bolt" });

        // The order of AlienRescueSite's RescueKind enum (Tree, Rocket, Bubble, Slime).
        const int KindTree = 0;
        const int KindRocket = 1;
        const int KindBubble = 2;
        const int KindSlime = 3;

        /// <summary>A tree with its perch (where the baby sits) and the trampoline under it.</summary>
        internal sealed class TreeSite
        {
            public Vector3 Trunk;
            public Transform Perch;
            public JumpPad Pad;
        }

        /// <summary>The static geometry and the points that the second step needs.</summary>
        internal sealed class FieldLayout
        {
            public KnightsArenaBuilder.ArenaLayout Axis;
            public Transform Root;
            public Transform Rocket;
            public Transform RocketLanding;
            public Transform RocketSky;
            public Transform RocketPerch;
            public JumpPad RocketPad;
            public readonly List<TreeSite> Trees = new List<TreeSite>();
            public JumpPad OpenPad;
            public Transform BubbleHome;
            public readonly List<AlienCover> Covers = new List<AlienCover>();
            public Transform[] SlimeSpots;
            public Transform[] SpawnPoints;
            public Transform[] PartyPoints;
            public Bounds FieldBounds;
            public Vector3 ZonePosition;
            public bool RocketCreated;
            public int TreesCreated;

            // For the clearance check: (name, group, position). Things of the same group may be close (a tree and its trampoline).
            public readonly List<(string name, string group, Vector3 position)> Spaced = new List<(string, string, Vector3)>();
        }

        /// <summary>What the second step wires up.</summary>
        internal sealed class AlienInputs
        {
            public RuntimeAnimatorController KidController;
            public int KidsLayer = -1;
            public PlayerController Player;
            public ActionButton ActionButton;
            public ImaginationEffect Imagination;
        }

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The rocket (standing at its landing spot, so the NavMesh goes around it), the trees with their perches, the
        /// trampolines (their footprints are marked not walkable for the bake), the covers, the slime spots and the spawn,
        /// party and sky points. axis points from the playground into the field (west); startDistance and endDistance
        /// are where the field begins and ends along it, measured from the world origin.
        /// </summary>
        internal static FieldLayout BuildField(Transform environmentRoot, Vector3 axis, float startDistance, float endDistance)
        {
            var layout = new KnightsArenaBuilder.ArenaLayout(axis) { Start = startDistance, End = endDistance };
            var field = new FieldLayout { Axis = layout, Root = NewGroup("AlienField", environmentRoot) };

            Vector3 landing = FieldPoint(field, RocketAlong, RocketAcross);
            field.RocketLanding = NewPoint("RocketLanding", field.Root, landing);
            field.RocketSky = NewPoint("RocketSky", field.Root, landing + Vector3.up * RocketSkyHeight);
            field.ZonePosition = FieldPoint(field, ZoneAlong, 0f);

            float rocketRadius = BuildRocket(field, landing);
            BuildRocketTrampoline(field, landing, rocketRadius);
            BuildTrees(field);
            BuildOpenTrampoline(field);
            BuildCovers(field);
            BuildSlimeSpots(field);

            field.SpawnPoints = CreateSpawnPoints(field, landing, rocketRadius);
            field.PartyPoints = CreatePartyPoints(field);
            field.FieldBounds = FieldBoundsOf(field);

            foreach (Transform point in field.PartyPoints)
                field.Spaced.Add((point.name, "party", point.position));
            CheckClearance(field);
            return field;
        }

        /// <summary>A world position on the ground, "along" metres into the field and "across" metres to the north.</summary>
        static Vector3 FieldPoint(FieldLayout field, float along, float across)
        {
            return field.Axis.Point(field.Axis.Start + along, across);
        }

        static Bounds FieldBoundsOf(FieldLayout field)
        {
            float length = field.Axis.End - field.Axis.Start;
            Vector3 a = FieldPoint(field, BoundsAlongMin, BoundsAcrossMin);
            Vector3 b = FieldPoint(field, length - BoundsAlongMargin, BoundsAcrossMax);
            var bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);
            bounds.Encapsulate(new Vector3(a.x, -1f, a.z));
            bounds.Encapsulate(new Vector3(b.x, 6f, b.z));
            return bounds;
        }

        // ---- The rocket ----

        /// <summary>
        /// The cardboard rocket as a "Rocket" root (kinematic, so it can fly) with the model scaled to about 5 m tall
        /// and a capsule collider. It stands at the landing spot for now; the second step lifts it into the sky.
        /// The baby's perch for the rocket rescue is a child of it, at the top of the model, a little to the south.
        /// Returns the rocket's footprint radius.
        /// </summary>
        static float BuildRocket(FieldLayout field, Vector3 landing)
        {
            var root = new GameObject("Rocket");
            root.transform.SetParent(field.Root, false);
            root.transform.position = landing;
            field.Rocket = root.transform;

            float perchY = RocketHeight - RocketPerchBelowTop;
            float radius = 1.5f;

            GameObject prefab = LoadPrefab(RocketPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;

                if (TryGetBounds(model, out Bounds measured) && measured.size.y > 0.01f)
                    model.transform.localScale *= RocketHeight / measured.size.y;
                AlignToBounds(model, landing.x, landing.z, 0f, false);

                // The prefab's own mesh colliders are for something that never moves. One capsule does for a rocket that flies.
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                if (TryGetBounds(model, out Bounds bounds))
                {
                    radius = Mathf.Max(0.8f, 0.4f * Mathf.Max(bounds.size.x, bounds.size.z));
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = radius;
                    capsule.height = bounds.size.y;
                    capsule.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                    radius = Mathf.Max(radius, 0.5f * Mathf.Max(bounds.size.x, bounds.size.z));
                    perchY = bounds.max.y - landing.y - RocketPerchBelowTop;
                }

                field.RocketCreated = true;
            }

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // A child of the rocket, so it flies along. Faces the camera.
            field.RocketPerch = NewPoint("RocketPerch", root.transform, landing + new Vector3(0f, perchY, -RocketPerchSouth));
            field.RocketPerch.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            return radius;
        }

        /// <summary>The rocket's trampoline: about 2.5 m south of the rocket, hidden until the rocket rescue (the landing switches it on).</summary>
        static void BuildRocketTrampoline(FieldLayout field, Vector3 landing, float rocketRadius)
        {
            float distance = Mathf.Max(2.3f, rocketRadius + 1.1f);
            Vector3 position = landing + Vector3.back * distance;

            // Launch about 1 m above the nose.
            float perchHeight = field.RocketPerch.position.y;
            float speed = Mathf.Sqrt(2f * Gravity * (perchHeight + 1f));

            field.RocketPad = CreateTrampoline(field, "Trampoline_Rocket", position, speed, false);
            field.RocketPad.gameObject.SetActive(false);
            field.Spaced.Add(("Trampoline_Rocket", "rocket", position));
            field.Spaced.Add(("Rocket", "rocket", landing));
        }

        // ---- The trees ----

        /// <summary>
        /// Four trees. Their colliders are replaced by a thin trunk, so the kid can fly up through the crown. The perch point
        /// (where the baby sits) is on the south, camera-facing side of the crown, and the trampoline 0.5 m further south.
        /// </summary>
        static void BuildTrees(FieldLayout field)
        {
            for (int i = 0; i < TreeSpecs.Length; i++)
            {
                TreeSpec spec = TreeSpecs[i];
                Vector3 trunk = FieldPoint(field, spec.Along, spec.Across);
                float perchHeight = PerchHeight;
                float perchSouth = PerchSouthMin;

                GameObject prefab = LoadPrefab(spec.Prefab);
                if (prefab != null)
                {
                    Transform group = NewGroup($"Tree_{i + 1}", field.Root);
                    GameObject tree = Spawn(prefab, group);
                    tree.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);

                    if (spec.Height > 0f && TryGetBounds(tree, out Bounds measured) && measured.size.y > 0.01f)
                        tree.transform.localScale *= spec.Height / measured.size.y;
                    tree.transform.position = new Vector3(trunk.x, 0f, trunk.z);

                    if (TryGetBounds(tree, out Bounds bounds))
                    {
                        // Stand on the ground by its lowest point, with the trunk (the prefab's origin) at the wanted spot.
                        tree.transform.position += Vector3.up * -bounds.min.y;
                        TryGetBounds(tree, out bounds);

                        perchHeight = Mathf.Min(PerchHeight, bounds.size.y * 0.7f);
                        float south = trunk.z - bounds.min.z;
                        perchSouth = Mathf.Clamp(south * 0.55f, PerchSouthMin, PerchSouthMax);
                    }

                    foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);

                    // A thin trunk. The tree may be scaled, so the capsule is given in the tree's own units.
                    float scale = Mathf.Max(0.01f, tree.transform.lossyScale.y);
                    var trunkCollider = tree.AddComponent<CapsuleCollider>();
                    trunkCollider.direction = 1;
                    trunkCollider.radius = TrunkRadius / scale;
                    trunkCollider.height = 3f / scale;
                    trunkCollider.center = new Vector3(0f, 1.5f / scale, 0f);

                    FinishEnvironmentObject(tree);
                    field.TreesCreated++;
                }

                Vector3 perchGround = trunk + Vector3.back * perchSouth;
                var site = new TreeSite { Trunk = trunk };
                site.Perch = NewPoint($"TreePerch_{i + 1}", field.Root, perchGround + Vector3.up * perchHeight);
                // The baby faces the camera.
                site.Perch.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

                Vector3 padPosition = perchGround + Vector3.back * PadSouthOfPerch;
                float speed = Mathf.Sqrt(2f * Gravity * (perchHeight + 1.1f));
                site.Pad = CreateTrampoline(field, $"Trampoline_Tree_{i + 1}", padPosition, speed, true);
                field.Trees.Add(site);

                field.Spaced.Add(($"Tree_{i + 1}", $"tree{i}", trunk));
                field.Spaced.Add(($"Trampoline_Tree_{i + 1}", $"tree{i}", padPosition));
            }
        }

        /// <summary>The trampoline in the open field: the bubble rescue's. The bubble hangs 4.5 m above it.</summary>
        static void BuildOpenTrampoline(FieldLayout field)
        {
            Vector3 position = FieldPoint(field, OpenPadAlong, OpenPadAcross);
            field.OpenPad = CreateTrampoline(field, "Trampoline_Field", position, OpenPadLaunchSpeed, true);
            field.BubbleHome = NewPoint("BubbleHome", field.Root, position + Vector3.up * BubbleHeight);
            field.Spaced.Add(("Trampoline_Field", "field", position));
        }

        // ---- Trampolines ----

        /// <summary>
        /// A real trampoline: the Synty model (colliders stripped, so the kid can never get stuck on the frame), scaled to
        /// about 2.4 m across, with a JumpPad on it. The model sits under a "Model" pivot at the ground, so the squash
        /// presses it down onto the ground. The highlight (disc, ring, light) is hidden until the rescue needs this pad.
        /// The footprint is marked not walkable for the NavMesh bake with a NavMeshModifierVolume (the model has no collider,
        /// so the ground under it would stay walkable), so aliens walk around it. The player is not a NavMesh agent.
        /// </summary>
        static JumpPad CreateTrampoline(FieldLayout field, string name, Vector3 ground, float launchSpeed, bool alwaysBouncy)
        {
            ground.y = 0f;

            var padObject = new GameObject(name);
            padObject.transform.SetParent(field.Root, false);
            padObject.transform.position = ground;

            Transform pivot = NewGroup("Model", padObject.transform);
            float top = 0.4f;
            GameObject prefab = LoadPrefab(TrampolinePrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, pivot);
                model.name = "Trampoline";

                if (TryGetBounds(model, out Bounds measured))
                {
                    float width = Mathf.Max(measured.size.x, measured.size.z);
                    if (width > 0.01f) model.transform.localScale *= TrampolineSize / width;
                }

                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                AlignToBounds(model, ground.x, ground.z, 0f, false);
                if (TryGetBounds(model, out measured)) top = measured.max.y;
            }

            // The highlight: a glowing disc on the mat, a pulsing ring around the frame, and a soft purple light.
            Transform visual = NewGroup("Highlight", padObject.transform);
            Material discMaterial = KnightsArenaBuilder.CreateGlowMaterial(PadMaterialPath, new Color(0.85f, 0.3f, 1f), 0.6f);
            Material ringMaterial = KnightsArenaBuilder.CreateGlowMaterial(PadRingMaterialPath, new Color(1f, 0.7f, 1f), 0.35f);
            CreateFlatDisc("Disc", visual, TrampolineSize * 0.85f, top + 0.03f, discMaterial);
            Transform ring = CreateFlatDisc("Ring", visual, TrampolineSize * 1.2f, 0.05f, ringMaterial);

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(visual, false);
            lightObject.transform.localPosition = new Vector3(0f, top + 1.2f, 0f);
            var glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.85f, 0.4f, 1f);
            glow.range = 4f;
            glow.intensity = 2f;
            glow.shadows = LightShadows.None;

            var pad = padObject.AddComponent<JumpPad>();
            SetObject(pad, "visual", visual.gameObject);
            SetObject(pad, "ring", ring);
            SetObject(pad, "model", pivot);
            SetFloat(pad, "radius", TrampolineRim);
            SetFloat(pad, "launchSpeed", launchSpeed);
            SetBool(pad, "alwaysBouncy", alwaysBouncy);
            visual.gameObject.SetActive(false);

            // Not walkable for the bake. It stays in the scene (it does nothing at run time); it is a separate object so
            // the hidden rocket trampoline keeps its hole.
            var footprint = new GameObject($"NoWalk_{name}");
            footprint.transform.SetParent(field.Root, false);
            footprint.transform.position = ground;
            var volume = footprint.AddComponent<NavMeshModifierVolume>();
            volume.size = new Vector3(TrampolineSize + 0.4f, 3f, TrampolineSize + 0.4f);
            volume.center = new Vector3(0f, 1f, 0f);
            volume.area = 1; // Not Walkable

            return pad;
        }

        // ---- Covers and slime spots ----

        /// <summary>
        /// Four rocks and bushes for shy aliens, scaled to about 2.2 m tall so one hides behind each from the camera. Each
        /// is a root with an AlienCover, a plain box collider (the NavMesh goes around it), a hide point just north of it
        /// and a peek point beside it, on the side toward the middle of the field.
        /// </summary>
        static void BuildCovers(FieldLayout field)
        {
            Transform group = NewGroup("Covers", field.Root);
            float middle = (field.Axis.End - field.Axis.Start) * 0.5f;

            for (int i = 0; i < CoverSpecs.Length; i++)
            {
                CoverSpec spec = CoverSpecs[i];
                Vector3 ground = FieldPoint(field, spec.Along, spec.Across);

                var root = new GameObject($"Cover_{i + 1}");
                root.transform.SetParent(group, false);
                root.transform.position = ground;

                Vector3 center = ground;
                Vector3 size = new Vector3(2f, CoverHeight, 2f);

                GameObject prefab = LoadPrefab(spec.Prefab);
                if (prefab != null)
                {
                    GameObject prop = Spawn(prefab, root.transform);
                    prop.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);

                    if (TryGetBounds(prop, out Bounds measured) && measured.size.y > 0.01f)
                    {
                        float scale = Mathf.Min(CoverHeight / measured.size.y, CoverMaxWidth / Mathf.Max(0.5f, Mathf.Max(measured.size.x, measured.size.z)));
                        prop.transform.localScale *= scale;
                    }

                    foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);

                    AlignToBounds(prop, ground.x, ground.z, 0f, false);
                    if (TryGetBounds(prop, out Bounds bounds))
                    {
                        center = bounds.center;
                        size = bounds.size;
                    }
                }

                // One box for the NavMesh bake, a bit smaller than the model.
                var box = root.AddComponent<BoxCollider>();
                box.center = center - ground;
                box.size = new Vector3(size.x * 0.85f, size.y, size.z * 0.85f);

                // North of it, out of the camera's sight; and beside it, toward the middle of the field.
                float north = center.z + size.z * 0.5f + 0.5f;
                float side = spec.Along < middle ? 1f : -1f; // along the axis (west) is +1
                Vector3 beside = center + field.Axis.Axis * (side * (size.x * 0.5f + 1f));
                Transform hide = NewPoint("HidePoint", root.transform, new Vector3(center.x, 0f, north));
                Transform peek = NewPoint("PeekPoint", root.transform, new Vector3(beside.x, 0f, center.z));
                hide.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                peek.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

                var cover = root.AddComponent<AlienCover>();
                SetObject(cover, "hidePoint", hide);
                SetObject(cover, "peekPoint", peek);
                field.Covers.Add(cover);

                field.Spaced.Add(($"Cover_{i + 1}", $"cover{i}", ground));
            }
        }

        static void BuildSlimeSpots(FieldLayout field)
        {
            Transform group = NewGroup("SlimeSpots", field.Root);
            field.SlimeSpots = new Transform[SlimeSpots.Length];
            for (int i = 0; i < SlimeSpots.Length; i++)
            {
                Vector3 position = FieldPoint(field, SlimeSpots[i].x, SlimeSpots[i].y);
                field.SlimeSpots[i] = NewPoint($"SlimeSpot_{i + 1}", group, position);
                field.Spaced.Add(($"SlimeSpot_{i + 1}", $"slime{i}", position));
            }
        }

        // ---- Spawn and party points ----

        /// <summary>
        /// Eight spawn points all over the field: two by the rocket's door (south-east and south-west of its foot), the
        /// north side of each tree, and behind two of the covers. At least two are in the east half and two in the west half.
        /// </summary>
        static Transform[] CreateSpawnPoints(FieldLayout field, Vector3 landing, float rocketRadius)
        {
            Transform group = NewGroup("SpawnPoints", field.Root);
            var points = new List<Transform>();

            // The rocket's door: either side of its south, away from the rocket trampoline.
            float distance = rocketRadius + 1.3f;
            float angle = 55f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * distance;
            points.Add(NewPoint("AlienSpawn_RocketEast", group, landing + offset));
            points.Add(NewPoint("AlienSpawn_RocketWest", group, landing + new Vector3(-offset.x, 0f, offset.z)));

            for (int i = 0; i < field.Trees.Count; i++)
                points.Add(NewPoint($"AlienSpawn_Tree{i + 1}", group, field.Trees[i].Trunk + Vector3.forward * 2.5f));

            // Behind the first two covers, a little further north than their hide points.
            for (int i = 0; i < 2 && i < field.Covers.Count; i++)
                points.Add(NewPoint($"AlienSpawn_Cover{i + 1}", group, field.Covers[i].HidePoint.position + Vector3.forward * 1.2f));

            return points.ToArray();
        }

        /// <summary>A row of ten dance spots facing the camera: eight aliens, the baby and a spare. A visit uses a centred slice.</summary>
        static Transform[] CreatePartyPoints(FieldLayout field)
        {
            Transform group = NewGroup("PartyLine", field.Root);
            var points = new Transform[PartySpots];
            for (int i = 0; i < points.Length; i++)
            {
                Transform point = NewPoint($"PartySpot_{i + 1}", group, FieldPoint(field, PartyAlongStart + i * PartySpacing, PartyAcross));
                point.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                points[i] = point;
            }
            return points;
        }

        /// <summary>Logs any two things (trunks, trampolines, covers, puddles, party spots) closer than 2.5 m that are not one group.</summary>
        static void CheckClearance(FieldLayout field)
        {
            int problems = 0;
            for (int i = 0; i < field.Spaced.Count; i++)
            {
                for (int j = i + 1; j < field.Spaced.Count; j++)
                {
                    var a = field.Spaced[i];
                    var b = field.Spaced[j];
                    if (a.group == b.group) continue;

                    Vector3 delta = a.position - b.position;
                    delta.y = 0f;
                    if (delta.magnitude >= Clearance) continue;

                    problems++;
                    Debug.LogWarning($"CoolSchool alien field: {a.name} and {b.name} are only {delta.magnitude:0.0} m apart (want {Clearance:0.0}).");
                }
            }
            if (problems == 0) Debug.Log($"CoolSchool alien field: all {field.Spaced.Count} spaced things keep {Clearance:0.0} m apart.");
        }

        // ----- Step 2: everything alive --------------------------------------------------------

        /// <summary>
        /// Creates the aliens, the babies, the bubble, the slime traps and the zone, dresses the player up, and builds the AlienLanding.
        /// Call after the NavMesh is baked, after the player exists and after the lesson UI and the activity inviter exist.
        /// </summary>
        internal static AlienLanding Build(FieldLayout field, AlienInputs inputs)
        {
            if (field == null || inputs == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the alien field was not built (no field layout or no player).");
                return null;
            }

            Transform root = field.Root;
            SnapPointsToNavMesh(field.SpawnPoints);
            SnapPointsToNavMesh(field.PartyPoints);
            foreach (AlienCover cover in field.Covers)
            {
                cover.HidePoint.position = SnapToNavMesh(cover.HidePoint.position);
                cover.PeekPoint.position = SnapToNavMesh(cover.PeekPoint.position);
            }

            // The rocket waits in the sky until the game starts.
            if (field.Rocket != null)
            {
                field.Rocket.position = field.RocketSky.position;
                field.Rocket.gameObject.SetActive(false);
            }

            Material slimeBall = KnightsArenaBuilder.CreateEmissiveMaterial(SlimeBallMaterialPath, new Color(0.35f, 0.9f, 0.25f));
            Material slimeMarker = KnightsArenaBuilder.CreateGlowMaterial(SlimeMarkerMaterialPath, new Color(0.4f, 1f, 0.3f), 0.4f);
            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);

            var landing = new GameObject("AlienLanding").AddComponent<AlienLanding>();
            landing.transform.SetParent(root, false);

            AlienKidAI[] aliens = CreateAliens(field, inputs, slimeBall, slimeMarker, particleMaterial);
            RescueTarget[] babies = CreateBabies(field, inputs);
            FloatingBubble bubble = CreateBubble(field);
            SlimeTrap[] traps = CreateSlimeTraps(field);
            AlienRescueSite[] sites = CreateRescueSites(field, bubble, traps);
            RayGunShooter rayGun = DressUpPlayer(inputs, particleMaterial);
            ImaginationEffect imagination = CreateImagination(inputs.Imagination);

            WireLanding(landing, field, inputs, rayGun, imagination, babies, aliens, sites);
            WireCountShot(landing, field);

            Material ringMaterial = KnightsArenaBuilder.CreateGlowMaterial(ZoneRingMaterialPath, new Color(0.7f, 0.35f, 1f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("AlienZone", root, field.ZonePosition, ZoneRadius, landing,
                ringMaterial, AlienBeacon);
            AddToInviter(zone);

            LogField(field, aliens.Length, babies.Length, sites.Length);
            return landing;
        }

        static void SnapPointsToNavMesh(Transform[] points)
        {
            foreach (Transform point in points)
                if (point != null) point.position = SnapToNavMesh(point.position);
        }

        static void LogField(FieldLayout field, int aliens, int babies, int sites)
        {
            string trees = "";
            foreach (TreeSite tree in field.Trees)
                trees += $" trunk {tree.Trunk.ToString("0.0")} perch {tree.Perch.position.ToString("0.0")} pad {tree.Pad.transform.position.ToString("0.0")};";

            string covers = "";
            foreach (AlienCover cover in field.Covers)
                covers += $" {cover.transform.position.ToString("0.0")} (hide {cover.HidePoint.position.ToString("0.0")}, peek {cover.PeekPoint.position.ToString("0.0")});";

            string slime = "";
            foreach (Transform spot in field.SlimeSpots) slime += $" {spot.position.ToString("0.0")};";

            string spawns = "";
            foreach (Transform spawn in field.SpawnPoints) spawns += $" {spawn.position.ToString("0.0")};";

            Debug.Log($"CoolSchool alien field: zone {field.ZonePosition.ToString("0.0")}, rocket {field.RocketLanding.position.ToString("0.0")} "
                + $"(perch {field.RocketPerch.position.ToString("0.0")}, trampoline {field.RocketPad.transform.position.ToString("0.0")}, hidden), "
                + $"field trampoline {field.OpenPad.transform.position.ToString("0.0")} (bubble {field.BubbleHome.position.ToString("0.0")}).\n"
                + $"  trees:{trees}\n  covers:{covers}\n  slime spots:{slime}\n  spawn points ({field.SpawnPoints.Length}):{spawns}\n"
                + $"  party line {field.PartyPoints[0].position.ToString("0.0")}..{field.PartyPoints[field.PartyPoints.Length - 1].position.ToString("0.0")} "
                + $"({field.PartyPoints.Length} spots); field bounds {field.FieldBounds.min.ToString("0.0")}..{field.FieldBounds.max.ToString("0.0")}; "
                + $"{aliens} aliens/robots, {babies} babies, {sites} rescue sites.");
        }

        // ---- Shared bits ----

        static Transform CreateFlatDisc(string discName, Transform parent, float diameter, float lift, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = discName;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, lift, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);

            var renderer = disc.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return disc.transform;
        }

        // ---- Aliens and the babies ----

        /// <summary>
        /// The pool: eight alien kids (alternating the two models) and two robots. The kind of a kid is chosen at run time;
        /// only the robots are marked here (isRobot).
        /// </summary>
        static AlienKidAI[] CreateAliens(FieldLayout field, AlienInputs inputs, Material slimeBall, Material slimeMarker, Material trail)
        {
            Transform group = NewGroup("Aliens", field.Root);
            var aliens = new List<AlienKidAI>();

            for (int i = 0; i < AlienKidCount + RobotPrefabs.Length; i++)
            {
                bool robot = i >= AlienKidCount;
                PrefabRef prefab = robot ? RobotPrefabs[i - AlienKidCount] : AlienPrefabs[i % AlienPrefabs.Length];

                Vector3 position = SnapToNavMesh(field.SpawnPoints[i % field.SpawnPoints.Length].position);
                string kidName = prefab.Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid($"{kidName}_{i + 1}", prefab, inputs.KidController, position, out float height);
                root.transform.SetParent(group, true);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 1.4f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                var hittable = root.AddComponent<Hittable>();
                SetEnum(hittable, "team", (int)BattleTeam.Opponents);
                SetInt(hittable, "hitsToDefeat", 1);
                SetBool(hittable, "canBeDefeated", true);
                SetFloat(hittable, "stunSeconds", 0.3f);

                var thrower = root.AddComponent<SlimeThrower>();
                SetObject(thrower, "hand", RightHandOf(root));
                SetObject(thrower, "slimeMaterial", slimeBall);
                SetObject(thrower, "markerMaterial", slimeMarker);
                SetObject(thrower, "trailMaterial", trail);

                var ai = root.AddComponent<AlienKidAI>();
                if (robot) SetBool(ai, "isRobot", true);
                aliens.Add(ai);

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
            }
            return aliens.ToArray();
        }

        /// <summary>The two baby aliens: kid-rig characters that are switched off, with their legs (the agent) off until they are rescued.</summary>
        static RescueTarget[] CreateBabies(FieldLayout field, AlienInputs inputs)
        {
            Transform group = field.Root.Find("Aliens");
            if (group == null) group = NewGroup("Aliens", field.Root);

            var babies = new RescueTarget[BabyPrefabs.Length];
            for (int i = 0; i < BabyPrefabs.Length; i++)
            {
                Vector3 position = SnapToNavMesh(field.SpawnPoints[0].position);
                GameObject root = CreateKid($"Baby_Alien_{i + 1}", BabyPrefabs[i], inputs.KidController, position, out float height);
                root.transform.SetParent(group, true);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.25f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 2.6f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.22f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                babies[i] = root.AddComponent<RescueTarget>();

                // It sits up high or in the slime, off the NavMesh, until it is rescued.
                agent.enabled = false;
                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
            }
            return babies;
        }

        static Transform RightHandOf(GameObject kidRoot)
        {
            Animator animator = kidRoot.GetComponentInChildren<Animator>();
            return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        }

        // ---- The bubble ----

        /// <summary>
        /// The floating bubble: a see-through URP sphere about 1.6 m across with a faint iridescent tint. The "Seat" (where
        /// the baby sits, and the middle for popping) is at the root; the skin is lifted 0.4 m so the baby, whose feet are on
        /// the seat, sits inside it. Hidden until the bubble rescue shows it.
        /// </summary>
        static FloatingBubble CreateBubble(FieldLayout field)
        {
            Material material = KnightsArenaBuilder.CreateGlowMaterial(BubbleMaterialPath, new Color(0.72f, 0.92f, 1f), 0.2f);
            if (material != null)
            {
                // Soft and shiny, with only a little glow so it stays see-through.
                material.SetColor("_EmissionColor", new Color(0.55f, 0.35f, 0.75f) * 0.5f);
                material.SetFloat("_Smoothness", 0.95f);
                EditorUtility.SetDirty(material);
            }

            var bubbleObject = new GameObject("RescueBubble");
            bubbleObject.transform.SetParent(field.Root, false);
            bubbleObject.transform.position = field.BubbleHome.position;

            var seat = new GameObject("Seat").transform;
            seat.SetParent(bubbleObject.transform, false);
            seat.localPosition = Vector3.zero;
            seat.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            GameObject skin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skin.name = "Skin";
            Object.DestroyImmediate(skin.GetComponent<Collider>());
            skin.transform.SetParent(bubbleObject.transform, false);
            skin.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            skin.transform.localScale = Vector3.one * 1.6f;
            var renderer = skin.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var bubble = bubbleObject.AddComponent<FloatingBubble>();
            SetObject(bubble, "seat", seat);
            SetObject(bubble, "visual", skin);

            bubbleObject.SetActive(false);
            return bubble;
        }

        // ---- The slime traps ----

        /// <summary>
        /// One slime trap for each slime spot: a flat green puddle, a spot for the stuck baby, and five slime blobs in a ring
        /// around it (green spheres with a Hittable on the Opponents team, one hit). Everything but the trap itself is switched
        /// off until the slime rescue shows it.
        /// </summary>
        static SlimeTrap[] CreateSlimeTraps(FieldLayout field)
        {
            Material puddleMaterial = KnightsArenaBuilder.CreateEmissiveMaterial(PuddleMaterialPath, new Color(0.3f, 0.8f, 0.2f));
            if (puddleMaterial != null)
            {
                puddleMaterial.SetFloat("_Smoothness", 0.9f);
                puddleMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 0.2f) * 0.5f);
                EditorUtility.SetDirty(puddleMaterial);
            }
            var blobMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlimeBallMaterialPath);

            Transform group = NewGroup("SlimeTraps", field.Root);
            var traps = new SlimeTrap[field.SlimeSpots.Length];
            for (int i = 0; i < traps.Length; i++)
            {
                Vector3 spot = SnapToNavMesh(field.SlimeSpots[i].position);
                var trapObject = new GameObject($"SlimeTrap_{i + 1}");
                trapObject.transform.SetParent(group, false);
                trapObject.transform.position = spot;

                // The puddle: a squashed sphere, mostly under the ground.
                GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puddle.name = "Puddle";
                Object.DestroyImmediate(puddle.GetComponent<Collider>());
                puddle.transform.SetParent(trapObject.transform, false);
                puddle.transform.localPosition = Vector3.zero;
                puddle.transform.localScale = new Vector3(PuddleDiameter, 0.16f, PuddleDiameter);
                var puddleRenderer = puddle.GetComponent<MeshRenderer>();
                if (puddleMaterial != null) puddleRenderer.sharedMaterial = puddleMaterial;
                puddleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Transform babySpot = NewPoint("BabySpot", trapObject.transform, spot + Vector3.up * 0.05f);
                babySpot.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

                Transform blobGroup = NewGroup("Blobs", trapObject.transform);
                var blobs = new SlimeBlob[BlobsPerTrap];
                for (int b = 0; b < blobs.Length; b++)
                {
                    // A ring around the puddle; the first blob is due south so the player sees it first.
                    float angle = (b / (float)blobs.Length) * Mathf.PI * 2f + Mathf.PI * 1.5f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * BlobRingRadius;
                    Vector3 position = spot + offset + Vector3.up * (BlobSize * 0.5f);

                    GameObject blobObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blobObject.name = $"SlimeBlob_{b + 1}";
                    Object.DestroyImmediate(blobObject.GetComponent<Collider>());
                    blobObject.transform.SetParent(blobGroup, false);
                    blobObject.transform.position = position;
                    blobObject.transform.localScale = Vector3.one * BlobSize;
                    var blobRenderer = blobObject.GetComponent<MeshRenderer>();
                    if (blobMaterial != null) blobRenderer.sharedMaterial = blobMaterial;
                    blobRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    var hittable = blobObject.AddComponent<Hittable>();
                    SetEnum(hittable, "team", (int)BattleTeam.Opponents);
                    SetInt(hittable, "hitsToDefeat", 1);
                    SetBool(hittable, "canBeDefeated", true);
                    SetFloat(hittable, "stunSeconds", 0f);
                    SetFloat(hittable, "hitRadius", 0.5f);

                    blobs[b] = blobObject.AddComponent<SlimeBlob>();
                    blobObject.SetActive(false);
                }

                var trap = trapObject.AddComponent<SlimeTrap>();
                SetObject(trap, "puddle", puddle);
                SetObject(trap, "babySpot", babySpot);
                SetObjectArray(trap, "blobs", blobs);
                traps[i] = trap;

                // Hidden until the slime rescue shows it (SlimeTrap.Show switches the trap and the puddle on).
                puddle.SetActive(false);
                trapObject.SetActive(false);
            }
            return traps;
        }

        // ---- Rescue sites ----

        /// <summary>One site for each tree, one for the rocket, one for the bubble and one for each slime trap.</summary>
        static AlienRescueSite[] CreateRescueSites(FieldLayout field, FloatingBubble bubble, SlimeTrap[] traps)
        {
            Transform group = NewGroup("RescueSites", field.Root);
            var sites = new List<AlienRescueSite>();

            for (int i = 0; i < field.Trees.Count; i++)
                sites.Add(CreateSite(group, $"RescueSite_Tree_{i + 1}", KindTree, field.Trees[i].Perch, field.Trees[i].Pad, null, null));

            sites.Add(CreateSite(group, "RescueSite_Rocket", KindRocket, field.RocketPerch, field.RocketPad, null, null));
            sites.Add(CreateSite(group, "RescueSite_Bubble", KindBubble, field.BubbleHome, field.OpenPad, bubble, null));

            for (int i = 0; i < traps.Length; i++)
                sites.Add(CreateSite(group, $"RescueSite_Slime_{i + 1}", KindSlime, null, null, null, traps[i]));

            return sites.ToArray();
        }

        static AlienRescueSite CreateSite(Transform parent, string name, int kind, Transform perch, JumpPad pad, FloatingBubble bubble, SlimeTrap slime)
        {
            var siteObject = new GameObject(name);
            siteObject.transform.SetParent(parent, false);
            if (perch != null) siteObject.transform.position = perch.position;
            else if (slime != null) siteObject.transform.position = slime.transform.position;

            var site = siteObject.AddComponent<AlienRescueSite>();
            SetEnum(site, "kind", kind);
            SetObject(site, "perch", perch);
            SetObject(site, "pad", pad);
            SetObject(site, "bubble", bubble);
            SetObject(site, "slime", slime);
            return site;
        }

        // ---- The player ----

        /// <summary>Gives the player the superhero cape and mask, and the ray gun (switched off, the alien landing turns it on).</summary>
        static RayGunShooter DressUpPlayer(AlienInputs inputs, Material beamMaterial)
        {
            GameObject player = inputs.Player.gameObject;

            var costume = player.AddComponent<CostumeHolder>();
            SetObject(costume, "capePrefab", LoadPrefab(CapePrefab));
            SetObject(costume, "maskPrefab", LoadPrefab(MaskPrefab));

            var rayGun = player.AddComponent<RayGunShooter>();
            SetObject(rayGun, "button", inputs.ActionButton);
            SetObject(rayGun, "beamMaterial", beamMaterial);
            SetObject(rayGun, "gunPrefab", LoadPrefab(RayGunPrefab));
            SetVector3(rayGun, "gunLocalPosition", GunLocalPosition);
            SetVector3(rayGun, "gunLocalEuler", GunLocalEuler);
            rayGun.enabled = false;
            return rayGun;
        }

        /// <summary>
        /// The alien landing's own imagination glow. It shares the gold overlay with the castle's, but has no props:
        /// the castle's flags and torches must not appear on the alien field.
        /// </summary>
        static ImaginationEffect CreateImagination(ImaginationEffect castleEffect)
        {
            var effectObject = new GameObject("AlienImagination");
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

        // ---- Wiring ----

        static void WireLanding(AlienLanding landing, FieldLayout field, AlienInputs inputs, RayGunShooter rayGun,
            ImaginationEffect imagination, RescueTarget[] babies, AlienKidAI[] aliens, AlienRescueSite[] sites)
        {
            SetObject(landing, "player", inputs.Player);
            SetObject(landing, "rayGun", rayGun);
            SetObject(landing, "actionButton", inputs.ActionButton);
            SetObject(landing, "boltIcon", ActivityBeaconBuilder.FindSprite(new[] { "Bolt" }));
            SetObject(landing, "imagination", imagination);
            SetObject(landing, "rocket", field.Rocket);
            SetObject(landing, "rocketLandingPoint", field.RocketLanding);
            SetObject(landing, "rocketSkyPoint", field.RocketSky);
            SetObjectArray(landing, "alienSpawnPoints", field.SpawnPoints);
            SetObjectArray(landing, "aliens", aliens);
            SetObjectArray(landing, "babies", babies);
            SetObjectArray(landing, "rescueSites", sites);
            SetObjectArray(landing, "covers", field.Covers);
            SetObjectArray(landing, "partyLinePoints", field.PartyPoints);
            SetBounds(landing, "fieldBounds", field.FieldBounds);
            // Optional: the number cards are only there if the lesson was built.
            SetObject(landing, "lessonUI", Object.FindFirstObjectByType<LessonUI>());
        }

        /// <summary>
        /// The default close-up for the counting question: a camera point in front of (south of) the middle of the party line,
        /// looking north and a little down so the aliens fill the upper two thirds of the screen above the number cards. The
        /// alien landing frames the real one at run time for the number of aliens. And the baby's waiting spot behind that
        /// camera, which is never in the shot.
        /// </summary>
        static void WireCountShot(AlienLanding landing, FieldLayout field)
        {
            Vector3 center = Vector3.zero;
            foreach (Transform point in field.PartyPoints) center += point.position;
            center /= field.PartyPoints.Length;

            Transform group = NewGroup("CountShot", field.Root);
            Vector3 cameraPosition = center + new Vector3(0f, CountCameraHeight, -CountCameraBack);
            Transform camera = NewPoint("CountCamera", group, cameraPosition);
            camera.rotation = Quaternion.Euler(CountCameraPitch, 0f, 0f);

            Transform wait = NewPoint("BabyWaitPoint", group, SnapToNavMesh(center + Vector3.back * BabyWaitBack));
            wait.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            SetObject(landing, "countCameraPoint", camera);
            SetObject(landing, "babyWaitPoint", wait);
            SetObject(landing, "cameraRig", landing.gameObject.AddComponent<LessonCameraRig>());
        }

        /// <summary>Adds the alien zone at the END of the inviter's turns, with its own invite line. Skips quietly if there is no inviter.</summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the alien field will not invite the kid.");
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
            groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = "invite.aliens";

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform NewPoint(string name, Transform parent, Vector3 position)
        {
            Transform point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }
    }
}
