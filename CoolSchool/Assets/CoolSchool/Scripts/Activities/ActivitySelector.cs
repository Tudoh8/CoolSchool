namespace CoolSchool.Activities
{
    /// <summary>
    /// Optional hook on an ActivityZone (a component on the same object): decides which activity the zone starts this time.
    /// The classroom uses it to alternate between the numbers and the letters lesson.
    /// </summary>
    public interface IActivitySelector
    {
        /// <summary>The activity to start now. May return null or the fallback, which is the zone's own activity.</summary>
        Activity Select(Activity fallback);
    }
}
