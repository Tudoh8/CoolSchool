using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// A gold coin floating over the road. It spins and bobs, and vanishes when a cart drives through it (the player's cart
    /// scores it). Put it where the coin's middle should float; <c>height</c> must say how high that is above the road.
    /// A cart picks it up when it is close enough sideways and its body is within a metre of the coin's height, so a jump
    /// reaches coins over a ramp and low coins are still found by a cart that is in the air a little.
    /// </summary>
    public class RaceCoin : RaceTrackItem
    {
        const float SpinDegreesPerSecond = 160f;
        const float BobMetres = 0.06f;
        const float BobSpeed = 3f;
        // The middle of the cart's body is this far above the road when it drives on the ground.
        const float CartBodyHeight = 0.6f;
        const float PickHeightRange = 1.0f;

        [Tooltip("How high the coin's middle floats above the road, in metres.")]
        [SerializeField] float height = 0.6f;
        [Tooltip("The model that spins and bobs. Falls back to the first child, or this object.")]
        [SerializeField] Transform visual;

        Renderer[] renderers;
        Transform spinner;
        Vector3 spinnerHome;
        float phase;
        bool collected;

        public float Height => height;

        public override bool Blocking => false;

        /// <summary>Easy to pick up: a coin is wider than it looks.</summary>
        protected override float LateralHalfWidth => 0.7f;

        void Awake()
        {
            Cache();
            phase = (GetInstanceID() & 0xFF) / 255f * Mathf.PI * 2f;
        }

        void Cache()
        {
            if (renderers != null) return;

            renderers = GetComponentsInChildren<Renderer>(true);
            spinner = visual != null ? visual : (transform.childCount > 0 ? transform.GetChild(0) : transform);
            spinnerHome = spinner.localPosition;
        }

        void Update()
        {
            if (collected || spinner == null) return;

            spinner.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            spinner.localPosition = spinnerHome + Vector3.up * (Mathf.Sin(Time.time * BobSpeed + phase) * BobMetres);
        }

        public override void OnCartPassing(CartController cart, RaceTrack track)
        {
            if (collected || !Touches(cart)) return;
            if (Mathf.Abs(cart.Height + CartBodyHeight - height) >= PickHeightRange) return;

            collected = true;
            SetVisible(false);

            if (!cart.IsPlayer) return;

            Vector3 where = transform.position;
            Sfx.Play("race.coin", where);
            BattleEffects.Sparkle(where);
            track.RaiseCoinCollected(this, cart);
        }

        public override void ResetItem()
        {
            Cache();
            collected = false;
            spinner.localPosition = spinnerHome;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer coinRenderer in renderers)
                if (coinRenderer != null) coinRenderer.enabled = visible;
        }
    }
}
