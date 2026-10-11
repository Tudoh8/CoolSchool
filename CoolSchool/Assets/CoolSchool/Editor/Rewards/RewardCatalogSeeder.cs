using System.Collections.Generic;
using System.Linq;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/CoolSchool/Rewards/Catalog.asset with the presents in the order they are given.
    /// Models are found by name. Items already in the catalog keep their reveal tweaks; their model and kind are refreshed.
    /// </summary>
    public static class RewardCatalogSeeder
    {
        const string RewardsFolder = "Assets/CoolSchool/Rewards";
        const string CatalogPath = RewardsFolder + "/Catalog.asset";

        /// <summary>One wanted item. PrefabName is the exact model name; null means "find a pirate hat".</summary>
        readonly struct Seed
        {
            public readonly string Id;
            public readonly RewardKind Kind;
            public readonly string PrefabName;

            public Seed(string id, RewardKind kind, string prefabName)
            {
                Id = id;
                Kind = kind;
                PrefabName = prefabName;
            }
        }

        static readonly Seed[] Seeds =
        {
            new Seed("sword.plastic", RewardKind.Sword, "SM_Wep_Sword_Plastic_01"),
            new Seed("hat.knight_helmet", RewardKind.Hat, "SM_Chr_Attach_Cardboard_KnightHelmet_01"),
            new Seed("plush.dino", RewardKind.PlushToy, "SM_Prop_Plush_Dino_01"),
            new Seed("sword.wood", RewardKind.Sword, "SM_Wep_Sword_Wood_02"),
            new Seed("shield.round", RewardKind.Shield, "SM_Wep_Shield_03"),
            new Seed("plush.unicorn", RewardKind.PlushToy, "SM_Prop_Plush_Unicorn_01"),
            new Seed("hat.cap", RewardKind.Hat, "SM_Chr_Attach_Hat_Cap_Back_01"),
            new Seed("sword.foam_big", RewardKind.Sword, "SM_Wep_Sword_Foam_03"),
            new Seed("plush.bear", RewardKind.PlushToy, "SM_Prop_Plush_Bear_01"),
            new Seed("hat.pirate", RewardKind.Hat, null),
        };

        [MenuItem("CoolSchool/Rewards/Seed Catalog")]
        static void SeedFromMenu()
        {
            RewardCatalog catalog = CreateOrUpdate();
            Selection.activeObject = catalog;
        }

        /// <summary>Makes or refreshes the catalog asset and returns it.</summary>
        public static RewardCatalog CreateOrUpdate()
        {
            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Rewards");

            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RewardCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.items = BuildItems(catalog.items);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static List<RewardItem> BuildItems(List<RewardItem> existing)
        {
            var result = new List<RewardItem>();
            foreach (Seed seed in Seeds)
            {
                GameObject prefab = seed.PrefabName != null ? FindPrefab(seed.PrefabName) : FindPirateHat();
                if (prefab == null)
                {
                    Debug.LogWarning($"CoolSchool: no model found for reward '{seed.Id}', skipping it.");
                    continue;
                }

                RewardItem item = existing != null ? existing.Find(e => e != null && e.id == seed.Id) : null;
                if (item == null) item = new RewardItem { id = seed.Id, revealScale = 1f };

                item.kind = seed.Kind;
                item.prefab = prefab;
                result.Add(item);
            }

            // Anything added by hand stays, after the seeded items.
            if (existing != null)
                result.AddRange(existing.Where(e => e != null && !Seeds.Any(s => s.Id == e.id)));
            return result;
        }

        static GameObject FindPrefab(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != prefabName) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }
            return null;
        }

        /// <summary>Any prefab whose name contains both "Pirate" and "Hat".</summary>
        static GameObject FindPirateHat()
        {
            string bestPath = null;
            foreach (string guid in AssetDatabase.FindAssets("Pirate Hat t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!fileName.Contains("Pirate") || !fileName.Contains("Hat")) continue;

                if (bestPath == null || string.CompareOrdinal(fileName, System.IO.Path.GetFileNameWithoutExtension(bestPath)) < 0)
                    bestPath = path;
            }
            return bestPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(bestPath) : null;
        }
    }
}
