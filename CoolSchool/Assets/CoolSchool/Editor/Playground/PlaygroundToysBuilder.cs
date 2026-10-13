using System;
using System.Collections.Generic;
using CoolSchool.Playground;
using CoolSchool.Player;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Makes the playground toys playable (Docs/specs/playground-toys-contract.md). For each of the 12 Synty toys on the equipment ring it
    /// adds the PlaygroundToy, its touch zone and an anchors group, then calls the toy's own Configure method (Core.cs for the slide, the
    /// rockers, the roundabout and the tunnel; More.cs for the rest). It also puts ToyPlayer on the player, builds the sticker board and
    /// draws the sticker icons. Everything is logged with the "PlaygroundToys:" prefix.
    /// Called from PlaygroundPrototypeBuilder.RunBuild right after ToyRoomBuilder.Build. An error in here never stops the scene build.
    /// </summary>
    public static partial class PlaygroundToysBuilder
    {
        public sealed class ToyInputs
        {
            public PlayerController Player;
        }

        // The Configure methods. Each one finds the toy's anchors and moving parts from its geometry and calls s.SetRoutine<T>(exit).
        static partial void ConfigureSlide(ToySetup s);
        static partial void ConfigureRocker(ToySetup s);
        static partial void ConfigureRoundabout(ToySetup s);
        static partial void ConfigureTunnel(ToySetup s);
        static partial void ConfigureMonkeyBars(ToySetup s);
        static partial void ConfigureSeesaw(ToySetup s);
        static partial void ConfigureSwings(ToySetup s);
        static partial void ConfigureFort(ToySetup s);
        static partial void ConfigureTyreWall(ToySetup s);
        static partial void ConfigureTrackRide(ToySetup s);
        static partial void ConfigureShip(ToySetup s);

        const string LogPrefix = "PlaygroundToys: ";
        const float TouchMargin = 0.25f;
        const float TouchMinHeight = 2.2f;

        sealed class ToySpec
        {
            public string Prefab;
            public string StickerId;
            public string VoiceName;
            public Action<ToySetup> Configure;

            public ToySpec(string prefab, string stickerId, string voiceName, Action<ToySetup> configure)
            {
                Prefab = prefab;
                StickerId = stickerId;
                VoiceName = voiceName;
                Configure = configure;
            }
        }

        static readonly ToySpec[] Specs =
        {
            new ToySpec("SM_Prop_Playground_Slide_01", "slide", "slide", s => { ConfigureSlide(s); }),
            new ToySpec("SM_Prop_Playground_Rocker_01", "rocker_1", "rocker", s => { ConfigureRocker(s); }),
            new ToySpec("SM_Prop_Playground_Rocker_02", "rocker_2", "rocker", s => { ConfigureRocker(s); }),
            new ToySpec("SM_Prop_Playground_Roundabout_01", "roundabout", "roundabout", s => { ConfigureRoundabout(s); }),
            new ToySpec("SM_Prop_Playground_Tunnel_01", "tunnel", "tunnel", s => { ConfigureTunnel(s); }),
            new ToySpec("SM_Prop_Playground_Monkey_Bars_01", "monkey_bars", "monkey_bars", s => { ConfigureMonkeyBars(s); }),
            new ToySpec("SM_Prop_Playground_Seesaw_01", "seesaw", "seesaw", s => { ConfigureSeesaw(s); }),
            new ToySpec("SM_Prop_Playground_Swings_01", "swings", "swings", s => { ConfigureSwings(s); }),
            new ToySpec("SM_Prop_Playground_Fort_01", "fort", "fort", s => { ConfigureFort(s); }),
            new ToySpec("SM_Prop_Playground_Tyrewall_01", "tyre_wall", "tyre_wall", s => { ConfigureTyreWall(s); }),
            new ToySpec("SM_Prop_Playground_Track_Ride_01", "track_ride", "track_ride", s => { ConfigureTrackRide(s); }),
            new ToySpec("SM_Prop_Playground_Ship_01", "ship", "ship", s => { ConfigureShip(s); }),
        };

        /// <summary>Builds everything. Errors are logged, never thrown: the rest of the scene build must go on.</summary>
        public static void Build(ToyInputs inputs)
        {
            try
            {
                BuildAll(inputs);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "the toys build failed, the playground has no working toys: " + exception);
            }
        }

        static void BuildAll(ToyInputs inputs)
        {
            Physics.SyncTransforms();

            GameObject equipment = GameObject.Find("Environment/Equipment");
            if (equipment == null)
            {
                Debug.LogWarning(LogPrefix + "no Environment/Equipment group, so there are no toys.");
                return;
            }

            PlayerController player = inputs != null ? inputs.Player : null;
            ToyPoseOffsets offsets = ToyPoseOffsets.Defaults;
            if (player != null)
            {
                if (player.GetComponent<ToyPlayer>() == null) player.gameObject.AddComponent<ToyPlayer>();
                offsets = ToyPoseMeasure.Measure(player);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "no player given: no ToyPlayer is added and the pose offsets are the defaults.");
            }

            var icons = new Dictionary<string, Sprite>();
            int configured = 0;
            var children = new List<Transform>();
            foreach (Transform child in equipment.transform) children.Add(child);

            foreach (Transform child in children)
            {
                ToySpec spec = SpecFor(child.name);
                if (spec == null) continue;

                if (BuildToy(child.gameObject, spec, offsets, icons)) configured++;
            }

            Debug.Log($"{LogPrefix}{configured} of {Specs.Length} toys are playable.");

            BuildStickerBoard(player, icons);
        }

        static ToySpec SpecFor(string objectName)
        {
            foreach (ToySpec spec in Specs)
                if (objectName.StartsWith(spec.Prefab, StringComparison.Ordinal)) return spec;
            return null;
        }

        /// <summary>Prepares and configures one toy. Returns true when it ended up with a routine.</summary>
        static bool BuildToy(GameObject root, ToySpec spec, ToyPoseOffsets offsets, Dictionary<string, Sprite> icons)
        {
            ToySetup setup = null;
            try
            {
                setup = PrepareToy(root, spec, offsets, icons);
                if (setup == null) return false;

                setup.Log($"root {root.name} at {ToySetup.Fmt(root.transform.position)}, turned {root.transform.eulerAngles.y:0}°, "
                    + $"scale {ToySetup.Fmt(root.transform.lossyScale)}, bounds size {ToySetup.Fmt(setup.WorldBounds.size)} centre {ToySetup.Fmt(setup.WorldBounds.center)}");
                LogRenderers(setup);
                setup.LogHeightMap();

                spec.Configure(setup);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogPrefix}{spec.StickerId}: configure failed: {exception}");
            }
            finally
            {
                if (setup != null) setup.Finish();
            }

            if (setup == null) return false;

            if (setup.Toy.Routine == null)
            {
                setup.Warn("no routine was set, so this toy is left as plain scenery (the PlaygroundToy and its touch zone are removed).");
                Transform zone = root.transform.Find("TouchZone");
                if (zone != null) UnityEngine.Object.DestroyImmediate(zone.gameObject);
                UnityEngine.Object.DestroyImmediate(setup.Toy);
                return false;
            }
            return true;
        }

        /// <summary>Unpacks the prefab, gives it the toy component, the anchors group and the touch zone, and draws its sticker icon.</summary>
        static ToySetup PrepareToy(GameObject root, ToySpec spec, ToyPoseOffsets offsets, Dictionary<string, Sprite> icons)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            Sprite icon = ToyStickerIcons.GetOrRender(source, spec.StickerId);
            icons[spec.StickerId] = icon;

            // A prefab instance cannot lose components or move its own children: make it an ordinary object first.
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            MoveRootMeshToChild(root);

            if (!PlaygroundPrototypeBuilder.TryGetBounds(root, out Bounds bounds))
            {
                Debug.LogWarning($"{LogPrefix}{spec.StickerId}: {root.name} has no mesh, so there is no toy.");
                return null;
            }

            var toy = root.GetComponent<PlaygroundToy>();
            if (toy == null) toy = root.AddComponent<PlaygroundToy>();
            ToySetup.SetSerialized(toy, "stickerId", p => p.stringValue = spec.StickerId);
            ToySetup.SetSerialized(toy, "voiceName", p => p.stringValue = spec.VoiceName);
            PlaygroundPrototypeBuilder.SetObject(toy, "stickerIcon", icon);
            ToySetup.SetSerialized(toy, "poseOffsets", p =>
            {
                p.FindPropertyRelative("sitSeatHips").floatValue = offsets.sitSeatHips;
                p.FindPropertyRelative("sitSlideHips").floatValue = offsets.sitSlideHips;
                p.FindPropertyRelative("hangHands").floatValue = offsets.hangHands;
            });

            var anchors = new GameObject("ToyAnchors");
            anchors.transform.SetParent(root.transform, false);

            BuildTouchZone(root, toy);

            return new ToySetup
            {
                Root = root,
                Toy = toy,
                StickerId = spec.StickerId,
                WorldBounds = bounds,
                Anchors = anchors.transform,
                PoseOffsets = offsets,
            };
        }

        /// <summary>
        /// A toy whose mesh sits on the root object itself could not be turned or moved as a part. Its mesh, renderer and mesh collider move to
        /// a child object "Body" with the same pose, so every toy has its meshes on child objects.
        /// </summary>
        static void MoveRootMeshToChild(GameObject root)
        {
            var filter = root.GetComponent<MeshFilter>();
            var renderer = root.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null) return;

            var body = new GameObject("Body");
            body.layer = root.layer;
            body.transform.SetParent(root.transform, false);

            var bodyFilter = body.AddComponent<MeshFilter>();
            bodyFilter.sharedMesh = filter.sharedMesh;
            var bodyRenderer = body.AddComponent<MeshRenderer>();
            EditorUtility.CopySerialized(renderer, bodyRenderer);
            bodyRenderer.sharedMaterials = renderer.sharedMaterials;

            foreach (MeshCollider collider in root.GetComponents<MeshCollider>())
            {
                var bodyCollider = body.AddComponent<MeshCollider>();
                bodyCollider.sharedMesh = collider.sharedMesh;
                bodyCollider.convex = collider.convex;
                bodyCollider.isTrigger = collider.isTrigger;
                UnityEngine.Object.DestroyImmediate(collider);
            }

            UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(filter);
            Debug.Log($"{LogPrefix}{root.name}: the mesh was on the root object; moved to a child 'Body'.");
        }

        /// <summary>
        /// The touch zone: a trigger box around the toy's renderers, 0.25 m wider on each horizontal side, from the ground to 2.2 m (or the
        /// top of the toy if higher), worked out in the toy's own space so it follows the toy's turn.
        /// </summary>
        static void BuildTouchZone(GameObject root, PlaygroundToy toy)
        {
            Transform t = root.transform;
            bool found = false;
            Bounds local = default;
            var corners = new Vector3[8];
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;

                Bounds mesh = filter.sharedMesh.bounds;
                int i = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            corners[i++] = mesh.center + Vector3.Scale(mesh.extents, new Vector3(sx, sy, sz));

                foreach (Vector3 corner in corners)
                {
                    Vector3 inRoot = t.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found) { local = new Bounds(inRoot, Vector3.zero); found = true; }
                    else local.Encapsulate(inRoot);
                }
            }

            PlaygroundPrototypeBuilder.TryGetBounds(root, out Bounds world);
            Vector3 scale = t.lossyScale;
            float growX = TouchMargin / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float growZ = TouchMargin / Mathf.Max(0.0001f, Mathf.Abs(scale.z));

            // Height: from the foot of the toy to 2.2 m above it (or the top of the toy). The root only turns about the vertical axis,
            // so a world height maps to a local height through the root's position.
            float bottomWorld = world.min.y;
            float topWorld = Mathf.Max(world.max.y, world.min.y + TouchMinHeight);
            float bottomLocal = t.InverseTransformPoint(new Vector3(t.position.x, bottomWorld, t.position.z)).y;
            float topLocal = t.InverseTransformPoint(new Vector3(t.position.x, topWorld, t.position.z)).y;

            var zone = new GameObject("TouchZone");
            zone.transform.SetParent(t, false);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (found)
            {
                box.center = new Vector3(local.center.x, (bottomLocal + topLocal) * 0.5f, local.center.z);
                box.size = new Vector3(local.size.x + 2f * growX, Mathf.Abs(topLocal - bottomLocal), local.size.z + 2f * growZ);
            }
            else
            {
                box.center = new Vector3(0f, 1.1f, 0f);
                box.size = new Vector3(3f, 2.2f, 3f);
            }

            var touch = zone.AddComponent<ToyTouchZone>();
            PlaygroundPrototypeBuilder.SetObject(touch, "toy", toy);
        }

        static void LogRenderers(ToySetup setup)
        {
            var text = new System.Text.StringBuilder("renderers:");
            foreach (Renderer renderer in setup.Root.GetComponentsInChildren<Renderer>(false))
            {
                Bounds b = renderer.bounds;
                text.Append($"\n    {renderer.name} (parent {(renderer.transform.parent != null ? renderer.transform.parent.name : "none")}) "
                    + $"bounds centre {ToySetup.Fmt(b.center)} size {ToySetup.Fmt(b.size)} min y {b.min.y:0.00} max y {b.max.y:0.00}");
            }
            setup.Log(text.ToString());
        }
    }
}
