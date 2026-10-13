# Cart garage: implementation contract

Date: 2026-10-13. Today a soapbox cart is one of 3 bodies plus 3 on/off extras (flag, stickers, gold paint), chosen in a
fixed 6-card `CartPicker`. The user wants many more cart presents. This contract turns the cart into a small "garage":

1. **Paint colours** (one at a time): red, blue, green, pink, purple, and the existing gold.
2. **Add-ons** (on/off, any mix): the existing flag and stickers, plus googly eyes, balloons, a spinning propeller and a spoiler.
3. **Trails** behind the cart (one at a time): rainbow, stars, bubbles, glitter.
4. **Horns** (one at a time) on a new honk button in the race: a bell (starter), cow, dog, cat, elephant, magic horn.
5. **Number plate**: he picks his race number 1–10 (free, not a present; number learning).
6. **New bodies**: red wagon, cardboard rocket, cardboard boat, rocking horse, trike, mini fire truck, mini police car,
   mini convertible.
7. **The pit lane**: a parking row at the race start where every body stands (locked ones as black silhouettes), because
   the Toy Room is full.
8. **The picker with tabs**, like the wardrobe mirror: Body / Paint / Add-ons / Trail / Horn / Number.
9. **Rivals** get random looks (any body, paint and add-ons, owned or not), so he sees what can be won.

Two agents build it **one after the other** (they share files):

- **Agent G1 (garage system):** data, save, catalog seeds (all but the bodies), `CartLooks`, the tabbed picker, paints,
  add-ons, trails, horns + honk button, number plate, rival looks, icons, voice lines, invite, docs.
- **Agent G2 (bodies):** the 8 new bodies in the race builder (fit, seat, wheel base), their reveal prefabs and catalog
  seeds, the pit lane, docs.

Read first: `Docs/HANDOVER.md`, `Docs/specs/soapbox-race-contract.md`, `Docs/specs/toy-room-and-equipment-contract.md`
(the cart picker and wardrobe parts), then the files you touch.

**Verify** with `powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1` from the repo root. It must print
`compile-check: OK` before you report back. The Unity editor is usually open, so no batch mode; scene rebuild, voice
generation and preview shots happen afterwards in one batch session (G2 adds preview shots for that, see below).

**Player:** a 4-year-old who can't read. Everything is spoken; no words in the UI (icons and digits are fine). Nothing
he picks can break the race; every choice can be changed again in the next race.

## Decisions

From the user (2026-10-13): build all four directions (new bodies, paint colours, trails + add-ons, horns + number plate).
Main-agent defaults (not asked):
- Cart things that don't fit the Toy Room live in the **pit lane** at the race start; the Toy Room keeps its 6 cart slots.
- The **number plate is free** from the start; the **bell horn is a starter**, so the honk button exists from day one.
- Old saves keep working: `cart.flag`, `cart.stickers` and `cart.gold` keep their ids; a gold toggle that was on becomes
  the chosen paint.
- No sounds are synthesised; horns use clips already in the project (car horns and sirens aren't available).

## Data (G1)

- Cart items stay `RewardKind.Cart` (existing checks keep working: `reward.cart_new`, the reveal pose), with a new
  serialized field on `RewardItem`, `cartPart`: `Body`, `Paint`, `Addon`, `Trail`, `Horn`. Set it from the seed.
  Existing: racer_1/2/3 = Body, flag/stickers = Addon, gold = Paint. Per-part extra data a seed needs (paint colour,
  horn sound cue key, trail type) goes in small optional seed fields, not in id string parsing.
- `CartLooks`, `CartPicker` and `ProgressStore` are **data-driven from the catalog** (`ItemsOfKind(Cart)` filtered by
  part); no hard-coded id lists beyond the starter ids. Adding a paint, add-on, trail or horn later = a seed line (+ the
  visual for a new add-on/trail type, + a voice line).
- `PlayerProgress` (save-compatible, new fields default to empty): `equippedCart` (body, exists), `cartPaint` (id or ""
  = original colours), `cartExtrasOff` (exists, now for every add-on: owned add-ons are on unless switched off),
  `cartTrail` ("" = none), `cartHorn` ("" = the bell), `cartNumber` (0 = not chosen yet → show 1… or pick 4; G1 picks
  one default and documents it). Migration on load: if `cartPaint` is empty, `cart.gold` is unlocked and not in
  `cartExtrasOff`, set `cartPaint = "cart.gold"` once.
- Choosing something locked is impossible (the picker refuses it with the lock line), and a chosen id that's no longer
  owned falls back to the default.

## Catalog order

After the 18 new presents (ending with `hat.cowboy`), the gold variants are interleaved with the cart presents. Final
order of that tail (**G1 seeds everything except the `[G2]` bodies, in this order; G2 inserts its bodies at their
places**):

```
cart.paint_red, gold.sword.wood, [G2] cart.rocket, cart.horn_cow, gold.plush.dino, cart.eyes, cart.trail_rainbow,
gold.hat.knight_helmet, [G2] cart.firetruck, cart.paint_blue, gold.shield.round, cart.balloons, cart.horn_dog,
gold.plush.unicorn, [G2] cart.wagon, cart.trail_stars, gold.sword.pirate, cart.paint_green, cart.propeller,
gold.hat.cap, [G2] cart.horse, cart.horn_cat, gold.plush.bear, cart.trail_bubbles, cart.paint_pink,
gold.sword.plastic, [G2] cart.police, cart.spoiler, gold.hat.pirate, cart.horn_elephant, [G2] cart.boat,
cart.paint_purple, gold.sword.foam_big, cart.trail_sparkles, [G2] cart.trike, cart.horn_magic, [G2] cart.convertible
```

`cart.horn_bell` is a **starter** (listed with the other starters). Then medals, as today.

## G1: picker with tabs

- Rebuild `CartPicker` (and its builder in `SoapboxStreetBuilder.Ui.cs`) as a tabbed panel along the bottom, following
  the wardrobe's `WardrobePanel` pattern: a column of big icon tabs (body = cart icon, paint = paint drop/brush, add-ons =
  star/sticker, trail = sparkles, horn = horn/bell, number = "123" made of digits) and a card grid. No words.
