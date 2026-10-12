using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// Loads and saves the child's progress as JSON in the app's data folder.
    /// Every change saves straight away, so nothing is lost if the tablet is switched off.
    /// </summary>
    public static class ProgressStore
    {
        const string FileName = "progress.json";

        static PlayerProgress current;

        /// <summary>Raised after every save, so the jar and the sword can update themselves.</summary>
        public static event Action Changed;

        public static PlayerProgress Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // Static data survives Play mode when domain reload is switched off, so start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            Changed = null;
        }

        public static void Load()
        {
            current = null;
            try
            {
                if (File.Exists(FilePath))
                    current = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(FilePath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"CoolSchool: could not read {FilePath}, starting fresh. {exception.Message}");
            }

            if (current == null) current = new PlayerProgress();
            if (current.unlockedItems == null) current.unlockedItems = new List<string>();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"CoolSchool: could not save {FilePath}. {exception.Message}");
            }

            Changed?.Invoke();
        }

        /// <summary>One star has landed in the jar.</summary>
        public static void AddStar()
        {
            Current.starsInJar++;
            Current.totalStars++;
            Save();
        }

        /// <summary>The full jar turns into a present, so a jar's worth of stars goes away.</summary>
        public static void EmptyJar(int capacity)
        {
            Current.starsInJar = Mathf.Max(0, Current.starsInJar - capacity);
            Save();
        }

        /// <summary>A present was opened. The item (if any) is unlocked, and a sword is also picked up to use.</summary>
        public static void OpenPresent(RewardItem item)
        {
            PlayerProgress progress = Current;
            progress.presentsOpened++;

            if (item != null && !string.IsNullOrEmpty(item.id))
            {
                if (!progress.IsUnlocked(item.id)) progress.unlockedItems.Add(item.id);
                if (item.kind == RewardKind.Sword) progress.equippedSword = item.id;
            }
            Save();
        }

        const int MaxChildNameLength = 12;

        /// <summary>The ice cream shop level (1 to 3). Setting it saves.</summary>
        public static int ShopLevel
        {
            get => Mathf.Clamp(Current.shopLevel, 1, 3);
            set
            {
                Current.shopLevel = Mathf.Clamp(value, 1, 3);
                Save();
            }
        }

        /// <summary>Lowercase letters a to z only, at most 12 of them. Anything else (spaces, digits, accents) is dropped.</summary>
        public static string NormalizeChildName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var letters = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                char lower = char.ToLowerInvariant(c);
                if (lower >= 'a' && lower <= 'z') letters.Append(lower);
                if (letters.Length >= MaxChildNameLength) break;
            }
            return letters.ToString();
        }

        /// <summary>Stores the child's name (normalised), saves, and raises Changed.</summary>
        public static void SetChildName(string name)
        {
            Current.childName = NormalizeChildName(name);
            Save();
        }

        /// <summary>Forgets everything. For the editor menu and, later, the parent area.</summary>
        public static void ResetAll()
        {
            current = new PlayerProgress();
            Save();
        }
    }
}
