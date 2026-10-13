using System.Collections.Generic;
using CoolSchool.Race;
using CoolSchool.Rewards;
using CoolSchool.ToyRoom;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The pit lane (contract Docs/specs/cart-garage-contract.md, part G2): a row of painted parking bays on the east sidewalk beside
    /// the race start, one bay for each cart body, each with a ToyShelfSlot where the PitLane component shows the body as a model (owned)
    /// or as a black silhouette (not won yet). The bodies park across the sidewalk with their noses toward the road, so the camera sees
    /// each one from the side.
    /// Why here: the road and the lanes belong to the race, the west sidewalk has the announcer's stand (z 33.2) and the first street lamp
    /// (z 34.9), and the east side has nothing before the east houses (which start behind the fence at z 48 and later). The row runs from
    /// z 22.9 to about z 43.5 along x = EastSidewalkX (-10.9), leaving a gap for the start banner's post at z 30. It is 5 m east of the
    /// race zone (x -16, z 26) and of the straight walk in from the entrance gap (x -18..-14), and east of the start line's lanes
    /// (x -18.2..-13.8), so it blocks none of them; nor the camera (the cars are low and stand beside the picture).
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        const float PitBayPitch = 1.85f;
        const float PitBayWidth = 1.65f;
        const float PitBayDepth = 1.9f;
        const float PitFirstBayZ = 22.9f;
        /// <summary>How big the biggest side of a parked body is.</summary>
        const float PitDisplaySize = 1.5f;
        /// <summary>The start banner's post on the east sidewalk: a bay keeps this far (centre to centre, minus half a bay) away from it.</summary>
        const float PitPostClearance = 0.5f;

        /// <summary>The z of each bay's middle, in order, skipping the start banner's post.</summary>
        static List<float> PitBayCentres(int count)
        {
            var centres = new List<float>();
            float z = PitFirstBayZ;
            float clear = PitBayPitch * 0.5f + PitPostClearance;
            for (int i = 0; i < count; i++)
            {
                // A bay that would stand on the post moves to just past it. No second check: float rounding could make
                // StartLineZ + clear test as "still too close" and loop forever.
                if (Mathf.Abs(z - StartLineZ) < clear) z = StartLineZ + clear;
                centres.Add(z);
                z += PitBayPitch;
            }
            return centres;
        }

        /// <summary>
        /// Builds the bays and the PitLane component (one slot per entry of <see cref="BodyDefs"/>, in catalog order). Call after the
        /// carts exist (their reveal prefabs are what the slots show) and the reward catalog asset exists.
        /// </summary>
        static void BuildPitLane(Transform parent)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(RewardCatalogPath);
            if (catalog == null) catalog = RewardCatalogSeeder.CreateOrUpdate();

            Transform group = NewGroup("PitLane", parent);
            float x = EastSidewalkX;
            List<float> centres = PitBayCentres(BodyDefs.Length);

            Material pad = Mat("PitLanePad", new Color(0.55f, 0.76f, 0.95f), 0.15f);
            Material line = Mat("PitLaneLine", new Color(0.97f, 0.97f, 0.95f), 0.15f);
            Material silhouette = PitLaneSilhouette();

            var slots = new List<ToyShelfSlot>();
            for (int i = 0; i < BodyDefs.Length; i++)
            {
                float z = centres[i];
                string bayName = "Bay_" + BodyDefs[i].Id.Replace("cart.", "");
                Transform bay = NewGroup(bayName, group);

                // The painted bay: a light blue pad with a white line on its three sides (the open side is toward the road).
                const float lineThickness = 0.07f;
                Prim(PrimitiveType.Cube, "Pad", bay, new Vector3(x, SidewalkY + 0.006f, z), new Vector3(PitBayDepth, 0.012f, PitBayWidth), Quaternion.identity, pad);
                Prim(PrimitiveType.Cube, "LineBack", bay, new Vector3(x + PitBayDepth * 0.5f - lineThickness * 0.5f, SidewalkY + 0.01f, z),
                    new Vector3(lineThickness, 0.02f, PitBayWidth), Quaternion.identity, line);
                foreach (float side in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "LineSide", bay, new Vector3(x, SidewalkY + 0.01f, z + side * (PitBayWidth * 0.5f - lineThickness * 0.5f)),
                        new Vector3(PitBayDepth, 0.02f, lineThickness), Quaternion.identity, line);
                MarkStatic(bay.gameObject);

                // The slot: nose toward the road (west), standing on the pad.
                var slotObject = new GameObject("Slot_" + BodyDefs[i].Id);
                slotObject.transform.SetParent(group, false);
                slotObject.transform.position = new Vector3(x, SidewalkY + 0.012f, z);
                slotObject.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                var slot = slotObject.AddComponent<ToyShelfSlot>();
                SetString(slot, "itemId", BodyDefs[i].Id);
                SetFloat(slot, "displaySize", PitDisplaySize);
                SetObject(slot, "silhouetteMaterial", silhouette);
                slots.Add(slot);
            }

            float firstZ = centres[0] - PitBayWidth * 0.5f;
            float lastZ = centres[centres.Count - 1] + PitBayWidth * 0.5f;
            var pit = group.gameObject.AddComponent<PitLane>();
            SetObject(pit, "catalog", catalog);
            SetObjectArray(pit, "slots", slots);
            SetBounds(pit, "area", new Bounds(new Vector3(x, 1f, (firstZ + lastZ) * 0.5f), new Vector3(PitBayDepth + 1.4f, 4f, lastZ - firstZ + 1.4f)));
            SetFloat(pit, "talkRadius", 1.5f);

            Debug.Log($"CoolSchool cart bodies: the pit lane has {slots.Count} bays on the east sidewalk, x {x:0.0}, z {firstZ:0.0}..{lastZ:0.0} (the banner post at z {StartLineZ:0.0} is skipped); "
                + $"cars are shown {PitDisplaySize:0.0} m long with their noses toward the road.");
        }

        /// <summary>Black and unlit, put on every renderer slot of a body he has not won yet. Its own material, so the Toy Room's builder cannot replace it.</summary>
        static Material PitLaneSilhouette()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("CoolSchool pit lane: URP/Unlit shader not found; locked bodies will use the slot's fallback material.");
                return null;
            }

            var material = new Material(unlit) { name = "PitLaneSilhouette" };
            material.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.06f, 1f));
            return SaveMaterial(material, "PitLaneSilhouette");
        }
    }
}
