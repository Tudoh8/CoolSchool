using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>
    /// Plays the spoken lines, one at a time. The teacher is never drowned out: while she speaks,
    /// kid and goblin lines are dropped. A line with no recording yet is logged and shown as a
    /// debug subtitle, so the game can be tested before any audio exists.
    /// </summary>
    public class VoicePlayer : MonoBehaviour
    {
        [SerializeField] LineTable table;
        [Tooltip("A 2D audio source. Created automatically when left empty.")]
        [SerializeField] AudioSource source;
        [Tooltip("Draws the spoken text on screen in the editor and in development builds.")]
        [SerializeField] bool showDebugSubtitles = true;

        [Header("Pitch per speaker")]
        [Tooltip("Raising the pitch makes the adult text-to-speech voices sound like kids and silly goblins.")]
        [SerializeField, Range(0.5f, 2f)] float teacherPitch = 1f;
        [SerializeField, Range(0.5f, 2f)] float narratorPitch = 1f;
        [SerializeField, Range(0.5f, 2f)] float kidPitch = 1.22f;
        [SerializeField, Range(0.5f, 2f)] float goblinPitch = 1.35f;
        [SerializeField, Range(0.5f, 2f)] float alienPitch = 1.5f;

        const int MaxWaiting = 2;
        const float RepeatBlockSeconds = 1.5f;
        const float SilentLineSeconds = 2f;
        const float GapSeconds = 0.25f;

        public static VoicePlayer Instance { get; private set; }

        /// <summary>True while a line is playing (or its debug subtitle is showing).</summary>
        public bool IsSpeaking => current != null;

        /// <summary>True while a line is playing or waiting in the queue.</summary>
        public bool IsBusy => current != null || waiting.Count > 0;

        /// <summary>Who says the line that is playing now. Teacher when nothing is playing (check IsSpeaking first).</summary>
        public Speaker CurrentSpeaker => current != null ? current.speaker : Speaker.Teacher;

        VoiceLine current;
        float currentEndTime;
        readonly Queue<VoiceLine> waiting = new Queue<VoiceLine>();
        readonly Dictionary<string, float> lastSaidTime = new Dictionary<string, float>();
        string subtitle = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUIStyle subtitleStyle;
#endif

        /// <summary>Says a line from the group. With interrupt, whatever is playing stops and the queue is cleared.</summary>
        public static void Say(string group, bool interrupt = false)
        {
            if (Instance != null) Instance.Request(group, interrupt);
        }

        void Awake()
        {
            Instance = this;

            if (source == null) source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (current != null && Time.unscaledTime >= currentEndTime && !IsClipStillPlaying())
                current = null;

            if (current == null && waiting.Count > 0)
                Play(waiting.Dequeue());
        }

        void Request(string group, bool interrupt)
        {
            if (table == null) return;

            VoiceLine line = table.Pick(group);
            if (line == null)
            {
                Debug.LogWarning($"[Voice] No line in group '{group}'.");
                return;
            }

            if (IsRepeat(line)) return;
            if (IsDrownedOut(line, interrupt)) return;

            lastSaidTime[line.group] = Time.unscaledTime;

            if (interrupt)
            {
                waiting.Clear();
                Play(line);
            }
            else if (current == null)
            {
                Play(line);
            }
            else if (waiting.Count < MaxWaiting)
            {
                waiting.Enqueue(line);
            }
        }

        /// <summary>The same group a moment ago: dropped, so quick repeats never pile up.</summary>
        bool IsRepeat(VoiceLine line)
        {
            return lastSaidTime.TryGetValue(line.group, out float last) && Time.unscaledTime - last < RepeatBlockSeconds;
        }

        /// <summary>While the teacher is talking, kids, goblins and aliens keep quiet.</summary>
        bool IsDrownedOut(VoiceLine line, bool interrupt)
        {
            if (interrupt || current == null || current.speaker != Speaker.Teacher) return false;
            return line.speaker == Speaker.Kid || line.speaker == Speaker.Goblin || line.speaker == Speaker.Alien;
        }

        void Play(VoiceLine line)
        {
            GameLanguage language = LanguageSettings.Current;
            string text = line.TextFor(language);
            AudioClip clip = line.ClipFor(language);

            current = line;
            subtitle = text;
            source.Stop();

            float pitch = PitchFor(line.speaker);
            source.pitch = pitch;

            if (clip != null)
            {
                source.clip = clip;
                source.Play();
                // A higher pitch also plays faster, so the clip is shorter.
                currentEndTime = Time.unscaledTime + clip.length / Mathf.Max(0.1f, pitch) + GapSeconds;
            }
            else
            {
                Debug.Log($"[Voice] {text}");
                currentEndTime = Time.unscaledTime + SilentLineSeconds;
            }
        }

        float PitchFor(Speaker speaker)
        {
            switch (speaker)
            {
                case Speaker.Kid: return kidPitch;
                case Speaker.Goblin: return goblinPitch;
                case Speaker.Alien: return alienPitch;
                case Speaker.Narrator: return narratorPitch;
                default: return teacherPitch;
            }
        }

        bool IsClipStillPlaying()
        {
            return source != null && source.isPlaying;
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showDebugSubtitles || current == null) return;

            if (subtitleStyle == null)
            {
                subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                };
            }
            subtitleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);

            float width = Screen.width * 0.7f;
            float height = Screen.height * 0.14f;
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.08f, width, height);

            // A dark outline keeps the text readable on any background.
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 2f, area.y + 2f, area.width, area.height), subtitle, subtitleStyle);
            GUI.color = Color.white;
            GUI.Label(area, subtitle, subtitleStyle);
#endif
        }
    }
}
