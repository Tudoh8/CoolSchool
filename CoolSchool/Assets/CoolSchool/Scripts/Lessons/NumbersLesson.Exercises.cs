using System.Collections;
using System.Collections.Generic;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The four kinds of exercise of the numbers lesson. None of them can be failed:
    /// a wrong answer gets a friendly hint, and a child who does nothing is helped step by step.
    /// </summary>
    public partial class NumbersLesson
    {
        // A child who does nothing for this long hears the prompt again.
        const float IdleSeconds = 8f;
        const int RepeatsBeforeHelp = 2;
        // After this many quiet rounds the teacher does the exercise herself, a little faster each time.
        const int CyclesBeforeAutoHelp = 6;
        const float AutoHelpIdleSeconds = 3f;

        const float ToyAppearGapSeconds = 0.12f;
        const float SpeechEndPauseSeconds = 1f;
        const float CelebrationPauseSeconds = 0.8f;
        const int SingleRowMaxToys = 5;
        const int MaxGiveMeToys = 12;
        const float BasketToyScale = 0.7f;
        const float ToyFlightSeconds = 0.5f;
        const float ToyFlightArc = 0.5f;
        const float BasketSlotSpacing = 0.14f;
        const float BasketLayerHeight = 0.12f;

        readonly List<CountableObject> basketToys = new List<CountableObject>();
        Bounds basketBounds;
        int countedAlong;

        /// <summary>Watches for a child who has gone quiet. Tick returns true once each time the quiet has lasted long enough.</summary>
        sealed class IdleTimer
        {
            float quietSince = Time.time;

            public int Cycles { get; private set; }

            public bool Tick(bool busy, float lastActivity)
            {
                if (busy) quietSince = Time.time;
                else if (lastActivity > quietSince) quietSince = lastActivity;

                float limit = Cycles >= CyclesBeforeAutoHelp ? AutoHelpIdleSeconds : IdleSeconds;
                if (Time.time - quietSince < limit) return false;

                quietSince = Time.time;
                Cycles++;
                return true;
            }
        }

        /// <summary>
        /// What to do when the child has been quiet: repeat the prompt twice, then make the right card or toy
        /// pulse, and in the end let the teacher do it. Anything that needed this does not count as a first try.
        /// </summary>
        void HandleIdle(int cycles, System.Action prompt, System.Action help, System.Action autoSolve)
        {
            if (cycles > RepeatsBeforeHelp)
            {
                exerciseNeededHelp = true;
                help();
            }

            if (cycles >= CyclesBeforeAutoHelp)
            {
                autoSolve();
                return;
            }
            prompt();
        }

        static bool IsBusy()
        {
            return IsSpeaking();
        }

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
        IEnumerator AskForNumber(int answer, int maxNumber, System.Action prompt, System.Func<IEnumerator> hint)
        {
            LessonUI ui = UI;
            int[] choices = BuildChoices(answer, maxNumber);
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

        /// <summary>Three different numbers in a random order: the answer and two others from the lesson's range.</summary>
        static int[] BuildChoices(int answer, int maxNumber)
        {
            var pool = new List<int>();
            for (int n = 1; n <= Mathf.Max(3, maxNumber); n++)
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
            SpawnToys(toy, total, giveMeToyZ);
            basketToys.Clear();
            yield return new WaitForSeconds(SpawnWaitSeconds(total));

            void Give(CountableObject tapped)
            {
                if (tapped == null || tapped.IsMoving || basketToys.Count >= number) return;

                if (tapped.Counted)
                {
                    TakeOutOfBasket(tapped);
                    return;
                }
                PutInBasket(tapped);
            }

            System.Action prompt = () =>
            {
                Say(toy.giveMePromptGroup);
                Say("number." + number);
            };
            tapHandler = Give;
            prompt();

            var idle = new IdleTimer();
            while (basketToys.Count != number || AnyMoving())
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(NextOnTable()), () => Give(NextOnTable()));
                yield return null;
            }

            tapHandler = null;
            yield return SettleAfterLastNumber();
            yield return CelebrateAnswer(number);
        }

        void PutInBasket(CountableObject toy)
        {
            basketToys.Add(toy);
            int count = basketToys.Count;
            Say("number." + count, true);

            toy.FlyTo(BasketSlot(count - 1, toy), ToyFlightSeconds, ToyFlightArc, BasketToyScale, () =>
            {
                if (toy != null && basketToys.Contains(toy)) toy.Pop(basketToys.IndexOf(toy) + 1);
            });
        }

        /// <summary>No wrong answer, only "not yet": the toy hops back to its place and the others are counted again.</summary>
        void TakeOutOfBasket(CountableObject toy)
        {
            basketToys.Remove(toy);
            toy.ResetCounted();
            toy.ReturnToRest(ToyFlightSeconds, null);

            for (int i = 0; i < basketToys.Count; i++)
                if (basketToys[i].Counted) basketToys[i].ShowNumber(i + 1);
        }

        CountableObject NextOnTable()
        {
            foreach (CountableObject toy in toys)
                if (toy != null && !toy.Counted && !toy.IsMoving && !basketToys.Contains(toy)) return toy;
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
            if (basket == null) return;

            Transform t = basket.transform;
            t.SetPositionAndRotation(stage.TablePoint(basketPosition.x, basketPosition.y), stage.TableFacing());
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

        /// <summary>Puts toys on the table in a neat row, or two rows when there are many. The first toy is on the left of the screen.</summary>
        void SpawnToys(Countable entry, int count, float centerZ)
        {
            Vector2[] layout = ToyLayout(count, centerZ);
            Quaternion facing = stage.TableFacing();

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(entry.prefab);
                var toy = instance.GetComponent<CountableObject>();
                if (toy == null) toy = instance.AddComponent<CountableObject>();

                toy.LabelFont = numberFont;
                toy.Place(stage.TablePoint(layout[i].x, layout[i].y), facing * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f), entry.scale);
                toy.Appear(i * ToyAppearGapSeconds);
                toys.Add(toy);
            }
        }

        /// <summary>(x, z) on the table for each toy. Table x grows toward the left of the screen, so it is mirrored.</summary>
        Vector2[] ToyLayout(int count, float centerZ)
        {
            int perRow = count <= SingleRowMaxToys ? count : Mathf.CeilToInt(count * 0.5f);
            float spacing = perRow > 1 ? Mathf.Min(toySpacing, maxRowWidth / (perRow - 1)) : 0f;

            var layout = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int inRow = row == 0 ? Mathf.Min(count, perRow) : count - perRow;
                int column = i % perRow;

                float x = ((inRow - 1) * 0.5f - column) * spacing;
                float z = centerZ + (perRow == count ? 0f : (row == 0 ? -0.5f : 0.5f) * rowSpacing);
                layout[i] = new Vector2(x, z);
            }
            return layout;
        }

        static float SpawnWaitSeconds(int count)
        {
            return count * ToyAppearGapSeconds + 0.5f;
        }

        static void Pulse(CountableObject toy)
        {
            if (toy != null) toy.Highlight(true);
        }

        /// <summary>Lets the last number word finish, then a beat of quiet so the next line is not mistaken for a repeat.</summary>
        IEnumerator SettleAfterLastNumber()
        {
            yield return WaitForSpeech();
            yield return new WaitForSeconds(SpeechEndPauseSeconds);
        }

        /// <summary>Confetti over the table.</summary>
        void Cheer()
        {
            BattleEffects.Confetti(stage.TablePoint(0f, 0f) + Vector3.up * 0.8f);
            stage.TeacherWave();
        }
    }
}
