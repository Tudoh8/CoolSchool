using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A ladder leaning against the outside of the castle's back wall. Goblins start at its foot (hidden behind the wall),
    /// climb up the Shoulder, step onto the wall top (Rim), jump down to the Landing in the courtyard, and run home to
    /// the Landing again when they are sent away. The four points are set by the builder.
    /// </summary>
    public class WallLadder : MonoBehaviour
    {
        [Tooltip("On the ground outside the wall, at the bottom of the ladder.")]
        [SerializeField] Transform foot;
        [Tooltip("On the ladder, as high as the top of the wall.")]
        [SerializeField] Transform shoulder;
        [Tooltip("On top of the wall, where a climber steps off the ladder.")]
        [SerializeField] Transform rim;
        [Tooltip("On the NavMesh inside the courtyard, a little way from the wall.")]
        [SerializeField] Transform landing;

        float nextFreeTime;

        public Vector3 Foot => foot != null ? foot.position : transform.position;
        public Vector3 Shoulder => shoulder != null ? shoulder.position : Foot;
        public Vector3 Rim => rim != null ? rim.position : Shoulder;
        public Vector3 Landing => landing != null ? landing.position : Rim;

        /// <summary>
        /// Books the next free place on the ladder and returns when the climber may start, so two goblins
        /// never climb on top of each other. The next place opens "gap" seconds later.
        /// </summary>
        public float ReserveSlot(float gap)
        {
            float start = Mathf.Max(Time.time, nextFreeTime);
            nextFreeTime = start + gap;
            return start;
        }
    }
}
