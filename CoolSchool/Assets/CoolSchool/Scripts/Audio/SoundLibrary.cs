using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Audio
{
    /// <summary>
    /// One named sound cue and the clips that can play for it. A cue with no clips is silent,
    /// so the game can call every cue before any audio pack has been imported.
    /// </summary>
    [Serializable]
    public class SoundEntry
    {
        [Tooltip("The cue name the game calls, like battle.sword_swing.")]
        public string cue;
        [Tooltip("One of these is picked at random each time.")]
        public List<AudioClip> clips = new List<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Each play picks a random pitch between these two numbers.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("3D sound at the place where it happens. Off = always plays flat, at full volume.")]
        public bool spatial = true;
        [Tooltip("Seconds before the same cue may play again, so quick repeats never pile up.")]
        public float cooldown = 0.05f;
    }

    /// <summary>
    /// The table of every sound cue in the game. Filled in once the audio packs are imported
    /// (see the CoolSchool/Audio menu). Assets/CoolSchool/Audio/SoundLibrary.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "CoolSchool/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [SerializeField] List<SoundEntry> entries = new List<SoundEntry>();

        Dictionary<string, SoundEntry> lookup;
        int lookupCount = -1;

        public List<SoundEntry> Entries => entries;

        /// <summary>The entry for a cue, or null when the cue is not in the table.</summary>
        public SoundEntry Find(string cue)
        {
            if (string.IsNullOrEmpty(cue) || entries == null) return null;

            if (lookup == null || lookupCount != entries.Count)
            {
                lookup = new Dictionary<string, SoundEntry>();
                foreach (SoundEntry entry in entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.cue)) lookup[entry.cue] = entry;
                }
                lookupCount = entries.Count;
            }

            lookup.TryGetValue(cue, out SoundEntry found);
            return found;
        }

        void OnValidate()
        {
            // Entries may have been edited in the inspector.
            lookup = null;
        }
    }
}
