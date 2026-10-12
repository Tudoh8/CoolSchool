using System;
using System.Collections.Generic;
using System.IO;
using CoolSchool.Characters;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The costume shots of PreviewCapture, to check that AttachmentFit puts every attachment where it belongs on the kid:
    /// the superhero cape and mask (from behind, the side and the front), the pirate bandana / captain's hat and eyepatch, and the
    /// head pieces one by one (cap, knight's helmet, pirate hat, the three cardboard race helmets) from the front and the side.
    /// The player is dressed in edit mode in the idle pose, without weapons, so the costume is easy to see.
    /// </summary>
    public static partial class PreviewCapture
    {
        /// <summary>The head pieces to check: a short shot name and the prefab (found by name in the PolygonKids pack).</summary>
        static readonly (string name, string prefab)[] HatPieces =
        {
            ("cap", "SM_Chr_Attach_Hat_Cap_Back_01"),
            ("knight_helmet", "SM_Chr_Attach_Cardboard_KnightHelmet_01"),
            ("pirate_hat", "SM_Chr_Attach_Hat_Pirate_01"),
            ("race_helmet_01", "SM_Chr_Attach_Helmet_Cardboard_01"),
            ("race_helmet_02", "SM_Chr_Attach_Helmet_Cardboard_02"),
            ("race_helmet_robot", "SM_Chr_Attach_Helmet_Cardboard_Robot_01"),
        };

        static void AddCostumeGroups(List<Group> groups)
        {
            var cape = new Group { Name = "costume_cape", Stage = StageSuperhero };
            cape.Shots.Add(new Shot { Name = "back", Frame = () => CostumeView(180f, 0.62f, 2.8f) });
            cape.Shots.Add(new Shot { Name = "side", Frame = () => CostumeView(90f, 0.62f, 2.8f) });
            cape.Shots.Add(new Shot { Name = "front", Frame = () => CostumeView(0f, 0.62f, 2.8f) });
            cape.Shots.Add(new Shot { Name = "mask", Frame = () => CostumeView(25f, 0.9f, 1.2f) });
            groups.Add(cape);

            var pirate = new Group { Name = "costume_pirate", Stage = StagePirate };
            pirate.Shots.Add(new Shot { Name = "bandana_front", Frame = () => PirateHeadView("bandana", 20f) });
            pirate.Shots.Add(new Shot { Name = "bandana_side", Frame = () => PirateHeadView("bandana", 90f) });
            pirate.Shots.Add(new Shot { Name = "captain_front", Frame = () => PirateHeadView("captain", 20f) });
            pirate.Shots.Add(new Shot { Name = "captain_side", Frame = () => PirateHeadView("captain", 90f) });
            groups.Add(pirate);

            // One staging for all the hats: each shot swaps the head piece, then frames the head.
            var hats = new Group { Name = "costume_hats", Stage = StageHats };
            foreach ((string name, string prefab) in HatPieces)
            {
                string pieceName = name;
                string prefabName = prefab;
                hats.Shots.Add(new Shot { Name = pieceName + "_front", Frame = () => { WearHeadPiece(prefabName); CostumeView(25f, 0.9f, 1.2f); } });
                hats.Shots.Add(new Shot { Name = pieceName + "_side", Frame = () => { WearHeadPiece(prefabName); CostumeView(90f, 0.9f, 1.2f); } });
            }
            hats.Shots.Add(new Shot { Name = "race_helmet_player", Frame = () => { WearPlayerRaceHelmet(); CostumeView(25f, 0.9f, 1.2f); } });
            groups.Add(hats);
        }

        // ----- Staging --------------------------------------------------------------------------

        /// <summary>Switches the player on and lets go of the movers, but puts no weapons on.</summary>
        static GameObject StageCostumeKid()
        {
            HideZoneVisuals();
            GameObject player = FindKid("Player");
            if (player == null) throw new InvalidOperationException("player not found");
            ActivateWithParents(player);
            DisableMovers(player);
            Physics.SyncTransforms();
            return player;
        }

        /// <summary>The costume goes on in the rest pose; then the kid idles, so the pieces are seen on a living body.</summary>
        static void SettleCostumeKid(GameObject player)
        {
            ResetAnimators(player);
            StepAnimators(player, 0.8f);
        }

        static void StageSuperhero()
        {
            GameObject player = StageCostumeKid();
            var costume = player.GetComponent<CostumeHolder>();
            if (costume == null) throw new InvalidOperationException("the player has no CostumeHolder");
            costume.ShowCostume(true);
            SettleCostumeKid(player);
        }

        static void StagePirate()
        {
            GameObject player = StageCostumeKid();
            var costume = player.GetComponent<PirateCostume>();
            if (costume == null) throw new InvalidOperationException("the player has no PirateCostume");
            costume.Show(true);
            SettleCostumeKid(player);
        }

        static void StageHats()
        {
            GameObject player = StageCostumeKid();
            if (player.GetComponent<HatHolder>() == null) throw new InvalidOperationException("the player has no HatHolder");
            SettleCostumeKid(player);
        }

        // ----- Switching pieces and cameras -----------------------------------------------------

        /// <summary>Shows the bandana or the captain's hat (whichever is asked for), whatever the saved progress says.</summary>
        static void PirateHeadView(string piece, float azimuth)
        {
            GameObject player = FindKid("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            foreach (Transform child in head)
            {
                if (child.name.Contains("Bandana")) child.gameObject.SetActive(piece == "bandana");
                else if (child.name.Contains("Hat_Pirate")) child.gameObject.SetActive(piece == "captain");
            }
            CostumeView(azimuth, 0.9f, 1.2f);
        }

        /// <summary>Puts one head piece on the player through the HatHolder, and takes the race helmet off.</summary>
        static void WearHeadPiece(string prefabName)
        {
            GameObject player = FindKid("Player");
            var helmet = player.GetComponent<RaceHelmet>();
            if (helmet != null) helmet.Show(false);

            GameObject prefab = LoadKidsAttachment(prefabName);
            if (prefab == null) throw new InvalidOperationException("prefab not found: " + prefabName);
            player.GetComponent<HatHolder>().SetHat(prefab, false);
        }

        static void WearPlayerRaceHelmet()
        {
            GameObject player = FindKid("Player");
            player.GetComponent<HatHolder>().SetHat(null, false);
            var helmet = player.GetComponent<RaceHelmet>();
            if (helmet == null) throw new InvalidOperationException("the player has no RaceHelmet");
            helmet.Show(true);
        }

        /// <summary>
        /// The camera around the player: a whole-body view (height is metres above the feet), or, above 0.8 m, a head view that
        /// follows the head bone whatever the kid's height.
        /// </summary>
        static void CostumeView(float azimuth, float height, float distance)
        {
            GameObject player = FindKid("Player");
            Vector3 centre = player.transform.position + Vector3.up * height;
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (height > 0.8f && head != null) centre = head.position + Vector3.up * 0.1f;
            Orbit(centre, player.transform, azimuth, distance, 8f, 30f);
        }

        static GameObject LoadKidsAttachment(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { "Assets/Imported/Synty/PolygonKids" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == prefabName) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }
    }
}
