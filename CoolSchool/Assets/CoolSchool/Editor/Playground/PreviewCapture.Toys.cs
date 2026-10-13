using System;
using System.Collections.Generic;
using System.Linq;
using CoolSchool.Characters;
using CoolSchool.Playground;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The playground toy shots of PreviewCapture. Every shot is named toy_&lt;id&gt;_&lt;moment&gt; (the group is toy_&lt;id&gt;), so
    /// "-previewShots toy" renders them all. The kid is staged on a named anchor of a toy ("ToyAnchors" child), posed with a ToyPose (the
    /// animator stepped in edit mode), and the camera is put at the game camera's side (azimuth 0, from the south) or at a side angle.
    /// Each toy also has a "markers" group: small coloured balls on all its anchors, seen from the front and from above, to check that the
    /// anchors the builder worked out lie where they should.
    /// Agent D adds the other toys through AddMoreToyGroups (Editor/Playground/PreviewCapture.Toys.More.cs) and may use the helpers here.
    /// </summary>
    public static partial class PreviewCapture
    {
        /// <summary>Implemented in PreviewCapture.Toys.More.cs: the shots of the other toys.</summary>
        static partial void AddMoreToyGroups(List<Group> groups);

        static void AddToyGroups(List<Group> groups)
        {
            AddSlideGroups(groups);
            AddRockerGroups(groups, "rocker_1");
            AddRockerGroups(groups, "rocker_2");
            AddRoundaboutGroups(groups);
            AddTunnelGroups(groups);
            AddStickerBoardGroups(groups);
            AddMoreToyGroups(groups);
        }

        // ----- Slide ---------------------------------------------------------------------------

        static void AddSlideGroups(List<Group> groups)
        {
            var slide = new Group { Name = "toy_slide", Stage = HideZoneVisuals };
            // The whole tower, without the kid, from the game camera's side, the west side, the back and above (its own group: a fresh scene,
            // so nothing was drawn with the kid before he is staged).
            var whole = new Group { Name = "toy_slide", Stage = HideZoneVisuals };
            whole.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("slide")), 0f, 38f, 1.0f, 40f) });
            whole.Shots.Add(new Shot { Name = "whole_side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("slide")), 90f, 14f, 1.0f, 40f) });
            whole.Shots.Add(new Shot { Name = "whole_back", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("slide")), 160f, 25f, 1.0f, 40f) });
            whole.Shots.Add(new Shot { Name = "whole_top", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("slide")), 0f, 85f, 1.0f, 40f) });
            // The kid on the way up (running on the spot), on the landing, at the top, and on the chute.
            slide.Shots.Add(ToyKidShot("stairs_lower_camera", "slide", "PreviewStairsLower", ToyPose.None, 0f, 28f, 0.55f, 35f, null, 3.2f));
            slide.Shots.Add(ToyKidShot("stairs_lower_side", "slide", "PreviewStairsLower", ToyPose.None, 90f, 10f, 0.55f, 35f, null, 3.2f));
            slide.Shots.Add(ToyKidShot("landing_side", "slide", "PreviewLanding", ToyPose.None, 90f, 10f, 0.55f, 35f, null, 3.2f));
            slide.Shots.Add(ToyKidShot("stairs_upper_camera", "slide", "PreviewStairsUpper", ToyPose.None, 0f, 28f, 0.55f, 35f, null, 3.2f));
            slide.Shots.Add(ToyKidShot("stairs_upper_side", "slide", "PreviewStairsUpper", ToyPose.None, 90f, 10f, 0.55f, 35f, null, 3.2f));
            slide.Shots.Add(ToyKidShot("seat_camera", "slide", "PreviewSeat", ToyPose.SitSlide, 0f, 28f, 0.55f, 35f));
            slide.Shots.Add(ToyKidShot("seat_side", "slide", "PreviewSeat", ToyPose.SitSlide, 90f, 10f, 0.55f, 35f));
            slide.Shots.Add(ToyKidShot("mid_camera", "slide", "PreviewSlideMid", ToyPose.SitSlide, 0f, 28f, 0.55f, 35f));
            slide.Shots.Add(ToyKidShot("mid_side", "slide", "PreviewSlideMid", ToyPose.SitSlide, 90f, 10f, 0.55f, 35f));
            slide.Shots.Add(ToyKidShot("end_side", "slide", "PreviewSlideEnd", ToyPose.SitSlide, 90f, 10f, 0.55f, 35f));
            slide.Shots.Add(ToyKidShot("end_closeup", "slide", "PreviewSlideEnd", ToyPose.SitSlide, 60f, 14f, 0.3f, 35f));
            groups.Add(whole);
            groups.Add(slide);
            groups.Add(ToyMarkersGroup("slide"));
        }

        // ----- Rockers -------------------------------------------------------------------------

        static void AddRockerGroups(List<Group> groups, string id)
        {
            var rocker = new Group { Name = "toy_" + id, Stage = HideZoneVisuals };
            rocker.Shots.Add(ToyKidShot("rest_camera", id, "RockerSeat", ToyPose.SitSeat, 0f, 28f, 1.1f, 35f));
            rocker.Shots.Add(ToyKidShot("rest_side", id, "RockerSeat", ToyPose.SitSeat, 90f, 10f, 1.1f, 35f));
            rocker.Shots.Add(ToyKidShot("forward_side", id, "RockerSeat", ToyPose.SitSeat, 90f, 10f, 1.1f, 35f, () => TiltRocker(id, 1f)));
            rocker.Shots.Add(ToyKidShot("back_side", id, "RockerSeat", ToyPose.SitSeat, 90f, 10f, 1.1f, 35f, () => TiltRocker(id, -1f)));
            groups.Add(rocker);
            groups.Add(ToyMarkersGroup(id));
        }

        static void TiltRocker(string id, float swing)
        {
            GameObject toy = ToyFindToy(id);
            RockerRoutine routine = toy != null ? toy.GetComponent<RockerRoutine>() : null;
            if (routine == null) throw new InvalidOperationException("no RockerRoutine on " + id);
            routine.Apply(swing);
            Physics.SyncTransforms();
        }

        // ----- Roundabout ----------------------------------------------------------------------

        static void AddRoundaboutGroups(List<Group> groups)
        {
            var roundabout = new Group { Name = "toy_roundabout", Stage = HideZoneVisuals };
            roundabout.Shots.Add(ToyKidShot("seat_camera", "roundabout", "RoundaboutSeat", RoundaboutPose(), 0f, 30f, 1.3f, 35f));
            roundabout.Shots.Add(ToyKidShot("seat_side", "roundabout", "RoundaboutSeat", RoundaboutPose(), 90f, 10f, 1.3f, 35f));
            roundabout.Shots.Add(ToyKidShot("seat_top", "roundabout", "RoundaboutSeat", RoundaboutPose(), 0f, 85f, 1.2f, 35f));
            groups.Add(roundabout);
            groups.Add(ToyMarkersGroup("roundabout"));
        }

        /// <summary>The pose the roundabout's routine sits in (read from the scene when the shot is taken).</summary>
        static ToyPose RoundaboutPose()
        {
            return ToyPose.SitSlide;
        }

        // ----- Tunnel --------------------------------------------------------------------------

        static void AddTunnelGroups(List<Group> groups)
        {
            var tunnel = new Group { Name = "toy_tunnel", Stage = HideZoneVisuals };
            tunnel.Shots.Add(ToyKidShot("outside_camera", "tunnel", "TunnelPath_0", ToyPose.Crawl, 0f, 28f, 1.2f, 35f));
            tunnel.Shots.Add(ToyKidShot("middle_camera", "tunnel", "PreviewMiddle", ToyPose.Crawl, 0f, 28f, 1.2f, 35f));
            tunnel.Shots.Add(ToyKidShot("middle_side", "tunnel", "PreviewMiddle", ToyPose.Crawl, 90f, 10f, 1.2f, 35f));
            tunnel.Shots.Add(ToyKidShot("middle_top", "tunnel", "PreviewMiddle", ToyPose.Crawl, 0f, 85f, 1.2f, 35f));
            groups.Add(tunnel);
            groups.Add(ToyMarkersGroup("tunnel"));
        }

        // ----- The sticker board ---------------------------------------------------------------

        static void AddStickerBoardGroups(List<Group> groups)
        {
            // Half the stickers owned (in memory only: nothing is saved).
            var board = new Group
            {
                Name = "toy_board",
                Stage = () =>
                {
                    HideZoneVisuals();
                    PlayerProgress progress = ProgressStore.Current;
                    progress.toyStickers = new List<string>();
                    for (int i = 0; i < ToyStickers.AllIds.Length; i += 2) progress.toyStickers.Add(ToyStickers.AllIds[i]);
                    StickerBoard found = UnityEngine.Object.FindFirstObjectByType<StickerBoard>(FindObjectsInactive.Include);
                    if (found == null) throw new InvalidOperationException("no StickerBoard");
                    ActivateWithParents(found.gameObject);
                    found.Refresh();
                },
            };
            board.Shots.Add(new Shot { Name = "camera", Frame = () => BoardLook(0f, 42f, 1.05f, 40f) });
            board.Shots.Add(new Shot { Name = "front", Frame = () => BoardLook(0f, 3f, 0.95f, 35f) });
            board.Shots.Add(new Shot { Name = "side", Frame = () => BoardLook(50f, 15f, 1.1f, 40f) });
            board.Shots.Add(new Shot { Name = "near_spawn", Frame = BoardNearSpawn });
            groups.Add(board);
        }

        static void BoardLook(float azimuth, float elevation, float distanceFactor, float fov)
        {
            StickerBoard board = UnityEngine.Object.FindFirstObjectByType<StickerBoard>(FindObjectsInactive.Include);
            if (board == null) throw new InvalidOperationException("no StickerBoard");
            ToyLookAt(BoundsOf(board.gameObject), azimuth, elevation, distanceFactor, fov);
        }

        /// <summary>From the follow camera's pose with the kid at the spawn: does the board show up?</summary>
        static void BoardNearSpawn()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) throw new InvalidOperationException("no Player");
            Vector3 spawn = player.transform.position;
            LookAt(spawn + new Vector3(0f, 6.5f, -7f), spawn + new Vector3(0f, 0.8f, 0f), 50f);
        }

        // ----- Shared pieces -------------------------------------------------------------------

        /// <summary>A shot of the kid staged on a toy's anchor with a pose, seen from an angle around the toy (0 = from the south).</summary>
        static Shot ToyKidShot(string name, string stickerId, string anchorName, ToyPose pose, float azimuth, float elevation,
            float distanceFactor, float fov, Action beforePose = null, float speed = 0f)
        {
            return new Shot
            {
                Name = name,
                Frame = () =>
                {
                    beforePose?.Invoke();
                    Transform anchor = ToyAnchor(stickerId, anchorName);
                    if (anchor == null) throw new InvalidOperationException($"toy {stickerId} has no anchor '{anchorName}'");

                    StageToyKid(anchor, pose, 0.8f, speed);
                    GameObject toy = ToyFindToy(stickerId);
                    Bounds focus = BoundsOf(toy);
                    // Look at the kid, not the middle of a big toy, and keep the whole kid in view.
                    Bounds kid = BoundsOf(FindKid("Player"));
                    focus.center = Vector3.Lerp(focus.center, kid.center, 0.65f);
                    ToyLookAt(focus, azimuth, elevation, distanceFactor, fov);
                },
            };
        }

        /// <summary>Small coloured balls on every anchor of the toy, seen from the front and from above.</summary>
        static Group ToyMarkersGroup(string stickerId)
        {
            var group = new Group { Name = "toy_" + stickerId + "_markers", Stage = () => { HideZoneVisuals(); ToyAddMarkers(stickerId); } };
            group.Shots.Add(new Shot { Name = "camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy(stickerId)), 0f, 42f, 1.0f, 35f) });
            group.Shots.Add(new Shot { Name = "side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy(stickerId)), 90f, 12f, 1.0f, 35f) });
            group.Shots.Add(new Shot { Name = "top", Frame = () => ToyLookAt(BoundsOf(ToyFindToy(stickerId)), 0f, 88f, 1.0f, 35f) });
            return group;
        }

        static void ToyAddMarkers(string stickerId)
        {
            GameObject toy = ToyFindToy(stickerId);
            if (toy == null) throw new InvalidOperationException("no toy " + stickerId);

            Transform anchors = toy.transform.Find("ToyAnchors");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var colours = new[] { Color.red, Color.yellow, Color.green, Color.cyan, Color.blue, Color.magenta, new Color(1f, 0.5f, 0f), Color.white };
            int index = 0;
            foreach (Transform t in toy.GetComponentsInChildren<Transform>(true))
            {
                bool anchor = anchors != null && t != anchors && t.IsChildOf(anchors);
                bool moved = t.name.EndsWith("Rider") || t.name.EndsWith("Seat") || t.name.EndsWith("Pivot") || t.name == "Spinner";
                if (!anchor && !moved) continue;

                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Marker_" + t.name;
                UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.transform.position = t.position;
                ball.transform.localScale = Vector3.one * 0.1f;
                var renderer = ball.GetComponent<MeshRenderer>();
                if (shader != null)
                {
                    var material = new Material(shader);
                    material.SetColor("_BaseColor", colours[index % colours.Length]);
                    renderer.sharedMaterial = material;
                }

                // A short stick along the anchor's forward so its facing shows.
                var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nose.name = "MarkerNose_" + t.name;
                UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());
                nose.transform.position = t.position + t.forward * 0.18f;
                nose.transform.rotation = t.rotation;
                nose.transform.localScale = new Vector3(0.04f, 0.04f, 0.3f);
                if (shader != null) nose.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
                index++;
            }
        }

        /// <summary>The toy root with this sticker id.</summary>
        static GameObject ToyFindToy(string stickerId)
        {
            foreach (PlaygroundToy toy in UnityEngine.Object.FindObjectsByType<PlaygroundToy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (toy.StickerId == stickerId) return toy.gameObject;
            return null;
        }

        /// <summary>A named anchor (or any named object) under the toy.</summary>
        static Transform ToyAnchor(string stickerId, string anchorName)
        {
            GameObject toy = ToyFindToy(stickerId);
            if (toy == null) return null;
            return toy.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == anchorName);
        }

        /// <summary>Puts the player kid on the anchor like PlayerController.RideOn does, in the pose, and steps the animator so the pose shows.</summary>
        static void StageToyKid(Transform anchor, ToyPose pose, float seconds = 0.8f, float speed = 0f)
        {
            GameObject kid = FindKid("Player");
            if (kid == null) throw new InvalidOperationException("no Player");

            ActivateWithParents(kid);
            DisableMovers(kid);
            SeatUtil.Mount(kid.transform, anchor);
            Physics.SyncTransforms();

            ResetAnimators(kid);
            foreach (Animator animator in kid.GetComponentsInChildren<Animator>())
            {
                if (HasParameter(animator, "ToyPose")) animator.SetInteger("ToyPose", (int)pose);
                if (speed > 0f && HasParameter(animator, "Speed")) animator.SetFloat("Speed", speed);
            }
            StepAnimators(kid, seconds);
            RefreshKidSkinning(kid, anchor, pose);
        }

        /// <summary>
        /// Makes sure the kid's skinned meshes follow the bones that the animator just moved (a mesh that is not re-skinned shows the T-pose
        /// while the attachments, which follow the bones, show the real pose), and logs the pose that was reached so a stuck pose shows in the log.
        /// </summary>
        static void RefreshKidSkinning(GameObject kid, Transform anchor, ToyPose pose)
        {
            foreach (SkinnedMeshRenderer skinned in kid.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinned.updateWhenOffscreen = true;
                skinned.forceMatrixRecalculationPerRender = true;
                bool was = skinned.enabled;
                skinned.enabled = false;
                skinned.enabled = was;
            }

            Animator animator = kid.GetComponentInChildren<Animator>();
            if (animator == null) return;

            animator.Update(0.001f);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "none";
            Debug.Log($"{LogPrefix}toy kid staged on {anchor.name} with pose {pose}: animator clip '{clipName}', hips "
                + $"{(hips != null ? (hips.position.y - kid.transform.position.y).ToString("0.00") : "?")} m above the kid's root, "
                + $"scale {kid.transform.lossyScale.x:0.00}, human {animator.isHuman}, controller {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "none")}");
        }

        /// <summary>
        /// The camera round a subject: azimuth 0 looks from the south (the game camera's side), 90 from the east; elevation in degrees above
        /// the ground; the distance fits the subject's size into the view, times the factor.
        /// </summary>
        static void ToyLookAt(Bounds subject, float azimuth, float elevationDegrees, float distanceFactor, float fov)
        {
            float radius = Mathf.Max(0.5f, subject.extents.magnitude);
            float distance = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * distanceFactor;
            Vector3 direction = Quaternion.AngleAxis(azimuth, Vector3.up) * Vector3.back;
            direction = Quaternion.AngleAxis(elevationDegrees, Vector3.Cross(Vector3.up, direction).normalized * -1f) * direction;
            LookAt(subject.center + direction.normalized * distance, subject.center, fov);
            cam.farClipPlane = 200f;
        }
    }
}
