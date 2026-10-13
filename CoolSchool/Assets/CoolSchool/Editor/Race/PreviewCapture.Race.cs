using System;
using System.Collections.Generic;
using System.Linq;
using CoolSchool.Characters;
using CoolSchool.Race;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>The soapbox street shots of PreviewCapture: the start area, the chase view, an overview, seated kids, the animals, the podium.</summary>
    public static partial class PreviewCapture
    {
        const float RaceStreetX = SoapboxStreetBuilder.StreetX;
        const float RaceStartZ = SoapboxStreetBuilder.StartLineZ;

        static float RaceZ(float distance) => RaceStartZ + distance;

        static void AddRaceGroups(List<Group> groups)
        {
            // From the playground gap with the follow camera's pose: the zone, the banner, the stand and the start of the street.
            var start = new Group { Name = "race_start", Stage = () => { } };
            start.Shots.Add(new Shot { Name = "from_gap", Frame = () => RaceLook(new Vector3(RaceStreetX, 5.5f, RaceStartZ - 16f), new Vector3(RaceStreetX, 1.2f, RaceStartZ + 2f), 50f) });
            start.Shots.Add(new Shot { Name = "banner", Frame = () => RaceLook(new Vector3(RaceStreetX + 2f, 3.5f, RaceStartZ - 9f), new Vector3(RaceStreetX, 2.5f, RaceStartZ), 50f) });
            start.Shots.Add(new Shot { Name = "announcer", Frame = () => RaceLook(new Vector3(RaceStreetX - 1f, 2f, RaceStartZ - 3f), new Vector3(SoapboxStreetBuilder.WestSidewalkX, 1.1f, RaceStartZ + 3.2f), 40f) });
            groups.Add(start);

            // The chase camera's pose behind the player's cart (offset 0, 3, -6; looks 6 m ahead): at the start line, and in front of the first gate.
            var chase = new Group { Name = "race_chase", Stage = StageRaceSeated };
            chase.Shots.Add(new Shot { Name = "start", Frame = () => ChaseView("PlayerCart", 0f) });
            groups.Add(chase);

            var gate = new Group { Name = "race_chase_gate", Stage = () => { StageRaceSeated(); MoveCart("PlayerCart", 110f); } };
            gate.Shots.Add(new Shot { Name = "d110", Frame = () => ChaseView("PlayerCart", 110f) });
            groups.Add(gate);

            var ramp = new Group { Name = "race_chase_ramp", Stage = () => { StageRaceSeated(); MoveCart("PlayerCart", 185f); } };
            ramp.Shots.Add(new Shot { Name = "d185", Frame = () => ChaseView("PlayerCart", 185f) });
            // From the east sidewalk, level with the ramp: does it rise toward the north (to the right of the picture)?
            ramp.Shots.Add(new Shot { Name = "side", Frame = () => RaceLook(new Vector3(RaceStreetX + 6f, 1.5f, RaceZ(200f)), new Vector3(RaceStreetX, 0.5f, RaceZ(200f)), 40f, 600f) });
            ramp.Shots.Add(new Shot { Name = "top", Frame = () => RaceLook(new Vector3(RaceStreetX, 7f, RaceZ(200f) - 2f), new Vector3(RaceStreetX, 0f, RaceZ(200f)), 40f, 600f) });
            groups.Add(ramp);

            // High views of the street.
            var street = new Group { Name = "race_street", Stage = () => HideZoneVisualsRace() };
            street.Shots.Add(new Shot { Name = "start_high", Frame = () => RaceLook(new Vector3(RaceStreetX + 6f, 45f, RaceStartZ - 22f), new Vector3(RaceStreetX, 0f, RaceStartZ + 45f), 60f, 600f) });
            street.Shots.Add(new Shot { Name = "middle_high", Frame = () => RaceLook(new Vector3(RaceStreetX + 8f, 70f, RaceZ(200f) - 40f), new Vector3(RaceStreetX, 0f, RaceZ(200f) + 30f), 60f, 600f) });
            street.Shots.Add(new Shot { Name = "finish_high", Frame = () => RaceLook(new Vector3(RaceStreetX + 6f, 30f, RaceZ(440f)), new Vector3(RaceStreetX, 0f, RaceZ(485f)), 60f, 600f) });
            street.Shots.Add(new Shot { Name = "houses_side", Frame = () => RaceLook(new Vector3(RaceStreetX + 2f, 9f, RaceZ(150f)), new Vector3(RaceStreetX - 14f, 3f, RaceZ(170f)), 55f, 600f) });
            street.Shots.Add(new Shot { Name = "sidewalk_ground", Frame = () => RaceLook(new Vector3(RaceStreetX - 4.5f, 0.35f, RaceZ(60f)), new Vector3(RaceStreetX - 6f, 0.3f, RaceZ(75f)), 45f, 600f) });
            groups.Add(street);

            // A seated kid in a cart, from the side and from the front, and a rival.
            var seat = new Group { Name = "race_seat", Stage = StageRaceSeated };
            seat.Shots.Add(new Shot { Name = "player_side", Frame = () => CartSeatView("PlayerCart", 90f, 3.6f) });
            seat.Shots.Add(new Shot { Name = "player_front", Frame = () => CartSeatView("PlayerCart", 0f, 3.8f) });
            seat.Shots.Add(new Shot { Name = "player_three_quarter", Frame = () => CartSeatView("PlayerCart", 40f, 3.6f) });
            seat.Shots.Add(new Shot { Name = "rival_side", Frame = () => CartSeatView("RivalCart_A", 90f, 3.6f) });
            seat.Shots.Add(new Shot { Name = "rival_b_three_quarter", Frame = () => CartSeatView("RivalCart_B", -40f, 3.6f) });
            groups.Add(seat);

            // The cart picker's view: the camera spot in front of his cart, which wears racer 2 and the flag (the cards are screen UI, not in the shot).
            var picker = new Group { Name = "race_cart_picker", Stage = StageCartPicker };
            picker.Shots.Add(new Shot { Name = "camera", Frame = CartPickerView });
            groups.Add(picker);

            AddCartGarageGroups(groups);

            // The crossing animals on their sidewalk spots.
            var animals = new Group { Name = "race_animals", Stage = StageRaceAnimals };
            foreach (string kind in new[] { "cat", "dog", "hen", "rabbit" })
            {
                string animal = kind;
                animals.Shots.Add(new Shot { Name = animal, Frame = () => AnimalView("Animal_" + animal) });
            }
            groups.Add(animals);

            // The podium with the winner and the two rivals dancing.
            var podium = new Group { Name = "race_podium", Stage = StageRacePodium };
            podium.Shots.Add(new Shot { Name = "camera", Frame = PodiumCameraView });
            podium.Shots.Add(new Shot { Name = "close", Frame = () => RaceLook(new Vector3(RaceStreetX + 1.5f, 1.9f, RaceZ(SoapboxStreetBuilder.PodiumDistance) - 4.6f), new Vector3(RaceStreetX, 1.2f, RaceZ(SoapboxStreetBuilder.PodiumDistance)), 45f) });
            podium.Shots.Add(new Shot { Name = "arch", Frame = () => RaceLook(new Vector3(RaceStreetX, 3.2f, RaceZ(SoapboxStreetBuilder.FinishDistance) - 12f), new Vector3(RaceStreetX, 3f, RaceZ(SoapboxStreetBuilder.FinishDistance)), 50f, 600f) });
            groups.Add(podium);
        }

        // ----- Cameras -----

        /// <summary>Looks from the eye at the target with the field of view; the far plane can be pushed out for the street views.</summary>
        static void RaceLook(Vector3 eye, Vector3 target, float fov, float far = 200f)
        {
            LookAt(eye, target, fov);
            cam.farClipPlane = far;
        }

        /// <summary>The RaceChaseCamera pose behind a cart at a distance along the street: offset (0, 3, -6), looking 6 m ahead at 1 m.</summary>
        static void ChaseView(string cartName, float distance)
        {
            Vector3 cart = new Vector3(RaceStreetX, SoapboxStreetBuilder.RoadY, RaceZ(distance));
            RaceLook(cart + new Vector3(0f, 3f, -6f), cart + new Vector3(0f, 1f, 6f), 50f, 600f);
        }

        static void CartSeatView(string cartName, float azimuth, float distance)
        {
            GameObject cart = GameObject.Find(cartName);
            if (cart == null) throw new InvalidOperationException("no " + cartName);
            Transform seat = cart.GetComponent<CartController>().Seat;
            Orbit(seat.position + Vector3.up * 0.45f, cart.transform, azimuth, distance, 14f, 34f);
        }

        static void AnimalView(string animalName)
        {
            GameObject animal = GameObject.Find(animalName);
            if (animal == null) throw new InvalidOperationException("no " + animalName);
            Bounds bounds = BoundsOf(animal);
            // From the street side, a little ahead of it and low.
            float towardStreet = animal.transform.forward.x >= 0f ? 1f : -1f;
            Vector3 eye = bounds.center + new Vector3(towardStreet * 2.6f, 0.5f, -2.2f);
            RaceLook(eye, bounds.center, 38f);
        }

        static void PodiumCameraView()
        {
            GameObject spot = GameObject.Find("PodiumCameraSpot");
            if (spot == null) throw new InvalidOperationException("no PodiumCameraSpot");
            cam.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
            cam.fieldOfView = 50f;
            cam.farClipPlane = 200f;
        }

        static void CartPickerView()
        {
            GameObject spot = GameObject.Find(SoapboxStreetBuilder.CartPickCameraSpotName);
            if (spot == null) throw new InvalidOperationException("no " + SoapboxStreetBuilder.CartPickCameraSpotName);
            cam.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
            cam.fieldOfView = 50f;
            cam.farClipPlane = 200f;
        }

        // ----- Staging -----

        /// <summary>The player's cart with racer 2 and the flag, the rivals on the other bodies and sitting; the player is not in a cart.</summary>
        static void StageCartPicker()
        {
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));

            GameObject playerCart = GameObject.Find("PlayerCart");
            if (playerCart == null) throw new InvalidOperationException("no PlayerCart");
            var looks = playerCart.GetComponent<CartLooks>();
            looks.Apply(RaceChoice(looks.BodyIdAt(1), "", new[] { CartLooks.FlagId }), false);

            int[] rivalBodies = { 0, 2 };
            string[] rivalNames = { "RivalCart_A", "RivalCart_B" };
            for (int i = 0; i < rivalNames.Length; i++)
            {
                GameObject cart = GameObject.Find(rivalNames[i]);
                if (cart == null) continue;

                var rivalLooks = cart.GetComponent<CartLooks>();
                rivalLooks.Apply(RaceChoice(rivalLooks.BodyIdAt(rivalBodies[i])), false);
                foreach (KidAnimator kid in cart.GetComponentsInChildren<KidAnimator>(true))
                {
                    ActivateWithParents(kid.gameObject);
                    ShowHelmet(kid.gameObject);
                    SitAndSettle(kid.gameObject);
                }
            }
            Physics.SyncTransforms();
        }

        static void HideZoneVisualsRace()
        {
            // The street views are about the street itself, the zone's beacon is only in the start views.
            HideZoneVisuals();
        }

        static void MoveCart(string cartName, float distance)
        {
            GameObject cart = GameObject.Find(cartName);
            if (cart == null) throw new InvalidOperationException("no " + cartName);
            cart.transform.position = new Vector3(cart.transform.position.x, SoapboxStreetBuilder.RoadY, RaceZ(distance));
            Physics.SyncTransforms();
        }

        /// <summary>The player in his cart, sitting, with a helmet; the two rivals sit and wear theirs.</summary>
        static void StageRaceSeated()
        {
            // While the race runs, ActivityZone hides its glowing zone (the activity is current); the edit-mode shot has to do it by hand.
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));
            GameObject player = GameObject.Find("Player");
            GameObject playerCart = GameObject.Find("PlayerCart");
            if (player == null || playerCart == null) throw new InvalidOperationException("no Player or PlayerCart");

            ActivateWithParents(player);
            DisableMovers(player);
            SeatUtil.Mount(player.transform, playerCart.GetComponent<CartController>().Seat);
            ShowHelmet(player);

            foreach (string rival in new[] { "RivalCart_A", "RivalCart_B" })
            {
                GameObject cart = GameObject.Find(rival);
                if (cart == null) continue;
                foreach (KidAnimator kid in cart.GetComponentsInChildren<KidAnimator>(true))
                {
                    ActivateWithParents(kid.gameObject);
                    ShowHelmet(kid.gameObject);
                }
            }

            foreach (string cartName in new[] { "PlayerCart", "RivalCart_A", "RivalCart_B" })
            {
                GameObject cart = GameObject.Find(cartName);
                if (cart == null) continue;
                foreach (KidAnimator kid in cart.GetComponentsInChildren<KidAnimator>(true)) SitAndSettle(kid.gameObject);
            }
            // The player is parented to the seat of his cart: found as a child of it.
            SitAndSettle(player);
            Physics.SyncTransforms();
        }

        static void SitAndSettle(GameObject kid)
        {
            var animator = kid.GetComponent<KidAnimator>();
            ResetAnimators(kid);
            if (animator != null) animator.SetSitting(true);
            StepAnimators(kid, 1.0f);
        }

        static void ShowHelmet(GameObject kid)
        {
            var helmet = kid.GetComponent<RaceHelmet>();
            if (helmet != null) helmet.Show(true);
        }

        static void StageRaceAnimals()
        {
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));
            foreach (string name in new[] { "Animal_cat", "Animal_dog", "Animal_hen", "Animal_rabbit" })
            {
                GameObject animal = GameObject.Find(name);
                if (animal == null) continue;
                ActivateWithParents(animal);
                ResetAnimators(animal);
                StepAnimators(animal, 0.4f);
            }
        }

        /// <summary>The player on the middle block, the rivals on the others, all dancing.</summary>
        static void StageRacePodium()
        {
            HideZoneVisuals();
            ActivateWithParents(GameObject.Find("RaceObjects"));
            GameObject player = GameObject.Find("Player");
            if (player == null) throw new InvalidOperationException("no Player");

            var kids = new List<GameObject> { player };
            foreach (string cartName in new[] { "RivalCart_A", "RivalCart_B" })
            {
                GameObject cart = GameObject.Find(cartName);
                KidAnimator kid = cart != null ? cart.GetComponentInChildren<KidAnimator>(true) : null;
                if (kid != null) kids.Add(kid.gameObject);
            }

            for (int i = 0; i < kids.Count; i++)
            {
                GameObject spot = GameObject.Find("PodiumSpot_" + (i + 1));
                if (spot == null) continue;
                GameObject kid = kids[i];
                ActivateWithParents(kid);
                DisableMovers(kid);
                kid.transform.SetParent(null, true);
                kid.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
                var animator = kid.GetComponent<KidAnimator>();
                ResetAnimators(kid);
                if (animator != null) animator.SetDancing(true, i);
                StepAnimators(kid, 1.2f);
            }
            Physics.SyncTransforms();
        }
    }
}
