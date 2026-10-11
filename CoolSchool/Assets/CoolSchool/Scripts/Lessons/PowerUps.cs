using CoolSchool.Rewards;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Little bonuses won in a lesson that the next game uses up. The lesson sets
    /// ProgressStore.Current.pendingPowerUp; the game that wants it calls TryConsume.
    /// </summary>
    public static class PowerUps
    {
        public const string GoldenSword = "golden_sword";

        /// <summary>True (once) when this power-up was waiting. It is cleared and saved straight away.</summary>
        public static bool TryConsume(string id)
        {
            PlayerProgress progress = ProgressStore.Current;
            if (string.IsNullOrEmpty(id) || progress.pendingPowerUp != id) return false;

            progress.pendingPowerUp = "";
            ProgressStore.Save();
            return true;
        }
    }
}
