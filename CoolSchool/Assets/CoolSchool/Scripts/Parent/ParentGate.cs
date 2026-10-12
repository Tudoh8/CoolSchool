using UnityEngine;

namespace CoolSchool.Parent
{
    /// <summary>
    /// True while the parent panel or the "do you want to stop?" prompt is open. The game is frozen then (time and audio), but
    /// scripts that read the pointer or the keyboard directly in Update would still see taps and key presses, so gameplay
    /// scripts check InputBlocked and ignore input while it is set.
    /// </summary>
    public static class ParentGate
    {
        /// <summary>True while the parent panel is open.</summary>
        public static bool IsOpen { get; internal set; }

        /// <summary>True while the quit prompt (back button, then check or cross) is open.</summary>
        public static bool QuitPromptOpen { get; internal set; }

        /// <summary>What gameplay input polling checks: the parent panel or the quit prompt is covering the game.</summary>
        public static bool InputBlocked => IsOpen || QuitPromptOpen;

        // Static data survives Play mode when domain reload is switched off, so start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            QuitPromptOpen = false;
        }
    }
}
