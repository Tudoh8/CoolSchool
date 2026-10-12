using CoolSchool.Activities;
using CoolSchool.Player;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// Once the child has won the toy parrot (plush.parrot) from a present, Lorre sits on his shoulder in free play and
    /// comes along everywhere. During the pirate adventure the adventure uses the parrot itself and this stays out of the
    /// way; when the adventure is over it flies back to the shoulder. Without the toy the parrot is hidden.
    /// Sits on the same object as the ParrotGuide.
    /// </summary>
    public class ShoulderParrot : MonoBehaviour
    {
        enum State { Unknown, Perched, Hidden, Adventure }

        [SerializeField] ParrotGuide parrot;
        [SerializeField] Animator playerAnimator;
        [SerializeField] string unlockId = "plush.parrot";

        // Progress changes (and the activity) are checked a few times per second, which is plenty.
        const float PollSeconds = 0.25f;

        State state = State.Unknown;
        float nextPoll;

        void Awake()
        {
            if (parrot == null) parrot = GetComponent<ParrotGuide>();
        }

        void OnEnable()
        {
            ProgressStore.Changed += OnProgressChanged;
            nextPoll = 0f;
        }

        void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
        }

        void Update()
        {
            if (Time.time < nextPoll) return;

            nextPoll = Time.time + PollSeconds;
            Evaluate();
        }

        void OnProgressChanged()
        {
            Evaluate();
        }

        void Evaluate()
        {
            if (parrot == null) return;

            // The adventure moves the parrot around itself.
            ActivityManager manager = ActivityManager.Instance;
            if (manager != null && manager.Current is PirateAdventure)
            {
                state = State.Adventure;
                return;
            }

            // The adventure just ended: forget where the parrot was left and decide again.
            if (state == State.Adventure) state = State.Unknown;

            bool owned = ProgressStore.Current.IsUnlocked(unlockId);
            if (owned && state != State.Perched)
            {
                Animator kid = FindPlayerAnimator();
                if (kid == null) return;

                parrot.PerchOnShoulder(kid);
                state = State.Perched;
            }
            else if (!owned && state != State.Hidden)
            {
                parrot.Hide();
                state = State.Hidden;
            }
        }

        Animator FindPlayerAnimator()
        {
            if (playerAnimator != null) return playerAnimator;

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerAnimator = player.GetComponentInChildren<Animator>();
            return playerAnimator;
        }
    }
}
