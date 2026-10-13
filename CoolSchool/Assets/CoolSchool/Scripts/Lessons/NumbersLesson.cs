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
    /// This part has the flow and the levels (what each level unlocks is in NumberCurriculum). The first four kinds
    /// of exercise are in NumbersLesson.Exercises.cs; quick look, compare, one more / one less and same amount are in
    /// NumbersLesson.Exercises2.cs; number line, count back, count on and hidden are in NumbersLesson.Exercises3.cs.
    /// What it shares with the letters lesson (camera, seat, voice, taps, outro) is in ClassroomLesson.
    /// </summary>
    public partial class NumbersLesson : ClassroomLesson
    {
        [Header("Toys")]
        [SerializeField] CountableSet countables;
        [Tooltip("The basket for 'give me N'. Switched off until that exercise.")]
        [SerializeField] GameObject basket;
        [Tooltip("Inter Bold: the font of the numbers floating above the toys.")]
        [SerializeField] Font numberFont;

        [Header("Ten-frames and the cup")]
        [Tooltip("Two ten-frame trays. Toys above ten fill the first tray, then the second; compare and 'same amount' use one per group.")]
        [SerializeField] TenFrameTray[] tenFrames;
        [Tooltip("Toys in a tray are scaled by this much (on top of the toy's own scale) so they fit a cell.")]
        [SerializeField] float trayToyScale = 0.5f;
        [Tooltip("The upside-down cup for the 'how many are hiding?' exercise.")]
        [SerializeField] HidingCup hidingCup;

        // The table is about 2.9 m wide (x, growing toward the left of the screen) and 1.4 m deep (z, toward the camera), and the
        // camera looks down at it from the front at about 41 degrees. Rows of toys 0.6 m apart do not hide each other from there;
        // anything closer than that is staggered. Everything that belongs together stands side by side across the screen.
        [Header("Table layout (metres, relative to the table surface anchor)")]
        [SerializeField] float toySpacing = 0.55f;
        [SerializeField] float maxRowWidth = 2.4f;
        [SerializeField] float rowSpacing = 0.62f;
        [Tooltip("Where the basket stands, at the right end of the table. X along the table, Z toward the camera.")]
        [SerializeField] Vector2 basketPosition = new Vector2(-1.1f, 0f);
        [Tooltip("Z of the middle of the toys to give away. They stand left of the basket, not behind it.")]
        [SerializeField] float giveMeToyZ = 0f;
        [Tooltip("X of the middle of the toys to give away, and how wide their rows may be.")]
        [SerializeField] float giveMeToysX = 0.3f;
        [SerializeField] float giveMeToysWidth = 1.8f;

        const int FirstTriesToLevelUp = 3;
        const int HintsToLevelDown = 2;
        // A kind of exercise that was unlocked at exactly this level is chosen this many times as often.
        const int NewTypeWeight = 3;

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
            basketToys.Clear();
            HideTrays();
            if (hidingCup != null) hidingCup.Hide();
            ClearCountOnLabel();
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
            int level = NumberCurriculum.CurrentLevel();
            int max = NumberCurriculum.MaxNumber(level);
            ExerciseType type = ChooseType(level, index == 0);
            int number = ChooseNumber(max);
            Countable toy = countables != null ? countables.Pick(lastCountable) : null;

            // No toys to count with: a number-card question still works.
            if (toy == null && NeedsToys(type)) type = ExerciseType.FindNumber;

            exerciseMadeMistake = false;
            exerciseNeededHelp = false;
            lastType = type;
            lastCountable = toy;

            // The first four kinds use the number chosen here; the others pick their own (see PickNumber).
            if (type <= ExerciseType.GiveMe) lastNumber = number;

            switch (type)
            {
                case ExerciseType.TapAndCount: yield return RunTapAndCount(number, toy); break;
                case ExerciseType.HowMany: yield return RunHowMany(number, toy, max); break;
                case ExerciseType.FindNumber: yield return RunFindNumber(number, max); break;
                case ExerciseType.GiveMe: yield return RunGiveMe(Mathf.Min(number, MaxGiveMeNumber), toy); break;
                case ExerciseType.QuickLook: yield return RunQuickLook(level, max); break;
                case ExerciseType.Compare: yield return RunCompare(toy, level, max); break;
                case ExerciseType.OneMoreOneLess: yield return RunOneMoreOneLess(toy, max); break;
                case ExerciseType.SameAmount: yield return RunSameAmount(toy, max); break;
                case ExerciseType.NumberLine: yield return RunNumberLine(level, max); break;
                case ExerciseType.CountBack: yield return RunCountBack(toy, max); break;
                case ExerciseType.CountOn: yield return RunCountOn(toy, level, max); break;
                default: yield return RunHidden(toy, level, max); break;
            }

            RecordResult();
        }

        static bool NeedsToys(ExerciseType type)
        {
            return type != ExerciseType.FindNumber && type != ExerciseType.QuickLook && type != ExerciseType.NumberLine;
        }

        // ----- Levels ----------------------------------------------------------------------------

        /// <summary>
        /// The first exercise is always counting along. After that every kind whose idea is unlocked at this level is a
        /// candidate (kinds that need the basket, the trays or the cup only when those exist). A kind that was unlocked at
        /// exactly this level comes up three times as often, and the same kind never comes twice in a row.
        /// </summary>
        ExerciseType ChooseType(int level, bool first)
        {
            if (first) return ExerciseType.TapAndCount;

            var options = new List<ExerciseType>();
            foreach (ExerciseType type in System.Enum.GetValues(typeof(ExerciseType)))
            {
                if (!NumberCurriculum.IsUnlocked(type.SkillOf(), level) || !HasProps(type)) continue;
                if (lastType.HasValue && type == lastType.Value) continue;

                int weight = NumberCurriculum.UnlockLevel(type.SkillOf()) == level ? NewTypeWeight : 1;
                for (int i = 0; i < weight; i++) options.Add(type);
            }

            if (options.Count == 0) return ExerciseType.TapAndCount;
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>Whether what the exercise stands on (basket, trays, cup) is in the scene.</summary>
        bool HasProps(ExerciseType type)
        {
            switch (type)
            {
                case ExerciseType.GiveMe:
                case ExerciseType.CountOn:
                    return basket != null;
                case ExerciseType.Compare:
                    return TraysReady;
                case ExerciseType.SameAmount:
                    return TraysReady && basket != null;
                case ExerciseType.Hidden:
                    return hidingCup != null;
                default:
                    return true;
            }
        }

        /// <summary>A number from 1 to the level's biggest, not the same as the one before.</summary>
        int ChooseNumber(int max)
        {
            int number = Random.Range(1, max + 1);
            if (number == lastNumber && max > 1) number = number % max + 1;
            return number;
        }

        /// <summary>A number between low and high (both included), not the same as the one before. It becomes "the one before" for the next exercise.</summary>
        int PickNumber(int low, int high)
        {
            high = Mathf.Max(low, high);
            int number = Random.Range(low, high + 1);
            if (number == lastNumber && high > low) number = number == high ? low : number + 1;

            lastNumber = number;
            return number;
        }

        /// <summary>Counts the exercise and moves the level: three clean ones go up, two helped ones in a row go down.</summary>
        void RecordResult()
        {
            bool firstTry = !exerciseMadeMistake && !exerciseNeededHelp;
            if (firstTry) firstTryCorrect++;

            firstTryStreak = firstTry ? firstTryStreak + 1 : 0;
            hintStreak = exerciseNeededHelp ? hintStreak + 1 : 0;

            int level = NumberCurriculum.CurrentLevel();
            if (firstTryStreak >= FirstTriesToLevelUp && level < NumberCurriculum.MaxLevel)
                ChangeLevel(level + 1);
            else if (hintStreak >= HintsToLevelDown && level > 1)
                ChangeLevel(level - 1);
        }

        void ChangeLevel(int level)
        {
            firstTryStreak = 0;
            hintStreak = 0;
            ProgressStore.Current.SetLevel(NumberCurriculum.Skill, level);
            ProgressStore.Save();
        }

    }
}
