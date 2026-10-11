# Ice cream shop (create & role-play): implementation contract

The first create & role-play section. The child runs an ice cream stall on the playground. Customers come one by one, each with a picture bubble showing the order. He makes the cone himself by scooping balls out of coloured tubs and putting them on the cone. Then he hands it over and counts the coins into the register.

Learning goals:
- **Counting:** the scoops, then the coins.
- Colours are not a learning goal: they are only for matching the picture (roze, bruin, geel, groen), so the narrator never names a colour when a scoop is picked.

Two agents work in parallel:
- **Agent S (shop):** the stall, the gameplay, the order bubble, the coins, voice, sound and builder wiring.
- **Agent C (customers):** `ShopCustomer` and the five customer characters.

Read these first:
- `Docs/HANDOVER.md`
- `Docs/specs/knights-slice-contract.md` (activities, builders, finding assets by name)
- `Docs/specs/classroom-numbers-contract.md` (lesson tap input, `LessonCameraRig`, `LessonUI`)
- `Docs/specs/aliens-and-sound-contract.md` (sound cues, alien characters)

**Verify:** run `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print `compile-check: OK` before you report back.

**Do not:**
- run Unity batch commands
- generate voice
- commit

The main agent does all three.

**Design rules (from the handover):**
- The player is a 4-year-old who can't read. Everything is spoken. Visuals are big pictures and never words.
- He never fails. A mistake gets a friendly bounce-back and a hint, and the right answer starts glowing after the second mistake.
- Generous touch targets. Every tap and drop has juicy feedback: a squash, a sparkle and a sound cue.

---

## World placement (agent S)

- **Location.** The stall stands on the playground, in the **north-west quadrant**, inside the fence. Its **front (the customer side) faces SOUTH**, toward the follow camera, so the sign and the counter are visible from the playground. Suggested centre: about (-9, 0, 16). Measure the real layout and pick a clear spot. It must not overlap:
  - the equipment ring (radius about 12 m)
  - the benches (radius about 15 m)
  - the fence
  - the school path through the fence gap at x = 0
  - the alien field entrance on the west
- **Reserve the footprint** in `PlaygroundPrototypeBuilder`, so trees, benches and bins skip it. Add a small reserved-areas list that the scatter and placement code checks, and keep the change minimal.
- **Build before the NavMesh bake.** The stall is built in the environment step (like `SchoolBuilder` and `AlienFieldBuilder`), so the NavMesh carves around it.
- **Stall props** (Synty, found by name with a pack hint, placed by renderer bounds):
  - `SM_Prop_ShopCounter_01` or `SM_Prop_Icecream_Freezer_01` as the counter body
  - `SM_Prop_LargeSign_Icecream_01` up high, facing south and readable from the playground
  - `SM_Prop_Umbrella_01_Open` (PolygonKids) or an awning
  - `SM_Prop_Cash_Register_01` (PolygonKids) on the counter's right end, as seen from the shop camera
  - optionally `SM_Prop_Cart_01` or `SM_Prop_Ice_Cream_0x` as decoration
- **Tubs.** Four flavour tubs stand in a row on the counter, each a cylinder with a scoop-mound on top. Each tub has a collider for taps.

  | Flavour | Colour (NL / EN) | Value |
  |---|---|---|
  | pink | roze / pink | strawberry pink, #FF8FB8 |
  | brown | bruin / brown | chocolate, #7A4A2A |
  | yellow | geel / yellow | vanilla, #FFE27A |
  | green | groen / green | pistachio / mint, #8FD98A |

  Save the URP/Lit materials under `Assets/CoolSchool/Materials/Shop/`.
- **Cone holder.** It stands at the front centre of the counter, between the tubs and the customer. Generate the waffle cone mesh as an asset under `Assets/CoolSchool/Meshes/IceCreamCone.asset` (the same pattern as `FireWarningFan`), with a golden-brown material.
- **Scoops.** Each scoop is a slightly squashed sphere of about 0.16 m, in its flavour material.
- **Points**, under a `Shop` root:
  - `ShopkeeperSpot`: behind the counter on the north side, at the left end, facing south. The player stands here.
  - `ShopCamera`: the over-the-shoulder viewpoint.
    - It is placed **north of and above** the counter (about 2.4–2.8 m up, about 30–40° down) and looks **south** over the counter.
    - The tubs are close to the camera, so they are big tap targets.
    - The customer stands at the order spot facing the camera, and the bubble above the customer's head is fully on screen.
    - The player stands at the left end and must not cover the tubs.
  - `OrderSpot`: in front of the counter, on the south side, facing north.
  - `CustomerEntry`: about 8 m to the side (east), out of the shop camera's view.
  - `CustomerExit`: about 8 m to the other side (west), out of view.
  - `CoinSpot`: the counter top on the customer's side, beside the cone holder.
- **Zone.** Create an `ActivityZone` "ShopZone" on the playground side, in front of the stall. Use `KnightsArenaBuilder.CreateActivityZone` with a ring, a beacon and a billboard icon. For the icon, use the ModularGameUIKit coin or ice-cream-like sprite through `ActivityBeaconBuilder` (add an entry if needed). Add the zone to `ActivityInviter` with the invite group `invite.shop`.

## Runtime: `Assets/CoolSchool/Scripts/Shop/` (namespace `CoolSchool.Shop`)

### Agent C owns `ShopCustomer.cs`. Use these exact names.

```csharp
public enum CustomerKind { Kid, Knight, Alien, Dragon }

