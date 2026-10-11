using UnityEngine;

namespace CoolSchool.Tag
{
    /// <summary>
    /// Anyone who plays tag: the player or a computer kid.
    /// </summary>
    public class TagParticipant : MonoBehaviour
    {
        [SerializeField] bool isPlayer;
        [Tooltip("How close this kid must get to tag someone. The player gets a generous reach.")]
        [SerializeField] float tagReach = 1f;
        [Tooltip("Height of the kid, used to float the 'it' marker above the head.")]
        [SerializeField] float height = 1.2f;

        public bool IsPlayer => isPlayer;
        public float TagReach => tagReach;
        public float Height => height;
        public bool IsIt { get; private set; }

        /// <summary>While frozen the kid stands still, e.g. counting to three after being tagged.</summary>
        public bool IsFrozen => Time.time < frozenUntil;

        /// <summary>A kid who just tagged someone can't be tagged straight back.</summary>
        public bool CanBeTagged => !IsIt && Time.time >= immuneUntil;

        float frozenUntil;
        float immuneUntil;

        public void BecomeIt(float freezeSeconds)
        {
            IsIt = true;
            frozenUntil = Time.time + freezeSeconds;
        }

        public void StopBeingIt(float immuneSeconds)
        {
            IsIt = false;
            immuneUntil = Time.time + immuneSeconds;
        }

        public void Configure(bool player, float reach, float kidHeight)
        {
            isPlayer = player;
            tagReach = reach;
            height = kidHeight;
        }
    }
}
