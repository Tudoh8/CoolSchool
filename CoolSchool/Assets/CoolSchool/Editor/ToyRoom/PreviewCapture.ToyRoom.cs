using System;
using System.Collections.Generic;
using System.Linq;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using CoolSchool.ToyRoom;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The Toy Room shots of PreviewCapture: the building from the follow camera, the inside with a fresh save (every item a black
    /// silhouette except the starters), the inside with everything won and 12 medals, and the dress-up mirror (the kid at the mirror
    /// from the wardrobe camera, with and without the picture menu).
    /// The roof and the south wall are hidden for the inside shots, like the interior reveal does while he is in the room.
    /// All the inside views are given in the room's own offsets (the room is not turned, so they are world offsets from its centre).
    /// </summary>
    public static partial class PreviewCapture
    {
        const string ToyRoomName = "ToyRoom";

        static void AddToyRoomGroups(List<Group> groups)
        {
            // From the follow camera's pose with the kid at the door, and from further away.
            var outside = new Group { Name = "toyroom_outside", Stage = () => { HideZoneVisuals(); } };
            outside.Shots.Add(new Shot { Name = "follow", Frame = () => ToyDoorLook(new Vector3(0f, 6.5f, -7f), new Vector3(0f, 0.8f, 0f), 50f) });
            outside.Shots.Add(new Shot { Name = "wide", Frame = () => ToyRoomLook(new Vector3(0f, 11f, -15f), new Vector3(0f, 1f, 0f), 50f) });
            outside.Shots.Add(new Shot { Name = "door", Frame = () => ToyDoorLook(new Vector3(2.5f, 1.8f, -4.5f), new Vector3(0f, 1.3f, 0f), 50f) });
            groups.Add(outside);

            groups.Add(ToyInsideGroup("toyroom_inside_empty", () => new PlayerProgress()));
            groups.Add(ToyInsideGroup("toyroom_inside_full", ToyFullProgress));

            // The kid at the mirror, from the wardrobe camera: without the menu, and with it.
            var mirror = new Group { Name = "wardrobe_mirror", Stage = () => StageWardrobe(false) };
            mirror.Shots.Add(new Shot { Name = "camera", Frame = ToyWardrobeCamera });
            groups.Add(mirror);

            var panel = new Group { Name = "wardrobe_panel", Stage = () => StageWardrobe(true) };
            panel.Shots.Add(new Shot { Name = "menu", Frame = ToyWardrobeCamera });
            groups.Add(panel);
        }

        // ----- Groups ---------------------------------------------------------------------------

        /// <summary>The inside of the room with the given progress: from the follow camera's pose, from above, and the walls one by one.</summary>
        static Group ToyInsideGroup(string name, Func<PlayerProgress> progress)
        {
            var group = new Group { Name = name, Stage = () => StageToyInside(progress()) };
            group.Shots.Add(new Shot { Name = "follow", Frame = () => ToyRoomLook(new Vector3(0f, 6.5f, -7f), new Vector3(0f, 0.8f, 0f), 50f) });
            group.Shots.Add(new Shot { Name = "top", Frame = () => ToyRoomLook(new Vector3(0f, 9f, -3.5f), new Vector3(0f, 0f, 0.2f), 55f) });
            group.Shots.Add(new Shot { Name = "west", Frame = () => ToyRoomLook(new Vector3(1.3f, 1.9f, -0.3f), new Vector3(-2.8f, 0.4f, -0.3f), 60f) });
            group.Shots.Add(new Shot { Name = "east", Frame = () => ToyRoomLook(new Vector3(-1.0f, 1.9f, -0.5f), new Vector3(2.8f, 0.4f, -0.5f), 60f) });
            group.Shots.Add(new Shot { Name = "north", Frame = () => ToyRoomLook(new Vector3(0f, 1.6f, -2.0f), new Vector3(0f, 1.4f, 2.3f), 75f) });
            return group;
        }

        /// <summary>Everything won: every non-starter item unlocked and 12 medals, all seen.</summary>
        static PlayerProgress ToyFullProgress()
        {
            var progress = new PlayerProgress { medals = 12, medalsSeen = 12 };
            RewardCatalog catalog = ToyCatalog();
            if (catalog != null)
                foreach (RewardItem item in catalog.items)
                    if (item != null && !item.starter && !string.IsNullOrEmpty(item.id)) progress.unlockedItems.Add(item.id);
            return progress;
        }

        // ----- Staging --------------------------------------------------------------------------

        static void StageToyInside(PlayerProgress progress)
        {
            HideZoneVisuals();
            HideToyRoofAndSouthWall();

            ToyShelf shelf = UnityEngine.Object.FindFirstObjectByType<ToyShelf>(FindObjectsInactive.Include);
            if (shelf == null) throw new InvalidOperationException("no ToyShelf");
            shelf.Refresh(progress);
        }

        /// <summary>The kid at the mirror with his weapons out; the room in full (so the menu has something to show); optionally the menu.</summary>
        static void StageWardrobe(bool withMenu)
        {
            HideZoneVisuals();
            HideToyRoofAndSouthWall();

            ToyShelf shelf = UnityEngine.Object.FindFirstObjectByType<ToyShelf>(FindObjectsInactive.Include);
            if (shelf != null) shelf.Refresh(ToyFullProgress());

            GameObject kid = FindKid("Player");
            GameObject stand = ToyFind("StandSpot");
            if (kid == null || stand == null) throw new InvalidOperationException("no player or no StandSpot");

            ActivateWithParents(kid);
            DisableMovers(kid);
            kid.transform.SetPositionAndRotation(stand.transform.position, stand.transform.rotation);
            var weapons = kid.GetComponent<WeaponHolder>();
            if (weapons != null) weapons.ShowWeapons(true);
            Physics.SyncTransforms();

            if (!withMenu) return;

            // The touch canvas is a screen overlay, which a camera render does not draw: show it through the camera for this picture.
            WardrobePanel panel = UnityEngine.Object.FindFirstObjectByType<WardrobePanel>(FindObjectsInactive.Include);
            if (panel == null) throw new InvalidOperationException("no WardrobePanel");

            Canvas canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                foreach (Transform child in canvas.transform)
                {
                    // Only the menu: the joystick and the rest of the touch UI would be in the way.
                    if (child != panel.transform) child.gameObject.SetActive(false);
                }
            }

            ActivateWithParents(panel.gameObject);
            panel.Show();
            panel.transform.Find("Window").localScale = Vector3.one;
            if (canvas != null) Canvas.ForceUpdateCanvases();
        }

        static void HideToyRoofAndSouthWall()
        {
            GameObject room = ToyFind(ToyRoomName);
            if (room == null) throw new InvalidOperationException("no ToyRoom");

            foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Roof" || t.name.StartsWith("Wall_South_")) t.gameObject.SetActive(false);
            }
        }

        // ----- Cameras --------------------------------------------------------------------------

        /// <summary>Looks from an offset of the room's middle (on the floor) at another offset of it.</summary>
        static void ToyRoomLook(Vector3 eyeOffset, Vector3 targetOffset, float fov)
        {
            GameObject room = ToyFind(ToyRoomName);
            if (room == null) throw new InvalidOperationException("no ToyRoom");

            Vector3 centre = room.transform.position;
            LookAt(centre + eyeOffset, centre + targetOffset, fov);
            cam.farClipPlane = 200f;
        }

        /// <summary>Like ToyRoomLook, but around the doorway.</summary>
        static void ToyDoorLook(Vector3 eyeOffset, Vector3 targetOffset, float fov)
        {
            GameObject door = ToyFind("DoorSpot");
            if (door == null) throw new InvalidOperationException("no DoorSpot");

            Vector3 centre = door.transform.position;
            LookAt(centre + eyeOffset, centre + targetOffset, fov);
            cam.farClipPlane = 200f;
        }

        /// <summary>The wardrobe camera's pose (the CameraSpot) and the main camera's field of view.</summary>
        static void ToyWardrobeCamera()
        {
            GameObject spot = ToyFind("CameraSpot");
            if (spot == null) throw new InvalidOperationException("no CameraSpot");

            cam.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
            cam.fieldOfView = 50f;
            cam.farClipPlane = 200f;
        }

        // ----- Finding things -------------------------------------------------------------------

        static GameObject ToyFind(string objectName)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Where(t => t.name == objectName && t.gameObject.scene.IsValid())
                .Select(t => t.gameObject)
                .FirstOrDefault();
        }

        /// <summary>The catalog the Toy Room's shelf was wired with.</summary>
        static RewardCatalog ToyCatalog()
        {
            ToyShelf shelf = UnityEngine.Object.FindFirstObjectByType<ToyShelf>(FindObjectsInactive.Include);
            if (shelf == null) return null;

            SerializedProperty property = new SerializedObject(shelf).FindProperty("catalog");
            return property != null ? property.objectReferenceValue as RewardCatalog : null;
        }
    }
}
