using System.Collections.Generic;

namespace CoolSchool.Parent
{
    /// <summary>
    /// The written texts of the parent area, Dutch and English. Parents read these; the child never does, so they
    /// are kept here and not in the voice line table.
    /// </summary>
    public static class ParentStrings
    {
        public enum Language
        {
            Dutch,
            English,
        }

        public const string Title = "title";
        public const string NameLabel = "name.label";
        public const string NamePlaceholder = "name.placeholder";
        public const string NameHint = "name.hint";
        public const string Difficulty = "difficulty.label";
        public const string RowNumbers = "row.numbers";
        public const string RowLetters = "row.letters";
        public const string RowShop = "row.shop";
        public const string RowPirates = "row.pirates";
        public const string RowRace = "row.race";
        public const string LevelCaption = "level.caption";
        public const string Close = "close";

        /// <summary>The language shown. Dutch for now; a language setting can change this later.</summary>
        public static Language Current { get; set; } = Language.Dutch;

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // key, then { Dutch, English }
            { Title, new[] { "Ouderinstellingen", "Parent settings" } },
            { NameLabel, new[] { "Naam van je kind", "Your child's name" } },
            { NamePlaceholder, new[] { "Naam", "Name" } },
            { NameHint, new[] { "De letters van de naam leert hij het eerst.", "The letters of the name are taught first." } },
            { Difficulty, new[] { "Moeilijkheid", "Difficulty" } },
            { RowNumbers, new[] { "Getallen", "Numbers" } },
            { RowLetters, new[] { "Letters", "Letters" } },
            { RowShop, new[] { "IJswinkel", "Ice cream shop" } },
            { RowPirates, new[] { "Piraten", "Pirates" } },
            { RowRace, new[] { "Zeepkistrace", "Soapbox race" } },
            { LevelCaption, new[]
                {
                    "Past zich automatisch aan; de schuif zet het huidige niveau.",
                    "Adjusts by itself; the slider sets the current level.",
                } },
            { Close, new[] { "Sluiten", "Close" } },
        };

        public static string Get(string key)
        {
            if (!Table.TryGetValue(key, out string[] texts)) return key;

            return texts[Current == Language.English ? 1 : 0];
        }
    }
}
