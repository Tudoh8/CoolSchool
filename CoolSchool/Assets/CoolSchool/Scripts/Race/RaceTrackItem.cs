using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// Anything that sits on the race track and reacts when a cart drives by: obstacles, coins, ramps, crossing animals and
    /// learning gates. The <see cref="RaceTrack"/> collects them, works out where along the track each one is (from its
    /// position) and tells it, once per cart, when that cart's nose reaches it.
    /// An item is centred on its position: it covers <see cref="Start"/> to <see cref="End"/> along the track and
    /// <see cref="Lane"/> to <c>Lane + LaneSpan - 1</c> sideways.
    /// </summary>
    public abstract class RaceTrackItem : MonoBehaviour
    {
        [Tooltip("The first lane this item covers (0 = left on screen, 2 = right).")]
        [SerializeField] protected int lane = 1;
        [Tooltip("How many lanes it covers, 1 to 3.")]
        [SerializeField] protected int laneSpan = 1;
        [Tooltip("How long it is along the track, in metres.")]
        [SerializeField] protected float length = 1f;
        [Tooltip("The race level (1..3) from which this item is on the track.")]
        [SerializeField] protected int minLevel = 1;
        [Tooltip("A cart higher than this passes over it.")]
        [SerializeField] protected float clearance = 0.8f;

        /// <summary>Metres along the track (the middle of the item). Set by the track in Prepare.</summary>
        public float Distance { get; private set; }

        /// <summary>The track this item belongs to (set in Prepare).</summary>
        public RaceTrack Track { get; private set; }

        public int MinLevel => minLevel;

        /// <summary>The first lane it covers.</summary>
        public int Lane => lane;

        public int LaneSpan => laneSpan;

        public float Length => length;

        /// <summary>Where the item begins along the track.</summary>
        public float Start => Distance - length * 0.5f;

        /// <summary>Where the item ends along the track.</summary>
        public float End => Distance + length * 0.5f;

        /// <summary>The cart's nose position at which <see cref="OnCartPassing"/> is called (the start of the item by default).</summary>
        public virtual float TriggerAt => Start;

        /// <summary>A cart higher than this (above the road) passes over the item.</summary>
        public virtual float Clearance => clearance;

        /// <summary>True when the item is in the way of a cart in a covered lane (false for coins, gates and ramps).</summary>
        public virtual bool Blocking => true;

        /// <summary>True when jumping over it works.</summary>
        public virtual bool Jumpable => Clearance < 1.2f;

        /// <summary>True when the item can ever be in the way (the rival AI keeps these in view even while they are not blocking yet).</summary>
        public virtual bool MayBlock => Blocking;

        /// <summary>Half the width of the thing itself in a lane, sideways. A cart overlapping this much is touched.</summary>
        protected virtual float LateralHalfWidth => 0.45f;

        /// <summary>The place used to find the item's distance along the track (its position by default).</summary>
        protected virtual Vector3 AnchorPosition => transform.position;

        /// <summary>True when the item covers the lane.</summary>
        public virtual bool Covers(int laneIndex)
        {
            return laneIndex >= lane && laneIndex < lane + laneSpan;
        }

        /// <summary>
        /// True when the cart's body overlaps the item sideways, with the cart where it really is right now (also halfway
        /// through a lane change).
        /// </summary>
        public bool Touches(CartController cart)
        {
            if (cart == null || Track == null) return false;

            float first = Track.LaneOffset(lane) - LateralHalfWidth;
            float last = Track.LaneOffset(lane + Mathf.Max(1, laneSpan) - 1) + LateralHalfWidth;
            return cart.LaneX + RaceTrack.CartHalfWidth > first && cart.LaneX - RaceTrack.CartHalfWidth < last;
        }

        /// <summary>Called once per cart when its nose reaches <see cref="TriggerAt"/>.</summary>
        public abstract void OnCartPassing(CartController cart, RaceTrack track);

        /// <summary>Puts the item back as it was before the race.</summary>
        public virtual void ResetItem() { }

        internal void AttachTo(RaceTrack track)
        {
            Track = track;
        }

        internal void UpdateDistance()
        {
            Distance = Track != null ? Track.DistanceOf(AnchorPosition) : 0f;
        }

        protected virtual void OnValidate()
        {
            laneSpan = Mathf.Clamp(laneSpan, 1, 3);
            lane = Mathf.Clamp(lane, 0, 3 - laneSpan);
            length = Mathf.Max(0.05f, length);
            minLevel = Mathf.Max(1, minLevel);
        }
    }
}
