using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Parent;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>
    /// The podium question (from the numbers level that unlocks "first, second, third"): the digits 1, 2 and 3 float above the
    /// three places and the announcer asks who stands on one of them. He taps that kid. Nothing here can go wrong for him: a
    /// wrong tap makes the tapped kid hop and the announcer says "almost", then after the second wrong tap (or a quiet pause)
    /// the right digit pulses and sparkles, and in the end the game answers itself. No effect on stars or the race level.
    /// Contract: Docs/specs/harder-numbers-contract.md (agent B).
    /// </summary>
    public partial class SoapboxRace
    {
        // The question never takes more than 20 seconds in all: help from 8 s of quiet, the answer itself at 16 s, and a
        // short closing line that is cut off by the 20 s limit.
        const float PodiumHelpAfterSeconds = 8f;
        const float PodiumSolveAfterSeconds = 16f;
        const float PodiumMaxSeconds = 20f;
        const int PodiumWrongBeforeHelp = 2;
        const float PodiumHelpPulseSeconds = 1f;

        // A tap counts for the character nearest to the finger, as long as it is closer than this many screen heights.
        const float PodiumTapRadius = 0.15f;

        // Where the digit hangs above the place, and where a character is touched / sparkled (metres above the place's ground).
        const float PodiumDigitHeight = 2.5f;
        const float PodiumBodyHeight = 0.9f;

        static readonly Color[] PodiumDigitColors =
        {
            new Color(1f, 0.78f, 0.12f),   // first: gold
            new Color(0.45f, 0.62f, 0.95f),   // second: blue
            new Color(1f, 0.52f, 0.2f),   // third: orange
        };

        // The digits on the podium; they remove themselves when the race ends (see PodiumDigit).
        readonly List<PodiumDigit> podiumDigits = new List<PodiumDigit>();

        /// <summary>Digits above the three places, then "who is first / second / third?" until he taps right (or the game does it).</summary>
        IEnumerator RunPodiumQuestion()
        {
            ClearPodiumDigits();
            Camera cam = FindPodiumCamera();
            Font font = FindPodiumFont();

            var spots = new Vector3[3];
            var characters = new KidAnimator[3];
            KidAnimator[] kids = FindObjectsByType<KidAnimator>(FindObjectsSortMode.None);
            for (int i = 0; i < 3; i++)
            {
                spots[i] = PodiumPoint(i);
                characters[i] = NearestKid(kids, spots[i]);
                podiumDigits.Add(PodiumDigit.Create(this, cam, font, (i + 1).ToString(), PodiumDigitColors[i], spots[i] + Vector3.up * PodiumDigitHeight, i * 0.12f));
            }

            int ask = Random.Range(1, 4);
            VoicePlayer.Say("race.podium_ask." + ask, true);

            float start = Time.time;
            float lastTap = start;
            float nextHelp = 0f;
            int wrong = 0;
            bool helping = false;
            bool solved = false;

            while (Time.time - start < PodiumSolveAfterSeconds)
            {
                if (!ParentGate.InputBlocked && TryReadPodiumTap(out Vector2 screenPoint))
                {
                    int tapped = NearestPodiumPlace(cam, screenPoint, spots, characters);
                    if (tapped == ask - 1)
                    {
                        solved = true;
                        PodiumCelebrate(characters[tapped], spots[tapped]);
                        break;
                    }

                    if (tapped >= 0)
                    {
                        wrong++;
                        lastTap = Time.time;
                        PodiumHop(characters[tapped], spots[tapped]);
                        VoicePlayer.Say("race.podium_wrong", true);
                    }
                }

                if (!helping && (wrong >= PodiumWrongBeforeHelp || Time.time - lastTap >= PodiumHelpAfterSeconds))
                    helping = true;

                if (helping && Time.time >= nextHelp)
                {
                    nextHelp = Time.time + PodiumHelpPulseSeconds;
                    PodiumDigit.PulseDigit(podiumDigits, ask - 1);
                    BattleEffects.Sparkle(PodiumBodyPoint(characters[ask - 1], spots[ask - 1]) + Vector3.up * 0.4f);
                }

                yield return null;
            }

            // The game answers when he did not (sparkle on the right kid, as if he had tapped it).
            if (!solved)
            {
                BattleEffects.Sparkle(PodiumBodyPoint(characters[ask - 1], spots[ask - 1]) + Vector3.up * 0.4f);
                PodiumCelebrate(characters[ask - 1], spots[ask - 1]);
            }

            // Let the praise finish, but not beyond the 20 seconds of the whole question.
            float budget = PodiumMaxSeconds - (Time.time - start);
            if (budget > 0.4f) yield return WaitForVoice(Mathf.Min(3f, budget));
        }

        // ----- Answers -----------------------------------------------------------------------------

        void PodiumCelebrate(KidAnimator who, Vector3 spot)
        {
            if (who != null) who.PlayCheer();
            BattleEffects.Confetti(PodiumBodyPoint(who, spot) + Vector3.up * 0.8f);
            Sfx.Play("race.gate_ding");
            VoicePlayer.Say("race.gate_right", true);
        }

        static void PodiumHop(KidAnimator who, Vector3 spot)
        {
            if (who != null) who.PlayJump();
            BattleEffects.Sparkle(PodiumBodyPoint(who, spot) + Vector3.up * 0.4f);
        }

        // ----- Finding things ----------------------------------------------------------------------

        static Vector3 PodiumBodyPoint(KidAnimator who, Vector3 spot)
        {
            Vector3 ground = who != null ? who.transform.position : spot;
            return ground + Vector3.up * PodiumBodyHeight;
        }

        /// <summary>The kid standing nearest to a place, within two metres (the player and the two rivals; null when nobody is there).</summary>
        static KidAnimator NearestKid(KidAnimator[] kids, Vector3 spot)
        {
            KidAnimator best = null;
            float bestDistance = 2f;
            foreach (KidAnimator kid in kids)
            {
                if (kid == null || !kid.isActiveAndEnabled) continue;

                Vector3 offset = kid.transform.position - spot;
                offset.y = 0f;
                if (offset.magnitude >= bestDistance) continue;

                bestDistance = offset.magnitude;
                best = kid;
            }
            return best;
        }

        /// <summary>The place (0 to 2) whose character is nearest to the finger and close enough, or -1.</summary>
        static int NearestPodiumPlace(Camera cam, Vector2 screenPoint, Vector3[] spots, KidAnimator[] characters)
        {
            if (cam == null) return -1;

            int best = -1;
            float bestDistance = Screen.height * PodiumTapRadius;
            for (int i = 0; i < 3; i++)
            {
                Vector3 onScreen = cam.WorldToScreenPoint(PodiumBodyPoint(characters[i], spots[i]));
                if (onScreen.z <= 0f) continue;

                float distance = Vector2.Distance(screenPoint, new Vector2(onScreen.x, onScreen.y));
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }
            return best;
        }

        Camera FindPodiumCamera()
        {
            if (chaseCamera != null)
            {
                var own = chaseCamera.GetComponent<Camera>();
                if (own != null) return own;
            }
            return Camera.main;
        }

        /// <summary>The Inter font of the gate boards, or the built-in one.</summary>
        Font FindPodiumFont()
        {
            if (track != null)
                foreach (LearningGate gate in track.Gates)
                    if (gate != null && gate.GlyphFont != null) return gate.GlyphFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A new press this frame (a finger, or the mouse when no finger pressed), not on a button.</summary>
        static bool TryReadPodiumTap(out Vector2 position)
        {
            position = Vector2.zero;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;

                    position = touch.position.ReadValue();
                    return !IsOverPodiumUi(touch.touchId.ReadValue());
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return !IsOverPodiumUi(-1);
            }
            return false;
        }

        static bool IsOverPodiumUi(int pointerId)
        {
            EventSystem system = EventSystem.current;
            return system != null && system.IsPointerOverGameObject(pointerId);
        }

        void ClearPodiumDigits()
        {
            foreach (PodiumDigit digit in podiumDigits)
                if (digit != null) Destroy(digit.gameObject);
            podiumDigits.Clear();
        }

        // ----- The floating digit ------------------------------------------------------------------

        /// <summary>
        /// One big digit in the air, always facing the camera, white outline, popping in. It removes itself when the race
        /// (its owner) is no longer running, which covers the normal end of the podium and the back button at any moment.
        /// </summary>
        sealed class PodiumDigit : MonoBehaviour
        {
            const float WorldScale = 0.011f;
            const float PopSeconds = 0.35f;
            const float PulseSeconds = 0.6f;
            const float MaxLifeSeconds = 40f;

            SoapboxRace owner;
            Camera cam;
            float born;
            float popDelay;
            float pulseStart = -10f;

            public static PodiumDigit Create(SoapboxRace owner, Camera cam, Font font, string text, Color color, Vector3 position, float popDelay)
            {
                var root = new GameObject("PodiumDigit" + text, typeof(RectTransform));
                root.transform.position = position;
                root.transform.localScale = Vector3.zero;

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                if (cam != null) canvas.worldCamera = cam;
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 140f);

                var textObject = new GameObject("Text", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var label = textObject.AddComponent<Text>();
                label.font = font;
                label.text = text;
                label.fontSize = 110;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.color = color;
                label.raycastTarget = false;
                var outline = textObject.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(6f, -6f);

                var digit = root.AddComponent<PodiumDigit>();
                digit.owner = owner;
                digit.cam = cam;
                digit.born = Time.time;
                digit.popDelay = popDelay;
                return digit;
            }

            /// <summary>Makes digit index (0 to 2) swell for a moment.</summary>
            public static void PulseDigit(List<PodiumDigit> digits, int index)
            {
                if (index >= 0 && index < digits.Count && digits[index] != null) digits[index].pulseStart = Time.time;
            }

            void LateUpdate()
            {
                if (owner == null || !owner.IsRunning || Time.time - born > MaxLifeSeconds)
                {
                    Destroy(gameObject);
                    return;
                }

                if (cam == null) cam = Camera.main;
                if (cam != null) transform.rotation = cam.transform.rotation;

                float pop = Mathf.Clamp01((Time.time - born - popDelay) / PopSeconds);
                float size = pop < 1f ? Mathf.SmoothStep(0f, 1.15f, pop) : 1f + Mathf.Max(0f, 0.15f * (1f - (Time.time - born - popDelay - PopSeconds) * 4f));

                float pulse = (Time.time - pulseStart) / PulseSeconds;
                if (pulse >= 0f && pulse <= 1f) size *= 1f + 0.5f * Mathf.Sin(pulse * Mathf.PI);

                transform.localScale = Vector3.one * (WorldScale * size);
            }
        }
    }
}
