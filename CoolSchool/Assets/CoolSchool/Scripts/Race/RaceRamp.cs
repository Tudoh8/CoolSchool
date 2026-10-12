using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// A ramp. A cart that drives onto it on the ground is launched into a big slow-motion jump (the cart does the arc).
    /// The coins over the ramp are separate <see cref="RaceCoin"/> items. The ramp is only a trigger: it has no collider.
    /// </summary>
    public class RaceRamp : RaceTrackItem
    {
        /// <summary>The cart launches this far into the ramp, as a fraction of its length (so it is on the slope, not before it).</summary>
        const float LaunchFraction = 0.3f;
        const float OnGroundHeight = 0.3f;

        [SerializeField] float launchHeight = 2.6f;
        [SerializeField] float airSeconds = 1.5f;

        public override bool Blocking => false;

        public override float TriggerAt => Start + length * LaunchFraction;

        /// <summary>A ramp is wider than a box.</summary>
        protected override float LateralHalfWidth => 0.8f;

        public override void OnCartPassing(CartController cart, RaceTrack track)
        {
            if (!Touches(cart) || cart.Height >= OnGroundHeight) return;

            cart.Launch(launchHeight, airSeconds);
            Sfx.Play("race.ramp_whoosh", transform.position);
            track.RaiseRampTaken(this, cart);
        }
    }
}
