using System.Collections.Generic;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The voice lines of the playground toys (Docs/specs/playground-toys-contract.md): the Kid's "whee" and friends while he plays, the
    /// narrator's sticker line for each toy, the line for the whole collection and the introduction of the sticker board.
    /// Kid is Freya (pitched up at run time), Narrator is Lily.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        static IEnumerable<Seed> ToySeeds()
        {
            // The Kid while he plays.
            yield return new Seed("toys.whee.1", Speaker.Kid, "Wiiieee!", "Wheee!");
            yield return new Seed("toys.whee.2", Speaker.Kid, "Joepie!", "Yippee!");
            yield return new Seed("toys.whee.3", Speaker.Kid, "Wat leuk!", "This is fun!");
            yield return new Seed("toys.whee.4", Speaker.Kid, "Nog een keer!", "One more time!");
            yield return new Seed("toys.dizzy.1", Speaker.Kid, "Ik ben duizelig!", "I'm dizzy!");
            yield return new Seed("toys.dizzy.2", Speaker.Kid, "Alles draait!", "Everything is spinning!");
            yield return new Seed("toys.peekaboo.1", Speaker.Kid, "Kiekeboe!", "Peekaboo!");
            yield return new Seed("toys.top.1", Speaker.Kid, "Ik ben helemaal boven!", "I'm all the way up!");
            yield return new Seed("toys.top.2", Speaker.Kid, "Kijk eens hoe hoog!", "Look how high I am!");
            yield return new Seed("toys.ahoy.1", Speaker.Kid, "Ahoi! Land in zicht!", "Ahoy! Land ho!");

            // The narrator names each sticker (the group is toys.sticker.<voice name>; both rockers share "rocker").
            yield return new Seed("toys.sticker.slide.1", Speaker.Narrator, "Een sticker van de glijbaan!", "A slide sticker!");
            yield return new Seed("toys.sticker.roundabout.1", Speaker.Narrator, "Een sticker van de draaimolen!", "A roundabout sticker!");
            yield return new Seed("toys.sticker.swings.1", Speaker.Narrator, "Een sticker van de schommel!", "A swing sticker!");
            yield return new Seed("toys.sticker.tunnel.1", Speaker.Narrator, "Een sticker van de tunnel!", "A tunnel sticker!");
            yield return new Seed("toys.sticker.monkey_bars.1", Speaker.Narrator, "Een sticker van het klimrek!", "A monkey bars sticker!");
            yield return new Seed("toys.sticker.rocker.1", Speaker.Narrator, "Een sticker van de wipkip!", "A spring rider sticker!");
            yield return new Seed("toys.sticker.ship.1", Speaker.Narrator, "Een sticker van het schip!", "A ship sticker!");
            yield return new Seed("toys.sticker.seesaw.1", Speaker.Narrator, "Een sticker van de wip!", "A seesaw sticker!");
            yield return new Seed("toys.sticker.fort.1", Speaker.Narrator, "Een sticker van het fort!", "A fort sticker!");
            yield return new Seed("toys.sticker.tyre_wall.1", Speaker.Narrator, "Een sticker van de bandenmuur!", "A tyre wall sticker!");
            yield return new Seed("toys.sticker.track_ride.1", Speaker.Narrator, "Een sticker van de kabelbaan!", "A zip line sticker!");

            // The whole collection, and the sticker board.
            yield return new Seed("toys.sticker_all.1", Speaker.Narrator, "Alle speeltuinstickers! Wat knap van jou!", "All the playground stickers! Well done!");
            yield return new Seed("toys.board_intro.1", Speaker.Narrator,
                "Dit is je stickerbord. Speel met alles in de speeltuin, dan krijg je stickers!",
                "This is your sticker board. Play with everything on the playground to get stickers!");
        }
    }
}
