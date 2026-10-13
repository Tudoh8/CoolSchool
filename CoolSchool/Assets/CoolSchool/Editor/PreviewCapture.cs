using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.CameraRig;
using CoolSchool.Characters;
using CoolSchool.Pirates;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// A way to SEE the world without playing: builds the playground, then (without saving) stages groups of shots and renders
    /// each shot to a 1280x720 PNG in Logs/preview/. Weapons on kids (rest pose, idle, attack, block), a knight ally, a goblin,
    /// a skeleton, ground-level contact shots and the seated ship and boat crews.
    ///
    /// To add a shot, add a Shot to an existing group in BuildGroups() (a camera framing: Orbit / Skim / LookAt). To add a
    /// scene to stage, add a Group with a Stage action (it runs after the saved scene was reloaded, so groups never affect
    /// each other) and its shots.
    ///
    /// Run: Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.PreviewCapture.BuildAndCaptureFromCommandLine
    /// Faster, without rebuilding the scene (renders the scene as last saved): ...PreviewCapture.CaptureFromCommandLine
    /// Only some shots: add -ExtraArgs '-previewShots','sword,shield' (a shot or group name containing one of the words).
    /// Log lines start with "PreviewCapture:".
    /// </summary>
    public static partial class PreviewCapture
    {
        const string LogPrefix = "PreviewCapture: ";
        const string ScenePath = "Assets/CoolSchool/Scenes/Playground.unity";
        static readonly Vector2Int DefaultSize = new Vector2Int(1280, 720);

        // The repository's Logs folder (the Unity project is one folder down from the repository root).
        static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Logs/preview"));

        sealed class Shot
        {
            public string Name;
            /// <summary>Moves the camera (and may step the animation on) right before the picture is taken.</summary>
            public Action Frame;
            public Vector2Int Size = DefaultSize;
        }

        sealed class Group
        {
            public string Name;
            public Action Stage;
            public readonly List<Shot> Shots = new List<Shot>();
        }

        static Camera cam;
        static string sceneToReload;

        // ----- Entry points ---------------------------------------------------------------------

        /// <summary>Builds and saves the playground like CommandLineBuild, checks the grounding, then renders the previews. Exits 0 on success, 1 on failure.</summary>
        public static void BuildAndCaptureFromCommandLine()
        {
            Run(true);
        }

        /// <summary>Renders the previews from the scene as last saved (no rebuild). Exits 0 on success, 1 on failure.</summary>
        public static void CaptureFromCommandLine()
        {
            Run(false);
        }

        static void Run(bool build)
        {
            int code = 1;
            try
            {
                if (build)
                {
                    if (!PlaygroundPrototypeBuilder.RunBuild())
                    {
                        Debug.LogError(LogPrefix + "the playground build was cancelled.");
                        EditorApplication.Exit(1);
                        return;
                    }
                    AssetDatabase.SaveAssets();
                    Debug.Log(LogPrefix + "playground built and saved; staging the previews (not saved).");
                }
                else
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }

                sceneToReload = EditorSceneManager.GetActiveScene().path;
                if (string.IsNullOrEmpty(sceneToReload)) sceneToReload = ScenePath;

                SceneGroundCheck.Run();
                LogWeaponShapes();
                Capture();
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        static void Capture()
        {
            Directory.CreateDirectory(OutputFolder);
            string[] filter = ReadFilter();

            int taken = 0;
            foreach (Group group in BuildGroups())
            {
                List<Shot> shots = group.Shots.Where(s => Wanted(filter, group.Name, s.Name)).ToList();
                if (shots.Count == 0) continue;

                try
                {
                    ReloadScene();
                    group.Stage?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogPrefix}group {group.Name}: staging failed: {e}");
                    continue;
                }

                foreach (Shot shot in shots)
                {
                    try
                    {
                        shot.Frame?.Invoke();
                        RenderAndSave(shot.Size, group.Name + "_" + shot.Name);
                        taken++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"{LogPrefix}shot {group.Name}_{shot.Name} failed: {e}");
                    }
                }
            }

            // Throw the staging away: the saved scene is the plain build.
            ReloadScene();
            Debug.Log($"{LogPrefix}{taken} pictures are in {OutputFolder}");
        }

        /// <summary>The words after -previewShots (comma separated), or none for "everything".</summary>
        static string[] ReadFilter()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-previewShots")
                    return args[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return Array.Empty<string>();
        }

        static bool Wanted(string[] filter, string group, string shot)
        {
            if (filter.Length == 0) return true;
            string full = group + "_" + shot;
            return filter.Any(word => full.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ----- The shot list --------------------------------------------------------------------

        static List<Group> BuildGroups()
        {
            var groups = new List<Group>();

            // --- A kid with sword and shield: resting, idling, swinging, blocking. -------------------
            // Each pose is its own group (one staging each). The weapons are put on while the kid is still in the rest pose.
            groups.Add(KidGroup("player_rest", () => FindKid("Player"), Pose.Rest,
                Views("front", 0f), Views("three_quarter", 45f), Views("side_right", 90f), Views("side_left", -90f), Views("back", 180f),
                HandView("sword_hand", HumanBodyBones.RightHand, 35f, 0.9f), HandView("shield_arm", HumanBodyBones.LeftLowerArm, -35f, 1.1f)));
            groups.Add(KidGroup("player_idle", () => FindKid("Player"), Pose.Idle,
                Views("front", 0f), Views("three_quarter", 45f), Views("side_right", 90f), Views("side_left", -90f),
                HandView("sword_hand", HumanBodyBones.RightHand, 35f, 0.9f)));
            groups.Add(KidGroup("player_attack", () => FindKid("Player"), Pose.Attack,
                Views("a_wind_up", 45f, 0.15f), Views("b_swing", 45f, 0.2f), Views("c_follow", 45f, 0.2f), Views("d_side", 90f, 0f)));
            groups.Add(KidGroup("player_block", () => FindKid("Player"), Pose.Block,
                Views("front", 0f), Views("three_quarter", 45f), Views("side_left", -90f)));

            // --- Other characters with weapons. -------------------------------------------------------
            groups.Add(KidGroup("ally", () => FindKid("Kid_Hoodie_02"), Pose.Rest,
                Views("three_quarter", 45f), Views("side_right", 90f)));
            groups.Add(KidGroup("ally_idle", () => FindKid("Kid_Hoodie_02"), Pose.Idle, Views("three_quarter", 45f)));
            groups.Add(KidGroup("goblin", () => FindKid("Opponent_", true), Pose.Rest,
                Views("three_quarter", 45f), Views("side_right", 90f)));
            groups.Add(KidGroup("skeleton", () => FindKid("Skeleton_1"), Pose.Rest,
                Views("three_quarter", 45f), Views("side_right", 90f), Views("side_left", -90f)));
            groups.Add(KidGroup("skeleton_idle", () => FindKid("Skeleton_1"), Pose.Idle, Views("three_quarter", 45f)));

            // --- Ground level: does it touch the grass? ----------------------------------------------
            groups.Add(SkimGroup("ground_fence", () => FindEnvironmentPiece("Environment/Fence", "Park_Fence", new Vector3(0f, 0f, -15f)),
                ("front", 0f), ("angle", 55f)));
            groups.Add(SkimGroup("ground_school_wall", () => FindWallPiece(), ("front", 0f), ("angle", 55f)));
            groups.Add(SkimGroup("ground_bench", () => FindEnvironmentPiece("Environment/Props", "Park_Seat", new Vector3(0f, 0f, -15f)),
                ("front", 0f), ("angle", 55f)));
            groups.Add(SkimGroup("ground_equipment", () => FindEnvironmentPiece("Environment/Equipment", "Playground", new Vector3(0f, 0f, -15f)),
                ("front", 0f), ("angle", 55f)));
            groups.Add(SkimGroup("ground_tree", () => FindEnvironmentPiece("Environment/Greenery", "Tree", new Vector3(0f, 0f, -15f)),
                ("front", 0f)));

            // --- Seated on the pirate ship and on the skeleton boat. ---------------------------------
            var helm = new Group { Name = "ship_helm", Stage = StageShipHelm };
            helm.Shots.Add(new Shot { Name = "front", Frame = () => SeatView(ShipHelmSeat(), 0f, 4.0f) });
            helm.Shots.Add(new Shot { Name = "three_quarter", Frame = () => SeatView(ShipHelmSeat(), 40f, 4.4f) });
            helm.Shots.Add(new Shot { Name = "side", Frame = () => SeatView(ShipHelmSeat(), 90f, 4.4f) });
            helm.Shots.Add(new Shot { Name = "crew", Frame = () => SeatView(ShipCrewSeat(0), 30f, 4.6f) });
            helm.Shots.Add(new Shot { Name = "wide", Frame = ShipWide });
            groups.Add(helm);

            var boat = new Group { Name = "skeleton_boat", Stage = StageSkeletonBoat };
            boat.Shots.Add(new Shot { Name = "three_quarter", Frame = () => BoatView(35f, 9f, 30f) });
            boat.Shots.Add(new Shot { Name = "side", Frame = () => BoatView(90f, 9f, 18f) });
            boat.Shots.Add(new Shot { Name = "close", Frame = () => BoatView(35f, 5.5f, 55f) });
            groups.Add(boat);


            // --- The pirate cove: sandpit edge and dock gap, island with its landmarks, the trophy chest, a top view. ---
            var cove = new Group { Name = "cove" };
            cove.Shots.Add(new Shot { Name = "pit_edge", Frame = () => CoveView("TrophyChest", new Vector3(5f, 0f, 3f), new Vector3(0f, 6f, -9f), 45f) });
            cove.Shots.Add(new Shot { Name = "pit_south_west", Frame = () => CoveView("TrophyChest", new Vector3(-2f, 0f, 3f), new Vector3(-9f, 5f, -4f), 45f) });
            cove.Shots.Add(new Shot { Name = "island", Frame = () => CoveView("Island", Vector3.zero, new Vector3(0f, 7f, -14f), 45f) });
            cove.Shots.Add(new Shot { Name = "landmarks", Frame = () => CoveView("Landmarks", new Vector3(0f, 0f, 1f), new Vector3(0f, 4.5f, -8f), 50f) });
            cove.Shots.Add(new Shot { Name = "trophy", Frame = () => CoveView("TrophyChest", Vector3.zero, new Vector3(3f, 2.2f, -4f), 40f) });
            cove.Shots.Add(new Shot { Name = "top", Frame = () => CoveView("Sandpit", new Vector3(0f, 0f, 14f), new Vector3(0f, 52f, -6f), 60f) });
            groups.Add(cove);

            // --- The soapbox street (see Race/PreviewCapture.Race.cs). ---
            AddRaceGroups(groups);

            // --- The alien field (see Aliens/PreviewCapture.Aliens.cs). ---
            AddAlienGroups(groups);

            // --- The zoo park: entrance, pens, keeper and every hiding spot with an animal in it (see Zoo/PreviewCapture.Zoo.cs). ---
            AddZooGroups(groups);

            // --- The Toy Room and the dress-up mirror (see ToyRoom/PreviewCapture.ToyRoom.cs). ---
            AddToyRoomGroups(groups);

            // --- The playground toys and the sticker board (see Playground/PreviewCapture.Toys.cs). ---
            AddToyGroups(groups);

            // --- Costumes and head pieces on the player (see Characters/PreviewCapture.Costumes.cs). ---
            AddCostumeGroups(groups);

            // --- The classroom: every exercise's props from the lesson camera (see Lessons/PreviewCapture.Lesson.cs). ---
            AddLessonGroups(groups);
            return groups;
        }

        // ----- Group builders -------------------------------------------------------------------

        enum Pose { Rest, Idle, Attack, Block }

        /// <summary>A view of a character: name, the camera's angle around it (0 = in front, 90 = its right side), and optional seconds to step the animation first.</summary>
        sealed class KidView
        {
            public string Name;
            public float Azimuth;
            public float StepSeconds;
            public float Distance = 2.8f;
            public HumanBodyBones? Bone;
            public float Fov = 30f;
        }

        static KidView Views(string name, float azimuth, float step = 0f)
        {
            return new KidView { Name = name, Azimuth = azimuth, StepSeconds = step };
        }

        /// <summary>A close view of a bone (the hand with the sword, the forearm with the shield).</summary>
        static KidView HandView(string name, HumanBodyBones bone, float azimuth, float distance)
        {
            return new KidView { Name = name, Azimuth = azimuth, Distance = distance, Bone = bone, Fov = 30f };
        }

        static Group KidGroup(string name, Func<GameObject> find, Pose pose, params KidView[] views)
        {
            GameObject kid = null;
            var group = new Group
            {
                Name = name,
                Stage = () =>
                {
                    kid = find();
                    if (kid == null) throw new InvalidOperationException("character not found");
                    StageKid(kid, pose);
                },
            };

            foreach (KidView view in views)
            {
                KidView v = view;
                group.Shots.Add(new Shot
                {
                    Name = v.Name,
                    Frame = () =>
                    {
                        if (v.StepSeconds > 0f) StepAnimators(kid, v.StepSeconds);
                        Vector3 centre = kid.transform.position + Vector3.up * 0.62f;
                        if (v.Bone.HasValue)
                        {
                            Animator animator = kid.GetComponentInChildren<Animator>();
                            Transform bone = animator != null ? animator.GetBoneTransform(v.Bone.Value) : null;
                            if (bone != null) centre = bone.position;
                        }
                        Orbit(centre, kid.transform, v.Azimuth, v.Distance, 8f, v.Fov);
                    },
                });
            }
            return group;
        }

        /// <summary>
        /// Ground-level shots of one placed piece (found by the function): the camera sits a hand above the ground and looks at the
        /// piece's foot from the playground side. Each view is a name and an angle off that direction.
        /// </summary>
        static Group SkimGroup(string name, Func<GameObject> find, params (string name, float azimuth)[] views)
        {
            GameObject piece = null;
            var group = new Group { Name = name, Stage = () => { piece = find(); HideZoneVisuals(); } };
            foreach ((string viewName, float azimuth) in views)
            {
                float angle = azimuth;
                group.Shots.Add(new Shot
                {
                    Name = viewName,
                    Frame = () =>
                    {
                        if (piece == null) throw new InvalidOperationException("piece not found");
                        Skim(piece, angle);
                    },
                });
            }
            return group;
        }

        // ----- Staging --------------------------------------------------------------------------

        static void ReloadScene()
        {
            EditorSceneManager.OpenScene(sceneToReload, OpenSceneMode.Single);
            cam = Camera.main;
            if (cam == null) throw new InvalidOperationException("no main camera");

            var follow = cam.GetComponent<FollowCamera>();
            if (follow != null) follow.enabled = false;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
        }

        /// <summary>Switches a character on, puts it down in front of the camera's usual spot, puts its weapons on, and poses it.</summary>
        static void StageKid(GameObject kid, Pose pose)
        {
            HideZoneVisuals();
            ActivateWithParents(kid);
            DisableMovers(kid);

            // A clear piece of ground in the south of the playground, facing the camera's side of the world.
            if (kid.name != "Player")
            {
                Vector3 spawn = GameObject.Find("Player") != null ? GameObject.Find("Player").transform.position : Vector3.zero;
                kid.transform.SetParent(null, true);
                kid.transform.SetPositionAndRotation(new Vector3(spawn.x + 4f, 0f, spawn.z), Quaternion.LookRotation(Vector3.forward));
            }

            // The weapons first, while the kid is in the rest pose.
            var weapons = kid.GetComponent<WeaponHolder>();
            if (weapons != null) weapons.ShowWeapons(true);
            else Debug.LogWarning($"{LogPrefix}{kid.name} has no WeaponHolder.");
            Physics.SyncTransforms();

            switch (pose)
            {
                case Pose.Rest:
                    break;
                case Pose.Idle:
                    ResetAnimators(kid);
                    StepAnimators(kid, 0.8f);
                    break;
                case Pose.Attack:
                    ResetAnimators(kid);
                    foreach (Animator animator in kid.GetComponentsInChildren<Animator>())
                    {
                        SetUpperBody(animator, 1f);
                        if (HasParameter(animator, "AttackVariant")) animator.SetInteger("AttackVariant", 0);
                        if (HasParameter(animator, "Attack")) animator.SetTrigger("Attack");
                        animator.Update(0.05f);
                    }
                    break;
                case Pose.Block:
                    ResetAnimators(kid);
                    foreach (Animator animator in kid.GetComponentsInChildren<Animator>())
                    {
                        SetUpperBody(animator, 1f);
                        if (HasParameter(animator, "Blocking")) animator.SetBool("Blocking", true);
                    }
                    StepAnimators(kid, 0.8f);
                    break;
            }
        }

        static void StageShipHelm()
        {
            HideZoneVisuals();
            PirateShip ship = UnityEngine.Object.FindFirstObjectByType<PirateShip>(FindObjectsInactive.Include);
            if (ship == null) throw new InvalidOperationException("no PirateShip");
            ActivateWithParents(ship.gameObject);

            GameObject player = GameObject.Find("Player");
            var riders = new List<(GameObject who, Transform seat)> { (player, ship.HelmSeat) };
            for (int i = 0; i < 2; i++)
            {
                GameObject crew = FindKid("Crew_", true, i);
                Transform seat = ship.CrewSeats != null && ship.CrewSeats.Length > 0 ? ship.CrewSeats[i % ship.CrewSeats.Length] : null;
                riders.Add((crew, seat));
            }

            foreach ((GameObject who, Transform seat) in riders)
            {
                if (who == null || seat == null)
                {
                    Debug.LogWarning($"{LogPrefix}ship helm: a rider or a seat is missing ({(who != null ? who.name : "none")}).");
                    continue;
                }
                Seat(who, seat);
            }
        }

        static void StageSkeletonBoat()
        {
            HideZoneVisuals();
            SkeletonBoat boat = UnityEngine.Object.FindFirstObjectByType<SkeletonBoat>(FindObjectsInactive.Include);
            if (boat == null) throw new InvalidOperationException("no SkeletonBoat");
            ActivateWithParents(boat.gameObject);

            Transform[] seats = boat.SkeletonSeats ?? Array.Empty<Transform>();
            for (int i = 0; i < seats.Length; i++)
            {
                GameObject skeleton = FindKid("Skeleton_" + (i + 1));
                if (skeleton == null || seats[i] == null) continue;
                Seat(skeleton, seats[i]);
            }
        }

        /// <summary>Seats a kid like PlayerController.RideOn does: parented to the seat with a zero local pose.</summary>
        static void Seat(GameObject who, Transform seat)
        {
            ActivateWithParents(who);
            DisableMovers(who);
            SeatUtil.Mount(who.transform, seat);

            var weapons = who.GetComponent<WeaponHolder>();
            if (weapons != null) weapons.ShowWeapons(true);
            ResetAnimators(who);
            StepAnimators(who, 0.6f);
            Physics.SyncTransforms();
        }

        static void DisableMovers(GameObject kid)
        {
            foreach (NavMeshAgent agent in kid.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            var controller = kid.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
        }

        static void HideZoneVisuals()
        {
            foreach (ActivityZone zone in UnityEngine.Object.FindObjectsByType<ActivityZone>(FindObjectsSortMode.None))
            {
                var visual = new SerializedObject(zone).FindProperty("visual").objectReferenceValue as GameObject;
                if (visual != null) visual.SetActive(false);
            }
        }

        static void ActivateWithParents(GameObject go)
        {
            for (Transform t = go.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        }

        // ----- Animation ------------------------------------------------------------------------

        static void ResetAnimators(GameObject root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>())
            {
                // The animator does not run in edit mode: step it so the kid is not left in the bind pose.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        static void StepAnimators(GameObject root, float seconds)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>())
            {
                // In small steps, so transitions and triggers behave as in play.
                float left = seconds;
                while (left > 0f)
                {
                    float dt = Mathf.Min(0.05f, left);
                    animator.Update(dt);
                    left -= dt;
                }
            }
        }

        static void SetUpperBody(Animator animator, float weight)
        {
            int layer = animator.GetLayerIndex("UpperBody");
            if (layer >= 0) animator.SetLayerWeight(layer, weight);
        }

        static bool HasParameter(Animator animator, string parameter)
        {
            return animator.parameters.Any(p => p.name == parameter);
        }

        // ----- Finding things -------------------------------------------------------------------

        static IEnumerable<GameObject> AllSceneObjects()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.gameObject.scene != scene) continue;
                yield return t.gameObject;
            }
        }

        /// <summary>A character by name (or by name prefix; the n-th match in name order), inactive ones too.</summary>
        static GameObject FindKid(string name, bool prefix = false, int index = 0)
        {
            List<GameObject> matches = AllSceneObjects()
                .Where(go => go.GetComponent<WeaponHolder>() != null && (prefix ? go.name.StartsWith(name) : go.name == name))
                .OrderBy(go => go.name, StringComparer.Ordinal)
                .ToList();
            return index < matches.Count ? matches[index] : null;
        }

        /// <summary>The piece under a group whose name contains the word and that stands nearest the point.</summary>
        static GameObject FindEnvironmentPiece(string groupPath, string word, Vector3 near)
        {
            GameObject group = GameObject.Find(groupPath);
            if (group == null) throw new InvalidOperationException("no group " + groupPath);

            GameObject best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform child in group.transform)
            {
                if (!child.name.Contains(word) || !child.gameObject.activeInHierarchy) continue;
                float distance = (FlatCentre(child.gameObject) - near).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = child.gameObject;
                }
            }
            return best;
        }

        /// <summary>A wall piece of the school, on the south facade (the side the camera sees), standing on the ground.</summary>
        static GameObject FindWallPiece()
        {
            GameObject school = GameObject.Find("School");
            if (school == null) throw new InvalidOperationException("no School");

            GameObject best = null;
            float bestScore = float.MaxValue;
            foreach (MeshRenderer renderer in school.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.name.Contains("Wall_West") || renderer.bounds.min.y > 0.3f || renderer.bounds.size.y < 1f) continue;
                // The south-most wall pieces, then the one nearest the middle of the school.
                float score = renderer.bounds.center.z * 10f + Mathf.Abs(renderer.bounds.center.x);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = renderer.gameObject;
                }
            }
            return best;
        }

        static Vector3 FlatCentre(GameObject go)
        {
            Bounds bounds = BoundsOf(go);
            return new Vector3(bounds.center.x, 0f, bounds.center.z);
        }

        static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // ----- Cameras --------------------------------------------------------------------------

        /// <summary>The camera at an angle around a subject: 0 = in front of it, 90 = at its right side. Looks at the centre.</summary>
        static void Orbit(Vector3 centre, Transform subject, float azimuth, float distance, float elevationDegrees, float fov)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(subject.forward, Vector3.up).normalized;
            Vector3 direction = Quaternion.AngleAxis(azimuth, Vector3.up) * flatForward;
            direction = Quaternion.AngleAxis(-elevationDegrees, Vector3.Cross(Vector3.up, direction)) * direction;
            LookAt(centre + direction * distance, centre, fov);
        }

        static void LookAt(Vector3 eye, Vector3 target, float fov)
        {
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            cam.fieldOfView = fov;
        }

        /// <summary>
        /// Ground level: the camera is 10 cm above the ground and looks at the foot of the piece from the side facing the middle of
        /// the playground (turned by the angle), so any gap between the piece and the grass shows.
        /// </summary>
        static void Skim(GameObject piece, float azimuth)
        {
            Bounds bounds = BoundsOf(piece);
            Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 toMiddle = new Vector3(-foot.x, 0f, -foot.z);
            if (toMiddle.sqrMagnitude < 0.01f) toMiddle = Vector3.back;
            Vector3 direction = Quaternion.AngleAxis(azimuth, Vector3.up) * toMiddle.normalized;

            float reach = Mathf.Max(2.2f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.9f);
            Vector3 eye = foot + direction * reach;
            eye.y = foot.y + 0.3f;
            LookAt(eye, foot + Vector3.up * 0.2f, 40f);
        }

        static Transform ShipHelmSeat()
        {
            return UnityEngine.Object.FindFirstObjectByType<PirateShip>(FindObjectsInactive.Include).HelmSeat;
        }

        static Transform ShipCrewSeat(int index)
        {
            Transform[] seats = UnityEngine.Object.FindFirstObjectByType<PirateShip>(FindObjectsInactive.Include).CrewSeats;
            return seats != null && seats.Length > 0 ? seats[index % seats.Length] : ShipHelmSeat();
        }

        /// <summary>Looks at a seated kid from an angle around the seat (0 = from the seat's front).</summary>
        static void SeatView(Transform seat, float azimuth, float distance)
        {
            Orbit(seat.position + Vector3.up * 0.55f, seat, azimuth, distance, 58f, 34f);
        }

        /// <summary>A view of a named pirate cove object: the camera sits at an offset from the point it looks at (the object's middle plus focusOffset).</summary>
        static void CoveView(string objectName, Vector3 focusOffset, Vector3 eyeOffset, float fov)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null) throw new InvalidOperationException("no object named " + objectName);

            Vector3 focus = BoundsOf(target).center + focusOffset;
            LookAt(focus + eyeOffset, focus, fov);
        }

        static void ShipWide()
        {
            PirateShip ship = UnityEngine.Object.FindFirstObjectByType<PirateShip>(FindObjectsInactive.Include);
            Bounds bounds = BoundsOf(ship.gameObject);
            Vector3 flatForward = Vector3.ProjectOnPlane(ship.HelmSeat.forward, Vector3.up).normalized;
            Vector3 eye = bounds.center + Quaternion.AngleAxis(150f, Vector3.up) * flatForward * (bounds.size.magnitude * 0.9f) + Vector3.up * bounds.size.y * 1.2f;
            LookAt(eye, bounds.center, 40f);
        }

        static void BoatView(float azimuth, float distance, float elevation)
        {
            SkeletonBoat boat = UnityEngine.Object.FindFirstObjectByType<SkeletonBoat>(FindObjectsInactive.Include);
            Bounds bounds = BoundsOf(boat.gameObject);
            Vector3 centre = bounds.center + Vector3.up * 0.2f;
            Orbit(centre, boat.transform, azimuth, distance, elevation, 38f);
        }

        // ----- Rendering ------------------------------------------------------------------------

        static void RenderAndSave(Vector2Int size, string label)
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
            Debug.Log(LogPrefix + "wrote " + path);

            UnityEngine.Object.DestroyImmediate(picture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            cam.ResetAspect();
        }

        // ----- Weapon survey --------------------------------------------------------------------

        /// <summary>Logs the measured shape and the detected axes of every sword, club, shield and similar toy weapon prefab.</summary>
        static void LogWeaponShapes()
        {
            string[] words = { "Sword", "Club", "Shield", "Mace", "Baton", "Dagger", "Axe" };
            foreach (string guid in AssetDatabase.FindAssets("SM_Wep_ t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!words.Any(w => name.Contains(w))) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                WeaponFit.Shape shape = WeaponFit.MeasureShape(instance);
                if (shape.Valid)
                {
                    Debug.Log($"{LogPrefix}weapon {name}: size {shape.Size.ToString("0.000")}, centre {shape.Centre.ToString("0.000")}, "
                        + $"long axis {"xyz"[shape.LongAxis]}, mid {"xyz"[shape.MidAxis]}, thin {"xyz"[shape.ThinAxis]}, "
                        + $"grip end {(shape.GripSign < 0f ? "-" : "+")}{"xyz"[shape.LongAxis]}, pivot near an end: {shape.PivotNearEnd}");
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix}weapon {name}: no mesh bounds.");
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
