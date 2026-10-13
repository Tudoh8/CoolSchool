using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Characters;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// On the player. Runs at most one toy routine at a time, watches the joystick for a skip, ends the routine when an activity starts,
    /// and gives the sticker (with its little moment) when a first play is over.
    /// </summary>
    public class ToyPlayer : MonoBehaviour
    {
        // The stick must be let go (below the first number) and then pushed (above the second) to skip.
        const float ReleasedBelow = 0.2f;
        const float PushedAbove = 0.55f;
        const float MinGapSeconds = 1.0f;
        const float RestSeconds = 0.5f;
        const float StickerGapSeconds = 0.6f;
        // A routine that has not ended after this long is stopped (a bug in a routine must never trap him on a toy).
        const float WatchdogSeconds = 40f;

        public static ToyPlayer Instance { get; private set; }

        /// <summary>Raised when a routine starts.</summary>
        public static event Action<PlaygroundToy> Started;

        /// <summary>Raised when a routine has ended and the kid is back on the ground.</summary>
        public static event Action<PlaygroundToy> Finished;

        public bool IsPlaying => current != null;
        public PlaygroundToy CurrentToy => current;

        /// <summary>The player this component sits on.</summary>
        public PlayerController Player => player;

        PlayerController player;
        KidAnimator kid;
        PlaygroundToy current;
        ToyPlayContext context;
        Coroutine run;
        bool stickReleased;
        float lastEndTime = -100f;
        float startTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Started = null;
            Finished = null;
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            player = GetComponent<PlayerController>();
            kid = GetComponent<KidAnimator>();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (current != null) CleanUpQuietly();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (current == null) return;

            // An activity starting (the parent area or a debug menu could start one) ends the toy at once.
            ActivityManager manager = ActivityManager.Instance;
            if (manager != null && manager.Current != manager.DefaultActivity)
            {
                StopNow();
                return;
            }

            if (Time.time - startTime > WatchdogSeconds)
            {
                Debug.LogWarning("CoolSchool toys: a routine ran too long and was stopped.");
                StopNow();
                return;
            }

            // A push of the stick skips, but only after the stick has been let go once.
            float push = player != null ? player.MoveInput.magnitude : 0f;
            if (push < ReleasedBelow) stickReleased = true;
            else if (stickReleased && push > PushedAbove && context != null) context.SkipRequested = true;
        }

        /// <summary>Starts the toy's routine when every start rule allows it. Returns false when it is not allowed.</summary>
        public bool TryStart(PlaygroundToy toy)
        {
            return TryStart(toy, false);
        }

        /// <summary>The same, optionally ignoring the rules (the "Play now" menu of a toy).</summary>
        internal bool TryStart(PlaygroundToy toy, bool ignoreRules)
        {
            if (toy == null || toy.Routine == null || current != null || player == null) return false;

            if (!ignoreRules)
            {
                ActivityManager manager = ActivityManager.Instance;
                if (manager == null || manager.DefaultActivity == null || manager.Current != manager.DefaultActivity) return false;
                if (CoolSchool.Parent.ParentGate.InputBlocked) return false;
                if (!player.InputEnabled || !player.IsGrounded || player.IsRiding) return false;
                if (!toy.Armed) return false;
                if (Time.time - lastEndTime < MinGapSeconds) return false;
            }
            else if (player.IsRiding)
            {
                return false;
            }

            current = toy;
            toy.Disarm();
            context = new ToyPlayContext(player, kid, toy);
            stickReleased = false;
            startTime = Time.time;
            Started?.Invoke(toy);
            run = StartCoroutine(Run(toy, context));
            return true;
        }

        /// <summary>Ends the routine at once: the parts snap to rest, the kid hops to the exit, the pose is None.</summary>
        public void StopNow()
        {
            if (current == null) return;

            if (run != null) StopCoroutine(run);
            run = null;

            PlaygroundToy toy = current;
            ToyPlayContext ended = context;
            CleanUp(toy, true);
            End(toy, ended);
        }

        // ---- The routine ----

        IEnumerator Run(PlaygroundToy toy, ToyPlayContext ctx)
        {
            yield return SafeRun(toy.Routine.Play(ctx), toy.name);

            // Whatever happened, he is off the toy and the pose is gone.
            if (player != null && player.IsRiding) yield return SafeRun(ctx.HopOff(ExitPosition(toy)), toy.name);
            ctx.SetPose(ToyPose.None);
            if (kid != null) kid.SpeedOverride = -1f;

            yield return SafeRun(toy.Routine.EaseToRest(RestSeconds), toy.name);
            SafeSnap(toy);

            run = null;
            End(toy, ctx);
        }

        void End(PlaygroundToy toy, ToyPlayContext ctx)
        {
            if (current == toy)
            {
                current = null;
                context = null;
            }
            lastEndTime = Time.time;
            toy.RoutineEnded(player != null ? player.transform.position : transform.position);
            Finished?.Invoke(toy);

            if (ctx != null && ctx.Played && !string.IsNullOrEmpty(toy.StickerId) && !ToyStickers.Has(toy.StickerId))
                StartCoroutine(StickerRoutine(toy));
        }

        /// <summary>Parts to rest, kid off the toy (hopping to the exit), pose None.</summary>
        void CleanUp(PlaygroundToy toy, bool hop)
        {
            // Off the toy first (StopRiding unparents him at once), so the parts snapping back do not carry him along.
            if (kid != null) { kid.SetToyPose(ToyPose.None); kid.SpeedOverride = -1f; }
            if (hop && player != null && player.IsRiding && player.isActiveAndEnabled) player.StopRiding(ExitPosition(toy));
            SafeSnap(toy);
        }

        /// <summary>For OnDisable: nothing may start (the object is going away), but the state must be left clean.</summary>
        void CleanUpQuietly()
        {
            if (run != null) StopCoroutine(run);
            run = null;

            PlaygroundToy toy = current;
            current = null;
            context = null;
            CleanUp(toy, true);
        }

        void SafeSnap(PlaygroundToy toy)
        {
            try
            {
                if (toy != null && toy.Routine != null) toy.Routine.SnapToRest();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>Where he lands when the routine ends: the toy's exit point, or the ground under where he is.</summary>
        Vector3 ExitPosition(PlaygroundToy toy)
        {
            Transform exit = toy != null && toy.Routine != null ? toy.Routine.ExitPoint : null;
            if (exit != null) return exit.position;

            Vector3 here = player != null ? player.transform.position : transform.position;
            if (Physics.Raycast(here + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return new Vector3(here.x, 0f, here.z);
        }

        /// <summary>
        /// Runs an enumerator (and the enumerators it yields) and never lets an exception stop the game: it is logged and the routine
        /// simply ends, then the clean-up in Run puts the kid back on the ground.
        /// </summary>
        static IEnumerator SafeRun(IEnumerator root, string label)
        {
            var stack = new Stack<IEnumerator>();
            if (root != null) stack.Push(root);

            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"CoolSchool toys: the routine of {label} failed: {exception}");
                    yield break;
                }

                if (!more)
                {
                    stack.Pop();
                    continue;
                }

                object value = top.Current;
                if (value is IEnumerator nested) stack.Push(nested);
                else yield return value;
            }
        }

        // ---- The sticker ----

        IEnumerator StickerRoutine(PlaygroundToy toy)
        {
            // A moment after he landed, so the cheer is seen first.
            yield return new WaitForSeconds(StickerGapSeconds);

            string id = toy.StickerId;
            StickerBoard board = StickerBoard.Instance;
            if (board != null) board.Hold(id);

            if (!ToyStickers.Give(id))
            {
                if (board != null) board.Release(id);
                yield break;
            }

            Transform where = player != null ? player.transform : transform;
            yield return ToyStickerMoment.Run(toy, where);

            RewardSystem.AddStars(1, where.position, false);

            if (ToyStickers.Count >= ToyStickers.AllIds.Length)
            {
                VoicePlayer.Say("toys.sticker_all");
                RewardSystem.AddStars(3, where.position, false);
            }

            if (board != null)
            {
                if (board.IsOnScreen(id)) yield return board.PlaceSticker(id);
                else board.Release(id);
            }
        }
    }
}
