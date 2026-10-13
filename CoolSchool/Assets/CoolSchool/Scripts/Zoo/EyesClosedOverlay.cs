using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Lessons;
using CoolSchool.Parent;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// "Eyes closed" for the counting: a full-screen dark veil that fades in over the whole screen (so he does not see the animals
    /// run off to hide), with a big white digit in the middle for the number being counted. Two soft lighter blobs left and right
    /// of the digit make it read as eyes covered by hands with a gap between the fingers, not as a black screen.
    /// It never takes touches (so the back button and the parent gear keep working).
    /// </summary>
    public class EyesClosedOverlay : MonoBehaviour
    {
        [Tooltip("The dark panel and everything on it. Alpha 0 when open, ClosedAlpha when closed.")]
        [SerializeField] CanvasGroup group;
        [Tooltip("The big digit in the middle: Inter-Bold, white. Cleared while open.")]
        [SerializeField] Text digit;

        const float ClosedAlpha = 0.93f;
        const float FadeSeconds = 0.4f;
        const float DigitPopSeconds = 0.3f;
        // The peek holes: soft blobs either side of the digit, in canvas units (a 1920x1080 screen).
        const float HoleOffsetX = 330f;
        const float HoleOffsetY = 40f;
        const float HoleWidth = 420f;
        const float HoleHeight = 340f;
        static readonly Color HoleColor = new Color(1f, 0.93f, 0.8f, 0.16f);

        static Sprite softSprite;

        Coroutine fadeRoutine;
        Coroutine popRoutine;
        Vector3 digitBaseScale = Vector3.one;
        bool captured;

        /// <summary>True from Close until Open (even while the veil is still fading in or out).</summary>
        public bool IsClosed { get; private set; }

        void Awake()
        {
            Capture();
            SetAlpha(0f);
            if (digit != null) digit.text = string.Empty;
        }

        void Capture()
        {
            if (captured) return;

            captured = true;
            if (group != null)
            {
                // Never in the way of a tap: the back button and the parent gear sit under or beside the veil.
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (digit != null)
            {
                digitBaseScale = digit.rectTransform.localScale;
                if (digit.GetComponent<Shadow>() == null)
                {
                    var shadow = digit.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                    shadow.effectDistance = new Vector2(6f, -6f);
                }
            }
            CreatePeekHoles();
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Fades the veil in over 0.4 s.</summary>
        public void Close()
        {
            Capture();
            IsClosed = true;
            SitBelowTopHud();
            FadeTo(ClosedAlpha);
        }

        /// <summary>
        /// HUD built after this overlay (star jar, toy room, sticker board) would draw over the veil. So the veil moves to just below
        /// the first of the parent gear, the quit button or prompt and the present popup, or to the very top when none is found.
        /// </summary>
        void SitBelowTopHud()
        {
            Transform parent = transform.parent;
            if (parent == null) return;

            int lowest = int.MaxValue;
            foreach (Component top in FindTopHud(parent))
            {
                // The child of our parent that holds this part (the part may be nested deeper).
                Transform branch = top.transform;
                while (branch != null && branch.parent != parent) branch = branch.parent;
                if (branch == null || branch == transform) continue;

                lowest = Mathf.Min(lowest, branch.GetSiblingIndex());
            }

            if (lowest == int.MaxValue) transform.SetAsLastSibling();
            else transform.SetSiblingIndex(transform.GetSiblingIndex() < lowest ? lowest - 1 : lowest);
        }

        static List<Component> FindTopHud(Transform parent)
        {
            var found = new List<Component>();
            found.AddRange(parent.GetComponentsInChildren<ParentAreaButton>(true));
            found.AddRange(parent.GetComponentsInChildren<QuitButton>(true));
            found.AddRange(parent.GetComponentsInChildren<QuitPrompt>(true));
            found.AddRange(parent.GetComponentsInChildren<PresentPopup>(true));
            return found;
        }

        /// <summary>Shows the number being counted, with a pop.</summary>
        public void ShowDigit(int number)
        {
            Capture();
            if (digit == null) return;

            digit.text = number.ToString();
            if (!isActiveAndEnabled)
            {
                digit.rectTransform.localScale = digitBaseScale;
                return;
            }
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopDigit());
        }

        /// <summary>Clears the digit and fades the veil out over 0.4 s.</summary>
        public void Open()
        {
            Capture();
            IsClosed = false;
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                popRoutine = null;
            }
            if (digit != null)
            {
                digit.text = string.Empty;
                digit.rectTransform.localScale = digitBaseScale;
            }
            FadeTo(0f);
        }

        // ----- Animation ---------------------------------------------------------------------------

        void FadeTo(float alpha)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;

            if (!isActiveAndEnabled)
            {
                SetAlpha(alpha);
                return;
            }
            fadeRoutine = StartCoroutine(Fade(alpha));
        }

        IEnumerator Fade(float target)
        {
            float from = group != null ? group.alpha : 0f;
            for (float elapsed = 0f; elapsed < FadeSeconds; elapsed += Time.deltaTime)
            {
                SetAlpha(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / FadeSeconds)));
                yield return null;
            }

            SetAlpha(target);
            fadeRoutine = null;
        }

        IEnumerator PopDigit()
        {
            for (float elapsed = 0f; elapsed < DigitPopSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / DigitPopSeconds;
                digit.rectTransform.localScale = digitBaseScale * Mathf.Lerp(0.4f, 1f, LessonMotion.OutBack(t));
                yield return null;
            }

            digit.rectTransform.localScale = digitBaseScale;
            popRoutine = null;
        }

        void SetAlpha(float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        void OnDisable()
        {
            // Switched off mid-fade: settle where the fade was going, so the veil is never stuck half way.
            fadeRoutine = null;
            popRoutine = null;
            SetAlpha(IsClosed ? ClosedAlpha : 0f);
            if (digit != null) digit.rectTransform.localScale = digitBaseScale;
        }

        // ----- The peek holes ----------------------------------------------------------------------

        /// <summary>Two soft lighter blobs left and right of the middle, made here so the builder need not supply a sprite.</summary>
        void CreatePeekHoles()
        {
            if (group == null) return;

            Transform parent = group.transform;
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject(side == 0 ? "PeekHoleLeft" : "PeekHoleRight", typeof(RectTransform), typeof(Image));
                go.layer = parent.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(side == 0 ? -HoleOffsetX : HoleOffsetX, HoleOffsetY);
                rect.sizeDelta = new Vector2(HoleWidth, HoleHeight);

                var image = go.GetComponent<Image>();
                image.sprite = SoftSprite();
                image.color = HoleColor;
                image.raycastTarget = false;

                // Under the digit when the digit is on the same level, so the number stays on top.
                if (digit != null && digit.transform.parent == parent)
                    rect.SetSiblingIndex(digit.transform.GetSiblingIndex());
            }
        }

        /// <summary>A white blob that fades smoothly to nothing at its edge.</summary>
        static Sprite SoftSprite()
        {
            if (softSprite != null) return softSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "EyesClosedSoftBlob",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - distance)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            softSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return softSprite;
        }
    }
}
