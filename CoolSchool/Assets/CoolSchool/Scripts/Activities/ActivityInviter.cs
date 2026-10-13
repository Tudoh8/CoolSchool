using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

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
        [Tooltip("The Toy Room's doorway. When something new waits there (a new toy or medal), the next invitation leads to it.")]
        [SerializeField] Transform toyRoomSpot;

        const float TrailSpacing = 1.5f;
        const float TrailRefreshSeconds = 0.7f;
        const float TrailStartDistance = 1f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;
        const float NavSampleDistance = 2f;
        // The trail to the Toy Room stops this far before its doorway.
        const float ToyRoomTrailStop = 1f;
        const string RaceInviteGroup = "invite.race";

        PlayerController player;
        NavMeshPath trailPath;
        float secondsUntilInvite;
        int lastInvited = -1;
        bool trailActive;
        Vector3 trailTarget;
        float trailStopDistance;
        // The number of presents opened when the Toy Room last invited him, so it invites once per present.
        int toyRoomInvitedAtPresent = -1;
        // The same for the race: it invites once per present when there is something new for the cart.
        int raceInvitedAtPresent = -1;
        CoolSchool.ToyRoom.ToyShelf shelf;
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
                trailActive = false;
                return;
            }

            secondsUntilInvite -= Time.deltaTime;
            if (secondsUntilInvite <= 0f)
            {
                secondsUntilInvite = repeatSeconds;
                Invite();
            }

            if (trailActive)
            {
                if (Time.time >= trailEndTime) trailActive = false;
                else if (Time.time >= nextTrailTime) ShowTrail();
            }
        }

        /// <summary>Picks the zone that was invited least recently, says its line and starts the trail.</summary>
        void Invite()
        {
            if (ToyRoomHasNews())
            {
                InviteToToyRoom();
                return;
            }

            if (zones == null || zones.Length == 0) return;

            // Something new for the soapbox cart (a paint, a horn...): the race goes first, once per present.
            int raceZone = RaceZoneWithNews();
            if (raceZone >= 0)
            {
                raceInvitedAtPresent = ProgressStore.Current.presentsOpened;
                InviteZone(raceZone);
                return;
            }

            for (int step = 1; step <= zones.Length; step++)
            {
                int index = (lastInvited + step) % zones.Length;
                if (zones[index] == null) continue;

                InviteZone(index);
                return;
            }
        }

        void InviteZone(int index)
        {
            lastInvited = index;
            Sfx.Play("zone.invite_chime");
            if (inviteGroups != null && index < inviteGroups.Length && !string.IsNullOrEmpty(inviteGroups[index]))
                VoicePlayer.Say(InviteGroupFor(inviteGroups[index]));

            StartTrail(zones[index].transform.position, zones[index].Radius);
        }

        /// <summary>The race zone's index when he has won cart things he has not seen in the cart picker yet (and was not asked since the last present), else -1.</summary>
        int RaceZoneWithNews()
        {
            if (!HasUnseenCartItems() || ProgressStore.Current.presentsOpened == raceInvitedAtPresent) return -1;

            for (int i = 0; zones != null && inviteGroups != null && i < zones.Length && i < inviteGroups.Length; i++)
                if (zones[i] != null && inviteGroups[i] == RaceInviteGroup) return i;
            return -1;
        }

        static bool HasUnseenCartItems()
        {
            PlayerProgress progress = ProgressStore.Current;
            return progress.unseenCartItems != null && progress.unseenCartItems.Count > 0;
        }

        /// <summary>True when there is a new toy or medal in the Toy Room and he has not been invited there since the last present.</summary>
        bool ToyRoomHasNews()
        {
            if (toyRoomSpot == null) return false;

            // Only things that stand on a shelf count: cart things without a slot (paints, horns...) are news at the race, not here.
            if (shelf == null) shelf = FindAnyObjectByType<CoolSchool.ToyRoom.ToyShelf>();

            PlayerProgress progress = ProgressStore.Current;
            bool itemNews = shelf != null ? shelf.HasUnseenOnShelf(progress) : progress.unseenItems != null && progress.unseenItems.Count > 0;
            bool news = itemNews || progress.medals > progress.medalsSeen;
            return news && progress.presentsOpened != toyRoomInvitedAtPresent;
        }

        /// <summary>The next invitation after a present: come and look in the Toy Room, with the sparkle trail to its doorway.</summary>
        void InviteToToyRoom()
        {
            toyRoomInvitedAtPresent = ProgressStore.Current.presentsOpened;
            Sfx.Play("zone.invite_chime");
            VoicePlayer.Say("toyroom.invite");
            StartTrail(toyRoomSpot.position, ToyRoomTrailStop);
        }

        void StartTrail(Vector3 target, float stopDistance)
        {
            trailActive = true;
            trailTarget = target;
            trailStopDistance = stopDistance;
            trailEndTime = Time.time + trailSeconds;
            nextTrailTime = 0f;
        }

        /// <summary>With a golden sword waiting, the castle invitation talks about it instead.</summary>
        static string InviteGroupFor(string group)
        {
            if (group == "invite.knights" && ProgressStore.Current.pendingPowerUp == PowerUps.GoldenSword)
                return "invite.knights.golden";
            // Something new for the cart: "You have something new for your cart! Are you coming to race?"
            if (group == RaceInviteGroup && HasUnseenCartItems())
                return "invite.race.new";
            return group;
        }

        /// <summary>
        /// Sparkles every few metres along the walking route from the player to the zone (so the trail goes through
        /// gates instead of walls). Without a route it falls back to the straight line.
        /// </summary>
        void ShowTrail()
        {
            nextTrailTime = Time.time + TrailRefreshSeconds;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            if (ShowPathTrail()) return;
            ShowStraightTrail();
        }

        /// <summary>Sparkles along the NavMesh path. Returns false when there is no usable path.</summary>
        bool ShowPathTrail()
        {
            if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit from, NavSampleDistance, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(trailTarget, out NavMeshHit to, NavSampleDistance, NavMesh.AllAreas))
                return false;

            if (trailPath == null) trailPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, trailPath)) return false;
            if (trailPath.status == NavMeshPathStatus.PathInvalid) return false;

            Vector3[] corners = trailPath.corners;
            if (corners.Length < 2) return false;

            float totalLength = 0f;
            for (int i = 1; i < corners.Length; i++) totalLength += Vector3.Distance(corners[i - 1], corners[i]);

            // Stop a zone radius before the end, measured along the path.
            float end = Mathf.Max(0f, totalLength - trailStopDistance);
            float along = TrailStartDistance;
            float segmentStart = 0f;
            int segment = 1;
            for (int i = 0; i < MaxTrailPoints && along <= end && segment < corners.Length; i++)
            {
                // Move on to the path segment that contains this distance.
                float segmentLength = Vector3.Distance(corners[segment - 1], corners[segment]);
                while (along > segmentStart + segmentLength && segment < corners.Length - 1)
                {
                    segmentStart += segmentLength;
                    segment++;
                    segmentLength = Vector3.Distance(corners[segment - 1], corners[segment]);
                }

                float t = segmentLength > 0.0001f ? Mathf.Clamp01((along - segmentStart) / segmentLength) : 1f;
                Vector3 point = Vector3.Lerp(corners[segment - 1], corners[segment], t);
                BattleEffects.Sparkle(point + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
            return true;
        }

        /// <summary>The old fallback: sparkles along the straight line from the player toward the zone.</summary>
        void ShowStraightTrail()
        {
            Vector3 start = player.transform.position;
            Vector3 toZone = trailTarget - start;
            toZone.y = 0f;
            float distance = toZone.magnitude;
            if (distance < 0.01f) return;

            Vector3 direction = toZone / distance;
            float end = Mathf.Max(0f, distance - trailStopDistance);
            float along = TrailStartDistance;
            for (int i = 0; i < MaxTrailPoints && along <= end; i++)
            {
                BattleEffects.Sparkle(start + direction * along + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
        }
    }
}
