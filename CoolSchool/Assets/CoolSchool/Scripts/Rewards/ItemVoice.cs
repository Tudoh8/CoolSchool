namespace CoolSchool.Rewards
{
    /// <summary>Which voice line group says the name of an item.</summary>
    public static class ItemVoice
    {
        /// <summary>"item.gold" for gold variants, otherwise "item." plus the id (for example "item.sword.wood").</summary>
        public static string GroupFor(RewardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) return "";
            return item.IsGold ? "item.gold" : "item." + item.id;
        }
    }
}
