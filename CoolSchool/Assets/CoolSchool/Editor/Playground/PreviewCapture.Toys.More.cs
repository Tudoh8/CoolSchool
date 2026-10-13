using System;
using System.Collections.Generic;
using CoolSchool.Playground;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The preview shots of the monkey bars, seesaw, swings, fort, tyre wall, track ride and ship (the other toys are in
    /// PreviewCapture.Toys.cs). Every shot is named toy_&lt;id&gt;_&lt;moment&gt;. Each toy has the kid staged on its anchors in the right pose
    /// (from the camera's side and from the east), parts moved to an end position where that matters (the plank tilted, a swing swung high,
    /// the handle at the low end), and a "markers" group with a ball on every anchor, seen from the front and from above.
    /// </summary>
    public static partial class PreviewCapture
    {
        static partial void AddMoreToyGroups(List<Group> groups)
        {
            AddMonkeyBarsGroups(groups);
            AddSeesawGroups(groups);
            AddSwingGroups(groups);
            AddFortGroups(groups);
            AddTyreWallGroups(groups);
            AddTrackRideGroups(groups);
            AddShipGroups(groups);
        }

        // ----- Monkey bars ---------------------------------------------------------------------

        static void AddMonkeyBarsGroups(List<Group> groups)
        {
            var bars = new Group { Name = "toy_monkey_bars", Stage = HideZoneVisuals };
            bars.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("monkey_bars")), 0f, 30f, 1.0f, 35f) });
            bars.Shots.Add(new Shot { Name = "whole_side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("monkey_bars")), 90f, 12f, 1.0f, 35f) });
            bars.Shots.Add(ToyKidShot("hang_camera", "monkey_bars", "HangStart", ToyPose.Hang, 0f, 20f, 1.2f, 35f));
            bars.Shots.Add(ToyKidShot("hang_side", "monkey_bars", "HangStart", ToyPose.Hang, 90f, 8f, 1.2f, 35f));
            bars.Shots.Add(ToyKidShot("hang_front", "monkey_bars", "HangStart", ToyPose.Hang, 45f, 15f, 0.6f, 30f));
            bars.Shots.Add(ToyKidShot("move_camera", "monkey_bars", "HangEnd", ToyPose.HangMove, 0f, 20f, 1.2f, 35f));
            bars.Shots.Add(ToyKidShot("move_side", "monkey_bars", "HangEnd", ToyPose.HangMove, 90f, 8f, 1.2f, 35f));
            groups.Add(bars);
            groups.Add(ToyMarkersGroup("monkey_bars"));
        }

        // ----- Seesaw --------------------------------------------------------------------------

        static void AddSeesawGroups(List<Group> groups)
        {
            var seesaw = new Group { Name = "toy_seesaw", Stage = HideZoneVisuals };
            seesaw.Shots.Add(ToyKidShot("level_camera", "seesaw", "SeatAnchor", ToyPose.SitSeat, 0f, 25f, 1.1f, 35f));
            seesaw.Shots.Add(ToyKidShot("level_side", "seesaw", "SeatAnchor", ToyPose.SitSeat, 90f, 8f, 1.1f, 35f));
            seesaw.Shots.Add(ToyKidShot("down_side", "seesaw", "SeatAnchor", ToyPose.SitSeat, 90f, 8f, 1.1f, 35f, () => MoreSeesawAngle(12f)));
            seesaw.Shots.Add(ToyKidShot("up_side", "seesaw", "SeatAnchor", ToyPose.SitSeat, 90f, 8f, 1.1f, 35f, () => MoreSeesawAngle(-12f)));
            seesaw.Shots.Add(ToyKidShot("down_camera", "seesaw", "SeatAnchor", ToyPose.SitSeat, 0f, 25f, 1.1f, 35f, () => MoreSeesawAngle(12f)));
            seesaw.Shots.Add(new Shot
            {
                Name = "teddy",
                Frame = () =>
                {
                    GameObject toy = ToyFindToy("seesaw");
                    Transform teddy = toy != null ? toy.transform.Find("PlankPivot/Teddy") : null;
                    if (teddy == null) throw new InvalidOperationException("the seesaw has no teddy");
                    ToyLookAt(BoundsOf(teddy.gameObject), 25f, 15f, 1.6f, 35f);
                },
            });
            groups.Add(seesaw);
            groups.Add(ToyMarkersGroup("seesaw"));
        }

        static void MoreSeesawAngle(float degrees)
        {
            GameObject toy = ToyFindToy("seesaw");
            SeesawRoutine routine = toy != null ? toy.GetComponent<SeesawRoutine>() : null;
            if (routine == null) throw new InvalidOperationException("no SeesawRoutine on the seesaw");
            routine.Apply(degrees);
            Physics.SyncTransforms();
        }

        // ----- Swings --------------------------------------------------------------------------

        static void AddSwingGroups(List<Group> groups)
        {
            var swings = new Group { Name = "toy_swings", Stage = HideZoneVisuals };
            swings.Shots.Add(ToyKidShot("rest_camera", "swings", "SeatAnchor_0", ToyPose.SitSeat, 0f, 25f, 1.2f, 35f));
            swings.Shots.Add(ToyKidShot("rest_side", "swings", "SeatAnchor_0", ToyPose.SitSeat, 90f, 8f, 1.2f, 35f));
            swings.Shots.Add(ToyKidShot("rest_other", "swings", "SeatAnchor_1", ToyPose.SitSeat, 0f, 25f, 1.2f, 35f));
            swings.Shots.Add(ToyKidShot("high_camera", "swings", "SeatAnchor_0", ToyPose.SitSeat, 0f, 25f, 1.2f, 35f, () => MoreSwingAngle(0, 35f)));
            swings.Shots.Add(ToyKidShot("high_side", "swings", "SeatAnchor_0", ToyPose.SitSeat, 90f, 8f, 1.2f, 35f, () => MoreSwingAngle(0, 35f)));
            swings.Shots.Add(ToyKidShot("back_side", "swings", "SeatAnchor_0", ToyPose.SitSeat, 90f, 8f, 1.2f, 35f, () => MoreSwingAngle(0, -30f)));
            groups.Add(swings);
            groups.Add(ToyMarkersGroup("swings"));
        }

        static void MoreSwingAngle(int index, float degrees)
        {
            GameObject toy = ToyFindToy("swings");
            SwingRoutine routine = toy != null ? toy.GetComponent<SwingRoutine>() : null;
            if (routine == null) throw new InvalidOperationException("no SwingRoutine on the swings");
            routine.Apply(index, degrees);
            Physics.SyncTransforms();
        }

        // ----- Fort ----------------------------------------------------------------------------

        static void AddFortGroups(List<Group> groups)
        {
            // The path is a slope (crawl up it, sit and slide down) or a wall (climb); the shots use the names both versions have.
            var fort = new Group { Name = "toy_fort", Stage = HideZoneVisuals };
            fort.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("fort")), 0f, 30f, 1.0f, 35f) });
            fort.Shots.Add(new Shot { Name = "whole_side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("fort")), 90f, 12f, 1.0f, 35f) });
            fort.Shots.Add(ToyKidShot("foot_camera", "fort", "ClimbAnchor", MoreFortClimbPose(), 0f, 25f, 1.2f, 35f));
            fort.Shots.Add(ToyKidShot("foot_side", "fort", "ClimbAnchor", MoreFortClimbPose(), 90f, 8f, 1.2f, 35f));
            fort.Shots.Add(ToyKidShot("mid_side", "fort", "ClimbPath_2", MoreFortClimbPose(), 90f, 8f, 1.2f, 35f));
            fort.Shots.Add(ToyKidShot("top_camera", "fort", "ClimbTop", ToyPose.None, 0f, 25f, 1.2f, 35f));
            fort.Shots.Add(ToyKidShot("top_side", "fort", "ClimbTop", ToyPose.None, 90f, 8f, 1.2f, 35f));
            fort.Shots.Add(ToyKidShot("slide_mid_side", "fort", "ClimbPath_2", ToyPose.SitSlide, 90f, 8f, 1.2f, 35f));
            groups.Add(fort);
            groups.Add(ToyMarkersGroup("fort"));
        }

        /// <summary>Crawl when the fort's route is a slope (anchors ClimbPath_n), Climb when it is a wall.</summary>
        static ToyPose MoreFortClimbPose()
        {
            return ToyPose.Crawl;
        }

        // ----- Tyre wall -----------------------------------------------------------------------

        static void AddTyreWallGroups(List<Group> groups)
        {
            var wall = new Group { Name = "toy_tyre_wall", Stage = HideZoneVisuals };
            wall.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("tyre_wall")), 0f, 20f, 1.0f, 35f) });
            wall.Shots.Add(ToyKidShot("foot_camera", "tyre_wall", "ClimbAnchor", ToyPose.Climb, 0f, 15f, 1.2f, 35f));
            wall.Shots.Add(ToyKidShot("foot_side", "tyre_wall", "ClimbAnchor", ToyPose.Climb, 90f, 8f, 1.2f, 35f));
            wall.Shots.Add(ToyKidShot("high_camera", "tyre_wall", "ClimbWallTop", ToyPose.Climb, 0f, 15f, 1.2f, 35f));
            wall.Shots.Add(ToyKidShot("high_side", "tyre_wall", "ClimbWallTop", ToyPose.Climb, 90f, 8f, 1.2f, 35f));
            wall.Shots.Add(ToyKidShot("top_camera", "tyre_wall", "ClimbTop", ToyPose.None, 0f, 15f, 1.2f, 35f));
            wall.Shots.Add(ToyKidShot("top_side", "tyre_wall", "ClimbTop", ToyPose.None, 90f, 8f, 1.2f, 35f));
            groups.Add(wall);
            groups.Add(ToyMarkersGroup("tyre_wall"));
        }

        // ----- Track ride ----------------------------------------------------------------------

        static void AddTrackRideGroups(List<Group> groups)
        {
            var ride = new Group { Name = "toy_track_ride", Stage = HideZoneVisuals };
            ride.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("track_ride")), 0f, 20f, 1.0f, 35f) });
            ride.Shots.Add(new Shot { Name = "whole_side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("track_ride")), 90f, 10f, 1.0f, 35f) });
            ride.Shots.Add(ToyKidShot("start_camera", "track_ride", "HangAnchor", ToyPose.Hang, 0f, 15f, 1.2f, 35f));
            ride.Shots.Add(ToyKidShot("start_side", "track_ride", "HangAnchor", ToyPose.Hang, 90f, 8f, 1.2f, 35f));
            ride.Shots.Add(ToyKidShot("middle_camera", "track_ride", "HangAnchor", ToyPose.Hang, 0f, 15f, 1.2f, 35f, () => MoreRideAlong(0.5f)));
            ride.Shots.Add(ToyKidShot("end_camera", "track_ride", "HangAnchor", ToyPose.Hang, 0f, 15f, 1.2f, 35f, () => MoreRideAlong(1f)));
            ride.Shots.Add(ToyKidShot("end_side", "track_ride", "HangAnchor", ToyPose.Hang, 90f, 8f, 1.2f, 35f, () => MoreRideAlong(1f)));
            groups.Add(ride);
            groups.Add(ToyMarkersGroup("track_ride"));
        }

        static void MoreRideAlong(float along)
        {
            GameObject toy = ToyFindToy("track_ride");
            TrackRideRoutine routine = toy != null ? toy.GetComponent<TrackRideRoutine>() : null;
            if (routine == null) throw new InvalidOperationException("no TrackRideRoutine on the track ride");
            routine.Apply(along);
            Physics.SyncTransforms();
        }

        // ----- Ship ----------------------------------------------------------------------------

        static void AddShipGroups(List<Group> groups)
        {
            var ship = new Group { Name = "toy_ship", Stage = HideZoneVisuals };
            ship.Shots.Add(new Shot { Name = "whole_camera", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("ship")), 0f, 25f, 1.0f, 35f) });
            ship.Shots.Add(new Shot { Name = "whole_side", Frame = () => ToyLookAt(BoundsOf(ToyFindToy("ship")), 90f, 12f, 1.0f, 35f) });
            ship.Shots.Add(ToyKidShot("helm_camera", "ship", "HelmAnchor", ToyPose.None, 0f, 25f, 1.2f, 35f));
            ship.Shots.Add(ToyKidShot("helm_side", "ship", "HelmAnchor", ToyPose.None, 90f, 8f, 1.2f, 35f));
            ship.Shots.Add(ToyKidShot("helm_front", "ship", "HelmAnchor", ToyPose.None, 180f, 15f, 1.2f, 35f));
            ship.Shots.Add(ToyKidShot("helm_top", "ship", "HelmAnchor", ToyPose.None, 0f, 80f, 1.0f, 35f));
            groups.Add(ship);
            groups.Add(ToyMarkersGroup("ship"));
        }
    }
}
