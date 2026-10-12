using System;
using System.Collections;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoolSchool.Parent
{
    /// <summary>
    /// The parent screen: the child's name and a difficulty slider per subject. Written text, because parents read it.
    /// While it is open the game is frozen (time scale 0, audio paused) and ParentGate.IsOpen is set so gameplay scripts
    /// ignore input; everything is put back exactly on close. The sliders only set the current level; the lessons keep
    /// adapting from there. All animation here runs on unscaled time.
    /// </summary>
    public class ParentPanel : MonoBehaviour
    {
        /// <summary>One subject's slider. The shop level is stored in its own field, not as a skill.</summary>
        [Serializable]
        public class LevelRow
        {
            [Tooltip("The skill key in the progress file (\"numbers\", \"letters\"), or \"shopLevel\" for the ice cream shop.")]
            public string skill;
            [Tooltip("The key of the row's name in ParentStrings.")]
            public string labelKey;
            public int maxLevel = 5;
            public Text label;
            public Text caption;
            public Text valueText;
            public Slider slider;
        }

        public const string ShopSkill = "shopLevel";

        [Tooltip("Everything that is shown while the panel is open (dimmed backdrop and window). Switched off when closed.")]
        [SerializeField] GameObject content;
        [SerializeField] CanvasGroup contentGroup;
        [SerializeField] RectTransform window;

        [Header("Texts")]
        [SerializeField] Text titleText;
        [SerializeField] Text nameLabel;
        [SerializeField] Text nameHint;
        [SerializeField] Text difficultyLabel;
        [SerializeField] Text closeLabel;

        [Header("Controls")]
        [SerializeField] InputField nameField;
        [SerializeField] Text namePlaceholder;
        [SerializeField] Button closeButton;
        [SerializeField] LevelRow[] rows;

        const float PopSeconds = 0.18f;
        const float PopStartScale = 0.92f;

        float savedTimeScale = 1f;
        bool savedAudioPause;
        Coroutine popRoutine;

        public bool IsOpen => ParentGate.IsOpen;

        void Awake()
        {
            if (nameField != null)
            {
                nameField.onValidateInput = ValidateNameCharacter;
                nameField.onEndEdit.AddListener(_ => CommitName());
            }
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (rows != null)
            {
                foreach (LevelRow row in rows)
                {
                    LevelRow captured = row;
                    if (captured.slider != null) captured.slider.onValueChanged.AddListener(value => OnSliderChanged(captured, value));
                }
            }

            ApplyTexts();
            if (content != null) content.SetActive(false);
        }

        void Update()
        {
            // The Android back button arrives as Escape.
            if (!IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        void OnDisable()
        {
            // If this object is switched off while open, put the game back first.
            if (IsOpen) Close();
        }

        void OnDestroy()
        {
            if (IsOpen) Close();
        }

        // ----- Open and close --------------------------------------------------------------------

        public void Open()
        {
            if (IsOpen) return;

            savedTimeScale = Time.timeScale;
            savedAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ParentGate.IsOpen = true;

            ApplyTexts();
            Refresh();
            if (content != null) content.SetActive(true);

            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopIn());
        }

        public void Close()
        {
            if (!IsOpen) return;

            CommitName();
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                popRoutine = null;
            }

            // Take the focus off the name field so a phone's keyboard goes away.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (content != null) content.SetActive(false);

            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
            ParentGate.IsOpen = false;
        }

        IEnumerator PopIn()
        {
            for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / PopSeconds);
                if (contentGroup != null) contentGroup.alpha = t;
                if (window != null) window.localScale = Vector3.one * Mathf.Lerp(PopStartScale, 1f, t);
                yield return null;
            }

            if (contentGroup != null) contentGroup.alpha = 1f;
            if (window != null) window.localScale = Vector3.one;
            popRoutine = null;
        }

        // ----- Showing and storing values --------------------------------------------------------

        void ApplyTexts()
        {
            SetText(titleText, ParentStrings.Title);
            SetText(nameLabel, ParentStrings.NameLabel);
            SetText(nameHint, ParentStrings.NameHint);
            SetText(difficultyLabel, ParentStrings.Difficulty);
            SetText(closeLabel, ParentStrings.Close);
            SetText(namePlaceholder, ParentStrings.NamePlaceholder);

            if (rows == null) return;
            foreach (LevelRow row in rows)
            {
                SetText(row.label, row.labelKey);
                SetText(row.caption, ParentStrings.LevelCaption);
            }
        }

        static void SetText(Text text, string key)
        {
            if (text != null) text.text = ParentStrings.Get(key);
        }

        /// <summary>Reads the stored values into the controls. Done at every open, because the game may have changed a level meanwhile.</summary>
        void Refresh()
        {
            if (nameField != null) nameField.SetTextWithoutNotify(ProgressStore.Current.childName ?? "");

            if (rows == null) return;
            foreach (LevelRow row in rows)
            {
                if (row.slider == null) continue;

                row.slider.wholeNumbers = true;
                row.slider.minValue = 1f;
                row.slider.maxValue = Mathf.Max(2, row.maxLevel);

                int level = Mathf.Clamp(ReadLevel(row.skill), 1, Mathf.Max(2, row.maxLevel));
                row.slider.SetValueWithoutNotify(level);
                ShowLevel(row, level);
            }
        }

        static void ShowLevel(LevelRow row, int level)
        {
            if (row.valueText != null) row.valueText.text = level.ToString();
        }

        void OnSliderChanged(LevelRow row, float value)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(value), 1, Mathf.Max(2, row.maxLevel));
            WriteLevel(row.skill, level);
            ShowLevel(row, level);
        }

        static int ReadLevel(string skill)
        {
            return skill == ShopSkill ? ProgressStore.ShopLevel : ProgressStore.Current.GetLevel(skill);
        }

        static void WriteLevel(string skill, int level)
        {
            if (skill == ShopSkill)
            {
                ProgressStore.ShopLevel = level;
                return;
            }

            ProgressStore.Current.SetLevel(skill, level);
            ProgressStore.Save();
        }

        // ----- The name --------------------------------------------------------------------------

        /// <summary>Keeps typing to the letters a to z: capitals become lowercase, everything else is ignored.</summary>
        static char ValidateNameCharacter(string text, int charIndex, char added)
        {
            char lower = char.ToLowerInvariant(added);
            return lower >= 'a' && lower <= 'z' ? lower : '\0';
        }

        void CommitName()
        {
            if (nameField == null) return;

            string name = ProgressStore.NormalizeChildName(nameField.text);
            if (name != (ProgressStore.Current.childName ?? "")) ProgressStore.SetChildName(name);
            nameField.SetTextWithoutNotify(name);
        }
    }
}
