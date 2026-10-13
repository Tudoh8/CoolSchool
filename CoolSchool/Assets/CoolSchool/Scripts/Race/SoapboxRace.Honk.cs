using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The honk button: a round button on the race HUD plays the horn he picked (the starter bell until he has won another), with a
    /// sparkle on his cart. The rivals honk back now and then with their own random horns, but never often.
    /// </summary>
    public partial class SoapboxRace
    {
        const float HonkCooldownSeconds = 0.6f;
        const float HonkSoundSeconds = 1.5f;
        const float RivalHonkGapSeconds = 4f;
        const float RivalReplyChance = 0.35f;
        const float RivalReplyMinSeconds = 0.6f;
        const float RivalReplyMaxSeconds = 1.3f;
        const float RivalHonkMinSeconds = 9f;
        const float RivalHonkMaxSeconds = 16f;

        float lastHonk = -100f;
        float lastRivalHonk = -100f;
        float nextRivalHonk;
        float replyAt = -1f;

        void SubscribeHonk()
        {
            lastHonk = -100f;
            lastRivalHonk = -100f;
            replyAt = -1f;
            nextRivalHonk = Time.time + Random.Range(RivalHonkMinSeconds, RivalHonkMaxSeconds);

            if (hud != null)
            {
                hud.Honk += OnHonk;
                UpdateHonkIcon();
            }
            // A tap that starts on the honk button never changes lanes.
            if (input != null && hud != null) input.SetExtraButton(hud.HonkRect);
        }

        void UnsubscribeHonk()
        {
            if (hud != null) hud.Honk -= OnHonk;
            if (input != null) input.SetExtraButton(null);
        }

        /// <summary>The picture on the honk button is the horn he picked.</summary>
        void UpdateHonkIcon()
        {
            if (hud == null) return;

            RewardCatalog garage = GarageCatalog;
            RewardItem horn = garage != null ? garage.Find(ProgressStore.ChosenHornId()) : null;
            hud.SetHonkIcon(horn != null ? horn.icon : null);
        }

        void OnHonk()
        {
            if (phase != Phase.Countdown && phase != Phase.Racing && phase != Phase.Finishing) return;
            if (playerCart == null || Time.time - lastHonk < HonkCooldownSeconds) return;

            lastHonk = Time.time;
            Sfx.PlayShort(HornCue(ProgressStore.ChosenHornId()), HonkSoundSeconds, playerCart.transform.position);
            BattleEffects.Sparkle(playerCart.transform.position + Vector3.up * 1.5f);

            // A rival may honk back a moment later.
            if (rivals != null && rivals.Length > 0 && Time.time - lastRivalHonk >= RivalHonkGapSeconds && Random.value < RivalReplyChance)
                replyAt = Time.time + Random.Range(RivalReplyMinSeconds, RivalReplyMaxSeconds);
        }

        /// <summary>Called every frame of the race: the rivals honk now and then, at most once every few seconds.</summary>
        void UpdateRivalHonks()
        {
            if (rivals == null || rivals.Length == 0) return;

            if (replyAt > 0f && Time.time >= replyAt)
            {
                replyAt = -1f;
                RivalHonk(Random.Range(0, rivals.Length));
            }
            else if (Time.time >= nextRivalHonk)
            {
                nextRivalHonk = Time.time + Random.Range(RivalHonkMinSeconds, RivalHonkMaxSeconds);
                RivalHonk(Random.Range(0, rivals.Length));
            }
        }

        void RivalHonk(int index)
        {
            if (Time.time - lastRivalHonk < RivalHonkGapSeconds || index >= rivals.Length) return;

            CartController cart = rivals[index] != null ? rivals[index].Cart : null;
            if (cart == null || cart.Finished) return;

            lastRivalHonk = Time.time;
            string cue = index < rivalHornCues.Length && !string.IsNullOrEmpty(rivalHornCues[index]) ? rivalHornCues[index] : HornCue("");
            Sfx.PlayShort(cue, HonkSoundSeconds, cart.transform.position);
            BattleEffects.Sparkle(cart.transform.position + Vector3.up * 1.4f);
        }

        /// <summary>The sound cue of this horn item (the starter bell's when the id is empty or unknown).</summary>
        string HornCue(string hornId)
        {
            RewardCatalog garage = GarageCatalog;
            RewardItem horn = garage != null ? garage.Find(string.IsNullOrEmpty(hornId) ? ProgressStore.StarterHornId : hornId) : null;
            return horn != null && !string.IsNullOrEmpty(horn.soundCue) ? horn.soundCue : "race.horn_bell";
        }

        /// <summary>The cue of a horn picked at random from the whole garage (a rival's horn; owned or not).</summary>
        string RandomHornCue()
        {
            RewardCatalog garage = GarageCatalog;
            if (garage == null) return HornCue("");

            var horns = garage.ItemsOfPart(CartPart.Horn);
            return horns.Count == 0 ? HornCue("") : HornCue(horns[Random.Range(0, horns.Count)].id);
        }
    }
}
