using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The harder number ideas at the treasure chest (contract Docs/specs/harder-numbers-contract.md, "Agent D"). Each visit picks
    /// one extra from the ideas the classroom has taught (never the same as last visit): two piles of gold to compare, a bucket
    /// that hides some of the coins, or a chest that already holds some coins and he counts on from there.
    /// The stars are only the coins that came out of the chest this visit (the chosen pile, all the coins of the hiding game,
    /// or the new coins of the count-on game), never the coins that were "already in the chest".
    /// Everything here survives OnEnd at any moment: ResetNumberExtras (called from ResetWorld) hides the bucket, removes the
    /// floating digit and forgets the piles; the chest destroys its coins.
    /// </summary>
    public partial class PirateAdventure
    {
        enum TreasureExtra { None, Compare, Hidden, CountOn }

        [Header("Number extras at the chest")]
        [Tooltip("For testing: 0 = by the numbers level, 1 = compare two piles, 2 = hidden coins, 3 = count on. Ignores the numbers level.")]
        [SerializeField, Range(0, 3)] int debugForceExtra;

        // The extra of the last visit, so two visits in a row differ (also across rounds in one run of the game).
        static TreasureExtra lastTreasureExtra = TreasureExtra.None;

        // How far Lorre's count of the first coin is shifted: the count-on game says K + 1, K + 2, ...
        int coinNumberOffset;

        const float PileHintAfterSeconds = 30f;
        const float PileRemindSeconds = 12f;
        const float PileAutoSeconds = 55f;
        const float HiddenHintAfterSeconds = 20f;
        const float HiddenAutoSeconds = 60f;
        const float CountStepSeconds = 0.85f;
        // The middle of a coin lying on the sand above the sand (the same height the chest's fan uses).
        const float CoinRestHeight = 0.42f;
        const float BucketDropHeight = 2.6f;
        const float BucketLiftHeight = 1.0f;
        const float DigitLiftAboveChest = 1.0f;
        const float ParrotHeightWithDigit = 3.8f;
        const float ParrotHeightAtChest = 2.4f;
        static readonly Color SandPuff = new Color(0.86f, 0.74f, 0.5f, 1f);

        // Compare: which pile each coin belongs to, and the pile he tapped.
        readonly Dictionary<TreasureCoin, int> pileOf = new Dictionary<TreasureCoin, int>();
        TreasureCoin[] pileCoins = new TreasureCoin[0];
        int pickedPile = -1;

        // Hidden: where the bucket stands, and whether it is up in the air.
        Vector3 bucketGround;
        bool bucketLifted;
        Vector3 bucketRestScale = Vector3.one;
        bool bucketScaleKnown;

        // Count on: the gold digit above the chest.
        PirateDigitLabel digit;

        // ----- Choosing -----------------------------------------------------------------------------

        /// <summary>One extra for this visit: any of the unlocked ones or none, but not the same as last time.</summary>
        TreasureExtra ChooseTreasureExtra()
        {
            TreasureExtra pick;
            if (debugForceExtra > 0)
            {
                pick = (TreasureExtra)debugForceExtra;
            }
            else
            {
                var options = new List<TreasureExtra> { TreasureExtra.None };
                if (lessonUI != null)
                {
                    if (NumberCurriculum.IsUnlocked(NumberSkill.Compare)) options.Add(TreasureExtra.Compare);
                    if (NumberCurriculum.IsUnlocked(NumberSkill.Hidden)) options.Add(TreasureExtra.Hidden);
                    if (NumberCurriculum.IsUnlocked(NumberSkill.CountOn)) options.Add(TreasureExtra.CountOn);
                }

                if (options.Count > 1) options.Remove(lastTreasureExtra);
                pick = options[Random.Range(0, options.Count)];
            }

            lastTreasureExtra = pick;
            return pick;
        }

        /// <summary>Safe to call at any time: the bucket away, the digit gone, the piles forgotten.</summary>
        void ResetNumberExtras()
        {
            coinNumberOffset = 0;
            pickedPile = -1;
            pileOf.Clear();
            pileCoins = new TreasureCoin[0];
            if (coinPicker != null) coinPicker.Picked -= OnPilePicked;

            RemoveDigit();
            HideBucketNow();
        }

        // ----- Extra 1: compare two piles ------------------------------------------------------------

        IEnumerator RunCompareTreasure()
        {
            // Two piles from the pirates level's coin range, differing by at least two, inside what he has learned and what the chest spills.
            int high = Mathf.Min(ByLevel(CoinsMaxByLevel, level), Mathf.Min(NumberCurriculum.MaxNumber(), TreasureChest.FanSize));
            int low = Mathf.Clamp(ByLevel(CoinsMinByLevel, level), 1, Mathf.Max(1, high - 2));
            var pairs = new List<Vector2Int>();
            for (int small = low; small <= high; small++)
                for (int big = small + 2; big <= high; big++)
                    pairs.Add(new Vector2Int(small, big));

            if (pairs.Count == 0)
            {
                yield return RunPlainTreasure();
                yield break;
            }

            Vector2Int sizes = pairs[Random.Range(0, pairs.Count)];
            int bigPile = Random.value < 0.5f ? 0 : 1;
            TreasureCoin[][] piles = chest.SpillPiles(bigPile == 0 ? sizes.y : sizes.x, bigPile == 0 ? sizes.x : sizes.y);
            TreasureCoin[] bigCoins = piles[bigPile];
            TreasureCoin[] smallCoins = piles[1 - bigPile];
            if (bigCoins.Length == 0)
            {
                yield return RunPlainTreasure();
                yield break;
            }

            var all = new List<TreasureCoin>();
            pileOf.Clear();
            for (int pile = 0; pile < 2; pile++)
            {
                foreach (TreasureCoin coin in piles[pile])
                {
                    all.Add(coin);
                    pileOf[coin] = pile;
                }
            }
            pileCoins = all.ToArray();
            if (coinPicker != null) coinPicker.SetCoins(pileCoins);

            VoicePlayer.Say("pirates.compare");
            yield return new WaitForSeconds(1.6f);

            // He taps any coin of the pile he thinks is bigger.
            pickedPile = -1;
            if (coinPicker != null)
            {
                coinPicker.Picked += OnPilePicked;
                coinPicker.Armed = true;
            }

            float quietSince = Time.time;
            float nextRemind = quietSince + PileRemindSeconds;
            bool hinted = false;
            bool pulsing = false;
            while (true)
            {
                if (pickedPile >= 0)
                {
                    int pile = pickedPile;
                    pickedPile = -1;
                    quietSince = Time.time;
                    nextRemind = quietSince + PileRemindSeconds;
                    SetPulse(bigCoins, false);
                    pulsing = false;
                    if (pile == bigPile) break;

                    // Never a failure: the pile hops, and we count both piles together.
                    mistakes++;
                    HopAll(piles[pile]);
                    if (!hinted)
                    {
                        hinted = true;
                        yield return CompareHint(piles, bigCoins);
                        pulsing = true;
                    }
                    else
                    {
                        VoicePlayer.Say("lesson.try_again", true);
                        SetPulse(bigCoins, true);
                        pulsing = true;
                    }
                    quietSince = Time.time;
                    nextRemind = quietSince + PileRemindSeconds;
                }
                else if (Time.time - quietSince > PileAutoSeconds)
                {
                    break;
                }
                else if (!hinted && Time.time - quietSince > PileHintAfterSeconds)
                {
                    hinted = true;
                    yield return CompareHint(piles, bigCoins);
                    pulsing = true;
                    quietSince = Time.time;
                    nextRemind = Time.time + PileRemindSeconds;
                }
                else if (Time.time >= nextRemind)
                {
                    nextRemind = Time.time + PileRemindSeconds;
                    VoicePlayer.Say("pirates.compare");
                    if (!pulsing)
                    {
                        SetPulse(bigCoins, true);
                        pulsing = true;
                    }
                }
                yield return null;
            }

            if (coinPicker != null)
            {
                coinPicker.Armed = false;
                coinPicker.Picked -= OnPilePicked;
            }
            pileOf.Clear();

            // The bigger pile: its coins pulse and Lorre squawks. The small pile is a present for the skeletons.
            SetPulse(bigCoins, true);
            if (parrot != null) parrot.Squawk();
            Cheer(player);
            yield return new WaitForSeconds(1.3f);
            SetPulse(bigCoins, false);

            VoicePlayer.Say("pirates.compare_share");
            yield return ShareWithSkeletons(smallCoins);
            yield return WaitForVoice(5f);

            // The bigger pile goes into the chest the usual way, then the stars are those coins.
            coinNumberOffset = 0;
            yield return TapCoinsIntoChest(bigCoins, "pirates.count");
            yield return AskHowMany(bigCoins.Length);
            FinishCounted(bigCoins.Length);
        }

        void OnPilePicked(TreasureCoin coin)
        {
            if (coin == null || !pileOf.TryGetValue(coin, out int pile)) return;

            pickedPile = pile;
            // Every coin stays tappable (the picker forgets a coin once it has been picked).
            if (coinPicker != null) coinPicker.SetCoins(pileCoins);
        }

        /// <summary>"We count them both": both piles are counted aloud, the left one first, then the bigger pile pulses.</summary>
        IEnumerator CompareHint(TreasureCoin[][] piles, TreasureCoin[] bigCoins)
        {
            if (coinPicker != null) coinPicker.Armed = false;

            VoicePlayer.Say("pirates.compare_hint", true);
            yield return WaitForVoice(6f);
            for (int pile = 0; pile < 2; pile++)
            {
                yield return CountCoinsAloud(piles[pile]);
                yield return new WaitForSeconds(0.7f);
            }

            SetPulse(bigCoins, true);
            pickedPile = -1;
            if (coinPicker != null)
            {
                coinPicker.SetCoins(pileCoins);
                coinPicker.Armed = true;
            }
        }

        /// <summary>Each coin pulses while Lorre says its number (1 to the number of coins).</summary>
        IEnumerator CountCoinsAloud(TreasureCoin[] coins)
        {
            for (int k = 1; k <= coins.Length; k++)
            {
                TreasureCoin coin = coins[k - 1];
                if (coin != null) coin.Pulse(true);
                VoicePlayer.Say("number." + k, true);
                yield return new WaitForSeconds(CountStepSeconds);
                if (coin != null) coin.Pulse(false);
            }
        }

        /// <summary>The small pile flies toward the skeleton crew (the friends) and sparkles away; they cheer.</summary>
        IEnumerator ShareWithSkeletons(TreasureCoin[] coins)
        {
            var friends = new List<SkeletonPirateAI>();
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null && skeleton.gameObject.activeInHierarchy) friends.Add(skeleton);

            for (int i = 0; i < coins.Length; i++)
            {
                if (coins[i] == null) continue;

                SkeletonPirateAI target = friends.Count > 0 ? friends[i % friends.Count] : null;
                StartCoroutine(FlyCoinAway(coins[i], target, i * 0.15f));
            }
            yield return new WaitForSeconds(coins.Length * 0.15f + 1.0f);
        }

        IEnumerator FlyCoinAway(TreasureCoin coin, SkeletonPirateAI target, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (coin == null) yield break;

            coin.Pulse(false);
            Vector3 point = target != null ? target.transform.position + Vector3.up * 1.1f : coin.transform.position + Vector3.up * 3f;
            Sfx.Play("pirates.coin", coin.transform.position);
            coin.FlyInto(point, () =>
            {
                BattleEffects.Sparkle(point);
                if (target != null) Cheer(target);
            });
        }

        static void SetPulse(TreasureCoin[] coins, bool on)
        {
            foreach (TreasureCoin coin in coins)
                if (coin != null) coin.Pulse(on);
        }

        static void HopAll(TreasureCoin[] coins)
        {
            foreach (TreasureCoin coin in coins)
                if (coin != null) coin.Hop();
        }

        // ----- Extra 2: coins hidden under the bucket -------------------------------------------------

        IEnumerator RunHiddenTreasure()
        {
            int top = Mathf.Min(NumberCurriculum.CurrentLevel() >= 8 ? 10 : 5, Mathf.Min(NumberCurriculum.MaxNumber(), TreasureChest.FanSize));
            if (debugForceExtra == (int)TreasureExtra.Hidden) top = Mathf.Max(top, 5);
            int total = Random.Range(3, Mathf.Max(3, top) + 1);
            int hiddenCount = Random.Range(1, total);

            TreasureCoin[] coins = chest.SpillCoins(total);
            if (coins.Length < 3 || hidingBucket == null || lessonUI == null)
            {
                // Nothing to hide under: the usual count with these coins.
                if (coins.Length == 0) yield break;
                yield return TapCoinsIntoChest(coins, "pirates.count");
                yield return AskHowMany(coins.Length);
                FinishCounted(coins.Length);
                yield break;
            }
            total = coins.Length;
            hiddenCount = Mathf.Min(hiddenCount, total - 1);
            yield return new WaitForSeconds(0.9f + total * 0.09f);

            // Lorre counts all of them himself.
            yield return CountCoinsAloud(coins);

            // The hidden ones are the rightmost coins as the camera sees them (the camera looks north, so more x is more right).
            var home = new Dictionary<TreasureCoin, Vector3>();
            var byRight = new List<TreasureCoin>(coins);
            foreach (TreasureCoin coin in coins) home[coin] = coin.transform.position;
            byRight.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
            var hidden = byRight.GetRange(0, hiddenCount);

            float maxX = float.MinValue;
            foreach (TreasureCoin coin in coins) maxX = Mathf.Max(maxX, coin.transform.position.x);
            Vector3 under = new Vector3(maxX + 0.8f, chest.GroundY, byRight[0].transform.position.z);

            VoicePlayer.Say("pirates.hidden");
            yield return DropBucket(under);
            yield return SlideCoinsUnder(hidden, under);
            yield return WaitForVoice(5f);

            VoicePlayer.Say("pirates.hidden_ask");
            yield return AskHiddenCount(hiddenCount, total, hidden, under);

            // The bucket lifts and the coins come back to where they lay; then all of them are counted into the chest as usual.
            yield return RevealHiddenCoins(hidden, home, under);
            yield return TapCoinsIntoChest(coins, "pirates.count");
            FinishCounted(total);
        }

        /// <summary>"How many are under the bucket?" Never a failure: the bucket lifts and the hidden coins are counted aloud.</summary>
        IEnumerator AskHiddenCount(int answer, int total, List<TreasureCoin> hiddenCoins, Vector3 under)
        {
            int[] cards = MakeNumberCards(answer, ByLevel(NumberCardsByLevel, level), total - 1);
            pickedCard = -1;
            lessonUI.ShowNumberCards(cards, n => pickedCard = n);

            int wrong = 0;
            int wrongLeft = cards.Length - 1;
            float quietSince = Time.time;
            float nextHint = quietSince + HiddenHintAfterSeconds;
            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;
                    quietSince = Time.time;
                    nextHint = quietSince + HiddenHintAfterSeconds;
                    if (picked == answer) break;

                    mistakes++;
                    wrong++;
                    wrongLeft--;
                    lessonUI.WiggleCard(picked);
                    lessonUI.DimCard(picked);
                    VoicePlayer.Say("lesson.try_again", true);
                    if (wrong >= 2 || wrongLeft <= 0)
                    {
                        yield return new WaitForSeconds(1.6f);
                        yield return LookUnderBucket(hiddenCoins, under);
                        lessonUI.PulseCard(answer);
                        quietSince = Time.time;
                        nextHint = quietSince + HiddenHintAfterSeconds;
                    }
                }
                else if (Time.time >= nextHint)
                {
                    // Quiet for a while: take a look under the bucket together.
                    nextHint = Time.time + HiddenHintAfterSeconds;
                    yield return LookUnderBucket(hiddenCoins, under);
                    lessonUI.PulseCard(answer);
                }
                else if (Time.time - quietSince > HiddenAutoSeconds)
                {
                    break;
                }
                yield return null;
            }

            lessonUI.StopPulse();
            lessonUI.CelebrateCard(answer);
            VoicePlayer.Say("lesson.correct", true);
            if (parrot != null) parrot.Squawk();
            CheerEveryone();
            yield return new WaitForSeconds(1.8f);
            lessonUI.HideCards();
        }

        /// <summary>The bucket lifts, the hidden coins hop out in a row and are counted aloud, then they slip back and the bucket drops again.</summary>
        IEnumerator LookUnderBucket(List<TreasureCoin> hiddenCoins, Vector3 under)
        {
            VoicePlayer.Say("pirates.hidden_reveal", true);
            yield return LiftBucket(BucketLiftHeight);

            for (int i = 0; i < hiddenCoins.Count; i++)
                StartCoroutine(PopCoinOut(hiddenCoins[i], under, RevealSpot(under, i, hiddenCoins.Count), i * 0.12f));
            yield return new WaitForSeconds(0.7f + hiddenCoins.Count * 0.12f);

            yield return CountCoinsAloud(hiddenCoins.ToArray());
            yield return new WaitForSeconds(0.4f);

            yield return SlideCoinsUnder(hiddenCoins, under);
            yield return DropBucket(under);
        }

        /// <summary>The right answer: the bucket lifts away and the hidden coins hop back to where they lay.</summary>
        IEnumerator RevealHiddenCoins(List<TreasureCoin> hiddenCoins, Dictionary<TreasureCoin, Vector3> home, Vector3 under)
        {
            yield return LiftBucket(BucketLiftHeight);
            for (int i = 0; i < hiddenCoins.Count; i++)
            {
                TreasureCoin coin = hiddenCoins[i];
                Vector3 spot = home.TryGetValue(coin, out Vector3 found) ? found : RevealSpot(under, i, hiddenCoins.Count);
                StartCoroutine(PopCoinOut(coin, under, spot, i * 0.12f));
            }
            StartCoroutine(RemoveBucket());
            yield return new WaitForSeconds(0.9f + hiddenCoins.Count * 0.12f);
        }

        /// <summary>A row of spots just south of the bucket (toward the camera), five to a row, centred under the bucket.</summary>
        static Vector3 RevealSpot(Vector3 under, int index, int total)
        {
            int row = index / 5;
            int inRow = Mathf.Min(5, total - row * 5);
            float column = index % 5 - (inRow - 1) * 0.5f;
            return new Vector3(under.x + column * 0.45f, under.y + CoinRestHeight, under.z - 0.7f - row * 0.45f);
        }

        IEnumerator SlideCoinsUnder(List<TreasureCoin> coins, Vector3 under)
        {
            for (int i = 0; i < coins.Count; i++)
                StartCoroutine(SlideUnder(coins[i], under, i * 0.15f));
            yield return new WaitForSeconds(0.55f + coins.Count * 0.15f + 0.2f);
        }

        /// <summary>The coin glides under the bucket, shrinking out of sight, and switches off.</summary>
        IEnumerator SlideUnder(TreasureCoin coin, Vector3 under, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (coin == null) yield break;

            coin.Pulse(false);
            Sfx.Play("pirates.coin", coin.transform.position);
            Vector3 start = coin.transform.position;
            Vector3 end = new Vector3(under.x, under.y + 0.2f, under.z);
            const float seconds = 0.55f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (coin == null) yield break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                coin.transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.12f);
                coin.transform.localScale = Vector3.one * (1f - Mathf.SmoothStep(0.45f, 1f, k));
                yield return null;
            }

            if (coin == null) yield break;

            coin.transform.localScale = Vector3.zero;
            coin.gameObject.SetActive(false);
        }

        /// <summary>A hidden coin pops out from under the bucket and hops to its spot.</summary>
        IEnumerator PopCoinOut(TreasureCoin coin, Vector3 under, Vector3 spot, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (coin == null) yield break;

            coin.transform.position = new Vector3(under.x, under.y + 0.3f, under.z);
            coin.transform.localScale = Vector3.zero;
            coin.gameObject.SetActive(true);
            coin.Appear();
            Sfx.Play("pirates.coin", under);
            yield return TreasureMotion.Fly(coin.transform, spot, 0.5f, 0.5f);
        }

        // ----- The bucket ----------------------------------------------------------------------------

        Vector3 BucketRestScale()
        {
            if (!bucketScaleKnown && hidingBucket != null)
            {
                bucketRestScale = hidingBucket.localScale;
                bucketScaleKnown = true;
            }
            return bucketRestScale;
        }

        /// <summary>It drops from above onto the sand with a little squash and a puff of sand.</summary>
        IEnumerator DropBucket(Vector3 ground)
        {
            if (hidingBucket == null) yield break;

            Vector3 rest = BucketRestScale();
            Vector3 from;
            if (bucketLifted && hidingBucket.gameObject.activeSelf)
            {
                from = hidingBucket.position;
            }
            else
            {
                from = ground + Vector3.up * BucketDropHeight;
                hidingBucket.position = from;
                hidingBucket.localScale = rest;
                hidingBucket.gameObject.SetActive(true);
            }

            bucketGround = ground;
            hidingBucket.localScale = rest;
            Transform bucket = hidingBucket;
            bucket.position = new Vector3(ground.x, from.y, ground.z);
            yield return TreasureMotion.Fly(bucket, ground, 0.45f, 0f);
            bucketLifted = false;

            Sfx.Play("pirates.dig", ground);
            BattleEffects.Confetti(ground + Vector3.up * 0.05f, SandPuff);
            yield return TreasureMotion.Punch(bucket, rest, new Vector3(1.12f, 0.85f, 1.12f), 0.35f);
        }

        IEnumerator LiftBucket(float height)
        {
            if (hidingBucket == null || !hidingBucket.gameObject.activeSelf) yield break;

            Sfx.Play("pirates.chest_rise", bucketGround);
            bucketLifted = true;
            yield return TreasureMotion.Fly(hidingBucket, bucketGround + Vector3.up * height, 0.5f, 0f);
        }

        /// <summary>It floats up and shrinks away, and is switched off.</summary>
        IEnumerator RemoveBucket()
        {
            if (hidingBucket == null) yield break;

            Transform bucket = hidingBucket;
            Vector3 rest = BucketRestScale();
            Vector3 start = bucket.position;
            const float seconds = 0.5f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                bucket.position = start + Vector3.up * (k * 1.2f);
                bucket.localScale = rest * (1f - k);
                yield return null;
            }

            BattleEffects.Sparkle(bucket.position);
            HideBucketNow();
        }

        void HideBucketNow()
        {
            bucketLifted = false;
            if (hidingBucket == null) return;

            hidingBucket.localScale = BucketRestScale();
            hidingBucket.gameObject.SetActive(false);
        }

        // ----- Extra 3: count on from the coins already in the chest ---------------------------------

        IEnumerator RunCountOnTreasure()
        {
            // K coins are "already in the chest": a gold digit says so. M new coins spill, and he counts on from K.
            int max = NumberCurriculum.MaxNumber();
            int highK = Mathf.Max(2, Mathf.Min(17, max - 3));
            int k = Random.Range(2, highK + 1);
            int m = Random.Range(1, 4);
            m = Mathf.Min(m, NumberCurriculum.HighestNumber - k);

            ShowDigit(k);
            if (parrot != null) parrot.HoverAbove(chest.transform, ParrotHeightWithDigit);
            string group = "pirates.count_on." + k;
            VoicePlayer.Say(group);
            yield return WaitForVoice(7f);

            TreasureCoin[] coins = chest.SpillCoins(m);
            if (coins.Length == 0)
            {
                RemoveDigit();
                yield break;
            }

            // Each coin flies in while Lorre says K + 1, K + 2, ...
            coinNumberOffset = k;
            yield return TapCoinsIntoChest(coins, group, false);
            coinNumberOffset = 0;

            yield return AskHowMany(k + m, () => CountOnAlong(k, m));

            RemoveDigit();
            if (parrot != null) parrot.HoverAbove(chest.transform, ParrotHeightAtChest);
            FinishCounted(m);
        }

        /// <summary>The hint: the digit says K, then the new coins fly in one at a time while we count on.</summary>
        IEnumerator CountOnAlong(int k, int m)
        {
            if (digit != null) digit.Pulse(true);
            VoicePlayer.Say("number." + k, true);
            yield return new WaitForSeconds(1.3f);
            if (digit != null) digit.Pulse(false);

            TreasureCoin[] coins = chest.SpillCoins(m);
            yield return new WaitForSeconds(1.3f);
            for (int i = 1; i <= coins.Length; i++)
            {
                if (coins[i - 1] != null) coins[i - 1].FlyInto(chest.IntoChestPoint, null);
                Sfx.Play("pirates.coin", chest.IntoChestPoint);
                VoicePlayer.Say("number." + (k + i), true);
                yield return new WaitForSeconds(1.0f);
            }
        }

        void ShowDigit(int value)
        {
            RemoveDigit();

            Vector3 position = chest.transform.position + Vector3.up * 2f;
            if (TreasureMotion.TryGetBounds(chest.gameObject, out Bounds bounds))
                position = new Vector3(bounds.center.x, bounds.max.y + DigitLiftAboveChest, bounds.center.z);

            digit = PirateDigitLabel.Create(value, position, digitFont);
        }

        void RemoveDigit()
        {
            if (digit == null) return;

            Destroy(digit.gameObject);
            digit = null;
        }
    }

    /// <summary>
    /// A big gold number that floats in the air and always faces the camera (the count-on game's "already in the chest").
    /// It bobs a little and can pulse when it is the one to look at.
    /// </summary>
    internal sealed class PirateDigitLabel : MonoBehaviour
    {
        const float LabelScale = 0.007f;
        const float BobMetres = 0.06f;
        const float PulseAmount = 0.2f;
        static readonly Color Gold = new Color(1f, 0.82f, 0.22f, 1f);
        static readonly Color OutlineColor = new Color(0.4f, 0.22f, 0.05f, 1f);

        Vector3 anchor;
        bool pulsing;
        Vector3 baseScale = Vector3.one * LabelScale;

        public static PirateDigitLabel Create(int value, Vector3 position, Font font)
        {
            var root = new GameObject("CountOnDigit", typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(240f, 180f);
            rect.localScale = Vector3.one * LabelScale;

            var textObject = new GameObject("Digit", typeof(RectTransform), typeof(Text), typeof(Outline));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 140;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Gold;
            text.raycastTarget = false;
            text.text = value.ToString();

            var outline = textObject.GetComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(6f, -6f);

            var label = root.AddComponent<PirateDigitLabel>();
            label.anchor = position;
            root.transform.position = position;
            label.Face();
            return label;
        }

        public void Pulse(bool on)
        {
            pulsing = on;
            if (!on) transform.localScale = baseScale;
        }

        void LateUpdate()
        {
            transform.position = anchor + Vector3.up * (Mathf.Sin(Time.time * 2f) * BobMetres);
            if (pulsing)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                transform.localScale = baseScale * (1f + beat * PulseAmount);
            }
            Face();
        }

        void Face()
        {
            Camera main = Camera.main;
            if (main != null) transform.rotation = main.transform.rotation;
        }
    }
}
