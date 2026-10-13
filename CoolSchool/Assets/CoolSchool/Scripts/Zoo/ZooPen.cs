using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// One animal pen along the north fence of the zoo park: a low fence, a gate that swings open, a stand point in the middle and
    /// a sign with the animal's picture (a black silhouette until that kind has been found). The game sends a found animal to
    /// <see cref="TakeIn"/> at the end of a round; <see cref="PlaceInstantly"/> puts one in without ceremony (free play, scene
    /// start, ending early).
    /// </summary>
    public class ZooPen : MonoBehaviour
    {
        [SerializeField] string kindId;
        [Tooltip("Swings around its own up axis. Its pivot (or a hinge parent that is this object) must sit at the hinge end.")]
        [SerializeField] Transform gate;
        [Tooltip("In the middle of the pen, facing south (the way the animal stands in its pen).")]
        [SerializeField] Transform standPoint;
        [Tooltip("One metre south of the gate, outside the pen. Animals walk here before they hop in.")]
        [SerializeField] Transform gatePoint;
        [Tooltip("The kind's picture on the sign post. Black while the kind is unknown.")]
        [SerializeField] SpriteRenderer sign;
        [Tooltip("How far the gate swings open, in degrees around its up axis. Positive swings a west-hinged gate out to the south.")]
        [SerializeField] float openAngle = 90f;

        const float GateSeconds = 0.4f;
        const float WalkSpeed = 3f;
        const float ArriveDistance = 0.3f;
        const float WalkTimeoutSeconds = 6f;
        const float HopSeconds = 0.7f;
        const float HopTimeoutSeconds = 1.6f;
        const float SignPopSeconds = 0.45f;
        static readonly Color UnknownColor = Color.black;
        static readonly Color KnownColor = Color.white;

        Quaternion gateClosedRotation = Quaternion.identity;
        float gateAmount;
        Coroutine gateRoutine;
        Coroutine signRoutine;
        Vector3 signBaseScale = Vector3.one;
        bool captured;

        public string KindId => kindId;
        public Transform StandPoint => standPoint;
        public Transform GatePoint => gatePoint;

        void Awake()
        {
            Capture();
            RefreshSign();
        }

        void Capture()
        {
            if (captured) return;

            captured = true;
            if (gate != null) gateClosedRotation = gate.localRotation;
            if (sign != null) signBaseScale = sign.transform.localScale;
        }

        // ----- The gate ----------------------------------------------------------------------------

        /// <summary>Swings the gate open or shut over 0.4 s, with the gate sound.</summary>
        public void OpenGate(bool open)
        {
            Capture();
            if (gate == null) return;

            Sfx.Play("zoo.gate", gate.position);
            if (gateRoutine != null) StopCoroutine(gateRoutine);

            if (!isActiveAndEnabled)
            {
                SetGate(open ? 1f : 0f);
                return;
            }
            gateRoutine = StartCoroutine(SwingGate(open ? 1f : 0f));
        }

        IEnumerator SwingGate(float target)
        {
            float from = gateAmount;
            for (float elapsed = 0f; elapsed < GateSeconds; elapsed += Time.deltaTime)
            {
                SetGate(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / GateSeconds)));
                yield return null;
            }

            SetGate(target);
            gateRoutine = null;
        }

        void SetGate(float amount)
        {
            gateAmount = amount;
            if (gate != null) gate.localRotation = gateClosedRotation * Quaternion.AngleAxis(openAngle * amount, Vector3.up);
        }

        // ----- Animals -----------------------------------------------------------------------------

        /// <summary>
        /// The gate opens, the animal walks to the gate point (if it is not there yet) and hops in to the stand point, then the
        /// gate closes and `done` is called. Never gets stuck: the walk and the hop both have a time limit.
        /// </summary>
        public void TakeIn(ZooAnimal animal, Action done)
        {
            if (animal == null || standPoint == null)
            {
                done?.Invoke();
                return;
            }
            if (!isActiveAndEnabled)
            {
                PlaceInstantly(animal);
                done?.Invoke();
                return;
            }
            StartCoroutine(TakeInRoutine(animal, done));
        }

        IEnumerator TakeInRoutine(ZooAnimal animal, Action done)
        {
            OpenGate(true);
            yield return new WaitForSeconds(GateSeconds);

            // Walk to the gate. WalkTo steps the animal a little towards the point, so it is called every frame.
            if (gatePoint != null)
            {
                for (float waited = 0f; waited < WalkTimeoutSeconds; waited += Time.deltaTime)
                {
                    if (FlatDistance(animal.transform.position, gatePoint.position) <= ArriveDistance) break;

                    animal.WalkTo(gatePoint.position, WalkSpeed);
                    yield return null;
                }
                animal.SetMoving(false, false);
            }

            // Hop in to the stand point.
            bool landed = false;
            animal.HopTo(standPoint.position, standPoint.rotation, HopSeconds, () => landed = true);
            for (float waited = 0f; !landed && waited < HopTimeoutSeconds; waited += Time.deltaTime)
                yield return null;
            if (!landed) animal.ShowAt(standPoint.position, standPoint.rotation, 0f);

            Sfx.Play("zoo.pen_hop", standPoint.position);
            OpenGate(false);
            yield return new WaitForSeconds(GateSeconds);

            done?.Invoke();
        }

        /// <summary>Puts the animal on the stand point, facing south, with the gate shut. No sound.</summary>
        public void PlaceInstantly(ZooAnimal animal)
        {
            if (animal == null || standPoint == null) return;

            // A take-in still running (the game ended early) must not go on: no open gate, no animal walking on.
            StopAllCoroutines();
            gateRoutine = null;
            signRoutine = null;
            Capture();
            if (sign != null) sign.transform.localScale = signBaseScale;

            animal.SetMoving(false, false);
            animal.ShowAt(standPoint.position, standPoint.rotation, 0f);
            SetGate(0f);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        // ----- The sign ----------------------------------------------------------------------------

        /// <summary>Black silhouette → the picture in colour, with a pop and a sparkle. For a kind found for the first time.</summary>
        public void LightSign()
        {
            Capture();
            if (sign == null) return;

            if (signRoutine != null) StopCoroutine(signRoutine);
            Vector3 top = sign.transform.position;
            BattleEffects.Sparkle(top);
            Sfx.Play("zoo.found_pop", top);

            if (!isActiveAndEnabled)
            {
                RefreshSign();
                return;
            }
            signRoutine = StartCoroutine(PopSign());
        }

        IEnumerator PopSign()
        {
            sign.color = KnownColor;
            for (float elapsed = 0f; elapsed < SignPopSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / SignPopSeconds;
                sign.transform.localScale = signBaseScale * Mathf.Lerp(1.6f, 1f, LessonMotion.OutBack(t));
                yield return null;
            }

            sign.transform.localScale = signBaseScale;
            signRoutine = null;
        }

        /// <summary>The sign is the colour picture when this kind has been found, a black silhouette when not.</summary>
        public void RefreshSign()
        {
            Capture();
            if (sign == null) return;

            if (signRoutine != null)
            {
                StopCoroutine(signRoutine);
                signRoutine = null;
                sign.transform.localScale = signBaseScale;
            }
            sign.color = ZooProgress.IsKnown(kindId) ? KnownColor : UnknownColor;
        }
    }
}
