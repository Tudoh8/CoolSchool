using System.Collections.Generic;
using CoolSchool.Rewards;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// Which zoo animals he has found at least once (PlayerProgress.zooAnimals). Found kinds live in their pens in free play, and
    /// the pens' signs show them in colour. MarkFound only changes the list: the caller saves (ProgressStore.Save).
    /// </summary>
    public static class ZooProgress
    {
        /// <summary>True when this kind has been found at least once.</summary>
        public static bool IsKnown(string id)
        {
            List<string> known = ProgressStore.Current.zooAnimals;
            return !string.IsNullOrEmpty(id) && known != null && known.Contains(id);
        }

        /// <summary>Adds the kind to the found ones (no duplicates). Does not save.</summary>
        public static void MarkFound(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            PlayerProgress progress = ProgressStore.Current;
            if (progress.zooAnimals == null) progress.zooAnimals = new List<string>();
            if (!progress.zooAnimals.Contains(id)) progress.zooAnimals.Add(id);
        }

        /// <summary>How many of the kinds have been found so far.</summary>
        public static int KnownCount
        {
            get
            {
                int count = 0;
                foreach (ZooAnimalKind kind in ZooAnimalKind.Order)
                    if (IsKnown(kind.Id)) count++;
                return count;
            }
        }
    }
}
