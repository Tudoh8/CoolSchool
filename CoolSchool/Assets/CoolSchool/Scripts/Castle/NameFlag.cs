using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Castle
{
    /// <summary>
    /// His name on the castle's flag. The flag stands by the celebration spot with a gold shield on it. After a visit's fight the
    /// queen gives it to him: he picks the first letter of his name from the cards, the card flies to the banner and the whole name
    /// writes itself there in chalk-dot letters. Once it is done the name stays on the flag (also in free play, and it follows a
    /// change of the name in the parent area).
    /// </summary>
    public class NameFlag : MonoBehaviour
    {
        [Tooltip("An empty rect in the middle of the banner's world-space canvas, where the letters go.")]
        [SerializeField] RectTransform letterRow;
        [Tooltip("The gold shield, shown until the name is on the banner.")]
        [SerializeField] GameObject emblem;
        [SerializeField] LessonUI lessonUI;
        [SerializeField] LetterSet letters;
        [SerializeField] CastleQueen queen;
        [Tooltip("A round white dot picture for the chalk letters.")]
        [SerializeField] Sprite dot;
        [Tooltip("What sways when the name is done. Optional: without it the banner is the parent of the canvas.")]
        [SerializeField] Transform banner;

        const int MaxLetters = 8;
        const float DotFraction = 0.17f;
        const float PopSeconds = 0.3f;
        const float LetterGapSeconds = 0.4f;
        const float CardFlySeconds = 0.6f;
        const float SwaySeconds = 3f;
        const float SwayDegrees = 9f;

        static readonly Color ChalkWhite = new Color(1f, 1f, 1f, 1f);

        readonly List<GameObject> letterObjects = new List<GameObject>();
        string shownName = "";
        bool running;
        Quaternion swayRest;
        bool haveSwayRest;

        /// <summary>True when his name has at least one letter that can be drawn.</summary>
        public bool CanRun => DrawableName().Count > 0;

        void OnEnable()
        {
            ProgressStore.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= Refresh;
        }

        /// <summary>The card question for the first letter, then the name writes itself on the banner.</summary>
        public IEnumerator Run(int level, MomentResult result)
        {
            List<char> name = DrawableName();
            if (name.Count == 0 || lessonUI == null || letters == null || letterRow == null) yield break;

            running = true;
            ClearLetters();
            if (emblem != null) emblem.SetActive(true);

            yield return CastleMotion.SayAndWait("castle.flag");

            LetterEntry first = letters.Find(name[0]);
            var soundKey = CastleMotion.SoundKeyOf(first);
            char picked = '\0';
            yield return CardQuestion.AskLetter(lessonUI, letters, name[0], level <= 1 ? 2 : 3,
                () => VoicePlayer.Say(soundKey), result, true, c => picked = c, keepCards: true);

            // The right card flies to the banner.
            bool landed = false;
            Camera view = Camera.main;
            if (view != null)
            {
                Vector3 point = view.WorldToScreenPoint(letterRow.position);
                if (point.z > 0f)
                {
                    lessonUI.FlyCardTo(name[0], point, CardFlySeconds, () => landed = true);
                    float giveUp = Time.time + CardFlySeconds + 1f;
                    while (!landed && Time.time < giveUp) yield return null;
                }
            }
            lessonUI.HideCards();
            yield return new WaitForSeconds(0.2f);

            // The whole name, letter by letter.
            if (emblem != null) emblem.SetActive(false);
            yield return WriteName(name, true);

            Sfx.Play("ui.card_right");
            VoicePlayer.Say("castle.flag_done", true);
            this.Fork(Sway());

            ProgressStore.Current.castleNameFlagDone = true;
            running = false;
            shownName = new string(name.ToArray());
            ProgressStore.Save();
            yield return CastleMotion.WaitForVoice(5f);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>The flag shows what is saved: his name when it is done, the gold shield when not. Cards are hidden.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            running = false;
            RestoreSway();
            if (lessonUI != null && lessonUI.IsShowingCards) lessonUI.HideCards();
            Refresh();
        }

        // ----- Showing the name ------------------------------------------------------------------

        /// <summary>Makes the banner match the saved progress (called when the progress changes). Not while the moment writes it.</summary>
        void Refresh()
        {
            if (running) return;

            List<char> name = DrawableName();
            string wanted = ProgressStore.Current.castleNameFlagDone ? new string(name.ToArray()) : "";
            bool emblemRight = emblem == null || emblem.activeSelf == (wanted.Length == 0);
            if (wanted == shownName && emblemRight) return;

            ShowNameAtOnce(wanted);
        }

        void ShowNameAtOnce(string text)
        {
            ClearLetters();
            shownName = text;
            if (emblem != null) emblem.SetActive(text.Length == 0);
            if (text.Length == 0 || letterRow == null || letters == null) return;

            List<char> name = DrawableName();
            for (int i = 0; i < name.Count; i++)
                CreateLetter(name, i);
        }

        /// <summary>Makes one letter of the row as chalk dots, in its place. Returns its holder, or null for a letter that can not be drawn.</summary>
        RectTransform CreateLetter(List<char> name, int index)
        {
            float width = Mathf.Max(100f, letterRow.rect.width);
            float height = Mathf.Max(60f, letterRow.rect.height);
            int count = name.Count;
            float pitch = Mathf.Min(width / count, height * 1.1f);
            float side = Mathf.Min(pitch * 0.9f, height * 0.9f);

            LetterEntry entry = letters.Find(name[index]);
            if (entry == null || !entry.HasStrokes) return null;

            var holder = new GameObject("Letter_" + name[index], typeof(RectTransform));
            holder.layer = letterRow.gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(letterRow, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * pitch, 0f);
            rect.sizeDelta = Vector2.one * side;
            LetterGlyph.Build(rect, entry, side, side * DotFraction, ChalkWhite, dot);
            letterObjects.Add(holder);
            return rect;
        }

        /// <summary>Writes the name letter by letter: each pops in with a soft sound, with a pause between them.</summary>
        IEnumerator WriteName(List<char> name, bool animated)
        {
            for (int i = 0; i < name.Count; i++)
            {
                RectTransform rect = CreateLetter(name, i);
                if (rect == null || !animated) continue;

                Sfx.Play("castle.flag_pop");
                rect.localScale = Vector3.zero;
                for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.deltaTime)
                {
                    if (rect == null) yield break;
                    rect.localScale = Vector3.one * LessonMotion.OutBack(elapsed / PopSeconds);
                    yield return null;
                }
                if (rect != null) rect.localScale = Vector3.one;
                yield return new WaitForSeconds(LetterGapSeconds);
            }
        }

        void ClearLetters()
        {
            foreach (GameObject letter in letterObjects)
                if (letter != null) Destroy(letter);
            letterObjects.Clear();
            shownName = "";
        }

        /// <summary>The first letters of his name that can be drawn, in order (at most eight).</summary>
        List<char> DrawableName()
        {
            var result = new List<char>();
            if (letters == null) return result;

            string name = ProgressStore.NormalizeChildName(ProgressStore.Current.childName);
            foreach (char c in name)
            {
                LetterEntry entry = letters.Find(c);
                if (entry != null && entry.HasStrokes && result.Count < MaxLetters) result.Add(c);
            }
            return result;
        }

        // ----- The banner waves ------------------------------------------------------------------

        Transform SwayTarget()
        {
            if (banner != null) return banner;
            if (letterRow == null) return null;

            Canvas canvas = letterRow.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return canvas.transform.parent != null ? canvas.transform.parent : canvas.transform;
        }

        IEnumerator Sway()
        {
            Transform target = SwayTarget();
            if (target == null) yield break;

            if (!haveSwayRest)
            {
                swayRest = target.localRotation;
                haveSwayRest = true;
            }

            for (float elapsed = 0f; elapsed < SwaySeconds; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / SwaySeconds;
                target.localRotation = swayRest * Quaternion.Euler(0f, Mathf.Sin(elapsed * 4f) * SwayDegrees * fade, 0f);
                yield return null;
            }
            target.localRotation = swayRest;
        }

        void RestoreSway()
        {
            Transform target = SwayTarget();
            if (target != null && haveSwayRest) target.localRotation = swayRest;
        }
    }
}
