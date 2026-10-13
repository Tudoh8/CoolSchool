using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>The number ideas the game teaches. Each one is first met in the classroom, from its own numbers level on.</summary>
    public enum NumberSkill
    {
        /// <summary>Counting toys, and "how many?" with number cards.</summary>
        Counting,
        /// <summary>Hear a number and find its card.</summary>
        FindNumber,
        /// <summary>Put this many in the basket.</summary>
        GiveMe,
        /// <summary>See a small amount at a glance (dots shown briefly).</summary>
        QuickLook,
        /// <summary>Which group has more (later also: fewer)?</summary>
        Compare,
        /// <summary>One more comes, one goes away: how many now?</summary>
        OneMoreOneLess,
        /// <summary>Make the same amount as the other group.</summary>
        SameAmount,
        /// <summary>First, second, third.</summary>
        Ordinals,
        /// <summary>Which number comes after / before?</summary>
        NumberLine,
        /// <summary>Counting backwards, down to zero.</summary>
        CountBack,
        /// <summary>Counting on from a number other than one.</summary>
        CountOn,
        /// <summary>Some are hidden: how many are under the cup?</summary>
        Hidden,
        /// <summary>Which number is missing in a row (4, 5, _, 7)?</summary>
        Gap,
    }

    /// <summary>
    /// The one place that knows what the numbers level means: the biggest number for each level, and from which level
    /// each number idea is used. The classroom lesson moves the level; the play sections only read it, so they use an idea
    /// only after he has met it at the classroom table. Contract: Docs/specs/harder-numbers-contract.md.
    /// </summary>
    public static class NumberCurriculum
    {
        /// <summary>The skill key in the progress file.</summary>
        public const string Skill = "numbers";

        public const int MaxLevel = 9;

        /// <summary>Voice lines exist for number.0 up to number.20, so nothing may ask for more.</summary>
        public const int HighestNumber = 20;

        // The biggest number the kid is asked about, for levels 1 to 9.
        static readonly int[] MaxNumberByLevel = { 3, 5, 5, 7, 10, 10, 15, 20, 20 };

        /// <summary>The saved numbers level, 1 to 9.</summary>
        public static int CurrentLevel()
        {
            return Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
        }

        /// <summary>The biggest number for this level.</summary>
        public static int MaxNumber(int level)
        {
            return MaxNumberByLevel[Mathf.Clamp(level, 1, MaxLevel) - 1];
        }

        /// <summary>The biggest number for the saved level.</summary>
        public static int MaxNumber()
        {
            return MaxNumber(CurrentLevel());
        }

        /// <summary>The numbers level from which this idea is used.</summary>
        public static int UnlockLevel(NumberSkill skill)
        {
            switch (skill)
            {
                case NumberSkill.Counting: return 1;
                case NumberSkill.FindNumber: return 2;
                case NumberSkill.GiveMe: return 3;
                case NumberSkill.QuickLook: return 3;
                case NumberSkill.Compare: return 4;
                case NumberSkill.OneMoreOneLess: return 5;
                case NumberSkill.SameAmount: return 5;
                case NumberSkill.Ordinals: return 5;
                case NumberSkill.NumberLine: return 6;
                case NumberSkill.CountBack: return 6;
                case NumberSkill.CountOn: return 7;
                case NumberSkill.Hidden: return 7;
                case NumberSkill.Gap: return 8;
                default: return MaxLevel;
            }
        }

        public static bool IsUnlocked(NumberSkill skill, int level)
        {
            return level >= UnlockLevel(skill);
        }

        /// <summary>Whether the idea is used at the saved level.</summary>
        public static bool IsUnlocked(NumberSkill skill)
        {
            return IsUnlocked(skill, CurrentLevel());
        }
    }
}
