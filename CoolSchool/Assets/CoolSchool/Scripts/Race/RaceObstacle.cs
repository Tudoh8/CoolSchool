using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// Something in the road. Boxes and cones are soft: a cart that drives into one sends it tumbling away (a scripted arc,
    /// no physics) and wobbles a little. Puddles and sprinklers splash the cart, which is just fun. A cart that is high enough
    /// (a jump) passes over. The clearance and the width of each kind come from a table here, whatever the serialized
    /// <c>clearance</c> says.
    /// </summary>
    public class RaceObstacle : RaceTrackItem
    {
        public enum ObstacleKind { Box, Cone, BoxStack, Puddle, Sprinkler }

        static readonly Color WaterColor = new Color(0.55f, 0.8f, 1f, 1f);

        const float TumbleSeconds = 0.9f;
        const float TumbleSideMetres = 2.4f;
        const float TumbleForwardMetres = 3f;
        const float TumbleLiftMetres = 1.1f;
        const float TumbleSpinDegrees = 720f;
        const float SprinklerSwingDegrees = 55f;
        const float SprinklerSwingSpeed = 1.6f;

        [SerializeField] ObstacleKind kind = ObstacleKind.Box;
        [Tooltip("The thing that tumbles away (the box, the cone). Falls back to this object.")]
        [SerializeField] Transform visual;
        [Tooltip("Sprinkler only: the parent of the water jets. It swings left and right across the lanes.")]
        [SerializeField] Transform jetPivot;

        Vector3 homePosition;
        Quaternion homeRotation;
        Vector3 homeScale;
        Quaternion jetHomeRotation = Quaternion.identity;
        bool cached;

        bool tumbling;
        bool tumbled;
        float tumbleTime;
        Vector3 tumbleStart;
        Quaternion tumbleStartRotation;
        Vector3 tumbleTravel;
        Vector3 tumbleAxis;
        float swingPhase;

        public ObstacleKind Kind => kind;

        public override float Clearance
        {
            get
            {
                switch (kind)
                {
                    case ObstacleKind.Cone: return 0.7f;
                    case ObstacleKind.Puddle: return 0.3f;
                    case ObstacleKind.Sprinkler: return 0.9f;
                    case ObstacleKind.BoxStack: return 1.8f;
                    default: return 0.8f;
                }
            }
        }

        /// <summary>A box that already tumbled away is not in the way any more.</summary>
        public override bool Blocking => !tumbled && !tumbling;

        public override bool MayBlock => true;

        protected override float LateralHalfWidth
        {
            get
            {
                switch (kind)
                {
                    case ObstacleKind.Cone: return 0.35f;
                    case ObstacleKind.BoxStack: return 0.5f;
                    case ObstacleKind.Puddle: return 0.9f;
                    case ObstacleKind.Sprinkler: return 1.0f;
                    default: return 0.45f;
                }
            }
        }

        bool Tumbles => kind == ObstacleKind.Box || kind == ObstacleKind.Cone || kind == ObstacleKind.BoxStack;

        Transform Visual => visual != null ? visual : transform;

        void Awake()
        {
            Cache();
            swingPhase = (GetInstanceID() & 0xFF) / 255f * Mathf.PI * 2f;
        }

        void Cache()
        {
            if (cached) return;

            cached = true;
            Transform thing = Visual;
            homePosition = thing.position;
            homeRotation = thing.rotation;
            homeScale = thing.localScale;
            if (jetPivot != null) jetHomeRotation = jetPivot.localRotation;
        }

        public override void OnCartPassing(CartController cart, RaceTrack track)
        {
            if (tumbled || tumbling) return;
            if (!Touches(cart) || cart.Height >= Clearance) return;

            Cache();
            if (Tumbles) Hit(cart, track);
            else Splash(cart, track);
        }

        void Hit(CartController cart, RaceTrack track)
        {
            Vector3 where = Visual.position + Vector3.up * 0.3f;
            Sfx.Play("race.bonk", where);
            if (kind == ObstacleKind.Cone) cart.Bonk(0.75f, 0.6f);
            else cart.Bonk(0.6f, 1.0f);

            // Away to the side the box sits on (relative to the cart), and on a bit forward.
            float sideSign = Mathf.Sign(track.LateralOf(Visual.position) - cart.LaneX);
            if (Mathf.Abs(track.LateralOf(Visual.position) - cart.LaneX) < 0.1f) sideSign = Random.value < 0.5f ? -1f : 1f;
            tumbleTravel = track.Right * (sideSign * TumbleSideMetres) + track.Forward * TumbleForwardMetres;
            tumbleStart = Visual.position;
            tumbleStartRotation = Visual.rotation;
            tumbleAxis = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)).normalized;
            tumbleTime = 0f;
            tumbling = true;

            track.RaiseItemHit(this, cart);
        }

        void Splash(CartController cart, RaceTrack track)
        {
            Vector3 where = track.PointAt(Distance, cart.LaneX) + Vector3.up * 0.2f;
            BattleEffects.Confetti(where, WaterColor);
            Sfx.Play("race.splash", where);
            cart.Splash(0.85f, 0.6f);

            track.RaiseItemHit(this, cart);
        }

        void Update()
        {
            if (kind == ObstacleKind.Sprinkler && jetPivot != null)
            {
                float swing = Mathf.Sin(Time.time * SprinklerSwingSpeed + swingPhase) * SprinklerSwingDegrees;
                jetPivot.localRotation = jetHomeRotation * Quaternion.Euler(0f, swing, 0f);
            }

            if (!tumbling) return;

            tumbleTime += Time.deltaTime;
            float k = Mathf.Clamp01(tumbleTime / TumbleSeconds);
            float eased = 1f - (1f - k) * (1f - k);

            Transform thing = Visual;
            thing.position = tumbleStart + tumbleTravel * eased + Vector3.up * (Mathf.Sin(k * Mathf.PI) * TumbleLiftMetres);
            thing.rotation = Quaternion.AngleAxis(TumbleSpinDegrees * eased, tumbleAxis) * tumbleStartRotation;

            if (k >= 1f)
            {
                tumbling = false;
                tumbled = true;
            }
        }

        public override void ResetItem()
        {
            Cache();
            tumbling = false;
            tumbled = false;

            Transform thing = Visual;
            thing.position = homePosition;
            thing.rotation = homeRotation;
            thing.localScale = homeScale;
            if (!thing.gameObject.activeSelf && thing != transform) thing.gameObject.SetActive(true);
            if (jetPivot != null) jetPivot.localRotation = jetHomeRotation;
        }
    }
}
