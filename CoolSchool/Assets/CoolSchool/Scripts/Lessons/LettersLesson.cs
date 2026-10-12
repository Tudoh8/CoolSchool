using System.Collections;
using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The letters lesson: the kid sits at the table and learns the sounds and shapes of letters, starting with the ones in
    /// his name. Like the numbers lesson everything is spoken, nothing can be failed, and a child who is stuck is helped
    /// step by step. This part has the plan and the levels; the five kinds of exercise are in LettersLesson.Exercises.cs.
    /// </summary>
    public partial class LettersLesson : ClassroomLesson
    {
        [Header("Letters")]
        [SerializeField] LetterSet letterSet;
        [Tooltip("The chalk letters on the blackboard. Sits on the same object as the BlackboardDisplay.")]
        [SerializeField] LetterBoard letterBoard;

        [Header("Table layout (metres, relative to the table surface anchor)")]
        [SerializeField] float propSpacing = 0.85f;
        [Tooltip("Z of the props on the table, toward the camera.")]
        [SerializeField] float propZ = 0f;

        const string Skill = "letters";
        const int MaxLevel = 5;
        const int MistakesToLevelDown = 3;

        enum Kind
        {
            /// <summary>A new letter: written big, its sound, a word with a prop, then a short trace.</summary>
            NewLetter,
            /// <summary>Hear the sound and tap the right letter card.</summary>
            FindLetter,
            /// <summary>Hear a sound and tap the prop whose word starts with it.</summary>
            StartsWith,
            /// <summary>Follow the dotted path with a finger.</summary>
            Trace,
            /// <summary>Put the letters of his own name in their slots.</summary>
            SpellName,
        }

        /// <summary>One exercise of the lesson. Count is the number of cards or props to choose from.</summary>
        readonly struct Step
        {
            public readonly Kind Kind;
            public readonly int Count;

            public Step(Kind kind, int count = 0)
            {
                Kind = kind;
                Count = count;
            }
        }

        readonly List<Step> plan = new List<Step>();

        int lessonLevel = 1;
        int lessonMistakes;
        bool introducedThisLesson;
        char? newLetter;
        char currentTarget;
        char lastTarget;

        protected override string LessonName => "letters";

        protected override void ResetLessonState()
        {
            lessonMistakes = 0;
            introducedThisLesson = false;
            currentTarget = '\0';
            BuildPlan();
        }

        protected override void ClearProps()
        {
            if (letterBoard != null) letterBoard.Clear();
        }

        // ----- The flow of the lesson ------------------------------------------------------------

        protected override IEnumerator LessonRoutine()
        {
            if (plan.Count == 0 || letterBoard == null)
            {
                Debug.LogWarning("CoolSchool: the letters lesson has no letters or no letter board, so it was skipped.");
                End();
                yield break;
            }

            if (cameraRig != null) yield return cameraRig.EnterLesson(stage.LessonCamera);

            stage.TeacherWave();
            Say("lesson.letters.intro");
            yield return WaitForSpeech();
            yield return new WaitForSeconds(IntroPauseSeconds);

            for (int i = 0; i < plan.Count; i++)
            {
                yield return RunStep(plan[i]);
                yield return ClearTable();
            }

            FinishLesson();
            yield return OutroRoutine("lesson.letters.outro");

            if (cameraRig != null) yield return cameraRig.ExitLesson();
            lessonRoutine = null;
            End();
        }

        IEnumerator RunStep(Step step)
        {
            exerciseMadeMistake = false;
            exerciseNeededHelp = false;

            Kind kind = step.Kind;
            // Falls back to something that always works when the data for a kind is not there.
            if (kind == Kind.StartsWith && !CanAskStartsWith(step.Count)) kind = Kind.FindLetter;
            if (kind == Kind.SpellName && NameLetters().Count == 0) kind = Kind.FindLetter;

            switch (kind)
            {
                case Kind.NewLetter: yield return RunNewLetter(); break;
                case Kind.StartsWith: yield return RunStartsWith(Mathf.Max(2, step.Count)); break;
                case Kind.Trace: yield return RunReviewTrace(); break;
                case Kind.SpellName: yield return RunSpellName(); break;
                default: yield return RunFindLetter(Mathf.Clamp(step.Count, 2, 3)); break;
            }

            RecordResult(kind);
        }

        /// <summary>Counts the exercise for the stars, and the letter's first tries for choosing when the next letter comes.</summary>
        void RecordResult(Kind kind)
        {
            bool firstTry = !exerciseMadeMistake && !exerciseNeededHelp;
            if (!firstTry) return;

            firstTryCorrect++;
            if (kind != Kind.SpellName && currentTarget != '\0')
            {
                LetterCurriculum.AddScore(ProgressStore.Current, currentTarget);
                ProgressStore.Save();
            }
        }

        // ----- Levels ----------------------------------------------------------------------------

        static int CurrentLevel()
        {
            return Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
        }

        /// <summary>The lesson's exercises for the level the child is at right now (read fresh every lesson, never cached).</summary>
        void BuildPlan()
        {
            plan.Clear();
            newLetter = null;
            if (letterSet == null) return;

            PlayerProgress progress = ProgressStore.Current;
            lessonLevel = CurrentLevel();

            // A lesson needs at least one letter the child knows, so the very first lesson always brings one.
            if (LetterCurriculum.NewLetterDue(progress)) newLetter = LetterCurriculum.NextNewLetter(progress, letterSet);
            if (!newLetter.HasValue && KnownLetters().Count == 0) return;

            if (newLetter.HasValue) plan.Add(new Step(Kind.NewLetter));
            else if (lessonLevel == 1) plan.Add(new Step(Kind.Trace));

            bool spell = lessonLevel >= MaxLevel && NameLetters().Count > 0;
            int fillTo = spell ? exercisesPerLesson - 1 : exercisesPerLesson;
            while (plan.Count < fillTo) plan.Add(PickStep(plan.Count > 0 ? plan[plan.Count - 1].Kind : (Kind?)null));

            if (spell) plan.Add(new Step(Kind.SpellName));
        }

        /// <summary>The next exercise for this level, not of the same kind as the one before.</summary>
        Step PickStep(Kind? previous)
        {
            var options = new List<Step>();
            switch (lessonLevel)
            {
                case 1:
                    options.Add(new Step(Kind.FindLetter, 2));
                    break;
                case 2:
                    options.Add(new Step(Kind.FindLetter, 3));
                    options.Add(new Step(Kind.StartsWith, 2));
                    break;
                case 3:
                    options.Add(new Step(Kind.StartsWith, 3));
                    options.Add(new Step(Kind.Trace));
                    options.Add(new Step(Kind.FindLetter, 3));
                    break;
                default:
                    options.Add(new Step(Kind.FindLetter, 3));
                    options.Add(new Step(Kind.StartsWith, 3));
                    options.Add(new Step(Kind.Trace));
                    break;
            }

            if (previous.HasValue && options.Count > 1)
                options.RemoveAll(option => option.Kind == previous.Value);
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>Up when there were no mistakes, down after three or more. The next lesson reads the new level.</summary>
        void FinishLesson()
        {
            PlayerProgress progress = ProgressStore.Current;
            int level = CurrentLevel();

            if (lessonMistakes == 0 && level < MaxLevel) progress.SetLevel(Skill, level + 1);
            else if (lessonMistakes >= MistakesToLevelDown && level > 1) progress.SetLevel(Skill, level - 1);

            if (!introducedThisLesson) progress.lessonsSinceNewLetter++;
            ProgressStore.Save();
        }
    }
}
