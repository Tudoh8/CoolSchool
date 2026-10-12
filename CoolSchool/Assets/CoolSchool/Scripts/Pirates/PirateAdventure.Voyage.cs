using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Pirates
{
    /// <summary>The first half of the adventure: dressing up, sailing, the cannon, landing on the island and the skeleton duel.</summary>
    public partial class PirateAdventure
    {
        // ----- 1. Begin: the sand turns into sea, the pirate is dressed, the crew comes aboard ----

        IEnumerator RunBegin()
        {
            // "Let's imagine...": sparkle and glow, then the sand pit becomes a sea.
            if (imagination != null) imagination.Play(true);
            Sfx.Play("pirates.waves", ShipPosition());
            Sfx.PlayMusic("music.pirates");
            yield return new WaitForSeconds(0.6f);

            if (sea != null) sea.SetActive(true);
            if (ship != null) ship.Afloat = true;
            SeaSparkles();

            if (costume != null) costume.Show(true);
            if (playerWeapons != null) playerWeapons.ShowWeapons(true);

            // Lorre flies in and sits on his shoulder.
            VoicePlayer.Say("pirates.start");
            yield return ParrotFliesIn();
            yield return WaitForVoice();

            // Two pirate classmates appear with a sparkle.
            ActivateCrew();
            VoicePlayer.Say("pirates.crew");
            yield return new WaitForSeconds(1.2f);
            yield return WaitForVoice(4f);

            // Everybody hops aboard: the captain to the wheel, the crew beside him.
            BoardEverybody();
            yield return new WaitForSeconds(0.8f);
            VoicePlayer.Say("pirates.steer");
            yield return WaitForVoice();
        }

        /// <summary>Sparkles all over the new sea.</summary>
        void SeaSparkles()
        {
            Bounds area = new Bounds(ShipPosition(), new Vector3(14f, 1f, 18f));
            if (sea != null)
            {
                Renderer seaRenderer = sea.GetComponentInChildren<Renderer>(true);
                if (seaRenderer != null && seaRenderer.bounds.size.sqrMagnitude > 1f) area = seaRenderer.bounds;
            }

            for (int i = 0; i < 12; i++)
            {
                Vector3 spot = new Vector3(Random.Range(area.min.x, area.max.x), area.center.y + Random.Range(0.2f, 0.8f), Random.Range(area.min.z, area.max.z));
                BattleEffects.Sparkle(spot);
            }
        }

        IEnumerator ParrotFliesIn()
        {
            if (parrot == null || player == null) yield break;

            Animator kid = player.GetComponentInChildren<Animator>();
            if (!parrot.IsVisible) parrot.transform.position = PlayerPosition() + new Vector3(-6f, 4.5f, 2f);

            bool arrived = false;
            parrot.FlyTo(PlayerPosition() + Vector3.up * 1.9f, () =>
            {
                parrot.PerchOnShoulder(kid);
                arrived = true;
            });

            float giveUp = Time.time + 5f;
            while (!arrived && Time.time < giveUp) yield return null;
            parrot.Squawk();
            yield return new WaitForSeconds(0.7f);
        }

        // ----- The crew ----------------------------------------------------------------------------

        /// <summary>The pirate classmates appear around the player at the dock (sparkles), with the navigation and the AI off.</summary>
        void ActivateCrew()
        {
            if (crew == null) return;

            EnsureCrewArrays();
            for (int i = 0; i < crew.Length; i++)
            {
                KnightAllyAI member = crew[i];
                if (member == null) continue;

                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 spawn = PlayerPosition() + new Vector3(side * (1.6f + 0.7f * (i / 2)), 0f, 0.7f);
                if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 2f, NavMesh.AllAreas)) spawn = hit.position;

                // Off before switching on, so nobody starts away from the NavMesh.
                member.enabled = false;
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                member.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(Vector3.forward));
                member.gameObject.SetActive(true);

                var weapons = member.GetComponent<WeaponHolder>();
                if (weapons != null) weapons.ShowWeapons(true);
                var hittable = member.GetComponent<Hittable>();
                if (hittable != null) hittable.ResetState();
                BattleEffects.Sparkle(spawn + Vector3.up * 1f);
            }
        }

        void BoardEverybody()
        {
            if (ship == null) return;

            if (player != null && ship.HelmSeat != null) player.RideOn(ship.HelmSeat);

            if (crew == null || ship.CrewSeats == null || ship.CrewSeats.Length == 0) return;
            for (int i = 0; i < crew.Length; i++)
                if (crew[i] != null) StartCoroutine(BoardCrew(i, ship.CrewSeats[i % ship.CrewSeats.Length]));
        }

        IEnumerator BoardCrew(int index, Transform seat)
        {
            Transform member = crew[index].transform;
            var kid = member.GetComponent<KidAnimator>();
            if (kid != null)
            {
                kid.ForceIdle = true;
                kid.PlayJump();
            }

            yield return SeatUtil.HopTo(member, () => seat.position, 0.45f, 0.7f, () => seat.rotation);
            SeatUtil.Mount(member, seat);
            crewMounted[index] = true;
        }

        /// <summary>The crew hops off to its place on the beach, and the navigation comes back on.</summary>
        IEnumerator DisembarkCrew(int index, Vector3 point)
        {
            Transform member = crew[index].transform;
            if (crewMounted[index])
            {
                SeatUtil.Dismount(member, crewParents[index]);
                crewMounted[index] = false;
            }

            Vector3 land = point;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) land = hit.position;
            Quaternion facing = Quaternion.LookRotation(Vector3.forward);

            var kid = member.GetComponent<KidAnimator>();
            if (kid != null) kid.PlayJump();
            yield return SeatUtil.HopTo(member, () => land, 0.5f, 0.8f, () => facing);

            if (kid != null) kid.ForceIdle = false;
            var agent = member.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                if (!agent.isOnNavMesh) agent.Warp(land);
            }
            BattleEffects.Sparkle(land + Vector3.up * 0.8f);
        }

        Vector3 CrewLandingPoint(int index)
        {
            if (crewLandingPoints != null && crewLandingPoints.Length > 0)
            {
                Transform point = crewLandingPoints[index % crewLandingPoints.Length];
                if (point != null) return point.position;
            }
            return LandingPoint() + new Vector3((index % 2 == 0 ? -1f : 1f) * 1.8f, 0f, 0.5f);
        }

        Vector3 LandingPoint()
        {
            return islandLandingPoint != null ? islandLandingPoint.position : ShipPosition() + Vector3.forward * 4f;
        }

        // ----- 2. Sailing --------------------------------------------------------------------------

        IEnumerator RunSail()
        {
            if (ship == null) yield break;

            ship.StartSailing();
            float giveUp = Time.time + SailTimeoutSeconds;
            while (ship.Progress01 < cannonAtProgress && !shipArrived && Time.time < giveUp)
                yield return null;
        }

        // ----- 3. The cannon -----------------------------------------------------------------------

        IEnumerator RunCannon()
        {
            if (ship == null || cannon == null || skeletonBoat == null) yield break;

            ship.PauseSailing(true);

            // The skeleton boat sails in from the west with its balloons; the skeletons ride on it, waving.
            skeletonBoat.Appear(ByLevel(BalloonsByLevel, level));
            PutSkeletonsOnBoat();
            VoicePlayer.Say("pirates.balloons");
            yield return WaitForVoice();
            yield return new WaitForSeconds(0.5f);

            ShowIcon(cannonIcon);
            cannon.Popped += OnBalloonPopped;
            cannon.Armed = true;
            lastActionTime = Time.time;

            float start = Time.time;
            float nextHint = Time.time + cannonHintSeconds;
            float nextAutoPop = 0f;
            while (!AllBalloonsPopped())
            {
                // Hasn't tapped for a while: show him what the button does.
                if (Time.time >= nextHint && Time.time - lastActionTime >= cannonHintSeconds)
                {
                    nextHint = Time.time + cannonHintSeconds * 2f;
                    VoicePlayer.Say("pirates.cannon_hint");
                }

                // Safety net: after a very long time the balloons pop by themselves, one by one.
                if (Time.time - start > CannonTimeoutSeconds && Time.time >= nextAutoPop)
                {
                    nextAutoPop = Time.time + 0.8f;
                    PopNextBalloonByItself();
                }
                yield return null;
            }

            cannon.Armed = false;
            cannon.Popped -= OnBalloonPopped;
            ClearIcon();

            yield return new WaitForSeconds(0.7f);
            VoicePlayer.Say("pirates.balloons_done");
            skeletonBoat.SailToIsland();
            ship.PauseSailing(false);
        }

        /// <summary>The first wave of skeletons climbs onto the boat's seats (switched on, waving). The second wave comes later.</summary>
        void PutSkeletonsOnBoat()
        {
            roundSkeletons.Clear();
            if (skeletons == null) return;

            int wanted = ByLevel(WaveOneByLevel, level);
            Transform[] seats = skeletonBoat.SkeletonSeats;
            foreach (SkeletonPirateAI skeleton in skeletons)
            {
                if (skeleton == null || roundSkeletons.Count >= wanted) continue;

                skeleton.SetHitsToDefeat(ByLevel(HitsByLevel, level));
                if (seats != null && seats.Length > 0) skeleton.Ride(seats[roundSkeletons.Count % seats.Length]);
                roundSkeletons.Add(skeleton);
            }
        }

        bool AllBalloonsPopped()
        {
            foreach (BalloonTarget balloon in skeletonBoat.ActiveBalloons)
                if (balloon != null && !balloon.IsPopped) return false;
            return true;
        }

        void PopNextBalloonByItself()
        {
            foreach (BalloonTarget balloon in skeletonBoat.ActiveBalloons)
            {
                if (balloon == null || balloon.IsPopped) continue;

                balloon.Pop();
                OnBalloonPopped(balloon);
                return;
            }
        }

        /// <summary>Each pop: Lorre counts along, "one, two, three...".</summary>
        void OnBalloonPopped(BalloonTarget balloon)
        {
            lastActionTime = Time.time;

            int popped = 0;
            foreach (BalloonTarget other in skeletonBoat.ActiveBalloons)
                if (other != null && other.IsPopped) popped++;

            VoicePlayer.Say("number." + popped, true);
            if (parrot != null && AllBalloonsPopped()) parrot.Squawk();
        }

        // ----- 4. Arriving at the island -----------------------------------------------------------

        IEnumerator RunArrive()
        {
            if (ship == null) yield break;

            float giveUp = Time.time + SailTimeoutSeconds;
            while (!shipArrived && Time.time < giveUp) yield return null;

            VoicePlayer.Say("pirates.arrive");
            Sfx.Play("pirates.wheel", ShipPosition());

            // Everybody off: the captain to the landing spot, the crew to the beach. Pretend water is kept out by invisible walls.
            if (player != null) player.StopRiding(LandingPoint());
            if (crew != null)
                for (int i = 0; i < crew.Length; i++)
                    if (crew[i] != null) StartCoroutine(DisembarkCrew(i, CrewLandingPoint(i)));

            float hopGiveUp = Time.time + 3f;
            while (player != null && player.IsRiding && Time.time < hopGiveUp) yield return null;
            if (islandBounds != null) islandBounds.SetActive(true);

            // The skeleton boat should be at the beach by now; wait for it a little.
            float boatGiveUp = Time.time + 25f;
            while (skeletonBoat != null && !skeletonBoat.AtIsland && Time.time < boatGiveUp) yield return null;
            yield return WaitForVoice(5f);
        }

        // ----- 5. The skeleton duel ----------------------------------------------------------------

        IEnumerator RunSkeletons()
        {
            if (roundSkeletons.Count == 0) yield break;

            // Wave 1: they hop off the boat onto the beach with a sparkle, and challenge the player.
            for (int i = 0; i < roundSkeletons.Count; i++)
            {
                SkeletonPirateAI skeleton = roundSkeletons[i];
                if (skeleton == null) continue;

                skeleton.Fight(player != null ? player.transform : null);
                skeleton.HopOff(SkeletonLandingPoint(i));
                yield return new WaitForSeconds(0.3f);
            }
            VoicePlayer.Say("pirates.skeletons");

            // Now he can swing the sword, and the crew bumps in as well (the crew only makes them wobble; his sword does the rest).
            if (playerInput != null) playerInput.enabled = true;
            ShowIcon(swordIcon);
            EnableCrewAi(true);
            lastActionTime = Time.time;
            yield return DuelUntilFriends();

            // Wave 2: more skeletons jump off the boat, one after another. The first wave's friends cheer where they stand.
            if (skeletons != null && skeletons.Length > roundSkeletons.Count)
            {
                CheerEveryone();
                yield return new WaitForSeconds(1.2f);
                VoicePlayer.Say("pirates.skeletons");
                yield return LaunchSecondWave();
                lastActionTime = Time.time;
                yield return DuelUntilFriends();
            }

            // Duel over: no more sword for now. The new friends follow him, and so does the crew.
            if (playerInput != null) playerInput.enabled = false;
            ClearIcon();
            FriendsFollowPlayer();
            CheerEveryone();
            yield return new WaitForSeconds(1.2f);
        }

        /// <summary>Waits until every skeleton of this round is a friend, with the hint and the safety net.</summary>
        IEnumerator DuelUntilFriends()
        {
            float start = Time.time;
            float nextHint = Time.time + fightHintSeconds;
            while (!AllSkeletonsFriends())
            {
                if (Time.time >= nextHint && Time.time - lastActionTime >= fightHintSeconds)
                {
                    nextHint = Time.time + fightHintSeconds * 2.4f;
                    VoicePlayer.Say("pirates.fight_hint");
                }

                // Safety net: after a very long time the rest of the skeletons (of both waves) give up and become friends.
                if (Time.time - start > FightTimeoutSeconds)
                {
                    foreach (SkeletonPirateAI skeleton in roundSkeletons)
                        if (skeleton != null && !skeleton.IsFriend) skeleton.Befriend();
                    start = Time.time;
                }
                yield return null;
            }
        }

        /// <summary>The next skeletons appear on the boat's deck with a sparkle and jump to the beach one by one.</summary>
        IEnumerator LaunchSecondWave()
        {
            int wanted = ByLevel(WaveTwoByLevel, level);
            Transform[] seats = skeletonBoat != null ? skeletonBoat.SkeletonSeats : null;
            Transform fallback = skeletonBoat != null ? skeletonBoat.transform : (ship != null ? ship.transform : transform);

            int launched = 0;
            foreach (SkeletonPirateAI skeleton in skeletons)
            {
                if (launched >= wanted) break;
                if (skeleton == null || roundSkeletons.Contains(skeleton)) continue;

                Transform seat = seats != null && seats.Length > 0 ? seats[launched % seats.Length] : fallback;
                skeleton.SetHitsToDefeat(ByLevel(HitsByLevel, level));
                skeleton.Ride(seat);
                roundSkeletons.Add(skeleton);
                BattleEffects.Sparkle(seat.position + Vector3.up * 1f);
                yield return new WaitForSeconds(0.5f);

                skeleton.Fight(player != null ? player.transform : null);
                skeleton.HopOff(SkeletonLandingPoint(WaveTwoFirstLandingPoint + launched));
                launched++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>The friends follow the player, each keeping its own place on a wide ring so they never crowd him.</summary>
        void FriendsFollowPlayer()
        {
            Transform leader = player != null ? player.transform : null;
            int slot = 0;
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null) skeleton.Follow(leader, slot++);
        }

        bool AllSkeletonsFriends()
        {
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null && !skeleton.IsFriend) return false;
            return true;
        }

        Vector3 SkeletonLandingPoint(int index)
        {
            if (skeletonLandingPoints != null && skeletonLandingPoints.Length > 0)
            {
                Transform point = skeletonLandingPoints[index % skeletonLandingPoints.Length];
                if (point != null) return point.position;
            }
            return LandingPoint() + new Vector3(-4f + 1.8f * (index % 5), 0f, 2f + 1.5f * (index / 5) + 0.7f * (index % 2));
        }

        /// <summary>The crew fights next to the player. Their brain follows him around when there is nobody left to duel.</summary>
        void EnableCrewAi(bool on)
        {
            if (crew == null) return;

            foreach (KnightAllyAI member in crew)
            {
                if (member == null || !member.gameObject.activeSelf) continue;

                var agent = member.GetComponent<NavMeshAgent>();
                if (on && agent != null && !agent.enabled)
                {
                    agent.enabled = true;
                    if (!agent.isOnNavMesh && NavMesh.SamplePosition(member.transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                        agent.Warp(hit.position);
                }
                member.enabled = on;
            }
        }
    }
}
