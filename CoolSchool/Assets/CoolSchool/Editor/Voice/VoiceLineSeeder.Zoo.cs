using System.Collections.Generic;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The voice lines of hide and seek in the zoo park (Docs/specs/hide-and-seek-contract.md): the invitation, the escaped
    /// animals, the counting with eyes closed, finding each animal, how many are left, the hints (a first-sound clue, the
    /// position words, a shake, a star trail), his own turn to hide, the recap of where each animal was, and the victory.
    /// The zookeeper is a classmate, so every line except the invitation is spoken by Kid. Numbers, letter sounds and
    /// right/wrong come from the existing number.*, letter.sound.* and lesson.* lines.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        static IEnumerable<Seed> ZooSeeds()
        {
            // The invitation and the start.
            yield return new Seed("invite.zoo.1", Speaker.Teacher, "Help! De dieren uit de dierentuin zijn ontsnapt! Kom je zoeken?", "Help! The zoo animals have escaped! Will you come and look?");
            yield return new Seed("zoo.start.1", Speaker.Kid, "Help! Ik ben de dierenverzorger, en de dieren zijn ontsnapt!", "Help! I'm the zookeeper, and the animals have escaped!");
            yield return new Seed("zoo.escaped.1", Speaker.Kid, "Ze hebben zich verstopt in het park. Zoek jij mee?", "They're hiding in the park. Will you help me look?");

            // Counting with eyes closed.
            yield return new Seed("zoo.close_eyes.1", Speaker.Kid, "Eerst ogen dicht en tellen! Tel je mee?", "First close your eyes and count! Count with me?");
            yield return new Seed("zoo.ready.1", Speaker.Kid, "Wie niet weg is, is gezien! We komen eraan!", "Ready or not, here we come!");

            // Finding an animal.
            yield return new Seed("zoo.found_new.1", Speaker.Kid, "Een nieuw dier!", "A new animal!");
            yield return new Seed("zoo.found.elephant.1", Speaker.Kid, "Gevonden! De olifant!", "Found you! The elephant!");
            yield return new Seed("zoo.found.giraffe.1", Speaker.Kid, "Gevonden! De giraf!", "Found you! The giraffe!");
            yield return new Seed("zoo.found.monkey.1", Speaker.Kid, "Gevonden! De aap!", "Found you! The monkey!");
            yield return new Seed("zoo.found.penguin.1", Speaker.Kid, "Gevonden! De pinguïn!", "Found you! The penguin!");
            yield return new Seed("zoo.found.hippo.1", Speaker.Kid, "Gevonden! Het nijlpaard!", "Found you! The hippo!");
            yield return new Seed("zoo.found.lion.1", Speaker.Kid, "Gevonden! De leeuw!", "Found you! The lion!");
            yield return new Seed("zoo.found.zebra.1", Speaker.Kid, "Gevonden! De zebra!", "Found you! The zebra!");
            yield return new Seed("zoo.found.crocodile.1", Speaker.Kid, "Gevonden! De krokodil!", "Found you! The crocodile!");
            yield return new Seed("zoo.found.flamingo.1", Speaker.Kid, "Gevonden! De flamingo!", "Found you! The flamingo!");
            yield return new Seed("zoo.found.tiger.1", Speaker.Kid, "Gevonden! De tijger!", "Found you! The tiger!");

            // How many are left.
            yield return new Seed("zoo.left.1.1", Speaker.Kid, "Nog één dier!", "One more animal!");
            yield return new Seed("zoo.left.2.1", Speaker.Kid, "Nog twee dieren!", "Two more animals!");
            yield return new Seed("zoo.left.3.1", Speaker.Kid, "Nog drie dieren!", "Three more animals!");
            yield return new Seed("zoo.left.4.1", Speaker.Kid, "Nog vier dieren!", "Four more animals!");
            yield return new Seed("zoo.how_many_left.1", Speaker.Kid, "Hoeveel dieren zijn er nog verstopt?", "How many animals are still hiding?");
            yield return new Seed("zoo.count_silhouettes.1", Speaker.Kid, "Tel de zwarte dieren maar!", "Count the black animals!");
            yield return new Seed("zoo.right.1", Speaker.Kid, "Goed zo!", "Well done!");
            yield return new Seed("zoo.all_found.1", Speaker.Kid, "Hoera! Alle dieren gevonden!", "Hooray! We found all the animals!");

            // An empty spot with a surprise critter.
            yield return new Seed("zoo.surprise.1", Speaker.Kid, "Oh! Dat is geen olifant!", "Oh! That's no elephant!");
            yield return new Seed("zoo.surprise.2", Speaker.Kid, "Hihi, verkeerd dier!", "Hehe, wrong animal!");
            yield return new Seed("zoo.surprise.3", Speaker.Kid, "Hier niet! Zoek maar verder!", "Not here! Keep looking!");

            // Hints: the first-sound clue, then where it is (the position words).
            yield return new Seed("zoo.clue.1", Speaker.Kid, "Ik hoor een dier met de…", "I can hear an animal with the…");
            yield return new Seed("zoo.hint.hedge.1", Speaker.Kid, "Ik hoor iets achter de heg!", "I can hear something behind the hedge!");
            yield return new Seed("zoo.hint.tree.1", Speaker.Kid, "Ik hoor iets achter de boom!", "I can hear something behind the tree!");
            yield return new Seed("zoo.hint.treetop.1", Speaker.Kid, "Ik hoor iets boven in de boom!", "I can hear something up in the tree!");
            yield return new Seed("zoo.hint.pond.1", Speaker.Kid, "Ik hoor iets in de vijver!", "I can hear something in the pond!");
            yield return new Seed("zoo.hint.bin.1", Speaker.Kid, "Ik hoor iets in de prullenbak!", "I can hear something in the bin!");
            yield return new Seed("zoo.hint.tent.1", Speaker.Kid, "Ik hoor iets in de tent!", "I can hear something in the tent!");
            yield return new Seed("zoo.hint.box.1", Speaker.Kid, "Ik hoor iets in de doos!", "I can hear something in the box!");
            yield return new Seed("zoo.hint.bush.1", Speaker.Kid, "Ik hoor iets achter de struik!", "I can hear something behind the bush!");
            yield return new Seed("zoo.hint.house.1", Speaker.Kid, "Ik hoor iets in het huisje!", "I can hear something in the little house!");
            yield return new Seed("zoo.hint.leaves.1", Speaker.Kid, "Ik hoor iets onder de bladeren!", "I can hear something under the leaves!");
            yield return new Seed("zoo.hint.table.1", Speaker.Kid, "Ik hoor iets onder de tafel!", "I can hear something under the table!");
            yield return new Seed("zoo.hint.rock.1", Speaker.Kid, "Ik hoor iets achter de steen!", "I can hear something behind the rock!");
            yield return new Seed("zoo.shake.1", Speaker.Kid, "Kijk! Daar beweegt iets!", "Look! Something's moving over there!");
            yield return new Seed("zoo.trail.1", Speaker.Kid, "Volg de sterretjes!", "Follow the little stars!");

            // His turn to hide, and the keeper searching.
            yield return new Seed("zoo.your_turn.1", Speaker.Kid, "Nu ben jij aan de beurt! Loop naar een lichtje en verstop je!", "Now it's your turn! Walk to a light and hide!");
            yield return new Seed("zoo.not_yet.1", Speaker.Kid, "Ben je al verstopt? Ik tel nog even door!", "Are you hidden yet? I'll keep counting!");
            yield return new Seed("zoo.seek_start.1", Speaker.Kid, "Klaar of niet, ik kom!", "Ready or not, here I come!");
            yield return new Seed("zoo.seek_wrong.1", Speaker.Kid, "Zit je hier? Nee… jij bent het niet!", "Are you here? No… that's not you!");
            yield return new Seed("zoo.seek_wrong.2", Speaker.Kid, "Hier dan? Nee, niemand!", "Here then? No, nobody!");
            yield return new Seed("zoo.seek_wrong.3", Speaker.Kid, "Hmm, hier ook niet! Waar ben je toch?", "Hmm, not here either! Where are you?");
            yield return new Seed("zoo.seek_wrong.4", Speaker.Kid, "Oei, verkeerd! Wat ben jij goed verstopt!", "Oops, wrong! You're hiding so well!");
            yield return new Seed("zoo.seek_close.1", Speaker.Kid, "Hmmm… ik hoor iets…", "Hmmm… I can hear something…");
            yield return new Seed("zoo.seek_found.1", Speaker.Kid, "Gevonden! Hihi, wat een goede verstopplek!", "Found you! Hehe, what a good hiding spot!");
            yield return new Seed("zoo.seek_found.2", Speaker.Kid, "Boe! Gevonden! Daar zat je!", "Boo! Found you! There you are!");

            // Back to the zoo: where was each animal.
            yield return new Seed("zoo.back_home.1", Speaker.Kid, "Breng de dieren terug naar de dierentuin!", "Bring the animals back to the zoo!");
            yield return new Seed("zoo.where.elephant.1", Speaker.Kid, "Waar zat de olifant?", "Where was the elephant?");
            yield return new Seed("zoo.where.giraffe.1", Speaker.Kid, "Waar zat de giraf?", "Where was the giraffe?");
            yield return new Seed("zoo.where.monkey.1", Speaker.Kid, "Waar zat de aap?", "Where was the monkey?");
            yield return new Seed("zoo.where.penguin.1", Speaker.Kid, "Waar zat de pinguïn?", "Where was the penguin?");
            yield return new Seed("zoo.where.hippo.1", Speaker.Kid, "Waar zat het nijlpaard?", "Where was the hippo?");
            yield return new Seed("zoo.where.lion.1", Speaker.Kid, "Waar zat de leeuw?", "Where was the lion?");
            yield return new Seed("zoo.where.zebra.1", Speaker.Kid, "Waar zat de zebra?", "Where was the zebra?");
            yield return new Seed("zoo.where.crocodile.1", Speaker.Kid, "Waar zat de krokodil?", "Where was the crocodile?");
            yield return new Seed("zoo.where.flamingo.1", Speaker.Kid, "Waar zat de flamingo?", "Where was the flamingo?");
            yield return new Seed("zoo.where.tiger.1", Speaker.Kid, "Waar zat de tijger?", "Where was the tiger?");
            yield return new Seed("zoo.spot.hedge.1", Speaker.Kid, "Achter de heg!", "Behind the hedge!");
            yield return new Seed("zoo.spot.tree.1", Speaker.Kid, "Achter de boom!", "Behind the tree!");
            yield return new Seed("zoo.spot.treetop.1", Speaker.Kid, "Boven in de boom!", "Up in the tree!");
            yield return new Seed("zoo.spot.pond.1", Speaker.Kid, "In de vijver!", "In the pond!");
            yield return new Seed("zoo.spot.bin.1", Speaker.Kid, "In de prullenbak!", "In the bin!");
            yield return new Seed("zoo.spot.tent.1", Speaker.Kid, "In de tent!", "In the tent!");
            yield return new Seed("zoo.spot.box.1", Speaker.Kid, "In de doos!", "In the box!");
            yield return new Seed("zoo.spot.bush.1", Speaker.Kid, "Achter de struik!", "Behind the bush!");
            yield return new Seed("zoo.spot.house.1", Speaker.Kid, "In het huisje!", "In the little house!");
            yield return new Seed("zoo.spot.leaves.1", Speaker.Kid, "Onder de bladeren!", "Under the leaves!");
            yield return new Seed("zoo.spot.table.1", Speaker.Kid, "Onder de tafel!", "Under the table!");
            yield return new Seed("zoo.spot.rock.1", Speaker.Kid, "Achter de steen!", "Behind the rock!");

            // How many did we find, and the party.
            yield return new Seed("zoo.how_many_found.1", Speaker.Kid, "Hoeveel dieren hebben we gevonden?", "How many animals did we find?");
            yield return new Seed("zoo.victory.1", Speaker.Kid, "Alle dieren zijn weer thuis! Dank je wel, dierenvriend!", "All the animals are home again! Thank you, animal friend!");
        }
    }
}
