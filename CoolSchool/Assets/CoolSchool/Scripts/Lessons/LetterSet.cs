using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>A word that starts with a letter, with the 3D prop that shows what it is. The voice key is letter.word.&lt;word&gt;.</summary>
    [Serializable]
    public class LetterWord
    {
        [Tooltip("Dutch, lowercase, e.g. maan")]
        public string word;
        [Tooltip("Voice group, letter.word.<word>")]
        public string voiceKey;
        public GameObject prefab;
        [Tooltip("Uniform scale that makes the prop about as big as the toys on the table.")]
        public float scale = 1f;
    }

    /// <summary>One stroke of a letter: points in writing order inside a 0 to 1 box, x to the right and y UP.</summary>
    [Serializable]
    public class LetterStroke
    {
        public Vector2[] points = new Vector2[0];
    }

    /// <summary>One letter: its sound, the words that start with it, and the strokes the child traces.</summary>
    [Serializable]
    public class LetterEntry
    {
        [Tooltip("A single lowercase letter a to z. (Unity can not save a char, so it is a string.)")]
        public string letter;
        [Tooltip("Voice group of the letter's sound, letter.sound.<x>")]
        public string soundKey;
        public List<LetterWord> words = new List<LetterWord>();
        [Tooltip("In writing order. The letter is centred in a 0 to 1 box.")]
        public List<LetterStroke> strokes = new List<LetterStroke>();

        public char Char => string.IsNullOrEmpty(letter) ? '\0' : letter[0];

        /// <summary>Words that have a prop to show.</summary>
        public bool HasWords
        {
            get
            {
                foreach (LetterWord word in words)
                    if (word != null && word.prefab != null && !string.IsNullOrEmpty(word.word)) return true;
                return false;
            }
        }

        public bool HasStrokes => strokes != null && strokes.Count > 0;
    }

    /// <summary>All 26 letters for the letters lesson. Filled by the LetterSetSeeder.</summary>
    [CreateAssetMenu(menuName = "CoolSchool/Lessons/Letter Set", fileName = "Letters")]
    public class LetterSet : ScriptableObject
    {
        public List<LetterEntry> letters = new List<LetterEntry>();

        public LetterEntry Find(char letter)
        {
            foreach (LetterEntry entry in letters)
                if (entry != null && entry.Char == letter) return entry;
            return null;
        }

        /// <summary>A random word of this letter that has a prop, preferably not the one to avoid. Null if there is none.</summary>
        public LetterWord PickWord(char letter, LetterWord avoid = null)
        {
            LetterEntry entry = Find(letter);
            if (entry == null) return null;

            var usable = new List<LetterWord>();
            foreach (LetterWord word in entry.words)
                if (word != null && word.prefab != null && word != avoid) usable.Add(word);

            if (usable.Count == 0 && avoid != null && avoid.prefab != null) return avoid;
            return usable.Count > 0 ? usable[UnityEngine.Random.Range(0, usable.Count)] : null;
        }
    }
}
