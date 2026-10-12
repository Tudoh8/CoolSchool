using System.Collections.Generic;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The voice lines of the harder-numbers work (Docs/specs/harder-numbers-contract.md): number words 0 and 11 to 20,
    /// the classroom exercises for levels 6 to 9, and the number ideas reused in the race, aliens, pirates and shop.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        // Number words 0..20, lowercase as they sit inside a sentence. Standalone words are capitalised when seeded.
        static readonly string[] NumberWordsNl =
        {
            "nul", "één", "twee", "drie", "vier", "vijf", "zes", "zeven", "acht", "negen", "tien",
            "elf", "twaalf", "dertien", "veertien", "vijftien", "zestien", "zeventien", "achttien", "negentien", "twintig",
        };

        static readonly string[] NumberWordsEn =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
        };

        static string Cap(string word) => char.ToUpperInvariant(word[0]) + word.Substring(1);

        static IEnumerable<Seed> NumberSeeds()
        {
            // Number words (Teacher). The words 1 to 10 already exist elsewhere.
            yield return new Seed("number.0.1", Speaker.Teacher, "Nul", "Zero");
            for (int n = 11; n <= 20; n++)
                yield return new Seed($"number.{n}.1", Speaker.Teacher, Cap(NumberWordsNl[n]), Cap(NumberWordsEn[n]));

            // Classroom: quick look, compare, same amount, one more / one less.
            yield return new Seed("lesson.quick_look.1", Speaker.Teacher, "Kijk goed! De stippen zijn er maar heel even.", "Look closely! The dots are only there for a moment.");
            yield return new Seed("lesson.quick_look_ask.1", Speaker.Teacher, "Hoeveel stippen zag je?", "How many dots did you see?");
            yield return new Seed("lesson.compare_more.1", Speaker.Teacher, "Waar zijn er meer? Tik op die kant!", "Where are there more? Tap that side!");
            yield return new Seed("lesson.compare_fewer.1", Speaker.Teacher, "Waar zijn er minder? Tik op die kant!", "Where are there fewer? Tap that side!");
            yield return new Seed("lesson.compare_hint.1", Speaker.Teacher, "We tellen ze allebei. Tel maar mee!", "Let's count them both. Count along!");
            yield return new Seed("lesson.same_amount.1", Speaker.Teacher, "Doe er net zoveel in de mand als er in het bakje liggen!", "Put just as many in the basket as there are in the tray!");
            yield return new Seed("lesson.one_more.1", Speaker.Teacher, "Kijk! Er komt er eentje bij!", "Look! One more is coming!");
            yield return new Seed("lesson.one_less.1", Speaker.Teacher, "Kijk! Er gaat er eentje weg!", "Look! One is going away!");
            yield return new Seed("lesson.how_many_now.1", Speaker.Teacher, "Hoeveel zijn het er nu?", "How many are there now?");

            // Classroom: the number line.
            for (int n = 1; n <= 19; n++)
                yield return new Seed($"lesson.number_after.{n}.1", Speaker.Teacher,
                    $"Welk getal komt na {NumberWordsNl[n]}?", $"Which number comes after {NumberWordsEn[n]}?");
            for (int n = 2; n <= 20; n++)
                yield return new Seed($"lesson.number_before.{n}.1", Speaker.Teacher,
                    $"Welk getal komt vóór {NumberWordsNl[n]}?", $"Which number comes before {NumberWordsEn[n]}?");
            yield return new Seed("lesson.number_gap.1", Speaker.Teacher, "Welk getal mist er? Tel maar mee!", "Which number is missing? Count along!");

            // Classroom: counting back, counting on, the hidden cup.
            yield return new Seed("lesson.count_back.1", Speaker.Teacher, "Tik ze één voor één weg, en tel terug!", "Tap them away one by one, and count backwards!");
            yield return new Seed("lesson.count_back_done.1", Speaker.Teacher, "Nul! Allemaal weg!", "Zero! All gone!");
            for (int n = 2; n <= 17; n++)
                yield return new Seed($"lesson.count_on.{n}.1", Speaker.Teacher,
                    $"In de mand zitten er al {NumberWordsNl[n]}. Tel maar verder!", $"There are already {NumberWordsEn[n]} in the basket. Keep counting!");
            yield return new Seed("lesson.count_on_ask.1", Speaker.Teacher, "Hoeveel zitten er nu in de mand?", "How many are in the basket now?");
            yield return new Seed("lesson.hidden.1", Speaker.Teacher, "Kijk goed! De beker verstopt er een paar.", "Watch closely! The cup hides some.");
            yield return new Seed("lesson.hidden_ask.1", Speaker.Teacher, "Hoeveel zitten er onder de beker?", "How many are under the cup?");
            yield return new Seed("lesson.hidden_hint.1", Speaker.Teacher, "Zullen we eens onder de beker kijken?", "Shall we look under the cup?");

            // Race: the number gates and the podium question.
            for (int n = 11; n <= 20; n++)
                yield return new Seed($"race.gate_number.{n}.1", Speaker.Announcer,
                    $"Rij door de {NumberWordsNl[n]}!", $"Drive through the {NumberWordsEn[n]}!");
            for (int n = 1; n <= 19; n++)
                yield return new Seed($"race.gate_after.{n}.1", Speaker.Announcer,
                    $"Welk getal komt na {NumberWordsNl[n]}? Rij erdoor!", $"Which number comes after {NumberWordsEn[n]}? Drive through it!");

            string[] placeNl = { "eerste", "tweede", "derde" };
            string[] placeEn = { "first", "second", "third" };
            for (int p = 1; p <= 3; p++)
                yield return new Seed($"race.podium_ask.{p}.1", Speaker.Announcer,
                    $"Wie staat er op de {placeNl[p - 1]} plek? Tik maar aan!", $"Who is in {placeEn[p - 1]} place? Tap them!");
            yield return new Seed("race.podium_wrong.1", Speaker.Announcer, "Bijna! Kijk maar naar de cijfers.", "Almost! Look at the numbers.");

            // Aliens.
            yield return new Seed("aliens.baby_joins.1", Speaker.Teacher, "De baby-alien doet ook mee! Hoeveel aliens dansen er nu?", "The baby alien joins in! How many aliens are dancing now?");
            yield return new Seed("aliens.one_leaves.1", Speaker.Teacher, "Oei, eentje rent even naar de raket! Hoeveel dansen er nu nog?", "Oops, one runs off to the rocket! How many are still dancing?");
            yield return new Seed("aliens.countdown.1", Speaker.Teacher, "De raket gaat vertrekken! Tel mee terug. Tik op het getal!", "The rocket is about to leave! Count down with me. Tap the number!");
            yield return new Seed("aliens.liftoff.1", Speaker.Teacher, "Lancering! Daar gaat de raket!", "Lift-off! There goes the rocket!");

            // Pirates.
            yield return new Seed("pirates.compare.1", Speaker.Parrot, "Twee stapels goud! Welke stapel is het grootst? Tik erop!", "Two piles of gold! Which pile is bigger? Tap it!");
            yield return new Seed("pirates.compare_hint.1", Speaker.Parrot, "Kraa! We tellen ze allebei. Tel maar mee!", "Squawk! Let's count them both. Count along!");
            yield return new Seed("pirates.compare_share.1", Speaker.Parrot, "De kleine stapel is voor de skeletten. Kijk eens hoe blij ze zijn!", "The small pile is for the skeletons. Look how happy they are!");
            yield return new Seed("pirates.hidden.1", Speaker.Parrot, "Kijk goed! Lorre verstopt een paar munten onder de emmer!", "Watch closely! Lorre hides some coins under the bucket!");
            yield return new Seed("pirates.hidden_ask.1", Speaker.Parrot, "Hoeveel munten liggen er onder de emmer?", "How many coins are under the bucket?");
            yield return new Seed("pirates.hidden_reveal.1", Speaker.Parrot, "Daar zijn ze! Tel maar mee!", "There they are! Count along!");
            for (int n = 2; n <= 17; n++)
                yield return new Seed($"pirates.count_on.{n}.1", Speaker.Parrot,
                    $"In de kist zitten al {NumberWordsNl[n]} munten. Tel maar verder!", $"There are already {NumberWordsEn[n]} coins in the chest. Keep counting!");

            // Ice cream shop: the tip coin.
            yield return new Seed("shop.tip.kid.1", Speaker.Kid, "En nog eentje extra, omdat het zo lekker is!", "And one extra, because it's so yummy!");
            yield return new Seed("shop.tip.knight.1", Speaker.Kid, "Voor de beste ijsverkoper: nog één muntje extra!", "For the best ice cream seller: one extra coin!");
            yield return new Seed("shop.tip.alien.1", Speaker.Alien, "Bliep! Nog eentje extra voor jou!", "Bleep! One extra for you!");
            yield return new Seed("shop.tip.dragon.1", Speaker.Goblin, "Grrroar! Nog een muntje extra!", "Grrroar! One more coin!");
            yield return new Seed("shop.how_many_now.1", Speaker.Narrator, "Hoeveel muntjes zijn het nu?", "How many coins are there now?");
        }
    }
}