public class ShopCustomer : MonoBehaviour
{
    [SerializeField] CustomerKind kind;
    public CustomerKind Kind => kind;
    public Transform HandPoint { get; }    // where a held cone sits (hand bone; dragon: just in front of its mouth)
    public Transform BubbleAnchor { get; } // a point just above the head, for the order bubble
    public bool IsWalking { get; }         // true until WalkTo/Leave arrives

    public void Appear(Vector3 position, Vector3 lookAt);   // activate, idle, face lookAt
    public void WalkTo(Vector3 position, Vector3 lookAt);   // straight-line walk (no NavMeshAgent), about 1.6 m/s, run/walk anim, then face lookAt and idle
    public void Talk();                                     // a short talk/wave gesture while its line plays
    public void Cheer();                                    // happy jump/clap (served, or paid)
    public void HoldCone(Transform cone);                   // parent the cone to HandPoint (keep its world scale), eat/lick loop if one exists
    public void PutCoin();                                  // a small reach-forward gesture (cosmetic)
    public void Leave(Vector3 exit);                        // walk to exit, then Hide()
    public void Hide();                                     // deactivate; destroy a held cone
}
```

- **Animation:**
  - Kids, knights and aliens use the Kid animator (`KidAnimator`: Speed, Cheer, Laugh, Dance…).
  - The dragon uses `Dragon.controller`. Use its walk and idle, its happy/roar for Cheer, and anything else that fits.
  - Missing parameters must be ignored safely, as `KidAnimator` already does.
- **No dependencies.** No NavMeshAgent and no physics pushing. Customers must not have colliders that block the player or the taps. Use a trigger or no collider at all.
- **Hide on start.** `Hide()` is called on Awake, or the builder leaves them inactive.

### Agent C also owns `Editor/Shop/ShopCustomerBuilder.cs` (namespace `CoolSchool.EditorTools`)

```csharp
internal static class ShopCustomerBuilder
{
    /// Builds the five customers under parent, inactive, in visit order:
    /// [0] Kid, [1] Knight, [2] Kid (a different model from [0]), [3] Alien, [4] Dragon.
    internal static ShopCustomer[] BuildCustomers(Transform parent);
}
```

- **Reuse existing builders.** Use the existing builders' ways of creating characters: the Synty kid models and kid controller from `PlaygroundPrototypeBuilder`, the knight costume or cape and helmet look from the knights setup, the alien look from `AlienFieldBuilder`, and the dragon from `KnightsArenaBuilder`. Make them **separate new instances**, not the playground's roaming kids.
  - Refactor a small `internal static` helper out of an existing builder where needed, without changing what it builds.
- **The dragon** is a small, friendly version: about 1.6–2 m tall at the shoulder. Strip the vendor scripts, as is already done.
- **Look.** Customers must look clearly different from each other. Avoid the exact model the player uses.

### Agent S owns everything else in `Scripts/Shop/`

Suggested files; adjust the names if you like, but keep them in this namespace:
- `IceCreamShop : CoolSchool.Activities.Activity`
- `FlavourTub`
- `ScoopBall`
- `IceCreamCone`
- `OrderBubble` (screen-space UI)
- `ShopCoin`
- `CashRegister`

#### The flow (`IceCreamShop`)
1. **Begin.**
   - imagination sparkle (`ImaginationEffect`), `Sfx.PlayMusic("music.shop")`
   - The player is put at `ShopkeeperSpot` with a sparkle, facing south, with movement and jump off.
   - Hide the gameplay HUD (`LessonUI.SetGameplayHudVisible(false)`).
   - Glide the camera with a `LessonCameraRig` to `ShopCamera`.
   - Say `shop.start`. The first time ever (a progress flag), also say `shop.howto`.
2. **Five customers in builder order.** For each one:
   - **Arrival.** `Appear(CustomerEntry)`, then `WalkTo(OrderSpot)`, with the `shop.bell` cue on arrival. The dragon is preceded by `shop.dragon_arrives`.
   - **Order.** The customer says `shop.hello.<kind>` and plays `Talk()`. The order bubble pops up over `BubbleAnchor`. Then the narrator reads the order: one `shop.order.<n>.<colour>` line per colour group.
   - **Making the cone.** An empty cone pops into the holder (`shop.cone_pop`). The child adds scoops (see Interaction).
     - Each correct scoop plops on top (`shop.plop`) and squashes, the matching bubble slot fills, and `number.<k>` is said, where k is the total number of scoops on the cone.
     - A wrong scoop bounces off and flies back to its tub (`shop.bounce`), and the narrator says `shop.wrong_colour`. After the 2nd mistake in this order, the right tub glows and pulses until it is used.
   - **Done.**
     1. `shop.done` ("Klaar! Tel maar mee.") plays.
     2. Each scoop bounces in turn while `number.1…n` is said.
     3. The player's kid says `shop.serve`.
     4. The cone flies into `HoldCone`, then the customer does `Cheer()` and says `shop.thanks.<kind>`, with the `shop.slurp` cue.
     5. The bubble pops away.
   - **Paying.** The customer does `PutCoin()` and n gold coins appear on `CoinSpot` one by one (`shop.coin_drop`), where n is the number of scoops. The narrator says `shop.pay` (only for the first customer, or after a long pause).
     - Each tapped coin flies into the register (`shop.coin_tap`) and `number.<k>` is said.
     - After the last coin: `shop.register` (ka-ching), the register gives a little hop, and the narrator says `shop.paid`.
   - **Leaving.** The customer does `Leave(CustomerExit)`. Unless this is the last customer, the narrator says `shop.next`.
   - **The dragon's extra.** After eating, it sneezes a harmless burst of confetti fire (`BattleEffects`, if a fitting effect exists). Its order is always 3 scoops.
3. **End.**
   1. The teacher says `shop.end`.
   2. `RewardSystem.AddStars(5, counter position, announce: false)`, then wait for the stars to land.
   3. The camera goes back (`ExitLesson`), the HUD comes back, the player is free, `End()`.
   4. `OnEnd` must clean up everything at any moment: customers hidden, balls, cones and coins destroyed, glow off, camera restored, HUD visible, player movement on.
4. **Idle hints.** After 12 s without a correct action, re-read the order and pulse the bubble. After 24 s, the next needed tub glows. He is never stuck.

#### Interaction (touch and mouse, **new Input System only**; copy the pattern `NumbersLesson` uses for taps)
- **Drag.**
  - Pressing on a tub spawns a scoop ball at the tub and plays `shop.scoop`. The narrator does not say the colour.
  - The ball follows the finger on a plane just above the counter.
  - On release, if it is within a **generous** radius of the cone top (about 15% of the screen height, measured in screen space), it flies the last bit and is checked.
  - Otherwise it flies back into its tub. That is not a mistake.
- **Tap.** A quick press and release on a tub (under 0.3 s, little movement) makes the ball arc onto the cone by itself, so a child who doesn't drag can still play.
- **Coins.** A coin can be tapped or dragged toward the register.
- **Input locks.** Ignore input while the cone is being served or a customer is walking. Only one ball can be in flight at a time.

#### Orders and levels
- Add `public int shopLevel = 1;` and `public bool shopHowtoHeard;` to `PlayerProgress` (Scripts/Rewards).

| Level | Order |
|---|---|
| 1 | 1–2 scoops, one colour |
| 2 | 1–3 scoops, one colour |
| 3 | 2–3 scoops, up to two colours (for example 2 pink + 1 brown) |

- **Level changes, after each session:**
  - 0–1 mistakes in total: level up (maximum 3).
  - 5 or more: level down (minimum 1).
  - Save it.
- **No back-to-back repeats.** Don't give the same colour twice in a row across customers when it can be avoided.
- **Placement order.** Any placement order is accepted, as long as no colour goes over its count.

#### Order bubble (`OrderBubble`)
- **Placement.** Screen-space UI on the existing TouchUI canvas (or its own overlay canvas). It follows `Camera.main.WorldToScreenPoint(BubbleAnchor)` and is clamped on screen.
- **Contents.** A big white speech bubble (a ModularGameUIKit sprite) with a cone picture: a triangle image, generated as a texture asset if no sprite fits, with the ordered scoops stacked on it as coloured circles.
  - An unfilled slot is a pale, outlined circle of its colour.
  - A filled slot is a solid circle and pops.
- **No words.** No words or digits are needed. The number is the circles.

#### Coins and the register
- **Coins.** 3D gold coins: flat cylinders in a gold URP/Lit material. Reuse the golden look from `WeaponHolder.ApplyGlow` if handy.
- **Register.** `CashRegister` holds a reference to the register prop. It plays the hop and the ka-ching.

## Voice lines (agent S adds them to `Editor/Voice/VoiceLineSeeder.cs`)

Speakers:
- **N** = `Speaker.Narrator`
- **T** = `Speaker.Teacher`
- **K** = `Speaker.Kid`
- **A** = `Speaker.Alien`
- **G** = `Speaker.Goblin` (the dragon's goofy voice)

The number lines `number.1..10` already exist.

| Key | Spk | NL | EN |
|---|---|---|---|
| invite.shop.1 | T | Wie wil er ijsjes verkopen? Kom naar de ijswinkel! | Who wants to sell ice cream? Come to the ice cream shop! |
| invite.shop.2 | T | Mmm, ijs! Kom jij in de ijswinkel helpen? | Mmm, ice cream! Will you help in the ice cream shop? |
| shop.start.1 | N | Jij bent de ijsverkoper! Daar komt je eerste klant. | You're the ice cream seller! Here comes your first customer. |
| shop.howto.1 | N | Pak een bolletje uit de bak en zet het op het hoorntje! | Take a scoop from the tub and put it on the cone! |
| shop.hello.kid.1 | K | Hallo! Mag ik een ijsje, alsjeblieft? | Hello! Can I have an ice cream, please? |
| shop.hello.kid.2 | K | Hoi! Ik heb zin in ijs! | Hi! I'd love an ice cream! |
| shop.hello.knight.1 | K | Hallo ijsverkoper! Een ijsje voor een dappere ridder! | Hello ice cream seller! An ice cream for a brave knight! |
| shop.hello.alien.1 | A | Bliep bloep! Ijsje, alsjeblieft! | Bleep bloop! Ice cream, please! |
| shop.dragon_arrives.1 | N | Wie komt daar aan? De draak wil ook een ijsje! | Who's coming? The dragon wants an ice cream too! |
| shop.hello.dragon.1 | G | Grrroaar! Ijsje! Een groot ijsje! | Grrroar! Ice cream! A big ice cream! |
| shop.order.1.pink.1 | N | Eén roze bolletje. | One pink scoop. |
| shop.order.2.pink.1 | N | Twee roze bolletjes. | Two pink scoops. |
| shop.order.3.pink.1 | N | Drie roze bolletjes. | Three pink scoops. |
| shop.order.1.brown.1 | N | Eén bruin bolletje. | One brown scoop. |
| shop.order.2.brown.1 | N | Twee bruine bolletjes. | Two brown scoops. |
| shop.order.3.brown.1 | N | Drie bruine bolletjes. | Three brown scoops. |
| shop.order.1.yellow.1 | N | Eén geel bolletje. | One yellow scoop. |
| shop.order.2.yellow.1 | N | Twee gele bolletjes. | Two yellow scoops. |
| shop.order.3.yellow.1 | N | Drie gele bolletjes. | Three yellow scoops. |
| shop.order.1.green.1 | N | Eén groen bolletje. | One green scoop. |
| shop.order.2.green.1 | N | Twee groene bolletjes. | Two green scoops. |
| shop.order.3.green.1 | N | Drie groene bolletjes. | Three green scoops. |
| shop.wrong_colour.1 | N | Oeps, die kleur niet! Kijk maar naar het plaatje. | Oops, not that colour! Look at the picture. |
| shop.wrong_colour.2 | N | Hmm, welke kleur wil de klant? | Hmm, which colour does the customer want? |
| shop.done.1 | N | Klaar! Tel maar mee. | Done! Let's count. |
| shop.serve.1 | K | Alsjeblieft! | Here you go! |
| shop.thanks.kid.1 | K | Mmm, lekker! Dankjewel! | Mmm, yummy! Thank you! |
| shop.thanks.kid.2 | K | Jammie! Dankjewel! | Yummy! Thank you! |
| shop.thanks.knight.1 | K | Lekker! Nu ben ik nog sterker! | Yummy! Now I'm even stronger! |
| shop.thanks.alien.1 | A | Bliep! Lekker koud! Dankjewel! | Bleep! Nice and cold! Thank you! |
| shop.thanks.dragon.1 | G | Mmmm! Brrr, koud! Dankjewel! | Mmmm! Brrr, cold! Thank you! |
| shop.pay.1 | N | Nu betalen! Tik op de muntjes en tel mee. | Time to pay! Tap the coins and count along. |
| shop.paid.1 | N | Ka-ching! Goed geteld! | Ka-ching! Well counted! |
| shop.paid.2 | N | Alle muntjes zitten in de kassa! | All the coins are in the register! |
| shop.next.1 | N | Daar komt de volgende klant! | Here comes the next customer! |
| shop.hint.1 | N | Kijk naar het plaatje boven de klant. | Look at the picture above the customer. |
| shop.end.1 | T | Wat ben jij een goede ijsverkoper! Hier zijn je sterren. | What a good ice cream seller you are! Here are your stars. |

Mapping from `CustomerKind` to the line suffix: `kid`, `knight`, `alien`, `dragon`.

## Sound cues (agent S)

- **Add these** to `Editor/Audio/SoundCues.cs`, and add keyword synonyms in `SoundAutoAssign.cs`:
  - `shop.bell`
  - `shop.scoop`
  - `shop.plop`
  - `shop.bounce`
  - `shop.cone_pop`
  - `shop.slurp`
  - `shop.coin_drop`
  - `shop.coin_tap`
  - `shop.register`
  - `music.shop`
- **No clips exist yet.** A cue with no clips is silent, which is fine.

## Builder wiring (agent S)

- **Builder file.** `Editor/Shop/IceCreamShopBuilder.cs`: `internal static` entry points called from `PlaygroundPrototypeBuilder.RunBuild()`:
  - the stall and props in the environment step, before the NavMesh bake
  - the activity, zone, customers (`ShopCustomerBuilder.BuildCustomers`), bubble UI and references, after the UI and the player exist
- **Inviter.** Hook into the `ActivityInviter` zone/group arrays the same way the other zones are added.
- **Before the customers exist.** Until agent C's files exist, code against the contract above. If the compile check fails only because `ShopCustomer` or `ShopCustomerBuilder` is missing, wait and re-run; don't create stubs with the same names.

## Coordination

- **Shared files.** Both agents may touch `PlaygroundPrototypeBuilder.cs`. Agent C only touches it to extract helpers, and agent S only to add the reserved area and the shop calls. **Re-read a shared file right before each edit**, and keep edits small and local.
- **Report back:**
  - files changed and created
  - which values probably need tuning in play (camera pose, prop offsets, sizes)
  - anything you couldn't verify without running Unity
