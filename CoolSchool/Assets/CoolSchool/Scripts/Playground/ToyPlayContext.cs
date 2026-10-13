using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Characters;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>Handed to ToyRoutine.Play. Wraps the player and the helpers every routine needs: hopping on and off, poses, voice, sound, waiting.</summary>
    public sealed class ToyPlayContext
    {
        const float MountTimeoutSeconds = 1.6f;
        const float DismountTimeoutSeconds = 2.0f;

        public PlayerController Player { get; }
        public KidAnimator Kid { get; }
        public PlaygroundToy Toy { get; }

        /// <summary>True from the moment he pushes the stick after having let go of it. The routine then goes straight to HopOff.</summary>
        public bool SkipRequested { get; internal set; }

        /// <summary>True once MarkPlayed was called: the sticker is earned.</summary>
        public bool Played { get; private set; }

        /// <summary>Whether HopOff ends with a cheer (when not skipped). A routine that plays another move on landing turns this off.</summary>
        public bool CheerOnLand { get; set; } = true;

        internal ToyPlayContext(PlayerController player, KidAnimator kid, PlaygroundToy toy)
        {
            Player = player;
            Kid = kid;
            Toy = toy;
        }

        /// <summary>RideOn and wait until he is mounted (about 0.45 s).</summary>
        public IEnumerator HopOnto(Transform anchor)
        {
            if (anchor == null || Player == null) yield break;

            Player.RideOn(anchor);
            float timeout = Time.time + MountTimeoutSeconds;
            while (Player != null && Player.transform.parent != anchor && Time.time < timeout) yield return null;
        }

        /// <summary>Pose None, hop off to the ground point and wait until he walks again. Cheers on landing unless skipped.</summary>
        public IEnumerator HopOff(Vector3 groundPoint)
        {
            if (Player == null) yield break;

            SetPose(ToyPose.None);
            if (!Player.IsRiding) yield break;

            Player.StopRiding(groundPoint);
            float timeout = Time.time + DismountTimeoutSeconds;
            while (Player != null && Player.IsRiding && Time.time < timeout) yield return null;

            if (CheerOnLand && !SkipRequested && Kid != null) Kid.PlayCheer();
        }

        public void SetPose(ToyPose pose)
        {
            if (Kid != null) Kid.SetToyPose(pose);
        }

        /// <summary>The fun part has started: the sticker is earned, also when he skips the rest.</summary>
        public void MarkPlayed()
        {
            Played = true;
        }

        /// <summary>Says a line of the group (the Kid's "whee", for example), with a random chance from 0 to 1.</summary>
        public void Say(string group, float chance = 1f)
        {
            if (string.IsNullOrEmpty(group)) return;
            if (chance < 1f && Random.value > chance) return;

            VoicePlayer.Say(group);
        }

        /// <summary>Plays a sound cue where the kid is.</summary>
        public void Cue(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return;

            Sfx.Play(cue, Player != null ? Player.transform.position : (Vector3?)null);
        }

        /// <summary>Waits, but returns early when he has pushed the stick to skip.</summary>
        public IEnumerator Wait(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds && !SkipRequested; elapsed += Time.deltaTime)
                yield return null;
        }
    }
}
