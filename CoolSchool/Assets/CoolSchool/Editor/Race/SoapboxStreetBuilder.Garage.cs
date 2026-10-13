using System.Collections.Generic;
using System.IO;
using CoolSchool.Race;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The cart garage on the carts (contract Docs/specs/cart-garage-contract.md): where the seats and the add-ons go on each body
    /// (FitBody), the add-ons themselves (eyes, balloons, propeller, spoiler, flag, stickers), the two number plates, the trails, the
    /// paints, and the wiring of all of it into CartLooks. The reveal prefabs and icons of the garage items are in
    /// SoapboxStreetBuilder.GarageTokens.cs. Agent G2 adds bodies by adding them to <see cref="BodyDefs"/> (SoapboxStreetBuilder.Carts.cs);
    /// everything here then works for them without a change (FitBody probes the real surface), and a body whose seat the probe gets
    /// wrong can be corrected in <see cref="SeatOffsets"/>.
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        const string GarageFolder = RaceFolder + "/Garage";
        /// <summary>The body width the add-ons are designed for; a narrower or wider body scales them (0.7 to 1.3 times).</summary>
        const float ReferenceBodyWidth = 0.9f;
        /// <summary>How many points a body stores: one for each <see cref="CartAnchor"/>.</summary>
        const int AnchorCount = 7;

        /// <summary>Where the things go on one body, in the cart Model's space.</summary>
        internal sealed class BodyFit
        {
            public Vector3 Seat;
            public Vector3 BuddySeat;
            public Vector3 BoundsCenter;
            public Vector3 BoundsSize;
            /// <summary>One point for each <see cref="CartAnchor"/>, in that order.</summary>
            public Vector3[] Anchors;
            public float AddonScale = 1f;
        }

        /// <summary>
        /// Per body id: a correction (in the Model's space, metres) added to the seat the ray probe found, for a body where the probe
        /// picks the wrong surface (agent G2 fills this for the bodies that need it). The buddy seat follows the seat.
        /// </summary>
        internal static readonly Dictionary<string, Vector3> SeatOffsets = new Dictionary<string, Vector3>();

        /// <summary>One body as built: its id, its model and its fit.</summary>
        sealed class BodyBuild
        {
            public string Id;
            public GameObject Root;
            public BodyFit Fit;
        }

        sealed class AddonBuild
        {
            public string Id;
            public GameObject Root;
            public CartAnchor Anchor;
            public AddonPlacement Placement;
            public bool ScaleWithBody = true;
        }

        sealed class TrailBuild
        {
            public string Id;
            public GameObject Root;
        }

        // ----- Fitting a body -----------------------------------------------------------------------

        /// <summary>
        /// Works out the seat points and the add-on anchors of a body that PlaceBody has just fitted (active, nose north, standing on its
        /// y). Rays are cast at the real mesh, so eyes sit on the nose and the plates on the bumpers, whatever the shape. Positions are
        /// relative to the cart root (= the Model's space). The ray probes are temporary colliders that are removed again.
        /// </summary>
        internal static BodyFit FitBody(string id, GameObject body, Bounds b, Vector3 seatWorld, Vector3 rootWorld)
        {
            var fit = new BodyFit
            {
                BoundsCenter = b.center - rootWorld,
                BoundsSize = b.size,
                AddonScale = Mathf.Clamp(b.size.x / ReferenceBodyWidth, 0.7f, 1.3f),
                Anchors = new Vector3[AnchorCount],
            };

            Vector3 seat = seatWorld - rootWorld;
            if (SeatOffsets.TryGetValue(id, out Vector3 offset)) seat += offset;
            fit.Seat = seat;
            fit.BuddySeat = seat + new Vector3(0f, 0.02f, -BuddySeatBehind);

            Vector3 c = b.center;
            using (var probes = new Probes(body))
            {
                Collider[] colliders = probes.Colliders;

                Vector3 front = ProbeEnd(colliders, b, true, new[] { 0.65f, 0.5f, 0.35f }, out Vector3 hitFront)
                    ? hitFront : new Vector3(c.x, b.min.y + b.size.y * 0.65f, b.max.z);
                Vector3 back = ProbeEnd(colliders, b, false, new[] { 0.55f, 0.4f, 0.3f }, out Vector3 hitBack)
                    ? hitBack : new Vector3(c.x, b.min.y + b.size.y * 0.55f, b.min.z);
                Vector3 plateFront = ProbeEnd(colliders, b, true, new[] { 0.3f, 0.2f, 0.4f }, out Vector3 hitPlateFront)
                    ? hitPlateFront : new Vector3(c.x, b.min.y + b.size.y * 0.3f, b.max.z);
                Vector3 plateBack = ProbeEnd(colliders, b, false, new[] { 0.3f, 0.2f, 0.4f }, out Vector3 hitPlateBack)
                    ? hitPlateBack : new Vector3(c.x, b.min.y + b.size.y * 0.3f, b.min.z);

                // The propeller stands on the right behind the seat (the buddy sits in the middle), the spoiler on the tail's top edge.
                float propellerX = c.x + b.size.x * 0.2f;
                float propellerZ = b.min.z + 0.3f;
                Vector3 top = new Vector3(propellerX, ProbeTop(colliders, b, propellerX, propellerZ, out float topY) ? topY : seatWorld.y + 0.1f, propellerZ);
                float spoilerZ = b.min.z + 0.12f;
                Vector3 spoiler = new Vector3(c.x, ProbeTop(colliders, b, c.x, spoilerZ, out float spoilerY) ? spoilerY : b.min.y + b.size.y * 0.75f, spoilerZ);
                Vector3 flag = new Vector3(c.x + b.size.x * 0.25f, b.min.y + b.size.y * 0.7f, b.min.z + 0.12f);

                fit.Anchors[(int)CartAnchor.Front] = front - rootWorld;
                fit.Anchors[(int)CartAnchor.Back] = back - rootWorld;
                fit.Anchors[(int)CartAnchor.Top] = top - rootWorld;
                fit.Anchors[(int)CartAnchor.Spoiler] = spoiler - rootWorld;
                fit.Anchors[(int)CartAnchor.FlagFoot] = flag - rootWorld;
                fit.Anchors[(int)CartAnchor.PlateFront] = plateFront - rootWorld;
                fit.Anchors[(int)CartAnchor.PlateBack] = plateBack - rootWorld;
            }

            Debug.Log($"CoolSchool cart garage: {id} seat {fit.Seat.ToString("0.00")}, nose point {fit.Anchors[(int)CartAnchor.Front].ToString("0.00")}, "
                + $"tail point {fit.Anchors[(int)CartAnchor.Back].ToString("0.00")}, top {fit.Anchors[(int)CartAnchor.Top].ToString("0.00")}, add-on scale {fit.AddonScale:0.00}.");
            return fit;
        }

        /// <summary>
        /// The first surface a ray along the body meets, coming from the nose (or from the tail) at one of the given height fractions.
        /// A hit that is not in the end half of the body (a seat back or a roof seen over a low nose) does not count.
        /// </summary>
        static bool ProbeEnd(Collider[] colliders, Bounds b, bool nose, float[] heights, out Vector3 point)
        {
            foreach (float fraction in heights)
            {
                float y = b.min.y + b.size.y * fraction;
                var origin = new Vector3(b.center.x, y, nose ? b.max.z + 0.5f : b.min.z - 0.5f);
                var ray = new Ray(origin, nose ? Vector3.back : Vector3.forward);
                if (!Hit(colliders, ray, b.size.z + 1f, out RaycastHit hit)) continue;

                bool inEnd = nose ? hit.point.z > b.center.z + b.size.z * 0.15f : hit.point.z < b.center.z - b.size.z * 0.15f;
                if (!inEnd) continue;

                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>The height of the top surface at this x and z (a ray straight down from above the body).</summary>
        static bool ProbeTop(Collider[] colliders, Bounds b, float x, float z, out float y)
        {
            var ray = new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down);
            if (Hit(colliders, ray, b.size.y + 2f, out RaycastHit hit))
            {
                y = hit.point.y;
                return true;
            }
            y = 0f;
            return false;
        }

        // ----- Materials ----------------------------------------------------------------------------

        /// <summary>A lit colour saved as Materials/Race/Garage&lt;name&gt;. The prefix keeps the name unique to the garage.</summary>
        static Material GMat(string materialName, Color colour, float smoothness = 0.3f, float metallic = 0f, bool doubleSided = false)
        {
            Material material = Mat("Garage" + materialName, colour, smoothness, metallic);
            if (material != null && doubleSided && material.HasProperty("_Cull") && material.GetFloat("_Cull") != 0f)
            {
                material.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // ----- The add-ons --------------------------------------------------------------------------

        /// <summary>
        /// All six add-ons of a cart, built once and switched off: the flag, the stickers, the googly eyes, the balloons, the propeller
        /// and the spoiler. Each root's origin is its anchor point, facing forward; CartLooks puts them on the shown body.
        /// </summary>
        static List<AddonBuild> BuildAddons(Transform model, Bounds referenceBounds, Vector3 rootWorld)
        {
            var list = new List<AddonBuild>();

            GameObject flag = BuildCartFlag(model, rootWorld);
            list.Add(new AddonBuild { Id = CartLooks.FlagId, Root = flag, Anchor = CartAnchor.FlagFoot, ScaleWithBody = false });

            GameObject stickers = BuildStickers(model, referenceBounds, rootWorld);
            if (stickers != null)
                list.Add(new AddonBuild { Id = CartLooks.StickersId, Root = stickers, Placement = AddonPlacement.SideStickers, ScaleWithBody = false });

            list.Add(new AddonBuild { Id = CartGarageData.Eyes, Root = BuildEyes(model).gameObject, Anchor = CartAnchor.Front });
            list.Add(new AddonBuild { Id = CartGarageData.Balloons, Root = BuildBalloons(model).gameObject, Anchor = CartAnchor.Back });
            list.Add(new AddonBuild { Id = CartGarageData.Propeller, Root = BuildPropeller(model).gameObject, Anchor = CartAnchor.Top });
            list.Add(new AddonBuild { Id = CartGarageData.Spoiler, Root = BuildSpoiler(model).gameObject, Anchor = CartAnchor.Spoiler });

            foreach (AddonBuild addon in list)
                if (addon.Root != null) addon.Root.SetActive(false);
            return list;
        }

        /// <summary>A thin pole with the toy flag on top, ~0.9 m high, standing on the group's origin. The group is switched on; the caller hides it.</summary>
        static GameObject BuildCartFlag(Transform model, Vector3 foot)
        {
            Transform group = NewGroup("Flag", model);
            Material pole = Mat("RaceFlagPole", new Color(0.9f, 0.9f, 0.88f), 0.3f);
            Prim(PrimitiveType.Cylinder, "Pole", group, new Vector3(0f, 0.45f, 0f), new Vector3(0.025f, 0.45f, 0.025f), Quaternion.identity, pole);
            // The cloth is placed in world space next to where the group stands now (the model's origin), then the group moves as a whole.
            GameObject cloth = Place(FlagModelPrefab, group, 0f, Fit.Height, 0.4f, foot + Vector3.up * 0.7f, foot.y + 0.5f, false, false);
            if (cloth == null)
            {
                Prim(PrimitiveType.Cube, "Cloth", group, new Vector3(0.14f, 0.75f, 0f), new Vector3(0.28f, 0.18f, 0.02f),
                    Quaternion.identity, Mat("RaceFlagCloth", new Color(0.95f, 0.25f, 0.25f), 0.2f));
            }
            return group.gameObject;
        }

        /// <summary>Two big googly eyes side by side, white balls with black pupils that bounce about. Faces forward (+Z).</summary>
        static Transform BuildEyes(Transform parent)
        {
            Transform root = NewGroup("Eyes", parent);
            Material white = GMat("EyeWhite", new Color(0.98f, 0.98f, 0.96f), 0.75f);
            Material black = GMat("EyePupil", new Color(0.04f, 0.04f, 0.06f), 0.9f);

            var pupils = new List<Object>();
            foreach (float side in new[] { -1f, 1f })
            {
                Transform eye = Prim(PrimitiveType.Sphere, side < 0f ? "EyeLeft" : "EyeRight", root, new Vector3(side * 0.15f, 0.04f, 0.06f),
                    Vector3.one * 0.23f, Quaternion.identity, white);
                // The pupil is a child of the eye (the eye is scaled 0.23, so these are eye-sized units).
                Transform pupil = Prim(PrimitiveType.Sphere, "Pupil", eye, new Vector3(0f, -0.04f, 0.34f), Vector3.one * 0.5f, Quaternion.identity, black);
                pupils.Add(pupil);
            }

            var motion = root.gameObject.AddComponent<CartAddonMotion>();
            SetObjectArray(motion, "pupils", pupils);
            SetFloat(motion, "pupilRange", 0.12f);
            return root;
        }

        /// <summary>Three balloons on strings that fan out from the tie point above the tail and sway.</summary>
        static Transform BuildBalloons(Transform parent)
        {
            Transform root = NewGroup("Balloons", parent);
            Material cord = GMat("BalloonString", new Color(0.92f, 0.92f, 0.9f), 0.2f);
            Color[] colours = { new Color(0.95f, 0.2f, 0.22f), new Color(1f, 0.84f, 0.18f), new Color(0.24f, 0.5f, 1f) };
            float[] lengths = { 0.95f, 0.8f, 1.05f };
            float[] rolls = { 32f, 0f, -32f };
            float[] pitches = { -12f, -22f, -12f };

            var swayers = new List<Object>();
            for (int i = 0; i < 3; i++)
            {
                Transform group = NewGroup("Balloon_" + (i + 1), root);
                group.localRotation = Quaternion.Euler(pitches[i], 0f, rolls[i]);
                swayers.Add(group);

                float length = lengths[i];
                Material skin = GMat("Balloon" + i, colours[i], 0.85f);
                Prim(PrimitiveType.Cylinder, "String", group, new Vector3(0f, length * 0.5f, 0f), new Vector3(0.008f, length * 0.5f, 0.008f), Quaternion.identity, cord);
                Prim(PrimitiveType.Sphere, "Knot", group, new Vector3(0f, length - 0.01f, 0f), Vector3.one * 0.045f, Quaternion.identity, skin);
                Prim(PrimitiveType.Sphere, "Balloon", group, new Vector3(0f, length + 0.17f, 0f), new Vector3(0.3f, 0.36f, 0.3f), Quaternion.identity, skin);
            }

            var motion = root.gameObject.AddComponent<CartAddonMotion>();
            SetObjectArray(motion, "swayers", swayers);
            return root;
        }

        /// <summary>A pole with two crossed blades on top; the blades spin faster the faster the cart goes.</summary>
        static Transform BuildPropeller(Transform parent)
        {
            Transform root = NewGroup("Propeller", parent);
            Material grey = GMat("PropPole", new Color(0.75f, 0.77f, 0.82f), 0.5f, 0.3f);
            Material red = GMat("PropBladeRed", new Color(0.95f, 0.22f, 0.2f), 0.4f);
            Material yellow = GMat("PropBladeYellow", new Color(1f, 0.85f, 0.15f), 0.4f);

            const float poleHeight = 0.55f;
            Prim(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, poleHeight * 0.5f, 0f), new Vector3(0.04f, poleHeight * 0.5f, 0.04f), Quaternion.identity, grey);
            Prim(PrimitiveType.Sphere, "Hub", root, new Vector3(0f, poleHeight, 0f), Vector3.one * 0.11f, Quaternion.identity, yellow);

            Transform spin = NewGroup("Spin", root);
            spin.localPosition = new Vector3(0f, poleHeight + 0.02f, 0f);
            Prim(PrimitiveType.Cube, "BladeA", spin, Vector3.zero, new Vector3(0.64f, 0.015f, 0.1f), Quaternion.identity, red);
            Prim(PrimitiveType.Cube, "BladeB", spin, Vector3.zero, new Vector3(0.64f, 0.015f, 0.1f), Quaternion.Euler(0f, 90f, 0f), yellow);

            var motion = root.gameObject.AddComponent<CartAddonMotion>();
            SetObject(motion, "spinner", spin);
            return root;
        }

        /// <summary>A racing wing on two struts, standing on the tail's top edge.</summary>
        static Transform BuildSpoiler(Transform parent)
        {
            Transform root = NewGroup("Spoiler", parent);
            Material red = GMat("SpoilerRed", new Color(0.92f, 0.16f, 0.18f), 0.5f);
            Material white = GMat("SpoilerWhite", new Color(0.97f, 0.97f, 0.95f), 0.4f);
            Material dark = GMat("SpoilerStrut", new Color(0.15f, 0.16f, 0.2f), 0.4f);

            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Strut", root, new Vector3(side * 0.26f, 0.08f, 0f), new Vector3(0.04f, 0.16f, 0.04f), Quaternion.identity, dark);
                Prim(PrimitiveType.Cube, "EndPlate", root, new Vector3(side * 0.4f, 0.2f, -0.04f), new Vector3(0.025f, 0.11f, 0.24f), Quaternion.identity, white);
            }
            Prim(PrimitiveType.Cube, "Wing", root, new Vector3(0f, 0.18f, -0.04f), new Vector3(0.8f, 0.035f, 0.2f), Quaternion.identity, red);
            Prim(PrimitiveType.Cube, "Stripe", root, new Vector3(0f, 0.2f, -0.04f), new Vector3(0.8f, 0.008f, 0.06f), Quaternion.identity, white);
            return root;
        }

        // ----- The number plates --------------------------------------------------------------------

        /// <summary>
        /// A small white plate with a black digit, one on the nose and one on the tail (the back one turned round). The digit is a
        /// world-space UI Text (the project uses UI Text everywhere and not TextMeshPro); CartLooks changes it.
        /// </summary>
        static Transform BuildPlate(Transform model, string plateName, bool back, Font font, out Text text)
        {
            Transform root = NewGroup(plateName, model);
            root.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            Material frame = GMat("PlateFrame", new Color(0.08f, 0.08f, 0.1f), 0.2f);
            Material face = GMat("PlateFace", new Color(0.97f, 0.97f, 0.94f), 0.25f);
            Prim(PrimitiveType.Cube, "Frame", root, Vector3.zero, new Vector3(0.32f, 0.19f, 0.014f), Quaternion.identity, frame);
            Prim(PrimitiveType.Cube, "Face", root, new Vector3(0f, 0f, 0.003f), new Vector3(0.29f, 0.16f, 0.02f), Quaternion.identity, face);

            // A world-space canvas shows its content to a camera on its -Z side, so it is turned to face outward (+Z of the plate).
            var canvasObject = new GameObject("Digits", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, 0.0145f);
            canvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.001f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(280f, 150f);

            RectTransform textRect = UiRect("Text", canvasObject.transform);
            UiStretch(textRect);
            text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 130;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = CartChoice.DefaultNumber.ToString();
            // The canvas is not a UI object: it lives in the world, on the default layer like the cart.
            SetLayerRecursive(canvasObject, 0);
            return root;
        }

        // ----- The trails ---------------------------------------------------------------------------

        /// <summary>
        /// The four trails, built once under the cart and switched off: a rainbow ribbon, stars, bubbles and glitter. They hang behind
        /// the tail and CartLooks lets them emit only while the cart moves. Cheap: one ribbon or one small particle system each, unlit.
        /// </summary>
        static List<TrailBuild> BuildTrails(Transform cartRoot, float tailLocalZ)
        {
            EnsureFolder(RaceFolder, "Garage");
            var list = new List<TrailBuild>();
            Transform group = NewGroup("Trails", cartRoot);
            group.localPosition = new Vector3(0f, 0.28f, tailLocalZ - 0.05f);

            list.Add(new TrailBuild { Id = CartGarageData.TrailRainbow, Root = BuildRainbowTrail(group) });
            list.Add(new TrailBuild { Id = CartGarageData.TrailStars, Root = BuildStarTrail(group) });
            list.Add(new TrailBuild { Id = CartGarageData.TrailBubbles, Root = BuildBubbleTrail(group) });
            list.Add(new TrailBuild { Id = CartGarageData.TrailSparkles, Root = BuildSparkleTrail(group) });

            foreach (TrailBuild trail in list) trail.Root.SetActive(false);
            return list;
        }

        static GameObject BuildRainbowTrail(Transform parent)
        {
            var root = new GameObject("Trail_Rainbow");
            root.transform.SetParent(parent, false);

            var ribbon = root.AddComponent<TrailRenderer>();
            ribbon.time = 1.1f;
            ribbon.minVertexDistance = 0.1f;
            ribbon.widthMultiplier = 0.5f;
            ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
            ribbon.numCornerVertices = 3;
            ribbon.numCapVertices = 3;
            ribbon.alignment = LineAlignment.View;
            ribbon.textureMode = LineTextureMode.Stretch;
            ribbon.shadowCastingMode = ShadowCastingMode.Off;
            ribbon.receiveShadows = false;
            ribbon.sharedMaterial = ParticleMaterial("GarageTrailRibbon", null);

            // Red next to the cart, then the colours of the rainbow, and the end fades away.
            Color[] colours =
            {
                new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.2f), new Color(0.3f, 0.8f, 0.3f),
                new Color(0.25f, 0.55f, 1f), new Color(0.4f, 0.3f, 0.85f), new Color(0.7f, 0.35f, 0.9f),
            };
            var colourKeys = new GradientColorKey[colours.Length];
            for (int i = 0; i < colours.Length; i++) colourKeys[i] = new GradientColorKey(colours[i], i / (float)(colours.Length - 1));
            var gradient = new Gradient();
            gradient.SetKeys(colourKeys, new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ribbon.colorGradient = gradient;
            return root;
        }

        static GameObject BuildStarTrail(Transform parent)
        {
            Texture2D star = ParticleTexture("GarageTrailStarTex", StarAlpha);
            Gradient colours = MakeGradient(new Color(1f, 0.9f, 0.25f), new Color(1f, 0.6f, 0.85f), new Color(0.6f, 0.9f, 1f), new Color(1f, 1f, 1f));
            return TrailParticles(parent, "Trail_Stars", ParticleMaterial("GarageTrailStar", star), 14f, new Vector2(0.8f, 1.1f), new Vector2(0.22f, 0.36f),
                colours, 0.7f, -0.1f, 0.12f, true);
        }

        static GameObject BuildBubbleTrail(Transform parent)
        {
            Texture2D bubble = ParticleTexture("GarageTrailBubbleTex", BubbleAlpha);
            Gradient colours = MakeGradient(new Color(0.8f, 0.93f, 1f, 0.85f), new Color(0.9f, 1f, 1f, 0.85f), new Color(1f, 0.92f, 1f, 0.85f));
            return TrailParticles(parent, "Trail_Bubbles", ParticleMaterial("GarageTrailBubble", bubble), 8f, new Vector2(1.4f, 2.0f), new Vector2(0.16f, 0.4f),
                colours, 0.35f, -0.06f, 0.18f, false);
        }

        static GameObject BuildSparkleTrail(Transform parent)
        {
            Texture2D sparkle = ParticleTexture("GarageTrailSparkleTex", SparkleAlpha);
            Gradient colours = MakeGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 1f, 1f), new Color(1f, 0.65f, 0.9f), new Color(0.5f, 0.95f, 1f));
            return TrailParticles(parent, "Trail_Sparkles", ParticleMaterial("GarageTrailSparkle", sparkle), 26f, new Vector2(0.45f, 0.8f), new Vector2(0.1f, 0.22f),
                colours, 0.4f, 0f, 0.2f, true);
        }

        /// <summary>A looping particle system that stays where each particle was born (world space) and shrinks and fades away.</summary>
        static GameObject TrailParticles(Transform parent, string objectName, Material material, float rate, Vector2 lifetime, Vector2 size, Gradient startColours,
            float speed, float gravity, float radius, bool spin)
        {
            var root = new GameObject(objectName, typeof(ParticleSystem));
            root.transform.SetParent(parent, false);
            var system = root.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(startColours) { mode = ParticleSystemGradientMode.RandomColor };

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            // Pop up quickly, then shrink away.
            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            ParticleSystem.ColorOverLifetimeModule colourOverLife = system.colorOverLifetime;
            colourOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colourOverLife.color = new ParticleSystem.MinMaxGradient(fade);

            if (spin)
            {
                ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            }
            return root;
        }

        static Gradient MakeGradient(params Color[] colours)
        {
            var keys = new GradientColorKey[colours.Length];
            var alphas = new GradientAlphaKey[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                float t = colours.Length == 1 ? 0f : i / (float)(colours.Length - 1);
                keys[i] = new GradientColorKey(colours[i], t);
                alphas[i] = new GradientAlphaKey(colours[i].a, t);
            }
            var gradient = new Gradient();
            gradient.SetKeys(keys, alphas);
            return gradient;
        }

        // Particle textures: white with a shape in the alpha, drawn here (no art needed). u and v run from -1 to 1 across the picture.

        static float StarAlpha(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            float edge = 0.58f + 0.3f * Mathf.Cos(5f * (angle - Mathf.PI * 0.5f));
            return Mathf.Clamp01((edge - radius) / 0.08f + 0.5f);
        }

        static float SparkleAlpha(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            // Four thin spikes and a soft glow in the middle.
            float spike = 0.12f + 0.88f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * angle)), 6f);
            float shape = Mathf.Clamp01((spike - radius) / 0.1f + 0.5f);
            float glow = Mathf.Clamp01(1f - radius / 0.45f) * 0.7f;
            return Mathf.Max(shape, glow);
        }

        static float BubbleAlpha(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            if (radius > 1f) return 0f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.9f) / 0.1f) * 0.75f;
            float body = radius < 0.9f ? 0.18f : 0f;
            float du = (u + 0.38f) / 0.22f, dv = (v - 0.4f) / 0.12f;
            float shine = Mathf.Clamp01(1f - (du * du + dv * dv)) * 0.9f;
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(ring, body), shine));
        }

        static Texture2D ParticleTexture(string textureName, System.Func<float, float, float> alphaAt)
        {
            EnsureFolder(RaceFolder, "Garage");
            const int size = 64;
            string path = GarageFolder + "/" + textureName + ".png";

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAt((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f)));
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>An unlit, alpha-blended particle material (URP Particles/Unlit) with this picture; vertex colours tint it.</summary>
        static Material ParticleMaterial(string materialName, Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("CoolSchool cart garage: URP Particles/Unlit shader not found; the trails will be invisible.");
                return null;
            }

            var material = new Material(shader) { name = materialName };
            material.SetColor("_BaseColor", Color.white);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Cull", 0f);
            BattleEffectsBuilder.SetAlphaBlended(material);

            string path = GarageFolder + "/" + materialName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ----- The paints ---------------------------------------------------------------------------

        /// <summary>
        /// The paints of a cart: gold (the whole-body material) and one painted copy of every body material for each paint in the
        /// garage table. The materials are collected from every body, so a new body is painted without a change here. A body whose
        /// recipe says PaintWhites (the police car) gets copies that also tint its white panels; the swaps are keyed by the source
        /// material, so that only works for materials no other body uses (a shared one stays with the normal rule and is logged).
        /// </summary>
        static List<CartLooks.PaintEntry> BuildPaints(IEnumerable<BodyBuild> bodies, Material gold)
        {
            var sources = new List<Material>();
            var whiteSources = new HashSet<Material>();
            var otherSources = new HashSet<Material>();
            foreach (BodyBuild body in bodies)
            {
                if (body.Root == null) continue;
                bool whites = BodyStyles.TryGetValue(body.Id, out BodyStyle style) && style.PaintWhites;
                foreach (Renderer renderer in body.Root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || IsGlass(material)) continue;

                        if (!sources.Contains(material)) sources.Add(material);
                        (whites ? whiteSources : otherSources).Add(material);
                    }
                }
            }
            foreach (Material shared in whiteSources)
                if (otherSources.Contains(shared))
                    Debug.LogWarning($"CoolSchool cart paint: the material '{shared.name}' is used by a PaintWhites body and by another body, so its white panels are not tinted (give the body its own material).");

            var paints = new List<CartLooks.PaintEntry>();
            paints.Add(new CartLooks.PaintEntry { id = CartLooks.GoldId, whole = gold, swaps = new CartLooks.MaterialSwap[0] });
            foreach (CartGarageData.Item item in CartGarageData.OfPart(CartPart.Paint))
            {
                var swaps = new List<CartLooks.MaterialSwap>();
                foreach (Material source in sources)
                {
                    bool paintWhites = whiteSources.Contains(source) && !otherSources.Contains(source);
                    swaps.Add(new CartLooks.MaterialSwap { from = source, to = CartPaintMaker.Painted(item.Id, item.Colour, source, paintWhites) });
                }
                paints.Add(new CartLooks.PaintEntry { id = item.Id, swaps = swaps.ToArray() });
            }
            return paints;
        }

        // ----- Wiring CartLooks ---------------------------------------------------------------------

        /// <summary>Fills a CartLooks (and the parts it moves) from what BuildCart made.</summary>
        static void WireLooks(CartLooks looks, Transform seat, Transform buddySeat, List<BodyBuild> bodies, List<AddonBuild> addons,
            List<CartLooks.PaintEntry> paints, List<TrailBuild> trails, Transform plateFront, Transform plateBack, Text plateFrontText, Text plateBackText)
        {
            var serialized = new SerializedObject(looks);

            SerializedProperty bodyList = serialized.FindProperty("bodies");
            bodyList.arraySize = bodies.Count;
            for (int i = 0; i < bodies.Count; i++)
            {
                SerializedProperty entry = bodyList.GetArrayElementAtIndex(i);
                BodyFit fit = bodies[i].Fit;
                entry.FindPropertyRelative("id").stringValue = bodies[i].Id;
                entry.FindPropertyRelative("root").objectReferenceValue = bodies[i].Root;
                entry.FindPropertyRelative("seat").vector3Value = fit.Seat;
                entry.FindPropertyRelative("buddySeat").vector3Value = fit.BuddySeat;
                entry.FindPropertyRelative("boundsCenter").vector3Value = fit.BoundsCenter;
                entry.FindPropertyRelative("boundsSize").vector3Value = fit.BoundsSize;
                entry.FindPropertyRelative("addonScale").floatValue = fit.AddonScale;
                SerializedProperty anchors = entry.FindPropertyRelative("anchors");
                anchors.arraySize = fit.Anchors.Length;
                for (int a = 0; a < fit.Anchors.Length; a++) anchors.GetArrayElementAtIndex(a).vector3Value = fit.Anchors[a];
            }

            SerializedProperty addonList = serialized.FindProperty("addons");
            addonList.arraySize = addons.Count;
            for (int i = 0; i < addons.Count; i++)
            {
                SerializedProperty entry = addonList.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = addons[i].Id;
                entry.FindPropertyRelative("root").objectReferenceValue = addons[i].Root;
                entry.FindPropertyRelative("anchor").enumValueIndex = (int)addons[i].Anchor;
                entry.FindPropertyRelative("placement").enumValueIndex = (int)addons[i].Placement;
                entry.FindPropertyRelative("scaleWithBody").boolValue = addons[i].ScaleWithBody;
            }

            SerializedProperty paintList = serialized.FindProperty("paints");
            paintList.arraySize = paints.Count;
            for (int i = 0; i < paints.Count; i++)
            {
                SerializedProperty entry = paintList.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = paints[i].id;
                entry.FindPropertyRelative("whole").objectReferenceValue = paints[i].whole;
                SerializedProperty swaps = entry.FindPropertyRelative("swaps");
                swaps.arraySize = paints[i].swaps.Length;
                for (int s = 0; s < paints[i].swaps.Length; s++)
                {
                    swaps.GetArrayElementAtIndex(s).FindPropertyRelative("from").objectReferenceValue = paints[i].swaps[s].from;
                    swaps.GetArrayElementAtIndex(s).FindPropertyRelative("to").objectReferenceValue = paints[i].swaps[s].to;
                }
            }

            SerializedProperty trailList = serialized.FindProperty("trails");
            trailList.arraySize = trails != null ? trails.Count : 0;
            for (int i = 0; trails != null && i < trails.Count; i++)
            {
                SerializedProperty entry = trailList.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = trails[i].Id;
                entry.FindPropertyRelative("root").objectReferenceValue = trails[i].Root;
            }

            serialized.FindProperty("seat").objectReferenceValue = seat;
            serialized.FindProperty("buddySeat").objectReferenceValue = buddySeat;
            serialized.FindProperty("plateFront").objectReferenceValue = plateFront;
            serialized.FindProperty("plateBack").objectReferenceValue = plateBack;
            serialized.FindProperty("plateFrontText").objectReferenceValue = plateFrontText;
            serialized.FindProperty("plateBackText").objectReferenceValue = plateBackText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
