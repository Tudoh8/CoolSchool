using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A rock or a bush a shy alien can hide behind. The hide point is on the north side of the prop (the camera looks
    /// north, so the prop is between the kid and the alien); the peek point is beside it, where the alien can be seen.
    /// One alien at a time may use a cover.
    /// </summary>
    public class AlienCover : MonoBehaviour
    {
        [Tooltip("Behind the prop, out of sight (north side).")]
        [SerializeField] Transform hidePoint;
        [Tooltip("Beside the prop, where the alien steps out to be seen.")]
        [SerializeField] Transform peekPoint;

        public Transform HidePoint => hidePoint != null ? hidePoint : transform;
        public Transform PeekPoint => peekPoint != null ? peekPoint : transform;

        /// <summary>True while a shy alien has claimed this cover.</summary>
        public bool IsTaken { get; private set; }

        /// <summary>Takes the cover. Returns false when somebody else has it.</summary>
        public bool Claim()
        {
            if (IsTaken) return false;

            IsTaken = true;
            return true;
        }

        public void Release()
        {
            IsTaken = false;
        }

        void OnDisable()
        {
            IsTaken = false;
        }
    }
}
