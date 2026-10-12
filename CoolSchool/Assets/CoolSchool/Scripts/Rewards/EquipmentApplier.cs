using CoolSchool.Activities;
using CoolSchool.Aliens;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Pirates;
using CoolSchool.Race;
using CoolSchool.ToyRoom;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// Lives on the player. Puts on him what he chose: the sword and shield in his hands, the hat on his head and the
    /// plush buddy beside him. It applies everything at the start and whenever the progress changes, and it checks the
    /// current activity a few times per second for the places where a hat or a buddy does not belong:
    ///
    /// | Activity                           | Hat                                                              | Buddy                       |
    /// | KnightsGame                        | the knight's helmet (worn one, else gold, else plain), else worn | hidden                      |
    /// | Pirates, Soapbox race, Aliens      | hidden (those games dress the head)                              | hidden (the race lets it ride) |
    /// | ToyRoomWardrobe                    | worn hat                                                         | shown (the wardrobe holds it) |
    /// | anything else                      | worn hat                                                         | free play: follows; else hidden |
    ///
    /// Replaces the old EquippedSwordApplier.
    /// </summary>
    public class EquipmentApplier : MonoBehaviour
    {
        const string KnightHelmetId = "hat.knight_helmet";
        const string GoldKnightHelmetId = "gold.hat.knight_helmet";
        // The activity is checked a few times per second, which is plenty.
        const float PollSeconds = 0.25f;

        [SerializeField] RewardCatalog catalog;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] HatHolder hat;
        [SerializeField] BuddyFollower buddy;

        float nextPoll;

        void Awake()
        {
            if (weapons == null) weapons = GetComponent<WeaponHolder>();
            if (hat == null) hat = GetComponent<HatHolder>();
        }

        void OnEnable()
        {
            ProgressStore.Changed += Apply;
            nextPoll = 0f;
        }

        void Start()
        {
            Apply();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= Apply;
        }

        void Update()
        {
            if (Time.time < nextPoll) return;

            nextPoll = Time.time + PollSeconds;
            ApplyHatAndBuddy();
        }

        void Apply()
        {
            if (catalog == null) return;

            ApplyWeapons();
            ApplyHatAndBuddy();
        }

        void ApplyWeapons()
        {
            if (weapons == null) return;

            RewardItem sword = FindOrStarter(ProgressStore.EquippedId(EquipSlot.Sword), EquipSlots.StarterSwordId);
            if (sword != null) weapons.SetSwordPrefab(sword.prefab, sword.IsGold);

            RewardItem shield = FindOrStarter(ProgressStore.EquippedId(EquipSlot.Shield), EquipSlots.StarterShieldId);
            if (shield != null) weapons.SetShieldPrefab(shield.prefab, shield.IsGold);
        }

        /// <summary>The item with this id, or the starter's item when the id is empty or unknown.</summary>
        RewardItem FindOrStarter(string id, string starterId)
        {
            RewardItem item = catalog.Find(id);
            if (item == null || item.prefab == null) item = catalog.Find(starterId);
            return item != null && item.prefab != null ? item : null;
        }

        void ApplyHatAndBuddy()
        {
            if (catalog == null) return;

            ActivityManager manager = ActivityManager.Instance;
            Activity current = manager != null ? manager.Current : null;

            RewardItem worn = catalog.Find(ProgressStore.EquippedId(EquipSlot.Hat));
            RewardItem hatItem = worn;
            bool hatHidden = false;
            bool buddyHidden = true;

            if (current is KnightsGame)
            {
                hatItem = KnightsHat(worn);
            }
            else if (current is PirateAdventure || current is SoapboxRace || current is AlienLanding)
            {
                hatHidden = true;
            }
            else if (current is ToyRoomWardrobe)
            {
                buddyHidden = false;
            }
            else
            {
                // Free play (the default activity) lets the buddy follow; tag, lessons and the shop do not.
                buddyHidden = current != null && manager != null && current != manager.DefaultActivity;
            }

            if (hat != null)
            {
                hat.SetHat(hatItem != null ? hatItem.prefab : null, hatItem != null && hatItem.IsGold);
                hat.SetHidden(hatHidden || hatItem == null);
            }

            if (buddy != null)
            {
                buddy.SetBuddy(catalog.Find(ProgressStore.EquippedId(EquipSlot.Buddy)));
                buddy.SetHidden(buddyHidden);
            }
        }

        /// <summary>In the knights game he wears the knight's helmet if he has one: the one he wears, else gold, else plain.</summary>
        RewardItem KnightsHat(RewardItem worn)
        {
            if (worn != null && (worn.id == KnightHelmetId || worn.id == GoldKnightHelmetId)) return worn;

            PlayerProgress progress = ProgressStore.Current;
            RewardItem gold = catalog.Find(GoldKnightHelmetId);
            if (gold != null && gold.IsOwnedBy(progress)) return gold;

            RewardItem plain = catalog.Find(KnightHelmetId);
            if (plain != null && plain.IsOwnedBy(progress)) return plain;

            return worn;
        }
    }
}
