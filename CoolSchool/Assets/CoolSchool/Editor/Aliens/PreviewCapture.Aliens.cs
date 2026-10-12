using System.Collections.Generic;
using System.Linq;
using CoolSchool.Aliens;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>The alien field shots of PreviewCapture: an overview, the follow view from the zone, a tree with its trampoline,
    /// the open-field trampoline with the bubble, a cover, and the landed rocket with its trampoline and a baby on the nose.</summary>
    public static partial class PreviewCapture
    {
        static void AddAlienGroups(List<Group> groups)
        {
            var field = new Group { Name = "aliens_field", Stage = () => { } };
            field.Shots.Add(new Shot { Name = "overview", Frame = () => AlienLook("AlienField", new Vector3(3f, 20f, -20f), new Vector3(-1f, 0f, 3f), 60f) });
            field.Shots.Add(new Shot { Name = "from_zone", Frame = () => AlienLookAt("AlienZoneProbe", new Vector3(0f, 5.5f, -8f), new Vector3(-3f, 1f, 8f), 50f) });
            field.Shots.Add(new Shot { Name = "tree", Frame = () => AlienLookAt(FirstName<AlienRescueSite>(s => s.Kind == RescueKind.Tree), new Vector3(0f, 2.8f, -8f), new Vector3(0f, 2.5f, 0f), 50f) });
            field.Shots.Add(new Shot { Name = "cover", Frame = () => AlienLookAt(FirstName<AlienCover>(c => true), new Vector3(1.5f, 2.5f, -6f), new Vector3(0f, 0.8f, 0f), 45f) });
            groups.Add(field);

            var bubble = new Group { Name = "aliens_bubble", Stage = StageAlienBubble };
            bubble.Shots.Add(new Shot { Name = "trampoline", Frame = () => AlienLookAt(FirstName<AlienRescueSite>(s => s.Kind == RescueKind.Bubble), new Vector3(0f, 2.6f, -8f), new Vector3(0f, 2.2f, 0f), 50f) });
            groups.Add(bubble);

            var rocket = new Group { Name = "aliens_rocket", Stage = StageAlienRocket };
            rocket.Shots.Add(new Shot { Name = "front", Frame = () => AlienLookAt("Rocket", new Vector3(0f, 3.5f, -11f), new Vector3(0f, 2.8f, 0f), 50f) });
            rocket.Shots.Add(new Shot { Name = "side", Frame = () => AlienLookAt("Rocket", new Vector3(9f, 3f, -3f), new Vector3(0f, 2.8f, -1f), 50f) });
            groups.Add(rocket);
        }

        // ----- Cameras -----

        /// <summary>Looks from an offset of a named object's position at an offset of it (both in world axes).</summary>
        static void AlienLookAt(string objectName, Vector3 eyeOffset, Vector3 targetOffset, float fov)
        {
            Vector3 centre = AlienPosition(objectName);
            LookAt(centre + eyeOffset, centre + targetOffset, fov);
            cam.farClipPlane = 300f;
        }

        /// <summary>Like AlienLookAt, but around the middle of the named object's renderers (the whole field).</summary>
        static void AlienLook(string objectName, Vector3 eyeOffset, Vector3 targetOffset, float fov)
        {
            GameObject found = FindIncludingInactive(objectName);
            Vector3 centre = found != null && PlaygroundPrototypeBuilder.TryGetBounds(found, out Bounds bounds) ? bounds.center : Vector3.zero;
            centre.y = 0f;
            LookAt(centre + eyeOffset, centre + targetOffset, fov);
            cam.farClipPlane = 300f;
        }

        static Vector3 AlienPosition(string objectName)
        {
            if (objectName == "AlienZoneProbe")
            {
                AlienLanding landing = Object.FindObjectsByType<AlienLanding>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                Transform zone = landing != null ? landing.transform : null;
                return zone != null ? zone.position : Vector3.zero;
            }

            GameObject found = FindIncludingInactive(objectName);
            return found != null ? found.transform.position : Vector3.zero;
        }

        static string FirstName<T>(System.Func<T, bool> wanted) where T : Component
        {
            T found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).FirstOrDefault(wanted);
            if (found == null) return null;

            // A rescue site is a point; its pad is where the trampoline stands.
            if (found is AlienRescueSite site && site.Pad != null) return site.Pad.name;
            return found.name;
        }

        static GameObject FindIncludingInactive(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Where(t => t.name == objectName && t.gameObject.scene.IsValid())
                .Select(t => t.gameObject)
                .FirstOrDefault();
        }

        // ----- Staging -----

        /// <summary>The bubble floats at its home point above the open-field trampoline with a baby inside.</summary>
        static void StageAlienBubble()
        {
            AlienRescueSite site = Object.FindObjectsByType<AlienRescueSite>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(s => s.Kind == RescueKind.Bubble);
            if (site == null || site.Bubble == null) return;

            site.Bubble.transform.position = site.BubbleCentre();
            site.Bubble.gameObject.SetActive(true);
            PutBabyAt(site.Bubble.Seat);
        }

        /// <summary>The rocket stands at its landing spot with its trampoline out and a baby on the nose.</summary>
        static void StageAlienRocket()
        {
            GameObject rocket = FindIncludingInactive("Rocket");
            GameObject landing = FindIncludingInactive("RocketLanding");
            if (rocket == null || landing == null) return;

            rocket.transform.position = landing.transform.position;
            rocket.SetActive(true);

            GameObject pad = FindIncludingInactive("Trampoline_Rocket");
            if (pad != null) pad.SetActive(true);

            GameObject perch = FindIncludingInactive("RocketPerch");
            if (perch != null) PutBabyAt(perch.transform);
        }

        static void PutBabyAt(Transform seat)
        {
            if (seat == null) return;

            RescueTarget baby = Object.FindObjectsByType<RescueTarget>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).FirstOrDefault();
            if (baby == null) return;

            baby.transform.SetPositionAndRotation(seat.position, seat.rotation);
            baby.gameObject.SetActive(true);
        }
    }
}
