using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// One zoo animal (a polyperfect model as a child). Everything it does is scripted, no agent and no physics: it can be hidden,
    /// put somewhere (sunk into a bin, for instance), hop along an arc, step towards a point (the conga line, the pen's gate) and call.
    /// The root stays active all the time; "hidden" only switches the model off. The Animator's yes/no parameters (isWalking,
    /// isRunning) differ per animal, so only those it really has are set.
    /// </summary>
    public class ZooAnimal : MonoBehaviour
    {
        [SerializeField] string kindId;
        [Tooltip("The polyperfect Animator (isWalking / isRunning yes/no parameters, set only if they exist).")]
        [SerializeField] Animator animator;
        [Tooltip("The child that bobs, sinks and hops. Switched off when the animal is hidden.")]
        [SerializeField] Transform model;
        [SerializeField] float height = 1f;
        [Tooltip("3D source for the call and the giggle.")]
        [SerializeField] AudioSource voice;

        const float TurnSpeed = 540f;
        const float RunPitchSpeed = 3.2f;
        const float VoiceMinDistance = 2f;
        const float VoiceMaxDistance = 14f;
        const float BigPitch = 0.8f;
        const float SmallPitch = 1.3f;

        // How long each kind is, as a multiple of its height (for the conga spacing and for standing behind a cover).
        static readonly Dictionary<string, float> LengthByHeight = new Dictionary<string, float>
        {
            { "elephant", 1.5f }, { "giraffe", 0.45f }, { "monkey", 0.55f }, { "penguin", 0.5f }, { "hippo", 2f },
            { "lion", 1.6f }, { "zebra", 1.4f }, { "crocodile", 4f }, { "flamingo", 0.4f }, { "tiger", 1.8f },
        };

        HashSet<string> flags;
        bool flagsRead;
        Vector3 modelBasePosition;
        bool baseCaptured;
        float sink;
        bool visible;
        Coroutine motion;

        public string KindId => kindId;

        public float Height => height;

        /// <summary>How far the animal stretches front to back, in metres (an estimate from its kind).</summary>
        public float Length => Mathf.Max(0.3f, height * (LengthByHeight.TryGetValue(kindId ?? string.Empty, out float factor) ? factor : 1f));

        /// <summary>True while the model is switched on.</summary>
        public bool IsVisible => visible;

        /// <summary>How deep the model is sunk, as a fraction of its height.</summary>
        public float Sink => sink;

        void Awake()
        {
            Capture();
        }

        void Capture()
        {
            if (baseCaptured) return;

            baseCaptured = true;
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
            if (animator == null && model != null) animator = model.GetComponentInChildren<Animator>(true);
            if (model != null)
            {
                modelBasePosition = model.localPosition;
                visible = model.gameObject.activeSelf;
            }
            EnsureVoice();
        }

        void EnsureVoice()
        {
            if (voice == null)
            {
                voice = GetComponent<AudioSource>();
                if (voice == null) voice = gameObject.AddComponent<AudioSource>();
            }
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = VoiceMinDistance;
            voice.maxDistance = VoiceMaxDistance;
        }

        // ----- Showing and hiding ------------------------------------------------------------------

        /// <summary>The model off, no sound. The animal stays where it is.</summary>
        public void Hide()
        {
            Capture();
            StopMotion();
            SetMoving(false, false);
            if (model != null) model.gameObject.SetActive(false);
            visible = false;
        }

        /// <summary>The model on, standing at this pose, sunk into the ground by sink times its height.</summary>
        public void ShowAt(Vector3 position, Quaternion rotation, float sinkFraction)
        {
            Capture();
            StopMotion();
            transform.SetPositionAndRotation(position, rotation);
            if (model != null) model.gameObject.SetActive(true);
            visible = true;
            SetSink(sinkFraction);
        }

        /// <summary>Sinks (or lifts) the model by a fraction of its height. 0 stands on the ground.</summary>
        public void SetSink(float sinkFraction)
        {
            Capture();
            sink = sinkFraction;
            if (model != null) model.localPosition = modelBasePosition + Vector3.down * (sinkFraction * height);
        }

        // ----- Moving ------------------------------------------------------------------------------

        /// <summary>Hops to the pose along an arc over that many seconds. The animal stands up out of any sink on the way.</summary>
        public void HopTo(Vector3 position, Quaternion rotation, float seconds, Action done = null)
        {
            Capture();
            StopMotion();
            if (model != null) model.gameObject.SetActive(true);
            visible = true;

            if (!isActiveAndEnabled)
            {
                transform.SetPositionAndRotation(position, rotation);
                SetSink(0f);
                done?.Invoke();
                return;
            }
            motion = StartCoroutine(HopRoutine(position, rotation, Mathf.Max(0.05f, seconds), done));
        }

        IEnumerator HopRoutine(Vector3 end, Quaternion endRotation, float seconds, Action done)
        {
            Vector3 start = transform.position;
            Quaternion startRotation = transform.rotation;
            float startSink = sink;
            float arc = Mathf.Clamp(0.25f + 0.2f * height, 0.3f, 0.9f);

            SetMoving(false, false);
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / seconds;
                float eased = Mathf.SmoothStep(0f, 1f, k);
                transform.position = Vector3.Lerp(start, end, eased) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arc);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                SetSink(Mathf.Lerp(startSink, 0f, Mathf.Clamp01(k * 3f)));
                yield return null;
            }

            transform.SetPositionAndRotation(end, endRotation);
            SetSink(0f);
            motion = null;
            done?.Invoke();
        }

        /// <summary>
        /// Steps a little towards the point (call it every frame), turning to face the way it goes, and walks or runs in the
        /// Animator. When it has arrived it stands still. No agent: a straight line.
        /// </summary>
        public void WalkTo(Vector3 position, float speed)
        {
            Capture();
            if (!visible) return;

            Vector3 flat = position - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < 0.05f)
            {
                SetMoving(false, false);
                return;
            }

            float step = Mathf.Min(distance, Mathf.Max(0f, speed) * Time.deltaTime);
            Vector3 next = transform.position + flat / distance * step;
            // Follow the height of the target softly (the ground may slope a little).
            next.y = Mathf.MoveTowards(transform.position.y, position.y, 4f * Time.deltaTime);
            transform.position = next;

            Quaternion wanted = Quaternion.LookRotation(flat / distance, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, TurnSpeed * Time.deltaTime);
            SetMoving(true, speed >= RunPitchSpeed);
        }

        /// <summary>Turns on the spot towards a direction (flat). Returns true when it faces that way.</summary>
        public bool TurnTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, TurnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, wanted) < 3f;
        }

        void StopMotion()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
        }

        /// <summary>The Animator's isWalking / isRunning (those it has).</summary>
        public void SetMoving(bool walking, bool running)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            if (!flagsRead)
            {
                flags = new HashSet<string>();
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Bool) flags.Add(parameter.name);
                flagsRead = true;
            }

            if (flags.Contains("isWalking")) animator.SetBool("isWalking", walking);
            if (flags.Contains("isRunning")) animator.SetBool("isRunning", running);
            if (flags.Contains("isJumping")) animator.SetBool("isJumping", false);
        }

        // ----- Sounds and happy moments ------------------------------------------------------------

        /// <summary>The animal's own call (cue zoo.call.id), or a giggle pitched by its size when there is no recording yet.</summary>
        public void Call()
        {
            Capture();
            if (Sfx.TryPickClip("zoo.call." + kindId, out AudioClip clip, out float volume, out float pitch))
            {
                Play(clip, volume, pitch);
                return;
            }
            Giggle();
        }

        /// <summary>A giggle, deeper for a big animal and squeakier for a small one.</summary>
        public void Giggle()
        {
            Capture();
            if (!Sfx.TryPickClip("zoo.giggle", out AudioClip clip, out float volume, out float pitch)) return;

            float size = Mathf.InverseLerp(0.5f, 3f, height);
            Play(clip, volume, pitch * Mathf.Lerp(SmallPitch, BigPitch, size));
        }

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (voice == null || !isActiveAndEnabled) return;

            voice.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * Sfx.SfxVolume));
        }

        /// <summary>A little happy hop on the spot.</summary>
        public void HappyHop()
        {
            // Not in the middle of another hop (it would land where that one was in the air).
            if (!visible || motion != null) return;

            HopTo(transform.position, transform.rotation, 0.45f);
        }
    }
}
