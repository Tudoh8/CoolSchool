using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>All the spoken lines of the game. Filled by the VoiceLineSeeder, clips linked later.</summary>
    [CreateAssetMenu(menuName = "CoolSchool/Voice/Line Table", fileName = "Lines")]
    public class LineTable : ScriptableObject
    {
        public List<VoiceLine> lines = new List<VoiceLine>();

        readonly Dictionary<string, VoiceLine> lastPicked = new Dictionary<string, VoiceLine>();
        readonly List<VoiceLine> candidates = new List<VoiceLine>();

        /// <summary>A random line from the group, not the one played last for that group. Null if the group is empty.</summary>
        public VoiceLine Pick(string group)
        {
            if (string.IsNullOrEmpty(group)) return null;

            candidates.Clear();
            lastPicked.TryGetValue(group, out VoiceLine previous);
            foreach (VoiceLine line in lines)
                if (line != null && line.group == group && line != previous) candidates.Add(line);

            // A group with a single line has nothing else to choose.
            if (candidates.Count == 0 && previous != null) candidates.Add(previous);
            if (candidates.Count == 0) return null;

            VoiceLine picked = candidates[Random.Range(0, candidates.Count)];
            lastPicked[group] = picked;
            return picked;
        }

        public VoiceLine Find(string key)
        {
            foreach (VoiceLine line in lines)
                if (line != null && line.key == key) return line;
            return null;
        }
    }
}
