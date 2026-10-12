using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.CameraRig;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The soapbox race ("zeepkistrace"): a lane runner down a street in a cart, against two classmates in their own carts.
    /// He steers by tapping the left or right half of the screen and jumps with the big button. Three arches across the road
    /// (the learning gates) ask for a number or a letter sound and give bonus coins for the right lane. He can never fail: a
    /// crash only wobbles the cart, the rivals are rubber-banded so he usually wins, and on the podium everybody celebrates.
    /// The flow is split over files: this one (set up, flow, putting everything back), SoapboxRace.Race.cs (what happens during
    /// the race), SoapboxRace.Gates.cs (the questions) and SoapboxRace.Finish.cs (finish, podium, going home).
    /// </summary>
    public partial class SoapboxRace : Activity
    {
        [Header("Player")]
        [SerializeField] PlayerController player;
        [SerializeField] KidAnimator playerAnimator;
        [SerializeField] WeaponHolder playerWeapons;
        [SerializeField] RaceHelmet helmet;
        [SerializeField] FloatingJoystick joystick;
        [SerializeField] ActionButton actionButton;
        [SerializeField] FollowCamera followCamera;
        [Tooltip("On the main camera, disabled at the start.")]
        [SerializeField] RaceChaseCamera chaseCamera;

        [Header("Race")]
        [SerializeField] RaceTrack track;
        [SerializeField] CartController playerCart;
        [SerializeField] CartLooks playerLooks;
        [Tooltip("The two classmates.")]
        [SerializeField] RivalCartAI[] rivals;
        [SerializeField] RaceInput input;
        [SerializeField] RaceHud hud;
        [SerializeField] RaceAnnouncer announcer;
        [SerializeField] Transform announcerStartSpot;
        [SerializeField] Transform announcerFinishSpot;
        [Tooltip("3: first (middle, highest), second, third.")]
        [SerializeField] Transform[] podiumSpots;
        [SerializeField] Transform podiumCameraSpot;
        [Tooltip("In the start area, on the NavMesh.")]
        [SerializeField] Transform returnPoint;
        [SerializeField] float gateLeadSeconds = 5f;
        [Tooltip("Optional: found in the scene when empty.")]
        [SerializeField] FinishArch finishArch;

        /// <summary>Raised on the podium: how many stars he earned, and where (for the stars to fly from).</summary>
        public event System.Action<int, Vector3> RaceFinished;

        // The adaptive level: 1 to 3, read when the race starts.
        const string Skill = "race";
        const int MaxLevel = 3;
        static readonly float[] CruiseSpeedByLevel = { 6f, 7f, 8f };
        // How often he wins when the rivals settle at the end.
        static readonly float[] WinChanceByLevel = { 0.9f, 0.8f, 0.7f };
        // The biggest number a gate asks for, by numbers level 1 to 5. Mirrors NumbersLesson.MaxNumberByLevel.
        static readonly int[] MaxNumberByLevel = { 3, 5, 5, 7, 10 };

        const float GateSlowFactor = 0.75f;
        const float CountdownStepSeconds = 1f;
        const float MaxVoiceWaitSeconds = 8f;
        const float ArrowsGoneSeconds = 12f;
        const int ArrowsGoneAfterLaneChanges = 2;
        const float FinalLineDistance = 380f;
        const float RivalTauntDistance = 60f;

        // Safety nets, so the race always ends.
        const float RivalGraceSeconds = 8f;
        const float RaceTimeoutSeconds = 180f;

        enum Phase { Idle, Boarding, Countdown, Racing, Finishing }

        Phase phase = Phase.Idle;
        int level = 1;
        bool quitting;
        Coroutine flowRoutine;

        // What happened in this race.
        float raceStartTime;
        int coins;
        int gatesRight;
        int gatesWrong;
        int laneChanges;
        bool arrowsOn;
        bool announcerAtFinish;
        bool playerWins;
        readonly List<CartController> finishOrder = new List<CartController>();
        CartController[] allCarts;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (playerAnimator == null && player != null) playerAnimator = player.GetComponent<KidAnimator>();
            if (finishArch == null) finishArch = FindAnyObjectByType<FinishArch>(FindObjectsInactive.Include);

            level = Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
            ResetRaceState();
            BuildCartList();
            Subscribe();

            phase = Phase.Boarding;
            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            StopAllCoroutines();
            flowRoutine = null;
            phase = Phase.Idle;

            Unsubscribe();
            if (input != null) input.Active = false;

            RestorePlayer();
            RestoreWorld();
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-race, put everything back first.
            if (IsRunning && !quitting) End();
        }

        void ResetRaceState()
        {
            coins = 0;
            gatesRight = 0;
            gatesWrong = 0;
            laneChanges = 0;
            arrowsOn = false;
            announcerAtFinish = false;
            finishOrder.Clear();
            ResetRaceVoiceState();
        }

        void BuildCartList()
        {
            var carts = new List<CartController>();
            if (playerCart != null) carts.Add(playerCart);
            if (rivals != null)
                foreach (RivalCartAI rival in rivals)
                    if (rival != null && rival.Cart != null) carts.Add(rival.Cart);
            allCarts = carts.ToArray();
        }

        // ----- The flow ----------------------------------------------------------------------------

        IEnumerator FlowRoutine()
        {
            yield return RunBegin();
            yield return RunCountdown();
            yield return RunRace();
            yield return RunFinish();

            flowRoutine = null;
            End();
        }

        /// <summary>The welcome: dress up, climb into the cart, the camera moves behind it, and how to steer.</summary>
        IEnumerator RunBegin()
        {
            Sfx.PlayMusic("music.race");
            if (playerWeapons != null) playerWeapons.ShowWeapons(false);
            if (helmet != null) helmet.Show(true);
            if (player != null) player.JumpEnabled = false;

            PrepareCarts();
            PrepareGates();

            if (announcer != null && player != null) announcer.FaceTowards(player.transform.position);
            VoicePlayer.Say("race.welcome", true);

            // Hop into the cart and sit down.
            if (player != null && playerCart != null && playerCart.Seat != null) player.RideOn(playerCart.Seat);
            yield return new WaitForSeconds(0.55f);
            if (playerAnimator != null) playerAnimator.SetSitting(true);

            if (joystick != null) joystick.gameObject.SetActive(false);
            if (actionButton != null) actionButton.ResetIcon();

            if (followCamera != null) followCamera.enabled = false;
            if (chaseCamera != null)
            {
                chaseCamera.Target = playerCart;
                chaseCamera.enabled = true;
                chaseCamera.BlendIn(1.2f);
            }

            if (hud != null)
            {
                hud.Show(true);
                hud.SetCoins(0, false);
                hud.SetPlace(1);
                hud.SetProgress(0f, 0f, 0f);
                hud.ShowTarget(null);
            }

            yield return WaitForVoice();

            // How to steer: the first time ever, and every time at level 1.
            PlayerProgress progress = ProgressStore.Current;
            if (!progress.raceHowtoHeard || level == 1)
            {
                arrowsOn = true;
                if (hud != null) hud.ShowArrows(true);
                VoicePlayer.Say("race.howto");

                if (!progress.raceHowtoHeard)
                {
                    progress.raceHowtoHeard = true;
                    ProgressStore.Save();
                }
                yield return WaitForVoice();
            }
        }

        /// <summary>Tracks, looks, rivals and the question of who wins: everything decided before the start.</summary>
        void PrepareCarts()
        {
            if (track != null)
            {
                track.PlayerCart = playerCart;
                track.Prepare(level);
            }
            if (allCarts != null)
                foreach (CartController cart in allCarts)
                    if (cart != null) cart.Track = track;

            if (playerCart != null)
            {
                playerCart.IsPlayer = true;
                playerCart.ResetToStart(1);
            }

            // The rivals take the two bodies the player does not use.
            int playerBody = 0;
            if (playerLooks != null)
            {
                playerLooks.ApplyPlayerLooks(ProgressStore.Current);
                playerBody = playerLooks.BodyIndex;
            }

            playerWins = Random.value < WinChanceByLevel[level - 1];
            int nextBody = 0;
            for (int i = 0; rivals != null && i < rivals.Length; i++)
            {
                RivalCartAI rival = rivals[i];
                if (rival == null) continue;

                if (nextBody == playerBody) nextBody++;
                var looks = rival.GetComponent<CartLooks>();
                if (looks != null)
                {
                    looks.SetBody(nextBody);
                    looks.SetExtras(false, false, false);
                }
                nextBody++;

                rival.PlayerCart = playerCart;
                rival.ResetRival(i == 0 ? 0 : 2);
                rival.Prepare(level, playerWins, i);
            }
        }

        IEnumerator RunCountdown()
        {
            phase = Phase.Countdown;

            for (int n = 3; n >= 1; n--)
            {
                if (hud != null) hud.Countdown(n);
                VoicePlayer.Say("race.count" + n, true);
                Sfx.Play("race.countdown_beep");
                yield return new WaitForSeconds(CountdownStepSeconds);
            }

            if (hud != null) hud.Countdown(0);
            VoicePlayer.Say("race.go", true);
            Sfx.Play("race.go_horn");
            if (announcer != null) announcer.WaveFlag();

            // Off they go.
            raceStartTime = Time.time;
            laneChanges = 0;
            if (playerCart != null)
            {
                playerCart.TargetSpeed = CruiseSpeedByLevel[level - 1];
                playerCart.StartRunning();
            }
            if (rivals != null)
                foreach (RivalCartAI rival in rivals)
                    if (rival != null) rival.Begin();
            if (input != null) input.Active = true;
            phase = Phase.Racing;

            // The announcer goes to the finish, out of the picture.
            yield return new WaitForSeconds(1.2f);
            if (announcer != null && announcerFinishSpot != null && phase == Phase.Racing)
            {
                announcer.MoveTo(announcerFinishSpot);
                announcerAtFinish = true;
            }
        }

        // ----- Levels ------------------------------------------------------------------------------

        /// <summary>No wrong gates and few crashes: up. Two or more wrong gates, or many crashes: down. The next race reads it.</summary>
        void AdaptLevel()
        {
            PlayerProgress progress = ProgressStore.Current;
            int current = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);
            int crashes = playerCart != null ? playerCart.Crashes : 0;

            if (gatesWrong == 0 && crashes <= 2 && current < MaxLevel) progress.SetLevel(Skill, current + 1);
            else if ((gatesWrong >= 2 || crashes >= 6) && current > 1) progress.SetLevel(Skill, current - 1);
            ProgressStore.Save();
        }

        // ----- Putting things back -----------------------------------------------------------------

        /// <summary>The player as he was before the race: off the cart, no helmet, walking and jumping again, joystick back.</summary>
        void RestorePlayer()
        {
            if (playerAnimator != null)
            {
                playerAnimator.SetDancing(false);
                playerAnimator.SetSitting(false);
            }

            // The cart goes back to the start first, so a kid still sitting in it hops off there.
            if (playerCart != null) playerCart.ResetToStart(1);

            if (player != null)
            {
                Vector3 home = returnPoint != null ? returnPoint.position : player.transform.position;
                if (player.IsRiding) player.StopRiding(home);
                else if (returnPoint != null && (player.transform.position - home).sqrMagnitude > 2f) TeleportPlayer();

                player.JumpEnabled = true;
                player.InputEnabled = true;
            }

            if (helmet != null) helmet.Show(false);
            if (joystick != null) joystick.gameObject.SetActive(true);
            if (actionButton != null) actionButton.ResetIcon();

            if (chaseCamera != null) chaseCamera.enabled = false;
            if (followCamera != null)
            {
                followCamera.enabled = true;
                followCamera.Snap();
            }
        }

        /// <summary>Carts, rivals, track, HUD and announcer back where they were.</summary>
        void RestoreWorld()
        {
            if (rivals != null)
            {
                for (int i = 0; i < rivals.Length; i++)
                    if (rivals[i] != null) rivals[i].ResetRival(i == 0 ? 0 : 2);
            }
            if (track != null) track.ResetAll();
            if (hud != null) hud.Show(false);

            if (announcer != null && announcerAtFinish && announcerStartSpot != null) announcer.MoveTo(announcerStartSpot);
            announcerAtFinish = false;
        }

        /// <summary>Puts the player on the return point, facing up the street (the character controller is off for the move).</summary>
        void TeleportPlayer()
        {
            if (player == null || returnPoint == null) return;

            Vector3 forward = track != null ? track.Forward : Vector3.forward;
            var controller = player.GetComponent<CharacterController>();
            bool wasOn = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            // Standing still in the animator for the jump, so the move does not look like a sprint.
            if (playerAnimator != null) playerAnimator.ForceIdle = true;
            player.transform.SetPositionAndRotation(returnPoint.position, Quaternion.LookRotation(forward, Vector3.up));
            if (controller != null) controller.enabled = wasOn;

            if (isActiveAndEnabled) StartCoroutine(ReleaseIdleSoon());
            else if (playerAnimator != null) playerAnimator.ForceIdle = false;
        }

        IEnumerator ReleaseIdleSoon()
        {
            yield return null;
            yield return null;
            if (playerAnimator != null && player != null && !player.IsRiding) playerAnimator.ForceIdle = false;
        }

        // ----- Helpers -----------------------------------------------------------------------------

        /// <summary>Waits for whoever is talking (and whoever is waiting to talk) to finish, but never for long.</summary>
        IEnumerator WaitForVoice(float maxSeconds = MaxVoiceWaitSeconds)
        {
            yield return new WaitForSeconds(0.3f);

            float giveUp = Time.time + maxSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < giveUp)
                yield return null;
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
