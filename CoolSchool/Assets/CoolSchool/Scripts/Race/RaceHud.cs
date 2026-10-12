using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>
    /// What is on the screen during the soapbox race: the big countdown digit, the coin counter, the place medal, a strip
    /// that shows how far each cart is, the number or letter of the gate that was just announced, and the two arrows that
    /// show where to tap. No words: only digits and letter glyphs. Everything is animated from Update, so it also works when
    /// the HUD is switched on and off. The pictures themselves are made by the builder.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        [Tooltip("Everything, hidden outside the race. Must be a child of this object (not this object itself).")]
        [SerializeField] GameObject root;
        [SerializeField] Text countdownText;
        [Tooltip("Top left, right of the parent gear: the coin icon and the digits.")]
        [SerializeField] RectTransform coinCounter;
        [SerializeField] Text coinText;
        [Tooltip("A circle tinted gold, silver or bronze.")]
        [SerializeField] Image placeBadge;
        [SerializeField] Text placeText;
        [Tooltip("The strip at the top. The dots must be children of it.")]
        [SerializeField] RectTransform progressBar;
        [Tooltip("3: the player first (bigger), then the rivals.")]
        [SerializeField] RectTransform[] progressDots;
        [SerializeField] RectTransform targetBubble;
        [SerializeField] Text targetText;
        [SerializeField] Image leftArrow;
        [SerializeField] Image rightArrow;

        static readonly Color GoldMedal = new Color(1f, 0.82f, 0.2f);
        static readonly Color SilverMedal = new Color(0.78f, 0.8f, 0.86f);
        static readonly Color BronzeMedal = new Color(0.8f, 0.5f, 0.25f);
        static readonly Color CoinGold = new Color(1f, 0.8f, 0.15f);

        const float CountdownSeconds = 1f;
        const float CountdownPopSeconds = 0.3f;
        const float PopSeconds = 0.3f;
        const float CoinFlightSeconds = 0.7f;
        const float CoinFlightGap = 0.08f;
        const float CoinFlightArc = 140f;
        const float FlyingCoinSize = 56f;
        const float ArrowPulseHertz = 0.8f;

        sealed class FlyingCoin
        {
            public RectTransform rect;
            public Vector2 start;
            public Vector2 control;
            public float delay;
            public float time;
        }

        readonly List<FlyingCoin> flying = new List<FlyingCoin>();
        Sprite coinSprite;
        bool coinSpriteSearched;

        float countdownT = -1f;
        float coinPopT = -1f;
        float placePopT = -1f;
        float targetPopT = -1f;
        bool arrowsOn;
        int shownPlace;
        Vector3 coinBaseScale = Vector3.one;
        Vector3 placeBaseScale = Vector3.one;
        Vector3 targetBaseScale = Vector3.one;
        int targetBaseFontSize;
        Vector3 countdownBaseScale = Vector3.one;

        void Awake()
        {
            if (coinCounter != null) coinBaseScale = coinCounter.localScale;
            if (placeBadge != null) placeBaseScale = placeBadge.rectTransform.localScale;
            if (targetBubble != null) targetBaseScale = targetBubble.localScale;
            if (targetText != null) targetBaseFontSize = targetText.fontSize;
            if (countdownText != null) countdownBaseScale = countdownText.rectTransform.localScale;

            // The HUD never blocks a tap: the race reads taps on the whole screen itself.
            if (root != null && root != gameObject)
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;

            // The player's dot is drawn on top of the rivals'.
            if (progressDots != null && progressDots.Length > 0 && progressDots[0] != null) progressDots[0].SetAsLastSibling();
        }

        // ----- Public ------------------------------------------------------------------------------

        public void Show(bool show)
        {
            if (root != null) root.SetActive(show);
            if (show) return;

            // Hidden: forget what was on the screen.
            countdownT = -1f;
            coinPopT = -1f;
            placePopT = -1f;
            targetPopT = -1f;
            arrowsOn = false;
            shownPlace = 0;
            ClearFlyingCoins();
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (targetBubble != null) targetBubble.gameObject.SetActive(false);
            SetArrowsActive(false);
            RestoreScales();
        }

        /// <summary>A big digit that pops in (3, 2, 1). Zero hides it.</summary>
        public void Countdown(int n)
        {
            if (countdownText == null) return;

            if (n <= 0)
            {
                countdownT = -1f;
                countdownText.gameObject.SetActive(false);
                return;
            }

            countdownText.text = n.ToString();
            countdownText.color = n == 3 ? new Color(1f, 0.35f, 0.3f) : n == 2 ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.9f, 0.4f);
            countdownText.gameObject.SetActive(true);
            countdownT = 0f;
        }

        public void SetCoins(int coins, bool pop)
        {
            if (coinText != null) coinText.text = coins.ToString();
            if (pop) coinPopT = 0f;
        }

        /// <summary>Little coins arc from a spot in the world into the coin counter.</summary>
        public void FlyCoinsFromWorld(Vector3 world, int count)
        {
            if (coinCounter == null || root == null || !root.activeInHierarchy) return;

            Camera camera = Camera.main;
            Vector2 start = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (camera != null)
            {
                Vector3 point = camera.WorldToScreenPoint(world);
                if (point.z > 0f) start = point;
            }

            Vector2 end = coinCounter.position;
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = CreateFlyingCoin();
                Vector2 begin = start + Random.insideUnitCircle * 30f;
                var coin = new FlyingCoin
                {
                    rect = rect,
                    start = begin,
                    control = (begin + end) * 0.5f + Vector2.up * CoinFlightArc + Random.insideUnitCircle * 40f,
                    delay = i * CoinFlightGap,
                    time = 0f,
                };
                rect.position = begin;
                rect.gameObject.SetActive(false);
                flying.Add(coin);
            }
        }

        /// <summary>The medal-coloured digit for the current place (1 to 3).</summary>
        public void SetPlace(int place)
        {
            place = Mathf.Clamp(place, 1, 3);
            if (placeText != null) placeText.text = place.ToString();
            if (placeBadge != null) placeBadge.color = place == 1 ? GoldMedal : place == 2 ? SilverMedal : BronzeMedal;
            if (place != shownPlace && shownPlace != 0) placePopT = 0f;
            shownPlace = place;
        }

        /// <summary>How far each cart is along the street (0 to 1).</summary>
        public void SetProgress(float player01, float rivalA01, float rivalB01)
        {
            if (progressDots == null) return;

            PlaceDot(0, player01);
            PlaceDot(1, rivalA01);
            PlaceDot(2, rivalB01);
        }

        /// <summary>The number or letter of the announced gate, big at the top. Null or empty hides it.</summary>
        public void ShowTarget(string glyph)
        {
            if (targetBubble == null) return;

            bool show = !string.IsNullOrEmpty(glyph);
            targetBubble.gameObject.SetActive(show);
            if (!show) return;

            if (targetText != null)
            {
                // "7→?" (which number comes after 7): a plain space if the font has no arrow.
                if (glyph.IndexOf('→') >= 0 && targetText.font != null && !targetText.font.HasCharacter('→'))
                    glyph = glyph.Replace("→", " ");

                // Two digits and "12→?" are wider than one, so the type gets smaller to stay inside the bubble.
                float shrink = glyph.Length <= 1 ? 1f : glyph.Length == 2 ? 0.62f : glyph.Length == 3 ? 0.45f : 0.36f;
                if (targetBaseFontSize <= 0) targetBaseFontSize = targetText.fontSize;
                targetText.fontSize = Mathf.RoundToInt(targetBaseFontSize * shrink);
                targetText.text = glyph;
            }
            targetPopT = 0f;
        }

        /// <summary>The two how-to arrows on the left and right halves of the screen.</summary>
        public void ShowArrows(bool show)
        {
            arrowsOn = show;
            SetArrowsActive(show);
        }

        // ----- Animation ---------------------------------------------------------------------------

        void Update()
        {
            if (root != null && !root.activeInHierarchy) return;

            float dt = Time.deltaTime;
            UpdateCountdown(dt);
            UpdatePops(dt);
            UpdateArrows();
            UpdateFlyingCoins(dt);
        }

        void UpdateCountdown(float dt)
        {
            if (countdownT < 0f || countdownText == null) return;

            countdownT += dt / CountdownSeconds;
            if (countdownT >= 1f)
            {
                countdownT = -1f;
                countdownText.gameObject.SetActive(false);
                return;
            }

            // Big at first, settling to full size; fading out in the last quarter of the second.
            float settle = Mathf.Clamp01(countdownT * CountdownSeconds / CountdownPopSeconds);
            float scale = Mathf.Lerp(1.9f, 1f, 1f - (1f - settle) * (1f - settle));
            countdownText.rectTransform.localScale = countdownBaseScale * scale;

            Color color = countdownText.color;
            color.a = countdownT > 0.75f ? Mathf.Lerp(1f, 0.2f, (countdownT - 0.75f) / 0.25f) : 1f;
            countdownText.color = color;
        }

        void UpdatePops(float dt)
        {
            if (coinCounter != null) UpdatePop(ref coinPopT, coinCounter, coinBaseScale, 1.25f, dt);
            if (placeBadge != null) UpdatePop(ref placePopT, placeBadge.rectTransform, placeBaseScale, 1.4f, dt);
            if (targetBubble != null) UpdatePop(ref targetPopT, targetBubble, targetBaseScale, 1.25f, dt);
        }

        static void UpdatePop(ref float time, RectTransform rect, Vector3 baseScale, float peak, float dt)
        {
            if (time < 0f) return;

            time += dt / PopSeconds;
            if (time >= 1f)
            {
                time = -1f;
                rect.localScale = baseScale;
                return;
            }
            rect.localScale = baseScale * (1f + (peak - 1f) * Mathf.Sin(time * Mathf.PI));
        }

        void UpdateArrows()
        {
            if (!arrowsOn) return;

            float alpha = 0.5f + 0.35f * Mathf.Sin(Time.time * ArrowPulseHertz * Mathf.PI * 2f);
            SetAlpha(leftArrow, alpha);
            SetAlpha(rightArrow, alpha);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        void SetArrowsActive(bool active)
        {
            if (leftArrow != null) leftArrow.gameObject.SetActive(active);
            if (rightArrow != null) rightArrow.gameObject.SetActive(active);
        }

        void PlaceDot(int index, float progress01)
        {
            if (index >= progressDots.Length) return;

            RectTransform dot = progressDots[index];
            if (dot == null) return;

            // Anchored along the strip, so it works at any strip width.
            float t = Mathf.Clamp01(progress01);
            dot.anchorMin = new Vector2(t, 0.5f);
            dot.anchorMax = new Vector2(t, 0.5f);
            dot.anchoredPosition = Vector2.zero;
        }

        // ----- Flying coins ------------------------------------------------------------------------

        void UpdateFlyingCoins(float dt)
        {
            if (flying.Count == 0 || coinCounter == null) return;

            Vector2 end = coinCounter.position;
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                FlyingCoin coin = flying[i];
                if (coin.rect == null)
                {
                    flying.RemoveAt(i);
                    continue;
                }

                coin.time += dt;
                if (coin.time < coin.delay) continue;

                if (!coin.rect.gameObject.activeSelf) coin.rect.gameObject.SetActive(true);

                float k = Mathf.Clamp01((coin.time - coin.delay) / CoinFlightSeconds);
                float eased = k * k * (3f - 2f * k);
                // A curved path: a quadratic Bezier through the control point.
                Vector2 a = Vector2.Lerp(coin.start, coin.control, eased);
                Vector2 b = Vector2.Lerp(coin.control, end, eased);
                coin.rect.position = Vector2.Lerp(a, b, eased);
                coin.rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);

                if (k < 1f) continue;

                Destroy(coin.rect.gameObject);
                flying.RemoveAt(i);
                coinPopT = 0f;
            }
        }

        RectTransform CreateFlyingCoin()
        {
            var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * FlyingCoinSize;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = FindCoinSprite();
            image.color = image.sprite != null ? Color.white : CoinGold;
            return rect;
        }

        /// <summary>The picture of the coin in the counter, so the flying coins look the same.</summary>
        Sprite FindCoinSprite()
        {
            if (coinSpriteSearched) return coinSprite;
            coinSpriteSearched = true;

            if (coinCounter == null) return null;

            Image fallback = null;
            foreach (Image image in coinCounter.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                if (image.name.ToLowerInvariant().Contains("coin"))
                {
                    coinSprite = image.sprite;
                    return coinSprite;
                }
                if (fallback == null) fallback = image;
            }
            if (fallback != null) coinSprite = fallback.sprite;
            return coinSprite;
        }

        void ClearFlyingCoins()
        {
            foreach (FlyingCoin coin in flying)
                if (coin.rect != null) Destroy(coin.rect.gameObject);
            flying.Clear();
        }

        void RestoreScales()
        {
            if (coinCounter != null) coinCounter.localScale = coinBaseScale;
            if (placeBadge != null) placeBadge.rectTransform.localScale = placeBaseScale;
            if (targetBubble != null) targetBubble.localScale = targetBaseScale;
            if (countdownText != null) countdownText.rectTransform.localScale = countdownBaseScale;
        }
    }
}
