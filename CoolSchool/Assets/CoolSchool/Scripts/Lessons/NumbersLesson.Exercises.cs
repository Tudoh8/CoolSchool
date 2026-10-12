using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The first four kinds of exercise of the numbers lesson (tap and count, how many, find the number, give me N)
    /// and the pieces the others reuse: the number cards, the basket and the toys on the table. None of them can be failed:
    /// a wrong answer gets a friendly hint, and a child who does nothing is helped step by step.
    /// </summary>
    public partial class NumbersLesson
    {
        const float ToyAppearGapSeconds = 0.12f;
        const float SpeechEndPauseSeconds = 1f;
        const float CelebrationPauseSeconds = 0.8f;
        // Toys (about 0.35 m wide) stand in one row while their centres can be this far apart, otherwise in two staggered rows.
        const float MinToySpacing = 0.36f;
        const int MaxGiveMeToys = 12;
        const int MaxGiveMeNumber = 10;
        // Near-miss number cards for a number above ten: a teen is compared with teens.
        const int NearChoiceReach = 3;
        const float BasketToyScale = 0.7f;
        const float ToyFlightSeconds = 0.5f;
        const float ToyFlightArc = 0.5f;
        const float BasketSlotSpacing = 0.14f;
        const float BasketLayerHeight = 0.12f;

        readonly List<CountableObject> basketToys = new List<CountableObject>();
        Bounds basketBounds;
        int countedAlong;

        // ----- Tap and count ---------------------------------------------------------------------

        /// <summary>Toys appear in a row; every tap counts one out loud. The last one ends with the total on the blackboard.</summary>
        IEnumerator RunTapAndCount(int number, Countable toy)
        {
            SpawnToys(toy, number, 0f);
            yield return new WaitForSeconds(SpawnWaitSeconds(number));

            int counted = 0;

            void CountOne(CountableObject tapped)
            {
                if (tapped == null || tapped.Counted || tapped.IsMoving) return;

                counted++;
                tapped.Pop(counted);
                Say("number." + counted, true);
            }

            System.Action prompt = () => Say(toy.countPromptGroup);
            tapHandler = CountOne;
            prompt();

            var idle = new IdleTimer();
            while (counted < number)
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(NextUncounted()), () => CountOne(NextUncounted()));
                yield return null;
            }

            tapHandler = null;
            yield return SettleAfterLastNumber();
            yield return ShowTotal(number);
        }

        /// <summary>"There are... three!" with the number written on the blackboard.</summary>
        IEnumerator ShowTotal(int number)
        {
            if (blackboard != null) blackboard.ShowNumber(number);
            Say("lesson.total");
            Say("number." + number);
            Cheer();

            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        CountableObject NextUncounted()
        {
            foreach (CountableObject toy in toys)
                if (toy != null && !toy.Counted && !toy.IsMoving) return toy;
            return null;
        }

        // ----- How many / find the number --------------------------------------------------------

        /// <summary>Toys appear; the child counts them in their head or out loud and picks a number card.</summary>
        IEnumerator RunHowMany(int number, Countable toy, int maxNumber)
        {
            SpawnToys(toy, number, 0f);
            yield return new WaitForSeconds(SpawnWaitSeconds(number));

            System.Action prompt = () => Say(toy.howManyPromptGroup);
            tapHandler = CountAlong;
            countedAlong = 0;
            prompt();

            yield return AskForNumber(number, maxNumber, prompt, CountToysAloud);
            tapHandler = null;
            yield return CelebrateAnswer(number);
        }

        /// <summary>A child who likes to count by tapping the toys can: each tap counts one out loud. It is never wrong.</summary>
        void CountAlong(CountableObject tapped)
        {
            if (tapped == null || tapped.Counted || tapped.IsMoving) return;

            countedAlong++;
            tapped.Pop(countedAlong);
            Say("number." + countedAlong, true);
        }

        /// <summary>The teacher says a number and the blackboard shows that many dots as a helper.</summary>
        IEnumerator RunFindNumber(int number, int maxNumber)
        {
            if (blackboard != null) blackboard.ShowDots(number);

            System.Action prompt = () =>
            {
                Say("lesson.find_number");
                Say("number." + number);
            };
            prompt();

            yield return AskForNumber(number, maxNumber, prompt, () => CountDotsAloud(number));
            yield return CelebrateAnswer(number);
        }

        /// <summary>
        /// Shows three cards and waits for the right one. A wrong card wiggles and gets another try; the second
        /// wrong card starts the hint (counting aloud), after which the wrong cards fade away one by one.
        /// </summary>
        IEnumerator AskForNumber(int answer, int maxNumber, System.Action prompt, System.Func<IEnumerator> hint, int[] fixedChoices = null)
        {
            LessonUI ui = UI;
            int[] choices = fixedChoices ?? BuildChoices(answer, maxNumber);
            int misses = 0;
            bool solved = false;
            bool hintWanted = false;

            void Picked(int number)
            {
                NoteActivity();
                if (solved || hintWanted) return;

                if (number == answer)
                {
                    solved = true;
                    return;
                }

                exerciseMadeMistake = true;
                misses++;
                ui.WiggleCard(number);
                if (misses == 2)
                {
                    hintWanted = true;
                    return;
                }

                Say("lesson.try_again", true);
                if (misses > 2) ui.DimCard(number);
            }

            ui.ShowNumberCards(choices, Picked);

            var idle = new IdleTimer();
            while (!solved)
            {
                if (hintWanted)
                {
                    exerciseNeededHelp = true;
                    ui.HideCards();
                    yield return hint();

                    hintWanted = false;
                    ui.ShowNumberCards(choices, Picked);
                    ui.PulseCard(answer);
                    idle = new IdleTimer();
                }
                else if (idle.Tick(IsBusy(), lastActivityTime))
                {
                    HandleIdle(idle.Cycles, prompt, () => ui.PulseCard(answer), () => Picked(answer));
                }
                yield return null;
            }

            ui.StopPulse();
            ui.CelebrateCard(answer);
        }

        /// <summary>The hint for "how many": every toy hops in turn while the teacher says its number.</summary>
        IEnumerator CountToysAloud()
        {
            Say("lesson.hint", true);
            yield return WaitForSpeech();

            for (int i = 0; i < toys.Count; i++)
            {
                if (toys[i] == null) continue;

                toys[i].Pop(i + 1);
                Say("number." + (i + 1), true);
                yield return WaitForSpeech();
            }
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The hint for "find the number": the dots on the blackboard swell in turn while counted.</summary>
        IEnumerator CountDotsAloud(int number)
        {
            Say("lesson.hint", true);
            yield return WaitForSpeech();

            for (int i = 0; i < number; i++)
            {
                if (blackboard != null) blackboard.PulseDot(i);
                Say("number." + (i + 1), true);
                yield return WaitForSpeech();
            }
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The right answer: the number in big chalk, praise, and the number once more.</summary>
        IEnumerator CelebrateAnswer(int number)
        {
            if (blackboard != null) blackboard.ShowNumber(number);
            Say("lesson.correct", true);
            Say("number." + number);
            Cheer();

            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
            UI.HideCards();
        }

        /// <summary>
        /// Three different numbers in a random order: the answer and two others from the lesson's range.
        /// For an answer above ten the others are close to it (up to three away), so a teen is compared with teens.
        /// </summary>
        static int[] BuildChoices(int answer, int maxNumber)
        {
            if (answer > 10)
                return BuildNearChoices(answer, 1, Mathf.Min(NumberCurriculum.HighestNumber, Mathf.Max(answer, maxNumber)), NearChoiceReach);

            var pool = new List<int>();
            for (int n = 1; n <= Mathf.Max(3, Mathf.Min(maxNumber, 10)); n++)
                if (n != answer) pool.Add(n);

            var choices = new List<int> { answer };
            for (int i = 0; i < 2; i++)
            {
                int pick = Random.Range(0, pool.Count);
                choices.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            Shuffle(choices);
            return choices.ToArray();
        }

        /// <summary>
        /// Three different numbers in a random order: the answer and two others no further than reach away from it,
        /// all inside low to high. A narrow range widens the reach so there are always two others.
        /// </summary>
        static int[] BuildNearChoices(int answer, int low, int high, int reach)
        {
            var pool = new List<int>();
            for (int distance = 1; distance <= reach || pool.Count < 2; distance++)
            {
                if (answer - distance >= low) pool.Add(answer - distance);
                if (answer + distance <= high) pool.Add(answer + distance);
                if (distance > high - low + 1) break;
            }

            var choices = new List<int> { answer };
            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                choices.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            Shuffle(choices);
            return choices.ToArray();
        }

        static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ----- Give me N -------------------------------------------------------------------------

        /// <summary>More toys than needed beside a basket. Tapping a toy puts it in the basket and counts it; tapping it again takes it out.</summary>
        IEnumerator RunGiveMe(int number, Countable toy)
        {
            int total = Mathf.Min(MaxGiveMeToys, number + (number >= 8 ? 2 : 3));
            PlaceBasket();
            SpawnToys(toy, total, giveMeToyZ, allowTrays: false, centerX: giveMeToysX, maxWidth: giveMeToysWidth);
            basketToys.Clear();
            yield return new WaitForSeconds(SpawnWaitSeconds(total));

            System.Action prompt = () =>
            {
                Say(toy.giveMePromptGroup);
                Say("number." + number);
            };
            tapHandler = tapped => GiveToBasket(tapped, number);
            prompt();

            var idle = new IdleTimer();
            while (basketToys.Count != number || AnyMoving())
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(NextOnTable()), () => GiveToBasket(NextOnTable(), number));
                yield return null;
            }

            tapHandler = null;
            yield return SettleAfterLastNumber();
            yield return CelebrateAnswer(number);
        }

        /// <summary>A tap on a toy: it goes into the basket (and is counted), or, if it is already in, comes out again. The basket takes at most limit.</summary>
        void GiveToBasket(CountableObject tapped, int limit)
        {
            if (tapped == null || tapped.IsMoving) return;

            if (tapped.Counted)
            {
                TakeOutOfBasket(tapped);
                return;
            }

            if (basketToys.Count >= limit) return;
            PutInBasket(tapped);
        }

        /// <summary>
        /// The toy flies into the basket and is counted. The numbers said and shown start after "alreadyIn" (for the basket
        /// that already holds some), so the third toy of a basket holding 12 is called 15. Without speak the toy only plops in.
        /// </summary>
        void PutInBasket(CountableObject toy, int alreadyIn = 0, bool speak = true)
        {
            basketToys.Add(toy);
            if (speak) Say("number." + (alreadyIn + basketToys.Count), true);

            toy.FlyTo(BasketSlot(basketToys.Count - 1, toy), ToyFlightSeconds, ToyFlightArc, BasketToyScale, () =>
            {
                if (toy == null) return;
                Sfx.Play("lesson.toy_into_basket", toy.transform.position);
                if (!speak || !basketToys.Contains(toy)) return;

                toy.Pop(alreadyIn + basketToys.IndexOf(toy) + 1);
                ClearOtherBasketNumbers(toy);
            });
        }

        /// <summary>
        /// Only the newest toy in the basket shows its number. The toys sit close together in the basket, so a number for each of
        /// them would pile up in one unreadable clump above it.
        /// </summary>
        void ClearOtherBasketNumbers(CountableObject keep)
        {
            foreach (CountableObject other in basketToys)
                if (other != null && other != keep) other.ClearNumber();
        }

        /// <summary>No wrong answer, only "not yet": the toy hops back to its place and the others are counted again.</summary>
        void TakeOutOfBasket(CountableObject toy)
        {
            basketToys.Remove(toy);
            toy.ResetCounted();
            toy.ReturnToRest(ToyFlightSeconds, null);

            // The count now is the number of the newest toy, and that one number is all that is shown.
            foreach (CountableObject inBasket in basketToys)
                if (inBasket != null) inBasket.ClearNumber();

            CountableObject newest = basketToys.Count > 0 ? basketToys[basketToys.Count - 1] : null;
            if (newest != null && newest.Counted) newest.ShowNumber(basketToys.Count);
        }

        /// <summary>The next toy that could still go into the basket. With a list: only toys from that list (not the ones in a tray).</summary>
        CountableObject NextOnTable(List<CountableObject> among = null)
        {
            foreach (CountableObject toy in toys)
            {
                if (toy == null || toy.Counted || toy.IsMoving || basketToys.Contains(toy)) continue;
                if (among == null || among.Contains(toy)) return toy;
            }
            return null;
        }

        bool AnyMoving()
        {
            foreach (CountableObject toy in toys)
                if (toy != null && toy.IsMoving) return true;
            return false;
        }

        /// <summary>A spot inside the basket for the toy with this index: three across, stacked in layers.</summary>
        Vector3 BasketSlot(int index, CountableObject toy)
        {
            Transform table = stage.TableSurface;
            float across = ((index % 3) - 1) * BasketSlotSpacing;
            float layer = (index / 3) * BasketLayerHeight;

            Vector3 slot = basketBounds.center + table.right * across;
            slot.y = basketBounds.min.y + basketBounds.size.y * 0.35f + layer + toy.PivotLift * BasketToyScale;
            return slot;
        }

        void PlaceBasket()
        {
            PlaceBasket(basketPosition);
        }

        /// <summary>Stands the basket on the table at this spot (x along the table, z toward the camera).</summary>
        void PlaceBasket(Vector2 position)
        {
            if (basket == null) return;

            Transform t = basket.transform;
            t.SetPositionAndRotation(stage.TablePoint(position.x, position.y), stage.TableFacing());
            basket.SetActive(true);

            // Pivots can't be trusted, so sit the basket on the table by its bounds.
            if (TryGetBounds(basket, out Bounds bounds))
                t.position += Vector3.up * (stage.TableSurface.position.y - bounds.min.y);

            if (TryGetBounds(basket, out basketBounds)) return;
            basketBounds = new Bounds(t.position + Vector3.up * 0.2f, Vector3.one * 0.4f);
        }

        void SetBasketActive(bool active)
        {
            if (basket != null) basket.SetActive(active);
        }

        static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer) continue;

                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            return found;
        }

        // ----- Toys on the table -----------------------------------------------------------------

        /// <summary>
        /// Puts toys on the table in a neat row, or two rows when there are many. The first toy is on the left of the screen.
        /// More than ten toys go into the ten-frame trays instead (the first tray full, then the second), unless allowTrays is off.
        /// </summary>
        void SpawnToys(Countable entry, int count, float centerZ, bool allowTrays = true, float centerX = 0f, float maxWidth = -1f)
        {
            if (allowTrays && count > TenFrameTray.Capacity && TraysReady)
            {
                ShowTrayPair(centerZ);
                for (int i = 0; i < count; i++)
                    SpawnTrayToy(entry, i / TenFrameTray.Capacity, i % TenFrameTray.Capacity, i * ToyAppearGapSeconds);
                return;
            }

            Vector2[] layout = ToyLayout(count, centerZ, centerX, maxWidth);
            for (int i = 0; i < count; i++)
                SpawnToy(entry, stage.TablePoint(layout[i].x, layout[i].y), 1f, i * ToyAppearGapSeconds);
        }

        /// <summary>One toy on the table at this point. The factor shrinks or grows it on top of the toy's own scale.</summary>
        CountableObject SpawnToy(Countable entry, Vector3 point, float scaleFactor, float appearDelay)
        {
            GameObject instance = Instantiate(entry.prefab);
            var toy = instance.GetComponent<CountableObject>();
            if (toy == null) toy = instance.AddComponent<CountableObject>();

            toy.LabelFont = numberFont;
            toy.Place(point, stage.TableFacing() * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f), entry.scale * scaleFactor);
            if (!stagingForPreview) toy.Appear(appearDelay);
            toys.Add(toy);
            return toy;
        }

        /// <summary>
        /// (x, z) on the table for each toy. Table x grows toward the left of the screen, so it is mirrored. The toys stand in one
        /// row across the screen as long as they fit side by side without touching; more than that go in two rows rowGap apart
        /// (the table is 1.4 m deep), and with two equal rows the back row is nudged sideways so it peeks through the gaps.
        /// The rows are centred on centerX and no wider than maxWidth (the whole table row by default).
        /// </summary>
        Vector2[] ToyLayout(int count, float centerZ, float centerX = 0f, float maxWidth = -1f, float rowGap = -1f)
        {
            if (maxWidth <= 0f) maxWidth = maxRowWidth;
            if (rowGap <= 0f) rowGap = rowSpacing;

            int perRow = (count - 1) * MinToySpacing <= maxWidth ? count : Mathf.CeilToInt(count * 0.5f);
            float spacing = perRow > 1 ? Mathf.Min(toySpacing, maxWidth / (perRow - 1)) : 0f;
            bool equalRows = perRow < count && count == perRow * 2;

            var layout = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int inRow = row == 0 ? Mathf.Min(count, perRow) : count - perRow;
                int column = i % perRow;

                float x = ((inRow - 1) * 0.5f - column) * spacing;
                if (equalRows) x += (row == 0 ? -0.25f : 0.25f) * spacing;
                float z = centerZ + (perRow == count ? 0f : (row == 0 ? -0.5f : 0.5f) * rowGap);
                layout[i] = new Vector2(centerX + x, z);
            }
            return layout;
        }

        static float SpawnWaitSeconds(int count)
        {
            return count * ToyAppearGapSeconds + 0.5f;
        }

        /// <summary>Lets the last number word finish, then a beat of quiet so the next line is not mistaken for a repeat.</summary>
        IEnumerator SettleAfterLastNumber()
        {
            yield return WaitForSpeech();
            yield return new WaitForSeconds(SpeechEndPauseSeconds);
        }
    }
}
