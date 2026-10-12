using CoolSchool.Battle;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The friendly neighbour with the chequered flag who runs the soapbox race. Between races he stands at the start,
    /// turns toward the player when he is near and waves his flag now and then. During the race the activity moves him
    /// (MoveTo) and makes him wave the flag at the start and cheer at the finish.
    /// Uses the Teacher animator controller (a "Talking" bool and a "Wave" trigger; a "Cheer" trigger is used if it exists).
    /// </summary>
    public class RaceAnnouncer : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("The chequered flag in his right hand. Swung by hand while he waves it.")]
        [SerializeField] Transform flag;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 220f;
        [Tooltip("He turns toward the player when closer than this (metres).")]
        [SerializeField] float noticeDistance = 6f;
        [Tooltip("Seconds between two flag waves while he is just standing there.")]
        [SerializeField] float idleWaveSeconds = 8f;
        [SerializeField] float flagSwingSeconds = 2f;
        [SerializeField] float flagSwingDegrees = 35f;
        [Tooltip("Keeps the talking pose for a moment between two of his lines, so he does not twitch.")]
        [SerializeField] float talkLingerSeconds = 0.35f;

        const float ManualFacingSeconds = 5f;
        const float FlagSwingHz = 2.2f;

        static readonly int TalkingHash = Animator.StringToHash("Talking");
        static readonly int WaveHash = Animator.StringToHash("Wave");
        static readonly int CheerHash = Animator.StringToHash("Cheer");

        PlayerController player;
        Quaternion restRotation;
        Quaternion flagRestRotation;
        bool hasFlagRest;

        bool hasManualFacing;
        Vector3 manualPoint;
        float manualUntil;

        float swingUntil = -1f;
        float nextIdleWave;
        float talkingUntil;
        bool talking;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            restRotation = transform.rotation;
            if (flag != null)
            {
                flagRestRotation = flag.localRotation;
                hasFlagRest = true;
            }
            nextIdleWave = Time.time + idleWaveSeconds * 0.5f;
        }

        void Update()
        {
            UpdateFacing();
            UpdateTalking();
            UpdateIdleWave();
            UpdateFlagSwing();
        }

        /// <summary>Turns him smoothly toward a point (flat), for a few seconds. After that he goes back to looking at the player when near.</summary>
        public void FaceTowards(Vector3 point)
        {
            hasManualFacing = true;
            manualPoint = point;
            manualUntil = Time.time + ManualFacingSeconds;
        }

        /// <summary>Swings the flag for about two seconds, with a wave of the arm.</summary>
        public void WaveFlag()
        {
            swingUntil = Time.time + flagSwingSeconds;
            nextIdleWave = Time.time + idleWaveSeconds;
            Trigger(WaveHash);
        }

        /// <summary>A happy cheer: the cheer move if the controller has one, otherwise a wave, and the flag swings.</summary>
        public void Cheer()
        {
            swingUntil = Time.time + flagSwingSeconds;
            nextIdleWave = Time.time + idleWaveSeconds;
            if (!Trigger(CheerHash)) Trigger(WaveHash);
        }

        /// <summary>Stands him on the spot (position and facing) with a sparkle where he leaves and where he arrives.</summary>
        public void MoveTo(Transform spot)
        {
            if (spot == null) return;

            BattleEffects.Sparkle(transform.position + Vector3.up);
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            restRotation = spot.rotation;
            hasManualFacing = false;
            BattleEffects.Sparkle(spot.position + Vector3.up);
        }

        void UpdateFacing()
        {
            Quaternion target = restRotation;

            if (hasManualFacing && Time.time < manualUntil)
            {
                if (TryLookRotation(manualPoint, out Quaternion look)) target = look;
            }
            else
            {
                hasManualFacing = false;
                if (player == null) player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    Vector3 toPlayer = player.transform.position - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f && toPlayer.sqrMagnitude <= noticeDistance * noticeDistance)
                        target = Quaternion.LookRotation(toPlayer, Vector3.up);
                }
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        bool TryLookRotation(Vector3 point, out Quaternion rotation)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
            {
                rotation = transform.rotation;
                return false;
            }

            rotation = Quaternion.LookRotation(flat, Vector3.up);
            return true;
        }

        void UpdateTalking()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            VoicePlayer voice = VoicePlayer.Instance;
            bool speaking = voice != null && voice.IsSpeaking && voice.CurrentSpeaker == Speaker.Announcer;
            if (speaking) talkingUntil = Time.time + talkLingerSeconds;

            bool shouldTalk = Time.time < talkingUntil;
            if (shouldTalk == talking) return;

            talking = shouldTalk;
            if (HasParameter(TalkingHash)) animator.SetBool(TalkingHash, talking);
        }

        void UpdateIdleWave()
        {
            if (Time.time < nextIdleWave) return;
            WaveFlag();
        }

        void UpdateFlagSwing()
        {
            if (flag == null || !hasFlagRest) return;

            if (Time.time >= swingUntil)
            {
                flag.localRotation = flagRestRotation;
                return;
            }

            // Fades in and out at both ends of the swing.
            float t = 1f - (swingUntil - Time.time) / Mathf.Max(0.1f, flagSwingSeconds);
            float envelope = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            float angle = Mathf.Sin(Time.time * FlagSwingHz * Mathf.PI * 2f) * flagSwingDegrees * envelope;
            flag.localRotation = flagRestRotation * Quaternion.Euler(0f, 0f, angle);
        }

        bool Trigger(int id)
        {
            if (!HasParameter(id)) return false;
            animator.SetTrigger(id);
            return true;
        }

        bool HasParameter(int id)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == id) return true;
            return false;
        }
    }
}
