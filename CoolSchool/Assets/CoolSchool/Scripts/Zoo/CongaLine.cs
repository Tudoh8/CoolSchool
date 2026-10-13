using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// The found animals follow the leader (the player, or the keeper during his hiding turn) in a loose line, like a conga. The
    /// leader's path is recorded as breadcrumbs, one every 0.25 m, and animal number i walks to the point of that path which is
    /// 1.4 m plus the lengths of the animals in front of it behind the leader. Far behind they run; when the leader stands still
    /// they stop where they are. The animals face the way they walk.
    /// </summary>
    public class CongaLine : MonoBehaviour
    {
        const float CrumbSpacing = 0.25f;
        const int MaxCrumbs = 400;
        const float FirstGap = 1.4f;
        const float WalkSpeed = 2.4f;
        const float RunSpeed = 4.2f;
        const float RunDistance = 2.5f;
        const float ArriveDistance = 0.25f;

        readonly List<ZooAnimal> members = new List<ZooAnimal>();
        // The path, oldest first. The newest crumb is at the end.
        readonly List<Vector3> crumbs = new List<Vector3>();
        Transform leader;

        public IReadOnlyList<ZooAnimal> Members => members;

        /// <summary>While true the animals stand still; the game moves them itself (around the counting keeper, say).</summary>
        public bool Paused { get; set; }

        /// <summary>Who the animals follow. Changing it starts a new path.</summary>
        public Transform Leader
        {
            get => leader;
            set
            {
                if (leader == value) return;

                leader = value;
                crumbs.Clear();
                if (leader != null) crumbs.Add(leader.position);
            }
        }

        public void Add(ZooAnimal animal)
        {
            if (animal != null && !members.Contains(animal)) members.Add(animal);
        }

        public void Remove(ZooAnimal animal)
        {
            if (animal == null || !members.Remove(animal)) return;

            animal.SetMoving(false, false);
        }

        /// <summary>Everybody stops following (the animals stay where they are).</summary>
        public void Clear()
        {
            foreach (ZooAnimal animal in members)
                if (animal != null) animal.SetMoving(false, false);
            members.Clear();
            crumbs.Clear();
            if (leader != null) crumbs.Add(leader.position);
            Paused = false;
        }

        void Update()
        {
            if (leader == null) return;

            RecordCrumb();
            if (Paused)
            {
                // The game moves the animals itself now (WalkTo sets the walk again); the rest stand idle.
                foreach (ZooAnimal member in members)
                    if (member != null) member.SetMoving(false, false);
                return;
            }
            if (members.Count == 0) return;

            float gap = FirstGap;
            for (int i = 0; i < members.Count; i++)
            {
                ZooAnimal animal = members[i];
                if (animal == null) continue;

                // Each animal keeps half of its own length and half of the one in front of it between them.
                gap += animal.Length * 0.5f;
                Vector3 target = PointBehind(gap);
                gap += animal.Length * 0.5f;

                Vector3 flat = target - animal.transform.position;
                flat.y = 0f;
                float distance = flat.magnitude;
                if (distance <= ArriveDistance)
                {
                    animal.SetMoving(false, false);
                    continue;
                }
                animal.WalkTo(target, distance > RunDistance ? RunSpeed : WalkSpeed);
            }
        }

        void RecordCrumb()
        {
            Vector3 position = leader.position;
            if (crumbs.Count == 0)
            {
                crumbs.Add(position);
                return;
            }

            Vector3 last = crumbs[crumbs.Count - 1];
            Vector3 flat = position - last;
            flat.y = 0f;
            if (flat.magnitude < CrumbSpacing) return;

            crumbs.Add(position);
            if (crumbs.Count > MaxCrumbs) crumbs.RemoveRange(0, crumbs.Count - MaxCrumbs);
        }

        /// <summary>The point on the leader's path that is this far behind the leader. Past the start of the path it goes straight back.</summary>
        Vector3 PointBehind(float distance)
        {
            Vector3 from = leader.position;
            float remaining = distance;
            for (int i = crumbs.Count - 1; i >= 0; i--)
            {
                Vector3 crumb = crumbs[i];
                Vector3 step = crumb - from;
                step.y = 0f;
                float length = step.magnitude;
                if (length >= remaining && length > 0.0001f) return from + step / length * remaining + Vector3.up * (crumb.y - from.y);

                remaining -= length;
                from = crumb;
            }

            // The path is shorter than the line: the rest of the line trails straight behind the start of the path.
            Vector3 back = -leader.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            return from + back.normalized * remaining;
        }
    }
}
