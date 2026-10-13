# Toy Room, equipment and cart picker: implementation contract

Date: 2026-10-12. Today, 7 of the 17 presents do nothing after the reveal (knight helmet, round shield, cap, dino,
unicorn, bear), swords are swapped in automatically, there is nowhere to see what he has won, and after the last
present the jar gives empty parties. This contract fixes all of that:

1. **Equipment slots** on the kid: sword, shield, hat and buddy (a plush toy that comes along).
2. **The swap question** in the present popup: a new sword/shield/hat/buddy asks "use the new one?" (green check /
   red cross) instead of swapping by itself.
3. **The Toy Room** ("Speelgoedkamer"): a small building in the playground where every item stands on a shelf (locked
   ones as black silhouettes), with a **dress-up mirror** (the wardrobe) to change what he wears.
4. **The cart picker** at the start of the soapbox race: three cart bodies and three extras, locked ones blacked out.
5. **After the catalog:** gold versions of the items, and after those, medals that hang in the Toy Room.

Four agents build it in parallel:

- **Agent D (data, popup, voice, icons):** reward data, progress, the present popup (swap question, medal), the
  catalog seeder (starters, gold variants), item icons, voice lines, sound cues, the test menus.
- **Agent E (equipment on the kid):** shield swap, hats, the buddy follower, the parrot change, one applier for all
  slots.
- **Agent T (Toy Room):** the building, the shelves, the medal board, the wardrobe activity and its UI, the inviter.
- **Agent R (race):** the cart picker, the buddy riding in the cart.

Read first: `Docs/HANDOVER.md`, `Docs/specs/voice-and-rewards-contract.md` (the reward system this extends),
`Docs/specs/soapbox-race-contract.md` (agent R), then the files you touch.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. If it only fails on a type another agent owns, wait about a minute and
retry, for up to about 15 minutes. Never create another agent's types.

**Player:** a 4-year-old who can't read. Everything is spoken; no words in the UI (icons and digits are fine). He can
never break anything: every choice can be changed again in the wardrobe.

## Decisions

From the user (2026-10-12):
- Build the Toy Room with all suggestions: shelf, dress-up mirror, plush buddies, gold versions and medals after the
  catalog.
- Pick the cart at the start of the race; locked carts show as **blacked-out** options.
- A newly won **wearable** item asks whether to swap it for the current one, or keep the old one.

Defaults chosen by the main agent (easy to change before building, so check these with the user):
- **Buddies count as wearable** for the swap question ("Mag de beer met je mee?"). Cart parts don't ask; they are
  picked at the race.
- **One buddy at a time.** The parrot is one of the buddies (it sits on his shoulder; the others hop after him).
  Existing saves keep the parrot on his shoulder.
- **Hats** are worn in free play and most activities. They are hidden in activities that dress the head (pirates,
  race, aliens). In the knights game he automatically wears the knight helmet if he owns one.
- **Toy Room location:** a small building in the north-east corner inside the playground (a mirror of the ice cream
  stall in the north-west), door facing south.
- **Gold versions** (11 items) come after the 17 presents. Gold weapons glow softly; the lesson power-up's golden
  sword still glows much brighter and doubles hits.
- **Medals:** after the gold versions every present holds a medal for the Toy Room wall (20 are shown; beyond that,
  still a medal + party).
- **The swap question has no timeout**, like the present itself. The teacher repeats it every 8 s.
- **The cart picker auto-starts after 20 s** without a tap (keeping the selection), so the race never stalls.

## Items

### Ids (agent D seeds them; everyone uses these strings)

| Id | Kind | Model | Notes |
|---|---|---|---|
| `sword.foam` | Sword | `SM_Wep_Sword_Foam_01` | **starter** (the default sword today) |
| `shield.basic` | Shield | `SM_Wep_Shield_01` | **starter** (the default shield today) |
| `cart.racer_1` | Cart | `SM_Veh_Soapbox_Racer_01` | **starter**, `CartReveal` euler |
| the 17 presents | | | unchanged order and ids (`RewardCatalogSeeder.Seeds`) |
| 18 more presents | | | added 2026-10-13 after `cart.gold`: `hat.crown`, `plush.lion`, `sword.wand` (`SM_Chr_Attach_Magician_Wand_01`, held like a sword), `hat.astronaut`, `plush.rabbit`, `shield.silver`, `hat.wizard`, `plush.elephant`, `sword.foam_4`, `hat.viking`, `plush.duck`, `sword.staff`, `hat.propeller`, `plush.pig`, `hat.fireman`, `plush.monster`, `sword.plastic_3`, `hat.cowboy` (voice `item.<id>`, the wand and staff ask `reward.swap_wand`) |
| `gold.sword.wood` | Sword | as `sword.wood` | gold variant |
| `gold.plush.dino` | PlushToy | as `plush.dino` | |
| `gold.hat.knight_helmet` | Hat | as `hat.knight_helmet` | |
| `gold.shield.round` | Shield | as `shield.round` | |
| `gold.plush.unicorn` | PlushToy | as `plush.unicorn` | |
| `gold.sword.pirate` | Sword | as `sword.pirate` | |
| `gold.hat.cap` | Hat | as `hat.cap` | |
| `gold.plush.bear` | PlushToy | as `plush.bear` | |
| `gold.sword.plastic` | Sword | as `sword.plastic` | |
| `gold.hat.pirate` | Hat | as `hat.pirate` | not used by `PirateCostume` (that stays `hat.pirate`) |
| `gold.sword.foam_big` | Sword | as `sword.foam_big` | |

