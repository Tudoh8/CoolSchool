using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// A way to see the letters lesson's table without playing it: the editor's preview pictures call StageForPreview to put
    /// the word props of a "what starts with..." question on the table at once. It uses the same SpawnProps as the real exercise.
    /// (The chalk letters on the board are drawn by animations and are not staged.) Nothing here runs in the game.
    /// </summary>
    public partial class LettersLesson
    {
        // Set while the preview stages props: they then appear without their pop-in animation.
        bool stagingForPreview;

        /// <summary>Puts this many word props (words with different first sounds) on the table. Returns false if the letter set has none.</summary>
        public bool StageForPreview(int propCount)
        {
            if (stage == null || stage.TableSurface == null || letterSet == null) return false;

            var words = new List<LetterWord>();
            var usedSounds = new List<char>();
            foreach (LetterEntry entry in letterSet.letters)
            {
                if (words.Count >= propCount) break;
                if (entry == null || !entry.HasWords) continue;

                char sound = LetterCurriculum.SoundGroup(entry.Char);
                if (usedSounds.Contains(sound)) continue;

                LetterWord word = letterSet.PickWord(entry.Char);
                if (word == null) continue;

                words.Add(word);
                usedSounds.Add(sound);
            }
            if (words.Count == 0) return false;

            stagingForPreview = true;
            try
            {
                SpawnProps(words);
            }
            finally
            {
                stagingForPreview = false;
            }
            return true;
        }
    }
}
