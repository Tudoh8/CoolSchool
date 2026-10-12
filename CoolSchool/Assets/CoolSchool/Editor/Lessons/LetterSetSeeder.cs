using System.Collections.Generic;
using System.Linq;
using CoolSchool.Lessons;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/CoolSchool/Lessons/Letters.asset: the 26 letters with their sound key, the stroke data
    /// for tracing (LetterStrokeData) and the words with their props (LetterWordTable). Props are found by name and scaled
    /// so the biggest side is about 0.4 m. Words added by hand stay, after the seeded ones.
    /// </summary>
    public static class LetterSetSeeder
    {
        const string LessonsFolder = "Assets/CoolSchool/Lessons";
        public const string AssetPath = LessonsFolder + "/Letters.asset";
        const float PropSize = 0.4f;

        [MenuItem("CoolSchool/Lessons/Seed Letters")]
        static void SeedFromMenu()
        {
            LetterSet set = CreateOrUpdate();
            Selection.activeObject = set;
            int words = set.letters.Sum(entry => entry.words.Count);
            Debug.Log($"CoolSchool: letter set updated, {set.letters.Count} letters and {words} words ({AssetPath}).");
        }

        /// <summary>Makes or refreshes the set and returns it.</summary>
        public static LetterSet CreateOrUpdate()
        {
            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Lessons");

            var set = AssetDatabase.LoadAssetAtPath<LetterSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<LetterSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            var letters = new List<LetterEntry>();
            for (char letter = 'a'; letter <= 'z'; letter++)
            {
                LetterEntry entry = set.letters.Find(e => e != null && e.Char == letter) ?? new LetterEntry { letter = letter.ToString() };
                entry.soundKey = "letter.sound." + letter;
                entry.strokes = LetterStrokeData.Build(letter);
                entry.words = BuildWords(letter, entry.words);
                letters.Add(entry);
            }
            set.letters = letters;

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        static List<LetterWord> BuildWords(char letter, List<LetterWord> existing)
        {
            var result = new List<LetterWord>();
            var seeded = new HashSet<string>();

            foreach (LetterWordTable.Entry seed in LetterWordTable.All)
            {
                if (seed.Letter != letter) continue;

                seeded.Add(seed.Word);
                var reference = new PlaygroundPrototypeBuilder.PrefabRef(seed.Prefab, seed.Pack);
                GameObject prefab = PlaygroundPrototypeBuilder.LoadPrefab(reference);
                if (prefab == null) continue;

                LetterWord word = existing != null ? existing.Find(w => w != null && w.word == seed.Word) : null;
                if (word == null) word = new LetterWord { word = seed.Word };

                word.voiceKey = "letter.word." + seed.Word;
                word.prefab = prefab;
                word.scale = CountableSetSeeder.ScaleForSize(prefab, PropSize);
                result.Add(word);
            }

            // Anything added by hand stays.
            if (existing != null)
                result.AddRange(existing.Where(w => w != null && !seeded.Contains(w.word)));
            return result;
        }
    }
}
