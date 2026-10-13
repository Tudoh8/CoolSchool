using System;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The poses the kid takes on a playground toy. The number is the value of the int parameter "ToyPose" on Kid.controller;
    /// 0 is the normal locomotion.
    /// </summary>
    public enum ToyPose
    {
        None = 0,
        SitSeat = 1,
        SitSlide = 2,
        Climb = 3,
        Hang = 4,
        HangMove = 5,
        Crawl = 6,
    }

    /// <summary>Hip and hand heights above the kid's root (his feet) for each pose, measured by the builder from the clips.</summary>
    [Serializable]
    public struct ToyPoseOffsets
    {
        /// <summary>Hips height when sitting on a seat, about 0.45.</summary>
        public float sitSeatHips;

        /// <summary>Hips height when sitting on the ground or a slide, about 0.12.</summary>
        public float sitSlideHips;

        /// <summary>Hands height when hanging from a bar, about 1.35.</summary>
        public float hangHands;

        /// <summary>The values to use when the clips could not be measured (for the Synty kid, whose standing hips are about 0.54 m high).</summary>
        public static ToyPoseOffsets Defaults => new ToyPoseOffsets { sitSeatHips = 0.27f, sitSlideHips = 0.10f, hangHands = 1.15f };
    }
}
