using System;
using UnityEngine;

namespace CoolSchool.Activities
{
    /// <summary>
    /// Something the class can be doing right now: free play, a game of tag, knights and the dragon.
    /// Only one activity runs at a time; the ActivityManager starts and ends them.
    /// </summary>
    public abstract class Activity : MonoBehaviour
    {
        public bool IsRunning { get; private set; }

        /// <summary>Raised after the activity has cleaned up, so the manager can go back to free play.</summary>
        public event Action<Activity> Ended;

        [Header("Hooks for voice lines and sounds")]
        public UnityEngine.Events.UnityEvent onBegan;
        public UnityEngine.Events.UnityEvent onEnded;

        public void Begin()
        {
            if (IsRunning) return;

            IsRunning = true;
            OnBegin();

            // OnBegin may have ended the activity again (e.g. not enough players).
            if (IsRunning) onBegan?.Invoke();
        }

        public void End()
        {
            if (!IsRunning) return;

            IsRunning = false;
            OnEnd();
            onEnded?.Invoke();
            Ended?.Invoke(this);
        }

        protected abstract void OnBegin();
        protected abstract void OnEnd();
    }
}
