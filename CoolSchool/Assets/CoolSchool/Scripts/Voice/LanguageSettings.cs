using System;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>
    /// The chosen game language, remembered between sessions. Dutch unless a parent picks otherwise.
    /// </summary>
    public static class LanguageSettings
    {
        const string PrefsKey = "cs.language";

        static GameLanguage? current;

        /// <summary>Raised after the language has changed.</summary>
        public static event Action Changed;

        public static GameLanguage Current
        {
            get
            {
                if (current == null)
                    current = (GameLanguage)PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.Dutch);
                return current.Value;
            }
            set
            {
                if (current == value) return;

                current = value;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache() => current = null;
    }
}
