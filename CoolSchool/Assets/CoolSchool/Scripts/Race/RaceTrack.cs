using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The soapbox race track: a straight street with three lanes. It turns "metres along the street plus a sideways offset"
    /// into world positions (always go through <see cref="PointAt"/>, so the street could curve one day), collects every
    /// <see cref="RaceTrackItem"/> on it, switches the items on for the race level and tells each item, once per cart, when a
    /// cart reaches it. The carts call <see cref="Sweep"/> every frame; the track never moves a cart itself.
    /// </summary>
    public class RaceTrack : MonoBehaviour
    {
        /// <summary>Half the length of a cart: its nose is this far ahead of its Distance.</summary>
        public const float CartHalfLength = 0.8f;

        /// <summary>Half the width of a cart, for touching items sideways.</summary>
        public const float CartHalfWidth = 0.3f;

        [Tooltip("Lane 1 at distance 0. The track runs along +Z from here.")]
        [SerializeField] Transform startLine;
        [SerializeField] float laneWidth = 2.2f;
        [SerializeField] float finishDistance = 450f;
        [Tooltip("All the track items are children of this (at any depth).")]
        [SerializeField] Transform itemsRoot;

        /// <summary>What the track remembers about a cart: how many items it already passed.</summary>
        sealed class CartState
        {
            public int cursor;
            public float lastTo;
        }

        readonly List<RaceTrackItem> allItems = new List<RaceTrackItem>();
        readonly List<RaceTrackItem> triggers = new List<RaceTrackItem>();   // active items, by TriggerAt
        readonly List<RaceTrackItem> blockers = new List<RaceTrackItem>();   // active items that may block, by Start
        readonly List<LearningGate> gates = new List<LearningGate>();
        readonly Dictionary<CartController, CartState> states = new Dictionary<CartController, CartState>();
        readonly List<CartController> carts = new List<CartController>();
        float longestBlocker;

        public float LaneWidth => laneWidth;
        public float FinishDistance => finishDistance;

        /// <summary>The direction of the track at the start line (today: straight north).</summary>
        public Vector3 Forward => Vector3.forward;

        /// <summary>The direction to the right of <see cref="Forward"/> (lane 2 side).</summary>
        public Vector3 Right => Vector3.right;

        /// <summary>The player's cart (set by the race at runtime). Crossing animals wake up for it.</summary>
        public CartController PlayerCart { get; set; }

        /// <summary>The active learning gates, by distance.</summary>
        public IReadOnlyList<LearningGate> Gates => gates;

        /// <summary>The level the track was prepared for.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Every cart that has driven on the track since the last Prepare / ResetAll (the animals watch these).</summary>
        public IReadOnlyList<CartController> Carts => carts;

        /// <summary>Obstacles that bonked, splashed or braked a cart (any cart: check <c>cart.IsPlayer</c>).</summary>
        public event Action<RaceTrackItem, CartController> ItemHit;

        /// <summary>An animal starts walking into the road.</summary>
        public event Action<AnimalCrosser> AnimalCrossing;

        public event Action<RaceRamp, CartController> RampTaken;

        /// <summary>The player picked up a coin.</summary>
        public event Action<RaceCoin, CartController> CoinCollected;

        Vector3 Origin => startLine != null ? startLine.position : transform.position;

        /// <summary>The world position at a distance along the track and a sideways offset (0 = the middle lane's centre).</summary>
        public Vector3 PointAt(float distance, float xOffset)
        {
            return Origin + Forward * distance + Right * xOffset;
        }

        /// <summary>The sideways offset of a lane's centre.</summary>
        public float LaneOffset(int lane)
        {
            return (lane - 1) * laneWidth;
        }

        /// <summary>How far along the track a world point is.</summary>
        public float DistanceOf(Vector3 world)
        {
            return Vector3.Dot(world - Origin, Forward);
        }

        /// <summary>The sideways offset of a world point (0 = the middle lane's centre).</summary>
        public float LateralOf(Vector3 world)
        {
            return Vector3.Dot(world - Origin, Right);
        }

        /// <summary>The lane closest to a sideways offset.</summary>
        public int NearestLane(float lateral)
        {
            return Mathf.Clamp(Mathf.RoundToInt(lateral / Mathf.Max(0.01f, laneWidth)) + 1, 0, 2);
        }

        // ----- Preparing ----------------------------------------------------------------------------

        /// <summary>
        /// Finds every item, switches on those for the level, works out where each one is and puts everything back as it
        /// was before a race.
        /// </summary>
        public void Prepare(int level)
        {
            Level = Mathf.Max(1, level);
            ForgetCarts();

            allItems.Clear();
            (itemsRoot != null ? itemsRoot : transform).GetComponentsInChildren(true, allItems);

            triggers.Clear();
            blockers.Clear();
            gates.Clear();
            longestBlocker = 0f;

            foreach (RaceTrackItem item in allItems)
            {
                if (item == null) continue;

                bool on = item.MinLevel <= Level;
                if (item.gameObject.activeSelf != on) item.gameObject.SetActive(on);

                item.AttachTo(this);
                item.ResetItem();
                item.UpdateDistance();
                if (!on) continue;

                triggers.Add(item);
                if (item.MayBlock)
                {
                    blockers.Add(item);
                    longestBlocker = Mathf.Max(longestBlocker, item.Length);
                }
                if (item is LearningGate gate) gates.Add(gate);
            }

            triggers.Sort((a, b) => a.TriggerAt.CompareTo(b.TriggerAt));
            blockers.Sort((a, b) => a.Start.CompareTo(b.Start));
            gates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        /// <summary>Puts every item back as it was and forgets which carts passed what.</summary>
        public void ResetAll()
        {
            ForgetCarts();
            foreach (RaceTrackItem item in allItems)
                if (item != null) item.ResetItem();
        }

        void ForgetCarts()
        {
            states.Clear();
            carts.Clear();
        }

        // ----- Carts driving by -------------------------------------------------------------------

        /// <summary>
        /// Called by every cart every frame after it moved. Tells each item the cart's nose just reached, exactly once per
        /// cart (a cursor per cart walks along the sorted items).
        /// </summary>
        public void Sweep(CartController cart, float fromDistance, float toDistance)
        {
            if (cart == null) return;

            if (!states.TryGetValue(cart, out CartState state))
            {
                state = new CartState { cursor = StartCursor(fromDistance), lastTo = fromDistance };
                states.Add(cart, state);
                carts.Add(cart);
            }
            else if (fromDistance < state.lastTo - 0.5f)
            {
                // The cart was put back (a new race): start again from where it is.
                state.cursor = StartCursor(fromDistance);
            }
            state.lastTo = toDistance;

            float nose = toDistance + CartHalfLength;
            while (state.cursor < triggers.Count && triggers[state.cursor].TriggerAt <= nose)
            {
                RaceTrackItem item = triggers[state.cursor++];
                if (item != null) item.OnCartPassing(cart, this);
            }
        }

        /// <summary>A cart at the very start has passed nothing yet; one that appears further on skips what is behind it.</summary>
        int StartCursor(float fromDistance)
        {
            return fromDistance < 1f ? 0 : FirstTriggerAfter(fromDistance + CartHalfLength);
        }

        /// <summary>The index of the first item whose trigger point is beyond the distance.</summary>
        int FirstTriggerAfter(float distance)
        {
            int low = 0;
            int high = triggers.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (triggers[middle].TriggerAt <= distance) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        /// <summary>
        /// The nearest item in the way of a cart in the lane between the two distances (for the rivals to look ahead).
        /// Only items that are blocking right now count: a box that already tumbled away is not in the way.
        /// </summary>
        public bool Blocks(int laneIndex, float fromDistance, float toDistance, out RaceTrackItem item)
        {
            item = null;

            // Items are sorted by their start: skip those that began too far back to reach fromDistance.
            float earliest = fromDistance - longestBlocker;
            int low = 0;
            int high = blockers.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (blockers[middle].Start < earliest) low = middle + 1;
                else high = middle;
            }

            for (int i = low; i < blockers.Count; i++)
            {
                RaceTrackItem candidate = blockers[i];
                if (candidate == null) continue;
                if (candidate.Start > toDistance) break;
                if (candidate.End < fromDistance) continue;
                if (!candidate.Blocking || !candidate.Covers(laneIndex)) continue;

                if (item == null || candidate.Start < item.Start) item = candidate;
            }
            return item != null;
        }

        // ----- Events, raised by the items --------------------------------------------------------------

        internal void RaiseItemHit(RaceTrackItem item, CartController cart) => ItemHit?.Invoke(item, cart);
        internal void RaiseAnimalCrossing(AnimalCrosser animal) => AnimalCrossing?.Invoke(animal);
        internal void RaiseRampTaken(RaceRamp ramp, CartController cart) => RampTaken?.Invoke(ramp, cart);
        internal void RaiseCoinCollected(RaceCoin coin, CartController cart) => CoinCollected?.Invoke(coin, cart);
    }
}
