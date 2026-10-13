using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The touch (and mouse) part of the castle moments, like the pirates' coin picker but for any 3D thing: a press picks the
    /// registered target whose middle is nearest to the finger on the screen, as long as it is within a finger's reach, so small
    /// fingers never have to be precise. Only works while Armed and while the parent panel is closed. One of these lives in the
    /// scene; each moment sets the targets it wants, listens to Tapped, and lets go of both when it is done.
    /// </summary>
    public class WorldTapPicker : MonoBehaviour
    {
        [SerializeField] Camera cam;

        // How close (in pixels) a press must be to a target. Scaled up on screens with many dots per inch, so it is a finger-sized
        // distance on a tablet too; never smaller than this on a low-resolution editor window.
        const float PickPixels = 110f;
        const float ReferenceDpi = 160f;
        const float MaxDpiScale = 2.5f;

        /// <summary>True while presses pick targets.</summary>
        public bool Armed { get; set; }

        /// <summary>Raised once for every press that picked a target.</summary>
        public event Action<Transform> Tapped;

        /// <summary>
        /// Optional: presses for which this returns true are not taps on the world (a press on an on-screen button, for example).
        /// The moment that sets it must clear it again.
        /// </summary>
        public Func<Vector2, bool> IgnorePress { get; set; }

        readonly List<Transform> targets = new List<Transform>();

        /// <summary>Replaces the list of things that can be tapped. The middle of a target is the middle of its pictures.</summary>
        public void SetTargets(IEnumerable<Transform> newTargets)
        {
            targets.Clear();
            if (newTargets == null) return;

            foreach (Transform target in newTargets)
                if (target != null) targets.Add(target);
        }

        /// <summary>Takes one thing off the list (it was picked and is flying away).</summary>
        public void Remove(Transform target)
        {
            targets.Remove(target);
        }

        /// <summary>Forgets all targets, the ignore rule and stops picking.</summary>
        public void Clear()
        {
            targets.Clear();
            IgnorePress = null;
            Armed = false;
        }

        void OnDisable()
        {
            Armed = false;
        }

        void Update()
        {
            if (!Armed || targets.Count == 0) return;

            // A tap on the parent panel (or the stop question) is not a tap on the world.
            if (CoolSchool.Parent.ParentGate.InputBlocked) return;

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 press = pointer.position.ReadValue();
            if (IgnorePress != null && IgnorePress(press)) return;

            Transform picked = Nearest(press);
            if (picked != null) Tapped?.Invoke(picked);
        }

        /// <summary>The target whose middle is nearest to the press, if it is within a finger's reach.</summary>
        Transform Nearest(Vector2 press)
        {
            Camera view = cam != null ? cam : Camera.main;
            if (view == null) return null;

            float best = PickRadius();
            Transform nearest = null;
            foreach (Transform target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;

                Vector3 point = view.WorldToScreenPoint(CastleMotion.CenterOf(target));
                if (point.z <= 0f) continue;

                float distance = Vector2.Distance(press, point);
                if (distance >= best) continue;

                best = distance;
                nearest = target;
            }
            return nearest;
        }

        static float PickRadius()
        {
            float dpi = Screen.dpi;
            float scale = dpi > 0f ? Mathf.Clamp(dpi / ReferenceDpi, 1f, MaxDpiScale) : 1f;
            return PickPixels * scale;
        }
    }
}
