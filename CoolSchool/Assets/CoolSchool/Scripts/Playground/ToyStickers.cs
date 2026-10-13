using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>The sticker collection: one sticker per playground toy, earned the first time he plays on it. Stored in PlayerProgress.toyStickers.</summary>
    public static class ToyStickers
    {
        /// <summary>Every sticker id, in the order of the equipment ring.</summary>
        public static readonly string[] AllIds =
            { "slide", "roundabout", "swings", "tunnel", "monkey_bars", "rocker_1", "ship", "seesaw", "fort", "rocker_2", "tyre_wall", "track_ride" };

        public static bool Has(string id)
        {
            var list = ProgressStore.Current.toyStickers;
            return !string.IsNullOrEmpty(id) && list != null && list.Contains(id);
        }

        /// <summary>How many of the stickers he has.</summary>
        public static int Count
        {
            get
            {
                int count = 0;
                foreach (string id in AllIds)
                    if (Has(id)) count++;
                return count;
            }
        }

        /// <summary>Adds the sticker and saves (ProgressStore.Save raises Changed). Returns true when it is new.</summary>
        public static bool Give(string id)
        {
            if (string.IsNullOrEmpty(id) || System.Array.IndexOf(AllIds, id) < 0)
            {
                Debug.LogWarning($"CoolSchool toys: unknown sticker id '{id}'.");
                return false;
            }

            PlayerProgress progress = ProgressStore.Current;
            if (progress.toyStickers == null) progress.toyStickers = new System.Collections.Generic.List<string>();
            if (progress.toyStickers.Contains(id)) return false;

            progress.toyStickers.Add(id);
            ProgressStore.Save();
            return true;
        }

        /// <summary>For the test menus: every sticker (true) or none (false). Saves.</summary>
        public static void SetAll(bool owned)
        {
            PlayerProgress progress = ProgressStore.Current;
            if (progress.toyStickers == null) progress.toyStickers = new System.Collections.Generic.List<string>();
            progress.toyStickers.Clear();
            if (owned) progress.toyStickers.AddRange(AllIds);
            ProgressStore.Save();
        }
    }
}
