namespace CoolSchool.Lessons
{
    /// <summary>The kinds of exercise in the numbers lesson.</summary>
    public enum ExerciseType
    {
        /// <summary>Tap each toy and count along.</summary>
        TapAndCount,
        /// <summary>Count the toys and pick the right number card.</summary>
        HowMany,
        /// <summary>Hear a number and pick its card.</summary>
        FindNumber,
        /// <summary>Put this many toys in the basket.</summary>
        GiveMe,
        /// <summary>Dots show for a moment: how many were there?</summary>
        QuickLook,
        /// <summary>Two groups in two trays: which has more (or fewer)?</summary>
        Compare,
        /// <summary>One more toy comes, or one goes away: how many now?</summary>
        OneMoreOneLess,
        /// <summary>Put as many toys in the basket as lie in the tray.</summary>
        SameAmount,
        /// <summary>Which number comes after or before, or is missing in a row?</summary>
        NumberLine,
        /// <summary>Tap the toys away one by one and count backwards to zero.</summary>
        CountBack,
        /// <summary>The basket already holds some: keep counting from there.</summary>
        CountOn,
        /// <summary>A cup hides some toys: how many are under it?</summary>
        Hidden,
    }

    /// <summary>Which number idea each kind of exercise practises, so the numbers level can decide what is used.</summary>
    public static class ExerciseTypes
    {
        public static NumberSkill SkillOf(this ExerciseType type)
        {
            switch (type)
            {
                case ExerciseType.FindNumber: return NumberSkill.FindNumber;
                case ExerciseType.GiveMe: return NumberSkill.GiveMe;
                case ExerciseType.QuickLook: return NumberSkill.QuickLook;
                case ExerciseType.Compare: return NumberSkill.Compare;
                case ExerciseType.OneMoreOneLess: return NumberSkill.OneMoreOneLess;
                case ExerciseType.SameAmount: return NumberSkill.SameAmount;
                case ExerciseType.NumberLine: return NumberSkill.NumberLine;
                case ExerciseType.CountBack: return NumberSkill.CountBack;
                case ExerciseType.CountOn: return NumberSkill.CountOn;
                case ExerciseType.Hidden: return NumberSkill.Hidden;
                default: return NumberSkill.Counting;
            }
        }
    }
}
