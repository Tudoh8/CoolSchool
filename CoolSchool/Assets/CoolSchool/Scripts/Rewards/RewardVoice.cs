namespace CoolSchool.Rewards
{
    /// <summary>The few places where rewards talk to the voice system, kept together.</summary>
    static class RewardVoice
    {
        public static void Say(string group)
        {
            CoolSchool.Voice.VoicePlayer.Say(group);
        }

        /// <summary>True while a line is playing or queued.</summary>
        public static bool IsBusy()
        {
            CoolSchool.Voice.VoicePlayer voice = CoolSchool.Voice.VoicePlayer.Instance;
            return voice != null && voice.IsBusy;
        }

        public static bool IsSpeaking()
        {
            CoolSchool.Voice.VoicePlayer voice = CoolSchool.Voice.VoicePlayer.Instance;
            return voice != null && voice.IsSpeaking;
        }
    }
}
