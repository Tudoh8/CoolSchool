using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Which DeepVoice voice speaks for which character, and the voice style. Editor-only.
    /// Voice names must exist in DeepVoice's Voice enum (for example Lily, Freya, Clyde); an unknown name falls back to Lily.
    /// </summary>
    public class VoiceGenerationSettings : ScriptableObject
    {
        public string teacherVoice = "Lily";
        public string narratorVoice = "Lily";
        public string kidVoice = "Freya";
        public string goblinVoice = "Clyde";
        public string alienVoice = "Freya";
        public string parrotVoice = "Clyde";
        public string announcerVoice = "Dave";

        [Range(0f, 1f)] public float variability = 0.3f;
        [Range(0f, 1f)] public float clarity = 0.75f;
    }
}
