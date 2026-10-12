using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>The end of the race: the podium (everybody cheers, he gets stars even when he is second), then everybody goes home.</summary>
    public partial class SoapboxRace
    {
        const float BrakeWaitSeconds = 1.3f;
        const float PodiumSeconds = 5f;
        const float PodiumCameraSeconds = 1f;

        IEnumerator RunFinish()
        {
            phase = Phase.Finishing;
            if (input != null) input.Active = false;
            arrowsOn = false;
            currentGate = null;
            if (hud != null)
            {
                hud.ShowArrows(false);
                hud.ShowTarget(null);
                hud.Countdown(0);
            }

            // Anybody who has not crossed the line (a very slow rival, or the safety timeout) stops now.
            foreach (CartController cart in allCarts)
                if (cart != null && !cart.Finished) cart.StopRunning(true);

            List<CartController> places = FinalPlaces();
            int place = Mathf.Clamp(places.IndexOf(playerCart) + 1, 1, 3);
            if (hud != null) hud.SetPlace(place);
            yield return new WaitForSeconds(BrakeWaitSeconds);

            // ----- The podium -----
            float podiumStart = Time.time;
            Vector3 spot = PodiumPoint(place - 1);

            if (chaseCamera != null && podiumCameraSpot != null) chaseCamera.MoveTo(podiumCameraSpot, PodiumCameraSeconds);
            if (announcer != null)
            {
                if (!announcerAtFinish && announcerFinishSpot != null)
                {
                    announcer.MoveTo(announcerFinishSpot);
                    announcerAtFinish = true;
                }
                announcer.FaceTowards(spot);
            }

            // Sitting off first: a sitting kid cannot cheer or dance.
            if (playerAnimator != null) playerAnimator.SetSitting(false);
            yield return new WaitForSeconds(0.2f);

            if (player != null)
            {
                player.InputEnabled = false;
                player.StopRiding(spot);
            }
            if (rivals != null)
            {
                foreach (RivalCartAI rival in rivals)
                {
                    if (rival == null || rival.Cart == null) continue;

                    int rivalPlace = Mathf.Clamp(places.IndexOf(rival.Cart) + 1, 1, 3);
                    rival.HopOutTo(PodiumPoint(rivalPlace - 1));
                }
            }

            VoicePlayer.Say(place == 1 ? "race.win" : "race.second", true);
            if (announcer != null) announcer.Cheer();
            Sfx.Play("race.cheer");

            // Landed on the podium: turn to the camera and cheer.
            yield return new WaitForSeconds(0.7f);
            if (player != null)
            {
                Vector3 toCamera = track != null ? -track.Forward : Vector3.back;
                yield return TurnTo(player.transform, Quaternion.LookRotation(toCamera, Vector3.up), 0.3f);
            }
            if (playerAnimator != null) playerAnimator.PlayCheer();
            for (int i = 0; i < 3; i++) BattleEffects.Confetti(PodiumPoint(i) + Vector3.up * 1.5f);

            // The stars, even when he was second.
            int stars = Mathf.Clamp(2 + gatesRight, 1, 5);
            RaceFinished?.Invoke(stars, spot);
            PlayerProgress progress = ProgressStore.Current;
            progress.raceCoinsTotal += coins;
            AdaptLevel();

            yield return new WaitForSeconds(1.2f);
            if (playerAnimator != null) playerAnimator.SetDancing(true);

            // Five seconds of party, then the announcer says goodbye.
            while (Time.time - podiumStart < PodiumSeconds) yield return null;
            yield return WaitForVoice(5f);

            VoicePlayer.Say("race.end", true);
            if (player != null) BattleEffects.Sparkle(player.transform.position + Vector3.up * 1.5f);
            yield return WaitForVoice(5f);

            // ----- Everybody back at the start -----
            if (playerAnimator != null) playerAnimator.SetDancing(false);
            if (player != null) BattleEffects.Sparkle(player.transform.position + Vector3.up);
            if (player != null && !player.IsRiding) TeleportPlayer();
            if (playerCart != null) playerCart.ResetToStart(1);
            if (rivals != null)
                for (int i = 0; i < rivals.Length; i++)
                    if (rivals[i] != null) rivals[i].ResetRival(i == 0 ? 0 : 2);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>The order of crossing, then anybody who has not crossed the line by how far they got.</summary>
        List<CartController> FinalPlaces()
        {
            var places = new List<CartController>(finishOrder);
            var rest = new List<CartController>();
            foreach (CartController cart in allCarts)
                if (cart != null && !places.Contains(cart)) rest.Add(cart);
            rest.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            places.AddRange(rest);
            return places;
        }

        /// <summary>Where the podium place (0 = first) is: the builder's spot, or a guess at the end of the street.</summary>
        Vector3 PodiumPoint(int index)
        {
            if (podiumSpots != null && index >= 0 && index < podiumSpots.Length && podiumSpots[index] != null)
                return podiumSpots[index].position;

            float finish = track != null ? track.FinishDistance : 450f;
            float side = index == 0 ? 0f : index == 1 ? -1.6f : 1.6f;
            return track != null ? track.PointAt(finish + 28f, side) : playerCart.transform.position;
        }

        static IEnumerator TurnTo(Transform who, Quaternion target, float seconds)
        {
            Quaternion from = who.rotation;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (who == null) yield break;

                who.rotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
                yield return null;
            }
            if (who != null) who.rotation = target;
        }
    }
}
