using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The five kinds of exercise of the letters lesson. None of them can be failed: a wrong card or prop wiggles and the
    /// sound is said again, the right one starts to pulse after the second mistake, and a child who does nothing is helped
    /// step by step until the teacher does it herself.
    /// </summary>
    public partial class LettersLesson
    {
        const float PropAppearGapSeconds = 0.15f;
        const float CelebrationPauseSeconds = 0.8f;
        const float CardFlightSeconds = 0.6f;
        const float TraceFirstHintSeconds = 12f;
        const float TraceNextHintSeconds = 8f;
        const int TraceGhostsBeforeTeacherDraws = 2;
        const int MaxNameLetters = 8;
        const int CardSlots = 3;

        // ----- New letter ------------------------------------------------------------------------

        /// <summary>"This is a new letter!" It is written big with its sound and a word with a prop, and then it is traced.</summary>
        IEnumerator RunNewLetter()
        {
            char letter = newLetter.Value;
            LetterEntry entry = letterSet.Find(letter);
            currentTarget = letter;
            lastTarget = letter;

            PlayerProgress progress = ProgressStore.Current;
            LetterCurriculum.Introduce(progress, letter);
            ProgressStore.Save();
            introducedThisLesson = true;

            Say("letters.new");
            yield return letterBoard.WriteLetter(entry);
            yield return WaitForSpeech();

            Say(entry.soundKey);
            yield return WaitForSpeech();

            LetterWord word = letterSet.PickWord(letter);
            if (word != null)
            {
                List<CountableObject> props = SpawnProps(new List<LetterWord> { word });
                yield return new WaitForSeconds(PropAppearGapSeconds + 0.5f);

                Say(word.voiceKey);
                if (props.Count > 0) props[0].Hop();
                yield return WaitForSpeech();
            }
            yield return new WaitForSeconds(0.4f);

            yield return RunTrace(entry, false);
        }

        // ----- Find the letter -------------------------------------------------------------------

        /// <summary>The teacher says a sound and the child taps the matching letter card.</summary>
        IEnumerator RunFindLetter(int cardCount)
        {
            LetterEntry target = PickTarget(null);
            if (target == null) yield break;
            currentTarget = target.Char;

            LetterEntry[] choices = BuildChoices(target, cardCount);
            System.Action prompt = () =>
            {
                Say("letters.find");
                Say(target.soundKey);
            };

            yield return AskForLetter(target, choices, prompt, true);
            yield return CelebrateLetter(target);
        }

        /// <summary>The right letter in big chalk, praise, and its sound once more.</summary>
        IEnumerator CelebrateLetter(LetterEntry letter)
        {
            Say("lesson.correct", true);
            Say(letter.soundKey);
            Cheer();

            yield return letterBoard.WriteLetter(letter);
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
            UI.HideCards();
        }

        /// <summary>
        /// Shows the cards and waits for the right one. A wrong card wiggles and fades, the sound is said again, and after the
        /// second mistake (or when only the right card is left) the right card pulses. With celebrate off the caller decides what
        /// happens to the right card.
        /// </summary>
        IEnumerator AskForLetter(LetterEntry answer, LetterEntry[] choices, System.Action prompt, bool celebrate)
        {
            LessonUI ui = UI;
            int misses = 0;
            int wrongLeft = choices.Length - 1;
            bool solved = false;

            void Picked(char letter)
            {
                NoteActivity();
                if (solved) return;

                if (letter == answer.Char)
                {
                    solved = true;
                    return;
                }

                exerciseMadeMistake = true;
                lessonMistakes++;
                misses++;
                wrongLeft--;
                ui.WiggleCard(letter);
                ui.DimCard(letter);

                Say("lesson.try_again", true);
                Say(answer.soundKey);
                if (misses >= 2 || wrongLeft <= 0) ui.PulseCard(answer.Char);
            }

            ui.ShowLetterCards(choices, Picked);
            prompt();

            var idle = new IdleTimer();
            while (!solved)
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => ui.PulseCard(answer.Char), () => Picked(answer.Char));
                yield return null;
            }

            ui.StopPulse();
            if (celebrate) ui.CelebrateCard(answer.Char);
        }

        // ----- What starts with... ---------------------------------------------------------------

        /// <summary>Props stand on the table; the teacher says a sound and the child taps the one whose word starts with it.</summary>
        IEnumerator RunStartsWith(int propCount)
        {
            LetterEntry target = PickTarget(entry => entry.HasWords);
            if (target == null) yield break;
            currentTarget = target.Char;

            LetterWord rightWord = letterSet.PickWord(target.Char);
            var words = new List<LetterWord> { rightWord };
            foreach (LetterWord other in PickOtherWords(target.Char, propCount - 1))
                words.Add(other);
            Shuffle(words);

            List<CountableObject> props = SpawnProps(words);
            CountableObject right = props[words.IndexOf(rightWord)];
            yield return new WaitForSeconds(props.Count * PropAppearGapSeconds + 0.5f);

            int misses = 0;
            bool solved = false;

            void Tapped(CountableObject tapped)
            {
                if (solved || tapped == null) return;
                if (tapped == right)
                {
                    solved = true;
                    return;
                }

                exerciseMadeMistake = true;
                lessonMistakes++;
                misses++;
                tapped.Wobble();
                Sfx.Play("ui.card_wrong");
                Say("lesson.try_again", true);
                Say(target.soundKey);
                if (misses >= 2) Pulse(right);
            }

            System.Action prompt = () =>
            {
                Say("letters.starts_with");
                Say(target.soundKey);
            };
            tapHandler = Tapped;
            prompt();

            var idle = new IdleTimer();
            while (!solved)
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(right), () => Tapped(right));
                yield return null;
            }
            tapHandler = null;
            right.Highlight(false);
            right.Hop();
            Sfx.Play("ui.card_right");

            // "Maan... begint met... mmm."
            Say("lesson.correct", true);
            Say(rightWord.voiceKey);
            Say("letters.begins_with");
            Say(target.soundKey);
            Cheer();

            yield return letterBoard.WriteLetter(target);
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        // ----- Tracing ---------------------------------------------------------------------------

        /// <summary>A letter he already knows, to trace again.</summary>
        IEnumerator RunReviewTrace()
        {
            LetterEntry target = PickTarget(null);
            if (target == null) yield break;
            currentTarget = target.Char;

            yield return RunTrace(target, true);
        }

        /// <summary>
        /// The dotted letter on the board; he drags a finger along each stroke in order. Very generous: progress counts when the
        /// finger is near the path, and lifting the finger keeps it. When he is quiet a dot shows the way, and in the end the
        /// teacher draws it herself, so the lesson never gets stuck.
        /// </summary>
        IEnumerator RunTrace(LetterEntry entry, bool sayTheSound)
        {
            letterBoard.BeginTrace(entry);
            if (!letterBoard.IsTracing) yield break;

            Say("letters.trace");
            if (sayTheSound) Say(entry.soundKey);

            float lastProgressAt = Time.time;
            float hintAfter = TraceFirstHintSeconds;
            int ghosts = 0;

            while (!letterBoard.TraceDone)
            {
                // The parent panel is open: the game waits.
                if (!CoolSchool.Parent.ParentGate.InputBlocked)
                {
                    Pointer pointer = Pointer.current;
                    bool pressed = pointer != null && pointer.press.isPressed;
                    Vector2 position = pressed ? pointer.position.ReadValue() : Vector2.zero;

                    LetterBoard.TraceEvent traceEvent = letterBoard.TraceStep(pressed, position);
                    if (traceEvent != LetterBoard.TraceEvent.None)
                    {
                        NoteActivity();
                        lastProgressAt = Time.time;
                        hintAfter = TraceNextHintSeconds;
                        letterBoard.StopGhost();
                    }
                    else if (!letterBoard.GhostPlaying && !IsSpeaking() && Time.time - lastProgressAt >= hintAfter)
                    {
                        lastProgressAt = Time.time;
                        hintAfter = TraceNextHintSeconds;
                        ghosts++;
                        if (ghosts > TraceGhostsBeforeTeacherDraws)
                        {
                            exerciseNeededHelp = true;
                            yield return letterBoard.AutoDrawRest();
                            break;
                        }

                        if (ghosts == TraceGhostsBeforeTeacherDraws) exerciseNeededHelp = true;
                        if (ghosts > 1) Say("letters.trace");
                        letterBoard.ShowGhost();
                    }
                }
                yield return null;
            }

            yield return letterBoard.FillIn();
            Say("letters.trace_done", true);
            Cheer();
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        // ----- Spell his name --------------------------------------------------------------------

        /// <summary>
        /// Empty slots on the board, one per letter of his name. For each slot the teacher says a sound; he taps the letter card
        /// (there are three cards at a time) and it flies into the slot. At the end all the letters bounce.
        /// </summary>
        IEnumerator RunSpellName()
        {
            List<char> name = NameLetters();
            if (name.Count == 0) yield break;

            Say("letters.name");
            letterBoard.ShowNameSlots(name.Count);
            yield return WaitForSpeech();

            for (int i = 0; i < name.Count; i++)
            {
                LetterEntry entry = letterSet.Find(name[i]);
                if (entry == null || !entry.HasStrokes) continue;

                // The other cards are the other letters of his name first, so he looks at the shapes he knows.
                LetterEntry[] choices = BuildNameChoices(entry, name);
                System.Action prompt = () => Say(entry.soundKey);
                yield return AskForLetter(entry, choices, prompt, false);

                bool landed = false;
                if (letterBoard.TryGetSlotScreenPoint(i, out Vector2 slotPoint))
                {
                    UI.FlyCardTo(entry.Char, slotPoint, CardFlightSeconds, () => landed = true);
                    float giveUp = Time.time + CardFlightSeconds + 1f;
                    while (!landed && Time.time < giveUp) yield return null;
                }
                UI.HideCards();
                letterBoard.FillSlot(i, entry);
                yield return new WaitForSeconds(0.3f);
            }

            Cheer();
            Say("letters.name_done");
            yield return letterBoard.BounceName();
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        // ----- Which letters ---------------------------------------------------------------------

        /// <summary>The letters he has had so far that can be drawn, in the order they were introduced.</summary>
        List<char> KnownLetters()
        {
            var known = new List<char>();
            foreach (string text in ProgressStore.Current.lettersIntroduced)
            {
                if (string.IsNullOrEmpty(text) || letterSet == null) continue;

                LetterEntry entry = letterSet.Find(text[0]);
                if (entry != null && entry.HasStrokes && !known.Contains(text[0])) known.Add(text[0]);
            }
            return known;
        }

        /// <summary>The letters of his name in order (doubles stay), at most eight, and only those the letter set can draw.</summary>
        List<char> NameLetters()
        {
            var letters = new List<char>();
            if (letterSet == null) return letters;

            string name = ProgressStore.NormalizeChildName(ProgressStore.Current.childName);
            foreach (char c in name)
            {
                LetterEntry entry = letterSet.Find(c);
                if (entry != null && entry.HasStrokes && letters.Count < MaxNameLetters) letters.Add(c);
            }
            return letters;
        }

        /// <summary>
        /// A known letter that passes the filter. The newest letters and those he gets wrong more often come up more often,
        /// and not the same one twice in a row when there is a choice.
        /// </summary>
        LetterEntry PickTarget(System.Func<LetterEntry, bool> filter)
        {
            var candidates = new List<LetterEntry>();
            var weights = new List<float>();
            List<char> known = KnownLetters();
            PlayerProgress progress = ProgressStore.Current;

            for (int i = 0; i < known.Count; i++)
            {
                LetterEntry entry = letterSet.Find(known[i]);
                if (entry == null || (filter != null && !filter(entry))) continue;

                float weight = 1f + Mathf.Max(0, 3 - LetterCurriculum.Score(progress, known[i]));
                if (i == known.Count - 1) weight += 2f;
                if (known[i] == lastTarget) weight *= 0.25f;

                candidates.Add(entry);
                weights.Add(weight);
            }
            if (candidates.Count == 0) return null;

            float total = 0f;
            foreach (float weight in weights) total += weight;

            float roll = Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll > 0f && i < candidates.Count - 1) continue;

                lastTarget = candidates[i].Char;
                return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        /// <summary>
        /// The target and other letters, shuffled. Known letters are used first, and until level 4 letters that look alike
        /// (b and d) are kept apart. From level 4 one look-alike is mixed in on purpose.
        /// </summary>
        LetterEntry[] BuildChoices(LetterEntry target, int count)
        {
            var choices = new List<LetterEntry> { target };
            var pool = new List<LetterEntry>();
            foreach (LetterEntry entry in letterSet.letters)
                if (entry != null && entry.HasStrokes && entry.Char != target.Char) pool.Add(entry);

            // Known letters first, then the rest of the alphabet, each in random order.
            Shuffle(pool);
            List<char> known = KnownLetters();
            pool = KnownFirst(pool, known);

            if (lessonLevel >= 4 && count >= 3)
            {
                LetterEntry twin = pool.Find(entry => LetterCurriculum.AreSimilar(entry.Char, target.Char) && known.Contains(entry.Char));
                if (twin != null) choices.Add(twin);
            }

            for (int pass = 0; pass < 2 && choices.Count < count; pass++)
            {
                foreach (LetterEntry entry in pool)
                {
                    if (choices.Count >= count) break;
                    if (choices.Contains(entry)) continue;
                    if (pass == 0 && lessonLevel < 4 && LooksLikeAny(entry, choices)) continue;
                    choices.Add(entry);
                }
            }

            Shuffle(choices);
            return choices.ToArray();
        }

        /// <summary>The same list with the letters he knows in front. Both parts keep their (shuffled) order.</summary>
        static List<LetterEntry> KnownFirst(List<LetterEntry> list, List<char> known)
        {
            var sorted = new List<LetterEntry>();
            foreach (LetterEntry entry in list)
                if (known.Contains(entry.Char)) sorted.Add(entry);
            foreach (LetterEntry entry in list)
                if (!known.Contains(entry.Char)) sorted.Add(entry);
            return sorted;
        }

        static bool LooksLikeAny(LetterEntry entry, List<LetterEntry> others)
        {
            foreach (LetterEntry other in others)
                if (LetterCurriculum.AreSimilar(entry.Char, other.Char)) return true;
            return false;
        }

        /// <summary>The cards for one slot of his name: the right letter, the other letters of the name, then any others.</summary>
        LetterEntry[] BuildNameChoices(LetterEntry right, List<char> name)
        {
            var choices = new List<LetterEntry> { right };
            var others = new List<char>();
            foreach (char c in name)
                if (c != right.Char && !others.Contains(c)) others.Add(c);
            Shuffle(others);

            foreach (char c in others)
            {
                LetterEntry entry = letterSet.Find(c);
                if (choices.Count < CardSlots && entry != null && entry.HasStrokes) choices.Add(entry);
            }

            var pool = new List<LetterEntry>();
            foreach (LetterEntry entry in letterSet.letters)
                if (entry != null && entry.HasStrokes && !choices.Contains(entry) && !LetterCurriculum.AreSimilar(entry.Char, right.Char)) pool.Add(entry);
            Shuffle(pool);
            foreach (LetterEntry entry in pool)
                if (choices.Count < CardSlots) choices.Add(entry);

            Shuffle(choices);
            return choices.ToArray();
        }

        // ----- Props on the table ----------------------------------------------------------------

        /// <summary>
        /// Can "what starts with..." be asked? It needs a known letter with a word, and enough other letters with words
        /// that start with a different sound.
        /// </summary>
        bool CanAskStartsWith(int propCount)
        {
            foreach (char known in KnownLetters())
            {
                LetterEntry entry = letterSet.Find(known);
                if (entry != null && entry.HasWords && PickOtherWords(known, Mathf.Max(1, propCount - 1)).Count >= Mathf.Max(1, propCount - 1))
                    return true;
            }
            return false;
        }

        /// <summary>Words from other letters, each with a different first sound. Letters he knows are preferred.</summary>
        List<LetterWord> PickOtherWords(char not, int count)
        {
            var letters = new List<LetterEntry>();
            foreach (LetterEntry entry in letterSet.letters)
                if (entry != null && entry.HasWords && LetterCurriculum.SoundGroup(entry.Char) != LetterCurriculum.SoundGroup(not)) letters.Add(entry);

            Shuffle(letters);
            List<char> known = KnownLetters();
            letters = KnownFirst(letters, known);

            var words = new List<LetterWord>();
            var usedSounds = new List<char>();
            foreach (LetterEntry entry in letters)
            {
                if (words.Count >= count) break;

                char sound = LetterCurriculum.SoundGroup(entry.Char);
                if (usedSounds.Contains(sound)) continue;

                LetterWord word = letterSet.PickWord(entry.Char);
                if (word == null) continue;

                words.Add(word);
                usedSounds.Add(sound);
            }
            return words;
        }

        /// <summary>Puts the props in a neat row on the table, centred, the first one on the left of the screen. They pop up in turn.</summary>
        List<CountableObject> SpawnProps(List<LetterWord> words)
        {
            var props = new List<CountableObject>();
            Quaternion facing = stage.TableFacing();

            for (int i = 0; i < words.Count; i++)
            {
                GameObject instance = Instantiate(words[i].prefab);
                var prop = instance.GetComponent<CountableObject>();
                if (prop == null) prop = instance.AddComponent<CountableObject>();

                // Table x grows toward the left of the screen, so the row is mirrored.
                float x = ((words.Count - 1) * 0.5f - i) * propSpacing;
                prop.Place(stage.TablePoint(x, propZ), facing * Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f), words[i].scale);
                prop.Appear(i * PropAppearGapSeconds);

                toys.Add(prop);
                props.Add(prop);
            }
            return props;
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
