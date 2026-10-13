using System.Collections.Generic;
using CoolSchool.Race;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The reveal prefabs of the cart garage items (CS_Cart_PaintRed, CS_Cart_Eyes, CS_Cart_TrailRainbow, CS_Cart_HornCow...). They are what
    /// spins in the present popup and what RewardIconRenderer draws for the picker's cards: a paint pot in the colour, a cart with the
    /// add-on on it (like CS_Cart_Stickers), a rainbow arc, a row of stars, bubbles or glitter for the trails, and a bell, a magic horn
    /// or the animal for the horns. Everything is made from primitives and a few small generated meshes (saved under Meshes/), with
    /// materials named Garage... so no other builder shares them. The catalog seeder finds the prefabs by path, so this runs before it.
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        static readonly PrefabRef CowPrefab = new PrefabRef("Cow_White", "polyperfect");
        static readonly PrefabRef ElephantPrefab = new PrefabRef("Elephant_Female", "polyperfect");

        /// <summary>Makes (or remakes) the reveal prefab of every garage item. Call after the carts are built and before the catalog is seeded.</summary>
        static void CreateGaragePrefabs()
        {
            EnsureFolder(RaceFolder, "Prefabs");
            EnsureFolder(RaceFolder, "Garage");

            int made = 0;
            foreach (CartGarageData.Item item in CartGarageData.All)
            {
                GameObject token = BuildToken(item);
                if (token == null) continue;

                string path = RaceFolder + "/Prefabs/" + item.PrefabName + ".prefab";
                AssetDatabase.DeleteAsset(path);
                PrefabUtility.SaveAsPrefabAsset(token, path);
                Object.DestroyImmediate(token);
                made++;
            }
            Debug.Log($"CoolSchool cart garage: made {made} of {CartGarageData.All.Length} reveal prefabs under {RaceFolder}/Prefabs.");
        }

        static GameObject BuildToken(CartGarageData.Item item)
        {
            switch (item.Part)
            {
                case CartPart.Paint: return BuildPaintPot(item);
                case CartPart.Addon: return BuildAddonCart(item);
                case CartPart.Trail: return BuildTrailToken(item);
                case CartPart.Horn: return BuildHornToken(item);
                default: return null;
            }
        }

        // ----- Paint pot ----------------------------------------------------------------------------

        /// <summary>A tin of paint in the paint's colour with a dripping rim, a puddle on the floor and a brush resting in it.</summary>
        static GameObject BuildPaintPot(CartGarageData.Item item)
        {
            var root = new GameObject(item.PrefabName);
            string key = item.Id.Substring(item.Id.LastIndexOf('.') + 1);
            Material tin = GMat("PotTin", new Color(0.8f, 0.82f, 0.86f), 0.65f, 0.4f);
            Material wood = GMat("PotBrushHandle", new Color(0.72f, 0.5f, 0.28f), 0.3f);
            Material paint = GMat("PotPaint_" + key, item.Colour, 0.85f);

            Prim(PrimitiveType.Cylinder, "Tin", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.56f, 0.2f, 0.56f), Quaternion.identity, tin);
            Prim(PrimitiveType.Cylinder, "Label", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.575f, 0.09f, 0.575f), Quaternion.identity, paint);
            Prim(PrimitiveType.Cylinder, "Rim", root.transform, new Vector3(0f, 0.405f, 0f), new Vector3(0.62f, 0.012f, 0.62f), Quaternion.identity, tin);
            Prim(PrimitiveType.Cylinder, "Surface", root.transform, new Vector3(0f, 0.41f, 0f), new Vector3(0.5f, 0.008f, 0.5f), Quaternion.identity, paint);
            Prim(PrimitiveType.Sphere, "Blob", root.transform, new Vector3(0f, 0.43f, 0f), new Vector3(0.34f, 0.1f, 0.34f), Quaternion.identity, paint);
            Prim(PrimitiveType.Sphere, "Drip", root.transform, new Vector3(0f, 0.34f, 0.285f), new Vector3(0.08f, 0.16f, 0.06f), Quaternion.identity, paint);
            Prim(PrimitiveType.Sphere, "Puddle", root.transform, new Vector3(0.12f, 0.01f, 0.45f), new Vector3(0.46f, 0.03f, 0.3f), Quaternion.identity, paint);

            // The brush leans on the rim with its tip in the paint.
            Quaternion lean = Quaternion.Euler(0f, 0f, -35f);
            Vector3 along = lean * Vector3.up;
            Vector3 handleCentre = new Vector3(0.3f, 0.6f, 0f);
            Prim(PrimitiveType.Cylinder, "BrushHandle", root.transform, handleCentre, new Vector3(0.045f, 0.27f, 0.045f), lean, wood);
            Prim(PrimitiveType.Cube, "BrushTip", root.transform, handleCentre - along * 0.3f, new Vector3(0.09f, 0.13f, 0.035f), lean, paint);
            Prim(PrimitiveType.Cylinder, "BrushBand", root.transform, handleCentre - along * 0.22f, new Vector3(0.055f, 0.03f, 0.055f), lean, tin);
            return root;
        }

        // ----- Carts with an add-on -----------------------------------------------------------------

        /// <summary>Racer 1 with the add-on on it (like CS_Cart_Stickers), no kid. The add-on sits on the body's real anchor point.</summary>
        static GameObject BuildAddonCart(CartGarageData.Item item)
        {
            GameObject bodyPrefab = LoadPrefab(BodyDefs[0].Prefab);
            if (bodyPrefab == null)
            {
                Debug.LogWarning($"CoolSchool cart garage: racer 01 is missing, so {item.PrefabName} was not made.");
                return null;
            }

            var root = new GameObject(item.PrefabName);
            root.transform.position = Vector3.zero;
            GameObject model = PlaceBody(bodyPrefab, root.transform, Vector3.zero, "reveal cart", out Bounds bounds, out Vector3 seatWorld);
            model.name = "Racer_01";
            BodyFit fit = FitBody(BodyDefs[0].Id, model, bounds, seatWorld, Vector3.zero);

            Transform addon;
            CartAnchor anchor;
            switch (item.Id)
            {
                case CartGarageData.Eyes: addon = BuildEyes(root.transform); anchor = CartAnchor.Front; break;
                case CartGarageData.Balloons: addon = BuildBalloons(root.transform); anchor = CartAnchor.Back; break;
                case CartGarageData.Propeller: addon = BuildPropeller(root.transform); anchor = CartAnchor.Top; break;
                default: addon = BuildSpoiler(root.transform); anchor = CartAnchor.Spoiler; break;
            }
            addon.localPosition = fit.Anchors[(int)anchor];
            addon.localScale = Vector3.one * fit.AddonScale;
            return root;
        }

        // ----- Trails -------------------------------------------------------------------------------

        static GameObject BuildTrailToken(CartGarageData.Item item)
        {
            var root = new GameObject(item.PrefabName);
            switch (item.Id)
            {
                case CartGarageData.TrailRainbow: BuildRainbowToken(root.transform); break;
                case CartGarageData.TrailStars: BuildStarsToken(root.transform, 5, 0.45f, new[] { Gold, Pink, Sky }, "Stars"); break;
                case CartGarageData.TrailBubbles: BuildBubblesToken(root.transform); break;
                default: BuildStarsToken(root.transform, 4, 0.2f, new[] { Gold, Color.white, Pink, Sky, Gold }, "Sparkles"); break;
            }
            return root;
        }

        static readonly Color Gold = new Color(1f, 0.82f, 0.2f);
        static readonly Color Pink = new Color(1f, 0.5f, 0.78f);
        static readonly Color Sky = new Color(0.45f, 0.82f, 1f);

        /// <summary>A rainbow standing on two clouds.</summary>
        static void BuildRainbowToken(Transform root)
        {
            Color[] rainbow =
            {
                new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.2f), new Color(0.3f, 0.8f, 0.3f),
                new Color(0.25f, 0.55f, 1f), new Color(0.4f, 0.3f, 0.85f), new Color(0.7f, 0.35f, 0.9f),
            };
            const float inner = 0.26f;
            const float width = 0.05f;
            for (int k = 0; k < rainbow.Length; k++)
            {
                // k = 0 is the red outside band.
                float r1 = inner + (rainbow.Length - k) * width;
                MeshPiece("Band_" + (k + 1), root, ArcBand("GarageRainbowBand" + k, r1 - width, r1, 0.08f, 28), Vector3.zero,
                    GMat("RainbowBand" + k, rainbow[k], 0.45f, 0f, true));
            }

            Material cloud = GMat("CloudWhite", new Color(0.98f, 0.98f, 1f), 0.3f);
            float footX = inner + rainbow.Length * width * 0.5f + 0.02f;
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Cloud", root, new Vector3(side * footX, 0.06f, 0.02f), new Vector3(0.24f, 0.18f, 0.18f), Quaternion.identity, cloud);
                Prim(PrimitiveType.Sphere, "Cloud", root, new Vector3(side * (footX - 0.1f), 0.1f, 0.04f), new Vector3(0.18f, 0.16f, 0.16f), Quaternion.identity, cloud);
                Prim(PrimitiveType.Sphere, "Cloud", root, new Vector3(side * (footX + 0.1f), 0.05f, 0.03f), new Vector3(0.16f, 0.13f, 0.14f), Quaternion.identity, cloud);
            }
        }

        /// <summary>Three flat stars (5 points) or sparkles (4 thin points) at different heights, facing forward.</summary>
        static void BuildStarsToken(Transform root, int points, float innerFraction, Color[] colours, string label)
        {
            Mesh star = ExtrudeOutline("GarageStar" + points, StarOutline(points, 0.5f, 0.5f * innerFraction), 0.07f);
            Vector3[] positions = { new Vector3(-0.3f, 0.2f, 0f), new Vector3(0.02f, 0.46f, 0.02f), new Vector3(0.32f, 0.16f, 0f), new Vector3(0.12f, 0.1f, 0.04f), new Vector3(-0.1f, 0.06f, 0.03f) };
            float[] sizes = { 0.5f, 0.62f, 0.46f, 0.3f, 0.26f };
            float[] turns = { 14f, -8f, 20f, 0f, 30f };
            int count = points == 5 ? 3 : 5;
            for (int i = 0; i < count; i++)
            {
                Color colour = colours[i % colours.Length];
                Transform piece = MeshPiece(label + "_" + (i + 1), root, star, positions[i], GMat(label + i, colour, 0.5f, 0f, true));
                piece.localScale = Vector3.one * sizes[i];
                piece.localRotation = Quaternion.Euler(0f, 0f, turns[i]);
            }
        }

        /// <summary>Three see-through soap bubbles with a white shine.</summary>
        static void BuildBubblesToken(Transform root)
        {
            Material skin = GarageGlass("BubbleSkin", new Color(0.7f, 0.9f, 1f, 0.5f));
            Material shine = GMat("BubbleShine", Color.white, 0.9f);
            Vector3[] positions = { new Vector3(-0.2f, 0.28f, 0f), new Vector3(0.18f, 0.5f, 0.05f), new Vector3(0.24f, 0.2f, 0.1f) };
            float[] sizes = { 0.5f, 0.38f, 0.28f };
            for (int i = 0; i < positions.Length; i++)
            {
                Prim(PrimitiveType.Sphere, "Bubble", root, positions[i], Vector3.one * sizes[i], Quaternion.identity, skin);
                Prim(PrimitiveType.Sphere, "Shine", root, positions[i] + new Vector3(-0.2f, 0.22f, 0.4f) * sizes[i], new Vector3(0.22f, 0.12f, 0.06f) * sizes[i] * 2f,
                    Quaternion.Euler(0f, 0f, 35f), shine);
            }
        }

        /// <summary>A see-through glossy material (alpha blended).</summary>
        static Material GarageGlass(string materialName, Color colour)
        {
            return MatTransparent("Garage" + materialName, colour, 0.9f);
        }

        // ----- Horns --------------------------------------------------------------------------------

        static GameObject BuildHornToken(CartGarageData.Item item)
        {
            var root = new GameObject(item.PrefabName);
            switch (item.Id)
            {
                case CartGarageData.HornBell: BuildBellToken(root.transform); break;
                case CartGarageData.HornMagic: BuildMagicHornToken(root.transform); break;
                case CartGarageData.HornCow: AnimalToken(root.transform, CowPrefab, 0.9f); break;
                case CartGarageData.HornDog: AnimalToken(root.transform, DogPrefab, 0.75f); break;
                case CartGarageData.HornCat: AnimalToken(root.transform, CatPrefab, 0.6f); break;
                default: AnimalToken(root.transform, ElephantPrefab, 1f); break;
            }
            return root;
        }

        /// <summary>The animal that makes the sound, standing still (the polyperfect model without its scripts and animator).</summary>
        static void AnimalToken(Transform root, PrefabRef animal, float height)
        {
            GameObject model = MakeAnimal(animal, root, height, Vector3.zero, out _);
            if (model == null) return;

            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
        }

        /// <summary>A golden bell with a ring on top and a clapper, standing on its rim.</summary>
        static void BuildBellToken(Transform root)
        {
            Material gold = GMat("BellGold", new Color(1f, 0.8f, 0.2f), 0.75f, 0.35f);
            Material red = GMat("BellRibbon", new Color(0.9f, 0.18f, 0.2f), 0.4f);
            var profile = new[]
            {
                new Vector2(0.34f, 0f), new Vector2(0.35f, 0.03f), new Vector2(0.31f, 0.07f), new Vector2(0.26f, 0.15f), new Vector2(0.21f, 0.26f),
                new Vector2(0.16f, 0.37f), new Vector2(0.1f, 0.46f), new Vector2(0.05f, 0.5f), new Vector2(0f, 0.52f),
            };
            MeshPiece("Bell", root, Lathe("GarageBell", profile, 28), Vector3.zero, gold);
            Prim(PrimitiveType.Sphere, "Loop", root, new Vector3(0f, 0.56f, 0f), new Vector3(0.1f, 0.08f, 0.1f), Quaternion.identity, gold);
            Prim(PrimitiveType.Sphere, "Clapper", root, new Vector3(0f, 0.04f, 0f), Vector3.one * 0.13f, Quaternion.identity, gold);
            Prim(PrimitiveType.Sphere, "BowLeft", root, new Vector3(-0.07f, 0.5f, 0.06f), new Vector3(0.1f, 0.07f, 0.06f), Quaternion.Euler(0f, 0f, 20f), red);
            Prim(PrimitiveType.Sphere, "BowRight", root, new Vector3(0.07f, 0.5f, 0.06f), new Vector3(0.1f, 0.07f, 0.06f), Quaternion.Euler(0f, 0f, -20f), red);
        }

        /// <summary>A unicorn-horn trumpet in pink and white stripes with a golden star on the tip.</summary>
        static void BuildMagicHornToken(Transform root)
        {
            Material pink = GMat("HornPink", new Color(1f, 0.55f, 0.82f), 0.6f);
            Material white = GMat("HornWhite", new Color(0.98f, 0.97f, 1f), 0.6f);
            Material gold = GMat("HornGold", new Color(1f, 0.82f, 0.22f), 0.8f, 0.3f);

            Transform horn = NewGroup("Horn", root);
            horn.localRotation = Quaternion.Euler(0f, 0f, -12f);
            const int stripes = 6;
            const float stripeHeight = 0.1f;
            float radius = 0.17f;
            for (int i = 0; i < stripes; i++)
            {
                float next = radius - 0.026f;
                var profile = new[] { new Vector2(radius, 0f), new Vector2(next, stripeHeight) };
                Transform piece = MeshPiece("Stripe_" + (i + 1), horn, Lathe("GarageHornStripe" + i, profile, 20), new Vector3(0f, 0.04f + i * stripeHeight, 0f),
                    i % 2 == 0 ? pink : white);
                piece.name = "Stripe_" + (i + 1);
                radius = next;
            }
            Prim(PrimitiveType.Cylinder, "Base", horn, new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.02f, 0.38f), Quaternion.identity, gold);
            Transform star = MeshPiece("Star", horn, ExtrudeOutline("GarageHornStar", StarOutline(5, 0.5f, 0.225f), 0.07f), new Vector3(0f, 0.04f + stripes * stripeHeight + 0.06f, 0f), gold);
            star.localScale = Vector3.one * 0.22f;
        }

        // ----- Small mesh pieces --------------------------------------------------------------------

        static Transform MeshPiece(string objectName, Transform parent, Mesh mesh, Vector3 localPosition, Material material)
        {
            var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>The corners of a star with this many points, counter-clockwise from the top.</summary>
        static List<Vector2> StarOutline(int points, float outer, float inner)
        {
            var outline = new List<Vector2>();
            for (int k = 0; k < points * 2; k++)
            {
                float angle = Mathf.PI * 0.5f + k * Mathf.PI / points;
                float radius = k % 2 == 0 ? outer : inner;
                outline.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return outline;
        }

        /// <summary>
        /// A flat shape (a counter-clockwise outline that can be seen from its middle, like a star) pushed out to a thickness: the face
        /// toward +Z, the one toward -Z and the edges. Saved as a mesh asset under Meshes/.
        /// </summary>
        static Mesh ExtrudeOutline(string meshName, IList<Vector2> outline, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = depth * 0.5f;
            int n = outline.Count;

            int front = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, half));
            foreach (Vector2 p in outline) vertices.Add(new Vector3(p.x, p.y, half));
            for (int i = 0; i < n; i++) triangles.AddRange(new[] { front, front + 1 + (i + 1) % n, front + 1 + i });

            int back = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, -half));
            foreach (Vector2 p in outline) vertices.Add(new Vector3(p.x, p.y, -half));
            for (int i = 0; i < n; i++) triangles.AddRange(new[] { back, back + 1 + i, back + 1 + (i + 1) % n });

            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                int s = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, half));
                vertices.Add(new Vector3(b.x, b.y, half));
                vertices.Add(new Vector3(b.x, b.y, -half));
                vertices.Add(new Vector3(a.x, a.y, -half));
                triangles.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            return FinishMesh(meshName, vertices, triangles);
        }

        /// <summary>A half ring (an arch from the right, over the top, to the left) in the XY plane, between two radii, with a thickness.</summary>
        static Mesh ArcBand(string meshName, float innerRadius, float outerRadius, float depth, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = depth * 0.5f;
            int count = segments + 1;
            var frontOuter = new int[count];
            var frontInner = new int[count];
            var backOuter = new int[count];
            var backInner = new int[count];
            var wallOuterFront = new int[count];
            var wallOuterBack = new int[count];
            var wallInnerFront = new int[count];
            var wallInnerBack = new int[count];

            for (int k = 0; k < count; k++)
            {
                float angle = Mathf.PI * k / segments;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                frontOuter[k] = AddVertex(vertices, direction * outerRadius + Vector3.forward * half);
                frontInner[k] = AddVertex(vertices, direction * innerRadius + Vector3.forward * half);
                backOuter[k] = AddVertex(vertices, direction * outerRadius - Vector3.forward * half);
                backInner[k] = AddVertex(vertices, direction * innerRadius - Vector3.forward * half);
                wallOuterFront[k] = AddVertex(vertices, direction * outerRadius + Vector3.forward * half);
                wallOuterBack[k] = AddVertex(vertices, direction * outerRadius - Vector3.forward * half);
                wallInnerFront[k] = AddVertex(vertices, direction * innerRadius + Vector3.forward * half);
                wallInnerBack[k] = AddVertex(vertices, direction * innerRadius - Vector3.forward * half);
            }

            for (int k = 0; k < segments; k++)
            {
                int j = k + 1;
                triangles.AddRange(new[] { frontOuter[k], frontInner[k], frontInner[j], frontOuter[k], frontInner[j], frontOuter[j] });
                triangles.AddRange(new[] { backOuter[k], backInner[j], backInner[k], backOuter[k], backOuter[j], backInner[j] });
                triangles.AddRange(new[] { wallOuterFront[k], wallOuterFront[j], wallOuterBack[j], wallOuterFront[k], wallOuterBack[j], wallOuterBack[k] });
                triangles.AddRange(new[] { wallInnerFront[k], wallInnerBack[j], wallInnerFront[j], wallInnerFront[k], wallInnerBack[k], wallInnerBack[j] });
            }
            return FinishMesh(meshName, vertices, triangles);
        }

        /// <summary>A shape turned about the Y axis: each profile point is (radius, height), listed from the bottom to the top.</summary>
        static Mesh Lathe(string meshName, Vector2[] profile, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int j = 0; j < profile.Length; j++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float angle = Mathf.PI * 2f * s / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[j].x, profile[j].y, Mathf.Sin(angle) * profile[j].x));
                }
            }
            for (int j = 0; j < profile.Length - 1; j++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int topLeft = (j + 1) * segments + next, topRight = (j + 1) * segments + s;
                    int bottomRight = j * segments + s, bottomLeft = j * segments + next;
                    triangles.AddRange(new[] { topLeft, topRight, bottomRight, topLeft, bottomRight, bottomLeft });
                }
            }
            return FinishMesh(meshName, vertices, triangles);
        }

        static int AddVertex(List<Vector3> vertices, Vector3 position)
        {
            vertices.Add(position);
            return vertices.Count - 1;
        }

        static Mesh FinishMesh(string meshName, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, meshName);
        }
    }
}
