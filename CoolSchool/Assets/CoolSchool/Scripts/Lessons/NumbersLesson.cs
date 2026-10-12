using System.Collections;
using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The numbers lesson: the kid sits at the table in the classroom and learns to count with toys.
    /// Everything is spoken (Dutch first), nothing can be failed, and every exercise moves forward on its own
    /// when the child is stuck: the prompt is repeated, then the right card or toy pulses, then the teacher helps.
    /// This part has the flow and the levels; the four kinds of exercise are in NumbersLesson.Exercises.cs,
    /// and what it shares with the letters lesson (camera, seat, voice, taps, outro) is in ClassroomLesson.
    /// </summary>
    public partial class NumbersLesson : ClassroomLesson
    {
        [Header("Toys")]
        [SerializeField] CountableSet countables;
        [Tooltip("The basket for 'give me N'. Switched off until that exercise.")]
        [SerializeField] GameObject basket;
        [Tooltip("Inter Bold: the font of the numbers floating above the toys.")]
        [SerializeField] Font numberFont;

        [Header("Table layout (metres, relative to the table surface anchor)")]
        [SerializeField] float toySpacing = 0.55f;
        [SerializeField] float maxRowWidth = 2.4f;
        [SerializeField] float rowSpacing = 0.5f;
        [Tooltip("Where the basket stands. X along the table, Z toward the camera.")]
        [SerializeField] Vector2 basketPosition = new Vector2(0f, 0.55f);
        [Tooltip("Z of the toys to give away, behind the basket.")]
        [SerializeField] float giveMeToyZ = -0.2f;

        const string Skill = "numbers";
        const int MaxLevel = 5;
        const int FirstTriesToLevelUp = 3;
        const int HintsToLevelDown = 2;

        // The biggest number the kid is asked about, for levels 1 to 5.
        static readonly int[] MaxNumberByLevel = { 3, 5, 5, 7, 10 };

        // Remembered between lessons while the game runs, so three good ones in a row can span two lessons.
        int firstTryStreak;
        int hintStreak;

        Countable lastCountable;
        ExerciseType? lastType;
        int lastNumber;

        protected override string LessonName => "numbers";

        protected override void ResetLessonState()
        {
            lastCountable = null;
            lastType = null;
            lastNumber = 0;
        }

        protected override void ClearProps()
        {
            SetBasketActive(false);
        }

        // ----- The flow of the lesson ------------------------------------------------------------

        protected override IEnumerator LessonRoutine()
        {
            if (cameraRig != null) yield return cameraRig.EnterLesson(stage.LessonCamera);

            stage.TeacherWave();
            Say("lesson.intro");
            yield return WaitForSpeech();
            yield return new WaitForSeconds(IntroPauseSeconds);

            for (int i = 0; i < exercisesPerLesson; i++)
            {
                yield return RunExercise(i);
                yield return ClearTable();
            }

            yield return OutroRoutine("lesson.outro");

            if (cameraRig != null) yield return cameraRig.ExitLesson();
            lessonRoutine = null;
            End();
        }

        IEnumerator RunExercise(int index)
        {
            int level = CurrentLevel();
            ExerciseType type = ChooseType(level, index == 0);
            int number = ChooseNumber(level);
            Countable toy = countables != null ? countables.Pick(lastCountable) : null;

            // No toys to count with: a number-card question still works.
            if (toy == null && type != ExerciseType.FindNumber) type = ExerciseType.FindNumber;

            exerciseMadeMistake = false;
            exerciseNeededHelp = false;
            lastType = type;
            lastNumber = number;
            lastCountable = toy;

            switch (type)
            {
                case ExerciseType.TapAndCount: yield return RunTapAndCount(number, toy); break;
                case ExerciseType.HowMany: yield return RunHowMany(number, toy, MaxNumberByLevel[level - 1]); break;
                case ExerciseType.FindNumber: yield return RunFindNumber(number, MaxNumberByLevel[level - 1]); break;
                default: yield return RunGiveMe(number, toy); break;
            }

            RecordResult();
        }


        // ----- Levels ----------------------------------------------------------------------------

        static int CurrentLevel()
        {
            return Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
        }

        /// <summary>The first exercise is always counting along. After that the level decides, without repeating the last kind.</summary>
        ExerciseType ChooseType(int level, bool first)
        {
            if (first) return ExerciseType.TapAndCount;

            var options = new List<ExerciseType> { ExerciseType.TapAndCount, ExerciseType.HowMany };
            if (level >= 2) options.Add(ExerciseType.FindNumber);
            if (level >= 3 && basket != null) options.Add(ExerciseType.GiveMe);

            if (lastType.HasValue && options.Count > 1) options.Remove(lastType.Value);
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>A number for this level, not the same as the one before.</summary>
        int ChooseNumber(int level)
        {
            int max = MaxNumberByLevel[level - 1];
            int number = Random.Range(1, max + 1);
            if (number == lastNumber && max > 1) number = number % max + 1;
            return number;
        }

        /// <summary>Counts the exercise and moves the level: three clean ones go up, two helped ones in a row go down.</summary>
        void RecordResult()
        {
            bool firstTry = !exerciseMadeMistake && !exerciseNeededHelp;
            if (firstTry) firstTryCorrect++;

            firstTryStreak = firstTry ? firstTryStreak + 1 : 0;
            hintStreak = exerciseNeededHelp ? hintStreak + 1 : 0;

            int level = CurrentLevel();
            if (firstTryStreak >= FirstTriesToLevelUp && level < MaxLevel)
                ChangeLevel(level + 1);
            else if (hintStreak >= HintsToLevelDown && level > 1)
                ChangeLevel(level - 1);
        }

        void ChangeLevel(int level)
        {
            firstTryStreak = 0;
            hintStreak = 0;
            ProgressStore.Current.SetLevel(Skill, level);
            ProgressStore.Save();
        }

    }
}
