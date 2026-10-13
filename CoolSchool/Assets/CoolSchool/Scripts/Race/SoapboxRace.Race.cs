using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>The middle of the race: steering, bumping the rivals, the HUD, coins and what the announcer says about it.</summary>
    public partial class SoapboxRace
    {
        // How often the announcer comments (seconds between two lines of the same kind).
        const float CrashLineGap = 8f;
        const float SplashLineGap = 8f;
        const float RivalLineGap = 20f;
        const float BumpLineGap = 6f;
        const float AnimalLineGap = 4f;
        const float CrashLineChance = 0.5f;
        // A rival's bump zone, in metres along the road.
        const float BumpMeters = 1.6f;

        float quietUntil;
        float lastCrashLine = -100f;
        float lastSplashLine = -100f;
        float lastRivalLine = -100f;
        float lastBumpLine = -100f;
        float lastAnimalLine = -100f;
        bool rampLineSaid;
        bool finalLineSaid;
        bool tauntSaid;
        int lastPlace = 1;

        void ResetRaceVoiceState()
        {
            quietUntil = 0f;
            lastCrashLine = lastSplashLine = lastRivalLine = lastBumpLine = lastAnimalLine = -100f;
            rampLineSaid = false;
            finalLineSaid = false;
            tauntSaid = false;
            lastPlace = 1;
        }

        void Subscribe()
        {
            if (input != null)
            {
                input.LaneChange += OnLaneChange;
                input.Jump += OnJump;
            }
            if (track != null)
            {
                track.AnimalCrossing += OnAnimalCrossing;
                track.RampTaken += OnRampTaken;
                track.CoinCollected += OnCoinCollected;
            }
            if (allCarts != null)
            {
                foreach (CartController cart in allCarts)
                {
                    if (cart == null) continue;
                    cart.CrossedFinish += OnCartCrossedFinish;
                    cart.Bonked += OnCartBonked;
                    cart.Splashed += OnCartSplashed;
                }
            }
        }

        void Unsubscribe()
        {
            if (input != null)
            {
                input.LaneChange -= OnLaneChange;
                input.Jump -= OnJump;
            }
            if (track != null)
            {
                track.AnimalCrossing -= OnAnimalCrossing;
                track.RampTaken -= OnRampTaken;
                track.CoinCollected -= OnCoinCollected;
            }
            if (allCarts != null)
            {
                foreach (CartController cart in allCarts)
                {
                    if (cart == null) continue;
                    cart.CrossedFinish -= OnCartCrossedFinish;
                    cart.Bonked -= OnCartBonked;
                    cart.Splashed -= OnCartSplashed;
                }
            }
            UnsubscribeGates();
        }

        // ----- The race loop -----------------------------------------------------------------------

        IEnumerator RunRace()
        {
            float finishedAt = -1f;
            while (true)
            {
                if (playerCart == null) break;

                if (!playerCart.Finished) RaceTick();
                else if (finishedAt < 0f) finishedAt = Time.time;

                // Done when everybody crossed the line; the rivals get a few seconds after the player at most.
                if (playerCart.Finished && (AllCartsFinished() || Time.time - finishedAt > RivalGraceSeconds)) break;
                if (Time.time - raceStartTime > RaceTimeoutSeconds) break;
                yield return null;
            }
            phase = Phase.Finishing;
        }

        bool AllCartsFinished()
        {
            foreach (CartController cart in allCarts)
                if (cart != null && !cart.Finished) return false;
            return true;
        }

        void RaceTick()
        {
            float elapsed = Time.time - raceStartTime;
            float distance = playerCart.Distance;

            // A little slower while a gate is announced, to give him time to think. A ramp arc keeps its own speed.
            float cruise = CruiseSpeedByLevel[level - 1];
            bool thinking = currentGate != null && !playerCart.IsAirborne;
            playerCart.TargetSpeed = cruise * (thinking ? GateSlowFactor : 1f);

            UpdateGates();
            UpdateHud();
            UpdateRivalHonks();

            if (arrowsOn && (elapsed > ArrowsGoneSeconds || laneChanges >= ArrowsGoneAfterLaneChanges))
            {
                arrowsOn = false;
                if (hud != null) hud.ShowArrows(false);
            }

            if (!finalLineSaid && distance >= FinalLineDistance && TryAmbient("race.final"))
                finalLineSaid = true;
            if (!tauntSaid && distance >= RivalTauntDistance && TryAmbient("race.rival"))
            {
                tauntSaid = true;
                lastRivalLine = Time.time;
            }
        }

        void UpdateHud()
        {
            int place = CurrentPlace();
            if (hud != null)
            {
                hud.SetPlace(place);
                float finish = Mathf.Max(1f, track != null ? track.FinishDistance : 450f);
                hud.SetProgress(Progress01(playerCart, finish), Progress01(RivalCart(0), finish), Progress01(RivalCart(1), finish));
            }

            // Somebody just drove past: a taunt from the rival, not too often.
            if (place > lastPlace && Time.time - lastRivalLine >= RivalLineGap && TryAmbient("race.rival"))
                lastRivalLine = Time.time;
            lastPlace = place;
        }

        /// <summary>1 to 3: after the finish by the order of crossing, before it by who is furthest.</summary>
        int CurrentPlace()
        {
            int crossed = finishOrder.IndexOf(playerCart);
            if (crossed >= 0) return crossed + 1;

            int place = 1;
            foreach (CartController cart in allCarts)
            {
                if (cart == null || cart == playerCart) continue;
                if (cart.Finished || cart.Distance > playerCart.Distance) place++;
            }
            return Mathf.Clamp(place, 1, 3);
        }

        static float Progress01(CartController cart, float finish)
        {
            return cart != null ? Mathf.Clamp01(cart.Distance / finish) : 0f;
        }

        CartController RivalCart(int index)
        {
            return rivals != null && index < rivals.Length && rivals[index] != null ? rivals[index].Cart : null;
        }

        // ----- Input -------------------------------------------------------------------------------

        void OnLaneChange(int direction)
        {
            if (phase != Phase.Racing || playerCart == null || playerCart.Finished) return;
            if (!playerCart.ChangeLane(direction)) return;

            laneChanges++;
            BumpRivalsInLane(playerCart.Lane);
        }

        void OnJump()
        {
            if (phase != Phase.Racing || playerCart == null || playerCart.Finished) return;

            playerCart.Jump();
        }

        /// <summary>Steering into a rival makes him spin out, giggling. The player only gets a tiny wobble.</summary>
        void BumpRivalsInLane(int lane)
        {
            if (rivals == null) return;

            foreach (RivalCartAI rival in rivals)
            {
                CartController cart = rival != null ? rival.Cart : null;
                if (cart == null || cart.IsSpinning || cart.Finished) continue;
                if (cart.Lane != lane || Mathf.Abs(cart.Distance - playerCart.Distance) > BumpMeters) continue;

                cart.SpinOut();
                playerCart.Wobble(0.35f);
                if (Time.time - lastBumpLine >= BumpLineGap)
                {
                    lastBumpLine = Time.time;
                    VoicePlayer.Say("race.bump");
                }
            }
        }

        // ----- Things that happen on the track -----------------------------------------------------

        void OnCartBonked(CartController cart)
        {
            if (cart == null || !cart.IsPlayer || phase != Phase.Racing) return;

            // A crash costs one coin, which pops out of the cart.
            if (coins > 0)
            {
                coins--;
                if (hud != null) hud.SetCoins(coins, false);
                BattleEffects.Sparkle(cart.transform.position + Vector3.up * 1.2f);
            }
            TryAmbient("race.crash", ref lastCrashLine, CrashLineGap, CrashLineChance);
        }

        void OnCartSplashed(CartController cart)
        {
            if (cart == null || !cart.IsPlayer || phase != Phase.Racing) return;

            TryAmbient("race.splash", ref lastSplashLine, SplashLineGap, 1f);
        }

        void OnAnimalCrossing(AnimalCrosser animal)
        {
            if (animal == null || phase != Phase.Racing) return;

            string kind = string.IsNullOrEmpty(animal.Kind) ? "cat" : animal.Kind;
            TryAmbient("race.animal_" + kind, ref lastAnimalLine, AnimalLineGap, 1f);
        }

        void OnRampTaken(RaceRamp ramp, CartController cart)
        {
            if (cart == null || !cart.IsPlayer || rampLineSaid || phase != Phase.Racing) return;

            // He is in the air: this one is worth saying even if the line is a bit late.
            if (TryAmbient("race.ramp")) rampLineSaid = true;
        }

        void OnCoinCollected(RaceCoin coin, CartController cart)
        {
            if (cart == null || !cart.IsPlayer) return;

            coins++;
            if (hud != null) hud.SetCoins(coins, true);
        }

        void OnCartCrossedFinish(CartController cart)
        {
            if (cart == null || finishOrder.Contains(cart)) return;

            finishOrder.Add(cart);
            cart.StopRunning(true);
            if (!cart.IsPlayer) return;

            // The player is over the line: cheering, confetti, and the screen goes quiet.
            if (input != null) input.Active = false;
            arrowsOn = false;
            if (hud != null)
            {
                hud.ShowArrows(false);
                hud.ShowTarget(null);
                hud.SetPlace(CurrentPlace());
            }
            if (currentGate != null)
            {
                currentGate.LightUp(false);
                currentGate = null;
            }

            Sfx.Play("race.cheer");
            Vector3 over = track != null ? track.PointAt(track.FinishDistance, 0f) + Vector3.up * 3.5f : cart.transform.position;
            BattleEffects.Confetti(over);
            if (finishArch != null) finishArch.Celebrate();
        }

        // ----- What the announcer says -------------------------------------------------------------

        /// <summary>True when nobody is talking and no gate question is being asked.</summary>
        bool CanTalk()
        {
            if (phase != Phase.Racing || Time.time < quietUntil) return false;
            return VoicePlayer.Instance == null || !VoicePlayer.Instance.IsBusy;
        }

        bool TryAmbient(string group)
        {
            if (!CanTalk()) return false;

            VoicePlayer.Say(group);
            return true;
        }

        bool TryAmbient(string group, ref float last, float gap, float chance)
        {
            if (Time.time - last < gap || Random.value > chance || !CanTalk()) return false;

            last = Time.time;
            VoicePlayer.Say(group);
            return true;
        }
    }
}
