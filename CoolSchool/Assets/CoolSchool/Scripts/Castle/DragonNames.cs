namespace CoolSchool.Castle
{
    /// <summary>
    /// The names the baby dragon can get: one for each letter, so he names it by the first sound he hears
    /// (m is Mimi, v is Vonkie...). Dutch names that work in English too. No names for c, y, x and q.
    /// </summary>
    public static class DragonNames
    {
        /// <summary>The letters that have a name, in the order of the school curriculum.</summary>
        public const string Letters = "mrsvikapntebodlhwzjgfu";

        static readonly string[] Names =
        {
            "Mimi", "Rocco", "Sproet", "Vonkie", "Ikkie", "Kiki", "Appie", "Pip", "Noekie", "Tuut", "Eddie",
            "Bolle", "Okkie", "Dribbel", "Lolly", "Hikkie", "Wiebel", "Zoef", "Joppie", "Guus", "Fikkie", "Ukkie",
        };

        /// <summary>The name for a letter, or an empty text when there is none.</summary>
        public static string NameFor(char letter)
        {
            int index = Letters.IndexOf(char.ToLowerInvariant(letter));
            return index >= 0 ? Names[index] : string.Empty;
        }

        public static bool Has(char letter)
        {
            return Letters.IndexOf(char.ToLowerInvariant(letter)) >= 0;
        }
    }
}
