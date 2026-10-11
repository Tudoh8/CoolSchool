using System.Collections.Generic;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.School
{
    /// <summary>
    /// The teacher in the classroom. She turns toward the player, talks with her hands while one of
    /// her lines is playing, and waves when asked. Needs an Animator with a "Talking" bool and a "Wave" trigger.
    /// </summary>
    public class TeacherCharacter : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 220f;
        [Tooltip("She only turns toward the player when closer than this (metres); otherwise she goes back to her spot's direction.")]
        [SerializeField] float noticeDistance = 9f;
        [Tooltip("Keeps the talking pose for a moment between two of her lines, so she does not twitch.")]
        [SerializeField] float talkLingerSeconds = 0.35f;

        static readonly int TalkingHash = Animator.StringToHash("Talking");
        static readonly int WaveHash = Animator.StringToHash("Wave");

        readonly HashSet<int> parameters = new HashSet<int>();
        PlayerController player;
        Quaternion restRotation;
        float talkingUntil;
        bool talking;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            restRotation = transform.rotation;

            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                parameters.Add(parameter.nameHash);
        }

        void Update()
        {
            UpdateFacing();
            UpdateTalking();
        }

        /// <summary>Plays the waving gesture once.</summary>
        public void Wave()
        {
            if (animator != null && parameters.Contains(WaveHash)) animator.SetTrigger(WaveHash);
        }

        void UpdateFacing()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            Quaternion target = restRotation;
            if (player != null)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f && toPlayer.sqrMagnitude <= noticeDistance * noticeDistance)
                    target = Quaternion.LookRotation(toPlayer, Vector3.up);
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        void UpdateTalking()
        {
            if (animator == null || !parameters.Contains(TalkingHash)) return;

            VoicePlayer voice = VoicePlayer.Instance;
            bool teacherSpeaking = voice != null && voice.IsSpeaking && voice.CurrentSpeaker == Speaker.Teacher;
            if (teacherSpeaking) talkingUntil = Time.time + talkLingerSeconds;

            bool shouldTalk = Time.time < talkingUntil;
            if (shouldTalk == talking) return;

            talking = shouldTalk;
            animator.SetBool(TalkingHash, talking);
        }
    }
}
