using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Activities
{
    /// <summary>
    /// Keeps track of the one activity that is running. When it ends, the default activity
    /// (free play) starts again so there is always something going on.
    /// </summary>
    public class ActivityManager : MonoBehaviour
    {
        [SerializeField] Activity defaultActivity;

        public static ActivityManager Instance { get; private set; }

        public Activity Current { get; private set; }

        /// <summary>The activity that runs when nothing else does (free play).</summary>
        public Activity DefaultActivity => defaultActivity;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (defaultActivity != null) StartActivity(defaultActivity);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartActivity(Activity activity)
        {
            if (activity == null) return;
            if (Current == activity && activity.IsRunning) return;

            // Stop listening first, so ending the old activity does not restart free play.
            if (Current != null)
            {
                Activity old = Current;
                Current = null;
                old.Ended -= HandleEnded;
                old.End();
            }

            Current = activity;
            activity.Ended += HandleEnded;
            activity.Begin();
            if (activity == defaultActivity) Sfx.PlayMusic("music.playground");
        }

        void HandleEnded(Activity activity)
        {
            activity.Ended -= HandleEnded;
            if (activity != Current) return;

            Current = null;
            if (defaultActivity != null) StartActivity(defaultActivity);
        }
    }
}
