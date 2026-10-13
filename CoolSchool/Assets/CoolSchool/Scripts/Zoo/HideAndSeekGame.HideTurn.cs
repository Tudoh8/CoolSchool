using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// His turn to hide. Three hiding places glow; the keeper counts at the base tree (the found animals stand around her with
    /// their eyes closed too), he walks into a glow and hops in. Then the keeper looks in a few wrong places first, and always
    /// finds him. He is never caught in the open: if the count ends before he is hidden she keeps counting and a trail leads him.
    /// </summary>
    public partial class HideAndSeekGame
    {
        // He is "there" when he is this close to the edge of the cover, or this close to the glowing ring (whichever comes first).
        const float HideEdgeMeters = 1f;
        const float HideGlowMeters = 1.4f;
        // The count waits for the "your turn" line to finish, but never longer than this.
        const float YourTurnVoiceMaxSeconds = 7f;
        const float RideMountMaxSeconds = 1.2f;
        const int PlayerSpotCount = 3;
        const float MinPlayerSpotDistance = 3f;
        const float HideTurnTimeoutSeconds = 75f;
        const float DecoyBudgetSeconds = 22f;
        const float KeeperStandAside = 1.4f;

        // The kinds of spot he can hide in: he stays partly visible there. Not the pond, the treetop, the tent or the house.
        static readonly HashSet<string> PlayerSpotIds = new HashSet<string> { "hedge", "bush", "rock", "table", "tree", "bin", "box", "leaves" };

        // Where he is hiding now (while he rides on its hide point).
        HidingSpot playerHiddenIn;

        IEnumerator RunHideTurn()
        {
            StopHintRoutine();
            if (trail != null) trail.Stop();
            foreach (HidingSpot spot in spots)
                if (spot != null) spot.ResetSpot();

            List<HidingSpot> mine = ChoosePlayerSpots();
            if (mine.Count == 0) yield break;

            Say("zoo.your_turn", true);
            float yourTurnSaid = Time.time;
            foreach (HidingSpot spot in mine) spot.SetPlayerGlow(true);
            FreezePlayer(false);

            // The keeper counts at the base tree, the found animals stand around her.
            if (conga != null) conga.Paused = true;
            Vector3 basePoint = countPoint != null ? countPoint.position : PlayerPosition();
            bool keeperThere = keeper == null;
            if (keeper != null) keeper.CountAt(basePoint, () => keeperThere = true, 6f);

            int countTo = HideCountTo;
            int counted = 0;
            bool countStarted = false;
            bool notYetSaid = false;
            float nextNumber = 0f;
            float nextTrail = 0f;
            float started = Time.time;
            HidingSpot hiddenIn = null;

            while (true)
            {
                // He walks into a glow and hops in.
                if (hiddenIn == null)
                {
                    HidingSpot reached = GlowReached(mine);
                    if (reached == null && Time.time - started > HideTurnTimeoutSeconds) reached = NearestGlow(mine, PlayerPosition());
                    if (reached != null)
                    {
                        yield return HidePlayerIn(reached);
                        hiddenIn = reached;
                    }
                }

                if (!keeperThere && Time.time - started > 8f) keeperThere = true;
                if (keeperThere)
                {
                    PlaceAnimalsAroundKeeper(basePoint);

                    // The first number waits until "Nu ben jij aan de beurt" has been said (it would cut it off).
                    bool voiceFree = Time.time - yourTurnSaid > YourTurnVoiceMaxSeconds
                        || (Time.time - yourTurnSaid > 0.4f && (VoicePlayer.Instance == null || !VoicePlayer.Instance.IsBusy));
                    if (!countStarted && voiceFree)
                    {
                        countStarted = true;
                        nextNumber = Time.time + 0.6f;
                    }
                }

                if (countStarted && Time.time >= nextNumber)
                {
                    nextNumber = Time.time + 1f;
                    if (counted >= countTo && hiddenIn != null) break;

                    if (counted >= countTo && !notYetSaid)
                    {
                        // The count is over but he is not hidden yet: she counts on.
                        notYetSaid = true;
                        Say("zoo.not_yet", true);
                        nextNumber = Time.time + 3f;
                    }
                    else
                    {
                        counted++;
                        if (counted <= NumberCurriculum.HighestNumber) Say("number." + counted, true);
                    }
                }

                // Not hidden yet: the sparkles lead him to the nearest glow.
                if (hiddenIn == null && counted >= countTo && Time.time >= nextTrail && trail != null)
                {
                    nextTrail = Time.time + 3f;
                    HidingSpot nearest = NearestGlow(mine, PlayerPosition());
                    if (nearest != null) trail.Show(player.transform, nearest.GlowPosition, 4f, 0.6f);
                }
                yield return null;
            }

            if (trail != null) trail.Stop();
            foreach (HidingSpot spot in spots)
                if (spot != null) spot.SetPlayerGlow(false);

            // The keeper turns round: ready or not, she comes.
            if (conga != null)
            {
                conga.Paused = false;
                if (keeper != null) conga.Leader = keeper.transform;
            }
            if (keeper != null) keeper.FaceDirection(Vector3.back);
            Say("zoo.seek_start", true);
            yield return WaitForVoice(5f);

            // She looks in a few wrong places first (nearest first)...
            float budgetEnd = Time.time + DecoyBudgetSeconds;
            foreach (HidingSpot decoy in ChooseDecoys(hiddenIn, ByLevel(DecoysByLevel)))
            {
                if (Time.time > budgetEnd) break;

                yield return KeeperGoesTo(decoy);
                if (keeper != null) keeper.FaceDirection(decoy.CoverBounds.center - keeper.transform.position);
                yield return new WaitForSeconds(0.3f);
                if (keeper != null) keeper.PlayLaugh();
                decoy.Reveal();
                if (Random.value < 0.6f)
                {
                    SurpriseCritter critter = PickCritter();
                    if (critter != null) critter.PopOut(decoy.PopPoint);
                }
                Say("zoo.seek_wrong", true);
                yield return new WaitForSeconds(0.8f);
                yield return WaitForVoice(4f);
            }

            // ...then at his spot: "Hmmm... I hear something...", a pause, and found him.
            yield return KeeperGoesTo(hiddenIn);
            if (keeper != null) keeper.FaceDirection(hiddenIn.CoverBounds.center - keeper.transform.position);
            Say("zoo.seek_close", true);
            yield return WaitForVoice(4f);
            yield return new WaitForSeconds(1.5f);

            Say("zoo.seek_found", true);
            yield return PlayerPopsOut(hiddenIn);
            yield return WaitForVoice(5f);

            foreach (HidingSpot spot in spots)
                if (spot != null) spot.ResetSpot();
            if (conga != null) conga.Leader = player.transform;
            if (keeper != null) keeper.Follow(player.transform, 3f);
        }

        /// <summary>The three spots nearest to him (preferring ones north of him, and not right at his feet) where he can hide.</summary>
        List<HidingSpot> ChoosePlayerSpots()
        {
            Vector3 position = PlayerPosition();
            var candidates = new List<HidingSpot>();
            foreach (HidingSpot spot in spots)
            {
                if (spot == null || !spot.CanHidePlayer || !PlayerSpotIds.Contains(spot.SpotId)) continue;
                candidates.Add(spot);
            }

            // Not right on top of him, if there are enough others.
            var farEnough = candidates.FindAll(s => FlatDistance(s.GlowPosition, position) >= MinPlayerSpotDistance);
            if (farEnough.Count >= PlayerSpotCount) candidates = farEnough;

            candidates.Sort((a, b) => HideScore(a, position).CompareTo(HideScore(b, position)));
            if (candidates.Count > PlayerSpotCount) candidates.RemoveRange(PlayerSpotCount, candidates.Count - PlayerSpotCount);
            return candidates;
        }

        /// <summary>Lower is better: near him, and north of him counts as 4 m closer (that way he walks away from the camera).</summary>
        static float HideScore(HidingSpot spot, Vector3 position)
        {
            Vector3 point = spot.GlowPosition;
            return FlatDistance(point, position) - (point.z > position.z ? 4f : 0f);
        }

        /// <summary>The glowing spot he has reached: close to the edge of its cover, or close to its ring, whichever comes first.</summary>
        HidingSpot GlowReached(List<HidingSpot> mine)
        {
            Vector3 position = PlayerPosition();
            foreach (HidingSpot spot in mine)
                if (spot.DistanceTo(position) <= HideEdgeMeters || FlatDistance(spot.GlowPosition, position) <= HideGlowMeters) return spot;
            return null;
        }

        static HidingSpot NearestGlow(List<HidingSpot> mine, Vector3 position)
        {
            HidingSpot best = null;
            float bestDistance = float.MaxValue;
            foreach (HidingSpot spot in mine)
            {
                float distance = FlatDistance(spot.GlowPosition, position);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = spot;
            }
            return best;
        }

        /// <summary>He hops into the spot: the other glows go out, a rustle, and he stands tucked in behind the cover facing the camera.</summary>
        IEnumerator HidePlayerIn(HidingSpot spot)
        {
            FreezePlayer(true);
            if (trail != null) trail.Stop();
            foreach (HidingSpot other in spots)
                if (other != null) other.SetPlayerGlow(false);

            // He hops onto the hide point and rides there: the controller sleeps and he is parented to the point, so gravity can't
            // pull him down (a bin or a sunk leaves point is above the ground).
            Transform seat = spot.PlayerHidePoint;
            Sfx.Play("zoo.hide_in", seat.position);
            playerHiddenIn = spot;
            player.RideOn(seat);
            for (float waited = 0f; player.transform.parent != seat && waited < RideMountMaxSeconds; waited += Time.deltaTime)
                yield return null;
        }

        /// <summary>The found animals stand in a loose row south of the keeper, facing north like her.</summary>
        void PlaceAnimalsAroundKeeper(Vector3 basePoint)
        {
            if (conga == null) return;

            Vector3 centre = keeper != null ? keeper.transform.position : basePoint;
            IReadOnlyList<ZooAnimal> members = conga.Members;
            for (int i = 0; i < members.Count; i++)
            {
                ZooAnimal animal = members[i];
                if (animal == null) continue;

                Vector3 spot = centre + new Vector3((i - (members.Count - 1) * 0.5f) * 1.4f, 0f, -1.5f);
                if (FlatDistance(animal.transform.position, spot) > 0.3f)
                {
                    animal.WalkTo(spot, 3.4f);
                }
                else
                {
                    animal.SetMoving(false, false);
                    animal.TurnTowards(Vector3.forward);
                }
            }
        }

        /// <summary>Spots to look in first, the nearest to the keeper first (each next one nearest to the one before). Not his spot.</summary>
        List<HidingSpot> ChooseDecoys(HidingSpot hiddenIn, int count)
        {
            var left = new List<HidingSpot>();
            foreach (HidingSpot spot in spots)
                if (spot != null && spot != hiddenIn) left.Add(spot);

            var chosen = new List<HidingSpot>();
            Vector3 from = keeper != null ? keeper.transform.position : PlayerPosition();
            while (chosen.Count < count && left.Count > 0)
            {
                HidingSpot best = left[0];
                float bestDistance = float.MaxValue;
                foreach (HidingSpot spot in left)
                {
                    float distance = FlatDistance(spot.CoverBounds.center, from);
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    best = spot;
                }
                chosen.Add(best);
                left.Remove(best);
                from = best.CoverBounds.center;
            }
            return chosen;
        }

        /// <summary>The keeper walks to the south side of a spot, a little to the side of where things pop out.</summary>
        IEnumerator KeeperGoesTo(HidingSpot spot)
        {
            if (keeper == null) yield break;

            Vector3 stand = spot.PopPoint + Vector3.left * KeeperStandAside;
            if (NavMesh.SamplePosition(stand, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) stand = hit.position;

            bool arrived = false;
            keeper.WalkTo(stand, () => arrived = true, 8f);
            float giveUp = Time.time + 10f;
            while (!arrived && Time.time < giveUp) yield return null;
        }

        /// <summary>"Gevonden!" He hops out in front of the spot, cheers, and everybody laughs.</summary>
        IEnumerator PlayerPopsOut(HidingSpot spot)
        {
            Vector3 pop = spot.PopPoint;
            Vector3 towardKeeper = keeper != null ? keeper.transform.position - pop : Vector3.back;
            towardKeeper.y = 0f;
            if (towardKeeper.sqrMagnitude < 0.01f) towardKeeper = Vector3.back;

            // Hops down from the hide point to the ground in front of the spot (StopRiding does the hop and wakes the controller).
            if (player.IsRiding)
            {
                player.StopRiding(pop);
                for (float waited = 0f; player.IsRiding && waited < RideMountMaxSeconds * 2f; waited += Time.deltaTime)
                    yield return null;
            }
            else
            {
                yield return PlayerHop(pop, 0.45f);
            }
            playerHiddenIn = null;
            player.Teleport(pop, Quaternion.LookRotation(towardKeeper, Vector3.up));
            FreezePlayer(false);
            if (playerAnimator != null) playerAnimator.PlayCheer();

            spot.Reveal();
            Sfx.Play("zoo.found_pop", pop);
            BattleEffects.Confetti(pop + Vector3.up * 1.2f);
            if (keeper != null) keeper.PlayLaugh();

            if (conga != null)
            {
                foreach (ZooAnimal animal in conga.Members)
                {
                    if (animal == null) continue;

                    animal.Giggle();
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
    }
}
