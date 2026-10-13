using System.Collections.Generic;
using CoolSchool.Voice;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/CoolSchool/Voice/Lines.asset from the seed list below.
    /// Lines are matched by key: text is updated, recorded clips are never cleared, new keys are added.
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        const string RootFolder = "Assets/CoolSchool";
        public const string TablePath = RootFolder + "/Voice/Lines.asset";

        /// <summary>One line to seed. The group is the key without its trailing number.</summary>
        readonly struct Seed
        {
            public readonly string Key;
            public readonly Speaker Speaker;
            public readonly string Dutch;
            public readonly string English;

            public Seed(string key, Speaker speaker, string dutch, string english)
            {
                Key = key;
                Speaker = speaker;
                Dutch = dutch;
                English = english;
            }

            public string Group => Key.Substring(0, Key.LastIndexOf('.'));
        }

        static readonly Seed[] Seeds =
        {
            new Seed("general.welcome.1", Speaker.Teacher, "Hoi! Wat fijn dat je er bent. Kom, we gaan spelen!", "Hi! So nice to see you. Come on, let's play!"),

            new Seed("tag.start.1", Speaker.Teacher, "Tikkertje! Jij bent hem. Tik de andere kinderen!", "Tag! You're it. Go and tag the other kids!"),
            new Seed("tag.tagged_someone.1", Speaker.Kid, "Oh nee, getikt!", "Oh no, you got me!"),
            new Seed("tag.tagged_someone.2", Speaker.Kid, "Haha, jij bent snel!", "Haha, you're fast!"),
            new Seed("tag.tagged_someone.3", Speaker.Kid, "Oké, nu ben ik hem!", "Okay, now I'm it!"),
            new Seed("tag.became_it.1", Speaker.Kid, "Hihi, nu ben jij hem!", "Hehe, now you're it!"),
            new Seed("tag.became_it.2", Speaker.Kid, "Getikt! Jij bent hem!", "Tagged! You're it!"),
            new Seed("tag.round_finished.1", Speaker.Teacher, "Wat een goed spel! Even uitrusten.", "What a great game! Time for a little rest."),

            new Seed("knights.start.1", Speaker.Teacher, "Ridders! Bescherm het kasteel! Tik op de knop om met je zwaard te zwaaien.", "Knights! Protect the castle! Tap the button to swing your sword."),
            new Seed("knights.golden_sword.1", Speaker.Teacher, "Kijk! Je gouden zwaard! Daarmee ben je extra sterk!", "Look! Your golden sword! It makes you extra strong!"),
            new Seed("knights.shield_hint.1", Speaker.Teacher, "Houd de knop ingedrukt, dan pak je je schild!", "Hold the button down to raise your shield!"),
            new Seed("knights.wave.1", Speaker.Kid, "Daar komen de kobolden!", "Here come the goblins!"),
            new Seed("knights.wave_more.1", Speaker.Kid, "Nog meer kobolden!", "Even more goblins!"),
            new Seed("knights.goblin_down.1", Speaker.Goblin, "Hihihi, je hebt me!", "Hehehe, you got me!"),
            new Seed("knights.goblin_down.2", Speaker.Goblin, "Oei, ik val om!", "Whoops, I'm falling over!"),
            new Seed("knights.goblin_down.3", Speaker.Goblin, "Haha, jij bent sterk!", "Haha, you're strong!"),
            new Seed("knights.dragon.1", Speaker.Teacher, "Kijk! Een draak! Hij wil alleen spelen. Pas op voor zijn confettivuur!", "Look! A dragon! He just wants to play. Watch out for his confetti fire!"),
            new Seed("knights.fire_warning.1", Speaker.Kid, "Schild omhoog!", "Shields up!"),
            new Seed("knights.fire_warning.2", Speaker.Kid, "Pas op, vuur!", "Watch out, fire!"),
            new Seed("knights.victory.1", Speaker.Teacher, "Hoera! Jullie hebben het kasteel gered!", "Hooray! You saved the castle!"),

            new Seed("invite.tag.1", Speaker.Teacher, "Kom, we gaan tikkertje spelen! Volg de sterretjes!", "Come on, let's play tag! Follow the stars!"),
            new Seed("invite.knights.1", Speaker.Teacher, "Kom, we gaan ridders spelen bij het kasteel! Volg de sterretjes!", "Come on, let's play knights at the castle! Follow the stars!"),
            new Seed("invite.knights.2", Speaker.Teacher, "Zie je dat zwaard daar? Daar is het kasteel!", "See that sword over there? That's the castle!"),
            new Seed("invite.knights.golden.1", Speaker.Teacher, "Je gouden zwaard wacht op je bij het kasteel!", "Your golden sword is waiting for you at the castle!"),

            // The alien landing (superhero ray gun game).
            new Seed("aliens.start.1", Speaker.Teacher, "Kijk! Er landt een raket! Er komen aliens uit! Jij bent de superheld!", "Look! A rocket is landing! Aliens are coming out! You're the superhero!"),
            new Seed("aliens.zap_hint.1", Speaker.Teacher, "Tik op de knop, dan flits je met je straalpistool!", "Tap the button to zap with your ray gun!"),
            new Seed("aliens.slime_warning.1", Speaker.Kid, "Pas op, slijm!", "Watch out, slime!"),
            new Seed("aliens.zapped.1", Speaker.Alien, "Wiehoe! Ik wil dansen!", "Wheee! I want to dance!"),
            new Seed("aliens.zapped.2", Speaker.Alien, "Hihi, jij bent mijn vriend!", "Hehe, you're my friend!"),
            new Seed("aliens.zapped.3", Speaker.Alien, "Bliep bloep, wat leuk!", "Bleep bloop, what fun!"),
            new Seed("aliens.rescue.1", Speaker.Teacher, "Help! De baby-alien zit vast in de boom! Spring op de trampoline!", "Help! The baby alien is stuck in the tree! Jump on the trampoline!"),
            new Seed("aliens.rescued.1", Speaker.Teacher, "Je hebt hem gered! Wat een held!", "You saved him! What a hero!"),
            new Seed("aliens.count.1", Speaker.Teacher, "Hoeveel aliens dansen er?", "How many aliens are dancing?"),
            new Seed("aliens.victory.1", Speaker.Teacher, "Hoera! Alle aliens zijn onze vrienden! Feest!", "Hooray! All the aliens are our friends! Party!"),
            new Seed("invite.aliens.1", Speaker.Teacher, "Kom snel! Er zijn aliens geland!", "Come quickly! Aliens have landed!"),

            // Alien landing variety (kinds, spawning everywhere, rescue kinds).
            new Seed("aliens.arrive.1", Speaker.Teacher, "Kijk goed, ze komen overal vandaan!", "Look closely, they're coming from everywhere!"),
            new Seed("aliens.big_hint.1", Speaker.Teacher, "Wat een grote alien! Flits hem een paar keer!", "What a big alien! Zap it a few times!"),
            new Seed("aliens.big_hit.1", Speaker.Alien, "Hihi, ik krimp!", "Hehe, I'm shrinking!"),
            new Seed("aliens.big_hit.2", Speaker.Alien, "Oei, ik word kleiner!", "Oops, I'm getting smaller!"),
            new Seed("aliens.tiny_hint.1", Speaker.Teacher, "Die kleine is snel! Als hij moe is, kun je hem flitsen!", "That little one is fast! When he's tired, you can zap him!"),
            new Seed("aliens.tiny_tease.1", Speaker.Alien, "Hihi, vang me dan!", "Hehe, catch me!"),
            new Seed("aliens.tiny_tired.1", Speaker.Alien, "Pfoe, ik ben moe!", "Phew, I'm tired!"),
            new Seed("aliens.shy_hint.1", Speaker.Teacher, "Daar verstopt er eentje! Loop ernaartoe!", "One is hiding there! Walk over to it!"),
            new Seed("aliens.shy_peek.1", Speaker.Alien, "Kiekeboe!", "Peekaboo!"),
            new Seed("aliens.robot_zapped.1", Speaker.Alien, "Bliep bliep! Robotdans!", "Beep beep! Robot dance!"),
            new Seed("aliens.rescue_rocket.1", Speaker.Teacher, "Help! De baby-alien zit bovenop de raket! Spring op de trampoline!", "Help! The baby alien is on top of the rocket! Jump on the trampoline!"),
            new Seed("aliens.rescue_bubble.1", Speaker.Teacher, "Kijk! De baby-alien zweeft in een bel! Spring omhoog en pak hem!", "Look! The baby alien is floating in a bubble! Jump up and grab him!"),
            new Seed("aliens.rescue_slime.1", Speaker.Teacher, "Oh nee, de baby-alien zit vast in het slijm! Flits het slijm weg!", "Oh no, the baby alien is stuck in the slime! Zap the slime away!"),
            new Seed("aliens.slime_free.1", Speaker.Alien, "Dankjewel! Ik ben weer vrij!", "Thank you! I'm free again!"),

            // Stopping a game with the back arrow.
            new Seed("quit.ask.1", Speaker.Teacher, "Wil je stoppen met dit spel?", "Do you want to stop this game?"),
            new Seed("quit.bye.1", Speaker.Teacher, "Oké! Dan gaan we weer lekker buiten spelen.", "Okay! Let's go and play outside again."),
            new Seed("quit.bye.2", Speaker.Teacher, "Prima! Je mag altijd weer terugkomen.", "Fine! You can always come back."),

            // The ice cream shop (create and role-play).
            new Seed("invite.shop.1", Speaker.Teacher, "Wie wil er ijsjes verkopen? Kom naar de ijswinkel!", "Who wants to sell ice cream? Come to the ice cream shop!"),
            new Seed("invite.shop.2", Speaker.Teacher, "Mmm, ijs! Kom jij in de ijswinkel helpen?", "Mmm, ice cream! Will you help in the ice cream shop?"),
            new Seed("shop.start.1", Speaker.Narrator, "Jij bent de ijsverkoper! Daar komt je eerste klant.", "You're the ice cream seller! Here comes your first customer."),
            new Seed("shop.howto.1", Speaker.Narrator, "Pak een bolletje uit de bak en zet het op het hoorntje!", "Take a scoop from the tub and put it on the cone!"),
            new Seed("shop.hello.kid.1", Speaker.Kid, "Hallo! Mag ik een ijsje, alsjeblieft?", "Hello! Can I have an ice cream, please?"),
            new Seed("shop.hello.kid.2", Speaker.Kid, "Hoi! Ik heb zin in ijs!", "Hi! I'd love an ice cream!"),
            new Seed("shop.hello.knight.1", Speaker.Kid, "Hallo ijsverkoper! Een ijsje voor een dappere ridder!", "Hello ice cream seller! An ice cream for a brave knight!"),
            new Seed("shop.hello.alien.1", Speaker.Alien, "Bliep bloep! Ijsje, alsjeblieft!", "Bleep bloop! Ice cream, please!"),
            new Seed("shop.dragon_arrives.1", Speaker.Narrator, "Wie komt daar aan? De draak wil ook een ijsje!", "Who's coming? The dragon wants an ice cream too!"),
            new Seed("shop.hello.dragon.1", Speaker.Goblin, "Grrroaar! Ijsje! Een groot ijsje!", "Grrroar! Ice cream! A big ice cream!"),
            new Seed("shop.order.1.pink.1", Speaker.Narrator, "Eén roze bolletje.", "One pink scoop."),
            new Seed("shop.order.2.pink.1", Speaker.Narrator, "Twee roze bolletjes.", "Two pink scoops."),
            new Seed("shop.order.3.pink.1", Speaker.Narrator, "Drie roze bolletjes.", "Three pink scoops."),
            new Seed("shop.order.1.brown.1", Speaker.Narrator, "Eén bruin bolletje.", "One brown scoop."),
            new Seed("shop.order.2.brown.1", Speaker.Narrator, "Twee bruine bolletjes.", "Two brown scoops."),
            new Seed("shop.order.3.brown.1", Speaker.Narrator, "Drie bruine bolletjes.", "Three brown scoops."),
            new Seed("shop.order.1.yellow.1", Speaker.Narrator, "Eén geel bolletje.", "One yellow scoop."),
            new Seed("shop.order.2.yellow.1", Speaker.Narrator, "Twee gele bolletjes.", "Two yellow scoops."),
            new Seed("shop.order.3.yellow.1", Speaker.Narrator, "Drie gele bolletjes.", "Three yellow scoops."),
            new Seed("shop.order.1.green.1", Speaker.Narrator, "Eén groen bolletje.", "One green scoop."),
            new Seed("shop.order.2.green.1", Speaker.Narrator, "Twee groene bolletjes.", "Two green scoops."),
            new Seed("shop.order.3.green.1", Speaker.Narrator, "Drie groene bolletjes.", "Three green scoops."),
            new Seed("shop.wrong_colour.1", Speaker.Narrator, "Oeps, die kleur niet! Kijk maar naar het plaatje.", "Oops, not that colour! Look at the picture."),
            new Seed("shop.wrong_colour.2", Speaker.Narrator, "Hmm, welke kleur wil de klant?", "Hmm, which colour does the customer want?"),
            new Seed("shop.done.1", Speaker.Narrator, "Klaar! Tel maar mee.", "Done! Let's count."),
            new Seed("shop.serve.1", Speaker.Kid, "Alsjeblieft!", "Here you go!"),
            new Seed("shop.thanks.kid.1", Speaker.Kid, "Mmm, lekker! Dankjewel!", "Mmm, yummy! Thank you!"),
            new Seed("shop.thanks.kid.2", Speaker.Kid, "Jammie! Dankjewel!", "Yummy! Thank you!"),
            new Seed("shop.thanks.knight.1", Speaker.Kid, "Lekker! Nu ben ik nog sterker!", "Yummy! Now I'm even stronger!"),
            new Seed("shop.thanks.alien.1", Speaker.Alien, "Bliep! Lekker koud! Dankjewel!", "Bleep! Nice and cold! Thank you!"),
            new Seed("shop.thanks.dragon.1", Speaker.Goblin, "Mmmm! Brrr, koud! Dankjewel!", "Mmmm! Brrr, cold! Thank you!"),
            new Seed("shop.pay.1", Speaker.Narrator, "Nu betalen! Tik op de muntjes en tel mee.", "Time to pay! Tap the coins and count along."),
            new Seed("shop.paid.1", Speaker.Narrator, "Ka-ching! Goed geteld!", "Ka-ching! Well counted!"),
            new Seed("shop.paid.2", Speaker.Narrator, "Alle muntjes zitten in de kassa!", "All the coins are in the register!"),
            new Seed("shop.next.1", Speaker.Narrator, "Daar komt de volgende klant!", "Here comes the next customer!"),
            new Seed("shop.hint.1", Speaker.Narrator, "Kijk naar het plaatje boven de klant.", "Look at the picture above the customer."),
            new Seed("shop.end.1", Speaker.Teacher, "Wat ben jij een goede ijsverkoper! Hier zijn je sterren.", "What a good ice cream seller you are! Here are your stars."),

            // The pirate cove (ship, skeleton duel, treasure hunt). Lorre the parrot is the guide.
            new Seed("invite.pirates.1", Speaker.Teacher, "Ahoi! Het piratenschip wacht op jou!", "Ahoy! The pirate ship is waiting for you!"),
            new Seed("pirates.start.1", Speaker.Parrot, "Ahoi, kapitein! Ik ben Lorre de papegaai. Kom aan boord!", "Ahoy, captain! I'm Lorre the parrot. Come aboard!"),
            new Seed("pirates.crew.1", Speaker.Kid, "Ahoi! Wij varen mee!", "Ahoy! We're sailing with you!"),
            new Seed("pirates.steer.1", Speaker.Parrot, "Jij bent de kapitein! Stuur het schip naar het eiland!", "You're the captain! Steer the ship to the island!"),
            new Seed("pirates.balloons.1", Speaker.Parrot, "Kijk! Een skelettenboot vol ballonnen! Tik op de knop. Boem!", "Look! A skeleton boat full of balloons! Tap the button. Boom!"),
            new Seed("pirates.cannon_hint.1", Speaker.Parrot, "Tik op de knop, dan schiet het kanon!", "Tap the button to fire the cannon!"),
            new Seed("pirates.balloons_done.1", Speaker.Parrot, "Alle ballonnen zijn knal! De skeletten varen naar het eiland!", "All the balloons popped! The skeletons are sailing to the island!"),
            new Seed("pirates.arrive.1", Speaker.Parrot, "Land in zicht! Iedereen van boord!", "Land ahoy! Everybody off the ship!"),
            new Seed("pirates.skeletons.1", Speaker.Goblin, "Arrr! Wij zijn de skelettenpiraten!", "Arrr! We're the skeleton pirates!"),
            new Seed("pirates.fight_hint.1", Speaker.Parrot, "Tik op de knop en zwaai met je zwaard!", "Tap the button and swing your sword!"),
            new Seed("pirates.skeleton_friend.1", Speaker.Goblin, "Hihi, mijn botjes kriebelen!", "Hehe, my bones tickle!"),
            new Seed("pirates.skeleton_friend.2", Speaker.Goblin, "Oeps, ik val uit elkaar! Mag ik bij jullie?", "Oops, I fell apart! Can I join you?"),
            new Seed("pirates.skeleton_friend.3", Speaker.Goblin, "Klik klak, ik ben weer heel! Vrienden?", "Clickety-clack, I'm back together! Friends?"),
            new Seed("pirates.map.1", Speaker.Parrot, "Een schatkaart! Volg de plaatjes naar de schat!", "A treasure map! Follow the pictures to the treasure!"),
            new Seed("pirates.landmark.1", Speaker.Parrot, "Goed zo! Nu de volgende!", "Well done! Now the next one!"),
            new Seed("pirates.landmark.2", Speaker.Parrot, "Gevonden! Verder maar!", "Found it! Keep going!"),
            new Seed("pirates.dig.1", Speaker.Parrot, "Hier is de X! Tik op de knop om te graven!", "Here's the X! Tap the button to dig!"),
            new Seed("pirates.lock.1", Speaker.Parrot, "De kist zit op slot! Het slot gaat open met de…", "The chest is locked! The lock opens with the…"),
            new Seed("pirates.lock_new.1", Speaker.Parrot, "Dit is een nieuwe letter! Luister maar:", "This is a new letter! Listen:"),
            new Seed("pirates.lock_right.1", Speaker.Parrot, "Klik! Het slot is open!", "Click! The lock is open!"),
            new Seed("pirates.lock_wrong.1", Speaker.Parrot, "Bijna! Luister nog eens:", "Almost! Listen again:"),
            new Seed("pirates.count.1", Speaker.Parrot, "Goud! Tik op elke munt, dan tellen we samen!", "Gold! Tap every coin and we'll count together!"),
            new Seed("pirates.how_many.1", Speaker.Parrot, "Hoeveel munten hebben we gevonden?", "How many coins did we find?"),
            new Seed("pirates.victory.1", Speaker.Parrot, "Hoera! We hebben de schat! Piratenfeest!", "Hooray! We found the treasure! Pirate party!"),

            // The soapbox race. The announcer is the friendly neighbour with the chequered flag.
            new Seed("invite.race.1", Speaker.Teacher, "Wie doet er mee met de zeepkistrace?", "Who wants to join the soapbox race?"),
            new Seed("race.welcome.1", Speaker.Announcer, "Welkom bij de grote zeepkistrace! Stap maar in je kar!", "Welcome to the big soapbox race! Hop into your cart!"),
            new Seed("race.howto.1", Speaker.Announcer, "Tik links of rechts om te sturen. Tik op de knop om te springen!", "Tap left or right to steer. Tap the button to jump!"),
            new Seed("race.count3.1", Speaker.Announcer, "Drie!", "Three!"),
            new Seed("race.count2.1", Speaker.Announcer, "Twee!", "Two!"),
            new Seed("race.count1.1", Speaker.Announcer, "Eén!", "One!"),
            new Seed("race.go.1", Speaker.Announcer, "Start!", "Go!"),
            new Seed("race.gate_number.1.1", Speaker.Announcer, "Rij door de één!", "Drive through the one!"),
            new Seed("race.gate_number.2.1", Speaker.Announcer, "Rij door de twee!", "Drive through the two!"),
            new Seed("race.gate_number.3.1", Speaker.Announcer, "Rij door de drie!", "Drive through the three!"),
            new Seed("race.gate_number.4.1", Speaker.Announcer, "Rij door de vier!", "Drive through the four!"),
            new Seed("race.gate_number.5.1", Speaker.Announcer, "Rij door de vijf!", "Drive through the five!"),
            new Seed("race.gate_number.6.1", Speaker.Announcer, "Rij door de zes!", "Drive through the six!"),
            new Seed("race.gate_number.7.1", Speaker.Announcer, "Rij door de zeven!", "Drive through the seven!"),
            new Seed("race.gate_number.8.1", Speaker.Announcer, "Rij door de acht!", "Drive through the eight!"),
            new Seed("race.gate_number.9.1", Speaker.Announcer, "Rij door de negen!", "Drive through the nine!"),
            new Seed("race.gate_number.10.1", Speaker.Announcer, "Rij door de tien!", "Drive through the ten!"),
            new Seed("race.gate_letter.1", Speaker.Announcer, "Rij door de letter…", "Drive through the letter…"),
            new Seed("race.gate_right.1", Speaker.Announcer, "Ja, goed zo!", "Yes, well done!"),
            new Seed("race.gate_right.2", Speaker.Announcer, "Super! Precies goed!", "Super! Exactly right!"),
            new Seed("race.gate_right.3", Speaker.Announcer, "Knap gedaan!", "Nicely done!"),
            new Seed("race.gate_wrong.1", Speaker.Announcer, "Bijna! Kijk, daar was hij!", "Almost! Look, there it was!"),
            new Seed("race.crash.1", Speaker.Announcer, "Boem! Gelukkig is het zacht!", "Boom! Good thing it's soft!"),
            new Seed("race.crash.2", Speaker.Announcer, "Oeps! Rij maar door!", "Oops! Keep going!"),
            new Seed("race.splash.1", Speaker.Announcer, "Plons! Lekker nat!", "Splash! Nice and wet!"),
            new Seed("race.animal_cat.1", Speaker.Announcer, "Kijk uit, een poes!", "Watch out, a cat!"),
            new Seed("race.animal_dog.1", Speaker.Announcer, "Kijk uit, een hond!", "Watch out, a dog!"),
            new Seed("race.animal_hen.1", Speaker.Announcer, "Kijk uit, een kip met kuikentjes!", "Watch out, a hen with her chicks!"),
            new Seed("race.animal_rabbit.1", Speaker.Announcer, "Kijk uit, een konijn!", "Watch out, a rabbit!"),
            new Seed("race.ramp.1", Speaker.Announcer, "Een schans! Hup, de lucht in!", "A ramp! Up into the air!"),
            new Seed("race.final.1", Speaker.Announcer, "Bijna bij de finish! Zet hem op!", "Almost at the finish! Go for it!"),
            new Seed("race.win.1", Speaker.Announcer, "Jij hebt gewonnen! Hoera voor de kampioen!", "You won! Hooray for the champion!"),
            new Seed("race.second.1", Speaker.Announcer, "Wat een spannende race! Jullie waren allemaal supersnel!", "What an exciting race! You were all super fast!"),
            new Seed("race.end.1", Speaker.Announcer, "Tot de volgende race!", "See you at the next race!"),
            new Seed("race.rival.1", Speaker.Kid, "Ik ga je inhalen!", "I'm going to catch you!"),
            new Seed("race.rival.2", Speaker.Kid, "Wie is er het snelst?", "Who's the fastest?"),
            new Seed("race.bump.1", Speaker.Kid, "Wiee! Ik draai rondjes!", "Wheee! I'm spinning!"),
            new Seed("race.bump.2", Speaker.Kid, "Hihi, botsauto!", "Hehe, bumper car!"),

            new Seed("reward.star.1", Speaker.Narrator, "Een ster!", "A star!"),
            new Seed("reward.jar_full.1", Speaker.Teacher, "De pot is vol! Je krijgt een cadeautje!", "The jar is full! You get a present!"),
            new Seed("reward.present_tap.1", Speaker.Teacher, "Tik op het cadeau!", "Tap the present!"),
            new Seed("reward.present_open.1", Speaker.Teacher, "Wauw! Kijk eens wat erin zit!", "Wow! Look what's inside!"),

            // Toy room and equipment (contract Docs/specs/toy-room-and-equipment-contract.md): the swap question in the present popup.
            new Seed("reward.swap_sword.1", Speaker.Teacher, "Wil je je nieuwe zwaard gebruiken?", "Do you want to use your new sword?"),
            new Seed("reward.swap_shield.1", Speaker.Teacher, "Wil je je nieuwe schild gebruiken?", "Do you want to use your new shield?"),
            new Seed("reward.swap_hat.1", Speaker.Teacher, "Wil je hem meteen opzetten?", "Do you want to put it on right away?"),
            new Seed("reward.swap_buddy.1", Speaker.Teacher, "Mag je nieuwe knuffel met je mee?", "Can your new cuddly toy come along?"),
            new Seed("reward.swap_yes.1", Speaker.Teacher, "Goed gekozen!", "Good choice!"),
            new Seed("reward.swap_yes.2", Speaker.Teacher, "Super!", "Super!"),
            new Seed("reward.swap_no.1", Speaker.Teacher, "Oké! Hij wacht op je in je speelgoedkamer.", "Okay! It's waiting for you in your toy room."),
            new Seed("reward.cart_new.1", Speaker.Teacher, "Die kun je kiezen bij de race!", "You can pick it at the race!"),
            new Seed("reward.medal.1", Speaker.Teacher, "Een medaille! Die hangt in je speelgoedkamer.", "A medal! It hangs in your toy room."),

            // The Toy Room and its dress-up mirror.
            new Seed("toyroom.invite.1", Speaker.Teacher, "Kom kijken in je speelgoedkamer!", "Come and look in your toy room!"),
            new Seed("toyroom.welcome.1", Speaker.Narrator, "Dit is jouw speelgoedkamer! Hier staat alles wat je hebt verdiend.", "This is your toy room! Everything you've earned is here."),
            new Seed("toyroom.new_item.1", Speaker.Narrator, "Kijk, iets nieuws!", "Look, something new!"),
            new Seed("toyroom.medal.1", Speaker.Narrator, "Je hebt een nieuwe medaille!", "You have a new medal!"),
            new Seed("wardrobe.welcome.1", Speaker.Narrator, "Wat wil je aan?", "What do you want to wear?"),
            new Seed("wardrobe.pick.1", Speaker.Narrator, "Mooi!", "Nice!"),
            new Seed("wardrobe.pick.2", Speaker.Narrator, "Wat stoer!", "So cool!"),
            new Seed("wardrobe.locked.1", Speaker.Narrator, "Die moet je nog verdienen. Vul de sterrenpot!", "You still have to earn that one. Fill the star jar!"),
            new Seed("wardrobe.done.1", Speaker.Narrator, "Klaar! Je ziet er geweldig uit!", "Done! You look great!"),

            // The cart picker at the start of the soapbox race.
            new Seed("race.pick_cart.1", Speaker.Announcer, "Kies je kar!", "Pick your cart!"),
            new Seed("race.cart_locked.1", Speaker.Announcer, "Die kar moet je nog winnen!", "You still have to win that one!"),
            new Seed("race.cart_new.1", Speaker.Announcer, "Ooh, een nieuwe kar! Probeer hem uit!", "Ooh, a new cart! Try it out!"),
            new Seed("race.cart_ready.1", Speaker.Announcer, "Daar gaan we!", "Here we go!"),

            // The name of each item, said when it is revealed and when he walks past it in the Toy Room (gold variants share item.gold).
            new Seed("item.gold.1", Speaker.Narrator, "Wauw, goud!", "Wow, gold!"),
            new Seed("item.sword.foam.1", Speaker.Narrator, "Het schuimzwaard!", "The foam sword!"),
            new Seed("item.shield.basic.1", Speaker.Narrator, "Het schild!", "The shield!"),
            new Seed("item.cart.racer_1.1", Speaker.Narrator, "Kar één!", "Cart one!"),
            new Seed("item.sword.plastic.1", Speaker.Narrator, "Het plastic zwaard!", "The plastic sword!"),
            new Seed("item.hat.knight_helmet.1", Speaker.Narrator, "De ridderhelm!", "The knight's helmet!"),
            new Seed("item.plush.dino.1", Speaker.Narrator, "De dino!", "The dino!"),
            new Seed("item.cart.flag.1", Speaker.Narrator, "De vlag!", "The flag!"),
            new Seed("item.sword.wood.1", Speaker.Narrator, "Het houten zwaard!", "The wooden sword!"),
            new Seed("item.shield.round.1", Speaker.Narrator, "Het ronde schild!", "The round shield!"),
            new Seed("item.cart.racer_2.1", Speaker.Narrator, "Kar twee!", "Cart two!"),
            new Seed("item.plush.parrot.1", Speaker.Narrator, "Lorre de papegaai!", "Lorre the parrot!"),
            new Seed("item.plush.unicorn.1", Speaker.Narrator, "De eenhoorn!", "The unicorn!"),
            new Seed("item.hat.cap.1", Speaker.Narrator, "De pet!", "The cap!"),
            new Seed("item.cart.stickers.1", Speaker.Narrator, "De stickers!", "The stickers!"),
            new Seed("item.sword.pirate.1", Speaker.Narrator, "Het piratenzwaard!", "The pirate sword!"),
            new Seed("item.sword.foam_big.1", Speaker.Narrator, "Het grote schuimzwaard!", "The big foam sword!"),
            new Seed("item.plush.bear.1", Speaker.Narrator, "De beer!", "The bear!"),
            new Seed("item.cart.racer_3.1", Speaker.Narrator, "Kar drie!", "Cart three!"),
            new Seed("item.hat.pirate.1", Speaker.Narrator, "De piratenhoed!", "The pirate hat!"),
            new Seed("item.cart.gold.1", Speaker.Narrator, "De gouden verf!", "The gold paint!"),

            // The numbers lesson. Each number is its own group (number.3), so it can be said on its own.
            new Seed("number.1.1", Speaker.Teacher, "Eén", "One"),
            new Seed("number.2.1", Speaker.Teacher, "Twee", "Two"),
            new Seed("number.3.1", Speaker.Teacher, "Drie", "Three"),
            new Seed("number.4.1", Speaker.Teacher, "Vier", "Four"),
            new Seed("number.5.1", Speaker.Teacher, "Vijf", "Five"),
            new Seed("number.6.1", Speaker.Teacher, "Zes", "Six"),
            new Seed("number.7.1", Speaker.Teacher, "Zeven", "Seven"),
            new Seed("number.8.1", Speaker.Teacher, "Acht", "Eight"),
            new Seed("number.9.1", Speaker.Teacher, "Negen", "Nine"),
            new Seed("number.10.1", Speaker.Teacher, "Tien", "Ten"),

            new Seed("invite.lesson.1", Speaker.Teacher, "De bel gaat! Kom maar naar de klas!", "There's the bell! Come to class!"),
            new Seed("invite.lesson.2", Speaker.Teacher, "Kom naar de klas, we gaan leren! Volg de sterretjes!", "Come to class, let's learn! Follow the stars!"),

            new Seed("lesson.intro.1", Speaker.Teacher, "Welkom in de klas! Vandaag gaan we tellen.", "Welcome to class! Today we're going to count."),
            new Seed("lesson.total.1", Speaker.Teacher, "Het zijn er…", "There are…"),
            new Seed("lesson.correct.1", Speaker.Teacher, "Goed zo!", "Well done!"),
            new Seed("lesson.correct.2", Speaker.Teacher, "Heel knap!", "Very clever!"),
            new Seed("lesson.correct.3", Speaker.Teacher, "Ja, dat klopt!", "Yes, that's right!"),
            new Seed("lesson.try_again.1", Speaker.Teacher, "Bijna! Probeer het nog eens.", "Almost! Try again."),
            new Seed("lesson.hint.1", Speaker.Teacher, "Tel maar mee!", "Let's count together!"),
            new Seed("lesson.find_number.1", Speaker.Teacher, "Waar is het getal…", "Where is the number…"),
            new Seed("lesson.outro.1", Speaker.Teacher, "Wat heb jij goed geteld! Hier zijn je sterren.", "You counted so well! Here are your stars."),
            new Seed("lesson.powerup.1", Speaker.Teacher, "En je krijgt een gouden zwaard voor bij het kasteel!", "And you get a golden sword for the castle!"),
            new Seed("lesson.powerup_waiting.1", Speaker.Teacher, "Je gouden zwaard ligt nog klaar bij het kasteel!", "Your golden sword is still waiting at the castle!"),

            // What the toys on the table are called: count them, ask how many, and put some in the basket.
            new Seed("lesson.count.duck.1", Speaker.Teacher, "Tel de eendjes! Tik op elk eendje.", "Count the ducks! Tap each duck."),
            new Seed("lesson.count.duck.2", Speaker.Teacher, "Kun jij de eendjes tellen? Tik ze maar aan!", "Can you count the ducks? Tap them!"),
            new Seed("lesson.howmany.duck.1", Speaker.Teacher, "Hoeveel eendjes zie je?", "How many ducks can you see?"),
            new Seed("lesson.howmany.duck.2", Speaker.Teacher, "Tel de eendjes. Hoeveel zijn het?", "Count the ducks. How many are there?"),
            new Seed("lesson.giveme.duck.1", Speaker.Teacher, "Doe zoveel eendjes in de mand:", "Put this many ducks in the basket:"),

            new Seed("lesson.count.ball.1", Speaker.Teacher, "Tel de ballen! Tik op elke bal.", "Count the balls! Tap each ball."),
            new Seed("lesson.count.ball.2", Speaker.Teacher, "Kun jij de ballen tellen? Tik ze maar aan!", "Can you count the balls? Tap them!"),
            new Seed("lesson.howmany.ball.1", Speaker.Teacher, "Hoeveel ballen zie je?", "How many balls can you see?"),
            new Seed("lesson.howmany.ball.2", Speaker.Teacher, "Tel de ballen. Hoeveel zijn het?", "Count the balls. How many are there?"),
            new Seed("lesson.giveme.ball.1", Speaker.Teacher, "Doe zoveel ballen in de mand:", "Put this many balls in the basket:"),

            new Seed("lesson.count.bear.1", Speaker.Teacher, "Tel de beertjes! Tik op elk beertje.", "Count the teddy bears! Tap each bear."),
            new Seed("lesson.count.bear.2", Speaker.Teacher, "Kun jij de beertjes tellen? Tik ze maar aan!", "Can you count the teddy bears? Tap them!"),
            new Seed("lesson.howmany.bear.1", Speaker.Teacher, "Hoeveel beertjes zie je?", "How many teddy bears can you see?"),
            new Seed("lesson.howmany.bear.2", Speaker.Teacher, "Tel de beertjes. Hoeveel zijn het?", "Count the teddy bears. How many are there?"),
            new Seed("lesson.giveme.bear.1", Speaker.Teacher, "Doe zoveel beertjes in de mand:", "Put this many teddy bears in the basket:"),

            new Seed("lesson.count.block.1", Speaker.Teacher, "Tel de blokken! Tik op elk blok.", "Count the blocks! Tap each block."),
            new Seed("lesson.count.block.2", Speaker.Teacher, "Kun jij de blokken tellen? Tik ze maar aan!", "Can you count the blocks? Tap them!"),
            new Seed("lesson.howmany.block.1", Speaker.Teacher, "Hoeveel blokken zie je?", "How many blocks can you see?"),
            new Seed("lesson.howmany.block.2", Speaker.Teacher, "Tel de blokken. Hoeveel zijn het?", "Count the blocks. How many are there?"),
            new Seed("lesson.giveme.block.1", Speaker.Teacher, "Doe zoveel blokken in de mand:", "Put this many blocks in the basket:"),

            new Seed("lesson.count.dino.1", Speaker.Teacher, "Tel de dino's! Tik op elke dino.", "Count the dinos! Tap each dino."),
            new Seed("lesson.count.dino.2", Speaker.Teacher, "Kun jij de dino's tellen? Tik ze maar aan!", "Can you count the dinos? Tap them!"),
            new Seed("lesson.howmany.dino.1", Speaker.Teacher, "Hoeveel dino's zie je?", "How many dinos can you see?"),
            new Seed("lesson.howmany.dino.2", Speaker.Teacher, "Tel de dino's. Hoeveel zijn het?", "Count the dinos. How many are there?"),
            new Seed("lesson.giveme.dino.1", Speaker.Teacher, "Doe zoveel dino's in de mand:", "Put this many dinos in the basket:"),

            // The letters lesson. The letter sounds and the words are added from the LetterWordTable (see LetterSeeds).
            new Seed("lesson.letters.intro.1", Speaker.Teacher, "Welkom in de klas! Vandaag gaan we letters leren.", "Welcome to class! Today we're learning letters."),
            new Seed("lesson.letters.outro.1", Speaker.Teacher, "Wat heb jij goed geleerd! Hier zijn je sterren.", "You learned so well! Here are your stars."),
            new Seed("letters.new.1", Speaker.Teacher, "Dit is een nieuwe letter!", "This is a new letter!"),
            new Seed("letters.find.1", Speaker.Teacher, "Waar is de…", "Where is the…"),
            new Seed("letters.starts_with.1", Speaker.Teacher, "Wat begint met…", "What starts with…"),
            new Seed("letters.begins_with.1", Speaker.Teacher, "…begint met…", "…starts with…"),
            new Seed("letters.trace.1", Speaker.Teacher, "Teken de letter met je vinger. Volg de stippen!", "Draw the letter with your finger. Follow the dots!"),
            new Seed("letters.trace_done.1", Speaker.Teacher, "Wat mooi getekend!", "Beautifully drawn!"),
            new Seed("letters.name.1", Speaker.Teacher, "Weet je wat hier komt te staan? Jouw naam! Zoek de letters.", "Do you know what goes here? Your name! Find the letters."),
            new Seed("letters.name_done.1", Speaker.Teacher, "Dat is jouw naam! Knap gedaan!", "That's your name! Well done!"),
        };

        /// <summary>
        /// One voice line per letter sound and one per word of the letters lesson, made from the LetterWordTable.
        /// The sound spellings (the "THE SOUND TABLE" in LetterWordTable.cs) are tuned by ear after generating.
        /// </summary>
        static IEnumerable<Seed> LetterSeeds()
        {
            foreach (var sound in LetterWordTable.Sounds)
                yield return new Seed("letter.sound." + sound.Letter + ".1", Speaker.Teacher, sound.Dutch, sound.English);

            foreach (LetterWordTable.Entry entry in LetterWordTable.All)
            {
                string dutch = char.ToUpperInvariant(entry.Word[0]) + entry.Word.Substring(1) + ".";
                yield return new Seed("letter.word." + entry.Word + ".1", Speaker.Teacher, dutch, entry.English + ".");
            }
        }

        [MenuItem("CoolSchool/Voice/Create or Update Lines")]
        static void CreateOrUpdateMenu()
        {
            LineTable table = CreateOrUpdate();
            Selection.activeObject = table;
            Debug.Log($"CoolSchool: voice line table updated, {table.lines.Count} lines ({TablePath}).");
        }

        /// <summary>Creates the table if needed and brings the seeded lines up to date. Returns the table.</summary>
        public static LineTable CreateOrUpdate()
        {
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Voice");

            var table = AssetDatabase.LoadAssetAtPath<LineTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LineTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            foreach (Seed seed in Seeds)
                ApplySeed(table, seed);

            foreach (Seed seed in LetterSeeds())
                ApplySeed(table, seed);

            foreach (Seed seed in NumberSeeds()) ApplySeed(table, seed);
            foreach (Seed seed in ZooSeeds()) ApplySeed(table, seed);

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        /// <summary>Loads the table, creating it first when it does not exist yet.</summary>
        public static LineTable LoadOrCreate()
        {
            var table = AssetDatabase.LoadAssetAtPath<LineTable>(TablePath);
            return table != null ? table : CreateOrUpdate();
        }

        static void ApplySeed(LineTable table, Seed seed)
        {
            VoiceLine line = table.Find(seed.Key);
            if (line == null)
            {
                line = new VoiceLine { key = seed.Key };
                table.lines.Add(line);
            }

            // Clips are left alone on purpose: they were recorded and linked by hand.
            line.group = seed.Group;
            line.speaker = seed.Speaker;
            line.dutch = seed.Dutch;
            line.english = seed.English;
        }
    }
}
