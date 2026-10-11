using System;
using System.Collections;
using CoolSchool.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The lesson's touch UI on the TouchUI canvas: three huge round number cards along the bottom of the
    /// screen. Cards wiggle on a wrong pick (never red, never a buzzer), and grow and glow on the right one.
    /// Also hides the joystick and action button during a lesson. The star jar stays visible.
    /// </summary>
    public class LessonUI : MonoBehaviour
    {
        [Tooltip("Inter Bold from the Modular Game UI Kit.")]
        [SerializeField] Font font;
        [Tooltip("A round picture for the cards.")]
        [SerializeField] Sprite cardSprite;
        [Tooltip("Joystick zone, action button: hidden while the lesson runs.")]
        [SerializeField] GameObject[] gameplayHud;
        [SerializeField] float cardSize = 330f;
        [SerializeField] float cardGap = 90f;
        [SerializeField] float bottomMargin = 50f;

        const int CardSlots = 3;
        const int DigitFontSize = 210;
        const float AppearSeconds = 0.4f;
        const float WiggleSeconds = 0.55f;
        const float WiggleDegrees = 14f;
        const float CelebrateSeconds = 0.5f;
        const float CelebrateScale = 1.35f;
        const float PulseSpeed = 6f;
        const float PulseAmount = 0.12f;
        const float DimmedAlpha = 0.3f;

        // One friendly colour per slot: coral, sunshine and sky.
        static readonly Color[] CardColors =
        {
            new Color(1f, 0.45f, 0.4f, 1f),
            new Color(1f, 0.78f, 0.2f, 1f),
            new Color(0.35f, 0.68f, 1f, 1f),
        };

        sealed class Card
        {
            public RectTransform Rect;
            public Image Face;
            public Image Glow;
            public Text Label;
            public Button Button;
            public int Number;
            public Coroutine ScaleRoutine;
            public Coroutine TurnRoutine;
        }

        readonly Card[] cards = new Card[CardSlots];
        Action<int> onPicked;
        bool built;

        public bool IsShowingCards { get; private set; }

        void Awake()
        {
            BuildCards();
            HideCards();
        }

        /// <summary>Shows up to three number cards. onPicked gets the number on the card that was tapped.</summary>
        public void ShowNumberCards(int[] numbers, Action<int> onPicked)
        {
            BuildCards();
            StopAllCoroutines();
            ResetCardRoutines();

            this.onPicked = onPicked;
            IsShowingCards = true;

            int count = Mathf.Min(numbers != null ? numbers.Length : 0, CardSlots);
            for (int i = 0; i < CardSlots; i++)
            {
                Card card = cards[i];
                bool used = i < count;
                card.Rect.gameObject.SetActive(used);
                if (!used) continue;

                card.Number = numbers[i];
                card.Label.text = numbers[i].ToString();
                card.Rect.anchoredPosition = CardPosition(i, count);
                card.Rect.localRotation = Quaternion.identity;
                card.Face.color = CardColors[i];
                card.Glow.enabled = false;
                card.Button.interactable = true;
                StartScale(card, AppearRoutine(card.Rect, i * 0.1f));
            }
        }

        /// <summary>A friendly shake for a card that was not the answer.</summary>
        public void WiggleCard(int number)
        {
            Card card = Find(number);
            if (card == null || !isActiveAndEnabled) return;

            Sfx.Play("ui.card_wrong");
            if (card.TurnRoutine != null) StopCoroutine(card.TurnRoutine);
            card.TurnRoutine = StartCoroutine(WiggleRoutine(card));
        }

        /// <summary>The right card grows and glows, and the other cards step back.</summary>
        public void CelebrateCard(int number)
        {
            Card card = Find(number);
            if (card == null || !isActiveAndEnabled) return;

            Sfx.Play("ui.card_right");
            foreach (Card other in cards)
            {
                if (other == null || !other.Rect.gameObject.activeSelf) continue;

                other.Button.interactable = false;
                if (other != card) other.Face.color = Dimmed(other.Face.color);
            }

            card.Glow.enabled = true;
            StartScale(card, CelebrateRoutine(card));
        }

        /// <summary>A card keeps pulsing until the lesson asks it to stop. Shows a stuck child which one to tap.</summary>
        public void PulseCard(int number)
        {
            Card card = Find(number);
            if (card == null || !isActiveAndEnabled) return;

            StartScale(card, PulseRoutine(card));
        }

        public void StopPulse()
        {
            foreach (Card card in cards)
            {
                if (card == null || card.ScaleRoutine == null || !card.Rect.gameObject.activeSelf) continue;

                StopCoroutine(card.ScaleRoutine);
                card.ScaleRoutine = null;
                card.Rect.localScale = Vector3.one;
            }
        }

        /// <summary>Fades a wrong card out so fewer wrong choices are left. It can not be tapped any more.</summary>
        public void DimCard(int number)
        {
            Card card = Find(number);
            if (card == null) return;

            card.Button.interactable = false;
            card.Face.color = Dimmed(card.Face.color);
            card.Label.color = Dimmed(card.Label.color);
        }

        public void HideCards()
        {
            IsShowingCards = false;
            onPicked = null;
            StopAllCoroutines();
            ResetCardRoutines();

            foreach (Card card in cards)
                if (card != null) card.Rect.gameObject.SetActive(false);
        }

        /// <summary>Shows or hides the joystick and the action button. The star jar is not part of it.</summary>
        public void SetGameplayHudVisible(bool visible)
        {
            if (gameplayHud == null) return;

            foreach (GameObject part in gameplayHud)
                if (part != null) part.SetActive(visible);
        }

        // ----- Building --------------------------------------------------------------------------

        void BuildCards()
        {
            if (built) return;

            built = true;
            for (int i = 0; i < CardSlots; i++)
                cards[i] = BuildCard(i);
        }

        Card BuildCard(int index)
        {
            var go = new GameObject("NumberCard" + (index + 1), typeof(RectTransform), typeof(Button));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * cardSize;

            // Children draw in order: halo at the back, then the coloured face, then the digit.
            var card = new Card { Rect = rect };
            card.Glow = BuildGlow(rect);
            card.Face = BuildFace(rect, CardColors[index]);

            card.Button = go.GetComponent<Button>();
            card.Button.targetGraphic = card.Face;
            card.Button.transition = Selectable.Transition.None;
            card.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            card.Button.onClick.AddListener(() => OnCardClicked(card));

            card.Label = BuildLabel(rect);
            return card;
        }

        /// <summary>The round coloured part of the card, with a soft drop shadow. This is what a finger hits.</summary>
        Image BuildFace(RectTransform card, Color color)
        {
            var go = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(Shadow));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(card, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var face = go.GetComponent<Image>();
            face.sprite = cardSprite;
            face.color = color;
            face.raycastTarget = true;

            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
            shadow.effectDistance = new Vector2(0f, -14f);
            return face;
        }

        /// <summary>A soft golden halo behind the card, only visible when the card is celebrated.</summary>
        Image BuildGlow(RectTransform card)
        {
            var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(card, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * -40f;
            rect.offsetMax = Vector2.one * 40f;

            var glow = go.GetComponent<Image>();
            glow.sprite = cardSprite;
            glow.color = new Color(1f, 0.95f, 0.5f, 0.85f);
            glow.raycastTarget = false;
            glow.enabled = false;
            return glow;
        }

        Text BuildLabel(RectTransform card)
        {
            var go = new GameObject("Digit", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(card, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = DigitFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            outline.effectDistance = new Vector2(5f, -5f);
            return text;
        }

        // ----- Helpers ---------------------------------------------------------------------------

        void OnCardClicked(Card card)
        {
            if (!IsShowingCards || !card.Button.interactable) return;

            onPicked?.Invoke(card.Number);
        }

        Vector2 CardPosition(int index, int count)
        {
            float x = (index - (count - 1) * 0.5f) * (cardSize + cardGap);
            return new Vector2(x, bottomMargin + cardSize * 0.5f);
        }

        Card Find(int number)
        {
            foreach (Card card in cards)
                if (card != null && card.Rect.gameObject.activeSelf && card.Number == number) return card;
            return null;
        }

        void ResetCardRoutines()
        {
            foreach (Card card in cards)
            {
                if (card == null) continue;

                card.ScaleRoutine = null;
                card.TurnRoutine = null;
                card.Rect.localScale = Vector3.one;
                card.Rect.localRotation = Quaternion.identity;
                card.Label.color = Color.white;
            }
        }

        void StartScale(Card card, IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;

            if (card.ScaleRoutine != null) StopCoroutine(card.ScaleRoutine);
            card.ScaleRoutine = StartCoroutine(routine);
        }

        static Color Dimmed(Color color)
        {
            color.a = DimmedAlpha;
            return color;
        }

        // ----- Animations ------------------------------------------------------------------------

        IEnumerator AppearRoutine(RectTransform rect, float delay)
        {
            rect.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            for (float elapsed = 0f; elapsed < AppearSeconds; elapsed += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * LessonMotion.OutBack(elapsed / AppearSeconds);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        IEnumerator WiggleRoutine(Card card)
        {
            for (float elapsed = 0f; elapsed < WiggleSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float angle = LessonMotion.DampedWobble(elapsed / WiggleSeconds, 3f) * WiggleDegrees;
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            card.Rect.localRotation = Quaternion.identity;
            card.TurnRoutine = null;
        }

        IEnumerator CelebrateRoutine(Card card)
        {
            for (float elapsed = 0f; elapsed < CelebrateSeconds; elapsed += Time.unscaledDeltaTime)
            {
                card.Rect.localScale = Vector3.one * Mathf.Lerp(1f, CelebrateScale, LessonMotion.OutBack(elapsed / CelebrateSeconds));
                yield return null;
            }

            // Then it keeps breathing gently, with its halo, until the cards go away.
            while (true)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed * 0.6f);
                card.Rect.localScale = Vector3.one * (CelebrateScale + beat * 0.05f);
                yield return null;
            }
        }

        IEnumerator PulseRoutine(Card card)
        {
            while (true)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
                card.Rect.localScale = Vector3.one * (1f + beat * PulseAmount);
                yield return null;
            }
        }
    }
}
