using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Aliens;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the alien landing field at the west end of the playground (-X, mirroring the knights arena on +X):
    /// a cardboard rocket, a big tree with a perch for the baby alien, a jump pad, the spawn and party points, the
    /// glowing activity zone, four alien kids and the baby, the superhero costume and ray gun for the player, and the
    /// AlienLanding that ties it all together.
    /// Two steps, like the knights arena, because the NavMesh must be baked after the rocket and tree exist but before
    /// any character: BuildField (static geometry) and Build (everything alive).
    /// All positions are worked out as a distance along the field (from its east edge, the playground side, toward the
    /// west end) and a distance across it (positive is north). The camera looks north, so the things the kid must see
    /// (the tree, the pad, the party line) are laid out north of where he stands.
    /// </summary>
    internal static class AlienFieldBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string ZoneRingMaterialPath = RootFolder + "/Materials/AlienZoneRing.mat";
        const string SlimeBallMaterialPath = RootFolder + "/Materials/AlienSlimeBall.mat";
        const string SlimeMarkerMaterialPath = RootFolder + "/Materials/AlienSlimeMarker.mat";
        const string PadMaterialPath = RootFolder + "/Materials/AlienJumpPad.mat";
        const string PadRingMaterialPath = RootFolder + "/Materials/AlienJumpPadRing.mat";
        // Made by BattleEffectsBuilder: a plain URP particle material that takes its colour from the vertex colour.
        const string ParticleMaterialPath = RootFolder + "/Materials/ParticleQuad.mat";

        // ---- Layout (metres along the field from the playground side, and across it, positive = north) ----
        const float ZoneAlong = 4.5f;
        const float ZoneRadius = 1.8f;

        // The rocket stands at the north-west of the field; the tree and pad are closer to the entrance.
        const float RocketAlong = 16f;
        const float RocketAcross = 14.5f;
        const float RocketHeight = 5f;
        const float RocketSkyHeight = 25f;

        // The trunk stands 1.7 m north of the pad: the perch is in the crown 1.2 m south of the trunk, the pad 0.5 m south of the perch.
        const float TreeAlong = 11f;
        const float TreeAcross = 10.7f;
        const float PerchHeight = 4f;
        const float PerchSouthOfTrunk = 1.2f;
        const float PadSouthOfPerch = 0.5f;
        const float TrunkRadius = 0.5f;

        // The party line is north-east of the pad, east of the tree, facing the camera.
        const float PartyAlongStart = 2f;
        const float PartyAcross = 12f;
        const float PartySpacing = 1.4f;

        const int AlienCount = 4;
        const float PadRadius = 1f;

        // Weapon and costume offsets relative to the hand / chest / head bone. These are guesses: tune them on the components.
        // The ray gun's barrel points along its own +Z, the hand's fingers along +X, so the gun is turned a quarter circle.
        static readonly Vector3 GunLocalPosition = new Vector3(0.05f, 0.02f, 0f);
        static readonly Vector3 GunLocalEuler = new Vector3(0f, 90f, 0f);

        static readonly PrefabRef RocketPrefab = Kids("SM_Prop_Cardboard_Rocket_01");
        static readonly PrefabRef TreePrefab = Kids("SM_Env_Tree_Large_01");
        static readonly PrefabRef BabyPrefab = Kids("SM_Chr_Baby_Alien_01");
        static readonly PrefabRef CapePrefab = Kids("SM_Chr_Attach_Cape_01");
        static readonly PrefabRef MaskPrefab = Kids("SM_Chr_Attach_Superhero_Mask_01");
        static readonly PrefabRef RayGunPrefab = Kids("SM_Wep_Ray_Gun_01");

        static readonly PrefabRef[] AlienPrefabs =
        {
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
            Kids("SM_Chr_Kid_Alien_01"),
            Kids("SM_Chr_Kid_Alien_02"),
        };

        // Purple beacon with a hero face (or a lightning bolt if the hero icon is missing).
        static readonly ActivityBeaconBuilder.Style AlienBeacon = new ActivityBeaconBuilder.Style(
            "Aliens", new Color(1.6f, 0.7f, 2.2f, 1f), new[] { "Head-Hero", "Bolt" });

        /// <summary>The static geometry and the points that the second step needs.</summary>
        internal sealed class FieldLayout
        {
            public KnightsArenaBuilder.ArenaLayout Axis;
            public Transform Root;
            public Transform Rocket;
            public Transform RocketLanding;
            public Transform RocketSky;
            public Transform TreePerch;
            public Transform[] SpawnPoints;
            public Transform[] PartyPoints;
            public Vector3 PadPosition;
            public Vector3 ZonePosition;
            public bool RocketCreated;
            public bool TreeCreated;
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
        /// The rocket (standing at its landing spot, so the NavMesh goes around it), the tree with its perch point, and the
        /// spawn, party and sky points. axis points from the playground into the field (west); startDistance and endDistance
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
            BuildTree(field);

            field.SpawnPoints = CreateSpawnPoints(field, landing, rocketRadius);
            field.PartyPoints = CreatePartyPoints(field);
            return field;
        }

        /// <summary>A world position on the ground, "along" metres into the field and "across" metres to the north.</summary>
        static Vector3 FieldPoint(FieldLayout field, float along, float across)
        {
            return field.Axis.Point(field.Axis.Start + along, across);
        }

        /// <summary>
        /// The cardboard rocket as a "Rocket" root (kinematic, so it can fly) with the model scaled to about 5 m tall
        /// and a capsule collider. It stands at the landing spot for now; the second step lifts it into the sky.
        /// Returns the rocket's footprint radius.
        /// </summary>
        static float BuildRocket(FieldLayout field, Vector3 landing)
        {
            var root = new GameObject("Rocket");
            root.transform.SetParent(field.Root, false);
            root.transform.position = landing;
            field.Rocket = root.transform;

            GameObject prefab = LoadPrefab(RocketPrefab);
            if (prefab == null) return 1.5f;

            GameObject model = Spawn(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;

            if (TryGetBounds(model, out Bounds measured) && measured.size.y > 0.01f)
                model.transform.localScale *= RocketHeight / measured.size.y;
            AlignToBounds(model, landing.x, landing.z, 0f, false);

            // The prefab's own mesh colliders are for something that never moves. One capsule does for a rocket that flies.
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            float radius = 1.5f;
            if (TryGetBounds(model, out Bounds bounds))
            {
                radius = Mathf.Max(0.8f, 0.4f * Mathf.Max(bounds.size.x, bounds.size.z));
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = bounds.size.y;
                capsule.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                radius = Mathf.Max(radius, 0.5f * Mathf.Max(bounds.size.x, bounds.size.z));
            }

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            field.RocketCreated = true;
            return radius;
        }

        /// <summary>
        /// The big tree. Its colliders are replaced by a thin trunk, so the kid can fly up through the crown and the tree
        /// house. The perch point (where the baby sits) is on the south, camera-facing side of the crown.
        /// </summary>
        static void BuildTree(FieldLayout field)
        {
            Vector3 trunk = FieldPoint(field, TreeAlong, TreeAcross);
            Vector3 perchGround = trunk + Vector3.back * PerchSouthOfTrunk;
            float perchHeight = PerchHeight;

            GameObject prefab = LoadPrefab(TreePrefab);
            if (prefab != null)
            {
                Transform group = NewGroup("Tree", field.Root);
                GameObject tree = Spawn(prefab, group);

                // The tree house faces the camera (south).
                tree.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                tree.transform.position = new Vector3(trunk.x, 0f, trunk.z);

                if (TryGetBounds(tree, out Bounds bounds))
                {
                    // Stand on the ground by its lowest point, with the trunk (the prefab's origin) at the wanted spot.
                    tree.transform.position += Vector3.up * -bounds.min.y;
                    TryGetBounds(tree, out bounds);

                    perchHeight = Mathf.Min(PerchHeight, bounds.size.y * 0.7f);
                    float south = trunk.z - bounds.min.z;
                    perchGround = trunk + Vector3.back * Mathf.Clamp(south * 0.5f, 0.8f, 1.6f);
                }

                foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                var trunkCollider = tree.AddComponent<CapsuleCollider>();
                trunkCollider.direction = 1;
                trunkCollider.radius = TrunkRadius;
                trunkCollider.height = 3f;
                trunkCollider.center = new Vector3(0f, 1.5f, 0f);

                FinishEnvironmentObject(tree);
                field.TreeCreated = true;
            }

            field.TreePerch = NewPoint("TreePerch", field.Root, perchGround + Vector3.up * perchHeight);
            // The baby faces the camera.
            field.TreePerch.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            field.PadPosition = perchGround + Vector3.back * PadSouthOfPerch;
        }

        /// <summary>Four points in an arc around the foot of the rocket, on its camera side (south).</summary>
        static Transform[] CreateSpawnPoints(FieldLayout field, Vector3 landing, float rocketRadius)
        {
            Transform group = NewGroup("SpawnPoints", field.Root);
            var points = new Transform[AlienCount];
            float distance = rocketRadius + 1.3f;
            for (int i = 0; i < points.Length; i++)
            {
                // Spread over 90 degrees, centred on south (toward the camera).
                float angle = Mathf.Lerp(-45f, 45f, i / (float)(points.Length - 1)) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * distance;
                points[i] = NewPoint($"AlienSpawn_{i + 1}", group, landing + offset);
            }
            return points;
        }

        /// <summary>A row of dance spots, one for each alien and one for the baby, facing the camera.</summary>
        static Transform[] CreatePartyPoints(FieldLayout field)
        {
            Transform group = NewGroup("PartyLine", field.Root);
            var points = new Transform[AlienCount + 1];
            for (int i = 0; i < points.Length; i++)
            {
                Transform point = NewPoint($"PartySpot_{i + 1}", group, FieldPoint(field, PartyAlongStart + i * PartySpacing, PartyAcross));
                point.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                points[i] = point;
            }
            return points;
        }

        // ----- Step 2: everything alive --------------------------------------------------------

        /// <summary>
        /// Creates the pad, the zone, the aliens and the baby, dresses the player up, and builds the AlienLanding.
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

            JumpPad pad = CreateJumpPad(field);
            AlienKidAI[] aliens = CreateAliens(field, inputs, slimeBall, slimeMarker, particleMaterial);
            RescueTarget baby = CreateBaby(field, inputs);
            RayGunShooter rayGun = DressUpPlayer(inputs, particleMaterial);
            ImaginationEffect imagination = CreateImagination(inputs.Imagination);

            WireLanding(landing, field, inputs, rayGun, imagination, pad, baby, aliens);

            Material ringMaterial = KnightsArenaBuilder.CreateGlowMaterial(ZoneRingMaterialPath, new Color(0.7f, 0.35f, 1f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("AlienZone", root, field.ZonePosition, ZoneRadius, landing,
                ringMaterial, AlienBeacon);
            AddToInviter(zone);

            Debug.Log($"CoolSchool alien field: zone {field.ZonePosition.ToString("0.0")}, rocket {field.RocketLanding.position.ToString("0.0")}, "
                + $"tree perch {field.TreePerch.position.ToString("0.0")}, jump pad {field.PadPosition.ToString("0.0")}, "
                + $"party line {field.PartyPoints[0].position.ToString("0.0")}..{field.PartyPoints[field.PartyPoints.Length - 1].position.ToString("0.0")}.");
            return landing;
        }

        static void SnapPointsToNavMesh(Transform[] points)
        {
            foreach (Transform point in points)
                if (point != null) point.position = SnapToNavMesh(point.position);
        }

        // ---- The jump pad ----

        /// <summary>A glowing disc with a pulsing ring and a soft purple light under the perch. Hidden until the rescue begins.</summary>
        static JumpPad CreateJumpPad(FieldLayout field)
        {
            var padObject = new GameObject("JumpPad");
            padObject.transform.SetParent(field.Root, false);
            padObject.transform.position = field.PadPosition;

            Transform visual = NewGroup("Visual", padObject.transform);

            Material discMaterial = KnightsArenaBuilder.CreateGlowMaterial(PadMaterialPath, new Color(0.85f, 0.3f, 1f), 0.6f);
            Material ringMaterial = KnightsArenaBuilder.CreateGlowMaterial(PadRingMaterialPath, new Color(1f, 0.7f, 1f), 0.35f);

            CreateFlatDisc("Disc", visual, PadRadius * 2f, 0.03f, discMaterial);
            Transform ring = CreateFlatDisc("Ring", visual, PadRadius * 2.6f, 0.04f, ringMaterial);

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(visual, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.85f, 0.4f, 1f);
            glow.range = 4f;
            glow.intensity = 2f;
            glow.shadows = LightShadows.None;

            var pad = padObject.AddComponent<JumpPad>();
            SetObject(pad, "visual", visual.gameObject);
            SetObject(pad, "ring", ring);
            SetFloat(pad, "radius", PadRadius);

            visual.gameObject.SetActive(false);
            return pad;
        }

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

        // ---- Aliens and the baby ----

        static AlienKidAI[] CreateAliens(FieldLayout field, AlienInputs inputs, Material slimeBall, Material slimeMarker, Material trail)
        {
            Transform group = NewGroup("Aliens", field.Root);
            var aliens = new List<AlienKidAI>();

            for (int i = 0; i < AlienPrefabs.Length; i++)
            {
                Vector3 position = SnapToNavMesh(field.SpawnPoints[i % field.SpawnPoints.Length].position);
                string kidName = AlienPrefabs[i].Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid($"{kidName}_{i + 1}", AlienPrefabs[i], inputs.KidController, position, out float height);
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
                SetFloat(hittable, "stunSeconds", 0.4f);

                var thrower = root.AddComponent<SlimeThrower>();
                SetObject(thrower, "hand", RightHandOf(root));
                SetObject(thrower, "slimeMaterial", slimeBall);
                SetObject(thrower, "markerMaterial", slimeMarker);
                SetObject(thrower, "trailMaterial", trail);

                aliens.Add(root.AddComponent<AlienKidAI>());

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
            }
            return aliens.ToArray();
        }

        /// <summary>The baby alien: a kid-rig character that is switched off, with its legs (the agent) off until it is down from the tree.</summary>
        static RescueTarget CreateBaby(FieldLayout field, AlienInputs inputs)
        {
            Transform group = field.Root.Find("Aliens");
            if (group == null) group = NewGroup("Aliens", field.Root);

            Vector3 position = SnapToNavMesh(field.SpawnPoints[0].position);
            GameObject root = CreateKid("Baby_Alien", BabyPrefab, inputs.KidController, position, out float height);
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

            var baby = root.AddComponent<RescueTarget>();

            // It sits in the tree, off the NavMesh, until it is rescued.
            agent.enabled = false;
            SetLayerRecursive(root, inputs.KidsLayer);
            root.SetActive(false);
            return baby;
        }

        static Transform RightHandOf(GameObject kidRoot)
        {
            Animator animator = kidRoot.GetComponentInChildren<Animator>();
            return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
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
            ImaginationEffect imagination, JumpPad pad, RescueTarget baby, AlienKidAI[] aliens)
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
            SetObject(landing, "babyAlien", baby);
            SetObject(landing, "treePerchPoint", field.TreePerch);
            SetObject(landing, "jumpPad", pad);
            SetObjectArray(landing, "partyLinePoints", field.PartyPoints);
            // Optional: the number cards are only there if the lesson was built.
            SetObject(landing, "lessonUI", Object.FindFirstObjectByType<LessonUI>());
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
