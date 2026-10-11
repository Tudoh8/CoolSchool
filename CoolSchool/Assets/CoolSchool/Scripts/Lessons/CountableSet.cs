using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>One kind of toy the lesson can count, with the voice line groups that talk about it.</summary>
    [Serializable]
    public class Countable
    {
        [Tooltip("Short name, e.g. duck")]
        public string id;
        public GameObject prefab;
        [Tooltip("Uniform scale that makes the toy about 0.35 m big.")]
        public float scale = 1f;
        [Tooltip("Voice group for 'Count the ducks!'")]
        public string countPromptGroup;
        [Tooltip("Voice group for 'How many ducks do you see?'")]
        public string howManyPromptGroup;
        [Tooltip("Voice group for 'Put this many ducks in the basket:'")]
        public string giveMePromptGroup;
    }

    /// <summary>The toys the numbers lesson can put on the table. Filled by the CountableSetSeeder.</summary>
    [CreateAssetMenu(menuName = "CoolSchool/Lessons/Countable Set", fileName = "Countables")]
    public class CountableSet : ScriptableObject
    {
        public List<Countable> entries = new List<Countable>();

        /// <summary>A random usable toy, preferably not the one used last. Null if the set has none.</summary>
        public Countable Pick(Countable avoid)
        {
            var usable = new List<Countable>();
            foreach (Countable entry in entries)
                if (entry != null && entry.prefab != null && entry != avoid) usable.Add(entry);

            if (usable.Count == 0 && avoid != null && avoid.prefab != null) return avoid;
            return usable.Count > 0 ? usable[UnityEngine.Random.Range(0, usable.Count)] : null;
        }
    }
}
