using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The number and letter card questions of the castle moments (count the goblins, the password, the name flag...).
    /// Nobody fails: a wrong card wiggles and fades and we help (count along, say the sound again), the right card starts to
    /// pulse when he is quiet for a while, and after a longer while the question is solved for him. The mistakes are only
    /// counted in the MomentResult, which moves the adaptive castle level.
    /// </summary>
    public static class CardQuestion
    {
        const float PulseSeconds = 12f;
        const float SolveSeconds = 26f;
        const float CelebrateSeconds = 0.8f;
        const float AfterWrongSeconds = 1.8f;

        // ----- Numbers ---------------------------------------------------------------------------

        /// <summary>
        /// "How many?": the right answer and cardCount - 1 other numbers near it, on cards. A wrong card wiggles, fades, we count along
        /// (countAlong) and ask again with the same cards (the faded one stays faded). The right card pulses after 12 s without a tap;
        /// after 26 s it is solved for him (count along, celebrate). The cards are hidden 0.8 s after the right answer.
        /// </summary>
        public static IEnumerator AskNumber(LessonUI ui, int answer, int cardCount, Func<IEnumerator> countAlong, MomentResult result)
        {
            if (ui == null) yield break;

            int[] cards = NumberChoices(answer, cardCount);
            int picked = -1;
            ui.ShowNumberCards(cards, n => picked = n);

            int wrong = 0;
            int wrongLeft = cards.Length - 1;
            float lastActivity = Time.time;
            bool pulsing = false;
            bool solvedForHim = false;

            while (true)
            {
                if (picked >= 0)
                {
                    int tapped = picked;
                    picked = -1;
                    if (tapped == answer) break;

                    wrong++;
                    wrongLeft--;
                    if (result != null) result.Mistakes++;
                    ui.WiggleCard(tapped);
                    ui.DimCard(tapped);
                    VoicePlayer.Say("lesson.try_again", true);
                    yield return new WaitForSeconds(AfterWrongSeconds);
                    if (countAlong != null) yield return countAlong();

                    // Taps while we counted along are forgotten.
                    picked = -1;
                    lastActivity = Time.time;
                    if (wrong >= 2 || wrongLeft <= 0)
                    {
                        pulsing = true;
                        ui.PulseCard(answer);
                    }
                }
                else if (!pulsing && Time.time - lastActivity >= PulseSeconds)
                {
                    pulsing = true;
                    ui.PulseCard(answer);
                }
                else if (Time.time - lastActivity >= SolveSeconds)
                {
                    solvedForHim = true;
                    break;
                }
                yield return null;
            }

            ui.StopPulse();
            if (solvedForHim && countAlong != null) yield return countAlong();

            ui.CelebrateCard(answer);
            VoicePlayer.Say("lesson.correct", true);
            yield return new WaitForSeconds(CelebrateSeconds);
            ui.HideCards();
        }

        /// <summary>
        /// The right answer and (cardCount - 1) others one or two away from it (never below 1, never above the biggest number of
        /// the numbers level, no doubles), shuffled. Two or three cards.
        /// </summary>
        public static int[] NumberChoices(int answer, int cardCount)
        {
            int count = Mathf.Clamp(cardCount, 2, 3);
            int max = Mathf.Max(NumberCurriculum.MaxNumber(), answer);
            max = Mathf.Min(max, NumberCurriculum.HighestNumber);

            var cards = new List<int> { answer };
            AddNear(cards, answer, count, max, new[] { -2, -1, 1, 2 });
            // Not enough numbers in range (tiny levels): go a little further out.
            AddNear(cards, answer, count, max, new[] { -3, 3, -4, 4 });
            // Still short: allow numbers above the level's biggest one rather than fewer cards.
            if (cards.Count < count)
                AddNear(cards, answer, count, NumberCurriculum.HighestNumber, new[] { 1, 2, 3, 4, -1, -2 });

            Shuffle(cards);
            return cards.ToArray();
        }

        static void AddNear(List<int> cards, int answer, int count, int max, int[] offsets)
        {
            var options = new List<int>();
            foreach (int offset in offsets)
            {
                int candidate = answer + offset;
                if (candidate >= 1 && candidate <= max && !cards.Contains(candidate)) options.Add(candidate);
            }
            Shuffle(options);

            foreach (int candidate in options)
            {
                if (cards.Count >= count) return;
                cards.Add(candidate);
            }
        }

        // ----- Letters ---------------------------------------------------------------------------

        /// <summary>
        /// "Which letter has this sound?": the target letter and other letters from the school order (letters he has had first, never
        /// the same sound, never a look-alike). prompt() is said at the start and after a wrong pick. A wrong card wiggles and fades,
        /// the right one pulses after 12 s (or when only it is left) and is chosen for him after 26 s. With countMistakes false any card
        /// is right (a free choice) and onPicked gets the one he took. With keepCards true the cards stay on screen after the right
        /// pick, with no praise: the caller flies the card somewhere and hides them.
        /// </summary>
        public static IEnumerator AskLetter(LessonUI ui, LetterSet letters, char target, int cardCount, Action prompt,
            MomentResult result, bool countMistakes = true, Action<char> onPicked = null, bool keepCards = false)
        {
            if (ui == null || letters == null) yield break;

            LetterEntry[] cards = BuildLetterCards(letters, target, cardCount);
            if (cards.Length == 0) yield break;

            yield return AskLetterFrom(ui, cards, countMistakes ? target : '\0', prompt, result, onPicked, keepCards);
        }

        /// <summary>
        /// The same question with cards chosen by the caller. rightLetter '\0' means every card is right (a free choice).
        /// </summary>
        public static IEnumerator AskLetterFrom(LessonUI ui, LetterEntry[] cards, char rightLetter, Action prompt, MomentResult result,
            Action<char> onPicked = null, bool keepCards = false)
        {
            if (ui == null || cards == null || cards.Length == 0) yield break;

            bool freeChoice = rightLetter == '\0';
            char picked = '\0';
            ui.ShowLetterCards(cards, c => picked = c);
            prompt?.Invoke();

            int wrong = 0;
            int wrongLeft = cards.Length - 1;
            float lastActivity = Time.time;
            bool pulsing = false;
            char chosen = '\0';

            while (true)
            {
                if (picked != '\0')
                {
                    char tapped = picked;
                    picked = '\0';
                    if (freeChoice || tapped == rightLetter)
                    {
                        chosen = tapped;
                        break;
                    }

                    wrong++;
                    wrongLeft--;
                    if (result != null) result.Mistakes++;
                    ui.WiggleCard(tapped);
                    ui.DimCard(tapped);
                    VoicePlayer.Say("lesson.try_again", true);
                    yield return new WaitForSeconds(AfterWrongSeconds);
                    yield return CastleMotion.WaitForVoice(4f);
                    prompt?.Invoke();

                    picked = '\0';
                    lastActivity = Time.time;
                    if (wrong >= 2 || wrongLeft <= 0)
                    {
                        pulsing = true;
                        ui.PulseCard(rightLetter);
                    }
                }
                else if (!pulsing && !freeChoice && Time.time - lastActivity >= PulseSeconds)
                {
                    pulsing = true;
                    ui.PulseCard(rightLetter);
                    prompt?.Invoke();
                }
                else if (Time.time - lastActivity >= SolveSeconds)
                {
                    // Solved for him: the right card, or the first card of a free choice.
                    chosen = freeChoice ? cards[0].Char : rightLetter;
                    break;
                }
                yield return null;
            }

            ui.StopPulse();
            onPicked?.Invoke(chosen);
            if (keepCards) yield break;

            ui.CelebrateCard(chosen);
            VoicePlayer.Say("lesson.correct", true);
            yield return new WaitForSeconds(CelebrateSeconds);
            ui.HideCards();
        }

        /// <summary>The target and (cardCount - 1) other letters, shuffled. Letters he has had come first, then the school order.</summary>
        public static LetterEntry[] BuildLetterCards(LetterSet letters, char target, int cardCount)
        {
            int count = Mathf.Clamp(cardCount, 2, 3);
            LetterEntry targetEntry = letters != null ? letters.Find(target) : null;
            if (targetEntry == null) return new LetterEntry[0];

            var known = new List<char>();
            PlayerProgress progress = ProgressStore.Current;
            if (progress.lettersIntroduced != null)
                foreach (string text in progress.lettersIntroduced)
                    if (!string.IsNullOrEmpty(text) && !known.Contains(text[0])) known.Add(text[0]);

            var pool = new List<char>();
            foreach (char letter in known) pool.Add(letter);
            Shuffle(pool);

            var rest = new List<char>();
            foreach (char letter in LetterCurriculum.SchoolOrder)
                if (!known.Contains(letter)) rest.Add(letter);
            Shuffle(rest);
            pool.AddRange(rest);

            var choices = new List<LetterEntry> { targetEntry };
            foreach (char letter in pool)
            {
                if (choices.Count >= count) break;
                if (letter == target) continue;
                // c, k and q all say "kuh": never the wrong answer to each other. Look-alikes (b and d) are kept apart too.
                if (LetterCurriculum.SoundGroup(letter) == LetterCurriculum.SoundGroup(target)) continue;
                if (LetterCurriculum.AreSimilar(letter, target)) continue;

                LetterEntry entry = letters.Find(letter);
                if (entry != null && entry.HasStrokes) choices.Add(entry);
            }

            Shuffle(choices);
            return choices.ToArray();
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
