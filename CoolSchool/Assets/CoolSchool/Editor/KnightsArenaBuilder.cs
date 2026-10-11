using System.Collections.Generic;
using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Input;
using CoolSchool.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the knights arena at the far end of the playground (east, +X): a cardboard castle wall with a gate,
    /// imagination props, spawn/retreat/entry points, the glowing castle zone, the opponent pool,
    /// the dragon and the KnightsGame that ties it all together.
    /// The arena lies along an axis (a flat unit vector pointing from the playground into the arena): every position is
    /// worked out as a distance along that axis and a distance across it, so the arena can point any cardinal way.
    /// The castle faces back along the axis, toward the playground, and the dragon waits behind it.
    /// Two steps, because the NavMesh must be baked after the castle exists but before any character:
    /// BuildCastle (static geometry) and Build (everything alive).
    /// </summary>
    public static class KnightsArenaBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string MeshFolder = RootFolder + "/Meshes";
        const string ZoneRingMaterialPath = RootFolder + "/Materials/CastleZoneRing.mat";
        const string TagZoneRingMaterialPath = RootFolder + "/Materials/TagZoneRing.mat";
        const string FireWarningMaterialPath = RootFolder + "/Materials/FireWarning.mat";
        const string TelegraphMaterialPath = RootFolder + "/Materials/Telegraph.mat";
        const string FireWarningMeshPath = MeshFolder + "/FireWarningFan.asset";

        // Castle layout. CastleYaw is the extra turn the pieces need to face the playground when the arena lies along +Z
        // (the pieces face local +Z by default); other axes add their own angle on top.
        const int CastlePieceCount = 6;
        const float CastleYaw = 180f;
        const float GateWidth = 5f;
        const float PieceGap = 0.05f;
        const float CastleBackMargin = 10f;

        // Dragon.
        const float DragonHeight = 3.5f;
        const float FireRange = 5f;
        const float FireAngle = 50f;

        static readonly PrefabRef DragonPrefab = new PrefabRef("Dragon_LP", "Magic Pig");

        static readonly PrefabRef FlagA = Kids("SM_Prop_Flag_01");
        static readonly PrefabRef FlagB = Kids("SM_Prop_Flag_02");
        static readonly PrefabRef Torch = Kids("SM_Prop_Torch_01");

        static readonly PrefabRef[] OpponentPrefabs =
        {
            Kids("SM_Chr_Kid_Goblin_01"),
            Kids("SM_Chr_Kid_Goblin_02"),
            Kids("SM_Chr_Kid_Troll_01"),
            Kids("SM_Chr_Kid_Goblin_01"),
        };

        static readonly PrefabRef[] OpponentClubs =
        {
            Kids("SM_Wep_Goblin_Club_01"),
            Kids("SM_Wep_Goblin_Club_01"),
            Kids("SM_Wep_Club_Inflatable_01"),
            Kids("SM_Wep_Goblin_Club_01"),
        };

        /// <summary>
        /// Numbers from the castle step that the later steps need. Positions are worked out along the arena's axis
        /// (from the playground into the arena) and across it; Point turns a pair of them into a world position.
        /// </summary>
        internal sealed class ArenaLayout
        {
            public Transform Root;

            /// <summary>Flat unit vector from the playground into the arena (a cardinal direction).</summary>
            public readonly Vector3 Axis;

            /// <summary>Flat unit vector across the arena: to the right when looking along the axis.</summary>
            public readonly Vector3 Lateral;

            /// <summary>Where the arena begins and ends, as distances along the axis from the world origin.</summary>
            public float Start;
            public float End;

            /// <summary>The castle wall's playground-side face and its back, as distances along the axis.</summary>
            public float CastleFront;
            public float CastleBack;

            public readonly List<GameObject> ImaginationProps = new List<GameObject>();

            public ArenaLayout(Vector3 axis)
            {
                axis.y = 0f;
                if (axis.sqrMagnitude < 0.0001f) axis = Vector3.forward;
                Axis = Mathf.Abs(axis.x) > Mathf.Abs(axis.z)
                    ? new Vector3(Mathf.Sign(axis.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(axis.z));
                Lateral = Vector3.Cross(Vector3.up, Axis);
            }

            /// <summary>The yaw (degrees) that turns a thing facing +Z to face along the axis.</summary>
            public float AxisYaw => Mathf.Atan2(Axis.x, Axis.z) * Mathf.Rad2Deg;

            /// <summary>The world position that is "along" metres along the axis and "across" metres to the right of it (y = 0).</summary>
            public Vector3 Point(float along, float across)
            {
                return Axis * along + Lateral * across;
            }

            /// <summary>How wide the bounds are across the arena.</summary>
            public float AcrossSize(Bounds bounds)
            {
                return Mathf.Abs(Lateral.x) * bounds.size.x + Mathf.Abs(Lateral.z) * bounds.size.z;
            }

            /// <summary>The nearest and farthest extent of the bounds along the axis.</summary>
            public void AlongRange(Bounds bounds, out float near, out float far)
            {
                float centre = Vector3.Dot(bounds.center, Axis);
                float half = Mathf.Abs(Axis.x) * bounds.extents.x + Mathf.Abs(Axis.z) * bounds.extents.z;
                near = centre - half;
                far = centre + half;
            }
        }

        /// <summary>What the second step wires into the KnightsGame.</summary>
        internal sealed class ArenaInputs
        {
            public RuntimeAnimatorController KidController;
            /// <summary>KidClub override (mace swings) for the opponents; null falls back to KidController.</summary>
            public RuntimeAnimatorController OpponentController;
            public RuntimeAnimatorController DragonController;
            public int KidsLayer = -1;
            public PlayerController Player;
            public PlayerBattleInput PlayerInput;
            public ActionButton ActionButton;
            public Sprite SwordIcon;
            public ImaginationEffect Imagination;
            public KnightAllyAI[] Allies;
            public WeaponHolder[] HeroWeaponHolders;
            public ParticleSystem FireConfettiPrefab;
        }

        internal sealed class ArenaResult
        {
            public bool DragonCreated;
            public bool DragonControllerMissing;
        }

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The cardboard castle as a wall across the far end of the arena with a gate gap in the middle,
        /// plus the inactive imagination props (flags on top, torches at the gate).
        /// axis points from the playground into the arena (flat; snapped to the nearest cardinal direction);
        /// startDistance and endDistance are where the arena begins and ends along it, measured from the world origin.
        /// </summary>
        internal static ArenaLayout BuildCastle(Transform environmentRoot, Vector3 axis, float startDistance, float endDistance)
        {
            var layout = new ArenaLayout(axis) { Start = startDistance, End = endDistance };
            float castleDistance = endDistance - CastleBackMargin;
            layout.CastleFront = castleDistance;
            layout.CastleBack = castleDistance;

            layout.Root = NewGroup("Arena", environmentRoot);
            Transform castleGroup = NewGroup("Castle", layout.Root);
            Transform propsGroup = NewGroup("ImaginationProps", layout.Root);

            // The pieces face the playground (back along the axis).
            Quaternion castleRotation = Quaternion.Euler(0f, CastleYaw + layout.AxisYaw, 0f);

            var pieces = new List<GameObject>();
            var widths = new List<float>();
            for (int i = 1; i <= CastlePieceCount; i++)
            {
                GameObject prefab = LoadPrefab(Kids($"SM_Prop_Cardboard_Castle_Piece_{i:00}"));
                if (prefab == null) continue;

                GameObject piece = Spawn(prefab, castleGroup);
                piece.transform.rotation = castleRotation;
                pieces.Add(piece);
                widths.Add(TryGetBounds(piece, out Bounds bounds) ? layout.AcrossSize(bounds) : 2f);
            }

            if (pieces.Count == 0) return layout;

            // Half the pieces on each side of the gate, in order outwards.
            int leftCount = pieces.Count / 2;
            float leftTotal = 0f;
            for (int i = 0; i < leftCount; i++) leftTotal += widths[i] + PieceGap;

            float cursor = -GateWidth * 0.5f - leftTotal;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i == leftCount) cursor = GateWidth * 0.5f;

                Vector3 spot = layout.Point(castleDistance, cursor + widths[i] * 0.5f);
                AlignToBounds(pieces[i], spot.x, spot.z, 0f, false);
                FinishEnvironmentObject(pieces[i]);
                cursor += widths[i] + PieceGap;
            }

            // The wall's real front (playground side) and back, measured after placement.
            float front = float.MaxValue;
            float back = float.MinValue;
            foreach (GameObject piece in pieces)
            {
                if (!TryGetBounds(piece, out Bounds bounds)) continue;
                layout.AlongRange(bounds, out float near, out float far);
                front = Mathf.Min(front, near);
                back = Mathf.Max(back, far);
            }
            if (front < back)
            {
                layout.CastleFront = front;
                layout.CastleBack = back;
            }

            PlaceImaginationProps(layout, propsGroup, pieces);
            return layout;
        }

        /// <summary>Flags on the two outer pieces, a torch on each side of the gate. All start hidden.</summary>
        static void PlaceImaginationProps(ArenaLayout layout, Transform parent, List<GameObject> pieces)
        {
            GameObject flagA = LoadPrefab(FlagA);
            GameObject flagB = LoadPrefab(FlagB);
            GameObject torch = LoadPrefab(Torch);

            // The props were made facing +Z; turn them with the arena.
            Quaternion turn = Quaternion.Euler(0f, layout.AxisYaw, 0f);

            void PlaceFlag(GameObject prefab, GameObject piece)
            {
                if (prefab == null || !TryGetBounds(piece, out Bounds bounds)) return;

                GameObject flag = Spawn(prefab, parent);
                flag.transform.rotation = turn;
                AlignToBounds(flag, bounds.center.x, bounds.center.z, bounds.max.y, false);
                HideProp(layout, flag);
            }

            PlaceFlag(flagA, pieces[0]);
            if (pieces.Count > 1) PlaceFlag(flagB, pieces[pieces.Count - 1]);

            if (torch == null) return;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject item = Spawn(torch, parent);
                item.transform.rotation = turn;
                Vector3 spot = layout.Point(layout.CastleFront - 0.5f, side * (GateWidth * 0.5f + 0.6f));
                AlignToBounds(item, spot.x, spot.z, 0f, false);
                HideProp(layout, item);
            }
        }

        /// <summary>Props are decoration only: no colliders, hidden until the imagination effect shows them.</summary>
        static void HideProp(ArenaLayout layout, GameObject prop)
        {
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            prop.SetActive(false);
            layout.ImaginationProps.Add(prop);
        }

        // ----- Step 2: everything alive --------------------------------------------------------

        /// <summary>
        /// Creates the points, the castle zone, the opponent pool, the dragon and the KnightsGame.
        /// Call after the NavMesh is baked and after the player and allies exist.
        /// </summary>
        internal static ArenaResult Build(ArenaLayout layout, ArenaInputs inputs)
        {
            var result = new ArenaResult();
            Transform arena = layout.Root != null ? layout.Root : new GameObject("Arena").transform;
            Transform points = NewGroup("Points", arena);

            // Spawn points just inside the gate, retreat and dragon entry behind the castle (further along the axis).
            float spawnAlong = layout.CastleBack + 1.2f;
            var spawns = new Transform[4];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = NewPoint($"OpponentSpawn_{i + 1}", points, layout.Point(spawnAlong, -1.5f + i));

            float retreatAlong = Mathf.Min(layout.CastleBack + 4f, layout.End - 1.5f);
            float entryAlong = Mathf.Min(layout.CastleBack + 6.5f, layout.End - 0.8f);
            Transform retreat = NewPoint("RetreatPoint", points, layout.Point(retreatAlong, 0f));
            Transform entry = NewPoint("DragonEntry", points, layout.Point(entryAlong, 0f));
            float centerAlong = Mathf.Lerp(layout.Start, layout.CastleFront, 0.55f);
            Transform center = NewPoint("ArenaCenter", points, layout.Point(centerAlong, 0f));

            var game = new GameObject("KnightsGame").AddComponent<KnightsGame>();
            game.transform.SetParent(arena, false);

            // In front of the castle, on the playground side: the first thing the kid walks into when coming from the playground.
            Material ringMaterial = CreateGlowMaterial(ZoneRingMaterialPath, new Color(0.35f, 0.55f, 1f), 0.4f);
            ActivityZone castleZone = CreateActivityZone("CastleZone", arena, layout.Point(layout.Start + 3.5f, 0f), 1.8f, game, ringMaterial,
                ActivityBeaconBuilder.KnightsBeacon);

            BattleOpponentAI[] opponents = CreateOpponents(arena, spawns, inputs, out WeaponHolder[] opponentHolders);

            DragonBoss dragon = CreateDragon(arena, entry, -layout.Axis, inputs, result);

            WireGame(game, layout, inputs, spawns, retreat, entry, center, opponents, dragon,
                inputs.HeroWeaponHolders.Concat(opponentHolders).ToArray());

            Debug.Log($"CoolSchool arena: centre {center.position.ToString("0.00")}, castle zone {castleZone.transform.position.ToString("0.00")}, "
                + $"castle at {layout.Point(layout.CastleFront, 0f).ToString("0.00")}..{layout.Point(layout.CastleBack, 0f).ToString("0.00")} "
                + $"facing {(-layout.Axis).ToString("0.0")} (toward the playground), arena from {layout.Point(layout.Start, 0f).ToString("0.0")} "
                + $"to {layout.Point(layout.End, 0f).ToString("0.0")} along {layout.Axis.ToString("0.0")}, dragon enters at {entry.position.ToString("0.00")} "
                + $"facing {(-layout.Axis).ToString("0.0")}.");
            return result;
        }

        static Transform NewPoint(string name, Transform parent, Vector3 position)
        {
            Transform point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        static void WireGame(KnightsGame game, ArenaLayout layout, ArenaInputs inputs, Transform[] spawns, Transform retreat,
            Transform entry, Transform center, BattleOpponentAI[] opponents, DragonBoss dragon, WeaponHolder[] holders)
        {
            SetObject(game, "player", inputs.Player);
            SetObject(game, "playerInput", inputs.PlayerInput);
            SetObject(game, "actionButton", inputs.ActionButton);
            SetObject(game, "swordIcon", inputs.SwordIcon);
            SetObject(game, "imagination", inputs.Imagination);
            SetObjectArray(game, "opponentSpawnPoints", spawns);
            SetObject(game, "retreatPoint", retreat);
            SetObjectArray(game, "opponents", opponents);
            SetIntArray(game, "waveSizes", new[] { 2, 3 });
            SetObject(game, "dragon", dragon);
            SetObject(game, "dragonEntry", entry);
            SetObject(game, "arenaCenter", center);
            SetObjectArray(game, "allies", inputs.Allies);
            SetObjectArray(game, "weaponHolders", holders);
            SetFloat(game, "victorySeconds", 4f);
        }

        // ----- Zone ----------------------------------------------------------------------------

        /// <summary>
        /// An ActivityZone whose visual holds a glowing flat disc (no collider) and the beacon that shows it from afar.
        /// </summary>
        internal static ActivityZone CreateActivityZone(string name, Transform parent, Vector3 position, float radius,
            Activity activity, Material ringMaterial, ActivityBeaconBuilder.Style beacon)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;

            // The ring is squashed flat, so the beacon cannot be its child: both go under one unscaled visual.
            Transform visual = NewGroup("Visual", root.transform);

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GlowRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(visual, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var renderer = ring.GetComponent<MeshRenderer>();
            if (ringMaterial != null) renderer.sharedMaterial = ringMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var zone = root.AddComponent<ActivityZone>();
            SetObject(zone, "activity", activity);
            SetFloat(zone, "radius", radius);
            ActivityBeaconBuilder.Build(visual, beacon);
            SetObject(zone, "visual", visual.gameObject);
            return zone;
        }

        /// <summary>The ring material for the tag zone: a warm yellow-green glow.</summary>
        internal static Material CreateTagZoneMaterial()
        {
            return CreateGlowMaterial(TagZoneRingMaterialPath, new Color(1f, 0.85f, 0.15f), 0.4f);
        }

        /// <summary>The ring material for the classroom zone: a warm green glow.</summary>
        internal static Material CreateLessonZoneMaterial()
        {
            return CreateGlowMaterial(RootFolder + "/Materials/LessonZoneRing.mat", new Color(0.4f, 0.9f, 0.45f), 0.4f);
        }

        // ----- Opponents -----------------------------------------------------------------------

        /// <summary>Four inactive goblin/troll kids with a club each, ready for KnightsGame to wake up in waves.</summary>
        static BattleOpponentAI[] CreateOpponents(Transform arena, Transform[] spawns, ArenaInputs inputs, out WeaponHolder[] holders)
        {
            Transform group = NewGroup("Opponents", arena);
            Material telegraphMaterial = CreateEmissiveMaterial(TelegraphMaterialPath, new Color(1f, 0.35f, 0.1f));
            var opponents = new List<BattleOpponentAI>();
            var holderList = new List<WeaponHolder>();

            for (int i = 0; i < OpponentPrefabs.Length; i++)
            {
                Vector3 position = SnapToNavMesh(spawns[i % spawns.Length].position);
                string kidName = OpponentPrefabs[i].Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid("Opponent_" + kidName + "_" + (i + 1), OpponentPrefabs[i],
                    inputs.OpponentController != null ? inputs.OpponentController : inputs.KidController,
                    position, out float height);
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
                SetEnum(hittable, "team", (int)BattleTeam.Opponents);
                SetInt(hittable, "hitsToDefeat", 3);
                SetBool(hittable, "canBeDefeated", true);

                root.AddComponent<SwordFighter>();

                var ai = root.AddComponent<BattleOpponentAI>();
                SetObject(ai, "telegraph", CreateTelegraph(root.transform, height, telegraphMaterial));

                // The club goes in the sword slot; there is no shield.
                holderList.Add(AddWeaponHolder(root, OpponentClubs[i], default));

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
                opponents.Add(ai);
            }

            holders = holderList.Where(h => h != null).ToArray();
            return opponents.ToArray();
        }

        /// <summary>A little "!" (bar plus dot) above the head, hidden until the wind-up.</summary>
        static GameObject CreateTelegraph(Transform kid, float height, Material material)
        {
            var root = new GameObject("Telegraph");
            root.transform.SetParent(kid, false);
            root.transform.localPosition = new Vector3(0f, height + 0.45f, 0f);

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar";
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            bar.transform.localScale = new Vector3(0.08f, 0.22f, 0.08f);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Dot";
            dot.transform.SetParent(root.transform, false);
            dot.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            dot.transform.localScale = Vector3.one * 0.1f;

            foreach (GameObject part in new[] { bar, dot })
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
                var renderer = part.GetComponent<MeshRenderer>();
                if (material != null) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            root.SetActive(false);
            return root;
        }

        // ----- Dragon --------------------------------------------------------------------------

        /// <summary>
        /// A "Dragon" root (Hittable + DragonBoss, scale 1) with the Dragon_LP model as its child, scaled to
        /// about 3.5 m tall. The fire origin is estimated at the mouth; the whole thing starts inactive.
        /// </summary>
        static DragonBoss CreateDragon(Transform arena, Transform entry, Vector3 facing, ArenaInputs inputs, ArenaResult result)
        {
            GameObject prefab = LoadPrefab(DragonPrefab);
            if (prefab == null) return null;

            var root = new GameObject("Dragon");
            root.transform.SetParent(arena, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            StripForeignScripts(model);

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (inputs.DragonController != null) animator.runtimeAnimatorController = inputs.DragonController;
            }
            result.DragonControllerMissing = inputs.DragonController == null;

            Transform mouthBone = FindMouthBone(model);
            FaceModelForward(model, mouthBone);

            if (TryGetBounds(model, out Bounds measured) && measured.size.y > 0.01f)
                model.transform.localScale *= DragonHeight / measured.size.y;
            AlignToBounds(model, 0f, 0f, 0f, false);

            Vector3 fireWorld = EstimateMouth(model, mouthBone);
            Transform fireOrigin = NewPoint("FireOrigin", root.transform, fireWorld);

            ParticleSystem fire = null;
            if (inputs.FireConfettiPrefab != null)
            {
                var fireObject = (GameObject)PrefabUtility.InstantiatePrefab(inputs.FireConfettiPrefab.gameObject, fireOrigin);
                fireObject.transform.localPosition = Vector3.zero;
                fireObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                fire = fireObject.GetComponent<ParticleSystem>();
            }

            GameObject warning = CreateFireWarning(root.transform, fireOrigin.localPosition);
            Transform aim = NewPoint("AimPoint", root.transform, new Vector3(0f, 1.4f, 0f));

            var hittable = root.AddComponent<Hittable>();
            SetEnum(hittable, "team", (int)BattleTeam.Opponents);
            SetInt(hittable, "hitsToDefeat", 8);
            SetBool(hittable, "canBeDefeated", true);
            SetFloat(hittable, "stunSeconds", 0.8f);
            SetFloat(hittable, "hitRadius", 1.8f);
            SetObject(hittable, "aimPoint", aim);

            var boss = root.AddComponent<DragonBoss>();
            SetObject(boss, "animator", animator);
            SetObject(boss, "fireOrigin", fireOrigin);
            SetObject(boss, "fireConfetti", fire);
            SetObject(boss, "fireWarning", warning);
            SetFloat(boss, "fireRange", FireRange);
            SetFloat(boss, "fireAngle", FireAngle);

            // Waiting behind the castle, facing the arena (back along the arena axis, toward the playground).
            root.transform.SetPositionAndRotation(entry.position, Quaternion.LookRotation(facing, Vector3.up));
            SetLayerRecursive(root, inputs.KidsLayer);
            root.SetActive(false);

            result.DragonCreated = true;
            return boss;
        }

        /// <summary>
        /// Removes the vendor's demo scripts (they would fight DragonBoss) and any missing-script components from
        /// the model and all its children. MonoBehaviours in a CoolSchool namespace and all other components stay.
        /// </summary>
        static void StripForeignScripts(GameObject model)
        {
            // Components of a prefab instance can't be removed reliably, so unpack it first.
            if (PrefabUtility.IsPartOfPrefabInstance(model))
            {
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            int removed = 0;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                foreach (MonoBehaviour behaviour in t.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;

                    string ns = behaviour.GetType().Namespace;
                    if (ns != null && ns.StartsWith("CoolSchool")) continue;

                    Object.DestroyImmediate(behaviour);
                    removed++;
                }
            }

            Debug.Log($"CoolSchool: removed {removed} vendor script component(s) from the dragon model.");
        }

        /// <summary>The jaw bone if the model has one, else the head bone (mesh nodes ending in _LP are skipped).</summary>
        static Transform FindMouthBone(GameObject model)
        {
            Transform[] all = model.GetComponentsInChildren<Transform>(true)
                .Where(t => !t.name.EndsWith("_LP") && t.GetComponent<Renderer>() == null).ToArray();
            return all.FirstOrDefault(t => t.name.Contains("Jaw"))
                ?? all.FirstOrDefault(t => t.name.EndsWith("Head"));
        }

        /// <summary>If the head sits behind the middle of the model, the model faces -Z: turn it half a circle.</summary>
        static void FaceModelForward(GameObject model, Transform mouthBone)
        {
            if (mouthBone == null || !TryGetBounds(model, out Bounds bounds)) return;

            float offset = mouthBone.position.z - bounds.center.z;
            if (offset < -0.1f * bounds.size.z)
            {
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("CoolSchool: the dragon model faced -Z; turned it around.");
            }
        }

        /// <summary>The mouth in world space: the jaw bone nudged forward, or the front-top of the bounds as a fallback.</summary>
        static Vector3 EstimateMouth(GameObject model, Transform mouthBone)
        {
            if (!TryGetBounds(model, out Bounds bounds)) return new Vector3(0f, DragonHeight * 0.6f, 1.5f);

            if (mouthBone != null)
                return mouthBone.position + Vector3.forward * 0.3f;

            return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.7f, bounds.max.z - 0.2f);
        }

        /// <summary>A flat translucent orange fan on the ground in front of the dragon, shown during the telegraph.</summary>
        static GameObject CreateFireWarning(Transform dragon, Vector3 originLocal)
        {
            var go = new GameObject("FireWarning");
            go.transform.SetParent(dragon, false);
            go.transform.localPosition = new Vector3(originLocal.x, 0.05f, originLocal.z);

            go.AddComponent<MeshFilter>().sharedMesh = CreateFanMesh(FireRange, FireAngle);
            var renderer = go.AddComponent<MeshRenderer>();
            Material material = CreateGlowMaterial(FireWarningMaterialPath, new Color(1f, 0.45f, 0.1f), 0.4f);
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go;
        }

        /// <summary>A flat pie slice pointing along +Z, wound so it faces up.</summary>
        static Mesh CreateFanMesh(float range, float angleDegrees)
        {
            const int segments = 16;
            var vertices = new Vector3[segments + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-angleDegrees * 0.5f, angleDegrees * 0.5f, i / (float)segments);
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * range, 0f, Mathf.Cos(angle) * range);
            }

            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = "FireWarningFan", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();

            EnsureFolder(RootFolder, "Meshes");
            AssetDatabase.DeleteAsset(FireWarningMeshPath);
            AssetDatabase.CreateAsset(mesh, FireWarningMeshPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(FireWarningMeshPath);
        }

        // ----- Materials -----------------------------------------------------------------------

        /// <summary>A transparent, emissive URP/Lit material for glowing floor discs.</summary>
        static Material CreateGlowMaterial(string path, Color color, float alpha)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var material = new Material(lit) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BattleEffectsBuilder.SetAlphaBlended(material);

            return SaveMaterial(material, path);
        }

        /// <summary>An opaque, glowing URP/Lit material.</summary>
        static Material CreateEmissiveMaterial(string path, Color color)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var material = new Material(lit) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.3f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            return SaveMaterial(material, path);
        }

        static Material SaveMaterial(Material material, string path)
        {
            EnsureFolder(RootFolder, "Materials");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
