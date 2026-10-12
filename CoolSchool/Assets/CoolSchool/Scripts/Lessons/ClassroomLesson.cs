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
    /// What every lesson in the classroom shares: the camera gliding in and out, seating the kid at the table,
    /// the teacher's voice, tapping toys, noticing a child who has gone quiet, the outro with the stars and the
    /// golden sword, and putting everything back when the lesson ends (at any moment). The numbers lesson and the
    /// letters lesson fill in the rest: their exercises and their levels.
    /// </summary>
    public abstract class ClassroomLesson : Activity
    {
        [Header("Classroom")]
        [SerializeField] protected LessonStage stage;
        [SerializeField] protected LessonCameraRig cameraRig;
        [SerializeField] protected BlackboardDisplay blackboard;

        [Header("Lesson")]
        [SerializeField] protected int exercisesPerLesson = 4;
        [SerializeField] protected int starsPerCorrect = 1;
        [SerializeField] protected int minStars = 3;

        protected const float IntroPauseSeconds = 0.4f;
        protected const float BetweenExercisesSeconds = 0.5f;
        protected const float MaxSpeechWaitSeconds = 10f;
        protected const float StarsLandSeconds = 3f;
        // The golden sword floats this high above the table top, and flying away never takes longer than this.
        protected const float GoldenSwordHeight = 0.9f;
        protected const float MaxSwordFlightSeconds = 3f;
        protected const float TapRayRadius = 0.08f;
        protected const float TapSlopFraction = 0.12f;

        // A child who does nothing for this long hears the prompt again.
        protected const float IdleSeconds = 8f;
        protected const int RepeatsBeforeHelp = 2;
        // After this many quiet rounds the teacher does the exercise herself, a little faster each time.
        protected const int CyclesBeforeAutoHelp = 6;
        protected const float AutoHelpIdleSeconds = 3f;

        protected Coroutine lessonRoutine;
        protected PlayerController player;
        protected System.Action<CountableObject> tapHandler;
        protected float lastActivityTime;
        protected readonly List<CountableObject> toys = new List<CountableObject>();

        protected int firstTryCorrect;
        protected bool exerciseMadeMistake;
        protected bool exerciseNeededHelp;

        bool quitting;

        protected LessonUI UI => stage != null ? stage.UI : null;

        /// <summary>What the lesson is called in warnings, e.g. "numbers".</summary>
        protected abstract string LessonName { get; }

        /// <summary>The whole lesson, from the camera gliding in to calling End.</summary>
        protected abstract IEnumerator LessonRoutine();

        /// <summary>Forget what happened in the previous lesson (last toy, last exercise, ...). Runs at the start.</summary>
        protected virtual void ResetLessonState() { }

        /// <summary>Takes down whatever the lesson put on the table or the board besides the toys. Runs between exercises and at the end.</summary>
        protected virtual void ClearProps() { }

        protected sealed override void OnBegin()
        {
            if (stage == null || stage.TableSurface == null || stage.UI == null)
            {
                Debug.LogWarning($"CoolSchool: the {LessonName} lesson has no stage, table anchor or lesson UI, so it can not start.");
                End();
                return;
            }

            if (player == null) player = FindAnyObjectByType<PlayerController>();

            firstTryCorrect = 0;
            ResetLessonState();

            MovePlayerToSeat();
            Sfx.Play("lesson.bell");
            Sfx.PlayMusic("music.classroom");
            if (UI != null) UI.SetGameplayHudVisible(false);
            lessonRoutine = StartCoroutine(LessonRoutine());
        }

        protected sealed override void OnEnd()
        {
            if (lessonRoutine != null)
            {
                StopCoroutine(lessonRoutine);
                lessonRoutine = null;
            }

            tapHandler = null;
            GoldenSwordReward.Finish();
            RemoveToys();
            ClearProps();

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
            if (!IsRunning || tapHandler == null || CoolSchool.Parent.ParentGate.InputBlocked) return;

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
        protected void NoteActivity()
        {
            lastActivityTime = Time.time;
        }

        // ----- Idle child ------------------------------------------------------------------------

        /// <summary>Watches for a child who has gone quiet. Tick returns true once each time the quiet has lasted long enough.</summary>
        protected sealed class IdleTimer
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
        protected void HandleIdle(int cycles, System.Action prompt, System.Action help, System.Action autoSolve)
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

        protected static bool IsBusy()
        {
            return IsSpeaking();
        }

        // ----- Between exercises -----------------------------------------------------------------

        /// <summary>Wipes the table with a small poof and gets ready for the next exercise.</summary>
        protected IEnumerator ClearTable()
        {
            if (UI != null) UI.HideCards();
            if (blackboard != null) blackboard.Clear();
            tapHandler = null;

            foreach (CountableObject toy in toys)
                if (toy != null) BattleEffects.Sparkle(toy.transform.position + Vector3.up * 0.15f);
            RemoveToys();
            ClearProps();

            yield return new WaitForSeconds(BetweenExercisesSeconds);
        }

        protected void RemoveToys()
        {
            foreach (CountableObject toy in toys)
                if (toy != null) Destroy(toy.gameObject);
            toys.Clear();
        }

        protected static void Pulse(CountableObject toy)
        {
            if (toy != null) toy.Highlight(true);
        }

        /// <summary>Confetti over the table.</summary>
        protected void Cheer()
        {
            BattleEffects.Confetti(stage.TablePoint(0f, 0f) + Vector3.up * 0.8f);
            stage.TeacherWave();
        }

        // ----- The outro -------------------------------------------------------------------------

        /// <summary>The thank-you, the stars, and the golden sword for the castle.</summary>
        protected IEnumerator OutroRoutine(string outroGroup)
        {
            Say(outroGroup);
            stage.TeacherWave();
            yield return WaitForSpeech();

            int stars = Mathf.Max(minStars, firstTryCorrect * starsPerCorrect);
            RewardSystem.AddStars(stars, stage.TableSurface.position, announce: false);
            float starsGivenAt = Time.time;
            yield return new WaitForSeconds(0.6f);

            // The golden sword is shown big above the table while the teacher talks, then flies into the badge by the star jar.
            // A sword that is still waiting from an earlier lesson is not promised twice.
            bool alreadyPending = ProgressStore.Current.pendingPowerUp == PowerUps.GoldenSword;
            GoldenSwordReward.Begin(stage.TableSurface.position + Vector3.up * GoldenSwordHeight, !alreadyPending);
            if (!alreadyPending)
            {
                ProgressStore.Current.pendingPowerUp = PowerUps.GoldenSword;
                ProgressStore.Save();
            }
            Say(alreadyPending ? "lesson.powerup_waiting" : "lesson.powerup");
            yield return WaitForSpeech();

            GoldenSwordReward.Finish();
            float swordGiveUp = Time.time + MaxSwordFlightSeconds;
            while (GoldenSwordReward.IsPlaying && Time.time < swordGiveUp) yield return null;

            // Leave only when the stars have landed in the jar.
            float wait = starsGivenAt + StarsLandSeconds - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);
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

        protected static void Say(string group, bool interrupt = false)
        {
            VoicePlayer.Say(group, interrupt);
        }

        protected static bool IsSpeaking()
        {
            VoicePlayer voice = VoicePlayer.Instance;
            return voice != null && voice.IsSpeaking;
        }

        /// <summary>Waits for the teacher to finish, but never for long.</summary>
        protected static IEnumerator WaitForSpeech()
        {
            // Gives a line that was just requested a frame to start playing.
            yield return null;

            float giveUp = Time.time + MaxSpeechWaitSeconds;
            while (IsSpeaking() && Time.time < giveUp) yield return null;
        }
    }
}
