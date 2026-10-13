using System.Collections.Generic;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The voice lines of the cart garage (Docs/specs/cart-garage-contract.md): the name of each new cart present (said when the present
    /// opens and when he taps its card in the picker), the question of each picker tab, the race invitation for a new cart present and
    /// the announcer's line for his race number. The names of the new cart BODIES are added by the agent that builds them.
    /// Narrator is Lily, Teacher too; the announcer is Dave.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        static IEnumerable<Seed> GarageSeeds()
        {
            // The name of each new cart present (group item.<id>; the horns say their animal's sound).
            yield return new Seed("item.cart.paint_red.1", Speaker.Narrator, "De rode verf!", "The red paint!");
            yield return new Seed("item.cart.paint_blue.1", Speaker.Narrator, "De blauwe verf!", "The blue paint!");
            yield return new Seed("item.cart.paint_green.1", Speaker.Narrator, "De groene verf!", "The green paint!");
            yield return new Seed("item.cart.paint_pink.1", Speaker.Narrator, "De roze verf!", "The pink paint!");
            yield return new Seed("item.cart.paint_purple.1", Speaker.Narrator, "De paarse verf!", "The purple paint!");
            yield return new Seed("item.cart.eyes.1", Speaker.Narrator, "De wiebeloogjes!", "The googly eyes!");
            yield return new Seed("item.cart.balloons.1", Speaker.Narrator, "De ballonnen!", "The balloons!");
            yield return new Seed("item.cart.propeller.1", Speaker.Narrator, "De propeller!", "The propeller!");
            yield return new Seed("item.cart.spoiler.1", Speaker.Narrator, "De spoiler!", "The spoiler!");
            yield return new Seed("item.cart.trail_rainbow.1", Speaker.Narrator, "Het regenboogspoor!", "The rainbow trail!");
            yield return new Seed("item.cart.trail_stars.1", Speaker.Narrator, "De sterretjes!", "The little stars!");
            yield return new Seed("item.cart.trail_bubbles.1", Speaker.Narrator, "De bellenblazer!", "The bubble blower!");
            yield return new Seed("item.cart.trail_sparkles.1", Speaker.Narrator, "De glitters!", "The glitter!");
            yield return new Seed("item.cart.horn_bell.1", Speaker.Narrator, "Het belletje!", "The little bell!");
            yield return new Seed("item.cart.horn_cow.1", Speaker.Narrator, "De koe! Boe!", "The cow! Moo!");
            yield return new Seed("item.cart.horn_dog.1", Speaker.Narrator, "De hond! Woef!", "The dog! Woof!");
            yield return new Seed("item.cart.horn_cat.1", Speaker.Narrator, "De poes! Miauw!", "The cat! Meow!");
            yield return new Seed("item.cart.horn_elephant.1", Speaker.Narrator, "De olifant! Toet!", "The elephant! Toot!");
            yield return new Seed("item.cart.horn_magic.1", Speaker.Narrator, "De toverhoorn!", "The magic horn!");

            // The question of each tab of the cart picker, said the first time a tab is opened in a race.
            yield return new Seed("race.tab_body.1", Speaker.Announcer, "Welke kar?", "Which cart?");
            yield return new Seed("race.tab_paint.1", Speaker.Announcer, "Welke kleur?", "Which colour?");
            yield return new Seed("race.tab_addon.1", Speaker.Announcer, "Wat zet je erop?", "What do you put on it?");
            yield return new Seed("race.tab_trail.1", Speaker.Announcer, "Welk spoor?", "Which trail?");
            yield return new Seed("race.tab_horn.1", Speaker.Announcer, "Welke toeter?", "Which horn?");
            yield return new Seed("race.tab_number.1", Speaker.Announcer, "Welk nummer?", "Which number?");

            // The invitation to the race while there is a new cart present he has not seen in the picker yet.
            yield return new Seed("invite.race.new.1", Speaker.Teacher, "Je hebt iets nieuws voor je kar! Kom je racen?", "You have something new for your cart! Are you coming to race?");

            // The announcer says his number once, at the start.
            string[] dutch = { "één", "twee", "drie", "vier", "vijf", "zes", "zeven", "acht", "negen", "tien" };
            string[] english = { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            for (int n = 1; n <= 10; n++)
                yield return new Seed($"race.number.{n}.1", Speaker.Announcer, $"Daar is nummer {dutch[n - 1]}!", $"There is number {english[n - 1]}!");
        }
    }
}
