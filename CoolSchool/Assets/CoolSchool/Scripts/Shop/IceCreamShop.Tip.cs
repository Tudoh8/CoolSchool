using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// The tip coin ("one more"). Once the numbers lesson has taught one more / one less, a happy customer sometimes puts one
    /// extra coin on the counter and the child says how many coins that is now: the coins already in the register plus one.
    /// It never touches the shop level or the stars, and he can never fail: a wrong card is dimmed, the narrator counts along,
    /// and the right card wins by itself. Contract: Docs/specs/harder-numbers-contract.md (Agent C).
    /// </summary>
    public partial class IceCreamShop
    {
        const float TipChance = 0.5f;
        const int MaxTipsPerVisit = 2;
        const float TipPulseAfterSeconds = 12f;
        const float TipSolveAfterSeconds = 26f;

        int tipsGiven;
        int tipPicked = -1;
        ShopCoin tipCoin;

        /// <summary>The tip happens now and then, after the numbers lesson has taught "one more", at most twice per visit.</summary>
        bool ShouldTip()
        {
            if (lessonUI == null || tipsGiven >= MaxTipsPerVisit) return false;
            if (!NumberCurriculum.IsUnlocked(NumberSkill.OneMoreOneLess)) return false;

            return Random.value < TipChance;
        }

        /// <summary>
        /// Runs between the last coin in the register and the ka-ching. paid is the number of coins that are in the register.
        /// Returns at once when this customer does not tip.
        /// </summary>
        IEnumerator TipRoutine(ShopCustomer who, int paid)
        {
            if (!ShouldTip()) yield break;

            tipsGiven++;
            int answer = paid + 1;
            tipPicked = -1;

            // The customer holds out one more coin, and it hops onto the counter.
            yield return new WaitForSeconds(0.3f);
            who.PutCoin();
            Say("shop.tip." + KindName(who.Kind));

            Vector3 position = coinSpot.position + Vector3.up * (ShopCoin.Thickness * 0.5f + 0.03f);
            Vector3 towardsCamera = Camera.main != null ? Camera.main.transform.position - position : Vector3.back;
            tipCoin = ShopCoin.Create(goldMaterial, CoinStart(who), towardsCamera);
            tipCoin.transform.SetParent(Runtime, true);
            Sfx.Play("shop.coin_drop", tipCoin.transform.position);
            StartCoroutine(HopCoinRoutine(tipCoin, position));
            yield return WaitForSpeech();

            Say("shop.how_many_now");
            yield return WaitForSpeech();

            lessonUI.ShowNumberCards(TipCards(answer), OnTipCardPicked);
            float shownAt = Time.time;
            bool pulsing = false;

            while (true)
            {
                if (tipPicked >= 0)
                {
                    int picked = tipPicked;
                    tipPicked = -1;
                    if (picked == answer) break;

                    // Not this one: a friendly wiggle, then we count the coins together and the tip coin is the last one.
                    lessonUI.WiggleCard(picked);
                    lessonUI.DimCard(picked);
                    Say("lesson.try_again", true);
                    yield return new WaitForSeconds(1.8f);
                    yield return TipCountAlong(paid);
                    break;
                }

                if (!pulsing && Time.time - shownAt >= TipPulseAfterSeconds)
                {
                    pulsing = true;
                    lessonUI.PulseCard(answer);
                    if (tipCoin != null) tipCoin.SetPulse(true);
                }

                // Never stuck: after a long pause we count together and the right card wins.
                if (Time.time - shownAt >= TipSolveAfterSeconds)
                {
                    yield return TipCountAlong(paid);
                    break;
                }

                yield return null;
            }

            if (tipCoin != null) tipCoin.SetPulse(false);
            lessonUI.StopPulse();
            lessonUI.CelebrateCard(answer);
            Say("lesson.correct", true);
            PlayerCheer();
            if (current != null) current.Cheer();
            yield return new WaitForSeconds(1.8f);
            lessonUI.HideCards();

            // The tip coin goes into the register like all the others.
            if (tipCoin != null)
            {
                ShopCoin coin = tipCoin;
                tipCoin = null;
                coin.Taken = true;
                Sfx.Play("shop.coin_tap", coin.transform.position);
                StartCoroutine(FlyCoinRoutine(coin));
                while (coinsFlying > 0) yield return null;
            }
        }

        /// <summary>The narrator counts the coins in the register (it bumps for each), then the tip coin hops and is the last number.</summary>
        IEnumerator TipCountAlong(int paid)
        {
            for (int k = 1; k <= paid; k++)
            {
                if (register != null) register.Bump();
                Say("number." + k, true);
                yield return new WaitForSeconds(1f);
            }

            if (tipCoin != null)
            {
                tipCoin.SetPulse(true);
                StartCoroutine(ShopMotion.Fly(tipCoin.transform, tipCoin.transform.position, 0.45f, 0.3f));
            }
            Say("number." + (paid + 1), true);
            BattleEffects.Sparkle(coinSpot.position + Vector3.up * 0.2f);
            yield return new WaitForSeconds(1.2f);
        }

        void OnTipCardPicked(int number)
        {
            tipPicked = number;
        }

        /// <summary>The right answer plus two near numbers, shuffled (all between 1 and 20).</summary>
        static int[] TipCards(int answer)
        {
            var cards = new List<int> { answer };
            int[] offsets = { -1, 1, 2, -2, 3 };
            foreach (int offset in offsets)
            {
                int candidate = answer + offset;
                if (candidate < 1 || candidate > NumberCurriculum.HighestNumber || cards.Contains(candidate)) continue;

                cards.Add(candidate);
                if (cards.Count == 3) break;
            }

            for (int i = cards.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                int held = cards[i];
                cards[i] = cards[swap];
                cards[swap] = held;
            }
            return cards.ToArray();
        }

        /// <summary>Called by ResetEverything: no tip question is left on the screen and no tip coin on the counter.</summary>
        void ResetTip()
        {
            tipsGiven = 0;
            tipPicked = -1;
            tipCoin = null;
            if (lessonUI != null) lessonUI.HideCards();
        }
    }
}
