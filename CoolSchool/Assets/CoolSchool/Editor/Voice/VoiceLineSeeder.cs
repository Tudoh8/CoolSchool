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
    public static class VoiceLineSeeder
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

            new Seed("reward.star.1", Speaker.Narrator, "Een ster!", "A star!"),
            new Seed("reward.jar_full.1", Speaker.Teacher, "De pot is vol! Je krijgt een cadeautje!", "The jar is full! You get a present!"),
            new Seed("reward.present_tap.1", Speaker.Teacher, "Tik op het cadeau!", "Tap the present!"),
            new Seed("reward.present_open.1", Speaker.Teacher, "Wauw! Kijk eens wat erin zit!", "Wow! Look what's inside!"),

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
            new Seed("invite.lesson.2", Speaker.Teacher, "Kom, we gaan tellen in de klas! Volg de sterretjes!", "Come on, let's count in class! Follow the stars!"),

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
        };

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
