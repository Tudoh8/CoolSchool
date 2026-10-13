using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CoolSchool.Characters;
using CoolSchool.Race;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The cart garage shots of PreviewCapture (contract Docs/specs/cart-garage-contract.md):
    /// race_cart_bodies (every body with the kid seated on it, from the side and from the 3/4 front), race_cart_lineup (all the bodies at
    /// once, kids on them), race_cart_looks (the paints on several bodies), race_cart_addons_chase (every add-on, the plate and a trail
    /// from the chase camera, to check that nothing blocks the view), race_pit_lane (the bays with everything won, and with a fresh
    /// save: black silhouettes) and race_cart_picker_tabs (the picker on each tab, everything won; race_cart_picker_locked: a fresh save).
    /// The picker shots set the progress the picker reads to a temporary one (it is never saved: the picker only saves when something
    /// new was marked as seen, and the temporary progress has nothing new) and the Toy Room's way of showing the touch canvas through the camera.
    /// </summary>
    public static partial class PreviewCapture
    {
        /// <summary>Where the lineup stands: on the road just past the start banner.</summary>
        const float LineupZ = 35f;
        const float PitLaneX = -10.9f;

        static void AddCartGarageGroups(List<Group> groups)
        {
            // Every body with the kid seated on it, from the side (right) and from the 3/4 front: seat, legs, wheels and the wheel base show.
            var bodies = new Group { Name = "race_cart_bodies", Stage = StageRaceSeated };
            foreach (SoapboxStreetBuilder.BodyDef def in SoapboxStreetBuilder.BodyDefs)
            {
                string id = def.Id;
                string label = id.Replace("cart.", "");
                bodies.Shots.Add(new Shot { Name = label + "_side", Frame = () => { ShowPlayerBody(RaceChoice(id)); CartSeatView("PlayerCart", 90f, 3.6f); } });
                bodies.Shots.Add(new Shot { Name = label + "_front", Frame = () => { ShowPlayerBody(RaceChoice(id)); CartSeatView("PlayerCart", 40f, 3.6f); } });
            }
            groups.Add(bodies);

            // All the bodies at once (copies of the first rival's cart, kid and helmet included): from the front and from above.
            var lineup = new Group { Name = "race_cart_lineup", Stage = StageCartLineup };
            lineup.Shots.Add(new Shot { Name = "front", Frame = () => RaceLook(new Vector3(RaceStreetX + 4f, 9f, LineupZ + 9.5f), new Vector3(RaceStreetX, 0.5f, LineupZ + 1.3f), 42f, 600f) });
            lineup.Shots.Add(new Shot { Name = "top", Frame = () => RaceLook(new Vector3(RaceStreetX, 14f, LineupZ - 2.5f), new Vector3(RaceStreetX, 0f, LineupZ + 1.3f), 42f, 600f) });
            groups.Add(lineup);

            // The paints: the fire truck with every paint (it uses the PolygonTown atlas), then a few other bodies in one paint each.
            var looks = new Group { Name = "race_cart_looks", Stage = StageRaceSeated };
            var paints = new[]
            {
                ("none", ""), ("gold", CartLooks.GoldId), ("red", CartGarageData.PaintRed), ("blue", CartGarageData.PaintBlue),
                ("green", CartGarageData.PaintGreen), ("pink", CartGarageData.PaintPink), ("purple", CartGarageData.PaintPurple),
            };
            foreach ((string label, string paint) in paints)
            {
                string chosenPaint = paint;
                looks.Shots.Add(new Shot { Name = "firetruck_" + label, Frame = () => { ShowPlayerBody(RaceChoice("cart.firetruck", chosenPaint)); CartSeatView("PlayerCart", 40f, 3.4f); } });
            }
            var others = new[]
            {
                ("police_blue", "cart.police", CartGarageData.PaintBlue), ("convertible_red", "cart.convertible", CartGarageData.PaintRed),
                ("rocket_purple", "cart.rocket", CartGarageData.PaintPurple), ("horse_pink", "cart.horse", CartGarageData.PaintPink),
                ("boat_green", "cart.boat", CartGarageData.PaintGreen), ("wagon_gold", "cart.wagon", CartLooks.GoldId),
                ("racer_1_blue", "cart.racer_1", CartGarageData.PaintBlue),
            };
            foreach ((string label, string body, string paint) in others)
            {
                string chosenBody = body;
                string chosenPaint = paint;
                looks.Shots.Add(new Shot { Name = label, Frame = () => { ShowPlayerBody(RaceChoice(chosenBody, chosenPaint)); CartSeatView("PlayerCart", 40f, 3.4f); } });
            }
            groups.Add(looks);

            // Every add-on, the plate and the rainbow trail, from the chase camera: do the balloons or the propeller block the road ahead?
            var addons = new Group { Name = "race_cart_addons_chase", Stage = StageRaceSeated };
            foreach (string body in new[] { "cart.racer_1", "cart.horse", "cart.trike", "cart.firetruck", "cart.rocket" })
            {
                string chosenBody = body;
                addons.Shots.Add(new Shot
                {
                    Name = body.Replace("cart.", ""),
                    Frame = () =>
                    {
                        ShowPlayerBody(RaceChoice(chosenBody, "", AllAddonIds().ToArray(), CartGarageData.TrailRainbow, 7));
                        FakeTrail();
                        ChaseView("PlayerCart", 0f);
                    },
                });
            }
            groups.Add(addons);

            // The pit lane: with everything won (models) and with a fresh save (silhouettes except the starters).
            groups.Add(PitLaneGroup("race_pit_lane", true));
            groups.Add(PitLaneGroup("race_pit_lane_locked", false));

            // The picker on each tab (everything won), and its first two tabs with a fresh save (locked cards).
            string[] tabNames = { "body", "paint", "addon", "trail", "horn", "number" };
            var tabs = new Group { Name = "race_cart_picker_tabs", Stage = () => StageCartPickerTabs(true) };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tab = i;
                tabs.Shots.Add(new Shot { Name = tabNames[i], Frame = () => PickerTab(tab) });
            }
            groups.Add(tabs);

            var locked = new Group { Name = "race_cart_picker_locked", Stage = () => StageCartPickerTabs(false) };
            locked.Shots.Add(new Shot { Name = "body", Frame = () => PickerTab(0) });
            locked.Shots.Add(new Shot { Name = "horn", Frame = () => PickerTab(4) });
            groups.Add(locked);
        }

        // ----- The choice ------------------------------------------------------------------------

        /// <summary>The ids of every add-on the garage has (the flag, the stickers, the googly eyes...).</summary>
        static IEnumerable<string> AllAddonIds()
        {
            yield return CartLooks.FlagId;
            yield return CartLooks.StickersId;
            foreach (CartGarageData.Item item in CartGarageData.OfPart(CartPart.Addon)) yield return item.Id;
        }

        /// <summary>A cart choice: this body and paint, only the listed add-ons on, this trail and number.</summary>
        static CartChoice RaceChoice(string bodyId, string paintId = "", string[] addonsOn = null, string trailId = "", int number = CartChoice.DefaultNumber)
        {
            var choice = new CartChoice { bodyId = bodyId, paintId = paintId, trailId = trailId, number = number };
            var on = new HashSet<string>(addonsOn ?? new string[0]);
            foreach (string id in AllAddonIds())
                if (!on.Contains(id)) choice.addonsOff.Add(id);
            return choice;
        }

        /// <summary>Puts a look on the player's cart (everything shown, owned or not) and lets the seated kid follow the seat.</summary>
        static void ShowPlayerBody(CartChoice choice)
        {
            GameObject cart = GameObject.Find("PlayerCart");
            if (cart == null) throw new InvalidOperationException("no PlayerCart");

            cart.GetComponent<CartLooks>().Apply(choice, false);
            Physics.SyncTransforms();
        }

        // ----- The lineup ------------------------------------------------------------------------

        /// <summary>11 copies of the first rival's cart on the road behind the banner (two staggered rows), each with a body, its kid sitting and helmeted.</summary>
        static void StageCartLineup()
        {
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));
            GameObject source = GameObject.Find("RivalCart_A");
            if (source == null) throw new InvalidOperationException("no RivalCart_A");

            int count = SoapboxStreetBuilder.BodyDefs.Length;
            const int firstRow = 6;
            for (int i = 0; i < count; i++)
            {
                int row = i < firstRow ? 0 : 1;
                int column = i < firstRow ? i : i - firstRow;
                float x = row == 0 ? RaceStreetX + (column - 2.5f) * 1.45f : RaceStreetX + (column - 2f) * 1.45f;
                float z = LineupZ + row * 2.6f;

                GameObject copy = UnityEngine.Object.Instantiate(source, new Vector3(x, SoapboxStreetBuilder.RoadY, z), Quaternion.identity);
                copy.name = "LineupCart_" + i;
                copy.GetComponent<CartLooks>().Apply(RaceChoice(SoapboxStreetBuilder.BodyDefs[i].Id), false);
                foreach (KidAnimator kid in copy.GetComponentsInChildren<KidAnimator>(true))
                {
                    ActivateWithParents(kid.gameObject);
                    ShowHelmet(kid.gameObject);
                    SitAndSettle(kid.gameObject);
                }
            }
            Physics.SyncTransforms();
        }

        // ----- The trail -------------------------------------------------------------------------

        /// <summary>
        /// A trail is only drawn while a cart moves, and nothing moves in a still picture: lay the rainbow ribbon out behind the cart point by point,
        /// and let the particle trails run for a moment (so the chase shot shows what a trail looks like and whether it hides anything).
        /// </summary>
        static void FakeTrail()
        {
            GameObject cart = GameObject.Find("PlayerCart");
            if (cart == null) return;

            Vector3 back = -cart.transform.forward;
            foreach (TrailRenderer ribbon in cart.GetComponentsInChildren<TrailRenderer>(false))
            {
                ribbon.Clear();
                ribbon.emitting = true;
                Vector3 start = ribbon.transform.position;
                for (int step = 16; step >= 0; step--) ribbon.AddPosition(start + back * (step * 0.2f));
            }
            foreach (ParticleSystem system in cart.GetComponentsInChildren<ParticleSystem>(false))
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = true;
                system.Simulate(0.9f, true, true);
            }
        }

        // ----- The pit lane ----------------------------------------------------------------------

        static Group PitLaneGroup(string name, bool everythingWon)
        {
            var group = new Group { Name = name, Stage = () => StagePitLane(everythingWon) };
            // From the playground side with the follow camera's pose (the kid at the first bay), then the row from the road and from above.
            group.Shots.Add(new Shot { Name = "follow", Frame = () => RaceLook(new Vector3(PitLaneX - 1.2f, 5.6f, RaceStartZ - 14.5f), new Vector3(PitLaneX, 0.6f, RaceStartZ + 4f), 50f) });
            group.Shots.Add(new Shot { Name = "row_south", Frame = () => RaceLook(new Vector3(RaceStreetX + 1.5f, 2.6f, RaceStartZ - 3.5f), new Vector3(PitLaneX, 0.5f, RaceStartZ + 6f), 52f) });
            group.Shots.Add(new Shot { Name = "row_north", Frame = () => RaceLook(new Vector3(RaceStreetX + 1.5f, 2.6f, RaceStartZ + 22f), new Vector3(PitLaneX, 0.5f, RaceStartZ + 9f), 52f) });
            group.Shots.Add(new Shot { Name = "high", Frame = () => RaceLook(new Vector3(RaceStreetX - 1f, 17f, RaceStartZ - 6f), new Vector3(PitLaneX, 0f, RaceStartZ + 5f), 48f, 600f) });
            return group;
        }

        static void StagePitLane(bool everythingWon)
        {
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));

            PitLane pit = UnityEngine.Object.FindFirstObjectByType<PitLane>(FindObjectsInactive.Include);
            if (pit == null) throw new InvalidOperationException("no PitLane");
            ActivateWithParents(pit.gameObject);

            pit.Refresh(everythingWon ? FullGarageProgress(CatalogOf(pit)) : new PlayerProgress());
        }

        static RewardCatalog CatalogOf(UnityEngine.Object component)
        {
            SerializedProperty property = new SerializedObject(component).FindProperty("catalog");
            return property != null ? property.objectReferenceValue as RewardCatalog : null;
        }

        /// <summary>Every item of the catalog won (nothing new, no medals).</summary>
        static PlayerProgress FullGarageProgress(RewardCatalog catalog)
        {
            var progress = new PlayerProgress();
            if (catalog != null)
                foreach (RewardItem item in catalog.items)
                    if (item != null && !item.starter && !string.IsNullOrEmpty(item.id)) progress.unlockedItems.Add(item.id);
            return progress;
        }

        // ----- The picker ------------------------------------------------------------------------

        /// <summary>
        /// The player in his cart (wearing a police car with blue paint when everything is won, racer 1 otherwise) and the picker open on the body tab,
        /// shown through the camera like the wardrobe's menu. The progress the picker reads is a temporary one for this picture.
        /// </summary>
        static void StageCartPickerTabs(bool everythingWon)
        {
            StageRaceSeated();

            CartPicker picker = UnityEngine.Object.FindFirstObjectByType<CartPicker>(FindObjectsInactive.Include);
            if (picker == null) throw new InvalidOperationException("no CartPicker");
            GameObject cart = GameObject.Find("PlayerCart");
            var looks = cart.GetComponent<CartLooks>();

            UsePreviewProgress(everythingWon ? FullGarageProgress(picker.Catalog) : new PlayerProgress());

            // The touch canvas is a screen overlay, which a camera render does not draw: show it through the camera for this picture.
            Canvas canvas = picker.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                foreach (Transform child in canvas.transform)
                    if (child != picker.transform) child.gameObject.SetActive(false);
            }
            ActivateWithParents(picker.gameObject);

            CartChoice start = everythingWon
                ? RaceChoice("cart.police", CartGarageData.PaintBlue, new[] { CartLooks.FlagId, CartGarageData.Eyes }, CartGarageData.TrailStars, 7)
                : RaceChoice("cart.racer_1");
            looks.Apply(start, true);
            picker.Open(start, choice => looks.Apply(choice, true), () => { });
            Physics.SyncTransforms();
        }

        /// <summary>Opens a tab of the picker (the cards of the tab before are removed by hand: the picker's own Destroy does nothing in edit mode).</summary>
        static void PickerTab(int tab)
        {
            CartPicker picker = UnityEngine.Object.FindFirstObjectByType<CartPicker>(FindObjectsInactive.Include);
            if (picker == null) throw new InvalidOperationException("no CartPicker");

            var cardArea = new SerializedObject(picker).FindProperty("cardArea").objectReferenceValue as RectTransform;
            if (cardArea != null)
                for (int i = cardArea.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(cardArea.GetChild(i).gameObject);

            MethodInfo select = typeof(CartPicker).GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic);
            if (select == null) throw new InvalidOperationException("CartPicker.SelectTab was not found (renamed?)");
            select.Invoke(picker, new object[] { tab, false });

            Canvas.ForceUpdateCanvases();
            CartPickerView();
        }

        /// <summary>
        /// Makes the picker (and everything else that reads ProgressStore.Current) see this progress. Nothing saves it: only the temporary
        /// object is changed and the process ends after the shots (play mode resets the static again).
        /// </summary>
        static void UsePreviewProgress(PlayerProgress progress)
        {
            FieldInfo field = typeof(ProgressStore).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("ProgressStore.current was not found (renamed?)");
            field.SetValue(null, progress);
        }
    }
}
