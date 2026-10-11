using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>Small easing curves shared by the lesson's bouncy, friendly animations.</summary>
    public static class LessonMotion
    {
        /// <summary>0 to 1 with a little overshoot past 1 on the way, like a toy popping into place.</summary>
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float s = 1.9f;
            return 1f + t * t * ((s + 1f) * t + s);
        }

        /// <summary>0 at both ends and 1 in the middle: a hop or a scale punch.</summary>
        public static float Hump(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        /// <summary>A sideways shake that dies away: it starts strong and ends at 0.</summary>
        public static float DampedWobble(float t, float cycles)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI * 2f * cycles) * (1f - t);
        }
    }
}
