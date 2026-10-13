namespace CoolSchool.Zoo
{
    /// <summary>How an animal moves and how a hider of this kind is posed: big ones plod, tall ones need cover, low ones sink easily.</summary>
    public enum ZooSize { Small, Medium, Big, Tall, Low }

    /// <summary>
    /// The ten animals of the zoo park (Docs/specs/hide-and-seek-contract.md): plain data, no scene objects.
    /// <see cref="Order"/> is the fixed order everything else follows (the pens east to west, the checklist icons, the order
    /// in which new kinds appear over the visits).
    /// </summary>
    public sealed class ZooAnimalKind
    {
        /// <summary>The id used in voice keys (zoo.found.elephant), cues (zoo.call.elephant), progress and the pens.</summary>
        public string Id;
        /// <summary>The first sound of the Dutch name, for the letter.sound.x clue: de olifant starts with o.</summary>
        public char FirstSound;
        /// <summary>The polyperfect Low Poly Animated Animals prefab the builder uses.</summary>
        public string PrefabName;
        /// <summary>How tall the animal stands in the world, in metres.</summary>
        public float Height;
        public ZooSize Size;
        /// <summary>The HidingSpot ids this animal may hide in.</summary>
        public string[] AllowedSpots;

        public static readonly ZooAnimalKind[] Order =
        {
            new ZooAnimalKind { Id = "elephant", FirstSound = 'o', PrefabName = "Elephant_Female", Height = 2.0f, Size = ZooSize.Big, AllowedSpots = new[] { "hedge", "tree", "bush", "rock" } },
            new ZooAnimalKind { Id = "giraffe", FirstSound = 'g', PrefabName = "Giraffe", Height = 3.4f, Size = ZooSize.Tall, AllowedSpots = new[] { "hedge", "tree", "bush", "house" } },
            new ZooAnimalKind { Id = "monkey", FirstSound = 'a', PrefabName = "Chimpanzee", Height = 0.9f, Size = ZooSize.Small, AllowedSpots = new[] { "treetop", "house", "box", "bin", "tent" } },
            new ZooAnimalKind { Id = "penguin", FirstSound = 'p', PrefabName = "Penguin", Height = 0.6f, Size = ZooSize.Small, AllowedSpots = new[] { "bin", "box", "tent", "table", "leaves" } },
            new ZooAnimalKind { Id = "hippo", FirstSound = 'n', PrefabName = "Hippo", Height = 1.2f, Size = ZooSize.Big, AllowedSpots = new[] { "pond", "hedge", "leaves" } },
            new ZooAnimalKind { Id = "lion", FirstSound = 'l', PrefabName = "Lion_Male", Height = 1.1f, Size = ZooSize.Medium, AllowedSpots = new[] { "leaves", "bush", "tent", "rock", "table" } },
            new ZooAnimalKind { Id = "zebra", FirstSound = 'z', PrefabName = "Zebra", Height = 1.5f, Size = ZooSize.Medium, AllowedSpots = new[] { "hedge", "bush", "tree", "tent" } },
            new ZooAnimalKind { Id = "crocodile", FirstSound = 'k', PrefabName = "Crocodile", Height = 0.45f, Size = ZooSize.Low, AllowedSpots = new[] { "pond", "leaves", "hedge", "table" } },
            new ZooAnimalKind { Id = "flamingo", FirstSound = 'f', PrefabName = "Flamingo", Height = 1.3f, Size = ZooSize.Medium, AllowedSpots = new[] { "pond", "bush", "hedge", "rock" } },
            new ZooAnimalKind { Id = "tiger", FirstSound = 't', PrefabName = "Tiger", Height = 1.0f, Size = ZooSize.Medium, AllowedSpots = new[] { "bush", "leaves", "tent", "house", "rock" } },
        };

        /// <summary>The kind with this id, or null.</summary>
        public static ZooAnimalKind Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (ZooAnimalKind kind in Order)
                if (kind.Id == id) return kind;
            return null;
        }
    }
}
