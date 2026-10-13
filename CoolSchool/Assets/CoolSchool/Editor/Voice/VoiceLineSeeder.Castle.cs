using System.Collections.Generic;
using CoolSchool.Castle;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The voice lines of the castle adventure (Docs/specs/castle-adventure-contract.md): the queen and her missions, the
    /// password, the wall patch, counting the goblins, the goblin kinds, the thief, helping a friend, the tower, the hungry
    /// or sad dragon, the cookies, the egg and the baby dragon, the ride, the name flag and the growing castle.
    /// Speakers: the queen and the knights are Kid, the goblins and the big dragon Goblin, the baby dragon Alien, hints Teacher.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        static IEnumerable<Seed> CastleSeeds()
        {
            // The queen greets him and gives the mission.
            yield return new Seed("castle.queen_hello.1", Speaker.Kid, "Hallo, ridder! Ik ben de koningin van het kasteel. Wat fijn dat je er bent!", "Hello, knight! I'm the queen of the castle. I'm so glad you're here!");
            yield return new Seed("castle.mission.cake.1", Speaker.Kid, "Vanavond is er feest, met een grote taart! Maar de kobolden willen de taart stelen. Help je ons?", "Tonight there's a party with a big cake! But the goblins want to steal the cake. Will you help us?");
            yield return new Seed("castle.mission.crown.1", Speaker.Kid, "Mijn gouden kroon ligt klaar voor het feest. Maar de kobolden willen hem pakken! Help je ons?", "My golden crown is ready for the party. But the goblins want to grab it! Will you help us?");
            yield return new Seed("castle.mission.egg.1", Speaker.Kid, "Kijk, we hebben een groot ei gevonden! De kobolden willen het hebben. Pas jij erop?", "Look, we found a big egg! The goblins want it. Will you look after it?");
            yield return new Seed("castle.mission.knight.1", Speaker.Kid, "Oh nee! Een ridder zit vast boven op de toren! En de kobolden komen eraan. Help je ons?", "Oh no! A knight is stuck on top of the tower! And the goblins are coming. Will you help us?");
            yield return new Seed("castle.tower_help.1", Speaker.Kid, "Help! Ik zit vast!", "Help! I'm stuck!");

            // The password at the gate.
            yield return new Seed("castle.password.1", Speaker.Kid, "Eerst het wachtwoord! Tik op het plaatje dat begint met…", "First the password! Tap the picture that starts with…");
            yield return new Seed("castle.password_new.1", Speaker.Kid, "Dit is een nieuwe letter! Luister maar:", "This is a new letter! Listen:");
            yield return new Seed("castle.password_right.1", Speaker.Kid, "Goed zo, dat is het wachtwoord! De poort gaat open!", "Well done, that's the password! The gate is opening!");

            // The hole in the wall.
            yield return new Seed("castle.wall.1", Speaker.Kid, "Oh nee, er zit een gat in de muur! Tik op de blokken, dan maken we het dicht!", "Oh no, there's a hole in the wall! Tap the blocks and we'll close it!");
            yield return new Seed("castle.wall_peek.1", Speaker.Goblin, "Hihi! Ik kom zo naar binnen!", "Hehe! I'm coming in!");
            yield return new Seed("castle.wall_hint.1", Speaker.Teacher, "Tik op een blok!", "Tap a block!");
            yield return new Seed("castle.wall_done.1", Speaker.Goblin, "Hé! Het gat is dicht!", "Hey! The hole is closed!");

            // Counting the goblins on the wall.
            yield return new Seed("castle.count.1", Speaker.Kid, "Kijk, daar op de muur! Hoeveel kobolden zie je?", "Look, up on the wall! How many goblins do you see?");
            yield return new Seed("castle.count_more.1", Speaker.Goblin, "Ik ook! Ik doe ook mee!", "Me too! I'm joining in!");
            yield return new Seed("castle.count_less.1", Speaker.Goblin, "Oei, ik ben bang! Ik ga weg!", "Eek, I'm scared! I'm leaving!");
            yield return new Seed("castle.count_now.1", Speaker.Kid, "Hoeveel kobolden zijn er nu?", "How many goblins are there now?");
            yield return new Seed("castle.jump_down.1", Speaker.Goblin, "Hop! Daar komen we!", "Hop! Here we come!");

            // The goblin kinds.
            yield return new Seed("castle.shield_goblin.1", Speaker.Teacher, "Die kobold heeft een schild! Wacht tot hij het laat zakken!", "That goblin has a shield! Wait until he lowers it!");
            yield return new Seed("castle.dizzy.1", Speaker.Goblin, "Oei, ik zie sterretjes!", "Whoa, I'm seeing stars!");
            yield return new Seed("castle.troll.1", Speaker.Goblin, "Ik ben de grote trol! Hohoho!", "I'm the big troll! Ho ho ho!");
            yield return new Seed("castle.barrel.1", Speaker.Goblin, "Hihi, ik zit in de ton! Vind je mij?", "Hehe, I'm in the barrel! Can you find me?");
            yield return new Seed("castle.barrel_out.1", Speaker.Goblin, "Gevonden! Hihi!", "You found me! Hehe!");

            // The thief.
            yield return new Seed("castle.thief_grab.cake.1", Speaker.Goblin, "Hihi! Een stuk taart voor mij!", "Hehe! A piece of cake for me!");
            yield return new Seed("castle.thief_grab.crown.1", Speaker.Goblin, "Hihi! Nu ben ik de koning!", "Hehe! Now I'm the king!");
            yield return new Seed("castle.thief_grab.egg.1", Speaker.Goblin, "Hihi! Dat ei is van mij!", "Hehe! That egg is mine!");
            yield return new Seed("castle.thief_chase.1", Speaker.Kid, "Snel, vang die kobold!", "Quick, catch that goblin!");
            yield return new Seed("castle.loot_back.1", Speaker.Kid, "Goed zo! Je hebt het teruggepakt!", "Well done! You got it back!");

            // Help a friend.
            yield return new Seed("castle.help_me.1", Speaker.Kid, "Help! Help mij!", "Help! Help me!");
            yield return new Seed("castle.help_me.2", Speaker.Kid, "Help! Ze zitten achter mij aan!", "Help! They're chasing me!");
            yield return new Seed("castle.thanks.1", Speaker.Kid, "Dankjewel! Jij bent een echte ridder!", "Thank you! You're a real knight!");

            // The knight on the tower.
            yield return new Seed("castle.rescue.1", Speaker.Kid, "Nu de ridder op de toren! Loop naar de toren!", "Now the knight on the tower! Walk to the tower!");
            yield return new Seed("castle.rescue_down.1", Speaker.Kid, "Joepie, ik ben vrij! Dankjewel!", "Yippee, I'm free! Thank you!");

            // The dragon.
            yield return new Seed("castle.dragon_arrives.1", Speaker.Kid, "Kijk! Een draak!", "Look! A dragon!");
            yield return new Seed("castle.dragon_hungry.1", Speaker.Kid, "Wacht! Hoor je dat? Zijn buik rommelt! De draak heeft gewoon honger!", "Wait! Do you hear that? Its tummy is rumbling! The dragon is just hungry!");
            yield return new Seed("castle.dragon_egg.1", Speaker.Kid, "Wacht! De draak is verdrietig. Ze zoekt haar ei!", "Wait! The dragon is sad. She's looking for her egg!");

            // The cookies: "I want <n> cookies" for 2 to 8, spoken by the dragon.
            for (int n = 2; n <= 8; n++)
                yield return new Seed($"castle.cookies_want.{n}.1", Speaker.Goblin,
                    $"Ik wil {NumberWordsNl[n]} koekjes!", $"I want {NumberWordsEn[n]} cookies!");
            yield return new Seed("castle.cookies_hint.1", Speaker.Kid, "Tik op de koekjes. Is het genoeg? Tik dan op de knop!", "Tap the cookies. Is it enough? Then tap the button!");
            yield return new Seed("castle.cookies_more.1", Speaker.Goblin, "Nog meer! Ik heb nog honger!", "More! I'm still hungry!");
            yield return new Seed("castle.cookies_too_many.1", Speaker.Goblin, "Oei, dat zijn er te veel!", "Oops, that's too many!");
            yield return new Seed("castle.cookies_yum.1", Speaker.Goblin, "Mmm, lekker! Precies genoeg!", "Mmm, yummy! Just right!");
            yield return new Seed("castle.cookie_big.1", Speaker.Kid, "Geef de draak dit grote koekje! Tik erop!", "Give the dragon this big cookie! Tap it!");

            // The egg and the baby dragon.
            yield return new Seed("castle.egg_tap.1", Speaker.Kid, "Tik op het ei, dan brengen we het naar de draak!", "Tap the egg and we'll bring it to the dragon!");
            yield return new Seed("castle.egg_hatch.1", Speaker.Kid, "Kijk! Het ei gaat open!", "Look! The egg is hatching!");
            yield return new Seed("castle.baby_hello.1", Speaker.Alien, "Piep! Hallo mama!", "Peep! Hello mummy!");
            yield return new Seed("castle.baby_name.1", Speaker.Kid, "Het draakje heeft nog geen naam! Kies een letter. Met die klank begint zijn naam!", "The little dragon has no name yet! Pick a letter. Its name starts with that sound!");
            foreach (char letter in DragonNames.Letters)
                yield return new Seed($"castle.baby_named.{letter}.1", Speaker.Kid,
                    $"Het draakje heet {DragonNames.NameFor(letter)}!", $"The little dragon is called {DragonNames.NameFor(letter)}!");
            yield return new Seed("castle.baby_buddy.1", Speaker.Kid, "Het draakje wil met jou mee! Het is nu jouw vriendje!", "The little dragon wants to come with you! Now it's your buddy!");

            // Friends, and the ride.
            yield return new Seed("castle.dragon_friend.1", Speaker.Goblin, "Dankjewel! Nu zijn we vrienden. Klim maar op mijn rug!", "Thank you! Now we're friends. Climb onto my back!");
            yield return new Seed("castle.ride_start.1", Speaker.Kid, "Houd je goed vast! Tik op de knop voor confettivuur!", "Hold on tight! Tap the button for confetti fire!");
            yield return new Seed("castle.ride_land.1", Speaker.Goblin, "Daar zijn we weer!", "Here we are again!");

            // The end: his name on the flag, the castle grows, the party.
            yield return new Seed("castle.flag.1", Speaker.Kid, "Voor jou, ridder: een vlag met jouw naam! Met welke letter begint jouw naam?", "For you, knight: a flag with your name! Which letter does your name start with?");
            yield return new Seed("castle.flag_done.1", Speaker.Kid, "Kijk! Jouw naam op de vlag!", "Look! Your name on the flag!");
            yield return new Seed("castle.grow.1", Speaker.Kid, "En kijk: er staat een nieuwe vlag op het kasteel!", "And look: there's a new flag on the castle!");
            yield return new Seed("castle.victory.1", Speaker.Kid, "Hoera! Jullie hebben het kasteel gered! Feest!", "Hooray! You saved the castle! Party time!");
            yield return new Seed("castle.dragon_bye.1", Speaker.Goblin, "Dag! Tot de volgende keer!", "Bye! See you next time!");
        }
    }
}
