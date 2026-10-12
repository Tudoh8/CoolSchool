using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// "Pirate cove". Imagination turns the sand pit into a sea. The child is the captain: he steers the playground
    /// ship to an island (the island pulls the ship in, so he can't get it wrong), shoots balloons off the skeleton boat
    /// with the cannon, duels the silly skeletons on the beach (a bump makes them fall apart into giggling bones and
    /// become friends), follows a picture map to the buried treasure, digs, opens the chest's letter lock and counts the
    /// gold. Then everybody dances. He can never fail: every step has a hint and a safety net.
    /// The flow is split over three files: this one (set up, levels, putting everything back), PirateAdventure.Voyage.cs
    /// (sailing, cannon, landing, skeletons) and PirateAdventure.Treasure.cs (map, dig, lock, counting, party).
    /// Everything that was changed is put back in OnEnd, also when the game is ended in the middle of a step.
    /// </summary>
    public partial class PirateAdventure : Activity
    {
        [Header("Player")]
        [SerializeField] PlayerController player;
        [SerializeField] PlayerBattleInput playerInput;
        [SerializeField] ActionButton actionButton;
        [SerializeField] WeaponHolder playerWeapons;
        [SerializeField] PirateCostume costume;
        [SerializeField] Sprite swordIcon;
        [SerializeField] Sprite cannonIcon;
        [SerializeField] Sprite spadeIcon;
        [SerializeField] ImaginationEffect imagination;

        [Header("Cove")]
        [SerializeField] PirateShip ship;
        [SerializeField] ShipCannon cannon;
        [SerializeField] SkeletonBoat skeletonBoat;
        [SerializeField] GameObject sea;
        [SerializeField] GameObject islandBounds;
        [SerializeField] Transform islandLandingPoint;
        [SerializeField] Transform[] crewLandingPoints;
        [SerializeField] Transform[] skeletonLandingPoints;
        [SerializeField] Transform[] landmarks;       // 4
        [SerializeField] Sprite[] landmarkIcons;      // same order as landmarks
        [SerializeField] DigSpot digSpot;
        [SerializeField] TreasureChest chest;
        [SerializeField] TreasureCoinPicker coinPicker;
        [SerializeField] Transform[] partyPoints;     // 12: a front row of 8 on the island beach, camera side, and 4 staggered behind
        [Tooltip("The trophy chest by the dock. Optional: found in the scene when empty.")]
        [SerializeField] TreasureTrophy trophy;

        [Header("Characters")]
        [SerializeField] SkeletonPirateAI[] skeletons; // 9, inactive (the first wave uses the first ones, the second wave the next)
        [SerializeField] KnightAllyAI[] crew;          // 2 pirate kids, inactive
        [SerializeField] ParrotGuide parrot;

        [Header("UI")]
        [SerializeField] TreasureMapUI map;
        [SerializeField] LessonUI lessonUI;
        [SerializeField] LetterSet letters;

        [Header("Timing and tuning")]
        [Tooltip("The ship stops for the cannon when it has sailed this far (0 = dock, 1 = island).")]
        [SerializeField, Range(0.1f, 0.9f)] float cannonAtProgress = 0.4f;
        [SerializeField] float cannonHintSeconds = 6f;
        [SerializeField] float fightHintSeconds = 5f;
        [SerializeField] float landmarkReachMeters = 2.2f;
        [SerializeField] float partySeconds = 4f;

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent onVictory;

        /// <summary>Raised when the child has counted the gold: how many coins, and where the chest is. Gives that many stars.</summary>
        public event System.Action<int, Vector3> TreasureCounted;

        // The adaptive level: 1 to 3, read when the round starts.
        const string Skill = "pirates";
        const int MaxLevel = 3;
        // Two waves of skeletons on the beach: the first rides the boat, the second jumps off the boat one by one.
        static readonly int[] WaveOneByLevel = { 3, 3, 4 };
        static readonly int[] WaveTwoByLevel = { 3, 4, 5 };
        // Wave 2 lands on the landing points after the ones wave 1 can use (wave 1 never has more than 4).
        const int WaveTwoFirstLandingPoint = 4;
        static readonly int[] HitsByLevel = { 1, 1, 2 };
        static readonly int[] BalloonsByLevel = { 3, 5, 6 };
        static readonly int[] LandmarksByLevel = { 2, 3, 3 };
        static readonly int[] LetterCardsByLevel = { 2, 3, 3 };
        static readonly int[] CoinsMinByLevel = { 3, 4, 6 };
        static readonly int[] CoinsMaxByLevel = { 5, 7, 10 };
        static readonly int[] NumberCardsByLevel = { 2, 3, 3 };

        const float MaxVoiceWaitSeconds = 8f;
        const float TrailSpacing = 1.5f;
        const float TrailRefreshSeconds = 0.7f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;
        const float PlayerWalkSpeed = 3.2f;
        // Safety nets, so the kid is never stuck.
        const float CannonTimeoutSeconds = 100f;
        const float FightTimeoutSeconds = 150f;
        const float SailTimeoutSeconds = 150f;

        // What the child has done this round.
        int level = 1;
        int mistakes;
        bool treasureCounted;
        bool quitting;
        bool hudHidden;
        bool playerDancing;
        Vector3 playerStartPosition;
        Coroutine flowRoutine;
        float lastActionTime;
        char lastLockLetter;

        // The skeletons and crew that are in this round.
        readonly List<SkeletonPirateAI> roundSkeletons = new List<SkeletonPirateAI>();
        Transform[] crewParents;
        bool[] crewMounted;

        // Flags the flow waits for.
        bool shipArrived;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (trophy == null) trophy = FindAnyObjectByType<TreasureTrophy>(FindObjectsInactive.Include);

            level = Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
            mistakes = 0;
            treasureCounted = false;
            shipArrived = false;
            roundSkeletons.Clear();
            lastActionTime = Time.time;

            if (player != null)
            {
                playerStartPosition = player.transform.position;
                player.JumpEnabled = false;

                var playerHittable = player.GetComponent<Hittable>();
                if (playerHittable != null) playerHittable.ResetState();
            }

            ResetWorld();

            if (ship != null) ship.ArrivedAtIsland += OnShipArrived;
            if (actionButton != null) actionButton.Pressed += OnActionPressed;

            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            StopAllCoroutines();
            flowRoutine = null;

            if (ship != null) ship.ArrivedAtIsland -= OnShipArrived;
            if (cannon != null) cannon.Popped -= OnBalloonPopped;
            if (digSpot != null) digSpot.Dug -= OnDug;
            if (coinPicker != null) coinPicker.Picked -= OnCoinPicked;
            if (actionButton != null) actionButton.Pressed -= OnActionPressed;

            if (cannon != null) cannon.Armed = false;
            if (digSpot != null) digSpot.Armed = false;
            if (coinPicker != null) coinPicker.Armed = false;
            if (lessonUI != null)
            {
                lessonUI.HideCards();
                if (hudHidden) lessonUI.SetGameplayHudVisible(true);
            }
            hudHidden = false;

            RestorePlayer();
            ResetWorld();

            if (imagination != null) imagination.Play(false);
            AdaptLevel();

            // The parrot goes back to where it lives: ShoulderParrot puts it on the shoulder or hides it.
            if (parrot != null && parrot.GetComponent<ShoulderParrot>() == null) parrot.Hide();
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-game, put everything back first.
            if (IsRunning && !quitting) End();
        }

        // ----- The flow of the game ----------------------------------------------------------------

        IEnumerator FlowRoutine()
        {
            yield return RunBegin();
            yield return RunSail();
            yield return RunCannon();
            yield return RunArrive();
            yield return RunSkeletons();
            yield return RunMapRoute();
            yield return RunDig();
            yield return RunLetterLock();
            yield return RunCountTreasure();
            yield return RunParty();

            flowRoutine = null;
            End();
        }

        // ----- Levels ------------------------------------------------------------------------------

        static int ByLevel(int[] table, int level)
        {
            return table[Mathf.Clamp(level, 1, table.Length) - 1];
        }

        /// <summary>Up when the whole round had no mistakes, down after two or more. The next round reads the new level.</summary>
        void AdaptLevel()
        {
            if (!treasureCounted) return;

            PlayerProgress progress = ProgressStore.Current;
            int current = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);
            if (mistakes == 0 && current < MaxLevel) progress.SetLevel(Skill, current + 1);
            else if (mistakes >= 2 && current > 1) progress.SetLevel(Skill, current - 1);
            ProgressStore.Save();
        }

        // ----- Putting things back -----------------------------------------------------------------

        /// <summary>The player as he was before the game: no costume, no sword, jumping again, off the ship.</summary>
        void RestorePlayer()
        {
            if (player != null)
            {
                var kid = player.GetComponent<KidAnimator>();
                if (kid != null && playerDancing) kid.SetDancing(false);
                playerDancing = false;

                // Ended at sea: he hops back onto the dock.
                if (player.IsRiding) player.StopRiding(playerStartPosition);

                player.JumpEnabled = true;
                player.InputEnabled = true;
            }

            if (playerInput != null) playerInput.enabled = false;
            if (actionButton != null) actionButton.ResetIcon();
            if (playerWeapons != null) playerWeapons.ShowWeapons(false);
            if (costume != null) costume.Show(false);
        }

        /// <summary>
        /// Everything in the cove back to the start: sea and island walls off, ship at the dock, the skeleton boat and the
        /// skeletons put away, the crew gone, the map, the dig spot and the chest reset. Safe to call at any time.
        /// </summary>
        void ResetWorld()
        {
            if (map != null) map.Hide();
            if (digSpot != null) digSpot.ResetSpot();
            if (coinPicker != null)
            {
                coinPicker.Armed = false;
                coinPicker.SetCoins(null);
            }
            if (chest != null) chest.ResetChest();
            if (cannon != null) cannon.Armed = false;

            ResetCrew();
            if (skeletons != null)
                foreach (SkeletonPirateAI skeleton in skeletons)
                    if (skeleton != null) skeleton.ResetSkeleton();
            if (skeletonBoat != null) skeletonBoat.ResetBoat();
            if (ship != null) ship.ResetToDock();

            if (sea != null) sea.SetActive(false);
            if (islandBounds != null) islandBounds.SetActive(false);
        }

        void ResetCrew()
        {
            if (crew == null) return;

            EnsureCrewArrays();
            for (int i = 0; i < crew.Length; i++)
            {
                KnightAllyAI member = crew[i];
                if (member == null) continue;

                if (crewMounted[i])
                {
                    SeatUtil.Dismount(member.transform, crewParents[i]);
                    crewMounted[i] = false;
                }

                var kid = member.GetComponent<KidAnimator>();
                if (kid != null)
                {
                    kid.ForceIdle = false;
                    kid.SetDancing(false);
                }

                bool wasThere = member.gameObject.activeSelf;
                if (wasThere) BattleEffects.Sparkle(member.transform.position + Vector3.up * 1f);

                member.enabled = false;
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                var weapons = member.GetComponent<WeaponHolder>();
                if (weapons != null) weapons.ShowWeapons(false);
                var hittable = member.GetComponent<Hittable>();
                if (hittable != null) hittable.ResetState();
                member.gameObject.SetActive(false);
            }
        }

        void EnsureCrewArrays()
        {
            int count = crew != null ? crew.Length : 0;
            if (crewParents != null && crewParents.Length == count) return;

            crewParents = new Transform[count];
            crewMounted = new bool[count];
            for (int i = 0; i < count; i++)
                if (crew[i] != null) crewParents[i] = crew[i].transform.parent;
        }

        // ----- Events ------------------------------------------------------------------------------

        void OnShipArrived()
        {
            shipArrived = true;
        }

        /// <summary>Remembers the last tap, so the hints only come when he is stuck.</summary>
        void OnActionPressed()
        {
            lastActionTime = Time.time;
        }

        // ----- Helpers -----------------------------------------------------------------------------

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
        }

        Vector3 ShipPosition()
        {
            return ship != null ? ship.transform.position : PlayerPosition();
        }

        /// <summary>Waits for whoever is talking (and whoever is waiting to talk) to finish, but never for long.</summary>
        IEnumerator WaitForVoice(float maxSeconds = MaxVoiceWaitSeconds)
        {
            yield return new WaitForSeconds(0.3f);

            float giveUp = Time.time + maxSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < giveUp)
                yield return null;
        }

        void ShowIcon(Sprite icon)
        {
            if (actionButton != null) actionButton.SetIcon(icon);
        }

        void ClearIcon()
        {
            if (actionButton != null) actionButton.ResetIcon();
        }

        void SetHudVisible(bool visible)
        {
            if (lessonUI == null) return;

            lessonUI.SetGameplayHudVisible(visible);
            hudHidden = !visible;
        }

        static void Cheer(Component who)
        {
            if (who == null) return;

            var kid = who.GetComponent<KidAnimator>();
            if (kid != null) kid.PlayCheer();
        }

        void CheerEveryone()
        {
            Cheer(player);
            if (crew != null)
                foreach (KnightAllyAI member in crew)
                    if (member != null && member.gameObject.activeInHierarchy) Cheer(member);
            foreach (SkeletonPirateAI skeleton in roundSkeletons)
                if (skeleton != null && skeleton.gameObject.activeInHierarchy) Cheer(skeleton);
        }

        /// <summary>Sparkles every few metres along the straight line from the player toward the target (the same trail the inviter uses).</summary>
        void ShowTrailTo(Vector3 target, float stopRadius)
        {
            if (player == null) return;

            Vector3 start = player.transform.position;
            Vector3 toTarget = target - start;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < stopRadius + 1f) return;

            Vector3 direction = toTarget / distance;
            float end = Mathf.Max(0f, distance - stopRadius);
            float along = 1f;
            for (int i = 0; i < MaxTrailPoints && along <= end; i++)
            {
                BattleEffects.Sparkle(start + direction * along + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
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
