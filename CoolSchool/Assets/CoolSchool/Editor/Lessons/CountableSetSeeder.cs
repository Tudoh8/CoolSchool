using System.Collections.Generic;
using System.Linq;
using CoolSchool.Lessons;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/CoolSchool/Lessons/Countables.asset: the toys the numbers lesson counts.
    /// Models are found by name and scaled so the biggest side is about 0.35 m.
    /// </summary>
    public static class CountableSetSeeder
    {
        const string LessonsFolder = "Assets/CoolSchool/Lessons";
        public const string AssetPath = LessonsFolder + "/Countables.asset";
        const float TargetSize = 0.35f;

        /// <summary>One toy to seed. The prompt groups are named lesson.count.id, lesson.howmany.id and lesson.giveme.id.</summary>
        readonly struct Seed
        {
            public readonly string Id;
            public readonly string PrefabName;

            public Seed(string id, string prefabName)
            {
                Id = id;
                PrefabName = prefabName;
            }
        }

        static readonly Seed[] Seeds =
        {
            new Seed("duck", "SM_Prop_Toy_Duck_01"),
            new Seed("ball", "SM_Prop_Ball_01"),
            new Seed("bear", "SM_Prop_Plush_Bear_01"),
            new Seed("block", "SM_Prop_Toy_Block_01"),
            new Seed("dino", "SM_Prop_Plush_Dino_01"),
        };

        [MenuItem("CoolSchool/Lessons/Seed Countables")]
        static void SeedFromMenu()
        {
            CountableSet set = CreateOrUpdate();
            Selection.activeObject = set;
        }

        /// <summary>Makes or refreshes the set and returns it.</summary>
        public static CountableSet CreateOrUpdate()
        {
            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Lessons");

            var set = AssetDatabase.LoadAssetAtPath<CountableSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CountableSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            set.entries = BuildEntries(set.entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        static List<Countable> BuildEntries(List<Countable> existing)
        {
            var result = new List<Countable>();
            foreach (Seed seed in Seeds)
            {
                GameObject prefab = PlaygroundPrototypeBuilder.LoadPrefab(PlaygroundPrototypeBuilder.Kids(seed.PrefabName));
                if (prefab == null) continue;

                Countable entry = existing != null ? existing.Find(e => e != null && e.id == seed.Id) : null;
                if (entry == null) entry = new Countable { id = seed.Id };

                entry.prefab = prefab;
                entry.scale = ScaleForSize(prefab);
                entry.countPromptGroup = "lesson.count." + seed.Id;
                entry.howManyPromptGroup = "lesson.howmany." + seed.Id;
                entry.giveMePromptGroup = "lesson.giveme." + seed.Id;
                result.Add(entry);
            }

            // Anything added by hand stays, after the seeded toys.
            if (existing != null)
                result.AddRange(existing.Where(e => e != null && !Seeds.Any(s => s.Id == e.id)));
            return result;
        }

        /// <summary>The uniform scale that makes the prefab's biggest side about 0.35 m.</summary>
        static float ScaleForSize(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float scale = 1f;
            if (PlaygroundPrototypeBuilder.TryGetBounds(instance, out Bounds bounds))
            {
                float biggest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (biggest > 0.0001f) scale = TargetSize / biggest;
            }
            else
            {
                Debug.LogWarning($"CoolSchool: {prefab.name} has no mesh to measure; keeping its own size for counting.");
            }

            Object.DestroyImmediate(instance);
            return scale;
        }
    }
}
