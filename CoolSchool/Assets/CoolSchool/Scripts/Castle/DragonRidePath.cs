using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The fixed flight path of the dragon ride: a smooth curve (Catmull-Rom) through the points, from the landing spot up over the
    /// back wall, round the castle and back down. Evaluate(t) gives the same speed all along the curve (t is the fraction of its
    /// length). The golden stars float along the path; they are only shown during the ride.
    /// </summary>
    public class DragonRidePath : MonoBehaviour
    {
        [Tooltip("10 to 12 points: from the landing spot, up, round the castle and back to the landing spot.")]
        [SerializeField] Transform[] points;
        [Tooltip("About 8 stars, 1 m above the path.")]
        [SerializeField] RideStar[] stars;

        const int SamplesPerSegment = 24;

        readonly List<Vector3> samples = new List<Vector3>();
        readonly List<float> distances = new List<float>();
        bool built;

        /// <summary>The length of the path in metres.</summary>
        public float Length
        {
            get
            {
                EnsureBuilt();
                return distances.Count > 0 ? distances[distances.Count - 1] : 0f;
            }
        }

        public IReadOnlyList<RideStar> Stars => stars;

        /// <summary>The point at this fraction (0 to 1) of the path's length.</summary>
        public Vector3 Evaluate(float t)
        {
            EnsureBuilt();
            if (samples.Count == 0) return transform.position;
            if (samples.Count == 1) return samples[0];

            float target = Mathf.Clamp01(t) * distances[distances.Count - 1];

            // Binary search for the sample just before the wanted distance.
            int low = 0;
            int high = distances.Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (distances[middle] <= target) low = middle;
                else high = middle;
            }

            float span = distances[high] - distances[low];
            float k = span > 0.0001f ? (target - distances[low]) / span : 0f;
            return Vector3.Lerp(samples[low], samples[high], k);
        }

        /// <summary>Shows every star, fresh (the ride starts).</summary>
        public void ShowStars()
        {
            if (stars == null) return;

            foreach (RideStar star in stars)
                if (star != null) star.Show();
        }

        /// <summary>Hides the stars that are left (the ride is over).</summary>
        public void HideStars()
        {
            if (stars == null) return;

            foreach (RideStar star in stars)
                if (star != null && !star.Caught) star.gameObject.SetActive(false);
        }

        /// <summary>All stars uncaught, in place and hidden.</summary>
        public void ResetStars()
        {
            if (stars == null) return;

            foreach (RideStar star in stars)
                if (star != null) star.ResetStar();
        }

        /// <summary>Works the curve out again (if the points were moved).</summary>
        public void Rebuild()
        {
            built = false;
            EnsureBuilt();
        }

        void EnsureBuilt()
        {
            if (built) return;

            built = true;
            samples.Clear();
            distances.Clear();
            if (points == null) return;

            var control = new List<Vector3>();
            foreach (Transform point in points)
                if (point != null) control.Add(point.position);
            if (control.Count < 2)
            {
                if (control.Count == 1)
                {
                    samples.Add(control[0]);
                    distances.Add(0f);
                }
                return;
            }

            // The first and last points are repeated, so the curve starts and ends exactly on them.
            for (int i = 0; i < control.Count - 1; i++)
            {
                Vector3 p0 = control[Mathf.Max(i - 1, 0)];
                Vector3 p1 = control[i];
                Vector3 p2 = control[i + 1];
                Vector3 p3 = control[Mathf.Min(i + 2, control.Count - 1)];

                for (int step = 0; step < SamplesPerSegment; step++)
                    Add(CatmullRom(p0, p1, p2, p3, step / (float)SamplesPerSegment));
            }
            Add(control[control.Count - 1]);
        }

        void Add(Vector3 point)
        {
            float travelled = samples.Count == 0 ? 0f : distances[distances.Count - 1] + Vector3.Distance(samples[samples.Count - 1], point);
            samples.Add(point);
            distances.Add(travelled);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
