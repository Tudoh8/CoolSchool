using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The castle adventure's materials (saved under Assets/CoolSchool/Materials/Castle, re-made on each build) and its generated
    /// meshes (the jagged hole, the gold star, the arrow cone). A material is made once per build and shared.
    /// </summary>
    internal static partial class CastleAdventureBuilder
    {
        static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        static void ClearMaterialCache() => MaterialCache.Clear();

        /// <summary>A flat URP/Lit material under Materials/Castle. `tweak` runs before the asset is saved.</summary>
        static Material Mat(string materialName, Color colour, float smoothness = 0.2f, float metallic = 0f, float emission = 0f,
            System.Action<Material> tweak = null)
        {
            if (MaterialCache.TryGetValue(materialName, out Material cached) && cached != null) return cached;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Castle");

            var material = new Material(lit) { name = materialName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(colour.r, colour.g, colour.b, 1f) * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            tweak?.Invoke(material);

            string path = MaterialFolder + "/" + materialName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            Material saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            MaterialCache[materialName] = saved;
            return saved;
        }

        /// <summary>A transparent glowing disc material (rings on the ground).</summary>
        static Material GlowMat(string materialName, Color colour, float alpha)
        {
            return Mat(materialName, new Color(colour.r, colour.g, colour.b, alpha), 0f, 0f, 1.5f, BattleEffectsBuilder.SetAlphaBlended);
        }

        // ----- The materials, by purpose ------------------------------------------------------------

        // Dark and matt, so the hole reads as a hole in the wall (no shine, no reflections).
        static Material HoleMat => Mat("CastleHole", new Color(0.07f, 0.045f, 0.03f), 0f, 0f, 0f, m =>
        {
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        });
        static Material GhostBlockMat => Mat("CastleGhostBlock", new Color(1f, 1f, 1f, 0.25f), 0.1f, 0f, 0.35f, BattleEffectsBuilder.SetAlphaBlended);
        static Material CookieMat => Mat("CastleCookie", new Color(0.74f, 0.47f, 0.22f), 0.15f);
        static Material ChipMat => Mat("CastleChip", new Color(0.27f, 0.14f, 0.07f), 0.15f);
        static Material PlateMat => Mat("CastlePlate", new Color(0.96f, 0.94f, 0.88f), 0.5f);
        static Material EggMat => Mat("CastleEgg", new Color(1f, 0.95f, 0.8f), 0.45f);
        static Material EggSpotMat(int index)
        {
            Color[] colours =
            {
                new Color(0.45f, 0.8f, 0.55f), new Color(0.45f, 0.65f, 0.95f), new Color(0.95f, 0.6f, 0.7f),
            };
            return Mat("CastleEggSpot_" + index, colours[index % colours.Length], 0.4f);
        }
        static Material NestMat => Mat("CastleNest", new Color(0.5f, 0.33f, 0.16f), 0.05f);
        static Material CushionMat => Mat("CastleCushion", new Color(0.8f, 0.12f, 0.15f), 0.2f);
        static Material BannerMat => Mat("CastleBanner", new Color(0.2f, 0.36f, 0.85f), 0.1f);
        static Material BannerRedMat => Mat("CastleBannerRed", new Color(0.85f, 0.18f, 0.2f), 0.1f);
        static Material BannerYellowMat => Mat("CastleBannerYellow", new Color(0.96f, 0.78f, 0.2f), 0.1f);
        static Material GoldMat => Mat("CastleGold", new Color(1f, 0.78f, 0.2f), 0.6f, 0.4f, 0.5f);
        static Material StarMat => Mat("CastleStar", new Color(1f, 0.82f, 0.15f), 0.4f, 0f, 1.6f);
        static Material DizzyMat => Mat("CastleDizzy", new Color(1f, 0.92f, 0.2f), 0.3f, 0f, 1.6f);
        static Material HelpMat => Mat("CastleHelp", new Color(0.2f, 0.5f, 1f), 0.3f, 0f, 1.6f);
        static Material SaddleMat => Mat("CastleSaddle", new Color(0.75f, 0.1f, 0.1f), 0.3f);
        static Material PoleMat => Mat("CastlePole", new Color(0.9f, 0.88f, 0.82f), 0.3f);
        static Material CardboardMat => Mat("CastleCardboard", new Color(0.72f, 0.55f, 0.35f), 0.05f);
        static Material ShellMat => Mat("CastleShell", new Color(1f, 0.96f, 0.84f), 0.4f);
        static Material GuideArrowMat => Mat("CastleGuideArrow", new Color(1f, 0.8f, 0.1f), 0.3f, 0f, 1.5f);
        static Material GuideRingMat => GlowMat("CastleGuideRing", new Color(1f, 0.85f, 0.2f), 0.55f);
        static Material RescueRingMat => GlowMat("CastleRescueRing", new Color(0.35f, 0.9f, 0.5f), 0.5f);

        /// <summary>The glowing yellow of the dizzy stars (also used by the goblins KnightsArenaBuilder makes).</summary>
        internal static Material DizzyStarMaterial() => DizzyMat;

        // ----- Generated meshes ---------------------------------------------------------------------

        /// <summary>
        /// The hole in the wall: a jagged dark shape about 1.0 m wide and 1.6 m high with its flat bottom at y 0, facing -Z (south,
        /// toward the camera). Fanned from a middle point; the corners are listed clockwise as seen from the camera.
        /// </summary>
        static Mesh CreateHoleMesh()
        {
            var outline = new[]
            {
                new Vector2(-0.50f, 0.00f), new Vector2(-0.45f, 0.42f), new Vector2(-0.54f, 0.80f), new Vector2(-0.38f, 1.14f),
                new Vector2(-0.28f, 1.40f), new Vector2(-0.10f, 1.30f), new Vector2(0.04f, 1.60f), new Vector2(0.22f, 1.36f),
                new Vector2(0.38f, 1.22f), new Vector2(0.32f, 0.92f), new Vector2(0.52f, 0.62f), new Vector2(0.44f, 0.26f),
                new Vector2(0.50f, 0.00f),
            };

            var vertices = new List<Vector3> { new Vector3(0f, 0.7f, 0f) };
            foreach (Vector2 corner in outline) vertices.Add(new Vector3(corner.x, corner.y, 0f));

            var triangles = new List<int>();
            for (int i = 1; i <= outline.Length; i++)
            {
                int next = i % outline.Length + 1;
                AddTriangle(triangles, vertices, 0, i, next, Vector3.back);
            }

            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;

            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray(), normals = normals };
            mesh.RecalculateBounds();
            return SaveMesh(mesh, "CastleHole");
        }

        /// <summary>
        /// A chunky five-pointed star standing in the XY plane (facing -Z), about 0.5 m across at scale 1 and 0.1 m thick. Flat shaded.
        /// </summary>
        static Mesh CreateStarMesh()
        {
            const int points = 5;
            const float outer = 0.25f;
            const float inner = 0.11f;
            const float half = 0.05f;

            var ring = new Vector2[points * 2];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? outer : inner;
                ring[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                for (int k = 0; k < 3; k++) normals.Add(facing);
                AddTriangle(triangles, vertices, start, start + 1, start + 2, facing);
            }

            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 a = ring[i];
                Vector2 b = ring[(i + 1) % ring.Length];

                // Front (-Z) and back (+Z) of the wedge between the middle and this edge.
                Face(new Vector3(0f, 0f, -half), new Vector3(a.x, a.y, -half), new Vector3(b.x, b.y, -half), Vector3.back);
                Face(new Vector3(0f, 0f, half), new Vector3(a.x, a.y, half), new Vector3(b.x, b.y, half), Vector3.forward);

                // The edge itself, facing out (the star's corners go round counter-clockwise, so out is to the right of the edge).
                Vector2 edge = (b - a).normalized;
                Vector3 outward = new Vector3(edge.y, -edge.x, 0f);
                Face(new Vector3(a.x, a.y, -half), new Vector3(b.x, b.y, -half), new Vector3(b.x, b.y, half), outward);
                Face(new Vector3(a.x, a.y, -half), new Vector3(b.x, b.y, half), new Vector3(a.x, a.y, half), outward);
            }

            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray(), normals = normals.ToArray() };
            mesh.RecalculateBounds();
            return SaveMesh(mesh, "CastleStar");
        }
    }
}
