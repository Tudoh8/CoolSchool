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
            if (current.cartExtrasOff == null) current.cartExtrasOff = new List<string>();
            if (current.cartsTried == null) current.cartsTried = new List<string>();
            if (current.unseenItems == null) current.unseenItems = new List<string>();
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

        /// <summary>
        /// A present was opened. The item (if any) is unlocked and marked as new for the Toy Room.
        /// It is NOT worn automatically: the popup asks, and its answer calls Equip. A null item (the medal) only counts.
        /// </summary>
        public static void OpenPresent(RewardItem item)
        {
            PlayerProgress progress = Current;
            progress.presentsOpened++;

            if (item != null && !string.IsNullOrEmpty(item.id))
            {
                if (!progress.IsUnlocked(item.id)) progress.unlockedItems.Add(item.id);
                if (!progress.unseenItems.Contains(item.id)) progress.unseenItems.Add(item.id);
            }
            Save();
        }

        // ---- Equipment ----

        const string NoBuddy = "none";
        const string Racer2Id = "cart.racer_2";
        const string Racer3Id = "cart.racer_3";

        /// <summary>
        /// The id worn in this slot now, resolved: sword and shield fall back to the starters, the buddy is
        /// the parrot when nothing was chosen and it is owned. "" means nothing.
        /// </summary>
        public static string EquippedId(EquipSlot slot)
        {
            PlayerProgress progress = Current;
            switch (slot)
            {
                case EquipSlot.Sword:
                    return string.IsNullOrEmpty(progress.equippedSword) ? EquipSlots.StarterSwordId : progress.equippedSword;
                case EquipSlot.Shield:
                    return string.IsNullOrEmpty(progress.equippedShield) ? EquipSlots.StarterShieldId : progress.equippedShield;
                case EquipSlot.Hat:
                    return progress.equippedHat ?? "";
                case EquipSlot.Buddy:
                    if (progress.equippedBuddy == NoBuddy) return "";
                    if (string.IsNullOrEmpty(progress.equippedBuddy))
                        return progress.IsUnlocked(EquipSlots.ParrotId) ? EquipSlots.ParrotId : "";
                    return progress.equippedBuddy;
                default:
                    return "";
            }
        }

        /// <summary>
        /// Wears this item in the slot, or nothing with "" (for the sword and shield that means the starter).
        /// An empty buddy is stored as "none", so it does not fall back to the parrot. Saves.
        /// </summary>
        public static void Equip(EquipSlot slot, string id)
        {
            PlayerProgress progress = Current;
            id = id ?? "";
            switch (slot)
            {
                case EquipSlot.Sword:
                    progress.equippedSword = id == EquipSlots.StarterSwordId ? "" : id;
                    break;
                case EquipSlot.Shield:
                    progress.equippedShield = id == EquipSlots.StarterShieldId ? "" : id;
                    break;
                case EquipSlot.Hat:
                    progress.equippedHat = id;
                    break;
                case EquipSlot.Buddy:
                    progress.equippedBuddy = id == "" ? NoBuddy : id;
                    break;
                default:
                    return;
            }
            Save();
        }

        /// <summary>The chosen cart body: equippedCart if it is owned, else the best owned one (racer 3, then 2, then 1).</summary>
        public static string ChosenCartId()
        {
            PlayerProgress progress = Current;
            string chosen = progress.equippedCart;
            if (!string.IsNullOrEmpty(chosen) && OwnsCartBody(progress, chosen)) return chosen;

            if (progress.IsUnlocked(Racer3Id)) return Racer3Id;
            if (progress.IsUnlocked(Racer2Id)) return Racer2Id;
            return EquipSlots.StarterCartId;
        }

        static bool OwnsCartBody(PlayerProgress progress, string id)
        {
            return id == EquipSlots.StarterCartId || progress.IsUnlocked(id);
        }

        /// <summary>A cart extra (flag, stickers, gold paint) is on when it is unlocked and not switched off in the picker.</summary>
        public static bool CartExtraOn(string extraId)
        {
            PlayerProgress progress = Current;
            return progress.IsUnlocked(extraId) && !progress.cartExtrasOff.Contains(extraId);
        }

        /// <summary>Stores the cart picker's choice, notes that the body and the extras that are on were tried, and saves.</summary>
        public static void SetCart(string bodyId, bool flagOn, bool stickersOn, bool goldOn)
        {
            PlayerProgress progress = Current;
            progress.equippedCart = bodyId ?? "";

            SetExtra(progress, "cart.flag", flagOn);
            SetExtra(progress, "cart.stickers", stickersOn);
            SetExtra(progress, "cart.gold", goldOn);

            AddOnce(progress.cartsTried, bodyId);
            Save();
        }

        static void SetExtra(PlayerProgress progress, string extraId, bool on)
        {
            // Only owned extras can be switched on or off; a locked one is left alone.
            if (!progress.IsUnlocked(extraId)) return;

            if (on)
            {
                progress.cartExtrasOff.Remove(extraId);
                AddOnce(progress.cartsTried, extraId);
            }
            else if (!progress.cartExtrasOff.Contains(extraId))
            {
                progress.cartExtrasOff.Add(extraId);
            }
        }

        static void AddOnce(List<string> list, string id)
        {
            if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
        }

        /// <summary>He has looked at this item in the Toy Room. Saves only if it was marked as new.</summary>
        public static void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Current.unseenItems.Remove(id)) Save();
        }

        /// <summary>A present after the whole catalog gave a medal for the Toy Room wall.</summary>
        public static void AddMedal()
        {
            Current.medals++;
            Save();
        }

        /// <summary>The Toy Room has shown all the medals won so far.</summary>
        public static void MarkMedalsSeen()
        {
            Current.medalsSeen = Current.medals;
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
