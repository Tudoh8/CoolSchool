using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The hand-made soapbox track as a table: what stands where along the street (distance in metres from the start line,
    /// lane 0..2, how many lanes it covers, the race level from which it is there). Deterministic, so a rebuild gives the same
    /// street. Rules (contract Docs/specs/soapbox-race-contract.md, "Track items"): nothing but coins in the first 40 m, nothing
    /// blocking in the last 20 m, no blocking items from 30 m before to 10 m after a gate, none within 15 m after a ramp.
    /// With those the free stretches of the 450 m hold 22 rows 10 m apart, so the rows are spread over the levels: 13 at level 1
    /// (at least 14 m apart), 5 more at level 2, 4 more at level 3 (3 of them with a box stack).
    /// </summary>
    internal static class SoapboxTrackLayout
    {
        internal enum Kind
        {
            Box, Cone, BoxStack, Puddle, Sprinkler,
            Coin, Ramp, Gate,
            Cat, Dog, Hen, Rabbit,
        }

        internal struct Entry
        {
            public float Distance;
            public Kind Kind;
            public int Lane;
            public int Span;
            public int MinLevel;
            /// <summary>Coins: how high above the road the coin floats.</summary>
            public float Height;
            /// <summary>Animals: true when it waits on the west sidewalk (left on screen).</summary>
            public bool West;
            /// <summary>Gates: 0-based index in distance order.</summary>
            public int Index;

            public bool IsObstacle => Kind <= Kind.Sprinkler;
            public bool IsAnimal => Kind >= Kind.Cat;
        }

        internal const float GateLength = 1.5f;
        internal const float RampLength = 2.5f;

        static readonly float[] GateDistances = { 120f, 250f, 370f };
        static readonly int[] GateLevels = { 1, 1, 2 };

        // Windows in which no blocking item may stand: start, gates (-30..+10), ramps (0..+15), the finish run.
        static readonly Vector2[] Forbidden =
        {
            new Vector2(0f, 40f), new Vector2(90f, 130f), new Vector2(220f, 260f), new Vector2(340f, 380f),
            new Vector2(200f, 215f), new Vector2(320f, 335f), new Vector2(430f, 460f),
        };

        static List<Entry> cached;

        internal static List<Entry> Entries
        {
            get
            {
                if (cached == null) cached = Create();
                return cached;
            }
        }

        static Entry Item(float d, Kind kind, int lane, int level, int span = 1, float height = 0f)
        {
            return new Entry { Distance = d, Kind = kind, Lane = lane, Span = span, MinLevel = level, Height = height };
        }

        static List<Entry> Create()
        {
            var list = new List<Entry>();

            // ---- Obstacle rows: level 1 (one item per row, at least 14 m apart) ----
            list.Add(Item(44f, Kind.Box, 2, 1));
            list.Add(Item(64f, Kind.Cone, 0, 1));
            list.Add(Item(84f, Kind.Puddle, 2, 1));
            list.Add(Item(134f, Kind.Box, 0, 1));
            list.Add(Item(154f, Kind.Cone, 2, 1));
            list.Add(Item(168f, Kind.Sprinkler, 0, 1));
            list.Add(Item(188f, Kind.Box, 1, 1));
            list.Add(Item(262f, Kind.Cone, 0, 1));
            list.Add(Item(292f, Kind.Puddle, 2, 1));
            list.Add(Item(312f, Kind.Box, 1, 1));
            list.Add(Item(382f, Kind.Sprinkler, 0, 1));
            list.Add(Item(402f, Kind.Cone, 1, 1));
            list.Add(Item(417f, Kind.Box, 0, 1));

            // ---- Level 2: five more rows, two lanes blocked in some, one puddle across the whole street ----
            list.Add(Item(54f, Kind.Box, 0, 2));
            list.Add(Item(54f, Kind.Cone, 2, 2));
            list.Add(Item(144f, Kind.Puddle, 0, 2, 3));
            list.Add(Item(178f, Kind.Box, 1, 2));
            list.Add(Item(178f, Kind.Cone, 2, 2));
            list.Add(Item(302f, Kind.Sprinkler, 0, 2));
            list.Add(Item(302f, Kind.Box, 2, 2));
            list.Add(Item(392f, Kind.Cone, 0, 2));
            list.Add(Item(392f, Kind.Box, 1, 2));

            // ---- Level 3: four more rows, three with a box stack (not jumpable: steer around it) ----
            list.Add(Item(74f, Kind.BoxStack, 1, 3));
            list.Add(Item(198f, Kind.BoxStack, 0, 3));
            list.Add(Item(198f, Kind.Cone, 2, 3));
            list.Add(Item(272f, Kind.BoxStack, 2, 3));
            list.Add(Item(272f, Kind.Box, 0, 3));
            list.Add(Item(427f, Kind.Box, 1, 3));
            list.Add(Item(427f, Kind.Cone, 0, 3));

            // ---- Ramps with a coin arc each ----
            list.Add(Item(200f, Kind.Ramp, 1, 1));
            list.Add(Item(320f, Kind.Ramp, 2, 2));
            AddRampCoins(list, 200f, 1, 1);
            AddRampCoins(list, 320f, 2, 2);

            // ---- Gates ----
            for (int i = 0; i < GateDistances.Length; i++)
            {
                Entry gate = Item(GateDistances[i], Kind.Gate, 0, GateLevels[i], 3);
                gate.Index = i;
                list.Add(gate);
            }

            // ---- Animals; the sidewalk side alternates ----
            list.Add(new Entry { Distance = 90f, Kind = Kind.Cat, Lane = 1, Span = 1, MinLevel = 1, West = true });
            list.Add(new Entry { Distance = 160f, Kind = Kind.Dog, Lane = 1, Span = 1, MinLevel = 2, West = false });
            list.Add(new Entry { Distance = 285f, Kind = Kind.Hen, Lane = 0, Span = 1, MinLevel = 1, West = true });
            list.Add(new Entry { Distance = 410f, Kind = Kind.Rabbit, Lane = 2, Span = 1, MinLevel = 3, West = false });

            AddCoinLines(list);
            return list;
        }

        /// <summary>Five coins along the arc of the jump: the cart is as high as the arc, and picks the coins that are within a metre of it.</summary>
        static void AddRampCoins(List<Entry> list, float rampDistance, int lane, int level)
        {
            const float launchHeight = 2.6f;
            const float cartBody = 0.6f;
            // The cart takes off about at the ramp and flies for 1.5 s at about 7 m/s; the nose is 0.8 m ahead of the cart.
            float[] fractions = { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f };
            foreach (float t in fractions)
            {
                float d = rampDistance - 1.6f + 10.5f * t + 0.8f;
                float h = launchHeight * 4f * t * (1f - t) + cartBody;
                list.Add(Item(d, Kind.Coin, lane, level, 1, h));
            }
        }

        /// <summary>About 25 lines of 5 coins (1.5 m apart), each in a lane that is free of everything at every level.</summary>
        static void AddCoinLines(List<Entry> list)
        {
            int[] preferred = { 1, 1, 0, 0, 1, 2, 2, 1, 0, 1, 2, 2, 1, 1, 0, 2, 1, 0, 0, 1, 2, 1, 1, 2, 0 };
            List<Entry> obstacles = list.Where(e => e.IsObstacle).ToList();
            List<Entry> animals = list.Where(e => e.IsAnimal).ToList();
            List<Entry> ramps = list.Where(e => e.Kind == Kind.Ramp).ToList();

            for (int i = 0; i < preferred.Length; i++)
            {
                float start = 8f + i * 17.3f;
                float end = start + 6f;
                int[] order = { preferred[i], 1, 0, 2 };
                int chosen = -1;
                foreach (int lane in order)
                {
                    if (LaneFree(lane, start, end, obstacles, animals, ramps)) { chosen = lane; break; }
                }
                if (chosen < 0) continue;

                for (int k = 0; k < 5; k++)
                    list.Add(Item(start + k * 1.5f, Kind.Coin, chosen, 1, 1, 0.6f));
            }
        }

        static bool LaneFree(int lane, float from, float to, List<Entry> obstacles, List<Entry> animals, List<Entry> ramps)
        {
            foreach (Entry o in obstacles)
                if (lane >= o.Lane && lane < o.Lane + o.Span && o.Distance > from - 2f && o.Distance < to + 2.5f) return false;
            foreach (Entry a in animals)
                if (a.Lane == lane && a.Distance > from - 6f && a.Distance < to + 8f) return false;
            foreach (Entry r in ramps)
                if (r.Lane == lane && r.Distance > from - 6f && r.Distance < to + 14f) return false;
            return true;
        }

        /// <summary>Counts of everything that is on the track at a level.</summary>
        internal static void Count(int level, out int obstacleRows, out int obstacles, out int coins, out int ramps, out int gates, out int animals)
        {
            List<Entry> items = Entries.Where(e => e.MinLevel <= level).ToList();
            obstacles = items.Count(e => e.IsObstacle);
            obstacleRows = items.Where(e => e.IsObstacle).Select(e => e.Distance).Distinct().Count();
            coins = items.Count(e => e.Kind == Kind.Coin);
            ramps = items.Count(e => e.Kind == Kind.Ramp);
            gates = items.Count(e => e.Kind == Kind.Gate);
            animals = items.Count(e => e.IsAnimal);
        }

        /// <summary>Checks the contract rules; returns one text per problem (empty when the table is fine).</summary>
        internal static List<string> Validate()
        {
            var problems = new List<string>();
            var rowsByLevel = new Dictionary<int, List<float>>();

            foreach (Entry e in Entries)
            {
                if (!e.IsObstacle) continue;
                foreach (Vector2 window in Forbidden)
                    if (e.Distance >= window.x && e.Distance <= window.y)
                        problems.Add($"{e.Kind} at {e.Distance} (level {e.MinLevel}) is inside the free window {window.x}..{window.y}");
            }

            for (int level = 1; level <= 3; level++)
            {
                List<float> rows = Entries.Where(e => e.IsObstacle && e.MinLevel <= level).Select(e => e.Distance).Distinct().OrderBy(d => d).ToList();
                float gap = level == 1 ? 14f : 10f;
                for (int i = 1; i < rows.Count; i++)
                    if (rows[i] - rows[i - 1] < gap - 0.01f) problems.Add($"level {level}: rows at {rows[i - 1]} and {rows[i]} are less than {gap} m apart");

                foreach (float row in rows)
                {
                    // Never all three lanes blocked by something that cannot be jumped.
                    var blocked = new HashSet<int>();
                    foreach (Entry e in Entries)
                        if (e.IsObstacle && e.MinLevel <= level && Mathf.Abs(e.Distance - row) < 0.01f && e.Kind == Kind.BoxStack)
                            for (int l = e.Lane; l < e.Lane + e.Span; l++) blocked.Add(l);
                    if (blocked.Count >= 3) problems.Add($"level {level}: row {row} cannot be passed");
                }
            }
            return problems;
        }
    }
}
