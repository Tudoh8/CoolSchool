using System.Collections.Generic;
using System.Linq;
using CoolSchool.Battle;
using CoolSchool.Castle;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>Step 2 of the castle adventure: the queen and the other living things, every runtime component, and the wiring into the KnightsGame.</summary>
    internal static partial class CastleAdventureBuilder
    {
        const string RewardCatalogPath = RootFolder + "/Rewards/Catalog.asset";
        const float QueenWalkSpeed = 2.2f;

        static void BuildMoments(KnightsArenaBuilder.ArenaLayout layout, PropLayout p, CastleInputs inputs, KnightsGame game)
        {
            SnapPointsToNavMesh(p);

            var dragon = Object.FindFirstObjectByType<DragonBoss>(FindObjectsInactive.Include);
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            var lessonUI = Object.FindFirstObjectByType<LessonUI>();
            var letters = AssetDatabase.LoadAssetAtPath<LetterSet>(LetterSetSeeder.AssetPath);
            if (lessonUI == null) Debug.LogWarning("CoolSchool castle: no LessonUI in the scene, so the card questions cannot show cards.");
            if (letters == null) Debug.LogWarning("CoolSchool castle: no letter set found, so the letter moments have no letters.");

            Transform characters = NewGroup("Characters", p.Root);

            // One tap picker for all the "tap the 3D thing" moments.
            var pickerObject = new GameObject("TapPicker");
            pickerObject.transform.SetParent(p.Root, false);
            var picker = pickerObject.AddComponent<WorldTapPicker>();
            SetObject(picker, "cam", cam);

            // The living things.
            CastleQueen queen = CreateQueen(p, inputs, characters);
            Transform peekGoblin = CreatePeekGoblin(layout, p, inputs, characters);
            BabyDragon baby = CreateBabyDragon(p, inputs, characters);
            int markers = AddHelpMarkers(inputs);

            // The moments, each on the group that holds its props.
            CastleMissionProps missionProps = BuildMissionProps(p);
            PasswordGate password = BuildPasswordGate(p, picker, queen);
            WallPatch wall = BuildWallComponent(p, picker, peekGoblin);
            CookiePlate cookies = BuildCookiePlate(p, picker, inputs);
            DragonEgg egg = BuildEggComponent(p, picker, baby);
            DragonNaming naming = BuildNaming(p.Root, lessonUI, letters, baby);
            DragonRidePath ridePath = BuildRidePath(p);
            NameFlag nameFlag = BuildNameFlag(p, lessonUI, letters, queen);
            CastleGrowth growth = BuildGrowth(p, queen);
            TowerRescue towerRescue = BuildTowerRescue(p);
            CastleGuideMarker guide = BuildGuideMarker(p.Root);
            CastleBarrel[] barrels = BuildBarrelComponents(p);

            if (dragon == null) Debug.LogWarning("CoolSchool castle: no DragonBoss found; the ride has no dragon.");
            WireGame(layout, p, inputs, game, lessonUI, letters, queen, missionProps, guide, password, wall, cookies, egg, naming, baby, ridePath,
                nameFlag, growth, towerRescue, barrels);

            LogSummary(layout, p, ridePath, barrels, markers, dragon);
        }

        static void SnapPointsToNavMesh(PropLayout p)
        {
            var points = new List<Transform>
            {
                p.GateTalkSpot, p.QueenHome, p.QueenSpot, p.QueenCelebrateSpot, p.BabyCelebrateSpot, p.DismountPoint, p.StandPoint,
            };
            points.AddRange(p.PerchLandings);
            points.AddRange(p.ThiefPoints);
            points.AddRange(p.BarrelHopOut);

            foreach (Transform point in points)
                if (point != null) point.position = SnapToNavMesh(point.position);
        }

        // ----- The queen -------------------------------------------------------------------------------------

        /// <summary>
        /// The queen: the princess kid with a paper crown on her head bone, a NavMeshAgent and the Kid animator, no Hittable, Kids layer.
        /// She stands at her home point (outside the gate) facing the way that point faces.
        /// </summary>
        static CastleQueen CreateQueen(PropLayout p, CastleInputs inputs, Transform parent)
        {
            GameObject root = CreateKid("Queen", PrincessPrefab, inputs.KidController, p.QueenHome.position, out float height);
            root.transform.SetParent(parent, true);
            root.transform.rotation = p.QueenHome.rotation;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = height;
            agent.angularSpeed = 720f;
            agent.acceleration = 12f;
            agent.speed = QueenWalkSpeed;

            var queen = root.AddComponent<CastleQueen>();
            SetObject(queen, "home", p.QueenHome);
            SetFloat(queen, "walkSpeed", QueenWalkSpeed);

            AttachPaperCrown(root);
            SetLayerRecursive(root, inputs.KidsLayer);
            return queen;
        }

        /// <summary>The paper crown on the kid's head bone, fitted by AttachmentFit like the other hats; no colliders.</summary>
        static void AttachPaperCrown(GameObject kid)
        {
            GameObject crown = LoadPrefab(PaperCrownPrefab);
            if (crown == null) return;

            Animator animator = kid.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) head = PirateCoveBuilder.FindDeep(kid.transform, "Head");
            if (head == null)
            {
                Debug.LogWarning("CoolSchool castle: the queen has no head bone, so she has no paper crown.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(crown, head);
            AttachmentFit.Place(instance.transform, animator, head, Vector3.zero, Vector3.zero, Vector3.one);
            StripColliders(instance);
        }

        // ----- The goblin that peeks through the hole -------------------------------------------------------

        /// <summary>
        /// The goblin that peeks while he patches the wall: a normal goblin model with the Kid animator only (no AI, no collider), scaled 1.
        /// The hole is a decal on the wall's inner face, and a goblin behind the wall could not be seen through it, so it stands on the
        /// OUTSIDE of the back wall at the hole's x with its head just under the wall's top; its 0.5 m rise (WallPatch) puts the head
        /// over the top edge. Switched off; WallPatch switches it on for each peek.
        /// </summary>
        static Transform CreatePeekGoblin(KnightsArenaBuilder.ArenaLayout layout, PropLayout p, CastleInputs inputs, Transform parent)
        {
            GameObject goblin = CreateKid("PeekGoblin", PeekGoblinPrefab, inputs.KidController, p.PeekGoblinFeet, out float height);
            goblin.transform.SetParent(parent, true);

            float bodyHeight = TryGetBounds(goblin, out Bounds bounds) ? bounds.size.y : height;
            Vector3 rest = new Vector3(p.PeekGoblinFeet.x, layout.BackWallTopY - 0.15f - bodyHeight, p.PeekGoblinFeet.z);
            goblin.transform.SetPositionAndRotation(rest, SouthFacing);

            SetLayerRecursive(goblin, inputs.KidsLayer);
            goblin.SetActive(false);
            return goblin.transform;
        }

        // ----- The baby dragon -----------------------------------------------------------------------------

        /// <summary>The BabyDragon component on an instance of the baby dragon prefab, waiting (switched off) at the nest.</summary>
        static BabyDragon CreateBabyDragon(PropLayout p, CastleInputs inputs, Transform parent)
        {
            GameObject prefab = EnsureBabyDragonPrefab();
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "BabyDragon";
            instance.transform.SetPositionAndRotation(p.NestPosition, SouthFacing);

            var baby = instance.AddComponent<BabyDragon>();
            SetLayerRecursive(instance, inputs.KidsLayer);
            instance.SetActive(false);
            return baby;
        }

        // ----- The knights' "help me" markers --------------------------------------------------------------

        /// <summary>A pulsing blue "!" (like the goblins' telegraph) 0.45 m above each ally's head, hidden. Returns how many were added.</summary>
        static int AddHelpMarkers(CastleInputs inputs)
        {
            if (inputs.Allies == null) return 0;

            Material help = HelpMat;
            int count = 0;
            foreach (KnightAllyAI ally in inputs.Allies)
            {
                if (ally == null) continue;

                float height = TryGetBounds(ally.gameObject, out Bounds bounds) ? bounds.size.y : 1.2f;
                GameObject marker = KnightsArenaBuilder.CreateTelegraph(ally.transform, height, help);
                marker.name = "HelpMarker";
                SetObject(ally, "helpMarker", marker);
                count++;
            }
            return count;
        }

        // ----- The moments' components -----------------------------------------------------------------------

        static CastleMissionProps BuildMissionProps(PropLayout p)
        {
            // The three loots: the slice, the crown and the egg (a thief steals them; they fly home again).
            p.CakeLoot.AddComponent<MissionLoot>();
            p.CrownLoot.AddComponent<MissionLoot>();
            p.EggLoot.AddComponent<MissionLoot>();

            var props = p.MissionSpot.gameObject.AddComponent<CastleMissionProps>();
            SetObject(props, "cake", p.CakeSet);
            SetObject(props, "crown", p.CrownSet);
            SetObject(props, "egg", p.EggSet);
            return props;
        }

        static PasswordGate BuildPasswordGate(PropLayout p, WorldTapPicker picker, CastleQueen queen)
        {
            var gate = p.PasswordGate.gameObject.AddComponent<PasswordGate>();
            SetObjectArray(gate, "stands", p.Stands);
            SetObject(gate, "picker", picker);
            SetObject(gate, "queen", queen);
            return gate;
        }

        static WallPatch BuildWallComponent(PropLayout p, WorldTapPicker picker, Transform peekGoblin)
        {
            var wall = p.WallPatch.gameObject.AddComponent<WallPatch>();
            SetObject(wall, "standPoint", p.StandPoint);
            SetObjectArray(wall, "slots", p.Slots);
            SetObjectArray(wall, "ghosts", p.Ghosts);
            SetObjectArray(wall, "pileBlocks", p.PileBlocks);
            SetObject(wall, "peekGoblin", peekGoblin);
            SetObject(wall, "picker", picker);
            return wall;
        }

        static CookiePlate BuildCookiePlate(PropLayout p, WorldTapPicker picker, CastleInputs inputs)
        {
            var plate = p.CookieTable.gameObject.AddComponent<CookiePlate>();
            SetObjectArray(plate, "cookies", p.Cookies);
            SetObject(plate, "bigCookie", p.BigCookie);
            SetObject(plate, "bowl", p.Bowl);
            SetObject(plate, "picker", picker);
            SetObject(plate, "button", inputs.ActionButton);
            SetObject(plate, "checkIcon", FindCheckIcon());
            return plate;
        }

        static DragonEgg BuildEggComponent(PropLayout p, WorldTapPicker picker, BabyDragon baby)
        {
            var egg = p.EggLoot.AddComponent<DragonEgg>();
            SetObject(egg, "picker", picker);
            SetObject(egg, "baby", baby);
            SetObjectArray(egg, "shellPieces", p.ShellPieces);
            return egg;
        }

        static DragonNaming BuildNaming(Transform parent, LessonUI lessonUI, LetterSet letters, BabyDragon baby)
        {
            var go = new GameObject("DragonNaming");
            go.transform.SetParent(parent, false);
            var naming = go.AddComponent<DragonNaming>();
            SetObject(naming, "lessonUI", lessonUI);
            SetObject(naming, "letters", letters);
            SetObject(naming, "baby", baby);
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(RewardCatalogPath);
            if (catalog == null) Debug.LogWarning("CoolSchool castle: the reward catalog asset is not there yet; the baby dragon naming gets it on the next build.");
            SetObject(naming, "catalog", catalog);
            return naming;
        }

        static DragonRidePath BuildRidePath(PropLayout p)
        {
            var path = p.RideGroup.gameObject.AddComponent<DragonRidePath>();
            SetObjectArray(path, "points", p.RidePoints);

            var stars = new List<RideStar>();
            foreach (GameObject star in p.RideStars)
                stars.Add(star.AddComponent<RideStar>());
            SetObjectArray(path, "stars", stars);
            return path;
        }

        static NameFlag BuildNameFlag(PropLayout p, LessonUI lessonUI, LetterSet letters, CastleQueen queen)
        {
            var flag = p.FlagPole.gameObject.AddComponent<NameFlag>();
            SetObject(flag, "letterRow", p.LetterRow);
            SetObject(flag, "emblem", p.Emblem);
            SetObject(flag, "lessonUI", lessonUI);
            SetObject(flag, "letters", letters);
            SetObject(flag, "queen", queen);
            SetObject(flag, "dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));
            SetObject(flag, "banner", p.Emblem.transform.parent);
            return flag;
        }

        static CastleGrowth BuildGrowth(PropLayout p, CastleQueen queen)
        {
            var growth = p.Growth.gameObject.AddComponent<CastleGrowth>();
            SetObjectArray(growth, "tiers", p.Tiers);
            SetIntArray(growth, "thresholds", new[] { 1, 2, 3, 4, 6, 8, 11 });
            SetObject(growth, "queen", queen);
            return growth;
        }

        static TowerRescue BuildTowerRescue(PropLayout p)
        {
            var rescue = p.TowerRescue.gameObject.AddComponent<TowerRescue>();
            SetObject(rescue, "towerTop", p.TowerTop);
            SetObject(rescue, "rescueRing", p.RescueRing);
            SetObject(rescue, "ladder", p.RescueLadder);
            return rescue;
        }

        /// <summary>CastleBarrel on each barrel root: the goblin hides at "Inside" (a child) and hops out to the floor point beside it.</summary>
        static CastleBarrel[] BuildBarrelComponents(PropLayout p)
        {
            var barrels = new List<CastleBarrel>();
            for (int i = 0; i < p.Barrels.Length; i++)
            {
                var barrel = p.Barrels[i].AddComponent<CastleBarrel>();
                SetObject(barrel, "inside", p.BarrelInside[i]);
                SetObject(barrel, "hopOutPoint", p.BarrelHopOut[i]);
                barrels.Add(barrel);
            }
            return barrels.ToArray();
        }

        /// <summary>
        /// The bouncing gold arrow and glowing ring that show where to go. Built from primitives like the pirates' marker (the arrow's
        /// cone mesh is made by the component), with the glowing materials under Materials/Castle. Switched off, on the Ignore Raycast
        /// layer so the camera ignores it.
        /// </summary>
        static CastleGuideMarker BuildGuideMarker(Transform parent)
        {
            var root = new GameObject("CastleGuideMarker").transform;
            root.SetParent(parent, false);

            Material arrowMaterial = GuideArrowMat;
            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root, false);
            var head = new GameObject("Head");
            head.transform.SetParent(arrow, false);
            var headFilter = head.AddComponent<MeshFilter>();
            var headRenderer = head.AddComponent<MeshRenderer>();
            headRenderer.sharedMaterial = arrowMaterial;
            headRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // The component's cone is 1 m tall (tip at the arrow's origin); a shaft goes on top of it.
            const float coneHeight = 1f;
            const float shaftLength = 0.8f;
            MakePrimitive(PrimitiveType.Cylinder, "Shaft", arrow, new Vector3(0f, coneHeight + shaftLength * 0.5f, 0f),
                new Vector3(0.5f, shaftLength * 0.5f, 0.5f), Quaternion.identity, arrowMaterial);

            // The ring on the ground, about 1.8 m across (the component keeps this size).
            Transform ring = MakePrimitive(PrimitiveType.Cylinder, "Ring", root, Vector3.zero, new Vector3(1.8f, 0.01f, 1.8f), Quaternion.identity, GuideRingMat);

            var marker = root.gameObject.AddComponent<CastleGuideMarker>();
            SetObject(marker, "arrow", arrow);
            SetObject(marker, "arrowHead", headFilter);
            SetObject(marker, "ring", ring);

            SetLayerRecursive(root.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
            root.gameObject.SetActive(false);
            return marker;
        }

        // ----- Icons ------------------------------------------------------------------------------------------

        /// <summary>A green tick for the cookies' "done" button: Checkmark-Success-Transparency, else Checkmark, else a plain round sprite.</summary>
        static Sprite FindCheckIcon()
        {
            Sprite tick = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark-Success-Transparency", "Checkmark" });
            if (tick != null) return tick;

            Debug.LogWarning("CoolSchool castle: no check mark sprite found; the cookies' done button uses a plain round sprite.");
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        // ----- The wiring into the game -------------------------------------------------------------------------

        static void WireGame(KnightsArenaBuilder.ArenaLayout layout, PropLayout p, CastleInputs inputs, KnightsGame game, LessonUI lessonUI,
            LetterSet letters, CastleQueen queen, CastleMissionProps missionProps, CastleGuideMarker guide, PasswordGate password,
            WallPatch wall, CookiePlate cookies, DragonEgg egg, DragonNaming naming, BabyDragon baby, DragonRidePath ridePath,
            NameFlag nameFlag, CastleGrowth growth, TowerRescue towerRescue, CastleBarrel[] barrels)
        {
            // The gate and where he talks at it.
            SetObject(game, "portcullis", layout.Portcullis != null ? layout.Portcullis.GetComponent<CastlePortcullis>() : null);
            SetObject(game, "gateTalkSpot", p.GateTalkSpot);
            SetObject(game, "queenSpot", p.QueenSpot);

            // Counting perches, barrels, the thief's run points and what a thief steals.
            SetObjectArray(game, "wallPerches", p.Perches);
            SetObjectArray(game, "perchLandings", p.PerchLandings);
            SetObjectArray(game, "barrels", barrels);
            SetObjectArray(game, "thiefRunPoints", p.ThiefPoints);
            SetObject(game, "cakeLoot", p.CakeLoot.GetComponent<MissionLoot>());
            SetObject(game, "crownLoot", p.CrownLoot.GetComponent<MissionLoot>());
            SetObject(game, "eggLoot", p.EggLoot.GetComponent<MissionLoot>());

            // The ride and the cards.
            SetObject(game, "rideDismountPoint", p.DismountPoint);
            SetObject(game, "fireIcon", ActivityBeaconBuilder.FindSprite(new[] { "Fire" }) ?? inputs.SwordIcon);
            SetObject(game, "lessonUI", lessonUI);
            SetObject(game, "letters", letters);

            // The moments.
            SetObject(game, "queen", queen);
            SetObject(game, "missionProps", missionProps);
            SetObject(game, "guide", guide);
            SetObject(game, "password", password);
            SetObject(game, "wall", wall);
            SetObject(game, "cookies", cookies);
            SetObject(game, "egg", egg);
            SetObject(game, "naming", naming);
            SetObject(game, "babyDragon", baby);
            SetObject(game, "ridePath", ridePath);
            SetObject(game, "nameFlag", nameFlag);
            SetObject(game, "growth", growth);
            SetObject(game, "towerRescue", towerRescue);
            SetObject(game, "queenCelebrateSpot", p.QueenCelebrateSpot);
            SetObject(game, "babyCelebrateSpot", p.BabyCelebrateSpot);

            EditorUtility.SetDirty(game);
        }

        // ----- The summary line -----------------------------------------------------------------------------------

        static void LogSummary(KnightsArenaBuilder.ArenaLayout layout, PropLayout p, DragonRidePath ridePath, CastleBarrel[] barrels, int markers,
            DragonBoss dragon)
        {
            var game = Object.FindFirstObjectByType<KnightsGame>();
            var zone = Object.FindObjectsByType<CoolSchool.Activities.ActivityZone>(FindObjectsSortMode.None)
                .FirstOrDefault(z => z.Activity == game);
            var opponents = Object.FindObjectsByType<BattleOpponentAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(o => o.name.Replace("Opponent_", "")).OrderBy(n => n);

            Debug.Log("CoolSchool castle adventure: "
                + $"zone {(zone != null ? zone.transform.position.ToString("0.0") : "?")}, talk spot {p.GateTalkSpot.position.ToString("0.0")}, "
                + $"queen home {p.QueenHome.position.ToString("0.0")}, {p.Stands.Length} password stands at z {p.Stands[0].position.z:0.0}, "
                + $"wall hole at {p.HolePosition.ToString("0.0")} (stand point {p.StandPoint.position.ToString("0.0")}), "
                + $"mission spot {p.MissionSpot.GetChild(0).position.ToString("0.0")} (nest {p.NestPosition.ToString("0.0")}), "
                + $"{barrels.Length} barrels at {string.Join(" ", p.Barrels.Select(b => b.transform.position.ToString("0.0")))}, "
                + $"{p.Perches.Length} perches at height {layout.BackWallTopY:0.00}, "
                + $"cookie table {p.CookieTable.GetChild(0).position.ToString("0.0")} and bowl {p.Bowl.position.ToString("0.0")}, "
                + $"tower top {p.TowerTop.position.ToString("0.0")}, flag pole {p.FlagPole.GetChild(0).position.ToString("0.0")}, "
                + $"ride path {p.RidePoints.Length} points / {p.RideLength:0} m with {p.RideStars.Length} stars, "
                + $"{markers} help markers, dragon seat {(dragon != null && dragon.Seat != null ? dragon.Seat.position.ToString("0.00") : "missing")} ({saddleSummary}), "
                + $"tiny goblin {TinyGoblinModel}, opponents: {string.Join(", ", opponents)}.");
        }
    }
}