- Body / Paint / Trail / Horn / Number: one choice (a gold ring on the chosen card; paint and trail have a "nothing"
  card first). Add-ons: each card toggles on/off.
- The cart in front of him changes at once; tapping a horn card plays the horn; tapping a number says the number (the
  existing `number.N` lines); tapping any item card says its `item.<id>` line (with the existing gap rules).
- Locked cards: black silhouette icon + lock, `ui.locked` sound and the picker's lock line. Keep the 10 s nag and the
  20 s auto-start; the green check starts the race.
- At most 12 cards per tab (11 bodies, 6 paints + nothing, 6 add-ons, 4 trails + nothing, 6 horns, 10 numbers).

## G1: the looks on the cart (`CartLooks`)

- **Paint:** the main body colour changes; tyres, metal and dark details stay. Preferred method: per paint, generated
  copies of the body's material(s) whose texture is **hue-shifted / recoloured toward the paint colour** for saturated
  texels only (greys and darks untouched), made in the editor and saved as assets under `Assets/CoolSchool/Race/Paint/`.
  If the bodies' atlas makes that look wrong, use the simplest method that does look right and explain why. Gold stays
  the full-material swap it is today. Paint works on every body (G2's too: the generation must take any body's materials).
- **Add-ons:** each add-on is built once per cart by the builder, inactive, positioned from the shown body's bounds
  (eyes on the front face, balloons tied at the back, propeller on top behind the seat, spoiler at the back). When the
  body changes, add-ons move to that body's anchors (store per-body anchor points, or recompute from bounds at run time).
  Models: googly eyes from spheres (white + black pupil, slight wobble); balloons can reuse the pirate balloon
  materials/approach; the propeller can use `SM_Chr_Attach_Hat_Propeller_01`'s blade or primitives, spinning faster with
  speed; spoiler from primitives.
- **Trail:** one trail object per cart, emitting only while the cart moves: rainbow = `TrailRenderer` with a rainbow
  gradient; stars = particles with the star sprite; bubbles = soft translucent spheres; glitter = small sparkles. Cheap
  (Android), unlit, and nothing collides with anything.
- **Number plate:** a small white plate with a black digit (1–10) on the front and back of every body. The digits can be
  a TextMeshPro text (digits are fine) or sprites; it must render on Android.
- **Seat per body:** `CartLooks.SetBody` also moves the seat and the buddy seat to that body's seat points (today all
  bodies share racer 1's). G1 adds the per-body arrays (seat, buddy seat, add-on anchors); for the three racers they are
  filled by the builder exactly as now. G2 fills them for the new bodies.

## G1: horns and the honk button

- Horn items carry a sound cue key; add the cues to the sound system (see how `Sfx` cues and `SoundAutoAssign` map clips):
  bell = a store/bike-bell clip from `Imported/Audio/PhantomMagic/Source Bonus/` or `ActionRPG/UI/UI_Bell_Hit_01_01`;
  cow = `polyperfect/.../SFX_Cow_Moo.ogg`; dog = `SFX_Dog_Bark_Sigle.ogg`; cat = `SFX_Cat_Meow.ogg`; elephant =
  `SFX_Elephant_Trumpeting_1.ogg`; magic = `PhantomMagic/Fantasy Transitions/Fantasy App Game Horn Blow or Tone Whoosh.wav`.
  Trim/limit to about 1.5 s if a clip is long.
- A round **honk button** on the race HUD (top centre or above the jump button; it must not cover the lanes' tap areas
  and a tap on it must not also change lanes). Cooldown about 0.6 s. Rivals honk back sometimes (with their own random
  horn), at most once every few seconds.
