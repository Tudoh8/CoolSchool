using CoolSchool.Audio;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/CoolSchool/Audio/SoundLibrary.asset with every cue in SoundCues.
    /// Clips that were already assigned are never cleared; missing cues are added with an empty clip list.
    /// </summary>
    public static class SoundLibrarySeeder
    {
        const string RootFolder = "Assets/CoolSchool";
        public const string LibraryPath = RootFolder + "/Audio/SoundLibrary.asset";

        [MenuItem("CoolSchool/Audio/Create Or Update Sound Library")]
        static void CreateOrUpdateMenu()
        {
            SoundLibrary library = CreateOrUpdate();
            Selection.activeObject = library;
            Debug.Log($"CoolSchool: sound library updated, {library.Entries.Count} cues ({LibraryPath}).");
        }

        /// <summary>Creates the library if needed and adds any missing cues. Returns it.</summary>
        public static SoundLibrary CreateOrUpdate()
        {
            PlaygroundPrototypeBuilder.EnsureFolder(RootFolder, "Audio");

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            foreach (string cue in SoundCues.All)
            {
                if (library.Find(cue) != null) continue;

                library.Entries.Add(new SoundEntry
                {
                    cue = cue,
                    // Music and ambience play flat; so do the UI sounds. Everything else is heard from where it happens.
                    spatial = !SoundCues.IsMusic(cue) && !SoundCues.IsAmbience(cue) && !cue.StartsWith("ui.") && !cue.StartsWith("reward."),
                    cooldown = SoundCues.IsMusic(cue) || SoundCues.IsAmbience(cue) ? 0f : 0.05f,
                });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>Loads the library, creating it first when it does not exist yet.</summary>
        public static SoundLibrary LoadOrCreate()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            return library != null ? library : CreateOrUpdate();
        }
    }
}
