using CoolSchool.Zoo;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The ten pens in one row along the park's north fence, in ZooAnimalKind.Order from east to west (the elephant nearest the
    /// entrance): a low fence (the main fence scaled to 0.7 m) on three sides and two short pieces on the south side with a gate
    /// gap between them, a gate on a hinge at its west end, a ground patch for the savanna kinds, and a sign post with a picture quad.
    /// The ZooPen component is added in step 2.
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        const float PenFenceHeight = 0.7f;
        const float PenGateGap = 1.6f;
        const float PenGateHeight = 0.8f;
        const float PenSandHeight = 0.1f;
        // The sign: a wooden post with a light board (primitives, so nothing printed on it), beside the gate on its east side.
        const float SignHeight = 1.1f;
        const float SignBoardWidth = 0.7f;
        const float SignBoardHeight = 0.5f;
        const float SignPostWidth = 0.12f;
        const float SignBoardDepth = 0.05f;
        // The picture sits this far in front of the board's south face.
        const float SignIconGap = 0.01f;

        static readonly PrefabRef GatePrefab = new PrefabRef("SM_Env_Fence_Wood_Gate_01", "PolygonTown");

        // The savanna animals stand on sand, the others on the park's grass.
        static readonly string[] SandKinds = { "elephant", "giraffe", "zebra", "lion" };

        static void BuildPens(ParkLayout p, Transform parent, FenceKit kit)
        {
            Transform group = NewGroup("Pens", parent);
            GameObject gatePrefab = LoadPrefab(GatePrefab);
            Material sand = LitMaterial("ZooPenSand", new Color(0.93f, 0.8f, 0.52f), 0.1f);
            Material postWood = LitMaterial("ZooSignPost", new Color(0.4f, 0.26f, 0.14f), 0.15f);
            Material boardWood = LitMaterial("ZooSignBoard", new Color(0.72f, 0.6f, 0.45f), 0.15f);
            float lowScale = kit.Ok ? PenFenceHeight / kit.Height : 1f;

            for (int i = 0; i < PenCount && i < ZooAnimalKind.Order.Length; i++)
                p.Pens.Add(BuildPen(p, group, kit, lowScale, gatePrefab, sand, postWood, boardWood, i));
        }

        static PenData BuildPen(ParkLayout p, Transform group, FenceKit kit, float lowScale, GameObject gatePrefab, Material sand,
            Material postWood, Material boardWood, int index)
        {
            ZooAnimalKind kind = ZooAnimalKind.Order[index];
            float u0 = PenU0 + PenWidth * index;
            float u1 = u0 + PenWidth;
            float uc = (u0 + u1) * 0.5f;
            float vc = (PenV0 + PenV1) * 0.5f;

            var data = new PenData { KindId = kind.Id };
            var root = new GameObject("Pen_" + kind.Id).transform;
            root.SetParent(group, false);
            root.position = p.At(uc, vc);
            data.Root = root;

            // The low fence: north, west (shared with the next pen's east side), the first pen's east side, and the south pieces.
            if (kit.Ok)
            {
                FenceRun(kit, root, p.At(u0, PenV1), p.At(u1, PenV1), Vector3.back, lowScale, true);
                FenceRun(kit, root, p.At(u1, PenV0), p.At(u1, PenV1), Vector3.right, lowScale, true);
                if (index == 0)
                    FenceRun(kit, root, p.At(u0, PenV0), p.At(u0, PenV1), Vector3.left, lowScale, true);
                FenceRun(kit, root, p.At(u0, PenV0), p.At(uc - PenGateGap * 0.5f, PenV0), Vector3.forward, lowScale, true);
                FenceRun(kit, root, p.At(uc + PenGateGap * 0.5f, PenV0), p.At(u1, PenV0), Vector3.forward, lowScale, true);
            }

            // The ground patch.
            float standY = 0f;
            if (System.Array.IndexOf(SandKinds, kind.Id) >= 0)
            {
                Primitive(PrimitiveType.Cube, "Sand", root, new Vector3(0f, PenSandHeight * 0.5f, 0f),
                    new Vector3(PenWidth - 0.2f, PenSandHeight, PenV1 - PenV0 - 0.2f), Quaternion.identity, sand);
                standY = PenSandHeight;
            }

            data.Gate = BuildGate(p, root, gatePrefab, uc);

            data.Stand = NewPoint("StandPoint", root, p.At(uc, vc, standY));
            data.Stand.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            data.GatePoint = NewPoint("GatePoint", root, p.At(uc, PenV0 - 1f));
            data.GatePoint.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            data.Sign = BuildSign(p, root, postWood, boardWood, uc);
            return data;
        }

        /// <summary>
        /// The gate on a hinge object at the west end of the gap (the hinge has no rotation: closed, the gate runs from it eastward
        /// along the south fence; ZooPen swings the hinge around its up axis, a positive angle swings the gate out to the south).
        /// </summary>
        static Transform BuildGate(ParkLayout p, Transform pen, GameObject gatePrefab, float uc)
        {
            Vector3 hingePosition = p.At(uc + PenGateGap * 0.5f, PenV0);
            Transform hinge = NewPoint("Gate", pen, hingePosition);
            if (gatePrefab == null) return hinge;

            GameObject gate = Spawn(gatePrefab, hinge);
            gate.name = "GateModel";
            if (TryGetBounds(gate, out Bounds bounds))
            {
                // Its long side must run east-west, closed.
                bool turned = bounds.size.z > bounds.size.x * 1.05f;
                if (turned) gate.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                TryGetBounds(gate, out bounds);

                float across = (PenGateGap - 0.04f) / Mathf.Max(0.05f, bounds.size.x);
                float up = PenGateHeight / Mathf.Max(0.05f, bounds.size.y);
                Vector3 scale = gate.transform.localScale;
                gate.transform.localScale = turned ? new Vector3(scale.x, scale.y * up, scale.z * across) : new Vector3(scale.x * across, scale.y * up, scale.z);
            }
            AlignToBounds(gate, hingePosition.x + PenGateGap * 0.5f, hingePosition.z, 0f, false);
            AddMeshCollidersIfNone(gate);
            return hinge;
        }

        /// <summary>
        /// A sign beside the gate, on its east side, 1.1 m tall so it does not block the view into the pen: a thin brown post and a light
        /// wooden board (0.7 x 0.5 m) facing south, with an empty sprite quad on the board's south face (the kind's picture goes there).
        /// Nothing is printed on it: it is made from primitives.
        /// </summary>
        static SpriteRenderer BuildSign(ParkLayout p, Transform pen, Material postWood, Material boardWood, float uc)
        {
            Vector3 foot = p.At(uc - 1.4f, PenV0 - 0.6f);
            Transform holder = NewPoint("Sign", pen, foot);

            // A cube primitive is 1 m on each side: the scale is the size.
            Transform post = Primitive(PrimitiveType.Cube, "SignPost", holder, new Vector3(0f, SignHeight * 0.5f, 0f),
                new Vector3(SignPostWidth, SignHeight, SignPostWidth), Quaternion.identity, postWood, true);
            Transform board = Primitive(PrimitiveType.Cube, "SignBoard", holder, new Vector3(0f, SignHeight - SignBoardHeight * 0.5f, -SignBoardDepth),
                new Vector3(SignBoardWidth, SignBoardHeight, SignBoardDepth), Quaternion.identity, boardWood);
            foreach (Transform part in new[] { post, board })
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, GameObjectUtility.GetStaticEditorFlags(part.gameObject) | StaticEditorFlags.BatchingStatic);

            var icon = new GameObject("SignIcon");
            icon.transform.SetParent(holder, false);
            // The sprite quad faces south (-Z, the camera) with no rotation; the board's south face is half its depth in front of its centre.
            icon.transform.localPosition = new Vector3(0f, SignHeight - SignBoardHeight * 0.5f, -SignBoardDepth - SignBoardDepth * 0.5f - SignIconGap);
            icon.transform.localRotation = Quaternion.identity;
            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 5;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
