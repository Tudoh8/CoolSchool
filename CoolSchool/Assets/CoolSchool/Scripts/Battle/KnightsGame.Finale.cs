using System.Collections;
using CoolSchool.Castle;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The end of the castle adventure: everyone gathers at the gate, his name goes on the flag, the castle grows, the cheer,
    /// the stars, and the dragon flies away happy.
    /// </summary>
    public partial class KnightsGame
    {
        const float NameFlagTimeoutSeconds = 130f;
        const float GrowthTimeoutSeconds = 15f;

        /// <summary>Hooray! Everyone runs to the gate first, so the cheer happens right in front of the camera.</summary>
        IEnumerator RunFinale()
        {
            finished = true;
            SetPlayerMode(PlayerMode.Frozen);

            if (queen != null && queenCelebrateSpot != null) queen.WalkTo(queenCelebrateSpot.position);
            if (babyDragon != null && babyCelebrateSpot != null && babyHatched) babyDragon.HopTo(babyCelebrateSpot.position);
            yield return GatherAtGate();

            if (letterMoment == CastleLetterMoment.NameFlag && nameFlag != null && nameFlag.CanRun)
                yield return Bounded(nameFlag.Run(level, NewResult()), NameFlagTimeoutSeconds);

            PlayerProgress progress = ProgressStore.Current;
            progress.castleWins++;
            ProgressStore.Save();
            if (growth != null) yield return Bounded(growth.CelebrateNewest(), GrowthTimeoutSeconds);

            CheerAll();
            BattleEffects.Confetti(PlayerPosition() + Vector3.up * 1.5f);
            VoicePlayer.Say("castle.victory");
            onVictory?.Invoke();

            if (dragon != null && dragon.gameObject.activeInHierarchy)
            {
                dragon.FlyAwayHappy();
                VoicePlayer.Say("castle.dragon_bye");
            }

            AdaptLevel();
            yield return new WaitForSeconds(victorySeconds);
        }

        /// <summary>
        /// The player and the knights run to the celebrate spot just inside the gate and turn to face the camera. The player runs
        /// by hand (his controls are off), the knights by the navigation. If the player is stuck he is lifted to the spot. Without
        /// a celebrate spot nobody moves.
        /// </summary>
        IEnumerator GatherAtGate()
        {
            if (celebrateSpot == null) yield break;

            Vector3 spot = celebrateSpot.position;
            Vector3 faceCamera = Vector3.back;

            // Nobody wanders off: the player's controls and the knights' fighting brain stay off (the tag brain is off already).
            EnableAllies(false);

            walkersBusy = 0;
            if (player != null) walkRoutines.Add(StartCoroutine(WalkPlayerTo(spot, faceCamera)));
            if (allies != null)
            {
                int slot = 0;
                foreach (KnightAllyAI ally in allies)
                {
                    if (ally == null || !ally.gameObject.activeInHierarchy) continue;

                    walkRoutines.Add(StartCoroutine(WalkAllyTo(ally, AllySpot(spot, slot++), faceCamera)));
                }
            }

            // The walks give up by themselves; the extra second is for turning round at the end.
            float giveUp = Time.time + GatherGiveUpSeconds + 1f;
            while (walkersBusy > 0 && Time.time < giveUp) yield return null;
            StopWalks();

            if (player != null)
            {
                Vector3 away = spot - player.transform.position;
                away.y = 0f;
                if (away.magnitude > ArriveDistance * 2f) player.Teleport(spot, Quaternion.LookRotation(faceCamera, Vector3.up));
            }
        }

        /// <summary>
        /// Where a knight stands around the player: the first three in a little arc behind him, the extra ones continue
        /// outwards on both sides. "Behind" is away from the camera (larger z).
        /// </summary>
        static Vector3 AllySpot(Vector3 playerSpot, int index)
        {
            if (index < AllyArc.Length) return playerSpot + new Vector3(AllyArc[index].x, 0f, AllyArc[index].y);

            int extra = index - AllyArc.Length;
            float side = extra % 2 == 0 ? -1f : 1f;
            return playerSpot + new Vector3(side * (3.2f + 1.5f * (extra / 2)), 0f, 1.6f);
        }
    }
}
