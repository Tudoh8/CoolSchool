using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// The second half: back to the zoo with the animals, each one goes into its pen while he hears where it was hiding, the "how
    /// many did we find?" question and the party. The stars and the saved progress come at the party.
    /// </summary>
    public partial class HideAndSeekGame
    {
        const float WalkHomeSeconds = 60f;
        const float PenArriveMeters = 2f;
        const float TrailRefreshSeconds = 6f;
        const float TakeInGiveUpSeconds = 14f;
        const float PartySeconds = 4f;
        const float PartyWalkSeconds = 9f;
        // He stands this far south of a pen's gate point (so the pen, the gate and the animal hopping in are on the screen).
        const float PenViewMeters = 2.5f;
        const float PenWalkMaxSeconds = 9f;

        // ----- 6. Back to the zoo ------------------------------------------------------------------

        IEnumerator RunBackHome()
        {
            FreezePlayer(false);
            if (conga != null)
            {
                conga.Paused = false;
                conga.Leader = player.transform;
            }
            if (keeper != null) keeper.Follow(player.transform, 3f);

            Say("zoo.back_home", true);
            Vector3 target = penWalkPoint != null ? penWalkPoint.position : PlayerPosition();

            // A trail of sparkles to the pens, laid again every few seconds; after a minute the animals go home anyway.
            float giveUp = Time.time + WalkHomeSeconds;
            float nextTrail = 0f;
            while (Time.time < giveUp && FlatDistance(PlayerPosition(), target) > PenArriveMeters)
            {
                if (Time.time >= nextTrail && trail != null)
                {
                    nextTrail = Time.time + TrailRefreshSeconds;
                    trail.Show(player.transform, target, TrailRefreshSeconds + 1f, 1f);
                }
                yield return null;
            }
            if (trail != null) trail.Stop();

            // He waits in front of the pens, facing them.
            FreezePlayer(true);
            Quaternion face = penWalkPoint != null ? penWalkPoint.rotation : Quaternion.LookRotation(Vector3.forward, Vector3.up);
            player.transform.rotation = face;
            if (keeper != null) keeper.Stop();
            yield return new WaitForSeconds(0.4f);

            foreach (Hider hider in foundOrder)
                yield return AnimalGoesHome(hider);

            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// One animal walks to its pen and hops in, while the keeper asks where it was hiding. He has a moment to answer out loud,
        /// then she says it with the position word ("Achter de heg!").
        /// </summary>
        IEnumerator AnimalGoesHome(Hider hider)
        {
            ZooAnimal animal = hider.animal;

            // He walks (the other animals with him) to in front of this animal's pen, so the pen is on the screen.
            if (hider.pen != null && hider.pen.GatePoint != null)
            {
                if (keeper != null) keeper.Follow(player.transform, 3f);
                Vector3 viewPoint = hider.pen.GatePoint.position + Vector3.back * PenViewMeters;
                yield return WalkPlayerOnNavMesh(viewPoint, Vector3.forward, PenWalkMaxSeconds);
            }

            if (conga != null) conga.Remove(animal);

            bool inside = hider.pen == null;
            if (hider.pen != null) hider.pen.TakeIn(animal, () => inside = true);

            Say("zoo.where." + hider.kind.Id, true);
            yield return WaitForVoice(5f);
            yield return new WaitForSeconds(1.2f);
            Say("zoo.spot." + hider.spot.SpotId, true);
            yield return WaitForVoice(5f);

            float giveUp = Time.time + TakeInGiveUpSeconds;
            while (!inside && Time.time < giveUp) yield return null;
            if (!inside)
            {
                hider.pen.PlaceInstantly(animal);
                hider.pen.OpenGate(false);
            }

            // A kind he has never found before: its sign lights up.
            if (!hider.wasKnown && hider.pen != null) hider.pen.LightSign();
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>
        /// Walks the player (controls off) to a point along the NavMesh path (so a rock or a fence in the way is walked round), then turns
        /// him to face a direction. Gives up after that many seconds in all.
        /// </summary>
        IEnumerator WalkPlayerOnNavMesh(Vector3 point, Vector3 face, float maxSeconds)
        {
            if (player == null) yield break;

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) point = hit.position;
            float giveUp = Time.time + maxSeconds;

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(player.transform.position, point, NavMesh.AllAreas, path) && path.corners.Length > 1)
            {
                // The first corner is where he stands. The last one gets the turn.
                for (int i = 1; i < path.corners.Length - 1 && Time.time < giveUp; i++)
                    yield return WalkPlayerTo(path.corners[i], Vector3.zero, giveUp - Time.time);
            }
            if (Time.time < giveUp) yield return WalkPlayerTo(point, face, giveUp - Time.time);
        }

        // ----- 7. How many did we find? ------------------------------------------------------------

        IEnumerator RunHowManyFound()
        {
            int found = foundOrder.Count;
            if (found <= 0 || lessonUI == null) yield break;

            yield return AskNumber(found, "zoo.how_many_found", CountPenAnimals, false);
        }

        /// <summary>After a wrong card: the animals in the pens hop one by one while we count them.</summary>
        IEnumerator CountPenAnimals()
        {
            int k = 0;
            foreach (Hider hider in foundOrder)
            {
                k++;
                if (hider.animal != null) hider.animal.HappyHop();
                Say("number." + k, true);
                yield return new WaitForSeconds(1f);
            }
            yield return WaitForVoice(3f);
        }

        // ----- 8. Party ----------------------------------------------------------------------------

        IEnumerator RunParty()
        {
            FreezePlayer(true);
            if (keeper != null) keeper.Stop();

            // The stars and the saved progress: the animals found, the round, the level.
            int found = foundOrder.Count;
            CommitProgress(true);
            RoundFinished?.Invoke(found + 1, PlayerPosition());

            // He and the keeper walk to the party spots in front of the pens.
            int busy = 0;
            if (keeper != null)
            {
                busy++;
                keeper.WalkTo(PartySpot(3), () => busy--, PartyWalkSeconds);
            }
            yield return WalkPlayerTo(PartySpot(2), Vector3.back, PartyWalkSeconds);
            float giveUp = Time.time + PartyWalkSeconds + 1f;
            while (busy > 0 && Time.time < giveUp) yield return null;
            yield return new WaitForSeconds(0.5f);

            // Dance, with the animals hopping in their pens.
            if (playerAnimator != null)
            {
                player.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                playerAnimator.SetDancing(true);
                playerDancing = true;
            }
            if (keeper != null)
            {
                keeper.FaceDirection(Vector3.back);
                keeper.SetDancing(true);
            }
            Sfx.Play("zoo.party", PartySpot(2));
            Say("zoo.victory", true);

            for (float elapsed = 0f; elapsed < PartySeconds; elapsed += 0.6f)
            {
                PartyBurst();
                yield return new WaitForSeconds(0.6f);
            }
            yield return WaitForVoice(4f);
            onVictory?.Invoke();
        }

        void PartyBurst()
        {
            if (player != null) BattleEffects.Confetti(player.transform.position + Vector3.up * 2f);
            if (keeper != null) BattleEffects.Confetti(keeper.transform.position + Vector3.up * 2f);
            foreach (Hider hider in foundOrder)
                if (hider.animal != null) hider.animal.HappyHop();
        }

        Vector3 PartySpot(int index)
        {
            if (partyPoints != null && partyPoints.Length > 0)
            {
                Transform point = partyPoints[index % partyPoints.Length];
                if (point != null) return point.position;
            }
            return PlayerPosition() + new Vector3((index - 2) * 1.4f, 0f, -1f);
        }
    }
}
