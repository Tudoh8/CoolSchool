using System.Collections.Generic;
using CoolSchool.Rewards;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Which letter comes next and how well the child knows the ones he has had: his name first, then the school order.
    /// All of it is kept in the saved progress, so it survives restarts. Changing the name later only changes the order
    /// of the letters not introduced yet.
    /// </summary>
    public static class LetterCurriculum
    {
        /// <summary>The order the letters are taught in after the letters of his name.</summary>
        public const string SchoolOrder = "mrsvikapntebodlhwzjgfucyxq";

        /// <summary>Introduce the next letter once the newest one has been answered right on the first try this often.</summary>
        public const int FirstTriesToIntroduce = 3;
        /// <summary>...or after this many lessons without a new letter, whichever comes first.</summary>
        public const int LessonsWithoutNewLetter = 1;

        // Letters that look alike, which are only mixed on the cards from level 4.
        static readonly string[] SimilarPairs = { "bd", "pq", "bp", "dq", "mn", "nu", "nh", "ij", "ao", "ce", "ec", "vw", "ft", "ik", "lt" };

        /// <summary>The letters of the name without doubles, then the school order without the letters already taken.</summary>
        public static string OrderFor(string childName)
        {
            var order = new System.Text.StringBuilder();
            string name = ProgressStore.NormalizeChildName(childName);
            foreach (char c in name)
                if (order.ToString().IndexOf(c) < 0) order.Append(c);
            foreach (char c in SchoolOrder)
                if (order.ToString().IndexOf(c) < 0) order.Append(c);
            return order.ToString();
        }

        /// <summary>The first letter in teaching order that has not been introduced yet and has strokes to trace. Null when all are done.</summary>
        public static char? NextNewLetter(PlayerProgress progress, LetterSet set)
        {
            EnsureLists(progress);
            foreach (char c in OrderFor(progress.childName))
            {
                if (progress.lettersIntroduced.Contains(c.ToString())) continue;

                LetterEntry entry = set != null ? set.Find(c) : null;
                if (entry != null && entry.HasStrokes) return c;
            }
            return null;
        }

        /// <summary>True when this lesson should bring a new letter: the first lesson ever, a well-known newest letter, or too long without one.</summary>
        public static bool NewLetterDue(PlayerProgress progress)
        {
            EnsureLists(progress);
            if (progress.lettersIntroduced.Count == 0) return true;
            if (progress.lessonsSinceNewLetter >= LessonsWithoutNewLetter) return true;

            string newest = progress.lettersIntroduced[progress.lettersIntroduced.Count - 1];
            return newest.Length > 0 && Score(progress, newest[0]) >= FirstTriesToIntroduce;
        }

        public static int Score(PlayerProgress progress, char letter)
        {
            EnsureLists(progress);
            string key = letter.ToString();
            foreach (SkillLevel entry in progress.letterScores)
                if (entry != null && entry.skill == key) return entry.level;
            return 0;
        }

        public static void AddScore(PlayerProgress progress, char letter)
        {
            EnsureLists(progress);
            string key = letter.ToString();
            foreach (SkillLevel entry in progress.letterScores)
            {
                if (entry == null || entry.skill != key) continue;
                entry.level++;
                return;
            }
            progress.letterScores.Add(new SkillLevel { skill = key, level = 1 });
        }

        /// <summary>Marks a letter as introduced (once) and restarts the count of lessons without a new letter.</summary>
        public static void Introduce(PlayerProgress progress, char letter)
        {
            EnsureLists(progress);
            if (!progress.lettersIntroduced.Contains(letter.ToString())) progress.lettersIntroduced.Add(letter.ToString());
            progress.lessonsSinceNewLetter = 0;
        }

        public static bool AreSimilar(char a, char b)
        {
            if (a == b) return true;
            foreach (string pair in SimilarPairs)
                if ((pair[0] == a && pair[1] == b) || (pair[0] == b && pair[1] == a)) return true;
            return false;
        }

        /// <summary>Letters that start with the same sound, so they can not be the wrong answer to each other: c, k and q all say "kuh".</summary>
        public static char SoundGroup(char letter)
        {
            return letter == 'c' || letter == 'q' ? 'k' : letter;
        }

        static void EnsureLists(PlayerProgress progress)
        {
            if (progress.lettersIntroduced == null) progress.lettersIntroduced = new List<string>();
            if (progress.letterScores == null) progress.letterScores = new List<SkillLevel>();
            if (progress.childName == null) progress.childName = "";
        }
    }
}
