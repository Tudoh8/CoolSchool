using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Zoo;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>Step 2 of the zoo park: everything alive (animals, critters, the zookeeper, pens, spots), the UI, the activity and its wiring.</summary>
    internal static partial class ZooParkBuilder
    {
        const string InviteGroup = "invite.zoo";
        // The picture on a pen sign is this tall (metres): 80 percent of the 0.5 m board; the picture is square.
        const float SignIconHeight = 0.4f;

        /// <summary>
        /// Adds the runtime components to the static park, creates the animals, the critters and the zookeeper, renders the icons,
        /// builds the checklist and the eyes-closed overlay on the touch canvas, the HideAndSeekGame with every field wired, the
        /// zone (added to the inviter after the others) and the free-play display of the pens.
        /// Call after the NavMesh is baked, after the player exists and after the lesson UI and the inviter exist.
        /// </summary>
        internal static void Build(ParkLayout p, ZooInputs inputs)
        {
            if (p == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the zoo park was not built (no park layout or no player).");
                return;
            }

            // A mistake in the park must not break the whole world build: log it loudly and carry on.
            try
            {
                BuildCore(p, inputs);
            }
            catch (System.Exception e)
            {
                Debug.LogError("CoolSchool zoo park: Build failed, the zoo is incomplete in this build: " + e);
            }
        }

        static void BuildCore(ParkLayout p, ZooInputs inputs)
        {
            SnapPoint(p.CountPoint);
            SnapPoint(p.PenWalkPoint);
            foreach (Transform party in p.PartyPoints) SnapPoint(party);

            // The characters.
            Transform people = NewGroup("Characters", p.Root);
            AnimalSet set = CreateAnimals(p, people);
            SurpriseCritter[] critters = CreateCritters(p, people);
            ZooKeeper keeper = CreateKeeper(p, inputs, people);

            // The pictures, drawn from the prepared animals: on the signs and in the checklist.
            string[] ids = ZooAnimalKind.Order.Select(k => k.Id).ToArray();
            Sprite[] icons = ZooIconRenderer.RenderAll(ids, set.Models);

            ZooPen[] pens = WirePens(p, icons);
            HidingSpot[] spots = WireSpots(p);

            // The screen pieces.
            ZooChecklistUI checklist = BuildChecklist();
            EyesClosedOverlay eyes = BuildEyesOverlay();

            // The activity: the game, the conga line and the sparkle trail live on one object.
            var gameObject = NewGroup("ZooGame", p.Root).gameObject;
            var game = gameObject.AddComponent<HideAndSeekGame>();
            var conga = gameObject.AddComponent<CongaLine>();
            var trail = gameObject.AddComponent<SparkleTrail>();
            WireGame(game, p, inputs, keeper, set, icons, pens, spots, critters, conga, trail, checklist, eyes);

            // The pens in free play.
            var display = p.Root.gameObject.AddComponent<ZooParkDisplay>();
            SetObjectArray(display, "pens", pens);
            SetObjectArray(display, "animals", set.Animals);
            SetObject(display, "game", game);

            // The zone and the invitation.
            Material ring = KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/ZooZoneRing.mat", new Color(0.35f, 0.9f, 0.4f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("ZooZone", p.Root, p.ZonePoint.position, ZoneRadius, game, ring, ZooBeacon);
            AddToInviter(zone);

            Debug.Log($"CoolSchool zoo park: {set.Animals.Count(a => a != null)} animals, {critters.Length} critters, keeper {(keeper != null ? "yes" : "no")}, "
                + $"{icons.Count(s => s != null)} of {icons.Length} icons, {pens.Length} pens, {spots.Length} spots, "
                + $"checklist {(checklist != null ? "yes" : "no")}, eyes overlay {(eyes != null ? "yes" : "no")}, zone {p.ZonePoint.position.ToString("0.0")}.");
        }

        static void SnapPoint(Transform point)
        {
            if (point != null) point.position = SnapToNavMesh(point.position);
        }

        // ---- Pens and spots ----

        /// <summary>A ZooPen on every pen root, with the kind's picture on its sign (sized to 0.4 m tall).</summary>
        static ZooPen[] WirePens(ParkLayout p, Sprite[] icons)
        {
            var pens = new ZooPen[p.Pens.Count];
            for (int i = 0; i < p.Pens.Count; i++)
            {
                PenData data = p.Pens[i];
                if (data.Sign != null && i < icons.Length && icons[i] != null)
                {
                    data.Sign.sprite = icons[i];
                    float height = Mathf.Max(0.01f, icons[i].bounds.size.y);
                    data.Sign.transform.localScale = Vector3.one * (SignIconHeight / height);
                    EditorUtility.SetDirty(data.Sign);
                }

                var pen = data.Root.gameObject.AddComponent<ZooPen>();
                SetString(pen, "kindId", data.KindId);
                SetObject(pen, "gate", data.Gate);
                SetObject(pen, "standPoint", data.Stand);
                SetObject(pen, "gatePoint", data.GatePoint);
                SetObject(pen, "sign", data.Sign);
                pens[i] = pen;
            }
            return pens;
        }

        /// <summary>A HidingSpot on every spot root, in the table's order.</summary>
        static HidingSpot[] WireSpots(ParkLayout p)
        {
            var spots = new HidingSpot[p.Spots.Count];
            for (int i = 0; i < p.Spots.Count; i++)
            {
                SpotData data = p.Spots[i];
                var spot = data.Root.gameObject.AddComponent<HidingSpot>();
                SetString(spot, "spotId", data.Id);
                SetEnum(spot, "kind", data.Kind);
                SetObject(spot, "cover", data.Cover);
                SetObject(spot, "hidePoint", data.Hide);
                SetObject(spot, "peekPoint", data.Peek);
                SetObject(spot, "playerHidePoint", data.PlayerHide);
                SetFloat(spot, "sinkFraction", data.Sink);
                SetFloat(spot, "rimHeight", data.Rim);
                SetFloat(spot, "showFraction", data.Show);
                SetFloat(spot, "frontOut", data.FrontOut);
                SetFloat(spot, "checkRadius", data.CheckRadius);
                SetObject(spot, "playerGlow", data.Glow);
                spots[i] = spot;
            }
            return spots;
        }

        // ---- The activity ----

        static void WireGame(HideAndSeekGame game, ParkLayout p, ZooInputs inputs, ZooKeeper keeper, AnimalSet set, Sprite[] icons, ZooPen[] pens,
            HidingSpot[] spots, SurpriseCritter[] critters, CongaLine conga, SparkleTrail trail, ZooChecklistUI checklist, EyesClosedOverlay eyes)
        {
            // Player
            SetObject(game, "player", inputs.Player);
            SetObject(game, "playerAnimator", inputs.Player.GetComponent<KidAnimator>());

            // Park
            SetObject(game, "keeper", keeper);
            SetObjectArray(game, "animals", set.Animals);
            SetObjectArray(game, "icons", icons);
            SetObjectArray(game, "pens", pens);
            SetObjectArray(game, "spots", spots);
            SetObjectArray(game, "critters", critters);
            SetObject(game, "conga", conga);
            SetObject(game, "trail", trail);
            SetObject(game, "penWalkPoint", p.PenWalkPoint);
            SetObjectArray(game, "partyPoints", p.PartyPoints);
            SetObject(game, "countPoint", p.CountPoint);

            // UI
            SetObject(game, "checklist", checklist);
            SetObject(game, "eyes", eyes);
            SetObject(game, "lessonUI", Object.FindFirstObjectByType<LessonUI>());

            // Tuning
            SetFloat(game, "countStepSeconds", 0.9f);
            SetFloat(game, "callRadius", 8f);
        }

        /// <summary>Adds the zoo zone at the END of the inviter's turns (after the others), with its own invite line. Skips quietly if there is no inviter.</summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the zoo park will not invite the kid.");
                return;
            }

            var serialized = new SerializedObject(inviter);
            SerializedProperty zones = serialized.FindProperty("zones");
            SerializedProperty groups = serialized.FindProperty("inviteGroups");
            if (zones == null || groups == null) return;

            // The groups line up with the zones; pad them first in case an earlier zone was missing.
            while (groups.arraySize < zones.arraySize)
            {
                groups.InsertArrayElementAtIndex(groups.arraySize);
                groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = string.Empty;
            }

            zones.InsertArrayElementAtIndex(zones.arraySize);
            zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = zone;
            groups.InsertArrayElementAtIndex(groups.arraySize);
            groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = InviteGroup;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
