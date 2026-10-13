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
            if (current.unseenCartItems == null) current.unseenCartItems = new List<string>();
            MigrateCartPaint(current);
        }

        /// <summary>
        /// Old saves had gold paint as an on/off extra. Once: if no paint is chosen, gold is owned and was not switched off, gold becomes
        /// the chosen paint. The flag stops it from coming back after he picks "no paint".
        /// </summary>
        static void MigrateCartPaint(PlayerProgress progress)
        {
            if (progress.cartPaintMigrated) return;

            progress.cartPaintMigrated = true;
            if (string.IsNullOrEmpty(progress.cartPaint) && progress.IsUnlocked(GoldPaintId) && !progress.cartExtrasOff.Contains(GoldPaintId))
                progress.cartPaint = GoldPaintId;
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
                if (item.kind == RewardKind.Cart) MarkCartItemUnseen(item.id);
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

        // ---- The cart garage ----

        /// <summary>The horn that is owned from the start: the honk button always has a sound.</summary>
        public const string StarterHornId = "cart.horn_bell";
        /// <summary>The old gold paint, which used to be an on/off extra and is now one of the paints.</summary>
        public const string GoldPaintId = "cart.gold";

        /// <summary>Starters are always owned; everything else once it has been won.</summary>
        static bool OwnsCartItem(PlayerProgress progress, string id)
        {
            return id == EquipSlots.StarterCartId || id == StarterHornId || progress.IsUnlocked(id);
        }

        /// <summary>The race number on the plates (1 to 10). He is four, so 4 shows until he has picked one.</summary>
        public static int CartNumber()
        {
            int number = Current.cartNumber;
            return number >= CartChoice.MinNumber && number <= CartChoice.MaxNumber ? number : CartChoice.DefaultNumber;
        }

        /// <summary>The horn that is chosen, resolved: a horn that is not owned (or none chosen) gives the starter bell.</summary>
        public static string ChosenHornId()
        {
            PlayerProgress progress = Current;
            string chosen = progress.cartHorn;
            return !string.IsNullOrEmpty(chosen) && OwnsCartItem(progress, chosen) ? chosen : StarterHornId;
        }

        /// <summary>
        /// What his cart looks like now, resolved: the chosen body, and the paint, trail and horn only if they are owned (otherwise
        /// the defaults), the add-ons he switched off, and his number. This is what the picker starts from and what the race shows.
        /// </summary>
        public static CartChoice LoadCartChoice()
        {
            PlayerProgress progress = Current;
            var choice = new CartChoice
            {
                bodyId = ChosenCartId(),
                paintId = !string.IsNullOrEmpty(progress.cartPaint) && OwnsCartItem(progress, progress.cartPaint) ? progress.cartPaint : "",
                trailId = !string.IsNullOrEmpty(progress.cartTrail) && OwnsCartItem(progress, progress.cartTrail) ? progress.cartTrail : "",
                hornId = ChosenHornId(),
                number = CartNumber(),
            };
            foreach (string id in progress.cartExtrasOff) choice.addonsOff.Add(id);
            return choice;
        }

        /// <summary>
        /// Stores the cart picker's choice and saves. Something that is not owned is never stored (the default is kept instead).
        /// The body, paint, trail and horn are noted as tried.
        /// </summary>
        public static void SaveCartChoice(CartChoice choice)
        {
            if (choice == null) return;

            PlayerProgress progress = Current;
            progress.equippedCart = OwnsCartItem(progress, choice.bodyId) ? choice.bodyId : "";
            progress.cartPaint = !string.IsNullOrEmpty(choice.paintId) && OwnsCartItem(progress, choice.paintId) ? choice.paintId : "";
            progress.cartTrail = !string.IsNullOrEmpty(choice.trailId) && OwnsCartItem(progress, choice.trailId) ? choice.trailId : "";
            string horn = !string.IsNullOrEmpty(choice.hornId) && OwnsCartItem(progress, choice.hornId) ? choice.hornId : StarterHornId;
            progress.cartHorn = horn == StarterHornId ? "" : horn;
            progress.cartNumber = Mathf.Clamp(choice.number, CartChoice.MinNumber, CartChoice.MaxNumber);
            progress.cartPaintMigrated = true;

            progress.cartExtrasOff = new List<string>(choice.addonsOff);

            AddOnce(progress.cartsTried, progress.equippedCart);
            AddOnce(progress.cartsTried, progress.cartPaint);
            AddOnce(progress.cartsTried, progress.cartTrail);
            AddOnce(progress.cartsTried, horn);
            Save();
        }

        static void MarkCartItemUnseen(string id)
        {
            if (Current.unseenCartItems == null) Current.unseenCartItems = new List<string>();
            AddOnce(Current.unseenCartItems, id);
        }

        /// <summary>He has opened the cart picker, so every won cart item has been seen there. Saves only if something was new.</summary>
        public static void MarkCartItemsSeen()
        {
            List<string> unseen = Current.unseenCartItems;
            if (unseen == null || unseen.Count == 0) return;

            unseen.Clear();
            Save();
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
