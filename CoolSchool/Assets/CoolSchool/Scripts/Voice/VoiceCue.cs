using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>
    /// A tiny bridge so UnityEvents set up in the editor can trigger voice lines.
    /// Builders add persistent listeners that call these methods with a line group.
    /// </summary>
    public class VoiceCue : MonoBehaviour
    {
        public void Say(string group)
        {
            VoicePlayer.Say(group);
        }

        public void SayInterrupt(string group)
        {
            VoicePlayer.Say(group, true);
        }

        /// <summary>For KnightsGame.onWaveStarted: the first wave announces the goblins, later waves say "even more".</summary>
        public void SayWave(int wave)
        {
            VoicePlayer.Say(wave <= 1 ? "knights.wave" : "knights.wave_more");
        }
    }
}
