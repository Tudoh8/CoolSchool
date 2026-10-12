using CoolSchool.Activities;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// On the classroom's zone: walking in starts the numbers lesson or the letters lesson, taking turns. The very first
    /// lesson is numbers. What was taught last is saved in the progress, so the turns carry on after a restart.
    /// </summary>
    public class ClassroomActivitySelector : MonoBehaviour, IActivitySelector
    {
        [SerializeField] Activity numbersLesson;
        [SerializeField] Activity lettersLesson;

        public const string Numbers = "numbers";
        public const string Letters = "letters";

        public Activity Select(Activity fallback)
        {
            Activity numbers = numbersLesson != null ? numbersLesson : fallback;
            if (lettersLesson == null) return numbers;

            PlayerProgress progress = ProgressStore.Current;
            bool letters = progress.lastLessonSubject == Numbers;

            // Counts as taught as soon as it starts, so a child who walks out still gets the other one next time.
            progress.lastLessonSubject = letters ? Letters : Numbers;
            ProgressStore.Save();
            return letters ? lettersLesson : numbers;
        }
    }
}