- The present reveal of a horn plays its sound once when the item pops in.

## G1: rivals, icons, invite, voice

- Rivals: random body, paint and 0–2 add-ons from the whole catalog (owned or not), a random number different from his.
  Until G2's bodies exist, that is just the three racers.
- Icons: `RewardIconRenderer` needs a model per item. For paints, trails and horns G1 makes small generated token
  prefabs (a paint pot or blob in the colour, a little row of stars/bubbles/rainbow arc, the animal for each horn: use
  polyperfect animals or plush toys that exist; a bell from primitives if no prop fits). They're also what the reveal
  shows.
- A new cart item doesn't invite him to the Toy Room (it isn't shown there); instead the race invite gets a variant:
  `invite.race.new` "Je hebt iets nieuws voor je kar! Kom je racen?" while an unseen cart item exists (mark cart items
  seen when the picker has been opened).
- Voice lines (Narrator unless stated), added in `VoiceLineSeeder`: one `item.<id>.1` per new cart item, e.g.
  "De rode verf!", "De blauwe verf!", "De groene verf!", "De roze verf!", "De paarse verf!", "De wiebeloogjes!",
  "De ballonnen!", "De propeller!", "De spoiler!", "Het regenboogspoor!", "De sterretjes!", "De bellenblazer!",
  "De glitters!", "Het belletje!", "De koe! Boe!", "De hond! Woef!", "De poes! Miauw!", "De olifant! Toet!",
  "De toverhoorn!" (English alongside). Picker tab prompts when a tab is opened, once per race: "Welke kar?",
  "Welke kleur?", "Wat zet je erop?", "Welk spoor?", "Welke toeter?", "Welk nummer?". `invite.race.new` (Teacher).
  Announcer (Dave) start lines `race.number.1`…`race.number.10`: "Daar is nummer vijf!" (said once at the start).
- Parent area: nothing new needed. Test menus: **Unlock All** must cover the new items (it's catalog-driven; check).

## G2: the new bodies

| id | prefab | Dutch line |
|---|---|---|
| cart.rocket | SM_Prop_Cardboard_Rocket_01 | De raket! |
| cart.firetruck | SM_Veh_Firetruck_01 | De brandweerauto! |
| cart.wagon | SM_Prop_Red_Wagon_01 | De bolderkar! |
| cart.horse | SM_Veh_Rocking_Horse_01 | Het hobbelpaard! |
| cart.police | SM_Veh_Car_Police_01 | De politieauto! |
| cart.boat | SM_Prop_Cardboard_Boat_01 | De bootkar! |
| cart.trike | SM_Veh_Trike_01 (or 02/03, the best looking) | De driewieler! |
| cart.convertible | SM_Veh_Convertable_01 | De open auto! |

- Every body is fitted like the racers by `PlaceBody` (nose north, 1.7 m long), but each gets **its own seat**: the kid
  must sit *in* open bodies (wagon, boat, rocket opening, convertible seat) and *on* ride-on ones (horse saddle, trike
  seat, the roof of the fire truck/police car like a ride-on toy). Add per-body seat overrides (a local offset and/or a
  fraction along the length) where the ray probe picks the wrong surface; log the chosen seat height per body.
- Bodies without proper wheels (rocket, boat, horse on rockers) stand on a generated **wheel base**: a low wooden plank
  with four chunky cartoon wheels (soapbox style), the body on top. Mini cars keep their own wheels.
- Height limit: with the kid seated, the top of the cart + kid must still pass under anything on the track that a racer
  passes under, and jump/ramp behaviour (`CartController`) must be unchanged. Bodies are visual only; the collider and
  physics stay the racer's.
- Reveal prefabs `CS_Cart_<Name>` (body + wheel base, no kid) like `CS_Cart_Stickers`, for the reveal and the icon.
  Seeds go into the catalog at the `[G2]` places above, plus their `item.<id>.1` voice lines.
- The **pit lane**: next to the race start (where he walks in through the fence gap), a row of parking spots with low
  painted bays, one per body (11), each showing the body as a model when owned and as a black silhouette when locked
  (reuse `ToyShelfSlot` / the silhouette material if it works outside the Toy Room). Walking past a body says its line
  once (like the Toy Room shelf). It must not block the start, the route into the race, or the camera.
- **Preview shots** for the batch session: add `race_cart_bodies` (all 11 bodies side by side with a seated kid, from
  the side and 3/4 front), `race_cart_looks` (one body with every paint), `race_pit_lane`, and `race_cart_picker_tabs`
  (the picker on each tab). Use the existing `PreviewCapture.Race.cs` patterns.

## Docs (both)

Update `Docs/HANDOVER.md` (race + rewards sections, catalog counts) and `Docs/specs/soapbox-race-contract.md` briefly.
List all new voice lines (they're generated in the batch session).
