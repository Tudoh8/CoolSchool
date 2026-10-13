using System.Collections;
using CoolSchool.Castle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The middle of the castle adventure: rescuing the knight from the tower, the friendly dragon (hungry, or a sad mother
    /// looking for her egg), the cookies, the egg and the baby dragon's name, and the dragon ride.
    /// </summary>
    public partial class KnightsGame
    {
        const float TowerRescueTimeoutSeconds = 110f;
        const float DragonArriveTimeoutSeconds = 40f;
        const float BreatheTimeoutSeconds = 25f;
        const float EggTimeoutSeconds = 110f;
        const float NamingTimeoutSeconds = 130f;
        const float CookiesTimeoutSeconds = 160f;
        const float QuickCookieTimeoutSeconds = 70f;
        const float DragonPoseTimeoutSeconds = 10f;
        const float RideTimeoutSeconds = 80f;
        const float RideMountSeconds = 0.9f;
        const float RideDismountTimeoutSeconds = 3f;

        bool babyHatched;

        // ---- The knight on the tower ----

        /// <summary>"Now the knight on the tower!" He walks to the tower, and the knight climbs down and joins.</summary>
        IEnumerator RunTowerRescue()
        {
            if (towerRescue == null || stuckKnight == null) yield break;

            SetPlayerMode(PlayerMode.Walk);
            VoicePlayer.Say("castle.rescue");
            yield return Bounded(towerRescue.Run(player != null ? player.transform : transform), TowerRescueTimeoutSeconds);

            // He is free: from now on he is one of the knights again.
            stuckKnight = null;
            SetPlayerMode(PlayerMode.Frozen);
        }

        // ---- The dragon ----

        /// <summary>
        /// The dragon flies in and breathes one confetti breath. Then it turns out it is hungry (cookies), or sad because the egg
        /// is lost (the egg mission: the egg is given back and the baby dragon hatches, and gets a name). In the end they are friends.
        /// </summary>
        IEnumerator RunDragonVisit()
        {
            if (dragon == null) yield break;

            // The fighting is over: the knights stop and watch.
            EnableAllies(false);
            LetAlliesWatch(ArenaCenter());

            VoicePlayer.Say("castle.dragon_arrives");
            dragonArrived = false;
            subscribedDragon = dragon;
            dragon.Arrived += OnDragonArrived;

            Vector3 center = ArenaCenter();
            Vector3 entry = dragonEntry != null ? dragonEntry.position : center + Vector3.forward * 10f;
            dragon.FlyInFriendly(entry, center);

            // He can still hold up the shield for the one big breath.
            SetPlayerMode(PlayerMode.Fight);
            float arriveBy = Time.time + DragonArriveTimeoutSeconds;
            while (!dragonArrived && Time.time < arriveBy)
                yield return null;
            UnsubscribeDragon();

            yield return Bounded(dragon.BreatheOnce(), BreatheTimeoutSeconds);
            SetPlayerMode(PlayerMode.Frozen);

            if (mission == CastleMission.Egg)
            {
                dragon.SetMood(DragonMood.Sad);
                yield return SayAndWait("castle.dragon_egg");
                if (egg != null)
                {
                    yield return Bounded(egg.RunGiveBack(dragon, NewResult()), EggTimeoutSeconds);
                    babyHatched = true;
                }
                // Naming the baby never counts as a mistake: it has no result in the list.
                if (letterMoment == CastleLetterMoment.NameBabyDragon && naming != null)
                    yield return Bounded(naming.Run(level, new MomentResult()), NamingTimeoutSeconds);
            }
            else
            {
                dragon.SetMood(DragonMood.Hungry);
                yield return SayAndWait("castle.dragon_hungry");
                if (cookies != null)
                {
                    if (numberMoment == CastleNumberMoment.Cookies)
                        yield return Bounded(cookies.Run(dragon, cookieCount, NewResult()), CookiesTimeoutSeconds);
                    else
                        yield return Bounded(cookies.RunQuick(dragon), QuickCookieTimeoutSeconds);
                }
            }

            dragon.SetMood(DragonMood.Happy);
            yield return SayAndWait("castle.dragon_friend");
        }

        /// <summary>The knights stand still and turn to look at the dragon.</summary>
        void LetAlliesWatch(Vector3 point)
        {
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
            {
                if (ally == null || !ally.gameObject.activeInHierarchy) continue;

                var agent = ally.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                Vector3 toPoint = point - ally.transform.position;
                toPoint.y = 0f;
                if (toPoint.sqrMagnitude > 0.01f) walkRoutines.Add(StartCoroutine(TurnToFace(ally.transform, toPoint, 360f)));
            }
        }

        // ---- The dragon ride ----

        /// <summary>
        /// The dragon lies down, he climbs on its back and it flies the fixed path over the castle (the action button breathes
        /// confetti fire; the floating stars are caught on the way). Then it lands, he hops off and the dragon stands up.
        /// </summary>
        IEnumerator RunDragonRide()
        {
            if (dragon == null || dragon.Seat == null || ridePath == null || player == null) yield break;

            yield return Bounded(dragon.LieDown(), DragonPoseTimeoutSeconds);

            player.RideOn(dragon.Seat);
            var kid = player.GetComponent<KidAnimator>();
            if (kid != null) kid.SetSitting(true);
            yield return new WaitForSeconds(RideMountSeconds);

            if (actionButton != null)
            {
                actionButton.SetIcon(fireIcon != null ? fireIcon : swordIcon);
                actionButton.Pressed += OnRidePressed;
            }
            VoicePlayer.Say("castle.ride_start");
            yield return Bounded(dragon.FlyPath(ridePath), RideTimeoutSeconds);

            if (actionButton != null)
            {
                actionButton.Pressed -= OnRidePressed;
                actionButton.ResetIcon();
            }
            VoicePlayer.Say("castle.ride_land");

            yield return Bounded(dragon.LieDown(), DragonPoseTimeoutSeconds);
            Vector3 down = rideDismountPoint != null ? rideDismountPoint.position : dragon.transform.position + new Vector3(-2f, 0f, -1.5f);
            player.StopRiding(down);
            if (kid != null) kid.SetSitting(false);

            float hopBy = Time.time + RideDismountTimeoutSeconds;
            while (player.IsRiding && Time.time < hopBy)
                yield return null;

            yield return Bounded(dragon.StandUp(), DragonPoseTimeoutSeconds);
        }

        void OnRidePressed()
        {
            if (dragon != null) dragon.BreatheConfettiForward();
        }
    }
}