Catalog order: the 3 starters first, then the 17 presents, then the 11 gold variants in the order above.
That is 28 presents (280 stars) before the medals start. There is no gold parrot (Lorre is a scene character, not a
spawned plush) and no gold cart parts (`cart.gold` already exists).

### Slots

```csharp
namespace CoolSchool.Rewards
{
    public enum EquipSlot { None, Sword, Shield, Hat, Buddy }

    public static class EquipSlots
    {
        public const string StarterSwordId = "sword.foam";
        public const string StarterShieldId = "shield.basic";
        public const string StarterCartId = "cart.racer_1";
        public const string ParrotId = "plush.parrot";
        /// Sword→Sword, Shield→Shield, Hat→Hat, PlushToy→Buddy, anything else→None.
        public static EquipSlot SlotOf(RewardKind kind);
    }
}
```

## Agent D: data, popup, voice, icons

### `RewardItem` (add fields, keep the existing ones)

```csharp
[Tooltip("Owned from the start and never given in a present (the default sword, shield and cart).")]
public bool starter;
[Tooltip("For a gold variant: the id of the item it is a gold copy of. Same model, painted gold at run time.")]
public string goldOf = "";
[Tooltip("A picture of the item, rendered by the editor (RewardIconRenderer). Locked items show it tinted black.")]
public Sprite icon;

public bool IsGold => !string.IsNullOrEmpty(goldOf);
public EquipSlot Slot => EquipSlots.SlotOf(kind);
public bool IsOwnedBy(PlayerProgress progress) => starter || (progress != null && progress.IsUnlocked(id));
```

### `RewardCatalog`

- `NextLocked` skips starters.
- Add `public List<RewardItem> ItemsFor(EquipSlot slot)`: in catalog order (starters, presents, gold).
- Add `public List<RewardItem> ItemsOfKind(RewardKind kind)`: in catalog order.

### `GoldPaint` (new, `Scripts/Rewards/GoldPaint.cs`)

```csharp
public static class GoldPaint
{
    /// A softer glow than the lesson's golden-sword power-up, so the power-up still stands out.
    public static readonly Color SoftGlow = new Color(0.45f, 0.34f, 0.07f, 1f);
    /// Paints every renderer of this instance gold (calls WeaponHolder.ApplyGlow(instance, SoftGlow)).
    public static void Apply(GameObject instance);
}
```

Everyone who spawns an item model uses `if (item.IsGold) GoldPaint.Apply(instance);`.

### `PlayerProgress` (add fields; old saves load with these defaults)

```csharp
public string equippedShield = "";   // "" = the starter shield
public string equippedHat = "";      // "" = no hat
public string equippedBuddy = "";    // "" = automatic: the parrot if owned (today's behaviour); "none" = no buddy
public string equippedCart = "";     // "" = automatic: the best owned body (today's behaviour)
public List<string> cartExtrasOff = new List<string>();  // owned cart extras switched off in the picker
public List<string> cartsTried = new List<string>();     // cart bodies/extras used in a race at least once
public List<string> unseenItems = new List<string>();    // won, not yet looked at in the Toy Room
public bool toyRoomVisited;
public int medals;
public int medalsSeen;
```

`equippedSword` keeps its meaning ("" = the starter sword).

### `ProgressStore` (add)

```csharp
/// The id worn in this slot now, resolved: Sword ""→StarterSwordId, Shield ""→StarterShieldId, Hat as stored,
/// Buddy ""→ParrotId if unlocked else "", Buddy "none"→"". "" means nothing.
public static string EquippedId(EquipSlot slot);
/// Wears this item (or nothing with ""; for Sword/Shield "" means the starter). Buddy "" is stored as "none". Saves.
public static void Equip(EquipSlot slot, string id);

/// The chosen cart body: equippedCart if it is owned, else the best owned (racer_3 > racer_2 > racer_1).
public static string ChosenCartId();
/// An extra (cart.flag / cart.stickers / cart.gold) is on when it is unlocked and not in cartExtrasOff.
public static bool CartExtraOn(string extraId);
/// Stores the picker's choice, adds the body and the extras that are on to cartsTried, saves.
public static void SetCart(string bodyId, bool flagOn, bool stickersOn, bool goldOn);

public static void MarkSeen(string id);   // removes it from unseenItems, saves (only if it was there)
public static void AddMedal();            // medals++, saves
public static void MarkMedalsSeen();      // medalsSeen = medals, saves
```

**`OpenPresent` changes:** it unlocks the item and adds it to `unseenItems`, but **no longer sets `equippedSword`**.
The popup's answer equips it. `OpenPresent(null)` (the medal) only counts `presentsOpened`.

### `RewardSystem`

When `catalog.NextLocked` is null, call `ProgressStore.AddMedal()` and `popup.Show(null)`. Nothing else changes.

### `PresentPopup` (new steps after the reveal)

New serialized fields (the builder makes the UI):

