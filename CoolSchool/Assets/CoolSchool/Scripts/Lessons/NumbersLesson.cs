using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The numbers lesson: the kid sits at the table in the classroom and learns to count with toys.
    /// Everything is spoken (Dutch first), nothing can be failed, and every exercise moves forward on its own
    /// when the child is stuck: the prompt is repeated, then the right card or toy pulses, then the teacher helps.
    /// This part has the flow and the levels; the four kinds of exercise are in NumbersLesson.Exercises.cs.
    /// </summary>
    public partial class NumbersLesson : Activity
    {
        [Header("Classroom")]
        [SerializeField] LessonStage stage;
        [SerializeField] LessonCameraRig cameraRig;
        [SerializeField] BlackboardDisplay blackboard;
        [SerializeField] CountableSet countables;
        [Tooltip("The basket for 'give me N'. Switched off until that exercise.")]
        [SerializeField] GameObject basket;
        [Tooltip("Inter Bold: the font of the numbers floating above the toys.")]
        [SerializeField] Font numberFont;

        [Header("Lesson")]
        [SerializeField] int exercisesPerLesson = 4;
        [SerializeField] int starsPerCorrect = 1;
        [SerializeField] int minStars = 3;

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
        const float IntroPauseSeconds = 0.4f;
        const float BetweenExercisesSeconds = 0.5f;
        const float MaxSpeechWaitSeconds = 10f;
        const float StarsLandSeconds = 3f;
        const float TapRayRadius = 0.08f;
        const float TapSlopFraction = 0.12f;

        // The biggest number the kid is asked about, for levels 1 to 5.
        static readonly int[] MaxNumberByLevel = { 3, 5, 5, 7, 10 };

        Coroutine lessonRoutine;
        PlayerController player;
        bool quitting;
        System.Action<CountableObject> tapHandler;
        float lastActivityTime;

        readonly List<CountableObject> toys = new List<CountableObject>();

        // Remembered between lessons while the game runs, so three good ones in a row can span two lessons.
        int firstTryStreak;
        int hintStreak;

        int firstTryCorrect;
        Countable lastCountable;
        ExerciseType? lastType;
        int lastNumber;
        bool exerciseMadeMistake;
        bool exerciseNeededHelp;

        LessonUI UI => stage != null ? stage.UI : null;

        protected override void OnBegin()
        {
            if (stage == null || stage.TableSurface == null || stage.UI == null)
            {
                Debug.LogWarning("CoolSchool: the numbers lesson has no stage, table anchor or lesson UI, so it can not start.");
                End();
                return;
            }

            if (player == null) player = FindAnyObjectByType<PlayerController>();

            firstTryCorrect = 0;
            lastCountable = null;
            lastType = null;
            lastNumber = 0;

            MovePlayerToSeat();
            Sfx.Play("lesson.bell");
            Sfx.PlayMusic("music.classroom");
            if (UI != null) UI.SetGameplayHudVisible(false);
            lessonRoutine = StartCoroutine(LessonRoutine());
        }

        protected override void OnEnd()
        {
            if (lessonRoutine != null)
            {
                StopCoroutine(lessonRoutine);
                lessonRoutine = null;
            }

            tapHandler = null;
            RemoveToys();
            SetBasketActive(false);

            if (blackboard != null) blackboard.Clear();
            if (UI != null)
            {
                UI.HideCards();
                UI.SetGameplayHudVisible(true);
            }

            if (cameraRig != null) cameraRig.RestoreNow();
            if (player != null) player.InputEnabled = true;
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-lesson, put everything back first.
            if (IsRunning && !quitting) End();
        }

        // ----- Taps ------------------------------------------------------------------------------

        void Update()
        {
            if (!IsRunning || tapHandler == null) return;

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            CountableObject toy = PickToy(pointer.position.ReadValue());
            if (toy == null) return;

            NoteActivity();
            tapHandler(toy);
        }

        /// <summary>The toy under the finger. A near miss counts too, because small fingers are not precise.</summary>
        CountableObject PickToy(Vector2 screenPosition)
        {
            Camera view = Camera.main;
            if (view == null) return null;

            CountableObject hit = RaycastToy(view.ScreenPointToRay(screenPosition));
            return hit != null ? hit : NearestToyOnScreen(view, screenPosition);
        }

        static CountableObject RaycastToy(Ray ray)
        {
            RaycastHit[] hits = Physics.SphereCastAll(ray, TapRayRadius, 50f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Walls, the table and the blackboard may be in the way. Only toys are of interest.
            foreach (RaycastHit hit in hits)
            {
                var toy = hit.collider.GetComponentInParent<CountableObject>();
                if (toy != null) return toy;
            }
            return null;
        }

        CountableObject NearestToyOnScreen(Camera view, Vector2 screenPosition)
        {
            float best = Screen.height * TapSlopFraction;
            CountableObject nearest = null;

            foreach (CountableObject toy in toys)
            {
                if (toy == null) continue;

                Vector3 point = view.WorldToScreenPoint(toy.transform.position + Vector3.up * 0.15f);
                float distance = Vector2.Distance(screenPosition, point);
                if (point.z <= 0f || distance >= best) continue;

                best = distance;
                nearest = toy;
            }
            return nearest;
        }

        /// <summary>Something real happened (a toy or a card was tapped), so the child is not stuck.</summary>
        void NoteActivity()
        {
            lastActivityTime = Time.time;
        }

        // ----- The flow of the lesson ------------------------------------------------------------

        IEnumerator LessonRoutine()
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

            yield return OutroRoutine();

            if (cameraRig != null) yield return cameraRig.ExitLesson();
            lessonRoutine = null;
            End();
        }

        /// <summary>The thank-you, the stars, and the golden sword for the castle.</summary>
        IEnumerator OutroRoutine()
        {
            Say("lesson.outro");
            stage.TeacherWave();
            yield return WaitForSpeech();

            int stars = Mathf.Max(minStars, firstTryCorrect * starsPerCorrect);
            RewardSystem.AddStars(stars, stage.TableSurface.position);
            float starsGivenAt = Time.time;
            yield return new WaitForSeconds(0.6f);

            ProgressStore.Current.pendingPowerUp = PowerUps.GoldenSword;
            ProgressStore.Save();
            Say("lesson.powerup");
            yield return WaitForSpeech();

            // Leave only when the stars have landed in the jar.
            float wait = starsGivenAt + StarsLandSeconds - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);
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

        /// <summary>Wipes the table with a small poof and gets ready for the next exercise.</summary>
        IEnumerator ClearTable()
        {
            if (UI != null) UI.HideCards();
            if (blackboard != null) blackboard.Clear();
            tapHandler = null;

            foreach (CountableObject toy in toys)
                if (toy != null) BattleEffects.Sparkle(toy.transform.position + Vector3.up * 0.15f);
            RemoveToys();
            SetBasketActive(false);

            yield return new WaitForSeconds(BetweenExercisesSeconds);
        }

        void RemoveToys()
        {
            foreach (CountableObject toy in toys)
                if (toy != null) Destroy(toy.gameObject);
            toys.Clear();
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

        // ----- The child in the classroom --------------------------------------------------------

        /// <summary>Sits the kid at the table, looking at it, with the joystick switched off.</summary>
        void MovePlayerToSeat()
        {
            if (player == null || stage.PlayerSeat == null) return;

            Vector3 toTable = stage.TableSurface.position - stage.PlayerSeat.position;
            toTable.y = 0f;
            Quaternion facing = toTable.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toTable, Vector3.up) : stage.PlayerSeat.rotation;

            // A CharacterController would fight a teleport, so it sleeps for a moment.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(stage.PlayerSeat.position, facing);
            if (controller != null) controller.enabled = true;

            player.InputEnabled = false;
        }

        // ----- Voice -----------------------------------------------------------------------------

        static void Say(string group, bool interrupt = false)
        {
            VoicePlayer.Say(group, interrupt);
        }

        static bool IsSpeaking()
        {
            VoicePlayer voice = VoicePlayer.Instance;
            return voice != null && voice.IsSpeaking;
        }

        /// <summary>Waits for the teacher to finish, but never for long.</summary>
        static IEnumerator WaitForSpeech()
        {
            // Gives a line that was just requested a frame to start playing.
            yield return null;

            float giveUp = Time.time + MaxSpeechWaitSeconds;
            while (IsSpeaking() && Time.time < giveUp) yield return null;
        }
    }
}
