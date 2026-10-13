using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// A place where an animal (or the child) can hide: a hedge, a bin, a pond, a tent... The camera sees everything, so a hider
    /// is always partly visible: behind a cover it stands on the north side (the camera's far side), in a container it is sunk so
    /// only its top shows. The spot knows its poses (hide, peek, player hide), how close the child must come to check it, how
    /// to peek, shake and pop the hider out, and shows a pulsing ring when it is one of the child's own hiding places.
    /// </summary>
    public class HidingSpot : MonoBehaviour
    {
        [SerializeField] string spotId;
        [SerializeField] HidingSpotKind kind;
        [Tooltip("The prop. Its renderers give the bounds, and it is what shakes.")]
        [SerializeField] Transform cover;
        [SerializeField] Transform hidePoint;
        [SerializeField] Transform peekPoint;
        [Tooltip("Where the child stands when he hides here. Empty for the pond and the treetop (he can't go in there).")]
        [SerializeField] Transform playerHidePoint;
        [Tooltip("Containers only: above 0 the ground may swallow the animal (pond, leaves); 0 means it never goes below the ground. Used instead of the show fraction when no rim height is set.")]
        [SerializeField] float sinkFraction;
        [Tooltip("Containers: the height of the rim (or the water, or the roof) above the spot's ground, in metres. Set by the builder.")]
        [SerializeField] float rimHeight;
        [Tooltip("Containers: the part of the animal's height that stands above the rim (0.4 = the top 40 percent shows).")]
        [SerializeField] float showFraction;
        [Tooltip("The tent: the front part of the animal's length that sticks out past the south edge of the prop (its head). 0 = off.")]
        [SerializeField] float frontOut;
        [Tooltip("Checked when the child is this close to the edge of the cover.")]
        [SerializeField] float checkRadius = 1.4f;
        [Tooltip("A pulsing ring on the ground, inactive.")]
        [SerializeField] GameObject playerGlow;

        const float PeekMoveSeconds = 0.45f;
        const float PeekHoldSeconds = 0.7f;
        const float PeekRaise = 0.35f;
        const float HardShakeSeconds = 1f;
        const float HardShakeDegrees = 4f;
        const float SoftShakeSeconds = 0.4f;
        const float SoftShakeDegrees = 1.6f;
        const float PopStepOut = 1f;
        const float SideBySide = 0.8f;
        const float GlowPulseSpeed = 4f;
        const float GlowPulseAmount = 0.12f;
        const float SmallShowFraction = 0.65f;

        ZooAnimal hider;
        bool wasChecked;
        bool sidewaysRest;
        Vector3 restPosition;
        Quaternion restRotation = Quaternion.identity;
        float restSink;
        float halfDepth;
        Coroutine peekRoutine;
        Coroutine shakeRoutine;
        Bounds bounds;
        bool boundsKnown;
        Vector3 coverBasePosition;
        Quaternion coverBaseRotation = Quaternion.identity;
        bool coverCaptured;
        bool glowOn;
        Vector3 glowBaseScale = Vector3.one;
        bool glowScaleCaptured;

        public string SpotId => spotId;

        public HidingSpotKind Kind => kind;

        public bool CanHidePlayer => playerHidePoint != null;

        public Transform PlayerHidePoint => playerHidePoint;

        public float CheckRadius => checkRadius;

        /// <summary>The animal hiding here now (null when nobody hides here or it has been found).</summary>
        public ZooAnimal Hider => hider;

        /// <summary>True once the child has been close enough to look here this round.</summary>
        public bool Checked => wasChecked;

        /// <summary>The size of the prop on the ground (its pictures' bounds).</summary>
        public Bounds CoverBounds
        {
            get
            {
                EnsureBounds();
                return bounds;
            }
        }

        /// <summary>Where the child's glowing ring lies on the ground (the south side of the prop, where he can see it and walk to it).</summary>
        public Vector3 GlowPosition => playerGlow != null ? playerGlow.transform.position : PopPoint;

        /// <summary>
        /// A free place on the south (camera) side of the prop, on the walkable ground: where a found animal lands, where a surprise
        /// pops out and where the keeper stands to look.
        /// </summary>
        public Vector3 PopPoint
        {
            get
            {
                Bounds b = CoverBounds;
                var wanted = new Vector3(b.center.x, b.min.y, b.min.z - PopStepOut);
                return NavMesh.SamplePosition(wanted, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : wanted;
            }
        }

        void Awake()
        {
            EnsureBounds();
            CaptureCover();
        }

        void EnsureBounds()
        {
            if (boundsKnown) return;

            boundsKnown = true;
            Transform root = cover != null ? cover : transform;
            bool found = false;
            bounds = new Bounds(root.position, Vector3.one * 0.1f);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
                if (playerGlow != null && renderer.transform.IsChildOf(playerGlow.transform)) continue;

                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
        }

        void CaptureCover()
        {
            if (coverCaptured || cover == null) return;

            coverCaptured = true;
            coverBasePosition = cover.position;
            coverBaseRotation = cover.rotation;
        }

        /// <summary>How far the point is from the nearest side of the prop on the ground (0 inside it).</summary>
        public float DistanceTo(Vector3 point)
        {
            Bounds b = CoverBounds;
            point.y = b.center.y;
            return Mathf.Sqrt(b.SqrDistance(point));
        }

        // ----- Hiding an animal --------------------------------------------------------------------

        /// <summary>Puts the animal in its hide pose: sunk into a container, north of a cover, up in the crown. It faces south.</summary>
        public void Occupy(ZooAnimal animal)
        {
            StopRoutines();
            RestoreCover();
            hider = animal;
            if (animal == null) return;

            Vector3 position = hidePoint != null ? hidePoint.position : GroundCentre();
            restRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            restSink = 0f;
            halfDepth = 0f;
            sidewaysRest = false;

            switch (kind)
            {
                case HidingSpotKind.Cover:
                    // The long animals stand sideways behind a thin cover, so their length shows on both sides.
                    sidewaysRest = animal.KindId == "elephant" || animal.KindId == "giraffe" || animal.KindId == "zebra";
                    halfDepth = Mathf.Clamp(sidewaysRest ? Mathf.Min(animal.Length, animal.Height * 0.5f) * 0.5f : animal.Length * 0.5f, 0.1f, 1.2f);
                    position += Vector3.forward * halfDepth;
                    if (sidewaysRest) restRotation = Quaternion.LookRotation(PeekSide() * Vector3.right, Vector3.up);
                    break;
                case HidingSpotKind.Container:
                    position = PoseInContainer(animal, position);
                    break;
                case HidingSpotKind.Under:
                    restSink = sinkFraction;
                    break;
            }

            restPosition = position;
            animal.ShowAt(restPosition, restRotation, restSink);
        }

        /// <summary>
        /// Containers: the animal's model bottom stands at rimHeight - (1 - showFraction) * height above the ground, so the same part of
        /// every animal shows over the rim, tall or small. The tent is different: the animal stands inside at ground level, pushed south
        /// so its head sticks out in front. Sets restSink (negative = raised) and returns where the animal's root goes.
        /// </summary>
        Vector3 PoseInContainer(ZooAnimal animal, Vector3 position)
        {
            float ground = transform.position.y;
            if (frontOut > 0f)
            {
                Bounds b = CoverBounds;
                restSink = 0f;
                return new Vector3(b.center.x, ground, b.min.z + (0.5f - frontOut) * animal.Length);
            }

            float show = ShowFractionFor(animal);
            if (rimHeight <= 0f || show <= 0f)
            {
                restSink = sinkFraction;
                return position;
            }

            float bottom = ground + rimHeight - (1f - show) * animal.Height;
            // Only the pond and the leaves may swallow part of the animal into the ground.
            if (sinkFraction <= 0f) bottom = Mathf.Max(bottom, ground);
            restSink = (ground - bottom) / Mathf.Max(0.01f, animal.Height);
            position.y = ground;
            return position;
        }

        /// <summary>The part of the animal that shows over the rim. The pond differs per kind: the flamingo stands in the water.</summary>
        float ShowFractionFor(ZooAnimal animal)
        {
            if (spotId == "pond")
            {
                switch (animal.KindId)
                {
                    case "flamingo": return 0.9f;
                    case "crocodile": return 0.5f;
                    case "hippo": return 0.45f;
                }
            }

            // The small hiders (penguin, monkey) stand taller in bins, boxes and houses, so head and shoulders clearly stick out.
            if ((spotId == "bin" || spotId == "box" || spotId == "house") && ZooAnimalKind.Get(animal.KindId)?.Size == ZooSize.Small)
                return Mathf.Max(showFraction, SmallShowFraction);
            return showFraction;
        }

        /// <summary>1 when the peek point is east of the hide point, -1 when west (or unknown: a random side).</summary>
        float PeekSide()
        {
            if (peekPoint != null && hidePoint != null)
                return peekPoint.position.x >= hidePoint.position.x ? 1f : -1f;
            return Random.value < 0.5f ? 1f : -1f;
        }

        Vector3 GroundCentre()
        {
            Bounds b = CoverBounds;
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }

        /// <summary>The hider leans out to the peek pose and back, about 1.6 s, with a small rustle.</summary>
        public void Peek()
        {
            if (hider == null || wasChecked || peekRoutine != null || !isActiveAndEnabled) return;

            Sfx.Play("zoo.peek", restPosition);
            StartShake(SoftShakeSeconds, SoftShakeDegrees, false);
            peekRoutine = StartCoroutine(PeekRoutine(hider));
        }

        IEnumerator PeekRoutine(ZooAnimal animal)
        {
            ComputePeekPose(animal, out Vector3 position, out Quaternion rotation, out float sinkAmount);

            yield return MoveHider(animal, restPosition, restRotation, restSink, position, rotation, sinkAmount, PeekMoveSeconds);
            yield return new WaitForSeconds(PeekHoldSeconds);
            if (animal == hider)
                yield return MoveHider(animal, position, rotation, sinkAmount, restPosition, restRotation, restSink, PeekMoveSeconds);

            peekRoutine = null;
        }

        void ComputePeekPose(ZooAnimal animal, out Vector3 position, out Quaternion rotation, out float sinkAmount)
        {
            position = restPosition;
            rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            sinkAmount = restSink;

            switch (kind)
            {
                case HidingSpotKind.Cover:
                    // Beside the end of the cover, at ground level, looking out at the camera.
                    if (peekPoint != null) position = peekPoint.position + Vector3.forward * halfDepth;
                    else position = restPosition + Vector3.right * (PeekSide() * (CoverBounds.extents.x + SideBySide));
                    sinkAmount = 0f;
                    break;
                case HidingSpotKind.Container:
                    if (frontOut > 0f)
                    {
                        // The tent: leans out of the front a little more.
                        position = restPosition + Vector3.back * 0.5f;
                        break;
                    }
                    // Pops up out of the container by a third of its height.
                    sinkAmount = restSink - PeekRaise;
                    break;
                case HidingSpotKind.Perch:
                    position = peekPoint != null ? peekPoint.position : restPosition + Vector3.back * 0.3f + Vector3.down * 0.2f;
                    break;
                case HidingSpotKind.Under:
                    // Crawls out a little towards the camera.
                    position = restPosition + Vector3.back * Mathf.Min(0.6f, animal.Length * 0.4f);
                    break;
            }
        }

        static IEnumerator MoveHider(ZooAnimal animal, Vector3 fromPosition, Quaternion fromRotation, float fromSink,
            Vector3 toPosition, Quaternion toRotation, float toSink, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (animal == null) yield break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                animal.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, k), Quaternion.Slerp(fromRotation, toRotation, k));
                animal.SetSink(Mathf.Lerp(fromSink, toSink, k));
                yield return null;
            }

            if (animal == null) yield break;
            animal.transform.SetPositionAndRotation(toPosition, toRotation);
            animal.SetSink(toSink);
        }

        // ----- Shaking -----------------------------------------------------------------------------

        /// <summary>The cover wobbles hard for a second and leaves/sparkles fly: "look, something moves there!"</summary>
        public void Shake()
        {
            if (!isActiveAndEnabled) return;

            Sfx.Play("zoo.shake", restPosition);
            StartShake(HardShakeSeconds, HardShakeDegrees, true);
        }

        void StartShake(float seconds, float degrees, bool burst)
        {
            if (cover == null || !isActiveAndEnabled) return;

            CaptureCover();
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                RestoreCover();
            }
            shakeRoutine = StartCoroutine(ShakeRoutine(seconds, degrees, burst));
        }

        IEnumerator ShakeRoutine(float seconds, float degrees, bool burst)
        {
            Bounds b = CoverBounds;
            var pivot = new Vector3(b.center.x, b.min.y, b.center.z);
            float nextBurst = 0f;

            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / seconds;
                float wobble = Mathf.Sin(elapsed * 38f) * degrees * fade;
                cover.SetPositionAndRotation(coverBasePosition, coverBaseRotation);
                cover.RotateAround(pivot, Vector3.forward, wobble);
                cover.RotateAround(pivot, Vector3.right, wobble * 0.6f);

                if (burst && elapsed >= nextBurst)
                {
                    nextBurst += 0.3f;
                    BattleEffects.Sparkle(new Vector3(Random.Range(b.min.x, b.max.x), Mathf.Min(b.max.y, b.min.y + 2.5f), Random.Range(b.min.z, b.max.z)));
                }
                yield return null;
            }

            RestoreCover();
            shakeRoutine = null;
        }

        void RestoreCover()
        {
            if (cover != null && coverCaptured) cover.SetPositionAndRotation(coverBasePosition, coverBaseRotation);
        }

        void StopRoutines()
        {
            if (peekRoutine != null) StopCoroutine(peekRoutine);
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            peekRoutine = null;
            shakeRoutine = null;
        }

        // ----- Looking ----------------------------------------------------------------------------

        /// <summary>
        /// The child looks here: the spot is checked. The hider (if any) pops out with a hop, confetti and a pop sound and is
        /// returned. An empty spot only wobbles and returns null.
        /// </summary>
        public ZooAnimal Reveal()
        {
            wasChecked = true;
            StopRoutines();
            RestoreCover();

            ZooAnimal found = hider;
            hider = null;
            StartShake(SoftShakeSeconds, SoftShakeDegrees, false);
            if (found == null) return null;

            Vector3 pop = PopPoint;
            found.HopTo(pop, Quaternion.LookRotation(Vector3.back, Vector3.up), 0.55f);
            Sfx.Play("zoo.found_pop", pop);
            BattleEffects.Confetti(pop + Vector3.up * 1f);
            return found;
        }

        // ----- The child's own hiding place --------------------------------------------------------

        /// <summary>Shows or hides the pulsing ring around the child's hide point.</summary>
        public void SetPlayerGlow(bool on)
        {
            if (playerGlow == null) return;

            if (on && !glowScaleCaptured)
            {
                glowScaleCaptured = true;
                glowBaseScale = playerGlow.transform.localScale;
            }
            glowOn = on;
            playerGlow.SetActive(on);
            if (!on && glowScaleCaptured) playerGlow.transform.localScale = glowBaseScale;
        }

        void Update()
        {
            if (!glowOn || playerGlow == null) return;

            float pulse = 1f + Mathf.Sin(Time.time * GlowPulseSpeed) * GlowPulseAmount;
            playerGlow.transform.localScale = glowBaseScale * pulse;
        }

        /// <summary>Back to the start of a round: nobody hides here, not checked, the cover still, the ring off.</summary>
        public void ResetSpot()
        {
            StopRoutines();
            RestoreCover();
            hider = null;
            wasChecked = false;
            SetPlayerGlow(false);
        }

        void OnDisable()
        {
            StopRoutines();
            RestoreCover();
        }
    }
}
