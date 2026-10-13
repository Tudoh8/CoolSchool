using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The dragon's egg, in the egg mission. The dragon is sad because it is looking for this egg. He taps the egg, it rolls to the
    /// dragon's feet, the dragon nuzzles it, it wobbles three times and bursts open, and the baby dragon says hello to its mummy.
    /// Lives on the egg itself (which also has a MissionLoot, so a thief goblin can steal it earlier in the visit).
    /// </summary>
    public class DragonEgg : MonoBehaviour
    {
        [SerializeField] WorldTapPicker picker;
        [SerializeField] BabyDragon baby;
        [Tooltip("4 to 6 curved bits of shell, hidden at the start. They fly out when the egg bursts.")]
        [SerializeField] GameObject[] shellPieces;

        const float TapHintSeconds = 10f;
        const float SolveSeconds = 25f;
        const float FeetDistance = 1.2f;

        sealed class Home
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public bool Active;
        }

        readonly List<Renderer> eggRenderers = new List<Renderer>();
        readonly Dictionary<Transform, Home> pieceHomes = new Dictionary<Transform, Home>();
        Vector3 homePosition;
        Quaternion homeRotation;
        Transform homeParent;
        Vector3 homeScale;
        bool haveHome;
        bool tapped;

        /// <summary>The whole giving back, up to the baby saying hello. Safe to stop at any moment (then call ResetMoment).</summary>
        public IEnumerator RunGiveBack(DragonBoss dragon, MomentResult result)
        {
            CacheHome();
            if (dragon == null) yield break;

            yield return CastleMotion.SayAndWait("castle.egg_tap");

            tapped = false;
            if (picker != null)
            {
                picker.SetTargets(new[] { transform });
                picker.Tapped += OnTapped;
                picker.Armed = true;
            }

            float started = Time.time;
            float nextHint = Time.time + TapHintSeconds;
            while (!tapped && Time.time - started < SolveSeconds)
            {
                // The egg bobs a little to show it is the thing to tap.
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                transform.localScale = homeScale * (1f + beat * 0.08f);

                if (Time.time >= nextHint && CastleMotion.VoiceIsFree())
                {
                    nextHint = Time.time + TapHintSeconds;
                    VoicePlayer.Say("castle.egg_tap");
                }
                yield return null;
            }
            transform.localScale = homeScale;

            if (picker != null)
            {
                picker.Armed = false;
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            // It rolls to the dragon's feet.
            Vector3 feet = dragon.transform.position + FlatForward(dragon.transform) * FeetDistance;
            feet.y = dragon.transform.position.y + EggHalfHeight();
            transform.SetParent(null, true);
            this.Fork(RollTurn(1.3f));
            yield return CastleMotion.Arc(transform, feet, 1.3f, 1.1f);
            Sfx.Play("castle.block_place", feet);
            BattleEffects.Sparkle(feet + Vector3.up * 0.3f);

            dragon.Nuzzle();
            yield return new WaitForSeconds(1.2f);

            VoicePlayer.Say("castle.egg_hatch", true);
            for (int i = 0; i < 3; i++)
            {
                Sfx.Play("castle.egg_crack", transform.position);
                yield return Wobble(0.45f);
                yield return new WaitForSeconds(0.15f);
            }

            // It bursts: the shell flies out, confetti, the baby pops up.
            Vector3 burst = transform.position;
            SetEggVisible(false);
            Sfx.Play("castle.egg_crack", burst);
            BattleEffects.Confetti(burst + Vector3.up * 0.4f);
            BattleEffects.Sparkle(burst + Vector3.up * 0.3f);
            for (int i = 0; i < 3; i++) this.Fork(FlyShell(i, burst));
            if (baby != null) baby.Hatch(burst);
            yield return new WaitForSeconds(0.6f);

            yield return CastleMotion.WaitForVoice(4f);
            yield return CastleMotion.SayAndWait("castle.baby_hello");
        }

        /// <summary>The egg whole and back in the nest, the shell pieces hidden, the baby away, the picker released.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            if (picker != null)
            {
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            CacheHome();
            SetEggVisible(true);
            foreach (KeyValuePair<Transform, Home> entry in pieceHomes)
            {
                if (entry.Key == null) continue;

                entry.Key.SetPositionAndRotation(entry.Value.Position, entry.Value.Rotation);
                entry.Key.localScale = entry.Value.Scale;
                entry.Key.gameObject.SetActive(false);
            }

            // Back in the nest: the loot knows its own place; ours is the backup for an egg without one.
            transform.localScale = homeScale;
            MissionLoot loot = GetComponent<MissionLoot>();
            if (loot != null) loot.ResetLoot();
            else
            {
                transform.SetParent(homeParent, true);
                transform.SetPositionAndRotation(homePosition, homeRotation);
            }

            if (baby != null) baby.Hide();
        }

        void OnTapped(Transform target)
        {
            tapped = true;
        }

        // ----- Pieces of the show ----------------------------------------------------------------

        void CacheHome()
        {
            if (haveHome) return;

            haveHome = true;
            homeParent = transform.parent;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeScale = transform.localScale;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (!(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) && !IsShell(renderer.transform)) eggRenderers.Add(renderer);

            if (shellPieces != null)
                foreach (GameObject piece in shellPieces)
                    if (piece != null)
                        pieceHomes[piece.transform] = new Home
                        {
                            Position = piece.transform.position,
                            Rotation = piece.transform.rotation,
                            Scale = piece.transform.localScale,
                            Active = piece.activeSelf,
                        };
        }

        bool IsShell(Transform candidate)
        {
            if (shellPieces == null) return false;

            foreach (GameObject piece in shellPieces)
                if (piece != null && (candidate == piece.transform || candidate.IsChildOf(piece.transform))) return true;
            return false;
        }

        void SetEggVisible(bool visible)
        {
            foreach (Renderer renderer in eggRenderers)
                if (renderer != null) renderer.enabled = visible;
        }

        float EggHalfHeight()
        {
            return CastleMotion.TryGetBounds(gameObject, out Bounds bounds) ? bounds.extents.y : 0.3f;
        }

        static Vector3 FlatForward(Transform thing)
        {
            Vector3 forward = thing.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.back : forward.normalized;
        }

        /// <summary>The egg rolls end over end while it flies.</summary>
        IEnumerator RollTurn(float seconds)
        {
            Quaternion start = transform.rotation;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                transform.rotation = Quaternion.AngleAxis(Mathf.SmoothStep(0f, 540f, elapsed / seconds), Vector3.right) * start;
                yield return null;
            }
            transform.rotation = start;
        }

        /// <summary>A quick wobble to one side and the other, getting livelier each time.</summary>
        IEnumerator Wobble(float seconds)
        {
            Quaternion start = transform.rotation;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float angle = Mathf.Sin(elapsed / seconds * Mathf.PI * 4f) * 16f;
                transform.rotation = start * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            transform.rotation = start;
        }

        /// <summary>A piece of shell flies out in its own direction, falls, rests a moment and is hidden.</summary>
        IEnumerator FlyShell(int salt, Vector3 from)
        {
            if (shellPieces == null) yield break;

            for (int i = salt; i < shellPieces.Length; i += 3)
            {
                GameObject piece = shellPieces[i];
                if (piece == null) continue;

                float angle = (i * 137.5f + salt * 40f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 to = from + direction * Random.Range(0.6f, 1.2f);
                to.y = from.y - EggHalfHeight() + 0.04f;

                piece.transform.position = from + Vector3.up * 0.2f;
                piece.transform.rotation = Random.rotation;
                piece.SetActive(true);
                this.Fork(CastleMotion.Arc(piece.transform, to, 0.6f, 0.9f));
            }

            yield return new WaitForSeconds(2.6f);
            if (shellPieces == null) yield break;

            for (int i = salt; i < shellPieces.Length; i += 3)
                if (shellPieces[i] != null) shellPieces[i].SetActive(false);
        }
    }
}
