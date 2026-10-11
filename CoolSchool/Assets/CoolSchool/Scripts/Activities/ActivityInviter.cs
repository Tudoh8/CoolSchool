using CoolSchool.Battle;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Activities
{
    /// <summary>
    /// Invites the kid to play. When nothing has been started for a while, the teacher says a line
    /// and a trail of sparkles leads from the player toward one of the zones. The zones take turns,
    /// and the invitation repeats for as long as the kid stays idle.
    /// </summary>
    public class ActivityInviter : MonoBehaviour
    {
        [SerializeField] ActivityZone[] zones;
        [SerializeField] float idleSecondsBeforeInvite = 15f;
        [SerializeField] float repeatSeconds = 30f;
        [SerializeField] float trailSeconds = 5f;
        [Tooltip("Voice line group for each zone, in the same order as the zones.")]
        [SerializeField] string[] inviteGroups;

        const float TrailSpacing = 1.5f;
        const float TrailRefreshSeconds = 0.7f;
        const float TrailStartDistance = 1f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;

        PlayerController player;
        float secondsUntilInvite;
        int lastInvited = -1;
        ActivityZone trailZone;
        float trailEndTime;
        float nextTrailTime;

        void OnEnable()
        {
            secondsUntilInvite = idleSecondsBeforeInvite;
        }

        void Update()
        {
            ActivityManager manager = ActivityManager.Instance;
            if (manager == null) return;

            // Anything but free play running means the kid is busy: start the wait over.
            bool idle = manager.Current == manager.DefaultActivity;
            if (!idle)
            {
                secondsUntilInvite = idleSecondsBeforeInvite;
                trailZone = null;
                return;
            }

            secondsUntilInvite -= Time.deltaTime;
            if (secondsUntilInvite <= 0f)
            {
                secondsUntilInvite = repeatSeconds;
                Invite();
            }

            if (trailZone != null)
            {
                if (Time.time >= trailEndTime) trailZone = null;
                else if (Time.time >= nextTrailTime) ShowTrail();
            }
        }

        /// <summary>Picks the zone that was invited least recently, says its line and starts the trail.</summary>
        void Invite()
        {
            if (zones == null || zones.Length == 0) return;

            for (int step = 1; step <= zones.Length; step++)
            {
                int index = (lastInvited + step) % zones.Length;
                if (zones[index] == null) continue;

                lastInvited = index;
                if (inviteGroups != null && index < inviteGroups.Length && !string.IsNullOrEmpty(inviteGroups[index]))
                    VoicePlayer.Say(inviteGroups[index]);

                trailZone = zones[index];
                trailEndTime = Time.time + trailSeconds;
                nextTrailTime = 0f;
                return;
            }
        }

        /// <summary>Sparkles every few metres along the straight line from the player toward the zone.</summary>
        void ShowTrail()
        {
            nextTrailTime = Time.time + TrailRefreshSeconds;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            Vector3 start = player.transform.position;
            Vector3 toZone = trailZone.transform.position - start;
            toZone.y = 0f;
            float distance = toZone.magnitude;
            if (distance < 0.01f) return;

            Vector3 direction = toZone / distance;
            float end = Mathf.Max(0f, distance - trailZone.Radius);
            float along = TrailStartDistance;
            for (int i = 0; i < MaxTrailPoints && along <= end; i++)
            {
                BattleEffects.Sparkle(start + direction * along + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
        }
    }
}