```csharp
[SerializeField] RewardCatalog catalog;
[Tooltip("Shown after the reveal for wearable items. Inactive by default.")]
[SerializeField] RectTransform swapPanel;
[Tooltip("Picture of what he wears now (left of the 3D item). Empty-slot sprite when nothing is worn.")]
[SerializeField] Image currentIcon;
[SerializeField] Sprite emptySlotSprite;        // X-Delete-Close-Error, faded
[SerializeField] Button useNewButton;           // big green circle with Checkmark (right)
[SerializeField] Button keepOldButton;          // big red circle with X (left)
[SerializeField] Image medalImage;              // Trophies/Badge-Crown (or Trophy), shown for the medal present
[SerializeField] float swapNudgeSeconds = 8f;
```

Flow after `Open()`:
1. `reward.present_open` as now, then `item.<id>` queued (gold variants: `item.gold`).
2. **Wearable** (`item.Slot != None`) and **not already worn**:
   - Show the swap panel. `currentIcon` = the icon of `EquippedId(slot)`, or `emptySlotSprite`. It pops in with a
     small arrow from it to the 3D item.
   - Voice `reward.swap_<slot>` (`sword`, `shield`, `hat`, `buddy`), repeated every `swapNudgeSeconds` without an
     answer. **No timeout.**
   - Green check: `ProgressStore.Equip(slot, item.id)`, `Sfx.Play("wardrobe.equip")`, confetti,
     `reward.swap_yes`. Red cross: `reward.swap_no` ("it waits in your Toy Room"). Then wait for the voice (max 3 s)
     and close **without** another tap.
   - Taps on the dim do nothing while the panel is up (only the two buttons answer).
3. **Cart part** (`kind == Cart`): `reward.cart_new` ("you can pick it at the race"), then tap to close as today.
4. **Medal** (`item == null`): instead of the gift popping back, `medalImage` pops in with rays and confetti, and
   `reward.medal` plays. Tap to close.
5. If the popup is switched off mid-question (`OnDisable` / `Finish`), nothing is equipped: he keeps the old one.

### Item icons: `RewardIconRenderer` (new, `Editor/Rewards/RewardIconRenderer.cs`)

- Renders every catalog item's prefab to `Assets/CoolSchool/Rewards/Icons/<id>.png` (256×256, transparent
  background, 3/4 view, the same "stand long things up" rule as `RevealStage`, carts lying on their wheels). Gold
  variants are rendered with `GoldPaint.Apply`. Base this on `Editor/Pirates/PirateMapIconRenderer.cs`, and
  generalise its rendering into a shared helper if that is cleaner.
- Like the map icons, an existing PNG is kept (delete it to re-render). Imported as Sprite (single, alpha is
  transparency).
- `RewardCatalogSeeder.CreateOrUpdate` calls it after building the items and sets `item.icon`.
- **Locked = black silhouette:** UI code sets `Image.color = Color.black` (the alpha keeps the shape). Owned =
  white.

### `RewardCatalogSeeder`

