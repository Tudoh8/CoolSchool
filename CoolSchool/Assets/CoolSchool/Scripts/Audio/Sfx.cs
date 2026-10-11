using System.Collections.Generic;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.Audio;

namespace CoolSchool.Audio
{
    /// <summary>
    /// Plays every sound effect and the music. One per scene. The game calls cues by name
    /// (Sfx.Play("battle.sword_swing")); a cue with no clips is silent, and in the editor
    /// it logs its name once so missing sounds are easy to spot.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        [SerializeField] SoundLibrary library;
        [Tooltip("How many sounds can play at the same time.")]
        [SerializeField] int poolSize = 12;
        [Tooltip("Optional mixer group for all sounds and music.")]
        [SerializeField] AudioMixerGroup output;

        const string MusicVolumeKey = "audio.music_volume";
        const string SfxVolumeKey = "audio.sfx_volume";
        const float DuckedMusicLevel = 0.4f;
        const float DuckSpeed = 4f;
        const float DefaultFadeSeconds = 1.5f;
        const float MinDistance = 3f;
        const float MaxDistance = 40f;

        /// <summary>One of the two music sources. They swap places to crossfade.</summary>
        class MusicChannel
        {
            public AudioSource source;
            public float level;
            public float target;
            public float fadeSeconds = DefaultFadeSeconds;
            public float entryVolume = 1f;
            public string cue;
        }

        static Sfx instance;
        static float musicVolume = -1f;
        static float sfxVolume = -1f;

        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, float> lastPlayedTime = new Dictionary<string, float>();
        readonly MusicChannel[] channels = new MusicChannel[2];
        int nextPoolIndex;
        int activeChannel;
        float duck = 1f;
#if UNITY_EDITOR
        readonly HashSet<string> reportedCues = new HashSet<string>();
#endif

        /// <summary>Music loudness from 0 to 1, saved for the parent area.</summary>
        public static float MusicVolume
        {
            get
            {
                if (musicVolume < 0f) musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
                return musicVolume;
            }
            set
            {
                musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            }
        }

        /// <summary>Sound effect loudness from 0 to 1, saved for the parent area.</summary>
        public static float SfxVolume
        {
            get
            {
                if (sfxVolume < 0f) sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
                return sfxVolume;
            }
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            musicVolume = -1f;
            sfxVolume = -1f;
        }

        /// <summary>
        /// Plays a cue. With a position (and a spatial cue) it is heard from that place, otherwise it plays flat.
        /// Does nothing when the scene has no Sfx object or the cue has no clips yet.
        /// </summary>
        public static void Play(string cue, Vector3? position = null)
        {
            if (instance != null) instance.PlayCue(cue, position);
        }

        /// <summary>Starts looping music with a crossfade from whatever was playing.</summary>
        public static void PlayMusic(string cue)
        {
            if (instance != null) instance.StartMusic(cue);
        }

        /// <summary>Fades the music out.</summary>
        public static void StopMusic(float fade)
        {
            if (instance != null) instance.FadeOutMusic(fade);
        }

        void Awake()
        {
            instance = this;

            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
                pool.Add(CreateSource("Sfx " + i, false));

            for (int i = 0; i < channels.Length; i++)
                channels[i] = new MusicChannel { source = CreateSource("Music " + i, true) };
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            UpdateDuck();
            UpdateMusic();
        }

        AudioSource CreateSource(string sourceName, bool music)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = music;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
            return source;
        }

        // ---- Sound effects ----

        void PlayCue(string cue, Vector3? position)
        {
            if (string.IsNullOrEmpty(cue)) return;

            SoundEntry entry = library != null ? library.Find(cue) : null;
            AudioClip clip = PickClip(entry);
            if (clip == null)
            {
                ReportMissing(cue, entry != null);
                return;
            }

            if (lastPlayedTime.TryGetValue(cue, out float last) && Time.unscaledTime - last < entry.cooldown) return;
            lastPlayedTime[cue] = Time.unscaledTime;

            AudioSource source = NextSource();
            bool is3D = entry.spatial && position.HasValue;
            if (is3D) source.transform.position = position.Value;

            source.Stop();
            source.clip = clip;
            source.volume = entry.volume * SfxVolume;
            source.pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
            source.spatialBlend = is3D ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.Play();
        }

        static AudioClip PickClip(SoundEntry entry)
        {
            if (entry == null || entry.clips == null || entry.clips.Count == 0) return null;

            // Skip empty slots, so a half-filled list still works.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                AudioClip clip = entry.clips[Random.Range(0, entry.clips.Count)];
                if (clip != null) return clip;
            }
            foreach (AudioClip clip in entry.clips)
                if (clip != null) return clip;
            return null;
        }

        /// <summary>A free source, or the next one in line when all are busy.</summary>
        AudioSource NextSource()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int index = (nextPoolIndex + i) % pool.Count;
                if (!pool[index].isPlaying)
                {
                    nextPoolIndex = (index + 1) % pool.Count;
                    return pool[index];
                }
            }

            AudioSource stolen = pool[nextPoolIndex];
            nextPoolIndex = (nextPoolIndex + 1) % pool.Count;
            return stolen;
        }

        void ReportMissing(string cue, bool known)
        {
#if UNITY_EDITOR
            if (reportedCues.Add(cue))
                Debug.Log(known ? $"[Sfx] {cue}" : $"[Sfx] {cue} (not in the sound library)");
#endif
        }

        // ---- Music ----

        void StartMusic(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return;

            MusicChannel current = channels[activeChannel];
            if (current.cue == cue && current.target > 0f) return;

            SoundEntry entry = library != null ? library.Find(cue) : null;
            AudioClip clip = PickClip(entry);

            // The old music fades out either way.
            current.target = 0f;
            current.fadeSeconds = DefaultFadeSeconds;

            if (clip == null)
            {
                current.cue = null;
                ReportMissing(cue, entry != null);
                return;
            }

            activeChannel = 1 - activeChannel;
            MusicChannel next = channels[activeChannel];
            next.cue = cue;
            next.entryVolume = entry.volume;
            next.level = 0f;
            next.target = 1f;
            next.fadeSeconds = DefaultFadeSeconds;
            next.source.Stop();
            next.source.clip = clip;
            next.source.pitch = 1f;
            next.source.volume = 0f;
            next.source.Play();
        }

        void FadeOutMusic(float fade)
        {
            foreach (MusicChannel channel in channels)
            {
                channel.target = 0f;
                channel.fadeSeconds = Mathf.Max(0.01f, fade);
                channel.cue = null;
            }
        }

        /// <summary>Music gets quieter while someone is speaking, so the spoken lines are always clear.</summary>
        void UpdateDuck()
        {
            VoicePlayer voice = VoicePlayer.Instance;
            float wanted = voice != null && voice.IsSpeaking ? DuckedMusicLevel : 1f;
            duck = Mathf.MoveTowards(duck, wanted, DuckSpeed * Time.unscaledDeltaTime);
        }

        void UpdateMusic()
        {
            foreach (MusicChannel channel in channels)
            {
                if (!channel.source.isPlaying) continue;

                float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, channel.fadeSeconds);
                channel.level = Mathf.MoveTowards(channel.level, channel.target, step);
                channel.source.volume = channel.level * channel.entryVolume * MusicVolume * duck;

                if (channel.level <= 0f && channel.target <= 0f) channel.source.Stop();
            }
        }
    }
}
