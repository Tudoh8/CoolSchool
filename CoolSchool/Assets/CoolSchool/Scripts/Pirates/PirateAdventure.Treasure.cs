using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Pirates
{
    /// <summary>The second half of the adventure: the picture map, digging, the chest's letter lock, counting the gold and the party.</summary>
    public partial class PirateAdventure
    {
        const float ArriveHintSeconds = 25f;
        const float DigHintSeconds = 14f;
        const float CoinHintSeconds = 12f;
        const float CoinAutoSeconds = 60f;
        // After this long on one stretch of the map the stretch counts as done, so he can never be stuck.
        const float GoToTimeoutSeconds = 90f;
        // The guide arrow above a landmark: its height, the gap above the landmark, the tallest landmark top it follows, the ring on the ground.
        const float MarkerArrowHeight = 1.7f;
        const float MarkerGap = 0.5f;
        const float MarkerMaxTopHeight = 2.5f;
        const float MarkerMinRing = 1.3f;
        const float MarkerMaxRing = 2.6f;

        bool dugFlag;
        int coinsCollected;
        int pickedCard = -1;
        char pickedLetter;
        int walkersBusy;

        // ----- 6. The picture map ------------------------------------------------------------------

        IEnumerator RunMapRoute()
        {
            // Everybody who is not busy follows the captain now.
            EnableCrewAi(true);
            FriendsFollowPlayer();

            List<int> order = ChooseLandmarks(ByLevel(LandmarksByLevel, level));
            if (map != null && order.Count > 0)
            {
                var icons = new Sprite[order.Count];
                for (int i = 0; i < icons.Length; i++)
                    icons[i] = landmarkIcons != null && order[i] < landmarkIcons.Length ? landmarkIcons[order[i]] : null;

                map.Show(icons);
                VoicePlayer.Say("pirates.map");
                yield return new WaitForSeconds(1.2f);
            }

            // Follow the pictures: a bouncing arrow marks the landmark, Lorre hovers over it and a sparkle trail leads the way.
            for (int i = 0; i < order.Count; i++)
            {
                Transform landmark = landmarks[order[i]];
                yield return GoTo(landmark, () => DistanceToLandmarkEdge(landmark) <= landmarkEdgeReachMeters, landmarkEdgeReachMeters, "pirates.map");

                if (map != null) map.SetReached(i + 1);
                Sfx.Play("pirates.map_tick", landmark.position);
                VoicePlayer.Say("pirates.landmark");
                BattleEffects.Sparkle(landmark.position + Vector3.up * 1.2f);
                BattleEffects.Confetti(landmark.position + Vector3.up * 1.5f);
                if (parrot != null) parrot.Squawk();
                yield return new WaitForSeconds(0.8f);
            }

            // The X.
            if (digSpot == null) yield break;

            digSpot.Show(true);
            yield return GoTo(digSpot.transform, () => digSpot.PlayerNear, 1f, "pirates.dig");
            VoicePlayer.Say("pirates.dig");
        }

        /// <summary>Picks that many landmarks (at random) from the placed ones. They are indices into landmarks.</summary>
        List<int> ChooseLandmarks(int wanted)
        {
            var all = new List<int>();
            if (landmarks != null)
                for (int i = 0; i < landmarks.Length; i++)
                    if (landmarks[i] != null) all.Add(i);

            Shuffle(all);
            if (all.Count > wanted) all.RemoveRange(wanted, all.Count - wanted);
            return all;
        }

        /// <summary>
        /// The arrow and ring mark the target, Lorre hovers over the arrow and sparkles lead the way, until the player gets there.
        /// After a long time it counts as reached anyway, so he is never stuck.
        /// </summary>
        IEnumerator GoTo(Transform target, System.Func<bool> arrived, float stopRadius, string hintGroup)
        {
            // The arrow floats above the top of the target (a tall flag no higher than a person), the parrot just above the arrow.
            Bounds size = LandmarkBounds(target);
            float topHeight = Mathf.Clamp(size.max.y - target.position.y, 0f, MarkerMaxTopHeight);
            float ringRadius = Mathf.Clamp(Mathf.Max(size.extents.x, size.extents.z) + 0.7f, MarkerMinRing, MarkerMaxRing);
            float arrowBase = topHeight + MarkerGap;
            float parrotHeight = Mathf.Max(2.6f, arrowBase + MarkerArrowHeight + 0.9f);
            if (guideMarker != null) guideMarker.Show(target.position, arrowBase, ringRadius);
            if (parrot != null) parrot.FlyTo(target.position + Vector3.up * parrotHeight, () => parrot.HoverAbove(target, parrotHeight));

            float nextTrail = 0f;
            float nextHint = Time.time + ArriveHintSeconds;
            float giveUp = Time.time + GoToTimeoutSeconds;
            while (!arrived() && Time.time < giveUp)
            {
                if (Time.time >= nextTrail)
                {
                    nextTrail = Time.time + TrailRefreshSeconds;
                    ShowTrailTo(target.position, stopRadius);
                }

                if (Time.time >= nextHint)
                {
                    nextHint = Time.time + ArriveHintSeconds;
                    VoicePlayer.Say(hintGroup);
                }
                yield return null;
            }
        }

        /// <summary>
        /// The size of a landmark: its pictures' bounds (or its colliders', or just its point). Big landmarks have colliders he
        /// cannot walk into, so distances are measured to this box and not to the landmark's middle.
        /// </summary>
        Bounds LandmarkBounds(Transform landmark)
        {
            if (landmarkBounds.TryGetValue(landmark, out Bounds cached)) return cached;

            var bounds = new Bounds(landmark.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in landmark.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
            if (!found)
            {
                foreach (Collider collider in landmark.GetComponentsInChildren<Collider>(false))
                {
                    if (found) bounds.Encapsulate(collider.bounds);
                    else bounds = collider.bounds;
                    found = true;
                }
            }

            landmarkBounds[landmark] = bounds;
            return bounds;
        }

        /// <summary>How far the player is from the nearest side of the landmark on the ground (0 when he stands against it or in it).</summary>
        float DistanceToLandmarkEdge(Transform landmark)
        {
            Bounds bounds = LandmarkBounds(landmark);
            Vector3 position = PlayerPosition();
            position.y = bounds.center.y;
            return Mathf.Sqrt(bounds.SqrDistance(position));
        }

        // ----- 7. Digging --------------------------------------------------------------------------

        IEnumerator RunDig()
        {
            if (digSpot == null || chest == null) yield break;

            dugFlag = false;
            digSpot.Dug += OnDug;
            digSpot.Armed = true;
            ShowIcon(spadeIcon);
            lastActionTime = Time.time;

            float nextHint = Time.time + DigHintSeconds;
            while (!dugFlag)
            {
                if (Time.time >= nextHint && Time.time - lastActionTime >= DigHintSeconds - 2f)
                {
                    nextHint = Time.time + DigHintSeconds;
                    if (!digSpot.PlayerNear) ShowTrailTo(digSpot.transform.position, 1f);
                    VoicePlayer.Say("pirates.dig");
                }
                yield return null;
            }

            digSpot.Dug -= OnDug;
            if (guideMarker != null) guideMarker.Hide();
            digSpot.Armed = false;
            ClearIcon();
            if (map != null) map.Hide();

            // The chest comes up out of the sand.
            bool risen = false;
            chest.Rise(() => risen = true);
            float giveUp = Time.time + 4f;
            while (!risen && Time.time < giveUp) yield return null;

            if (parrot != null) parrot.Squawk();
            CheerEveryone();
            yield return new WaitForSeconds(0.8f);
        }

        void OnDug()
        {
            dugFlag = true;
        }

        // ----- 8. The letter lock ------------------------------------------------------------------

        IEnumerator RunLetterLock()
        {
            if (chest == null) yield break;

            // He steps to the side of the chest and the helpers gather on the other side, so the gold will be in plain view.
            yield return StepAside();

            LetterEntry target = ChooseLockLetter(out bool teaching);
            if (target == null || lessonUI == null)
            {
                // Nothing to ask (no letters set up): the lock simply opens.
                bool opened = false;
                chest.Unlock(() => opened = true);
                float wait = Time.time + 4f;
                while (!opened && Time.time < wait) yield return null;
                VoicePlayer.Say("pirates.lock_right");
                yield return new WaitForSeconds(1f);
                yield break;
            }

            lastLockLetter = target.Char;
            string soundKey = SoundKeyOf(target);
            LetterEntry[] choices = BuildLockChoices(target, ByLevel(LetterCardsByLevel, level));
            char answer = target.Char;

            player.InputEnabled = false;
            SetHudVisible(false);

            VoicePlayer.Say(teaching ? "pirates.lock_new" : "pirates.lock");
            VoicePlayer.Say(soundKey);
            pickedLetter = '\0';
            lessonUI.ShowLetterCards(choices, c => pickedLetter = c);

            bool solved = false;
            float quietSince = Time.time;
            bool reminded = false;
            while (!solved)
            {
                if (pickedLetter != '\0')
                {
                    char picked = pickedLetter;
                    pickedLetter = '\0';
                    quietSince = Time.time;
                    reminded = false;
                    lessonUI.StopPulse();

                    if (picked == answer)
                    {
                        solved = true;
                    }
                    else
                    {
                        // Never a failure: a friendly wiggle and the sound again. The right card never pulses before he has been quiet a while.
                        mistakes++;
                        lessonUI.WiggleCard(picked);
                        lessonUI.DimCard(picked);
                        VoicePlayer.Say("pirates.lock_wrong", true);
                        VoicePlayer.Say(soundKey);
                    }
                }
                else if (!reminded && Time.time - quietSince > hintPulseSeconds && (VoicePlayer.Instance == null || !VoicePlayer.Instance.IsBusy))
                {
                    // Nothing tapped for a while: the sound once more, and only now the right card pulses.
                    reminded = true;
                    VoicePlayer.Say(soundKey);
                    lessonUI.PulseCard(answer);
                }
                yield return null;
            }

            lessonUI.StopPulse();
            lessonUI.CelebrateCard(answer);
            bool unlocked = false;
            chest.Unlock(() => unlocked = true);
            VoicePlayer.Say("pirates.lock_right", true);

            float giveUp = Time.time + 4f;
            while (!unlocked && Time.time < giveUp) yield return null;
            yield return new WaitForSeconds(1.3f);
            lessonUI.HideCards();
        }

        /// <summary>
        /// A letter he knows: his name's first letter half of the time (when he has had it), otherwise any of the letters he has
        /// had. Before the first letter lesson it is the first letter of his name (or the first of the school order), and the
        /// lock teaches it: teaching is true then.
        /// </summary>
        LetterEntry ChooseLockLetter(out bool teaching)
        {
            teaching = false;
            if (letters == null) return null;

            PlayerProgress progress = ProgressStore.Current;
            var known = new List<char>();
            if (progress.lettersIntroduced != null)
            {
                foreach (string text in progress.lettersIntroduced)
                {
                    if (string.IsNullOrEmpty(text) || known.Contains(text[0])) continue;

                    LetterEntry entry = letters.Find(text[0]);
                    if (entry != null && entry.HasStrokes) known.Add(text[0]);
                }
            }

            if (known.Count > 0)
            {
                string name = ProgressStore.NormalizeChildName(progress.childName);
                char pick;
                if (name.Length > 0 && known.Contains(name[0]) && Random.value < 0.5f)
                {
                    pick = name[0];
                }
                else
                {
                    pick = known[Random.Range(0, known.Count)];
                    // Not the same letter as last round when there is a choice.
                    if (known.Count > 1 && pick == lastLockLetter) pick = known[(known.IndexOf(pick) + 1) % known.Count];
                }
                return letters.Find(pick);
            }

            // Nothing introduced yet: the lock teaches the first letter of his name.
            teaching = true;
            foreach (char c in LetterCurriculum.OrderFor(progress.childName))
            {
                LetterEntry entry = letters.Find(c);
                if (entry != null && entry.HasStrokes) return entry;
            }
            teaching = false;
            return null;
        }

        static string SoundKeyOf(LetterEntry entry)
        {
            return string.IsNullOrEmpty(entry.soundKey) ? "letter.sound." + entry.letter : entry.soundKey;
        }

        /// <summary>The right letter and other letters from the school order, shuffled. Look-alikes only on the highest level.</summary>
        LetterEntry[] BuildLockChoices(LetterEntry target, int cardCount)
        {
            var pool = new List<LetterEntry>();
            foreach (char c in LetterCurriculum.SchoolOrder)
            {
                if (c == target.Char) continue;
                // c, k and q all say "kuh": never the wrong answer to each other.
                if (LetterCurriculum.SoundGroup(c) == LetterCurriculum.SoundGroup(target.Char)) continue;
                if (level < MaxLevel && LetterCurriculum.AreSimilar(target.Char, c)) continue;

                LetterEntry entry = letters.Find(c);
                if (entry != null && entry.HasStrokes) pool.Add(entry);
            }
            Shuffle(pool);

            var choices = new List<LetterEntry> { target };
            for (int i = 0; i < pool.Count && choices.Count < Mathf.Clamp(cardCount, 2, 3); i++)
                choices.Add(pool[i]);

            Shuffle(choices);
            return choices.ToArray();
        }

        /// <summary>The player walks to the side of the chest and faces it; the others stand on the other side.</summary>
        IEnumerator StepAside()
        {
            if (chest == null || player == null) yield break;

            Vector3 chestPosition = chest.transform.position;
            EnableCrewAi(false);
            walkersBusy = 0;

            Vector3 playerSpot = chestPosition + new Vector3(-2.4f, 0f, 0.4f);
            StartCoroutine(WalkPlayerTo(playerSpot, chestPosition - playerSpot));

            // Crew and friendly skeletons: one on each side, further out the more of them there are.
            int helper = 0;
            if (crew != null)
            {
                for (int i = 0; i < crew.Length; i++)
                {
                    if (crew[i] == null || !crew[i].gameObject.activeInHierarchy) continue;

                    Vector3 spot = SideSpot(chestPosition, helper++);
                    StartCoroutine(WalkCrewTo(i, spot, chestPosition - spot));
                }
            }
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
            {
                if (skeleton == null || !skeleton.gameObject.activeInHierarchy) continue;

                Vector3 spot = SideSpot(chestPosition, helper++);
                skeleton.Follow(null);
                walkersBusy++;
                skeleton.WalkTo(spot, chestPosition - spot, () => walkersBusy--);
            }

            if (parrot != null) parrot.HoverAbove(chest.transform, 2.4f);
            yield return WaitForWalkers(8f);
        }

        /// <summary>
        /// Spots beside the chest: first the right side, then the left (the player stands at the first spot on the left). Three
        /// columns on each side at most; with more helpers than that, the next ones stand in a second row behind the chest.
        /// </summary>
        static Vector3 SideSpot(Vector3 chestPosition, int index)
        {
            float side = index % 2 == 0 ? 1f : -1f;
            int pair = index / 2;
            int column = pair % 3 + (side < 0f ? 1 : 0);
            int row = pair / 3;
            return chestPosition + new Vector3(side * (2.4f + 1.1f * column), 0f, 0.4f + (column % 2) * 0.7f + row * 2f);
        }

        IEnumerator WaitForWalkers(float maxSeconds)
        {
            float giveUp = Time.time + maxSeconds;
            while (walkersBusy > 0 && Time.time < giveUp) yield return null;
            walkersBusy = 0;
        }

        /// <summary>Walks the player to a point by hand (his controls are off), then turns him to face a direction.</summary>
        IEnumerator WalkPlayerTo(Vector3 point, Vector3 face)
        {
            if (player == null) yield break;

            walkersBusy++;
            var controller = player.GetComponent<CharacterController>();
            player.InputEnabled = false;

            float giveUp = Time.time + 8f;
            while (controller != null && controller.enabled && Time.time < giveUp)
            {
                Vector3 toPoint = point - player.transform.position;
                toPoint.y = 0f;
                float distance = toPoint.magnitude;
                if (distance < 0.25f) break;

                controller.Move(toPoint / distance * Mathf.Min(PlayerWalkSpeed * Time.deltaTime, distance));
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(toPoint), 720f * Time.deltaTime);
                yield return null;
            }

            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion wanted = Quaternion.LookRotation(face.normalized, Vector3.up);
                for (float elapsed = 0f; elapsed < 0.4f; elapsed += Time.deltaTime)
                {
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, wanted, 720f * Time.deltaTime);
                    yield return null;
                }
            }
            walkersBusy--;
        }

        /// <summary>Sends a crew member (by the navigation) to a point and turns them to face a direction.</summary>
        IEnumerator WalkCrewTo(int index, Vector3 point, Vector3 face)
        {
            KnightAllyAI member = crew[index];
            if (member == null) yield break;

            walkersBusy++;
            var agent = member.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.SetDestination(point);

                float giveUp = Time.time + 8f;
                yield return null;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > 0.4f))
                    yield return null;
                agent.isStopped = true;
            }

            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion wanted = Quaternion.LookRotation(face.normalized, Vector3.up);
                for (float elapsed = 0f; elapsed < 0.5f; elapsed += Time.deltaTime)
                {
                    member.transform.rotation = Quaternion.RotateTowards(member.transform.rotation, wanted, 540f * Time.deltaTime);
                    yield return null;
                }
            }
            walkersBusy--;
        }

        // ----- 9. Counting the gold ----------------------------------------------------------------

        IEnumerator RunCountTreasure()
        {
            if (chest == null) yield break;

            int wanted = Random.Range(ByLevel(CoinsMinByLevel, level), ByLevel(CoinsMaxByLevel, level) + 1);
            TreasureCoin[] coins = chest.SpillCoins(wanted);
            int count = coins.Length;
            if (count == 0) yield break;

            if (coinPicker != null) coinPicker.SetCoins(coins);
            coinsCollected = 0;

            VoicePlayer.Say("pirates.count");
            yield return new WaitForSeconds(1.2f);

            // He taps every coin; each one flies into the chest while Lorre counts.
            if (coinPicker != null)
            {
                coinPicker.Picked += OnCoinPicked;
                coinPicker.Armed = true;
            }
            lastActionTime = Time.time;
            float lastPick = Time.time;
            int seen = 0;
            float nextHint = Time.time + CoinHintSeconds;
            TreasureCoin pulsing = null;
            while (coinsCollected < count)
            {
                if (coinsCollected != seen)
                {
                    seen = coinsCollected;
                    lastPick = Time.time;
                    nextHint = Time.time + CoinHintSeconds;
                    if (pulsing != null) pulsing.Pulse(false);
                    pulsing = null;
                }

                if (Time.time >= nextHint)
                {
                    // Stuck: say it again and let the next coin pulse.
                    nextHint = Time.time + CoinHintSeconds;
                    VoicePlayer.Say("pirates.count");
                    pulsing = FirstUncollected(coins);
                    if (pulsing != null) pulsing.Pulse(true);
                }

                // Safety net: after a long time the coins fly into the chest by themselves, one by one, still counted out loud.
                if (Time.time - lastPick > CoinAutoSeconds)
                {
                    lastPick = Time.time - CoinAutoSeconds + 1.2f;
                    TreasureCoin next = FirstUncollected(coins);
                    if (next != null) OnCoinPicked(next);
                }
                yield return null;
            }

            if (coinPicker != null)
            {
                coinPicker.Armed = false;
                coinPicker.Picked -= OnCoinPicked;
            }
            yield return new WaitForSeconds(0.9f);
            yield return WaitForVoice(4f);

            yield return AskHowMany(count);

            // The treasure is counted: that many stars, and the trophy chest by the dock fills up.
            treasureCounted = true;
            ProgressStore.Current.pirateCoinsTotal += count;
            ProgressStore.Save();
            TreasureCounted?.Invoke(count, chest.transform.position);
        }

        void OnCoinPicked(TreasureCoin coin)
        {
            if (coin == null || coin.Collected) return;

            coinsCollected++;
            coin.Pulse(false);
            coin.FlyInto(chest.IntoChestPoint, null);
            Sfx.Play("pirates.coin", coin.transform.position);
            VoicePlayer.Say("number." + coinsCollected, true);
            if (parrot != null && coinsCollected % 3 == 0) parrot.Squawk();
        }

        static TreasureCoin FirstUncollected(TreasureCoin[] coins)
        {
            foreach (TreasureCoin coin in coins)
                if (coin != null && !coin.Collected) return coin;
            return null;
        }

        /// <summary>"How many coins did we find?" The number cards: a wrong one wiggles and we count along again.</summary>
        IEnumerator AskHowMany(int count)
        {
            if (lessonUI == null) yield break;

            VoicePlayer.Say("pirates.how_many");
            int[] cards = MakeNumberCards(count, ByLevel(NumberCardsByLevel, level));
            pickedCard = -1;
            lessonUI.ShowNumberCards(cards, n => pickedCard = n);

            int wrong = 0;
            int wrongLeft = cards.Length - 1;
            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;
                    if (picked == count) break;

                    // Never a failure: a wiggle, then the coins pop out and back in while we count along together.
                    mistakes++;
                    wrong++;
                    wrongLeft--;
                    lessonUI.WiggleCard(picked);
                    lessonUI.DimCard(picked);
                    VoicePlayer.Say("lesson.try_again", true);
                    yield return new WaitForSeconds(1.8f);
                    yield return CountAlong(count);
                    if (wrong >= 2 || wrongLeft <= 0) lessonUI.PulseCard(count);
                }
                yield return null;
            }

            lessonUI.StopPulse();
            lessonUI.CelebrateCard(count);
            VoicePlayer.Say("lesson.correct", true);
            if (parrot != null) parrot.Squawk();
            CheerEveryone();
            yield return new WaitForSeconds(2.2f);
            lessonUI.HideCards();
        }

        /// <summary>The gold comes out of the chest again and goes back in one coin at a time, each with its number.</summary>
        IEnumerator CountAlong(int count)
        {
            TreasureCoin[] coins = chest.SpillCoins(count);
            yield return new WaitForSeconds(1.3f);

            for (int k = 1; k <= coins.Length; k++)
            {
                if (coins[k - 1] != null) coins[k - 1].FlyInto(chest.IntoChestPoint, null);
                Sfx.Play("pirates.coin", chest.IntoChestPoint);
                VoicePlayer.Say("number." + k, true);
                yield return new WaitForSeconds(1.0f);
            }
        }

        /// <summary>The right answer and other numbers from 1 to 10, shuffled.</summary>
        static int[] MakeNumberCards(int correct, int cardCount)
        {
            var cards = new List<int> { correct };
            int[] offsets = { -1, 1, 2, -2, 3, -3, 4, -4 };
            foreach (int offset in offsets)
            {
                if (cards.Count >= Mathf.Clamp(cardCount, 2, 3)) break;

                int candidate = correct + offset;
                if (candidate < 1 || candidate > 10 || cards.Contains(candidate)) continue;
                cards.Add(candidate);
            }

            Shuffle(cards);
            return cards.ToArray();
        }

        // ----- 10. The party -----------------------------------------------------------------------

        IEnumerator RunParty()
        {
            // Everybody to the party spots on the beach.
            EnableCrewAi(false);
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null) skeleton.Follow(null);
            if (player != null) player.InputEnabled = false;

            Vector3 faceCamera = Vector3.back;
            walkersBusy = 0;
            int slot = 0;
            if (player != null) StartCoroutine(WalkPlayerTo(PartySpot(slot++), faceCamera));
            if (crew != null)
            {
                for (int i = 0; i < crew.Length; i++)
                    if (crew[i] != null && crew[i].gameObject.activeInHierarchy) StartCoroutine(WalkCrewTo(i, PartySpot(slot++), faceCamera));
            }
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
            {
                if (skeleton == null || !skeleton.gameObject.activeInHierarchy) continue;

                walkersBusy++;
                skeleton.WalkTo(PartySpot(slot++), faceCamera, () => walkersBusy--);
            }
            yield return WaitForWalkers(9f);

            // Dance, with Lorre circling above.
            DanceEveryone();
            if (parrot != null) parrot.Circle(PartyCentre(), 2.5f, 3.4f);
            VoicePlayer.Say("pirates.victory");
            Sfx.Play("pirates.party", PartyCentre());

            for (float elapsed = 0f; elapsed < partySeconds; elapsed += 0.6f)
            {
                PartyConfetti();
                yield return new WaitForSeconds(0.6f);
            }
            if (trophy != null) trophy.Celebrate();
            onVictory?.Invoke();
        }

        void DanceEveryone()
        {
            var kid = player != null ? player.GetComponent<KidAnimator>() : null;
            if (kid != null)
            {
                player.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                kid.SetDancing(true);
                playerDancing = true;
            }

            if (crew != null)
            {
                foreach (KnightAllyAI member in crew)
                {
                    if (member == null || !member.gameObject.activeInHierarchy) continue;

                    var memberKid = member.GetComponent<KidAnimator>();
                    if (memberKid != null) memberKid.SetDancing(true);
                }
            }
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null && skeleton.gameObject.activeInHierarchy) skeleton.Dance(true);
        }

        void PartyConfetti()
        {
            if (player != null) BattleEffects.Confetti(player.transform.position + Vector3.up * 2f);
            if (crew != null)
                foreach (KnightAllyAI member in crew)
                    if (member != null && member.gameObject.activeInHierarchy) BattleEffects.Confetti(member.transform.position + Vector3.up * 2f);
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null && skeleton.gameObject.activeInHierarchy) BattleEffects.Confetti(skeleton.transform.position + Vector3.up * 2f);
        }

        Vector3 PartySpot(int index)
        {
            if (partyPoints != null && partyPoints.Length > 0)
            {
                // More dancers than spots: the extra ones stand in a second row behind, between two spots of the first row.
                int count = partyPoints.Length;
                if (index >= count && count > 1)
                {
                    int extra = index - count;
                    Transform a = partyPoints[extra % (count - 1)];
                    Transform b = partyPoints[extra % (count - 1) + 1];
                    if (a != null && b != null)
                        return (a.position + b.position) * 0.5f + Vector3.forward * (1.8f * (1 + extra / (count - 1)));
                }

                Transform point = partyPoints[index % count];
                if (point != null) return point.position;
            }
            return PlayerPosition() + new Vector3((index - 2) * 1.4f, 0f, -1f);
        }

        Vector3 PartyCentre()
        {
            if (partyPoints == null || partyPoints.Length == 0) return PlayerPosition();

            Vector3 sum = Vector3.zero;
            int found = 0;
            foreach (Transform point in partyPoints)
            {
                if (point == null) continue;

                sum += point.position;
                found++;
            }
            return found > 0 ? sum / found : PlayerPosition();
        }
    }
}
