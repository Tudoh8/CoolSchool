using System.Collections;
using CoolSchool.Aliens;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Player;
using CoolSchool.Tag;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// Hands out stars after the games and turns a full star jar into a present.
    /// One of these lives in the scene. Anything can call RewardSystem.AddStars to give stars.
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        [SerializeField] RewardCatalog catalog;
        [Tooltip("How many stars fill the jar. A full jar turns into a present.")]
        [SerializeField] int jarCapacity = 10;
        [SerializeField] StarJarHud hud;
        [SerializeField] PresentPopup popup;

        [Header("Games that give stars")]
        [SerializeField] TagGame tagGame;
        [SerializeField] KnightsGame knightsGame;
        [SerializeField] AlienLanding alienLanding;
        [SerializeField] int starsForTagRound = 3;
        [SerializeField] int starsForKnightsVictory = 5;
        [SerializeField] int starsForAlienVictory = 5;
        [Tooltip("Seconds to wait after a game's own victory moment before the stars appear.")]
        [SerializeField] float rewardDelaySeconds = 1.5f;

        const float StarGapSeconds = 0.15f;
        const float JarFullPauseSeconds = 0.5f;
        const float MaxVoiceWaitSeconds = 4f;
        const float MaxStarWaitSeconds = 5f;
        const float AboveHeadMeters = 1.5f;

        static RewardSystem instance;

        PlayerController player;
        int starsInFlight;
        bool presentRoutineRunning;

        /// <summary>Flies stars from this spot in the world to the star jar, one after the other.</summary>
        public static void AddStars(int count, Vector3 worldPosition, bool announce = true)
        {
            if (instance != null) instance.GiveStars(count, worldPosition, announce);
        }

        void OnEnable()
        {
            instance = this;
            ProgressStore.Changed += RefreshHud;

            if (tagGame != null) tagGame.onRoundFinished.AddListener(OnTagRoundFinished);
            if (knightsGame != null) knightsGame.onVictory.AddListener(OnKnightsVictory);
            if (alienLanding != null) alienLanding.onVictory.AddListener(OnAlienVictory);
            RefreshHud();
        }

        void OnDisable()
        {
            if (instance == this) instance = null;
            ProgressStore.Changed -= RefreshHud;

            if (tagGame != null) tagGame.onRoundFinished.RemoveListener(OnTagRoundFinished);
            if (knightsGame != null) knightsGame.onVictory.RemoveListener(OnKnightsVictory);
            if (alienLanding != null) alienLanding.onVictory.RemoveListener(OnAlienVictory);
        }

        void Start()
        {
            // The jar was already full when the game was closed last time.
            if (ProgressStore.Current.starsInJar >= jarCapacity) StartPresentRoutine();
        }

        void OnTagRoundFinished()
        {
            StartCoroutine(GiveAfterDelay(starsForTagRound));
        }

        void OnKnightsVictory()
        {
            StartCoroutine(GiveAfterDelay(starsForKnightsVictory));
        }

        void OnAlienVictory()
        {
            StartCoroutine(GiveAfterDelay(starsForAlienVictory));
        }

        /// <summary>The reward comes after the game's own cheering, and flies from wherever the player is by then.</summary>
        IEnumerator GiveAfterDelay(int count)
        {
            yield return new WaitForSeconds(rewardDelaySeconds);
            GiveStars(count, PlayerPosition());
        }

        void GiveStars(int count, Vector3 worldPosition, bool announce = true)
        {
            if (count <= 0) return;

            StartCoroutine(FlyStars(count, ScreenPointOf(worldPosition), announce));
        }

        IEnumerator FlyStars(int count, Vector2 screenPoint, bool announce)
        {
            for (int i = 0; i < count; i++)
            {
                starsInFlight++;
                bool first = announce && i == 0;
                if (hud != null) hud.FlyStar(screenPoint, () => OnStarLanded(first));
                else OnStarLanded(first);

                yield return new WaitForSeconds(StarGapSeconds);
            }
        }

        void OnStarLanded(bool firstOfBatch)
        {
            starsInFlight = Mathf.Max(0, starsInFlight - 1);
            ProgressStore.AddStar();

            // Only when nobody is talking: a queued "star" would come out late, unrelated to the stars.
            if (firstOfBatch && !RewardVoice.IsBusy()) RewardVoice.Say("reward.star");
            if (ProgressStore.Current.starsInJar >= jarCapacity) StartPresentRoutine();
        }

        // ---- The full jar becomes a present ----

        void StartPresentRoutine()
        {
            if (presentRoutineRunning) return;

            StartCoroutine(PresentRoutine());
        }

        IEnumerator PresentRoutine()
        {
            presentRoutineRunning = true;

            // Extra stars may have landed while the first present was open, so there can be more than one.
            while (ProgressStore.Current.starsInJar >= jarCapacity)
            {
                yield return WaitForStarsToLand();
                yield return new WaitForSeconds(JarFullPauseSeconds);

                RewardVoice.Say("reward.jar_full");
                Sfx.Play("reward.jar_full");
                yield return WaitForVoice();

                ProgressStore.EmptyJar(jarCapacity);
                RewardItem item = catalog != null ? catalog.NextLocked(ProgressStore.Current) : null;
                yield return ShowPresent(item);
            }

            presentRoutineRunning = false;
        }

        IEnumerator ShowPresent(RewardItem item)
        {
            if (popup == null)
            {
                ProgressStore.OpenPresent(item);
                yield break;
            }

            popup.Show(item);
            while (popup.IsOpen) yield return null;
        }

        IEnumerator WaitForStarsToLand()
        {
            float giveUp = Time.time + MaxStarWaitSeconds;
            while (starsInFlight > 0 && Time.time < giveUp) yield return null;
        }

        /// <summary>Waits for the teacher to finish talking, but never for long.</summary>
        IEnumerator WaitForVoice()
        {
            yield return new WaitForSeconds(0.2f);

            float giveUp = Time.time + MaxVoiceWaitSeconds;
            while (RewardVoice.IsSpeaking() && Time.time < giveUp) yield return null;
        }

        // ---- Helpers ----

        void RefreshHud()
        {
            if (hud != null) hud.Refresh(ProgressStore.Current.starsInJar, jarCapacity);
        }

        Vector3 PlayerPosition()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            return player != null ? player.transform.position : Vector3.zero;
        }

        /// <summary>Where on the screen a point above the given world position is. The middle of the screen if it is out of view.</summary>
        static Vector2 ScreenPointOf(Vector3 worldPosition)
        {
            var middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Camera camera = Camera.main;
            if (camera == null) return middle;

            Vector3 point = camera.WorldToScreenPoint(worldPosition + Vector3.up * AboveHeadMeters);
            return point.z > 0f ? (Vector2)point : middle;
        }
    }
}