- Seeds the starters (`starter = true`), the 17 presents (unchanged), then the gold variants (`goldOf` set, the
  prefab of the base item, the base's reveal euler).
- `RevealStage` already handles any kind; the gold reveal calls `GoldPaint.Apply` on the spawned model (agent D adds
  that one line to `RevealStage.Show`).

### `RewardsSetupBuilder`

- Builds the popup's swap panel, buttons, medal image and empty-slot sprite, and wires `catalog`.
  Sprites by name: `Checkmark`, `X-Delete-Close-Error`, `Badge-Crown` (fallback `Trophy`). The buttons are 220 px
  circles (green `#4CC35A`, red `#E8574A`), the current-item card is 200 px, and everything sits on the same canvas
  order as the popup.
- Replaces the `EquippedSwordApplier` wiring with one call: `EquipmentSetupBuilder.Setup(player, catalog)` (agent
  E's).

### Test menus (`Editor/Rewards/RewardsMenu.cs`)

- **CoolSchool/Rewards/Unlock All (test)**: unlocks every non-starter item and adds them to `unseenItems`.
- **CoolSchool/Rewards/Unlock Next Present (test)**: Play mode only; shows the popup for `NextLocked` (to test the
  swap question).
- **CoolSchool/Rewards/Give Medal (test)**.

### Voice lines (agent D seeds all of them in `VoiceLineSeeder`)

| Key | Group | Speaker | NL | EN |
|---|---|---|---|---|
| reward.swap_sword.1 | reward.swap_sword | Teacher | Wil je je nieuwe zwaard gebruiken? | Do you want to use your new sword? |
| reward.swap_shield.1 | reward.swap_shield | Teacher | Wil je je nieuwe schild gebruiken? | Do you want to use your new shield? |
| reward.swap_hat.1 | reward.swap_hat | Teacher | Wil je hem meteen opzetten? | Do you want to put it on right away? |
| reward.swap_buddy.1 | reward.swap_buddy | Teacher | Mag je nieuwe knuffel met je mee? | Can your new cuddly toy come along? |
| reward.swap_yes.1 | reward.swap_yes | Teacher | Goed gekozen! | Good choice! |
| reward.swap_yes.2 | reward.swap_yes | Teacher | Super! | Super! |
| reward.swap_no.1 | reward.swap_no | Teacher | Oké! Hij wacht op je in je speelgoedkamer. | Okay! It's waiting for you in your toy room. |
| reward.cart_new.1 | reward.cart_new | Teacher | Die kun je kiezen bij de race! | You can pick it at the race! |
| reward.medal.1 | reward.medal | Teacher | Een medaille! Die hangt in je speelgoedkamer. | A medal! It hangs in your toy room. |
| toyroom.invite.1 | toyroom.invite | Teacher | Kom kijken in je speelgoedkamer! | Come and look in your toy room! |
| toyroom.welcome.1 | toyroom.welcome | Narrator | Dit is jouw speelgoedkamer! Hier staat alles wat je hebt verdiend. | This is your toy room! Everything you've earned is here. |
| toyroom.new_item.1 | toyroom.new_item | Narrator | Kijk, iets nieuws! | Look, something new! |
| toyroom.medal.1 | toyroom.medal | Narrator | Je hebt een nieuwe medaille! | You have a new medal! |
| wardrobe.welcome.1 | wardrobe.welcome | Narrator | Wat wil je aan? | What do you want to wear? |
| wardrobe.pick.1 | wardrobe.pick | Narrator | Mooi! | Nice! |
| wardrobe.pick.2 | wardrobe.pick | Narrator | Wat stoer! | So cool! |
| wardrobe.locked.1 | wardrobe.locked | Narrator | Die moet je nog verdienen. Vul de sterrenpot! | You still have to earn that one. Fill the star jar! |
| wardrobe.done.1 | wardrobe.done | Narrator | Klaar! Je ziet er geweldig uit! | Done! You look great! |
| race.pick_cart.1 | race.pick_cart | Announcer | Kies je kar! | Pick your cart! |
| race.cart_locked.1 | race.cart_locked | Announcer | Die kar moet je nog winnen! | You still have to win that one! |
| race.cart_new.1 | race.cart_new | Announcer | Ooh, een nieuwe kar! Probeer hem uit! | Ooh, a new cart! Try it out! |
| race.cart_ready.1 | race.cart_ready | Announcer | Daar gaan we! | Here we go! |
| item.gold.1 | item.gold | Narrator | Wauw, goud! | Wow, gold! |
| item.sword.foam.1 | item.sword.foam | Narrator | Het schuimzwaard! | The foam sword! |
| item.shield.basic.1 | item.shield.basic | Narrator | Het schild! | The shield! |
| item.cart.racer_1.1 | item.cart.racer_1 | Narrator | Kar één! | Cart one! |
| item.sword.plastic.1 | item.sword.plastic | Narrator | Het plastic zwaard! | The plastic sword! |
| item.hat.knight_helmet.1 | item.hat.knight_helmet | Narrator | De ridderhelm! | The knight's helmet! |
| item.plush.dino.1 | item.plush.dino | Narrator | De dino! | The dino! |
| item.cart.flag.1 | item.cart.flag | Narrator | De vlag! | The flag! |
| item.sword.wood.1 | item.sword.wood | Narrator | Het houten zwaard! | The wooden sword! |
| item.shield.round.1 | item.shield.round | Narrator | Het ronde schild! | The round shield! |
| item.cart.racer_2.1 | item.cart.racer_2 | Narrator | Kar twee! | Cart two! |
| item.plush.parrot.1 | item.plush.parrot | Narrator | Lorre de papegaai! | Lorre the parrot! |
| item.plush.unicorn.1 | item.plush.unicorn | Narrator | De eenhoorn! | The unicorn! |
| item.hat.cap.1 | item.hat.cap | Narrator | De pet! | The cap! |
| item.cart.stickers.1 | item.cart.stickers | Narrator | De stickers! | The stickers! |
| item.sword.pirate.1 | item.sword.pirate | Narrator | Het piratenzwaard! | The pirate sword! |
| item.sword.foam_big.1 | item.sword.foam_big | Narrator | Het grote schuimzwaard! | The big foam sword! |
| item.plush.bear.1 | item.plush.bear | Narrator | De beer! | The bear! |
| item.cart.racer_3.1 | item.cart.racer_3 | Narrator | Kar drie! | Cart three! |
| item.hat.pirate.1 | item.hat.pirate | Narrator | De piratenhoed! | The pirate hat! |
| item.cart.gold.1 | item.cart.gold | Narrator | De gouden verf! | The gold paint! |

43 lines, about 1,250 Dutch characters for DeepVoice. Generate them with the usual batch after the rebuild.
**Helper:** `ItemVoice.GroupFor(RewardItem item)` (static, `Scripts/Rewards/ItemVoice.cs`, agent D) returns
`"item.gold"` for gold variants and `"item." + id` otherwise. Everyone uses it.

### Sound cues (agent D: `Editor/Audio/SoundCues.cs` + `Assets/CoolSchool/Audio/SoundPackPicks.txt`)

`ui.pick` (a card is tapped), `ui.locked` (a soft "nope" thud for a locked card), `wardrobe.equip` (a pop/whoosh
when something is put on), `toyroom.sparkle` (a soft shimmer for unseen items). Only use clips that are already
imported; when unsure, copy the clip line of a similar existing cue (`ui.popup_open`, `ui.card_wrong`,
`reward.item_reveal`).

## Agent E: equipment on the kid

All new runtime types go in namespace `CoolSchool.Rewards`, folder `Scripts/Rewards/`, unless noted.

### `WeaponHolder` (Battle)

- `public void SetSwordPrefab(GameObject prefab, bool gold = false)`: as today, plus a gold flag. The early return
  compares both the prefab and the flag. A fresh sword gets `GoldPaint.Apply` when the flag is set, and that also
  holds after `SetGlow(null)` recreates the sword.
- `public void SetShieldPrefab(GameObject prefab, bool gold = false)`: the same for the shield.
- `GoldPaint` lives in `CoolSchool.Rewards` and calls `WeaponHolder.ApplyGlow`. To avoid a circular feel, the holder
  may call `ApplyGlow(instance, GoldPaint.SoftGlow)` directly (same result).

### `HatHolder` (new, `Scripts/Characters/HatHolder.cs`, namespace `CoolSchool.Characters`)

- On the player. It puts a hat on the head bone at zero offsets (Synty attachments fit at the bone origin, like
  `PirateCostume`), plus serialized tweak `localPosition`/`localEuler`. Colliders are removed.
- `public void SetHat(GameObject prefab, bool gold)`: null = no hat. Instances are cached per (prefab, gold), so
  swapping back is instant.
- `public void SetHidden(bool hidden)`.
- Bones may not exist while switched off: it tries again on the next call (same pattern as `RaceHelmet`).

### `BuddyFollower` (new)

- On its own scene object (not a child of the player). It spawns the buddy's model (colliders removed, scaled so
  its tallest side is `buddyHeight` 0.45 m, gold paint for gold variants).
- **Follows** the player in free play. The target point is 0.9 m behind and 0.6 m to his right, and it moves at up
  to 1.3× his speed. While moving it **hops** (y offset `|sin(t·π·2.5)|·0.15` m, a small squash on landing) and faces
  where it goes. Idle, it faces the player and does a little hop or spin every 4–7 s. It finds the ground with a
  raycast down from 2 m above (ignore triggers and the Kids layer), and teleports with a sparkle
  (`ImaginationEffect`) when more than 6 m away.
- `public void SetBuddy(RewardItem item)`: null or the parrot = nothing to show (the parrot is `ShoulderParrot`'s).
- `public void SetHidden(bool hidden)`: it disappears and reappears with a sparkle.
- `public void RideOn(Transform seat)` / `public void StopRiding()`: it sits on the seat, parented, no hopping.
  Riding wins over hidden.
- `public void HoldAt(Transform spot)` / `public void ReleaseHold()`: it stands still there, for the wardrobe
  preview. Hold also wins over hidden.

### `ShoulderParrot` (Pirates, one condition)

`owned` becomes `ProgressStore.EquippedId(EquipSlot.Buddy) == EquipSlots.ParrotId`. Everything else stays.

### `EquipmentApplier` (new; **replaces `EquippedSwordApplier`**, delete that file and its .meta)

```csharp
public class EquipmentApplier : MonoBehaviour   // on the player
{
    [SerializeField] RewardCatalog catalog;
    [SerializeField] WeaponHolder weapons;
    [SerializeField] HatHolder hat;
    [SerializeField] BuddyFollower buddy;
}
```

- It applies all four slots on `Start` and on `ProgressStore.Changed`. Sword and shield go through
  `SetSwordPrefab`/`SetShieldPrefab(item.prefab, item.IsGold)`; an empty or unknown sword/shield id falls back to
  the starter's catalog item.
- **Activity rules** (it polls `ActivityManager.Instance.Current` every 0.25 s, like `ShoulderParrot`):

  | Current activity | Hat | Buddy |
  |---|---|---|
  | `KnightsGame` | the worn hat if it is a knight helmet (plain or gold), else the gold helmet if owned, else the plain helmet if owned, else the worn hat | hidden |
  | `PirateAdventure`, `SoapboxRace`, `AlienLanding` | hidden (they dress the head) | hidden (agent R may make it ride) |
  | `ToyRoomWardrobe` | worn hat | shown (agent T holds it beside him) |
  | anything else (free play, tag, lessons, shop) | worn hat | free play: follows; others: hidden |

### `EquipmentSetupBuilder` (new, `Editor/Rewards/EquipmentSetupBuilder.cs`)

`public static void Setup(GameObject player, RewardCatalog catalog)` adds `HatHolder` and `EquipmentApplier` to the
player, creates the "Buddy" object with `BuddyFollower`, and wires everything. It removes any old
`EquippedSwordApplier`. Agent D calls it from `RewardsSetupBuilder`.

## Agent T: the Toy Room

Runtime: namespace `CoolSchool.ToyRoom`, folder `Scripts/ToyRoom/`. Editor: `Editor/ToyRoom/ToyRoomBuilder*.cs`.

### The building (`ToyRoomBuilder.Build(...)`, called from `PlaygroundPrototypeBuilder` after the ice cream shop, before the NavMesh bake)

- **Where:** the north-east corner inside the playground fence. Search for a free spot like
  `IceCreamShopBuilder` does: x 9..19, z from 3 m to 10 m in from the north fence. Keep 0.8 m clear of the fence,
  stay out of the school corridor (`SchoolBuilder.CorridorHalfWidth`) and away from playground equipment and trees.
  Log the chosen spot (`ToyRoom:` line). If nothing fits, shrink the footprint one wall piece at a time.
- **Shape:** Synty Generic modular walls (the same pieces and measuring as `SchoolBuilder`; move the helpers you
  need into a shared internal class instead of copying them). About 6 m × 4.5 m inside (3 × 2 wall pieces, or what
  the pieces measure), a wide doorway in the south wall facing the camera, a flat roof and warm lights.
  `BuildingInteriorReveal` hides the roof and the south wall while he is inside. Bright, cosy colours: a rug, a toy
  chest, a beanbag, posters (find props by name and skip any that are missing).
- **Beacon:** like the activity zones, a billboard icon above the door (a toy-chest or gift icon: `Gift`), but no
  ring, because the room itself is not an activity.

### Displays

```csharp
public class ToyShelfSlot : MonoBehaviour      // one per catalog item, placed by the builder
{
    [SerializeField] string itemId;
    [Tooltip("The model's biggest side is scaled to this.")]
    [SerializeField] float displaySize = 0.5f;
    [SerializeField] Material silhouetteMaterial;   // URP/Unlit, black
}

public class ToyShelf : MonoBehaviour          // one in the room, owns all slots
{
    [SerializeField] RewardCatalog catalog;
    [SerializeField] ToyShelfSlot[] slots;
    [Tooltip("World-space box of the room's inside (x/z tested).")]
    [SerializeField] Bounds roomArea;
    [SerializeField] float talkRadius = 1.4f;
    public void Refresh(PlayerProgress progress);   // also callable in edit mode (preview shots)
}
```

- **Owned:** the real model (gold paint for gold variants), standing nicely (swords upright on a rack, hats on pegs
  or stands, plushies sitting, carts as minis about 0.6 m long). **Locked:** the same model with the black
  silhouette material on every renderer slot, so he can see there is more to win. **Unseen** (in
  `unseenItems`): a soft sparkle loop and `toyroom.sparkle`.
- **Walking past:** the nearest owned slot within `talkRadius` hops (a scale bounce). Its name plays
  (`ItemVoice.GroupFor`) once per visit, only when the voice queue is idle and at least 2.5 s after the last one.
  Then `ProgressStore.MarkSeen(id)`. Locked slots stay silent.
- **Entering the room** (the player enters `roomArea`; a visit lasts until he leaves):
  - first time ever: `toyroom.welcome`, then `toyRoomVisited = true`;
  - otherwise, if anything is unseen: `toyroom.new_item`;
  - if `medals > medalsSeen`: `toyroom.medal`, the new medals pop in, then `MarkMedalsSeen()`.
  - It refreshes on `ProgressStore.Changed`.
- **Layout** (the builder places slots on these walls; adjust to what fits):
  - **West wall:** a weapon rack. Swords bottom row (starter + 8, in a zigzag so none hides the next), gold swords top row (4). Shields beside it
    (starter, round, silver, gold round).
  - **North wall, left:** plush shelf, two levels of seven at display size 0.38 (lower: dino, unicorn, bear, parrot, baby dragon, lion, rabbit;
    upper: gold dino, unicorn, bear, elephant, duck, pig, monster).
    The **medal board** sits above it.
  - **North wall, right:** the **dress-up mirror** (a tall frame with a light-blue glossy quad; a Synty mirror prop
    if one exists), with the wardrobe zone in front of it.
  - **East wall:** hat pegs (3 hats, 3 gold hats) on the podium, and above it (1.2 to 2.3 m) the **hat board**: two rows of pegs
    (4 + 3) with the 7 hats of the second round of presents (2026-10-13). The podium is a low cart podium: 3 cart bodies plus the flag,
    stickers and gold-paint-can minis.

```csharp
public class MedalBoard : MonoBehaviour
{
    [SerializeField] GameObject medalPrefab;   // a Synty medal/trophy prop by name; fallback: a gold disc + ribbon built in code
    [SerializeField] Transform[] spots;        // 20, a 5 × 4 grid
    public void Refresh(int medals, int medalsSeen);   // the new ones (medalsSeen..medals) pop in with a sparkle when shown
}
```

### The wardrobe (`ToyRoomWardrobe : Activity`)

```csharp
public class ToyRoomWardrobe : Activity
{
    [SerializeField] PlayerController player;
    [SerializeField] KidAnimator playerAnimator;
    [SerializeField] WeaponHolder playerWeapons;
    [SerializeField] BuddyFollower buddy;
    [SerializeField] FollowCamera followCamera;
    [SerializeField] Transform standSpot;      // in front of the mirror, facing the camera spot
    [SerializeField] Transform buddySpot;      // beside standSpot
    [SerializeField] Transform cameraSpot;     // frames him full-body in the left ~45% of the screen
    [SerializeField] Transform exitSpot;       // just outside the zone, so he does not walk straight back in
    [SerializeField] WardrobePanel panel;
    [SerializeField] RewardCatalog catalog;
    [SerializeField] float idleEndSeconds = 45f;
}
```

The zone is in front of the mirror (radius 0.9). Flow:

1. **Begin.** Input off and the joystick hidden. He is placed at `standSpot` (CharacterController off → position →
   on) facing the camera. `playerWeapons.ShowWeapons(true)` and `buddy.HoldAt(buddySpot)`. `followCamera` is
   disabled, and the main camera glides to `cameraSpot` over 0.8 s. Then `panel.Show()` and
   `wardrobe.welcome`.
2. **Panel** (`WardrobePanel`, on the TouchUI canvas, right ~55% of the screen):
   - **Tabs:** four big tabs down the left edge of the panel, with icons by name: `Sword`, `Shield`, `Helmet`, and a
     heart or paw for the buddy (`Chat-Heart`, falling back to the dino's icon). The selected tab is larger and
     highlighted. It opens on the sword tab.
   - **Cards:** a grid of item cards for the tab's slot (`catalog.ItemsFor(slot)`, 6 per row, 124 px cards since 2026-10-13: the fullest tab has 15 cards, three rows fit, room for 18). The
     hat and buddy tabs start with a "nothing" card (`X-Delete-Close-Error`, faded). An owned card shows the white
     icon; a locked card shows the black icon. The worn item has a gold ring and a small `Checkmark` badge. Unseen
     items twinkle.
   - **Done:** a green check button in the bottom-right corner.
3. **Tap an owned card:** `ProgressStore.Equip(slot, id)` (the applier updates the kid at once) and
   `ProgressStore.MarkSeen(id)`. He cheers (`PlayCheer`), there is a sparkle on him, `Sfx.Play("wardrobe.equip")`,
   the item's name plays (`ItemVoice.GroupFor`), and then `wardrobe.pick` half the time.
4. **Tap a locked card:** the card wobbles, `Sfx.Play("ui.locked")`, and `wardrobe.locked` plays (at most once
   per 6 s).
5. **Done** (or `idleEndSeconds` without a tap): `wardrobe.done`, he cheers, then `End()`.
6. **OnEnd** (also via the back button): hide the panel and the weapons, `buddy.ReleaseHold()`, put him at
   `exitSpot`, turn `followCamera` back on and `Snap()`, show the joystick and turn input on.

### `ActivityInviter` (shared file: re-read it right before editing)

- Add `[SerializeField] Transform toyRoomSpot;` (the doorway).
- When `ProgressStore.Current.unseenItems.Count > 0` or `medals > medalsSeen`, and the toy room has not been
  invited since the last present, the next invitation goes to the toy room: `toyroom.invite` and the sparkle trail
  to `toyRoomSpot`.
  - Generalise the trail target from `ActivityZone` to a position.
  - Remember "invited since the last present" in memory: compare `presentsOpened` to the value stored when it last
    invited.
- The normal zone rotation is unchanged otherwise. The wardrobe zone is **not** in the rotation.

### Preview shots (`PreviewCapture.BuildGroups`)

Add `toyroom_outside` (follow-camera view of the building), `toyroom_inside_empty` (`ToyShelf.Refresh` with a fresh
`PlayerProgress`: all silhouettes) and `toyroom_inside_full` (everything unlocked, 12 medals). Also add
`wardrobe_panel` if a UI capture is feasible; skip it otherwise.

## Agent R: the cart picker

### `CartLooks`

- `PlayerBodyIndex(progress)` uses `ProgressStore.ChosenCartId()` (racer_1 → 0, racer_2 → 1, racer_3 → 2).
- `ApplyPlayerLooks` uses `ProgressStore.CartExtraOn(...)` for the flag, stickers and gold.
- Add `public static readonly string[] BodyIds = { "cart.racer_1", Racer2Id, Racer3Id };` (racer_1 is
  `EquipSlots.StarterCartId`).

### `CartPicker` (new, `Scripts/Race/CartPicker.cs`, on the TouchUI canvas, built by the race builder)

```csharp
public class CartPicker : MonoBehaviour
{
    [SerializeField] GameObject root;              // child; hidden outside the picker
    [SerializeField] Button[] bodyCards;           // 3, big (200 px), icons from the catalog (cart.racer_1/2/3)
    [SerializeField] Button[] extraToggles;        // 3, smaller (140 px): cart.flag, cart.stickers, cart.gold
    [SerializeField] Button goButton;              // big green, Checkmark, bottom right
    [SerializeField] RewardCatalog catalog;
    public bool IsOpen { get; }
    /// Opens with the current choice selected; calls onChanged(body, flag, stickers, gold) on every change
    /// (for the live preview) and onDone when he taps go (or after the auto-start).
    public void Open(System.Action<int, bool, bool, bool> onChanged, System.Action onDone);
    public void Close();
}
```

- Cards along the bottom of the screen: the three bodies on the left, the three extras to the right.
  - **Owned:** a white icon. **Locked:** the black icon with a small `Lock` badge.
  - The **selected** body has a gold ring and is slightly bigger; an extra that is **on** has a gold ring, and one
    that is off is dimmed (white at 50%).
- **Tap an owned body:** select it, a scale punch, `Sfx.Play("ui.pick")`, and `onChanged`.
- **Tap an owned extra:** toggle it.
- **Tap a locked card:** the card wobbles, `Sfx.Play("ui.locked")`, and `race.cart_locked` plays (at most once
  per 6 s).
- **Go:** it closes and calls `onDone`. With no tap for 10 s, `race.pick_cart` is repeated. After 20 s without a tap
  it does the same as Go.

### `SoapboxRace` flow change (in `RunBegin`, after `race.welcome`, **before** he climbs in)

1. Add a serialized `[SerializeField] CartPicker cartPicker;` and `[SerializeField] Transform cartPickCameraSpot;`
   (front-left of the player's cart, looking at it; the builder places it).
2. Disable `followCamera`. The main camera glides to `cartPickCameraSpot` over 0.8 s, while he stands beside his
   cart.
3. Work out the selection: `ProgressStore.ChosenCartId()` plus the extras. If an **owned body is not in
   `cartsTried`**, select that body instead and say `race.cart_new`; otherwise say `race.pick_cart`.
4. `cartPicker.Open(...)`. `onChanged` calls `playerLooks.SetBody` + `SetExtras` (live on the real cart, with a
   little bounce of the cart). On `onDone`: `ProgressStore.SetCart(...)`, then `race.cart_ready`.
5. Continue as today (`player.RideOn`, the chase camera `BlendIn` from the current pose).
   - `PrepareCarts` must not overwrite the chosen look: apply the player's looks from progress (which now holds the
     choice), and give the rivals the two other bodies as today.
6. **Buddy in the cart:** add `[SerializeField] BuddyFollower buddy;`. Give `CartController` a
   `[SerializeField] Transform buddySeat` (beside his legs or behind the seat, placed by the builder). When the worn
   buddy is a plush (not the parrot, not none), call `buddy.RideOn(playerCart.BuddySeat)` on boarding. `OnEnd`
   calls `buddy.StopRiding()`.
7. **OnEnd** also closes the picker. The back button must work during the picker.

### `SoapboxStreetBuilder`

It builds the picker UI (sprites by name: `Checkmark`, `Lock`), places `cartPickCameraSpot` and the cart's
`buddySeat`, and wires `cartPicker`, `cartPickCameraSpot`, `buddy` (found by type; agent E's builder runs in
`RewardsSetupBuilder`, so check the build order and find it at wiring time, or wire it from `RewardsSetupBuilder`
if the race is built first) and `catalog`. Add a preview shot `race_cart_picker` (camera at `cartPickCameraSpot`,
the player's cart with racer_2 + flag).

## File ownership

- **Agent D:**
  - Runtime: `RewardItem`, `RewardCatalog`, `EquipSlot.cs` (new: `EquipSlot` + `EquipSlots`), `GoldPaint.cs`,
    `ItemVoice.cs`, `PlayerProgress`, `ProgressStore`, `RewardSystem`, `PresentPopup`, `RevealStage` (one line).
  - Editor: `RewardCatalogSeeder`, `RewardIconRenderer` (new), `RewardsSetupBuilder`, `RewardsMenu`.
  - Shared content: `VoiceLineSeeder` (all lines above), `SoundCues` + `SoundPackPicks.txt`.
- **Agent E:** `WeaponHolder`, `HatHolder`, `BuddyFollower`, `EquipmentApplier`, `EquipmentSetupBuilder`,
  deleting `EquippedSwordApplier`, the one-line `ShoulderParrot` change.
- **Agent T:** everything in `Scripts/ToyRoom/` and `Editor/ToyRoom/`, the shared-helper move out of `SchoolBuilder`
  (keep `SchoolBuilder`'s behaviour identical), `ActivityInviter`, one call in `PlaygroundPrototypeBuilder`, the
  Toy Room shots in `PreviewCapture`.
- **Agent R:** `CartPicker`, `SoapboxRace*.cs`, `CartLooks`, `CartController.buddySeat`, `SoapboxStreetBuilder*`,
  the race shot in `PreviewCapture`.
- **Shared files** (`PlaygroundPrototypeBuilder`, `PreviewCapture`, `ActivityInviter`, `VoiceLineSeeder`,
  `SoundCues`): **re-read each one right before every edit**, keep edits small and local, and never reformat.
- Types other agents need before they exist (`EquipSlot`, `ProgressStore.Equip`, `BuddyFollower`, ...): wait for the
  owner, as described under **Verify**. Agent D should land `EquipSlot.cs`, the `RewardItem` fields and the
  `ProgressStore` API first, then do the rest.

## Done when

- Compile-check OK. The scene is rebuilt, and the preview shots `toyroom_*` and `race_cart_picker` look right.
- With **Reset Progress** then **Give 15 Stars (test)**: the plastic sword asks the swap question. Red cross keeps
  the foam sword, and the plastic sword stands on the Toy Room rack, sparkling until he walks past it.
- **Unlock All (test)**: the Toy Room is full. The wardrobe can put on every sword, shield, hat and buddy, and they
  show on the kid: the hat in free play, the knight helmet in the knights game, no hat in the race, the pirate cove
  or the alien field, and the buddy hopping after him.
- The race starts with the picker: locked carts are black, the choice shows live and is remembered next race, and
  the buddy rides along.
- After everything is won, a present gives a medal, and it hangs on the Toy Room wall.
- `Docs/HANDOVER.md` gets a short Toy Room / equipment paragraph (main agent, after review).
