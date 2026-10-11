using System;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>Who is talking. The teacher is never talked over by the kids or the goblins.</summary>
    public enum Speaker
    {
        Teacher,
        Kid,
        Goblin,
        Narrator,
    }

    /// <summary>One spoken sentence, in every language, with its recorded clips once they exist.</summary>
    [Serializable]
    public class VoiceLine
    {
        [Tooltip("Unique key, for example tag.start.1")]
        public string key;
        [Tooltip("Lines in the same group are alternatives, for example tag.start")]
        public string group;
        public Speaker speaker;
        [TextArea(1, 3)] public string dutch;
        [TextArea(1, 3)] public string english;
        public AudioClip dutchClip;
        public AudioClip englishClip;

        /// <summary>The sentence in this language. Falls back to Dutch when a translation is missing.</summary>
        public string TextFor(GameLanguage language)
        {
            if (language == GameLanguage.English && !string.IsNullOrEmpty(english)) return english;
            return dutch;
        }

        /// <summary>The recording in this language, or null when it has not been recorded yet.</summary>
        public AudioClip ClipFor(GameLanguage language)
        {
            if (language == GameLanguage.English) return englishClip;
            return dutchClip;
        }
    }
}
