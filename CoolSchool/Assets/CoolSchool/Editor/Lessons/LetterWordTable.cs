namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The words of the letters lesson: for each letter the Dutch words that start with it, a 3D prop found by name in the
    /// vendor packs, and the English word. One table feeds both the LetterSetSeeder (props) and the VoiceLineSeeder
    /// (one voice line per word), so a word never exists in one without the other.
    /// Letters with no fitting prop are left out on purpose: i, j, q, u and y have no words yet (the lesson then skips
    /// the word part for them). Pack is the folder name preferred when several packs share a prefab name.
    /// </summary>
    static class LetterWordTable
    {
        internal readonly struct Entry
        {
            public readonly char Letter;
            public readonly string Word;
            public readonly string English;
            public readonly string Prefab;
            public readonly string Pack;

            public Entry(char letter, string word, string english, string prefab, string pack)
            {
                Letter = letter;
                Word = word;
                English = english;
                Prefab = prefab;
                Pack = pack;
            }
        }

        const string Kids = "PolygonKids";
        const string Town = "PolygonTown";
        const string City = "PolygonCity";
        const string Generic = "PolygonGeneric";

        internal static readonly Entry[] All =
        {
            new Entry('a', "anker", "Anchor", "SM_Gen_Prop_Chain_Anchor_01", Generic),
            new Entry('a', "ambulance", "Ambulance", "SM_Veh_Car_Ambo_01", City),

            new Entry('b', "bal", "Ball", "SM_Prop_Ball_01", Kids),
            new Entry('b', "beer", "Bear", "SM_Prop_Plush_Bear_01", Kids),
            new Entry('b', "boom", "Tree", "SM_Generic_Tree_01", Kids),
            new Entry('b', "bank", "Bench", "SM_Prop_ParkBench_01", Town),
            new Entry('b', "boot", "Boat", "SM_Prop_Toy_Boat_01", Kids),
            new Entry('b', "brood", "Bread", "SM_Gen_Prop_Food_Bread_01", Generic),
            new Entry('b', "bus", "Bus", "SM_Veh_SchoolBus_01", Town),
            new Entry('b', "bloem", "Flower", "SM_Prop_Flowers_01", Town),
            new Entry('b', "boek", "Book", "SM_Prop_Book_01", Town),
            new Entry('b', "bed", "Bed", "SM_Prop_Bed_Single_01", Town),

            // c says "kuh" (as in "cadeau"), so these never stand next to a k word.
            new Entry('c', "camera", "Camera", "SM_Prop_Camera_01", Kids),
            new Entry('c', "cake", "Cake", "SM_Prop_Cake_02", Kids),

            new Entry('d', "deur", "Door", "SM_Prop_Door_01", City),
            new Entry('d', "doos", "Box", "SM_Prop_Cardboard_Box_01", Kids),
            new Entry('d', "draak", "Dragon", "SM_Prop_Coin_Ride_Dragon", Kids),
            new Entry('d', "dino", "Dinosaur", "SM_Prop_Plush_Dino_01", Kids),

            new Entry('e', "eend", "Duck", "SM_Prop_Toy_Duck_01", Kids),
            new Entry('e', "emmer", "Bucket", "SM_Prop_Bucket_01", Kids),

            new Entry('f', "fiets", "Bike", "SM_Veh_Bike_01", Kids),
            new Entry('f', "fles", "Bottle", "SM_Gen_Prop_Bottle_01", Generic),
            new Entry('f', "fluit", "Flute", "SM_Prop_Flute_01", Kids),

            new Entry('g', "glas", "Glass", "SM_Item_Drink_Glass_01", Town),
            new Entry('g', "gieter", "Watering can", "SM_Prop_Watering_Can_01", Kids),
            new Entry('g', "globe", "Globe", "SM_Prop_Globe_01", Town),

            new Entry('h', "huis", "House", "SM_Prop_Play_House_01", Kids),
            new Entry('h', "hoed", "Hat", "SM_Chr_Attach_Hat_Cowboy_01", Kids),
            new Entry('h', "helm", "Helmet", "SM_Chr_Attach_Knight_Helmet_01", Kids),
            new Entry('h', "hamer", "Hammer", "SM_Prop_Toy_Hammer_01", Kids),

            new Entry('k', "kaas", "Cheese", "SM_Prop_Cheese_01", Town),
            new Entry('k', "kist", "Chest", "SM_Gen_Prop_Chest_01", Generic),
            new Entry('k', "kroon", "Crown", "SM_Chr_Attach_Crown_01", Kids),
            new Entry('k', "kaars", "Candle", "SM_Prop_Candles_01", Town),
            new Entry('k', "kasteel", "Castle", "SM_Prop_Sand_Castle_01", Kids),

            new Entry('l', "lamp", "Lamp", "SM_Prop_Lamp_01", Town),
            new Entry('l', "lepel", "Spoon", "SM_Item_Cutlery_Spoon_01", Town),
            new Entry('l', "ladder", "Ladder", "SM_Prop_Ladder_01", Town),
            new Entry('l', "leeuw", "Lion", "SM_Prop_Plush_Lion_01", Kids),

            new Entry('m', "muts", "Woolly hat", "SM_Chr_Attach_Hat_Beanie_Small_01", Kids),
            new Entry('m', "mok", "Mug", "SM_Item_Mug_01", Town),
            new Entry('m', "munt", "Coin", "SM_Gen_Prop_Coin_01", Generic),
            new Entry('m', "muis", "Mouse", "SM_Prop_Computer_Mouse_01", Town),

            new Entry('n', "net", "Net", "SM_Prop_Bug_Net_01", Kids),

            new Entry('o', "oven", "Oven", "SM_Prop_Oven_01", Kids),
            new Entry('o', "olifant", "Elephant", "SM_Prop_Plush_Elephant_01", Kids),

            new Entry('p', "pet", "Cap", "SM_Chr_Attach_Hat_Cap_Forward_01", Kids),
            new Entry('p', "paraplu", "Umbrella", "SM_Prop_Umbrella_01_Open", Kids),
            new Entry('p', "pan", "Pan", "SM_Prop_Pan_01", Town),
            new Entry('p', "pompoen", "Pumpkin", "SM_Item_Pumpkin_01", Town),
            new Entry('p', "paard", "Horse", "SM_Veh_Rocking_Horse_01", Kids),

            new Entry('r', "raket", "Rocket", "SM_Prop_Toy_Rocket_01", Kids),
            new Entry('r', "radio", "Radio", "SM_Prop_Two_Way_Radio_01", Kids),

            new Entry('s', "slee", "Sled", "SM_Prop_Sled_01", Kids),
            new Entry('s', "step", "Scooter", "SM_Veh_Scooter_01", Kids),
            new Entry('s', "sleutel", "Key", "SM_Gen_Prop_Key_01", Generic),
            new Entry('s', "stoel", "Chair", "SM_Prop_Chair_01", Kids),

            new Entry('t', "taart", "Cake", "SM_Prop_Cake_01", Kids),
            new Entry('t', "tafel", "Table", "SM_Prop_Table_01", Kids),
            new Entry('t', "trein", "Train", "SM_Prop_Toy_Train_01", Kids),
            new Entry('t', "tent", "Tent", "SM_Prop_Tent_01", Kids),
            new Entry('t', "tas", "Bag", "SM_Prop_Handbag_01", Kids),
            new Entry('t', "trommel", "Drum", "SM_Prop_Drum_01", Kids),
            new Entry('t', "tomaat", "Tomato", "SM_Item_Tomato_01", Town),

            new Entry('v', "vlag", "Flag", "SM_Prop_Flag_01", Kids),
            new Entry('v', "vork", "Fork", "SM_Item_Cutlery_Fork_01", Town),
            new Entry('v', "vliegtuig", "Plane", "SM_Prop_Toy_Plane_01", Kids),
            new Entry('v', "vlieger", "Kite", "SM_Prop_Kite_01", Kids),
            new Entry('v', "vuur", "Fire", "SM_Prop_Camp_Fire_01", Kids),

            new Entry('w', "wolk", "Cloud", "SM_Generic_Cloud_01", Kids),
            new Entry('w', "worst", "Sausage", "SM_Item_Sausage_Cooked_01", Town),
            new Entry('w', "wagen", "Wagon", "SM_Prop_Red_Wagon_01", Kids),

            // x says "ks" in the sound table; xylofoon is the one word that fits.
            new Entry('x', "xylofoon", "Xylophone", "SM_Prop_Xylophone_01", Kids),

            new Entry('z', "zwaard", "Sword", "SM_Wep_Sword_Foam_01", Kids),
            new Entry('z', "zeep", "Soap", "SM_Item_Soap_01", Town),
            new Entry('z', "zak", "Sack", "SM_Gen_Prop_Sack_01", Generic),
        };

        /// <summary>
        /// THE SOUND TABLE. How each letter is spoken (Dutch kindergarten style) and in English. These go to text-to-speech
        /// and will be tuned by ear after generating: change a spelling here, delete the clip and generate again.
        /// </summary>
        internal static readonly (char Letter, string Dutch, string English)[] Sounds =
        {
            ('a', "Ah.", "Ah."), ('b', "Buh.", "Buh."), ('c', "Kuh.", "Kuh."), ('d', "Duh.", "Duh."), ('e', "Eh.", "Eh."),
            ('f', "Fff.", "Fff."), ('g', "Ggg.", "Guh."), ('h', "Hhh.", "Hhh."), ('i', "Ih.", "Ih."), ('j', "Juh.", "Juh."),
            ('k', "Kuh.", "Kuh."), ('l', "Lll.", "Lll."), ('m', "Mmm.", "Mmm."), ('n', "Nnn.", "Nnn."), ('o', "Oh.", "Oh."),
            ('p', "Puh.", "Puh."), ('q', "Kuh.", "Kwuh."), ('r', "Rrr.", "Rrr."), ('s', "Sss.", "Sss."), ('t', "Tuh.", "Tuh."),
            ('u', "Uh.", "Uh."), ('v', "Vvv.", "Vvv."), ('w', "Www.", "Www."), ('x', "Ks.", "Ks."), ('y', "Ie.", "Yuh."),
            ('z', "Zzz.", "Zzz."),
        };
    }
}
