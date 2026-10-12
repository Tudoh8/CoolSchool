using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The learning gates: before the race each gate gets its question (a number or a letter sound, one answer per lane),
    /// while driving the announcer asks it a few seconds ahead, and driving through a lane gives the answer. A wrong lane has
    /// no penalty: the right arch just flashes.
    /// </summary>
    public partial class SoapboxRace
    {
        // The right answer is worth this many bonus coins.
        const int GateBonusCoins = 5;
        const float PromptQuietSeconds = 4f;
        const float AnswerQuietSeconds = 2.5f;
        const float ReminderSeconds = 2.2f;

        // The target bubble shows "7→?" for "which number comes after 7?" (the HUD swaps the arrow for a space if the font lacks it).
        const string ArrowGlyph = "→?";

        sealed class GateQuestion
        {
            public string[] options;
            public int correctLane;
            public LearningGate.GateKind kind;
            public string glyph;
            public int number;
            public bool after;
            public char letter;
            public bool reminded;
        }

        readonly List<LearningGate> gates = new List<LearningGate>();
        readonly Dictionary<LearningGate, GateQuestion> questions = new Dictionary<LearningGate, GateQuestion>();

        // The gate that was asked and not driven through yet.
        LearningGate currentGate;

        // ----- Making the questions ----------------------------------------------------------------

        /// <summary>One question per active gate: number, letter, number. A letter gate becomes a number gate before any letter is learned.</summary>
        void PrepareGates()
        {
            UnsubscribeGates();
            gates.Clear();
            questions.Clear();
            currentGate = null;
            if (track == null) return;

            List<char> known = KnownLetters();
            var usedNumbers = new List<int>();
            var usedLetters = new List<char>();

            for (int i = 0; i < track.Gates.Count; i++)
            {
                LearningGate gate = track.Gates[i];
                if (gate == null) continue;

                bool letter = i % 2 == 1 && known.Count > 0;
                GateQuestion question = letter ? MakeLetterQuestion(known, usedLetters) : MakeNumberQuestion(usedNumbers);

                gate.Passed += OnGatePassed;
                gates.Add(gate);
                questions[gate] = question;
                gate.Announced = false;
                gate.LightUp(false);
                gate.Setup(question.options, question.correctLane, question.kind);
            }
        }

        void UnsubscribeGates()
        {
            foreach (LearningGate gate in gates)
                if (gate != null) gate.Passed -= OnGatePassed;
        }

        GateQuestion MakeNumberQuestion(List<int> used)
        {
            int max = Mathf.Clamp(NumberCurriculum.MaxNumber(), 3, NumberCurriculum.HighestNumber);

            // The right answer: not the same number twice in one race.
            var candidates = new List<int>();
            for (int n = 1; n <= max; n++)
                if (!used.Contains(n)) candidates.Add(n);
            if (candidates.Count == 0)
                for (int n = 1; n <= max; n++) candidates.Add(n);
            int target = candidates[Random.Range(0, candidates.Count)];
            used.Add(target);

            // Once he has met "which number comes after", half of the number gates ask it (the answer is N + 1).
            bool after = target >= 2 && NumberCurriculum.IsUnlocked(NumberSkill.NumberLine) && Random.value < 0.5f;
            if (after) return MakeAfterQuestion(target - 1, max);

            // Two other numbers to choose from (at least 1 to 3, so there is always something to pick).
            // Teens are compared with numbers close by, so he has to look at the digits.
            var others = new List<int>();
            int low = target > 10 ? Mathf.Max(1, target - 3) : 1;
            int high = target > 10 ? Mathf.Min(max, target + 3) : max;
            for (int n = low; n <= Mathf.Max(high, 3); n++)
                if (n != target) others.Add(n);
            Shuffle(others);

            var question = new GateQuestion
            {
                kind = LearningGate.GateKind.Number,
                number = target,
                glyph = target.ToString(),
            };
            question.correctLane = Random.Range(0, 3);
            question.options = FillOptions(question.correctLane, target.ToString(), others[0].ToString(), others[1].ToString());
            return question;
        }

        /// <summary>"Which number comes after N?": the right lane shows N + 1; N itself is a good wrong answer.</summary>
        GateQuestion MakeAfterQuestion(int n, int max)
        {
            int answer = n + 1;
            var others = new List<int>();
            foreach (int candidate in new[] { n, n + 2, n - 1, n + 3 })
                if (candidate >= 1 && candidate <= max && candidate != answer) others.Add(candidate);

            // Always two wrong answers, also at the edges of the range.
            for (int fill = 1; others.Count < 2 && fill <= max; fill++)
                if (fill != answer && !others.Contains(fill)) others.Add(fill);
            Shuffle(others);

            var question = new GateQuestion
            {
                kind = LearningGate.GateKind.Number,
                after = true,
                number = n,
                glyph = n + ArrowGlyph,
            };
            question.correctLane = Random.Range(0, 3);
            question.options = FillOptions(question.correctLane, answer.ToString(), others[0].ToString(), others[1].ToString());
            return question;
        }

        GateQuestion MakeLetterQuestion(List<char> known, List<char> used)
        {
            PlayerProgress progress = ProgressStore.Current;
            string name = ProgressStore.NormalizeChildName(progress.childName);

            // His name's first letter half of the time, otherwise any letter he has had (not twice in a race).
            char target;
            if (name.Length > 0 && known.Contains(name[0]) && !used.Contains(name[0]) && Random.value < 0.5f)
            {
                target = name[0];
            }
            else
            {
                var candidates = new List<char>();
                foreach (char c in known)
                    if (!used.Contains(c)) candidates.Add(c);
                if (candidates.Count == 0) candidates.AddRange(known);
                target = candidates[Random.Range(0, candidates.Count)];
            }
            used.Add(target);

            // Letters from the school order; c, k and q say the same sound; look-alikes only from level 3.
            var others = new List<char>();
            foreach (char c in LetterCurriculum.SchoolOrder)
            {
                if (c == target) continue;
                if (LetterCurriculum.SoundGroup(c) == LetterCurriculum.SoundGroup(target)) continue;
                if (level < MaxLevel && LetterCurriculum.AreSimilar(target, c)) continue;
                others.Add(c);
            }
            Shuffle(others);

            var question = new GateQuestion
            {
                kind = LearningGate.GateKind.Letter,
                letter = target,
                glyph = target.ToString(),
            };
            question.correctLane = Random.Range(0, 3);
            question.options = FillOptions(question.correctLane, target.ToString(), others[0].ToString(), others[1].ToString());
            return question;
        }

        /// <summary>The right answer in its lane, the two others in the remaining lanes.</summary>
        static string[] FillOptions(int correctLane, string right, string otherA, string otherB)
        {
            var options = new string[3];
            options[correctLane] = right;
            bool first = true;
            for (int lane = 0; lane < 3; lane++)
            {
                if (lane == correctLane) continue;
                options[lane] = first ? otherA : otherB;
                first = false;
            }
            return options;
        }

        /// <summary>The letters he has been taught, lowercase a to z, without doubles.</summary>
        static List<char> KnownLetters()
        {
            var known = new List<char>();
            List<string> introduced = ProgressStore.Current.lettersIntroduced;
            if (introduced == null) return known;

            foreach (string text in introduced)
            {
                if (string.IsNullOrEmpty(text)) continue;

                char c = char.ToLowerInvariant(text[0]);
                if (c >= 'a' && c <= 'z' && !known.Contains(c)) known.Add(c);
            }
            return known;
        }

        // ----- Asking the question while driving ---------------------------------------------------

        /// <summary>Asks the next gate when it is about five seconds ahead, and once more just before it.</summary>
        void UpdateGates()
        {
            foreach (LearningGate gate in gates)
            {
                if (gate == null || gate.PlayerPassed) continue;

                if (!gate.Announced)
                {
                    float speed = Mathf.Max(playerCart.Speed, CruiseSpeedByLevel[level - 1] * GateSlowFactor);
                    if (gate.Distance - playerCart.Distance <= speed * gateLeadSeconds) Announce(gate);
                }
                else if (gate == currentGate)
                {
                    Remind(gate);
                }

                // Gates are in distance order: only the first one that is not passed matters.
                break;
            }
        }

        void Announce(LearningGate gate)
        {
            if (!questions.TryGetValue(gate, out GateQuestion question)) return;

            // At level 1 the answer is never in the lane he is in, so he has to steer (just one lane).
            if (level == 1 && question.correctLane == playerCart.Lane) MoveAnswerAwayFrom(gate, question, playerCart.Lane);

            gate.Announced = true;
            gate.LightUp(true);
            currentGate = gate;
            if (hud != null) hud.ShowTarget(question.glyph);

            SayQuestion(question, true);
            quietUntil = Time.time + PromptQuietSeconds;
        }

        /// <summary>Just before the gate, if he still has not passed it and nobody is talking: the question once more.</summary>
        void Remind(LearningGate gate)
        {
            if (!questions.TryGetValue(gate, out GateQuestion question) || question.reminded) return;
            if (playerCart.Speed < 0.5f) return;

            float seconds = (gate.Distance - playerCart.Distance) / playerCart.Speed;
            if (seconds > ReminderSeconds || seconds < 0.8f) return;
            if (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy) return;

            question.reminded = true;
            SayQuestion(question, false);
            quietUntil = Time.time + ReminderSeconds;
        }

        static void SayQuestion(GateQuestion question, bool interrupt)
        {
            if (question.kind == LearningGate.GateKind.Number)
            {
                VoicePlayer.Say((question.after ? "race.gate_after." : "race.gate_number.") + question.number, interrupt);
                return;
            }

            VoicePlayer.Say("race.gate_letter", interrupt);
            VoicePlayer.Say("letter.sound." + question.letter);
        }

        /// <summary>Swaps the right answer with another lane's, so it is not where the cart is.</summary>
        void MoveAnswerAwayFrom(LearningGate gate, GateQuestion question, int lane)
        {
            int other = (lane + (Random.value < 0.5f ? 1 : 2)) % 3;
            (question.options[lane], question.options[other]) = (question.options[other], question.options[lane]);
            question.correctLane = other;
            gate.Setup(question.options, question.correctLane, question.kind);
        }

        // ----- Driving through ---------------------------------------------------------------------

        void OnGatePassed(LearningGate gate, int lane, bool correct)
        {
            if (phase != Phase.Racing) return;

            if (gate == currentGate) currentGate = null;
            if (hud != null) hud.ShowTarget(null);
            gate.LightUp(false);
            quietUntil = Time.time + AnswerQuietSeconds;

            if (correct)
            {
                gatesRight++;
                gate.Celebrate();
                coins += GateBonusCoins;
                if (hud != null)
                {
                    hud.SetCoins(coins, true);
                    hud.FlyCoinsFromWorld(gate.transform.position + Vector3.up * 1.5f, GateBonusCoins);
                }
                Sfx.Play("race.gate_ding");
                StartCoroutine(CoinSounds(GateBonusCoins));
                VoicePlayer.Say("race.gate_right", true);
            }
            else
            {
                // No penalty: the right arch flashes so he can see where it was.
                gatesWrong++;
                gate.ShowRight();
                VoicePlayer.Say("race.gate_wrong", true);
            }
        }

        IEnumerator CoinSounds(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Sfx.Play("race.coin");
                yield return new WaitForSeconds(0.09f);
            }
        }
    }
}
